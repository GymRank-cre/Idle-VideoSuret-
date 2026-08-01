// Tour WebGL procédurale connectée au véritable état du jeu.
// Les formes simples sont volontaires : elles sont légères sur mobile et
// pourront être remplacées progressivement par des fichiers GLB.

import { FLOORS } from './data.js';
import { cycleTime, cycleRevenue, floorPerSecond, milestoneMult, costOf } from './economy.js';
import { fmt, money, duration } from './format.js';

const W = 8.4;
const D = 5.2;
const H = 2.75;
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
  return new THREE.MeshLambertMaterial({ color, ...extra });
}

function addWorker(group, def, index) {
  const worker = new THREE.Group();
  const skin = material([0xf3c9a2, 0xe0a878, 0xc2865a, 0x8d5a34][index % 4]);
  const uniform = material(hex(def.wear[index % def.wear.length]));
  const body = box(.32, .62, .24, uniform);
  body.position.y = .55;
  const head = new THREE.Mesh(new THREE.SphereGeometry(.18, 10, 8), skin);
  head.position.y = 1.03;
  worker.add(body, head);
  worker.position.set(-2.65 + (index % 4) * 1.35, .2, -.55 + Math.floor(index / 4) * 1.5);
  worker.userData.phase = index * 1.7;
  group.add(worker);
  return worker;
}

function addFurniture(group, def, index) {
  const accent = material(hex(def.accent));
  const dark = material(0x334155);
  const x = -2.6 + (index % 3) * 2.25;
  const z = -1.75;
  const desk = box(1.25, .12, .62, material(0xd6b486));
  desk.position.set(x, .64, z);
  const screen = box(.5, .38, .08, dark);
  screen.position.set(x, .91, z - .08);
  const device = box(.18, .18, .18, accent);
  device.position.set(x + .42, .8, z);
  group.add(desk, screen, device);
}

function createFloor(def, index, state) {
  const group = new THREE.Group();
  group.position.y = .55 + index * H;
  const wall = material(hex(def.wall));
  const pale = material(0xf1f5f9);

  const slab = box(W, .24, D, material(0xcbd5e1));
  slab.position.y = .12;
  const back = box(W, H - .25, .18, wall);
  back.position.set(0, H / 2, -D / 2);
  group.add(slab, back);
  for (const sideX of [-W / 2, W / 2]) {
    const side = box(.18, H - .25, D, pale);
    side.position.set(sideX, H / 2, 0);
    group.add(side);
  }
  for (let i = 0; i < 3; i++) addFurniture(group, def, i);

  const workers = [];
  for (let i = 0; i < crewSize(state.floors[def.id].count); i++) workers.push(addWorker(group, def, i));
  group.userData = { def, workers, crew: workers.length };
  return group;
}

function createLobby() {
  const lobby = new THREE.Group();
  const shell = box(W, .55, D, material(0xe2e8f0));
  shell.position.y = .27;
  const glass = box(W - 1.2, 1.4, .12, material(0x7dd3fc, { transparent: true, opacity: .72 }));
  glass.position.set(0, .72, D / 2);
  lobby.add(shell, glass);
  return lobby;
}

function createRoof(open) {
  const roof = new THREE.Group();
  roof.position.y = .55 + open * H;
  const deck = box(W, .22, D, material(open < FLOORS.length ? 0xfbbf24 : 0x94a3b8));
  roof.add(deck);
  if (open < FLOORS.length) {
    const mast = box(.22, 3.1, .22, material(0xf59e0b));
    mast.position.set(2.8, 1.55, -.5);
    const jib = box(4.4, .18, .18, material(0xf59e0b));
    jib.position.set(1.2, 3.0, -.5);
    roof.add(mast, jib);
    roof.userData.jib = jib;
  } else {
    const antenna = box(.12, 1.8, .12, material(0x334155));
    antenna.position.set(2.4, .9, 0);
    roof.add(antenna);
  }
  return roof;
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
  stage.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x9fd7f5);
  scene.fog = new THREE.Fog(0x9fd7f5, 34, 75);
  const camera = new THREE.PerspectiveCamera(39, 1, .1, 100);
  scene.add(new THREE.HemisphereLight(0xe8f6ff, 0x64748b, 1.45));
  const sun = new THREE.DirectionalLight(0xfff3d6, 1.35);
  sun.position.set(10, 18, 12);
  sun.castShadow = true;
  sun.shadow.mapSize.set(1024, 1024);
  scene.add(sun);

  const world = new THREE.Group();
  scene.add(world);
  const ground = box(50, .4, 50, material(0x87a96b));
  ground.position.y = -.25;
  world.add(ground, createLobby());
  const floors = new Map();
  FLOORS.slice(0, open).forEach((def, i) => {
    const floor = createFloor(def, i, state);
    floors.set(def.id, floor);
    world.add(floor);
  });
  const roof = createRoof(open);
  world.add(roof);

  const targetY = Math.max(3.5, open * H * .5);
  let yaw = -.58;
  let pitch = .24;
  let distance = Math.max(18, 14 + open * 1.45);
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
    pitch = Math.max(-.12, Math.min(.72, pitch + (e.clientY - py) * .005));
    px = e.clientX; py = e.clientY; positionCamera();
  });
  stage.addEventListener('pointerup', (e) => { pointers.delete(e.pointerId); dragging = pointers.size > 0; });
  stage.addEventListener('pointercancel', () => { pointers.clear(); dragging = false; });
  stage.addEventListener('wheel', (e) => { e.preventDefault(); distance = Math.max(12, Math.min(38, distance + e.deltaY * .012)); positionCamera(); }, { passive: false });

  view = { root, stage, renderer, scene, camera, world, floors, roof, open, yaw, last: performance.now() };
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
      worker.position.x += Math.sin(seconds * (1.05 + i * .04) + worker.userData.phase) * .0025;
      worker.rotation.y = Math.sin(seconds + worker.userData.phase) * .25;
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
  const card = view.root.querySelector(`[data-floor3d="${floorId}"]`);
  card?.animate([{ transform: 'translateX(0)' }, { transform: 'translateX(-7px)' }, { transform: 'translateX(0)' }], { duration: 420 });
}
