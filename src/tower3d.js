// Tour WebGL procédurale connectée au véritable état du jeu.
// Les formes simples sont volontaires : elles sont légères sur mobile et
// pourront être remplacées progressivement par des fichiers GLB.

import { FLOORS } from './data.js';
import { cycleTime, cycleRevenue, floorPerSecond, milestoneMult, costOf } from './economy.js';
import { fmt, money, duration } from './format.js';

const W = 8.4;
const D = 5.2;
const H = 2.75;
const BASE = 2.55;
const MAX_CREW = 7;
let view = null;

const hex = (value) => Number.parseInt(value.slice(1), 16);
const crewSize = (count) => count <= 0 ? 0 : Math.min(MAX_CREW, 1 + Math.floor(count / 6));

function box(w, h, d, material) {
  const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
  mesh.castShadow = mesh.receiveShadow = true;
  return mesh;
}

function material(color, extra = {}) {
  return new THREE.MeshPhongMaterial({ color, shininess: 18, flatShading: true, ...extra });
}

function cylinder(radius, height, mat, sides = 12) {
  const mesh = new THREE.Mesh(new THREE.CylinderGeometry(radius, radius, height, sides), mat);
  mesh.castShadow = mesh.receiveShadow = true;
  return mesh;
}

function addWorker(group, def, index) {
  const worker = new THREE.Group();
  const skin = material([0xf3c9a2, 0xe0a878, 0xc2865a, 0x8d5a34][index % 4]);
  const uniform = material(hex(def.wear[index % def.wear.length]));
  const body = box(.38, .54, .25, uniform);
  body.position.y = .67;
  const head = new THREE.Mesh(new THREE.SphereGeometry(.2, 12, 9), skin);
  head.position.y = 1.1;
  const hair = new THREE.Mesh(new THREE.SphereGeometry(.205, 12, 6, 0, Math.PI * 2, 0, Math.PI / 2), material([0x3f2d25, 0x6b4226, 0x1f2937][index % 3]));
  hair.position.y = 1.14;
  const legs = [];
  for (const x of [-.11, .11]) {
    const leg = box(.12, .38, .13, material(0x26364a));
    leg.position.set(x, .25, 0);
    legs.push(leg);
    const arm = box(.1, .43, .11, uniform);
    arm.position.set(x * 2.25, .67, 0);
    worker.add(arm);
  }
  worker.add(body, head, hair, ...legs);
  worker.position.set(-2.65 + (index % 4) * 1.35, .2, -.55 + Math.floor(index / 4) * 1.5);
  worker.userData.phase = index * 1.7;
  worker.userData.baseX = worker.position.x;
  worker.userData.legs = legs;
  group.add(worker);
  return worker;
}

function addFurniture(group, def, index) {
  const accent = material(hex(def.accent));
  const dark = material(0x334155);
  const x = -2.6 + (index % 3) * 2.25;
  const z = -1.75;
  const desk = box(1.25, .12, .62, material(0xd7b27b));
  desk.position.set(x, .64, z);
  const screen = box(.5, .38, .08, dark);
  screen.position.set(x, .91, z - .08);
  const device = box(.18, .18, .18, accent);
  device.position.set(x + .42, .8, z);
  group.add(desk, screen, device);

  // Un objet-signature rend chaque métier identifiable sans texte dans la 3D.
  if (index === 0) {
    let prop;
    if (['cameras', 'telesurveillance', 'ia'].includes(def.id)) {
      prop = cylinder(.18, .48, dark, 10); prop.rotation.z = Math.PI / 2;
    } else if (def.id === 'incendie') {
      prop = cylinder(.18, .62, material(0xef4444), 12);
    } else if (def.id === 'reseau' || def.id === 'cyber') {
      prop = box(.55, 1.25, .45, material(0x27364a));
      for (let k = 0; k < 4; k++) {
        const led = box(.28, .035, .012, material(k % 2 ? 0x22c55e : 0x38bdf8, { emissive: k % 2 ? 0x052e16 : 0x082f49 }));
        led.position.set(0, -.42 + k * .25, .232); prop.add(led);
      }
    } else {
      prop = box(.5, .7, .2, accent);
    }
    prop.position.set(3.05, .72, -1.95);
    group.add(prop);
  }
}

function createFloor(def, index, state) {
  const group = new THREE.Group();
  group.position.y = BASE + index * H;
  const wall = material(hex(def.wall));
  const pale = material(0xf1f5f9);

  const slab = box(W, .24, D, material(0xcbd5e1));
  slab.position.y = .12;
  const edge = box(W + .08, .18, .18, material(hex(def.accent), { shininess: 35 }));
  edge.position.set(0, .18, D / 2);
  const rug = box(5.7, .035, 2.45, material(hex(def.wall)));
  rug.position.set(-.15, .28, .35);
  const back = box(W, H - .25, .18, wall);
  back.position.set(0, H / 2, -D / 2);
  group.add(slab, edge, rug, back);
  const ceiling = box(W, .1, D, material(0xf8fafc));
  ceiling.position.y = H - .05;
  group.add(ceiling);
  for (const sideX of [-W / 2, W / 2]) {
    // Retours de mur courts : la coupe reste ouverte même en vue trois-quarts.
    const side = box(.18, H - .25, 1.15, pale);
    side.position.set(sideX, H / 2, -D / 2 + .575);
    group.add(side);
    const pillar = box(.22, H - .25, .22, material(hex(def.accent)));
    pillar.position.set(sideX, H / 2, D / 2 - .12);
    group.add(pillar);
  }
  // Baies vitrées arrière, encadrements et bande lumineuse.
  for (let x = -3.1; x <= 3.1; x += 1.55) {
    const window = box(1.18, 1.08, .035, material(0x83cdef, { transparent: true, opacity: .72, shininess: 80 }));
    window.position.set(x, 1.58, -D / 2 + .105);
    group.add(window);
  }
  const light = box(3.4, .035, .34, material(0xfff3c4, { emissive: 0x5b4310 }));
  light.position.set(0, H - .13, -.25);
  group.add(light);
  // Plante ronde façon jeu mobile, utilisée comme repère de profondeur.
  const pot = cylinder(.22, .38, material(0xe28b55), 10); pot.position.set(3.45, .46, 1.55);
  const leaves = new THREE.Mesh(new THREE.IcosahedronGeometry(.42, 1), material(0x43b96b));
  leaves.position.set(3.45, 1.0, 1.55); leaves.castShadow = true;
  group.add(pot, leaves);
  for (let i = 0; i < 3; i++) addFurniture(group, def, i);

  const workers = [];
  for (let i = 0; i < crewSize(state.floors[def.id].count); i++) workers.push(addWorker(group, def, i));
  group.userData = { def, workers, crew: workers.length };
  return group;
}

function createLobby() {
  const lobby = new THREE.Group();
  const shell = box(W + .35, .55, D + .25, material(0xd3dae4));
  shell.position.y = .27;
  const glassMat = material(0x62c5f2, { transparent: true, opacity: .68, shininess: 95 });
  const glass = box(W - 1.0, 1.75, .12, glassMat);
  glass.position.set(0, 1.1, D / 2);
  const canopy = box(4.8, .18, 1.05, material(0x2563eb));
  canopy.position.set(0, 2.05, D / 2 + .35);
  const doorFrame = box(1.85, 1.75, .18, material(0x1e3a5f));
  doorFrame.position.set(0, 1.1, D / 2 + .06);
  const door = box(1.55, 1.6, .2, glassMat);
  door.position.set(0, 1.0, D / 2 + .17);
  const sign = box(3.9, .55, .18, material(0xffffff, { emissive: 0x16213a }));
  sign.position.set(0, 2.42, D / 2 + .05);
  lobby.add(shell, glass, canopy, doorFrame, door, sign);
  return lobby;
}

function createRoof(open) {
  const roof = new THREE.Group();
  roof.position.y = BASE + open * H;
  const deck = box(W, .22, D, material(open < FLOORS.length ? 0xfbbf24 : 0x94a3b8));
  roof.add(deck);
  if (open < FLOORS.length) {
    const mast = box(.3, 3.5, .3, material(0xf59e0b));
    mast.position.set(2.8, 1.55, -.5);
    const jib = new THREE.Group();
    const beam = box(5.5, .16, .16, material(0xfbbf24));
    jib.add(beam);
    for (let x = -2.3; x < 2.5; x += .65) {
      const brace = box(.05, .52, .05, material(0xd97706));
      brace.position.x = x; brace.rotation.z = x % 1.3 ? .65 : -.65; jib.add(brace);
    }
    jib.position.set(.45, 3.25, -.5);
    roof.add(mast, jib);
    roof.userData.jib = jib;
  } else {
    const antenna = box(.12, 1.8, .12, material(0x334155));
    antenna.position.set(2.4, .9, 0);
    roof.add(antenna);
  }
  return roof;
}

function createElevator(open) {
  const elevator = new THREE.Group();
  const shaftMat = material(0x29435f, { transparent: true, opacity: .8 });
  const height = Math.max(BASE, BASE + open * H);
  for (const x of [-3.82, -2.78]) {
    const rail = box(.1, height, .12, shaftMat); rail.position.set(x, height / 2, -2.15); elevator.add(rail);
  }
  const cab = box(.9, 1.65, .78, material(0x60a5fa, { emissive: 0x0b2447, shininess: 65 }));
  cab.position.set(-3.3, 1.15, -2.12);
  const doors = box(.72, 1.35, .025, material(0xdbeafe, { shininess: 85 }));
  doors.position.set(0, 0, .405); cab.add(doors);
  elevator.add(cab);
  elevator.userData.cab = cab;
  return elevator;
}

function createEnvironment() {
  const env = new THREE.Group();
  const road = box(50, .025, 8, material(0x455568));
  road.position.set(0, .02, 9);
  env.add(road);
  for (let x = -22; x < 23; x += 3.8) {
    const dash = box(1.9, .035, .16, material(0xf8d46a));
    dash.position.set(x, .04, 9); env.add(dash);
  }
  const colors = [0x91a9ba, 0xb8c4ce, 0x7f9aab, 0xd0b99c];
  for (let i = 0; i < 18; i++) {
    const h = 2.5 + (i * 1.73 % 6);
    const building = box(3 + (i % 3), h, 3.2, material(colors[i % colors.length]));
    const side = i % 2 ? -1 : 1;
    building.position.set(side * (10 + (i % 5) * 4.3), h / 2, -5 - Math.floor(i / 5) * 5);
    env.add(building);
  }
  for (const x of [-7, -5.2, 6.1, 8]) {
    const trunk = cylinder(.11, 1.05, material(0x7c4f2c), 8); trunk.position.set(x, .52, 4.2);
    const crown = new THREE.Mesh(new THREE.IcosahedronGeometry(.72, 1), material(0x39a866));
    crown.position.set(x, 1.5, 4.2); crown.castShadow = true;
    env.add(trunk, crown);
  }
  return env;
}

function overlayHtml(state, open) {
  const labels = FLOORS.slice(0, open).map((def, index) => {
    const f = state.floors[def.id];
    const y = open <= 1 ? 55 : 82 - index * (64 / (open - 1));
    return `<div class="floor3d-card" data-floor3d="${def.id}" style="--y:${y.toFixed(2)}%">
      <button class="floor3d-main" type="button" data-act="tap" data-id="${def.id}">
        <span>${def.icon}</span><b>${def.name}</b><em data-role="level">${fmt(f.count)}</em>
        <small data-role="meta"></small><i><u data-role="progress"></u></i>
      </button>
      <button class="floor3d-manage" type="button" data-act="panel" data-id="${def.id}" aria-label="Gérer ${def.name}">›</button>
    </div>`;
  }).join('');
  const build = open < FLOORS.length ? FLOORS[open] : null;
  return `<div class="tower3d-overlay">${labels}
    ${build ? `<button class="build3d" type="button" data-act="build" data-id="${build.id}">
      🏗️ <span><b>${build.name}</b><small>Construire · <i data-role="build-price">${money(costOf(build, 0, 1))}</i></small></span>
    </button>` : '<div class="tower3d-complete">⭐ SIÈGE SOCIAL ACHEVÉ</div>'}
    <div class="tower3d-hint">Glisse pour tourner · pince ou molette pour zoomer</div>
    <div class="tower3d-gains" aria-hidden="true"></div>
  </div>`;
}

export function buildTower(container, state) {
  dispose();
  const open = FLOORS.filter((d) => state.floors[d.id].count > 0).length;
  container.innerHTML = `<div class="tower3d"><div class="tower3d-stage"></div>${overlayHtml(state, open)}</div>`;
  const root = container.querySelector('.tower3d');
  const stage = root.querySelector('.tower3d-stage');
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.75));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  if ('toneMapping' in renderer) {
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.15;
  }
  stage.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0xaedcf3);
  scene.fog = new THREE.Fog(0xaedcf3, 42, 88);
  // Longue focale : perspective quasi isométrique typique des tycoon mobiles.
  const camera = new THREE.PerspectiveCamera(27, 1, .1, 130);
  scene.add(new THREE.HemisphereLight(0xe8f6ff, 0x64748b, 1.45));
  const sun = new THREE.DirectionalLight(0xfff3d6, 1.35);
  sun.position.set(10, 18, 12);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  scene.add(sun);

  const world = new THREE.Group();
  scene.add(world);
  const ground = box(55, .4, 55, material(0x79a96b));
  ground.position.y = -.25;
  const environment = createEnvironment();
  const elevator = createElevator(open);
  world.add(ground, environment, createLobby(), elevator);
  const floors = new Map();
  FLOORS.slice(0, open).forEach((def, i) => {
    const floor = createFloor(def, i, state);
    floors.set(def.id, floor);
    world.add(floor);
  });
  const roof = createRoof(open);
  world.add(roof);

  const targetY = Math.max(3.2, BASE + open * H * .46);
  let yaw = -.72;
  let pitch = .36;
  let distance = Math.max(27, 23 + open * 2.05);
  let dragging = false;
  let px = 0;
  let py = 0;
  const pointers = new Map();

  const positionCamera = () => {
    camera.position.set(Math.sin(yaw) * Math.cos(pitch) * distance, targetY + Math.sin(pitch) * distance, Math.cos(yaw) * Math.cos(pitch) * distance);
    camera.lookAt(0, targetY, 0);
  };
  positionCamera();

  stage.addEventListener('pointerdown', (e) => { dragging = true; px = e.clientX; py = e.clientY; pointers.set(e.pointerId, e); stage.setPointerCapture(e.pointerId); });
  stage.addEventListener('pointermove', (e) => {
    if (!dragging) return;
    pointers.set(e.pointerId, e);
    yaw -= (e.clientX - px) * .008;
    pitch = Math.max(.18, Math.min(.62, pitch + (e.clientY - py) * .004));
    px = e.clientX; py = e.clientY; positionCamera();
  });
  stage.addEventListener('pointerup', (e) => { pointers.delete(e.pointerId); dragging = pointers.size > 0; });
  stage.addEventListener('pointercancel', () => { pointers.clear(); dragging = false; });
  stage.addEventListener('wheel', (e) => { e.preventDefault(); distance = Math.max(22, Math.min(58, distance + e.deltaY * .018)); positionCamera(); }, { passive: false });

  view = { root, stage, renderer, scene, camera, world, floors, roof, elevator, open, yaw, last: performance.now() };
  resize();
  return root;
}

function resize() {
  if (!view) return;
  const w = view.stage.clientWidth || 1;
  const h = view.stage.clientHeight || 1;
  if (view.renderer.domElement.width !== Math.round(w * view.renderer.getPixelRatio()) || view.renderer.domElement.height !== Math.round(h * view.renderer.getPixelRatio())) {
    view.renderer.setSize(w, h, false);
    view.camera.aspect = w / h;
    view.camera.updateProjectionMatrix();
  }
}

function dispose() {
  if (!view) return;
  view.world.traverse((node) => {
    node.geometry?.dispose?.();
    if (Array.isArray(node.material)) node.material.forEach((m) => m.dispose());
    else node.material?.dispose?.();
  });
  view.renderer.dispose();
  view = null;
}

export function updateTower(state) {
  if (!view || !view.root.isConnected) { dispose(); return; }
  resize();
  const now = performance.now();
  const seconds = now / 1000;
  for (const [id, group] of view.floors) {
    const def = group.userData.def;
    const f = state.floors[id];
    const expected = crewSize(f.count);
    while (group.userData.crew < expected) {
      group.userData.workers.push(addWorker(group, def, group.userData.crew));
      group.userData.crew++;
    }
    group.userData.workers.forEach((worker, i) => {
      const stride = Math.sin(seconds * (1.4 + i * .05) + worker.userData.phase);
      worker.position.x = worker.userData.baseX + stride * .34;
      worker.position.y = .2 + Math.abs(stride) * .025;
      worker.rotation.y = stride * .18;
      worker.userData.legs.forEach((leg, legIndex) => { leg.rotation.x = stride * (legIndex ? -.38 : .38); });
    });
    const t = cycleTime(state, def);
    const pct = f.count ? Math.min(100, f.progress / t * 100) : 0;
    const card = view.root.querySelector(`[data-floor3d="${id}"]`);
    if (card) {
      card.classList.toggle('auto', f.manager);
      card.querySelector('[data-role="level"]').textContent = fmt(f.count);
      card.querySelector('[data-role="progress"]').style.width = pct + '%';
      card.querySelector('[data-role="meta"]').textContent = f.manager
        ? `${money(floorPerSecond(state, def))}/s · ×${fmt(milestoneMult(f.count))}`
        : f.running ? duration(Math.max(0, t - f.progress)) : `${money(cycleRevenue(state, def))} · ▶`;
    }
  }
  if (view.roof.userData.jib) view.roof.userData.jib.rotation.y = seconds * .13;
  view.renderer.render(view.scene, view.camera);
  view.last = now;
}

export function updateBuildSlot(state) {
  if (!view) return;
  const button = view.root.querySelector('.build3d');
  if (!button) return;
  const def = FLOORS.find((d) => d.id === button.dataset.id);
  button.classList.toggle('ready', state.cash >= costOf(def, 0, 1));
}

export function popCoin(floorId, amount) {
  if (!view || amount <= 0) return;
  const card = view.root.querySelector(`[data-floor3d="${floorId}"]`);
  const layer = view.root.querySelector('.tower3d-gains');
  if (!card || !layer) return;
  const a = card.getBoundingClientRect();
  const b = view.root.getBoundingClientRect();
  const coin = document.createElement('span');
  coin.textContent = '+' + money(amount);
  coin.style.left = `${a.left - b.left + a.width * .45}px`;
  coin.style.top = `${a.top - b.top}px`;
  layer.appendChild(coin);
  setTimeout(() => coin.remove(), 1200);
}

export function moveCab(floorId) {
  if (!view) return;
  const index = FLOORS.findIndex((def) => def.id === floorId);
  const cab = view.elevator?.userData.cab;
  if (cab && index >= 0) cab.position.y = BASE + index * H + 1.05;
  const card = view.root.querySelector(`[data-floor3d="${floorId}"]`);
  card?.animate([{ transform: 'translateX(0)' }, { transform: 'translateX(-7px)' }, { transform: 'translateX(0)' }], { duration: 420 });
}
