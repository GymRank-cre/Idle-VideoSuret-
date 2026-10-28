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

## Relecture cloud de l'étape 3 : validée
Code propre (Wallet, Bill, BillCollector, HUD), pool partagé caisses/poste, texte UI classique sans TMP. Rien à corriger. Notes pour plus tard : l'argent n'est pas encore sauvegardé (étape 7) ; `BillCollector` fait un `GetComponent` par collider et par frame, acceptable pour l'instant.

## Tâche en cours : étape 4 — Dalles de déblocage (prix décroissant)

**Objectif :** une dalle au sol affiche un prix. Le joueur s'y place, son argent la paie petit à petit, le prix descend à vue d'œil ; à 0 la dalle disparaît et un 2e poste d'installation s'active.

**À créer**
1. `Wallet` : ajouter `bool TrySpend(int amount)` (retire si assez, renvoie false sinon, déclenche `OnChanged`). Un seul endroit où l'argent diminue.
2. `UnlockConfig` (ScriptableObject) : `price` (défaut 100), `payStep` (argent retiré à chaque pas, défaut 5), `payInterval` (secondes entre deux pas, défaut 0,05).
3. `UnlockPad` (MonoBehaviour, zone trigger comme `ResourcePile`) :
   - tant que le joueur est dans la zone et que `remainingPrice > 0` : toutes les `payInterval`, essaie de payer `min(payStep, remainingPrice, argent disponible)` via `Wallet`. S'il n'a plus d'argent, le prix reste là où il est (rien n'est perdu) ;
   - le prix restant est affiché sur la dalle (texte 3D : `TextMesh` intégré à Unity, face à la caméra, ou texte UI en monde ; pas de package externe) ;
   - à 0 : désactive la dalle et active l'objet `target` (le 2e poste), avec un petit effet (gonflement de 0 à 1 en ~0,3 s).
4. `PrototypeSetup` : dalle de 3×3 (cube plat jaune) à `(-6, 0.01, 5)`, un 2e `InstallStation` (même `StationConfig`, même construction que le 1er, factorisée en une fonction) à `(-6, 0.1, 9)`, **désactivé** au départ, relié à la dalle. Créer `Assets/Prototype/UnlockConfig.asset`. Une seule `Wallet`, le même `BillCollector` ramasse les billets des deux postes.
5. `Wallet` démarre à 0 : prévoir dans `PrototypeSetup` un champ `startMoney` (défaut 0) utilisable pour les tests, par exemple 100 en test, remis à 0 ensuite.

**Critères de réussite (test MCP, puis Sébastien)**
- 0 erreur de compilation ni en Play.
- Avec 100 en poche : le joueur sur la dalle, le prix passe de 100 à 0 en ~1 s, l'argent de 100 à 0, le 2e poste apparaît et fonctionne.
- Avec 40 en poche : le prix tombe à 60 puis s'arrête ; en gagnant plus d'argent et en revenant, on continue à payer depuis 60.
- Changer `price` dans `UnlockConfig` change le prix sans toucher au code.

**Hors périmètre :** clients, employés, sauvegarde, sons et effets avancés.

---

## Tâche parallèle : projet Unity complet sur GitHub (pour le build iOS cloud)
**REPORTÉE : Sébastien a choisi de finir le jeu avant le build iOS (licence Unity Personal, Codemagic exige Plus ou Pro). Ne la faire que si Sébastien le demande ou si le dépôt `GymRank-cre/idle-securite-unity` existe déjà : dans ce cas, seulement les points 1 à 3 (sauvegarde Git), pas les réglages iOS.**
Le dépôt privé `GymRank-cre/idle-securite-unity` doit être créé par Sébastien sur github.com (le cloud n'a pas le droit de créer des dépôts). S'il n'existe pas encore, le noter dans Questions et passer à la suite.
1. Dans `E:\PROJET 1\IdleSecurite` : `git init` si besoin, ajouter un `.gitignore` Unity standard (Library, Temp, Logs, obj, UserSettings, Builds, *.csproj, *.sln, .vs), vérifier que `Assets`, `Packages` et `ProjectSettings` sont suivis, ainsi que les `.meta`.
2. Ajouter `CLAUDE.md` et ce fichier `TACHES.md` à la racine de ce dépôt (copie), pour que la suite se fasse là-bas.
3. Branche `main`, commit « Projet Unity initial », `git remote add origin https://github.com/GymRank-cre/idle-securite-unity.git`, `git push -u origin main`. Si GitHub demande une connexion, le dire à Sébastien.
4. Réglages iOS (Project Settings > Player > iOS) : bundle identifier `com.gymrankcre.idlesecurite` (à confirmer par Sébastien), nom du produit « Idle Sécurité », portrait uniquement, iOS minimum 15.0. Ne rien activer d'autre.
5. Noter dans Résultats l'URL du dépôt et les réglages faits.

## Résultats (à remplir par Claude Code local)
**Étape 4 — Dalles de déblocage : compile, testée via MCP en Play.**
- Ajoutés : `UnlockConfig` (price 100, payStep 5, payInterval 0,05), `UnlockPad`, `PopIn` (gonflement 0→1 en 0,3 s avec léger dépassement), `FaceCamera` (étiquette face caméra). `Wallet` : `TrySpend(int)` (seul endroit où l'argent diminue) et champ `startMoney`.
- `PrototypeSetup` : la construction du poste est factorisée (`BuildStation(nom, position, ...)`, `BuildBillPrefab`, `LoadOrCreateStationConfig`). Poste 1 en `(-6, 0.1, 0)`, poste 2 `InstallStation2` en `(-6, 0.1, 9)` désactivé, même `StationConfig`. Dalle jaune 3×3 en `(-6, 0.01, 5)` avec `Assets/Prototype/UnlockConfig.asset` (conservé s'il existe). Constante `StartMoney = 0` en haut de `PrototypeSetup` (mettre 100 pour tester).
- Prix affiché par un `TextMesh` intégré (« $ 100 »), police `LegacyRuntime.ttf`, aucun package.
- Tests MCP : 0 erreur compilation, Build scene et Play. 40 en poche : prix 100 → 60, argent → 0, la dalle reste. +60 puis retour : le paiement reprend à 60 et finit en 0,70 s ; la dalle disparaît, le poste 2 apparaît (échelle 1,0 une fois le gonflement fini). Poste 2 : 3 caisses → 5 billets → ramassés par le même `BillCollector`, argent 0 → 50.
- Les billets du poste 2 tombent à `z = 5,8`, sur l'emplacement de la dalle : sans gêne car la dalle a disparu quand le poste 2 fonctionne.
- Non vérifié visuellement : lisibilité du prix 3D, effet de gonflement, couleur de la dalle.
- Tâche parallèle (dépôt `idle-securite-unity`) : non faite, marquée REPORTÉE.

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

## Archive : résultats de l'étape 3 (Claude Code local)
**Étape 3 — Billets, argent, HUD : compile, testée via MCP en Play.**
- Ajoutés : `Wallet`, `Bill`, `BillCollector` (sur le joueur), `MoneyHud`, `CrateHud`. `StackCarrier` : `Capacity` et événement `OnCountChanged`. `PrototypeSetup` : Wallet, BillCollector et les deux textes dans le Canvas du joystick (texte UI classique `Text`, police `LegacyRuntime.ttf`, aucun import TextMeshPro).
- Billets : le prefab porte un BoxCollider trigger ; `BillCollector` détecte par `OverlapSphereNonAlloc` (rayon 1,4), le billet vole 0,2 s vers le joueur, `Wallet.Add(Value)`, retour au pool. `InstallStation` fixe `Value` depuis `config.BillValue` à chaque apparition.
- Nettoyage : `ResourcePile` expose `Pool` ; `InstallStation` reçoit `cratesSource` (le `ResourcePile`) à la place de `cratePrefab` et remet les caisses dans ce pool partagé. Test : 6 caisses utilisées, 3 seulement créées.
- Test MCP : 0 erreur à la compilation, au Build scene et en Play. 3 caisses au poste, 5 billets au sol, ramassés : argent 0 → 50, HUD « $ 50 ». Compteur de caisses « 3/8 » puis « 0/8 ». Avec `billValue` = 25 : gain de 125 ; remis à 10.
- Non vérifié visuellement : lisibilité des textes (80 pt, Canvas 1080×1920), effet de gonflement, vol des billets.
- Le test a utilisé `EditorApplication.Step()` (éditeur non focalisé).

## Historique
- Étape 0 : scripts de base compilés et validés (WASD, ramassage, MAX).
- Étape 1 : joystick tactile `VirtualJoystick` ajouté ; compilation OK, déplacement OK via `SetMoveInput` ; glissé réel à confirmer par Sébastien.
- Étape 2 : poste d'installation (StationConfig, InstallStation, ProgressBar3D, ObjectPool) compilé et testé via MCP ; rendu visuel à confirmer par Sébastien.
- Étape 2 relue côté cloud : validée. Étape 3 spécifiée.
- Étape 3 : billets à ramasser, Wallet, HUD argent/caisses, ResourcePile sur pool partagé ; compilé et testé via MCP, rendu à confirmer par Sébastien.
- Étape 3 (billets, argent, HUD) relue côté cloud : validée. Étape 4 spécifiée. Build iOS cloud reporté.
- Étape 4 : dalle de déblocage à prix décroissant, 2e poste, Wallet.TrySpend ; compilé et testé via MCP, rendu à confirmer par Sébastien.
