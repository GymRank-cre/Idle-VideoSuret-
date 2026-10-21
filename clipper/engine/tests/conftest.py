"""Fixtures : transcription française synthétique et vidéo de test (visages mobiles + voix simulée)."""
from __future__ import annotations

import json
import shutil
import subprocess
from pathlib import Path

import numpy as np
import pytest

from clipengine.models import Word

TEXT = (
    "Pourquoi personne ne vous dit la vérité sur le sommeil ? "
    "Parce que la plupart des gens dorment mal, et le sommeil se dégrade sans le savoir. "
    "Moi, pendant dix ans, j'ai dormi cinq heures par nuit, un sommeil trop court. "
    "Et un jour, mon médecin m'a montré un chiffre incroyable : trente pour cent de risque en plus pour qui dort mal. "
    "Alors j'ai tout changé pour mon sommeil, et voici ce qui s'est passé. "
    "Premièrement, plus d'écran avant de dormir, plus d'écran après vingt et une heures. "
    "Deuxièmement, une chambre fraîche pour dormir, dix-huit degrés, pas plus. "
    "Le résultat a été immédiat : mon sommeil est profond et je me réveille enfin reposé. "
    "Sur un tout autre sujet, parlons un peu de cuisine italienne et de pâtes fraîches. "
    "Pour la sauce tomate, la cuisine demande du basilic, de l'ail et beaucoup de patience. "
    "La pâte fraîche se repose une heure au frais, puis on étale la pâte très finement. "
    "Voilà, la cuisine italienne c'est tout simple, et la sauce est vraiment délicieuse."
).split()


def make_words(text=TEXT, t0=0.0, wps=2.8, pause=0.55):
    """Mots avec des pauses plus longues après la ponctuation finale."""
    words, t = [], t0
    for tok in text:
        d = max(0.18, len(tok) / 14.0 + 0.08) * (2.8 / wps)
        words.append(Word(tok, t, t + d))
        t += d + (pause if tok[-1] in ".?!" else 0.03)
    return words


@pytest.fixture(scope="session")
def words():
    return make_words()


def _have_media_tools():
    return shutil.which("ffmpeg") and shutil.which("ffprobe")


@pytest.fixture(scope="session")
def face_img():
    cv2 = pytest.importorskip("cv2")
    skimage = pytest.importorskip("skimage.data")
    a = cv2.cvtColor(skimage.astronaut(), cv2.COLOR_RGB2BGR)
    return a[10:250, 110:330].copy()            # visage + épaules, 220 x 240


def _draw_face(frame, face, cx, cy, speaking, t, tint=False):
    import cv2
    f = face.copy()
    if tint:
        f = np.ascontiguousarray(f[:, :, ::-1])
    h, w = f.shape[:2]
    if speaking:                                # bouche qui s'ouvre/ferme (zone sous le nez)
        open_ = int(4 + 9 * abs(np.sin(t * 17)))
        cv2.ellipse(f, (int(w * 0.5), int(h * 0.50)), (18, open_), 0, 0, 360, (20, 20, 60), -1)
    x0, y0 = int(cx - w / 2), int(cy - h / 2)
    frame[y0:y0 + h, x0:x0 + w] = f


def make_video(path: Path, kind: str, face, dur=24.0, fps=25):
    """kind='pan' : un visage qui traverse l'écran ; 'duo' : deux visages, locuteur A-B-A ; 'empty' : paysage."""
    import cv2
    W, H = 1280, 720
    yy = np.linspace(40, 110, H, dtype=np.uint8)[:, None, None]
    bg = np.repeat(np.repeat(yy, W, 1), 3, 2).copy()
    bg[:, :, 1] += 20
    cmd = ["ffmpeg", "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "bgr24", "-s", f"{W}x{H}", "-r", str(fps),
           "-i", "-", "-f", "lavfi", "-i", f"sine=frequency=180:duration={dur}:sample_rate=16000",
           "-af", "volume='0.25+0.2*sin(2*PI*3*t)':eval=frame", "-c:v", "libx264", "-preset", "ultrafast",
           "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", str(path)]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(dur * fps)):
        t = n / fps
        fr = bg.copy()
        cv2.putText(fr, f"{t:5.1f}", (20, 60), cv2.FONT_HERSHEY_SIMPLEX, 1.4, (255, 255, 255), 3)
        if kind == "pan":
            cx = 300 + (900 - 300) * min(1.0, t / dur)
            _draw_face(fr, face, cx, 330, True, t)
        elif kind == "duo":
            a_speaks = not (8 <= t < 16)
            _draw_face(fr, face, 280, 340, a_speaks, t)
            _draw_face(fr, face, 1000, 340, not a_speaks, t, tint=False)
        p.stdin.write(fr.tobytes())
    p.stdin.close()
    p.wait()
    return path


@pytest.fixture(scope="session")
def media(tmp_path_factory, face_img):
    if not _have_media_tools():
        pytest.skip("ffmpeg indisponible")
    d = tmp_path_factory.mktemp("media")
    return {
        "pan": make_video(d / "pan.mp4", "pan", face_img),
        "duo": make_video(d / "duo.mp4", "duo", face_img),
        "empty": make_video(d / "empty.mp4", "empty", face_img, dur=12.0),
    }


@pytest.fixture(scope="session")
def media_long(tmp_path_factory, face_img):
    """70 s : couvre toute la transcription de test (≈ 66 s)."""
    if not _have_media_tools():
        pytest.skip("ffmpeg indisponible")
    return make_video(tmp_path_factory.mktemp("long") / "long.mp4", "pan", face_img, dur=70.0)


def wait_until(cond, timeout=60.0, step=0.1):
    import time
    end = time.time() + timeout
    while time.time() < end:
        v = cond()
        if v:
            return v
        time.sleep(step)
    raise AssertionError("délai dépassé")


@pytest.fixture()
def stub_engine(monkeypatch, words):
    """Transcription déterministe et rendu factice : on teste la logique, pas ffmpeg."""
    from clipengine import pipeline, projects
    from clipengine.pipeline import Rendered
    from clipengine.transcribe import Transcript

    monkeypatch.setattr(pipeline, "transcribe", lambda *a, **k: Transcript("fr", words))
    calls = []

    def fake_render(source, info, tr, prof, c, out_dir, stem, cfg, style=None, detector=None, on_progress=None):
        out_dir.mkdir(parents=True, exist_ok=True)
        v, j, a = out_dir / f"{stem}.mp4", out_dir / f"{stem}.jpg", out_dir / f"{stem}.ass"
        v.write_bytes(b"\x00\x00\x00\x18ftypmp42" + b"x" * 4000)
        j.write_bytes(b"\xff\xd8\xff" + b"x" * 200)
        a.write_text("ass")
        if on_progress:
            on_progress(0.5)
            on_progress(1.0)
        calls.append((c.start, c.end, style.name if style else None, style.to_dict() if style else None))
        return Rendered(v, a, j, ["track"], "stub")

    monkeypatch.setattr(projects, "render_candidate", fake_render)
    return calls
