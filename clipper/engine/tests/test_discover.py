"""Découvrir : tendances et nouveautés par l'API YouTube Data, simulée par un petit serveur local (pas de clé ici)."""
import json
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import pytest

from clipengine import discover
from clipengine.discover import Discover, normalize, parse_channel_ref, parse_duration, resolve_channel
from clipengine.errors import UserError
from clipengine.server import make_server

CLIPPER = Path(__file__).resolve().parents[2]
KEY = "yt-test-key-0123456789"
UC_A, UC_B = "UC" + "a" * 22, "UC" + "b" * 22


def iso(hours_ago: float) -> str:
    return (datetime.now(timezone.utc) - timedelta(hours=hours_ago)).strftime("%Y-%m-%dT%H:%M:%SZ")


def item(vid, minutes=20, hours_ago=5, views=10_000, live="none", channel=UC_A, title=None, lang="fr"):
    return {"id": vid, "snippet": {"publishedAt": iso(hours_ago), "channelId": channel, "title": title or f"Vidéo {vid}",
                                   "channelTitle": "Chaîne " + channel[2:4], "liveBroadcastContent": live,
                                   "thumbnails": {"medium": {"url": f"https://i.ytimg.com/vi/{vid}/mqdefault.jpg"}},
                                   **({"defaultAudioLanguage": lang} if lang else {})},
            "contentDetails": {"duration": f"PT{minutes // 60}H{minutes % 60}M0S" if minutes >= 60 else f"PT{minutes}M0S"},
            "statistics": {"viewCount": str(views), "likeCount": str(views // 50)}}


class FakeYouTube:
    def __init__(self):
        self.pages: list[list[dict]] = [[]]                      # pages du classement « mostPopular »
        self.pages_by_cat: dict[str, list[list[dict]]] = {}      # classement propre à une catégorie (sinon `pages`)
        self.missing_cats: set[str] = set()                      # catégories sans classement : 404 comme chez YouTube
        self.videos: dict[str, dict] = {}
        self.uploads: dict[str, list[str]] = {}
        self.channels: dict[str, dict] = {}
        self.fail: list[tuple[int, str, str]] = []
        self.requests: list[dict] = []
        self.anonymous_ok = False                                 # vrai : le proxy du secret réseau ajoute la clé
        outer = self

        class H(BaseHTTPRequestHandler):
            def do_GET(self):
                u = urllib.parse.urlparse(self.path)
                q = {k: v[0] for k, v in urllib.parse.parse_qs(u.query).items()}
                headers = {k.lower(): v for k, v in self.headers.items()}
                outer.requests.append({"path": u.path, "q": q, "headers": headers})
                key = headers.get("x-goog-api-key") or q.get("key")
                if key is None and not outer.anonymous_ok:
                    return self.reply(403, {"error": {"code": 403, "message": "Method doesn't allow unregistered callers (callers "
                                                      "without established identity). Please use API Key or other form of API "
                                                      "consumer identity to call this API.", "errors": [{"reason": "forbidden"}]}})
                if key is not None and key != KEY:
                    return self.reply(400, {"error": {"code": 400, "message": "API key not valid. Please pass a valid API key.",
                                                      "errors": [{"reason": "badRequest"}]}})
                if outer.fail:
                    code, reason, msg = outer.fail.pop(0)
                    return self.reply(code, {"error": {"code": code, "message": msg, "errors": [{"reason": reason}]}})
                if u.path == "/videos" and q.get("chart") == "mostPopular":
                    cat = q.get("videoCategoryId")
                    if cat in outer.missing_cats:
                        return self.reply(404, {"error": {"code": 404, "message": "Requested entity was not found.",
                                                          "errors": [{"reason": "notFound"}]}})
                    pages = outer.pages_by_cat.get(cat, outer.pages)
                    n = int(q.get("pageToken", "0") or 0)
                    page = pages[n] if n < len(pages) else []
                    body = {"items": page}
                    if n + 1 < len(pages):
                        body["nextPageToken"] = str(n + 1)
                    return self.reply(200, body)
                if u.path == "/videos":
                    return self.reply(200, {"items": [outer.videos[i] for i in q["id"].split(",") if i in outer.videos]})
                if u.path == "/playlistItems":
                    ids = outer.uploads.get(q["playlistId"], [])
                    return self.reply(200, {"items": [{"contentDetails": {"videoId": i}} for i in ids]})
                if u.path == "/channels":
                    ref = q.get("id") or q.get("forHandle") or q.get("forUsername")
                    ch = outer.channels.get(ref)
                    return self.reply(200, {"items": [ch] if ch else []})
                self.reply(404, {"error": {"code": 404, "message": "?", "errors": [{"reason": "notFound"}]}})

            def reply(self, code, payload):
                data = json.dumps(payload).encode()
                self.send_response(code)
                self.send_header("content-type", "application/json")
                self.send_header("content-length", str(len(data)))
                self.end_headers()
                self.wfile.write(data)

            def log_message(self, *a):
                pass

        self.server = ThreadingHTTPServer(("127.0.0.1", 0), H)
        self.url = f"http://127.0.0.1:{self.server.server_address[1]}"
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def add_channel(self, cid, title, handle=None):
        raw = {"id": cid, "snippet": {"title": title, "thumbnails": {"default": {"url": f"https://yt3.ggpht.com/{cid}"}}}}
        for ref in (cid, handle):
            if ref:
                self.channels[ref] = raw

    def hits(self, path):
        return [r for r in self.requests if r["path"] == path]


@pytest.fixture()
def yt(monkeypatch):
    fake = FakeYouTube()
    monkeypatch.setenv("YOUTUBE_API_KEY", KEY)
    monkeypatch.setenv("CLIPENGINE_YT_URL", fake.url)
    yield fake
    fake.server.shutdown()


# ----------------------------------------------------------------------------- lecture des données

def test_iso_durations_are_parsed():
    assert parse_duration("PT24M13S") == 1453 and parse_duration("PT1H2M") == 3720 and parse_duration("P1DT1H") == 90000
    assert parse_duration("PT45S") == 45 and parse_duration("") == 0 and parse_duration("n'importe quoi") == 0


def test_a_video_is_normalised_for_the_page():
    now = time.time()
    raw = item("abc", minutes=25, hours_ago=10, views=50_000)
    v = normalize(raw, now)
    assert v["url"] == "https://www.youtube.com/watch?v=abc" and v["duration"] == 1500 and v["live"] is False
    assert abs(v["age_hours"] - 10) < 0.1 and abs(v["views_per_hour"] - 5000) < 60 and v["language"] == "fr"
    assert v["thumbnail"].endswith("/abc/mqdefault.jpg") and v["channel_id"] == UC_A
    assert normalize({**raw, "statistics": {}}, now)["views"] == 0                    # statistiques masquées


# ----------------------------------------------------------------------------- tendances

def test_trending_keeps_long_recent_non_live_videos_and_ranks_by_views_per_hour(yt, tmp_path):
    yt.pages = [[item("slow", views=20_000, hours_ago=20),                           # 1 000 vues/h
                 item("fast", views=60_000, hours_ago=4),                            # 15 000 vues/h
                 item("short", minutes=3, views=900_000, hours_ago=2),               # trop courte
                 item("live", live="live", views=900_000, hours_ago=2),              # direct
                 item("old", views=900_000, hours_ago=24 * 30),                      # trop ancienne
                 item("huge", minutes=300, views=900_000, hours_ago=3)],             # plus de 4 h : refusée par le moteur
                [item("page2", views=30_000, hours_ago=3)]]                          # 10 000 vues/h, 2e page
    D = Discover(tmp_path)
    got = D.trending(category="0", days=3, min_min=10)
    assert [v["id"] for v in got] == ["fast", "page2", "slow"]
    assert len(yt.hits("/videos")) == 2                                              # deux pages = deux unités de quota
    assert all(r["q"]["regionCode"] == "FR" for r in yt.requests)


def test_trending_is_cached_so_that_filters_cost_no_quota(yt, tmp_path):
    yt.pages = [[item("a", hours_ago=3), item("b", hours_ago=60)]]
    D = Discover(tmp_path)
    assert [v["id"] for v in D.trending(category="0", days=1)] == ["a"]
    assert sorted(v["id"] for v in D.trending(category="0", days=7)) == ["a", "b"]
    assert len(yt.hits("/videos")) == 1                                              # même liste, filtrée autrement
    D.trending(category="0", days=7, fresh=True)
    assert len(yt.hits("/videos")) == 2                                              # « Actualiser » interroge à nouveau
    D.trending(category="28", fresh=True)
    assert yt.hits("/videos")[-1]["q"]["videoCategoryId"] == "28"


def test_youtube_errors_are_explained_and_never_leak_the_key(yt, tmp_path, monkeypatch):
    D = Discover(tmp_path)
    cases = [(403, "quotaExceeded", "quota", "quota YouTube du jour"),
             (403, "accessNotConfigured", "YouTube Data API v3 has not been used in project 1 before or it is disabled.",
              "pas activée"),
             (403, "forbidden", "Requests from referer <empty> are blocked.", "restreinte"),
             (404, "videoChartNotFound", "chart", "pas de classement"),
             (500, "backendError", "oups", "a refusé la requête \\(500\\)")]
    for code, reason, msg, expected in cases:
        yt.fail.append((code, reason, msg))
        with pytest.raises(UserError, match=expected) as e:
            D.trending(fresh=True)
        assert KEY not in str(e.value)
    monkeypatch.setenv("YOUTUBE_API_KEY", "mauvaise-cle")
    with pytest.raises(UserError, match="clé YouTube refusée") as e:
        D.trending(fresh=True)
    assert "mauvaise-cle" not in str(e.value)
    monkeypatch.delenv("YOUTUBE_API_KEY")
    with pytest.raises(UserError, match="YOUTUBE_API_KEY"):
        D.trending(fresh=True)
    monkeypatch.setenv("YOUTUBE_API_KEY", KEY)
    monkeypatch.setenv("CLIPENGINE_YT_URL", "http://127.0.0.1:9")
    with pytest.raises(UserError, match="injoignable"):
        D.trending(fresh=True)


def test_trending_rejects_odd_parameters(yt, tmp_path):
    for region, cat, lang in (("fr", "0", "fr"), ("FRA", "0", "fr"), ("FR", "x", "fr"), ("FR", "1234", "fr"), ("FR", "0", "FR1")):
        with pytest.raises(UserError):
            Discover(tmp_path).trending(region=region, category=cat, lang=lang)


# ----------------------------------------------------------------------------- chaînes suivies

def test_channel_references_are_understood():
    assert parse_channel_ref(UC_A) == ("id", UC_A)
    assert parse_channel_ref(f"https://www.youtube.com/channel/{UC_A}/videos") == ("id", UC_A)
    assert parse_channel_ref("https://www.youtube.com/@Framasoft") == ("handle", "@Framasoft")
    assert parse_channel_ref("@Framasoft") == ("handle", "@Framasoft") and parse_channel_ref("framasoft") == ("handle", "@framasoft")
    assert parse_channel_ref("https://youtube.com/c/Nom-Perso") == ("user", "Nom-Perso")
    for bad in ("", "a", "https://exemple.org/page avec espaces", "x" * 300):
        with pytest.raises(UserError):
            parse_channel_ref(bad)


def test_resolving_a_channel_by_id_handle_and_legacy_name(yt):
    yt.add_channel(UC_A, "Chaîne A", handle="@chaine-a")
    yt.add_channel(UC_B, "Chaîne B")
    yt.channels["ancien"] = yt.channels[UC_B]                                        # forUsername
    assert resolve_channel("@chaine-a")["id"] == UC_A and resolve_channel(UC_B)["title"] == "Chaîne B"
    assert resolve_channel("https://youtube.com/c/ancien")["id"] == UC_B
    with pytest.raises(UserError, match="introuvable"):
        resolve_channel("@inconnu-total")


def test_followed_channels_list_their_new_long_videos(yt, tmp_path):
    yt.add_channel(UC_A, "Chaîne A", handle="@chaine-a")
    yt.uploads["UU" + "a" * 22] = ["v1", "v2", "v3", "v4"]                           # liste des mises en ligne = UU…
    yt.videos = {"v1": item("v1", hours_ago=2), "v2": item("v2", hours_ago=30), "v3": item("v3", minutes=2, hours_ago=1),
                 "v4": item("v4", live="upcoming", hours_ago=1)}
    D = Discover(tmp_path)
    chan = D.add_channel("@chaine-a")
    assert chan["id"] == UC_A and (tmp_path / "watch.json").exists()
    with pytest.raises(UserError, match="déjà suivie"):
        D.add_channel(UC_A)
    time.sleep(0.01)
    out = D.channels()
    assert len(out) == 1 and [v["id"] for v in out[0]["videos"]] == ["v1", "v2"]    # courte et à venir écartées, récente d'abord
    assert out[0]["new"] == 0                                                        # rien de paru depuis l'ajout
    data = json.loads((tmp_path / "watch.json").read_text())
    data["channels"][0]["seen"] = time.time() - 6 * 3600                             # dernière visite il y a 6 h
    (tmp_path / "watch.json").write_text(json.dumps(data))
    out = D.channels(fresh=True)
    assert out[0]["new"] == 1 and [v["new"] for v in out[0]["videos"]] == [True, False]
    D.mark_seen()
    assert D.channels()[0]["new"] == 0
    assert D.remove_channel(UC_A) is True and D.remove_channel(UC_A) is False and D.channels() == []


def test_a_broken_channel_does_not_hide_the_others(yt, tmp_path):
    yt.add_channel(UC_A, "A")
    yt.add_channel(UC_B, "B")
    yt.uploads["UU" + "b" * 22] = ["w1"]
    yt.videos = {"w1": item("w1", channel=UC_B)}
    D = Discover(tmp_path)
    D.add_channel(UC_A)
    D.add_channel(UC_B)
    yt.fail.append((404, "playlistNotFound", "x"))                                   # la première chaîne échoue
    out = D.channels(fresh=True)
    assert out[0]["error"] == "chaîne introuvable" and out[0]["videos"] == []
    assert out[1]["error"] == "" and [v["id"] for v in out[1]["videos"]] == ["w1"]


def test_followed_channels_are_limited_and_the_file_survives_garbage(yt, tmp_path):
    (tmp_path / "watch.json").write_text("pas du json")
    assert Discover(tmp_path).channels() == []
    (tmp_path / "watch.json").write_text(json.dumps({"channels": [{"id": "pas-un-id"}, 3, {"id": UC_A, "title": "A"}]}))
    assert [c["id"] for c in Discover(tmp_path)._read()] == [UC_A]


# ----------------------------------------------------------------------------- routes de l'API

@pytest.fixture()
def api(tmp_path, yt):
    srv, app = make_server(CLIPPER, tmp_path / "data", port=0)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{srv.server_address[1]}"

    def call(method, path, body=None, ctype="application/json"):
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(base + path, data=data, method=method, headers={"Content-Type": ctype} if data else {})
        try:
            with urllib.request.urlopen(req) as r:
                return r.status, json.load(r)
        except urllib.error.HTTPError as e:
            return e.code, json.load(e)

    yield call
    srv.shutdown()
    app.close()


def test_discover_routes_end_to_end(api, yt):
    yt.pages = [[item("t1", views=40_000, hours_ago=4), item("t2", views=10_000, hours_ago=40)]]
    yt.add_channel(UC_A, "Chaîne A", handle="@chaine-a")
    yt.uploads["UU" + "a" * 22] = ["v1"]
    yt.videos = {"v1": item("v1", hours_ago=1)}
    assert api("GET", "/api/discover/status") == (200, {"enabled": True, "via": "env"})
    code, d = api("GET", "/api/discover/trending?days=1&min=10")
    assert code == 200 and [v["id"] for v in d["videos"]] == ["t1"] and "thumbnail" in d["videos"][0]
    code, d = api("GET", "/api/discover/trending?days=nimporte&min=-5&category=0&region=fr&fresh=1")
    assert code == 200                                                               # valeurs absurdes ramenées aux bornes
    assert api("POST", "/api/discover/channels", {"query": "@chaine-a"})[1]["channel"]["id"] == UC_A
    assert api("POST", "/api/discover/channels", {"query": "@chaine-a"})[0] == 400          # déjà suivie
    code, d = api("GET", "/api/discover/channels")
    assert code == 200 and d["channels"][0]["videos"][0]["id"] == "v1"
    assert api("POST", "/api/discover/seen", {})[0] == 200
    assert api("DELETE", f"/api/discover/channels/{UC_A}")[0] == 200
    assert api("DELETE", f"/api/discover/channels/{UC_A}")[0] == 404
    assert api("DELETE", "/api/discover/channels/pas-un-id")[0] == 404
    assert api("GET", "/api/discover/inconnu")[0] == 404


def test_discover_posts_need_a_non_simple_content_type_and_no_key_gives_a_clear_message(api, yt, monkeypatch):
    assert api("POST", "/api/discover/channels", {"query": "@chaine-a"}, ctype="text/plain")[0] == 415   # pas de requête forgée
    assert api("POST", "/api/discover/seen", {}, ctype="application/x-www-form-urlencoded")[0] == 415
    monkeypatch.delenv("YOUTUBE_API_KEY")
    assert api("GET", "/api/discover/status") == (200, {"enabled": False, "via": ""})
    code, d = api("GET", "/api/discover/trending")
    assert code == 400 and "YOUTUBE_API_KEY" in d["error"]
    code, d = api("GET", "/api/discover/channels")
    assert code == 200 and d["channels"] == []                                       # rien de suivi : pas d'appel, pas d'erreur


def test_check_youtube_says_whether_the_key_works_and_why_not(yt, monkeypatch, capsys):
    from clipengine.cli import main
    assert discover.check()[0] is True and main(["check-youtube"]) == 0 and capsys.readouterr().out.startswith("✓")
    assert yt.hits("/videos")[-1]["q"]["maxResults"] == "1"                          # un seul résultat : 1 unité de quota
    yt.fail.append((403, "quotaExceeded", "q"))
    assert discover.check() == (False, "quota YouTube du jour atteint : reviens demain")
    monkeypatch.setenv("YOUTUBE_API_KEY", "fausse")
    ok, msg = discover.check()
    assert not ok and "refusée" in msg and "fausse" not in msg and main(["check-youtube"]) == 1
    monkeypatch.delenv("YOUTUBE_API_KEY")
    ok, msg = discover.check()
    assert not ok and "YOUTUBE_API_KEY" in msg and "X-Goog-Api-Key" in msg


def test_the_key_travels_in_a_header_never_in_the_url(yt, tmp_path):
    yt.pages = [[item("a", hours_ago=3)]]
    Discover(tmp_path).trending()
    r = yt.requests[-1]
    assert r["headers"]["x-goog-api-key"] == KEY and "key" not in r["q"] and KEY not in r["path"]


def test_a_network_secret_works_without_the_variable(yt, tmp_path, monkeypatch):
    """Secret réseau : la session ne voit pas la clé, le proxy ajoute l'en-tête ; sans variable, le module essaie quand même."""
    monkeypatch.delenv("YOUTUBE_API_KEY")
    D = Discover(tmp_path)
    assert D.status() == {"enabled": False, "via": ""}                               # ni variable ni secret
    with pytest.raises(UserError, match="X-Goog-Api-Key"):
        D.trending(fresh=True)
    yt.anonymous_ok = True                                                           # le proxy ajoute la clé
    assert D.status() == {"enabled": True, "via": "secret"}                          # l'absence de clé n'a pas été mémorisée
    yt.pages = [[item("a", hours_ago=3)]]
    assert [v["id"] for v in D.trending(fresh=True)] == ["a"]
    n = len(yt.requests)
    assert D.status()["enabled"] is True and len(yt.requests) == n                   # sondage gardé : aucun appel de plus
    ok, msg = discover.check()
    assert ok and "secret réseau" in msg and "x-goog-api-key" not in yt.requests[-1]["headers"]


def test_a_non_key_failure_while_probing_does_not_hide_the_tab(yt, tmp_path, monkeypatch):
    monkeypatch.delenv("YOUTUBE_API_KEY")
    yt.anonymous_ok = True
    yt.fail.append((500, "backendError", "oups"))
    st = Discover(tmp_path).status()
    assert st["enabled"] is True and "500" in st["error"]


def test_language_filter_defaults_to_french_and_can_be_lifted(yt, tmp_path):
    yt.pages = [[item("fr1", views=20_000), item("en1", views=90_000, lang="en"), item("inconnue", views=50_000, lang="")]]
    D = Discover(tmp_path)
    assert [v["id"] for v in D.trending(category="0")] == ["fr1"]
    assert [v["id"] for v in D.trending(category="0", lang="")] == ["en1", "inconnue", "fr1"]
    assert [v["id"] for v in D.trending(category="0", lang="en")] == ["en1"]


def test_talk_merges_news_blogs_science_and_entertainment_and_skips_missing_charts(yt, tmp_path):
    yt.pages_by_cat = {"25": [[item("news", views=50_000)]], "22": [[item("blog", views=80_000), item("news", views=50_000)]],
                       "24": [[item("fun", views=10_000)]]}
    yt.missing_cats = {"28"}                                                         # pas de classement « sciences » : ignoré
    D = Discover(tmp_path)
    got = D.trending()                                                               # catégorie par défaut : « talk »
    assert [v["id"] for v in got] == ["blog", "news", "fun"]                         # fusionnées, sans doublon, par vues/h
    assert sorted(r["q"]["videoCategoryId"] for r in yt.hits("/videos")) == ["22", "24", "25", "28"]
    yt.missing_cats = {"25", "22", "28", "24"}
    with pytest.raises(UserError, match="pas de classement"):
        D.trending(fresh=True)
    yt.missing_cats = {"20"}
    with pytest.raises(UserError, match="pas de classement"):                        # une catégorie seule : erreur claire
        D.trending(category="20", fresh=True)
