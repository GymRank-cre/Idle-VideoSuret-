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

## Relecture cloud de 4f et 5a : validées
4f : pile pleine à 2,16 m (bon compromis), vrai panneau « MAX » en pastille rouge au-dessus de la pile, plus de vide, limites jouables, très bon. 5a : le plateau à 4 places avec les trois produits (dôme, badgeuse, alarme) est très lisible ; la fabrication, le ramassage et le HUD « Produits » marchent. Réponses :
- **Produits portés** : les poser **en 2 × 2 à plat** sur le petit plateau (un seul niveau), pas empilés (la pile cache le visage, `5a-03`).
- **Plateau plein** : le travail **attend** qu'une place se libère (le poste affiche sa barre pleine, aucun produit perdu).
- **Billets** : confirmé, on les **retire du poste** dès que les clients paient (5b) ; garder le code et `billsPerJob` pour le repli `produceProducts = faux`.
- **Course** : on attend « Jog Forward » de Sébastien, ne pas bloquer.
- Remarque visuelle : la pastille d'argent chevauche l'enseigne néon « SÉCURITÉ » sur `4f-01c` ; décaler l'enseigne vers le centre du mur ou la baisser.

## Relecture cloud de l'étape 5b : validée — la boucle complète fonctionne
Captures regardées : file de clients, bulle « x1 » avec le produit, livraison, billets sur le comptoir, bulle « OK ». Très bon : le jeu a maintenant sa vraie boucle (caisses → produit → client → argent → déblocage). Réponses :
- **Enseigne néon / pastille** : déplacer l'enseigne au milieu du mur du fond (pas de chevauchement avec les pastilles).
- **Premier client** : oui, **forcer une caméra dôme** pour le tout premier client (le joueur apprend), puis hasard.
- **Cheveux** : pas acceptable tel quel (voir défauts).
Défauts vus sur les captures `5b-01b`, `5b-03` :
1. **Clients chauves avec des morceaux de cheveux déchiquetés** autour du cou, et un **col de chemise déchiré** (triangles manquants) : le modèle perd des morceaux quand on enlève le gilet/casque. Cacher le maillage de cheveux du modèle et poser à la place **un bonnet de cheveux en formes simples** (calotte arrondie, 4 couleurs et 3 formes : court, mi-long, hérissé), et fermer le col (afficher le maillage de chemise complet ou ajouter un col simple). Aucun triangle visible déchiré.
2. **Éclats en carrés** (particules vertes et jaunes) : à remplacer par des **points ronds doux** (la texture radiale générée pour les ombres convient) avec un léger fondu.
3. Le panneau de la **bulle** est très grand en gros plan : réduire de 25 % la taille de la pastille et la maintenir à taille écran constante (même taille à l'écran quelle que soit la distance de la caméra).

## Relecture cloud de 5c et 6a : validées — l'atelier tourne tout seul
5c : les clients ont de vraies coiffures et une chemise propre (plus rien de déchiré), éclats ronds, bulle à taille constante, enseigne au centre. 6a : la dalle « EMBAUCHE $ 150 » et l'employé en gilet bleu qui porte les caisses du tas au poste fonctionnent ; NavMesh sans erreur. Très bon travail (vérification en jeu : 60 s sans toucher au joueur, 4 produits fabriqués). Réponses :
- **Plateau plein / atelier bloqué** : garder 4 places, mais ajouter un 2e employé « livreur » (6b) qui apporte les produits aux clients : c'est la fin de la boucle automatique.
- **Poste 2 et NavMesh** : cuire le NavMesh avec le poste 2 **activé** (obstacles en découpe dynamique si nécessaire), employés prêts à le servir plus tard.
- **Joueur et employé** : ajouter un `NavMeshObstacle` sans découpe sur le joueur pour que les employés l'évitent.
- Détail visuel : une bobine de câble passe sous le titre « EMBAUCHE » (`6a-04`) : la déplacer.

## Relecture cloud de l'étape 6b : validée — bonne avancée, 4 points à corriger
Le guide (flèche jaune 3D + phrase), le livreur au gilet vert, l'évitement, le NavMesh du poste 2 et le test « bot qui joue au joystick » sont très bons. Aucune erreur. Captures regardées : `6b-guide-1-caisses`, `6b-atelier-deux-employes`. Points à corriger :
1. **Trop de dalles jaunes dès la première seconde** : 100, 150 et 250 sont visibles ensemble et noient le guide. Les **faire apparaître l'une après l'autre** : la dalle du poste 2 au départ ; la dalle « EMBAUCHE » seulement une fois le poste 2 payé ; la dalle « LIVREUR » seulement une fois le porteur embauché. Apparition avec `PopIn` et une petite étincelle.
2. **Titre et prix qui se chevauchent** sur « LIVREUR $ 250 » (le titre passe sur le prix) et le titre « EMBAUCHE » est coupé par le bord de l'écran : séparer le titre du prix (titre plus haut, prix plus bas), et vérifier que les dalles et leurs textes restent dans le cadre portrait.
3. **Néon et phrase du guide** : le néon « SÉCURITÉ » est encore sous la phrase. Le déplacer sur **le mur de gauche** (près du coin du fond, tourné vers la pièce), ainsi le mur du fond reste libre pour les pastilles et la phrase.
4. **Production en fonction de la demande** : oui, le poste doit fabriquer d'abord ce que demande le client en tête de file (puis ce qui manque pour les clients suivants, enfin la rotation) en tenant compte des produits déjà sur le plateau, portés ou en cours de fabrication. Un petit objet `ProductDemand` (une seule responsabilité) fournit la liste ; le poste le lit. Pas de blocage possible : si personne n'attend, rotation.

Réponses à tes questions : **ne pas toucher aux prix** (100, 150, 250) ni à `billValue`. À la place, **rendre le début plus nerveux** : l'attente de 10 s par produit est trop longue (le joueur regarde sans rien faire). Essayer `cratesPerJob` = 6 et `jobDuration` = 4 s (la pile pleine de 8 reste utile, le joueur recharge pendant le travail). Cibles : premier billet encaissé en ≤ 25 s, première dalle vers 60 à 75 s, porteur vers 2 min, livreur vers 3 min 30 (au rythme d'un humain, pas d'un bot parfait : mesurer avec un bot qui ajoute des temps d'hésitation réalistes, par exemple 0,5 s à chaque changement de cible).

## Relecture cloud de 6c et 7a : validées — le début est propre et la sauvegarde solide
Le départ n'a plus qu'une dalle visible et la flèche guide bien ; titres/prix séparés ; néon sur le mur de gauche ; production à la demande ; sauvegarde atomique avec copie de secours, fichier abîmé géré, écran « Bon retour ! ». Mesures très utiles (premier billet à 20,8 s, première dalle à 54 s). Réponses :
- **Première dalle à 54 s** : garder ce début nerveux.
- **Gain hors ligne trop généreux** (2 h = 10 700 $ pour des dalles à 100-250) : en attendant les améliorations, passer le rendement à **20 %** et la durée max à **4 h** (valeurs dans `OfflineConfig`). Les gains seront re-calibrés quand l'économie montera (voir 7b).
- **Dalle d'embauche hors cadre** : faire un **petit déplacement de caméra** (1,2 s, aller puis retour) vers la dalle qui vient d'apparaître, puis rendre la main au joueur ; pas de blocage des commandes.
- **Écran « Bon retour ! »** : fonctionnel mais c'est un carré noir brut. Le mettre dans le même style que les pastilles (panneau arrondi semi-transparent, titre jaune, bouton vert arrondi, léger « pop » à l'ouverture).

## Relecture cloud de l'étape 7b : validée — très belle interface
Le panneau « Améliorations » (panneau arrondi, icônes, niveaux, boutons verts), le bouton rond jaune avec point rouge, les multiplicateurs mesurés au niveau maximum, la migration de sauvegarde v1 → v2 et l'écran « Bon retour ! » restylé sont excellents. Remarque : le bouton d'améliorations est visible dès le départ (`7b-01`) et ajoute du bruit à l'écran de départ. Réponses :
- **Bouton des améliorations** : le faire **apparaître seulement après l'embauche du porteur** (avec `PopIn`), pas avant. Pas de grisé à gérer.
- **Plafond d'économie** : oui, ajouter une 6e amélioration « **Capacité des employés** » (+1 caisse portée par le porteur, +1 produit porté par le livreur, par niveau, max 10, coût de base 160). Mettre à jour `OfflineEarnings` en conséquence.
- **Gain hors ligne** : cohérent pour l'instant ; on re-calibrera quand d'autres postes seront débloqués.

## Relecture cloud de l'étape 8a : validée (rigueur des mesures)
Sons générés sans allocation, volumes alignés à −20 dBFS, hauteur progressive des billets, réglages mémorisés, effacement avec confirmation, bouton d'améliorations caché jusqu'à l'embauche, 6e amélioration : tout est conforme. Le jugement à l'oreille revient à Sébastien (menu `Écouter les sons`). Réponses :
- **Musique par défaut** : désactivée tant que Sébastien n'a pas écouté (comme tu l'as fait).
- **Sons des employés** : oui, **plus discrets** : volume à 35 % de celui du joueur, et atténués avec la distance à la caméra (inaudibles au-delà de 12 m) pour ne pas faire un brouhaha avec deux employés.

## Tâche en cours : étape 8b — Préparer un test sur iPhone par le navigateur (WebGL)

**Contexte :** Sébastien a un iPhone, une licence Unity Personal et pas de Mac. Un build cloud iOS n'est pas possible gratuitement. La voie réaliste pour **sentir le jeu sur téléphone** est une version **WebGL** jouable dans Safari, hébergée sur un lien privé. Ne rien publier toi-même : préparer un dossier de build prêt à déployer.
1. **Vérifier le module** : `WebGL Build Support` est-il installé ? Sinon le noter dans Questions avec la marche à suivre pour Sébastien (Unity Hub > Installations > Ajouter des modules) et passer à la suite (points 2 à 4 sont faisables sans le module).
2. **SaveSystem compatible WebGL** : sur WebGL, `File.Replace`/`.tmp` et le dossier persistant sont fragiles (système de fichiers en mémoire). Ajouter un repli : sur `UNITY_WEBGL && !UNITY_EDITOR`, enregistrer le JSON dans `PlayerPrefs` (même `version`, même copie de secours sur une 2e clé) puis `PlayerPrefs.Save()`. Garder le comportement actuel partout ailleurs. Ne rien casser des migrations existantes.
3. **Audio WebGL** : les navigateurs bloquent le son avant un geste de l'utilisateur. Démarrer musique et sons après le premier toucher (écran « Touche pour jouer » très simple, dans le style des panneaux, qui sert aussi de premier geste). Pas de vibration sur WebGL (ignorer proprement).
4. **Réglages de build WebGL** (via un script éditeur `WebGLBuilder`, menu `Tools > Idle Prototype > Build WebGL de test`) : compression **Gzip** (Brotli exige HTTPS spécifique), `Decompression Fallback` activé (hébergement statique simple), pas de strip risqué, taille de mémoire raisonnable pour Safari iOS (≤ 256 Mo), qualité mobile (ombres douces limitées si besoin), `Run in background` désactivé. Sortie dans `E:\PROJET 1\Builds\WebGL` (hors du projet et hors du dépôt).
5. **Poids** : relever la taille totale du build (cible < 60 Mo compressés). Si c'est trop gros, réduire d'abord le modèle humain (`Technicien.fbx` : 65 Mo) : supprimer ce qui n'est pas utilisé (animations inutiles, normales/tangentes, compression des maillages), et dire ce qui a été fait.
6. **Test local** : servir le dossier avec un petit serveur local (`python -m http.server` ou équivalent), charger la page dans un navigateur de bureau et vérifier : chargement, joystick tactile émulé à la souris, sauvegarde conservée au rechargement, pas d'erreur dans la console du navigateur. Si un navigateur sans interface est disponible, le noter ; sinon Sébastien testera.
7. **Ne pas déployer**. Dans Résultats : taille, durée du build, chemin du dossier, commande exacte à lancer pour servir en local, et la procédure proposée pour un hébergement gratuit (Vercel : `vercel` en ligne de commande depuis ce dossier, connexion par Sébastien).
8. Captures : écran « Touche pour jouer », une capture de la page dans le navigateur de bureau en 390×844 (format iPhone).

**Critères de réussite :** 0 erreur en éditeur ; le jeu éditeur est inchangé ; le dossier WebGL se charge en local et se joue ; la sauvegarde WebGL fonctionne ; taille indiquée.

**Après la 8b : S'ARRÊTER.** Ne pas inventer de nouvelle étape : attendre que Sébastien ait joué (PC et iPhone) et que le cloud écrive la suite dans ce fichier. Si tu as terminé, continuer seulement à vérifier la branche toutes les 10 minutes.

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
_(vide — étape 8b)_

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

## Archive : résultats des étapes 4f et 5a (Claude Code local)
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

Questions posées à l'époque (répondues plus haut) :
- **Produits portés** : empilés sur 4 niveaux ils montent jusqu'au visage (`5a-03`). Veux-tu les poser en 2 × 2 sur le plateau (moins haut) ou garder la pile ?
- **Plateau plein** : un produit fabriqué quand le plateau est plein est perdu. Préfères-tu que le travail attende qu'une place se libère ?
- **Billets et produits** : les deux sortent pour l'instant. À l'étape 5b, on retire les billets du poste (l'argent viendra des clients) : confirmes-tu ?
- **Course** : toujours le clip « Running » (glissement léger), en attente de « Jog Forward ».
- **Captures anciennes** : `4f-02` et `4f-02b` ont été prises avant l'ajustement final du texte MAX (le texte débordait de la pastille) ; `4f-01*` montrent la version finale.
- Tâche parallèle (dépôt iOS) : toujours REPORTÉE.

## Archive : résultats de l'étape 5b (Claude Code local)
**Étape 5b (avec 5a-bis) : compile, 0 erreur en Play, boucle complète mesurée. Captures `5b-*` (1080×1920).**

### 5a-bis
- **Produits portés en 2 × 2 à plat** sur un petit plateau devant le torse (un seul niveau, plus de pile qui cache le visage) ; quand un produit est livré, les autres se serrent vers les premières places. Le plateau de portage n'est visible que si on porte quelque chose.
- **Plateau de sortie plein : le travail attend** (barre pleine et visible) qu'une place se libère, aucun produit perdu. Mesuré : 5 travaux → 4 produits sur le plateau et barre pleine ; après ramassage le 5e apparaît.
- **Enseigne néon** descendue de 0,5 m (y = 1,8) pour qu'elle ne passe plus sous la pastille d'argent ; elle apparaît encore derrière la pastille quand le joueur est à droite de la scène (voir Questions).

### 5b — clients, comptoir, paiement
- **Scripts** : `CustomerConfig` (ScriptableObject : intervalle d'arrivée 8 s, 3 clients max, vitesse 1,8, écart de file 1 m, pause de sortie 0,6 s, quantité 1 à 2, délai et durée de livraison, valeur max d'un billet 10), `Customer` (marche par points de passage, pas de NavMesh ; anime le modèle Idle/Walk d'après sa vitesse), `CustomerAppearance` (chemise et cheveux au hasard dans 6 couleurs, matériaux par instance), `OrderBubble`, `CustomerQueue`, `ServiceCounter`, `TrashBin`.
- **Client** : même `Technicien.fbx` et même `Technicien.controller`, sans casque ni gilet, avec visage ; pool de 6 clients (`ObjectPool`). Construit par `CustomerBuilder` (Editor), qui crée aussi `Assets/Prototype/CustomerConfig.asset` (conservé s'il existe).
- **Comptoir** en bois avec caisse enregistreuse (écran, touches, tiroir), au sud de la palette en `(5, 0, -5,4)` ; le centre et les couloirs restent libres (capture `5b-01`). Zone de livraison du côté du joueur.
- **File** : les clients entrent par une **porte percée dans le mur de droite** (cadre jaune, linteau) à `z = -10,9`, longent le couloir d'arrivée (z = -11,5), remontent la file devant le comptoir (places de 1 m vers le sud) ; en sortie ils prennent un autre couloir (z = -10,3) pour ne pas croiser les arrivants. Quand le premier part, les autres avancent d'une place en gardant leurs points de passage.
- **Bulle de commande** sur le client en tête seulement : pastille blanche, petite copie du produit, « x2 » ; se met à jour à chaque livraison, clignote en vert et affiche « OK » à la fin (icônes créées une fois par client, aucune allocation en jeu).
- **Livraison** : dans la zone du comptoir, les produits du bon type volent un par un vers le client (0,25 s). À la commande complète : éclat de joie, **billets sur le comptoir** (le prix `valeur × quantité` découpé en billets d'au plus 10, réutilise `Bill` et son pool), ramassés par le `BillCollector` existant. Le client attend 0,6 s puis part.
- **Corbeille** (à droite du comptoir) : y entrer avec des produits les vide, sans pénalité, retour au pool.
- **Poste** : quand `produceProducts` est vrai, il ne fait plus de billets (l'argent vient des clients) ; `billsPerJob` et le comportement d'avant restent pour `produceProducts` faux.
- **Mesures en Play** : 3 clients arrivés en 36 s, jamais plus de 3 dans la file, **distance minimale entre deux clients 1,00 m**, bulle seulement en tête. Boucle complète : 4 produits fabriqués (3 caisses chacun) → livraisons dôme ×2 (+50), badgeuse (+40), alarme (+60) = **150** → dalle de déblocage payée (100, en 60 pas) → poste 2 actif. Corbeille : 1 produit porté → 0. `produceProducts` faux : 0 produit, 5 billets au poste, comme avant.
- **Bugs trouvés et corrigés pendant le test** : (1) les clients restaient en T-pose, car `HumanCharacterBuilder` supprimait et recréait `Technicien.controller` à chaque appel, ce qui cassait la référence du prefab client ; il n'est plus recréé s'il est valide (corrige aussi le risque pour le technicien). (2) Des billets déposés sur le bord du comptoir étaient hors de portée du joueur à sa position la plus au sud ; le comptoir est rapproché de 0,4 m.
- **Captures** : `5b-01-file-bulle-jeu-hud`, `5b-01b-file-bulle-gros-plan`, `5b-02-livraison-en-vol`, `5b-03-billets-comptoir` (aussi : produits 2 × 2 sur le plateau de portage, bulle « OK »), `5b-03b-client-content`, `5b-04-hud-argent`.

Questions posées à l'époque (répondues plus haut) :
- **Enseigne néon / pastille d'argent** : l'enseigne reste visible derrière la pastille quand le joueur est à droite de la scène (`5b-04`). Veux-tu que je la déplace (par exemple au milieu du mur du fond) ou que je la supprime ?
- **Clients** : leurs cheveux sont un seul maillage du modèle (pas de coupes différentes) ; seules les couleurs changent. Acceptable pour le prototype ?
- **Commande** : le type est tiré au hasard dans la liste des produits du poste (dôme, badgeuse, alarme). Pour guider le joueur au début, veux-tu forcer le premier client à commander une caméra dôme ?
- **Course** : toujours en attente de « Jog Forward ».
- Tâche parallèle (dépôt iOS) : toujours REPORTÉE.

## Archive : résultats des étapes 5c et 6a (Claude Code local)
**Étapes 5c et 6a : compilent, 0 erreur et 0 warning en Play ; gameplay des étapes 1 à 5 inchangé tant que l'employé n'est pas embauché. Captures `5c-*` et `6a-*` (1080×1920).**

### Package installé
- **Aucun package ajouté** : `com.unity.ai.navigation` 2.0.15 (package officiel Unity, AI Navigation) était **déjà dans `manifest.json`** (il vient avec le modèle de projet Unity 6). Rien n'a été téléchargé ni installé pour la 6a.

### 5c — corrections
1. **Clients (cheveux et col)** : cause réelle trouvée au test : le maillage « gilet » du modèle **est son torse** ; sans lui (je l'avais caché) on voit le pantalon à travers la chemise, d'où le col « déchiré ». Il est donc **gardé, coloré comme la chemise** (`CustomerAppearance.shirtExtras`), plus aucun triangle déchiré (`5c-02`, `5c-02b`). Le maillage de cheveux du modèle est caché et remplacé par **une coiffure en formes simples** (`CustomerHair`) : 3 formes (courte, mi-longue, hérissée) × 4 couleurs, tirées à chaque apparition, calées sur les vrais sommets du crâne.
2. **Éclats** : points ronds doux (texture radiale générée, matériau transparent sans lumière) avec fondu de l'opacité sur la durée de vie ; taille par défaut 0,17 dans `FeedbackConfig` (`5c-04`).
3. **Bulle de commande** : pastille 25 % plus petite et **taille d'écran constante** (`FaceCamera.keepScreenSize`, distance de référence 14,3 m). Mesuré : la bulle fait 21 px de large à l'écran avec la caméra à 6 m, 14,3 m et 22 m (`5c-03-bulle-pres` / `5c-03b-bulle-loin`).
4. **Enseigne** : au milieu du mur du fond (`x = 2,75`), abaissée à `y = 1,35` ; la fenêtre, le tableau d'outils et le mur de caméras ont été redistribués de chaque côté. Elle ne chevauche plus aucune pastille (`5c-01`, `6a-04`).
5. **Premier client** : toujours une **caméra dôme ×1** (`CustomerConfig.forceFirstOrder`, `firstOrderProductIndex`, `firstOrderQuantity`) ; mesuré, les suivants sont au hasard.

### 6a — premier employé automatique
- **Scripts** : `EmployeeConfig` (ScriptableObject : vitesse 3, capacité 6, repos 0,4 s, prix d'embauche 150), `Employee` (états : aller au tas → attendre d'être plein + repos → aller au poste → déposer → repos), `HumanAnimator` étendu pour suivre la vitesse d'un `NavMeshAgent` (mêmes Idle/Walk/Run que le joueur). Il **réutilise `StackCarrier`** (6 caisses, pile dans le dos) : `ResourcePile` et `InstallStation` n'ont **pas été modifiés**, ils le détectent comme le joueur. Il n'a aucun accès aux billets ni aux produits.
- **Visuel** : même modèle humain équipé que le technicien, **gilet bleu et casque blanc** (arête du casque blanche aussi), pile de caisses dans le dos, ombre ronde ; apparition par `PopIn` sur le modèle.
- **NavMesh** (`EmployeeBuilder.BakeNavMesh`, relançable) : `NavMeshSurface` sur la géométrie de la pièce (établis, comptoir, étagères, palette, murs sont des obstacles), voxels de 0,08 m, sauvé en asset `Assets/Prototype/NavMesh.asset`. Le joueur, l'employé et les textes 3D sont exclus de la cuisson. Le poste 2 est désactivé à la cuisson : il n'est pas dans le NavMesh (l'employé ne va qu'au poste 1).
- **Dalle d'embauche** : `UnlockPad` réutilisé (`BuildUnlockPad` généralisé), en `(4,5; 0,01; 4,4)` près du tas, titre « EMBAUCHE » + prix « $ 150 » (`HireConfig.asset`, prix recopié depuis `EmployeeConfig`). La zone de ramassage du tas passe de 3 m à **4,2 m** de côté pour que l'employé s'y tienne à côté de la palette (le joueur ramasse un peu plus tôt, sans autre changement).
- **Mesures en Play** : NavMesh de 79 triangles, trajet tas → poste `PathComplete`. Dalle payée en 90 pas, argent 150 → 0, employé actif et sur le NavMesh. **60 s sans toucher au joueur** : 6 caisses portées, **4 produits fabriqués** (puis le plateau est plein et il attend au poste, comme prévu), **0 position dans un meuble**, 0 hors NavMesh, distance minimale à un client **7,2 m**, hauteur 0,03 à 0,27 m (le sol et la dalle du poste, à ±5 cm).
- **Captures** : `6a-01-employe-porte`, `6a-01b-employe-porte-jeu-hud`, `6a-02-employe-au-tas`, `6a-03-employe-au-poste`, `6a-04-dalle-embauche-jeu-hud`, `6a-04b-dalle-embauche`, `6a-05-embauche-apparition`.

Questions posées à l'époque (répondues plus haut) :
- **L'employé s'arrête quand le plateau de sortie est plein** (4 produits) : le poste attend que le joueur prenne les produits. C'est le comportement demandé (« il attend à côté »), mais le joueur doit vider le plateau pour que l'atelier tourne. Veux-tu que le plateau reste à 4, ou qu'une capacité plus grande ou un 2e employé qui livre aux clients soit prévu pour l'étape suivante ?
- **Poste 2** : il n'est pas dans le NavMesh (désactivé à la cuisson). Pour qu'un employé le serve plus tard, il faudra le cuire activé (ou faire un NavMesh dynamique). À prévoir à l'étape 6b ?
- **Joueur et employé** : l'employé ne fait pas attention au joueur (les agents s'évitent entre eux, pas le joueur) ; il peut passer à travers lui. Visible seulement si le joueur se place sur son trajet. À corriger (obstacle dynamique) ?
- **Course** : toujours en attente de « Jog Forward ».
- Tâche parallèle (dépôt iOS) : toujours REPORTÉE.

## Archive : résultats de l'étape 6b (Claude Code local)
**Étape 6b — compile (0 erreur, 0 warning), Build scene OK, testée via MCP en Play. Captures `6b-*` dans `unity/captures/`.**
- Ajoutés : `GuideConfig` (7 phrases, hauteur/oscillation de la flèche, `cratesToCollect` = 5, clé `guide_done`), `ObjectiveGuide`, `Courier`, `Editor/GuideBuilder.cs` (flèche 3D = tige + losange jaune émissif, phrase dans une pastille sous le HUD, menu `Tools > Idle Prototype > Réinitialiser le guide` qui efface la clé PlayerPrefs). `EmployeeBuilder` réécrit (porteur + livreur, `CourierConfig` capacité 4 / prix 250, `HireConfig` 150). `ProductCarrier.Has`, `InstallStation.ProductsOnTray` ajoutés. Aucun package nouveau (NavMesh = `com.unity.ai.navigation` 2.0.15, déjà dans le manifest).
- **Guide** : testé de bout en bout par un bot qui joue au joystick (`SetMoveInput`, sans téléportation) : caisses -> poste -> produit -> livraison -> billets -> dalle -> embauche, chaque étape se valide seule ; à la fin `guide_done` = 1, flèche et phrase masquées. Captures `6b-guide-1-caisses`, `6b-guide-2-poste`, `6b-guide-3-dalle`.
- **Livreur** : dalle « LIVREUR $ 250 » près du comptoir, gilet vert + casque blanc ; il prend les produits au plateau, les apporte au comptoir, repart chercher si le client veut un produit qu'il n'a pas, vide à la corbeille si mains pleines. Captures `6b-livreur`, `6b-atelier-deux-employes`.
- **Évitement / NavMesh** : `NavMeshObstacle` sans découpe sur le joueur ; priorités d'évitement 40/50 pour les employés ; poste 2 activé pendant la cuisson (143 triangles) ; bobine déplacée en (0,6 ; 0 ; 9,4).
- **Test 2 minutes, les deux employés, joueur immobile** : 81 billets produits au sol, aucun meuble traversé, pas de blocage (arrêts maximum : 16 s du porteur qui attend des caisses au tas, 12 s du livreur qui attend au plateau/comptoir = attentes normales). Sans employés, la boucle reste jouable (testé).
- **Équilibre (valeurs dans `StationConfig.asset` et `GuideConfig.asset`)** : avant : `cratesPerJob` 3, `jobDuration` 2 s -> 1er billet à ~13 s. Maintenant : `cratesPerJob` **5**, `jobDuration` **10 s**. Mesure avec le bot (parcours parfait) : 5 caisses à 1,3 s, produit pris à 14,2 s, 1er billet encaissé à **23,3 s** ; un humain sera vers 28-32 s : dans la cible 25-35 s. La mesure automatique des 100 premiers (1re dalle) n'a pas été concluante (le bot se bloquait quand les produits ne correspondaient pas à la commande) ; estimation : 1 produit / 10 s, ~10-25 $ par client -> 1re dalle vers 2 min, embauche (150) vers 4-5 min : un peu au-dessus de la cible 3-4 min.
- Constats : (1) les billets tombent à 1,7-2 m du joueur debout devant le comptoir : il faut marcher dessus (le guide le dit : « Ramasse tes billets »). (2) L'enseigne néon « SÉCURITÉ » passe sous la phrase du guide en portrait (léger chevauchement).

Questions posées à l'époque (répondues plus haut) :
- **Embauche trop tardive ?** Pour tomber à 3-4 min, je propose de baisser le prix du porteur à 120 ou de passer `billValue` à 12 ; à toi de choisir.
- **Néon** : le déplacer plus bas ou plus à gauche pour qu'il ne touche pas la phrase du guide ?
- **Mismatch client/produit** : les 3 types sortent en rotation alors que le client choisit au hasard ; faut-il que le poste fabrique d'abord ce que demande le premier client (ou une file visible) ?

## Archive : résultats des étapes 6c et 7a (Claude Code local)
**Étapes 6c et 7a — compile (0 erreur, 0 warning), Build scene OK, testées via MCP en Play. Captures `6c-*` et `7a-*` dans `unity/captures/`.**

**6c**
- **Dalles l'une après l'autre** : `UnlockPad` a un tableau `revealOnUnlock` : au départ seule la dalle du poste 2 (100) est visible ; la dalle « EMBAUCHE » apparaît quand elle est payée, la dalle « LIVREUR » quand le porteur est embauché (`PopIn` + étincelle `FeedbackSpawner`). Le guide tient compte des dalles cachées. Captures `6c-01-depart-une-dalle`, `6c-02-apparition-embauche`, `6c-03-dalles-titres`.
- **Titres et prix séparés** : titre à 3,5 m, prix à 0,7 m (plus de chevauchement) ; dalle d'embauche du porteur déplacée en (3,2 ; 4,4) pour rester dans le cadre portrait.
- **Néon** : déplacé sur le mur de gauche, près du coin du fond, tourné vers la pièce, au-dessus des étagères. Le mur du fond est libre pour les pastilles et la phrase (voir `6c-01`).
- **Production à la demande** : `ProductDemand` (une responsabilité) lit `CustomerQueue.Line` : d'abord le produit du client en tête, puis ce qui manque aux suivants, en comptant les produits déjà sur les plateaux ou portés ; sinon rotation. `InstallStation` le lit (champ facultatif `demand`).
- **Équilibre** (valeurs dans `StationConfig.asset` et `GuideConfig.asset`, prix et `billValue` inchangés) : `cratesPerJob` 6, `jobDuration` 4 s, `cratesToCollect` 6. Mesure avec un bot au joystick avec 0,5 s d'hésitation à chaque changement de cible : 1re caisse à 1,2 s, **1er billet encaissé à 20,8 s** (cible ≤ 25 s), **1re dalle à 54 s** (cible 60-75 s : un peu trop rapide), porteur vers 2 min (estimation à partir du débit mesuré ~2,6 $/s, la mesure continue a été faussée par une erreur du bot qui a perdu 150 s), **livreur 59 s après le porteur**, soit environ 3 min (cible 3 min 30). Avec les 2 employés et le joueur immobile : débit ~5,8 $/s (600 $ en 100 s).

**7a**
- Ajoutés : `SaveData`, `SaveSystem`, `OfflineConfig` (max 8 h, rendement 50 %, minimum 60 s, cadences mesurées 14 s / 14 s par aller-retour), `OfflineEarnings`, `OfflineWelcomeScreen`, `Editor/SaveBuilder.cs`, `DemandBuilder.cs`. `UnlockPad` : `saveId`, `Restore()`, `RemainingPrice`.
- **Fichier** : `save.json` dans `Application.persistentDataPath` ; écriture atomique `.tmp` puis `File.Replace` avec copie de secours `.bak` ; sauvegarde toutes les 10 s si l'état a changé, à la pause et à la sortie ; champ `version` + `Migrate()` vide. Contenu : argent, `lastSaveUtc`, prix restant de chaque dalle, employés embauchés, `guideDone`, plus `pendingOffline`/`pendingSeconds` (gain calculé mais pas encore récupéré, pour ne pas le perdre si on quitte avant de cliquer).
- **Tests** : relancer Play restaure argent 430, dalles payées, 2 employés actifs. Fichier abîmé + `.bak` valide : état restauré. Les deux abîmés : nouvelle partie propre (argent 0, 3 dalles intactes), pas d'exception. Sans sauvegarde : gameplay identique (la sauvegarde ne s'écrit qu'après).
- **Hors ligne** (calcul pur sur les assets) : 2 h avec porteur ET livreur = **10 714 $** (écran réel : +$ 10 721) ; porteur seul, livreur seul ou aucun = 0 ; 20 h = 8 h (plafond) ; durée négative = 0 ; 30 s = 0. Formule : débit = le plus petit entre poste (`productsPerJob / jobDuration`), porteur (`capacity / cycle / cratesPerJob`), livreur (`capacity / cycle`) et clients (quantité moyenne / `spawnInterval`) x valeur moyenne des produits x durée plafonnée x 50 %. Le débit réel mesuré (~5,8 $/s) est cohérent avec le débit théorique brut (~6 $/s avant rendement).
- **Écran « Bon retour ! »** : `7a-01-bon-retour` (durée, « +$ 10721 », bouton Récupérer, bouton ×2 grisé « bientôt »). Cliquer Récupérer ajoute le gain (60 -> 10 781). `7a-02-partie-rechargee` : dalles ouvertes, porteur et livreur présents.
- Menus : `Tools > Idle Prototype > Effacer la sauvegarde` (efface aussi la clé du guide) et `Simuler une absence de 2 h` (modifie `save.json` ; arrêter puis relancer Play).
- Guide : si une partie est rechargée après la dalle du poste 2, il reprend à « EMBAUCHE » au lieu de recommencer.
- Pour tes tests : `Effacer la sauvegarde` avant une nouvelle partie, sinon Play reprend l'état précédent.

Questions posées à l'époque (répondues plus haut) :
- **Gain hors ligne trop généreux ?** 2 h = ~10 700 $ alors que les dalles coûtent 100 à 250. Soit baisser le rendement (par exemple 15-20 %), soit attendre les améliorations de l'étape 7b (prix qui montent). Je propose de ne rien changer d'ici là.
- **1re dalle à 54 s** (cible 60-75 s) : ralentir un peu (par exemple `jobDuration` 5 s) ou garder ce début nerveux ?
- **Apparition de la dalle d'embauche** : le poste 2 et la dalle d'embauche ne sont pas visibles ensemble à l'écran quand on paye la dalle 1 (la nouvelle dalle est à droite, hors cadre). Faut-il une flèche ou un léger déplacement de caméra qui la montre ?

## Archive : résultats de l'étape 7b (Claude Code local)
**Étape 7b — compile (0 erreur, 0 warning), Build scene OK, testée via MCP en Play. Captures `7b-*` dans `unity/captures/`.** Corrections de la relecture 6c/7a incluses.

**Corrections de la relecture**
- `OfflineConfig` : rendement **20 %**, durée max **4 h** (asset et valeurs par défaut).
- **Caméra vers la dalle qui apparaît** : `CameraFollow.Peek(cible, 1,2 s)` (aller rapide, court maintien, retour sur le joueur, commandes jamais bloquées) ; appelée par `UnlockPad` quand une dalle suivante se dévoile (pas au rechargement d'une partie). Capture `7b-05-camera-dalle-embauche`.
- **Écran « Bon retour ! »** restylé : panneau arrondi semi-transparent, titre jaune, bouton vert arrondi, « pop » à l'ouverture (`7b-04`). Pour avoir de vrais coins arrondis j'ai généré par code un sprite `Assets/Prototype/Textures/RoundedRect.png` (9 zones, `Editor/UiSprites.cs`), aussi utilisé par les pastilles du HUD et la phrase du guide (même style partout).

**7b — améliorations**
- Ajoutés : `UpgradeConfig` (liste : id, nom, `kind`, `baseCost`, `costGrowth` 1,35, `maxLevel` 10, `effectPerLevel`), `UpgradeState` (niveaux en mémoire, jamais d'écriture dans les ScriptableObject), `UpgradeRow`, `UpgradePanel`, `Editor/UpgradeBuilder.cs`. Les scripts concernés lisent le multiplicateur à l'usage (champ facultatif `upgrades`) : `StackCarrier` (joueur seul), `PlayerController`, `InstallStation`, `ServiceCounter`, `Employee`, `Courier`. `FeedbackSpawner` : `Message()` et `Sparkle()`.
- Cinq améliorations et coûts de base : pile +1 caisse (60), vitesse joueur +5 % (80), fabrication −7 % de durée (100), paiement des clients +8 % (120), vitesse des employés +8 % (140). Coût du niveau n = base x 1,35^n (par exemple pile : 60 / 269 / 894 aux niveaux 0 / 5 / 9).
- **Mesuré en Play au niveau maximum** : pile 18 caisses (HUD « 0/18 » mis à jour), joueur 6,30 m/s (4,2 x 1,5), travail du poste 1,22 s (4 s x 0,3), paiement d'une commande de 25 $ = 45 $ (x 1,8), vitesse de l'agent employé 5,4 (3 x 1,8). Au niveau 1 : pile 9, joueur x1,05, travail x0,93, paiement x1,08, employés x1,08.
- **Interface** : bouton rond jaune « flèche vers le haut » en bas à droite (zone sûre), point rouge quand un achat est possible, panneau arrondi avec une ligne par amélioration (icône en formes simples, nom, « Niv x/10 », « Prochain : … », bouton « $ coût » grisé si argent insuffisant, « MAX » à la fin). Le jeu continue derrière. À l'achat : pop de la ligne, étincelles et chiffre flottant (« +5 % vitesse », capture `7b-03-achat`). Captures `7b-01-bouton-point`, `7b-02-panneau`. Le chiffre flottant est dans le monde 3D : il passe derrière le panneau s'il est ouvert.
- **Sauvegarde version 2** : niveaux (par `id`) dans `SaveData`, `Migrate()` 1 -> 2 (niveaux à 0). Testé : fichier v1 chargé (argent 777, niveaux 0, écran « Bon retour » +4 291 $ pour 2 h) ; fichier v2 avec niveaux 5/5/5 sur fabrication, employés et paiement : niveaux restaurés et gain 2 h = **8 412 $**. Les gains hors ligne tiennent compte des niveaux : durée du travail x multiplicateur, cycles des employés / multiplicateur de vitesse, valeur x multiplicateur de paiement (`OfflineEarnings`).
- **Guide** : deux étapes ajoutées après l'embauche du porteur : « Embauche un livreur sur la dalle LIVREUR » (flèche vers la dalle), puis « Ouvre le menu Améliorations » (le bouton bat, pas de flèche 3D). Fini à l'ouverture du panneau. Les phrases sont dans `GuideConfig`.
- Le jeu reste jouable sans toucher au panneau (aucune amélioration obligatoire).

Questions posées à l'époque (répondues plus haut) :
- **Prix de départ** (60 à 140) : avec ~2,5 $/s au début on a bien un achat toutes les 60 à 90 s, mais cela retarde la 1re dalle (100) si on achète d'abord. Faut-il griser les améliorations tant que la dalle du poste 2 n'est pas payée ?
- **Plafond d'économie** : au niveau 10 partout la fabrication dure 1,2 s, le débit est limité par le porteur (6 caisses par aller-retour), pas par le poste. Prochaine étape d'économie : améliorer aussi la capacité du porteur et du livreur ?
- **Gain hors ligne** : le rendement n'augmente que par les niveaux achetés ; à 20 %, 2 h = ~4 300 $ sans niveau, ~8 400 $ avec 5 niveaux sur trois lignes. Cohérent avec les prix qui montent ?

## Archive : résultats de l'étape 8a (Claude Code local)
**Étape 8a — compile (0 erreur, 0 warning), Build scene OK, testée via MCP en Play. Captures `8a-01-bouton-engrenage` et `8a-02-reglages`. Je n'ai rien écouté (pas d'oreilles) : tout est mesuré par script.**

**Suites de la relecture 7b (faites d'abord)**
- Le **bouton Améliorations n'apparaît qu'après l'embauche du porteur** (`PopIn`, via `revealOnUnlock` de la dalle EMBAUCHE ; le déplacement de caméra ne vise que des dalles). Vérifié : caché au départ, toujours caché après la dalle du poste 2, visible après l'embauche du porteur. Au rechargement d'une partie où le porteur est embauché, il est visible.
- **6e amélioration « Capacité des employés »** (+1 caisse portée par le porteur, +1 produit porté par le livreur, max 10, coût de base 160) : `UpgradeKind.EmployeeCapacity`, `StackCarrier.employee`, `ProductCarrier` lit l'amélioration, `OfflineEarnings` ajoute le bonus aux capacités du porteur et du livreur. Un asset `UpgradeConfig` existant reçoit la ligne sans perdre ses réglages.

**8a — sons**
- `SfxLibrary` : 10 clips générés par code au démarrage (`AudioManager.Awake`), jamais pendant le jeu : ramassage (pop), dépôt (choc sourd), produit prêt (ding), billet (2 notes), commande payée (2 tintements), déblocage (arpège), achat d'amélioration (accord montant), embauche (fanfare), clic, refus (bip grave). Chacun est branché : ramassage du joueur (`StackCarrier.makesSound`), dépôt et produit prêt (`InstallStation`), billets (`BillCollector`), commande (`ServiceCounter`), dalles (`UnlockPad`, fanfare pour les dalles d'embauche), achats, clics des boutons, refus quand on clique « $ coût » sans assez d'argent (le bouton reste cliquable pour pouvoir refuser). Vérifié que les 10 sons sont bien déclenchés en jeu.
- **Mesures** (menu `Tools > Idle Prototype > Mesurer les sons`) : RMS commun à -20,0 dBFS pour les 10 sons (écart 0,0 dB, mesuré sur la partie sonore, hors silence), crêtes 0,17 à 0,46 (jamais au-dessus de 0,9). Durées : pop 0,10 s, dépôt 0,20 s, ding 0,50 s, billet 0,26 s, commande 0,70 s, déblocage 0,62 s, achat 0,70 s, embauche 0,95 s, clic 0,05 s, refus 0,30 s.
- **Hauteur des billets** : testé : chaîne de 12 billets = 1,00 1,06 1,12 1,19 1,26 1,33 1,41 1,50 1,59 puis plafond 1,59 ; après 1 s de pause retour à 1,00 (fenêtre 0,6 s, 8 demi-tons, `MusicConfig`).
- **`AudioManager`** : instance unique, pool de 8 sources 2D, 1 source de musique ; 8 demandes simultanées du même son = 3 lectures ; pool plein = remplace le son le plus ancien. **Allocation : 0 octet** pour 5 500 appels de lecture (`GC.GetAllocatedBytesForCurrentThread`).
- **Musique** : `MusicConfig` (champ `AudioClip` facultatif) ; sans fichier, boucle de 32 s générée (nappe de sinus la mineur sur 4 accords + petite mélodie pentatonique, RMS -26,5 dBFS, crête 0,18, volume 0,22). **Je ne peux pas juger si elle est agréable : je l'ai désactivée par défaut** (`musicOnByDefault` = faux) ; à écouter puis à activer dans Réglages ou dans l'asset.
- **Vibrations** : `Handheld.Vibrate()` seulement pour déblocage, embauche, achat ; 0,5 s mini entre deux ; seulement sur mobile (`Application.isMobilePlatform`) ; désactivable. **Non testé sur appareil** (impossible dans l'éditeur).
- **Réglages** : bouton engrenage discret en haut au centre, panneau arrondi avec trois interrupteurs (Sons, Musique, Vibrations) et « Effacer la sauvegarde » avec confirmation (Annuler / Effacer ; efface fichier, copie de secours et guide, puis recharge la scène). Testé : interrupteurs mémorisés après arrêt/relance de Play (sauvegarde **version 3**, `settingsSaved`, migration 2 -> 3 = valeurs par défaut) ; Annuler laisse la sauvegarde ; Effacer la supprime. « Sons » coupe aussi la musique. Le périphérique audio absent n'a jamais produit d'erreur (Console propre).
- **À écouter** : menu `Tools > Idle Prototype > Écouter les sons` : joue les 10 sons (1,2 s d'écart) avec leur nom dans la Console, puis 12 s de musique (API interne `AudioUtil` de l'éditeur ; si ton Unity la refuse, dis-le-moi).
- Le jeu est identique visuellement, hors engrenage et bouton Améliorations caché au départ.

Questions posées à l'époque (répondues plus haut) :
- **Écoute** : peux-tu lancer `Écouter les sons` et me dire ce qui ne va pas (trop aigu, trop long, trop fort, musique fatigante) ? Je ne peux pas le juger moi-même.
- **Musique par défaut** : activée ou non une fois écoutée ? Ou on attend un vrai fichier libre de droits ?
- **Sons des employés** : le dépôt de caisses du porteur déclenche aussi le choc sourd (même poste) ; faut-il les rendre plus discrets que ceux du joueur ?

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
- Étapes 4f et 5a relues sur captures : validées. Étape 5b (clients, comptoir, paiement) spécifiée.
- Étape 5b : clients (modèle humain, pool de 6), file avec bulle de commande, comptoir et caisse, livraison, billets, corbeille, porte ; 5a-bis (2 x 2 à plat, le travail attend) ; testé via MCP, captures 5b-*.
- Étape 5b relue sur captures : validée (boucle complète). Étapes 5c (corrections) et 6a (premier employé, NavMesh) spécifiées.
- Étapes 5c et 6a : torse des clients conservé et coiffures simples, éclats ronds, bulle à taille d'écran constante, enseigne au centre, premier client forcé ; NavMesh (AI Navigation 2.0.15 déjà installé), employé automatique (gilet bleu), dalle d'embauche ; testé via MCP, captures 5c-* et 6a-*.
- Étapes 5c et 6a relues sur captures : validées. Étape 6b (guide du début, employé livreur) spécifiée.
- Étape 6b relue : validée avec 4 points. Étapes 6c (dalles progressives, demande, équilibre) et 7a (sauvegarde, hors ligne) spécifiées.
- Étapes 6c (dalles progressives, demande) et 7a (sauvegarde, hors ligne) relues : validées. Étape 7b (améliorations) spécifiée.
- Étape 7b (améliorations) relue sur captures : validée. Étape 8a (sons, musique, vibrations, réglages) spécifiée.
- Étape 8a (sons, musique, vibrations, réglages) relue : validée. Étape 8b (préparer un test WebGL pour l'iPhone) spécifiée ; ensuite pause en attendant les retours de Sébastien.
