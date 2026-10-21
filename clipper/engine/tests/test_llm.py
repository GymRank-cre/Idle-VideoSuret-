"""Appel à Claude par le SDK officiel : requête conforme, erreurs lisibles, nouvelles tentatives, repli ; l'API est simulée
par un petit serveur local (pas de clé ici) : on vérifie ce que le SDK envoie réellement et comment on lit ses réponses."""
import json
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import pytest

from clipengine import Config, llm
from clipengine.pipeline import find_moments
from clipengine.transcribe import Transcript

anthropic = pytest.importorskip("anthropic")


def message(text="[]", stop="end_turn", **extra):
    return {"id": "msg_1", "type": "message", "role": "assistant", "model": "claude-opus-5-5", "stop_sequence": None,
            "content": [{"type": "text", "text": text}], "stop_reason": stop,
            "usage": {"input_tokens": 10, "output_tokens": 5}, **extra}


def error(kind, text):
    return {"type": "error", "error": {"type": kind, "message": text}}


class FakeAPI:
    """Serveur local : répond dans l'ordre aux requêtes (ou par un message vide) et garde ce qu'il a reçu."""

    def __init__(self):
        self.responses: list[tuple[int, dict, dict]] = []
        self.requests: list[dict] = []
        outer = self

        class H(BaseHTTPRequestHandler):
            def do_POST(self):
                body = json.loads(self.rfile.read(int(self.headers.get("content-length", 0))) or b"{}")
                outer.requests.append({"path": self.path, "body": body,
                                       "headers": {k.lower(): v for k, v in self.headers.items()}})
                status, payload, headers = outer.responses.pop(0) if outer.responses else (200, message(), {})
                data = json.dumps(payload).encode()
                self.send_response(status)
                self.send_header("content-type", "application/json")
                self.send_header("content-length", str(len(data)))
                for k, v in headers.items():
                    self.send_header(k, v)
                self.end_headers()
                self.wfile.write(data)

            def log_message(self, *a):
                pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), H)
        self.url = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def reply(self, status, payload, **headers):
        self.responses.append((status, payload, {"retry-after-ms": "1", **headers}))


@pytest.fixture()
def api(monkeypatch):
    fake = FakeAPI()
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-test")
    monkeypatch.setenv("CLIPENGINE_LLM_URL", fake.url)
    yield fake
    fake.server.shutdown()


def test_the_request_sent_matches_the_documented_api(api):
    api.reply(200, message('[{"id": 0, "score": 80}]'))
    assert llm._call(Config(), "### Extrait 0 (30s)\nbonjour") == '[{"id": 0, "score": 80}]'
    r = api.requests[0]
    assert r["path"].startswith("/v1/messages") and r["headers"]["x-api-key"] == "sk-test"
    assert llm.FALLBACK_BETA in r["headers"]["anthropic-beta"] and r["headers"]["anthropic-version"]
    b = r["body"]
    assert b["model"] == "claude-opus-5-5" == Config().llm_model                 # modèle par défaut : Claude Opus 5.5
    assert b["max_tokens"] == 16000 and b["system"] == llm.SYSTEM and b["fallbacks"] == "default"
    assert b["messages"] == [{"role": "user", "content": "### Extrait 0 (30s)\nbonjour"}]
    assert not {"thinking", "temperature", "top_p", "top_k"} & set(b)             # refusés par les modèles 5.x


def test_a_refused_key_is_reported_with_its_reference_and_not_retried(api):
    api.reply(401, error("authentication_error", "invalid x-api-key"), **{"request-id": "req_abc"})
    with pytest.raises(llm.LLMError, match=r"clé refusée.*req_abc"):
        llm._call(Config(), "x")
    assert len(api.requests) == 1


def test_overload_is_retried_by_the_sdk_then_succeeds(api):
    api.reply(529, error("overloaded_error", "Overloaded"))
    api.reply(200, message("[]"))
    assert llm._call(Config(), "x") == "[]" and len(api.requests) == 2


def test_credit_workspace_permission_model_and_rate_limit_errors_are_explained(api):
    cases = [(400, "invalid_request_error", "Your credit balance is too low to access the Anthropic API.", "crédit insuffisant"),
             (400, "invalid_request_error", "anthropic-workspace-id is required when authenticating with an identity-linked "
              "API key", "espace de travail"),
             (403, "permission_error", "no access", "pas le droit"),
             (404, "not_found_error", "model: claude-x", "modèle introuvable"),
             (429, "rate_limit_error", "slow down", "limite de débit")]
    for status, kind, text, expected in cases:
        for _ in range(3):                               # 429 : le SDK réessaie avant d'abandonner
            api.reply(status, error(kind, text))
        with pytest.raises(llm.LLMError, match=expected):
            llm._call(Config(), "x")
        api.responses.clear()


def test_a_bad_fallback_request_is_retried_as_a_plain_call(api):
    api.reply(400, error("invalid_request_error", "fallbacks: Extra inputs are not permitted"))
    api.reply(200, message('[{"id": 0, "score": 60}]'))
    assert llm._call(Config(), "x") == '[{"id": 0, "score": 60}]'
    plain = api.requests[1]
    assert "fallbacks" not in plain["body"] and "anthropic-beta" not in plain["headers"]


def test_refusal_and_truncation_are_errors_in_plain_words(api):
    api.reply(200, message("", stop="refusal", stop_details={"type": "refusal", "category": "cyber", "explanation": "x"}))
    with pytest.raises(llm.LLMError, match=r"refusé.*cyber"):
        llm._call(Config(), "x")
    api.reply(200, message('[{"id": 0, "sc', stop="max_tokens"))
    with pytest.raises(llm.LLMError, match="coupée"):
        llm._call(Config(), "x")


def test_thinking_blocks_are_ignored_when_reading_the_answer(api):
    api.reply(200, message("[]", content=[{"type": "thinking", "thinking": "", "signature": "s"},
                                          {"type": "text", "text": '[{"id": 1, "score": 5}]'}]))
    assert llm._call(Config(), "x") == '[{"id": 1, "score": 5}]'


def test_network_failure_and_missing_sdk_are_readable(monkeypatch):
    monkeypatch.setenv("ANTHROPIC_API_KEY", "sk-test")
    monkeypatch.setenv("CLIPENGINE_LLM_URL", "http://127.0.0.1:9")                  # personne n'écoute
    with pytest.raises(llm.LLMError, match="réseau injoignable"):
        llm._call(Config(), "x", retries=0)
    monkeypatch.setitem(sys.modules, "anthropic", None)                              # `import anthropic` échoue
    with pytest.raises(llm.LLMError, match="paquet « anthropic » manque"):
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
    monkeypatch.setattr(llm, "_call", lambda cfg, user, **kw: (_ for _ in ()).throw(llm.LLMError("clé refusée (x)")))
    assert llm.check() == (False, "clé refusée (x)") and main(["check-llm"]) == 1
    monkeypatch.setattr(llm, "_call", lambda cfg, user, **kw: "pas du json")
    assert llm.check()[0] is False and "tableau JSON" in llm.check()[1]
    monkeypatch.setattr(llm, "_call", lambda cfg, user, **kw: '[{"id": 0, "score": 71}]')
    ok, msg = llm.check()
    capsys.readouterr()                                     # vide ce que les appels précédents ont affiché
    assert ok and "71" in msg and main(["check-llm"]) == 0 and capsys.readouterr().out.startswith("✓")
