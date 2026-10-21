from __future__ import annotations

import argparse
import sys
from pathlib import Path

from .config import Config
from .pipeline import run


def main(argv: list[str] | None = None) -> int:
    argv = sys.argv[1:] if argv is None else argv
    if argv and argv[0] == "serve":
        from .server import serve_main
        return serve_main(argv[1:])
    p = argparse.ArgumentParser(prog="clipengine", description="Vidéo longue -> clips verticaux sous-titrés")
    p.add_argument("source", help="fichier vidéo ou URL (YouTube…)")
    p.add_argument("-o", "--out", default="out", help="dossier de sortie")
    p.add_argument("--transcript", help="transcription existante (.json mot à mot ou .srt)")
    p.add_argument("--max-clips", type=int)
    p.add_argument("--min-dur", type=float)
    p.add_argument("--max-dur", type=float)
    p.add_argument("--min-score", type=float)
    p.add_argument("--model", dest="whisper_model", help="modèle Whisper (tiny … large-v3)")
    p.add_argument("--language", help="code langue (auto par défaut)")
    p.add_argument("--llm", choices=["auto", "claude", "none"], help="réévaluation éditoriale par Claude")
    p.add_argument("--layout", choices=["auto", "track", "fit", "split"])
    p.add_argument("--zoom", type=float)
    p.add_argument("--style", help="pack de style : podcast, business, sport, humour, education, recit")
    p.add_argument("--mode", choices=["best", "full"], help="best = meilleurs moments, full = toute la vidéo en parties")
    p.add_argument("--no-captions", action="store_true")
    p.add_argument("--preset", help="preset x264 (ultrafast … slow)")
    p.add_argument("--no-render", action="store_true", help="analyse seulement (JSON), sans vidéo")
    a = p.parse_args(argv)

    d = {k: v for k, v in vars(a).items() if k in Config.__dataclass_fields__ and v is not None}
    if a.no_captions:
        d["captions"] = False
    cfg = Config.from_dict(d)
    results = run(a.source, Path(a.out), cfg, a.transcript, render=not a.no_render)
    for r in results:
        c = r.candidate
        print(f"{r.index:>2}. {c.start:7.1f}s +{c.duration:4.0f}s  {c.score:5.1f}  {c.title}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
