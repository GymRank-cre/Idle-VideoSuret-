"""Sélection finale : meilleurs scores sans chevauchement ni redites."""
from __future__ import annotations

from collections import Counter
from math import sqrt

from . import lexicon as L
from .config import Config
from .models import Candidate


def _overlap(a: Candidate, b: Candidate) -> float:
    inter = min(a.end, b.end) - max(a.start, b.start)
    return max(0.0, inter) / max(1e-6, min(a.duration, b.duration))


def _sim(a: Counter, b: Counter) -> float:
    if not a or not b:
        return 0.0
    num = sum(v * b.get(k, 0) for k, v in a.items())
    return num / (sqrt(sum(v * v for v in a.values())) * sqrt(sum(v * v for v in b.values())) + 1e-9)


def select(cands: list[Candidate], cfg: Config) -> list[Candidate]:
    """Glouton par score décroissant. Une redite (même sujet) ou un recouvrement est écarté."""
    chosen: list[Candidate] = []
    bags: list[Counter] = []
    for c in sorted(cands, key=lambda x: x.score, reverse=True):
        if c.score < cfg.min_score or len(chosen) >= cfg.max_clips:
            break
        bag = Counter(L.content_tokens(c.text))
        if any(_overlap(c, o) > cfg.max_overlap for o in chosen):
            continue
        if any(_sim(bag, b) > cfg.max_similarity for b in bags):
            continue
        chosen.append(c)
        bags.append(bag)
    return sorted(chosen, key=lambda x: x.start)
