"""Rendu d'un clip : décodage ffmpeg -> recadrage OpenCV -> encodage ffmpeg (+ sous-titres ASS, audio normalisé)."""
from __future__ import annotations

import subprocess
from pathlib import Path

import cv2
import numpy as np

from .config import Config
from .reframe import CameraPlan, crop_size, frame_params


def _warp(frame: np.ndarray, box, out_w: int, out_h: int) -> np.ndarray:
    x0, y0, cw, ch = box
    s = out_w / cw
    m = np.float32([[s, 0, -x0 * s], [0, out_h / ch, -y0 * out_h / ch]])
    interp = cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC
    if interp == cv2.INTER_AREA:               # warpAffine ne supporte pas AREA : on réduit avant
        r = cv2.resize(frame, (max(1, int(frame.shape[1] * s)), max(1, int(frame.shape[0] * out_h / ch))),
                       interpolation=cv2.INTER_AREA)
        m = np.float32([[1, 0, -x0 * s], [0, 1, -y0 * out_h / ch]])
        return cv2.warpAffine(r, m, (out_w, out_h), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
    return cv2.warpAffine(frame, m, (out_w, out_h), flags=interp, borderMode=cv2.BORDER_REPLICATE)


def _fit(frame: np.ndarray, out_w: int, out_h: int) -> np.ndarray:
    """Plan entier centré sur un fond flou issu de la même image."""
    h, w = frame.shape[:2]
    cw, ch = crop_size(w, h, out_w / out_h, 1.0)
    x0, y0 = int((w - cw) / 2), int((h - ch) / 2)
    bg = cv2.resize(frame[y0:y0 + int(ch), x0:x0 + int(cw)], (out_w // 10, out_h // 10), interpolation=cv2.INTER_AREA)
    bg = cv2.GaussianBlur(bg, (0, 0), 4)
    bg = cv2.resize(bg, (out_w, out_h), interpolation=cv2.INTER_LINEAR)
    bg = (bg.astype(np.float32) * 0.55).astype(np.uint8)
    s = min(out_w / w, out_h / h)
    fw, fh = int(round(w * s)), int(round(h * s))
    fg = cv2.resize(frame, (fw, fh), interpolation=cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC)
    y = (out_h - fh) // 2
    x = (out_w - fw) // 2
    bg[y:y + fh, x:x + fw] = fg
    return bg


def compose(frame: np.ndarray, plan: CameraPlan, t: float, out_w: int, out_h: int) -> np.ndarray:
    layout, box = frame_params(plan, t)
    if layout == "track":
        return _warp(frame, box, out_w, out_h)
    if layout == "split":
        hh = out_h // 2
        top = _warp(frame, box[0], out_w, hh)
        bot = _warp(frame, box[1], out_w, out_h - hh)
        out = np.vstack([top, bot])
        out[hh - 3:hh + 3] = (20, 20, 20)
        return out
    return _fit(frame, out_w, out_h)


def _filter_path(p: Path) -> str:
    s = str(p.resolve()).replace("\\", "/")
    return s.replace(":", r"\:").replace("'", r"\'").replace(",", r"\,").replace("[", r"\[").replace("]", r"\]")


def render_clip(src: str | Path, start: float, end: float, plan: CameraPlan, ass: Path | None,
                out: Path, cfg: Config, has_audio: bool = True) -> Path:
    dur = end - start
    fps = cfg.fps
    W, H = cfg.out_w, cfg.out_h
    nframes = int(round(dur * fps))
    sw, sh = plan.src_w, plan.src_h

    dec = subprocess.Popen(
        ["ffmpeg", "-v", "error", "-nostdin", "-ss", f"{start:.3f}", "-i", str(src), "-t", f"{dur:.3f}",
         "-an", "-vf", f"fps={fps}", "-f", "rawvideo", "-pix_fmt", "bgr24", "-"],
        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)

    vf = f"ass='{_filter_path(ass)}'" if ass is not None else "null"
    enc_cmd = ["ffmpeg", "-y", "-v", "error", "-nostdin",
               "-f", "rawvideo", "-pix_fmt", "bgr24", "-s", f"{W}x{H}", "-r", str(fps), "-i", "-"]
    if has_audio:
        af = f"afade=t=in:d=0.04,afade=t=out:st={max(dur - 0.08, 0):.3f}:d=0.08"
        if cfg.loudnorm:
            af = "loudnorm=I=-14:TP=-1.5:LRA=11," + af
        enc_cmd += ["-ss", f"{start:.3f}", "-t", f"{dur:.3f}", "-i", str(src), "-map", "0:v", "-map", "1:a:0",
                    "-af", af, "-c:a", "aac", "-b:a", "192k", "-ar", "48000"]
    enc_cmd += ["-vf", vf, "-c:v", "libx264", "-preset", cfg.preset, "-crf", str(cfg.crf),
                "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-shortest", str(out)]
    enc = subprocess.Popen(enc_cmd, stdin=subprocess.PIPE, stderr=subprocess.PIPE)

    fbytes = sw * sh * 3
    assert dec.stdout is not None and enc.stdin is not None
    last = None
    written = 0
    try:
        for n in range(nframes):
            buf = dec.stdout.read(fbytes)
            if len(buf) < fbytes:
                break
            frame = np.frombuffer(buf, np.uint8).reshape(sh, sw, 3)
            last = compose(frame, plan, n / fps, W, H)
            enc.stdin.write(last.tobytes())
            written += 1
        # décodeur en retard de quelques images : on fige la dernière pour garder audio/vidéo alignés
        while last is not None and written < nframes:
            enc.stdin.write(last.tobytes())
            written += 1
    finally:
        enc.stdin.close()
        dec.stdout.close()
        dec.kill(); dec.wait()
        err = enc.stderr.read().decode(errors="replace") if enc.stderr else ""
        rc = enc.wait()
    if rc != 0:
        raise RuntimeError(f"ffmpeg a échoué ({rc}) : {err[-800:]}")
    return out
