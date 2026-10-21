"""Réévaluation par Claude (optionnelle) : note éditoriale, titre, accroche. Sans dépendance externe."""
from __future__ import annotations

import json
import os
import re
import time
import urllib.error
import urllib.request

from .config import Config
from .models import Candidate

API = "https://api.anthropic.com/v1/messages"

SYSTEM = """Tu es monteur en chef de contenus courts verticaux (TikTok, Reels, Shorts).
On te donne des extraits d'une vidéo longue. Pour chacun, juge s'il fonctionnerait SEUL comme short :
- accroche : les 3 premières secondes donnent-elles envie de rester ?
- autonomie : se comprend sans le reste de la vidéo (pas de « il », « ça » sans référent) ;
- tension / valeur : une idée forte, une histoire, une émotion, une révélation ;
- chute : la fin conclut ou fait réagir.
Réponds UNIQUEMENT par un tableau JSON, un objet par extrait :
{"id": <int>, "score": <0-100>, "title": "<titre court, 60 car. max, dans la langue de l'extrait>",
 "hook": "<texte d'accroche à afficher en haut, 6 mots max>", "why": "<une phrase>"}"""


def available(cfg: Config) -> bool:
    return cfg.llm != "none" and bool(os.environ.get("ANTHROPIC_API_KEY"))


def _call(cfg: Config, user: str, retries: int = 2) -> str:
    body = json.dumps({
        "model": cfg.llm_model,
        "max_tokens": 4000,
        "system": SYSTEM,
        "messages": [{"role": "user", "content": user}],
    }).encode()
    req = urllib.request.Request(API, data=body, headers={
        "x-api-key": os.environ["ANTHROPIC_API_KEY"],
        "anthropic-version": "2023-06-01",
        "content-type": "application/json",
    })
    for k in range(retries + 1):
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                data = json.load(r)
            return "".join(b.get("text", "") for b in data.get("content", []))
        except (urllib.error.URLError, TimeoutError):
            if k == retries:
                raise
            time.sleep(2 ** (k + 1))
    return ""


def parse_json_array(text: str) -> list[dict]:
    m = re.search(r"\[.*\]", text, re.S)
    if not m:
        return []
    try:
        arr = json.loads(m.group(0))
    except json.JSONDecodeError:
        return []
    return [x for x in arr if isinstance(x, dict) and "id" in x]


def rerank(cands: list[Candidate], cfg: Config, call=_call) -> list[Candidate]:
    """Note les `llm_top` meilleurs candidats et mélange avec le score heuristique.

    En cas d'erreur réseau/format, on garde silencieusement le score heuristique.
    """
    top = sorted(cands, key=lambda c: c.heuristic, reverse=True)[: cfg.llm_top]
    if not top:
        return cands
    prompt = "\n\n".join(
        f"### Extrait {i} ({c.duration:.0f}s)\n{c.text[:1200]}" for i, c in enumerate(top)
    )
    try:
        rows = parse_json_array(call(cfg, prompt))
    except Exception:
        return cands
    w = cfg.llm_weight
    for r in rows:
        try:
            c = top[int(r["id"])]
            c.llm_score = float(max(0, min(100, float(r.get("score", 0)))))
        except (ValueError, IndexError, TypeError):
            continue
        c.title = str(r.get("title", ""))[:80]
        c.hook = str(r.get("hook", ""))[:60]
        c.why = str(r.get("why", ""))[:240]
        c.score = (1 - w) * c.heuristic + w * c.llm_score
    # les candidats non évalués ne doivent pas passer devant les évalués sur la foi du seul heuristique
    scored = [c.llm_score for c in top if c.llm_score is not None]
    if scored:
        for c in cands:
            if c.llm_score is None:
                c.score = min(c.heuristic, c.heuristic * 0.85)
    return cands
