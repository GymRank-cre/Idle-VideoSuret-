// Rendu DOM. L'onglet principal est la tour (voir tower-view.js) ; les autres
// onglets sont des listes reconstruites une fois par seconde. Les valeurs
// « chaudes » (trésorerie, barres, minuteurs) sont rafraîchies à chaque image.

import { FLOORS, UPGRADES, RESEARCH, CONTRACTS, ACHIEVEMENTS, MILESTONES } from './data.js';
import {
  FLOOR_BY_ID, costOf, resolveQty, maxAffordable, milestoneMult,
  cycleTime, cycleRevenue, floorPerSecond, incomePerSecond,
  starsAvailable, nextStarAt, globalMult, boostSpec, offlineCap, offlineRate,
  isUpgradeAvailable, isResearchAvailable,
} from './economy.js';
import { fmt, money, duration, multiplier, percent } from './format.js';
import * as tower from './tower-view.js';
import * as G from './game.js';
import { game } from './game.js';

const CONTRACT_TPL = Object.fromEntries(CONTRACTS.map((c) => [c.id, c]));
const QTYS = [1, 10, 100, 'max'];

const el = {};
let refs = { costs: [], timers: [] };
let sheet = null;   // { id, refs… } quand le panneau d'étage est ouvert
let lastTop = -1;   // dernier étage accessible, pour recadrer après un chantier

export function initUI() {
  el.view = document.getElementById('view');
  el.cash = document.getElementById('hud-cash');
  el.cps = document.getElementById('hud-cps');
  el.stars = document.getElementById('hud-stars');
  el.rd = document.getElementById('hud-rd');
  el.boost = document.getElementById('btn-boost');
  el.boostLabel = document.getElementById('boost-label');
  el.tabs = document.getElementById('tabs');
  el.toasts = document.getElementById('toasts');
  el.sheet = document.getElementById('sheet');
  el.modal = document.getElementById('modal');
  el.modalTitle = document.getElementById('modal-title');
  el.modalBody = document.getElementById('modal-body');
  el.modalActions = document.getElementById('modal-actions');

  el.tabs.addEventListener('click', (e) => {
    const btn = e.target.closest('.tab');
    if (!btn) return;
    closeSheet();
    game.state.tab = btn.dataset.tab;
    lastTop = -1;
    render();
  });

  el.view.addEventListener('click', onClick);
  el.sheet.addEventListener('click', onClick);
  el.boost.addEventListener('click', () => G.triggerBoost());
  document.getElementById('btn-menu').addEventListener('click', openMenu);
  el.modal.addEventListener('click', (e) => { if (e.target === el.modal) closeModal(); });

  render();
}

// ------------------------------------------------------------- clics

function onClick(e) {
  const node = e.target.closest('[data-act]');
  if (!node) return;
  const { act, id, uid, qty } = node.dataset;
  let redraw = true;

  switch (act) {
    case 'tap': {
      const f = game.state.floors[id];
      if (f.manager || f.running) { openSheet(id); redraw = false; }
      else { redraw = G.runFloor(id); tower.moveCab(id); }
      break;
    }
    case 'panel':
      openSheet(id);
      redraw = false;
      break;
    case 'close-sheet':
      closeSheet();
      redraw = false;
      break;
    case 'build':
      redraw = G.buyFloor(id, 1);
      break;
    case 'qty':
      game.state.buyQty = qty === 'max' ? 'max' : Number(qty);
      paintQty();
      redraw = false;
      break;
    case 'buy':
      redraw = G.buyFloor(id, game.state.buyQty);
      break;
    case 'mgr':
      redraw = G.hireManager(id);
      break;
    case 'upg': redraw = G.buyUpgrade(id); break;
    case 'res': redraw = G.buyResearch(id); break;
    case 'accept': redraw = G.acceptOffer(Number(uid)); break;
    case 'decline': redraw = G.declineOffer(Number(uid)); break;
    case 'certify': confirmCertify(); redraw = false; break;
    default: redraw = false;
  }
  if (redraw) render();
}

// ------------------------------------------------------------ rendu

export function render() {
  const state = game.state;
  refs = { costs: [], timers: [] };

  for (const btn of el.tabs.querySelectorAll('.tab')) {
    btn.classList.toggle('on', btn.dataset.tab === state.tab);
  }

  el.view.classList.toggle('flat', state.tab === 'services' && tower.is3D());

  if (state.tab === 'services' && tower.is3D()) {
    tower.buildTower(el.view, state);
    tower.updateBuildSlot(state, el.view);
  } else if (state.tab === 'services') {
    const keep = el.view.scrollTop;
    const open = tower.openFloors(state);
    tower.buildTower(el.view, state);
    // Un nouvel étage vient d'être construit : on remonte le montrer.
    el.view.scrollTop = open !== lastTop ? 0 : keep;
    lastTop = open;
  } else {
    lastTop = -1;
    switch (state.tab) {
      case 'upgrades': el.view.innerHTML = viewUpgrades(state); break;
      case 'contracts': el.view.innerHTML = viewContracts(state); break;
      case 'research': el.view.innerHTML = viewResearch(state); break;
      default: el.view.innerHTML = viewCert(state);
    }
  }

  if (sheet) buildSheet(sheet.id);
  collectCostRefs();
  collectTimerRefs();
  frame();
}

// ------------------------------------------------- panneau d'un étage

function openSheet(id) {
  buildSheet(id);
  tower.moveCab(id);
}

/** Bascule la tour en WebGL si la machine le permet, puis redessine. */
export function enable3D() {
  tower.setPickHandler((floorId) => {
    const f = game.state.floors[floorId];
    if (!f) return;
    if (f.manager || f.running) openSheet(floorId);
    else if (G.runFloor(floorId)) tower.moveCab(floorId);
  });
  return tower.enable3D().then((on) => {
    if (on) render();
    return on;
  });
}

function buildSheet(id) {
  const def = FLOOR_BY_ID[id];
  const state = game.state;
  if (!def) return;

  el.sheet.innerHTML = `
    <div class="sheet-back" data-act="close-sheet"></div>
    <div class="sheet-card" style="--accent:${def.accent};--wall:${def.wall}">
      <div class="sheet-head">
        <div class="sheet-icon">${def.icon}</div>
        <div>
          <div class="sheet-title">${def.name}</div>
          <div class="sheet-sub">${def.desc}</div>
        </div>
        <button class="sheet-x" type="button" data-act="close-sheet" aria-label="Fermer">✕</button>
      </div>

      <div class="kpis">
        <div><b data-role="k-count">—</b><span>${def.unit}s</span></div>
        <div><b data-role="k-cycle">—</b><span>par cycle</span></div>
        <div><b data-role="k-sec">—</b><span>par seconde</span></div>
      </div>

      <div class="ms">
        <div class="ms-top"><span>Prochain palier</span><b data-role="ms-txt">—</b></div>
        <div class="ms-bar"><i data-role="ms-fill"></i></div>
      </div>

      <div class="qty-row">
        ${QTYS.map((q) => `<button class="qty" data-act="qty" data-qty="${q}" type="button">
            ${q === 'max' ? 'MAX' : '×' + q}</button>`).join('')}
      </div>

      <button class="big buy" type="button" data-act="buy" data-id="${def.id}">
        <span data-role="buy-label">Recruter</span>
        <small data-role="buy-cost">—</small>
      </button>

      <div class="mgr" data-role="mgr-box"></div>
    </div>`;

  sheet = {
    id,
    def,
    count: el.sheet.querySelector('[data-role="k-count"]'),
    cycle: el.sheet.querySelector('[data-role="k-cycle"]'),
    sec: el.sheet.querySelector('[data-role="k-sec"]'),
    msTxt: el.sheet.querySelector('[data-role="ms-txt"]'),
    msFill: el.sheet.querySelector('[data-role="ms-fill"]'),
    buy: el.sheet.querySelector('[data-act="buy"]'),
    buyLabel: el.sheet.querySelector('[data-role="buy-label"]'),
    buyCost: el.sheet.querySelector('[data-role="buy-cost"]'),
    mgrBox: el.sheet.querySelector('[data-role="mgr-box"]'),
  };
  el.sheet.hidden = false;
  paintQty();
  paintManager(state);
  updateSheet(state);
}

function paintQty() {
  if (!sheet) return;
  for (const b of el.sheet.querySelectorAll('.qty')) {
    const q = b.dataset.qty === 'max' ? 'max' : Number(b.dataset.qty);
    b.classList.toggle('on', game.state.buyQty === q);
  }
}

function paintManager(state) {
  if (!sheet) return;
  const def = sheet.def;
  const f = state.floors[def.id];
  sheet.mgrBox.innerHTML = f.manager
    ? `<div class="mgr-hired"><span class="mgr-face">👔</span>
         <div><b>${def.manager}</b><span>dirige le service — production automatique</span></div></div>`
    : `<button class="big mgr-buy" type="button" data-act="mgr" data-id="${def.id}"
               ${f.count > 0 ? `data-cost="${def.managerCost}"` : 'disabled'}>
         <span>👔 Recruter ${def.manager}</span>
         <small>${money(def.managerCost)} — automatise l'étage</small>
       </button>`;
}

function updateSheet(state) {
  if (!sheet) return;
  const def = sheet.def;
  const f = state.floors[def.id];

  sheet.count.textContent = fmt(f.count);
  sheet.cycle.textContent = money(cycleRevenue(state, def));
  sheet.sec.textContent = money(floorPerSecond(state, def)) + '/s';

  const next = nextMilestone(f.count);
  const prev = prevMilestone(f.count);
  sheet.msTxt.textContent = `${fmt(f.count)} / ${fmt(next)} → ×2`;
  sheet.msFill.style.width = Math.min(100, ((f.count - prev) / (next - prev)) * 100) + '%';

  const qty = resolveQty(def, f.count, state.cash, state.buyQty);
  const price = costOf(def, f.count, qty);
  const affordable = price <= state.cash
    && (state.buyQty !== 'max' || maxAffordable(def, f.count, state.cash) > 0);
  sheet.buyLabel.textContent = f.count === 0 ? 'Ouvrir le service' : `Recruter ×${fmt(qty)}`;
  sheet.buyCost.textContent = money(price);
  sheet.buy.disabled = !affordable;
}

function closeSheet() {
  sheet = null;
  el.sheet.hidden = true;
  el.sheet.innerHTML = '';
}

function nextMilestone(count) {
  for (const m of MILESTONES) if (count < m) return m;
  return Math.ceil((count + 1) / 100) * 100;
}

function prevMilestone(count) {
  let prev = 0;
  for (const m of MILESTONES) if (count >= m) prev = m;
  if (count >= 1000) prev = Math.floor(count / 100) * 100;
  return prev;
}

// ------------------------------------------------- onglet Matériel

function viewUpgrades(state) {
  const available = UPGRADES.filter((u) => isUpgradeAvailable(state, u)).sort((a, b) => a.cost - b.cost);
  const owned = UPGRADES.filter((u) => state.upgrades[u.id]);
  const locked = UPGRADES.filter((u) => !state.upgrades[u.id] && !isUpgradeAvailable(state, u));

  return `
    <div class="section-head"><h2>Investissements disponibles</h2>
      <span class="hint">${available.length} offre${available.length > 1 ? 's' : ''}</span></div>
    ${available.length
      ? available.map((u) => upgradeCard(u, 'buy')).join('')
      : '<div class="empty">Développez vos services : les fournisseurs vous rappelleront.</div>'}

    ${locked.length ? `<div class="section-head"><h2>Bientôt accessible</h2></div>
      ${locked.slice(0, 4).map((u) => upgradeCard(u, 'locked')).join('')}` : ''}

    ${owned.length ? `<div class="section-head"><h2>Déjà en parc</h2>
      <span class="hint">${owned.length}</span></div>
      ${owned.map((u) => upgradeCard(u, 'owned')).join('')}` : ''}`;
}

function upgradeCard(u, mode) {
  const req = FLOOR_BY_ID[u.req.floor];
  const tag = u.kind === 'speed'
    ? `<span class="badge warn">vitesse ×${u.mult}</span>`
    : `<span class="badge">CA ×${u.mult}</span>`;
  return `
    <div class="card ${mode === 'locked' ? 'locked' : ''}">
      <div class="card-top">
        <div class="thumb">${u.icon}</div>
        <div class="card-main">
          <div class="card-title">${u.name} ${tag}</div>
          <div class="card-sub">${u.desc}</div>
          ${mode === 'locked'
            ? `<div class="card-meta">Nécessite ${u.req.count} postes en « ${req.name} »</div>` : ''}
        </div>
      </div>
      ${mode === 'buy'
        ? `<button class="big" type="button" data-act="upg" data-id="${u.id}" data-cost="${u.cost}">
             <span>Investir</span><small>${money(u.cost)}</small></button>`
        : mode === 'owned' ? '<div class="card-meta ok">✔ Acquis</div>' : ''}
    </div>`;
}

// ------------------------------------------------- onglet Contrats

function viewContracts(state) {
  const perSec = G.potentialPerSecond(state);
  return `
    <div class="section-head"><h2>Appels d'offres</h2>
      <span class="hint">référence ${money(perSec)}/s</span></div>
    ${state.offers.length
      ? state.offers.map((o) => offerCard(state, o, perSec)).join('')
      : '<div class="empty">Aucune consultation en cours. Les prospects arrivent régulièrement — restez équipé.</div>'}

    <div class="section-head"><h2>Chantiers en cours</h2>
      <span class="hint">${state.contracts.length} / 2</span></div>
    ${state.contracts.length
      ? state.contracts.map((c) => activeCard(state, c)).join('')
      : '<div class="empty">Aucun chantier en cours.</div>'}

    <div class="section-head"><h2>Comment ça marche</h2></div>
    <div class="card"><div class="card-sub">
      Un appel d'offres mobilise vos équipes pendant sa durée puis verse une prime
      calculée sur votre chiffre d'affaires du moment, plus des points de R&amp;D.
      Une proposition non traitée expire au bout de 45 s.
    </div></div>`;
}

function offerCard(state, o, perSec) {
  const tpl = CONTRACT_TPL[o.tpl];
  if (!tpl) return '';
  const reward = Math.max(perSec, 1) * tpl.payout * (state.research.commerce ? 1.5 : 1);
  const full = state.contracts.length >= 2;
  return `
    <div class="card" data-timer="offer:${o.uid}">
      <div class="card-top">
        <div class="thumb">${tpl.icon}</div>
        <div class="card-main">
          <div class="card-title">${tpl.name}</div>
          <div class="card-sub">Durée ${duration(tpl.duration)} · prime ${money(reward)}
            · ${Math.round(tpl.rd * (state.research.bureau ? 2 : 1))} pts R&amp;D</div>
        </div>
      </div>
      <div class="timer"><div class="fill" data-role="fill"></div></div>
      <div class="row2">
        <button class="big" type="button" data-act="accept" data-uid="${o.uid}" ${full ? 'disabled' : ''}>
          <span>${full ? 'Équipes occupées' : 'Signer'}</span><small>${money(reward)}</small></button>
        <button class="big ghost" type="button" data-act="decline" data-uid="${o.uid}"><span>Refuser</span></button>
      </div>
    </div>`;
}

function activeCard(state, c) {
  const tpl = CONTRACT_TPL[c.tpl];
  if (!tpl) return '';
  return `
    <div class="card" data-timer="active:${c.uid}">
      <div class="card-top">
        <div class="thumb">${tpl.icon}</div>
        <div class="card-main">
          <div class="card-title">${tpl.name}<span class="badge">en cours</span></div>
          <div class="card-sub">Livraison dans <b data-role="left">—</b> · prime ${money(c.reward)} + ${c.rd} pts R&amp;D</div>
        </div>
      </div>
      <div class="timer run"><div class="fill" data-role="fill"></div></div>
    </div>`;
}

function collectTimerRefs() {
  refs.timers = [];
  for (const node of el.view.querySelectorAll('[data-timer]')) {
    const [kind, uid] = node.dataset.timer.split(':');
    refs.timers.push({
      kind, uid: Number(uid),
      fill: node.querySelector('[data-role="fill"]'),
      left: node.querySelector('[data-role="left"]'),
    });
  }
}

function updateTimers(state) {
  const now = state.stats.playTime;
  for (const t of refs.timers) {
    if (t.kind === 'offer') {
      const o = state.offers.find((x) => x.uid === t.uid);
      if (!o) continue;
      t.fill.style.width = Math.max(0, Math.min(100, ((o.expiresAt - now) / 45) * 100)) + '%';
    } else {
      const c = state.contracts.find((x) => x.uid === t.uid);
      if (!c) continue;
      t.fill.style.width = Math.max(0, Math.min(100, (1 - (c.endsAt - now) / c.duration) * 100)) + '%';
      if (t.left) t.left.textContent = duration(Math.max(0, c.endsAt - now));
    }
  }
}

// ----------------------------------------------------- onglet R&D

function viewResearch(state) {
  const groups = [
    { title: 'Disponible', list: RESEARCH.filter((r) => isResearchAvailable(state, r)) },
    { title: 'Verrouillé', list: RESEARCH.filter((r) => !state.research[r.id] && !isResearchAvailable(state, r)) },
    { title: 'Acquis', list: RESEARCH.filter((r) => state.research[r.id]) },
  ];
  return `
    <div class="section-head"><h2>Bureau d'études</h2>
      <span class="hint">🔬 ${fmt(state.rd)} points</span></div>
    <div class="card"><div class="card-sub">
      Les points de R&amp;D proviennent des appels d'offres livrés. Les travaux sont
      <b>conservés lors d'une certification</b> : c'est votre progression de long terme.
    </div></div>
    ${groups.map((g) => g.list.length ? `
      <div class="section-head"><h2>${g.title}</h2></div>
      ${g.list.map((r) => researchCard(state, r, g.title)).join('')}` : '').join('')}`;
}

function researchCard(state, r, group) {
  const locked = group === 'Verrouillé';
  const owned = group === 'Acquis';
  const missing = r.req.filter((id) => !state.research[id])
    .map((id) => RESEARCH.find((x) => x.id === id)?.name).filter(Boolean);
  return `
    <div class="card ${locked ? 'locked' : ''}">
      <div class="card-top">
        <div class="thumb">${r.icon}</div>
        <div class="card-main">
          <div class="card-title">${r.name}${owned ? '<span class="badge ok">acquis</span>' : ''}</div>
          <div class="card-sub">${r.desc}</div>
          ${locked && missing.length
            ? `<div class="card-meta">Nécessite : ${missing.join(', ')}</div>` : ''}
        </div>
      </div>
      ${!owned && !locked ? `
        <button class="big violet" type="button" data-act="res" data-id="${r.id}" data-rd-cost="${r.cost}">
          <span>Lancer l'étude</span><small>🔬 ${fmt(r.cost)}</small></button>` : ''}
    </div>`;
}

// -------------------------------------------- onglet Certification

function viewCert(state) {
  const gain = starsAvailable(state);
  const perStar = state.research.qualite ? 3 : 2;
  const unlocked = ACHIEVEMENTS.filter((a) => state.achievements[a.id]).length;

  return `
    <div class="section-head"><h2>Certification</h2><span class="hint">⭐ ${fmt(state.stars)} étoiles</span></div>
    <div class="card">
      <div class="card-sub">
        Repartir d'une entreprise neuve avec une certification reconnue : vous perdez trésorerie,
        postes, chefs de service et matériel, mais conservez <b>étoiles, R&amp;D et objectifs</b>.
        Chaque étoile ajoute <b>+${perStar} %</b> de chiffre d'affaires, définitivement.
      </div>
      <div class="card-meta">
        Bonus actuel : <b>${multiplier(1 + state.stars * perStar / 100)}</b> —
        prochaine étoile à ${money(nextStarAt(state))} de CA cumulé
        (vous en êtes à ${money(state.stats.lifetimeEarned)}).
      </div>
      <button class="big gold" type="button" data-act="certify" ${gain > 0 ? '' : 'disabled'}>
        <span>${gain > 0 ? 'Se certifier' : 'Pas encore éligible'}</span>
        <small>+${fmt(gain)} ⭐</small>
      </button>
    </div>

    <div class="section-head"><h2>Objectifs</h2>
      <span class="hint">${unlocked} / ${ACHIEVEMENTS.length} · ${multiplier(1 + unlocked * 0.02)}</span></div>
    <div class="ach-grid">
      ${ACHIEVEMENTS.map((a) => `
        <div class="ach ${state.achievements[a.id] ? 'on' : ''}">
          <div class="n">${state.achievements[a.id] ? '✔ ' : ''}${a.name}</div>
          <div class="d">${a.desc}</div>
        </div>`).join('')}
    </div>

    <div class="section-head"><h2>Tableau de bord</h2></div>
    <div class="stats">
      <div><span>CA automatisé</span><b>${money(incomePerSecond(state))}/s</b></div>
      <div><span>CA potentiel (tous services)</span><b>${money(G.potentialPerSecond(state))}/s</b></div>
      <div><span>Multiplicateur global</span><b>×${fmt(globalMult(state))}</b></div>
      <div><span>CA de cette entreprise</span><b>${money(state.stats.runEarned)}</b></div>
      <div><span>CA cumulé depuis toujours</span><b>${money(state.stats.lifetimeEarned)}</b></div>
      <div><span>Certifications obtenues</span><b>${fmt(state.stats.certifications)}</b></div>
      <div><span>Contrats livrés</span><b>${fmt(state.stats.contractsDone)}</b></div>
      <div><span>Temps de jeu</span><b>${duration(state.stats.playTime)}</b></div>
      <div><span>Hors-ligne</span><b>${duration(offlineCap(state))} à ${percent(offlineRate(state))}</b></div>
    </div>`;
}

function confirmCertify() {
  const gain = starsAvailable(game.state);
  openModal({
    title: 'Passer la certification ?',
    body: `<p>Vous gagnez <b>${fmt(gain)} étoile${gain > 1 ? 's' : ''}</b> et repartez d'une
           entreprise neuve. Trésorerie, postes, chefs de service et matériel sont perdus ;
           R&amp;D, étoiles et objectifs sont conservés.</p>`,
    actions: [
      { label: 'Annuler', cls: 'big ghost' },
      {
        label: 'Se certifier', cls: 'big gold', onClick() {
          if (G.certify()) {
            closeSheet();
            lastTop = -1;
            tower.reframe();
            toast(`⭐ Certification obtenue : +${fmt(gain)} étoiles`, 'gold');
            render();
          }
        },
      },
    ],
  });
}

// ------------------------------------------------- rafraîchissement

export function frame() {
  const state = game.state;
  el.cash.textContent = money(state.cash);
  el.cps.textContent = money(incomePerSecond(state)) + '/s';
  el.stars.textContent = fmt(state.stars);
  el.rd.textContent = fmt(state.rd);

  updateBoost(state);
  if (state.tab === 'services') {
    tower.updateTower(state);
    tower.updateBuildSlot(state, el.view);
  }
  if (sheet) updateSheet(state);
  updateCosts(state);
  updateTimers(state);
  updateDots(state);
}

/** Relaie un cycle payé vers la tour pour l'animation des gains. */
export function onCycle(floorId, amount) {
  if (game.state.tab !== 'services') return;
  tower.popCoin(floorId, amount);
}

/** Le panneau doit refléter un recrutement de chef de service. */
export function refreshSheetManager() {
  if (sheet) paintManager(game.state);
}

function updateBoost(state) {
  const spec = boostSpec(state);
  if (!spec) { el.boost.hidden = true; return; }
  el.boost.hidden = false;
  const now = state.stats.playTime;
  if (state.boost) {
    el.boost.classList.add('live');
    el.boost.disabled = true;
    el.boostLabel.textContent = `×${state.boost.mult} — ${duration(state.boost.until - now)}`;
  } else if (now < state.boostReadyAt) {
    el.boost.classList.remove('live');
    el.boost.disabled = true;
    el.boostLabel.textContent = duration(state.boostReadyAt - now);
  } else {
    el.boost.classList.remove('live');
    el.boost.disabled = false;
    el.boostLabel.textContent = `Renfort ×${spec.mult}`;
  }
}

function collectCostRefs() {
  refs.costs = [];
  const scopes = [el.view, el.sheet];
  for (const scope of scopes) {
    for (const node of scope.querySelectorAll('[data-cost]')) {
      refs.costs.push({ node, cost: Number(node.dataset.cost), currency: 'cash' });
    }
    for (const node of scope.querySelectorAll('[data-rd-cost]')) {
      refs.costs.push({ node, cost: Number(node.dataset.rdCost), currency: 'rd' });
    }
  }
}

function updateCosts(state) {
  for (const r of refs.costs) {
    r.node.disabled = (r.currency === 'rd' ? state.rd : state.cash) < r.cost;
  }
}

function updateDots(state) {
  const flags = {
    upgrades: UPGRADES.some((u) => isUpgradeAvailable(state, u) && state.cash >= u.cost),
    contracts: state.offers.length > 0,
    research: RESEARCH.some((r) => isResearchAvailable(state, r) && state.rd >= r.cost),
    cert: starsAvailable(state) > 0,
  };
  for (const btn of el.tabs.querySelectorAll('.tab')) {
    const dot = btn.querySelector('.dot');
    if (dot) dot.hidden = !flags[btn.dataset.tab];
  }
}

// --------------------------------------------------- notifications

export function toast(text, kind = '') {
  const node = document.createElement('div');
  node.className = 'toast ' + kind;
  node.innerHTML = text;
  el.toasts.appendChild(node);
  setTimeout(() => {
    node.style.opacity = '0';
    setTimeout(() => node.remove(), 320);
  }, 2200);
  while (el.toasts.children.length > 3) el.toasts.firstChild.remove();
}

export function openModal({ title, body, actions }) {
  el.modalTitle.textContent = title;
  el.modalBody.innerHTML = body;
  el.modalActions.innerHTML = '';
  for (const a of actions) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = a.cls || 'big';
    btn.innerHTML = `<span>${a.label}</span>`;
    btn.addEventListener('click', () => {
      closeModal();
      if (a.onClick) a.onClick();
    });
    el.modalActions.appendChild(btn);
  }
  el.modal.hidden = false;
}

export function closeModal() {
  el.modal.hidden = true;
}

function openMenu() {
  const state = game.state;
  openModal({
    title: 'Sûreté Tycoon',
    body: `
      <p>Le jeu idle du monde de la sûreté : vidéosurveillance, alarmes intrusion,
      contrôle d'accès, détection incendie et télésurveillance.</p>
      <p>La partie se sauvegarde toute seule dans ce navigateur.
      Temps de jeu : <b>${duration(state.stats.playTime)}</b>.</p>`,
    actions: [
      { label: 'Fermer', cls: 'big ghost' },
      {
        label: 'Tout effacer', cls: 'big', onClick() {
          openModal({
            title: 'Effacer la partie ?',
            body: '<p>La sauvegarde locale sera supprimée définitivement, étoiles et R&amp;D comprises.</p>',
            actions: [
              { label: 'Annuler', cls: 'big ghost' },
              {
                label: 'Effacer', cls: 'big gold', onClick() {
                  G.hardReset();
                  closeSheet();
                  lastTop = -1;
                  tower.reframe();
                  render();
                },
              },
            ],
          });
        },
      },
    ],
  });
}
