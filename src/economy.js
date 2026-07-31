// Toute l'arithmétique du jeu : coûts, multiplicateurs, production, certification.

import { FLOORS, MILESTONES, UPGRADES, RESEARCH } from './data.js';

export const FLOOR_BY_ID = Object.fromEntries(FLOORS.map((f) => [f.id, f]));
export const UPGRADE_BY_ID = Object.fromEntries(UPGRADES.map((u) => [u.id, u]));
export const RESEARCH_BY_ID = Object.fromEntries(RESEARCH.map((r) => [r.id, r]));

export const OFFLINE_BASE_CAP = 4 * 3600;
export const STAR_DIVISOR = 1e12; // CA cumulé nécessaire pour la formule d'étoiles

// ---------------------------------------------------------------- coûts

/** Coût cumulé de `qty` postes supplémentaires (suite géométrique). */
export function costOf(def, owned, qty) {
  if (qty <= 0) return 0;
  const r = def.growth;
  return (def.baseCost * Math.pow(r, owned) * (Math.pow(r, qty) - 1)) / (r - 1);
}

/** Nombre maximum de postes achetables avec `cash`. */
export function maxAffordable(def, owned, cash) {
  const r = def.growth;
  const first = def.baseCost * Math.pow(r, owned);
  if (cash < first) return 0;
  const n = Math.floor(Math.log((cash * (r - 1)) / first + 1) / Math.log(r));
  // Garde-fou contre les erreurs d'arrondi en virgule flottante.
  return costOf(def, owned, n) > cash ? Math.max(0, n - 1) : n;
}

/** Résout le sélecteur x1 / x10 / x100 / MAX en une quantité concrète. */
export function resolveQty(def, owned, cash, qty) {
  if (qty === 'max') return Math.max(1, maxAffordable(def, owned, cash));
  return qty;
}

// -------------------------------------------------- multiplicateurs

/** Doublements accordés par les paliers de postes atteints. */
export function milestoneMult(count) {
  let m = 1;
  for (const step of MILESTONES) {
    if (count >= step) m *= 2;
  }
  if (count > 1000) m *= Math.pow(2, Math.floor((count - 1000) / 100));
  return m;
}

/** Multiplicateur de CA global (étoiles, R&D, objectifs, améliorations, renfort). */
export function globalMult(state) {
  let m = 1;

  const perStar = state.research.qualite ? 0.03 : 0.02;
  m *= 1 + state.stars * perStar;

  if (state.research.proc) m *= 1.25;
  if (state.research.indus) m *= 1.5;
  if (state.research.excellence) m *= 2;

  m *= 1 + 0.02 * Object.keys(state.achievements).length;

  for (const id of Object.keys(state.upgrades)) {
    const u = UPGRADE_BY_ID[id];
    if (u && u.target === 'all' && u.kind === 'revenue') m *= u.mult;
  }

  if (state.boost && state.boost.until > state.stats.playTime) m *= state.boost.mult;

  return m;
}

/** Multiplicateur de CA propre à un service. */
export function floorMult(state, floorId) {
  let m = 1;
  for (const id of Object.keys(state.upgrades)) {
    const u = UPGRADE_BY_ID[id];
    if (u && u.target === floorId && u.kind === 'revenue') m *= u.mult;
  }
  return m;
}

/** Accélération des cycles : améliorations de vitesse ciblées + globales. */
export function speedMult(state, floorId) {
  let m = 1;
  for (const id of Object.keys(state.upgrades)) {
    const u = UPGRADE_BY_ID[id];
    if (u && u.kind === 'speed' && (u.target === floorId || u.target === 'all')) m *= u.mult;
  }
  return m;
}

// ------------------------------------------------------- production

/** Durée d'un cycle, en secondes. */
export function cycleTime(state, def) {
  return def.baseTime / speedMult(state, def.id);
}

/** CA versé à la fin d'un cycle complet. */
export function cycleRevenue(state, def) {
  const f = state.floors[def.id];
  if (!f || f.count === 0) return 0;
  return def.baseRevenue * f.count * milestoneMult(f.count) * floorMult(state, def.id) * globalMult(state);
}

/** CA par seconde d'un service (indépendamment du fait qu'il tourne ou non). */
export function floorPerSecond(state, def) {
  const t = cycleTime(state, def);
  return t > 0 ? cycleRevenue(state, def) / t : 0;
}

/** CA par seconde réellement produit : seuls les services automatisés comptent. */
export function incomePerSecond(state, { managedOnly = true } = {}) {
  let total = 0;
  for (const def of FLOORS) {
    const f = state.floors[def.id];
    if (!f || f.count === 0) continue;
    if (managedOnly && !f.manager) continue;
    total += floorPerSecond(state, def);
  }
  return total;
}

// ------------------------------------------------------- hors-ligne

export function offlineCap(state) {
  if (state.research.pc247) return 16 * 3600;
  if (state.research.veille) return 8 * 3600;
  return OFFLINE_BASE_CAP;
}

export function offlineRate(state) {
  return state.research.pc247 ? 0.75 : 0.5;
}

// ---------------------------------------------------- certification

/** Étoiles que rapporterait une certification immédiate. */
export function starsAvailable(state) {
  const bonus = state.research.dossier ? 1.3 : 1;
  const total = 150 * Math.sqrt(state.stats.lifetimeEarned / STAR_DIVISOR) * bonus;
  return Math.max(0, Math.floor(total) - state.stars);
}

/** CA cumulé nécessaire pour gagner au moins une étoile de plus. */
export function nextStarAt(state) {
  const bonus = state.research.dossier ? 1.3 : 1;
  const target = state.stars + 1;
  return Math.pow(target / (150 * bonus), 2) * STAR_DIVISOR;
}

// --------------------------------------------------------- contrats

export function contractRewardMult(state) {
  return state.research.commerce ? 1.5 : 1;
}

export function contractRdMult(state) {
  return state.research.bureau ? 2 : 1;
}

export function offerInterval(state) {
  return state.research.comptes ? 45 : 90;
}

export function boostSpec(state) {
  if (state.research.reserve) return { mult: 5, duration: 90, cooldown: 180 };
  if (state.research.renfort) return { mult: 3, duration: 60, cooldown: 300 };
  return null;
}

// ------------------------------------------------ disponibilité UI

/** Un service est visible dès que le précédent a au moins un poste. */
export function isFloorVisible(state, index) {
  if (index === 0) return true;
  const prev = FLOORS[index - 1];
  return (state.floors[prev.id]?.count || 0) > 0;
}

export function isUpgradeAvailable(state, u) {
  if (state.upgrades[u.id]) return false;
  return (state.floors[u.req.floor]?.count || 0) >= u.req.count;
}

export function isResearchAvailable(state, r) {
  if (state.research[r.id]) return false;
  return r.req.every((id) => state.research[id]);
}
