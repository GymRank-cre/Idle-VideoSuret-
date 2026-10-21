"""Bout en bout sur une vidéo synthétique (visage mobile, deux locuteurs, plan vide)."""
import json
import subprocess

import numpy as np
import pytest

cv2 = pytest.importorskip("cv2")

from clipengine import Config
from clipengine.face import FaceDetector, analyze
from clipengine.media import probe
from clipengine.pipeline import run
from clipengine.reframe import plan_camera
from conftest import make_words, TEXT

pytestmark = pytest.mark.slow
CFG = Config()


@pytest.fixture(scope="module")
def det():
    return FaceDetector()


def test_tracks_a_moving_face(media, det):
    info = probe(media["pan"])
    tl = analyze(media["pan"], 0, 24, info.width, info.height, 10, detector=det)
    assert len(tl.t) >= 230 and len(tl.tracks) == 1
    (tr,) = tl.tracks.values()
    expected = 300 + 600 * np.clip(tl.t / 24.0, 0, 1)
    ok = ~np.isnan(tr["cx"])
    assert ok.mean() > 0.95
    assert np.nanmedian(np.abs(tr["cx"][ok] - expected[ok])) < 25


def test_finds_who_speaks(media, det):
    info = probe(media["duo"])
    tl = analyze(media["duo"], 0, 24, info.width, info.height, 10, detector=det)
    assert len(tl.tracks) == 2
    left = min(tl.tracks, key=lambda k: np.nanmean(tl.tracks[k]["cx"]))
    right = [k for k in tl.tracks if k != left][0]
    at = lambda s: tl.active[int(s * 10)]
    assert at(4) == left and at(12) == right and at(21) == left
    assert info.width == 1280


def test_plan_is_split_for_duo_and_fit_for_empty(media, det):
    for name, layout in (("duo", "split"), ("empty", "fit")):
        info = probe(media[name])
        dur = info.duration
        tl = analyze(media[name], 0, dur - 0.5, info.width, info.height, 10, detector=det)
        plan = plan_camera(tl, info.width, info.height, dur - 0.5, CFG)
        assert {s.layout for s in plan.shots} == {layout}, name


def _frame(path, t):
    out = subprocess.run(["ffmpeg", "-v", "error", "-ss", str(t), "-i", str(path), "-frames:v", "1",
                          "-f", "image2pipe", "-vcodec", "png", "-"], capture_output=True, check=True).stdout
    return cv2.imdecode(np.frombuffer(out, np.uint8), cv2.IMREAD_COLOR)


def test_full_pipeline_renders_vertical_tracked_captioned_clip(tmp_path, media, det):
    words = make_words(TEXT[:55], wps=3.4)
    tr = tmp_path / "t.json"
    tr.write_text(json.dumps({"language": "fr", "words": [
        {"text": w.text, "start": w.start, "end": w.end} for w in words if w.end < 23.5]}))
    cfg = Config(min_dur=10, max_dur=22, target_dur=18, max_clips=1, min_score=0, llm="none", preset="ultrafast")
    res = run(media["pan"], tmp_path / "out", cfg, tr)
    assert len(res) == 1
    r = res[0]
    mp4 = r.video
    assert mp4 and mp4.exists() and r.ass.exists()

    d = json.loads(subprocess.run(["ffprobe", "-v", "error", "-print_format", "json", "-show_streams",
                                   "-show_format", str(mp4)], capture_output=True, check=True, text=True).stdout)
    v = next(s for s in d["streams"] if s["codec_type"] == "video")
    assert (v["width"], v["height"]) == (1080, 1920) and v["pix_fmt"] == "yuv420p"
    assert any(s["codec_type"] == "audio" for s in d["streams"])
    assert abs(float(d["format"]["duration"]) - r.candidate.duration) < 0.25

    # le visage reste au centre du cadre vertical alors qu'il traverse la source de gauche à droite
    for t in (1.5, r.candidate.duration / 2, r.candidate.duration - 1.0):
        f = _frame(mp4, t)
        faces = det.detect(f)
        assert faces, f"visage perdu à t={t}"
        x, y, w, h, _ = max(faces, key=lambda b: b[2] * b[3])
        assert abs((x + w / 2) - 540) < 160, (t, x + w / 2)

    # sous-titres incrustés : pixels de la couleur de surbrillance dans la zone des sous-titres
    f = _frame(mp4, 2.0)
    band = f[int(1920 * 0.55):int(1920 * 0.78)]
    yellow = np.sum((band[..., 2] > 220) & (band[..., 1] > 190) & (band[..., 0] < 140))
    assert yellow > 300
    meta = json.loads(next((tmp_path / "out").glob("01-*.json")).read_text())
    assert meta["title"] and meta["layouts"] and (tmp_path / "out" / "report.json").exists()


def test_split_layout_renders_two_faces(tmp_path, media, det):
    info = probe(media["duo"])
    from clipengine.render import render_clip
    tl = analyze(media["duo"], 0, 20, info.width, info.height, 10, detector=det)
    plan = plan_camera(tl, info.width, info.height, 20, CFG)
    out = render_clip(media["duo"], 0, 20, plan, None, tmp_path / "split.mp4", Config(preset="ultrafast"), True)
    f = _frame(out, 6.0)
    top, bot = det.detect(f[:960]), det.detect(f[960:])
    assert top and bot
