"""Réévaluation par Claude (optionnelle) : note éditoriale, titre, accroche. Appel par le SDK officiel `anthropic`
(extra `llm`) ; les erreurs sont traduites en messages lisibles, jamais avalées."""
from __future__ import annotations

import json
import os
import re

from .config import Config
from .models import Candidate
from .select import distinct_pool

API_URL = "https://api.anthropic.com"        # fixé : ANTHROPIC_BASE_URL peut désigner la passerelle d'un autre outil
MAX_TOKENS = 16000                           # la réflexion (activée d'office sur les modèles 5.x) compte dans ce plafond
FALLBACK_BETA = "server-side-fallback-2026-07-01"
LAST_CALL: dict = {}                         # dernier appel réussi : modèle, jetons, repli de sécurité accepté ou non

SYSTEM = """Tu es monteur en chef de contenus courts verticaux (TikTok, Reels, Shorts).
On te donne des extraits d'une vidéo longue. Pour chacun, juge s'il fonctionnerait SEUL comme short :
- accroche : les 3 premières secondes donnent-elles envie de rester ?
- autonomie : se comprend sans le reste de la vidéo (pas de « il », « ça » sans référent) ;
- valeur : punchline, opinion tranchée, info surprenante, histoire forte, conseil concret, chiffre choc ;
- chute : la fin conclut ou fait réagir.
Pénalise les intros molles, remerciements, transitions et digressions.
Titre, accroche et légende restent fidèles à ce qui est dit : n'invente ni chiffre ni fait, n'exagère pas, ne prête pas
à la personne une opinion plus tranchée que la sienne (« peut-être moins jolies » ne devient pas « moches »).
Réponds UNIQUEMENT par un tableau JSON, un objet par extrait :
{"id": <int>, "score": <0-100>, "title": "<titre court, 60 car. max, dans la langue de l'extrait>",
 "hook": "<texte d'accroche à afficher en haut, 6 mots max>", "why": "<une phrase>",
 "caption": "<légende de publication, 1 à 2 phrases, 150 car. max, sans hashtags>",
 "hashtags": ["#mot", "..."]}   (3 à 5 hashtags, dans la langue de l'extrait)"""


class LLMError(Exception):
    """Appel à Claude impossible ou réponse inexploitable : le message est lisible tel quel par l'utilisateur."""


def available(cfg: Config) -> bool:
    return cfg.llm != "none" and bool(os.environ.get("ANTHROPIC_API_KEY"))


def explain_error(e: Exception, model: str = "") -> str:
    """Cause lisible d'un échec de l'API (sans quoi une clé fausse donne des clips « normaux » sans que rien n'alerte)."""
    import anthropic
    ref = f" (réf. {e.request_id})" if getattr(e, "request_id", None) else ""
    msg = str(getattr(e, "message", "") or e)
    low = msg.lower()
    if isinstance(e, anthropic.AuthenticationError):
        return "clé refusée (ANTHROPIC_API_KEY invalide, expirée ou révoquée)" + ref
    if "credit balance" in low or "billing" in low:
        return "crédit insuffisant sur le compte Anthropic" + ref
    if "anthropic-workspace-id" in low:
        return "clé non liée à un espace de travail : la recréer en la limitant à un seul espace (Default)" + ref
    if isinstance(e, anthropic.PermissionDeniedError):
        return "la clé n'a pas le droit d'utiliser ce modèle" + ref
    if isinstance(e, anthropic.NotFoundError):
        return f"modèle introuvable ({model or 'llm_model'})" + ref
    if isinstance(e, anthropic.RateLimitError):
        return "limite de débit atteinte, réessayer plus tard" + ref
    if isinstance(e, anthropic.APITimeoutError):
        return "Claude n'a pas répondu à temps"
    if isinstance(e, anthropic.APIConnectionError):
        return "réseau injoignable"
    if isinstance(e, anthropic.APIStatusError) and e.status_code >= 500:
        return "service Claude surchargé ou en panne, réessayer plus tard" + ref
    return f"requête refusée : {msg[:140]}" + ref


def _text(resp) -> str:
    """Texte de la réponse ; un refus de sécurité ou une réponse coupée sont des erreurs dites en clair."""
    if resp.stop_reason == "refusal":
        cat = getattr(getattr(resp, "stop_details", None), "category", None)
        raise LLMError("Claude a refusé d'évaluer ce contenu" + (f" (catégorie {cat})" if cat else ""))
    if resp.stop_reason == "max_tokens":
        raise LLMError("réponse coupée : limite de longueur atteinte")
    return "".join(b.text for b in resp.content if b.type == "text")


def _call(cfg: Config, user: str, retries: int = 2, max_tokens: int = MAX_TOKENS) -> str:
    try:
        import anthropic
    except ImportError as e:
        raise LLMError("le paquet « anthropic » manque : pip install 'clipengine[llm]'") from e
    client = anthropic.Anthropic(api_key=os.environ["ANTHROPIC_API_KEY"], max_retries=retries, timeout=300.0,
                                 base_url=os.environ.get("CLIPENGINE_LLM_URL", API_URL))
    args = {"model": cfg.llm_model, "max_tokens": max_tokens, "system": SYSTEM,
            "messages": [{"role": "user", "content": user}]}
    fallbacks = True
    try:
        try:   # repli côté serveur si les filtres de sécurité refusent : Anthropic choisit le modèle de secours
            resp = client.beta.messages.create(**args, betas=[FALLBACK_BETA], fallbacks="default")
        except anthropic.BadRequestError as e:
            if not re.search(r"fallback|beta", str(e), re.I):
                raise
            fallbacks = False
            resp = client.messages.create(**args)             # modèle ou compte sans repli serveur : appel simple
    except anthropic.AnthropicError as e:
        raise LLMError(explain_error(e, cfg.llm_model)) from e
    LAST_CALL.update(model=resp.model, input_tokens=resp.usage.input_tokens, output_tokens=resp.usage.output_tokens,
                     fallbacks=fallbacks)
    return _text(resp)


def usage_note() -> str:
    """Ce que le dernier appel a consommé, pour que la dépense ne soit jamais une surprise."""
    if not LAST_CALL:
        return ""
    n = lambda v: f"{v:,}".replace(",", " ")
    return (f"{LAST_CALL['model']} : {n(LAST_CALL['input_tokens'])} jetons envoyés, {n(LAST_CALL['output_tokens'])} reçus"
            f" (réflexion comprise) ; repli de sécurité {'actif' if LAST_CALL['fallbacks'] else 'indisponible'}")


def check(cfg: Config | None = None) -> tuple[bool, str]:
    """Un petit appel réel : dit si la clé fonctionne avec le modèle choisi, et pourquoi sinon."""
    cfg = cfg or Config()
    if not os.environ.get("ANTHROPIC_API_KEY"):
        return False, "ANTHROPIC_API_KEY absente de l'environnement"
    sample = ("### Extrait 0 (30s)\nPourquoi personne ne vous dit la vérité sur le sommeil ? Parce que la plupart des "
              "gens dorment mal sans le savoir, et cela abîme leur santé à long terme.")
    try:
        rows = parse_json_array(_call(cfg, sample, retries=0, max_tokens=8000))
    except LLMError as e:
        return False, str(e)
    if not rows:
        return False, "l'API répond, mais pas par le tableau JSON attendu"
    return True, f"{cfg.llm_model} répond (note donnée à l'extrait d'essai : {rows[0].get('score')}) — {usage_note()}"


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
