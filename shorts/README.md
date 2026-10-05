# Shorts « Astuces argent » (TikTok / Instagram Reels)

Pipeline 100 % automatisé : texte → voix ElevenLabs → vidéo verticale 1080x1920 avec
sous-titres mot à mot. Aucune dépendance npm, il faut seulement Node 18+ et `ffmpeg`.

## Pourquoi cette niche

Analyse vidIQ (France, 30 derniers jours, Reels + TikTok) : des comptes de 2K à 13K abonnés
atteignent 0,8 à 2,4 M de vues avec des formats « liste d'astuces argent ». C'est aussi la
niche où la valeur par vue est la plus haute (affiliation, sponsoring, RPM).
Pistes de rechange : astuces ChatGPT/IA (très porteur mais demande des captures d'écran),
psychologie en storytelling (le plus simple à automatiser).

## Utilisation

```bash
cp .env.example .env        # puis renseigner ELEVENLABS_API_KEY
node shorts/build.mjs voices                      # trouver un voice_id français
ELEVENLABS_VOICE_ID=... node shorts/build.mjs     # génère tous les épisodes
node shorts/build.mjs --only 03-epargne-automatique
node shorts/build.mjs --dry                       # test du rendu sans appeler l'API
```

Sorties dans `shorts/out/` (ignoré par git) : `<id>.mp4` et `<id>.json` (légende + hashtags).

## Contenu

`shorts/content/episodes.json` : 10 épisodes. Chaque épisode a un `hook` (affiché 3 s),
un `script` (texte lu), une `caption` et des `hashtags`. Structure d'un bon épisode :
hook dans la première phrase, 25-35 s, une action concrète, un appel à enregistrer/partager.

Le contenu reste général et pédagogique : pas de promesse de gain, pas de trading ni de jeux
d'argent, mention « pas un conseil financier » incrustée sur chaque vidéo. Relire chaque script
avant publication, les règles et taux changent.

## Sécurité

La clé ElevenLabs ne doit **jamais** être commitée : elle vit dans `.env` (ignoré) ou dans une
variable d'environnement.
