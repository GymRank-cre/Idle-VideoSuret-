# TACHES — canal entre Claude (cloud) et Claude Code (PC de Sébastien)

Sébastien ne relaie rien à la main. Ce fichier sert de boîte aux lettres.
Dépôt : GymRank-cre/Idle-VideoSuret-, branche `claude/session-01m9uc8tm3gflbgjv6ppb6n5-vc1d43`, dossier `unity/`.

## Rôles
- **Claude cloud** : écrit les tâches et les spécifications, relit le code poussé. Il ne peut pas compiler Unity.
- **Claude Code local** (projet `E:\PROJET 1\IdleSecurite`, MCP Unity connecté) : écrit le code, compile, lance Play, corrige, teste.

## Protocole (Claude Code local)
1. Au début de chaque session : lire `CLAUDE.md`, puis ce fichier.
2. Cloner le dépôt une fois à côté du projet (ex. `E:\PROJET 1\sync`) sur la branche ci-dessus. Ensuite `git pull` avant de commencer.
3. Faire la tâche **dans le projet Unity** (compilation et test réels via MCP).
4. Quand ça compile et que ça marche : copier `Assets/Scripts` et `Assets/Editor` du projet vers `sync\unity\Assets\`, mettre à jour la section « Résultats » ci-dessous, puis `git add`, `git commit`, `git push` sur la même branche. Ne jamais pousser ailleurs, ne pas créer de pull request.
5. Ne pas toucher aux fichiers hors de `unity/`.
6. Si un point de la spec est impossible ou ambigu : le noter dans « Questions », choisir l'option la plus simple et continuer.

## Règles de code (rappel de CLAUDE.md)
Une responsabilité par script, champs en `[SerializeField]`, valeurs d'économie dans des ScriptableObject, pooling pour les objets créés souvent, formes simples (cubes, capsules), aucun package externe sans le dire.

---

## Relecture cloud de l'étape 2 : validée
Code conforme à CLAUDE.md (une responsabilité par script, SerializeField, économie dans StationConfig, pooling). Rien à corriger, deux remarques pour l'étape 3 :
- `ResourcePile` fait encore `Instantiate` à chaque caisse alors que les caisses déposées au poste partent dans un pool : brancher `ResourcePile` sur un `ObjectPool` pour que les caisses soient réellement recyclées.
- Les billets restent au sol sans limite : normal jusqu'au ramassage (étape 3), qui doit les rendre au pool.

## Tâche en cours : étape 3 — Billets à ramasser, argent, HUD

**Objectif :** le joueur ramasse les billets du poste, l'argent s'affiche en haut à gauche, le stock de caisses en haut à droite.

**À créer**
1. `Wallet` (MonoBehaviour, une seule instance dans la scène) : `Money` (int), `Add(int)`, événement `OnChanged`. Rien d'autre.
2. `Bill` (MonoBehaviour sur le prefab de billet) : porte sa `Value`, fixée à l'apparition par `InstallStation` depuis `config.BillValue` (le poste continue de lire la config en direct).
3. `BillCollector` (sur le joueur) : quand le joueur touche un billet (zone de ramassage autour du joueur, rayon dans un `[SerializeField]`), le billet vole vers lui (~0,2 s), puis `Wallet.Add(Value)` et retour au pool de billets. Pas de limite de capacité pour les billets.
4. `MoneyHud` : texte en haut à gauche, valeur de `Wallet`, léger effet de gonflement à chaque gain. `CrateHud` : en haut à droite, `Count/Capacity` de `StackCarrier`. Pour cela, ajouter à `StackCarrier` : `public int Capacity` et un événement `OnCountChanged`.
5. `PrototypeSetup` : ajouter Wallet, BillCollector, les deux textes dans le Canvas existant (joystick). Utiliser le composant texte de Unity UI déjà installé, pas de package externe ; si TextMeshPro oblige à importer des ressources, préférer le texte UI classique et le dire dans Résultats.
6. Nettoyage : `ResourcePile` utilise un `ObjectPool` pour les caisses (voir relecture).

**Critères de réussite (test MCP, puis Sébastien)**
- 0 erreur de compilation, 0 erreur en Play.
- Déposer 3 caisses au poste, attendre la fin du travail : 5 billets au sol, le joueur les ramasse en passant dessus, l'argent monte de 5 × `billValue`.
- Le compteur de caisses en haut à droite suit la pile (0/8 à 8/8) ; l'argent est en haut à gauche, lisible sur un écran de téléphone (Canvas 1080×1920).
- Changer `billValue` dans `StationConfig` change le gain sans toucher au code.

**Hors périmètre :** dalles de déblocage, clients, employés, sauvegarde.

---

## Résultats (à remplir par Claude Code local)
_(vide — étape 3)_

## Questions (à remplir par Claude Code local)
_(vide)_


## Archive : résultats de l'étape 2 (Claude Code local)
**Étape 2 — Poste d'installation : compile, testée via MCP en Play.**
- Ajoutés : `StationConfig`, `ObjectPool`, `ProgressBar3D`, `InstallStation` ; `PrototypeSetup` construit le poste en `(-6, 0.1, 0)`, `Assets/Prototype/StationConfig.asset` (conservé s'il existe), prefab `Bill`, barre au-dessus du poste. `ResourcePile` non modifié.
- Console : 0 erreur, 0 warning (hors message de connexion du pont MCP).
- Test automatisé (pile de 8, joueur placé dans la zone) : les caisses partent une par une, le MAX disparaît dès la 1re caisse déposée, le poste stocke, 1er travail à 1,0 s, 5 billets à 2,8 s, 2e travail enchaîné, 10 billets à 4,9 s, 2 caisses restent au poste (6 consommées sur 8).
- Config lue en direct (`config.X` à chaque usage) : modifier l'asset change le comportement sans toucher au code.
- Le collider trigger est sur la racine `InstallStation` (non mise à l'échelle) ; le cube bleu 3×0.2×3 est son enfant `Visual`.
- Les billets sont dans un pool, au sud du poste, hors zone. `billValue` est dans la config mais pas encore utilisé (étape 3).
- Non vérifié visuellement : le rendu de la barre (remplissage jaune) et le vol des caisses ; à confirmer par Sébastien en Play.
- Le test a eu besoin de `EditorApplication.Step()` car l'éditeur non focalisé n'avance pas les frames.

## Historique
- Étape 0 : scripts de base compilés et validés (WASD, ramassage, MAX).
- Étape 1 : joystick tactile `VirtualJoystick` ajouté ; compilation OK, déplacement OK via `SetMoveInput` ; glissé réel à confirmer par Sébastien.
- Étape 2 : poste d'installation (StationConfig, InstallStation, ProgressBar3D, ObjectPool) compilé et testé via MCP ; rendu visuel à confirmer par Sébastien.
- Étape 2 relue côté cloud : validée. Étape 3 spécifiée.
