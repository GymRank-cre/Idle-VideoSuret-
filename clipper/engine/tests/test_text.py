import json
from pathlib import Path

import numpy as np

from clipengine import Config
from clipengine.audio import AudioProfile
from clipengine.candidates import boundary_strengths, build_sentences, generate, make_candidates, snap
from clipengine.captions import build_ass, default_title, group_pages
from clipengine.llm import parse_json_array, rerank
from clipengine.models import Candidate, Sentence, Word
from clipengine.scoring import hook_score, payoff_score, score_candidates
from clipengine.select import select
from clipengine.transcribe import Transcript, load_srt, load_transcript

from conftest import make_words


def S(text, t0=0.0, per=0.3):
    return Sentence([Word(w, t0 + i * per, t0 + i * per + per * 0.9) for i, w in enumerate(text.split())])


def test_sentences_split_on_punctuation_and_pause(words):
    sents = build_sentences(words)
    assert len(sents) == 12
    assert sents[0].text.endswith("sommeil ?")
    assert all(s.start < s.end for s in sents)


def test_long_run_on_sentence_is_cut():
    ws = [Word(f"mot{i}{',' if i % 9 == 8 else ''}", i * 0.5, i * 0.5 + 0.4) for i in range(60)]
    sents = build_sentences(ws, max_len=10)
    assert len(sents) > 1 and all(s.end - s.start <= 12 for s in sents)


def test_topic_boundary_is_detected_at_the_subject_change(words):
    sents = build_sentences(words)
    bs = boundary_strengths(sents)
    # la rupture sommeil -> cuisine est après la phrase 7 ; l'intérieur du récit reste cohérent
    assert int(np.argmax(bs)) == 7 and bs[7] > 0.8
    assert max(bs[1:4]) < 0.3 and max(bs[9:11]) < 0.3


def test_generate_respects_duration_bounds(words):
    cfg = Config(min_dur=10, max_dur=30)
    sents = build_sentences(words)
    for lo, hi in generate(sents, cfg):
        assert 10 <= sents[hi].end - sents[lo].start <= 30


def test_snap_never_enters_neighbour_words(words):
    sents = build_sentences(words)
    cfg = Config()
    for lo in range(1, len(sents) - 2):
        a, b = snap(sents, lo, lo + 1, None, cfg)
        assert a >= sents[lo - 1].end and a <= sents[lo].start
        assert b <= sents[lo + 2].start and b >= sents[lo + 1].end


def test_snap_cuts_in_the_quietest_frame():
    sents = [S("fin de la phrase précédente.", 0.0), S("début du clip ici.", 3.0), S("suite.", 6.0)]
    db = np.full(200, -20.0)
    db[int(2.75 / 0.05)] = -80.0            # creux de silence entre les deux phrases
    a, _ = snap(sents, 1, 1, AudioProfile(db), Config())
    assert abs(a - 2.75) < 0.06


def test_hook_prefers_questions_over_dangling_openers():
    q = hook_score(S("Pourquoi personne ne vous dit la vérité ?"), None)
    dangling = hook_score(S("Et du coup il m'a dit que ça allait."), None)
    assert q > 0.7 and dangling < 0.3 and q > dangling + 0.4


def test_payoff_penalises_unanswered_question():
    assert payoff_score(S("Le résultat a été immédiat, je dors enfin.")) > payoff_score(S("Mais pourquoi alors ?"))


def test_scoring_ranks_the_story_above_the_dead_zone(words):
    cfg = Config(min_dur=12, max_dur=40, target_dur=25)
    sents = build_sentences(words)
    cands = score_candidates(make_candidates(sents, None, cfg), sents, None, cfg)
    best = max(cands, key=lambda c: c.heuristic)
    assert 0 <= min(c.heuristic for c in cands) and max(c.heuristic for c in cands) <= 100
    # un clip qui enjambe le changement de sujet est moins bien noté que le meilleur, et le meilleur n'enjambe pas
    cross = [c for c in cands if c.lo <= 7 < c.hi]
    assert cross and not (best.lo <= 7 < best.hi) and max(c.heuristic for c in cross) < best.heuristic - 3


def test_select_no_overlap_and_chronological(words):
    cfg = Config(min_dur=10, max_dur=30, min_score=0, max_clips=5, max_similarity=1.1)
    sents = build_sentences(words)
    cands = score_candidates(make_candidates(sents, None, cfg), sents, None, cfg)
    chosen = select(cands, cfg)
    assert len(chosen) >= 2
    for a, b in zip(chosen, chosen[1:]):
        assert a.end <= b.start + 1e-6


def test_select_drops_near_duplicates():
    mk = lambda s, e, t, sc: Candidate(s, e, 0, 0, text=t, score=sc)
    a = mk(0, 20, "le sommeil réparateur améliore la mémoire et la santé", 80)
    b = mk(40, 60, "le sommeil réparateur améliore la mémoire et la santé aussi", 79)
    c = mk(80, 100, "la sauce tomate demande du basilic et de la patience", 70)
    out = select([a, b, c], Config(min_score=0))
    assert [x.start for x in out] == [0, 80]


def test_transcript_roundtrip_and_srt(tmp_path, words):
    tr = Transcript("fr", words)
    p = tmp_path / "t.json"
    p.write_text(json.dumps(tr.to_json()))
    back = load_transcript(p)
    assert len(back.words) == len(words) and back.words[3].text == words[3].text
    srt = tmp_path / "a.srt"
    srt.write_text("1\n00:00:01,000 --> 00:00:03,000\nbonjour tout le monde\n\n2\n00:00:03,500 --> 00:00:05,000\nsuite\n")
    t2 = load_srt(srt)
    assert [w.text for w in t2.words] == ["bonjour", "tout", "le", "monde", "suite"]
    assert t2.words[0].start == 1.0 and abs(t2.words[3].end - 3.0) < 1e-6
    assert all(a.end <= b.start + 1e-9 for a, b in zip(t2.words, t2.words[1:]))


def test_llm_parse_and_rerank_blend():
    raw = 'Voici : [{"id":0,"score":90,"title":"Titre A","hook":"Stop","why":"x"},{"id":1,"score":10}] fin'
    assert len(parse_json_array(raw)) == 2
    cands = [Candidate(0, 30, 0, 0, text="a", heuristic=50, score=50), Candidate(40, 70, 0, 0, text="b", heuristic=60, score=60)]
    out = rerank(cands, Config(llm_weight=0.5), call=lambda cfg, p: raw)
    by_start = {c.start: c for c in out}          # id 0 = meilleur heuristique = celui qui commence à 40
    assert by_start[40].score == 75 and by_start[40].title == "Titre A" and by_start[40].hook == "Stop"
    assert by_start[0].score == 30


def test_llm_failure_keeps_heuristic():
    def boom(*a): raise RuntimeError("réseau")
    c = [Candidate(0, 30, 0, 0, text="a", heuristic=50, score=50)]
    assert rerank(c, Config(), call=boom)[0].score == 50


def test_ass_structure_and_timing(words):
    cfg = Config()
    sub = [w for w in words if w.end < 12]
    ass = build_ass(sub, 0.0, 12.0, cfg, "Pourquoi personne ne vous le dit")
    assert "[Script Info]" in ass and "PlayResX: 1080" in ass and "PlayResY: 1920" in ass
    lines = [l for l in ass.splitlines() if l.startswith("Dialogue: 1")]
    assert len(lines) == len(sub)
    def sec(t): h, m, s = t.split(":"); return int(h) * 3600 + int(m) * 60 + float(s)
    for l in lines:
        f = l.split(",", 9)
        assert sec(f[2]) > sec(f[1])
    assert any(l.startswith("Dialogue: 2") for l in ass.splitlines())   # bandeau d'accroche
    assert "\\clip" in ass                                                # barre de progression
    assert "&H4DE1FF&" in ass                                             # #FFE14D -> BGR


def test_pages_have_at_most_three_words(words):
    pages = group_pages(words)
    assert all(1 <= len(p) <= 3 for p in pages)
    assert sum(len(p) for p in pages) == len(words)


def test_default_title():
    assert default_title("Pourquoi ? Parce que.") == "Pourquoi ?"
    assert len(default_title("mot " * 60)) <= 60
