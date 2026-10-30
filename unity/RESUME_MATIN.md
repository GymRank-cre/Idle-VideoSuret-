# RÉSUMÉ DU MATIN (mis à jour par le cloud, pour Sébastien)

**Dernière mise à jour : 30/10, ~08 h 45 (heure du PC) : le build final P1 à P6 est relu et publié.** Ce fichier est réécrit à chaque relecture.

## À FAIRE EN PREMIER AU RÉVEIL (5 minutes)
1. **Ouvre le jeu** dans l'app Claude : https://claude.ai/artifact/7yAkFibvWvKaahZygu5T1X (écran de chargement jaune, « Idle Sécurité », « Jouer »).
2. **Réglages (⚙), en bas : lis la version** (date et heure du build) et dis-la-moi : elle doit dater de **ce matin, vers 08 h 15** (heure du PC). Si elle est plus ancienne, ferme et rouvre le lien (le navigateur a gardé l'ancien jeu).
3. **Joue 2 à 5 minutes** et regarde ceci :
   - joystick à droite, le technicien ne se balance plus, pas d'écran gris ;
   - la **main de tutoriel** qui tape sur la première dalle ;
   - le **bandeau « Commande livrée ! »** quand tu livres une commande (les gains s'envolent vers les pastilles) ;
   - le bouton **Vue** (loupe) : glisser pour déplacer la caméra, pincer pour zoomer ;
   - dans ⚙, l'option **« Moins de secousses »**.
   Envoie-moi **une capture ou une courte vidéo**, même si tout va bien.
4. **Réponds à ces 3 questions** (oui/non suffit) :
   - **Branche « builds »** : j'autorise le PC à publier chaque build sur une branche séparée à un seul commit (remplacée à chaque fois) pour que le dépôt (~960 Mo) cesse de grossir ? Rien n'est fait sans ton accord.
   - **Prochaine étape** : (a) la **2e usine « Chantiers chez les clients »** (11f), (b) encore du polissage et du son, (c) préparer l'**App Store** (icône, captures, build iPhone dans le cloud) ? Je recommande (a) ou (c) selon ce que tu ressens en jouant.
   - **Images Grok** : tu ajoutes des crédits xAI, ou tu génères dans l'app Grok avec `unity/art/PROMPTS_GROK_ATELIER.md` et tu me donnes les images ?
5. **Change ta clé xAI** : elle a été collée dans la conversation, considère-la comme exposée.

## Où tu en es
- **Fait et relu** : joystick plein écran (9a), balancement corrigé (9b), écran gris : version dans ⚙ + diagnostic (9c), sensation de jeu (10a), police Lilita One + interface en pastilles + écran titre (K1/K1b), **chaîne de production** (Réception, Assemblage, Test, Emballage, Expédition, tapis) (11a), **améliorations par machine** (Vitesse, Ajouter, paliers ×2 aux niveaux 10/25/50) (11b), **commandes de clients** avec Points Pro (11c), **décor de l'atelier** (10b), **Réputation** : 10 bonus permanents (11d), **vue libre** au doigt (11e), polissage K2/Q2.
- **Dernier build, publié dans l'app Claude (version 13) : P1 à P6** (commit 26d129e). Contenu : main de tutoriel (P1), bandeau « Commande livrée ! » (P2), boutons cohérents (P3), option « Moins de secousses » + audit d'accessibilité (P4), tous les textes rangés pour une version anglaise avec test de complétude (P5), test de fumée étendu de 300 s simulées (P6, OK : achats, 2 commandes livrées, réputation, sauvegarde relue identique, 0 erreur). 141 tests unitaires sur 141.
- **File vide, le PC attend ta décision** : il ne démarre rien d'autre (pas de 11f, pas de nouveau monde). Build vérifié : 144 tests sur 144, test de fumée de 300 s simulées OK, **59,6 images/seconde** dans le navigateur (cible ≥ 55), 0 erreur. Script `unity/tools/publier-build.ps1` (branche « builds ») **préparé, jamais lancé**. Captures réelles : `unity/captures/P1-main.png`, `P2-bandeau.png`, `P2-puces-en-vol.png`.
- Rythme mesuré par le robot sur 7 minutes : 1re caisse 0:00, 1er billet 0:12, 1re dalle 0:49 (baissée à 60 $), employé 1:30, livreur 2:12, Test 2:48, Emballage 4:29, 1re commande 4:31 ; 1er tapis visé à 6:30.

## Ce qui n'est pas vérifié (honnêtement)
- **Aucun test sur ton iPhone** des derniers ajouts : les gestes de la vue libre sont testés avec de vrais événements tactiles injectés dans un navigateur, pas avec ton doigt. Dis-moi si ça accroche (inertie, pincement, tape sur une machine).
- **La camionnette au quai** est maintenant visible sur les captures (point clos) ; reste à voir son mouvement à l'arrivée sur ton téléphone.
- **Petites retouches repérées, volontairement pas faites** (il faudrait un nouveau build, donc ton accord pour le dépôt) : bandeau « Commande livrée ! » trop transparent (il recouvre les boutons de droite), première dalle « 60 » à moitié coupée au départ en portrait, icône du premier bouton orange de droite peu lisible. Détails dans `TACHES.md`.
- **Images par seconde sur iPhone** : mesurées dans le navigateur du PC, jamais sur le téléphone. Si ça rame, dis-moi à quel moment.
- Le bonus de Réputation « Commandes en cours +1 » est **masqué** (une seule commande à la fois pour l'instant).

## Si rien n'a bougé cette nuit
Sur le PC : réveille-le, rouvre Claude Code, tape `/mcp` puis « Reconnect », puis `continue`. Tout le contexte est dans `unity/TACHES.md`.
