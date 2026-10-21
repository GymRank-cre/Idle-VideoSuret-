"""Styles de sous-titres : validation, présence des polices, ajustement à la zone sûre (mesuré sur le rendu libass)."""
import subprocess

import numpy as np
import pytest

from clipengine import Config
from clipengine.captions import build_ass, group_pages, layout_page
from clipengine.models import Word
from clipengine.styles import (FONT_DIR, FONTS, PRESETS, SAFE_LEFT, SAFE_RIGHT, Style, catalog, get_style,
                               preview_png, text_width)


def W(text, a, b, prob=1.0):
    return Word(text, a, b, prob)


def test_every_font_file_ships_with_its_license():
    for k, f in FONTS.items():
        assert (FONT_DIR / f["file"]).is_file(), k
    assert len(list(FONT_DIR.glob("OFL-*.txt"))) >= 6


def test_style_from_untrusted_dict_is_validated():
    s = Style.from_dict({"font": "nope", "size": 9999, "color": "red", "highlight": "#abcdef", "bg": "evil",
                         "position": "haut", "words": "x", "signature": "@moi{\\}\n.fr", "hook_seconds": -5,
                         "mask_below": 5, "emphasis_on": "yes", "unknown": 1, "name": "<b>x</b>"})
    d = Style()
    assert s.font == d.font and s.size == 160 and s.color == d.color and s.highlight == "#ABCDEF"
    assert s.bg == d.bg and s.position == "haut" and s.words == d.words
    assert s.signature == "@moi.fr" and s.hook_seconds == 0.0 and s.mask_below == 0.9
    assert s.emphasis_on is True and s.name == "bxb"
    assert Style.from_dict("pas un dict").to_dict() == d.to_dict()


def test_presets_are_valid_and_distinct():
    styles = {k: get_style(k) for k in PRESETS}
    assert len(PRESETS) >= 6
    assert len({(s.font, s.bg, s.highlight) for s in styles.values()}) == len(styles)
    assert get_style("inconnu").name == "podcast"
    s = get_style({"name": "sport", "size": 90, "signature": "@moi"})
    assert s.font == "bebas" and s.size == 90 and s.signature == "@moi"          # pack + réglages
    assert get_style("podcast", {"position": "haut"}).position == "haut"
    c = catalog()
    assert {p["id"] for p in c["presets"]} == set(PRESETS) and len(c["colors"]) == 12


def test_layout_breaks_then_shrinks_long_pages():
    em = 80
    assert layout_page(["BONJOUR"], "poppins-xb", em, 820) == (None, em)
    brk, e = layout_page(["INCROYABLEMENT", "EXTRAORDINAIRE"], "poppins-xb", em, 820)
    assert brk == 1 and e == em                                    # 2 lignes suffisent
    brk, e = layout_page(["ANTICONSTITUTIONNELLEMENT"], "poppins-xb", em, 820)
    assert brk is None and e < em and text_width("ANTICONSTITUTIONNELLEMENT", "poppins-xb", e) <= 820 * 1.01


def test_group_pages_respects_words_and_pauses():
    ws = [W("a", 0, .2), W("b", .25, .4), W("c", 2.0, 2.2), W("d", 2.25, 2.4)]
    assert [len(p) for p in group_pages(ws, 3)] == [2, 2]          # une pause longue coupe la page
    assert [len(p) for p in group_pages(ws, 1)] == [1, 1, 1, 1]


def test_emphasis_numbers_and_low_confidence_mask():
    cfg = Config()
    ws = [W("trente", 0, .3), W("pour", .35, .5), W("30", .55, .8), W("pourcent", .85, 1.2), W("zzxq", 1.3, 1.5, prob=0.05)]
    ass = build_ass(ws, 0, 4, cfg, "", get_style({"words": 5}))
    assert "ZZXQ" not in ass.upper()                               # mot douteux masqué
    ev = [l for l in ass.splitlines() if l.startswith("Dialogue: 1")]
    assert len(ev) == 4
    # le chiffre porte la couleur d'emphase (#FF6B35 -> BGR) tant qu'il n'est pas le mot prononcé
    assert "{\\c&H356BFF&\\fscx100\\fscy100}30" in ev[0]
    off = build_ass(ws, 0, 4, cfg, "", get_style({"words": 5, "emphasis_on": False}))
    assert "&H356BFF&" not in off


def test_anim_modes_and_pill_layers():
    cfg = Config()
    ws = [W("un", 0, .3), W("deux", .4, .7), W("trois", .8, 1.1)]
    fade = build_ass(ws, 0, 3, cfg, "", get_style({"anim": "fade"}))
    assert fade.count("\\fad(140,0)") == 1 and "\\t(0,90" not in fade
    pop = build_ass(ws, 0, 3, cfg, "", get_style({"anim": "pop"}))
    assert "\\t(0,90" in pop and "\\fad(140,0)" not in pop
    pill = build_ass(ws, 0, 3, cfg, "", get_style({"bg": "pill"}))
    assert sum(l.startswith("Dialogue: 0") for l in pill.splitlines()) == 3 and "\\blur9" in pill


def test_hook_signature_and_duration():
    cfg = Config()
    ws = [W("a", 0, .3)]
    ass = build_ass(ws, 0, 10, cfg, "Mon accroche", get_style({"hook_seconds": 0, "signature": "@moi.fr"}))
    hook = next(l for l in ass.splitlines() if ",Hook," in l)
    assert hook.split(",")[2] == "0:00:10.00"                      # 0 = tout le clip
    assert any(",Sig," in l and "@moi.fr" in l for l in ass.splitlines())
    short = build_ass(ws, 0, 10, cfg, "Mon accroche", get_style({"hook_seconds": 3}))
    assert next(l for l in short.splitlines() if ",Hook," in l).split(",")[2] == "0:00:03.00"
    assert not any(",Hook," in l for l in build_ass(ws, 0, 10, cfg, "x", get_style({"hook_on": False})).splitlines()
                   if l.startswith("Dialogue"))
    assert not any(l.startswith("Dialogue: 1") for l in build_ass(ws, 0, 10, Config(captions=False)).splitlines())


@pytest.mark.slow
@pytest.mark.parametrize("preset", list(PRESETS))
def test_caption_stays_inside_the_tiktok_safe_zone_on_the_real_render(preset, tmp_path):
    """On rend avec libass le pire cas (mots très longs) et on mesure : rien ne déborde sur la colonne de boutons."""
    cv2 = pytest.importorskip("cv2")
    from clipengine.styles import ass_filter
    cfg = Config(title_overlay=False)
    ws = [W("EXTRAORDINAIREMENT", 0, .5), W("anticonstitutionnellement", .6, 1.2), W("incroyable", 1.3, 1.8)]
    st = get_style(preset, {"words": 3, "bg_color": "#FF00FF", "position": "bas", "highlight": "#00FFFF"})
    f = tmp_path / "t.ass"
    f.write_text(build_ass(ws, 0, 3, cfg, "", st), encoding="utf-8")
    worst_left, worst_right = 1080, 0
    for t in (0.2, 0.9, 1.5):
        png = tmp_path / "o.png"
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "lavfi", "-i", "color=black:s=1080x1920:d=2",
                        "-vf", ass_filter(f), "-ss", str(t), "-frames:v", "1", str(png)], check=True)
        im = cv2.imread(str(png))
        ys, xs = np.where(im.max(axis=2) > 60)
        assert len(xs), "aucun texte rendu"
        worst_left, worst_right = min(worst_left, xs.min()), max(worst_right, xs.max())
    assert worst_left >= SAFE_LEFT - 14, (preset, worst_left)
    assert worst_right <= 1080 - SAFE_RIGHT + 14, (preset, worst_right)


@pytest.mark.slow
def test_preview_png_is_a_real_render(tmp_path):
    png = preview_png(get_style("business"), tmp_path, 360)
    assert png[:8] == b"\x89PNG\r\n\x1a\n"
    cv2 = pytest.importorskip("cv2")
    im = cv2.imdecode(np.frombuffer(png, np.uint8), cv2.IMREAD_COLOR)
    assert im.shape[1] == 360 and im.shape[0] == 640
    assert (im.max(axis=2) > 200).sum() > 200                       # du texte clair est bien dessiné
