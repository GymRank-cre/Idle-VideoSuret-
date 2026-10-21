"""Onglet Découvrir dans un vrai navigateur : sans clé, tendances et filtres, chaînes suivies, bouton Clipper."""
import base64
import os
import re
import shutil
import threading
from pathlib import Path

import pytest

sync_api = pytest.importorskip("playwright.sync_api")

from clipengine import projects as proj_mod
from clipengine.server import make_server
from test_discover import KEY, UC_A, FakeYouTube, item

pytestmark = pytest.mark.slow
CLIPPER = Path(__file__).resolve().parents[2]
PNG = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==")


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
    pg.errors = []
    pg.on("pageerror", lambda e: pg.errors.append(str(e)))
    pg.route(re.compile(r"https://(i\.ytimg\.com|yt3\.ggpht\.com)/.*"), lambda r: r.fulfill(status=200, content_type="image/png", body=PNG))
    yield pg
    pg.close()


@pytest.fixture()
def yt(monkeypatch):
    fake = FakeYouTube()
    monkeypatch.setenv("CLIPENGINE_YT_URL", fake.url)
    yield fake
    fake.server.shutdown()


@pytest.fixture()
def live(tmp_path, stub_engine, yt, monkeypatch):
    srv, app = make_server(CLIPPER, tmp_path / "data", port=0, base_cfg={"min_score": 0, "preset": "ultrafast"})
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{srv.server_address[1]}", app
    srv.shutdown()
    app.close()


def test_without_a_key_the_tab_explains_how_to_get_one(page, live, monkeypatch):
    monkeypatch.delenv("YOUTUBE_API_KEY", raising=False)
    base, _ = live
    page.goto(base); page.wait_for_selector("#app h1")
    page.click("a[data-nav=decouvrir]"); page.wait_for_selector("text=Il manque la clé YouTube")
    txt = page.inner_text("#app")
    assert "YouTube Data API v3" in txt and "YOUTUBE_API_KEY" in txt and page.locator(".vc").count() == 0
    assert page.errors == []


def test_trending_filters_followed_channels_and_seen_flags(page, live, yt, monkeypatch):
    monkeypatch.setenv("YOUTUBE_API_KEY", KEY)
    yt.pages = [[item("t1", views=80_000, hours_ago=4, title="Montée rapide"), item("t2", views=30_000, hours_ago=40, title="Plus lente"),
                 item("t3", minutes=3, views=900_000, hours_ago=2, title="Trop courte")]]
    yt.add_channel(UC_A, "Chaîne suivie", handle="@chaine-a")
    yt.uploads["UU" + "a" * 22] = ["v1", "v2"]
    yt.videos = {"v1": item("v1", hours_ago=1, title="Nouveauté du jour", channel=UC_A), "v2": item("v2", hours_ago=90, title="Ancienne", channel=UC_A)}
    base, app = live
    page.goto(base); page.wait_for_selector("#app h1")
    page.click("a[data-nav=decouvrir]"); page.wait_for_selector(".vc")
    titles = page.evaluate("[...document.querySelectorAll('.vc h3')].map(e => e.textContent)")
    assert titles == ["Montée rapide", "Plus lente"]                                   # la courte est écartée ; plus de vues/h d'abord
    assert "vues/h" in page.inner_text(".vc") and page.locator(".vc .dur").first.inner_text() == "20:00"
    page.locator(".seg button", has_text="24 h").click(); page.wait_for_function("document.querySelectorAll('.vc').length === 1")
    assert page.inner_text(".vc h3") == "Montée rapide"
    assert page.get_attribute(".vc a.thumb", "href").endswith("watch?v=t1") and "noopener" in page.get_attribute(".vc a.thumb", "rel")
    # chaînes suivies
    page.locator(".seg button", has_text="Mes chaînes").click(); page.wait_for_selector("text=Aucune chaîne suivie")
    page.fill("input[aria-label='Chaîne à suivre']", "@introuvable-chez-youtube"); page.click("button:has-text('Suivre')")
    page.wait_for_selector(".err:has-text('introuvable')")
    page.fill("input[aria-label='Chaîne à suivre']", "@chaine-a"); page.click("button:has-text('Suivre')")
    page.wait_for_selector(".chan h2:has-text('Chaîne suivie')")
    assert page.evaluate("[...document.querySelectorAll('.vc h3')].map(e => e.textContent)") == ["Nouveauté du jour", "Ancienne"]   # récente d'abord
    assert page.locator(".vc .badge").count() == 0                                     # rien de nouveau depuis l'ajout
    # la dernière visite remonte dans le temps : la nouveauté apparaît
    import json, time
    f = app.projects.root / "watch.json"
    d = json.loads(f.read_text()); d["channels"][0]["seen"] = time.time() - 6 * 3600; f.write_text(json.dumps(d))
    page.click("button:has-text('Actualiser')"); page.wait_for_selector(".vc .badge")
    assert page.inner_text(".chan .count") == "1 nouvelle"
    page.click("button:has-text('Tout marquer comme vu')"); page.wait_for_function("document.querySelectorAll('.vc .badge').length === 0")
    page.locator(".chan button", has_text="Ne plus suivre").click(); page.locator(".chan button", has_text="Confirmer").click()
    page.wait_for_selector("text=Aucune chaîne suivie")
    assert json.loads(f.read_text())["channels"] == []
    assert page.errors == []


def test_clip_button_creates_the_project_from_the_video_link(page, live, yt, monkeypatch, media_long):
    monkeypatch.setenv("YOUTUBE_API_KEY", KEY)
    monkeypatch.setattr(proj_mod, "check_url", lambda u: u)

    def fake_fetch(url, out_dir, max_s=0, log=print):
        out_dir.mkdir(parents=True, exist_ok=True)
        shutil.copy(media_long, out_dir / "source.mp4")
        return out_dir / "source.mp4", {"title": "Vidéo tendance", "id": "t1", "uploader": "", "duration": 70}

    monkeypatch.setattr(proj_mod, "fetch", fake_fetch)
    yt.pages = [[item("t1", views=80_000, hours_ago=4, title="Montée rapide")]]
    base, app = live
    page.goto(base + "/#decouvrir"); page.wait_for_selector(".vc")
    page.locator(".vc button", has_text="Clipper").click()
    page.wait_for_function("location.hash.startsWith('#p-')", timeout=20000)
    assert len(app.projects.summaries()) == 1
    page.wait_for_selector("h1", timeout=20000)
    page.go_back(); page.wait_for_selector(".vc")
    page.locator(".vc button", has_text="Clipper").click()                              # même vidéo : doublon, pas un second projet
    page.wait_for_function("location.hash.startsWith('#p-')", timeout=20000)
    assert len(app.projects.summaries()) == 1
    assert page.errors == []


def test_discover_fits_a_phone_screen(page, live, yt, monkeypatch):
    monkeypatch.setenv("YOUTUBE_API_KEY", KEY)
    yt.pages = [[item(f"v{i}", views=10_000 * (i + 1), hours_ago=3, title="Un titre assez long pour passer à la ligne " * 2) for i in range(4)]]
    base, _ = live
    for width in (390, 340):
        page.set_viewport_size({"width": width, "height": 800})
        page.goto(base + "/#decouvrir"); page.wait_for_selector(".vc")
        assert not page.evaluate("document.documentElement.scrollWidth > window.innerWidth"), width
        assert page.locator("a[data-nav=decouvrir]").is_visible() or page.evaluate("document.querySelector('.bar nav').scrollWidth > 0")
    assert page.errors == []
