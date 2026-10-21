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


def select_ranked(cands: list[Candidate], cfg: Config, limit: int, min_score: float | None = None) -> list[Candidate]:
    """Glouton par score décroissant ; ordre de sortie = rang (meilleur d'abord).

    Un recouvrement ou une redite (même sujet) avec un clip déjà retenu est écarté.
    """
    thr = cfg.min_score if min_score is None else min_score
    chosen: list[Candidate] = []
    bags: list[Counter] = []
    for c in sorted(cands, key=lambda x: x.score, reverse=True):
        if c.score < thr or len(chosen) >= limit:
            break
        bag = Counter(L.content_tokens(c.text))
        if any(_overlap(c, o) > cfg.max_overlap for o in chosen):
            continue
        if any(_sim(bag, b) > cfg.max_similarity for b in bags):
            continue
        chosen.append(c)
        bags.append(bag)
    return chosen


def select(cands: list[Candidate], cfg: Config) -> list[Candidate]:
    """Les `max_clips` meilleurs au-dessus du seuil, dans l'ordre chronologique."""
    return sorted(select_ranked(cands, cfg, cfg.max_clips), key=lambda x: x.start)


def propose(cands: list[Candidate], cfg: Config) -> tuple[list[Candidate], int]:
    """Moments classés et nombre à fabriquer d'office.

    Mieux vaut peu de clips solides que beaucoup de clips mous : seuls les moments au-dessus de `min_score` (dans la
    limite de `max_clips`) sont fabriqués d'office ; les suivants, jusqu'à `reserve_min_score`, restent en réserve.
    """
    ranked = select_ranked(cands, cfg, cfg.max_clips + cfg.reserve, min(cfg.min_score, cfg.reserve_min_score))
    n_auto = 0
    for c in ranked[:cfg.max_clips]:
        if c.score < cfg.min_score:
            break
        n_auto += 1
    return ranked, n_auto


def distinct_pool(cands: list[Candidate], k: int, max_overlap: float = 0.2) -> list[Candidate]:
    """Les k meilleurs moments *distincts* d'après l'heuristique.

    Sans cela, les fenêtres qui ne diffèrent que d'une phrase occupent tout le classement et l'avis du LLM
    porte sur trois moments répétés au lieu de k moments différents.
    """
    out: list[Candidate] = []
    for c in sorted(cands, key=lambda x: x.heuristic, reverse=True):
        if len(out) >= k:
            break
        if all(_overlap(c, o) <= max_overlap for o in out):
            out.append(c)
    return out
