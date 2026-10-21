"""API : dépôt de fichier, job, progression, clips lisibles avec Range. Le moteur est le vrai (transcription injectée)."""
import json
import threading
import time
import urllib.error
import urllib.request

import pytest

from clipengine.pipeline import run
from clipengine.server import clean_options, make_server
from conftest import TEXT, make_words

WEB = None


@pytest.fixture()
def api(tmp_path, media):
    words = make_words(TEXT[:55], wps=3.4)
    tr = tmp_path / "t.json"
    tr.write_text(json.dumps({"language": "fr", "words": [
        {"text": w.text, "start": w.start, "end": w.end} for w in words if w.end < 23.5]}))
    web = tmp_path / "web"
    (web / "engine").mkdir(parents=True)
    (web / "index.html").write_text("<h1>ok</h1>")
    (web / "engine" / "secret.py").write_text("x=1")

    def runner(source, out, cfg, render=True, log=print):
        return run(source, out, cfg, tr, render, log)

    srv, app = make_server(web, tmp_path / "data", port=0, runner=runner,
                           base_cfg={"min_dur": 10, "max_dur": 22, "target_dur": 18, "min_score": 0,
                                     "llm": "none", "preset": "ultrafast"})
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{srv.server_address[1]}", media
    srv.shutdown(); app.close()


def call(url, data=None, headers=None, method=None):
    req = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers)


def test_clean_options_clamps_and_filters():
    o = clean_options({"max_clips": 999, "min_dur": "abc", "layout": "evil", "captions": "yes", "whisper_model": "x"})
    assert o == {"max_clips": 12}
    assert clean_options({"layout": "split", "captions": False, "max_dur": 3}) == {"layout": "split", "captions": False,
                                                                                    "max_dur": 15.0}


def test_static_and_validation(api):
    base, _ = api
    assert b"<h1>ok</h1>" in call(base + "/")[1]
    assert call(base + "/engine/secret.py")[0] == 404          # le code n'est pas servi
    assert call(base + "/../../etc/passwd")[0] in (403, 404)
    assert call(base + "/api/health")[0] == 200
    s, body, _ = call(base + "/api/jobs", json.dumps({"url": "file:///etc/passwd"}).encode(),
                      {"Content-Type": "application/json"})
    assert s == 400
    s, _, _ = call(base + "/api/jobs?filename=x.exe", b"abc", {"Content-Type": "application/octet-stream"})
    assert s == 415
    assert call(base + "/api/jobs/" + "0" * 16)[0] == 404
    assert call(base + "/files/" + "0" * 16 + "/../../x.mp4")[0] in (400, 404)


def test_upload_job_to_playable_clips(api):
    base, media = api
    data = media["pan"].read_bytes()
    s, body, _ = call(base + "/api/jobs?filename=pan.mp4", data, {"Content-Type": "video/mp4",
                      "X-Options": json.dumps({"max_clips": 1})})
    assert s == 202
    jid = json.loads(body)["id"]
    seen = set()
    for _ in range(240):
        j = json.loads(call(f"{base}/api/jobs/{jid}")[1])
        seen.add(j["stage"])
        if j["status"] in ("done", "error"):
            break
        time.sleep(0.5)
    assert j["status"] == "done", j
    assert len(j["clips"]) == 1 and j["progress"] == 1.0 and len(seen) >= 2
    c = j["clips"][0]
    assert c["video"].endswith(".mp4") and c["score"] > 0 and c["title"]

    s, body, h = call(base + c["video"])
    assert s == 200 and h["Accept-Ranges"] == "bytes" and body[4:8] == b"ftyp"
    total = len(body)
    s, part, h = call(base + c["video"], headers={"Range": "bytes=0-99"})
    assert s == 206 and len(part) == 100 and h["Content-Range"] == f"bytes 0-99/{total}"
    s, tail, _ = call(base + c["video"], headers={"Range": "bytes=-50"})
    assert s == 206 and tail == body[-50:]
    assert call(base + c["video"], headers={"Range": f"bytes={total + 5}-"})[0] == 416
    s, _, h = call(base + c["video"] + "?download=1")
    assert "attachment" in h["Content-Disposition"]
    assert json.loads(call(base + c["meta"])[1])["title"] == c["title"]


def test_failed_job_reports_error_and_server_survives(api):
    base, _ = api
    s, body, _ = call(base + "/api/jobs?filename=bad.mp4", b"pas une video", {"Content-Type": "video/mp4"})
    jid = json.loads(body)["id"]
    for _ in range(60):
        j = json.loads(call(f"{base}/api/jobs/{jid}")[1])
        if j["status"] in ("done", "error"):
            break
        time.sleep(0.3)
    assert j["status"] == "error" and j["error"]
    assert call(base + "/api/health")[0] == 200
