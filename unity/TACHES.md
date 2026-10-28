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

## Tâche en cours : étape 2 — Poste d'installation

**Objectif :** le joueur dépose ses caisses à un poste ; une barre de progression se remplit ; des billets apparaissent.

**À créer**
1. `StationConfig` (ScriptableObject) : `cratesPerJob` (caisses nécessaires pour un travail, défaut 3), `jobDuration` (secondes par travail, défaut 2), `billsPerJob` (défaut 5), `billValue` (défaut 10), `depositInterval` (délai entre deux caisses déposées, défaut 0,15), `inputCapacity` (caisses stockées au poste, défaut 6).
2. `InstallStation` (MonoBehaviour, zone trigger comme `ResourcePile`) :
   - tant que le joueur est dans la zone et que `inputCapacity` n'est pas atteint : prend une caisse avec `StackCarrier.RemoveLast()` toutes les `depositInterval` secondes ; la caisse vole vers le poste puis est recyclée (pool) ;
   - quand le stock du poste atteint `cratesPerJob` et qu'aucun travail n'est en cours : démarre un travail de `jobDuration` secondes, consomme `cratesPerJob` caisses ;
   - à la fin du travail : fait apparaître `billsPerJob` billets (cubes verts plats) autour d'un point de sortie. Les billets restent au sol pour l'instant (ramassage = étape 3).
3. `ProgressBar3D` : petite barre au-dessus du poste (deux cubes, face à la caméra, comme l'indicateur MAX) qui affiche la progression du travail.
4. `ObjectPool` générique simple (liste de GameObjects inactifs), utilisé pour les caisses déposées et les billets. Ne pas modifier `ResourcePile` pour l'instant.
5. Mettre à jour `PrototypeSetup` : placer le poste à gauche de la zone de ramassage (ex. position `(-6, 0.1, 0)`, cube bleu foncé de 3×0.2×3, collider trigger), créer l'asset `Assets/Prototype/StationConfig.asset`, relier tout par `SerializedObject`.

**Critères de réussite (à tester via MCP, puis par Sébastien)**
- Aucune erreur de compilation ni dans la Console au lancement.
- Ramasser 8 caisses, aller au poste : les caisses partent une par une ; le MAX disparaît dès qu'il y a de la place dans la pile.
- La barre de progression se remplit pendant `jobDuration`, puis 5 billets apparaissent.
- Changer `StationConfig` dans l'Inspector change le comportement sans toucher au code.

**Hors périmètre :** compteur d'argent, ramassage des billets, clients, employés, joystick (déjà fait).

---

## Résultats (à remplir par Claude Code local)
_(vide)_

## Questions (à remplir par Claude Code local)
_(vide)_

## Historique
- Étape 0 : scripts de base compilés et validés (WASD, ramassage, MAX).
- Étape 1 : joystick tactile `VirtualJoystick` ajouté ; compilation OK, déplacement OK via `SetMoveInput` ; glissé réel à confirmer par Sébastien.
