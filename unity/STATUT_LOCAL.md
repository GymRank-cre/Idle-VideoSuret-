# Statut local (Claude Code, PC de Sébastien)
- Date et heure : 30/10/2026 17:09
- Étape en cours : UW-4 TERMINÉ et poussé (6 retouches d'UL, pincement testé, le jus) ; prochaine : UW-2 (monde vivant), puis U4
- État : en cours (chantiers U1 à U4 décidés par Sébastien ; un seul build, à la fin)
- Compilation 0/0, tests 206/206, test de fumée (300 s simulées) OK ; l'ancien jeu est au commit 25ac2e4
- Dernier build WebGL publié : P1 à P6 de l'ancien jeu (commit 26d129e) ; aucun nouveau build avant U4
- Taille du dépôt : .git ~980 Mo ; publier-build.ps1 non lancé (accord de Sébastien requis)
- Problème : sons jamais écoutés, poussière de déblocage peu visible ; 111 appels de dessin à 5 postes (cible 120, marge 9 pour UW-2), ~34 000 triangles ; allocations du jeu non isolées (mesure dominée par l'éditeur)
