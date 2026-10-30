# Nouveau cap de progression (décision de Sébastien, 30/10/2026)

Ce document passe avant `TACHES.md` pour l'ordre des chantiers : **ce chantier se fait avant UW-2 et U4**. Il complète `DESIGN.md`.

## Constat
Sébastien : « Le jeu n'est pas assez satisfaisant et surtout pas assez d'enjeux, on attend 10 min, on a déjà tout. »
Mesure du 30/10 avec un robot qui suit le « Meilleur achat » : 5e poste à 24-30 min, puis plus rien de neuf ; au plus 15 à 25 s d'attente entre deux achats pendant les 45 premières minutes. La Réputation n'est pas un vrai prestige : rien ne se remet à zéro.

## Ce que Sébastien a demandé
- Des étapes de progression comme Idle Lumber : **nouveaux sites, contrats à enjeu, rythme plus lent**.
- **4 machines en 3 exemplaires** (12 machines), avec des **emplacements** ; **DA d'usine gigantesque** ; **bon HUD** ; **bon système de livraison**.
- Au début, on est **obligé de recruter 3 rôles**, en version high-tech. Choix : **serre de cristaux à la place de la forêt, robot découpeur, robot graveur, drone récolteur**.
- Puis les machines, jusqu'à un montant qui permet de **changer d'usine**, avec de nouvelles caméras, de nouvelles versions, et **des caméras qui défendent le site**.

## Attention aux références
**La fiche App Store d'Idle Lumber est mensongère** (Sébastien) : tas de bois géants, camions fâchés et « -$ » rouges sont de la publicité. Ne pas s'en servir comme preuve de gameplay. Sources plus fiables : captures de Sébastien (promises), aide officielle Lumber Inc, guide LevelWinner. Vu sur 2 miniatures de vidéos, peut-être d'une ancienne version : usine vue de haut avec murs, sol quadrillé, emplacements marqués au sol, chariots élévateurs, HUD avec portraits à gauche et boutons à droite.
On reprend les mécaniques et le style jouet, on ne copie ni les images ni l'interface.

## Plan (ordre révisé avec le cloud le 30/10)
0. **Retouches et contrat de rythme (faits)** : jalons cibles dans le test unitaire, prix en temps de remboursement. Le tableau reste à valider par Sébastien.
1. **Étape 1 (faite)** : la serre remplace la Réception, 3 recrutements obligatoires, matière réelle.
2. **Usine géante et flux (fait)** : serre, 4 machines (Assemblage, Programmation, Test, Emballage) de 3 emplacements, Expédition ; stocks, tapis, chariots ; ce qui ne passe pas se vend au prix du poste ; un palier x2 double le débit.
3. **Build de contrôle** : seulement si Sébastien dit oui.
4. **Livraison, puis HUD** : quai, véhicules, commandes au flux réel, 2 objectifs toujours visibles. Le HUD s'ajustera avec les captures de Sébastien.
5. **Contrats à enjeu et changement d'usine** : un contrat raté perd la prime, jamais de progression (`QUALITE.md`).
6. **Nouvelles machines** : Programmation, robots, drone, caméras, en vrais modèles.
7. UW-2 (budget et décor fusionné), puis U4.

## Proposé à Sébastien, pas encore validé
- 5 usines, une génération de caméra par usine : dôme, motorisée 4K, thermique, intelligence artificielle, sentinelle. Elles remplaceraient les 4 sites proposés avant (Atelier, Entrepôt, Banque, Aéroport).
- Des intrus arrivent à la clôture, les caméras les repoussent. Échec : quelques dizaines de secondes d'arrêt, sans perte d'argent. Aucune trace de cette idée dans Idle Lumber : ce serait notre signature.
