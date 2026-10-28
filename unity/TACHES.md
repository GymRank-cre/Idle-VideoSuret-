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

## Relecture cloud de l'étape 4 : validée
`UnlockPad`, `UnlockConfig`, `Wallet.TrySpend` conformes (un seul endroit où l'argent diminue, économie dans un ScriptableObject). Sébastien confirme que l'étape 4 fonctionne en jeu.

## Tâche en cours : étape 4b — Habillage visuel (« le début doit être beau »)

**Décision de Sébastien :** avant d'ajouter des fonctions, le début du jeu doit être propre et beau : un vrai personnage, un vrai atelier. (Cela remplace pour cette étape la règle « cubes d'abord » de CLAUDE.md. **Le gameplay ne change pas** : aucun script de jeu n'est modifié, sauf pour brancher les visuels.)

**Méthode : sans téléchargement d'abord.** Tout se construit par du code éditeur avec les formes de Unity (pas de package, pas d'asset externe). Une palette cohérente, du bon éclairage et des détails font déjà 80 % du rendu. Un pack d'assets gratuit (Kenney, licence CC0) ne sera envisagé qu'après, si Sébastien le demande.

**Séparer le visuel du jeu :** créer `Editor/WorkshopBuilder.cs`, menu `Tools > Idle Prototype > Build workshop` (relançable, ne supprime rien du jeu). `PrototypeSetup` appelle `Build workshop` à la fin. Les matériaux vont dans `Assets/Prototype/Materials`.

**1. Ambiance (URP)**
- Palette : bois clair, gris-bleu acier, blanc cassé, accents jaune sécurité et orange ; 8 à 10 couleurs au total, définies en tête du fichier.
- Lumière : une lumière directionnelle douce (ombres douces activées), lumière ambiante chaude, ciel uni. Pas de fog.
- Volume de post-traitement global : Tonemapping (ACES), Bloom léger, Vignette légère, Color Adjustments (contraste +10, saturation +10). Pas de package à ajouter (inclus dans URP).

**2. L'atelier (pièce ouverte côté caméra)**
- Sol : grandes dalles en damier de deux gris clairs, bords légèrement plus sombres. Plus de grand plan gris uni.
- Murs : deux murs (fond et gauche) avec plinthe, bande de couleur et hauteur ~3 m ; **pas de mur côté caméra**. Une fenêtre (cadre + vitre bleu pâle) au fond.
- Décor sécurité électronique : tableau mural avec outils (tournevis, pince, câbles : formes simples), une grille de caméras dôme au mur, une enseigne néon « SÉCURITÉ » (`TextMesh`, émissif), étagères métalliques avec des cartons, bobines de câble au sol, une armoire électrique avec de petites lumières vertes/rouges, un poster.
- Zone de ramassage (tas gris) : remplacer par **une palette en bois chargée de cartons** (palette plate + 3 à 4 cartons empilés).
- Poste d'installation : **établi** (plateau en bois sur 4 pieds métalliques), avec un écran d'ordinateur, une caméra dôme en cours d'assemblage, un tapis de travail. La barre de progression flotte au-dessus.
- Dalle de déblocage : dalle jaune avec **bandes noires en biais** (texture générée par code) et léger halo.
- Garder des zones libres autour du joueur : les distances de jeu (positions des postes et de la dalle) ne changent pas.

**3. Le technicien**
- Remplacer la capsule orange par un personnage construit en formes simples : jambes (2 cylindres) et bottes noires, torse en **gilet orange haute visibilité** avec bandes réfléchissantes, tête (sphère couleur peau), **casque de chantier jaune**, bras, **ceinture d'outils** (petits cubes). Proportions « mignonnes » : tête grosse, corps court.
- Animation sans package : légère oscillation verticale à la marche, inclinaison vers l'avant selon la vitesse, bras qui balancent, petit « souffle » à l'arrêt. Dans un script `CharacterAnimator` qui lit `PlayerController.IsMoving` (propriété existante).
- Le `CharacterController`, `StackCarrier` et le point d'accroche de la pile restent inchangés (la pile doit rester bien dans le dos).

**4. Petits détails de finition**
- Ombre ronde douce sous le personnage et sous les caisses (disque sombre transparent).
- Caisses : cartons marron avec bande de ruban clair. Billets : vrais billets verts plats avec un liseré, qui flottent avec un léger balancement.
- Étiquettes (prix, MAX, textes) lisibles : contour sombre, taille cohérente.

**5. Captures d'écran : obligatoire**
Le cloud juge le rendu sur images. Après chaque grande partie (ambiance, atelier, personnage, finitions) :
- prendre des captures de la vue **Game** en 1080×1920 (portrait) : vue de départ, vue près du poste, vue de la dalle, personnage de près ;
- les enregistrer en PNG dans `unity/captures/` (noms datés : `4b-atelier-01.png`...), les copier dans le clone `sync`, commiter et pousser ;
- si les captures sont impossibles via MCP, le dire dans Questions (Sébastien en enverra).
Le cloud relit les images et note les corrections dans ce fichier.

**Critères de réussite**
- 0 erreur de compilation et en Play ; le gameplay des étapes 1 à 4 fonctionne comme avant (ramassage, MAX, dépôt, billets, argent, dalle, 2e poste).
- Au lancement, l'écran ressemble à un vrai atelier, pas à un test : couleurs cohérentes, ombres, personnage reconnaissable (casque jaune, gilet orange).
- Les images de `unity/captures/` montrent les 4 vues demandées.
- Aucune baisse de fluidité visible dans l'éditeur ; pas de lumière temps réel supplémentaire inutile (cible : téléphone).

**Hors périmètre :** nouvelles fonctions de jeu, clients, employés, sons, sauvegarde.

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
_(vide — étape 4b)_

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

## Archive : résultats de l'étape 4 (Claude Code local)
**Étape 4 — Dalles de déblocage : compile, testée via MCP en Play.**
- Ajoutés : `UnlockConfig` (price 100, payStep 5, payInterval 0,05), `UnlockPad`, `PopIn` (gonflement 0→1 en 0,3 s avec léger dépassement), `FaceCamera` (étiquette face caméra). `Wallet` : `TrySpend(int)` (seul endroit où l'argent diminue) et champ `startMoney`.
- `PrototypeSetup` : la construction du poste est factorisée (`BuildStation(nom, position, ...)`, `BuildBillPrefab`, `LoadOrCreateStationConfig`). Poste 1 en `(-6, 0.1, 0)`, poste 2 `InstallStation2` en `(-6, 0.1, 9)` désactivé, même `StationConfig`. Dalle jaune 3×3 en `(-6, 0.01, 5)` avec `Assets/Prototype/UnlockConfig.asset` (conservé s'il existe). Constante `StartMoney = 0` en haut de `PrototypeSetup` (mettre 100 pour tester).
- Prix affiché par un `TextMesh` intégré (« $ 100 »), police `LegacyRuntime.ttf`, aucun package.
- Tests MCP : 0 erreur compilation, Build scene et Play. 40 en poche : prix 100 → 60, argent → 0, la dalle reste. +60 puis retour : le paiement reprend à 60 et finit en 0,70 s ; la dalle disparaît, le poste 2 apparaît (échelle 1,0 une fois le gonflement fini). Poste 2 : 3 caisses → 5 billets → ramassés par le même `BillCollector`, argent 0 → 50.
- Les billets du poste 2 tombent à `z = 5,8`, sur l'emplacement de la dalle : sans gêne car la dalle a disparu quand le poste 2 fonctionne.
- Non vérifié visuellement : lisibilité du prix 3D, effet de gonflement, couleur de la dalle.
- Tâche parallèle (dépôt `idle-securite-unity`) : non faite, marquée REPORTÉE.

## Historique
- Étape 0 : scripts de base compilés et validés (WASD, ramassage, MAX).
- Étape 1 : joystick tactile `VirtualJoystick` ajouté ; compilation OK, déplacement OK via `SetMoveInput` ; glissé réel à confirmer par Sébastien.
- Étape 2 : poste d'installation (StationConfig, InstallStation, ProgressBar3D, ObjectPool) compilé et testé via MCP ; rendu visuel à confirmer par Sébastien.
- Étape 2 relue côté cloud : validée. Étape 3 spécifiée.
- Étape 3 : billets à ramasser, Wallet, HUD argent/caisses, ResourcePile sur pool partagé ; compilé et testé via MCP, rendu à confirmer par Sébastien.
- Étape 3 (billets, argent, HUD) relue côté cloud : validée. Étape 4 spécifiée. Build iOS cloud reporté.
- Étape 4 : dalle de déblocage à prix décroissant, 2e poste, Wallet.TrySpend ; compilé et testé via MCP, rendu à confirmer par Sébastien.
- Étape 4 (dalles de déblocage) validée par Sébastien et relue côté cloud. Étape 4b (habillage visuel) spécifiée.
