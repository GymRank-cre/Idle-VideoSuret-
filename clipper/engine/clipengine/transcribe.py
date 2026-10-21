"""Transcription mot à mot (faster-whisper) et import de transcriptions existantes."""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

from .models import Word


@dataclass
class Transcript:
    language: str
    words: list[Word]

    def to_json(self) -> dict:
        return {
            "language": self.language,
            "words": [
                {"text": w.text, "start": round(w.start, 3), "end": round(w.end, 3), "prob": round(w.prob, 3)}
                for w in self.words
            ],
        }

    @classmethod
    def from_json(cls, d: dict) -> "Transcript":
        words = [
            Word(str(w["text"]).strip(), float(w["start"]), float(w["end"]), float(w.get("prob", 1.0)))
            for w in d["words"]
            if str(w["text"]).strip()
        ]
        return cls(d.get("language", "fr"), _fix_timings(words))


def _fix_timings(words: list[Word]) -> list[Word]:
    """Garantit des timings croissants et des durées strictement positives."""
    out: list[Word] = []
    prev_end = 0.0
    for w in words:
        s = max(w.start, prev_end)
        e = max(w.end, s + 0.04)
        out.append(Word(w.text, s, e, w.prob))
        prev_end = e
    return out


def _read_wav(path: str | Path):
    import wave

    import numpy as np

    with wave.open(str(path), "rb") as w:
        assert w.getframerate() == 16000 and w.getnchannels() == 1 and w.getsampwidth() == 2
        return np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0


def transcribe(audio_path: str | Path, model: str = "large-v3", language: str | None = None,
               device: str = "auto", compute_type: str = "auto") -> Transcript:
    try:
        from faster_whisper import WhisperModel
    except ImportError as e:  # pragma: no cover
        raise RuntimeError("faster-whisper manquant : pip install 'clipengine[asr]'") from e

    wm = WhisperModel(model, device=device, compute_type=compute_type)
    segments, info = wm.transcribe(
        _read_wav(audio_path),              # tableau float32 16 kHz : évite de dépendre du décodeur PyAV
        language=language,
        word_timestamps=True,
        vad_filter=True,
        vad_parameters={"min_silence_duration_ms": 350},
        condition_on_previous_text=False,   # évite les boucles d'hallucination
        beam_size=5,
    )
    words: list[Word] = []
    for seg in segments:
        for w in seg.words or []:
            t = w.word.strip()
            if t:
                words.append(Word(t, float(w.start), float(w.end), float(w.probability)))
    return Transcript(info.language, _fix_timings(words))


_SRT_TIME = re.compile(r"(\d+):(\d+):(\d+)[,.](\d+)\s*-->\s*(\d+):(\d+):(\d+)[,.](\d+)")


def _t(h, m, s, ms) -> float:
    return int(h) * 3600 + int(m) * 60 + int(s) + int(ms.ljust(3, "0")[:3]) / 1000


def load_srt(path: str | Path, language: str = "fr") -> Transcript:
    """SRT -> mots (timings répartis proportionnellement à la longueur de chaque mot)."""
    words: list[Word] = []
    for block in re.split(r"\n\s*\n", Path(path).read_text(encoding="utf-8-sig").strip()):
        lines = block.strip().splitlines()
        for i, ln in enumerate(lines):
            m = _SRT_TIME.search(ln)
            if not m:
                continue
            a, b = _t(*m.groups()[:4]), _t(*m.groups()[4:])
            toks = re.sub(r"<[^>]+>", "", " ".join(lines[i + 1:])).split()
            if not toks:
                break
            total = sum(len(t) + 1 for t in toks)
            cur = a
            for t in toks:
                d = (b - a) * (len(t) + 1) / total
                words.append(Word(t, cur, cur + d))
                cur += d
            break
    return Transcript(language, _fix_timings(words))


def load_transcript(path: str | Path) -> Transcript:
    p = Path(path)
    if p.suffix.lower() == ".srt":
        return load_srt(p)
    return Transcript.from_json(json.loads(p.read_text(encoding="utf-8")))
