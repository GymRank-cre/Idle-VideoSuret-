# Prompts Grok Imagine — direction artistique « Idle Sécurité »

Mode d'emploi : **coller le BLOC STYLE en premier, puis une ligne « CIBLE »** (ci-dessous). Les prompts sont en anglais (meilleurs résultats) ; le jeu ajoutera lui-même tous les textes (police Lilita One) : **ne jamais demander de texte dans l'image**. Format par défaut : 9:16 (portrait). Générer 4 variantes, garder la meilleure, réutiliser le même bloc à chaque fois pour garder un style cohérent.

## BLOC STYLE (à coller à chaque fois)
```
Art direction for a premium mobile idle-factory game called "Idle Sécurité" (an electronic security installation company). Style: bright stylized 3D cartoon, chunky rounded toy-like miniature look, thick soft shapes with smooth bevels, tilt-shift diorama feel, soft global illumination, warm key light with a soft blue fill, soft shadows, highly saturated but harmonious colors, clean readable silhouettes, rich detail density, polished like the best casual tycoon games on the App Store. Color palette: safety yellow #FFC400, construction orange #FF7A1A, tech blue #1FA0FF and cyan #27D3E6, money green #3DDC5A, alert red #F2453D, dark slate #1E2A38, cream #F6EFE2, blue-grey floor #D5DCE4. No black outlines on 3D objects, no text, no letters, no numbers, no watermark, no real brand logos, no photorealism, no violence. Cheerful, family-friendly.
```

## CIBLES (une ligne à ajouter après le bloc)

### A. Décors (concept art de référence, 9:16)
- **Monde 1, l'atelier** : `TARGET: isometric three-quarter top-down view of a small cheerful security-equipment workshop, cream walls with a yellow safety stripe, blue-grey tile floor, a short conveyor belt carrying cardboard crates into a blue-grey assembly machine with a glowing yellow top light, a wooden pallet stacked with crates, a service counter with a cash register, an open roller door showing a white delivery van and a parking lot outside, shelves full of boxes, dome security cameras on the ceiling, plants, cables on the floor, a neon sign shape without text. Chunky, rounded, filled edge to edge, no empty floor. A small worker in an orange high-visibility vest and yellow hard hat carries a crate.`
- **Monde 2, chantiers chez les clients** : `TARGET: same style, a charming suburban street with small houses and shops, a technician van with a ladder on the roof, a technician installing a dome camera on a facade, alarm sirens, cables, toolbox, trees, sunny sky, tilt-shift miniature.`
- **Monde 3, banque et centre commercial** : `TARGET: same style, a modern bank lobby and a mall corridor, a control room with a wall of glowing monitors, a big round vault door, access-control turnstiles, polished floors with reflections, warm lights.`
- **Monde 4, ville connectée** : `TARGET: same style, a small smart city district at dusk, sensor poles, patrol drones in the air, glowing cyan data lines, camera towers, a control tower, neon accents.`
- **Monde 5, base du futur** : `TARGET: same style, a futuristic security base, holographic displays, biometric scanners, a satellite dish, robots, sleek white and cyan architecture with yellow accents.`

### B. Machines et accessoires (fond blanc uni pour détourage)
`TARGET: a clean sprite sheet of 12 isolated game props in a 4x3 grid, generous spacing, each on a plain pure white background, three-quarter view: conveyor belt segment, blue-grey assembly machine with yellow top light, testing station with a small screen, packing machine, wooden pallet with cardboard crates, service counter with cash register, white delivery van, dome security camera, badge reader, alarm siren, server rack, shelf full of boxes.`

### C. Personnages
- **Technicien (héros)** : `TARGET: character turnaround sheet (front, side, back) of a friendly adult security technician, orange high-visibility vest with reflective bands, yellow hard hat, navy work trousers, tool belt, sturdy boots, stylized proportions with a slightly large head, relaxed A-pose, plain pure white background.`
- **Clients (portraits du panneau de commandes)** : `TARGET: six bust portraits in a 3x2 grid of diverse friendly clients (bank manager, shop owner, mayor, school director, restaurant owner, warehouse manager), each with a different pastel gradient background, expressive faces, same art style, no text.`
- **Variantes de tenue par monde** : `TARGET: the same technician in 5 outfits for 5 worlds (workshop vest, customer-site polo and cap, bank suit with badge, smart-city jacket with glowing cyan details, futuristic white suit), full body, white background.`

### D. Icônes d'interface (détourage ensuite)
`TARGET: a set of 16 game UI icons in a 4x4 grid on a plain pure white background, generous spacing, each icon filled, rounded, glossy cartoon style with a thick dark navy (#0E1620) outline and a subtle highlight: money bill, star (premium points), cardboard crate, gear, up arrow (upgrade), clipboard (orders), delivery van, wrench, dome camera, badge reader, alarm siren, padlock, heart, stopwatch, trophy, gift box.`

### E. Logo et icône d'application
- **Icône d'app (1024×1024, 1:1)** : `TARGET: mobile app icon, a friendly chunky shield with a glowing cyan dome-camera lens in the center, orange and yellow safety stripes, soft glow, bright blue radial gradient background, rounded 3D cartoon, centered, no text.`
- **Logo avec texte (vérifier l'orthographe, sinon on le compose dans le jeu)** : `TARGET: game logo for "Idle Sécurité", fat rounded condensed letters in white with a thick dark navy outline and a yellow-orange drop shadow, a small shield with a camera lens next to it, on a plain background.`

### F. Écran titre, fiche App Store, bandeaux
- **Écran titre** : `TARGET: key art, portrait, the technician hero in the foreground waving, the cheerful workshop diorama behind with conveyors running, confetti, golden light rays, empty space at the top for a logo.`
- **Captures de la fiche** : `TARGET: promotional background, vertical, warm orange wooden-style banner area at the bottom left empty for text, a bright stylized factory scene above.`

### G. Textures (carrelables)
`TARGET: seamless tileable top-down texture, glossy blue-grey ceramic floor tiles with light grout, subtle wear, stylized hand-painted, square.` — variantes : `painted cream wall panels with a yellow safety stripe`, `diamond-plate metal floor`, `wooden pallet planks`, `grass`, `asphalt with white parking lines`.

### H. Vidéo (Grok Imagine vidéo, 6 à 10 s)
- `TARGET (video): slow cinematic orbit around a cheerful stylized miniature workshop diorama, conveyors moving cardboard crates, machine lights blinking, small workers carrying boxes, a confetti burst at the end, loopable, no text.`
- `TARGET (video): a vertical gameplay-style teaser, a technician runs and stacks crates on his back, money bills fly into a counter, a new machine pops in with a spring animation, bright and punchy, no text.`

## Après la génération
- **Enregistrer** les images sur le téléphone et les **envoyer dans la conversation Claude** : je les range dans `unity/art/` (nommage : `monde1-decor-01.png`, `icones-01.png`, `logo-01.png`…) et Claude Code local s'en sert.
- **Utilisables tout de suite dans le jeu** : icônes (après détourage), portraits de clients, logo, icône d'app, écran titre, textures, bandeaux de la fiche App Store. **Références seulement** (pas importables telles quelles) : décors et machines, que Claude Code local reproduit en 3D simple en s'en inspirant.
- **Droits** : vérifier dans les conditions de Grok/xAI que les images générées sont utilisables commercialement avant la publication ; ne jamais demander « dans le style de [jeu précis] » ni de personnage existant.
