"""API : sécurité, projets, fichiers (Range), export, réglages, aperçu de style."""
import json
import shutil
import threading
import urllib.error
import urllib.request
import zipfile
import io

import pytest

from clipengine import projects as proj_mod
from clipengine.server import make_server, serve_main
from conftest import wait_until

OPTS = {"max_clips": 2, "reserve": 2, "min_dur": 12, "max_dur": 30, "target_dur": 20, "llm": "none"}
JSON = {"Content-Type": "application/json"}


def call(url, data=None, headers=None, method=None):
    req = urllib.request.Request(url, data=data, headers=headers or {}, method=method)
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers)


def jcall(url, obj=None, method=None, headers=None):
    h = {**JSON, **(headers or {})}
    s, b, hd = call(url, None if obj is None else json.dumps(obj).encode(), h, method)
    try:
        return s, json.loads(b), hd
    except json.JSONDecodeError:
        return s, b, hd


def start(tmp_path, password=None, data_inside_web=False, token=None, origins=()):
    web = tmp_path / "web"
    (web / "engine").mkdir(parents=True, exist_ok=True)
    (web / "index.html").write_text("<h1>ok</h1>")
    (web / "engine" / "secret.py").write_text("x=1")
    data = web / "mesdonnees" if data_inside_web else tmp_path / "data"
    srv, app = make_server(web, data, port=0, base_cfg={"min_score": 0, "preset": "ultrafast"}, password=password,
                           token=token, origins=origins)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    return f"http://127.0.0.1:{srv.server_address[1]}", srv, app


@pytest.fixture()
def api(tmp_path, stub_engine):
    base, srv, app = start(tmp_path)
    yield base
    srv.shutdown(); app.close()


def upload(base, path, opts=None, name="ep.mp4", ctype="video/mp4"):
    return jcall(f"{base}/api/projects?filename={name}", None, "POST") if False else call(
        f"{base}/api/projects?filename={name}", path.read_bytes(),
        {"Content-Type": ctype, "X-Options": json.dumps({"options": opts or OPTS})})


def project_ready(base, pid, settle=True):
    def ok():
        s, d, _ = jcall(f"{base}/api/projects/{pid}")
        done = d["status"] == "error" or (d["status"] == "ready" and (not settle or all(
            x["state"] not in ("queued", "rendering") for x in d["proposals"])))
        return d if done else None
    return wait_until(ok, 90)


def test_static_pages_and_hidden_paths(tmp_path, stub_engine):
    base, srv, app = start(tmp_path, data_inside_web=True)
    try:
        assert b"<h1>ok</h1>" in call(base + "/")[1]
        assert call(base + "/engine/secret.py")[0] == 404                 # le code n'est pas servi
        assert call(base + "/mesdonnees/settings.json")[0] == 404          # ni les données, même placées sous la page
        assert call(base + "/../../etc/passwd")[0] in (403, 404)
        assert jcall(base + "/api/health")[1]["ok"] is True
    finally:
        srv.shutdown(); app.close()


def test_without_password_only_local_hosts_are_accepted(api):
    assert call(api + "/api/health")[0] == 200
    assert call(api + "/api/health", headers={"Host": "evil.example.com"})[0] == 403          # DNS rebinding
    assert call(api + "/api/health", headers={"Host": "localhost:9999"})[0] == 200
    assert call(api + "/api/health", headers={"Host": "[::1]:80"})[0] == 200


def test_password_protects_everything(tmp_path, stub_engine):
    import base64
    base, srv, app = start(tmp_path, password="s3cret")
    try:
        for path in ("/", "/api/health", "/api/projects", "/api/settings"):
            s, _, h = call(base + path)
            assert s == 401 and "Basic" in h["WWW-Authenticate"], path
        bad = {"Authorization": "Basic " + base64.b64encode(b"moi:mauvais").decode()}
        assert call(base + "/api/health", headers=bad)[0] == 401
        good = {"Authorization": "Basic " + base64.b64encode(b"moi:s3cret").decode()}
        s, b, _ = call(base + "/api/health", headers=good)
        assert s == 200 and json.loads(b) == {"ok": True, "auth": True}
        assert call(base + "/api/projects", headers=good)[0] == 200
        assert call(base + "/api/health", headers={**good, "Host": "mon-site.fr"})[0] == 200   # exposé : le mot de passe suffit
    finally:
        srv.shutdown(); app.close()


TOKEN = "t0ken-" + "x" * 30
PAGE = "https://clipo-rho.vercel.app"
BEARER = {"Authorization": "Bearer " + TOKEN}


@pytest.fixture()
def remote(tmp_path, stub_engine):
    """Moteur ouvert à une page hébergée ailleurs : jeton + origine autorisée."""
    base, srv, app = start(tmp_path, token=TOKEN, origins=[PAGE])
    yield base, app
    srv.shutdown(); app.close()


def test_token_closes_the_api_but_not_the_page(remote, monkeypatch):
    base, app = remote
    monkeypatch.setattr("time.sleep", lambda s: None)
    assert call(base + "/")[0] == 200                                                    # la page n'a rien de secret
    assert call(base + "/engine/secret.py")[0] == 404                                    # le code reste caché
    for path in ("/api/health", "/api/projects", "/api/settings", "/api/discover/status"):
        s, b, _ = call(base + path, headers={"Host": "mon-tunnel.trycloudflare.com"})
        assert s == 401 and b"jeton" in b, path
    assert call(base + "/api/health", headers={"Authorization": "Bearer " + "y" * 30})[0] == 401
    assert call(base + "/api/health", headers={"Authorization": TOKEN})[0] == 401        # schéma Bearer exigé
    s, d, _ = jcall(base + "/api/health", headers={**BEARER, "Host": "mon-tunnel.trycloudflare.com"})
    assert s == 200 and d["ok"] is True and d["auth"] is True and d["media"] == app.media_key and len(d["media"]) == 32
    assert TOKEN not in json.dumps(d)                                                    # la clé n'est pas le jeton
    s, _, _ = jcall(base + "/api/projects", {}, "POST")                                  # écritures fermées aussi
    assert s == 401
    assert call(base + "/api/projects/0123456789abcdef", method="DELETE")[0] == 401
    assert jcall(base + "/api/projects", headers=BEARER)[0] == 200


def test_media_key_opens_clips_and_zip_only(remote, media_long, monkeypatch):
    base, app = remote
    monkeypatch.setattr("time.sleep", lambda s: None)
    s, d, _ = call(f"{base}/api/projects?filename=ep.mp4", media_long.read_bytes(),
                   {"Content-Type": "video/mp4", "X-Options": json.dumps({"options": OPTS}), **BEARER})
    assert s == 202
    pid = json.loads(d)["project"]["id"]

    def first_clip():
        r = jcall(f"{base}/api/projects/{pid}", headers=BEARER)[1]
        return r if r["status"] == "ready" and any(x["state"] == "done" for x in r["proposals"]) else None

    info = wait_until(first_clip, 90)
    clip = next(x for x in info["proposals"] if x["state"] == "done")
    key = app.media_key
    url = f"{base}/files/{pid}/{clip['video']}"
    assert call(url)[0] == 401 and call(url + "?m=" + "0" * 32)[0] == 401
    assert call(url + "?m=" + key)[0] == 200
    s, body, h = call(url + "?download=1&m=" + key, headers={"Range": "bytes=0-9"})
    assert s == 206 and len(body) == 10 and "attachment" in h["Content-Disposition"]
    assert call(f"{base}/api/projects/{pid}/zip?m={key}")[0] == 200
    for path in (f"/api/projects/{pid}", "/api/projects", "/api/settings", "/api/health"):
        assert call(f"{base}{path}?m={key}")[0] == 401, path                             # la clé ne sert qu'à lire les clips
    assert call(f"{base}/api/projects/{pid}?m={key}", method="DELETE")[0] == 401
    assert call(f"{base}/api/projects/{pid}", method="DELETE", headers=BEARER)[0] == 200


def test_cors_only_for_the_allowed_page(remote):
    base, _ = remote
    pre = {"Origin": PAGE, "Access-Control-Request-Method": "POST", "Access-Control-Request-Headers": "authorization,content-type"}
    s, _, h = call(base + "/api/projects", headers=pre, method="OPTIONS")
    assert s == 204 and h["Access-Control-Allow-Origin"] == PAGE and h["Vary"] == "Origin"
    assert "Authorization" in h["Access-Control-Allow-Headers"] and "X-Options" in h["Access-Control-Allow-Headers"]
    assert "POST" in h["Access-Control-Allow-Methods"] and "DELETE" in h["Access-Control-Allow-Methods"]
    s, _, h = call(base + "/api/projects", headers={**pre, "Origin": "https://autre-site.example"}, method="OPTIONS")
    assert s == 204 and "Access-Control-Allow-Origin" not in h and "Access-Control-Allow-Methods" not in h
    s, _, h = call(base + "/api/projects", headers={**pre, "Origin": PAGE + ".autre.example"}, method="OPTIONS")
    assert "Access-Control-Allow-Origin" not in h                                        # pas de correspondance partielle
    s, _, h = call(base + "/api/projects", headers={**BEARER, "Origin": PAGE.upper().replace("HTTPS", "https")})
    assert s == 200 and h["Access-Control-Allow-Origin"] == PAGE.lower()
    s, b, h = call(base + "/api/projects", headers={"Origin": PAGE})                     # un refus reste lisible par la page
    assert s == 401 and h["Access-Control-Allow-Origin"] == PAGE and b"jeton" in b
    assert "Access-Control-Allow-Origin" not in call(base + "/api/projects", headers={**BEARER, "Origin": "https://autre-site.example"})[2]
    assert "Access-Control-Allow-Origin" not in call(base + "/")[2]


def test_forged_writes_from_another_site_stay_refused_with_a_token(remote, monkeypatch):
    base, _ = remote
    monkeypatch.setattr("time.sleep", lambda s: None)
    # simple requête de formulaire depuis une autre page : ni jeton, ni type « non simple »
    s, _, _ = call(base + "/api/projects", b'{"url": "https://exemple.org/v"}', {"Content-Type": "text/plain", "Origin": "https://autre-site.example"}, "POST")
    assert s == 401
    s, _, _ = call(base + "/api/projects", b'{"url": "https://exemple.org/v"}', {"Content-Type": "text/plain", **BEARER}, "POST")
    assert s == 415                                                                      # même avec le jeton, le type simple est refusé


def test_token_and_password_can_coexist(tmp_path, stub_engine, monkeypatch):
    monkeypatch.setattr("time.sleep", lambda s: None)
    base, srv, app = start(tmp_path, password="s3cret", token=TOKEN, origins=[PAGE])
    try:
        import base64 as b64
        basic = {"Authorization": "Basic " + b64.b64encode(b"x:s3cret").decode()}
        assert call(base + "/api/health")[0] == 401 and call(base + "/")[0] == 401        # le mot de passe garde la page
        assert call(base + "/api/health", headers=basic)[0] == 200                         # navigateur local, mot de passe
        assert call(base + "/api/health", headers=BEARER)[0] == 200                        # page distante, jeton
    finally:
        srv.shutdown(); app.close()


def test_invalid_token_or_origin_is_refused_at_start(tmp_path):
    from clipengine.errors import UserError
    web = tmp_path / "w"; web.mkdir(); (web / "index.html").write_text("x")
    with pytest.raises(UserError, match="trop court"):
        make_server(web, tmp_path / "d1", port=0, token="court")
    with pytest.raises(UserError, match="demande un jeton"):
        make_server(web, tmp_path / "d2", port=0, origins=[PAGE])
    for bad in ("clipo.vercel.app", "https://clipo.vercel.app/chemin", "ftp://x.example", "https://user@x.example", "*"):
        with pytest.raises(UserError, match="invalide"):
            make_server(web, tmp_path / "d3", port=0, token=TOKEN, origins=[bad])


def test_serve_creates_and_keeps_a_token_for_a_remote_page(monkeypatch, tmp_path, capsys):
    from clipengine import server as srv_mod
    seen = {}

    class Stop(Exception):
        pass

    def fake_make(*a, **k):
        seen.update(k)
        raise Stop

    monkeypatch.setattr(srv_mod, "make_server", fake_make)
    monkeypatch.delenv("CLIPENGINE_TOKEN", raising=False)
    monkeypatch.delenv("CLIPENGINE_ALLOW_ORIGIN", raising=False)
    monkeypatch.delenv("CLIPENGINE_PASSWORD", raising=False)
    with pytest.raises(Stop):
        serve_main(["--data", str(tmp_path / "d"), "--allow-origin", PAGE])
    tok = seen["token"]
    assert len(tok) >= 24 and (tmp_path / "d" / "token").read_text().strip() == tok
    assert oct((tmp_path / "d" / "token").stat().st_mode & 0o777) == "0o600"
    assert seen["origins"] == [PAGE]
    with pytest.raises(Stop):
        serve_main(["--data", str(tmp_path / "d"), "--token"])
    assert seen["token"] == tok                                                           # le même jeton à chaque démarrage
    monkeypatch.setenv("CLIPENGINE_TOKEN", "e" * 30)
    monkeypatch.setenv("CLIPENGINE_ALLOW_ORIGIN", "https://a.example, https://b.example")
    with pytest.raises(Stop):
        serve_main(["--data", str(tmp_path / "d"), "--allow-origin", PAGE])
    assert seen["token"] == "e" * 30 and seen["origins"] == ["https://a.example", " https://b.example", PAGE]
    # écouter hors de cet ordinateur avec un jeton n'est plus refusé
    monkeypatch.setattr(srv_mod, "make_server", lambda *a, **k: (_ for _ in ()).throw(Stop()))
    with pytest.raises(Stop):
        serve_main(["--data", str(tmp_path / "d"), "--host", "0.0.0.0", "--token"])


def test_serve_refuses_to_listen_publicly_without_password(monkeypatch, capsys):
    monkeypatch.delenv("CLIPENGINE_PASSWORD", raising=False)
    assert serve_main(["--host", "0.0.0.0", "--port", "0"]) == 2
    assert "CLIPENGINE_PASSWORD" in capsys.readouterr().out


def test_forged_cross_site_requests_are_refused(api, media_long):
    # un formulaire ou un fetch « simple » venu d'un autre site ne peut envoyer que ces types : tous refusés
    for ctype in ("text/plain", "application/x-www-form-urlencoded", "multipart/form-data; boundary=x"):
        assert call(api + "/api/projects", b'{"url":"https://8.8.8.8/x"}', {"Content-Type": ctype})[0] == 415, ctype
        assert call(api + "/api/projects?filename=a.mp4", b"abc", {"Content-Type": ctype})[0] == 415, ctype
        assert call(api + "/api/settings", b"{}", {"Content-Type": ctype})[0] == 415, ctype


def test_input_validation(api, media_long):
    assert jcall(api + "/api/projects", {"url": "file:///etc/passwd"}, "POST")[0] == 400
    s, d, _ = jcall(api + "/api/projects", {"url": "http://169.254.169.254/latest"}, "POST")
    assert s == 400 and "interne" in d["error"]
    assert jcall(api + "/api/projects", {}, "POST")[0] == 400
    assert call(api + "/api/projects", b"{pas json", JSON)[0] == 400
    assert call(api + "/api/projects?filename=x.exe", b"abc", {"Content-Type": "video/mp4"})[0] == 415
    assert call(api + "/api/projects?filename=x.mp4", b"", {"Content-Type": "video/mp4"})[0] in (400,)
    assert jcall(api + "/api/projects/" + "0" * 16)[0] == 404
    assert jcall(api + "/api/projects/" + "0" * 16 + "/render", {"ids": [1]}, "POST")[0] == 404
    assert call(api + "/files/" + "0" * 16 + "/a.mp4")[0] == 404
    assert call(api + "/api/projects/zz")[0] == 404


def test_upload_to_clips_files_zip_and_delete(api, media_long, stub_engine):
    s, d, _ = upload(api, media_long)
    assert s == 202
    pid = json.loads(d)["project"]["id"]
    proj = project_ready(api, pid)
    assert proj["status"] == "ready" and proj["proposals"][0]["state"] == "done"
    x = proj["proposals"][0]
    # liste
    s, lst, _ = jcall(api + "/api/projects")
    assert [p["id"] for p in lst["projects"]] == [pid] and lst["projects"][0]["done"] >= 2
    assert lst["projects"][0]["poster"].startswith(f"/files/{pid}/")
    # fichiers + Range
    url = f"{api}/files/{pid}/{x['video']}"
    s, body, h = call(url)
    assert s == 200 and h["Accept-Ranges"] == "bytes" and body[4:8] == b"ftyp"
    s, part, h = call(url, headers={"Range": "bytes=0-9"})
    assert s == 206 and len(part) == 10 and h["Content-Range"] == f"bytes 0-9/{len(body)}"
    s, tail, _ = call(url, headers={"Range": "bytes=-5"})
    assert s == 206 and tail == body[-5:]
    assert call(url, headers={"Range": f"bytes={len(body) + 5}-"})[0] == 416
    assert call(url, headers={"Range": "bytes=abc"})[0] == 416
    assert "attachment" in call(url + "?download=1")[2]["Content-Disposition"]
    assert call(f"{api}/files/{pid}/{x['poster']}")[1][:3] == b"\xff\xd8\xff"
    assert call(f"{api}/files/{pid}/nope.mp4")[0] == 404
    assert call(f"{api}/files/{pid}/..%2fproject.json")[0] == 404
    assert call(f"{api}/files/{pid}/project.json")[0] == 404                 # le fichier d'état n'est pas servi
    # export
    s, z, h = call(f"{api}/api/projects/{pid}/zip")
    assert s == 200 and h["Content-Type"] == "application/zip"
    names = zipfile.ZipFile(io.BytesIO(z)).namelist()
    assert "publication.txt" in names and sum(n.endswith(".mp4") for n in names) == 2
    txt = zipfile.ZipFile(io.BytesIO(z)).read("publication.txt").decode()
    assert x["title"] in txt and "#" in txt
    # suppression
    assert jcall(f"{api}/api/projects/{pid}", None, "DELETE")[0] == 200
    assert jcall(f"{api}/api/projects/{pid}")[0] == 404 and jcall(f"{api}/api/projects/{pid}", None, "DELETE")[0] == 404


def test_render_reserve_restyle_retry_and_settings_through_the_api(api, media_long, stub_engine):
    pid = json.loads(upload(api, media_long)[1])["project"]["id"]
    proj = project_ready(api, pid)
    reserve = next(x for x in proj["proposals"] if x["state"] == "reserve")
    assert jcall(f"{api}/api/projects/{pid}/render", {"ids": "tous"}, "POST")[0] == 400
    s, d, _ = jcall(f"{api}/api/projects/{pid}/render", {"ids": [reserve["id"]], "style": "sport"}, "POST")
    assert s == 202 and d["queued"] == [reserve["id"]]
    proj = project_ready(api, pid)
    assert next(x for x in proj["proposals"] if x["id"] == reserve["id"])["state"] == "done"
    assert stub_engine[-1][2] == "sport"
    s, d, _ = jcall(f"{api}/api/projects/{pid}/style", {"style": {"font": "bangers", "signature": "@moi"}}, "POST")
    assert s == 200 and d["style"]["font"] == "bangers" and d["style"]["signature"] == "@moi"
    assert jcall(f"{api}/api/projects/{pid}/retry", {}, "POST")[0] == 400          # pas en échec
    # réglages
    s, d, _ = jcall(api + "/api/settings", {"style": {"size": 120}, "defaults": {"max_clips": 4}}, "POST")
    assert s == 200 and d["style"]["size"] == 120 and d["defaults"] == {"max_clips": 4}
    assert jcall(api + "/api/settings")[1] == d
    cat = jcall(api + "/api/styles")[1]
    assert len(cat["presets"]) >= 6 and len(cat["fonts"]) >= 6


def test_duplicate_link_returns_the_existing_project(api, media_long, monkeypatch, tmp_path):
    monkeypatch.setattr(proj_mod, "check_url", lambda u: u)

    def fake_fetch(url, out_dir, max_s=0, log=print):
        out_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy(media_long, out_dir / "source.mp4")
        return out_dir / "source.mp4", {"title": "T", "id": "x", "uploader": "", "duration": 70}

    monkeypatch.setattr(proj_mod, "fetch", fake_fetch)
    s, a, _ = jcall(api + "/api/projects", {"url": "https://youtu.be/dQw4w9WgXcQ", "options": OPTS}, "POST")
    assert s == 202 and a["duplicate"] is False
    s, b, _ = jcall(api + "/api/projects", {"url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ"}, "POST")
    assert s == 200 and b["duplicate"] is True and b["project"]["id"] == a["project"]["id"]
    s, c, _ = jcall(api + "/api/projects", {"url": "https://youtu.be/dQw4w9WgXcQ", "force": True}, "POST")
    assert s == 202 and c["project"]["id"] != a["project"]["id"]
    project_ready(api, a["project"]["id"], settle=False); project_ready(api, c["project"]["id"], settle=False)


def test_error_project_reports_a_readable_message(api, tmp_path):
    bad = tmp_path / "bad.mp4"
    bad.write_bytes(b"pas une video")
    pid = json.loads(upload(api, bad)[1])["project"]["id"]
    d = project_ready(api, pid)
    assert d["status"] == "error" and "pas une vidéo" in d["error"]
    assert jcall(api + "/api/projects")[1]["projects"][0]["error"] == d["error"]
    assert jcall(f"{api}/api/projects/{pid}/retry", {}, "POST")[0] == 202


@pytest.mark.slow
def test_style_preview_is_a_png_cached_and_validated(api):
    s, png, h = call(api + "/api/style-preview", json.dumps({"style": {"font": "anton", "size": 9999}}).encode(), JSON)
    assert s == 200 and h["Content-Type"] == "image/png" and png[:8] == b"\x89PNG\r\n\x1a\n"
    s2, png2, _ = call(api + "/api/style-preview", json.dumps({"style": {"font": "anton", "size": 160}}).encode(), JSON)
    assert png2 == png                                                  # 9999 est borné à 160 : même rendu, même cache
    s3, png3, _ = call(api + "/api/style-preview", json.dumps({"style": {"font": "bangers"}}).encode(), JSON)
    assert png3 != png


@pytest.mark.slow
def test_real_render_through_the_api(tmp_path, media, words, monkeypatch):
    """Sans faux rendu : vraie analyse, vrai rendu 1080×1920 avec visage centré et sous-titres."""
    import subprocess
    import cv2
    import numpy as np
    from clipengine import pipeline
    from clipengine.face import FaceDetector
    from clipengine.transcribe import Transcript
    monkeypatch.setattr(pipeline, "transcribe", lambda *a, **k: Transcript("fr", [w for w in words if w.end < 23.5]))
    base, srv, app = start(tmp_path)
    try:
        opts = {"max_clips": 1, "reserve": 0, "min_dur": 10, "max_dur": 22, "target_dur": 18, "llm": "none"}
        pid = json.loads(upload(base, media["pan"], opts)[1])["project"]["id"]
        d = project_ready(base, pid)
        x = d["proposals"][0]
        assert x["state"] == "done" and x["layouts"] == ["track"]
        mp4 = app.projects.dir(pid) / "clips" / x["video"]
        probe = json.loads(subprocess.run(["ffprobe", "-v", "error", "-print_format", "json", "-show_streams", str(mp4)],
                                          capture_output=True, check=True, text=True).stdout)
        v = next(s for s in probe["streams"] if s["codec_type"] == "video")
        assert (v["width"], v["height"]) == (1080, 1920) and any(s["codec_type"] == "audio" for s in probe["streams"])
        out = subprocess.run(["ffmpeg", "-v", "error", "-ss", "6", "-i", str(mp4), "-frames:v", "1", "-f", "image2pipe",
                              "-vcodec", "png", "-"], capture_output=True, check=True).stdout
        fr = cv2.imdecode(np.frombuffer(out, np.uint8), cv2.IMREAD_COLOR)
        faces = FaceDetector().detect(fr)
        assert faces and abs(max(faces, key=lambda b: b[2] * b[3])[0] + max(faces, key=lambda b: b[2] * b[3])[2] / 2 - 540) < 160
        assert (app.projects.dir(pid) / "clips" / x["poster"]).stat().st_size > 1000
    finally:
        srv.shutdown(); app.close()
