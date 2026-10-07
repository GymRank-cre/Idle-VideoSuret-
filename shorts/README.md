# Shorts « Histoires de sûreté » (TikTok / Instagram Reels)

Les visuels (images ou clips) viennent de [modif.ai](https://modif.ai/fr/dashboard).
Tout le reste est automatique : voix ElevenLabs, montage plan par plan calé sur la voix,
mouvements de caméra, transitions, sous-titres animés, musique, bruitages, étalonnage, export.

## Lancer un montage

```bash
cp .env.example .env                          # renseigner ELEVENLABS_API_KEY
node shorts/montage.mjs 01-le-chat --preview  # aperçu rapide 540x960
node shorts/montage.mjs 01-le-chat            # export final 1080x1920, -14 LUFS
node shorts/montage.mjs voices                # liste des voix disponibles
```

Sorties dans `shorts/out/` : `<id>.mp4`, `<id>.jpg` (couverture), `<id>.txt` (légende).
Voix et musique sont mises en cache dans `episodes/<id>/cache/` : relancer un montage
après une retouche ne consomme aucun crédit ElevenLabs tant que le texte ne change pas.

## Créer un épisode

1. Générer les visuels sur modif.ai en 9:16 (768x1376 ou plus), même style cartoon.
2. Les déposer dans `shorts/episodes/<id>/assets/` (`.webp`, `.png`, `.jpg`, `.mp4`).
3. Écrire `shorts/episodes/<id>/episode.json` (voir `01-le-chat`) : une entrée par plan.

| Champ d'un plan | Rôle |
| --- | --- |
| `asset` | visuel du plan (image ou vidéo) |
| `say` | réplique lue pendant le plan ; le plan démarre sur son premier mot |
| `cam` | `in`, `out`, `punch`, `close`, `up`, `down`, `left`, `right`, `hold` |
| `focus` | point visé `[x, y]` entre 0 et 1 (visage, objet…) |
| `zoom` | `[début, fin]` pour forcer l'amplitude du zoom |
| `in` | transition d'entrée : `cut`, `whip`, `whip-up`, `flash`, `zoom`, `glitch`, `fade` |
| `label` | étiquette de personnage `"Nom\|Rôle"` |
| `emphasis` | mots affichés en rouge dans les sous-titres |
| `sfx` | bruitages : `{ "name": "alarm", "at": "mot:alarme" }`, `at` en secondes ou sur un mot |
| `musicDrop` | coupe la musique N secondes au début du plan (effet de chute) |

Bruitages disponibles : voir `lib/sfx.mjs`. Un nouveau son se déclare dans `sfxLibrary`
de l'épisode, il est généré une fois et conservé dans `shorts/audio/sfx/`.

## Générer les visuels automatiquement avec Grok (API xAI)

`grok.mjs` remplace la génération à la main (Higgsfield, Claude in Chrome) : il lit
`episodes/<id>/visuels.json`, crée une image par plan à partir des personnages de
`references/`, l'anime, puis télécharge `assets/pXX.png` et `assets/pXX.mp4`.

```bash
# .env : XAI_API_KEY=...   (clé créée sur https://console.x.ai, crédits API à part de l'abonnement Grok)
node shorts/grok.mjs 04-le-faux-technicien --images   # 1. storyboard seul, à vérifier
node shorts/grok.mjs 04-le-faux-technicien            # 2. animations (reprend où il s'est arrêté)
node shorts/grok.mjs 04-le-faux-technicien --only p07 --force   # refaire un plan raté
node shorts/grok.mjs 04-le-faux-technicien --montage  # animations puis montage final
```

- La durée de chaque clip est calculée d'après la voix déjà générée : on ne paie que les
  secondes montées (4 à 15 s par plan).
- Options : `--resolution 480p|720p|1080p` (720p par défaut), `--only p01,p02`, `--force`.
- Un plan dont l'animation échoue garde son image fixe : le montage l'utilise automatiquement.
- Coût indicatif (tarifs publics xAI, à vérifier) : environ 0,14 $ par seconde de vidéo en 720p
  (0,08 $ en 480p), soit 7 à 10 $ par épisode de 12 plans en 720p, plus les images.

`visuels.json` : une entrée par plan, `{ "id": "p01", "refs": ["cambrioleur"], "image": "…",
"animate": "…" }`, et un `costume` facultatif décrit dans `costumes` (voir l'épisode 4).

## Règles de montage appliquées

- La coupe arrive 3 images avant le mot : l'image précède le son.
- Jamais d'image figée : chaque plan bouge, avec un amorti.
- Sous-titres 2-3 mots, mot prononcé en jaune, hors des zones masquées par l'interface TikTok.
- Musique baissée automatiquement sous la voix (ducking), coupée sur les chutes.
- Loudness normalisée à -14 LUFS, plafond -1,5 dBTP.

## Banque d'histoires

`scripts/histoires-vraies.json` : 10 récits d'affaires réelles (Louvre, Anvers, Gardner,
Joconde, Target, Stuxnet, Notre-Dame…) prêts à être découpés en plans.

## Sécurité

La clé ElevenLabs ne doit **jamais** être commitée : elle vit dans `.env` (ignoré par git).
Les polices Anton et Bangers sont sous licence OFL (`fonts/`).
