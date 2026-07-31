// Point d'entrée : démarrage, boucle d'animation, sauvegarde périodique.

import * as G from './game.js';
import { game } from './game.js';
import { initUI, render, frame, toast, openModal, onCycle, refreshSheetManager } from './ui.js';
import { fmt, money, duration, percent } from './format.js';

const MAX_STEP = 0.25;      // pas maximum d'un tick, évite les sauts après un lag
const SAVE_EVERY = 10;      // secondes entre deux sauvegardes automatiques
const RENDER_EVERY = 1.0;   // reconstruction périodique de l'onglet courant

let last = performance.now();
let sinceSave = 0;
let sinceRender = 0;

function loop(now) {
  const dt = Math.min(MAX_STEP, Math.max(0, (now - last) / 1000));
  last = now;

  G.tick(dt);
  frame();

  sinceSave += dt;
  if (sinceSave >= SAVE_EVERY) {
    sinceSave = 0;
    G.saveNow();
  }

  // Les onglets autres que « Services » changent peu : un rafraîchissement
  // structurel par seconde suffit à faire apparaître offres et déblocages.
  sinceRender += dt;
  if (sinceRender >= RENDER_EVERY) {
    sinceRender = 0;
    if (game.state.tab !== 'services') render();
  }

  requestAnimationFrame(loop);
}

function wireEvents() {
  game.on((event, payload) => {
    switch (event) {
      case 'floor-opened':
        toast(`${payload.icon} Service ouvert : <b>${payload.name}</b>`, 'good');
        break;
      case 'manager':
        toast(`👔 ${payload.manager} prend la tête de « ${payload.name} »`, 'good');
        refreshSheetManager();
        break;
      case 'cycle':
        onCycle(payload.def.id, payload.amount);
        break;
      case 'offer':
        toast(`📄 Nouvel appel d'offres : <b>${payload.name}</b>`);
        break;
      case 'contract-done':
        toast(`✅ Chantier livré : <b>${money(payload.reward)}</b> + ${payload.rd} pts R&D`, 'good');
        break;
      case 'achievement':
        toast(`🏅 Objectif atteint : <b>${payload.name}</b>`, 'gold');
        break;
      case 'boost-start':
        toast(`⚡ Renfort d'équipe ×${payload.mult} pendant ${duration(payload.duration)}`, 'gold');
        break;
      case 'boost-end':
        toast('Le renfort d\'équipe est terminé.');
        break;
      case 'research':
        toast(`🔬 Étude terminée : <b>${payload.name}</b>`, 'good');
        break;
      default:
        break;
    }
  });

  // Sauvegarde dès que l'onglet passe en arrière-plan (mobile compris).
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') G.saveNow();
    else last = performance.now();
  });
  window.addEventListener('pagehide', () => G.saveNow());
}

function showOfflineReport(report) {
  if (!report || report.earned <= 0) return;
  openModal({
    title: 'Rapport de la nuit',
    body: `<p>Vos services automatisés ont tourné pendant <b>${duration(report.capped)}</b>
           ${report.capped < report.elapsed ? `(absence de ${duration(report.elapsed)}, plafond atteint)` : ''}
           au rendement de <b>${percent(report.rate)}</b>.</p>
           <p style="font-size:20px;color:var(--green)"><b>+ ${money(report.earned)}</b></p>`,
    actions: [{ label: 'Encaisser', cls: 'btn primary' }],
  });
}

function start() {
  const report = G.boot();
  initUI();
  wireEvents();
  showOfflineReport(report);
  last = performance.now();
  requestAnimationFrame(loop);

  if ('serviceWorker' in navigator && location.protocol.startsWith('http')) {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  }
}

start();

// Petite aide au débogage depuis la console.
window.SURETE = { game, G, fmt };
