# Statut local (Claude Code, PC de Sébastien)
- Date et heure : 30/10/2026 16:03
- Étape en cours : UL TERMINÉ et poussé (monde plein écran, caméra libre, plaques, fiche, POSTES) ; prochaine : UW-4 (le jus)
- État : en cours (chantiers U1 à U4 décidés par Sébastien ; un seul build, à la fin)
- Compilation 0/0, tests 201/201, test de fumée (300 s simulées) OK ; l'ancien jeu est au commit 25ac2e4
- Dernier build WebGL publié : P1 à P6 de l'ancien jeu (commit 26d129e) ; aucun nouveau build avant U4
- Taille du dépôt : .git ~980 Mo ; publier-build.ps1 non lancé (accord de Sébastien requis)
- Problème : pincement non testé, sons jamais écoutés, poussière invisible ; 107 appels de dessin à zoom 1 (74 à 2,6), ~33 900 triangles ; allocations du jeu non isolées (mesure dominée par l'éditeur)
