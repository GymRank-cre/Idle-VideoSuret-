"""Phrases, frontières de sujet, génération et calage des candidats."""
from __future__ import annotations

import re
from collections import Counter
from math import sqrt

import numpy as np

from .audio import AudioProfile
from .config import Config
from .lexicon import DANGLING_END, TERMINAL, content_tokens
from .models import Candidate, Sentence, Word


def _dur(ws: list[Word]) -> float:
    return ws[-1].end - ws[0].start


def _is_soft_point(ws: list[Word], k: int, min_gap: float) -> bool:
    """Peut-on couper après ws[k] sans laisser une idée en suspens ?"""
    w = ws[k]
    if k + 1 >= len(ws):
        return False
    if re.sub(r"[^\w']", "", w.text.lower()) in DANGLING_END:
        return False
    return w.text[-1] in ",;:" or (ws[k + 1].start - w.end) >= min_gap


def _split_long(ws: list[Word], max_len: float) -> list[Sentence]:
    """Phrase fleuve : recoupée au dernier point de respiration avant `max_len`, jamais juste après « et », « the »…

    Les morceaux de gauche sont marqués `soft_end` : une coupe de clip n'y est pas souhaitable.
    """
    out: list[Sentence] = []
    while _dur(ws) > max_len and len(ws) > 3:
        k = None
        for min_gap in (0.35, 0.2, 0.1):
            cands = [i for i in range(len(ws) - 1) if ws[i].end - ws[0].start <= max_len and _is_soft_point(ws, i, min_gap)]
            if cands:
                k = cands[-1]
                break
        if k is None:
            break
        out.append(Sentence(ws[:k + 1], soft_end=True))
        ws = ws[k + 1:]
    out.append(Sentence(ws))
    return out


def build_sentences(words: list[Word], gap: float = 0.8, max_len: float = 14.0) -> list[Sentence]:
    """Phrases à partir des mots, en tenant compte des artefacts de la transcription.

    1. coupe sur ponctuation finale ou pause longue ;
    2. recolle les miettes (« else. », « And » seul) ;
    3. recoupe les phrases fleuves à un vrai point de respiration (voir `_split_long`) ;
    4. marque `soft_end` toute frontière douteuse : coupe de force, ou point suivi d'une minuscule sans pause
       (« …Kennedy. so we'll… » : Whisper ponctue à la fin de chaque morceau de voix, même au milieu d'une phrase).
       Ces frontières restent des frontières, mais un clip qui s'y arrête ou y commence est pénalisé.
    """
    chunks: list[list[Word]] = []
    cur: list[Word] = []
    for i, w in enumerate(words):
        cur.append(w)
        nxt = words[i + 1] if i + 1 < len(words) else None
        if nxt is None or w.text[-1] in TERMINAL or (nxt.start - w.end) > gap:
            chunks.append(cur)
            cur = []
    merged: list[list[Word]] = []
    for ch in chunks:
        if merged:
            prev = merged[-1]
            g = ch[0].start - prev[-1].end
            crumb = len(ch) <= 2 or _dur(ch) < 0.9
            lone_prefix = len(prev) <= 2 and prev[-1].text[-1] not in TERMINAL     # « And » seul devant sa phrase
            if g < gap and (crumb or lone_prefix):
                prev.extend(ch)
                continue
        merged.append(list(ch))
    out: list[Sentence] = []
    for ch in merged:
        out.extend(_split_long(ch, max_len))
    for a, b in zip(out, out[1:]):
        # une minuscule en tête de phrase signale une suite d'idée, même après une pause d'hésitation
        if not a.soft_end and b.words[0].start - a.words[-1].end < 2.0 and b.words[0].text[:1].islower():
            a.soft_end = True
    return out


def _cos(a: Counter, b: Counter) -> float:
    if not a or not b:
        return 0.0
    num = sum(v * b.get(k, 0) for k, v in a.items())
    return num / (sqrt(sum(v * v for v in a.values())) * sqrt(sum(v * v for v in b.values())) + 1e-9)


def boundary_strengths(sents: list[Sentence], win: int = 3, reach: int = 4) -> np.ndarray:
    """Force du changement de sujet après chaque phrase (0 = même sujet, 1 = rupture nette).

    TextTiling : similarité cosinus entre les `win` phrases à gauche et à droite de chaque pause, puis score de
    profondeur = de combien cette similarité s'effondre par rapport aux pics voisins (dans `reach` pauses).
    """
    n = len(sents)
    bags = [Counter(content_tokens(s.text)) for s in sents]
    sim = np.zeros(max(n - 1, 0))
    for i in range(n - 1):
        left, right = Counter(), Counter()
        for k in range(max(0, i - win + 1), i + 1):
            left.update(bags[k])
        for k in range(i + 1, min(n, i + 1 + win)):
            right.update(bags[k])
        sim[i] = _cos(left, right)
    depth = np.zeros(n)
    for i in range(len(sim)):
        lp = sim[max(0, i - reach):i + 1].max()
        rp = sim[i:i + reach + 1].max()
        depth[i] = ((lp - sim[i]) + (rp - sim[i])) / 2
    d = depth[:-1]
    # seuil adaptatif (TextTiling) : moyenne + σ/2 vaut 0,5 ; le double vaut 1
    thr = (d.mean() + 0.5 * d.std()) if len(d) else 0.0
    return np.clip(depth / max(0.12, 2 * thr), 0.0, 1.0)


def generate(sents: list[Sentence], cfg: Config) -> list[tuple[int, int]]:
    """Toutes les fenêtres de phrases entières dont la durée tient dans [min_dur, max_dur]."""
    spans: list[tuple[int, int]] = []
    for i in range(len(sents)):
        t0 = sents[i].start
        for j in range(i, len(sents)):
            d = sents[j].end - t0
            if d > cfg.max_dur:
                break
            if d >= cfg.min_dur:
                spans.append((i, j))
    return spans


def snap(sents: list[Sentence], lo: int, hi: int, prof: AudioProfile | None, cfg: Config) -> tuple[float, float]:
    """Cale début/fin : marge de respiration, jamais dans le mot voisin, coupe au point le plus silencieux."""
    first, last = sents[lo].words[0], sents[hi].words[-1]
    prev_end = sents[lo - 1].end if lo > 0 else 0.0
    next_start = sents[hi + 1].start if hi + 1 < len(sents) else last.end + cfg.pad_after + 1.0

    a_min = max(prev_end + 0.03, first.start - 0.40)
    a_max = max(a_min, first.start - 0.02)
    b_min = min(last.end + 0.05, next_start - 0.03)
    b_max = max(b_min, min(last.end + 0.55, next_start - 0.03))

    if prof is not None and len(prof.db):
        a = prof.quietest(a_min, a_max)
        b = prof.quietest(b_min, b_max)
        # garde au moins une partie de la marge voulue côté fin pour ne pas couper la dernière syllabe
        b = max(b, min(last.end + 0.12, b_max))
    else:
        a = max(a_min, first.start - cfg.pad_before)
        b = min(b_max, last.end + cfg.pad_after)
    return float(a), float(max(b, last.end + 0.05))


def make_candidates(sents: list[Sentence], prof: AudioProfile | None, cfg: Config) -> list[Candidate]:
    out = []
    for lo, hi in generate(sents, cfg):
        a, b = snap(sents, lo, hi, prof, cfg)
        text = " ".join(s.text for s in sents[lo:hi + 1])
        out.append(Candidate(start=a, end=b, lo=lo, hi=hi, text=text))
    return out


def split_full(sents: list[Sentence], prof: AudioProfile | None, cfg: Config) -> list[Candidate]:
    """Mode « tout le long » : la vidéo entière, dans l'ordre, en parties proches de `target_dur`.

    Chaque coupe tombe sur une fin de phrase ; entre plusieurs fins possibles on préfère un vrai changement de
    sujet et une pause. Un reste trop court est rattaché à la partie précédente quand la durée le permet.
    """
    n = len(sents)
    if n == 0:
        return []
    bs = boundary_strengths(sents)
    spans: list[list[int]] = []
    i = 0
    while i < n:
        t0 = sents[i].start
        best: tuple[float, int] | None = None
        for j in range(i, n):
            d = sents[j].end - t0
            if d > cfg.max_dur and j > i:
                break
            if d >= cfg.min_dur or j == n - 1:
                pause = (sents[j + 1].start - sents[j].end) if j + 1 < n else 2.0
                score = (-abs(d - cfg.target_dur) / cfg.target_dur + 0.35 * float(bs[j]) + 0.1 * min(pause, 1.5)
                         - (0.6 if sents[j].soft_end else 0.0))
                if best is None or score > best[0]:
                    best = (score, j)
        j = best[1] if best else i
        spans.append([i, j])
        i = j + 1
    if len(spans) > 1:
        last = spans[-1]
        if sents[last[1]].end - sents[last[0]].start < cfg.min_dur:
            prev = spans[-2]
            if sents[last[1]].end - sents[prev[0]].start <= cfg.max_dur * 1.1:
                prev[1] = last[1]
                spans.pop()
    out = []
    for lo, hi in spans:
        a, b = snap(sents, lo, hi, prof, cfg)
        out.append(Candidate(start=a, end=b, lo=lo, hi=hi, text=" ".join(s.text for s in sents[lo:hi + 1])))
    return out
