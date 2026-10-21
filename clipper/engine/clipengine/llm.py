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
from .select import distinct_pool

API = "https://api.anthropic.com/v1/messages"

SYSTEM = """Tu es monteur en chef de contenus courts verticaux (TikTok, Reels, Shorts).
On te donne des extraits d'une vidéo longue. Pour chacun, juge s'il fonctionnerait SEUL comme short :
- accroche : les 3 premières secondes donnent-elles envie de rester ?
- autonomie : se comprend sans le reste de la vidéo (pas de « il », « ça » sans référent) ;
- valeur : punchline, opinion tranchée, info surprenante, histoire forte, conseil concret, chiffre choc ;
- chute : la fin conclut ou fait réagir.
Pénalise les intros molles, remerciements, transitions et digressions.
Réponds UNIQUEMENT par un tableau JSON, un objet par extrait :
{"id": <int>, "score": <0-100>, "title": "<titre court, 60 car. max, dans la langue de l'extrait>",
 "hook": "<texte d'accroche à afficher en haut, 6 mots max>", "why": "<une phrase>",
 "caption": "<légende de publication, 1 à 2 phrases, 150 car. max, sans hashtags>",
 "hashtags": ["#mot", "..."]}   (3 à 5 hashtags, dans la langue de l'extrait)"""


class LLMError(Exception):
    """Appel à Claude impossible ou réponse inexploitable : le message est lisible tel quel par l'utilisateur."""


def available(cfg: Config) -> bool:
    return cfg.llm != "none" and bool(os.environ.get("ANTHROPIC_API_KEY"))


def explain_http_error(code: int, body: str, model: str = "") -> str:
    """Cause lisible d'un refus de l'API (sans quoi une clé fausse donne des clips « normaux » sans que rien n'alerte)."""
    try:
        err = json.loads(body).get("error", {})
        kind, msg = str(err.get("type", "")), str(err.get("message", ""))
    except (ValueError, AttributeError):
        kind, msg = "", body[:160]
    low = msg.lower()
    if code == 401 or kind == "authentication_error":
        return "clé refusée (ANTHROPIC_API_KEY invalide ou révoquée)"
    if "credit balance" in low or "billing" in low:
        return "crédit insuffisant sur le compte Anthropic"
    if code == 403 or kind == "permission_error":
        return "la clé n'a pas le droit d'utiliser ce modèle"
    if code == 404 or kind == "not_found_error":
        return f"modèle introuvable ({model or 'llm_model'})"
    if code == 429 or kind == "rate_limit_error":
        return "limite de débit atteinte, réessayer plus tard"
    if code >= 500 or kind in ("api_error", "overloaded_error"):
        return "service Claude surchargé ou en panne, réessayer plus tard"
    return f"requête refusée ({code}) : {msg[:120]}" if msg else f"requête refusée ({code})"


RETRY_CODES = {429, 500, 502, 503, 504, 529}


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
        except urllib.error.HTTPError as e:
            if e.code in RETRY_CODES and k < retries:
                time.sleep(2 ** (k + 1))
                continue
            raise LLMError(explain_http_error(e.code, e.read().decode("utf-8", "replace"), cfg.llm_model)) from e
        except (urllib.error.URLError, TimeoutError, OSError) as e:
            if k < retries:
                time.sleep(2 ** (k + 1))
                continue
            raise LLMError(f"réseau injoignable ({type(e).__name__})") from e
    return ""


def check(cfg: Config | None = None) -> tuple[bool, str]:
    """Un petit appel réel : dit si la clé fonctionne avec le modèle choisi, et pourquoi sinon."""
    cfg = cfg or Config()
    if not os.environ.get("ANTHROPIC_API_KEY"):
        return False, "ANTHROPIC_API_KEY absente de l'environnement"
    sample = ("### Extrait 0 (30s)\nPourquoi personne ne vous dit la vérité sur le sommeil ? Parce que la plupart des "
              "gens dorment mal sans le savoir, et cela abîme leur santé à long terme.")
    try:
        rows = parse_json_array(_call(cfg, sample, retries=0))
    except LLMError as e:
        return False, str(e)
    if not rows:
        return False, "l'API répond, mais pas par le tableau JSON attendu"
    return True, f"{cfg.llm_model} répond (note donnée à l'extrait d'essai : {rows[0].get('score')})"


def parse_json_array(text: str) -> list[dict]:
    m = re.search(r"\[.*\]", text, re.S)
    if not m:
        return []
    try:
        arr = json.loads(m.group(0))
    except json.JSONDecodeError:
        return []
    return [x for x in arr if isinstance(x, dict) and "id" in x]


def _tags(raw) -> list[str]:
    out: list[str] = []
    for t in raw if isinstance(raw, list) else []:
        t = re.sub(r"[^\w]", "", str(t), flags=re.UNICODE)
        if 1 < len(t) <= 30 and "#" + t not in out:
            out.append("#" + t)
    return out[:6]


def rerank(cands: list[Candidate], cfg: Config, call=_call, on_error=None) -> list[Candidate]:
    """Note les `llm_top` meilleurs moments *distincts* et mélange avec le score heuristique.

    En cas d'erreur réseau/format, on garde le score heuristique ; `on_error(message)` dit pourquoi.
    """
    top = distinct_pool(cands, cfg.llm_top)
    if not top:
        return cands
    prompt = "\n\n".join(
        f"### Extrait {i} ({c.duration:.0f}s)\n{c.text[:1200]}" for i, c in enumerate(top)
    )
    fail = on_error or (lambda m: None)
    try:
        rows = parse_json_array(call(cfg, prompt))
    except Exception as e:
        fail(str(e) if isinstance(e, LLMError) else f"appel impossible ({type(e).__name__})")
        return cands
    if not rows:
        fail("réponse illisible (tableau JSON attendu)")
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
        c.caption = str(r.get("caption", ""))[:200]
        c.hashtags = _tags(r.get("hashtags"))
        c.score = (1 - w) * c.heuristic + w * c.llm_score
    judged = [c for c in top if c.llm_score is not None]
    if judged:
        # Seuls comptent les moments dont Claude a lu le texte exact : une variante non jugée du même passage (une phrase
        # de plus ou de moins) ne repasse pas devant sur la foi du seul score du moteur — et ne peut pas hériter du
        # titre ni de la note d'une fenêtre dont elle n'a pas toute la substance (essai réel : titre « Le Google du
        # Libre » sur un clip qui s'arrête juste avant cette phrase).
        floor = min(c.score for c in judged) - 0.01
        for c in cands:
            if c.llm_score is None:
                c.score = min(c.heuristic * 0.85, floor)
    return cands
