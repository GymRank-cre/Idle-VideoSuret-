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
