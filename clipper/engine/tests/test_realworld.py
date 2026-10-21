"""Régressions tirées de vraies transcriptions (Whisper « small » sur une interview en anglais) et du rendu réel :
ponctuation parasite, mots orphelins, phrases sans point, accroche posée sur un visage, volume trop bas."""
import json
import re
import subprocess

import numpy as np
import pytest

from clipengine import Config, kit
from clipengine.candidates import build_sentences, make_candidates
from clipengine.captions import build_ass, place_hook
from clipengine.face import Timeline
from clipengine.models import Word
from clipengine.reframe import face_zone, plan_camera
from clipengine.render import loudnorm_filter, measure_loudness
from clipengine.scoring import score_candidates

# 90 mots d'une vraie transcription (début, fin en secondes) : « … since Apollo down in Kennedy. so we'll go through
# all those checks … » — Whisper met un point à la fin d'un bloc de parole puis repart en minuscule, et laisse
# ensuite une longue phrase sans aucune ponctuation.
EXCERPT = """
    you'll|0.0|0.12 get|0.12|0.24 a|0.24|0.36 weather|0.36|0.52 brief,|0.52|0.88 and|1.24|1.48 then|1.48|1.72
    you'll|1.72|2.94 go|2.94|3.08 get|3.08|3.28 suited|3.28|3.58 up.|3.58|3.82 And|3.98|4.1 it's|4.1|4.3 the|4.3|4.48
    same|4.48|4.88 suit|4.88|5.78 -up|5.78|5.94 room|5.94|6.24 that|6.24|6.72 astronauts|6.72|8.24 have|8.24|8.56
    been|8.56|8.72 using|8.72|8.94 since|8.94|9.28 Apollo|9.28|9.76 down|9.76|10.18 in|10.18|10.4 Kennedy.|10.4|10.68
    so|10.68|11.06 we'll|11.06|12.1 go|12.1|12.22 through|12.22|12.44 all|12.44|12.62 those|12.62|12.84
    checks|12.84|13.26 with|13.26|13.92 the|13.92|14.12 suit|14.12|14.3 on|14.3|14.64 and|14.64|15.44 make|15.44|15.6
    sure|15.6|15.88 that|15.88|16.16 everything|16.16|16.66 with|16.66|16.98 our|16.98|17.1 suit|17.1|17.4
    works|17.4|18.04 the|18.04|18.24 way|18.24|18.36 it's|18.36|18.54 supposed|18.54|18.72 to|18.72|19.04
    and|19.04|19.78 then|19.78|19.92 we'll|19.92|20.16 walk|20.16|20.42 out|20.42|20.76 downstairs|21.6|22.16
    out|22.16|22.72 of|22.72|22.78 the|22.78|22.88 ONC|22.88|23.2 building|23.2|23.48 we'll|23.48|24.2 jump|24.2|24.68
    in|24.68|24.84 our|24.84|24.96 Teslas|24.96|25.4 and|25.4|25.88 we'll|25.88|26.06 ride|26.06|26.28 to|26.28|26.46
    the|26.46|26.54 pad|26.54|26.82 which|26.82|27.1 takes|27.1|27.34 roughly|27.34|27.7 20|27.7|29.14 -25|29.14|30.12
    minutes|30.12|30.44 typically|30.44|30.84 it's|30.84|31.22 like|31.22|31.34 we|31.34|31.48 tell|31.48|31.64
    everybody|31.64|31.98 at|31.98|32.28 Kennedy|32.28|32.54
""".split()


def excerpt_words():
    out = []
    for item in EXCERPT:
        text, s, e = item.rsplit("|", 2)
        out.append(Word(text, float(s), float(e)))
    return out


def sents_of():
    return build_sentences(excerpt_words())


# ----------------------------------------------------------------------------- phrases et bords

def test_phantom_period_is_a_soft_boundary_not_a_sentence_end():
    sents = sents_of()
    i = next(k for k, s in enumerate(sents) if s.text.endswith("Kennedy."))
    assert sents[i].soft_end                                  # « Kennedy. so we'll … » : le point est parasite
    assert sents[i + 1].text.startswith("so we'll")


def test_unpunctuated_run_on_is_cut_on_a_pause_never_on_a_connector():
    from clipengine.lexicon import DANGLING_END
    run = [s for s in sents_of() if s.start >= 10.6]
    assert len(run) >= 2 and all(s.end - s.start <= 14.0 for s in run)
    for s in run[:-1]:
        last = re.sub(r"[^\w']", "", s.words[-1].text.lower())
        assert last not in DANGLING_END, s.text               # jamais « … and », « … the », « … to »
        assert s.soft_end


def test_no_orphan_sentences():
    sents = sents_of()
    assert all(len(s.words) >= 3 for s in sents) and sum(len(s.words) for s in sents) == len(excerpt_words())


def test_real_excerpt_has_no_clean_cut_and_scores_accordingly():
    """Cette tranche n'a aucune frontière d'idée propre : aucun moment ne doit paraître « Excellent »."""
    cfg = Config(min_dur=8, max_dur=26, target_dur=14)
    sents = sents_of()
    cands = score_candidates(make_candidates(sents, None, cfg), sents, None, cfg)
    assert cands and all(c.features["start_clean"] < 1.0 or c.features["end_clean"] < 1.0 for c in cands)
    assert all(any("milieu d'une idée" in r for r in c.reasons) for c in cands)
    assert max(c.heuristic for c in cands) < 60


def test_a_cut_inside_an_idea_costs_points(words):
    cfg = Config(min_dur=12, max_dur=40, target_dur=25)
    sents = build_sentences(words)
    before = {(c.lo, c.hi): c.heuristic for c in score_candidates(make_candidates(sents, None, cfg), sents, None, cfg)}
    sents[5].soft_end = True                                  # un point parasite entre deux morceaux d'une même idée
    after = score_candidates(make_candidates(sents, None, cfg), sents, None, cfg)
    touched = [c for c in after if (c.hi == 5 or c.lo == 6) and (c.lo, c.hi) in before]
    untouched = [c for c in after if not (c.hi == 5 or c.lo == 6) and (c.lo, c.hi) in before]
    assert touched and untouched
    for c in touched:
        assert c.heuristic < before[(c.lo, c.hi)] - 3 and any("milieu d'une idée" in r for r in c.reasons)
    assert all(abs(c.heuristic - before[(c.lo, c.hi)]) < 1e-6 for c in untouched)


def test_edge_words_without_screen_time_are_not_captioned():
    from clipengine.pipeline import clip_words
    ws = [Word("fin", 0.0, 0.05), Word("avant", 0.05, 0.5), Word("separation.", 8.0, 8.48), Word("the", 8.48, 9.3),
          Word("a", 9.4, 9.5)]
    assert [w.text for w in clip_words(ws, 0.3, 8.53)] == ["avant", "separation."]      # « the » n'y est que 0,05 s
    assert [w.text for w in clip_words(ws, 0.3, 9.6)] == ["avant", "separation.", "the", "a"]


# ----------------------------------------------------------------------------- mots collés par Whisper

def test_french_elisions_and_hyphens_are_glued_back():
    from clipengine.transcribe import Transcript
    raw = ["C", "'est", "-à", "-dire", "qu", "'on", "self", "-sustaining", "20", "-25", "États", "-Unis,", "non,", "-x",
           "it", "'s", "qu", "'aujourd", "'hui"]
    tr = Transcript.from_json({"language": "fr", "words": [{"text": t, "start": i * 0.2, "end": i * 0.2 + 0.15, "prob": 0.9}
                                                          for i, t in enumerate(raw)]})
    assert [w.text for w in tr.words] == ["C'est-à-dire", "qu'on", "self-sustaining", "20-25", "États-Unis,", "non,", "-x",
                                          "it", "'s", "qu'aujourd'hui"]
    assert tr.words[0].start == 0 and abs(tr.words[0].end - (3 * 0.2 + 0.15)) < 1e-6


def test_tokens_split_french_elisions_and_contractions_never_become_hashtags():
    from clipengine import lexicon as L
    assert L.tokens("C’est l'esprit qu'on a aujourd'hui") == ["c", "est", "l", "esprit", "qu", "on", "a", "aujourd'hui"]
    tags = kit.hashtags("you'll we're we'll weather breakfast weather breakfast garments weather launch launch", 5)
    assert tags[:2] == ["#weather", "#launch"] or "#weather" in tags
    assert not {"#youll", "#were", "#well"} & set(tags)


# ----------------------------------------------------------------------------- accroche

def test_pick_hook_takes_a_whole_short_sentence_or_nothing():
    text = ("Il y a des choses que personne ne dit sur le sommeil et qui changent absolument tout dans la vie "
            "de tous les jours, vraiment. Pourquoi dort-on si mal ? Parce que le cerveau ne se coupe pas.")
    assert kit.pick_hook(text) == "Pourquoi dort-on si mal ?"              # question courte, entière
    assert kit.pick_hook("Un long récit sans fin qui continue encore et encore sans jamais de point pour finir "
                         "la phrase parce que la personne parle très vite") == ""
    assert kit.pick_hook("Et du coup je lui ai dit non. Mais bon.") == ""     # connecteur en tête, fragment
    assert kit.pick_hook("Le sommeil, c'est la base de tout. Il y a aussi autre chose.") == "Le sommeil, c'est la base de tout"
    hook = kit.pick_hook("Dix ans de sommeil perdu m'ont coûté cher. Voilà pourquoi.")
    assert hook and not hook.endswith("…")


def test_finalized_kit_never_carries_a_truncated_hook():
    from clipengine.models import Candidate
    from clipengine.pipeline import finalize_kit
    c = finalize_kit(Candidate(0, 30, 0, 0, text="mot " * 80))
    assert c.hook == "" and c.title                                          # pas d'accroche plutôt qu'une accroche coupée


def test_hook_goes_below_a_head_at_the_top_or_above_a_head_at_the_bottom_or_is_skipped():
    h = 0.09
    assert place_hook(0.105, h, None) == 0.105
    assert place_hook(0.105, h, (0.50, 0.80)) == 0.105                       # le visage est plus bas : on ne bouge pas
    top = place_hook(0.105, h, (0.10, 0.40))
    assert top is not None and top >= 0.40 and top + h <= 0.58               # sous la tête, au-dessus des sous-titres
    above = place_hook(0.105, h, (0.30, 0.70))
    assert above is not None and above + h <= 0.30                           # au-dessus de la tête
    assert place_hook(0.105, h, (0.12, 0.70)) is None                        # nulle part sans couvrir un visage
    assert place_hook(0.34 - h / 2, h, (0.0, 1.0)) is None                   # deux personnes empilées


def hook_top(ass: str) -> tuple[float, float]:
    ev = next(l for l in ass.splitlines() if l.startswith("Dialogue: 2") and ",Hook," in l)
    y = int(re.search(r"\\pos\(\d+,(\d+)\)", ev).group(1))
    return y / 1920, ev.count("\\N") + 1


def test_build_ass_moves_the_hook_off_the_face_zone():
    ws = [Word("bonjour", 0.2, 0.6), Word("tout", 0.7, 0.9), Word("le", 0.9, 1.0), Word("monde", 1.0, 1.4)]
    cfg = Config()
    free, _ = hook_top(build_ass(ws, 0.0, 6.0, cfg, "Pourquoi personne ne vous le dit"))
    moved, _ = hook_top(build_ass(ws, 0.0, 6.0, cfg, "Pourquoi personne ne vous le dit", avoid=(0.05, 0.40)))
    assert moved > 0.40 and moved > free                       # posée sous la tête
    assert "Hook" not in build_ass(ws, 0.0, 6.0, cfg, "Pourquoi personne ne vous le dit", avoid=(0.0, 1.0)).split("[Events]")[1]


def test_hook_wraps_in_balanced_lines_with_numbers_and_high_punctuation_kept_together():
    from clipengine.captions import wrap_px
    from clipengine.styles import SAFE_LEFT
    max_w = 1080 - 2 * SAFE_LEFT - 32
    lines, _ = wrap_px("800 000 utilisateurs par mois", "poppins-b", 62, max_w, max_lines=2)
    assert len(lines) == 2 and all(len(l.split(" ")) >= 2 for l in lines)            # pas de mot orphelin
    assert lines[0].startswith("800\u00a0000")
    lines, _ = wrap_px("Logiciels privateurs : pas la solution", "poppins-b", 62, max_w, max_lines=2)
    assert not any(l.startswith(":") for l in lines) and any(l.endswith("\u00a0:") or ":" in l for l in lines)
    lines, _ = wrap_px("Sans dons, on arrête", "poppins-b", 62, max_w, max_lines=2)
    assert lines == ["Sans dons, on arrête"]


# ----------------------------------------------------------------------------- zone des visages

def make_tl(tracks, n=100, fps=10):
    nvis = np.array([sum(not np.isnan(t["cx"][i]) for t in tracks.values()) for i in range(n)])
    return Timeline(np.arange(n) / fps, tracks, np.zeros(n, int) if tracks else np.full(n, -1), nvis, [], "test")


def trk(cx, cy=400, size=150, n=100):
    return {"cx": np.full(n, cx, float), "cy": np.full(n, float(cy)), "size": np.full(n, float(size)),
            "motion": np.zeros(n)}


def test_face_zone_follows_the_head_in_the_final_frame():
    from clipengine.reframe import frame_params
    tl = make_tl({0: trk(960, cy=250, size=160)})
    plan = plan_camera(tl, 1920, 1080, 10.0, Config())
    _, (x0, y0, cw, ch) = frame_params(plan, 1.0)
    a, b = face_zone(tl, plan, 0.0, 3.0)
    assert abs(a - max(0.0, (250 - 0.78 * 160 - y0) / ch)) < 1e-6 and abs(b - (250 + 0.62 * 160 - y0) / ch) < 1e-6
    assert 0.0 <= a < b <= 1.0


def test_face_zone_none_without_faces_and_full_height_for_stacked_layout():
    plan = plan_camera(make_tl({}), 1920, 1080, 10.0, Config())
    assert face_zone(make_tl({}), plan, 0.0, 3.0) is None
    n = 160
    tl = make_tl({0: trk(400, n=n), 1: trk(1500, n=n)}, n=n)
    tl.active = np.where((np.arange(n) // 40) % 2 == 0, 0, 1)
    plan = plan_camera(tl, 1920, 1080, 16.0, Config())
    assert plan.shots[0].layout == "split" and face_zone(tl, plan, 0.0, 3.0) == (0.0, 1.0)


def test_face_zone_ignores_a_small_face_in_the_background():
    tl = make_tl({0: trk(900, cy=500, size=200), 1: {**trk(1000, cy=120, size=40), "size": np.full(100, 40.0)}})
    plan = plan_camera(tl, 1920, 1080, 10.0, Config(layout="fit"))
    a, _ = face_zone(tl, plan, 0.0, 3.0)
    assert a > 0.3                                                           # le petit visage du fond ne compte pas


# ----------------------------------------------------------------------------- volume

@pytest.mark.slow
def test_two_pass_loudnorm_lands_on_the_target(tmp_path, media):
    src = media["pan"]
    m = measure_loudness(src, 2.0, 12.0)
    assert m and m["input_i"] < -10 and "linear=true" in loudnorm_filter(m) and "linear" not in loudnorm_filter(None)
    out = tmp_path / "n.wav"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-nostdin", "-ss", "2", "-t", "12", "-i", str(src), "-vn",
                    "-af", loudnorm_filter(m), "-ar", "48000", str(out)], check=True)
    r = subprocess.run(["ffmpeg", "-nostdin", "-hide_banner", "-i", str(out), "-af", "ebur128=peak=true", "-f", "null", "-"],
                       capture_output=True, text=True)
    lufs = float(re.findall(r"I:\s+(-?[\d.]+) LUFS", r.stderr)[-1])
    peak = float(re.findall(r"Peak:\s+(-?[\d.]+) dBFS", r.stderr)[-1])
    assert abs(lufs - (-14.0)) < 1.0 and peak <= -1.0


def test_loudness_of_silence_is_not_measured(tmp_path):
    wav = tmp_path / "s.wav"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "lavfi", "-i", "anullsrc=r=16000:cl=mono", "-t", "3", str(wav)],
                   check=True)
    assert measure_loudness(wav, 0.0, 3.0) is None and loudnorm_filter(None).startswith("loudnorm=I=-14")


# ----------------------------------------------------------------------------- rendu réel : l'accroche ne couvre pas le visage

def _video_with_face_at_top(path, face_img, dur=8.0, fps=25):
    """Un visage qui parle, placé haut dans l'image (comme une interview cadrée large)."""
    import cv2
    from conftest import _draw_face
    W, H = 1280, 720
    bg = np.full((H, W, 3), 70, np.uint8)
    cmd = ["ffmpeg", "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "bgr24", "-s", f"{W}x{H}", "-r", str(fps),
           "-i", "-", "-f", "lavfi", "-i", f"sine=frequency=180:duration={dur}:sample_rate=16000",
           "-af", "volume='0.25+0.2*sin(2*PI*3*t)':eval=frame", "-c:v", "libx264", "-preset", "ultrafast",
           "-pix_fmt", "yuv420p", "-c:a", "aac", "-shortest", str(path)]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for n in range(int(dur * fps)):
        fr = bg.copy()
        _draw_face(fr, face_img, 640, 170, True, n / fps)
        p.stdin.write(fr.tobytes())
    p.stdin.close()
    p.wait()
    return path


def _frame(video, at, path):
    import cv2
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-nostdin", "-ss", str(at), "-i", str(video), "-frames:v", "1", str(path)],
                   check=True)
    return cv2.imread(str(path))


@pytest.mark.slow
def test_rendered_hook_never_covers_the_face(tmp_path, face_img):
    from clipengine import face
    from clipengine.audio import compute_profile
    from clipengine.media import probe
    from clipengine.models import Candidate
    from clipengine.pipeline import render_candidate
    from clipengine.transcribe import Transcript
    src = _video_with_face_at_top(tmp_path / "top.mp4", face_img)
    info, prof = probe(src), compute_profile(src)
    tr = Transcript("fr", [Word(w, 0.4 + i * 0.35, 0.4 + i * 0.35 + 0.3) for i, w in enumerate("on commence ici avec une phrase".split())])
    c = Candidate(start=0.0, end=7.0, lo=0, hi=0, text="x", title="t", hook="Pourquoi personne ne vous le dit ?")
    detector = face.FaceDetector()
    with_hook = render_candidate(src, info, tr, prof, c, tmp_path / "a", "c", Config(preset="ultrafast"), None, detector)
    without = render_candidate(src, info, tr, prof, c, tmp_path / "b", "c", Config(preset="ultrafast", title_overlay=False),
                               None, detector)
    assert with_hook.hook_shown is True and without.hook_shown is None            # posée ailleurs ; non demandée
    fa, fb = _frame(with_hook.video, 1.5, tmp_path / "a.png"), _frame(without.video, 1.5, tmp_path / "b.png")
    diff = np.abs(fa.astype(int) - fb.astype(int)).max(axis=2) > 40
    rows = np.where(diff.any(axis=1))[0]
    assert len(rows) > 60, "l'accroche devrait être visible sur l'image"
    faces = detector.detect(fb)
    assert faces, "le visage doit être détecté dans le rendu sans accroche"
    x, y, w, h, _ = max(faces, key=lambda f: f[2] * f[3])
    # les sous-titres sont identiques dans les deux rendus : la différence est l'accroche seule
    assert rows.min() >= y + h - 0.05 * h or rows.max() <= y + 0.05 * h, (rows.min(), rows.max(), y, h)


@pytest.mark.slow
def test_rendered_audio_is_at_the_target_level_and_does_not_clip(tmp_path, media):
    from clipengine.audio import compute_profile
    from clipengine.media import probe
    from clipengine.models import Candidate
    from clipengine.pipeline import render_candidate
    from clipengine.transcribe import Transcript
    src = media["pan"]
    tr = Transcript("fr", [Word(w, 0.4 + i * 0.35, 0.4 + i * 0.35 + 0.3) for i, w in enumerate("une phrase de test".split())])
    r = render_candidate(src, probe(src), tr, compute_profile(src), Candidate(start=2.0, end=14.0, lo=0, hi=0, text="x"),
                         tmp_path, "c", Config(preset="ultrafast"))
    out = subprocess.run(["ffmpeg", "-nostdin", "-hide_banner", "-i", str(r.video), "-af", "ebur128=peak=true", "-f", "null", "-"],
                         capture_output=True, text=True).stderr
    lufs = float(re.findall(r"I:\s+(-?[\d.]+) LUFS", out)[-1])
    peak = float(re.findall(r"Peak:\s+(-?[\d.]+) dBFS", out)[-1])
    assert abs(lufs + 14.0) < 1.5 and peak <= -1.0, (lufs, peak)
