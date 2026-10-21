# clipengine

Studio de clips pour un usage personnel : une vidéo longue (podcast, interview, live, cours) devient des clips
verticaux 1080×1920, recadrés sur la personne qui parle, sous-titrés mot à mot, avec le son normalisé. Chaque clip
reçoit une note détaillée et son kit de publication (titre, légende, hashtags).

L'analyse de moqi.fr qui a guidé ce travail est dans [`../ANALYSE-MOQI.md`](../ANALYSE-MOQI.md).

## Lancer

Prérequis : Python 3.10+, `ffmpeg` (avec libass et x264).

```bash
cd clipper/engine
python -m venv .venv && source .venv/bin/activate     # Windows : .venv\Scripts\activate
pip install -e '.[asr,download]'
clipengine serve                                       # puis http://localhost:8080
```

- Premier lancement : le modèle de transcription se télécharge (`--model small` par défaut ; `large-v3` est plus
  précis et bien plus lent sans carte graphique).
- Titres, accroches, légendes et avis éditorial par Claude : définir `ANTHROPIC_API_KEY` avant `clipengine serve`.
  Sans clé, tout fonctionne avec le score du moteur et des textes générés sans IA.
- En ligne de commande, sans page : `clipengine https://youtube.com/watch?v=… -o out --max-clips 8`,
  `clipengine episode.mp4 --transcript episode.srt --llm none`, `--no-render` pour le classement seul,
  `--style sport`, `--mode full`.

## La page

| Écran | Ce qu'il fait |
|---|---|
| **Studio** | Lien ou fichier, nombre de clips, durée (30 s à 2 min), mode (meilleurs moments / toute la vidéo), style. Réglages avancés : mise en page, langue, avis de Claude, taille de la réserve. |
| **Projet** | Progression de l'analyse, puis clips rendus au fil de l'eau. Chaque carte : lecteur, note + étiquette, **détail de la note** (9 critères), titre / légende / hashtags à copier, texte du moment, télécharger, refaire avec un autre style. Les moments **en réserve** se fabriquent d'un clic. Export zip avec `publication.txt`. |
| **Bibliothèque** | Tous les projets, leur avancement, suppression. Une vidéo déjà analysée est reconnue (liens `youtu.be`, `shorts`, `m.youtube` = même vidéo). |
| **Mon style** | 6 packs, 7 polices libres, couleurs, fond (contour, ombre, boîte, pastille), position, mots par ligne, apparition, accroche, signature, fond du plan entier. L'aperçu est le **rendu réel** du moteur. |

Sans moteur (page ouverte depuis un hébergement statique), la page passe en **aperçu** avec un projet d'exemple produit
par le moteur (`demo/`).

## Fonctionnement

| Étape | Module | Ce qui est fait |
|---|---|---|
| Source | `media` | ffprobe (rotation comprise), yt-dlp (YouTube, Twitch, Vimeo…), refus clairs : direct en cours, playlist, > 4 h, < 20 s, sans piste audio |
| Parole | `transcribe` | faster-whisper mot à mot + VAD ; import `.json` ou `.srt` ; transcription bornée à la durée réelle de la vidéo |
| Audio | `audio` | énergie en dB par pas de 50 ms, en flux (3 h = mémoire constante) |
| Moments | `candidates` | phrases entières, toutes les fenêtres de durée voulue, frontières de sujet (TextTiling), coupe au point le plus silencieux ; mode « toute la vidéo » : parties successives coupées sur fin de phrase |
| Note | `scoring` | 9 critères : accroche, chute, rythme, voix expressive, mots forts, densité, durée, un seul sujet, coupes propres. Étiquette Excellent / Fort / Correct / Faible |
| Tri | `select` | meilleurs d'abord, sans recouvrement ni redite ; fabriqués d'office au-dessus de `min_score`, en réserve jusqu'à `reserve_min_score` |
| Avis | `llm` | Claude note des moments **distincts**, propose titre, accroche, légende, hashtags ; mélange 50/50 avec le score du moteur |
| Visages | `face` | YuNet (repli Haar), suivi, locuteur actif par mouvement de la bouche, détection de plans |
| Caméra | `reframe` | zone morte, filtre médian, lissage avec anticipation, coupe franche si le saut est grand ; cadrage de groupe, suivi, plan entier ou deux personnes empilées |
| Rendu | `captions`, `render`, `styles` | ASS karaoké, zone sûre TikTok, accroche, signature, fonds du plan entier, loudnorm −14 LUFS |
| Projets | `projects` | état sur disque, file unique, reprise après redémarrage, rendu à la demande |

Polices : Poppins, Anton, Bebas Neue, Bangers, DM Serif Display, Varela Round, sous licence SIL OFL
(`clipengine/fonts/OFL-*.txt`). Elles sont embarquées : le rendu ne dépend pas des polices de la machine.

## API

| Route | Rôle |
|---|---|
| `GET /api/projects` · `POST /api/projects` | liste · création (JSON `{url, options, style, force}` ou corps vidéo + `?filename=`) |
| `GET /api/projects/<id>` · `DELETE` | détail (moments, clips, progression) · suppression |
| `POST /api/projects/<id>/render` | `{ids, style}` fabrique ou refait des clips |
| `POST /api/projects/<id>/retry` · `/style` | relance l'analyse · style par défaut du projet |
| `GET /api/projects/<id>/zip` | clips + `publication.txt` |
| `GET/POST /api/settings` · `GET /api/styles` | réglages par défaut · packs, polices, couleurs |
| `POST /api/style-preview` | PNG rendu par libass pour un style |
| `GET /files/<id>/<clip>` | mp4 / jpg / ass, requêtes Range gérées |

Un projet à la fois travaille (le rendu occupe tous les cœurs) ; le reste attend en file.

## Sécurité

- `CLIPENGINE_PASSWORD` active un mot de passe (Basic) sur tout, page comprise.
- Sans mot de passe, le serveur n'accepte que les hôtes locaux (contre le DNS rebinding) et **refuse d'écouter hors de
  cet ordinateur** (`--host 0.0.0.0` exige un mot de passe, ou `--insecure`).
- Les liens doivent être http(s) vers une **adresse publique** : boucle locale, réseaux privés et métadonnées cloud sont
  refusés (le serveur télécharge ce lien pour vous).
- Toute requête JSON ou tout dépôt doit avoir un type de contenu « non simple » : un formulaire ou un `fetch` simple
  venu d'un autre site est refusé.
- Le code source et les données des projets ne sont jamais servis.
- Limites connues : un lien qui redirige vers une adresse interne n'est pas vérifié après redirection ; pas de limite
  de débit ; le mot de passe circule en clair sans HTTPS (à placer derrière un proxy TLS si exposé).

## Réglages utiles (`Config`)

`min_score`, `reserve_min_score`, `max_clips`, `reserve`, `min_dur` / `max_dur` / `target_dur`, `mode`, `layout`,
`zoom`, `group_frame`, `cam_smooth`, `dead_zone`, `style`, `captions`, `title_overlay`, `crf`, `preset`.

## Tests

```bash
pytest -q                 # ≈ 100 tests, dont des rendus réels et 3 parcours dans un navigateur (≈ 1 min 30)
pytest -q -m "not slow"   # logique seule, quelques secondes
```

Les tests du navigateur demandent `pip install -e '.[ui]'` et un Chromium (ignorés sinon). Les tests de zone sûre rendent réellement chaque pack avec libass et mesurent la position des pixels.

## Limites connues

- Le locuteur actif est deviné par le mouvement de la bouche, pas par la voix : un locuteur hors champ ou une personne
  très agitée peut tromper la détection.
- Le score est un filtre heuristique non calibré sur des vidéos qui ont marché ; l'avis de Claude est facultatif.
- Une source 1080p recadrée en 9:16 est agrandie ×1,78 : pour plus de netteté, fournir du 4K.
- Non testés ici : téléchargement yt-dlp depuis YouTube, appel réel à l'API Anthropic, transcription de vraie parole,
  lecture des clips dans un navigateur avec décodeur H.264 (le Chromium de test n'en a pas).
- Pas de publication automatique ni de statistiques TikTok, pas de génération vidéo/image : voir `../ANALYSE-MOQI.md`.
