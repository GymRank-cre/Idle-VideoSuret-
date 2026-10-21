# Analyse de moqi.fr et ce qu'on en a gardé

Source : les pages publiques de moqi.fr (accueil, offre, aide, nouveautés, pages de fonctions, comparatifs) lues le
21 octobre 2026. Ce sont des affirmations de marketing et d'aide en ligne : je n'ai pas vu l'application connectée,
donc le fonctionnement interne est déduit, pas observé.

## Ce que fait Moqi

Un outil de création pour TikTok / Reels / Shorts, vendu à crédits (offre découverte puis 14,99 / 29,99 / 54,99 €) :
**Clips** (vidéo longue → clips), **Vidéo IA**, **Image IA**, **Coach / Moqi ia** (assistant qui lit les statistiques
TikTok, conseille, programme, publie), **publication automatique TikTok**, **Vidéo du jour** (classement des vidéos
longues qui montent sur YouTube France), API et connecteur pour Claude, affiliation et campagnes payées à la vue.

## Les bonnes idées, et ce qu'on en a fait ici

| Idée de Moqi | Ce qu'elle apporte | Ici |
|---|---|---|
| Clips coupés sur des phrases entières | Pas de clip qui démarre au milieu d'un mot | Repris. Les coupes tombent au point le plus silencieux, jamais dans un mot voisin. |
| Note sur 100 « avec la raison » | Savoir lequel poster d'abord | Amélioré : la note est détaillée sur 9 critères (barres), avec étiquette ; c'est un avis sur le contenu, jamais une promesse de vues. |
| Moments « en réserve » fabricables d'un clic | On ne paie pas pour ce qu'on ne veut pas | Repris : les meilleurs sont fabriqués d'office, les suivants attendent. Amélioré : seuils séparés (`min_score` pour fabriquer, `reserve_min_score` pour figurer en réserve). |
| « Il préfère 3 clips solides à 10 mous » | Pas de remplissage | Repris : moins de clips que demandé si la vidéo n'a pas assez de bons moments. |
| Clips livrés au fil de l'eau, on peut fermer la page | Pas d'attente devant l'écran | Repris : rendu clip par clip, état sur disque, reprise après redémarrage. |
| Bibliothèque + « déjà clippée cette vidéo » | Ne pas recalculer | Repris : détection par identité de vidéo (youtu.be, shorts, m.youtube… donnent la même clé). |
| Refus clairs (direct, trop long, sans son, sans parole) | Pas de rendu vide | Repris, avec un message lisible pour chaque cas. |
| Découper toute la vidéo en parties | Lives, cours | Repris : mode « Toute la vidéo », parties coupées sur fin de phrase et changement de sujet. |
| Recadrage sur qui parle, sans « ping-pong » sur un « hmm » | Cadre stable | Déjà là (hystérésis). Ajouté : cadrage de groupe quand tous les visages tiennent, deux personnes empilées quand elles sont trop loin. |
| Fond autour du plan entier (flou, couleur, miroir) | Slides, écrans | Repris. |
| « Mon style » (polices, couleurs, fonds, positions) + packs | Identité visuelle | Repris et amélioré : 6 packs, 7 polices libres embarquées, aperçu **exact** (le même rendu libass que le clip). |
| Sous-titres posés hors des boutons TikTok | Lisibles dans l'appli | Amélioré : largeur mesurée avec les vraies polices, et test automatique qui vérifie sur le rendu qu'aucun pixel ne dépasse la zone sûre. |
| Chiffres et mots qui portent la chute en couleur | Lecture plus facile | Repris. Mots douteux masqués plutôt qu'affichés faux. |
| Titre + légende + hashtags par clip | Prêt à publier | Repris (Claude si une clé est configurée, sinon heuristique), avec boutons « copier » et export zip + `publication.txt`. |
| Avis d'une IA sur les moments | Meilleur tri | Amélioré : l'avis porte sur des moments **distincts** (sinon les 14 premiers candidats sont des variantes du même passage). |
| Connecteur Claude (MCP) | Piloter l'outil en une phrase | **Pas fait** (voir plus bas). |

## Ce que je n'ai pas repris, et pourquoi

- **Comptes, crédits, abonnement, affiliation, campagnes payées** : sans objet pour un usage personnel.
- **Publication automatique TikTok et statistiques du Coach** : demandent l'API TikTok (application approuvée, OAuth).
  Sans accès je ne peux ni les construire ni les tester. À reprendre quand tu auras une application TikTok, ou via un outil
  de planification qui expose une API.
- **Vidéo IA / Image IA** : demandent un fournisseur de génération et une clé ; aucun n'est branché à ce serveur.
- **Vidéo du jour** : demande l'API YouTube Data (clé). Et Moqi précise lui-même de ne clipper que les chaînes qui
  l'autorisent : clipper la vidéo d'un autre est ta responsabilité.
- **Traduction des sous-titres** : possible via Claude, non faite (le minutage mot à mot ne survit pas à une traduction
  sans choix de conception).
- **Connecteur Claude (MCP)** : réalisable (un petit serveur local qui appelle l'API de ce serveur), non fait ici.
- **Textes marketing, prix, avis clients** : volontairement pas repris. L'ancienne maquette en reprenait de près ;
  l'interface actuelle n'a que des textes fonctionnels.

## Ce que Moqi dit faire et que je n'ai pas pu vérifier

La qualité de leur sélection de moments (une « IA lit toute la transcription »), leur détection du locuteur et leur
vitesse. Ici, la sélection est un score heuristique (9 critères) plus l'avis optionnel de Claude ; elle n'a pas été
calibrée sur des vidéos qui ont marché.
