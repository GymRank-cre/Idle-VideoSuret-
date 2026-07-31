// La tour : immeuble en coupe, un étage par service, avec le personnel qui
// s'active dans chaque pièce et un ascenseur qui dessert l'étage sélectionné.
//
// Ce module ne connaît que l'affichage. Il lit l'état, il ne le modifie jamais.

import { FLOORS } from './data.js';
import { cycleTime, cycleRevenue, floorPerSecond, milestoneMult, costOf } from './economy.js';
import { fmt, money, duration } from './format.js';

const MAX_CREW = 8;        // personnages dessinés au maximum dans une pièce
const COIN_THROTTLE = 500; // ms entre deux pièces jaillissant d'un même étage
const SKIN_TONES = ['#f3c9a2', '#e0a878', '#c2865a', '#8d5a34', '#5c3a21'];

let host = null;    // conteneur de la tour
let cab = null;     // cabine d'ascenseur
let floorsBox = null;
let cards = [];     // { def, root, fill, txt, crew, crewSize, meta, lvl }
let lastCoin = {};

/** Nombre de silhouettes à dessiner pour un effectif donné. */
function crewSize(count) {
  if (count <= 0) return 0;
  return Math.min(MAX_CREW, 1 + Math.floor(count / 6));
}

// ------------------------------------------------------- construction

export function buildTower(container, state) {
  const open = openFloors(state);
  const parts = [];

  parts.push(roofHtml(state, open));
  for (let i = open - 1; i >= 0; i--) parts.push(floorHtml(state, FLOORS[i]));
  parts.push(lobbyHtml());

  container.innerHTML = `
    <div class="tower">
      <div class="floors">
        <div class="shaft"><div class="cab">🛗</div></div>
        ${parts.join('')}
      </div>
      <div class="street"><span class="van">🚐</span></div>
    </div>`;

  host = container.querySelector('.tower');
  cab = container.querySelector('.cab');
  floorsBox = container.querySelector('.floors');

  cards = [];
  for (const def of FLOORS) {
    const root = floorsBox.querySelector(`[data-floor="${def.id}"]`);
    if (!root) continue;
    const card = {
      def, root,
      fill: root.querySelector('.fbar .fill'),
      txt: root.querySelector('.fbar .txt'),
      crew: root.querySelector('.crew'),
      meta: root.querySelector('.f-meta'),
      lvl: root.querySelector('.f-lvl'),
      crewSize: -1,
    };
    paintCrew(card, state.floors[def.id].count);
    cards.push(card);
  }
  return host;
}

/** Nombre d'étages déjà construits. Les services s'ouvrent dans l'ordre. */
export function openFloors(state) {
  return FLOORS.filter((d) => (state.floors[d.id]?.count || 0) > 0).length;
}

/** Le sommet de la tour : un chantier tant qu'il reste un étage à ouvrir. */
function roofHtml(state, open) {
  if (open >= FLOORS.length) {
    return `<div class="roof done"><div class="antenna"></div>
      <div class="sign">SIÈGE&nbsp;SOCIAL</div></div>`;
  }
  const next = FLOORS[open];
  return `
    <div class="roof build" data-act="build" data-id="${next.id}">
      <div class="crane">🏗️</div>
      <div class="build-card">
        <div class="build-title">${next.icon} ${next.name}</div>
        <div class="build-sub">Nouvel étage à construire</div>
        <div class="build-price" data-role="build-price">${money(costOf(next, 0, 1))}</div>
      </div>
    </div>`;
}

function floorHtml(state, def, index) {
  const f = state.floors[def.id];
  const decor = def.decor.map((d) => `<i class="prop">${d}</i>`).join('');

  return `
    <section class="floor" data-floor="${def.id}"
             style="--wall:${def.wall};--accent:${def.accent}">
      <div class="room" data-act="tap" data-id="${def.id}">
        <div class="window"></div>
        <div class="props">${decor}</div>
        <div class="crew"></div>
        <div class="slab"></div>
        <div class="coins"></div>
        <div class="f-tag"><span class="f-ico">${def.icon}</span><b class="f-lvl">${fmt(f.count)}</b></div>
        <div class="f-meta"></div>
      </div>

      <div class="fbar" data-act="tap" data-id="${def.id}">
        <div class="fill"></div>
        <div class="bar-row">
          <span class="f-name">${def.name}</span>
          <span class="txt"></span>
          <button class="f-open" type="button" data-act="panel" data-id="${def.id}"
                  aria-label="Gérer ${def.name}">▸</button>
        </div>
      </div>
    </section>`;
}

function lobbyHtml() {
  return `
    <div class="lobby">
      <span class="lobby-cam">📹</span>
      <span class="lobby-sign">SÛRETÉ TYCOON</span>
      <span class="lobby-door">🚪</span>
    </div>`;
}

// ---------------------------------------------------------- personnel

function paintCrew(card, count) {
  const size = crewSize(count);
  if (size === card.crewSize) return;
  card.crewSize = size;

  const wear = card.def.wear;
  let html = '';
  for (let i = 0; i < size; i++) {
    // Chaque silhouette a sa propre allure : trajet, vitesse et départ décalés.
    const from = 4 + ((i * 37) % 62);
    const to = from + 14 + ((i * 23) % 22);
    const dur = (3.4 + ((i * 7) % 9) * 0.35).toFixed(2);
    const delay = ((i * 13) % 40) * -0.1;
    html += `<i class="person" style="
      --from:${from}%; --to:${to}%; --dur:${dur}s; --delay:${delay}s;
      --wear:${wear[i % wear.length]}; --skin:${SKIN_TONES[(i * 3) % SKIN_TONES.length]}"></i>`;
  }
  card.crew.innerHTML = html;
}

// -------------------------------------------------------- animations

/** Fait jaillir le gain d'un cycle au-dessus de l'étage concerné. */
export function popCoin(floorId, amount) {
  const card = cards.find((c) => c.def.id === floorId);
  if (!card || amount <= 0) return;

  const now = performance.now();
  if (now - (lastCoin[floorId] || 0) < COIN_THROTTLE) return;
  lastCoin[floorId] = now;

  const box = card.root.querySelector('.coins');
  if (!box) return;
  const coin = document.createElement('span');
  coin.className = 'coin';
  coin.textContent = '+' + money(amount);
  coin.style.left = 18 + Math.random() * 50 + '%';
  box.appendChild(coin);
  setTimeout(() => coin.remove(), 1100);
}

/** Amène la cabine devant un étage. */
export function moveCab(floorId) {
  if (!cab || !floorsBox) return;
  const card = cards.find((c) => c.def.id === floorId);
  if (!card) return;
  const y = card.root.offsetTop + card.root.offsetHeight / 2 - cab.offsetHeight / 2;
  cab.style.transform = `translateY(${Math.round(y)}px)`;
}

/** Cadre l'étage le plus haut au premier affichage. */
export function scrollToBottom(container) {
  container.scrollTop = container.scrollHeight;
}

// ----------------------------------------------------- rafraîchissement

export function updateTower(state) {
  for (const card of cards) {
    const def = card.def;
    const f = state.floors[def.id];
    const t = cycleTime(state, def);
    const pct = f.count === 0 ? 0 : Math.min(100, (f.progress / t) * 100);

    card.fill.style.width = pct + '%';
    card.root.classList.toggle('auto', f.manager);
    card.root.classList.toggle('idle', !f.running && !f.manager);

    card.txt.textContent = f.running ? duration(Math.max(0, t - f.progress)) : '▶ Lancer';

    card.lvl.textContent = fmt(f.count);
    card.meta.textContent = f.manager
      ? `${money(floorPerSecond(state, def))}/s · ×${fmt(milestoneMult(f.count))}`
      : `${money(cycleRevenue(state, def))} / ${duration(t)}`;

    paintCrew(card, f.count);
  }
}

/** Le prix du chantier suit la trésorerie : il est réévalué à chaque image. */
export function updateBuildSlot(state, container) {
  const slot = container.querySelector('.roof.build');
  if (!slot) return;
  const def = FLOORS.find((d) => d.id === slot.dataset.id);
  const price = costOf(def, 0, 1);
  slot.classList.toggle('ready', state.cash >= price);
}
