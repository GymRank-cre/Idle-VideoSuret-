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

## Relecture cloud de l'étape 4b : bonne base, 4c de retouches
Captures regardées (départ, poste, dalle, palette, personnage, mur du fond, vue paysage). Verdict : **le style est là** (palette cohérente, ombres douces, bloom discret). Le technicien se lit tout de suite (casque jaune, gilet orange), l'établi, la palette de cartons et la dalle à bandes sont réussis. Mais le **tout premier écran en portrait n'est pas encore beau** : un grand sol vide avec un petit personnage et les postes coupés sur les bords. Réponses à tes questions :
- **Cadrage portrait : ne pas monter la caméra** (le personnage deviendrait minuscule). On rapproche le jeu de la caméra, voir 4c-1.
- **Capture portrait avec HUD** : faire la capture de la vue Game en 1080×1920 avec le Canvas inclus (par exemple `ScreenCapture.CaptureScreenshot` en Play avec la vue Game réglée sur 1080×1920 via un `GameViewSize` en code, ou passer temporairement le Canvas en `ScreenSpaceCamera` pour le rendu). À toi de choisir la méthode qui marche.
- **Ombre sous les caisses portées** : OK comme tu l'as fait. **Performance** : OK pour l'instant, à mesurer sur téléphone plus tard.

## Tâche en cours : étape 4c — Retouches visuelles et cadrage portrait

Le gameplay ne change pas (règles, vitesses, prix). Seules les positions et le visuel bougent. Même méthode que 4b : captures 1080×1920 dans `unity/captures/` (préfixe `4c-`), avec le HUD cette fois.

1. **Resserrer le jeu pour l'écran portrait.** Variante A : tas/palette en `x = 4,5`, poste 1 en `x = -4,5`, dalle en `(-4,5, 0,01, 4)`, poste 2 en `(-4,5, 0,1, 7,5)`, champ de vision de la caméra 65° (au lieu de 60°). Variante B : même chose avec `CameraFollow` offset `(0, 11, -7)`. Capturer le départ avec A puis B ; garder celle où on voit le joueur **et** au moins la moitié du tas et du poste dès le départ. Déplacer les positions dans une seule zone de constantes en tête de `PrototypeSetup`, pas en dur partout.
2. **Joint noir mur/sol** : une bande noire est visible à la jonction (captures 01 et 07). La supprimer : plinthe collée au sol, de la couleur acier, sans espace ni face noire.
3. **Sol** : dalles plus petites (1 m, contraste plus faible entre les deux gris) pour que le personnage ait une bonne échelle ; ajouter un tapis ou des **lignes de circulation au sol** (jaune/blanc, larges de 0,2 m) du tas au poste, et quelques détails au sol près des murs (cartons, bobines déjà présents : en ajouter 2 autour du centre vide, jamais sur le chemin).
4. **Fond de caméra** : le ciel bleu uni derrière les murs fait « plateau de jeu ». Mettre un fond uni foncé neutre (bleu-gris `#2B3441` environ) et ajouter un **liseré de mur haut** (le mur monte à ~4 m ou une moulure haute) pour fermer la pièce.
5. **Barre de progression** : elle chevauche la tête du joueur au poste. La monter d'environ 0,6 m, la raccourcir (1,2 m) et arrondir/foncer son fond.
6. **Casque** : la petite barre jaune sur le dessus ressemble à un cube oublié. La remplacer par une **arête basse allongée de l'avant vers l'arrière** (ou la supprimer). Ajouter un **petit sourire** (arc sombre) sur le visage.
7. **Capture HUD portrait** (voir ci-dessus) : une capture du départ avec `$ 0` et `0/8` visibles et lisibles.

**Critères de réussite :** 0 erreur ; gameplay étapes 1 à 4 inchangé en Play ; sur la capture de départ en portrait, le joueur est reconnaissable et au moins deux éléments de jeu (tas, poste ou dalle) sont visibles ; plus de bande noire ; HUD lisible.

---

## Tâche suivante : étape 4d — Vrai personnage humain (Mixamo)

**Décision de Sébastien :** « je veux un vrai humain ». Le technicien en formes simples est remplacé par un **vrai modèle humain rigué avec de vraies animations**. Faire d'abord 4c, puis 4d.

**Prérequis (par Sébastien, sur son PC, avec un compte Adobe gratuit sur mixamo.com).** Il dépose les fichiers FBX dans `E:\PROJET 1\IdleSecurite\Assets\Characters\` :
- `Technicien.fbx` : un personnage masculin réaliste, pose T, format FBX Binary, avec la peau (skin).
- `Idle.fbx` : animation « Breathing Idle » (ou équivalent), FBX Binary, **Without Skin**.
- `Walk.fbx` : « Walking », FBX Binary, Without Skin, **In Place** coché.
- `Run.fbx` : « Running » (ou « Jog Forward »), Without Skin, **In Place** coché.
Si ces fichiers n'existent pas encore quand tu arrives ici, note-le dans Questions et continue autre chose : ne rien inventer.

**À faire (Claude Code local)**
1. Importer : rig `Humanoid` pour les 4 fichiers (Avatar copié depuis `Technicien.fbx` pour les animations), boucle activée sur Idle/Walk/Run, matériaux du personnage convertis en URP/Lit (corriger le rose éventuel), échelle pour que le personnage mesure environ 1,7 m dans la scène.
2. `AnimatorController` : états Idle, Walk, Run, avec un paramètre `Speed` (0 à 1) piloté par un petit script `HumanAnimator` à partir de l'entrée de `PlayerController`. Ne modifier le déplacement ni les règles : seulement lire la vitesse (ajouter une propriété publique en lecture seule si besoin). Transitions courtes (0,1 s).
3. **Équipement du technicien** (le métier doit se voir) : casque de chantier jaune attaché à l'os de la tête (formes simples, arête basse), gilet orange haute visibilité avec 2 bandes réfléchissantes sur le torse (coque fine attachée à l'os du torse), cela même si le modèle a déjà des vêtements. Ceinture d'outils facultative.
4. Remplacer le personnage en formes simples (désactiver `CharacterAnimator` et le visuel de `WorkshopBuilder`, sans supprimer le code). `CharacterController`, `StackCarrier` et `StackAnchor` ne changent pas : vérifier que la pile de caisses reste bien dans le dos du nouveau personnage (ajuster le point d'accroche seulement).
5. Captures 1080×1920 dans `unity/captures/` (préfixe `4d-`) : de face, de dos avec 8 caisses, en marche (si possible 2 images à 0,2 s d'écart), au poste.

**Critères de réussite :** 0 erreur ; personnage humain reconnaissable (proportions réalistes), animé à l'arrêt et en marche (pas de glissement des pieds visible), casque et gilet visibles ; gameplay étapes 1 à 4 inchangé ; la pile reste dans le dos.

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
**Étape 4c — Retouches visuelles et cadrage portrait : compile, gameplay inchangé, 8 captures `4c-*` (1080×1920, HUD inclus).**
1. **Cadrage portrait.** Variantes A et B capturées. **Variante A retenue** (`4c-01-depart-hud`) : on voit le joueur, le poste et la dalle à gauche, plus de la moitié de la palette à droite. La B (`4c-00-comparaison-variante-B`, caméra `(0, 11, -7)`) grossit un peu le joueur mais coupe davantage le poste et la palette. Constantes en tête de `PrototypeSetup` : `PilePosition (4,5; 0,1; 0)`, `Station1Position (-4,5; 0,1; 0)`, `Station2Position (-4,5; 0,1; 7,5)`, `PadPosition (-4,5; 0,01; 4)`, `CameraOffset (0, 12, -8)`, `CameraFov 65`. `CameraFollow` n'est pas modifié (l'offset est seulement réglé par le Setup).
2. **Joint noir** : supprimé. Plinthe acier clair (0,18 m) collée au sol et au mur, sans espace ; capture `4c-04-joint-mur-sol`.
3. **Sol** : dalles de 1 m, contraste plus faible. Lignes de circulation de 0,2 m : couloir tas → poste (2 lignes blanches, axe jaune en tirets), puis lignes jaunes poste → dalle → poste 2 ; elles sont calculées à partir des positions des objets. Pièce resserrée (x de -8,5 à 11, z de -9 à 11,5) ; 3 étagères, bobines et cartons le long des bords, plus une bobine et deux cartons autour du centre (hors du chemin).
4. **Fond de caméra** : uni `#2B3441`. Murs de 4 m avec liseré jaune bas et moulure haute (corniche acier) ; la pièce est fermée.
5. **Barre de progression** : montée de 0,6 m (y local 2,8), 1,1 m de long, fond en capsule sombre arrondie, remplissage plus fin ; elle ne touche plus la tête du joueur.
6. **Casque** : le cube est remplacé par une arête basse allongée (sphère écrasée, avant → arrière). Petit sourire de 5 billes sombres, sous les yeux. Le sourire se lit plutôt comme des points à cette échelle.
7. **Capture HUD portrait** : faite en passant le Canvas en `ScreenSpaceCamera` le temps du rendu dans la texture 1080×1920 (script de capture jetable, pas dans le projet). `$ 0` et `0/8` sont lisibles sur `4c-01-depart-hud`.
- Test Play après les changements (nouvelles positions) : 8 caisses ramassées (MAX), dépôt au poste, 10 billets ramassés (argent 100), dalle payée en 60 pas, poste 2 actif à `(-4,5; 0,1; 7,5)`, animation du personnage OK (balancement 40°, retour à 0 à l'arrêt). 0 erreur, 0 warning (hors message du pont MCP).
- Captures : `4c-00-comparaison-variante-B`, `01-depart-hud`, `02-poste-hud`, `03-perso-face`, `04-joint-mur-sol`, `05-perso-dos`, `06-dalle-hud`.

## Questions (à remplir par Claude Code local)
- **Barre de progression au repos** : elle reste visible en noir (pastille vide) quand le poste ne travaille pas. Veux-tu qu'elle n'apparaisse que pendant un travail ? (petite modification de `ProgressBar3D`, visuel seulement)
- **HUD** : le texte `$ 0` / `0/8` chevauche le haut du mur sur le cadrage actuel ; lisible grâce au contour, mais on peut le descendre un peu ou l'assombrir si tu préfères.
- **Étape 4d** : les 4 FBX Mixamo sont déjà dans `Assets/Characters` (`Technicien.fbx`, `Breathing Idle.fbx`, `Walking.fbx`, `Running.fbx`). Je ne les ai pas touchés, en attente de ton feu vert pour la 4d.
- Tâche parallèle (dépôt iOS) : non faite, toujours REPORTÉE.


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

## Archive : résultats de l'étape 4b (Claude Code local)
**Étape 4b — Habillage visuel : compile, gameplay intact, 9 captures dans `unity/captures/`.**
- Nouveau menu `Tools > Idle Prototype > Build workshop` (`Editor/WorkshopBuilder.cs`), appelé aussi à la fin de `Build scene`. Relançable (testé 2 fois) : il refait le dossier `Workshop` et l'habillage, sans toucher aux scripts de jeu.
- Aucun asset externe, aucun package. Matériaux `WS_*` dans `Assets/Prototype/Materials`, 3 textures générées par code dans `Assets/Prototype/Textures` (damier, bandes de danger, dégradé radial pour ombres et halo).
- Ambiance URP : lumière directionnelle chaude (ombres douces), ambiance plate chaude, ciel uni, pas de fog. Volume global `Assets/Prototype/PostFX.asset` : Tonemapping ACES, Bloom léger (seuil 1, intensité 0,25), Vignette 0,22, Contraste +10, Saturation +10. Une seule lumière temps réel. Décor fixe en `BatchingStatic`.
- Atelier : sol en damier de dalles de 2 m (le plan `Ground` garde son collider), murs du fond et de gauche avec plinthe, bande acier et liseré jaune, aucun mur côté caméra. Décor : fenêtre, enseigne néon « SÉCURITÉ » (TextMesh + cadre émissif), tableau d'outils, mur de 6 caméras dôme, armoire électrique avec voyants, poster, 3 étagères avec cartons, bobines de câble, cartons isolés.
- Palette de ramassage : palette en bois + 5 cartons à ruban (le tas gris est masqué, son trigger est gardé). Postes : établi (plateau bois, pieds métal, étagère basse) avec écran, caméra dôme en assemblage, vis, tournevis, tapis de travail ; l'établi est au fond du poste pour que les caisses arrivent devant. Dalle : texture à bandes noires en biais + halo jaune doux. Les positions de jeu n'ont pas changé.
- Technicien : gilet orange avec 2 bandes réfléchissantes, casque jaune, tête, yeux, cou, bras, jambes et bottes noires, ceinture d'outils, ombre ronde. La capsule d'origine est masquée (renderer désactivé), `CharacterController`, `StackCarrier` et `StackAnchor` inchangés. `CharacterAnimator` (lit `PlayerController.IsMoving`) : balancement des jambes et bras (40°), rebond 0,07 m, inclinaison 10°, souffle à l'arrêt. Mesuré en Play : valeurs atteintes à la marche, retour à 0 à l'arrêt.
- Détails : caisse en carton avec ruban, billet vert avec liseré et sceau qui flotte (`Bobbing`, vertical seulement car le parent est écrasé), ombre ronde sous le joueur, la palette et l'établi, prix de la dalle avec contour sombre (`TextMirror`, taille réduite à 0,075).
- Gameplay étapes 1 à 4 vérifié en Play après l'habillage : 8 caisses ramassées (MAX), dépôt au poste, 10 billets ramassés (argent 100), dalle payée en 60 pas, poste 2 actif à l'échelle 1. 0 erreur, 0 warning dans la Console.
- Captures (1080×1920, caméra du jeu rendue dans une texture) : `4b-atelier-01-depart`, `02-poste` (barre mi-chemin), `02b-billets`, `03-dalle` (prix visible), `04-palette`, `05-perso-dos` (8 caisses dans le dos), `06-perso-face`, `07-mur-fond`, et `09-hud-paysage` (vue Game de l'éditeur, avec le HUD).

Questions posées à l'époque (répondues plus haut) :
- **Cadrage portrait** : en 1080×1920, la caméra (0, 12, -8) voit seulement environ ±5 m en largeur. La palette (x = 6) et les postes (x = -6) sont sur les bords de l'écran quand le joueur est au centre. Je n'ai pas touché `CameraFollow` (gameplay). À décider : monter un peu la caméra ou élargir le champ de vision ?
- **HUD en portrait** : la capture caméra n'inclut pas le Canvas (overlay). Le HUD est montré dans `09-hud-paysage` (vue Game de l'éditeur en paysage). Une capture portrait avec HUD demande de régler la vue Game en 1080×1920 à la main.
- **Ombre sous les caisses** : faite sous la palette et l'établi, pas sous chaque caisse portée (les caisses de la pile sont déjà dans l'ombre du joueur, et une ombre par caisse se superposerait).
- **Performance** : non mesurée sur téléphone. Côté éditeur : 1 lumière temps réel, décor fixe batché, pas de lumière ajoutée. Les ombres douces dépendent de l'asset URP du projet.
- Tâche parallèle (dépôt iOS) : non faite, toujours REPORTÉE.

## Historique
- Étape 0 : scripts de base compilés et validés (WASD, ramassage, MAX).
- Étape 1 : joystick tactile `VirtualJoystick` ajouté ; compilation OK, déplacement OK via `SetMoveInput` ; glissé réel à confirmer par Sébastien.
- Étape 2 : poste d'installation (StationConfig, InstallStation, ProgressBar3D, ObjectPool) compilé et testé via MCP ; rendu visuel à confirmer par Sébastien.
- Étape 2 relue côté cloud : validée. Étape 3 spécifiée.
- Étape 3 : billets à ramasser, Wallet, HUD argent/caisses, ResourcePile sur pool partagé ; compilé et testé via MCP, rendu à confirmer par Sébastien.
- Étape 3 (billets, argent, HUD) relue côté cloud : validée. Étape 4 spécifiée. Build iOS cloud reporté.
- Étape 4 : dalle de déblocage à prix décroissant, 2e poste, Wallet.TrySpend ; compilé et testé via MCP, rendu à confirmer par Sébastien.
- Étape 4 (dalles de déblocage) validée par Sébastien et relue côté cloud. Étape 4b (habillage visuel) spécifiée.
- Étape 4b : habillage visuel (Build workshop, technicien animé, atelier, post-traitement) fait ; 9 captures dans unity/captures ; à relire côté cloud.
- Étape 4b (habillage) relue côté cloud sur captures : bonne base. Étape 4c (retouches et cadrage portrait) spécifiée.
- Sébastien valide la 4c et demande un vrai humain : étape 4d (Mixamo) ajoutée.
- Étape 4c : cadrage portrait (variante A), plinthe, sol 1 m + lignes, fond #2B3441, barre de progression, casque et sourire, capture HUD ; testé via MCP.
