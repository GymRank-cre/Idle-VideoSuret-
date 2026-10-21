"""Interface dans un vrai navigateur : mode aperçu sans moteur, puis parcours complet avec le moteur (rendu factice)."""
import json
import os
import shutil
import subprocess
import sys
import threading
import time
from pathlib import Path

import pytest

sync_api = pytest.importorskip("playwright.sync_api")

from clipengine import projects as proj_mod
from clipengine.server import make_server

pytestmark = pytest.mark.slow
CLIPPER = Path(__file__).resolve().parents[2]            # dossier contenant index.html et demo/
OPTS = {"max_clips": 2, "reserve": 3, "min_dur": 12, "max_dur": 30, "target_dur": 20, "llm": "none"}


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
    yield pg
    pg.close()


@pytest.fixture()
def live(tmp_path, stub_engine):
    srv, app = make_server(CLIPPER, tmp_path / "data", port=0, base_cfg={"min_score": 0, "preset": "ultrafast"})
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    yield f"http://127.0.0.1:{srv.server_address[1]}", app
    srv.shutdown(); app.close()


def test_preview_mode_without_engine(page):
    port = 8137
    srv = subprocess.Popen([sys.executable, "-m", "http.server", str(port), "--bind", "127.0.0.1", "--directory", str(CLIPPER)],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(1)
        page.goto(f"http://127.0.0.1:{port}/")
        page.wait_for_selector("#app h1")
        assert "Aperçu" in page.inner_text("#engine") and "Aperçu sans moteur" in page.inner_text("#app")
        page.fill("#url", "https://youtube.com/watch?v=abc"); page.click("button[type=submit]")
        assert "n'analyse pas" in page.inner_text(".err")
        page.click("a[data-nav=biblio]"); page.wait_for_selector(".lib .pc")
        assert page.inner_text("#count") == "1"
        page.click(".lib .pc a.btn"); page.wait_for_selector(".grid .pc")
        assert page.locator(".pc").count() >= 3 and page.locator(".pc .bars li").count() >= 9
        sections = page.evaluate("[...document.querySelectorAll('.sect h2')].map(e=>e.textContent)")
        assert sections[0] == "Clips" and sections[1].startswith("En réserve")
        page.locator("button", has_text="Fabriquer").first.click()
        assert "Disponible avec le moteur" in page.inner_text("#toast")
        page.click("a[data-nav=style]"); page.wait_for_selector(".edit .shot img:not([hidden])")
        page.locator(".chip", has_text="Sport").click(); page.wait_for_timeout(500)
        assert page.get_attribute(".edit .shot img", "src") == "demo/styles/sport.jpg"
        assert not page.evaluate("document.documentElement.scrollWidth > window.innerWidth")
        assert page.errors == []
    finally:
        srv.terminate()


def test_upload_through_the_page_then_library_and_delete(page, live, media_long):
    base, app = live
    page.goto(base); page.wait_for_selector("#app h1")
    assert page.inner_text("#engine").strip() == "Moteur connecté" and "Aperçu sans moteur" not in page.inner_text("#app")
    page.locator(".seg button", has_text="30 s").click()
    page.set_input_files("input[type=file]", str(media_long))
    page.wait_for_selector("h1:has-text('long')", timeout=20000)
    page.wait_for_selector(".pc", timeout=90000)
    page.wait_for_function("document.querySelectorAll('.pc video').length >= 1", timeout=90000)
    assert page.locator("a:has-text('Tout télécharger')").is_visible()
    page.click("a[data-nav=biblio]"); page.wait_for_selector(".lib .pc")
    assert page.inner_text("#count") == "1" and "clips" in page.locator(".lib .pc .pc-body p").first.inner_text()
    page.locator(".lib button", has_text="Supprimer").click()
    page.locator(".lib button", has_text="Confirmer").click()
    page.wait_for_selector(".empty", timeout=10000)
    assert page.errors == []
    assert [s for s in app.projects.summaries()] == []


def test_reserve_restyle_and_style_editor(page, live, media_long, tmp_path, stub_engine):
    base, app = live
    f = tmp_path / "r.mp4"; shutil.copy(media_long, f)
    proj, _ = app.projects.create(file=f, filename="reserve.mp4", options=OPTS)
    page.goto(f"{base}/#p-{proj['id']}")
    page.wait_for_function("document.querySelectorAll('.pc').length >= 3", timeout=90000)
    page.wait_for_function("document.querySelectorAll('.pc video').length >= 2", timeout=90000)
    h2 = page.evaluate("[...document.querySelectorAll('.sect h2')].map(e=>e.textContent)")
    assert h2[0] == "Clips" and h2[1].startswith("En réserve")
    n_res = page.locator(".grid").nth(1).locator(".pc").count()
    # fabriquer un moment en réserve : la carte rejoint « Clips », dans l'ordre des rangs
    page.locator("button", has_text="Fabriquer ce clip").first.click()
    page.wait_for_function("document.querySelectorAll('.grid')[0].querySelectorAll('.pc video').length >= 3", timeout=90000)
    assert page.locator(".grid").nth(1).locator(".pc").count() == n_res - 1
    ranks = page.evaluate("[...document.querySelectorAll('.grid')[0].children].map(c=>+c.dataset.rank)")
    assert ranks == sorted(ranks)
    # refaire avec un autre style : un nouveau fichier est servi
    before = page.get_attribute(".pc video", "src")
    page.select_option("select[aria-label='Style des prochains rendus']", "sport"); page.wait_for_timeout(300)
    page.locator("button", has_text="Refaire avec le style choisi").first.click()
    page.wait_for_function(f"document.querySelector('.pc video') && document.querySelector('.pc video').getAttribute('src') !== '{before}'", timeout=90000)
    assert stub_engine[-1][2] == "sport"
    # « pourquoi cette note » : 9 critères
    page.locator("summary", has_text="Pourquoi cette note").first.click()
    assert page.locator(".pc .bars").first.locator("li").count() == 9
    # éditeur de style : aperçu réel (blob), enregistrement
    page.click("a[data-nav=style]")
    page.wait_for_function("(()=>{const i=document.querySelector('.edit .shot img');return i && !i.hidden && i.src.startsWith('blob:')})()", timeout=30000)
    a = page.get_attribute(".edit .shot img", "src")
    page.locator(".chip", has_text="Humour").click()
    page.wait_for_function(f"document.querySelector('.edit .shot img').src !== '{a}'", timeout=30000)
    page.fill("input[aria-label='Signature']", "@mon.compte"); page.wait_for_timeout(500)
    page.locator("button", has_text="Enregistrer comme style").click()
    page.wait_for_selector("#toast div")
    saved = json.loads((app.projects.root / "settings.json").read_text())["style"]
    assert saved["font"] == "bangers" and saved["signature"] == "@mon.compte"
    assert page.errors == []
