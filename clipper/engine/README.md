# clipengine

Moteur de clipping : une vidéo longue (podcast, interview, live) devient des clips verticaux 1080×1920,
recadrés sur la personne qui parle, sous-titrés mot à mot, avec le son normalisé.

```bash
pip install -e '.[asr,download,dev]'
clipengine https://youtube.com/watch?v=… -o out --max-clips 8
clipengine episode.mp4 -o out --transcript episode.srt --llm none     # sans Whisper ni LLM
clipengine episode.mp4 -o out --no-render                             # classement des moments seulement
```

Sortie : `01-titre.mp4`, `01-titre.json` (score, raisons, titre, texte), `01-titre.ass`, `report.json`.
Prérequis système : `ffmpeg` avec libass et x264. Variable optionnelle : `ANTHROPIC_API_KEY`.

## Pipeline

| Étape | Module | Ce qui est fait |
|---|---|---|
| Ingestion | `media` | ffprobe (rotation comprise) ; yt-dlp pour les URL |
| Parole | `transcribe` | faster-whisper mot à mot + VAD ; import `.json` ou `.srt` possible |
| Audio | `audio` | énergie en dB par pas de 50 ms, décodage en flux (3 h = mémoire constante) |
| Candidats | `candidates` | phrases entières, toutes les fenêtres 18–60 s, frontières de sujet (TextTiling), coupe au point le plus silencieux |
| Notation | `scoring` | 9 critères : accroche, chute, débit, expressivité vocale, mots forts, densité, durée, cohésion, bords |
| Redondance | `select` | glouton par score, sans chevauchement ni doublon de sujet |
| Éditorial | `llm` | Claude note les meilleurs candidats, propose titre et accroche ; mélange 50/50 avec l'heuristique |
| Visages | `face` | YuNet (repli Haar), suivi, locuteur actif par mouvement de la bouche, détection de plans |
| Caméra | `reframe` | zone morte, médian anti-fausses détections, lissage avec anticipation, coupe franche si le saut est grand |
| Mise en page | `reframe` | `track` (suivi), `fit` (plan entier sur fond flou), `split` (deux locuteurs éloignés, empilés) |
| Rendu | `captions`, `render` | ASS karaoké (3 mots, surbrillance + pop), bandeau d'accroche, barre de progression, loudnorm −14 LUFS |

## Réglages utiles (`Config`)
`min_dur` / `max_dur` / `target_dur`, `max_clips`, `min_score`, `layout`, `zoom` (>1 = plan plus serré),
`cam_smooth` (nervosité de la caméra), `dead_zone`, `caption_*`, `highlight_color`, `crf`, `preset`.

## Tests
```bash
pytest -q                 # 36 tests, dont 5 de bout en bout (vidéo synthétique, ~40 s)
pytest -q -m "not slow"   # logique seule, < 1 s
```

## Limites connues
- Le choix du locuteur repose sur le mouvement de la bouche, pas sur la voix : un locuteur hors champ
  ou une personne qui bouge beaucoup peut tromper la détection.
- Pas de diarisation audio ni de lecture sur les lèvres ; avec un seul visage c'est sans effet.
- Le score heuristique est un filtre ; le jugement éditorial fin vient de l'étape LLM.
- La source 1080p recadrée en 9:16 est agrandie ×1,78 : pour une meilleure netteté, fournir du 4K.
- Chemins non testés ici : téléchargement yt-dlp, appel réel à l'API Anthropic, transcription de vraie parole.
