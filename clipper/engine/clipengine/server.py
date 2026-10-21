"""API HTTP (stdlib) + page : projets de clips pour un usage personnel.

    GET    /api/health
    GET    /api/projects                      liste (bibliothèque)
    POST   /api/projects                      JSON {"url", "options", "style", "force"}  ou  corps vidéo + ?filename=
    GET    /api/projects/<id>                 détail : moments, clips, progression
    DELETE /api/projects/<id>
    POST   /api/projects/<id>/render          {"ids": [..], "style": "pack" | {...}}   fabrique / refait des clips
    POST   /api/projects/<id>/retry           relance l'analyse après un échec
    POST   /api/projects/<id>/style           {"style": {...}}  style par défaut du projet
    GET    /api/projects/<id>/zip             tous les clips + publication.txt
    GET    /api/settings    POST /api/settings   {"style": {...}, "defaults": {...}}
    GET    /api/styles                        packs, polices, couleurs
    POST   /api/style-preview                 {"style": {...}} -> PNG rendu par libass
    GET    /files/<id>/<fichier>              mp4 / jpg / ass (requêtes Range gérées)
    GET    /...                               pages statiques du dossier --web

Sécurité : mot de passe optionnel (Basic), refus des adresses internes pour les liens, refus de tout JSON ou
dépôt qui n'a pas un type de contenu « non simple » (bloque les requêtes forgées depuis un autre site), contrôle
de l'en-tête Host sans mot de passe (bloque le DNS rebinding).
"""
from __future__ import annotations

import base64
import hmac
import json
import mimetypes
import os
import re
import secrets
import shutil
import tempfile
import threading
import time
import zipfile
from collections import OrderedDict
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, unquote, urlparse

from .errors import UserError
from .projects import ID_RE, Projects
from .styles import Style, catalog, preview_png

MAX_UPLOAD = int(os.environ.get("CLIPENGINE_MAX_UPLOAD", 8 * 1024**3))
FILE_RE = re.compile(r"^[A-Za-z0-9_\-.]+\.(mp4|jpg|ass|json)$")
LOOPBACK = {"localhost", "127.0.0.1", "[::1]", "::1"}


class App:
    def __init__(self, web_dir: Path, data_dir: Path, base_cfg: dict | None = None, password: str | None = None,
                 projects: Projects | None = None):
        self.web_dir = Path(web_dir).resolve()
        self.password = password or None
        self.projects = projects or Projects(Path(data_dir), base_cfg)
        self.incoming = self.projects.root / "_incoming"
        self.incoming.mkdir(exist_ok=True)
        for old in self.incoming.iterdir():                 # dépôts interrompus d'une session précédente
            shutil.rmtree(old, ignore_errors=True)
        self._previews: OrderedDict[str, bytes] = OrderedDict()
        self._preview_lock = threading.Lock()

    def preview(self, style: Style) -> bytes:
        key = json.dumps(style.to_dict(), sort_keys=True)
        with self._preview_lock:
            if key in self._previews:
                self._previews.move_to_end(key)
                return self._previews[key]
            png = preview_png(style, self.projects.root / "_preview")
            self._previews[key] = png
            if len(self._previews) > 64:
                self._previews.popitem(last=False)
            return png

    def close(self) -> None:
        self.projects.close()


def _host_only(h: str | None) -> str:
    h = (h or "").strip().lower()
    if h.startswith("["):
        return h.split("]")[0] + "]"
    return h.rsplit(":", 1)[0] if ":" in h else h


def make_handler(app: App):
    class H(BaseHTTPRequestHandler):
        server_version = "clipengine"

        def log_message(self, *a):
            pass

        # ---------------------------------------------------------- sorties
        def _send(self, code: int, body: bytes, ctype: str, extra: dict | None = None) -> None:
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            for k, v in (extra or {}).items():
                self.send_header(k, v)
            self.end_headers()
            self.wfile.write(body)

        def _json(self, code: int, obj, extra: dict | None = None) -> None:
            self._send(code, json.dumps(obj, ensure_ascii=False).encode(), "application/json; charset=utf-8", extra)

        def _err(self, code: int, msg: str) -> None:
            self._json(code, {"error": msg})

        def _file(self, path: Path, download: bool = False) -> None:
            if not path.is_file():
                return self._err(404, "introuvable")
            size = path.stat().st_size
            ctype = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
            start, end, code = 0, size - 1, 200
            rng = self.headers.get("Range")
            if rng:
                m = re.match(r"bytes=(\d*)-(\d*)$", rng.strip())
                bad = not m or (not m.group(1) and not m.group(2))
                if not bad:
                    if m.group(1):
                        start = int(m.group(1))
                        end = int(m.group(2)) if m.group(2) else size - 1
                    else:
                        start = max(0, size - int(m.group(2)))
                    bad = start >= size or start > end
                if bad:
                    self.send_response(416)
                    self.send_header("Content-Range", f"bytes */{size}")
                    self.send_header("Content-Length", "0")
                    self.end_headers()
                    return
                end = min(end, size - 1)
                code = 206
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Accept-Ranges", "bytes")
            self.send_header("Content-Length", str(end - start + 1))
            self.send_header("X-Content-Type-Options", "nosniff")
            if path.suffix in (".mp4", ".jpg"):
                self.send_header("Cache-Control", "private, max-age=3600")   # noms uniques par rendu
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

        # ---------------------------------------------------------- garde
        def _guard(self) -> bool:
            """Mot de passe (Basic) ou, à défaut, contrôle de l'en-tête Host. False = réponse déjà envoyée."""
            if app.password:
                auth = self.headers.get("Authorization", "")
                ok = False
                if auth.startswith("Basic "):
                    try:
                        _, _, pw = base64.b64decode(auth[6:]).decode("utf-8", "replace").partition(":")
                        ok = hmac.compare_digest(pw.encode(), app.password.encode())
                    except Exception:
                        ok = False
                if not ok:
                    time.sleep(0.4)                               # freine les essais en rafale
                    self._send(401, b'{"error":"mot de passe requis"}', "application/json",
                               {"WWW-Authenticate": 'Basic realm="Clipo", charset="UTF-8"'})
                    return False
                return True
            if _host_only(self.headers.get("Host")) not in LOOPBACK:
                self._err(403, "hôte refusé (aucun mot de passe défini : accès local uniquement)")
                return False
            return True

        def _body_json(self, limit: int = 256 * 1024):
            ctype = (self.headers.get("Content-Type") or "").split(";")[0].strip()
            if ctype != "application/json":
                self._err(415, "Content-Type application/json attendu")
                return None
            try:
                n = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                self._err(400, "Content-Length invalide")
                return None
            if n > limit:
                self._err(413, "requête trop grande")
                return None
            try:
                d = json.loads(self.rfile.read(n) or b"{}")
            except json.JSONDecodeError:
                self._err(400, "JSON invalide")
                return None
            return d if isinstance(d, dict) else {}

        # ---------------------------------------------------------- GET
        def do_GET(self):
            if not self._guard():
                return
            u = urlparse(self.path)
            p = unquote(u.path)
            P = app.projects
            if p == "/api/health":
                return self._json(200, {"ok": True, "auth": bool(app.password)})
            if p == "/api/projects":
                return self._json(200, {"projects": P.summaries()})
            if p == "/api/settings":
                return self._json(200, P.settings())
            if p == "/api/styles":
                return self._json(200, catalog())
            if m := re.fullmatch(r"/api/projects/([0-9a-f]{16})", p):
                d = P.public(m.group(1))
                return self._json(200, d) if d else self._err(404, "projet inconnu")
            if m := re.fullmatch(r"/api/projects/([0-9a-f]{16})/zip", p):
                return self._zip(m.group(1))
            if m := re.fullmatch(r"/files/([0-9a-f]{16})/([A-Za-z0-9_\-.]+)", p):
                if not P.exists(m.group(1)) or not FILE_RE.match(m.group(2)):
                    return self._err(404, "introuvable")
                return self._file(P.dir(m.group(1)) / "clips" / m.group(2), "download" in parse_qs(u.query))
            if p.startswith("/api/") or p.startswith("/files/"):
                return self._err(404, "introuvable")
            rel = "index.html" if p in ("", "/") else p.lstrip("/")
            target = (app.web_dir / rel).resolve()
            if app.web_dir not in target.parents and target != app.web_dir:
                return self._err(403, "interdit")
            if target.is_dir():
                target = target / "index.html"
            parts = target.relative_to(app.web_dir).parts
            if parts[:1] == ("engine",) or app.projects.root in target.parents or target == app.projects.root:
                return self._err(404, "introuvable")                    # ni code source ni données des projets
            self._file(target)

        def _zip(self, pid: str) -> None:
            proj = app.projects.public(pid)
            if proj is None:
                return self._err(404, "projet inconnu")
            done = [x for x in proj["proposals"] if x["state"] == "done" and x["video"]]
            if not done:
                return self._err(404, "aucun clip prêt")
            lines = []
            tmp = tempfile.SpooledTemporaryFile(max_size=64 * 1024 * 1024)
            with zipfile.ZipFile(tmp, "w", zipfile.ZIP_STORED) as z:
                for x in sorted(done, key=lambda r: r["rank"]):
                    f = app.projects.dir(pid) / "clips" / x["video"]
                    if f.is_file():
                        z.write(f, x["video"])
                        tags = " ".join(x["hashtags"])
                        lines.append(f"{x['video']}\nScore {x['score']} ({x['label']}) · {x['duration']:.0f} s\n"
                                     f"Titre : {x['title']}\nLégende : {x['caption']} {tags}\n")
                z.writestr("publication.txt", "\n".join(lines))
            size = tmp.tell()
            tmp.seek(0)
            name = re.sub(r"[^A-Za-z0-9_-]+", "-", proj["title"])[:40].strip("-") or "clips"
            self.send_response(200)
            self.send_header("Content-Type", "application/zip")
            self.send_header("Content-Length", str(size))
            self.send_header("Content-Disposition", f'attachment; filename="{name}.zip"')
            self.end_headers()
            shutil.copyfileobj(tmp, self.wfile)
            tmp.close()

        # ---------------------------------------------------------- DELETE
        def do_DELETE(self):
            if not self._guard():
                return
            m = re.fullmatch(r"/api/projects/([0-9a-f]{16})", urlparse(self.path).path)
            if not m:
                return self._err(404, "introuvable")
            return self._json(200, {"ok": True}) if app.projects.delete(m.group(1)) else self._err(404, "projet inconnu")

        # ---------------------------------------------------------- POST
        def do_POST(self):
            if not self._guard():
                return
            u = urlparse(self.path)
            p = u.path
            P = app.projects
            try:
                if p == "/api/projects":
                    return self._create(u)
                if p == "/api/style-preview":
                    b = self._body_json()
                    if b is None:
                        return
                    base = Style.from_dict(P.settings()["style"])
                    st = Style.from_dict(b.get("style"), base)
                    return self._send(200, app.preview(st), "image/png")
                if p == "/api/settings":
                    b = self._body_json()
                    return None if b is None else self._json(200, P.save_settings(b))
                m = re.fullmatch(r"/api/projects/([0-9a-f]{16})/(render|retry|style)", p)
                if m:
                    pid, action = m.groups()
                    b = self._body_json()
                    if b is None:
                        return
                    if P.public(pid) is None:
                        return self._err(404, "projet inconnu")
                    if action == "render":
                        raw = b.get("ids")
                        if not isinstance(raw, list):
                            return self._err(400, "ids : liste attendue")
                        ids = [i for i in raw if isinstance(i, int) and not isinstance(i, bool)][:60]
                        return self._json(202, {"queued": P.request_render(pid, ids, b.get("style"))})
                    if action == "retry":
                        P.retry(pid)
                        return self._json(202, {"ok": True})
                    return self._json(200, {"style": P.set_style(pid, b.get("style") or {})})
            except UserError as e:
                return self._err(400, str(e))
            return self._err(404, "introuvable")

        def _create(self, u) -> None:
            P = app.projects
            ctype = (self.headers.get("Content-Type") or "").split(";")[0].strip()
            if ctype == "application/json":
                b = self._body_json()
                if b is None:
                    return
                proj, dup = P.create(url=str(b.get("url", "")).strip() or None, options=b.get("options"),
                                     style=b.get("style"), force=bool(b.get("force")))
                return self._json(200 if dup else 202, {"project": proj, "duplicate": dup})
            # dépôt de fichier : corps binaire brut ; un type « non simple » est exigé (pas de requête forgée)
            if not (ctype.startswith("video/") or ctype == "application/octet-stream"):
                return self._err(415, "Content-Type video/* ou application/octet-stream attendu")
            q = parse_qs(u.query)
            name = os.path.basename(q.get("filename", ["video.mp4"])[0])
            ext = Path(name).suffix.lower()
            from .projects import ALLOWED_EXT
            if ext not in ALLOWED_EXT:
                return self._err(415, f"format non supporté ({ext or 'sans extension'})")
            try:
                n = int(self.headers.get("Content-Length") or 0)
            except ValueError:
                return self._err(400, "Content-Length invalide")
            if n <= 0:
                return self._err(400, "fichier vide")
            if n > MAX_UPLOAD:
                return self._err(413, f"fichier trop volumineux (max {MAX_UPLOAD >> 20} Mo)")
            tmpd = app.incoming / secrets.token_hex(6)
            tmpd.mkdir()
            dest = tmpd / f"upload{ext}"
            got = 0
            try:
                with open(dest, "wb") as f:
                    while got < n:
                        chunk = self.rfile.read(min(1 << 20, n - got))
                        if not chunk:
                            break
                        f.write(chunk)
                        got += len(chunk)
                if got != n:
                    return self._err(400, "envoi interrompu")
                try:
                    opts = json.loads(self.headers.get("X-Options") or "{}")
                except json.JSONDecodeError:
                    opts = {}
                opts = opts if isinstance(opts, dict) else {}
                proj, _ = P.create(file=dest, filename=name, options=opts.get("options", opts),
                                   style=opts.get("style"))
                return self._json(202, {"project": proj, "duplicate": False})
            finally:
                shutil.rmtree(tmpd, ignore_errors=True)

    return H


def make_server(web_dir: Path, data_dir: Path, host: str = "127.0.0.1", port: int = 8080,
                base_cfg: dict | None = None, password: str | None = None,
                projects: Projects | None = None) -> tuple[ThreadingHTTPServer, App]:
    app = App(web_dir, data_dir, base_cfg, password, projects)
    srv = ThreadingHTTPServer((host, port), make_handler(app))
    srv.daemon_threads = True
    return srv, app


def serve_main(argv: list[str]) -> int:
    import argparse

    here = Path(__file__).resolve().parents[2]          # .../clipper
    p = argparse.ArgumentParser(prog="clipengine serve")
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=8080)
    p.add_argument("--web", default=str(here), help="dossier de la page (contient index.html)")
    p.add_argument("--data", default=str(here / "engine" / "data"), help="dossier des projets")
    p.add_argument("--model", default=os.environ.get("CLIPENGINE_MODEL", "small"), help="modèle Whisper")
    p.add_argument("--preset", default="veryfast", help="preset x264")
    p.add_argument("--insecure", action="store_true", help="autorise l'écoute hors boucle locale sans mot de passe")
    a = p.parse_args(argv)
    password = os.environ.get("CLIPENGINE_PASSWORD") or None
    local = a.host in ("127.0.0.1", "localhost", "::1")
    if not local and not password and not a.insecure:
        print("Refus : écouter hors de cet ordinateur sans mot de passe exposerait le serveur.\n"
              "Définis CLIPENGINE_PASSWORD (ou --insecure si tu sais pourquoi).")
        return 2
    srv, app = make_server(Path(a.web), Path(a.data), a.host, a.port,
                           base_cfg={"whisper_model": a.model, "preset": a.preset}, password=password)
    print(f"clipengine : http://{a.host}:{a.port}  (Whisper {a.model}, "
          f"{'mot de passe actif' if password else 'accès local sans mot de passe'})")
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        app.close()
    return 0
