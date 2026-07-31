// État de la partie : création, sauvegarde locale, chargement, migrations.

import { FLOORS } from './data.js';

export const SAVE_KEY = 'idle-videosurete-save-v1';
export const SAVE_VERSION = 1;

export function newState() {
  const floors = {};
  for (const f of FLOORS) {
    floors[f.id] = { count: 0, manager: false, progress: 0, running: false };
  }
  return {
    version: SAVE_VERSION,
    cash: 4,
    stars: 0,
    rd: 0,
    floors,
    upgrades: {},
    research: {},
    achievements: {},
    offers: [],          // appels d'offres proposés, non acceptés
    contracts: [],       // appels d'offres en cours
    nextOfferAt: 25,     // secondes de jeu avant la prochaine proposition
    boost: null,         // { mult, until } — until en secondes de jeu
    boostReadyAt: 0,
    stats: {
      lifetimeEarned: 0,   // CA cumulé depuis toujours (sert aux étoiles)
      runEarned: 0,        // CA cumulé depuis la dernière certification
      contractsDone: 0,
      certifications: 0,
      clicks: 0,
      playTime: 0,         // secondes de jeu écoulées
      startedAt: Date.now(),
    },
    lastSeen: Date.now(),
    tab: 'services',
    buyQty: 1,
  };
}

/** Remet à zéro l'entreprise en conservant certification, R&D et objectifs. */
export function certifiedReset(state, gainedStars) {
  const fresh = newState();
  fresh.stars = state.stars + gainedStars;
  fresh.rd = state.rd;
  fresh.research = { ...state.research };
  fresh.achievements = { ...state.achievements };
  fresh.stats = {
    ...state.stats,
    runEarned: 0,
    certifications: state.stats.certifications + 1,
  };
  fresh.tab = state.tab;
  fresh.buyQty = state.buyQty;
  return fresh;
}

export function save(state) {
  try {
    state.lastSeen = Date.now();
    localStorage.setItem(SAVE_KEY, JSON.stringify(state));
    return true;
  } catch (err) {
    console.warn('Sauvegarde impossible', err);
    return false;
  }
}

export function load() {
  let raw;
  try {
    raw = localStorage.getItem(SAVE_KEY);
  } catch (err) {
    console.warn('Lecture de la sauvegarde impossible', err);
    return null;
  }
  if (!raw) return null;
  try {
    return migrate(JSON.parse(raw));
  } catch (err) {
    console.warn('Sauvegarde illisible, nouvelle partie', err);
    return null;
  }
}

export function wipe() {
  try {
    localStorage.removeItem(SAVE_KEY);
  } catch (err) {
    console.warn('Suppression impossible', err);
  }
}

/** Complète une sauvegarde ancienne ou partielle avec les valeurs par défaut. */
function migrate(data) {
  const base = newState();
  const state = { ...base, ...data };

  state.stats = { ...base.stats, ...(data.stats || {}) };
  state.floors = { ...base.floors };
  for (const f of FLOORS) {
    const saved = data.floors && data.floors[f.id];
    if (saved) {
      state.floors[f.id] = {
        count: num(saved.count),
        manager: !!saved.manager,
        progress: num(saved.progress),
        running: !!saved.running || !!saved.manager,
      };
    }
  }
  state.upgrades = data.upgrades || {};
  state.research = data.research || {};
  state.achievements = data.achievements || {};
  state.offers = Array.isArray(data.offers) ? data.offers : [];
  state.contracts = Array.isArray(data.contracts) ? data.contracts : [];
  state.cash = num(data.cash, base.cash);
  state.stars = num(data.stars);
  state.rd = num(data.rd);
  state.version = SAVE_VERSION;
  return state;
}

function num(v, fallback = 0) {
  return typeof v === 'number' && isFinite(v) && v >= 0 ? v : fallback;
}
