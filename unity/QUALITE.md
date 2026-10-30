# QUALITE — Cahier de qualité « clean de fou » (voulu par Sébastien, 29/10)

Document de référence **pour Claude Code local et pour le cloud**. À relire avant chaque étape. Il complète `DESIGN.md` (vision, cap « gestion d'usine type Idle Lumber ») et `TACHES.md` (étapes en cours).

## 0. Ce que « clean » veut dire (la barre à atteindre)
1. **Cohérent** : un seul style (palette, police, formes, icônes, animations) partout, du HUD au décor.
2. **Lisible sans lire** : on comprend quoi faire par l'image, le mouvement et la couleur ; jamais plus de 6 mots d'affilée.
3. **Réactif** : tout geste a un retour en **moins de 100 ms** (image + son).
4. **Fluide** : **60 images par seconde visées sur iPhone, jamais sous 45** ; chargement ≤ 8 s en 4G.
5. **Robuste** : 0 erreur en console, sauvegarde inviolable, aucun écran gris ni blocage, plus de 24 h de jeu sans bug visible.
6. **Respectueux** : jamais de publicité imposée, jamais de mur payant, jamais de perte de progression.
7. **Mobile pour de vrai** : portrait, une main, encoche et barre du bas respectées, zones tactiles généreuses.

## 1. Bible artistique (à appliquer partout ; cible visuelle : `unity/maquettes/ui-01-jeu.png` et `ui-02-commandes.png`)
- **Police unique : Lilita One** (licence OFL, fichier déjà dans le dépôt : `unity/Assets/Fonts/LilitaOne-Regular.ttf` + `OFL-LilitaOne.txt`). Supporte tous les accents français, « » € $ % + × ; **ne contient pas** les flèches, étoiles, coches : utiliser des **icônes** (sprites), jamais de symboles Unicode. Tailles sur le Canvas 1080×1920 : titres 80, chiffres 96 à 120, texte 60 minimum (rien en dessous de 44).
- **Texte de jeu** : blanc, **contour sombre `#0E1620` de 8 px** (sur 1080), ombre portée de 8 px vers le bas. Montants en vert clair `#8CFF9E`, Points Pro en cyan `#8DF1FF`.
- **Palette** : jaune sécurité `#FFC400` (dalles, accents), orange chantier `#FF7A1A` (boutons carrés, gilet), bleu techno `#1FA0FF` et cyan `#27D3E6` (écrans, Points Pro), vert argent `#3DDC5A`, rouge alerte `#F2453D`, ardoise `#1E2A38` (pastilles), crème `#F6EFE2` (murs), gris bleuté `#D5DCE4` / `#C3CCD7` (sol), encre `#0E1620`.
- **Formes** : pastilles arrondies à fond sombre pour les ressources ; **boutons carrés** orange de 150 px (rayon 45) avec icône blanche et **pastille rouge** pour « nouveau » ; panneaux rayon 80 ; chaque élément a un **socle** plein (ombre dure de 12 à 28 px) pour l'effet « jouet ».
- **Icônes** : un seul jeu cohérent (remplies, arrondies, contour sombre, 2 à 3 couleurs) : billet, étoile Points Pro, caisse, engrenage, flèche d'amélioration, liste de commandes, camionnette, clé, caméra dôme, badgeuse, alarme, cadenas. Créées en sprites (SVG convertis ou dessinés en code), jamais en texte.
- **Mouvement** : tout apparaît avec un **ressort** (échelle 0 → 1,15 → 1, 220 ms), tout disparaît en 120 ms ; boutons : échelle 0,92 à l'appui ; chiffres qui changent : « pop » 1 → 1,25 → 1 en 200 ms et défilement du nombre.
- **Décor** : jamais de zone vide à l'écran ; couleurs saturées mais harmonisées avec la palette ; éclairage chaud, ombres douces ; objets épais et arrondis ; un détail vivant (écran qui clignote, caméra qui pivote, ventilateur) par zone.
- **Personnages** : silhouettes lisibles à 9 % de la largeur d'écran ; tenue qui change à chaque monde ; clients variés.

## 2. Les 5 premières minutes (le plus important pour la rétention)
Chronologie cible, à vérifier à la main et à chronométrer : **0:05** le technicien est déjà en mouvement vers la flèche · **0:10** première caisse ramassée · **0:20** premier billet · **0:30** première dalle payée (confettis, son, zoom) · **1:00** la première machine travaille toute seule · **1:30** premier client servi · **2:00** premier employé · **6:30** premier tapis automatique (cible révisée le 30/10 après le test de rythme : le tapis suit le livreur, Test et Emballage) · **5:00** première commande et premiers Points Pro. Règles : **une nouveauté toutes les 20 à 30 s**, jamais de cul-de-sac (si le joueur ne fait rien pendant 20 s, le guide le relance), pas de texte long, la main/la flèche montre toujours l'objectif.

## 3. Budgets techniques (WebGL sur iPhone) et méthode
- **Images par seconde** ≥ 50 (visé 60) mesurées par le panneau Diagnostic sur le jeu complet ; **taille du build compressé ≤ 25 Mo** (17 Mo aujourd'hui) ; **mémoire < 256 Mo** ; **appels de dessin < 150**, triangles < 150 000, textures ≤ 1024, atlas pour l'interface ; **0 allocation par image** dans `Update` (pooling, pas de `new`, pas de LINQ).
- **Effets coûteux** (ombres temps réel, post-traitement, grosses particules) : seulement si la mesure le permet (`WebGlPostFxGate`), sinon version « légère ». Ombres : cuites (lightmap) pour le décor, temps réel seulement pour les personnages si ≥ 50 i/s.
- **Test de fumée automatique** (menu `Tools > Idle Prototype > Test de fumée`) : lance Play 90 s avec entrées simulées, vérifie **0 erreur / 0 avertissement**, que l'argent augmente, que la sauvegarde puis rechargement redonne **le même état**, et affiche le résultat. À exécuter avant chaque push.
- **Tests de l'économie** (EditMode) : courbes de coûts, gains hors ligne, migration de sauvegarde (une ancienne sauvegarde de chaque version doit se charger).
- **Journal d'événements local** (pas de service externe pour l'instant) : temps jusqu'à chaque déblocage, durée de session ; affiché dans le panneau Diagnostic pour que les testeurs envoient une capture.
- **Chaque étape se livre** avec : compilation 0/0, test de fumée OK, captures avant/après 390×844, images par seconde avant/après, build WebGL unique à la fin, `TACHES.md` mis à jour (25 lignes maximum), push.

## 4. Mobile et iPhone (à ne jamais oublier)
- **Zones tactiles** ≥ 44 pt (122 px sur le Canvas 1080) ; pas de bouton collé au bord (zone sûre : `SafeAreaFitter` partout, y compris en bas pour la barre de Safari) ; portrait verrouillé.
- **Safari** : pas de zoom au double toucher ni au pincement de la page, pas de rebond de défilement, hauteur d'écran dynamique (`100dvh`), son déverrouillé par le premier toucher (fait), contexte WebGL perdu = rechargement (fait), sauvegarde à la mise en pause et toutes les 10 s (fait). **Pas de vibration sur Safari iPhone** (l'API n'existe pas) : compenser par un retour visuel plus fort.
- **Écran d'accueil** : manifeste + icônes (180, 192, 512) + `apple-touch-icon` + plein écran pour « Ajouter à l'écran d'accueil » de la version Vercel ; écran de chargement avec le logo et la barre de progression ; **écran titre** (logo « Idle Sécurité », bouton Jouer).
- **Accessibilité** : contraste ≥ 4,5:1, informations jamais portées par la couleur seule (icône en plus), option « réduire les secousses », gros textes.
- **Langues** : **toutes les chaînes passent par une table** (`Loc`, FR par défaut, EN prévu) dès maintenant : coûteux à rattraper plus tard.

## 5. Économie et rétention (idées à intégrer dans l'ordre)
- **Courbes** : coût du niveau n = base × 1,12 à 1,18^n ; revenu par niveau × 1,10 ; paliers de gratification toutes les 3 à 5 min ; « mur » doux toutes les 10 à 15 min qui incite à passer en prestige ; **premier prestige vers 45 à 60 min** de jeu.
- **Gains hors ligne** plafonnés à 8 h, **x2 en regardant une vidéo** (factice pour l'instant) ; message de bienvenue clair (fait).
- **Retours quotidiens** : récompense du jour (7 jours qui montent), **quêtes quotidiennes** (3 par jour), **succès** (trophées avec petites récompenses), **événements** de la semaine.
- **Collection** : gadgets de sécurité à débloquer (caméra thermique, drone, serrure biométrique…) avec bonus passifs ; **tenues** du technicien par monde.
- **À surveiller** (objectifs avant publication) : retour au jour 1 ≥ 40 %, jour 7 ≥ 15 %, première session ≥ 8 min ; test fermé de 20 à 50 personnes avant toute sortie.

## 6. Monétisation propre (plus tard, après accord de Sébastien)
Vidéo récompensée (x2 d'une commande, x2 hors ligne, boost de 5 min) · « sans publicité » en achat unique · petits packs de départ · **aucun blocage payant**, aucune publicité qui coupe le jeu. Prévoir : politique de confidentialité (URL), étiquettes de confidentialité Apple, classification d'âge, demande de suivi (ATT) seulement si on suit vraiment.

## 7. Sortie App Store (checklist, pour la fin)
Nom vérifié (marque, App Store) · icône 1024 · captures 6,7" avec bandeaux dans le style de la référence · build iOS **dans le cloud** (Unity Build Automation ou autre service ; Codemagic écarté par Sébastien) puis TestFlight · compte développeur (existe déjà) · test fermé · fiche FR/EN · politique de confidentialité · version 1.0.

## 8. Améliorations possibles (idées classées par rapport qualité/prix ; piocher dans l'ordre)
| Priorité | Idée | Pourquoi |
|---|---|---|
| A | Police Lilita One + kit d'interface (maquettes) | Le plus gros gain visuel pour un petit travail |
| A | Écran titre + chargement avec logo | Première impression |
| A | Nombres abrégés (1,2K / 3,4M / aa) + défilement | Lisibilité des gros chiffres |
| A | Test de fumée automatique + journal d'erreurs | Zéro régression |
| A | Table de chaînes `Loc` | Évite une réécriture |
| B | Bouton « Collecter tout » / aimant de billets amélioré | Confort |
| B | Vitesse x2 (bouton) | Plaisir et monétisation douce |
| B | Mini-carte / flèches vers les dalles hors écran | Orientation |
| B | Tutoriel par la main qui pointe (comme la référence) | Compréhension |
| B | Photo/GIF de partage de sa belle usine | Viralité |
| C | Mode nuit/jour par monde, météo | Ambiance |
| C | Musique par monde, voix off courtes (« Livraison ! ») | Immersion |
| C | Skins de technicien, décorations de l'atelier | Collection |
| C | Classement amical (plus tard, avec un service) | Rétention |

## 9. Règles de travail pour Claude Code local (mode autonome, y compris la nuit)
1. **Un seul chantier à la fois**, dans l'ordre de la file d'attente de `TACHES.md` ; ne pas commencer le suivant avant d'avoir **poussé** le précédent.
2. **Ne pas attendre Sébastien** : si une décision humaine est nécessaire, prendre la valeur par défaut indiquée « (défaut) », la noter dans Résultats et continuer ; ne bloquer que pour un vrai risque (suppression de données, achat, compte externe).
3. **Ne rien casser** : l'ancienne sauvegarde doit se charger (version +1 avec migration), le test de fumée doit passer, les gains hors ligne rester justes.
4. **Un build WebGL par étape terminée**, pas par essai (poids du dépôt) ; indiquer le numéro de build dans Résultats.
5. **Preuves** : captures nommées par étape (`11a-…`), mesures chiffrées avant/après, ce qu'on n'a pas pu vérifier dit clairement (pas d'oreilles, pas d'iPhone).
6. **Écrire court** : Résultats ≤ 25 lignes par étape, le reste en archive.
7. **Si Unity ne répond plus** (MCP déconnecté) : l'écrire dans Questions, ne rien inventer, attendre la reconnexion.
