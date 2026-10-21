"""Rendu d'un clip : décodage ffmpeg -> recadrage OpenCV -> encodage ffmpeg (+ sous-titres ASS, audio normalisé)."""
from __future__ import annotations

import subprocess
from pathlib import Path

import cv2
import numpy as np

from .config import Config
from .reframe import CameraPlan, crop_size, frame_params
from .styles import Style, ass_filter


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


def _hex_bgr(h: str) -> tuple[int, int, int]:
    h = h.lstrip("#")
    return int(h[4:6], 16), int(h[2:4], 16), int(h[0:2], 16)


def _fit(frame: np.ndarray, out_w: int, out_h: int, bg: str = "blur", color: str = "#101014") -> np.ndarray:
    """Plan entier centré ; fond flou issu de l'image, uni, ou miroir (réflexion de l'image)."""
    h, w = frame.shape[:2]
    s = min(out_w / w, out_h / h)
    fw, fh = int(round(w * s)), int(round(h * s))
    fg = cv2.resize(frame, (fw, fh), interpolation=cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC)
    y, x = (out_h - fh) // 2, (out_w - fw) // 2
    if bg == "color":
        out = np.empty((out_h, out_w, 3), np.uint8)
        out[:] = _hex_bgr(color)
    elif bg == "mirror":
        out = np.pad(fg, ((y, out_h - y - fh), (x, out_w - x - fw), (0, 0)), mode="symmetric")
        out = cv2.GaussianBlur(out, (0, 0), 6)
        out = (out.astype(np.float32) * 0.55).astype(np.uint8)
    else:
        cw, ch = crop_size(w, h, out_w / out_h, 1.0)
        x0, y0 = int((w - cw) / 2), int((h - ch) / 2)
        small = cv2.resize(frame[y0:y0 + int(ch), x0:x0 + int(cw)], (out_w // 10, out_h // 10),
                           interpolation=cv2.INTER_AREA)
        small = cv2.GaussianBlur(small, (0, 0), 4)
        out = cv2.resize(small, (out_w, out_h), interpolation=cv2.INTER_LINEAR)
        out = (out.astype(np.float32) * 0.55).astype(np.uint8)
    out[y:y + fh, x:x + fw] = fg
    return out


def compose(frame: np.ndarray, plan: CameraPlan, t: float, out_w: int, out_h: int,
            fit_bg: str = "blur", fit_color: str = "#101014") -> np.ndarray:
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
    return _fit(frame, out_w, out_h, fit_bg, fit_color)


def render_clip(src: str | Path, start: float, end: float, plan: CameraPlan, ass: Path | None,
                out: Path, cfg: Config, has_audio: bool = True, style: Style | None = None,
                on_progress=None) -> Path:
    dur = end - start
    fps = cfg.fps
    W, H = cfg.out_w, cfg.out_h
    nframes = int(round(dur * fps))
    sw, sh = plan.src_w, plan.src_h

    dec = subprocess.Popen(
        ["ffmpeg", "-v", "error", "-nostdin", "-ss", f"{start:.3f}", "-i", str(src), "-t", f"{dur:.3f}",
         "-an", "-vf", f"fps={fps}", "-f", "rawvideo", "-pix_fmt", "bgr24", "-"],
        stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)

    vf = ass_filter(ass)
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
    fit_bg, fit_color = (style.fit_bg, style.fit_color) if style else ("blur", "#101014")
    assert dec.stdout is not None and enc.stdin is not None
    last = None
    written = 0
    try:
        for n in range(nframes):
            buf = dec.stdout.read(fbytes)
            if len(buf) < fbytes:
                break
            frame = np.frombuffer(buf, np.uint8).reshape(sh, sw, 3)
            last = compose(frame, plan, n / fps, W, H, fit_bg, fit_color)
            enc.stdin.write(last.tobytes())
            written += 1
            if on_progress and written % 15 == 0:
                on_progress(written / nframes)
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
