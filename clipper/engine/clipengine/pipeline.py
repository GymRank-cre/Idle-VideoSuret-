"""Étapes du moteur (transcription, recherche des moments, rendu d'un moment) et exécution complète en ligne de commande."""
from __future__ import annotations

import json
import re
import subprocess
import time
import unicodedata
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Callable

import numpy as np

from . import face, kit, llm
from .audio import AudioProfile, compute_profile, extract_wav
from .candidates import build_sentences, make_candidates, split_full
from .captions import build_ass, default_title
from .config import Config
from .errors import UserError
from .media import MediaInfo, check_info, fetch, probe
from .models import Candidate, Word
from .reframe import face_zone, plan_camera
from .render import render_clip
from .scoring import breakdown, label_for, score_candidates
from .select import propose
from .styles import Style, get_style
from .transcribe import Transcript, load_transcript, transcribe

Log = Callable[[str], None]
MAX_AUTO_FULL = 40            # en mode « tout le long », nombre de parties fabriquées d'office


@dataclass
class Rendered:
    video: Path
    ass: Path
    poster: Path | None
    layouts: list[str] = field(default_factory=list)
    face_backend: str = ""
    hook_shown: bool | None = None          # None : pas d'accroche demandée ; False : pas de place sans couvrir un visage


@dataclass
class ClipResult:
    index: int
    candidate: Candidate
    video: Path | None = None
    ass: Path | None = None
    layouts: list[str] = field(default_factory=list)
    face_backend: str = ""


def slug(s: str, n: int = 40) -> str:
    """Nom de fichier ASCII (sans accents) : sûr pour les URL, les disques et les téléchargements."""
    s = unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode()
    s = re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")
    return s[:n].rstrip("-") or "clip"


_slug = slug


# ----------------------------------------------------------------------------- analyse

def transcribe_source(source: Path, work: Path, cfg: Config, transcript_path: str | Path | None = None,
                      log: Log = print) -> tuple[Transcript, AudioProfile]:
    """Profil audio + transcription mot à mot (mis en cache dans `work`)."""
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
    return tr, prof


def clip_transcript(tr: Transcript, duration: float) -> Transcript:
    """Retire les mots au-delà de la fin réelle de la vidéo (queues d'hallucination de Whisper, transcription importée)."""
    words = [Word(w.text, w.start, min(w.end, duration), w.prob) for w in tr.words if w.start < duration - 0.05]
    return Transcript(tr.language, words)


def clip_words(words: list[Word], start: float, end: float, min_presence: float = 0.12) -> list[Word]:
    """Mots affichables dans [start, end] : un mot entamé par le bord doit y être resté assez longtemps pour être lu.

    Sans cela, le mot d'après (souvent mal daté par Whisper) clignote une image à la fin du clip.
    """
    return [w for w in words
            if (w.start >= start and w.end <= end) or min(w.end, end) - max(w.start, start) >= min_presence]


def check_speech(tr: Transcript, info: MediaInfo) -> None:
    """Le moteur travaille à partir de ce qui est dit : sans voix audible, on le dit plutôt que de rendre du vide."""
    spoken = sum(w.end - w.start for w in tr.words)
    if len(tr.words) < 12 or spoken < 0.04 * info.duration:
        raise UserError("Aucune parole détectée : il faut une voix bien audible (pas de musique seule ni de "
                        "gameplay sans commentaire).")


def find_moments(tr: Transcript, prof: AudioProfile, cfg: Config, log: Log = print, llm_call=None
                 ) -> tuple[list[Candidate], int, dict]:
    """Moments classés (meilleur d'abord) et nombre à fabriquer d'office ; le reste est en réserve.

    `llm_call(cfg, prompt) -> str` remplace l'appel HTTP à l'API (essais, autre fournisseur) ; sans lui, clé requise."""
    sents = build_sentences(tr.words)
    if cfg.mode == "full":
        parts = split_full(sents, prof, cfg)
        score_candidates(parts, sents, prof, cfg)        # notes à titre indicatif : l'ordre est celui de la vidéo
        log(f"• {len(sents)} phrases, {len(parts)} parties")
        return parts, min(len(parts), MAX_AUTO_FULL), {"sentences": len(sents), "candidates": len(parts)}

    cands = make_candidates(sents, prof, cfg)
    log(f"• {len(sents)} phrases, {len(cands)} candidats")
    score_candidates(cands, sents, prof, cfg)
    stats = {"sentences": len(sents), "candidates": len(cands), "llm": "off"}
    if cfg.llm != "none":
        if llm_call is not None or llm.available(cfg):
            log(f"• avis éditorial ({cfg.llm_model})…")
            errors: list[str] = []
            llm.rerank(cands, cfg, on_error=errors.append, **({"call": llm_call} if llm_call is not None else {}))
            stats["llm"] = "error" if errors else "ok"
            if errors:
                stats["llm_error"] = errors[0]
                log(f"  avis de Claude indisponible : {errors[0]} — score du moteur seul")
        else:
            stats["llm"] = "nokey"
            if cfg.llm == "claude":
                log("  ANTHROPIC_API_KEY absente : score heuristique seul")
    ranked, n_auto = propose(cands, cfg)
    log(f"• {len(ranked)} moments retenus dont {n_auto} à fabriquer d'office")
    return ranked, n_auto, stats


def finalize_kit(c: Candidate) -> Candidate:
    """Kit de publication : titre, accroche, légende, hashtags (LLM si disponible, sinon heuristique)."""
    if not c.hook:
        c.hook = kit.pick_hook(c.text)           # peut rester vide : pas d'accroche plutôt qu'une accroche tronquée
    if not c.title:                               # une phrase entière du clip (plus longue permise qu'à l'écran), sinon coupe propre
        c.title = c.hook or kit.pick_hook(c.text, max_words=18, max_chars=92) or default_title(c.text)
    if not c.caption:
        c.caption = kit.caption(c.title)
    if not c.hashtags:
        c.hashtags = kit.hashtags(c.text)
    return c


def proposal_dict(c: Candidate, pid: int, rank: int, auto: bool) -> dict:
    """Forme publique d'un moment (API et fichier de projet)."""
    return {
        "id": pid, "rank": rank, "auto": auto,
        "start": round(c.start, 2), "end": round(c.end, 2), "duration": round(c.duration, 2),
        "score": round(c.score), "label": label_for(c.score),
        "heuristic": round(c.heuristic), "llm_score": None if c.llm_score is None else round(c.llm_score),
        "title": c.title, "hook": c.hook, "caption": c.caption, "hashtags": c.hashtags, "why": c.why,
        "reasons": c.reasons, "breakdown": breakdown(c.features), "text": c.text,
        "state": "queued" if auto else "reserve", "progress": 0.0,
        "video": "", "poster": "", "layouts": [], "style": "", "error": "", "hook_shown": None,
    }


# ----------------------------------------------------------------------------- rendu

def make_poster(video: Path, out: Path, at: float = 1.2) -> Path | None:
    r = subprocess.run(["ffmpeg", "-y", "-v", "error", "-nostdin", "-ss", f"{at}", "-i", str(video), "-frames:v", "1",
                        "-vf", "scale=360:-2", "-q:v", "5", str(out)], capture_output=True)
    return out if r.returncode == 0 and out.exists() else None


def render_candidate(source: Path, info: MediaInfo, tr: Transcript, prof: AudioProfile, c: Candidate,
                     out_dir: Path, stem: str, cfg: Config, style: Style | None = None,
                     detector: "face.FaceDetector | None" = None, on_progress: Callable[[float], None] | None = None
                     ) -> Rendered:
    """Analyse des visages, plan de caméra, sous-titres, rendu, vignette d'un seul moment."""
    st = style or get_style(cfg.style)
    if c.end > info.duration:                                   # jamais au-delà de la fin réelle de la vidéo
        c = replace(c, end=info.duration)
    if c.end - c.start < 3:
        raise UserError("Ce moment se situe en dehors de la vidéo.")
    out_dir.mkdir(parents=True, exist_ok=True)
    prog = on_progress or (lambda f: None)
    words = clip_words(tr.words, c.start, c.end)
    prog(0.03)
    sample_t = c.start + np.arange(int(c.duration * cfg.sample_fps)) / cfg.sample_fps
    speech = prof.at(sample_t) > prof.speech_threshold
    tl = face.analyze(source, c.start, c.duration, info.width, info.height, cfg.sample_fps,
                      cfg.detect_width, cfg.scene_threshold, speech, detector or face.FaceDetector())
    plan = plan_camera(tl, info.width, info.height, c.duration, cfg)
    prog(0.15)
    ass_path = out_dir / f"{stem}.ass"
    hook = c.hook if cfg.title_overlay else ""
    zone = None
    if hook and st.hook_on:                                      # l'accroche ne se pose jamais sur un visage
        zone = face_zone(tl, plan, 0.0, min(st.hook_seconds, c.duration) if st.hook_seconds > 0 else c.duration)
    ass = build_ass(words, c.start, c.end, cfg, hook, st, avoid=zone)
    ass_path.write_text(ass, encoding="utf-8")
    hook_shown = any(l.startswith("Dialogue:") and ",Hook," in l for l in ass.splitlines()) if hook and st.hook_on else None
    video = render_clip(source, c.start, c.end, plan, ass_path, out_dir / f"{stem}.mp4", cfg, info.has_audio, st,
                        on_progress=lambda f: prog(0.15 + 0.82 * f))
    poster = make_poster(video, out_dir / f"{stem}.jpg", min(1.2, c.duration / 2))
    prog(1.0)
    return Rendered(video, ass_path, poster, [s.layout for s in plan.shots], tl.backend, hook_shown)


# ----------------------------------------------------------------------------- exécution complète (CLI)

def run(source: str | Path, out_dir: str | Path, cfg: Config | None = None,
        transcript_path: str | Path | None = None, render: bool = True,
        log: Log = print, llm_call=None) -> list[ClipResult]:
    cfg = cfg or Config()
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    work = out_dir / ".work"
    t_start = time.time()

    src = str(source)
    if re.match(r"https?://", src):
        log("• téléchargement…")
        source, _ = fetch(src, work, log=log)
    source = Path(source)
    info: MediaInfo = probe(source)
    check_info(info)
    log(f"• source : {info.width}x{info.height} @ {info.fps:.0f} fps, {info.duration / 60:.1f} min")

    tr, prof = transcribe_source(source, work, cfg, transcript_path, log)
    tr = clip_transcript(tr, info.duration)
    check_speech(tr, info)
    ranked, n_auto, _ = find_moments(tr, prof, cfg, log, llm_call)
    chosen = sorted(ranked[:n_auto], key=lambda c: c.start)
    log(f"• {len(chosen)} clips retenus")

    style = get_style(cfg.style)
    detector = face.FaceDetector() if render else None
    results: list[ClipResult] = []
    for i, c in enumerate(chosen, 1):
        finalize_kit(c)
        res = ClipResult(i, c)
        name = f"{i:02d}-{slug(c.title)}"
        if render:
            log(f"• clip {i}/{len(chosen)} [{c.start:.1f}–{c.end:.1f}s] score {c.score:.0f} — {c.title}")
            r = render_candidate(source, info, tr, prof, c, out_dir, name, cfg, style, detector)
            res.video, res.ass, res.layouts, res.face_backend = r.video, r.ass, r.layouts, r.face_backend
        meta = c.to_dict() | {"index": i, "file": f"{name}.mp4" if render else None, "layouts": res.layouts,
                              "label": label_for(c.score), "breakdown": breakdown(c.features)}
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
