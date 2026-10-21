from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(frozen=True)
class Word:
    text: str
    start: float
    end: float
    prob: float = 1.0


@dataclass
class Sentence:
    words: list[Word]

    @property
    def start(self) -> float:
        return self.words[0].start

    @property
    def end(self) -> float:
        return self.words[-1].end

    @property
    def text(self) -> str:
        return " ".join(w.text for w in self.words)


@dataclass
class Candidate:
    start: float                      # bornes finales (après calage sur les silences)
    end: float
    lo: int                           # indices des phrases couvertes
    hi: int
    text: str = ""
    features: dict[str, float] = field(default_factory=dict)
    heuristic: float = 0.0            # 0..100
    llm_score: float | None = None
    score: float = 0.0                # note finale 0..100
    reasons: list[str] = field(default_factory=list)
    title: str = ""
    hook: str = ""
    why: str = ""

    @property
    def duration(self) -> float:
        return self.end - self.start

    def to_dict(self) -> dict:
        return {
            "start": round(self.start, 3),
            "end": round(self.end, 3),
            "duration": round(self.duration, 3),
            "score": round(self.score, 1),
            "heuristic": round(self.heuristic, 1),
            "llm_score": None if self.llm_score is None else round(self.llm_score, 1),
            "title": self.title,
            "hook": self.hook,
            "why": self.why,
            "reasons": self.reasons,
            "features": {k: round(v, 3) for k, v in self.features.items()},
            "text": self.text,
        }
