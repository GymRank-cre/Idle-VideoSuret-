import numpy as np

from clipengine import Config
from clipengine.face import Timeline, pick_speaker
from clipengine.reframe import clamp_box, crop_size, frame_params, plan_camera, smooth_path

SW, SH = 1920, 1080


def tl_from(tracks, active, n=100, fps=10, cuts=()):
    nvis = np.array([sum(not np.isnan(t["cx"][i]) for t in tracks.values()) for i in range(n)])
    return Timeline(np.arange(n) / fps, tracks, np.asarray(active), nvis, list(cuts), "test")


def trk(cx, cy=400, size=150, motion=0.0, n=100):
    cx = np.full(n, cx, float) if np.isscalar(cx) else np.asarray(cx, float)
    return {"cx": cx, "cy": np.where(np.isnan(cx), np.nan, cy), "size": np.where(np.isnan(cx), np.nan, size),
            "motion": np.full(n, motion) if np.isscalar(motion) else np.asarray(motion, float)}


def test_crop_size_is_9_16_full_height():
    cw, ch = crop_size(SW, SH, 9 / 16)
    assert ch == SH and abs(cw / ch - 9 / 16) < 1e-9
    cw2, ch2 = crop_size(SW, SH, 9 / 16, 1.5)
    assert abs(ch2 - SH / 1.5) < 1e-9


def test_clamp_keeps_box_inside():
    cw, ch = crop_size(SW, SH, 9 / 16)
    x0, y0 = clamp_box(10, 500, cw, ch, SW, SH)
    assert x0 == 0 and y0 == 0
    x0, _ = clamp_box(SW - 5, 500, cw, ch, SW, SH)
    assert abs(x0 + cw - SW) < 1e-9


def test_dead_zone_ignores_small_movements():
    cfg = Config()
    cw = 607.0
    raw = 960 + 20 * np.sin(np.arange(100) / 3)          # petites oscillations (±3 % du cadre)
    segs = smooth_path(raw, cw, 10, cfg)
    assert len(segs) == 1 and np.ptp(segs[0][2]) < 1.0


def test_follows_real_move_with_ease_not_jump():
    cfg = Config()
    cw = 607.0
    raw = np.concatenate([np.full(50, 800.0), np.full(50, 1000.0)])      # déplacement de 200 px (< cut_jump)
    segs = smooth_path(raw, cw, 10, cfg)
    assert len(segs) == 1
    v = segs[0][2]
    step = np.max(np.abs(np.diff(v)))
    assert 799 < v[0] < 810 and v[-1] > 990 and step < 60       # transition étalée sur plusieurs images


def test_big_jump_becomes_a_cut():
    cfg = Config()
    raw = np.concatenate([np.full(50, 400.0), np.full(50, 1500.0)])
    segs = smooth_path(raw, 607.0, 10, cfg)
    assert len(segs) == 2 and segs[0][1] == segs[1][0] == 50


def test_median_removes_false_detection_spike():
    raw = np.full(100, 900.0)
    raw[40] = 100.0
    segs = smooth_path(raw, 607.0, 10, Config())
    assert len(segs) == 1 and np.ptp(segs[0][2]) < 1


def test_speaker_switches_with_hysteresis():
    n = 100
    a_motion = np.where(np.arange(n) < 50, 0.08, 0.005)
    b_motion = np.where(np.arange(n) < 50, 0.005, 0.08)
    tracks = {0: trk(400, motion=a_motion, size=140), 1: trk(1500, motion=b_motion, size=140)}
    act = pick_speaker(tracks, n, 10)
    assert act[20] == 0 and act[90] == 1
    flips = int(np.sum(np.diff(act) != 0))
    assert flips == 1


def test_speaker_does_not_flip_during_silence():
    n = 60
    tracks = {0: trk(400, motion=np.where(np.arange(n) < 20, 0.08, 0.0)),
              1: trk(1500, motion=np.where(np.arange(n) < 20, 0.0, 0.08))}
    speech = np.arange(n) < 20
    act = pick_speaker(tracks, n, 10, speech)
    assert set(act.tolist()) == {0}


def test_single_face_tracks_with_track_layout():
    n = 100
    tl = tl_from({0: trk(np.linspace(600, 1300, n))}, np.zeros(n, int))
    plan = plan_camera(tl, SW, SH, 10.0, Config())
    assert [s.layout for s in plan.shots] == ["track"]
    cw, _ = crop_size(SW, SH, 9 / 16)
    lay, (x0, y0, w, h) = frame_params(plan, 0.2)
    assert lay == "track" and abs((x0 + w / 2) - 600) < 60
    lay, (x0, _, _, _) = frame_params(plan, 9.9)
    assert abs((x0 + w / 2) - 1300) < 80


def test_no_face_falls_back_to_blurred_fit():
    n = 100
    tl = tl_from({}, np.full(n, -1))
    plan = plan_camera(tl, SW, SH, 10.0, Config())
    assert plan.shots[0].layout == "fit" and frame_params(plan, 3.0)[0] == "fit"


def test_two_far_speakers_alternating_use_split():
    n = 160
    act = np.where((np.arange(n) // 40) % 2 == 0, 0, 1)
    tl = tl_from({0: trk(400, n=n), 1: trk(1500, n=n)}, act, n=n)
    plan = plan_camera(tl, SW, SH, 16.0, Config())
    assert plan.shots[0].layout == "split"
    lay, boxes = frame_params(plan, 5.0)
    assert lay == "split" and len(boxes) == 2
    (xa, *_), (xb, *_) = boxes
    assert xa < xb                                              # le visage de gauche reste en haut


def test_two_close_faces_stay_in_one_track():
    n = 100
    tl = tl_from({0: trk(800, n=n), 1: trk(1000, n=n)}, np.zeros(n, int), n=n)
    assert plan_camera(tl, SW, SH, 10.0, Config()).shots[0].layout == "track"


def test_source_cut_creates_new_shot_and_resets_layout():
    n = 100
    cx = np.full(n, np.nan); cx[:50] = 900                      # visage seulement dans le premier plan
    tl = tl_from({0: trk(cx, n=n)}, np.where(np.arange(n) < 50, 0, -1), n=n, cuts=[5.0])
    plan = plan_camera(tl, SW, SH, 10.0, Config())
    assert [s.layout for s in plan.shots] == ["track", "fit"]
    assert plan.shots[1].t0 == 5.0


def test_forced_layout():
    n = 100
    tl = tl_from({0: trk(900)}, np.zeros(n, int))
    assert plan_camera(tl, SW, SH, 10.0, Config(layout="fit")).shots[0].layout == "fit"


def test_group_frame_centres_on_everyone_when_faces_fit():
    n = 100
    tl = tl_from({0: trk(800, n=n), 1: trk(1000, n=n)}, np.zeros(n, int), n=n)      # actif = visage de gauche
    _, (x0, _, w, _) = frame_params(plan_camera(tl, SW, SH, 10.0, Config()), 3.0)
    assert abs((x0 + w / 2) - 900) < 25                                             # milieu des deux visages
    _, (x0, _, w, _) = frame_params(plan_camera(tl, SW, SH, 10.0, Config(group_frame=False)), 3.0)
    assert abs((x0 + w / 2) - 800) < 25                                             # sans l'option : le locuteur


def test_group_frame_ignores_faces_too_far_apart_or_small():
    n = 100
    far = tl_from({0: trk(400, n=n), 1: trk(1500, n=n)}, np.zeros(n, int), n=n)
    _, (x0, _, w, _) = frame_params(plan_camera(far, SW, SH, 10.0, Config(layout="track")), 3.0)
    assert abs((x0 + w / 2) - 400) < 40
    small = tl_from({0: trk(800, size=200, n=n), 1: {**trk(1000, n=n), "size": np.full(n, 60.0)}}, np.zeros(n, int), n=n)
    _, (x0, _, w, _) = frame_params(plan_camera(small, SW, SH, 10.0, Config()), 3.0)
    assert abs((x0 + w / 2) - 800) < 25                                             # visage de fond ignoré
