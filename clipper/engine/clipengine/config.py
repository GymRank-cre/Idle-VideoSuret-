from __future__ import annotations

from dataclasses import dataclass, fields


@dataclass
class Config:
    # --- sélection des moments forts
    min_dur: float = 18.0
    max_dur: float = 60.0
    target_dur: float = 36.0
    dur_spread: float = 20.0
    max_clips: int = 8
    min_score: float = 38.0
    max_overlap: float = 0.0          # part de chevauchement tolérée entre deux clips
    max_similarity: float = 0.55      # similarité lexicale max entre deux clips retenus
    pad_before: float = 0.20
    pad_after: float = 0.40

    # --- transcription
    whisper_model: str = "large-v3"
    language: str | None = None
    device: str = "auto"
    compute_type: str = "auto"

    # --- LLM (réévaluation + titres) : "auto" | "claude" | "none"
    llm: str = "auto"
    llm_model: str = "claude-sonnet-5-5"
    llm_top: int = 14
    llm_weight: float = 0.5

    # --- recadrage
    out_w: int = 1080
    out_h: int = 1920
    fps: int = 30
    layout: str = "auto"              # auto | track | fit | split
    zoom: float = 1.0
    sample_fps: float = 10.0
    detect_width: int = 640
    cam_smooth: float = 0.45          # secondes (sigma gaussien)
    dead_zone: float = 0.12           # fraction de la largeur de crop
    cut_jump: float = 0.75            # saut > x * largeur de crop => coupe franche
    scene_threshold: float = 0.55

    # --- sous-titres
    captions: bool = True
    caption_font: str = "Inter"
    caption_size: int = 86
    caption_words: int = 3
    caption_upper: bool = True
    caption_color: str = "#FFFFFF"
    highlight_color: str = "#FFE14D"
    caption_y: float = 0.66           # position verticale (fraction de la hauteur)
    title_overlay: bool = True
    title_seconds: float = 2.8
    progress_bar: bool = True

    # --- encodage
    crf: int = 19
    preset: str = "medium"
    loudnorm: bool = True

    @classmethod
    def from_dict(cls, d: dict) -> "Config":
        names = {f.name for f in fields(cls)}
        return cls(**{k: v for k, v in d.items() if k in names and v is not None})
