"""La page publiée ailleurs (Vercel) pilote un moteur distant : jeton, CORS, clips lisibles, envoi de fichier.

Elle est servie avec les en-têtes de vercel.json, dont la politique de sécurité : pas de wait_for_function ici
(Playwright l'évalue par eval, que cette politique refuse) ; on attend des sélecteurs."""
import functools
import http.server
import json
import os
import shutil
import subprocess
import threading
from pathlib import Path

import pytest

sync_api = pytest.importorskip("playwright.sync_api")

from clipengine.server import make_server
from conftest import wait_until

pytestmark = pytest.mark.slow
CLIPPER = Path(__file__).resolve().parents[2]
TOKEN = "t0ken-" + "x" * 30
OPTS = {"max_clips": 2, "reserve": 2, "min_dur": 12, "max_dur": 30, "target_dur": 20, "llm": "none"}


@pytest.fixture(scope="module")
def browser():
    with sync_api.sync_playwright() as p:
        path = os.environ.get("CLIPO_CHROMIUM") or ("/opt/pw-browsers/chromium" if Path("/opt/pw-browsers/chromium").exists() else None)
        try:
            b = p.chromium.launch(executable_path=path, args=["--no-sandbox"]) if path else p.chromium.launch(args=["--no-sandbox"])
        except Exception as e:
            pytest.skip(f"Chromium indisponible : {e}")
        yield b
        b.close()


@pytest.fixture()
def page(browser):
    pg = browser.new_page(viewport={"width": 1280, "height": 900}, color_scheme="dark")
    pg.errors, pg.urls, pg.engine_requests = [], [], []
    pg.on("pageerror", lambda e: pg.errors.append(str(e)))
    pg.on("console", lambda m: pg.errors.append("console : " + m.text) if m.type == "error" and "Failed to load resource" not in m.text else None)
    pg.on("request", lambda r: pg.urls.append(r.url))
    yield pg
    pg.close()


@pytest.fixture()
def hosted(tmp_path):
    """Ce que Vercel publie : la commande de construction et les en-têtes de vercel.json, sur une adresse autre que celle du moteur."""
    cfg = json.loads((CLIPPER / "vercel.json").read_text(encoding="utf-8"))
    src = tmp_path / "src"
    src.mkdir()
    shutil.copy(CLIPPER / "index.html", src / "index.html")
    shutil.copytree(CLIPPER / "demo", src / "demo")
    subprocess.run(cfg["buildCommand"], shell=True, cwd=src, check=True, capture_output=True)
    out = src / cfg["outputDirectory"]
    heads = {h["key"]: h["value"] for rule in cfg["headers"] if rule["source"] == "/(.*)" for h in rule["headers"]}

    class Handler(http.server.SimpleHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def end_headers(self):
            for k, v in heads.items():
                self.send_header(k, v)
            super().end_headers()

    srv = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(Handler, directory=str(out)))
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{srv.server_address[1]}", out
    srv.shutdown()
    srv.server_close()


@pytest.fixture()
def engine(tmp_path, stub_engine, hosted):
    site, _ = hosted
    srv, app = make_server(CLIPPER, tmp_path / "data", port=0, base_cfg={"min_score": 0, "preset": "ultrafast"}, token=TOKEN, origins=[site])
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{srv.server_address[1]}", app
    srv.shutdown()
    app.close()


def connect(page, site, url, token):
    page.goto(site + "/#moteur"); page.wait_for_selector("#engine-url")
    page.fill("#engine-url", url); page.fill("#engine-token", token)
    page.click("button:has-text('Tester et enregistrer')")


def test_vercel_build_publishes_only_the_page_and_its_demo(hosted):
    _, out = hosted
    assert sorted(p.name for p in out.iterdir()) == ["demo", "index.html"]                # ni code du moteur, ni notes
    assert (out / "demo" / "projet.json").is_file()


def test_hosted_page_connects_to_the_remote_engine_and_shows_its_projects(page, hosted, engine, media_long, tmp_path, monkeypatch):
    site, _ = hosted
    base, app = engine
    f = tmp_path / "ep.mp4"; shutil.copy(media_long, f)
    proj, _ = app.projects.create(file=f, filename="Épisode distant.mp4", options=OPTS)
    pid = proj["id"]
    wait_until(lambda: (lambda d: d if d and d["status"] == "ready" and any(x["state"] == "done" for x in d["proposals"]) else None)(app.projects.public(pid)), 90)

    page.goto(site); page.wait_for_selector("#app h1")
    assert "Aperçu sans moteur" in page.inner_text("#app") and "Aperçu" in page.inner_text("#engine")      # sans moteur : l'aperçu
    page.click("a:has-text('Brancher mon moteur')"); page.wait_for_selector("#engine-url")
    assert "Aucun moteur branché" in page.inner_text("#app") and f"--allow-origin {site}" in page.inner_text("#app")
    # adresse invalide, injoignable, jeton refusé : chacun dit ce qui ne va pas
    for url, token, msg in (("mon-moteur.fr", TOKEN, "Adresse invalide"), ("http://127.0.0.1:9", TOKEN, "Pas de réponse"), (base, "mauvais-" * 4, "jeton")):
        connect(page, site, url, token)
        page.wait_for_selector(f".err:has-text('{msg}')")
        assert page.evaluate("localStorage.getItem('clipo.engine')") is None
    connect(page, site, base + "/", TOKEN)                                                 # barre finale tolérée
    page.wait_for_selector("#engine.on", timeout=20000)
    assert json.loads(page.evaluate("localStorage.getItem('clipo.engine')")) == {"url": base, "token": TOKEN}
    assert "Aperçu sans moteur" not in page.inner_text("#app")
    page.click("a[data-nav=biblio]"); page.wait_for_selector(".lib .pc")
    assert "Épisode distant" in page.inner_text(".lib .pc h3")
    page.click(".lib .pc a.btn"); page.wait_for_selector("video")
    srcs = page.evaluate("[...document.querySelectorAll('video')].map(v => v.getAttribute('src'))")
    assert srcs and all(s.startswith(f"{base}/files/{pid}/") and f"m={app.media_key}" in s for s in srcs)
    href = page.locator("a:text-is('Télécharger')").first.get_attribute("href")
    assert href.count("?") == 1 and href.endswith(f"m={app.media_key}&download=1")                   # ?m=… puis &download=1
    zip_href = page.locator("a:has-text('Tout télécharger')").get_attribute("href")
    assert zip_href == f"{base}/api/projects/{pid}/zip?m={app.media_key}"
    # le clip se lit avec la clé, pas sans
    assert page.request.get(srcs[0]).status == 200
    assert page.request.get(srcs[0].split("?")[0]).status == 401
    assert page.request.get(zip_href).status == 200
    # le jeton ne voyage que dans l'en-tête Authorization, jamais dans une adresse ni dans la page
    assert not any(TOKEN in u for u in page.urls)
    assert TOKEN not in page.content()
    assert page.errors == []                                                               # y compris la politique de sécurité de la page


def test_hosted_page_sends_a_video_to_the_remote_engine(page, hosted, engine, media_long):
    site, _ = hosted
    base, app = engine
    page.add_init_script(f"localStorage.setItem('clipo.engine', JSON.stringify({{url: '{base}', token: '{TOKEN}'}}))")
    page.goto(site + "/#studio"); page.wait_for_selector("#engine.on", timeout=20000)
    page.locator(".seg button", has_text="30 s").click()
    page.set_input_files("input[type=file]", str(media_long))
    page.wait_for_selector("h1:has-text('long')", timeout=30000)
    page.wait_for_selector(".pc video", timeout=90000)
    assert len(app.projects.summaries()) == 1
    page.click("a[data-nav=biblio]"); page.wait_for_selector(".lib .pc")
    page.locator(".lib button", has_text="Supprimer").click(); page.locator(".lib button", has_text="Confirmer").click()
    page.wait_for_selector(".empty", timeout=10000)
    assert app.projects.summaries() == [] and page.errors == []


def test_page_explains_a_dead_or_refused_engine_and_can_disconnect(page, hosted, engine):
    site, _ = hosted
    base, _ = engine
    page.add_init_script("if (!localStorage.getItem('clipo.engine')) localStorage.setItem('clipo.engine', JSON.stringify({url: 'http://127.0.0.1:9', token: 'x'}))")
    page.goto(site); page.wait_for_selector(".banner")
    assert "Le moteur ne répond pas" in page.inner_text(".banner") and "127.0.0.1:9" in page.inner_text(".banner")
    assert "Aperçu" in page.inner_text("#engine")
    page.click(".banner a:has-text('Régler le moteur')"); page.wait_for_selector("#engine-url")
    assert "Injoignable" in page.inner_text("#app")
    page.fill("#engine-url", base); page.fill("#engine-token", "faux-" * 8)
    page.click("button:has-text('Tester et enregistrer')"); page.wait_for_selector(".err:has-text('jeton')")
    page.fill("#engine-token", TOKEN); page.click("button:has-text('Tester et enregistrer')")
    page.wait_for_selector("#engine.on", timeout=20000)
    page.goto(site + "/#moteur"); page.wait_for_selector("#engine-url")
    assert "Connecté à " + base in page.inner_text("#app")
    page.click("button:has-text('Déconnecter')")
    page.wait_for_selector("#engine:not(.on)", timeout=20000)
    assert "Aperçu" in page.inner_text("#engine") and page.evaluate("localStorage.getItem('clipo.engine')") == "{}"
    assert page.errors == []
