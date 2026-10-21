"""API HTTP minimale (stdlib) : dépôt d'une vidéo ou d'une URL -> job -> clips.

    POST /api/jobs                 JSON {"url": "...", "options": {...}}  ou  corps binaire + ?filename=x.mp4
    GET  /api/jobs/<id>            état, progression, journal, clips
    GET  /files/<id>/<fichier>     mp4 / json / ass (requêtes Range gérées pour la lecture vidéo)
    GET  /api/health
    GET  /...                      pages statiques du dossier --web

Un seul job tourne à la fois (le rendu occupe tous les cœurs) ; les autres attendent en file.
"""
from __future__ import annotations

import json
import mimetypes
import os
import queue
import re
import secrets
import shutil
import threading
import time
from dataclasses import dataclass, field
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

from .config import Config
from .pipeline import run as run_pipeline

MAX_UPLOAD = int(os.environ.get("CLIPENGINE_MAX_UPLOAD", 2 * 1024**3))
TTL = 24 * 3600
ALLOWED_EXT = {".mp4", ".mov", ".mkv", ".webm", ".m4v", ".avi"}
ID_RE = re.compile(r"^[0-9a-f]{16}$")
FILE_RE = re.compile(r"^[A-Za-z0-9_\-.]+\.(mp4|json|ass)$")

# options que le client peut régler, avec bornes
OPTION_LIMITS = {
    "max_clips": (int, 1, 12), "min_dur": (float, 8, 90), "max_dur": (float, 15, 120),
}
OPTION_CHOICES = {"layout": {"auto", "track", "fit", "split"}}
OPTION_BOOLS = {"captions"}


@dataclass
class Job:
    id: str
    source: str
    options: dict
    status: str = "queued"            # queued | running | done | error
    stage: str = "En attente"
    progress: float = 0.0
    log: list[str] = field(default_factory=list)
    clips: list[dict] = field(default_factory=list)
    error: str = ""
    created: float = field(default_factory=time.time)

    def public(self, queue_pos: int = 0) -> dict:
        return {"id": self.id, "status": self.status, "stage": self.stage, "progress": round(self.progress, 3),
                "log": self.log[-12:], "clips": self.clips, "error": self.error, "queue": queue_pos}


def clean_options(raw: dict) -> dict:
    out: dict = {}
    for k, (typ, lo, hi) in OPTION_LIMITS.items():
        if k in raw:
            try:
                out[k] = max(lo, min(hi, typ(raw[k])))
            except (TypeError, ValueError):
                pass
    for k, choices in OPTION_CHOICES.items():
        if raw.get(k) in choices:
            out[k] = raw[k]
    for k in OPTION_BOOLS:
        if isinstance(raw.get(k), bool):
            out[k] = raw[k]
    return out


class App:
    def __init__(self, web_dir: Path, data_dir: Path, runner=run_pipeline, base_cfg: dict | None = None):
        self.web_dir = Path(web_dir).resolve()
        self.data_dir = Path(data_dir).resolve()
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.runner = runner
        self.base_cfg = base_cfg or {}
        self.jobs: dict[str, Job] = {}
        self.order: list[str] = []
        self.q: queue.Queue[str] = queue.Queue()
        self.lock = threading.Lock()
        self._stop = False
        self.worker = threading.Thread(target=self._work, daemon=True)
        self.worker.start()

    # ------------------------------------------------------------------ jobs
    def job_dir(self, jid: str) -> Path:
        return self.data_dir / jid

    def submit(self, source: str, options: dict) -> Job:
        self._gc()
        job = Job(secrets.token_hex(8), source, clean_options(options))
        with self.lock:
            self.jobs[job.id] = job
            self.order.append(job.id)
        self.q.put(job.id)
        return job

    def queue_pos(self, job: Job) -> int:
        """Rang dans la file (1 = prochain), 0 si le job n'attend pas."""
        if job.status != "queued":
            return 0
        with self.lock:
            waiting = [j for j in self.order if self.jobs[j].status == "queued"]
        return waiting.index(job.id) + 1 if job.id in waiting else 0

    def _gc(self) -> None:
        now = time.time()
        for d in self.data_dir.iterdir():
            if d.is_dir() and ID_RE.match(d.name) and now - d.stat().st_mtime > TTL:
                shutil.rmtree(d, ignore_errors=True)
                with self.lock:
                    self.jobs.pop(d.name, None)
                    if d.name in self.order:
                        self.order.remove(d.name)

    def _work(self) -> None:
        while not self._stop:
            try:
                jid = self.q.get(timeout=0.5)
            except queue.Empty:
                continue
            job = self.jobs[jid]
            job.status, job.stage, job.progress = "running", "Démarrage", 0.02
            out = self.job_dir(jid) / "out"
            try:
                cfg = Config.from_dict({**self.base_cfg, **job.options})
                results = self.runner(job.source, out, cfg, render=True, log=lambda m: self._on_log(job, m))
                job.clips = [{
                    "index": r.index, "title": r.candidate.title, "hook": r.candidate.hook,
                    "score": round(r.candidate.score), "start": round(r.candidate.start, 1),
                    "duration": round(r.candidate.duration, 1), "reasons": r.candidate.reasons,
                    "why": r.candidate.why, "layouts": sorted(set(r.layouts)),
                    "video": f"/files/{jid}/{r.video.name}" if r.video else None,
                    "meta": f"/files/{jid}/{r.video.stem}.json" if r.video else None,
                } for r in results]
                job.status, job.stage, job.progress = "done", "Terminé", 1.0
                if not job.clips:
                    job.stage = "Aucun moment assez fort trouvé"
            except Exception as e:                      # le job échoue, pas le serveur
                job.status, job.stage = "error", "Échec"
                job.error = str(e)[:400]
            finally:
                src = Path(job.source)
                if src.is_absolute() and self.data_dir in src.parents:      # on libère la vidéo déposée
                    shutil.rmtree(src.parent, ignore_errors=True)

    @staticmethod
    def _on_log(job: Job, msg: str) -> None:
        job.log.append(msg.strip())
        m = re.search(r"clip (\d+)/(\d+)", msg)
        if m:
            i, n = int(m.group(1)), int(m.group(2))
            job.stage = f"Rendu du clip {i}/{n}"
            job.progress = 0.4 + 0.58 * (i - 1) / max(n, 1)
        elif "télécharg" in msg:
            job.stage, job.progress = "Téléchargement de la vidéo", 0.05
        elif "profil audio" in msg:
            job.stage, job.progress = "Analyse du son", 0.1
        elif "transcription" in msg:
            job.stage, job.progress = "Transcription", 0.2
        elif "candidats" in msg:
            job.stage, job.progress = "Recherche des meilleurs moments", 0.35
        elif "réévaluation" in msg:
            job.stage, job.progress = "Avis éditorial", 0.38

    def close(self) -> None:
        self._stop = True


def make_handler(app: App):
    class H(BaseHTTPRequestHandler):
        server_version = "clipengine"

        def log_message(self, *a):          # silencieux
            pass

        # ---------------------------------------------------------- helpers
        def _json(self, code: int, obj) -> None:
            body = json.dumps(obj, ensure_ascii=False).encode()
            self.send_response(code)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def _file(self, path: Path, download: bool = False) -> None:
            if not path.is_file():
                return self._json(404, {"error": "introuvable"})
            size = path.stat().st_size
            ctype = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
            start, end, code = 0, size - 1, 200
            rng = self.headers.get("Range")
            if rng:
                m = re.match(r"bytes=(\d*)-(\d*)$", rng.strip())
                if not m or (not m.group(1) and not m.group(2)):
                    self.send_response(416); self.send_header("Content-Range", f"bytes */{size}")
                    self.end_headers(); return
                if m.group(1):
                    start = int(m.group(1))
                    end = int(m.group(2)) if m.group(2) else size - 1
                else:
                    start = max(0, size - int(m.group(2)))
                if start >= size or start > end:
                    self.send_response(416); self.send_header("Content-Range", f"bytes */{size}")
                    self.end_headers(); return
                end = min(end, size - 1)
                code = 206
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Accept-Ranges", "bytes")
            self.send_header("Content-Length", str(end - start + 1))
            if code == 206:
                self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
            if download:
                self.send_header("Content-Disposition", f'attachment; filename="{path.name}"')
            self.end_headers()
            with open(path, "rb") as f:
                f.seek(start)
                left = end - start + 1
                while left > 0:
                    chunk = f.read(min(1 << 20, left))
                    if not chunk:
                        break
                    try:
                        self.wfile.write(chunk)
                    except (BrokenPipeError, ConnectionResetError):
                        return
                    left -= len(chunk)

        # ---------------------------------------------------------- GET
        def do_GET(self):
            u = urlparse(self.path)
            p = unquote(u.path)
            if p == "/api/health":
                return self._json(200, {"ok": True, "queued": app.q.qsize()})
            m = re.fullmatch(r"/api/jobs/([0-9a-f]{16})", p)
            if m:
                job = app.jobs.get(m.group(1))
                if not job:
                    return self._json(404, {"error": "job inconnu"})
                return self._json(200, job.public(app.queue_pos(job)))
            m = re.fullmatch(r"/files/([0-9a-f]{16})/([A-Za-z0-9_\-.]+)", p)
            if m and FILE_RE.match(m.group(2)):
                return self._file(app.job_dir(m.group(1)) / "out" / m.group(2),
                                  download="download" in parse_qs(u.query))
            if p.startswith("/api/") or p.startswith("/files/"):
                return self._json(404, {"error": "introuvable"})
            rel = "index.html" if p in ("", "/") else p.lstrip("/")
            target = (app.web_dir / rel).resolve()
            if app.web_dir not in target.parents and target != app.web_dir:
                return self._json(403, {"error": "interdit"})
            if target.is_dir():
                target = target / "index.html"
            if "engine" in target.relative_to(app.web_dir).parts[:1]:     # pas de code source servi
                return self._json(404, {"error": "introuvable"})
            self._file(target)

        # ---------------------------------------------------------- POST
        def do_POST(self):
            u = urlparse(self.path)
            if u.path != "/api/jobs":
                return self._json(404, {"error": "introuvable"})
            ctype = (self.headers.get("Content-Type") or "").split(";")[0].strip()
            try:
                length = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                return self._json(400, {"error": "Content-Length invalide"})

            if ctype == "application/json":
                if length > 64 * 1024:
                    return self._json(413, {"error": "requête trop grande"})
                try:
                    body = json.loads(self.rfile.read(length) or b"{}")
                except json.JSONDecodeError:
                    return self._json(400, {"error": "JSON invalide"})
                url = str(body.get("url", "")).strip()
                if not re.match(r"^https?://[^\s]+$", url):
                    return self._json(400, {"error": "URL http(s) attendue"})
                job = app.submit(url, body.get("options") or {})
                return self._json(202, job.public(app.queue_pos(job)))

            # dépôt de fichier : corps binaire brut
            name = os.path.basename(parse_qs(u.query).get("filename", ["video.mp4"])[0])
            ext = Path(name).suffix.lower()
            if ext not in ALLOWED_EXT:
                return self._json(415, {"error": f"format non supporté ({ext or 'sans extension'})"})
            if length <= 0:
                return self._json(400, {"error": "fichier vide"})
            if length > MAX_UPLOAD:
                return self._json(413, {"error": f"fichier trop volumineux (max {MAX_UPLOAD >> 20} Mo)"})
            jid_dir = app.data_dir / ("up-" + secrets.token_hex(6))
            jid_dir.mkdir()
            dest = jid_dir / f"source{ext}"
            got = 0
            with open(dest, "wb") as f:
                while got < length:
                    chunk = self.rfile.read(min(1 << 20, length - got))
                    if not chunk:
                        break
                    f.write(chunk)
                    got += len(chunk)
            if got != length:
                shutil.rmtree(jid_dir, ignore_errors=True)
                return self._json(400, {"error": "envoi interrompu"})
            opts = {}
            try:
                opts = json.loads(self.headers.get("X-Options") or "{}")
            except json.JSONDecodeError:
                pass
            job = app.submit(str(dest), opts)
            return self._json(202, job.public(app.queue_pos(job)))

    return H


def make_server(web_dir: Path, data_dir: Path, host: str = "127.0.0.1", port: int = 8080,
                runner=run_pipeline, base_cfg: dict | None = None) -> tuple[ThreadingHTTPServer, App]:
    app = App(web_dir, data_dir, runner, base_cfg)
    srv = ThreadingHTTPServer((host, port), make_handler(app))
    return srv, app


def serve_main(argv: list[str]) -> int:
    import argparse

    here = Path(__file__).resolve().parents[2]          # .../clipper
    p = argparse.ArgumentParser(prog="clipengine serve")
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=8080)
    p.add_argument("--web", default=str(here), help="dossier de la page (contient index.html)")
    p.add_argument("--data", default=str(here / "engine" / "data"), help="dossier des jobs")
    p.add_argument("--model", default=os.environ.get("CLIPENGINE_MODEL", "small"), help="modèle Whisper")
    p.add_argument("--preset", default="veryfast", help="preset x264")
    a = p.parse_args(argv)
    srv, _ = make_server(Path(a.web), Path(a.data), a.host, a.port,
                         base_cfg={"whisper_model": a.model, "preset": a.preset})
    print(f"clipengine : http://{a.host}:{a.port}  (modèle Whisper {a.model})")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0
