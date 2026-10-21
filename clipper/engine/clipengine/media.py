"""Sonde ffprobe, identité d'une vidéo, garde contre les adresses internes, téléchargement (yt-dlp)."""
from __future__ import annotations

import hashlib
import ipaddress
import json
import re
import socket
import subprocess
from dataclasses import dataclass
from fractions import Fraction
from pathlib import Path
from typing import Callable
from urllib.parse import parse_qs, urlparse

from .errors import UserError

MAX_SOURCE_S = 4 * 3600
MIN_SOURCE_S = 20


@dataclass
class MediaInfo:
    width: int
    height: int
    fps: float
    duration: float
    has_audio: bool

    def to_dict(self) -> dict:
        return {"width": self.width, "height": self.height, "fps": round(self.fps, 2),
                "duration": round(self.duration, 2), "has_audio": self.has_audio}


def probe(path: str | Path) -> MediaInfo:
    try:
        out = subprocess.run(
            ["ffprobe", "-v", "error", "-print_format", "json", "-show_streams", "-show_format", str(path)],
            capture_output=True, check=True, text=True).stdout
        d = json.loads(out)
        v = next(s for s in d["streams"] if s["codec_type"] == "video")
    except (subprocess.CalledProcessError, StopIteration, json.JSONDecodeError, KeyError):
        raise UserError("Ce fichier n'est pas une vidéo lisible.") from None
    w, h = int(v["width"]), int(v["height"])
    rot = 0
    for sd in v.get("side_data_list", []):
        if "rotation" in sd:
            rot = int(sd["rotation"])
    rot = int(v.get("tags", {}).get("rotate", rot))
    if abs(rot) % 180 == 90:            # ffmpeg décode déjà tourné
        w, h = h, w
    try:
        fps = float(Fraction(v.get("avg_frame_rate") or v["r_frame_rate"]))
    except (ZeroDivisionError, ValueError):
        fps = 30.0
    dur = float(d.get("format", {}).get("duration") or v.get("duration") or 0)
    return MediaInfo(w, h, fps or 30.0, dur, any(s["codec_type"] == "audio" for s in d["streams"]))


def check_info(info: MediaInfo, max_s: float = MAX_SOURCE_S) -> None:
    """Refus clairs, avant tout calcul coûteux."""
    if info.duration < MIN_SOURCE_S:
        raise UserError(f"Vidéo trop courte ({info.duration:.0f} s) : il en faut au moins {MIN_SOURCE_S} s.")
    if info.duration > max_s:
        raise UserError(f"Vidéo trop longue ({info.duration / 3600:.1f} h) : {max_s / 3600:.0f} h au maximum. "
                        "Coupe-la en parties.")
    if not info.has_audio:
        raise UserError("La vidéo n'a pas de piste audio : les moments sont repérés grâce à la parole.")


# ----------------------------------------------------------------------------- liens

_YT_HOSTS = {"youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com", "youtu.be"}


def video_key(url: str) -> str:
    """Identité stable d'une vidéo : deux liens vers la même vidéo donnent la même clé (détection des doublons)."""
    u = urlparse(url.strip())
    host = (u.hostname or "").lower()
    if host in _YT_HOSTS:
        vid = ""
        if host == "youtu.be":
            vid = u.path.strip("/").split("/")[0]
        elif u.path == "/watch":
            vid = parse_qs(u.query).get("v", [""])[0]
        else:
            m = re.match(r"^/(?:shorts|embed|live|v)/([\w-]{6,})", u.path)
            vid = m.group(1) if m else ""
        if vid:
            return f"yt:{vid}"
    if host.endswith("twitch.tv"):
        m = re.match(r"^/videos/(\d+)", u.path)
        if m:
            return f"tw:{m.group(1)}"
    norm = f"{host}{u.path}?{u.query}".rstrip("?")
    return "url:" + hashlib.sha1(norm.encode()).hexdigest()[:16]


def check_url(url: str) -> str:
    """Accepte http(s) vers une adresse publique ; refuse boucle locale, réseau privé, métadonnées cloud…

    Le serveur télécharge ce lien pour vous : sans ce garde, une adresse interne servirait à sonder la machine.
    """
    u = urlparse((url or "").strip())
    if u.scheme not in ("http", "https") or not u.hostname:
        raise UserError("Colle un lien http(s) complet.")
    try:
        infos = socket.getaddrinfo(u.hostname, u.port or (443 if u.scheme == "https" else 80), proto=socket.IPPROTO_TCP)
    except socket.gaierror:
        raise UserError("Adresse introuvable : vérifie le lien.") from None
    for fam, _, _, _, sockaddr in infos:
        ip = ipaddress.ip_address(sockaddr[0].split("%")[0])
        if not ip.is_global:
            raise UserError("Adresse interne refusée : seul un lien public est accepté.")
    return url.strip()


def check_remote(meta: dict, max_s: float = MAX_SOURCE_S) -> None:
    """Contrôle des métadonnées renvoyées par yt-dlp avant de télécharger."""
    if meta.get("_type") == "playlist" or (meta.get("entries") and not meta.get("duration")):
        raise UserError("Lien de playlist : ouvre la vidéo et copie son lien à elle.")
    if meta.get("is_live") or meta.get("live_status") in ("is_live", "is_upcoming"):
        raise UserError("Direct en cours : attends la fin du live puis colle le lien de la rediffusion.")
    dur = meta.get("duration")
    if dur and dur > max_s:
        raise UserError(f"Vidéo trop longue ({dur / 3600:.1f} h) : {max_s / 3600:.0f} h au maximum.")
    if dur and dur < MIN_SOURCE_S:
        raise UserError(f"Vidéo trop courte ({dur:.0f} s) : il en faut au moins {MIN_SOURCE_S} s.")


def fetch(url: str, out_dir: str | Path, max_s: float = MAX_SOURCE_S,
          log: Callable[[str], None] = print) -> tuple[Path, dict]:
    """Télécharge une vidéo (YouTube, Twitch, Vimeo… tout ce que yt-dlp sait lire), ≤ 1080p, en mp4."""
    try:
        import yt_dlp
    except ImportError as e:  # pragma: no cover
        raise RuntimeError("yt-dlp manquant : pip install 'clipengine[download]'") from e
    check_url(url)
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    base = {"quiet": True, "noprogress": True, "noplaylist": True, "socket_timeout": 30, "retries": 3}
    try:
        with yt_dlp.YoutubeDL({**base, "skip_download": True}) as ydl:
            meta = ydl.extract_info(url, download=False)
        check_remote(meta, max_s)
        last = [-10]

        def hook(d):
            if d.get("status") == "downloading" and d.get("total_bytes"):
                pct = int(100 * d["downloaded_bytes"] / d["total_bytes"])
                if pct >= last[0] + 10:
                    last[0] = pct
                    log(f"  téléchargement {pct} %")

        opts = {**base, "format": "bv*[height<=1080]+ba/b[height<=1080]/b", "merge_output_format": "mp4",
                "outtmpl": str(out_dir / "source.%(ext)s"), "progress_hooks": [hook]}
        with yt_dlp.YoutubeDL(opts) as ydl:
            ydl.extract_info(url, download=True)
    except yt_dlp.utils.DownloadError as e:
        msg = str(e)
        if re.search(r"private|members|sign in|age|unavailable|removed", msg, re.I):
            raise UserError("YouTube refuse l'accès à cette vidéo (privée, supprimée, réservée aux membres ou "
                            "soumise à une limite d'âge). Télécharge-la et envoie le fichier.") from None
        raise UserError("Téléchargement impossible : " + re.sub(r"\x1b\[[0-9;]*m|ERROR:\s*", "", msg)[:200]) from None
    path = next(out_dir.glob("source.*"))
    return path, {"title": meta.get("title") or "", "id": meta.get("id") or "",
                  "uploader": meta.get("uploader") or "", "duration": meta.get("duration")}


def download(url: str, out_dir: str | Path) -> Path:
    """Compatibilité : téléchargement simple."""
    return fetch(url, out_dir)[0]
