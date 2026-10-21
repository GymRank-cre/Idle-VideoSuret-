"""Sonde ffprobe et téléchargement (yt-dlp)."""
from __future__ import annotations

import json
import subprocess
from dataclasses import dataclass
from fractions import Fraction
from pathlib import Path


@dataclass
class MediaInfo:
    width: int
    height: int
    fps: float
    duration: float
    has_audio: bool


def probe(path: str | Path) -> MediaInfo:
    out = subprocess.run(
        ["ffprobe", "-v", "error", "-print_format", "json", "-show_streams", "-show_format", str(path)],
        capture_output=True, check=True, text=True).stdout
    d = json.loads(out)
    v = next(s for s in d["streams"] if s["codec_type"] == "video")
    w, h = int(v["width"]), int(v["height"])
    rot = 0
    for sd in v.get("side_data_list", []):
        if "rotation" in sd:
            rot = int(sd["rotation"])
    rot = int(v.get("tags", {}).get("rotate", rot))
    if abs(rot) % 180 == 90:            # ffmpeg décode déjà tourné
        w, h = h, w
    try:
        fps = float(Fraction(v.get("avg_frame_rate") or v["r_frame_rate"]))
    except (ZeroDivisionError, ValueError):
        fps = 30.0
    dur = float(d.get("format", {}).get("duration") or v.get("duration") or 0)
    return MediaInfo(w, h, fps or 30.0, dur, any(s["codec_type"] == "audio" for s in d["streams"]))


def download(url: str, out_dir: str | Path) -> Path:
    """Télécharge une vidéo (YouTube, etc.) en mp4 ≤ 1080p."""
    try:
        import yt_dlp
    except ImportError as e:  # pragma: no cover
        raise RuntimeError("yt-dlp manquant : pip install 'clipengine[download]'") from e
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    opts = {
        "format": "bv*[height<=1080]+ba/b[height<=1080]/b",
        "merge_output_format": "mp4",
        "outtmpl": str(out_dir / "source.%(ext)s"),
        "quiet": True,
        "noprogress": True,
    }
    with yt_dlp.YoutubeDL(opts) as ydl:
        ydl.extract_info(url, download=True)
    return next(out_dir.glob("source.*"))
