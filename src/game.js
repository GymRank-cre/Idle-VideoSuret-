// Boucle de jeu et actions du joueur. Aucune manipulation du DOM ici.

import { FLOORS, CONTRACTS, ACHIEVEMENTS, UPGRADES, RESEARCH } from './data.js';
import {
  FLOOR_BY_ID, UPGRADE_BY_ID, RESEARCH_BY_ID,
  costOf, resolveQty, cycleTime, cycleRevenue, incomePerSecond,
  offlineCap, offlineRate, starsAvailable, globalMult,
  contractRewardMult, contractRdMult, offerInterval, boostSpec,
  isUpgradeAvailable, isResearchAvailable,
} from './economy.js';
import { newState, certifiedReset, save, load, wipe } from './state.js';

const OFFER_TTL = 45;      // secondes avant qu'une proposition expire
const MAX_OFFERS = 2;
const MAX_ACTIVE = 2;
const CONTRACT_TPL = Object.fromEntries(CONTRACTS.map((c) => [c.id, c]));

export const game = {
  state: newState(),
  listeners: [],
  offlineReport: null,
  uid: 1,

  on(fn) {
    this.listeners.push(fn);
  },
  emit(event, payload) {
    for (const fn of this.listeners) fn(event, payload);
  },
};

// ------------------------------------------------------- démarrage

export function boot() {
  const saved = load();
  if (saved) {
    game.state = saved;
    game.offlineReport = collectOffline(game.state, saved.lastSeen);
  } else {
    game.state = newState();
  }
  game.uid = 1 + Math.max(0, ...game.state.offers.map((o) => o.uid), ...game.state.contracts.map((c) => c.uid));
  return game.offlineReport;
}

/** Crédite la production des services automatisés pendant l'absence. */
function collectOffline(state, lastSeen) {
  const elapsed = Math.max(0, (Date.now() - (lastSeen || Date.now())) / 1000);
  if (elapsed < 60) return null;
  const capped = Math.min(elapsed, offlineCap(state));
  const rate = offlineRate(state);
  const perSec = incomePerSecond({ ...state, boost: null });
  const earned = perSec * capped * rate;
  if (earned <= 0) return { elapsed, capped, earned: 0, rate };
  earn(state, earned);
  return { elapsed, capped, earned, rate };
}

// ------------------------------------------------------ production

export function earn(state, amount) {
  if (!(amount > 0)) return;
  state.cash += amount;
  state.stats.lifetimeEarned += amount;
  state.stats.runEarned += amount;
}

export function tick(dt) {
  const state = game.state;
  state.stats.playTime += dt;

  for (const def of FLOORS) {
    const f = state.floors[def.id];
    if (!f || f.count === 0) continue;
    if (f.manager) f.running = true;
    if (!f.running) continue;

    const t = cycleTime(state, def);
    f.progress += dt;
    if (f.progress < t) continue;

    if (f.manager) {
      const cycles = Math.floor(f.progress / t);
      const gain = cycleRevenue(state, def) * cycles;
      earn(state, gain);
      f.progress -= cycles * t;
      game.emit('cycle', { def, amount: gain });
    } else {
      const gain = cycleRevenue(state, def);
      earn(state, gain);
      f.progress = 0;
      f.running = false;
      game.emit('cycle', { def, amount: gain });
    }
  }

  tickContracts(state);
  tickBoost(state);
  checkAchievements(state);
}

function tickBoost(state) {
  if (state.boost && state.boost.until <= state.stats.playTime) {
    state.boost = null;
    game.emit('boost-end');
  }
}

// -------------------------------------------------------- services

export function buyFloor(floorId, qty) {
  const state = game.state;
  const def = FLOOR_BY_ID[floorId];
  const f = state.floors[floorId];
  if (!def || !f) return false;

  const n = resolveQty(def, f.count, state.cash, qty);
  const price = costOf(def, f.count, n);
  if (n <= 0 || price > state.cash) return false;

  const wasEmpty = f.count === 0;
  state.cash -= price;
  f.count += n;
  if (wasEmpty) game.emit('floor-opened', def);
  game.emit('purchase', { floorId, qty: n, price });
  return true;
}

export function hireManager(floorId) {
  const state = game.state;
  const def = FLOOR_BY_ID[floorId];
  const f = state.floors[floorId];
  if (!def || !f || f.manager || f.count === 0 || state.cash < def.managerCost) return false;
  state.cash -= def.managerCost;
  f.manager = true;
  f.running = true;
  game.emit('manager', def);
  return true;
}

/** Lance un cycle manuel (services sans chef de service). */
export function runFloor(floorId) {
  const state = game.state;
  const f = state.floors[floorId];
  if (!f || f.count === 0 || f.running) return false;
  f.running = true;
  state.stats.clicks++;
  return true;
}

// ---------------------------------------------------- améliorations

export function buyUpgrade(id) {
  const state = game.state;
  const u = UPGRADE_BY_ID[id];
  if (!u || !isUpgradeAvailable(state, u) || state.cash < u.cost) return false;
  state.cash -= u.cost;
  state.upgrades[id] = true;
  game.emit('upgrade', u);
  return true;
}

export function buyResearch(id) {
  const state = game.state;
  const r = RESEARCH_BY_ID[id];
  if (!r || !isResearchAvailable(state, r) || state.rd < r.cost) return false;
  state.rd -= r.cost;
  state.research[id] = true;
  game.emit('research', r);
  return true;
}

// --------------------------------------------------------- renfort

export function triggerBoost() {
  const state = game.state;
  const spec = boostSpec(state);
  if (!spec || state.boost || state.stats.playTime < state.boostReadyAt) return false;
  state.boost = { mult: spec.mult, until: state.stats.playTime + spec.duration };
  state.boostReadyAt = state.stats.playTime + spec.duration + spec.cooldown;
  game.emit('boost-start', spec);
  return true;
}

// -------------------------------------------------------- contrats

function tickContracts(state) {
  const now = state.stats.playTime;

  if (now >= state.nextOfferAt) {
    state.nextOfferAt = now + offerInterval(state) * (0.7 + Math.random() * 0.6);
    if (state.offers.length < MAX_OFFERS) {
      const tpl = pickContract(state);
      if (tpl) {
        state.offers.push({ uid: game.uid++, tpl: tpl.id, expiresAt: now + OFFER_TTL });
        game.emit('offer', tpl);
      }
    }
  }

  const kept = state.offers.filter((o) => o.expiresAt > now);
  if (kept.length !== state.offers.length) state.offers = kept;

  for (let i = state.contracts.length - 1; i >= 0; i--) {
    const c = state.contracts[i];
    if (c.endsAt > now) continue;
    state.contracts.splice(i, 1);
    earn(state, c.reward);
    state.rd += c.rd;
    state.stats.contractsDone++;
    game.emit('contract-done', c);
  }
}

/** Tire une offre parmi celles que l'entreprise sait honorer. */
function pickContract(state) {
  const open = FLOORS.filter((f) => (state.floors[f.id]?.count || 0) > 0).length;
  const pool = CONTRACTS.filter((c) => c.minFloor < Math.max(1, open));
  if (!pool.length) return null;
  // Favorise les contrats les plus élevés accessibles.
  const weighted = pool.slice(-4);
  return weighted[Math.floor(Math.random() * weighted.length)];
}

export function acceptOffer(uid) {
  const state = game.state;
  const idx = state.offers.findIndex((o) => o.uid === uid);
  if (idx < 0 || state.contracts.length >= MAX_ACTIVE) return false;
  const offer = state.offers[idx];
  const tpl = CONTRACT_TPL[offer.tpl];
  if (!tpl) return false;

  const perSec = potentialPerSecond(state);
  state.offers.splice(idx, 1);
  state.contracts.push({
    uid: offer.uid,
    tpl: tpl.id,
    endsAt: state.stats.playTime + tpl.duration,
    duration: tpl.duration,
    reward: Math.max(perSec, 1) * tpl.payout * contractRewardMult(state),
    rd: Math.round(tpl.rd * contractRdMult(state)),
  });
  game.emit('contract-start', tpl);
  return true;
}

export function declineOffer(uid) {
  const state = game.state;
  const idx = state.offers.findIndex((o) => o.uid === uid);
  if (idx < 0) return false;
  state.offers.splice(idx, 1);
  return true;
}

/** CA/s théorique de tous les services ouverts, renfort exclu. */
export function potentialPerSecond(state) {
  return incomePerSecond({ ...state, boost: null }, { managedOnly: false });
}

// --------------------------------------------------- certification

export function certify() {
  const gained = starsAvailable(game.state);
  if (gained <= 0) return false;
  game.state = certifiedReset(game.state, gained);
  game.emit('certified', gained);
  return true;
}

// -------------------------------------------------------- objectifs

function checkAchievements(state) {
  for (const a of ACHIEVEMENTS) {
    if (state.achievements[a.id]) continue;
    if (a.check(state, state.stats)) {
      state.achievements[a.id] = true;
      game.emit('achievement', a);
    }
  }
}

// ------------------------------------------------------ sauvegarde

export function saveNow() {
  return save(game.state);
}

export function hardReset() {
  wipe();
  game.state = newState();
  game.emit('reset');
}

export { UPGRADES, RESEARCH, CONTRACT_TPL, globalMult };
