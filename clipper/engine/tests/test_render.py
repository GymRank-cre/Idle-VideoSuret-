import subprocess

import numpy as np
import pytest

cv2 = pytest.importorskip("cv2")

from clipengine.render import _fit


def frame(w=640, h=360):
    f = np.zeros((h, w, 3), np.uint8)
    f[:, :, 1] = 180
    f[:, : w // 2, 2] = 220
    return f


def test_fit_keeps_the_whole_frame_centred_for_every_background():
    f = frame()
    for bg in ("blur", "color", "mirror"):
        out = _fit(f, 540, 960, bg, "#102030")
        assert out.shape == (960, 540, 3)
        fh = int(round(360 * 540 / 640))
        y = (960 - fh) // 2
        ref = cv2.resize(f, (540, fh), interpolation=cv2.INTER_AREA)
        assert np.array_equal(out[y:y + fh], ref), bg                 # l'image d'origine est intacte, entière


def test_fit_background_variants_differ():
    f = frame()
    col = _fit(f, 540, 960, "color", "#102030")
    assert tuple(col[5, 5]) == (0x30, 0x20, 0x10)                   # BGR de #102030
    mir = _fit(f, 540, 960, "mirror")
    blur = _fit(f, 540, 960, "blur")
    assert not np.array_equal(mir[:50], col[:50]) and not np.array_equal(mir[:50], blur[:50])
    # le miroir réfléchit l'image : sous le plan, les lignes proches du bord ressemblent à celles du plan
    fh = int(round(360 * 540 / 640)); y = (960 - fh) // 2
    near_edge = mir[y - 4:y].astype(float).mean(); fg_edge = mir[y:y + 4].astype(float).mean()
    assert near_edge < fg_edge                                       # assombri (0,55) mais de même teinte


from conftest import make_words, TEXT  # noqa: E402


@pytest.mark.slow
def test_render_never_goes_past_the_end_and_encoder_errors_are_readable(tmp_path, media):
    from clipengine import face
    from clipengine.audio import compute_profile
    from clipengine.config import Config
    from clipengine.errors import UserError
    from clipengine.media import probe
    from clipengine.models import Candidate
    from clipengine.pipeline import render_candidate
    from clipengine.transcribe import Transcript
    src = media["pan"]
    info = probe(src)                                                  # 24 s
    tr = Transcript("fr", make_words(TEXT[:30], wps=3.4))
    prof = compute_profile(src)
    cfg = Config(preset="ultrafast")
    c = Candidate(start=8.0, end=31.0, lo=0, hi=0, text="x", title="t", hook="h")      # dépasse la fin de la vidéo
    r = render_candidate(src, info, tr, prof, c, tmp_path, "c", cfg)
    out = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0",
                                str(r.video)], capture_output=True, check=True, text=True).stdout)
    assert abs(out - (info.duration - 8.0)) < 0.3
    with pytest.raises(RuntimeError, match="(?i)preset"):
        render_candidate(src, info, tr, prof, c, tmp_path, "bad", Config(preset="nope"))
    with pytest.raises(UserError, match="en dehors"):
        render_candidate(src, info, tr, prof, Candidate(start=30, end=40, lo=0, hi=0, text="x"), tmp_path, "z", cfg)
