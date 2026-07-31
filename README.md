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

## Boucle de jeu

1. **Services** — dix métiers à ouvrir, du poste d'accueil à l'audit sûreté.
   Chaque service tourne par cycles : on lance le cycle à la main, il verse son
   chiffre d'affaires à la fin. Recruter des postes augmente le CA, les paliers
   (25, 50, 100, 200…) le doublent.
2. **Chef de service** — une fois recruté, il relance les cycles tout seul :
   c'est ce qui rend le service *idle*, en jeu comme hors-ligne.
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
styles/main.css       thème « salle de contrôle »
src/format.js         mise en forme des nombres (échelle longue : k, M, Md, Bn…)
src/data.js           contenu : services, améliorations, R&D, contrats, objectifs
src/state.js          état, sauvegarde locale, migrations
src/economy.js        arithmétique : coûts, multiplicateurs, production, étoiles
src/game.js           boucle logique et actions du joueur
src/ui.js             rendu DOM et rafraîchissement par image
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
