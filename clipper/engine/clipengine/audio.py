"""Décodage audio en flux et profil d'énergie (dB par pas de 50 ms)."""
from __future__ import annotations

import subprocess
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np

SR = 16000
HOP = 0.05


@dataclass
class AudioProfile:
    db: np.ndarray                    # niveau RMS en dBFS par pas de HOP secondes
    hop: float = HOP
    noise_floor: float = field(default=-90.0, init=False)
    speech_threshold: float = field(default=-90.0, init=False)

    @property
    def duration(self) -> float:
        return len(self.db) * self.hop

    def _sl(self, a: float, b: float) -> np.ndarray:
        i, j = int(max(a, 0) / self.hop), int(max(b, 0) / self.hop) + 1
        return self.db[i:min(j, len(self.db))]

    def __post_init__(self) -> None:
        # calculés une seule fois : recalculer ces percentiles pour chaque candidat serait quadratique
        if len(self.db):
            lo, hi = np.percentile(self.db, [10, 90])
            self.noise_floor = float(lo)
            self.speech_threshold = float(lo + 0.30 * (hi - lo))
        else:
            self.noise_floor = self.speech_threshold = -90.0

    def speech_mask(self, a: float, b: float) -> np.ndarray:
        return self._sl(a, b) > self.speech_threshold

    def dynamics(self, a: float, b: float) -> float:
        """Écart-type du niveau pendant la parole : voix plate vs expressive."""
        seg = self._sl(a, b)
        seg = seg[seg > self.speech_threshold]
        return float(np.std(seg)) if len(seg) > 5 else 0.0

    def peakiness(self, a: float, b: float) -> float:
        """Écart entre les pics (p95) et la médiane : emphases, rires, éclats."""
        seg = self._sl(a, b)
        seg = seg[seg > self.speech_threshold]
        return float(np.percentile(seg, 95) - np.median(seg)) if len(seg) > 5 else 0.0

    def quietest(self, a: float, b: float) -> float:
        """Instant le plus silencieux dans [a, b] (pour couper proprement)."""
        if b <= a:
            return a
        i = int(max(a, 0) / self.hop)
        seg = self._sl(a, b)
        if not len(seg):
            return a
        return (i + int(np.argmin(seg))) * self.hop

    def at(self, times: np.ndarray) -> np.ndarray:
        idx = np.clip((np.asarray(times) / self.hop).astype(int), 0, max(len(self.db) - 1, 0))
        return self.db[idx] if len(self.db) else np.full(len(idx), -90.0)

    def save(self, path: Path) -> None:
        np.save(path, self.db.astype(np.float32))

    @classmethod
    def load(cls, path: Path) -> "AudioProfile":
        return cls(np.load(path).astype(np.float32))


def compute_profile(path: str | Path, start: float | None = None, dur: float | None = None) -> AudioProfile:
    """Décode en flux (mémoire constante, même pour 3 h de podcast)."""
    cmd = ["ffmpeg", "-v", "error", "-nostdin"]
    if start is not None:
        cmd += ["-ss", f"{start:.3f}"]
    cmd += ["-i", str(path)]
    if dur is not None:
        cmd += ["-t", f"{dur:.3f}"]
    cmd += ["-vn", "-ac", "1", "-ar", str(SR), "-f", "s16le", "-"]
    hop_n = int(SR * HOP)
    chunk = hop_n * 2000
    levels: list[np.ndarray] = []
    carry = np.zeros(0, dtype=np.int16)
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    assert proc.stdout is not None
    while True:
        buf = proc.stdout.read(chunk * 2)
        if not buf:
            break
        x = np.concatenate([carry, np.frombuffer(buf[: len(buf) // 2 * 2], dtype=np.int16)])
        n = len(x) // hop_n * hop_n
        carry = x[n:]
        if n:
            f = x[:n].astype(np.float32).reshape(-1, hop_n) / 32768.0
            levels.append(10 * np.log10(np.mean(f * f, axis=1) + 1e-10))
    proc.wait()
    if len(carry) > hop_n // 2:
        f = carry.astype(np.float32) / 32768.0
        levels.append(np.array([10 * np.log10(np.mean(f * f) + 1e-10)], dtype=np.float32))
    return AudioProfile(np.concatenate(levels) if levels else np.zeros(0, dtype=np.float32))


def extract_wav(src: str | Path, dst: str | Path) -> Path:
    """Piste mono 16 kHz pour Whisper."""
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-nostdin", "-i", str(src), "-vn", "-ac", "1",
                    "-ar", str(SR), str(dst)], check=True)
    return Path(dst)
