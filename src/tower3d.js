// La tour en 3D temps réel. Même rôle que tower.js — lire l'état et le donner
// à voir — mais rendu par WebGL au lieu du DOM.
//
// La scène est construite une seule fois puis synchronisée : les appels
// répétés de buildTower() n'ajoutent que les étages nouvellement bâtis, sans
// réinitialiser la caméra ni rejouer d'animation.

import { FLOORS } from './data.js';
import { cycleTime, cycleRevenue, floorPerSecond, milestoneMult, costOf } from './economy.js';
import { money, fmt, duration } from './format.js';
import { W, D, H, SHAFT, SKIN_TONES, KITS } from './models.js';

const MAX_CREW = 8;
const COIN_THROTTLE = 500;

let THREE = null;
let host = null;        // conteneur racine inséré dans la vue
let uiLayer = null;     // surcouche DOM (étiquettes, bouton de chantier)
let renderer = null;
let scene = null;
let camera = null;
let world = null;
let cab = null;
let cabTarget = 0;
let crane = null;
let buildDeck = null;
let roofMesh = null;
let shaftGlass = null;
let lastOpen = -1;

const floors = new Map();  // id -> { def, group, rail, crew, y, tag, crewSize }
let pickHandler = null;
let lastCoin = {};
let lastFrame = performance.now();

const BASE = H * 0.92 + 0.28;   // hauteur du plancher du premier étage

// ------------------------------------------------------------ démarrage

export function init(three) {
  THREE = three;
}

export function isReady() {
  return !!THREE;
}

export function setPickHandler(fn) {
  pickHandler = fn;
}

const mat = (color, opts) => new THREE.MeshLambertMaterial(Object.assign({ color }, opts || {}));
const box = (w, h, d, m) => new THREE.Mesh(new THREE.BoxGeometry(w, h, d), m);

function crewSize(count) {
  if (count <= 0) return 0;
  return Math.min(MAX_CREW, 1 + Math.floor(count / 6));
}

export function openFloors(state) {
  return FLOORS.filter((d) => (state.floors[d.id]?.count || 0) > 0).length;
}

// -------------------------------------------------------- construction

export function buildTower(container, state) {
  if (!host || host.parentNode !== container) createScene(container);
  syncFloors(state);
  syncRoof(state);
  layoutTags(state);
}

function createScene(container) {
  container.innerHTML = '';
  host = document.createElement('div');
  host.className = 't3d';
  host.innerHTML = '<div class="t3d-canvas"></div><div class="t3d-ui"></div>';
  container.appendChild(host);
  uiLayer = host.querySelector('.t3d-ui');

  renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  host.querySelector('.t3d-canvas').appendChild(renderer.domElement);

  scene = new THREE.Scene();
  scene.background = skyTexture();
  scene.fog = new THREE.Fog(0xbcd9ef, 55, 130);

  camera = new THREE.PerspectiveCamera(42, 1, 0.1, 220);

  scene.add(new THREE.HemisphereLight(0xdff1ff, 0x6b7f96, 1.25));
  const sun = new THREE.DirectionalLight(0xfff3d6, 1.5);
  sun.position.set(11, 22, 12);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  sun.shadow.camera.left = -18; sun.shadow.camera.right = 18;
  sun.shadow.camera.top = 34; sun.shadow.camera.bottom = -8;
  sun.shadow.camera.far = 80;
  sun.shadow.bias = -0.0012;
  scene.add(sun);

  world = new THREE.Group();
  scene.add(world);
  buildGround();
  buildLobby();
  buildShaft();
  buildStreet();

  floors.clear();
  bindPointer();
  resize();
  addEventListener('resize', resize);
}

function skyTexture() {
  const c = document.createElement('canvas');
  c.width = 2; c.height = 256;
  const ctx = c.getContext('2d');
  const g = ctx.createLinearGradient(0, 0, 0, 256);
  g.addColorStop(0, '#4aa3e0');
  g.addColorStop(0.55, '#9fd2f2');
  g.addColorStop(1, '#e6f4fd');
  ctx.fillStyle = g; ctx.fillRect(0, 0, 2, 256);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

function buildGround() {
  const ground = box(120, 0.6, 120, mat(0x6f7f92));
  ground.position.y = -0.3;
  ground.receiveShadow = true;
  world.add(ground);

  const pavement = box(W + 7, 0.35, D + 7, mat(0xa9b6c4));
  pavement.position.y = 0.1;
  pavement.receiveShadow = true;
  world.add(pavement);
}

function buildStreet() {
  const road = box(120, 0.02, 7, mat(0x54606f));
  road.position.set(0, 0.32, D / 2 + 8.6);
  road.receiveShadow = true;
  world.add(road);
  for (let i = -8; i <= 8; i++) {
    const dash = box(1.6, 0.02, 0.22, mat(0xf2c94c));
    dash.position.set(i * 3.4, 0.35, D / 2 + 8.6);
    world.add(dash);
  }

  const van = new THREE.Group();
  const body = box(3.1, 1.3, 1.5, mat(0xf1f5f9));
  body.position.y = 0.95; body.castShadow = true;
  van.add(body);
  const cabin = box(1.1, 0.9, 1.45, mat(0xdbe6f2));
  cabin.position.set(1.6, 0.75, 0); cabin.castShadow = true;
  van.add(cabin);
  const stripe = box(3.1, 0.22, 1.52, mat(0x2563eb));
  stripe.position.y = 0.72;
  van.add(stripe);
  for (const [wx, wz] of [[-0.9, 0.78], [-0.9, -0.78], [1.3, 0.78], [1.3, -0.78]]) {
    const wheel = new THREE.Mesh(new THREE.CylinderGeometry(0.32, 0.32, 0.18, 14), mat(0x1f2937));
    wheel.rotation.x = Math.PI / 2;
    wheel.position.set(wx, 0.32, wz);
    van.add(wheel);
  }
  van.position.set(-2.4, 0.3, D / 2 + 3.4);
  world.add(van);
}

function buildLobby() {
  const shell = box(W, H * 0.92, D, mat(0xe7eef6));
  shell.position.y = H * 0.46 + 0.28;
  shell.castShadow = shell.receiveShadow = true;
  world.add(shell);

  const glass = box(W - 1.2, H * 0.6, 0.12,
    new THREE.MeshLambertMaterial({ color: 0x8fd0f8, transparent: true, opacity: 0.75 }));
  glass.position.set(0, H * 0.42 + 0.28, D / 2 + 0.02);
  world.add(glass);

  const sign = box(4.2, 0.62, 0.16, mat(0xffffff));
  sign.position.set(0, H * 0.92 + 0.05, D / 2 + 0.08);
  world.add(sign);
  const bar = box(4.2, 0.16, 0.2, mat(0x2563eb));
  bar.position.set(0, H * 0.92 - 0.18, D / 2 + 0.1);
  world.add(bar);
}

function buildShaft() {
  // Hauteur unitaire : on l'étire ensuite selon le nombre d'étages bâtis.
  shaftGlass = box(SHAFT - 0.1, 1, D - 0.5,
    new THREE.MeshLambertMaterial({ color: 0xbfe4ff, transparent: true, opacity: 0.26 }));
  shaftGlass.position.set(-W / 2 + SHAFT / 2, BASE, 0);
  world.add(shaftGlass);

  cab = box(SHAFT - 0.45, 1.5, D - 1.4, mat(0xf8fafc));
  cab.castShadow = true;
  cab.position.set(-W / 2 + SHAFT / 2, BASE + 0.9, 0);
  cabTarget = cab.position.y;
  world.add(cab);
}

// ------------------------------------------------------------- étages

function syncFloors(state) {
  const open = openFloors(state);

  for (let i = 0; i < open; i++) {
    const def = FLOORS[i];
    if (!floors.has(def.id)) floors.set(def.id, makeFloor(def, i));
    const f = floors.get(def.id);
    const count = state.floors[def.id].count;
    if (crewSize(count) !== f.crewSize) paintCrew(f, count);
  }

  // Une certification remet l'entreprise à zéro : on retire les étages perdus.
  for (const [id, f] of floors) {
    const idx = FLOORS.findIndex((d) => d.id === id);
    if (idx < open) continue;
    world.remove(f.group);
    f.tag.remove();
    floors.delete(id);
  }
}

function makeFloor(def, index) {
  const y = BASE + index * H;
  const g = new THREE.Group();
  g.position.y = y;

  const slab = box(W, 0.26, D, mat(parseInt(def.wall.slice(1), 16)));
  slab.position.y = 0.13;
  slab.castShadow = slab.receiveShadow = true;
  slab.userData.floorId = def.id;
  g.add(slab);

  const back = box(W, H - 0.26, 0.22, mat(parseInt(def.wall.slice(1), 16)));
  back.position.set(0, (H - 0.26) / 2 + 0.26, -D / 2 + 0.11);
  back.castShadow = back.receiveShadow = true;
  g.add(back);

  for (const s of [-1, 1]) {
    const side = box(0.22, H - 0.26, D, mat(0xeef3f8));
    side.position.set(s * (W / 2 - 0.11), (H - 0.26) / 2 + 0.26, 0);
    side.castShadow = side.receiveShadow = true;
    g.add(side);
  }

  const win = box(2.4, 1.15, 0.08,
    new THREE.MeshLambertMaterial({ color: 0x9ad6fb, emissive: 0x22405c, emissiveIntensity: 0.5 }));
  win.position.set(W / 2 - 2.3, 1.55, -D / 2 + 0.2);
  g.add(win);

  const wallShaft = box(0.16, H - 0.26, D - 0.4, mat(0xc3cedb));
  wallShaft.position.set(-W / 2 + SHAFT, (H - 0.26) / 2 + 0.26, 0);
  wallShaft.castShadow = true;
  g.add(wallShaft);

  const left = -W / 2 + SHAFT + 0.5;
  const right = W / 2 - 0.6;
  furnish(g, def, left, right);

  const railBg = box(W - 0.3, 0.13, 0.16, mat(0x94a3b8));
  railBg.position.set(0, 0.2, D / 2 - 0.02);
  g.add(railBg);
  const accent = parseInt(def.accent.slice(1), 16);
  const rail = box(1, 0.16, 0.2, mat(accent));
  rail.position.set(0, 0.2, D / 2 - 0.01);
  g.add(rail);

  world.add(g);

  const tag = document.createElement('button');
  tag.type = 'button';
  tag.className = 't3d-tag';
  tag.dataset.act = 'panel';
  tag.dataset.id = def.id;
  tag.innerHTML = `<span>${def.icon}</span><b class="nm">${def.name}</b><i class="st"></i>`;
  uiLayer.appendChild(tag);

  return { def, group: g, rail, railWidth: W - 0.3, crew: [], crewSize: -1, y, tag, left, right };
}

function furnish(g, def, left, right) {
  const kind = KITS[def.id]?.kind || 'bureau';
  const put = (m, x, y, z) => { m.position.set(x, y + 0.26, z); m.castShadow = true; g.add(m); return m; };
  const span = right - left;
  const accent = parseInt(def.accent.slice(1), 16);

  if (kind === 'bureau' || kind === 'pc') {
    for (let i = 0; i < 3; i++) {
      const x = left + 0.7 + (i * span) / 3;
      put(box(1.15, 0.08, 0.7, mat(0xd8bd94)), x, 0.74, -0.9);
      for (const [dx, dz] of [[-0.5, -0.3], [0.5, -0.3], [-0.5, 0.3], [0.5, 0.3]]) {
        put(box(0.07, 0.7, 0.07, mat(0x8d7351)), x + dx, 0.35, -0.9 + dz);
      }
      const screen = put(box(0.62, 0.4, 0.05,
        new THREE.MeshLambertMaterial({ color: 0x111827, emissive: accent, emissiveIntensity: 0.55 })),
        x, 1.0, -1.15);
      screen.rotation.x = -0.12;
      put(box(0.4, 0.35, 0.4, mat(0x475569)), x, 0.18, -0.35);
    }
  }
  if (kind === 'atelier') {
    for (let i = 0; i < 4; i++) {
      put(box(0.55, 0.55, 0.55, mat(i % 2 ? 0xb98b57 : 0xcfa06a)), left + 0.6 + i * 0.75, 0.28, -1.5);
    }
    for (const dx of [0, 0.45]) {
      put(box(0.16, 1.9, 0.16, mat(0xd6a453)), right - 1.6 + dx, 0.95, -1.2);
    }
    for (let i = 0; i < 4; i++) {
      put(box(0.5, 0.06, 0.16, mat(0xd6a453)), right - 1.38, 0.35 + i * 0.42, -1.2);
    }
    const dome = new THREE.Mesh(new THREE.SphereGeometry(0.22, 16, 12), mat(0x1f2937));
    put(dome, right - 0.7, 1.9, -0.8);
  }
  if (kind === 'baie') {
    for (let i = 0; i < 4; i++) {
      const rack = put(box(0.62, 1.75, 0.7, mat(0x334155)), left + 0.7 + i * 0.85, 0.88, -1.4);
      for (let k = 0; k < 6; k++) {
        const led = box(0.5, 0.05, 0.03,
          new THREE.MeshBasicMaterial({ color: k % 2 ? 0x22c55e : 0x38bdf8 }));
        led.position.set(rack.position.x, 0.45 + k * 0.25, -1.04);
        g.add(led);
      }
    }
  }

  put(box(0.3, 0.3, 0.3, mat(0xc2703f)), right - 0.35, 0.15, 1.3);
  const leaf = new THREE.Mesh(new THREE.IcosahedronGeometry(0.34, 0), mat(0x3f9d4f));
  put(leaf, right - 0.35, 0.62, 1.3);
}

function paintCrew(f, count) {
  const size = crewSize(count);
  f.crewSize = size;

  for (const p of f.crew) f.group.remove(p);
  f.crew = [];

  const wear = f.def.wear.map((c) => parseInt(c.slice(1), 16));
  for (let k = 0; k < size; k++) {
    const p = person(wear[k % wear.length], SKIN_TONES[(k * 3) % SKIN_TONES.length]);
    const from = f.left + 0.3 + Math.random() * Math.max(0.6, f.right - f.left - 1.6);
    p.userData = {
      from,
      to: Math.min(f.right - 0.35, from + 0.8 + Math.random() * 1.6),
      speed: 0.5 + Math.random() * 0.5,
      phase: Math.random() * Math.PI * 2,
      z: -D / 2 + 1.5 + Math.random() * (D - 2.4),
    };
    p.position.set(from, 0.26, p.userData.z);
    f.group.add(p);
    f.crew.push(p);
  }
}

function person(wear, skin) {
  const p = new THREE.Group();
  const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.17, 0.42, 4, 10), mat(wear));
  body.position.y = 0.46;
  body.castShadow = true;
  p.add(body);
  const head = new THREE.Mesh(new THREE.SphereGeometry(0.165, 14, 12), mat(skin));
  head.position.y = 0.87;
  head.castShadow = true;
  p.add(head);
  return p;
}

// ------------------------------------------------- toit et chantier

function syncRoof(state) {
  const open = openFloors(state);
  const y = BASE + open * H;

  if (!roofMesh) {
    roofMesh = box(W, 0.26, D, mat(0xcbd5e1));
    roofMesh.castShadow = roofMesh.receiveShadow = true;
    world.add(roofMesh);
  }
  roofMesh.position.y = y + 0.13;

  const finished = open >= FLOORS.length;

  if (!buildDeck) {
    buildDeck = new THREE.Group();
    for (const sx of [-1, 1]) {
      for (const sz of [-1, 1]) {
        const post = box(0.12, H, 0.12, mat(0xf59e0b));
        post.position.set(sx * (W / 2 - 0.4), H / 2 + 0.26, sz * (D / 2 - 0.4));
        post.castShadow = true;
        buildDeck.add(post);
      }
    }
    for (const sz of [-1, 1]) {
      const bar = box(W - 0.8, 0.1, 0.1, mat(0xfbbf24));
      bar.position.set(0, H * 0.75, sz * (D / 2 - 0.4));
      buildDeck.add(bar);
    }
    world.add(buildDeck);
  }
  buildDeck.position.y = y;
  buildDeck.visible = !finished;

  if (shaftGlass) {
    const height = Math.max(H, open * H) + 0.4;
    shaftGlass.scale.y = height;
    shaftGlass.position.y = BASE + height / 2 - 0.2;
  }

  if (!crane) {
    crane = new THREE.Group();
    const mast = box(0.28, 9, 0.28, mat(0xf59e0b));
    mast.position.set(-W / 2 - 2.6, -1.5, -D / 2 - 1.2);
    mast.castShadow = true;
    crane.add(mast);
    const jib = box(7.5, 0.22, 0.22, mat(0xfbbf24));
    jib.position.set(-W / 2 + 0.6, 3.1, -D / 2 - 1.2);
    jib.castShadow = true;
    crane.add(jib);
    const load = box(0.7, 0.5, 0.7, mat(0x94a3b8));
    load.position.set(-W / 2 + 1.6, 0.9, -D / 2 - 1.2);
    load.castShadow = true;
    crane.add(load);
    world.add(crane);
  }
  crane.position.y = y;
  crane.visible = !finished;

  if (open !== lastOpen) {
    lastOpen = open;
    frameTo(open);
  }
}

/** Recule et recentre à mesure que le bâtiment prend de la hauteur. */
function frameTo(open) {
  if (!camera || !orbit.target) return;
  const top = BASE + Math.max(1, open) * H + 3.2;
  orbit.target.set(0, top / 2, 0);
  orbit.radius = fitRadius(top);
  applyCamera();
}

// ------------------------------------------------- surcouche DOM

export function updateBuildSlot(state, container) {
  if (!uiLayer) return;
  const open = openFloors(state);
  let slot = uiLayer.querySelector('.build3d');

  if (open >= FLOORS.length) {
    if (slot) slot.remove();
    return;
  }

  const def = FLOORS[open];
  const price = costOf(def, 0, 1);
  if (!slot) {
    slot = document.createElement('button');
    slot.type = 'button';
    slot.className = 'build3d';
    uiLayer.appendChild(slot);
  }
  slot.dataset.act = 'build';
  slot.dataset.id = def.id;
  slot.innerHTML = `<span class="bt">🏗️ ${def.icon} ${def.name}</span>
    <span class="bp">${money(price)}</span>`;
  slot.classList.toggle('ready', state.cash >= price);
}

// Rien qui touche à THREE ne peut vivre au niveau module : la bibliothèque
// n'est chargée qu'à la demande, bien après l'évaluation de ce fichier.
const anchor = () => new THREE.Vector3();
let tmp = null;

function project(x, y, z) {
  if (!tmp) tmp = new THREE.Vector3();
  tmp.set(x, y, z).project(camera);
  const r = renderer.domElement;
  return {
    x: (tmp.x * 0.5 + 0.5) * r.clientWidth,
    y: (-tmp.y * 0.5 + 0.5) * r.clientHeight,
    visible: tmp.z < 1,
  };
}

// Le bouton de chantier occupe le bas de l'écran : les étiquettes n'y descendent pas.
const TAG_BOTTOM_GUARD = 96;

function layoutTags(state) {
  if (!camera) return;
  const wBox = renderer.domElement.clientWidth;
  const hBox = renderer.domElement.clientHeight;

  for (const f of floors.values()) {
    const p = project(-W / 2 - 0.6, f.y + 1.5, D / 2 + 0.4);
    const inFrame = p.visible && p.y < hBox - TAG_BOTTOM_GUARD && p.y > 4;
    if (!inFrame) { f.tag.style.display = 'none'; continue; }
    f.tag.style.display = '';
    const tw = f.tag.offsetWidth || 150;
    f.tag.style.left = Math.max(8, Math.min(wBox - tw - 8, p.x - tw)) + 'px';
    f.tag.style.top = p.y + 'px';
  }

}

// ----------------------------------------------------------- caméra

const orbit = { theta: 0.48, phi: 1.28, radius: 32, target: null };
let framed = false;

function applyCamera() {
  if (!camera) return;
  orbit.phi = Math.max(0.5, Math.min(1.5, orbit.phi));
  orbit.radius = Math.max(12, Math.min(70, orbit.radius));
  const t = orbit.target;
  camera.position.set(
    t.x + orbit.radius * Math.sin(orbit.phi) * Math.sin(orbit.theta),
    t.y + orbit.radius * Math.cos(orbit.phi),
    t.z + orbit.radius * Math.sin(orbit.phi) * Math.cos(orbit.theta)
  );
  camera.lookAt(t);
}

function fitRadius(top) {
  const vFov = (camera.fov * Math.PI) / 180;
  const byHeight = (top * 0.6) / Math.tan(vFov / 2);
  const hFov = 2 * Math.atan(Math.tan(vFov / 2) * camera.aspect);
  const byWidth = (Math.hypot(W, D) / 2 + 1.2) / Math.tan(hFov / 2);
  return Math.max(byHeight, byWidth);
}

function resize() {
  if (!renderer || !host) return;
  const w = host.clientWidth || 1;
  const h = host.clientHeight || 1;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  camera.updateProjectionMatrix();
  if (!orbit.target) orbit.target = anchor();
  if (!framed) { framed = true; frameTo(lastOpen < 0 ? 1 : lastOpen); }
  applyCamera();
}

function bindPointer() {
  const el = renderer.domElement;
  const pointers = new Map();
  let pinch = 0;
  let dragged = false;

  el.addEventListener('pointerdown', (e) => {
    el.setPointerCapture(e.pointerId);
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    dragged = false;
  });

  el.addEventListener('pointermove', (e) => {
    const prev = pointers.get(e.pointerId);
    if (!prev) return;
    const dx = e.clientX - prev.x, dy = e.clientY - prev.y;
    pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (Math.abs(dx) + Math.abs(dy) > 3) dragged = true;

    if (pointers.size >= 2) {
      const [a, b] = [...pointers.values()];
      const dist = Math.hypot(a.x - b.x, a.y - b.y);
      if (pinch) orbit.radius *= pinch / dist;
      pinch = dist;
    } else {
      orbit.theta -= dx * 0.008;
      orbit.phi -= dy * 0.006;
    }
    applyCamera();
  });

  el.addEventListener('pointerup', (e) => {
    if (!dragged && pointers.size === 1) pick(e);
    pointers.delete(e.pointerId);
    if (pointers.size < 2) pinch = 0;
  });
  el.addEventListener('pointercancel', (e) => pointers.delete(e.pointerId));

  el.addEventListener('wheel', (e) => {
    e.preventDefault();
    orbit.radius *= 1 + Math.sign(e.deltaY) * 0.09;
    applyCamera();
  }, { passive: false });
}

function pick(e) {
  const ray = new THREE.Raycaster();
  const ndc = new THREE.Vector2();
  const r = renderer.domElement.getBoundingClientRect();
  ndc.x = ((e.clientX - r.left) / r.width) * 2 - 1;
  ndc.y = -((e.clientY - r.top) / r.height) * 2 + 1;
  ray.setFromCamera(ndc, camera);

  const targets = [];
  for (const f of floors.values()) {
    f.group.traverse((o) => { if (o.userData.floorId) targets.push(o); });
  }
  const hit = ray.intersectObjects(targets, false)[0];
  if (hit && pickHandler) pickHandler(hit.object.userData.floorId);
}

// ------------------------------------------------------- animation

export function updateTower(state) {
  if (!renderer) return;
  const now = performance.now();
  const dt = Math.min(0.1, (now - lastFrame) / 1000);
  lastFrame = now;

  cab.position.y += (cabTarget - cab.position.y) * Math.min(1, dt * 3.2);
  if (crane && crane.visible) crane.rotation.y = Math.sin(now / 4200) * 0.2;

  for (const f of floors.values()) {
    const fs = state.floors[f.def.id];
    for (const p of f.crew) {
      const u = p.userData;
      const t = (Math.sin(now / 1000 * u.speed + u.phase) + 1) / 2;
      p.position.x = u.from + t * (u.to - u.from);
      p.position.y = 0.26 + Math.abs(Math.sin(now / 1000 * u.speed * 6 + u.phase)) * 0.035;
      p.rotation.y = Math.cos(now / 1000 * u.speed + u.phase) > 0 ? Math.PI / 2 : -Math.PI / 2;
    }

    const cycle = cycleTime(state, f.def);
    const pct = fs.count === 0 ? 0 : Math.min(1, fs.progress / cycle);
    f.rail.scale.x = Math.max(0.001, pct * f.railWidth);
    f.rail.position.x = -f.railWidth / 2 + (pct * f.railWidth) / 2;

    const st = f.tag.querySelector('.st');
    if (st) {
      st.textContent = fs.manager
        ? `${money(floorPerSecond(state, f.def))}/s`
        : fs.running ? duration(Math.max(0, cycle - fs.progress)) : '▶ Lancer';
    }
    f.tag.classList.toggle('auto', fs.manager);
  }

  layoutTags(state);
  renderer.render(scene, camera);
}

export function popCoin(floorId, amount) {
  const f = floors.get(floorId);
  if (!f || amount <= 0 || !uiLayer) return;
  const now = performance.now();
  if (now - (lastCoin[floorId] || 0) < COIN_THROTTLE) return;
  lastCoin[floorId] = now;

  const p = project(0, f.y + 1.9, D / 2);
  if (!p.visible) return;
  const node = document.createElement('div');
  node.className = 't3d-gain';
  node.textContent = '+' + money(amount);
  node.style.left = p.x + 'px';
  node.style.top = p.y + 'px';
  uiLayer.appendChild(node);
  setTimeout(() => node.remove(), 1250);
}

export function moveCab(floorId) {
  const f = floors.get(floorId);
  if (f) cabTarget = f.y + 0.9;
}

/** Recadre après une certification : la tour a perdu tous ses étages. */
export function reframe() {
  lastOpen = -1;
  framed = false;
  resize();
}
