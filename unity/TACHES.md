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

## Relectures cloud (étapes 3 à 4c) : toutes validées
Étape 3 (billets, argent, HUD), 4 (dalles de déblocage), 4b (habillage), 4c (cadrage portrait, sol, murs) : validées sur code et captures. Les spécifications de ces étapes sont dans l'historique Git de ce fichier.

## Relecture cloud de l'étape 4d : validée (très gros progrès)
Le technicien humain (barbu, casque jaune, gilet orange, ceinture d'outils) se lit très bien, l'animation est bonne, les HUD en pastilles sont propres et la barre est cachée au repos. Réponses à tes questions :
- **Course qui glisse** : d'abord baisser la vitesse du joueur à 4,2 m/s (`PlayerController`, valeur du `[SerializeField]`, sans changer le code) et recaler les vitesses de lecture ; si ça glisse encore, tu passes en Questions et Sébastien téléchargera « Jog Forward ». Ne pas bloquer là-dessus.
- **Textures** : pas besoin pour l'instant. **Visage** : yeux et sourcils acceptés. **FBX de 65 Mo** : noté pour la sauvegarde Git (utiliser Git LFS ou garder hors dépôt).

## Relecture cloud de l'étape 4e : validée avec 3 défauts à corriger
Bien : les bandes suivent le gilet, le prix de la dalle est droit, le « +100 » flottant est lisible, le pop du compteur marche, 0 erreur. Réponses à tes questions :
- **Pile pleine** : compromis (ni a, ni b, ni c) : caisses ×0,6 et pas de 0,17. La pile pleine doit monter à environ 2,3 m (≈0,6 m au-dessus de la tête). Réglages dans les constantes de `PrototypeSetup`.
- **Course qui glisse** : Sébastien va télécharger « Jog Forward » (Without Skin, In Place) dans `Assets/Characters/` ; tu le branches quand il est là, sinon on laisse tel quel. Ne pas bloquer.
- **Chiffre flottant fusionné** : garder « +100 » tel quel, c'est lisible.
Défauts vus sur les captures :
1. **Le panneau MAX est une dalle rouge sans texte**, très haute au-dessus de la pile et penchée (`4e-02-pile-trois-quarts`). Il doit afficher le mot « MAX » (TextMesh blanc à contour, sur pastille rouge arrondie), face caméra, juste au-dessus de la dernière caisse (sommet de la pile + 0,4 m), pas à hauteur fixe.
2. **Le vide en bas de l'écran** : sur `4e-05-plus10-jeu-hud`, le bas de l'image est noir quand le joueur est près du bord sud (la pièce s'arrête et on voit en dehors). La caméra ne doit jamais voir le vide : prolonger le sol et les murs (ou un mur bas au sud) pour couvrir tout ce que la caméra peut voir quand le joueur est dans la zone jouable, et poser des limites invisibles à la zone jouable.
3. **Les bandes de la ceinture/gilet vues de dos** sont un peu irrégulières (morceaux décalés, `4e-01b-bandes-dos`) : acceptable à la distance du jeu, à lisser si c'est rapide, sinon laisser.

## Tâche en cours : étape 4f (corrections ci-dessus) puis étape 5a — Produits finis et plateau

**4f :** faire les 3 corrections ci-dessus + le réglage de la pile. Captures `4f-`.

**5a — Le poste fabrique de vrais produits (avant les clients).** Objectif : la boucle devient *caisses → poste → produit fini à livrer*. Les billets viendront des clients à l'étape 5b. Pour l'instant, **ajouter** les produits sans retirer les billets (le jeu reste jouable) : un champ de config `produceProducts` dans `StationConfig` (défaut vrai) ; les billets sortent encore quand il est faux.
1. `ProductType` (ScriptableObject) : nom, forme/couleur, valeur. Trois produits en formes simples : **caméra dôme** (cylindre blanc + demi-sphère sombre), **badgeuse** (boîte noire avec voyant vert), **alarme** (boîte rouge avec sirène jaune).
2. `StationConfig` : `productsPerJob` (défaut 1), liste des produits fabriqués (en rotation). Valeurs dans les ScriptableObjects, rien en dur.
3. `InstallStation` : à la fin d'un travail, un produit apparaît sur un **plateau de sortie** devant le poste (pool d'objets, jusqu'à `outputCapacity` = 4).
4. `ProductCarrier` (sur le joueur) : ramassage automatique des produits du plateau par proximité (comme les billets), portés **à la main, devant le torse** (petit plateau empilé, capacité 4), distinct de la pile de caisses dans le dos. Compteur `Produits x/4` dans le HUD sous le compteur de caisses (même style de pastille).
5. Les produits portés ne retournent nulle part pour l'instant (la livraison arrive en 5b) : prévoir une action de test `Tools > Idle Prototype > Vider les produits`.
6. Captures `5a-` : un produit sur le plateau, joueur portant 4 produits, HUD.

**Critères de réussite :** 0 erreur ; gameplay étapes 1 à 4 inchangé tant que `produceProducts` est faux ; avec `produceProducts` vrai, 3 caisses déposées → après la durée du travail, un produit sur le plateau, ramassé par le joueur, visible dans ses mains et dans le HUD.
**Hors périmètre :** clients, comptoir, paiement (5b), employés (6).

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
**Étapes 4f et 5a : compilent, 0 erreur en Play, gameplay des étapes 1 à 4 inchangé. Captures `4f-*` et `5a-*` (1080×1920).**

### 4f — corrections
- **Pile** : caisses ×0,6 (`CrateScale`), pas 0,17 (`StackItemHeight`), point d'accroche collé au dos (`z = -0,3`). **Mesuré : sommet de la pile pleine à 2,16 m, soit 0,46 m au-dessus de la tête (1,7 m)** ; la cible était ≈ 2,3 m, donc un peu plus bas, ce qui convient.
- **Panneau MAX** : vraie pastille rouge arrondie avec le mot « MAX » en blanc à contour sombre, face caméra (`FaceCamera`, rotation = caméra), placée par `StackCarrier` à **sommet de la dernière caisse + 0,4 m** (mesuré : écart 0,40 m), plus de hauteur fixe. Texte ajusté pour tenir dans la pastille.
- **Plus de vide** : la pièce couvre tout ce que la caméra voit (sol jusqu'à `z = -14`, mur de droite, mur bas de 0,9 m au sud avec liseré jaune) ; l'écran de bord sud, de coin sud-est et de coin nord-ouest montre toujours du sol (`4f-02*`). **Limites invisibles** de la zone jouable : `x` de -7,2 à 8,2, `z` de -5 à 10,8 (4 BoxColliders `PlayBounds`) ; mesuré en poussant le joueur : `z` min atteint -4,42, `x` min -6,62.
- **Bandes de gilet vues de dos** : lissées (moyenne sur 3 secteurs, 2 passes), voir `4f-03-bandes-dos`.
- **Jog Forward** : absent de `Assets/Characters/`, la course reste telle quelle comme demandé.

### 5a — produits finis et plateau
- **Scripts** : `ProductType` (ScriptableObject : nom, prefab, valeur, couleur), `Product` (collider trigger, pool, libère sa place du plateau quand il est pris), `ProductCarrier` (sur le joueur), `ProductHud`. `StationConfig` : `produceProducts` (défaut vrai), `productsPerJob` (1), `products` (liste, en rotation), `outputCapacity` (4). `InstallStation` : champ `outputTray` ; à la fin d'un travail, après les billets, un produit apparaît sur la première place libre du plateau (un pool par type de produit).
- **3 produits en formes simples** (`Assets/Prototype/Products`, assets conservés s'ils existent) : caméra dôme (disque blanc + demi-sphère sombre + objectif, valeur 25), badgeuse (boîte noire, écran bleu, voyant vert, valeur 40), alarme (boîte rouge + sirène jaune, valeur 60).
- **Plateau de sortie** : plateau acier à 4 places marquées, liseré jaune, sur le côté ouest de chaque poste (`x` local -2,1), donc le produit se ramasse en se plaçant près de lui ; le poste 2 en a un aussi.
- **Portage à la main** : ramassage automatique par proximité (rayon 1,4 m, vol de 0,2 s), produits empilés sur un petit plateau devant le torse (`CarryAnchor`, capacité 4, écart 0,22), distinct de la pile de caisses dans le dos ; le petit plateau n'est visible que si on porte quelque chose. Pastille de HUD `Produits x/4` sous celle des caisses (icône de dôme, pop à chaque ajout).
- **Action de test** : `Tools > Idle Prototype > Vider les produits` (en Play) rend les produits portés à leur pool.
- **Tests Play (mesurés)** : 3 caisses déposées → après le travail 1 produit sur le plateau + 5 billets ; 4 travaux → 4 produits (plateau plein) ; un 5e travail → toujours 4 produits (le 5e est perdu) et 5 billets quand même ; en se plaçant près du plateau → porté 4/4, plateau vide ; avec les mains pleines un 6e produit reste sur le plateau ; `Vider les produits` → 0 porté (puis le produit du plateau est ramassé → 1) ; `produceProducts` faux → 0 produit et 5 billets comme avant (remis à vrai ensuite).
- **Décision** : avec `produceProducts` vrai, les **billets sortent toujours aussi** (« ajouter sans retirer »). Les produits portés ne retournent nulle part avant 5b.
- Captures : `4f-01-pile-max-trois-quarts`, `4f-01c-pile-max-jeu-hud`, `4f-02-bord-sud-jeu-hud`, `4f-02b-coin-sud-est-jeu-hud`, `4f-03-bandes-dos` ; `5a-01-produit-sur-plateau`, `5a-02-plateau-plein`, `5a-03-porte-4-produits-face`, `5a-03b-porte-4-produits-profil`, `5a-04-hud-produits`.

## Questions (à remplir par Claude Code local)
- **Produits portés** : empilés sur 4 niveaux ils montent jusqu'au visage (`5a-03`). Veux-tu les poser en 2 × 2 sur le plateau (moins haut) ou garder la pile ?
- **Plateau plein** : un produit fabriqué quand le plateau est plein est perdu. Préfères-tu que le travail attende qu'une place se libère ?
- **Billets et produits** : les deux sortent pour l'instant. À l'étape 5b, on retire les billets du poste (l'argent viendra des clients) : confirmes-tu ?
- **Course** : toujours le clip « Running » (glissement léger), en attente de « Jog Forward ».
- **Captures anciennes** : `4f-02` et `4f-02b` ont été prises avant l'ajustement final du texte MAX (le texte débordait de la pastille) ; `4f-01*` montrent la version finale.
- Tâche parallèle (dépôt iOS) : toujours REPORTÉE.


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

## Archive : résultats de l'étape 4c (Claude Code local)
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

Questions posées à l'époque (répondues plus haut) :
- **Barre de progression au repos** : elle reste visible en noir (pastille vide) quand le poste ne travaille pas. Veux-tu qu'elle n'apparaisse que pendant un travail ? (petite modification de `ProgressBar3D`, visuel seulement)
- **HUD** : le texte `$ 0` / `0/8` chevauche le haut du mur sur le cadrage actuel ; lisible grâce au contour, mais on peut le descendre un peu ou l'assombrir si tu préfères.
- **Étape 4d** : les 4 FBX Mixamo sont déjà dans `Assets/Characters` (`Technicien.fbx`, `Breathing Idle.fbx`, `Walking.fbx`, `Running.fbx`). Je ne les ai pas touchés, en attente de ton feu vert pour la 4d.
- Tâche parallèle (dépôt iOS) : non faite, toujours REPORTÉE.

## Archive : résultats des retouches HUD et de l'étape 4d (Claude Code local)
**Retouches HUD + barre, puis étape 4d — Vrai personnage humain (Mixamo) : compile, gameplay inchangé, 9 captures `4d-*` (1080×1920).**

*Retouches demandées avant la 4d*
- **Barre de progression** : cachée tant que la progression est à 0 (`ProgressBar3D` masque ses enfants), visible seulement pendant un travail. Vérifié en Play (enfants inactifs au repos).
- **HUD en pastilles** : conteneur `HudSafeArea` (script `SafeAreaFitter`, zone sûre de l'écran pour l'encoche) avec marge haute de 60 px ; pastille arrondie semi-transparente sombre en haut à gauche (icône de billet vert + nombre, sans « $ ») et en haut à droite (icône de caisse + `0/8`, la capacité en plus petit et plus discret, via texte riche). Texte UI classique, `UISprite`/`Knob` intégrés, aucun package.

*4d*
- **Import** (`Editor/HumanCharacterBuilder.cs`, menu `Tools > Idle Prototype > Setup human character`, appelé aussi par `Build scene`) : les 4 FBX en rig `Humanoid`, Avatar créé sur `Technicien.fbx` et copié sur les 3 animations, boucle activée (`loopTime`, `loopPose`), position/rotation de la racine figées (« In Place »). Clips renommés Idle (9,93 s), Walk (1,03 s), Run (0,70 s). Le FBX n'a pas de texture (matériaux blancs, pas de rose) : un matériau URP/Lit uni par pièce (peau, cheveux, bottes, pantalon, chemise, gilet orange, casque jaune) dans `Assets/Characters/Materials`. Échelle réglée par le Setup pour 1,7 m.
- **Le modèle contient déjà un casque et un gilet** (maillages `Ch17_Helmet`, `Ch17_Vest`) : je les ai colorés (jaune, orange). Ajouts en formes simples attachés aux os : 2 bandes réfléchissantes sur le torse (os `Chest`, calées sur les vrais sommets du gilet), arête basse sur le casque (os `Head`), ceinture d'outils avec 2 pochettes et un outil (os `Hips`), yeux et sourcils (os `Head`). Le visage du modèle est celui d'un homme barbu ; le sourire n'est pas visible sous la barbe.
- **Animator** `Assets/Characters/Technicien.controller` : états Idle, Walk, Run, paramètres `Speed` (0 à 1) et `AnimSpeed`, transitions de 0,1 s (Idle→Walk 0,1 ; Walk→Run 0,5 ; retours 0,06 et 0,4). `HumanAnimator` lit `PlayerController.MoveAmount` et `PlayerController.PlanarSpeed`, deux propriétés **en lecture seule ajoutées** à `PlayerController` (le déplacement n'a pas changé).
- **Remplacement** : `WorkshopBuilder.UseHumanCharacter = true` ; l'ancien technicien en formes simples et `CharacterAnimator` sont gardés dans le code mais ne sont plus construits (si les FBX manquent, le Setup retombe sur l'ancien). `CharacterController` et `StackCarrier` inchangés ; **seul le point d'accroche** `StackAnchor` passe de `-0,7` à `-0,55` en z (pile collée au dos). Le panneau MAX passe de 2,4 à 3,25 m de haut : il traversait la caisse du haut.
- **Tests Play** (0 erreur, 0 warning hors message du pont MCP) : états Idle → Walk (entrée 0,3) → Run (entrée 1) → Idle vérifiés ; ramassage 8/8 MAX, dépôt, 10 billets, argent 100 (HUD 100), dalle payée en 60 pas, poste 2 actif.
- **Glissement des pieds, mesuré** (vitesse monde de l'orteil, personnage en mouvement) : **marche** 0,13 m/s en moyenne sur les 25 % d'images les plus lentes (pied planté, pas de glissement) ; **course** : ~3 m/s, la course glisse encore un peu. J'ai calibré la vitesse de lecture sur le recul réel du pied d'appui (`walkClipSpeed` 1,4 m/s, `runClipSpeed` 3,4 m/s) ; aucune valeur de 2 à 5 ne fait mieux, donc le clip de course ne colle pas parfaitement à 5 m/s. Sur les captures de course (`4d-03-marche-a/b`) le mouvement est fluide.
- **Captures** : `4d-00-depart-hud`, `01-face`, `01b-visage-gros-plan`, `02-dos-8-caisses` (trois-quarts arrière), `02b-profil-8-caisses`, `03-marche-a` et `03-marche-b` (course, 0,2 s d'écart), `03-marche-lente`, `04-poste-hud`.

Questions posées à l'époque (répondues plus haut) :
- **Course** : le glissement résiduel du clip « Running » à 5 m/s. Veux-tu que je baisse la vitesse du joueur à la course, ou qu'on essaie un autre clip Mixamo (« Jog Forward ») ? Je n'ai pas touché au déplacement.
- **Textures du modèle** : le FBX n'a pas de texture, donc peau, barbe et vêtements sont des couleurs unies. Si tu veux plus de détail (texture de visage, de chemise), il faut re-télécharger le personnage depuis Mixamo avec les textures (`Technicien.fbx` « With Skin » et textures incluses).
- **Visage** : j'ai ajouté de petits yeux et sourcils car le modèle n'en a pas sans texture ; à juger sur `4d-01b-visage-gros-plan`.
- **Poids du FBX** : `Technicien.fbx` fait 65 Mo ; à garder hors du dépôt `sync` (seuls les scripts y sont copiés) mais à prévoir si on sauvegarde tout le projet sur GitHub.
- Tâche parallèle (dépôt iOS) : toujours REPORTÉE, non faite.

## Archive : résultats de l'étape 4e (Claude Code local)
**Étape 4e — Petites retouches et retour d'information : compile, gameplay inchangé, 9 captures `4e-*` (1080×1920).**
1. **Bandes réfléchissantes** : ce sont maintenant de vrais anneaux de 4 cm de haut qui suivent la surface du gilet (+2 mm), construits sur les sommets réels du gilet à chaque hauteur (72 secteurs). Les petits trous sont comblés, donc elles sont fermées dans le dos ; elles restent ouvertes là où le gilet est ouvert devant. Maillages sauvés en assets (`Assets/Characters/Meshes`), matériau visible des deux côtés. Captures `4e-01*`.
2. **Échelle de la pile** : caisses ×0,75 (constante `CrateScale` dans `PrototypeSetup`), `itemHeight` 0,26 (`StackItemHeight`), point d'accroche collé au dos (`z = -0,38`, hauteur de hanches). Balancement inchangé. **Mesuré : le sommet de la pile pleine est à 2,81 m, soit 1,1 m au-dessus de la tête (1,7 m)** : avec ces valeurs 8 caisses ne peuvent pas « dépasser la tête de peu » (il faudrait des caisses de 0,18 m et un pas de 0,19 m, ou une pile sur deux colonnes). Le panneau MAX est remonté juste au-dessus. Voir Questions.
3. **Prix de la dalle** : le texte est un vrai billboard (`FaceCamera`, rotation = caméra, sans inclinaison) mis à jour juste avant le rendu de chaque caméra de jeu (`beginCameraRendering`), donc aussi quand la caméra est déplacée à la main. La capture `4e-03-prix-dalle-oblique` montre « $ 100 » droit en vue oblique ; l'inclinaison de `4d-02` venait d'une caméra déplacée après la mise à jour du billboard, pas du jeu.
4. **Vitesse du joueur** : 4,2 m/s, réglée par la **valeur sérialisée** `moveSpeed` posée dans `PrototypeSetup` (`PlayerMoveSpeed`) ; `PlayerController` n'a pas changé. `runClipSpeed` passe à 4,2 (l'animation de course tourne à sa vitesse d'origine). Mesure (orteil, moyenne des 25 % d'images les plus lentes) : marche 0,11 m/s (planté) ; **course 2,7 à 2,9 m/s : ça glisse encore**, un peu moins qu'à 5 m/s (3,1). Voir Questions.
5. **Retour d'information** (formes simples, sans son ni package, avec pools) :
   - `FeedbackConfig` (ScriptableObject `Assets/Prototype/FeedbackConfig.asset`, conservé s'il existe) : durée et hauteur du chiffre, couleurs, nombre/durée/vitesse/taille des éclats, échelle et retour du pop.
   - (a) **Chiffre flottant** « +valeur » : `FloatingText` + `FeedbackSpawner` (pool). Les billets ramassés à moins de 0,25 s d'écart sont **additionnés dans un seul chiffre** (« +100 » pour 10 billets d'un coup) : sinon dix « +10 » se superposaient en une tache illisible (voir `4e-05b`). Un billet ramassé seul affiche « +10 ».
   - (b) **Éclats de particules** (système intégré, émission par `Emit`, pool) : à l'arrivée de chaque caisse au poste (`InstallStation`) et à chaque billet ramassé (`BillCollector`). Les deux scripts ont un champ `feedback` facultatif.
   - (c) **Pop du compteur de caisses** (`CrateHud`) : mesuré à 1,13 juste après un ramassage, retour à 1,0.
   - Mesures en Play : 1 éclat actif 20 images après le début du dépôt ; 1 chiffre « +100 » après le ramassage des 10 billets ; 0 chiffre actif 1 s plus tard.
- **Non-régression (Play)** : 8 caisses ramassées (MAX), dépôt, 10 billets, argent 100, dalle payée en 60 pas, poste 2 actif ; 0 erreur et 0 warning dans la Console (hors message du pont MCP).
- Captures : `4e-01-bandes-face`, `01b-bandes-dos`, `01c-bandes-trois-quarts`, `02-pile-trois-quarts`, `02b-pile-profil`, `03-prix-dalle-oblique`, `04-eclat-caisse-poste`, `05-plus10-jeu-hud`, `05b-plus10-gros-plan`.

Questions posées à l'époque (répondues plus haut) :
- **Pile pleine trop haute** (point 2) : avec ×0,75 et un pas de 0,26, 8 caisses font 2,81 m (tête à 1,7 m). Pour que la pile ne dépasse que de peu, veux-tu : (a) des caisses encore plus petites (×0,5) avec un pas de 0,13, (b) une pile sur 2 colonnes de 4 caisses côte à côte dans le dos, ou (c) garder 8 caisses et accepter la hauteur ?
- **Course qui glisse encore** (point 4) : même à 4,2 m/s le clip « Running » glisse (2,7 m/s mesurés sur le pied d'appui). Comme prévu, Sébastien peut télécharger « Jog Forward » (Without Skin, In Place) dans `Assets/Characters/` ; je le brancherai ensuite.
- **Chiffre flottant fusionné** : est-ce que tu préfères un « +10 » par billet (étalés dans le temps) ? Cela demanderait d'échelonner l'arrivée des billets au joueur.
- Tâche parallèle (dépôt iOS) : toujours REPORTÉE.

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
- Étape 4c relue sur captures : validée. Retouches HUD et barre cachée demandées ; 4d (vrai humain) lancée.
- Étape 4d : technicien humain Mixamo (Humanoid, Idle/Walk/Run, équipement), barre cachée au repos, HUD en pastilles ; testé via MCP, 9 captures 4d-*.
- Étape 4d (vrai humain Mixamo) relue sur captures : validée. Étape 4e (retouches, échelle de la pile, juice de base) spécifiée.
- Étape 4e : bandes collées au gilet, pile ×0,75, vitesse 4,2 m/s, prix en vrai billboard, retour d'information (+valeur, éclats, pop) ; testé via MCP, 9 captures 4e-*.
- Étape 4e relue sur captures : validée avec 3 défauts. Étapes 4f (corrections) et 5a (produits finis) spécifiées.
- Étapes 4f et 5a : pile ×0,6, panneau MAX avec texte au sommet de la pile, sol/murs/limites sans vide ; produits finis (3 types), plateau de sortie, portage à la main, HUD Produits ; testé via MCP, captures 4f-* et 5a-*.
