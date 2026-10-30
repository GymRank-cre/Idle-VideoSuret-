# RÉSUMÉ DU MATIN (mis à jour par le cloud, pour Sébastien)

**Dernière mise à jour : 29/10, 23 h 40 (heure du PC) ; 10b-1 partie 1 relue.** Ce fichier est réécrit à chaque relecture.

## Où tu en es
- **Fait et relu** : 9a (joystick plein écran), 9b (balancement corrigé), 9c (écran gris : version visible dans ⚙, diagnostic), 10a (sensation de jeu), **K1 (police Lilita One, HUD en pastilles ardoise, écran titre « Jouer », dalles lisibles, nombres abrégés)**.
- **Le jeu dans l'app Claude est à jour (build K1)** : https://claude.ai/artifact/7yAkFibvWvKaahZygu5T1X
- **K1b et Q1 faites** (pile verticale, titre dégagé, test de fumée de 90 s, 38 vérifications, textes rangés, journal de déblocages) ; le jeu dans l'app Claude est au build K1b+Q1.
- **En cours côté PC** : **11a** (la chaîne de production : machines, tapis, goulots) : **11a-1 faite et relue** (machine générique, stocks, voyants, prix de dalle dans la plaque) ; **11a-2 (machines Test et Emballage, produits testé/emballé avec valeur croissante) et 11a-3 (tapis, vue d'ensemble de la caméra) faites et relues** ; **11a-4 faite : la 11a est terminée** (machines arrondies et animées, pastilles de stock, tapis courts, livreur de bout en bout, gains hors ligne de la chaîne, sauvegarde v4, build). **Le jeu de l'app Claude est au build 11a.** Gain mesuré : ~650 $/min avec la chaîne complète, contre ~270 sans. Plan relu et approuvé (6 ajustements : vue d'ensemble de la caméra, machines arrondies et non cubiques, objets lisibles à chaque étape, valeur croissante par maillon).
- **11b faite et relue** (dalles d'amélioration par machine : Vitesse et Ajouter, niveaux, paliers ×2 aux niveaux 10/25/50, flèche verte « meilleur achat » sur le goulot, pastille « Niv N », sauvegarde v5). **Le jeu de l'app Claude est au build 11b.**
- **11c faite (logique validée)** : panneau « Commandes » avec 3 clients (portraits, contenu, $ et Points Pro, Refuser/Accepter/x2 bientôt), quai d'expédition, camionnette avec bulle d'humeur, Points Pro (pastille cyan). **11c-bis faite et relue** (croix de fermeture, fond cliquable, portraits pastel, texte à la ligne, boutons alignés, guide qui suit l'objectif réel) ; **le jeu de l'app Claude est au build 11c-bis**. Reste à retoucher : icône du bouton Commandes illisible, pastille de stock qui se chevauche, plaque du quai trop grande, **la camionnette n'est pas encore visible** (à rapprocher du quai).
- **10b-1 partie 1 faite** (ciel clair à la place du noir, extérieur avec parking/arbres/lampadaire, planchette « Commandes » lisible) ; **le jeu de l'app Claude est au build 10b-1 partie 1** ; **le sol reste très vide** (ce que tu as critiqué) : j'ai demandé de reprendre le décor tout de suite (outil qui meuble l'atelier : marquages, étagères, établis, affiches, tuyaux, accessoires, avec une règle « chaque case de 3×3 m contient quelque chose »).
- **En cours côté PC maintenant : le décor (10b-1 suite + 10b-2)**, puis 11d (Réputation) et 11e (vue libre) (la beauté : extérieur de l'atelier, ciel, sol varié, éclairage doux, décor vivant, machines moins « glacières »).
- **File d'attente** : 11b (chaîne de production à tapis) -> 11b (améliorations par machine) -> 11c (commandes de clients) -> 10b (décor) -> 11d (Points Pro) -> 11e (caméra d'ensemble) -> 11f (deuxième usine).

## À tester demain matin (2 minutes)
1. Ouvre le jeu : https://claude.ai/artifact/7yAkFibvWvKaahZygu5T1X (écran de chargement jaune, puis « Idle Sécurité » et « Jouer »).
2. Réglages (⚙), en bas : note la **version** (date et heure du build).
3. **Pour voir la nouvelle chaîne, il faut jouer ~5 à 8 minutes** (dalles LIVREUR 250, TEST 400, EMBAUCHE 150, EMBALLAGE 900, TAPIS 600 et 1200) : tu verras les machines Test et Emballage, les tapis et la vue d'ensemble de la caméra (quand tu ne touches plus l'écran).
4. Joue 2 minutes : joystick à droite OK ? le technicien se balance-t-il encore ? la pile de caisses penche-t-elle trop ? écran gris ? Envoie une capture ou une vidéo.

## Ce qui t'attend
- **Grok** : le compte xAI n'a plus de crédits (erreur de facturation). Ajoute des crédits ou génère dans l'app Grok avec `unity/art/PROMPTS_GROK_ATELIER.md` (P1 puis P2 sur ton image) ; **change ta clé xAI**, elle a été collée dans la conversation.
- **Dépôt** : ~800 Mo en local ; chaque build ajoute ~17 Mo ; seuil d'alerte 1 Go (décision à prendre alors).

## Si rien n'a bougé cette nuit
Sur le PC : réveille-le, rouvre Claude Code, tape `/mcp` puis « Reconnect », puis `continue`. Tout le contexte est dans `TACHES.md`.
