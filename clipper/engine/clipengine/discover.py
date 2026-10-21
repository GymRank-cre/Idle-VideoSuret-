"""Découvrir : vidéos qui montent sur YouTube et nouveautés des chaînes suivies, par l'API YouTube Data v3.

Une clé Google gratuite suffit (`YOUTUBE_API_KEY`) : 10 000 unités par jour, et chaque appel d'ici en coûte une. Les
réponses sont gardées 15 minutes, si bien qu'une visite coûte de l'ordre de 5 à 30 unités.

Ce module ne télécharge rien : il liste des vidéos. Clipper la vidéo d'un autre créateur pour la republier demande son
accord ; c'est pourquoi « Mes chaînes » (celles dont tu as l'accord) est mis au même rang que « Tendances ».
"""
from __future__ import annotations

import json
import os
import re
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

from .errors import UserError

API = "https://www.googleapis.com/youtube/v3"
CACHE_S = 900
MAX_CHANNELS = 50
CHANNEL_ID = re.compile(r"^UC[\w-]{22}$")


def api_key() -> str:
    return os.environ.get("YOUTUBE_API_KEY", "").strip()


def explain_error(code: int, body: str) -> str:
    """Cause lisible d'un refus de l'API YouTube ; la clé n'apparaît jamais dans un message."""
    try:
        err = json.loads(body).get("error", {})
        reason = str((err.get("errors") or [{}])[0].get("reason", ""))
        msg = str(err.get("message", ""))
    except (ValueError, AttributeError, IndexError):
        reason, msg = "", body[:120]
    low = msg.lower()
    if reason == "keyInvalid" or "api key not valid" in low or "api_key_invalid" in low:
        return "clé YouTube refusée (YOUTUBE_API_KEY invalide)"
    if reason in ("quotaExceeded", "dailyLimitExceeded", "rateLimitExceeded", "userRateLimitExceeded"):
        return "quota YouTube du jour atteint : reviens demain"
    if reason == "accessNotConfigured" or "has not been used" in low or "is disabled" in low:
        return "l'API « YouTube Data API v3 » n'est pas activée pour le projet de cette clé (bibliothèque d'API)"
    if reason in ("ipRefererBlocked", "forbidden") or "referer" in low or "ip address" in low:
        return "la clé est restreinte à d'autres adresses ou sites : retirer la restriction d'application"
    if reason in ("videoChartNotFound", "invalidChart") or "chart" in low and code == 404:
        return "pas de classement pour ce pays ou cette catégorie"
    if reason in ("channelNotFound", "playlistNotFound"):
        return "chaîne introuvable"
    return f"YouTube a refusé la requête ({code}) : {msg[:120]}" if msg else f"YouTube a refusé la requête ({code})"


def yt_get(path: str, params: dict) -> dict:
    """GET sur l'API ; UserError lisible en cas d'échec. L'adresse de base se change par CLIPENGINE_YT_URL (essais)."""
    key = api_key()
    if not key:
        raise UserError("Pas de clé YouTube : définir YOUTUBE_API_KEY (clé gratuite « YouTube Data API v3 »).")
    base = os.environ.get("CLIPENGINE_YT_URL", API).rstrip("/")
    url = f"{base}/{path}?{urllib.parse.urlencode({**params, 'key': key})}"
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers={"Accept": "application/json"}), timeout=20) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        raise UserError(explain_error(e.code, e.read().decode("utf-8", "replace"))) from None
    except (urllib.error.URLError, TimeoutError, OSError, ValueError):
        raise UserError("YouTube injoignable : vérifier la connexion") from None


def check() -> tuple[bool, str]:
    """Un appel réel (1 unité de quota) : dit si la clé YouTube marche, et pourquoi sinon."""
    if not api_key():
        return False, "YOUTUBE_API_KEY absente de l'environnement"
    try:
        yt_get("videos", {"part": "id", "chart": "mostPopular", "regionCode": "FR", "maxResults": 1})
    except UserError as e:
        return False, str(e)
    return True, "YouTube répond (cet essai a coûté 1 unité de quota sur 10 000 par jour)"


def parse_duration(iso: str) -> int:
    m = re.fullmatch(r"P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+)S)?)?", iso or "")
    if not m:
        return 0
    d, h, mi, s = (int(x or 0) for x in m.groups())
    return ((d * 24 + h) * 60 + mi) * 60 + s


def _ts(iso: str) -> float:
    try:
        return datetime.fromisoformat((iso or "").replace("Z", "+00:00")).astimezone(timezone.utc).timestamp()
    except ValueError:
        return 0.0


def normalize(item: dict, now: float | None = None) -> dict:
    """Une vidéo de l'API sous la forme que la page affiche."""
    now = time.time() if now is None else now
    sn, st = item.get("snippet", {}), item.get("statistics", {})
    pub = _ts(sn.get("publishedAt", ""))
    age_h = max(0.0, (now - pub) / 3600) if pub else 0.0
    views = int(st.get("viewCount") or 0)
    th = sn.get("thumbnails", {})
    thumb = (th.get("medium") or th.get("high") or th.get("default") or {}).get("url", "")
    return {
        "id": item.get("id", ""), "url": f"https://www.youtube.com/watch?v={item.get('id', '')}",
        "title": sn.get("title", ""), "channel": sn.get("channelTitle", ""), "channel_id": sn.get("channelId", ""),
        "thumbnail": thumb, "duration": parse_duration(item.get("contentDetails", {}).get("duration", "")),
        "published": pub, "age_hours": round(age_h, 1), "views": views,
        "likes": int(st.get("likeCount") or 0), "views_per_hour": round(views / max(age_h, 1.0)),
        "live": sn.get("liveBroadcastContent", "none") != "none",
        "language": sn.get("defaultAudioLanguage") or sn.get("defaultLanguage") or "",
    }


def _keep(v: dict, min_s: float, max_s: float = 4 * 3600) -> bool:
    """Le moteur refuse les directs, les vidéos trop courtes (< 20 s) et de plus de 4 h : inutile de les proposer."""
    return not v["live"] and min_s <= v["duration"] <= max_s


def _videos(ids: list[str]) -> list[dict]:
    out: list[dict] = []
    for i in range(0, len(ids), 50):
        d = yt_get("videos", {"part": "snippet,contentDetails,statistics", "id": ",".join(ids[i:i + 50])})
        out += [normalize(it) for it in d.get("items", [])]
    return out


def parse_channel_ref(text: str) -> tuple[str, str]:
    """('id', 'UC…') | ('handle', '@nom') | ('user', 'nom') depuis une adresse de chaîne, un @nom ou un identifiant."""
    t = (text or "").strip()
    if len(t) > 200:
        raise UserError("Adresse de chaîne trop longue.")
    if CHANNEL_ID.match(t):
        return "id", t
    if m := re.search(r"youtube\.com/channel/(UC[\w-]{22})", t):
        return "id", m.group(1)
    if m := re.search(r"youtube\.com/(@[\w.\-]{3,30})", t):
        return "handle", m.group(1)
    if m := re.search(r"youtube\.com/(?:c|user)/([\w.\-]{2,40})", t):
        return "user", m.group(1)
    if re.fullmatch(r"@?[\w.\-]{3,30}", t):
        return "handle", "@" + t.lstrip("@")
    raise UserError("Colle l'adresse de la chaîne (youtube.com/@nom) ou son identifiant (UC…).")


def resolve_channel(text: str) -> dict:
    kind, ref = parse_channel_ref(text)
    tries = [{"id": ref}] if kind == "id" else [{"forHandle": ref}] if kind == "handle" \
        else [{"forHandle": "@" + ref}, {"forUsername": ref}]
    for params in tries:
        items = yt_get("channels", {"part": "snippet", **params}).get("items") or []
        if items:
            it = items[0]
            th = it.get("snippet", {}).get("thumbnails", {})
            return {"id": it["id"], "title": it.get("snippet", {}).get("title", ref),
                    "thumbnail": (th.get("default") or th.get("medium") or {}).get("url", "")}
    raise UserError("Chaîne introuvable : vérifier l'adresse.")


class Discover:
    """Tendances et nouveautés ; état (chaînes suivies, dernière visite) dans `watch.json` à côté des projets."""

    def __init__(self, root: Path):
        self.file = Path(root) / "watch.json"
        self._lock = threading.Lock()
        self._cache: dict[tuple, tuple[float, object]] = {}

    # ------------------------------------------------------------------ état
    @staticmethod
    def enabled() -> bool:
        return bool(api_key())

    def _read(self) -> list[dict]:
        try:
            d = json.loads(self.file.read_text(encoding="utf-8"))
            return [c for c in d.get("channels", []) if isinstance(c, dict) and CHANNEL_ID.match(str(c.get("id", "")))]
        except (OSError, ValueError, AttributeError):
            return []

    def _write(self, chans: list[dict]) -> None:
        tmp = self.file.with_suffix(".tmp")
        tmp.write_text(json.dumps({"channels": chans}, ensure_ascii=False), encoding="utf-8")
        os.replace(tmp, self.file)

    def _cached(self, key: tuple, fresh: bool, fn):
        with self._lock:
            hit = self._cache.get(key)
            if hit and not fresh and time.time() - hit[0] < CACHE_S:
                return hit[1]
        val = fn()
        with self._lock:
            self._cache[key] = (time.time(), val)
        return val

    # ------------------------------------------------------------------ tendances
    def trending(self, region: str = "FR", category: str = "0", days: float = 3, min_min: float = 10,
                 fresh: bool = False) -> list[dict]:
        """Vidéos populaires récentes, longues, classées par vues par heure (celles qui montent d'abord)."""
        if not re.fullmatch(r"[A-Z]{2}", region) or not re.fullmatch(r"\d{1,3}", category):
            raise UserError("Pays ou catégorie invalide.")

        def load():
            items, token = [], None
            for _ in range(4):                                     # jusqu'à 200 vidéos pour 4 unités de quota
                p = {"part": "snippet,contentDetails,statistics", "chart": "mostPopular", "regionCode": region,
                     "maxResults": 50, **({"videoCategoryId": category} if category != "0" else {}),
                     **({"pageToken": token} if token else {})}
                d = yt_get("videos", p)
                items += d.get("items", [])
                token = d.get("nextPageToken")
                if not token:
                    break
            now = time.time()
            return [normalize(it, now) for it in items]

        every = self._cached(("trending", region, category), fresh, load)
        keep = [v for v in every if _keep(v, min_min * 60) and v["age_hours"] <= days * 24]
        return sorted(keep, key=lambda v: v["views_per_hour"], reverse=True)[:40]

    # ------------------------------------------------------------------ chaînes suivies
    def add_channel(self, query: str) -> dict:
        info = resolve_channel(query)
        with self._lock:
            chans = self._read()
            if any(c["id"] == info["id"] for c in chans):
                raise UserError("Cette chaîne est déjà suivie.")
            if len(chans) >= MAX_CHANNELS:
                raise UserError(f"{MAX_CHANNELS} chaînes au plus.")
            now = time.time()
            chan = {**info, "added": now, "seen": now}
            self._write(chans + [chan])
        return chan

    def remove_channel(self, cid: str) -> bool:
        with self._lock:
            chans = self._read()
            kept = [c for c in chans if c["id"] != cid]
            if len(kept) == len(chans):
                return False
            self._write(kept)
            return True

    def mark_seen(self) -> None:
        with self._lock:
            now = time.time()
            self._write([{**c, "seen": now} for c in self._read()])

    def channels(self, min_min: float = 8, fresh: bool = False, per_channel: int = 6) -> list[dict]:
        """Chaînes suivies, chacune avec ses dernières vidéos longues ; `new` compte celles parues depuis la dernière visite."""
        out = []
        for c in self._read():
            def load(cid=c["id"]):
                pl = "UU" + cid[2:]                              # la liste des mises en ligne d'une chaîne UC… est UU…
                d = yt_get("playlistItems", {"part": "contentDetails", "playlistId": pl, "maxResults": 15})
                ids = [it["contentDetails"]["videoId"] for it in d.get("items", []) if it.get("contentDetails", {}).get("videoId")]
                return _videos(ids) if ids else []
            try:
                vids = self._cached(("uploads", c["id"]), fresh, load)
                err = ""
            except UserError as e:
                vids, err = [], str(e)
            vids = sorted((v for v in vids if _keep(v, min_min * 60)), key=lambda v: v["published"], reverse=True)[:per_channel]
            for v in vids:
                v["new"] = v["published"] > c.get("seen", 0)
            out.append({**c, "videos": vids, "new": sum(v["new"] for v in vids), "error": err})
        return out
