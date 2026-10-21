"""Projets : analyse, moments classés, réserve, rendu à la demande, doublons, erreurs, reprise après redémarrage."""
import json
import shutil
import subprocess

import pytest

from clipengine import projects as proj_mod
from clipengine.projects import Projects, clean_options
from clipengine.transcribe import Transcript
from conftest import wait_until

OPTS = {"max_clips": 2, "reserve": 2, "min_dur": 12, "max_dur": 30, "target_dur": 20, "llm": "none"}


@pytest.fixture()
def P(tmp_path, stub_engine):
    p = Projects(tmp_path / "data", base_cfg={"min_score": 0, "preset": "ultrafast"})
    yield p
    p.close()


def new_file_project(P, media_long, tmp_path, name="episode.mp4", opts=OPTS, **kw):
    f = tmp_path / f"in-{name}"
    shutil.copy(media_long, f)
    return P.create(file=f, filename=name, options=opts, **kw)


def ready(P, pid):
    return wait_until(lambda: (lambda d: d if d["status"] in ("ready", "error") else None)(P.public(pid)), 90)


def all_rendered(P, pid):
    return wait_until(lambda: (lambda d: d if all(x["state"] not in ("queued", "rendering") for x in d["proposals"])
                               else None)(P.public(pid)), 90)


def test_clean_options_bounds_and_filters():
    o = clean_options({"max_clips": 99, "min_dur": "x", "max_dur": 5, "mode": "evil", "layout": "split",
                       "captions": "yes", "language": "fr", "whisper_model": "large", "reserve": -3})
    assert o == {"max_clips": 15, "max_dur": 15.0, "layout": "split", "language": "fr", "reserve": 0}
    assert clean_options({"min_dur": 60, "max_dur": 30})["min_dur"] < 30            # incohérence corrigée
    assert clean_options(None) == {} and clean_options("x") == {}


def test_analysis_ranks_moments_and_renders_the_best_first(P, media_long, tmp_path):
    proj, dup = new_file_project(P, media_long, tmp_path)
    assert dup is False and proj["status"] in ("queued", "analyzing")
    d = ready(P, proj["id"])
    assert d["status"] == "ready" and d["language"] == "fr" and d["info"]["duration"] > 60
    props = d["proposals"]
    assert 2 <= len(props) <= 4
    assert [x["rank"] for x in props] == list(range(1, len(props) + 1))
    assert [x["score"] for x in props] == sorted([x["score"] for x in props], reverse=True)
    assert [x["auto"] for x in props] == [True, True] + [False] * (len(props) - 2)
    x = props[0]
    assert x["label"] in ("Excellent", "Fort", "Correct", "Faible") and x["title"] and x["hook"] and x["caption"]
    assert x["hashtags"] and all(h.startswith("#") for h in x["hashtags"])
    assert len(x["breakdown"]) == 9 and {"key", "label", "value", "weight"} <= set(x["breakdown"][0])
    assert 12 <= x["duration"] <= 31 and x["text"]
    d = all_rendered(P, proj["id"])
    states = [x["state"] for x in d["proposals"]]
    assert states[:2] == ["done", "done"] and set(states[2:]) <= {"reserve"}
    assert all(x["video"].endswith(".mp4") and x["poster"].endswith(".jpg") for x in d["proposals"][:2])
    assert (P.dir(proj["id"]) / "clips" / d["proposals"][0]["video"]).is_file()


def test_reserve_renders_on_demand_and_restyle_replaces_the_file(P, media_long, tmp_path, stub_engine):
    proj, _ = new_file_project(P, media_long, tmp_path)
    d = all_rendered(P, proj["id"]) if ready(P, proj["id"])["status"] == "ready" else None
    reserve = next(x for x in d["proposals"] if x["state"] == "reserve")
    assert P.request_render(proj["id"], [reserve["id"]]) == [reserve["id"]]
    d = all_rendered(P, proj["id"])
    got = next(x for x in d["proposals"] if x["id"] == reserve["id"])
    assert got["state"] == "done" and got["video"]
    # nouveau rendu avec un autre style : nouveau fichier, ancien supprimé
    first = d["proposals"][0]
    old = P.dir(proj["id"]) / "clips" / first["video"]
    assert old.exists()
    P.request_render(proj["id"], [first["id"]], {"name": "x", "font": "anton", "position": "haut"})
    d = all_rendered(P, proj["id"])
    new = d["proposals"][0]
    assert new["video"] != first["video"] and not old.exists()
    assert (P.dir(proj["id"]) / "clips" / new["video"]).exists()
    assert stub_engine[-1][3]["font"] == "anton" and stub_engine[-1][3]["position"] == "haut"
    # un moment déjà en file ou en rendu ne se met pas deux fois en file
    assert P.request_render(proj["id"], [999]) == []


def test_full_mode_cuts_the_whole_video_in_order(P, media_long, tmp_path):
    proj, _ = new_file_project(P, media_long, tmp_path, opts={"mode": "full", "min_dur": 15, "max_dur": 35,
                                                             "target_dur": 25, "llm": "none"})
    d = ready(P, proj["id"])
    props = sorted(d["proposals"], key=lambda x: x["start"])
    assert len(props) >= 2 and all(x["auto"] for x in props)
    for a, b in zip(props, props[1:]):
        assert 0 <= b["start"] - a["end"] < 1.5                      # enchaînées, sans trou ni recouvrement
    assert props[0]["start"] < 1.5 and props[-1]["end"] > 55
    assert all(x["duration"] <= 40 for x in props)


def test_duplicate_url_is_detected_and_force_creates_anew(P, media_long, tmp_path, monkeypatch):
    monkeypatch.setattr(proj_mod, "check_url", lambda u: u)

    def fake_fetch(url, out_dir, max_s=0, log=print):
        out_dir.mkdir(parents=True, exist_ok=True)
        dst = out_dir / "source.mp4"
        shutil.copy(media_long, dst)
        return dst, {"title": "Mon épisode", "id": "dQw4w9WgXcQ", "uploader": "x", "duration": 70}

    monkeypatch.setattr(proj_mod, "fetch", fake_fetch)
    a, dup = P.create(url="https://www.youtube.com/watch?v=dQw4w9WgXcQ", options=OPTS)
    assert not dup and a["source"]["key"] == "yt:dQw4w9WgXcQ"
    d = ready(P, a["id"])
    assert d["status"] == "ready" and d["title"] == "Mon épisode"
    b, dup = P.create(url="https://youtu.be/dQw4w9WgXcQ?t=5", options=OPTS)
    assert dup and b["id"] == a["id"]
    c, dup = P.create(url="https://youtu.be/dQw4w9WgXcQ", options=OPTS, force=True)
    assert not dup and c["id"] != a["id"]


def test_errors_are_clear(P, tmp_path, media_long, words, monkeypatch):
    def run(path, name):
        proj, _ = P.create(file=path, filename=name, options=OPTS)
        return ready(P, proj["id"])

    junk = tmp_path / "junk.mp4"
    junk.write_bytes(b"ceci n'est pas une video")
    assert "pas une vidéo" in run(junk, "junk.mp4")["error"]

    silent = tmp_path / "silent.mp4"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "lavfi", "-i", "color=black:s=320x240:d=40", "-pix_fmt", "yuv420p",
                    str(silent)], check=True)
    assert "piste audio" in run(silent, "silent.mp4")["error"]

    short = tmp_path / "short.mp4"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "lavfi", "-i", "color=black:s=320x240:d=5", "-f", "lavfi", "-i",
                    "sine=d=5", "-shortest", "-pix_fmt", "yuv420p", str(short)], check=True)
    assert "trop courte" in run(short, "short.mp4")["error"]

    from clipengine import pipeline
    monkeypatch.setattr(pipeline, "transcribe", lambda *a, **k: Transcript("fr", words[:3]))
    copy = tmp_path / "c.mp4"
    shutil.copy(media_long, copy)
    d = run(copy, "c.mp4")
    assert d["status"] == "error" and "Aucune parole" in d["error"]
    assert d["stage"] == "Échec"


def test_retry_after_failure(P, media_long, tmp_path, words, monkeypatch):
    from clipengine import pipeline
    monkeypatch.setattr(pipeline, "transcribe", lambda *a, **k: Transcript("fr", words[:3]))
    proj, _ = new_file_project(P, media_long, tmp_path)
    assert ready(P, proj["id"])["status"] == "error"
    monkeypatch.setattr(pipeline, "transcribe", lambda *a, **k: Transcript("fr", words))
    P.retry(proj["id"])
    assert ready(P, proj["id"])["status"] == "ready"
    with pytest.raises(Exception, match="pas en échec"):
        P.retry(proj["id"])


def test_delete_removes_everything_and_queued_work(P, media_long, tmp_path):
    proj, _ = new_file_project(P, media_long, tmp_path)
    ready(P, proj["id"])
    assert P.delete(proj["id"]) is True and P.delete(proj["id"]) is False
    wait_until(lambda: not P.dir(proj["id"]).exists(), 20)
    assert P.public(proj["id"]) is None and not P.exists(proj["id"]) and P.summaries() == []


def test_state_survives_a_restart_and_interrupted_work_resumes(P, media_long, tmp_path, stub_engine):
    proj, _ = new_file_project(P, media_long, tmp_path)
    d = all_rendered(P, proj["id"]) if ready(P, proj["id"])["status"] == "ready" else None
    root = P.root
    P.close()
    # on simule un arrêt brutal pendant un rendu
    f = root / proj["id"] / "project.json"
    data = json.loads(f.read_text())
    data["proposals"][0].update(state="rendering", progress=0.4)
    f.write_text(json.dumps(data))
    P2 = Projects(root, base_cfg={"min_score": 0})
    try:
        assert P2.public(proj["id"])["title"] == "episode"
        d2 = wait_until(lambda: (lambda x: x if x["proposals"][0]["state"] == "done" else None)(P2.public(proj["id"])), 30)
        assert d2["proposals"][0]["video"]
        assert [s["id"] for s in P2.summaries()] == [proj["id"]]
    finally:
        P2.close()


def test_settings_roundtrip_and_defaults_apply_to_new_projects(P, media_long, tmp_path):
    s = P.settings()
    assert s["style"]["name"] == "podcast" and s["defaults"] == {}
    P.save_settings({"style": {"font": "anton", "size": 99, "signature": "@moi"}, "defaults": {"max_clips": 3, "mode": "x"}})
    s = P.settings()
    assert s["style"]["font"] == "anton" and s["style"]["size"] == 99 and s["style"]["signature"] == "@moi"
    assert s["defaults"] == {"max_clips": 3}
    proj, _ = new_file_project(P, media_long, tmp_path, opts={"llm": "none", "min_dur": 12, "max_dur": 30})
    assert proj["options"]["max_clips"] == 3 and proj["style"]["font"] == "anton"
    p2, _ = new_file_project(P, media_long, tmp_path, name="b.mp4", opts={"llm": "none"}, style="sport")
    assert p2["style"]["font"] == "bebas"                                 # un pack choisi à la création l'emporte
    ready(P, proj["id"]); ready(P, p2["id"])
