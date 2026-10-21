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
pip install -e '.[asr,download,llm]'
clipengine serve                                       # puis http://localhost:8080
```

- Premier lancement : le modèle de transcription se télécharge (`--model small` par défaut ; `large-v3` est plus
  précis et bien plus lent sans carte graphique).
- Titres, accroches, légendes et avis éditorial par Claude (extra `llm`, SDK officiel `anthropic`) :
  1. créer un compte sur [platform.claude.com](https://platform.claude.com) (la Console de l'API ; ce n'est pas
     l'abonnement claude.ai) et y ajouter un petit crédit (facturation à l'usage) ;
  2. page **API keys** (`platform.claude.com/settings/keys`) → **Create key** ; la **limiter à un seul espace de
     travail** (Default), sans quoi l'API réclame un en-tête d'espace en plus ; copier la clé (`sk-ant-…`), elle ne
     s'affiche qu'une fois ;
  3. la définir avant `clipengine serve` : `export ANTHROPIC_API_KEY=sk-ant-…` (Windows : `setx`) ;
  4. `clipengine check-llm` : un petit appel réel qui dit si la clé marche, et pourquoi sinon.

  Un appel par vidéo (≈ 12 000 caractères envoyés, 20 extraits), modèle `claude-opus-5-5` par défaut (réglage
  `llm_model` ; `claude-sonnet-5-5` coûte moitié moins). Estimation : de l'ordre de 10 à 20 centimes par vidéo, la
  réflexion du modèle comptant dans la sortie ; la Console affiche la dépense réelle. La page du projet indique si le
  choix a été revu par Claude, ou pourquoi il ne l'a pas été (clé refusée, crédit, modèle, réseau, refus de sécurité).
  Sans clé, tout fonctionne avec le score du moteur et des textes générés sans IA.
- Lien YouTube refusé avec « Sign in to confirm you're not a bot » (cas d'un serveur ou d'un VPN) : exporter les cookies
  du navigateur au format Netscape et lancer avec `CLIPENGINE_COOKIES=/chemin/cookies.txt`. Depuis une connexion
  personnelle, je n'ai pas pu tester : seul un serveur était disponible.
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
| Moments | `candidates` | phrases entières (Whisper met des points parasites suivis d'une minuscule et laisse des phrases sans point : ces coupes sont marquées « molles », jamais traitées comme de vraies fins de phrase), toutes les fenêtres de durée voulue, frontières de sujet (TextTiling), coupe au point le plus silencieux ; mode « toute la vidéo » : parties successives coupées sur fin de phrase |
| Note | `scoring` | 9 critères : accroche, chute, rythme, voix expressive, mots forts, densité, durée, un seul sujet, coupes propres. Malus quand un moment commence ou finit au milieu d'une idée (minuscule, mot de liaison, coupe molle). Étiquette Excellent / Fort / Correct / Faible |
| Tri | `select` | meilleurs d'abord, sans recouvrement ni redite ; fabriqués d'office au-dessus de `min_score`, en réserve jusqu'à `reserve_min_score` |
| Avis | `llm` | Claude note des moments **distincts**, propose titre, accroche, légende, hashtags ; mélange 50/50 avec le score du moteur |
| Visages | `face` | YuNet (repli Haar), suivi, locuteur actif par mouvement de la bouche, détection de plans |
| Caméra | `reframe` | zone morte, filtre médian, lissage avec anticipation, coupe franche si le saut est grand ; cadrage de groupe, suivi, plan entier ou deux personnes empilées |
| Rendu | `captions`, `render`, `styles` | ASS karaoké, zone sûre TikTok, accroche posée hors des visages (omise en gros plan, faute de place), signature, fonds du plan entier, son à −14 LUFS en deux passes (mesure puis gain) |
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

`min_score`, `reserve_min_score`, `max_clips`, `reserve`, `min_dur` / `max_dur` / `target_dur`, `mode`, `layout`, `llm_model`,
`zoom`, `group_frame`, `cam_smooth`, `dead_zone`, `style`, `captions`, `title_overlay`, `crf`, `preset`.

## Tests

```bash
pytest -q                 # ≈ 120 tests, dont des rendus réels et 3 parcours dans un navigateur (≈ 2 min)
pytest -q -m "not slow"   # logique seule, quelques secondes
```

Pour tout lancer : `pip install -e '.[dev,ui]'` ; les tests du navigateur demandent aussi un Chromium (ignorés sinon). Les tests de zone sûre rendent réellement chaque pack avec libass et mesurent la position des pixels.

## Vérifié sur de vraies vidéos

Deux interviews publiques de la NASA en anglais (4 min en 1080p ; 30 min en 480p à deux intervenantes), transcription
Whisper `small` sur processeur, 5 clips chacune :

- visage détecté dans 100 % des images mesurées (une toutes les 0,5 s) ; position du visage dans l'image finale :
  écart-type de 14 à 54 px sur 1080 de large (le cadre suit avec une zone morte : un orateur qui bouge bouge un peu
  dans le cadre) ; bascule sur la personne qui prend la parole quand elles alternent ;
- sous-titres lisibles et dans la zone sûre ; son à −14,0 / −14,2 LUFS (source très calme : −14,5 / −15,1, le plafond
  de crête à −1,5 dBFS limite le gain) ;
- les 10 clips commencent par une majuscule (début de phrase) et 9 finissent sur une ponctuation forte ; le dixième se
  termine sur un mot parasite de Whisper, de 0,05 s à l'écran, qui n'est plus sous-titré ;
- 4 min de vidéo analysées et rendues en ≈ 3 min ; 30 min en ≈ 5 min 30, sans carte graphique.

Ces essais ont fait corriger : la ponctuation parasite de Whisper (clips qui démarraient au milieu d'une phrase),
les coupes sur « and » ou « the », l'accroche posée sur le visage, les titres coupés en « … », le volume sous la cible.

Une interview française (Framasoft, 21 min en 720p, une personne à l'image, licence CC BY-SA), même chaîne, 5 clips :

- Whisper rend toute élision et tout trait d'union comme un mot à part (7 % des mots : « c » + « 'est »,
  « self » + « -sustaining ») : les sous-titres affichaient « C 'EST ». Ils sont recollés (`transcribe.glue_tokens`) ;
  les contractions anglaises (« you'll ») ne produisent plus de hashtags (#youll).
- Après l'encodage AAC, un clip dépassait 0 dBFS (+0,7 à +0,8) alors que `loudnorm` visait −1,5 : un limiteur suit maintenant
  `loudnorm` ; sur les 5 clips : −14,0 / −14,2 LUFS, crête −1,4 / −1,5 dBFS.
- Accroches sur deux lignes équilibrées, nombres (« 800 000 ») et ponctuation haute (« : », « ? ») jamais séparés.
- Visage détecté partout, accroche posée sous le menton (jamais sur le visage), accents et apostrophes corrects.

### L'avis de Claude, essayé une fois

Il n'y avait pas de clé dans l'environnement d'essai : **l'appel à l'API n'a pas été exécuté**. Pour savoir ce que vaut
l'avis, un agent Claude (modèle Sonnet, pas l'Opus 5.5 que le moteur appelle maintenant par défaut) a reçu le prompt
exact que le moteur envoie (consigne + 20 extraits distincts de l'interview française) et a répondu comme demandé ;
sa réponse a été passée dans `rerank` et `propose`. Côté appel, la requête que le SDK envoie (modèle, plafond, repli de
sécurité, en-têtes) et la lecture des erreurs sont vérifiées contre un faux serveur local, pas contre l'API réelle.

- Format : 20 réponses sur 20 exploitables. Notes réparties de 6 à 87, là où le score du moteur reste entre 60 et 87.
- Choix : 3 clips sur 5 changent. Écartés : une présentation de parcours, un passage technique confus, un témoignage flou
  (notes Claude 6, 32, 29). Retenus : « 98,2 % de dons », « 800 000 utilisateurs / le Google du Libre », « les quatre
  libertés du logiciel libre ». Jugement de l'auteur de ce moteur, à confirmer par le tien.
- Textes : tous les chiffres des titres, accroches et légendes se retrouvent dans la parole ; les légendes de deux clips,
  relues mot à mot contre la transcription, ne disent rien d'inventé ; les accroches sont réécrites
  (« Sans dons, on arrête ») au lieu de répéter la première phrase.
- Défaut du moteur trouvé grâce à cet essai : une variante d'un moment noté 5/100 pouvait repasser devant avec le seul
  score du moteur. Seuls les moments dont Claude a lu le texte exact peuvent maintenant passer devant.
- Limites : une vidéo, un seul passage, un agent de session et non l'API ; la calibration des notes de Claude n'est pas
  vérifiée sur des vidéos qui ont marché.

## Limites connues

- **Le choix des moments est un score heuristique** : il trouve des passages complets et cohérents, il ne comprend pas
  le sujet. Il peut retenir une longue question de l'animateur, une présentation de parcours ou une transition. L'avis
  de Claude corrige cela dans l'essai ci-dessus, mais **l'appel à l'API elle-même n'a jamais été exécuté ici**
  (`clipengine check-llm` le fait en une commande dès qu'une clé existe).
- Sans modèle de langage, titre et accroche sont des phrases du clip (la plus courte et entière), pas des textes
  rédigés pour accrocher. En gros plan, l'accroche est omise plutôt que posée sur le visage (la carte du clip le dit).
- Le locuteur actif est deviné par le mouvement de la bouche, pas par la voix : un locuteur hors champ ou une personne
  très agitée peut tromper la détection.
- Pas calibré sur des vidéos qui ont marché : la note est un avis sur le contenu, pas une prévision de vues.
- Une source 1080p recadrée en 9:16 est agrandie ×1,78 (une source 480p, ×4) : pour plus de netteté, fournir du 4K.
- Noms propres mal reconnus (« Framasoft » devient « Framasov », « Sramasoft »…) : Whisper `small` ne connaît pas le
  vocabulaire ; `large-v3` fait mieux, plus lentement.
- Non testés ici : appel réel à l'API Anthropic (`clipengine check-llm` le fait en une commande dès qu'une clé existe) ;
  téléchargement YouTube depuis une connexion personnelle (depuis ce serveur : refusé par le contrôle anti-robot,
  message clair) ; lecture des clips dans un navigateur avec décodeur H.264 (le Chromium de test n'en a pas).
- Pas de publication automatique ni de statistiques TikTok, pas de génération vidéo/image : voir `../ANALYSE-MOQI.md`.
