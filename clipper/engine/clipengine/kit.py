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
