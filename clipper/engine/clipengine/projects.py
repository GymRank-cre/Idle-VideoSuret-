"""Projets : état sur disque, file de tâches unique, analyse puis rendu clip par clip.

Un projet = une vidéo source + ses moments classés (auto fabriqués, ou « en réserve ») + les clips rendus.
Tout est écrit dans <racine>/<id>/ ; un redémarrage reprend là où le travail s'est arrêté.
"""
from __future__ import annotations

import copy
import hashlib
import json
import os
import re
import secrets
import shutil
import threading
import time
import traceback
from collections import deque
from pathlib import Path

from . import face
from .config import Config
from .errors import UserError
from .media import MediaInfo, check_info, check_url, fetch, probe, video_key
from .models import Candidate
from .pipeline import (check_speech, clip_transcript, finalize_kit, find_moments, proposal_dict, render_candidate, slug,
                       transcribe_source)
from .styles import PRESETS, Style, get_style
from .transcribe import load_transcript
from .audio import AudioProfile

ID_RE = re.compile(r"^[0-9a-f]{16}$")
ALLOWED_EXT = {".mp4", ".mov", ".mkv", ".webm", ".m4v", ".avi"}
LANGS = {"fr", "en", "es", "de", "it", "pt", "nl"}

LIMITS = {"max_clips": (int, 1, 15), "reserve": (int, 0, 12), "min_dur": (float, 8, 90), "max_dur": (float, 15, 180),
          "target_dur": (float, 10, 150)}
CHOICES = {"mode": {"best", "full"}, "layout": {"auto", "track", "fit", "split"}, "llm": {"auto", "none"}}
BOOLS = {"captions", "title_overlay", "group_frame"}


def clean_options(raw: dict | None) -> dict:
    """Réglages acceptés d'un client : chaque valeur est bornée ou ignorée."""
    raw = raw if isinstance(raw, dict) else {}
    out: dict = {}
    for k, (typ, lo, hi) in LIMITS.items():
        if k in raw:
            try:
                out[k] = typ(max(lo, min(hi, float(raw[k]))))
            except (TypeError, ValueError):
                pass
    for k, choices in CHOICES.items():
        if raw.get(k) in choices:
            out[k] = raw[k]
    for k in BOOLS:
        if isinstance(raw.get(k), bool):
            out[k] = raw[k]
    if raw.get("language") in LANGS:
        out["language"] = raw["language"]
    if "min_dur" in out and "max_dur" in out and out["min_dur"] >= out["max_dur"]:
        out["min_dur"] = max(8.0, out["max_dur"] * 0.5)
    return out


class Projects:
    def __init__(self, root: Path, base_cfg: dict | None = None, start: bool = True):
        self.root = Path(root).resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self.base_cfg = base_cfg or {}
        self._lock = threading.RLock()
        self._cv = threading.Condition(self._lock)
        self._tasks: deque[tuple] = deque()
        self._running: tuple | None = None
        self._stop = False
        self._projects: dict[str, dict] = {}
        self._deleted: set[str] = set()
        self._ctx: dict[str, tuple] = {}
        self._detector = None
        self._load_all()
        self._thread = threading.Thread(target=self._loop, daemon=True, name="clipengine-worker")
        if start:
            self._thread.start()
            self._resume()

    # ------------------------------------------------------------------ disque
    def dir(self, pid: str) -> Path:
        return self.root / pid

    def _save(self, p: dict) -> None:
        p["updated"] = time.time()
        d = self.dir(p["id"])
        if p["id"] in self._deleted or not d.exists():
            return
        tmp = d / "project.json.tmp"
        tmp.write_text(json.dumps(p, ensure_ascii=False), encoding="utf-8")
        os.replace(tmp, d / "project.json")

    def _load_all(self) -> None:
        for d in self.root.iterdir():
            f = d / "project.json"
            if d.is_dir() and ID_RE.match(d.name) and f.exists():
                try:
                    self._projects[d.name] = json.loads(f.read_text(encoding="utf-8"))
                except (OSError, json.JSONDecodeError):
                    continue

    def _resume(self) -> None:
        """Après un redémarrage, reprend les analyses et rendus interrompus."""
        with self._lock:
            for p in sorted(self._projects.values(), key=lambda x: x["created"]):
                if p["status"] in ("queued", "analyzing"):
                    p["status"], p["stage"] = "queued", "Reprise"
                    self._tasks.append(("analyze", p["id"]))
                for pr in p["proposals"]:
                    if pr["state"] in ("queued", "rendering"):
                        pr["state"], pr["progress"] = "queued", 0.0
                        self._tasks.append(("render", p["id"], pr["id"], None))
            self._cv.notify_all()

    # ------------------------------------------------------------------ réglages persistants
    def settings(self) -> dict:
        f = self.root / "settings.json"
        try:
            d = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            d = {}
        style = Style.from_dict(d.get("style")) if d.get("style") else get_style("podcast")
        return {"style": style.to_dict(), "defaults": clean_options(d.get("defaults"))}

    def save_settings(self, raw: dict) -> dict:
        cur = self.settings()
        style = Style.from_dict(raw.get("style"), Style.from_dict(cur["style"])) if "style" in raw else Style.from_dict(cur["style"])
        defaults = clean_options(raw["defaults"]) if "defaults" in raw else cur["defaults"]
        data = {"style": style.to_dict(), "defaults": defaults}
        tmp = self.root / "settings.json.tmp"
        tmp.write_text(json.dumps(data, ensure_ascii=False), encoding="utf-8")
        os.replace(tmp, self.root / "settings.json")
        return data

    # ------------------------------------------------------------------ API
    def create(self, *, url: str | None = None, file: Path | None = None, filename: str = "",
               options: dict | None = None, style=None, force: bool = False) -> tuple[dict, bool]:
        """Crée un projet et lance l'analyse. Renvoie (projet, déjà_existant)."""
        raw = options if isinstance(options, dict) else {}
        opts = {**self.settings()["defaults"], **clean_options(raw)}
        base_style = Style.from_dict(self.settings()["style"])
        if isinstance(style, str) and style in PRESETS:
            st = get_style(style)
        elif isinstance(style, dict) and style:
            st = Style.from_dict(style, base_style)          # par-dessus le style enregistré
        else:
            st = base_style
        key = ""
        if url:
            url = check_url(url)
            key = video_key(url)
            if not force:
                with self._lock:
                    for old in sorted(self._projects.values(), key=lambda x: -x["created"]):
                        if old["source"].get("key") == key and old["status"] != "error":
                            return self.public(old["id"]), True
        elif file is None:
            raise UserError("Colle un lien ou envoie un fichier vidéo.")
        pid = secrets.token_hex(8)
        d = self.dir(pid)
        d.mkdir(parents=True)
        src = {"kind": "url" if url else "file", "url": url or "", "name": filename or "", "key": key}
        if file is not None:
            ext = Path(filename).suffix.lower() or ".mp4"
            if ext not in ALLOWED_EXT:
                shutil.rmtree(d, ignore_errors=True)
                raise UserError(f"Format non supporté ({ext}).")
            shutil.move(str(file), d / f"source{ext}")
        p = {"id": pid, "created": time.time(), "updated": time.time(), "source": src,
             "title": Path(filename).stem if filename else url or "", "info": {}, "options": opts,
             "style": st.to_dict(), "status": "queued", "stage": "En attente", "progress": 0.0, "error": "",
             "language": "", "stats": {}, "log": [], "proposals": []}
        with self._lock:
            self._projects[pid] = p
            self._save(p)
            self._tasks.append(("analyze", pid))
            self._cv.notify_all()
        return self.public(pid), False

    def public(self, pid: str) -> dict | None:
        with self._lock:
            p = self._projects.get(pid)
            if p is None:
                return None
            out = copy.deepcopy(p)
            out["ahead"] = self._ahead(pid)
            return out

    def exists(self, pid: str) -> bool:
        with self._lock:
            return pid in self._projects

    def summaries(self) -> list[dict]:
        with self._lock:
            rows = []
            for p in sorted(self._projects.values(), key=lambda x: -x["created"]):
                props = p["proposals"]
                done = [x for x in props if x["state"] == "done"]
                first = next((x for x in done if x["poster"]), None)
                rows.append({
                    "id": p["id"], "title": p["title"], "created": p["created"], "status": p["status"],
                    "stage": p["stage"], "progress": round(p["progress"], 3), "error": p["error"],
                    "duration": p["info"].get("duration", 0), "language": p["language"],
                    "source": {"kind": p["source"]["kind"], "url": p["source"]["url"]},
                    "proposals": len(props), "done": len(done),
                    "rendering": sum(x["state"] in ("queued", "rendering") for x in props),
                    "best": max((x["score"] for x in props), default=0),
                    "poster": f"/files/{p['id']}/{first['poster']}" if first else "",
                })
            return rows

    def delete(self, pid: str) -> bool:
        with self._lock:
            if pid not in self._projects:
                return False
            self._deleted.add(pid)
            self._projects.pop(pid)
            self._tasks = deque(t for t in self._tasks if t[1] != pid)
            self._ctx.pop(pid, None)
        shutil.rmtree(self.dir(pid), ignore_errors=True)
        return True

    def request_render(self, pid: str, ids: list[int], style=None) -> list[int]:
        """Met des moments en file de rendu (réserve, échec, ou nouveau rendu avec un autre style)."""
        with self._lock:
            p = self._projects.get(pid)
            if p is None:
                raise UserError("Projet introuvable.")
            if p["status"] != "ready":
                raise UserError("L'analyse n'est pas terminée.")
            override = None
            if isinstance(style, (str, dict)) and style:
                base = Style.from_dict(p["style"])
                override = (get_style(style) if isinstance(style, str) else Style.from_dict(style, base)).to_dict()
            queued = []
            for pr in p["proposals"]:
                if pr["id"] in ids and pr["state"] in ("reserve", "done", "error"):
                    pr["state"], pr["progress"], pr["error"] = "queued", 0.0, ""
                    self._tasks.append(("render", pid, pr["id"], override))
                    queued.append(pr["id"])
            self._save(p)
            self._cv.notify_all()
            return queued

    def retry(self, pid: str) -> None:
        """Relance l'analyse d'un projet en échec (la source déjà téléchargée est conservée)."""
        with self._lock:
            p = self._projects.get(pid)
            if p is None:
                raise UserError("Projet introuvable.")
            if p["status"] != "error":
                raise UserError("Ce projet n'est pas en échec.")
            p.update(status="queued", stage="En attente", error="", progress=0.0)
            self._save(p)
            self._tasks.append(("analyze", pid))
            self._cv.notify_all()

    def set_style(self, pid: str, style: dict) -> dict:
        """Style par défaut du projet (utilisé par les prochains rendus)."""
        with self._lock:
            p = self._projects.get(pid)
            if p is None:
                raise UserError("Projet introuvable.")
            p["style"] = Style.from_dict(style, Style.from_dict(p["style"])).to_dict()
            self._save(p)
            return p["style"]

    def _ahead(self, pid: str) -> int:
        """Nombre de tâches qui passent avant la première tâche de ce projet."""
        order = ([self._running] if self._running else []) + list(self._tasks)
        for i, t in enumerate(order):
            if t[1] == pid:
                return i
        return 0

    def close(self) -> None:
        with self._lock:
            self._stop = True
            self._cv.notify_all()

    # ------------------------------------------------------------------ worker
    def _loop(self) -> None:
        while True:
            with self._cv:
                while not self._tasks and not self._stop:
                    self._cv.wait(0.5)
                if self._stop:
                    return
                task = self._tasks.popleft()
                self._running = task
            try:
                if task[0] == "analyze":
                    self._analyze(task[1])
                else:
                    self._render(task[1], task[2], task[3])
            except Exception:                                # un échec ne doit jamais arrêter le worker
                traceback.print_exc()
            finally:
                with self._lock:
                    self._running = None
                    if task[1] in self._deleted:
                        shutil.rmtree(self.dir(task[1]), ignore_errors=True)

    def _cfg(self, p: dict) -> Config:
        o = {k: v for k, v in p["options"].items()}
        if "target_dur" in o:
            o["dur_spread"] = max(8.0, o["target_dur"] * 0.55)
        return Config.from_dict({**self.base_cfg, **o})

    def _set(self, p: dict, **kw) -> None:
        with self._lock:
            p.update(kw)

    def _log(self, p: dict, msg: str) -> None:
        msg = msg.strip()
        with self._lock:
            p["log"] = (p["log"] + [msg])[-30:]
            if m := re.search(r"téléchargement (\d+) %", msg):
                p["stage"], p["progress"] = "Téléchargement de la vidéo", 0.02 + 0.18 * int(m.group(1)) / 100
            elif "profil audio" in msg:
                p["stage"], p["progress"] = "Analyse du son", 0.22
            elif "transcription" in msg:
                p["stage"], p["progress"] = "Transcription de la parole", 0.3
            elif "candidats" in msg or "parties" in msg:
                p["stage"], p["progress"] = "Recherche des meilleurs moments", 0.8
            elif "avis éditorial" in msg:
                p["stage"], p["progress"] = "Avis éditorial", 0.88

    def _analyze(self, pid: str) -> None:
        with self._lock:
            p = self._projects.get(pid)
        if p is None:
            return
        d = self.dir(pid)
        try:
            self._set(p, status="analyzing", stage="Préparation", progress=0.02, error="")
            cfg = self._cfg(p)
            src = next((f for f in d.glob("source.*")), None)
            if src is None:
                self._set(p, stage="Téléchargement de la vidéo")
                src, meta = fetch(p["source"]["url"], d / "dl", log=lambda m: self._log(p, m))
                final = d / f"source{src.suffix}"
                shutil.move(str(src), final)
                shutil.rmtree(d / "dl", ignore_errors=True)
                src = final
                if meta.get("title"):
                    self._set(p, title=meta["title"])
            info = probe(src)
            check_info(info)
            self._set(p, info=info.to_dict())
            self._save(p)
            tr, prof = transcribe_source(src, d / "work", cfg, None, lambda m: self._log(p, m))
            tr = clip_transcript(tr, info.duration)
            try:
                check_speech(tr, info)
            except UserError:
                (d / "work" / "transcript.json").unlink(missing_ok=True)    # une transcription ratée ne doit pas rester en cache
                raise
            ranked, n_auto, stats = find_moments(tr, prof, cfg, lambda m: self._log(p, m))
            props = [proposal_dict(finalize_kit(c), i + 1, i + 1, i < n_auto) for i, c in enumerate(ranked)]
            with self._lock:
                if pid in self._deleted:
                    return
                p.update(language=tr.language, stats=stats, proposals=props, status="ready",
                         stage="Moments trouvés" if props else "Aucun moment assez fort trouvé", progress=1.0)
                self._save(p)
                for pr in props:
                    if pr["state"] == "queued":
                        self._tasks.append(("render", pid, pr["id"], None))
                self._cv.notify_all()
        except UserError as e:
            self._set(p, status="error", stage="Échec", error=str(e))
            self._save(p)
        except Exception as e:
            traceback.print_exc()
            self._set(p, status="error", stage="Échec", error=f"Erreur d'analyse : {type(e).__name__}")
            (d / "work").mkdir(exist_ok=True)
            (d / "work" / "error.log").write_text(traceback.format_exc(), encoding="utf-8")
            self._save(p)

    def _context(self, pid: str, p: dict) -> tuple:
        """Source, infos, transcription et profil audio du projet (gardés en mémoire pour les rendus en série)."""
        with self._lock:
            if pid in self._ctx:
                return self._ctx[pid]
        d = self.dir(pid)
        src = next(d.glob("source.*"))
        info = MediaInfo(**p["info"])
        tr = clip_transcript(load_transcript(d / "work" / "transcript.json"), info.duration)
        prof = AudioProfile.load(d / "work" / "audio_profile.npy")
        with self._lock:
            if len(self._ctx) >= 2:
                self._ctx.pop(next(iter(self._ctx)))
            self._ctx[pid] = (src, info, tr, prof)
        return self._ctx[pid]

    def _render(self, pid: str, prop_id: int, override: dict | None) -> None:
        with self._lock:
            p = self._projects.get(pid)
            pr = next((x for x in p["proposals"] if x["id"] == prop_id), None) if p else None
            if pr is None or pr["state"] != "queued":
                return
            pr["state"], pr["progress"] = "rendering", 0.01
        d = self.dir(pid)
        try:
            src, info, tr, prof = self._context(pid, p)
            cfg = self._cfg(p)
            style = Style.from_dict(override) if override else Style.from_dict(p["style"])
            sh = hashlib.sha1(json.dumps(style.to_dict(), sort_keys=True).encode()).hexdigest()[:5]
            stem = f"{pr['rank']:02d}-{slug(pr['title'])}-{sh}"
            c = Candidate(start=pr["start"], end=pr["end"], lo=0, hi=0, text=pr["text"], title=pr["title"],
                          hook=pr["hook"], caption=pr["caption"], hashtags=pr["hashtags"])
            if self._detector is None:
                self._detector = face.FaceDetector()

            def prog(f):
                with self._lock:
                    pr["progress"] = round(f, 3)

            r = render_candidate(src, info, tr, prof, c, d / "clips", stem, cfg, style, self._detector, prog)
            with self._lock:
                if pid in self._deleted:
                    return
                old = {pr["video"], pr["poster"], Path(pr["video"]).with_suffix(".ass").name if pr["video"] else ""}
                new = {r.video.name, r.poster.name if r.poster else "", r.ass.name}
                for name in old - new - {""}:
                    (d / "clips" / name).unlink(missing_ok=True)
                pr.update(state="done", progress=1.0, video=r.video.name, poster=r.poster.name if r.poster else "",
                          layouts=sorted(set(r.layouts)), style=style.name, error="")
                self._save(p)
        except UserError as e:
            with self._lock:
                pr.update(state="error", error=str(e))
                self._save(p)
        except Exception as e:
            traceback.print_exc()
            with self._lock:
                pr.update(state="error", error=f"Erreur de rendu : {type(e).__name__}")
                self._save(p)
