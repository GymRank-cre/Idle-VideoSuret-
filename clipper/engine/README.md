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
- Les clés (toutes facultatives) sont décrites plus bas : sans elles, tout fonctionne avec le score du moteur et des
  textes générés sans IA ; avec elles, Claude revoit le choix des moments et écrit les textes, et l'onglet Découvrir
  liste les vidéos qui montent.
- En ligne de commande, sans page : `clipengine https://youtube.com/watch?v=… -o out --max-clips 8`,
  `clipengine episode.mp4 --transcript episode.srt --llm none`, `--no-render` pour le classement seul,
  `--style sport`, `--mode full`.

## Les clés, une par une

Elles se définissent comme **variables d'environnement** avant `clipengine serve`. Sur ton ordinateur :
`export NOM=valeur` (Windows : `setx NOM valeur`, puis rouvrir le terminal). Dans une session cloud : menu de
l'environnement (barre de titre de la session) → **Modifier** → variable d'environnement ; une **nouvelle session** la
prend en compte. Une clé ne se colle jamais dans un message, un fichier du dépôt ou une capture d'écran : si elle a fuité,
la supprimer et en créer une autre.

| Variable | Sert à | Coût | Vérifier |
|---|---|---|---|
| `ANTHROPIC_API_KEY` | choix des moments revu par Claude, titres, accroches, légendes | à l'usage, ≈ 0,11 $ par vidéo | `clipengine check-llm` |
| `YOUTUBE_API_KEY` (variable **ou** secret réseau) | onglet Découvrir (tendances, nouveautés des chaînes) | gratuit, 10 000 unités par jour | `clipengine check-youtube` |

### 1. Claude (`ANTHROPIC_API_KEY`)

1. Créer un compte sur [platform.claude.com](https://platform.claude.com) : c'est la Console de l'API, distincte de
   l'abonnement claude.ai (qui ne donne pas de clé).
2. Y ajouter un petit crédit (facturation à l'usage) : quelques dollars suffisent pour commencer.
3. Page **API keys** ([platform.claude.com/settings/keys](https://platform.claude.com/settings/keys)) → **Create key** :
   un nom, une expiration, **Linked account** = toi, et la **limiter à un seul espace de travail** (Default) — sinon l'API
   réclame un en-tête d'espace en plus et refuse la requête.
4. Copier la clé (`sk-ant-…`) tout de suite : elle ne s'affiche qu'une fois.
5. La définir (`ANTHROPIC_API_KEY`), puis `clipengine check-llm`.

Cette clé va **en variable d'environnement, pas en secret réseau** : l'agent proxy des sessions cloud n'ajoute jamais de
secret aux requêtes vers `api.anthropic.com` (limite de la plateforme, documentée par Anthropic).

Un appel par vidéo (≈ 12 000 caractères envoyés, 20 extraits), modèle `claude-opus-5-5` par défaut (réglage `llm_model` ;
`claude-sonnet-5-5` coûte moitié moins). Mesuré : 5 311 jetons envoyés et 4 384 reçus, soit environ 0,11 $ ; la Console
affiche la dépense réelle et permet de fixer une limite mensuelle. La page du projet indique si le choix a été revu par
Claude, ou pourquoi il ne l'a pas été (clé refusée, crédit, modèle, réseau, refus de sécurité).

### 2. YouTube Data API (`YOUTUBE_API_KEY`)

1. Se connecter avec un compte Google sur [console.cloud.google.com](https://console.cloud.google.com).
2. Sélecteur de projet → **Nouveau projet** (nom libre, par exemple « clipo ») → **Créer**, puis le sélectionner.
3. [Bibliothèque d'API](https://console.cloud.google.com/apis/library) → chercher **YouTube Data API v3** → **Activer**.
4. [Identifiants](https://console.cloud.google.com/apis/credentials) → **Créer des identifiants** → **Clé API** ; la copier.
5. **Restreindre la clé** → restrictions d'API → **YouTube Data API v3** → enregistrer : volée, elle ne sert à rien d'autre.
6. La fournir au moteur, de l'une de ces deux façons :
   - **variable d'environnement** `YOUTUBE_API_KEY` : la plus simple, mais lisible par quiconque utilise l'environnement ;
   - **secret réseau** (sessions cloud ; la session ne voit jamais la clé) : fenêtre de l'environnement → **Network
     secrets** → **Add secret** : *Allowed websites* = `www.googleapis.com` ; *Custom headers* = une ligne, nom
     `X-Goog-Api-Key`, **préfixe vide** (effacer « Bearer »), valeur = la clé. Le proxy ajoute alors l'en-tête aux requêtes
     vers ce site, et le moteur, qui n'a pas de variable, envoie sa requête quand même.
7. `clipengine check-youtube` : dit si ça marche et par quelle voie (variable ou secret réseau).

La clé voyage dans l'en-tête `X-Goog-Api-Key` (l'API YouTube le lit comme le paramètre `key`), jamais dans l'adresse.

Quota gratuit : 10 000 unités par jour ; une visite de l'onglet en coûte de 5 à 30 (réponses gardées 15 minutes, bouton
**Actualiser** pour interroger à nouveau). La documentation de Google ne mentionne aucune facturation pour cette API ; si
la console en demande une, s'arrêter là. Sans clé, l'onglet explique la marche à suivre. Par défaut la liste est en **français** et dans la catégorie « Parole » (actualités, blogs, sciences et tech, divertissement) : le classement général de YouTube est aux deux tiers du jeu vidéo.

### Réglages voisins (pas des clés d'API)

- `CLIPENGINE_PASSWORD` : mot de passe de tout le serveur, **obligatoire** pour l'ouvrir hors de cet ordinateur.
- `CLIPENGINE_COOKIES=/chemin/cookies.txt` : cookies du navigateur au format Netscape (extension de navigateur du type
  « Get cookies.txt »), pour les liens YouTube refusés avec « Sign in to confirm you're not a bot » (serveur, VPN).
  Ce fichier donne accès à ton compte : le garder hors du dépôt.

## La page

| Écran | Ce qu'il fait |
|---|---|
| **Studio** | Lien ou fichier, nombre de clips, durée (30 s à 2 min), mode (meilleurs moments / toute la vidéo), style. Réglages avancés : mise en page, langue, avis de Claude, taille de la réserve. |
| **Découvrir** | Les vidéos longues qui montent sur YouTube en France (classées par vues par heure ; âge, durée et catégorie au choix) et les nouveautés des chaînes que tu suis. **Clipper** lance l'analyse avec les réglages du Studio. Demande une clé YouTube gratuite (`YOUTUBE_API_KEY`). |
| **Projet** | Progression de l'analyse, puis clips rendus au fil de l'eau. Chaque carte : lecteur, note + étiquette, **détail de la note** (9 critères), titre / légende / hashtags à copier, texte du moment, télécharger, refaire avec un autre style. Les moments **en réserve** se fabriquent d'un clic. Export zip avec `publication.txt`. |
| **Bibliothèque** | Tous les projets, leur avancement, suppression. Une vidéo déjà analysée est reconnue (liens `youtu.be`, `shorts`, `m.youtube` = même vidéo). |
| **Mon style** | 6 packs, 7 polices libres, couleurs, fond (contour, ombre, boîte, pastille), position, mots par ligne, apparition, accroche, signature, fond du plan entier. L'aperçu est le **rendu réel** du moteur. |
| **Moteur** | Pour une page hébergée ailleurs : adresse du moteur + jeton, état de la connexion, les commandes pour le lancer. Cliquer sur l'état du moteur, en haut à droite, y mène. |

Sans moteur (page ouverte depuis un hébergement statique), la page passe en **aperçu** avec un projet d'exemple produit
par le moteur (`demo/`). Si `demo/decouvrir.json` existe (un relevé réel de la liste Découvrir, daté), l'onglet
l'affiche ; sinon il explique ce qu'il demande.

## Page sur Vercel, moteur chez toi

Deux morceaux, deux endroits :

| Morceau | Où | Pourquoi |
|---|---|---|
| **La page** (`index.html` + `demo/`) | Vercel : projet `clipo`, racine `clipper`, réglages dans [`../vercel.json`](../vercel.json) | Page statique : une adresse qui s'ouvre de partout. La commande de construction ne publie que ces deux éléments (jamais `engine/` ni les notes). |
| **Le moteur** (ffmpeg, Whisper, rendu) | Ton ordinateur, ou un serveur loué | Une analyse prend des minutes de calcul et plusieurs Go de disque : une fonction Vercel s'arrête bien avant et ne garde pas de fichiers. |

Pour que la page publiée pilote ton moteur :

```bash
clipengine serve --allow-origin https://clipo-rho.vercel.app   # affiche un jeton (gardé dans engine/data/token)
cloudflared tunnel --url http://localhost:8080                 # une adresse https publique vers ton moteur
```

Puis, sur la page : **Moteur** → coller l'adresse du tunnel et le jeton → *Tester et enregistrer*. L'adresse et le jeton
restent dans le navigateur de chaque appareil ; il faut recommencer l'étape du tunnel quand son adresse change (un tunnel
nommé, avec un nom de domaine, garde la même).

- Le jeton ferme l'API et les fichiers (`Authorization: Bearer …`) ; la page elle-même reste publique, elle n'a rien de
  secret. `--allow-origin` prend l'adresse exacte de la page (pas de joker) ; il demande un jeton. Variables : `CLIPENGINE_TOKEN`
  (le tien, 24 caractères au moins), `CLIPENGINE_ALLOW_ORIGIN` (liste séparée par des virgules). Pour changer de jeton :
  supprimer `engine/data/token` et relancer.
- Les clips (lecture, téléchargement, zip) se chargent avec `?m=<clé>`, une clé **dérivée** du jeton qui ne donne que la
  lecture des clips : le jeton n'apparaît dans aucune adresse (historique, journaux, captures).
- Hors de `localhost`, la page exige **https** (le navigateur refuse sinon). Le tunnel s'en charge.
- Limites : le moteur doit rester allumé ; Cloudflare plafonne les envois à environ 100 Mo par requête sur ses offres
  gratuites (au-delà : un lien, ou le dépôt du fichier depuis la page du moteur chez toi, `http://localhost:8080`) ;
  YouTube refuse beaucoup d'adresses de serveurs loués, donc les téléchargements passent mieux depuis ton ordinateur.
- La publication se refait avec un nouveau déploiement du projet Vercel quand la page change. Par défaut Vercel
  protège les adresses de déploiement ; l'adresse principale du projet est publique, avec `X-Robots-Tag: noindex`.

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
| `GET /api/discover/status` · `/trending?days=3&min=10&category=0&region=FR` · `/channels` | clé YouTube définie ? · vidéos qui montent · chaînes suivies et leurs dernières vidéos longues |
| `POST /api/discover/channels` `{query}` · `DELETE /api/discover/channels/<UC…>` · `POST /api/discover/seen` | suivre (`@nom`, adresse ou `UC…`) · ne plus suivre · marquer comme vu |
| `GET/POST /api/settings` · `GET /api/styles` | réglages par défaut · packs, polices, couleurs |
| `POST /api/style-preview` | PNG rendu par libass pour un style |
| `GET /files/<id>/<clip>` | mp4 / jpg / ass, requêtes Range gérées |
| `GET /api/health` | `{ok, auth}` ; avec un jeton, aussi `media` : la clé de lecture à ajouter aux adresses de clips (`?m=`) |

Un projet à la fois travaille (le rendu occupe tous les cœurs) ; le reste attend en file.

## Sécurité

- `CLIPENGINE_PASSWORD` active un mot de passe (Basic) sur tout, page comprise.
- `--token` / `CLIPENGINE_TOKEN` ferme l'API et les fichiers par un jeton (comparé en temps constant, échecs ralentis) ;
  avec `--allow-origin`, seules les pages listées reçoivent les en-têtes CORS. Voir « Page sur Vercel, moteur chez toi ».
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
pytest -q                 # 173 tests, dont des rendus réels et 14 parcours dans un navigateur (≈ 2 min 30)
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

### L'avis de Claude, avec le vrai appel (Claude Opus 5.5)

Deux étapes. D'abord sans clé : un agent Claude (modèle Sonnet) a répondu au prompt exact du moteur sur 20 extraits de
l'interview française, et sa réponse a été passée dans `rerank` et `propose` (choix : 3 clips sur 5 changent par rapport au
score du moteur seul ; notes de 6 à 87 là où le moteur reste entre 60 et 87). Ensuite avec une vraie clé, par le
chemin complet du moteur (SDK, repli de sécurité, `claude-opus-5-5`) :

- `clipengine check-llm` : l'API répond, le repli de sécurité (bêta `server-side-fallback-2026-07-01`) est accepté.
- Une vidéo entière (21 min, 20 extraits) : **5 311 jetons envoyés, 4 384 reçus** (réflexion comprise) ≈ 0,11 $ au tarif
  Opus 5.5 (4 $ / 20 $ le million) ; 192 s au total avec le rendu de 5 clips.
- Choix : « 98,2 % de dons », « un trafic multiplié par 16 pendant le confinement », « le Google du Libre », l'analogie
  supermarché / AMAP, « interfaces moins jolies mais moins énergivores ». Trois de ces cinq moments sont ceux de l'essai
  Sonnet ; relancé après un changement de consigne, le choix est identique (mêmes 5 moments).
- Textes : titres, accroches (« ×16 en une semaine », « Si personne ne donne, on arrête ») et légendes sont écrits comme
  on les publierait ; tous les chiffres se retrouvent dans la parole. Un titre allait plus loin que la personne (« les
  logiciels libres sont moches… et c'est voulu » pour « des interfaces peut-être moins jolies ») : la consigne exige
  maintenant la fidélité, et le même passage devient « Moins jolies, mais moins énergivores ». **Relire avant de publier.**
- Défaut du moteur trouvé en chemin : une variante d'un moment noté 5/100 pouvait repasser devant avec le seul score du
  moteur. Seuls les moments dont Claude a lu le texte exact peuvent maintenant passer devant.
- Limites : une vidéo, quelques appels ; la calibration des notes de Claude n'est pas vérifiée sur des vidéos qui ont
  marché.

## Limites connues

- **Le choix des moments est un score heuristique** : il trouve des passages complets et cohérents, il ne comprend pas
  le sujet. Il peut retenir une longue question de l'animateur, une présentation de parcours ou une transition. L'avis
  de Claude corrige cela dans l'essai ci-dessus (une vidéo : à confirmer sur d'autres).
- Sans modèle de langage, titre et accroche sont des phrases du clip (la plus courte et entière), pas des textes
  rédigés pour accrocher. En gros plan, l'accroche est omise plutôt que posée sur le visage (la carte du clip le dit).
- Le locuteur actif est deviné par le mouvement de la bouche, pas par la voix : un locuteur hors champ ou une personne
  très agitée peut tromper la détection.
- Pas calibré sur des vidéos qui ont marché : la note est un avis sur le contenu, pas une prévision de vues.
- Une source 1080p recadrée en 9:16 est agrandie ×1,78 (une source 480p, ×4) : pour plus de netteté, fournir du 4K.
- Noms propres mal reconnus (« Framasoft » devient « Framasov », « Sramasoft »…) : Whisper `small` ne connaît pas le
  vocabulaire ; `large-v3` fait mieux, plus lentement.
- Non testés ici : téléchargement YouTube depuis une connexion personnelle (depuis ce serveur : refusé par le contrôle anti-robot,
  message clair) ; lecture des clips dans un navigateur avec décodeur H.264 (le Chromium de test n'en a pas).
- **Découvrir** : les listes ont été essayées avec la vraie API YouTube (clé en secret réseau) ; l'interface, elle, contre un faux serveur. **Clipper une vidéo YouTube depuis un serveur cloud échoue** : YouTube répond 403 au téléchargement (message explicite) ; depuis ton ordinateur, ou avec des cookies, non essayé.
  « Vues par heure » est un indicateur de ce qui monte, pas une prévision. Le moteur ne vérifie pas que tu as le droit
  de clipper une vidéo : pour celle d'un autre créateur, c'est son accord qu'il faut (d'où « Mes chaînes »).
- Pas de publication automatique ni de statistiques TikTok, pas de génération vidéo/image : voir `../ANALYSE-MOQI.md`.
