# Prompts pour générer les modèles 3D

Jeu d'invites prêtes à coller dans un générateur 3D (Meshy, Tripo, Rodin, Luma
Genie, Sloyd…) pour produire les maillages de **Sûreté Tycoon**.

## Deux partis pris, avant de commencer

**Les prompts sont en anglais.** Tous ces outils sont entraînés sur des corpus
anglophones : la même demande en français rend des formes molles et ignore la
moitié des contraintes. Les explications restent en français, seul le texte à
copier est en anglais.

**Ils visent la maquette A (WebGL).** Des modèles 3D ne servent que là : la
maquette B est construite en CSS et ne sait pas charger de maillage. Si le choix
se porte finalement sur B, ce document devient sans objet.

Les dimensions sont données en mètres et correspondent **exactement** aux unités
de `demo/webgl.html` (1 unité = 1 m) : bâtiment 8,4 × 5,2 m, étage 2,75 m sous
plafond, cage d'ascenseur 1,5 m de large. Un modèle importé à ces cotes se pose
sans réglage.

---

## 1. Le bloc de style, à coller dans chaque prompt

C'est lui qui tient la cohérence de tout le jeu. Il ne change **jamais** d'un
asset à l'autre — c'est la seule façon d'obtenir un lot homogène.

```
STYLE: stylized low-poly mobile game asset, chunky simplified forms, soft
rounded edges, clean flat-shaded surfaces, bright saturated candy colors with
a light sky-blue and warm sand palette, no visible seams, matte plastic-toy
finish, subtle ambient occlusion only, isometric tycoon game art direction,
consistent with a cheerful daytime city builder, single object centered on the
origin, neutral studio lighting, plain background.
```

À ajouter systématiquement à la fin, après la description de l'objet.

## 2. Le bloc d'exclusion (negative prompt)

```
NEGATIVE: photorealistic, hyperrealistic, PBR metal roughness detail, rust,
grime, scratches, dirt, text, letters, logos, watermark, baked shadows, baked
lighting, ground plane, base platform, pedestal, human hands, extra limbs,
noisy topology, spiky artifacts, floating disconnected parts, transparent glass
refraction, dark moody lighting, horror, ruins.
```

## 3. Contraintes techniques communes

À exiger à l'export, quel que soit l'outil :

| Réglage | Valeur | Pourquoi |
| --- | --- | --- |
| Format | **glTF binaire (.glb)** | seul format que `GLTFLoader` charge sans conversion |
| Orientation | **Y vers le haut**, face avant vers **+Z** | convention Three.js ; évite un `rotation.x` correctif partout |
| Pivot | **au sol, centré** en X/Z | on positionne par `position.y = hauteur du plancher` |
| Échelle | **1 unité = 1 mètre** | les cotes ci-dessous tombent juste |
| Textures | **une seule atlas 512², sans normal map** | un idle game tourne en arrière-plan sur des téléphones modestes |
| Matériau | couleurs à plat, pas de métal/rugosité | cohérent avec `MeshLambertMaterial` déjà utilisé |

Budget en triangles : **bâtiment 3 000–5 000**, **personnage 1 500–2 500**,
**meuble 300–800**, **petit accessoire 150–400**. Au-delà, la scène s'effondre
sur mobile — c'est la contrainte la plus souvent ignorée par les générateurs,
il faut la répéter dans le prompt *et* passer un décimateur au retour.

---

## 4. Architecture

### Module d'étage (l'asset le plus important — à faire en premier)

```
An open-front office floor module for a cutaway tycoon building: a flat
rectangular floor slab, a solid back wall, two solid side walls, and no front
wall so the interior is fully visible. A rectangular window opening is cut into
the back wall. Clean architectural shapes, thick chunky wall edges, pale
interior walls. 8.4 m wide, 5.2 m deep, 2.75 m tall.
+ STYLE + NEGATIVE
```
Cotes : 8,4 × 5,2 × 2,75 m · ~1 500 tris · pivot au niveau du plancher.

> **Faites-le valider avant tout le reste.** C'est lui qui fixe la palette, la
> dureté des arêtes et le grain des surfaces. Une fois satisfait, servez-vous
> de son rendu comme **image de référence** pour les assets suivants (voir §9).

### Cage d'ascenseur

```
A modern elevator shaft column for a stylized office building: a slim vertical
enclosure with a light glass front panel and pale metal frame, plus a separate
elevator cab with double doors and a small interior ceiling light. 1.5 m wide,
4.2 m deep, 2.75 m tall for one floor section.
+ STYLE + NEGATIVE
```
Deux fichiers séparés : `shaft-section.glb` (empilable) et `elevator-cab.glb`
(déplacé verticalement par le jeu). ~600 tris chacun.

### Rez-de-chaussée / hall d'accueil

```
The ground floor lobby of a stylized corporate building: a wide glass entrance
facade with double doors, a small canopy above the entrance, and a blank sign
panel over the doorway. Bright welcoming look. 8.4 m wide, 5.2 m deep, 2.6 m
tall.
+ STYLE + NEGATIVE
```
Le panneau d'enseigne reste **vierge** : le nom de l'entreprise sera écrit en
surcouche HTML, pour rester lisible et traduisible.

### Toit achevé

```
A flat rooftop for a stylized office tower: a low parapet wall, two boxy air
conditioning units, a small rooftop access hatch, and a slim antenna mast with
a red beacon light on top. 8.4 m wide, 5.2 m deep.
+ STYLE + NEGATIVE
```

### Chantier du prochain étage

```
A construction site deck on top of a building: an unfinished concrete slab with
exposed rebar stubs, yellow and black hazard-striped safety barriers around the
edge, a stack of construction materials, and orange traffic cones. Cheerful
cartoon construction look. 8.4 m wide, 5.2 m deep, 1.2 m tall.
+ STYLE + NEGATIVE
```

### Grue à tour

```
A yellow tower crane: a lattice vertical mast, a horizontal jib with a counter
jib, a hook block hanging from a cable, and a small operator cabin. Simplified
chunky lattice, no thin wires. 14 m tall, jib 10 m long.
+ STYLE + NEGATIVE
```
Exporter la flèche comme **objet enfant nommé `jib`** pour pouvoir la faire
pivoter dans le jeu.

---

## 5. Le personnel

Un seul personnage de base, décliné ensuite par la couleur de tenue et un
accessoire. C'est bien plus rapide et infiniment plus cohérent que dix
personnages générés séparément.

### Personnage de base

```
A stylized low-poly office worker character in a neutral T-pose: chunky
simplified proportions, oversized rounded head, no facial features except two
simple dot eyes, simple one-piece work uniform, plain shoes, mitten-style hands
with no separate fingers. Gender-neutral silhouette. 1.7 m tall.
+ STYLE + NEGATIVE
```
~2 000 tris. Demander explicitement **une armature humanoïde (rig)** si l'outil
le propose (Rodin et Meshy le font) — sinon une simple animation de va-et-vient
avec balancement suffit, c'est ce que fait déjà la maquette.

### Les dix tenues

Reprendre le prompt de base en remplaçant la phrase d'uniforme :

| Métier | Phrase à substituer |
| --- | --- |
| Accueil | `wearing a smart amber blazer with a visitor badge on a lanyard` |
| Pose de caméras | `wearing a blue work jacket, a tool belt and a white hard hat` |
| Câblage réseau | `wearing a green polo shirt and a tool belt with cable coils` |
| Alarmes intrusion | `wearing a red technician jacket and carrying a small toolbox` |
| Contrôle d'accès | `wearing a purple shirt with an access badge clipped to the chest` |
| Détection incendie | `wearing an orange high-visibility jacket with reflective bands` |
| Télésurveillance | `wearing a teal operator shirt and a headset with a microphone` |
| Cybersécurité | `wearing an indigo hoodie over a shirt` |
| Analyse vidéo IA | `wearing a magenta lab coat over casual clothes` |
| Audit & formation | `wearing an olive green suit jacket and holding a clipboard` |

### Chefs de service

```
[prompt de base] ... wearing a dark navy business suit with a tie, standing
confidently with arms crossed. Slightly taller and broader than the standard
worker.
+ STYLE + NEGATIVE
```
Un seul suffit : il apparaît en vignette dans le panneau d'étage.

---

## 6. Le mobilier, métier par métier

Un objet par prompt. Les générateurs échouent presque toujours quand on leur
demande une scène entière — ils rendent une bouillie fusionnée, impossible à
placer. Assemblez le décor dans le jeu, pas dans le prompt.

**Accueil & filtrage visiteurs**
```
A curved reception desk with a raised counter and a small computer monitor. 2.0 × 0.8 × 1.1 m.
A waiting area sofa with two seats and thick rounded cushions. 1.6 × 0.7 × 0.8 m.
A security turnstile gate with two glass swing panels and a badge reader pillar. 1.2 × 0.6 × 1.0 m.
A tall potted plant with broad simple leaves in a terracotta pot. 0.6 × 0.6 × 1.3 m.
```

**Pose de caméras**
```
A folded aluminium step ladder leaning against a wall. 0.7 × 0.5 × 2.0 m.
A stack of three cardboard shipping boxes of slightly different sizes. 0.8 × 0.6 × 1.0 m.
A wall-mounted dome security camera on a short bracket. 0.3 × 0.3 × 0.25 m.
A bullet security camera on an articulated wall arm. 0.45 × 0.15 × 0.2 m.
A metal shelving unit holding camera boxes and coiled cables. 1.4 × 0.5 × 1.9 m.
```

**Câblage & réseau IP**
```
A large wooden cable drum with thick coiled black cable. 0.9 × 0.9 × 0.9 m.
An open wall-mounted patch panel with rows of colored network ports and cables. 0.6 × 0.2 × 0.5 m.
A perforated cable tray section with bundled cables running through it. 1.5 × 0.3 × 0.15 m.
A small network switch cabinet with a glass door and blinking status lights. 0.7 × 0.7 × 1.4 m.
```

**Alarmes intrusion**
```
A wall-mounted alarm control panel with a small keypad and screen. 0.4 × 0.12 × 0.35 m.
A workbench with scattered small electronic components and a soldering station. 1.6 × 0.7 × 0.9 m.
An outdoor alarm siren box with a flashing beacon on top. 0.3 × 0.15 × 0.4 m.
A ceiling-mounted motion detector, small wedge shape. 0.15 × 0.15 × 0.1 m.
```

**Contrôle d'accès & badges**
```
A freestanding door frame mockup with a magnetic lock and a badge reader beside it. 1.1 × 0.3 × 2.1 m.
A badge printing station on a small desk with a card printer. 1.0 × 0.6 × 1.1 m.
A wall-mounted key cabinet with a grid of hooks behind a glass door. 0.6 × 0.15 × 0.7 m.
```

**Détection incendie**
```
A wall-mounted fire alarm control panel with a red housing and small display. 0.6 × 0.15 × 0.5 m.
A red fire extinguisher on a floor stand. 0.25 × 0.25 × 0.8 m.
A red fire hose reel cabinet mounted on a wall. 0.7 × 0.25 × 0.7 m.
A ceiling smoke detector, simple flat disc. 0.14 × 0.14 × 0.05 m.
```

**Centre de télésurveillance**
```
A control room video wall made of nine screens in a three by three grid on a slim frame. 3.0 × 0.2 × 1.8 m.
An operator desk with three curved monitors side by side and a keyboard. 1.8 × 0.8 × 1.2 m.
An ergonomic office chair with a high mesh back and five-star base on castors. 0.7 × 0.7 × 1.2 m.
A countertop coffee machine with two cups beside it. 0.4 × 0.4 × 0.5 m.
```

**Cybersécurité des flux vidéo**
```
A full-height server rack with mounted blade servers and rows of small status LEDs. 0.6 × 0.9 × 2.0 m.
A standing desk with two monitors showing abstract dashboards. 1.4 × 0.7 × 1.3 m.
A whiteboard on wheels with simple abstract diagram shapes drawn on it. 1.6 × 0.5 × 1.8 m.
```

**Analyse vidéo IA**
```
A GPU compute rack with thick cooling fans and a glowing accent strip. 0.7 × 1.0 × 2.0 m.
A large wall display showing abstract tracking boxes over a blank scene. 2.2 × 0.1 × 1.3 m.
A camera calibration rig: a tripod holding a camera aimed at a checkerboard panel. 1.0 × 1.0 × 1.8 m.
```

**Audit & formation sûreté**
```
A training room table with four small chairs around it. 2.2 × 1.2 × 0.8 m.
A whiteboard with a ceiling projector mounted above it on an arm. 2.0 × 0.6 × 1.9 m.
A bookshelf filled with binders and folders of varied colors. 1.2 × 0.4 × 1.8 m.
```

---

## 7. Extérieur

```
A compact white service van with a blue side stripe, sliding side door, and a
small roof light bar. Chunky cartoon proportions, oversized rounded wheels.
5.0 × 2.0 × 2.3 m.
+ STYLE + NEGATIVE
```

```
A city sidewalk section with a curb, a street lamp post, and a small tree in a
square planter. 8.0 × 3.0 m.
+ STYLE + NEGATIVE
```

## 8. Objets d'interface en 3D

Ils tournent lentement dans le bandeau du haut ou jaillissent à l'encaissement.

```
A shiny golden euro coin with a raised euro symbol on both faces, thick rounded edge. 0.4 m diameter.
A five-pointed golden certification star with a beveled faceted surface. 0.4 m.
A laboratory flask with glowing violet liquid inside. 0.3 × 0.3 × 0.4 m.
A closed manila document folder with a small seal on the cover. 0.3 × 0.2 × 0.04 m.
```
+ STYLE + NEGATIVE à chaque fois.

---

## 9. La méthode qui donne un lot cohérent

Générer 60 objets à la suite donne 60 styles différents. L'ordre compte :

1. **Générer le module d'étage seul**, l'itérer jusqu'à ce qu'il plaise. Il fixe
   la direction artistique.
2. **En faire une image de référence** (une capture du rendu suffit).
3. **Passer tous les assets suivants en mode image + texte** quand l'outil le
   permet (Meshy, Tripo, Kaedim) : joindre la référence et écrire
   `match the art style, palette and edge treatment of the reference image`.
   C'est l'unique levier réellement efficace sur la cohérence.
4. **Verrouiller la graine (seed)** si l'outil l'expose, et ne plus y toucher.
5. **Décimer au retour** : les générateurs rendent volontiers 100 000 triangles
   pour une chaise. Passer chaque `.glb` dans gltfpack ou meshoptimizer avant
   de l'intégrer.

### Notes par outil

- **Meshy** — le plus complet : texte→3D, image→3D, préréglage `Stylized`, export
  glb direct. Le meilleur point de départ.
- **Tripo** — plus rapide et plus propre sur les objets isolés (mobilier,
  accessoires), moins à l'aise sur les personnages.
- **Rodin** — nettement meilleur sur les personnages, et sait sortir un rig.
- **Sloyd** — paramétrique : les maillages sortent déjà légers et propres, idéal
  pour le mobilier, mais le catalogue est limité.
- **Luma Genie** — gratuit, correct pour prototyper, moins régulier.

## 10. Intégration dans le jeu

Ranger les fichiers ainsi :

```
assets/models/
  building/   floor-module.glb  shaft-section.glb  elevator-cab.glb
              lobby.glb  roof.glb  construction-deck.glb  crane.glb
  crew/       worker-base.glb  worker-{metier}.glb  manager.glb
  props/      {metier}-{objet}.glb
  city/       van.glb  sidewalk.glb
  ui/         coin.glb  star.glb  flask.glb  folder.glb
```

Côté code, la maquette A construit aujourd'hui son décor avec des primitives
(`box(...)` dans `demo/webgl.html`). L'intégration consiste à remplacer chaque
primitive par un `GLTFLoader().loadAsync(...)` puis un `clone()` par
instance — la disposition, les positions et les cotes sont déjà en place et
n'ont pas à bouger.

Charger **une fois** chaque modèle et cloner : dix étages × huit employés font
quatre-vingts personnages, ce qui exige un maillage unique partagé (ou un
`InstancedMesh`) pour rester fluide.
