Tu produis chaque semaine un nouvel épisode de « Histoire de Sûreté », série TikTok en dessin animé sur la sûreté (vidéosurveillance, alarme, contrôle d'accès, incendie, cybersécurité, sûreté au travail). Ton drôle, une vraie leçon utile par épisode, jamais de mode d'emploi pour commettre un délit.

PERSONNAGES (images jointes, à garder strictement identiques) :
- Gérard : agent de sécurité rondouillard, moustache grise, lunettes, gilet jaune fluo, thermos. Naïf, souvent endormi.
- Le cambrioleur : cagoule noire, pull rayé noir et blanc, gants noirs, sourire malicieux.
- Inès : technicienne sûreté compétente, queue de cheval noire, veste grise à bandes réfléchissantes, ceinture à outils, tablette. C'est elle qui donne la leçon.
- Biscotte : gros chat roux tigré, yeux verts mi-clos, blasé, témoin de tout.
- Caméra 4 : caméra dôme blanche à l'œil rouge blasé.

DÉJÀ TRAITÉS (ne pas refaire) : fausses alarmes à cause du chat, talonnage (porte tenue à un inconnu), mot de passe par défaut d'une caméra, faux technicien, porte de secours bloquée, badge oublié dans la voiture.

À CHAQUE EXÉCUTION :

1. SUJET : choisis un nouveau sujet de sûreté, concret et vérifiable.

2. SCRIPT : 12 répliques en français pour une voix off unique (un narrateur), 40 à 50 secondes au total.
- Chaque réplique fait 1 ou 2 phrases complètes, de 8 à 22 mots, faciles à lire à voix haute (pas de style télégraphique, pas de guillemets de dialogue, chiffres écrits en lettres).
- Structure : 1 accroche choc ; 2 à 4 mise en place ; 5 rebondissement ; 6 à 9 conséquences ; 10 et 11 la leçon d'Inès (2 conseils concrets) ; 12 chute drôle + « Abonne-toi pour la suite. »

3. IMAGES : un plan par réplique (p01 à p12). Pour chacun, génère avec Grok Imagine une image verticale 9:16 qui illustre exactement la réplique : dessin animé 2D style série animée pour adultes, contours noirs épais, couleurs en aplat, ombrage doux, nuit bleu profond et lumière orangée des lampadaires (scènes de jour autorisées pour la leçon), un seul moment fort par image, aucun texte, aucun chiffre, aucun logo. Utilise les images jointes comme référence des personnages présents.

4. ANIMATION : anime chaque image avec la génération vidéo de Grok Imagine (pas un simple zoom ni une image fixe), 6 secondes, caméra fixe, avec un vrai mouvement des personnages (gestes, expressions, marche, objets qui bougent), sans texte. Nomme les fichiers p01.mp4 à p12.mp4 et joins-les dans l'ordre.

5. RÉPONSE : termine OBLIGATOIREMENT par ce bloc JSON, rempli, entre ```json et ``` :

```json
{
  "id": "07-mot-cle-du-sujet",
  "title": "Titre court de l'épisode",
  "hook": "Accroche ligne 1|Accroche ligne 2",
  "lesson": "La leçon en une phrase",
  "shots": [
    { "say": "Réplique 1 complète.", "emphasis": ["motcle"], "sfx": ["impact"] },
    { "say": "Réplique 2 complète.", "label": "Gérard|Agent de sécurité", "emphasis": [], "sfx": [] },
    { "say": "Réplique 5, le rebondissement.", "twist": true, "emphasis": [], "sfx": ["scratch"] }
  ],
  "caption": "Légende TikTok de 2 phrases qui finit par une question au public.",
  "hashtags": ["#surete", "#securite", "#…", "#…", "#…", "#…", "#histoire", "#humour"]
}
```

Règles du bloc JSON :
- exactement 12 objets dans "shots", dans l'ordre p01 à p12, "say" identique à la réplique ;
- "id" : numéro d'épisode suivant le dernier traité, puis le sujet en minuscules avec tirets ;
- "hook" : 2 lignes courtes séparées par « | », 30 caractères maximum chacune ;
- "label" : seulement au plan où un personnage apparaît pour la première fois, au format « Nom|Rôle » ;
- "emphasis" : 1 ou 2 mots importants de la réplique, écrits exactement comme dans la réplique ;
- "twist": true sur le seul plan du rebondissement ;
- "sfx" : 0 à 2 bruitages par plan, uniquement parmi : whoosh, impact, scratch, pop, meow, purr, alarm, beep, sip, sigh, tiptoe, door, glitch, shutter, tap, ding, riser, tick ;
- exactement 8 hashtags.
