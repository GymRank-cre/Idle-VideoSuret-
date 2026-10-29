# DESIGN — Vision du jeu (proposition à valider par Sébastien)

**Nom provisoire : Idle Sécurité** (choisi par Sébastien).
Jeu mobile idle arcade (hybrid-casual), thème : entreprise d'installation de sécurité électronique.
Ambition : un jeu au niveau des idle arcade connus de l'App Store (progression claire, sensations « juteuses », plusieurs mondes).

## Ce qui fait un bon idle arcade (les leviers, par ordre d'importance)
1. **Les 5 premières minutes** : le joueur comprend sans texte, gagne vite, débloque quelque chose toutes les 20 à 30 secondes.
2. **Sensations (« juice »)** : chaque action se voit, s'entend, se sent (vol des caisses, chiffres flottants, confettis, vibrations, sons).
3. **Progression visible** : l'atelier grandit à vue d'œil (nouveaux postes, employés, zones), on veut toujours « la prochaine dalle ».
4. **Rythme de l'économie** : prix croissants calibrés, pics de joie réguliers, petits murs que l'on franchit en 1 à 3 minutes.
5. **Raison de revenir** : gains hors ligne, récompenses du jour, quêtes, événements.
6. **Mondes qui renouvellent le jeu** (voir plus bas) : une nouvelle mécanique tous les quelques niveaux.

## Les mondes (évolution technologique de la sécurité électronique)
1. **L'Atelier** — alarmes, caméras dôme, badgeuses. *(en cours)* Mécanique de base : ramasser, installer, vendre.
2. **Chantiers chez les clients** — camionnette qui transporte le matériel, installation sur site (maisons, commerces). Nouvelle mécanique : déplacements et livraisons.
3. **Banque et centre commercial** — vidéosurveillance en réseau, contrôle d'accès, coffres. Nouvelle mécanique : salle de contrôle avec écrans, files de clients importants.
4. **Ville connectée** — capteurs, intelligence artificielle, reconnaissance, drones de surveillance. Nouvelle mécanique : drones automatiques qui livrent et installent.
5. **Base du futur** — biométrie, hologrammes, satellites. Nouvelle mécanique : automatisation poussée, mini-jeux de piratage.
Chaque monde : nouveau décor, nouvelles machines, nouvelle tenue du technicien, nouvelle musique, un boss d'objectif (gros contrat à livrer).

## Boucle de jeu
- **Boucle courte (10 s)** : ramasser → déposer → billets → payer une dalle.
- **Boucle moyenne (5 à 15 min)** : débloquer poste, employé, zone ; contrat à livrer.
- **Boucle longue (jours)** : passage au monde suivant (type « expansion/rachat ») avec bonus permanents, arbre d'améliorations, collection de gadgets, événements.

## Monétisation (à garder juste, sans bloquer le joueur)
- Vidéos récompensées : gains ×2 un moment, coup de pouce, double gain hors ligne.
- Achat « sans publicité » et petits packs de départ.
- Pas de blocage payant ; les publicités ne coupent jamais le jeu sans que le joueur ait choisi.
- Nécessite des services externes (publicité, analyses) : on en discutera avec Sébastien avant d'ajouter un package.

## Indicateurs à mesurer avant de lancer (ordres de grandeur courants, à vérifier)
- Retour au jour 1 : visé 40 % ou plus ; jour 7 : visé 15 % ou plus.
- Durée de la première session : 8 minutes ou plus.
- Test fermé avec 20 à 50 vraies personnes avant toute sortie publique.

## Satisfaisant et magnifique : la barre de qualité (voulu par Sébastien, 29/10)
Le jeu tourne sur son iPhone (Artifact Claude et lien Vercel). Objectif suivant : **un jeu agréable à toucher et beau à regarder**, au niveau des idle arcade du classement App Store. Trois piliers, dans cet ordre de travail :
1. **Sensation (« juice »)** : chaque geste du joueur a un retour en moins de 100 ms (image, son, mouvement), aucun événement muet ; le personnage accélère et freine doucement, la pile rebondit, les billets sont aspirés, les compteurs défilent, un déblocage fait une vraie fête (confettis, secousse légère, zoom, son).
2. **Beauté** : direction **cartoon 3D lumineux** : couleurs franches (jaune/orange chantier + bleu/cyan « technologie »), éclairage chaud avec ombres douces, matières variées (sol, murs, métal, néon), décor vivant (étagères pleines, caméras, écrans, plantes, câbles), police arrondie grasse partout, interface cohérente (panneaux arrondis, icônes uniformes). Personnage plus gros à l'écran (au moins 9 % de la largeur sur 390 px).
3. **Fluidité** : **60 images par seconde visées sur iPhone** (WebGL), jamais en dessous de 45 ; les effets coûteux (ombres temps réel, post-traitement) ne sont activés que si la mesure le permet (`WebGlPostFxGate`, panneau de diagnostic).
**Méthode :** une petite étape à la fois, captures **avant / après** à chaque étape (390×844, caméra de jeu), relues par le cloud avec un œil de directeur artistique ; Sébastien juge sur l'iPhone. Références : Sébastien peut envoyer 2 ou 3 captures de jeux App Store qu'il aime, on s'y compare.

### Référence visuelle n°1 choisie par Sébastien : « Idle Lumber – Gestion d'usine » (App Store, 4,6 étoiles, 13 500 avis)
Lien : https://apps.apple.com/fr/app/idle-lumber-gestion-dusine/id1552153525 . **On s'inspire du niveau de finition et des principes, on ne copie ni les images, ni l'interface, ni les personnages** (jeu commercial protégé). Ce qu'on retient, après lecture des captures de la fiche :
1. **Cadrage serré, objets énormes** : les machines et les piles occupent la moitié de l'écran, tout est lisible au premier coup d'œil. À nous : personnage ≥ 9 % de la largeur, dalles ≥ 25 %, aucune zone vide à l'écran.
2. **Le monde déborde de l'écran** : jamais de sol gris vide ; le décor continue partout (forêt, rivière, route). À nous : sol plus riche, tapis et marquages, objets posés, **l'extérieur de l'atelier visible** (parking, camionnette, rue, herbe, ciel) au lieu de l'obscurité sur les bords.
3. **Maquette « jouet » très détaillée** : formes rondes et épaisses, couleurs saturées et harmonisées (vert/orange/bleu), éclairage doux, ombres douces, pas de contours noirs. Des milliers de petits éléments (troncs, copeaux) qui s'empilent et bougent.
4. **Police d'affichage grasse et condensée avec contour sombre** pour les titres et les chiffres (bannières « bois » en bas des captures). À nous : police libre arrondie/condensée (type Lilita One, OFL), contour sombre, ombre portée.
5. **Kit d'interface cohérent** : barre du haut en pastilles sombres arrondies (icône + valeur + « + »), boutons carrés arrondis orange avec icône blanche et pastille rouge « nouveau », raccourcis en bas (caméra, « x2 argent », boutique). À nous : mêmes principes avec notre identité (jaune sécurité, bleu technologie).
6. **Dalles d'achat** : plaque sombre arrondie avec **gros prix blanc contouré**, flèche « amélioration » et icône billet ; la main de tutoriel qui pointe. À nous : garder le jaune/noir « chantier » mais avec ce niveau de lisibilité (prix énorme, icône), main de guide.
7. **Retours flottants** : « +256 $ » avec icône de billet au-dessus de l'objet qui rapporte ; **bulles d'humeur** (emoji colère rouge) sur les clients qui attendent ; panneaux de commande avec portraits de clients dessinés.
8. **Le monde vit tout seul** : tapis qui défilent, scies qui tournent, camions qui passent. À nous : écrans qui clignotent, caméras qui pivotent, imprimante, ventilateur, vapeur, véhicules dehors.

## Ce qu'on ne peut pas promettre
Aucun jeu n'est sûr de devenir un succès : la réussite se vérifie avec de vrais joueurs (tests, chiffres, retours), pas avant. La bonne méthode : petites versions, tests réguliers, corrections.

## Priorités proposées (ordre)
1. Finir le « début parfait » : atelier propre, vrai personnage, 5 premières minutes.
2. Employés automatiques, améliorations, sauvegarde, gains hors ligne (étapes 6 et 7 de la feuille de route).
3. Clients avec commandes (étape 5) et première boucle complète.
4. Effets visuels, sons, vibrations (étape 8) : c'est ce qui donne la sensation « jeu de fou ».
5. Monde 2, puis test fermé avec de vrais joueurs.
6. Seulement ensuite : publicité, analyses, publication.
