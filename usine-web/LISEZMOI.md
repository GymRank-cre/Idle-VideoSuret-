# Idle Sécurité Usine (version web, jouable dans l'app Claude)

Jeu idle sans personnage à piloter : l'usine tourne seule, on touche une icône (bouton de ligne, plaque au sol ou « Meilleur achat ») pour acheter ou améliorer.
Direction artistique : Idle Lumber (diorama isométrique, plaques d'achat au sol, pastilles sombres, boutons orange, panneaux orange et crème).

- `idle-securite-usine.html` : la page complète (une seule page, aucun fichier externe sauf la police Lilita One de Google Fonts).
- `src/econ.js` : toute l'économie (postes, coûts, paliers ×2, Réputation) ; `src/sim.js` : robot de simulation pour régler le rythme (`node sim.js 5`).
- `src/template.html` : interface, scène isométrique, sauvegarde, commandes ; `src/build.py` assemble les deux.

Rythme visé (robot qui achète le meilleur à chaque instant) : 2e poste ~1 min, 3e ~3 min, 4e ~11 min, 5e ~55 min.
Sauvegarde : `localStorage` du navigateur (par appareil), gains hors ligne plafonnés à 4 h (+2 h par niveau de « Veille automatique »).

## Disposition « Idle Lumber » (30/10)
Le monde occupe tout l'écran, la caméra est zoomée sur la zone active (glisser = déplacer, pincer = zoom, loupe = vue d'ensemble). On achète en touchant les **plaques au sol** (deux par poste : éclair = Vitesse, plus = Ajouter ; une étoile dorée marque la plus rentable) ; toucher une machine fait monter sa **fiche** en bas ; toucher la pastille de revenu ouvre la feuille **POSTES** (la liste complète).
