"""Sous-titres ASS « karaoké » : pages de quelques mots, mot actif en surbrillance, chiffres et mots forts en couleur,
texte ajusté à la zone sûre de TikTok, accroche en tête, signature."""
from __future__ import annotations

import re

from . import lexicon as L
from .config import Config
from .lexicon import TERMINAL
from .models import Word
from .styles import (FONTS, POSITIONS, SAFE_LEFT, SAFE_RIGHT, Style, ass_size, get_style, text_width)


def _ass_color(hex_rgb: str, alpha: int = 0) -> str:
    h = hex_rgb.lstrip("#")
    r, g, b = h[0:2], h[2:4], h[4:6]
    return f"&H{alpha:02X}{b}{g}{r}&".upper()


def _c(hex_rgb: str) -> str:
    """Couleur pour un tag inline \\c (sans canal alpha)."""
    h = hex_rgb.lstrip("#")
    return f"&H{h[4:6]}{h[2:4]}{h[0:2]}&".upper()


def _t(sec: float) -> str:
    sec = max(0.0, sec)
    cs = int(round(sec * 100))
    return f"{cs // 360000}:{cs // 6000 % 60:02d}:{cs // 100 % 60:02d}.{cs % 100:02d}"


def _esc(text: str) -> str:
    return text.replace("\\", "").replace("{", "(").replace("}", ")").replace("\n", " ")


def group_pages(words: list[Word], max_words: int = 3, max_chars: int = 24, gap: float = 0.6) -> list[list[Word]]:
    pages: list[list[Word]] = []
    cur: list[Word] = []
    for i, w in enumerate(words):
        cur.append(w)
        nxt = words[i + 1] if i + 1 < len(words) else None
        chars = sum(len(x.text) + 1 for x in cur)
        end_sentence = w.text[-1] in TERMINAL + ",;:"
        if nxt is None or len(cur) >= max_words or chars >= max_chars or end_sentence or nxt.start - w.end > gap:
            pages.append(cur)
            cur = []
    return pages


def wrap(text: str, width: int) -> str:
    words, lines, cur = text.split(), [], ""
    for w in words:
        if cur and len(cur) + 1 + len(w) > width:
            lines.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}".strip()
    if cur:
        lines.append(cur)
    return "\\N".join(lines)


def wrap_px(text: str, font: str, em: float, max_w: float, max_lines: int = 3) -> tuple[list[str], float]:
    """Coupe le texte en lignes tenant dans `max_w` px ; réduit la taille si trois lignes ne suffisent pas."""
    for shrink in (1.0, 0.92, 0.85, 0.78, 0.7, 0.62, 0.55):
        e = em * shrink
        lines, cur = [], ""
        for w in text.split():
            trial = f"{cur} {w}".strip()
            if cur and text_width(trial, font, e) > max_w:
                lines.append(cur)
                cur = w
            else:
                cur = trial
        if cur:
            lines.append(cur)
        if len(lines) <= max_lines and all(text_width(l, font, e) <= max_w for l in lines):
            return lines, e
    return lines, em * 0.55


def layout_page(tokens: list[str], font: str, em: float, max_w: float) -> tuple[int | None, float]:
    """Pour une page de mots : (indice après lequel couper en 2 lignes, ou None ; em à utiliser)."""
    full = " ".join(tokens)
    if text_width(full, font, em) <= max_w:
        return None, em
    best_k, best_w = None, text_width(full, font, em)
    for k in range(1, len(tokens)):
        w = max(text_width(" ".join(tokens[:k]), font, em), text_width(" ".join(tokens[k:]), font, em))
        if w < best_w:
            best_k, best_w = k, w
    if best_w <= max_w:
        return best_k, em
    return best_k, max(em * 0.55, em * max_w / best_w * 0.97)


def _is_emphasis(token: str) -> bool:
    t = re.sub(r"[^\w]", "", token.lower(), flags=re.UNICODE)
    return bool(t) and (any(ch.isdigit() for ch in t) or t in L.POWER)


def _page_emphasis(tokens: list[str], limit: int = 3) -> set[int]:
    """Indices des mots à mettre en valeur : les chiffres d'abord, puis les mots forts, trois au plus par page."""
    nums = [i for i, t in enumerate(tokens) if any(ch.isdigit() for ch in t)]
    power = [i for i, t in enumerate(tokens) if i not in nums and _is_emphasis(t)]
    return set((nums + power)[:limit])


def place_hook(default_top: float, h: float, avoid: tuple[float, float] | None,
               limit: float = 0.58) -> float | None:
    """Où poser l'accroche (haut de la boîte, fraction de la hauteur) sans couvrir `avoid` (bande des têtes).

    Ordre : position voulue ; sous le visage si ça tient au-dessus des sous-titres ; au-dessus du visage ; sinon rien.
    """
    if avoid is None:
        return default_top
    a, b = avoid
    margin = 0.012
    if default_top + h + margin <= a or default_top >= b + margin:
        return default_top
    below = b + 0.015
    if below + h <= limit:
        return below
    above = a - h - 0.015
    if above >= 0.095:
        return above
    return None


def build_ass(words: list[Word], clip_start: float, clip_end: float, cfg: Config, title: str = "",
              style: Style | None = None, avoid: tuple[float, float] | None = None) -> str:
    """`words` en temps source ; la sortie est en temps relatif au clip."""
    st = style or get_style(getattr(cfg, "style", "podcast"))
    W, H = cfg.out_w, cfg.out_h
    sc = W / 1080
    dur = clip_end - clip_start
    font = FONTS[st.font]
    fam, bold = font["family"], -1 if font["bold"] else 0
    col, hi, emph = _c(st.color), _c(st.highlight), _c(st.emphasis)

    rel = [Word(w.text, w.start - clip_start, w.end - clip_start, w.prob)
           for w in words if w.end > clip_start and w.start < clip_end and w.prob >= st.mask_below]

    em = st.size * sc
    outline = {"outline": 8, "shadow": 0, "box": 15, "pill": 0}[st.bg] * sc * (st.size / 80)
    shadow = {"outline": 3, "shadow": 8, "box": 0, "pill": 0}[st.bg] * sc * (st.size / 80)
    edge = {"outline": 11, "shadow": 8, "box": 15, "pill": 34}[st.bg] * sc * (st.size / 80)   # débord visible de chaque côté
    hook_em = max(46.0, min(92.0, 0.78 * st.size)) * sc
    sig_em = 38.0 * sc

    def style_line(name, fsize_em, border, outl, shad, outline_col, back_col, align=5):
        return (f"Style: {name},{fam},{ass_size(st.font, fsize_em)},{col},{col},{outline_col},{back_col},{bold},0,0,0,"
                f"100,100,0,0,{border},{round(outl)},{round(shad)},{align},0,0,0,1")

    bgc = _ass_color(st.bg_color)
    styles = [
        style_line("Cap", em, {"outline": 1, "shadow": 1, "box": 3, "pill": 1}[st.bg], outline, shadow, bgc,
                   _ass_color(st.bg_color, 0x50) if st.bg == "shadow" else _ass_color("#000000", 0x96)),
        style_line("PillBox", em, 3, 20 * sc * (st.size / 80), 0, bgc, bgc),
        style_line("Hook", hook_em, 3, 16 * sc, 0, _ass_color(st.hook_bg, 0x30), _ass_color(st.hook_bg, 0x30), 8),
        style_line("Sig", sig_em, 1, 4 * sc, 1 * sc, _ass_color("#000000"), _ass_color("#000000", 0x96)),
        style_line("Bar", 20, 1, 0, 0, _ass_color(st.highlight), _ass_color(st.highlight), 7),
    ]
    head = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {W}
PlayResY: {H}
WrapStyle: 2
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
""" + "\n".join(styles) + """

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    ev: list[str] = []

    # ---- sous-titres
    if cfg.captions and rel:
        right = SAFE_RIGHT * sc if st.position != "haut" else SAFE_LEFT * sc
        left = SAFE_LEFT * sc
        safe_w = W - left - right - 2 * edge
        cx, cy = (left + (W - right)) / 2, H * POSITIONS[st.position]
        pages = group_pages(rel, st.words)
        for pi, page in enumerate(pages):
            page_end = page[-1].end
            if pi + 1 < len(pages) and pages[pi + 1][0].start - page_end < 0.4:
                page_end = pages[pi + 1][0].start                    # pas de clignotement entre deux pages
            else:
                page_end = min(page_end + 0.15, dur)
            toks = [_esc(w.text.upper() if st.upper else w.text) for w in page]
            brk, em_page = layout_page(toks, st.font, em, safe_w)
            size_tag = f"\\fs{ass_size(st.font, em_page)}" if abs(em_page - em) > 0.5 else ""
            emph_idx = _page_emphasis(toks) if st.emphasis_on else set()
            for wi, w in enumerate(page):
                s = w.start if wi else min(w.start, page[0].start)
                e = page[wi + 1].start if wi + 1 < len(page) else page_end
                if e <= s:
                    e = s + 0.05
                parts, plain = [], []
                for wj, tok in enumerate(toks):
                    sep = "\\N" if brk is not None and wj + 1 == brk else " "
                    if wj == wi:
                        color = hi
                        pop = (f"\\fscx88\\fscy88\\t(0,90,\\fscx112\\fscy112)\\t(90,180,\\fscx100\\fscy100)"
                               if st.anim == "pop" else "")
                        parts.append(f"{{\\c{color}{pop}}}{tok}")
                    else:
                        color = emph if wj in emph_idx else col
                        parts.append(f"{{\\c{color}\\fscx100\\fscy100}}{tok}")
                    plain.append(tok)
                    if wj + 1 < len(toks):
                        parts.append(sep)
                        plain.append(sep)
                line = "".join(parts)
                fade = "\\fad(140,0)" if st.anim == "fade" and wi == 0 else ""
                pos = f"\\an5\\pos({round(cx)},{round(cy)}){size_tag}"
                if st.bg == "pill":
                    # boîte floutée (coins adoucis) sous le texte : le texte reste net
                    ghost = "".join(plain)
                    ev.append(f"Dialogue: 0,{_t(s)},{_t(e)},PillBox,,0,0,0,,"
                              f"{{{pos}\\1a&HFF&\\blur9{fade}}}{ghost}")
                ev.append(f"Dialogue: 1,{_t(s)},{_t(e)},Cap,,0,0,0,,{{{pos}{fade}}}{line}")

    # ---- accroche : jamais par-dessus un visage ni sur la zone des sous-titres
    if cfg.title_overlay and st.hook_on and title:
        lines, e_hook = wrap_px(_esc(title).upper() if st.upper and st.font in ("anton", "bebas", "bangers")
                                else _esc(title), st.font, hook_em, W - 2 * SAFE_LEFT * sc - 32 * sc, max_lines=2)
        end = min(st.hook_seconds, dur) if st.hook_seconds > 0 else dur
        pad = 16 * sc
        box_h = (len(lines) * ass_size(st.font, e_hook) + 2 * pad) / H          # hauteur réelle de la boîte (mesurée)
        default_top = 0.105 if st.hook_where == "haut" else 0.34 - box_h / 2
        top = place_hook(default_top, box_h, avoid)
        if top is not None:
            sz = f"\\fs{ass_size(st.font, e_hook)}" if abs(e_hook - hook_em) > 0.5 else ""
            where = f"\\an8\\pos({W // 2},{round(top * H + pad)})"
            txt = "\\N".join(lines)
            ev.append(f"Dialogue: 2,{_t(0)},{_t(end)},Hook,,0,0,0,,{{{where}{sz}\\c{_c(st.hook_color)}\\fad(220,260)}}{txt}")

    # ---- signature
    if st.signature:
        if st.signature_where == "haut":
            y = H * (0.215 if (st.hook_on and st.hook_where == "haut" and title) else 0.125)
        else:
            y = H * 0.94
        ev.append(f"Dialogue: 2,{_t(0)},{_t(dur)},Sig,,0,0,0,,{{\\an5\\pos({W // 2},{round(y)})\\1a&H50&}}{_esc(st.signature)}")

    # ---- barre de progression
    if st.progress_bar and dur > 0:
        bh = max(6, round(14 * sc))
        y0 = H - bh
        ms = int(dur * 1000)
        ev.append(
            f"Dialogue: 3,{_t(0)},{_t(dur)},Bar,,0,0,0,,{{\\an7\\pos(0,{y0})\\bord0\\shad0\\1c{hi}"
            f"\\clip(0,{y0},0,{H})\\t(0,{ms},\\clip(0,{y0},{W},{H}))\\p1}}m 0 0 l {W} 0 {W} {bh} 0 {bh}{{\\p0}}")
    return head + "\n".join(ev) + "\n"


def default_title(text: str, limit: int = 58) -> str:
    """Titre sans IA : la première phrase ; si elle est trop longue, coupée sur une virgule plutôt qu'au milieu d'un
    groupe de mots, et jamais sur un mot de liaison."""
    first = re.split(r"(?<=[.!?…])\s", text.strip(), maxsplit=1)[0]
    if len(first) <= limit:
        return first.rstrip(".")
    head = first[:limit]
    k = max(head.rfind(", "), head.rfind("; "), head.rfind(": "), head.rfind(" — "))
    if k >= 0.45 * limit:
        return head[:k].rstrip(" ,;:—")
    words = head.rsplit(" ", 1)[0].split()
    while len(words) > 3 and re.sub(r"[^\w']", "", words[-1].lower()) in L.DANGLING_END:
        words.pop()
    return " ".join(words).rstrip(",;:") + "…"
