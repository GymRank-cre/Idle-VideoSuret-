# Statut local (Claude Code, PC de Sébastien)
- Date et heure : 30/10/2026 19:11
- Étape en cours : (2) usine géante et flux réel terminés et poussés ; prochaine : (3) livraison puis HUD ; build de contrôle seulement si Sébastien dit oui
- État : en cours (chantiers U1 à U4 décidés par Sébastien ; un seul build, à la fin)
- Compilation 0/0, tests 267/267, test de fumée (300 s simulées) OK ; l'ancien jeu est au commit 25ac2e4
- Dernier build WebGL publié : P1 à P6 de l'ancien jeu (commit 26d129e) ; aucun nouveau build avant U4
- Taille du dépôt : .git ~980 Mo ; publier-build.ps1 non lancé (accord de Sébastien requis)
- Problème : pas de build, donc fps, SetSubMesh et instanciation non vérifiés en WebGL ; 171 à 194 appels de dessin dans l'éditeur (93 sans son regroupement), 52 000 à 58 000 triangles ; sons jamais écoutés ; tableau de jalons, usines et intrus à valider par Sébastien ; captures d'Idle Lumber attendues
