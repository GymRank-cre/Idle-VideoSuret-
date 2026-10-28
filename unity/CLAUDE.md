# Projet : idle arcade mobile (Unity)

## Contexte
Je suis Sébastien, technicien en sécurité électronique, sous Windows. Je fais un jeu mobile "arcade idle" (style hybrid-casual) avec le thème d'une entreprise d'installation de sécurité électronique. Réponds-moi en français, de façon concise et directe, en plain text, et avance par petites étapes que je peux tester.

## Moteur et cibles
- Unity 6 LTS, URP, C#, package Input System (celui par défaut).
- Android en premier (je teste en USB depuis Windows). iPhone ensuite via un build cloud (Unity Build Automation ou Codemagic) vers TestFlight, car je n'ai pas de Mac.
- J'ai les comptes développeur Apple et Google Play.

## Le jeu, en une phrase
Le joueur déplace un technicien au joystick : il ramasse des caisses de matériel (qui s'empilent dans son dos jusqu'à "MAX"), les dépose à un poste d'installation (barre de progression), gagne des billets à ramasser, et paie des dalles de déblocage (prix qui descend quand on paie) pour ouvrir de nouveaux postes, des employés automatiques et de nouvelles zones. Une file de clients attend avec une bulle de commande (caméra dôme, badgeuse, alarme).

## Ce qui existe déjà (dossier Assets du zip)
- PlayerController : déplacement clavier (éditeur) et SetMoveInput() prévu pour le joystick tactile.
- StackCarrier : pile dans le dos avec effet de balancement, indicateur MAX.
- ResourcePile : zone de ramassage automatique.
- CameraFollow : caméra en hauteur qui suit le joueur.
- Editor/PrototypeSetup : menu Tools > Idle Prototype > Build scene, qui monte toute la scène.
Ces scripts n'ont jamais été compilés dans Unity : vérifie d'abord qu'ils compilent et corrige ce qui ne va pas.

## Première tâche
1. Intégrer les scripts au projet, vérifier la compilation, lancer Build scene.
2. Me dire clairement ce que je dois tester (WASD, ramassage, MAX), et corriger selon mes retours.

## Feuille de route (une étape à la fois)
1. Joystick tactile relié à SetMoveInput().
2. Poste d'installation : prend les caisses de la pile, barre de progression, fait apparaître des billets.
3. Billets à ramasser, compteur d'argent (HUD en haut à gauche, stock de caisses en haut à droite).
4. Dalles de déblocage avec prix décroissant.
5. Clients en file avec bulle de commande et paiement.
6. Employés automatiques (NavMesh) qui répètent le cycle.
7. Améliorations, sauvegarde JSON, gains hors ligne.
8. Effets visuels et sonores (+180 flottant, confettis), puis build Android, test fermé, iOS via cloud.

## Règles de code
- Une seule responsabilité par script, champs en [SerializeField], pas de dépendance à un package externe sans me le dire.
- Valeurs d'économie (prix, vitesses, capacités) dans des ScriptableObject, jamais en dur.
- Pooling pour les objets créés souvent (caisses, billets).
- Prototype d'abord avec des formes simples (cubes, capsules) : si ce n'est pas agréable à jouer avec des cubes, on ne passe pas aux vrais assets.
- Assets visés plus tard : packs low-poly semi-réalistes (Synty POLYGON ou Kenney), animations Mixamo, DOTween.
