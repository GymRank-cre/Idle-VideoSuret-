"""Kit de publication sans LLM : titre, légende et hashtags à partir du texte du clip."""
from __future__ import annotations

import re
from collections import Counter

from . import lexicon as L

_MIN_LEN = 4

# mots fréquents mais sans valeur comme hashtag, en plus des mots vides généraux
_HASHTAG_STOP = set("""
pourquoi comment quand personne rien tout tous toute toutes très alors encore déjà jamais toujours voici voilà chose
choses parce depuis pendant après avant peut peuvent faut dit disent cela quoi comme ainsi bien même mais donc car
leur leurs notre votre mes tes ses moi toi lui eux elles ils elle nous vous cette ces cet celui celle ceux
really just like going what with that this have been will would could should there their about because
""".split())


def keywords(text: str, n: int = 4) -> list[str]:
    """Mots porteurs de sens les plus fréquents (formes entières, pour en faire des hashtags)."""
    toks = [t for t in L.tokens(text) if len(t) >= _MIN_LEN and t not in L.STOP and t not in L.FILLERS and t not in _HASHTAG_STOP
            and not t.isdigit()]
    freq = Counter(toks)
    ranked = sorted(freq, key=lambda t: (-freq[t], -len(t), t))
    return ranked[:n]


def hashtags(text: str, n: int = 4) -> list[str]:
    out = []
    for w in keywords(text, n):
        tag = "#" + re.sub(r"[^\w]", "", w, flags=re.UNICODE)
        if len(tag) > 2 and tag not in out:
            out.append(tag)
    return out


def caption(title: str, limit: int = 110) -> str:
    t = re.sub(r"\s+", " ", title).strip()
    return t if len(t) <= limit else t[:limit].rsplit(" ", 1)[0].rstrip(",;:") + "…"


def split_sentences(text: str) -> list[str]:
    return [x.strip() for x in re.split(r"(?<=[.!?…])\s+", text.strip()) if x.strip()]


def pick_hook(text: str, min_words: int = 3, max_words: int = 12, max_chars: int = 64) -> str:
    """Accroche d'un clip : une phrase **entière** et courte du clip, jamais une phrase tronquée.

    Préfère une question, un chiffre, des mots forts, une phrase qui arrive tôt. Sans phrase convenable, renvoie ""
    (mieux vaut pas d'accroche qu'une accroche coupée au milieu d'un mot).
    """
    best, best_sc = "", -1.0
    for idx, s in enumerate(split_sentences(text)[:8]):
        toks = L.tokens(s)
        n = len(toks)
        if not (min_words <= n <= max_words) or len(s) > max_chars or s[-1] not in ".!?…" or s[0].islower():
            continue
        if toks[0] in L.DANGLING or toks[0] in L.ANAPHORIC or toks[-1] in L.DANGLING_END and s[-1] == ",":
            continue
        sc = (0.35 * s.endswith("?") + 0.25 * bool(re.search(r"\d", s)) + 0.15 * min(2, sum(t in L.POWER for t in toks))
              + 0.10 * (1 - idx / 8) + 0.10 * (n <= 7))
        if sc > best_sc + 1e-9:                      # à égalité, la plus précoce
            best, best_sc = s, sc
    return best.rstrip(".") if best else ""
