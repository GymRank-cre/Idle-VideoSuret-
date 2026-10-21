"""Plan de caméra : où cadrer, quand couper, quelle mise en page (suivi, fond flou, écran partagé)."""
from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np

from .config import Config
from .face import Timeline


@dataclass
class Shot:
    t0: float
    t1: float
    layout: str                                    # track | fit | split
    ts: np.ndarray = field(default_factory=lambda: np.zeros(0))
    cx: np.ndarray = field(default_factory=lambda: np.zeros(0))   # centre x (track) ou haut (split)
    cy: np.ndarray = field(default_factory=lambda: np.zeros(0))
    cx2: np.ndarray = field(default_factory=lambda: np.zeros(0))  # split : centre x du second visage
    cy2: np.ndarray = field(default_factory=lambda: np.zeros(0))


@dataclass
class CameraPlan:
    src_w: int
    src_h: int
    out_w: int
    out_h: int
    zoom: float
    shots: list[Shot]

    def crop_size(self, aspect: float | None = None) -> tuple[float, float]:
        return crop_size(self.src_w, self.src_h, aspect or self.out_w / self.out_h, self.zoom)

    def at(self, t: float) -> tuple[Shot, int]:
        starts = [s.t0 for s in self.shots]
        i = max(0, int(np.searchsorted(starts, t, side="right")) - 1)
        return self.shots[i], i


def crop_size(src_w: int, src_h: int, aspect: float, zoom: float = 1.0) -> tuple[float, float]:
    """Plus grand rectangle d'un rapport donné contenu dans la source, divisé par le zoom."""
    if src_w / src_h >= aspect:
        ch = src_h / zoom
        return ch * aspect, ch
    cw = src_w / zoom
    return cw, cw / aspect


def clamp_box(cx: float, cy: float, cw: float, ch: float, src_w: int, src_h: int,
              face_y: float = 0.42) -> tuple[float, float]:
    x0 = min(max(cx - cw / 2, 0.0), src_w - cw)
    y0 = min(max(cy - face_y * ch, 0.0), src_h - ch)
    return x0, y0


# ----------------------------------------------------------------------------- lissage

def _fill(v: np.ndarray, fallback: float) -> np.ndarray:
    v = v.astype(float).copy()
    ok = ~np.isnan(v)
    if not ok.any():
        return np.full_like(v, fallback)
    idx = np.arange(len(v))
    return np.interp(idx, idx[ok], v[ok])


def _median(v: np.ndarray, k: int = 5) -> np.ndarray:
    if len(v) < k:
        return v
    pad = k // 2
    p = np.pad(v, pad, mode="edge")
    return np.array([np.median(p[i:i + k]) for i in range(len(v))])


def _gauss(v: np.ndarray, sigma: float) -> np.ndarray:
    if sigma <= 0.05 or len(v) < 2:
        return v
    r = max(1, int(3 * sigma))
    x = np.arange(-r, r + 1)
    ker = np.exp(-0.5 * (x / sigma) ** 2)
    ker /= ker.sum()
    return np.convolve(np.pad(v, r, mode="edge"), ker, mode="valid")


def smooth_path(raw: np.ndarray, cw: float, sample_fps: float, cfg: Config) -> list[tuple[int, int, np.ndarray]]:
    """Trajectoire de caméra façon cadreur.

    1. filtre médian (supprime les fausses détections) ;
    2. zone morte : la caméra ne bouge que si la cible sort de ±dead_zone·cw ;
    3. un grand saut (> cut_jump·cw) devient une coupe franche plutôt qu'un travelling ;
    4. lissage gaussien (anticipation, car on connaît le futur) dans chaque segment.

    Retourne des segments (i0, i1, valeurs) ; une coupe sépare deux segments.
    """
    v = _median(raw)
    hold = np.empty_like(v)
    h = v[0]
    for i, x in enumerate(v):
        if abs(x - h) > cfg.dead_zone * cw:
            h = x
        hold[i] = h
    jumps = np.where(np.abs(np.diff(hold)) > cfg.cut_jump * cw)[0] + 1
    edges = [0, *jumps.tolist(), len(hold)]
    sig = cfg.cam_smooth * sample_fps
    return [(a, b, _gauss(hold[a:b], sig)) for a, b in zip(edges[:-1], edges[1:])]


# ----------------------------------------------------------------------------- mise en page

def _stats(tl: Timeline, i0: int, i1: int, src_w: int, cw: float) -> dict:
    n = max(1, i1 - i0)
    nvis = tl.nvis[i0:i1]
    face_frac = float(np.mean(nvis >= 1)) if len(nvis) else 0.0
    # deux visages importants trop éloignés pour tenir dans un même cadre vertical ?
    far = 0
    for s in range(i0, i1):
        pts = [(tr["cx"][s], tr["size"][s]) for tr in tl.tracks.values() if not np.isnan(tr["cx"][s])]
        if len(pts) >= 2:
            big = max(p[1] for p in pts)
            xs = [p[0] for p in pts if p[1] >= 0.5 * big]
            if len(xs) >= 2 and max(xs) - min(xs) > 0.9 * cw:
                far += 1
    switches = int(np.sum((np.diff(tl.active[i0:i1]) != 0) & (tl.active[i0:i1][1:] >= 0))) if n > 1 else 0
    return {"face_frac": face_frac, "far_frac": far / n, "switches": switches}


def _choose_layout(st: dict, dur: float, src_w: int, src_h: int, cfg: Config) -> str:
    if cfg.layout != "auto":
        return cfg.layout
    if src_h >= src_w * 16 / 9 * 0.98:                # source déjà verticale
        return "track"
    if st["face_frac"] < 0.35:
        return "fit"                                  # pas de visage fiable : on montre tout le plan
    if st["far_frac"] > 0.35 and st["switches"] >= 2 and dur >= 4:
        return "split"
    return "track"


def _top_two(tl: Timeline, i0: int, i1: int) -> list[int]:
    pres = {k: int(np.sum(~np.isnan(tr["cx"][i0:i1]))) for k, tr in tl.tracks.items()}
    best = sorted(pres, key=lambda k: -pres[k])[:2]
    return sorted(best, key=lambda k: float(np.nanmean(tl.tracks[k]["cx"][i0:i1])))   # gauche -> haut


def _focus(tl: Timeline, i0: int, i1: int, src_w: int, src_h: int, cw: float = 0.0, group: bool = True):
    """Cible x/y de la caméra ; NaN tant qu'aucun visage n'a été vu.

    Un seul visage important, ou des visages trop éloignés pour tenir ensemble : on suit le locuteur actif.
    Tous les visages importants tiennent dans le cadre : on cadre l'ensemble (plan de groupe stable, aucune tête
    coupée, pas de va-et-vient quand la parole alterne).
    """
    cx = np.full(i1 - i0, np.nan)
    cy = np.full(i1 - i0, np.nan)
    for j, s in enumerate(range(i0, i1)):
        a = tl.active[s]
        if a >= 0 and not np.isnan(tl.tracks[a]["cx"][s]):
            cx[j], cy[j] = tl.tracks[a]["cx"][s], tl.tracks[a]["cy"][s]
        if not group or cw <= 0:
            continue
        faces = [(t["cx"][s], t["cy"][s], t["size"][s]) for t in tl.tracks.values() if not np.isnan(t["cx"][s])]
        if len(faces) < 2:
            continue
        big = max(f[2] for f in faces)
        faces = [f for f in faces if f[2] >= 0.5 * big]
        if len(faces) < 2:
            continue
        left = min(f[0] - 0.45 * f[2] for f in faces)
        right = max(f[0] + 0.45 * f[2] for f in faces)
        if right - left <= 0.82 * cw:
            cx[j] = (left + right) / 2
            cy[j] = float(np.mean([f[1] for f in faces]))
    return _fill(cx, src_w / 2), _fill(cy, src_h * 0.4)


def plan_camera(tl: Timeline, src_w: int, src_h: int, dur: float, cfg: Config, sample_fps: float | None = None
                ) -> CameraPlan:
    fps = sample_fps or cfg.sample_fps
    n = len(tl.t)
    aspect = cfg.out_w / cfg.out_h
    cw, ch = crop_size(src_w, src_h, aspect, cfg.zoom)
    cw2, _ = crop_size(src_w, src_h, cfg.out_w / (cfg.out_h / 2), 1.0)

    if n == 0:
        return CameraPlan(src_w, src_h, cfg.out_w, cfg.out_h, cfg.zoom,
                          [Shot(0.0, dur, "fit" if cfg.layout == "auto" else cfg.layout,
                                np.array([0.0, dur]), np.full(2, src_w / 2), np.full(2, src_h / 2))])

    # découpe en plans aux coupes de la vidéo source
    bounds = [0] + [min(n - 1, int(round(c * fps))) for c in tl.cuts] + [n]
    bounds = sorted(set(bounds))
    shots: list[Shot] = []
    for i0, i1 in zip(bounds[:-1], bounds[1:]):
        if i1 - i0 < 1:
            continue
        st = _stats(tl, i0, i1, src_w, cw)
        layout = _choose_layout(st, (i1 - i0) / fps, src_w, src_h, cfg)
        ts = tl.t[i0:i1]
        t_end = dur if i1 == n else tl.t[i1]
        t_start = 0.0 if i0 == 0 else tl.t[i0]
        if layout == "fit":
            shots.append(Shot(t_start, t_end, "fit"))
        elif layout == "split" and len(tl.tracks) >= 2:
            a, b = _top_two(tl, i0, i1)
            xa = _fill(tl.tracks[a]["cx"][i0:i1], src_w / 3)
            xb = _fill(tl.tracks[b]["cx"][i0:i1], 2 * src_w / 3)
            ya = _fill(tl.tracks[a]["cy"][i0:i1], src_h * 0.4)
            yb = _fill(tl.tracks[b]["cy"][i0:i1], src_h * 0.4)
            shots.append(Shot(t_start, t_end, "split", ts,
                              _gauss(xa, cfg.cam_smooth * fps), _gauss(ya, cfg.cam_smooth * fps),
                              _gauss(xb, cfg.cam_smooth * fps), _gauss(yb, cfg.cam_smooth * fps)))
        else:
            fx, fy = _focus(tl, i0, i1, src_w, src_h, cw, cfg.group_frame)
            segs = smooth_path(fx, cw, fps, cfg)
            fy_s = _gauss(_median(fy), cfg.cam_smooth * fps * 1.5)
            for a, b, vals in segs:
                s0 = t_start if a == 0 else ts[a]
                s1 = t_end if b == len(fx) else ts[b]
                shots.append(Shot(s0, s1, "track", ts[a:b], vals, fy_s[a:b]))
    if not shots:
        shots = [Shot(0.0, dur, "fit")]
    shots[0].t0 = 0.0
    return CameraPlan(src_w, src_h, cfg.out_w, cfg.out_h, cfg.zoom, shots)


def frame_params(plan: CameraPlan, t: float):
    """Paramètres de rendu à l'instant t : (layout, boîte(s) de recadrage)."""
    shot, _ = plan.at(t)
    if shot.layout == "fit" or len(shot.ts) == 0:
        return "fit", None
    if shot.layout == "track":
        cw, ch = plan.crop_size()
        cx = float(np.interp(t, shot.ts, shot.cx))
        cy = float(np.interp(t, shot.ts, shot.cy))
        x0, y0 = clamp_box(cx, cy, cw, ch, plan.src_w, plan.src_h)
        return "track", (x0, y0, cw, ch)
    cw, ch = crop_size(plan.src_w, plan.src_h, plan.out_w / (plan.out_h / 2), 1.0)
    boxes = []
    for xs, ys in ((shot.cx, shot.cy), (shot.cx2, shot.cy2)):
        x0, y0 = clamp_box(float(np.interp(t, shot.ts, xs)), float(np.interp(t, shot.ts, ys)),
                           cw, ch, plan.src_w, plan.src_h)
        boxes.append((x0, y0, cw, ch))
    return "split", tuple(boxes)
