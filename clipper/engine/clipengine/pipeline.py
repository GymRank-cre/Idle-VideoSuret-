"""Orchestration : source -> transcription -> candidats -> sélection -> recadrage -> rendu."""
from __future__ import annotations

import json
import re
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable

import numpy as np

from . import face, llm
from .audio import AudioProfile, compute_profile, extract_wav
from .candidates import build_sentences, make_candidates
from .captions import build_ass, default_title
from .config import Config
from .media import MediaInfo, download, probe
from .models import Candidate
from .reframe import plan_camera
from .render import render_clip
from .scoring import score_candidates
from .select import select
from .transcribe import Transcript, load_transcript, transcribe


@dataclass
class ClipResult:
    index: int
    candidate: Candidate
    video: Path | None = None
    ass: Path | None = None
    layouts: list[str] = field(default_factory=list)
    face_backend: str = ""


def _slug(s: str, n: int = 40) -> str:
    s = re.sub(r"[^\w]+", "-", s.lower(), flags=re.UNICODE).strip("-")
    return s[:n].rstrip("-") or "clip"


def analyze(source: Path, work: Path, cfg: Config, transcript_path: str | Path | None = None,
            log: Callable[[str], None] = print) -> tuple[Transcript, AudioProfile, list[Candidate]]:
    """Étapes sans rendu : utile pour prévisualiser le classement des moments."""
    work.mkdir(parents=True, exist_ok=True)

    prof_path = work / "audio_profile.npy"
    if prof_path.exists():
        prof = AudioProfile.load(prof_path)
    else:
        log("• profil audio…")
        prof = compute_profile(source)
        prof.save(prof_path)

    tr_path = work / "transcript.json"
    if transcript_path:
        tr = load_transcript(transcript_path)
    elif tr_path.exists():
        tr = load_transcript(tr_path)
    else:
        log(f"• transcription ({cfg.whisper_model})…")
        wav = extract_wav(source, work / "audio.wav")
        tr = transcribe(wav, cfg.whisper_model, cfg.language, cfg.device, cfg.compute_type)
        wav.unlink(missing_ok=True)
    tr_path.write_text(json.dumps(tr.to_json(), ensure_ascii=False), encoding="utf-8")
    log(f"  {len(tr.words)} mots, langue {tr.language}")

    sents = build_sentences(tr.words)
    cands = make_candidates(sents, prof, cfg)
    log(f"• {len(sents)} phrases, {len(cands)} candidats")
    score_candidates(cands, sents, prof, cfg)

    if cfg.llm != "none" and llm.available(cfg):
        log(f"• réévaluation par {cfg.llm_model}…")
        llm.rerank(cands, cfg)
    elif cfg.llm == "claude":
        log("  ANTHROPIC_API_KEY absente : score heuristique seul")
    return tr, prof, cands


def run(source: str | Path, out_dir: str | Path, cfg: Config | None = None,
        transcript_path: str | Path | None = None, render: bool = True,
        log: Callable[[str], None] = print) -> list[ClipResult]:
    cfg = cfg or Config()
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    work = out_dir / ".work"
    t_start = time.time()

    src = str(source)
    if re.match(r"https?://", src):
        log("• téléchargement…")
        source = download(src, work)
    source = Path(source)
    info: MediaInfo = probe(source)
    log(f"• source : {info.width}x{info.height} @ {info.fps:.0f} fps, {info.duration / 60:.1f} min")

    tr, prof, cands = analyze(source, work, cfg, transcript_path, log)
    chosen = select(cands, cfg)
    log(f"• {len(chosen)} clips retenus")

    detector = face.FaceDetector() if render else None
    results: list[ClipResult] = []
    for i, c in enumerate(chosen, 1):
        if not c.title:
            c.title = default_title(c.text)
        if not c.hook:
            c.hook = c.title
        res = ClipResult(i, c)
        name = f"{i:02d}-{_slug(c.title)}"
        if render:
            words = [w for w in tr.words if w.end > c.start and w.start < c.end]
            log(f"• clip {i}/{len(chosen)} [{c.start:.1f}–{c.end:.1f}s] score {c.score:.0f} — {c.title}")
            sample_t = c.start + np.arange(int(c.duration * cfg.sample_fps)) / cfg.sample_fps
            sp = prof.at(sample_t) > prof.speech_threshold
            tl = face.analyze(source, c.start, c.duration, info.width, info.height, cfg.sample_fps,
                              cfg.detect_width, cfg.scene_threshold, sp, detector)
            plan = plan_camera(tl, info.width, info.height, c.duration, cfg)
            res.layouts = [s.layout for s in plan.shots]
            res.face_backend = tl.backend
            ass_path = out_dir / f"{name}.ass"
            ass_path.write_text(build_ass(words, c.start, c.end, cfg, c.hook if cfg.title_overlay else ""),
                                encoding="utf-8")
            res.ass = ass_path
            res.video = render_clip(source, c.start, c.end, plan, ass_path, out_dir / f"{name}.mp4", cfg,
                                    info.has_audio)
        meta = c.to_dict() | {"index": i, "file": f"{name}.mp4" if render else None, "layouts": res.layouts}
        (out_dir / f"{name}.json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
        results.append(res)

    report = {
        "source": str(source), "duration": info.duration, "language": tr.language,
        "elapsed_s": round(time.time() - t_start, 1),
        "clips": [r.candidate.to_dict() | {"index": r.index, "layouts": r.layouts} for r in results],
    }
    (out_dir / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    log(f"✓ terminé en {time.time() - t_start:.0f}s → {out_dir}")
    return results
