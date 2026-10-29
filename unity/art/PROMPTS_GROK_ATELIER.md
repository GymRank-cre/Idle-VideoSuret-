# Prompts Grok Imagine — le décor de l'atelier (niveau « référence de jeu »)

Complète `PROMPTS_GROK.md`. Première image générée par Sébastien : `unity/art/refs/grok-atelier-01.jpg`.

## Analyse de la première image (ce qu'on garde, ce qu'on corrige)
**À garder** : la machine blanche et bleue arrondie avec sa lampe jaune sur le dessus ; les caisses avec ruban et étiquettes ; les deux caméras dômes au mur ; la camionnette vue par le rideau métallique ; les plantes ; le câble au sol ; le technicien (gilet orange, casque jaune) qui porte une caisse ; la lumière douce et les formes épaisses et chanfreinées.
**À corriger (important, parce que l'image doit servir de cible au jeu)** :
1. **C'est une boîte qui flotte** dans un vide gris-bleu (plus de la moitié de l'image est vide) ; le jeu, lui, est **plein cadre** : sol et murs débordent de tous les côtés.
2. **Vue en coin isométrique (diagonale)** ; la caméra du jeu est **droite, inclinée de 55°**, murs et sol parallèles aux bords de l'écran.
3. **Teinte violette/lavande** et couleurs pastel délavées ; notre palette est plus **saturée** (jaune sécurité, orange, bleu techno, crème, gris bleuté).
4. **Trop petit et trop peu dense** ; il manque ce qui fait le jeu : **dalles d'achat jaune/noir**, **file de clients** devant le comptoir, **zone de ramassage** lisible, sol varié, décor vivant.
5. Le sol est uniforme ; il faut du **carrelage à deux tons**, des **marquages**, des **décalcomanies**, des reflets légers.

## Méthode « niveau pro »
1. Générer **P1** 4 fois, garder la meilleure. 2. **Joindre cette image comme référence de style** à tous les autres prompts (consistance). 3. Corriger par petites retouches avec **P2** (édition), pas en repartant de zéro. 4. Générer en la plus haute résolution possible. 5. Envoyer les 3 à 5 meilleures à Claude, qui les range dans `unity/art/` et les donne à Claude Code local.
Structure de chaque prompt : **Sujet → Caméra → Disposition (zones) → Matières et couleurs → Lumière → Qualité → À éviter**.

---

## P1 — Le décor de référence « caméra du jeu » (le plus important)
```
Art direction for a premium mobile idle-factory game called "Idle Sécurité" (an electronic security installation company). Bright stylized 3D cartoon, chunky rounded toy-like miniature look, smooth bevels, soft clay-like materials, polished like the best casual tycoon games on the App Store. Cheerful, family-friendly.

SCENE: a bright, bustling security-equipment workshop seen through a fixed game camera: high three-quarter top-down view, camera pitched 55 degrees downward, looking straight ahead (walls and floor grid parallel to the frame edges, NOT a diagonal isometric corner), medium-long lens (about 50 mm equivalent) with a subtle tilt-shift miniature feel, portrait 9:16. The room is NOT a floating cutaway box: the floor and walls extend past the left, right and bottom edges of the image, so the picture is filled edge to edge with no void, no grey background and no outer border.

LAYOUT, top to bottom of the frame:
- TOP 25%: the back wall, cream painted with a yellow safety stripe at mid-height; a large window with soft daylight; a pegboard with colorful tools; two dome security cameras mounted on the wall; a dark slate shelf unit full of cardboard boxes (no readable text); a neon-style sign shape without any text.
- UPPER MIDDLE: a conveyor lane running left to right, carrying cardboard crates into a rounded white and tech-blue assembly machine with a glowing yellow lamp on top and a small screen; extra crates piled at both ends of the lane.
- MIDDLE RIGHT: a wooden pallet stacked with cardboard crates (the pickup zone), with a few cable reels beside it.
- CENTER: two empty square floor plates with yellow-and-black hazard-stripe borders and a dark slate center, one of them glowing softly (unlock pads), clearly readable.
- LOWER LEFT: a wooden service counter with a cash register, and two cartoon customers waiting in line on painted floor markings.
- RIGHT EDGE: an open roller door revealing a parking lot and a white delivery van with a ladder on the roof.
- FOREGROUND: a cheerful technician in an orange high-visibility vest and yellow hard hat, running while carrying crates stacked on his back, small dust puffs at his feet.
- EVERYWHERE: cables on the floor, potted plants, a water cooler, a workbench with tools, a fire extinguisher, ceiling lamps, floor decals, spare tires. Dense but organized: every object has a simple readable silhouette even at thumbnail size.

MATERIALS AND COLORS: glossy two-tone ceramic floor tiles in warm grey-blue (#D5DCE4 and #C3CCD7) with light grout and subtle reflections; cream walls (#F6EFE2) with a safety-yellow stripe (#FFC400); machines in white and tech blue (#1FA0FF) with cyan (#27D3E6) light accents; warm orange-brown cardboard crates with pale tape; dark slate (#1E2A38) shelves and counters; construction orange (#FF7A1A) accents; money green (#3DDC5A) and alert red (#F2453D) used sparingly. Saturated, harmonious, clean, no purple tint.

LIGHTING: warm sunlight from the upper left through the window, soft cool fill from the right, gentle ambient occlusion in corners and under objects, soft shadows, a faint glow from the machine lamp, subtle light shafts. High-key, bright, premium.

AVOID: text, letters, numbers, logos, watermark, floating base, outer border, grey void, dark vignette, empty floor areas, photorealism, black outlines, harsh shadows, washed-out pastel colors, lavender or purple cast.
```

## P2 — Retoucher TON image (à utiliser avec l'image jointe, mode édition)
```
Use the attached image as the base and keep its charm: the rounded white and blue assembly machine with the yellow lamp, the cardboard crates with tape, the two dome cameras, the white van seen through the roller door, the potted plants, the cable on the floor, the worker in the orange vest and yellow hard hat carrying a crate, the soft lighting and chunky bevels.
CHANGE: (1) remove the floating box and the grey-blue void: extend the room so the floor and the walls fill the entire frame edge to edge; (2) switch from the diagonal isometric corner to a straight game camera pitched 55 degrees, walls parallel to the frame edges; (3) replace the lavender tiles with glossy two-tone warm grey-blue tiles and add floor decals and painted markings; (4) increase color saturation by about 20 percent and remove every purple cast; (5) make everything larger and denser: add a pallet of crates on the right, two square yellow-and-black hazard-stripe floor plates in the center, a service counter with two customers waiting in line, a pegboard with tools, a water cooler, a fire extinguisher and more cables; (6) keep all objects chunky with simple readable silhouettes. No text, no letters, no watermark.
```

## P3 — Planche d'accessoires de l'atelier (2 planches pour garder la qualité)
**Planche A, grands éléments**
```
[Paste the style paragraph of P1.] TARGET: a clean game-asset sprite sheet of 9 isolated props in a 3x3 grid, generous spacing, each on a plain pure white background, same three-quarter view (camera pitched 55 degrees), soft contact shadow under each object: (1) rounded white and tech-blue assembly machine with a yellow lamp, (2) conveyor belt segment with crates, (3) testing station with a small screen and cables, (4) packing machine with a cardboard box, (5) dark slate shelf unit full of boxes, (6) wooden service counter with a cash register, (7) wooden pallet stacked with crates, (8) white delivery van with a ladder, (9) roll-up garage door half open. No text, no logos.
```
**Planche B, petits éléments**
```
[Paste the style paragraph of P1.] TARGET: a clean game-asset sprite sheet of 12 small isolated props in a 4x3 grid, generous spacing, plain pure white background, same view: dome security camera, badge reader, alarm siren, water cooler, potted plant, fire extinguisher, tool pegboard, workbench with tools, cable reel, stack of cardboard boxes, ceiling lamp, floor hazard-stripe plate. No text, no logos.
```

## P4 — L'extérieur de l'atelier (le monde déborde de l'écran)
```
[P1 style paragraph.] SCENE: the exterior in front of the workshop, same game camera (pitched 55 degrees, straight, portrait 9:16): a small asphalt parking lot with white parking lines, a white delivery van backing toward a loading dock, a ramp, a few traffic cones, a lamp post, a bright green lawn with a small hedge and two round trees, a street with a cartoon car passing, a blue sky with soft clouds at the top, the workshop facade with a big roller door and a sign shape without text. Filled edge to edge, no void, warm sunlight, saturated and clean. AVOID: text, logos, void, photorealism, purple cast.
```

## P5 — Plan de niveau vu de dessus (pour placer les zones)
```
[P1 style paragraph.] TARGET: a top-down orthographic level layout of the workshop floor plan, 90 degrees straight down, portrait 9:16, flat clean colors, simplified shapes, color-coded zones with small icon shapes (no text): crate pickup zone (orange), conveyor lane and assembly machine (blue), two unlock pads (yellow and black), service counter and customer queue (slate and green), loading door and van bay (grey), walking paths in light cream, a 1-meter grid faintly visible. Clean, readable, game-design diagram look.
```

## P6 — Planche de matières (cohérence des matériaux Unity)
```
[P1 style paragraph.] TARGET: a material swatch board, 3x4 grid of square seamless samples with soft rounded corners on a neutral background: glossy warm grey-blue ceramic floor tile, second tile tone, cream painted wall, safety-yellow stripe paint, brushed white and blue machine plastic, dark slate painted metal, warm cardboard with pale tape, light wood pallet planks, black rubber conveyor belt, yellow-and-black hazard stripe, window glass with soft reflection, green plant leaf. No text.
```

## Idées de variantes (une fois P1 validé)
Même décor **au petit matin** (lumière dorée), **en fin de journée** (orangé), **de nuit** avec lampes allumées et écrans bleus ; **version « niveau 5 »** de l'atelier (agrandi, plus de machines, plafond avec rails, drones d'inventaire) pour montrer la progression visuelle.
