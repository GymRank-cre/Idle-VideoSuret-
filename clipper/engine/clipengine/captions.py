"""Sous-titres ASS « karaoké » : pages de quelques mots, mot actif en surbrillance avec effet pop."""
from __future__ import annotations

import re

from .config import Config
from .lexicon import TERMINAL
from .models import Word


def _ass_color(hex_rgb: str) -> str:
    h = hex_rgb.lstrip("#")
    r, g, b = h[0:2], h[2:4], h[4:6]
    return f"&H{b}{g}{r}&".upper()


def _t(sec: float) -> str:
    sec = max(0.0, sec)
    cs = int(round(sec * 100))
    return f"{cs // 360000}:{cs // 6000 % 60:02d}:{cs // 100 % 60:02d}.{cs % 100:02d}"


def _esc(text: str) -> str:
    return text.replace("\\", "").replace("{", "(").replace("}", ")").replace("\n", " ")


def group_pages(words: list[Word], max_words: int = 3, max_chars: int = 20, gap: float = 0.6) -> list[list[Word]]:
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


def build_ass(words: list[Word], clip_start: float, clip_end: float, cfg: Config,
              title: str = "") -> str:
    """`words` en temps source ; la sortie est en temps relatif au clip."""
    W, H = cfg.out_w, cfg.out_h
    dur = clip_end - clip_start
    scale = W / 1080
    size = int(cfg.caption_size * scale)
    col, hi = _ass_color(cfg.caption_color), _ass_color(cfg.highlight_color)
    rel = [Word(w.text, w.start - clip_start, w.end - clip_start, w.prob)
           for w in words if w.end > clip_start and w.start < clip_end]

    head = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {W}
PlayResY: {H}
WrapStyle: 2
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Cap,{cfg.caption_font},{size},{col},{col},&H00000000&,&H96000000&,-1,0,0,0,100,100,0,0,1,{round(7 * scale)},{round(3 * scale)},5,{round(60 * scale)},{round(60 * scale)},0,1
Style: Hook,{cfg.caption_font},{int(66 * scale)},&H00FFFFFF&,&H00FFFFFF&,&H00000000&,&HC8000000&,-1,0,0,0,100,100,0,0,3,{round(16 * scale)},0,8,{round(70 * scale)},{round(70 * scale)},{round(190 * scale)},1
Style: Bar,{cfg.caption_font},20,{hi},{hi},{hi},{hi},0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
    ev: list[str] = []
    if cfg.captions:
        pages = group_pages(rel, cfg.caption_words)
        px, py = W // 2, int(H * cfg.caption_y)
        for pi, page in enumerate(pages):
            page_end = page[-1].end
            if pi + 1 < len(pages) and pages[pi + 1][0].start - page_end < 0.4:
                page_end = pages[pi + 1][0].start               # pas de clignotement entre deux pages
            else:
                page_end = min(page_end + 0.15, dur)
            for wi, w in enumerate(page):
                s = w.start if wi else min(w.start, page[0].start)
                e = page[wi + 1].start if wi + 1 < len(page) else page_end
                if e <= s:
                    e = s + 0.05
                parts = []
                for wj, x in enumerate(page):
                    txt = _esc(x.text.upper() if cfg.caption_upper else x.text)
                    if wj == wi:
                        parts.append(f"{{\\c{hi}\\fscx88\\fscy88\\t(0,90,\\fscx112\\fscy112)\\t(90,180,\\fscx100\\fscy100)}}{txt}{{\\c{col}\\fscx100\\fscy100}}")
                    else:
                        parts.append(txt)
                line = " ".join(parts)
                ev.append(f"Dialogue: 1,{_t(s)},{_t(e)},Cap,,0,0,0,,{{\\an5\\pos({px},{py})\\fad(0,0)}}{line}")

    if cfg.title_overlay and title:
        end = min(cfg.title_seconds, dur)
        ev.append(f"Dialogue: 2,{_t(0)},{_t(end)},Hook,,0,0,0,,{{\\fad(220,260)}}{wrap(_esc(title), 22)}")

    if cfg.progress_bar and dur > 0:
        bh = max(6, round(14 * scale))
        y0 = H - bh
        ms = int(dur * 1000)
        ev.append(
            f"Dialogue: 3,{_t(0)},{_t(dur)},Bar,,0,0,0,,{{\\an7\\pos(0,{y0})\\bord0\\shad0\\1c{hi}"
            f"\\clip(0,{y0},0,{H})\\t(0,{ms},\\clip(0,{y0},{W},{H}))\\p1}}m 0 0 l {W} 0 {W} {bh} 0 {bh}{{\\p0}}")
    return head + "\n".join(ev) + "\n"


def default_title(text: str, limit: int = 58) -> str:
    first = re.split(r"(?<=[.!?…])\s", text.strip(), maxsplit=1)[0]
    if len(first) <= limit:
        return first.rstrip(".")
    cut = first[:limit].rsplit(" ", 1)[0]
    return cut.rstrip(",;:") + "…"
