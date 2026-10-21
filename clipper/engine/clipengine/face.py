"""Détection de visages (YuNet, repli Haar), suivi, locuteur actif et détection de plans."""
from __future__ import annotations

import subprocess
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

import cv2
import numpy as np

YUNET_URL = "https://huggingface.co/opencv/face_detection_yunet/resolve/main/face_detection_yunet_2023mar.onnx"
YUNET_SIZE = 232589
CACHE = Path.home() / ".cache" / "clipengine"


def _yunet_path() -> Path | None:
    p = CACHE / "face_detection_yunet_2023mar.onnx"
    if p.exists() and p.stat().st_size == YUNET_SIZE:
        return p
    try:
        CACHE.mkdir(parents=True, exist_ok=True)
        urllib.request.urlretrieve(YUNET_URL, p)
        if p.stat().st_size == YUNET_SIZE:
            return p
    except Exception:
        pass
    p.unlink(missing_ok=True)
    return None


class FaceDetector:
    """YuNet si le modèle est disponible (robuste aux profils/rotations), sinon cascade de Haar."""

    def __init__(self) -> None:
        self.yunet = None
        self.haar = None
        path = _yunet_path()
        if path is not None:
            try:
                self.yunet = cv2.FaceDetectorYN.create(str(path), "", (320, 320), 0.6, 0.3, 5000)
            except Exception:
                self.yunet = None
        if self.yunet is None:
            self.haar = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_frontalface_default.xml")

    @property
    def backend(self) -> str:
        return "yunet" if self.yunet is not None else "haar"

    def detect(self, bgr: np.ndarray) -> list[tuple[float, float, float, float, float]]:
        """-> [(x, y, w, h, score)] en pixels de `bgr`."""
        h, w = bgr.shape[:2]
        if self.yunet is not None:
            self.yunet.setInputSize((w, h))
            _, faces = self.yunet.detect(bgr)
            if faces is None:
                return []
            return [(float(f[0]), float(f[1]), float(f[2]), float(f[3]), float(f[14])) for f in faces]
        gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
        found = self.haar.detectMultiScale(gray, 1.1, 5, minSize=(max(24, w // 30),) * 2)
        return [(float(x), float(y), float(fw), float(fh), 0.8) for x, y, fw, fh in found]


@dataclass
class Track:
    id: int
    cx: list = field(default_factory=list)       # une valeur par échantillon (NaN si absent)
    cy: list = field(default_factory=list)
    size: list = field(default_factory=list)     # hauteur du visage
    motion: list = field(default_factory=list)   # activité de la bouche
    last_box: tuple = (0, 0, 0, 0)
    last_i: int = -1
    patch: np.ndarray | None = None


@dataclass
class Timeline:
    """Résultat d'analyse d'un clip, en coordonnées de la vidéo source (pixels)."""
    t: np.ndarray                                # secondes relatives au début du clip
    tracks: dict[int, dict[str, np.ndarray]]     # id -> {cx, cy, size, motion}
    active: np.ndarray                           # id du locuteur actif par échantillon (-1 = aucun)
    nvis: np.ndarray                             # nombre de visages visibles
    cuts: list[float]                            # instants (relatifs) de changement de plan
    backend: str = "none"


def sample_frames(src, start: float, dur: float, fps: float, width: int, height: int):
    """Flux d'images BGR réduites, `fps` par seconde."""
    cmd = ["ffmpeg", "-v", "error", "-nostdin", "-ss", f"{start:.3f}", "-i", str(src), "-t", f"{dur:.3f}",
           "-vf", f"fps={fps},scale={width}:{height}", "-f", "rawvideo", "-pix_fmt", "bgr24", "-"]
    p = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
    assert p.stdout is not None
    n = width * height * 3
    try:
        while True:
            buf = p.stdout.read(n)
            if len(buf) < n:
                break
            yield np.frombuffer(buf, np.uint8).reshape(height, width, 3)
    finally:
        p.stdout.close()
        p.kill()
        p.wait()


def _iou(a, b) -> float:
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    ix = max(0.0, min(ax + aw, bx + bw) - max(ax, bx))
    iy = max(0.0, min(ay + ah, by + bh) - max(ay, by))
    inter = ix * iy
    return inter / (aw * ah + bw * bh - inter + 1e-9)


def _mouth_patch(gray: np.ndarray, box) -> np.ndarray:
    x, y, w, h = box
    x0, x1 = int(max(0, x + 0.15 * w)), int(min(gray.shape[1], x + 0.85 * w))
    y0, y1 = int(max(0, y + 0.55 * h)), int(min(gray.shape[0], y + 1.05 * h))
    if x1 - x0 < 4 or y1 - y0 < 4:
        return np.zeros((12, 24), np.float32)
    return cv2.resize(gray[y0:y1, x0:x1], (24, 12), interpolation=cv2.INTER_AREA).astype(np.float32)


def analyze(src, start: float, dur: float, src_w: int, src_h: int, sample_fps: float = 10.0,
            det_w: int = 640, scene_threshold: float = 0.55, speech: np.ndarray | None = None,
            detector: FaceDetector | None = None, max_age_s: float = 1.5) -> Timeline:
    """Détecte et suit les visages sur [start, start+dur], devine qui parle, repère les coupes.

    `speech` : booléen par échantillon (voix présente) pour ne pas changer de locuteur dans les silences.
    """
    det = detector or FaceDetector()
    det_h = max(2, int(round(det_w * src_h / src_w / 2)) * 2)
    k = src_w / det_w
    tracks: dict[int, Track] = {}
    next_id = 0
    cuts: list[float] = []
    nvis: list[int] = []
    prev_hist = None
    n = 0
    max_age = max(1, int(max_age_s * sample_fps))

    for i, frame in enumerate(sample_frames(src, start, dur, sample_fps, det_w, det_h)):
        n = i + 1
        # --- changement de plan : distance de Bhattacharyya entre histogrammes HSV
        hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)
        hist = cv2.calcHist([hsv], [0, 1], None, [16, 8], [0, 180, 0, 256])
        cv2.normalize(hist, hist, 1, 0, cv2.NORM_L1)
        if prev_hist is not None and cv2.compareHist(prev_hist, hist, cv2.HISTCMP_BHATTACHARYYA) > scene_threshold:
            cuts.append(i / sample_fps)
            for t in tracks.values():            # une coupe casse l'identité des visages
                t.last_i = -10**9
        prev_hist = hist

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        dets = det.detect(frame)
        dets.sort(key=lambda d: -d[2] * d[3])
        used: set[int] = set()
        seen = 0
        for (x, y, w, h, sc) in dets:
            if w < det_w * 0.025:
                continue
            best, best_s = None, 0.0
            for t in tracks.values():
                if t.id in used or i - t.last_i > max_age:
                    continue
                s = _iou(t.last_box, (x, y, w, h))
                if s == 0.0:                     # repli : proximité des centres
                    dx = (x + w / 2) - (t.last_box[0] + t.last_box[2] / 2)
                    dy = (y + h / 2) - (t.last_box[1] + t.last_box[3] / 2)
                    s = 0.05 if (dx * dx + dy * dy) ** 0.5 < 0.8 * max(w, t.last_box[2]) else 0.0
                if s > best_s:
                    best, best_s = t, s
            if best is None:
                best = Track(next_id)
                tracks[next_id] = best
                next_id += 1
            used.add(best.id)
            patch = _mouth_patch(gray, (x, y, w, h))
            mot = 0.0
            if best.patch is not None and best.last_i == i - 1:
                mot = float(np.mean(np.abs(patch - best.patch)) / 255.0)
            best.patch = patch
            best.last_box, best.last_i = (x, y, w, h), i
            while len(best.cx) < i:
                best.cx.append(np.nan); best.cy.append(np.nan); best.size.append(np.nan); best.motion.append(0.0)
            best.cx.append((x + w / 2) * k); best.cy.append((y + h / 2) * k)
            best.size.append(h * k); best.motion.append(mot)
            seen += 1
        nvis.append(seen)

    t_axis = np.arange(n) / sample_fps
    out_tracks: dict[int, dict[str, np.ndarray]] = {}
    for t in tracks.values():
        pad = n - len(t.cx)
        arr = {k_: np.array(v + [np.nan] * pad if k_ != "motion" else v + [0.0] * pad, dtype=float)
               for k_, v in (("cx", t.cx), ("cy", t.cy), ("size", t.size), ("motion", t.motion))}
        out_tracks[t.id] = arr

    active = pick_speaker(out_tracks, n, sample_fps, speech)
    return Timeline(t_axis, out_tracks, active, np.array(nvis, dtype=int), cuts, det.backend)


def pick_speaker(tracks: dict[int, dict[str, np.ndarray]], n: int, fps: float,
                 speech: np.ndarray | None = None, hold_s: float = 0.5, ratio: float = 1.35) -> np.ndarray:
    """Locuteur = visage dont la bouche bouge le plus (lissé 1 s), avec hystérésis pour éviter les va-et-vient."""
    active = np.full(n, -1, dtype=int)
    if not tracks:
        return active
    ids = list(tracks)
    win = max(1, int(fps))
    ker = np.ones(win) / win
    mot = {i: np.convolve(tracks[i]["motion"], ker, mode="same") for i in ids}
    present = {i: ~np.isnan(tracks[i]["cx"]) for i in ids}
    cur, pending = -1, 0
    hold_n = max(1, int(hold_s * fps))
    for s in range(n):
        vis = [i for i in ids if present[i][s]]
        if not vis:
            active[s] = cur
            continue
        if cur not in vis:
            cur = max(vis, key=lambda i: (tracks[i]["size"][s]))   # on repart du plus gros visage
            pending = 0
        elif len(vis) > 1 and (speech is None or (s < len(speech) and speech[s])):
            best = max(vis, key=lambda i: mot[i][s])
            if best != cur and mot[best][s] > ratio * mot[cur][s] + 1e-4:
                pending += 1
                if pending >= hold_n:
                    cur, pending = best, 0
            else:
                pending = 0
        active[s] = cur
    return active
