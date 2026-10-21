"""Appel à Claude : erreurs lisibles, nouvelles tentatives, état affiché ; l'API elle-même est simulée (pas de clé ici)."""
import io
import json
import urllib.error

import pytest

from clipengine import Config, llm
from clipengine.pipeline import find_moments
from clipengine.transcribe import Transcript


def http_error(code: int, kind: str = "", message: str = ""):
    body = json.dumps({"type": "error", "error": {"type": kind, "message": message}}).encode()
    return urllib.error.HTTPError(llm.API, code, "x", {}, io.BytesIO(body))


class FakeResponse(io.BytesIO):
    def __enter__(self):
        return self

    def __exit__(self, *a):
        return False


@pytest.fixture()
def fast_calls(monkeypatch):
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-test")
    monkeypatch.setattr(llm.time, "sleep", lambda s: None)


def test_http_errors_are_explained_in_plain_words():
    assert "clé refusée" in llm.explain_http_error(401, '{"error":{"type":"authentication_error","message":"invalid x-api-key"}}')
    assert "crédit" in llm.explain_http_error(400, '{"error":{"type":"invalid_request_error","message":"Your credit balance is too low"}}')
    assert "modèle introuvable (claude-x)" in llm.explain_http_error(404, '{"error":{"type":"not_found_error","message":"model: claude-x"}}', "claude-x")
    assert "limite" in llm.explain_http_error(429, "{}")
    assert "surchargé" in llm.explain_http_error(529, '{"error":{"type":"overloaded_error","message":"Overloaded"}}')
    assert "droit" in llm.explain_http_error(403, '{"error":{"type":"permission_error","message":"no"}}')
    assert "requête refusée (400)" in llm.explain_http_error(400, "pas du json")


def test_a_refused_key_is_reported_without_retrying(monkeypatch, fast_calls):
    calls = []
    def deny(req, timeout=0):
        calls.append(1)
        raise http_error(401, "authentication_error", "invalid x-api-key")
    monkeypatch.setattr(llm.urllib.request, "urlopen", deny)
    with pytest.raises(llm.LLMError, match="clé refusée"):
        llm._call(Config(), "x")
    assert len(calls) == 1


def test_overload_is_retried_then_succeeds(monkeypatch, fast_calls):
    answers = [http_error(529, "overloaded_error", "Overloaded"), http_error(429, "rate_limit_error", "slow"),
               FakeResponse(json.dumps({"content": [{"type": "text", "text": "[]"}]}).encode())]
    def flaky(req, timeout=0):
        a = answers.pop(0)
        if isinstance(a, Exception):
            raise a
        return a
    monkeypatch.setattr(llm.urllib.request, "urlopen", flaky)
    assert llm._call(Config(), "x") == "[]" and not answers


def test_network_failure_becomes_a_readable_error(monkeypatch, fast_calls):
    def down(req, timeout=0):
        raise urllib.error.URLError("dns")
    monkeypatch.setattr(llm.urllib.request, "urlopen", down)
    with pytest.raises(llm.LLMError, match="réseau injoignable"):
        llm._call(Config(), "x")


def test_rerank_reports_why_it_fell_back_to_the_engine_score(words):
    from clipengine.models import Candidate
    cands = [Candidate(0, 30, 0, 0, text="a b c", heuristic=50, score=50)]
    seen = []
    def refused(cfg, prompt):
        raise llm.LLMError("clé refusée (ANTHROPIC_API_KEY invalide ou révoquée)")
    assert llm.rerank(cands, Config(), call=refused, on_error=seen.append)[0].score == 50
    assert seen == ["clé refusée (ANTHROPIC_API_KEY invalide ou révoquée)"]
    llm.rerank(cands, Config(), call=lambda c, p: "pas du json", on_error=seen.append)
    assert seen[-1].startswith("réponse illisible")
    llm.rerank(cands, Config(), call=lambda c, p: 1 / 0, on_error=seen.append)
    assert seen[-1].startswith("appel impossible")


def _find(words, **kw):
    cfg = Config(min_dur=12, max_dur=40, target_dur=25, max_clips=3, min_score=0, **kw)
    return find_moments(Transcript("fr", words), None, cfg, log=lambda m: None, llm_call=kw.pop("llm_call", None))


def test_find_moments_blends_claude_scores_and_reports_its_state(words, monkeypatch):
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)
    cfg = Config(min_dur=12, max_dur=40, target_dur=25, max_clips=3, min_score=0, llm="auto")
    tr = Transcript("fr", words)
    base, _, st = find_moments(tr, None, cfg, log=lambda m: None)
    assert st["llm"] == "nokey" and all(c.llm_score is None for c in base)
    assert find_moments(tr, None, Config(**{**cfg.__dict__, "llm": "none"}), log=lambda m: None)[2]["llm"] == "off"

    prompts = []
    def claude(c, prompt):
        prompts.append(prompt)
        n = prompt.count("### Extrait ")
        return json.dumps([{"id": i, "score": 95 if i == n - 1 else 5, "title": f"Titre {i}", "hook": f"Accroche {i}",
                            "why": "x", "caption": f"Légende {i}", "hashtags": ["#a", "#b"]} for i in range(n)])
    ranked, _, st = find_moments(tr, None, cfg, log=lambda m: None, llm_call=claude)
    assert st["llm"] == "ok" and prompts
    favoured = [c for c in ranked if c.llm_score == 95]
    assert favoured and favoured[0].title.startswith("Titre") and favoured[0].caption.startswith("Légende")
    assert ranked[0].score == max(c.score for c in ranked) and ranked[0].llm_score is not None

    logs = []
    def refused(c, prompt):
        raise llm.LLMError("crédit insuffisant sur le compte Anthropic")
    ranked, _, st = find_moments(tr, None, cfg, log=logs.append, llm_call=refused)
    assert st == {**st, "llm": "error", "llm_error": "crédit insuffisant sur le compte Anthropic"}
    assert any("indisponible" in m and "crédit" in m for m in logs) and ranked


def test_unjudged_variants_never_overtake_the_moments_claude_actually_read():
    from clipengine.models import Candidate
    mk = lambda s, e, h, t: Candidate(s, e, 0, 0, text=t, heuristic=h, score=h)
    a = mk(0, 30, 95, "aaa bbb ccc")        # membre du pool, jugé mauvais par Claude
    a2 = mk(1, 29, 94, "aaa bbb ccc")       # variante du même passage : absente du pool (trop proche de a)
    b = mk(100, 130, 60, "ddd eee fff")     # autre moment, jugé excellent
    c = mk(300, 330, 55, "ggg hhh iii")     # hors pool, non évalué
    answer = json.dumps([{"id": 0, "score": 5, "title": "Titre A", "hook": "Accroche A", "caption": "Légende A", "hashtags": ["#a"]},
                         {"id": 1, "score": 90, "title": "Titre B"}])
    llm.rerank([a, a2, b, c], Config(llm_top=2, llm_weight=0.5), call=lambda cfg, p: answer)
    assert (a.score, b.score) == (50.0, 75.0)
    for x in (a2, c):                        # non jugés : ni note ni texte hérités, et derrière tous les jugés
        assert x.llm_score is None and not x.title and x.score < a.score


def test_check_llm_says_whether_the_key_works_and_why_not(monkeypatch, capsys):
    from clipengine.cli import main
    monkeypatch.delenv("ANTHROPIC_API_KEY", raising=False)
    assert llm.check()[0] is False and "absente" in llm.check()[1]
    assert main(["check-llm"]) == 1 and "ANTHROPIC_API_KEY absente" in capsys.readouterr().out
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-test")
    monkeypatch.setattr(llm, "_call", lambda cfg, user, retries=2: (_ for _ in ()).throw(llm.LLMError("clé refusée (x)")))
    assert llm.check() == (False, "clé refusée (x)") and main(["check-llm"]) == 1
    monkeypatch.setattr(llm, "_call", lambda cfg, user, retries=2: "pas du json")
    assert llm.check()[0] is False and "tableau JSON" in llm.check()[1]
    monkeypatch.setattr(llm, "_call", lambda cfg, user, retries=2: '[{"id": 0, "score": 71}]')
    ok, msg = llm.check()
    capsys.readouterr()                                     # vide ce que les appels précédents ont affiché
    assert ok and "71" in msg and main(["check-llm"]) == 0 and capsys.readouterr().out.startswith("✓")
