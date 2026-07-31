// Rendu DOM : construit chaque onglet puis rafraîchit les valeurs « chaudes »
// (trésorerie, barres de cycle, minuteurs) à chaque image.

import { FLOORS, UPGRADES, RESEARCH, CONTRACTS, ACHIEVEMENTS, MILESTONES } from './data.js';
import {
  FLOOR_BY_ID, costOf, resolveQty, maxAffordable, milestoneMult,
  cycleTime, cycleRevenue, floorPerSecond, incomePerSecond,
  starsAvailable, nextStarAt, globalMult, boostSpec, offlineCap, offlineRate,
  isFloorVisible, isUpgradeAvailable, isResearchAvailable,
} from './economy.js';
import { fmt, money, duration, multiplier, percent } from './format.js';
import * as G from './game.js';
import { game } from './game.js';

const CONTRACT_TPL = Object.fromEntries(CONTRACTS.map((c) => [c.id, c]));
const QTYS = [1, 10, 100, 'max'];

const el = {};
let refs = {};

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
  el.modal = document.getElementById('modal');
  el.modalTitle = document.getElementById('modal-title');
  el.modalBody = document.getElementById('modal-body');
  el.modalActions = document.getElementById('modal-actions');

  el.tabs.addEventListener('click', (e) => {
    const btn = e.target.closest('.tab');
    if (!btn) return;
    game.state.tab = btn.dataset.tab;
    render();
  });

  el.view.addEventListener('click', onViewClick);
  el.boost.addEventListener('click', () => G.triggerBoost());
  document.getElementById('btn-menu').addEventListener('click', openMenu);
  el.modal.addEventListener('click', (e) => {
    if (e.target === el.modal) closeModal();
  });

  render();
}

// ------------------------------------------------------------ clics

function onViewClick(e) {
  const node = e.target.closest('[data-act]');
  if (!node) return;
  const { act, id, uid, qty } = node.dataset;
  let changed = true;

  switch (act) {
    case 'qty':
      game.state.buyQty = qty === 'max' ? 'max' : Number(qty);
      break;
    case 'buy':
      if (G.buyFloor(id, game.state.buyQty)) bump(node);
      else changed = false;
      break;
    case 'run':
      changed = G.runFloor(id);
      break;
    case 'mgr':
      changed = G.hireManager(id);
      break;
    case 'upg':
      changed = G.buyUpgrade(id);
      break;
    case 'res':
      changed = G.buyResearch(id);
      break;
    case 'accept':
      changed = G.acceptOffer(Number(uid));
      break;
    case 'decline':
      changed = G.declineOffer(Number(uid));
      break;
    case 'certify':
      confirmCertify();
      changed = false;
      break;
    default:
      changed = false;
  }
  if (changed) render();
}

function bump(node) {
  node.animate(
    [{ transform: 'scale(1)' }, { transform: 'scale(.94)' }, { transform: 'scale(1)' }],
    { duration: 140 }
  );
}

// ----------------------------------------------------------- rendu

export function render() {
  const state = game.state;
  refs = { floors: [], costs: [], timers: [] };

  for (const btn of el.tabs.querySelectorAll('.tab')) {
    btn.classList.toggle('on', btn.dataset.tab === state.tab);
  }

  switch (state.tab) {
    case 'upgrades': el.view.innerHTML = viewUpgrades(state); break;
    case 'contracts': el.view.innerHTML = viewContracts(state); break;
    case 'research': el.view.innerHTML = viewResearch(state); break;
    case 'cert': el.view.innerHTML = viewCert(state); break;
    default: el.view.innerHTML = viewServices(state); collectFloorRefs(state);
  }
  collectCostRefs();
  collectTimerRefs();
  frame();
}

// -------------------------------------------------- onglet Services

function viewServices(state) {
  const rows = [];
  for (let i = 0; i < FLOORS.length; i++) {
    if (!isFloorVisible(state, i)) {
      // Un seul emplacement verrouillé suffit à montrer qu'il reste des métiers.
      rows.push(lockedFloor(i));
      break;
    }
    rows.push(floorCard(state, FLOORS[i]));
  }

  return `
    <div class="qty-row">
      ${QTYS.map((q) => `
        <button class="qty ${state.buyQty === q ? 'on' : ''}" data-act="qty" data-qty="${q}" type="button">
          ${q === 'max' ? 'MAX' : '×' + q}
        </button>`).join('')}
    </div>
    ${rows.join('')}
  `;
}

function lockedFloor(i) {
  return `
    <div class="card locked">
      <div class="card-top">
        <div class="thumb">🔒</div>
        <div class="card-main">
          <div class="card-title">Service verrouillé</div>
          <div class="card-sub">Ouvrez « ${FLOORS[i - 1].name} » pour débloquer la suite.</div>
        </div>
      </div>
    </div>`;
}

function floorCard(state, def) {
  const f = state.floors[def.id];
  const opened = f.count > 0;
  const next = nextMilestone(f.count);

  return `
    <div class="card" data-floor="${def.id}">
      <div class="card-top">
        <div class="thumb">${def.icon}${opened ? `<span class="lvl">${fmt(f.count)}</span>` : ''}</div>
        <div class="card-main">
          <div class="card-title">${def.name}${f.manager ? '<span class="badge done">auto</span>' : ''}</div>
          <div class="card-sub">${def.desc}</div>
          <div class="card-meta" data-role="rate"></div>
        </div>
      </div>

      <div class="bar ${f.manager ? 'auto' : ''}" data-role="bar"
           ${opened && !f.manager ? `data-act="run" data-id="${def.id}"` : 'disabled'}>
        <div class="fill" data-role="fill"></div>
        <div class="label" data-role="barlabel"></div>
      </div>

      <div class="actions">
        <button class="btn primary" data-act="buy" data-id="${def.id}" type="button"
                data-cost-role="floor" data-floor-id="${def.id}">
          <span data-role="buylabel">Recruter</span>
          <small data-role="buycost">—</small>
        </button>
        ${f.manager
          ? `<button class="btn" disabled type="button"><span>${def.manager}</span><small>chef de service</small></button>`
          : `<button class="btn gold" data-act="mgr" data-id="${def.id}" type="button"
                     ${opened ? `data-cost="${def.managerCost}"` : 'disabled'}>
               <span>Chef de service</span><small>${money(def.managerCost)}</small>
             </button>`}
      </div>

      ${opened ? `<div class="card-meta" style="margin-top:8px;color:var(--muted)">
        Palier suivant : <b style="color:var(--amber)">${next} ${def.unit}s</b> → ×2 de CA</div>` : ''}
    </div>`;
}

function nextMilestone(count) {
  for (const m of MILESTONES) if (count < m) return m;
  return Math.ceil((count + 1) / 100) * 100;
}

function collectFloorRefs(state) {
  refs.floors = [];
  for (const def of FLOORS) {
    const card = el.view.querySelector(`[data-floor="${def.id}"]`);
    if (!card) continue;
    refs.floors.push({
      def,
      fill: card.querySelector('[data-role="fill"]'),
      barLabel: card.querySelector('[data-role="barlabel"]'),
      rate: card.querySelector('[data-role="rate"]'),
      buy: card.querySelector('[data-act="buy"]'),
      buyLabel: card.querySelector('[data-role="buylabel"]'),
      buyCost: card.querySelector('[data-role="buycost"]'),
      card,
    });
  }
}

function updateFloors(state) {
  for (const r of refs.floors) {
    const f = state.floors[r.def.id];
    const t = cycleTime(state, r.def);
    const pct = f.count === 0 ? 0 : Math.min(100, (f.progress / t) * 100);
    r.fill.style.width = pct + '%';

    if (f.count === 0) {
      r.barLabel.textContent = 'Service non ouvert';
      r.rate.textContent = `${money(r.def.baseRevenue)} par cycle de ${duration(r.def.baseTime)}`;
    } else if (f.running) {
      r.barLabel.textContent = `${Math.floor(pct)} %  ·  ${duration(Math.max(0, t - f.progress))}`;
      r.rate.textContent = `${money(floorPerSecond(state, r.def))}/s  ·  ×${fmt(milestoneMult(f.count))} palier`;
    } else {
      r.barLabel.textContent = `▶ Lancer  ·  ${money(cycleRevenue(state, r.def))}`;
      r.rate.textContent = `${money(cycleRevenue(state, r.def))} par cycle de ${duration(t)}`;
    }

    const qty = resolveQty(r.def, f.count, state.cash, state.buyQty);
    const price = costOf(r.def, f.count, qty);
    const canBuy = price <= state.cash && (state.buyQty !== 'max' || maxAffordable(r.def, f.count, state.cash) > 0);
    r.buyLabel.textContent = f.count === 0 ? 'Ouvrir le service' : `Recruter ×${fmt(qty)}`;
    r.buyCost.textContent = money(price);
    r.buy.disabled = !canBuy;
  }
}

// ------------------------------------------------- onglet Matériel

function viewUpgrades(state) {
  const available = UPGRADES.filter((u) => isUpgradeAvailable(state, u));
  const owned = UPGRADES.filter((u) => state.upgrades[u.id]);
  const locked = UPGRADES.filter((u) => !state.upgrades[u.id] && !isUpgradeAvailable(state, u));

  return `
    <div class="section-head"><h2>Investissements disponibles</h2>
      <span class="hint">${available.length} offre${available.length > 1 ? 's' : ''}</span></div>
    ${available.length
      ? available.sort((a, b) => a.cost - b.cost).map((u) => upgradeCard(u, 'buy')).join('')
      : '<div class="empty">Développez vos services : les fournisseurs vous rappelleront.</div>'}

    ${locked.length ? `<div class="section-head"><h2>Bientôt accessible</h2></div>
      ${locked.slice(0, 4).map((u) => upgradeCard(u, 'locked')).join('')}` : ''}

    ${owned.length ? `<div class="section-head"><h2>Déjà en parc</h2>
      <span class="hint">${owned.length}</span></div>
      ${owned.map((u) => upgradeCard(u, 'owned')).join('')}` : ''}
  `;
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
            ? `<div class="card-meta" style="color:var(--muted)">Nécessite ${u.req.count} postes en « ${req.name} »</div>`
            : ''}
        </div>
      </div>
      ${mode === 'buy'
        ? `<div class="actions"><button class="btn primary wide" type="button"
             data-act="upg" data-id="${u.id}" data-cost="${u.cost}">
             <span>Investir</span><small>${money(u.cost)}</small></button></div>`
        : mode === 'owned'
          ? '<div class="card-meta" style="color:var(--green)">✔ Acquis</div>'
          : ''}
    </div>`;
}

// ------------------------------------------------- onglet Contrats

function viewContracts(state) {
  const perSec = G.potentialPerSecond(state);
  return `
    <div class="section-head"><h2>Appels d'offres</h2>
      <span class="hint">CA de référence ${money(perSec)}/s</span></div>
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
      Un appel d'offres immobilise vos équipes pendant sa durée puis verse une prime
      calculée sur votre chiffre d'affaires du moment, plus des points de R&amp;D.
      Une proposition non traitée expire au bout de ${45} s.
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
      <div class="actions">
        <button class="btn primary" type="button" data-act="accept" data-uid="${o.uid}" ${full ? 'disabled' : ''}>
          <span>${full ? 'Équipes occupées' : 'Signer'}</span><small>${money(reward)}</small></button>
        <button class="btn" type="button" data-act="decline" data-uid="${o.uid}"><span>Refuser</span><small>&nbsp;</small></button>
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
      kind,
      uid: Number(uid),
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
      const done = 1 - (c.endsAt - now) / c.duration;
      t.fill.style.width = Math.max(0, Math.min(100, done * 100)) + '%';
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
      ${g.list.map((r) => researchCard(state, r, g.title)).join('')}` : '').join('')}
  `;
}

function researchCard(state, r, group) {
  const locked = group === 'Verrouillé';
  const owned = group === 'Acquis';
  const missing = r.req.filter((id) => !state.research[id])
    .map((id) => RESEARCH.find((x) => x.id === id)?.name)
    .filter(Boolean);
  return `
    <div class="card ${locked ? 'locked' : ''}">
      <div class="card-top">
        <div class="thumb">${r.icon}</div>
        <div class="card-main">
          <div class="card-title">${r.name}${owned ? '<span class="badge done">acquis</span>' : ''}</div>
          <div class="card-sub">${r.desc}</div>
          ${locked && missing.length
            ? `<div class="card-meta" style="color:var(--muted)">Nécessite : ${missing.join(', ')}</div>` : ''}
        </div>
      </div>
      ${!owned && !locked ? `
        <div class="actions"><button class="btn violet wide" type="button"
          data-act="res" data-id="${r.id}" data-rd-cost="${r.cost}">
          <span>Lancer l'étude</span><small>🔬 ${fmt(r.cost)}</small></button></div>` : ''}
    </div>`;
}

// -------------------------------------------- onglet Certification

function viewCert(state) {
  const gain = starsAvailable(state);
  const target = nextStarAt(state);
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
      <div class="card-meta" style="margin-top:10px">
        Bonus actuel : <b>${multiplier(1 + state.stars * perStar / 100)}</b><br>
        Prochaine étoile à ${money(target)} de CA cumulé (vous en êtes à ${money(state.stats.lifetimeEarned)})
      </div>
      <div class="actions">
        <button class="btn gold wide" type="button" data-act="certify" ${gain > 0 ? '' : 'disabled'}>
          <span>${gain > 0 ? 'Se certifier' : 'Pas encore éligible'}</span>
          <small>+${fmt(gain)} ⭐</small>
        </button>
      </div>
    </div>

    <div class="section-head"><h2>Objectifs</h2><span class="hint">${unlocked} / ${ACHIEVEMENTS.length} · ${multiplier(1 + unlocked * 0.02)}</span></div>
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
      { label: 'Annuler', cls: 'btn' },
      {
        label: 'Se certifier', cls: 'btn gold', onClick() {
          if (G.certify()) {
            toast(`⭐ Certification obtenue : +${fmt(gain)} étoiles`, 'gold');
            render();
          }
        },
      },
    ],
  });
}

// ------------------------------------------------- rafraîchissement

/** Appelé à chaque image : uniquement des mises à jour de texte/largeur. */
export function frame() {
  const state = game.state;
  el.cash.textContent = money(state.cash);
  el.cps.textContent = money(incomePerSecond(state)) + '/s';
  el.stars.textContent = fmt(state.stars);
  el.rd.textContent = fmt(state.rd);

  updateBoost(state);
  if (state.tab === 'services') updateFloors(state);
  updateCosts(state);
  updateTimers(state);
  updateDots(state);
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
  for (const node of el.view.querySelectorAll('[data-cost]')) {
    refs.costs.push({ node, cost: Number(node.dataset.cost), currency: 'cash' });
  }
  for (const node of el.view.querySelectorAll('[data-rd-cost]')) {
    refs.costs.push({ node, cost: Number(node.dataset.rdCost), currency: 'rd' });
  }
}

function updateCosts(state) {
  for (const r of refs.costs) {
    const have = r.currency === 'rd' ? state.rd : state.cash;
    r.node.disabled = have < r.cost;
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

// ---------------------------------------------------- notifications

export function toast(text, kind = '') {
  const node = document.createElement('div');
  node.className = 'toast ' + kind;
  node.innerHTML = text;
  el.toasts.appendChild(node);
  setTimeout(() => {
    node.style.transition = 'opacity .3s';
    node.style.opacity = '0';
    setTimeout(() => node.remove(), 320);
  }, 2600);
  while (el.toasts.children.length > 4) el.toasts.firstChild.remove();
}

export function openModal({ title, body, actions }) {
  el.modalTitle.textContent = title;
  el.modalBody.innerHTML = body;
  el.modalActions.innerHTML = '';
  for (const a of actions) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = a.cls || 'btn';
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
      <p>Jeu idle du monde de la sûreté : vidéosurveillance, alarmes intrusion,
      contrôle d'accès, détection incendie et télésurveillance.</p>
      <p>La partie se sauvegarde toute seule dans ce navigateur.
      Temps de jeu : <b>${duration(state.stats.playTime)}</b>.</p>`,
    actions: [
      { label: 'Fermer', cls: 'btn' },
      {
        label: 'Tout effacer', cls: 'btn', onClick() {
          openModal({
            title: 'Effacer la partie ?',
            body: '<p>La sauvegarde locale sera supprimée définitivement, étoiles et R&amp;D comprises.</p>',
            actions: [
              { label: 'Annuler', cls: 'btn' },
              { label: 'Effacer', cls: 'btn gold', onClick() { G.hardReset(); render(); } },
            ],
          });
        },
      },
    ],
  });
}
