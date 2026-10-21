"""Score heuristique d'un moment : accroche, chute, rythme, énergie vocale, cohésion, durée."""
from __future__ import annotations

import math
import re

import numpy as np

from . import lexicon as L
from .audio import AudioProfile
from .candidates import boundary_strengths
from .config import Config
from .models import Candidate, Sentence

# poids du score final (somme = 1)
WEIGHTS = {
    "hook": 0.22,
    "payoff": 0.11,
    "pace": 0.08,
    "dynamics": 0.10,
    "emotion": 0.09,
    "novelty": 0.06,
    "duration": 0.09,
    "cohesion": 0.15,
    "edges": 0.10,
}

LABELS = {
    "hook": ("accroche forte dès la première phrase", "début faible ou sans contexte"),
    "payoff": ("fin nette qui conclut", "fin qui s'éteint ou reste en suspens"),
    "pace": ("débit de parole dynamique", "débit trop lent ou trop haché"),
    "dynamics": ("voix expressive", "voix monotone"),
    "emotion": ("vocabulaire marquant", "peu de mots forts"),
    "novelty": ("propos dense, peu de remplissage", "beaucoup de remplissage"),
    "duration": ("durée idéale", "durée peu adaptée au format"),
    "cohesion": ("un seul sujet du début à la fin", "plusieurs sujets mélangés"),
    "edges": ("démarre et finit sur un changement de sujet", "coupe au milieu d'une idée"),
}


CRITERIA = {
    "hook": "Accroche", "payoff": "Chute", "pace": "Rythme", "dynamics": "Voix expressive",
    "emotion": "Mots forts", "novelty": "Densité", "duration": "Durée", "cohesion": "Un seul sujet",
    "edges": "Coupes propres",
}


def label_for(score: float) -> str:
    """Étiquette lisible. Un avis sur le contenu : elle ne prédit ni vues ni abonnés."""
    return "Excellent" if score >= 80 else "Fort" if score >= 65 else "Correct" if score >= 50 else "Faible"


def breakdown(f: dict[str, float]) -> list[dict]:
    """Détail de la note : valeur 0-1 de chaque critère et son poids, du plus fort au plus faible."""
    rows = [{"key": k, "label": CRITERIA[k], "value": round(float(f.get(k, 0.0)), 3), "weight": WEIGHTS[k]}
            for k in WEIGHTS]
    return sorted(rows, key=lambda r: r["value"], reverse=True)


def _clip01(x: float) -> float:
    return max(0.0, min(1.0, x))


def hook_score(first: Sentence, second: Sentence | None) -> float:
    text = first.text.strip()
    toks = L.tokens(text)
    if not toks:
        return 0.0
    s = 0.25
    low = text.lower()
    if text.endswith("?") or toks[0] in L.QUESTION_START:
        s += 0.30
    if any(p.search(low) for p in L.OPENERS_RE):
        s += 0.28
    if re.search(r"\d", text) or (second and re.search(r"\d", second.text)):
        s += 0.12
    s += min(0.24, 0.12 * sum(t in L.POWER for t in toks))
    if 4 <= len(toks) <= 13:
        s += 0.10
    elif len(toks) > 24:
        s -= 0.10
    if toks[0] in L.DANGLING:
        s -= 0.35
    elif toks[0] in L.ANAPHORIC:
        s -= 0.18
    if sum(t in L.FILLERS for t in toks[:3]):
        s -= 0.12
    return _clip01(s)


def payoff_score(last: Sentence) -> float:
    text = last.text.strip()
    toks = L.tokens(text)
    if not toks:
        return 0.0
    s = 0.40
    if text.endswith("?"):
        s -= 0.25                       # se termine sur une question restée sans réponse
    elif text.endswith((".", "!", "…")):
        s += 0.15
    if 3 <= len(toks) <= 16:
        s += 0.20
    if any(t in L.POWER for t in toks) or re.search(r"\d", text):
        s += 0.15
    unfinished = not text.endswith((".", "!", "?", "…"))
    if toks[-1] in L.DANGLING or (unfinished and toks[-1] in L.STOP and len(toks) > 3):
        s -= 0.25
    return _clip01(s)


def pace_score(n_words: int, speech_time: float) -> float:
    if speech_time <= 0:
        return 0.0
    wps = n_words / speech_time
    return math.exp(-(((wps - 2.9) / 1.1) ** 2))


def emotion_score(toks: list[str], prof: AudioProfile | None, a: float, b: float) -> float:
    dens = sum(t in L.POWER for t in toks) / max(len(toks), 1)
    s = 1 - math.exp(-dens * 40)         # ~2.5 % de mots forts => 0.63
    if prof is not None:
        s = 0.7 * s + 0.3 * _clip01(prof.peakiness(a, b) / 14.0)
    return _clip01(s)


def novelty_score(toks: list[str], text: str) -> float:
    if len(toks) < 5:
        return 0.0
    content = [t for t in toks if t not in L.STOP]
    ttr = len(set(content)) / max(len(content), 1)
    fill = sum(t in L.FILLERS for t in toks) + sum(text.lower().count(p) for p in L.FILLER_PHRASES)
    return _clip01(0.35 + 0.75 * ttr - 4.0 * fill / len(toks))


def duration_score(d: float, cfg: Config) -> float:
    return math.exp(-(((d - cfg.target_dur) / cfg.dur_spread) ** 2))


def score_candidates(cands: list[Candidate], sents: list[Sentence], prof: AudioProfile | None,
                     cfg: Config) -> list[Candidate]:
    bs = boundary_strengths(sents)
    for c in cands:
        ss = sents[c.lo:c.hi + 1]
        words = [w for s in ss for w in s.words]
        toks = L.tokens(c.text)
        a, b = c.start, c.end

        # parole effective et plus longue pause interne
        gaps = [words[i + 1].start - words[i].end for i in range(len(words) - 1)]
        longest_gap = max(gaps, default=0.0)
        speech_time = max(1e-3, (words[-1].end - words[0].start) - sum(g for g in gaps if g > 0.35))

        internal = bs[c.lo:c.hi] if c.hi > c.lo else np.zeros(0)
        before = bs[c.lo - 1] if c.lo > 0 else 1.0
        after = bs[c.hi] if c.hi < len(bs) - 1 else 1.0

        f = {
            "hook": hook_score(ss[0], ss[1] if len(ss) > 1 else None),
            "payoff": payoff_score(ss[-1]),
            "pace": pace_score(len(words), speech_time),
            "dynamics": _clip01(math.tanh((prof.dynamics(a, b) - 3.0) / 4.0) if prof is not None else 0.5),
            "emotion": emotion_score(toks, prof, a, b),
            "novelty": novelty_score(toks, c.text),
            "duration": duration_score(c.duration, cfg),
            "cohesion": _clip01(1.0 - (0.55 * float(np.sum(internal >= 0.7)) + 0.8 * float(np.mean(internal))
                                       if len(internal) else 0.0)),
            "edges": _clip01(0.5 * float(before) + 0.5 * float(after)),
        }
        # mélanger deux sujets ruine un clip quelles que soient ses autres qualités : effet multiplicatif
        score = sum(WEIGHTS[k] * v for k, v in f.items()) * (0.6 + 0.4 * f["cohesion"]) * 100 / 0.82
        penalty = 0.0
        if longest_gap > 1.4:
            penalty += min(12.0, (longest_gap - 1.4) * 6)      # temps mort au milieu
        f["longest_gap"] = longest_gap
        c.features = f
        c.heuristic = max(0.0, min(100.0, score - penalty))
        c.score = c.heuristic
        c.reasons = explain(f, penalty)
    return cands


def explain(f: dict[str, float], penalty: float = 0.0) -> list[str]:
    """Deux atouts et un point faible, en français."""
    ranked = sorted(WEIGHTS, key=lambda k: f[k] * WEIGHTS[k] ** 0.5, reverse=True)
    good = [LABELS[k][0] for k in ranked if f[k] >= 0.6][:2]
    bad = [LABELS[k][1] for k in sorted(WEIGHTS, key=lambda k: f[k]) if f[k] < 0.35][:1]
    out = good + ["⚠ " + b for b in bad]
    if penalty > 0:
        out.append("⚠ temps mort au milieu")
    return out
