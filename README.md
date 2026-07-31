# Sûreté Tycoon

Jeu **idle / incremental** dans l'esprit d'*Idle Office Tycoon*, transposé au monde de la
sûreté : vidéosurveillance, alarmes intrusion, contrôle d'accès, détection incendie,
télésurveillance, cybersécurité des flux vidéo et audit.

Aucun framework, aucun build : du HTML, du CSS et des modules JavaScript natifs.

## Lancer le jeu

Les modules ES imposent un serveur HTTP (l'ouverture directe du fichier ne marche pas) :

```bash
python3 -m http.server 8000
# puis http://localhost:8000
```

ou, si Node est disponible :

```bash
npx --yes serve .
```

Le déploiement se fait tel quel sur n'importe quel hébergeur statique
(Vercel, GitHub Pages, Netlify) : la racine du dépôt **est** le site.

## La tour

L'écran principal est l'immeuble de l'entreprise, vu **en coupe** : on le
construit étage par étage et on le parcourt en le faisant défiler. Chaque étage
est une pièce meublée où **le personnel se déplace** — plus il y a de postes,
plus la pièce se remplit. Une cage d'ascenseur dessert l'étage sélectionné, le
rez-de-chaussée porte l'enseigne, et le sommet reste un chantier tant qu'il
reste un métier à ouvrir. Tout le décor est en CSS : aucune image, aucun sprite.

Taper une pièce lance sa production ; le chevron ouvre le panneau de l'étage
(effectif, paliers, recrutement, chef de service). Les gains jaillissent
au-dessus de l'étage qui les produit.

## Maquettes 3D en cours d'arbitrage

Deux directions visuelles sont proposées pour remplacer la vue actuelle, sur les
quatre premiers étages :

| Maquette | Fichier | Technique |
| --- | --- | --- |
| A — vraie 3D | `demo/webgl.html` | WebGL via Three.js (`vendor/three.min.js`), caméra orbitale, ombres portées, personnel en volume |
| B — isométrique | `demo/iso.html` | CSS 3D pur (`preserve-3d`), aucune dépendance, personnel en panneaux face caméra |

Les prompts pour générer les modèles 3D de la maquette A sont dans
[`docs/prompts-modeles-3d.md`](docs/prompts-modeles-3d.md).

Les deux se manipulent : on fait pivoter la tour, on tape un étage pour lancer sa
production. Elles n'utilisent pas encore le moteur de jeu — ce sont des maquettes
d'aspect, avec des chiffres factices.

## Boucle de jeu

1. **Construire** — dix métiers à ouvrir dans l'ordre, du poste d'accueil à
   l'audit sûreté. Chaque étage tourne par cycles : on lance le cycle à la main,
   il verse son chiffre d'affaires à la fin. Recruter des postes augmente le CA,
   les paliers (25, 50, 100, 200…) le doublent.
2. **Chef de service** — une fois recruté, il relance les cycles tout seul :
   c'est ce qui rend l'étage *idle*, en jeu comme hors-ligne.
3. **Matériel** — investissements en euros : ×3 de CA, ×2 de vitesse, bonus globaux.
4. **Contrats** — des appels d'offres apparaissent régulièrement. Signés, ils
   versent une prime proportionnelle au CA du moment plus des **points de R&D**.
5. **R&D** — douze travaux permanents (rendement, hors-ligne, renfort d'équipe,
   valeur des étoiles). **Conservés d'une certification à l'autre.**
6. **Certification** — le prestige. On repart d'une entreprise neuve et on gagne
   des étoiles, chacune valant +2 % (+3 % avec la direction qualité) de CA global.

Bonus annexes : objectifs (+2 % chacun), renfort d'équipe (boost temporaire),
revenus hors-ligne plafonnés (4 h → 16 h selon la R&D).

## Tests

Un test de bout en bout pilote un vrai navigateur et rejoue la boucle complète
(ouvrir un service, cycle manuel, achats ×10/MAX, chef de service, matériel,
contrat signé puis livré, étude de R&D, renfort, certification, sauvegarde,
revenus hors-ligne), en échouant à la moindre erreur console :

```bash
npm install
npm test
# PLAYWRIGHT_CHROMIUM=/chemin/vers/chrome npm test  si un Chromium est déjà présent
```

## Structure

```
index.html            page unique
styles/main.css       décor de l'immeuble et habillage cartoon
src/format.js         mise en forme des nombres (échelle longue : k, M, Md, Bn…)
src/data.js           contenu : services, habillage des pièces, R&D, contrats, objectifs
src/state.js          état, sauvegarde locale, migrations
src/economy.js        arithmétique : coûts, multiplicateurs, production, étoiles
src/game.js           boucle logique et actions du joueur
src/tower.js          l'immeuble en coupe : étages, personnel animé, ascenseur
src/ui.js             onglets, panneau d'étage, rafraîchissement par image
src/main.js           démarrage, requestAnimationFrame, sauvegarde automatique
sw.js / manifest.json installation PWA
```

La partie est sauvegardée dans le `localStorage` du navigateur (clé
`idle-videosurete-save-v1`), toutes les 10 secondes et à chaque mise en arrière-plan.

## Équilibrage

Les courbes de coût et de revenu reprennent l'ossature éprouvée des idle games
« à la AdVenture Capitalist » : coût du poste *n* = `baseCost × growth^n`, doublement
du CA à chaque palier, et étoiles de prestige en `150 × √(CA cumulé / 10¹²)`.
Tous les nombres sont regroupés dans `src/data.js` et `src/economy.js` pour pouvoir
être retouchés sans toucher au reste.
