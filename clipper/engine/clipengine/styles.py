"""Styles de sous-titres : packs, polices embarquées, validation, mesure de texte et aperçu exact (libass)."""
from __future__ import annotations

import re
import subprocess
from dataclasses import asdict, dataclass, fields
from functools import lru_cache
from pathlib import Path

FONT_DIR = Path(__file__).resolve().parent / "fonts"

# `k` = taille réelle de la police dans libass / taille ASS demandée. libass dimensionne le texte sur la hauteur
# de cellule (usWinAscent + usWinDescent), pas sur l'em : k = unitsPerEm / (usWinAscent + usWinDescent), vérifié
# par mesure sur le rendu. Les tailles de style sont exprimées en px d'em (à 1080 px de large).
FONTS: dict[str, dict] = {
    "poppins-xb": {"label": "Moderne", "family": "Poppins ExtraBold", "file": "Poppins-ExtraBold.ttf", "bold": False, "k": 0.568},
    "poppins-b": {"label": "Moderne léger", "family": "Poppins", "file": "Poppins-Bold.ttf", "bold": True, "k": 0.568},
    "anton": {"label": "Massive", "family": "Anton", "file": "Anton-Regular.ttf", "bold": False, "k": 0.577},
    "bebas": {"label": "Condensée", "family": "Bebas Neue", "file": "BebasNeue-Regular.ttf", "bold": False, "k": 0.769},
    "bangers": {"label": "Comic", "family": "Bangers", "file": "Bangers-Regular.ttf", "bold": False, "k": 0.569},
    "dmserif": {"label": "Journal", "family": "DM Serif Display", "file": "DMSerifDisplay-Regular.ttf", "bold": False, "k": 0.729},
    "varela": {"label": "Ronde", "family": "Varela Round", "file": "VarelaRound-Regular.ttf", "bold": False, "k": 0.763},
}

COLORS = ["#FFFFFF", "#FFE14D", "#FFB000", "#FF6B35", "#FF3B6B", "#C77DFF", "#7C5CFF", "#00B4FF",
          "#00E5C8", "#7CFFB2", "#A3FF3F", "#111111"]

BG = ("outline", "shadow", "box", "pill")
POSITIONS = {"haut": 0.25, "centre": 0.44, "bas": 0.66}       # centre vertical du texte (fraction de la hauteur)
ANIMS = ("none", "fade", "pop")
FIT_BG = ("blur", "color", "mirror")

# Zones sûres TikTok à 1080×1920 : colonne de boutons à droite (≈190 px) entre ≈50 % et ≈85 % de la hauteur,
# légende de l'auteur en bas (≈ 20 %), barre d'onglets en haut (≈ 10 %).
SAFE_LEFT, SAFE_RIGHT = 70, 190

HEX = re.compile(r"^#[0-9a-fA-F]{6}$")


@dataclass
class Style:
    name: str = "podcast"
    font: str = "poppins-xb"
    size: int = 80                      # px d'em à 1080 de large
    color: str = "#FFFFFF"
    highlight: str = "#FFE14D"          # mot prononcé
    emphasis: str = "#FF6B35"           # chiffres et mots forts
    emphasis_on: bool = True
    bg: str = "outline"                 # outline | shadow | box | pill
    bg_color: str = "#000000"
    position: str = "bas"               # haut | centre | bas
    upper: bool = True
    words: int = 3
    anim: str = "pop"                   # none | fade | pop
    mask_below: float = 0.2             # mots de confiance inférieure : masqués plutôt qu'affichés faux
    hook_on: bool = True
    hook_where: str = "haut"            # haut | centre
    hook_seconds: float = 3.0           # 0 = tout le clip
    hook_color: str = "#FFFFFF"
    hook_bg: str = "#000000"
    signature: str = ""                 # « @mon.compte »
    signature_where: str = "bas"        # haut | bas
    progress_bar: bool = False
    fit_bg: str = "blur"                # fond du plan entier : blur | color | mirror
    fit_color: str = "#101014"

    def to_dict(self) -> dict:
        return asdict(self)

    @classmethod
    def from_dict(cls, d: dict | None, base: "Style | None" = None) -> "Style":
        """Construit un style à partir d'une saisie non fiable : tout est validé et borné."""
        s = Style(**asdict(base)) if base else Style()
        if not isinstance(d, dict):
            return s
        known = {f.name: f.type for f in fields(cls)}
        for k, v in d.items():
            if k not in known:
                continue
            if k == "font" and v in FONTS:
                s.font = v
            elif k == "size":
                s.size = _num(v, 40, 160, s.size, int)
            elif k in ("color", "highlight", "emphasis", "bg_color", "hook_color", "hook_bg", "fit_color"):
                if isinstance(v, str) and HEX.match(v):
                    setattr(s, k, v.upper())
            elif k == "bg" and v in BG:
                s.bg = v
            elif k == "position" and v in POSITIONS:
                s.position = v
            elif k == "words":
                s.words = _num(v, 1, 5, s.words, int)
            elif k == "anim" and v in ANIMS:
                s.anim = v
            elif k == "mask_below":
                s.mask_below = _num(v, 0.0, 0.9, s.mask_below, float)
            elif k == "hook_where" and v in ("haut", "centre"):
                s.hook_where = v
            elif k == "hook_seconds":
                s.hook_seconds = _num(v, 0.0, 30.0, s.hook_seconds, float)
            elif k == "signature_where" and v in ("haut", "bas"):
                s.signature_where = v
            elif k == "signature":
                s.signature = re.sub(r"[{}\\\n\r]", "", str(v))[:30].strip()
            elif k in ("emphasis_on", "upper", "hook_on", "progress_bar"):
                if isinstance(v, bool):
                    setattr(s, k, v)
            elif k == "fit_bg" and v in FIT_BG:
                s.fit_bg = v
            elif k == "name" and isinstance(v, str):
                s.name = re.sub(r"[^\w\- ]", "", v)[:30] or s.name
        return s


def _num(v, lo, hi, default, typ):
    try:
        return typ(max(lo, min(hi, float(v))))
    except (TypeError, ValueError):
        return default


PRESETS: dict[str, dict] = {
    "podcast": {"label": "Podcast", "hint": "Podcast, interview, lifestyle", "style": {}},
    "business": {"label": "Business", "hint": "Opinions tranchées, chiffres", "style": {
        "font": "anton", "size": 100, "highlight": "#FFE14D", "emphasis": "#FF3B6B", "bg": "outline", "words": 2}},
    "sport": {"label": "Sport", "hint": "Énergie, motivation, vitesse", "style": {
        "font": "bebas", "size": 118, "highlight": "#00E5C8", "emphasis": "#FFE14D", "bg": "shadow", "words": 2}},
    "humour": {"label": "Humour", "hint": "Divertissement, réactions, gaming", "style": {
        "font": "bangers", "size": 98, "highlight": "#FF3B6B", "emphasis": "#FFE14D", "bg": "outline",
        "hook_bg": "#FF3B6B", "words": 3}},
    "education": {"label": "Éducation", "hint": "Tuto, tech, explications", "style": {
        "font": "varela", "size": 76, "highlight": "#7CFFB2", "emphasis": "#FFB000", "bg": "pill",
        "bg_color": "#111111", "upper": False, "words": 3}},
    "recit": {"label": "Récit", "hint": "Histoire, documentaire, autorité", "style": {
        "font": "dmserif", "size": 82, "color": "#F8F3E8", "highlight": "#FFD479", "emphasis": "#FF8A5B",
        "bg": "shadow", "upper": False, "words": 4, "anim": "fade", "hook_bg": "#1B1B1B"}},
}


def get_style(name: str | dict | None, overrides: dict | None = None) -> Style:
    """Style depuis un nom de pack, ou un dict (éventuellement basé sur un pack), avec réglages par-dessus."""
    base = Style()
    if isinstance(name, str) and name in PRESETS:
        base = Style.from_dict(PRESETS[name]["style"], Style(name=name))
    elif isinstance(name, dict):
        pack = name.get("name") if name.get("name") in PRESETS else "podcast"
        base = Style.from_dict(PRESETS[pack]["style"], Style(name=pack))
        base = Style.from_dict(name, base)
    return Style.from_dict(overrides, base) if overrides else base


def catalog() -> dict:
    """Ce que l'interface peut proposer."""
    return {
        "presets": [{"id": k, "label": v["label"], "hint": v["hint"], "style": get_style(k).to_dict()}
                    for k, v in PRESETS.items()],
        "fonts": [{"id": k, "label": v["label"]} for k, v in FONTS.items()],
        "colors": COLORS, "backgrounds": list(BG), "positions": list(POSITIONS), "animations": list(ANIMS),
        "fit_backgrounds": list(FIT_BG),
    }


# ----------------------------------------------------------------------------- mesure de texte

@lru_cache(maxsize=None)
def _pil_font(key: str):
    from PIL import ImageFont
    return ImageFont.truetype(str(FONT_DIR / FONTS[key]["file"]), 200)


def text_width(text: str, font: str, em: float) -> float:
    """Largeur en px du texte rendu par libass pour une police et un em donnés."""
    return _pil_font(font).getlength(text) * em / 200.0


def ass_size(font: str, em: float) -> int:
    """Taille à écrire dans le fichier ASS pour obtenir `em` px réels."""
    return max(8, round(em / FONTS[font]["k"]))


# ----------------------------------------------------------------------------- aperçu exact

def filter_path(p: Path) -> str:
    s = str(p.resolve()).replace("\\", "/")
    return s.replace(":", r"\:").replace("'", r"\'").replace(",", r"\,").replace("[", r"\[").replace("]", r"\]")


def ass_filter(ass: Path | None) -> str:
    if ass is None:
        return "null"
    return f"ass='{filter_path(ass)}':fontsdir='{filter_path(FONT_DIR)}'"


def preview_png(style: Style, work: Path, width: int = 360, sample: str = "Les moments forts arrivent",
                hook: str = "Ce que personne ne vous dit") -> bytes:
    """Rendu réel (libass) d'un style sur un fond neutre : ce que verra le clip, aux pixels près."""
    from .captions import build_ass          # import tardif : captions dépend de ce module
    from .config import Config
    from .models import Word

    W, H = 1080, 1920
    cfg = Config(out_w=W, out_h=H)
    words = [Word(w, 0.0 + i * 0.4, 0.0 + i * 0.4 + 0.35) for i, w in enumerate(sample.split())]
    # l'instant fixé est celui où le 2ᵉ mot est prononcé
    ass = build_ass(words, 0.0, 3.0, cfg, hook, style)
    work.mkdir(parents=True, exist_ok=True)
    f = work / "preview.ass"
    f.write_text(ass, encoding="utf-8")
    out = work / "preview.png"
    bg = f"gradients=s={W}x{H}:c0=0x39465c:c1=0x0c0e13:x0=0:y0=0:x1={W // 3}:y1={H}:d=1"
    vf = f"{ass_filter(f)},scale={width}:-1:flags=lanczos"
    r = subprocess.run(["ffmpeg", "-y", "-v", "error", "-nostdin", "-f", "lavfi", "-i", bg, "-vf", vf,
                        "-frames:v", "1", "-ss", "0.55", str(out)], capture_output=True)
    if r.returncode != 0 or not out.exists():
        # repli : fond uni si le filtre `gradients` est absent
        r = subprocess.run(["ffmpeg", "-y", "-v", "error", "-nostdin", "-f", "lavfi", "-i",
                            f"color=c=0x1d2433:s={W}x{H}:d=1", "-vf", vf, "-frames:v", "1", str(out)],
                           capture_output=True)
        if r.returncode != 0:
            raise RuntimeError(r.stderr.decode(errors="replace")[-300:])
    return out.read_bytes()
