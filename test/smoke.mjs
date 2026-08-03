// Test de bout en bout : sert le dossier, pilote un Chromium et vérifie que la
// boucle de jeu complète fonctionne sans erreur console.
//
//   npm install && npm test
//
// PLAYWRIGHT_CHROMIUM peut pointer un binaire déjà présent sur la machine.

import { chromium } from 'playwright';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const PORT = Number(process.env.PORT || 8731);
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
};

const server = http.createServer((req, res) => {
  let rel = decodeURIComponent(req.url.split('?')[0]);
  if (rel === '/') rel = '/index.html';
  const file = path.join(ROOT, rel);
  if (!file.startsWith(ROOT) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
    res.writeHead(404);
    return res.end('404');
  }
  res.writeHead(200, { 'content-type': MIME[path.extname(file)] || 'application/octet-stream' });
  res.end(fs.readFileSync(file));
});
await new Promise((resolve) => server.listen(PORT, resolve));
const URL = `http://localhost:${PORT}/`;
const URL_2D = URL + '?render=2d';

// Sans rendu matériel, un Chromium headless n'expose pas WebGL : on lui donne
// le rasteriseur logiciel pour que l'étape « tour 3D » soit testable partout.
const launchOptions = {
  args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
};
if (process.env.PLAYWRIGHT_CHROMIUM) launchOptions.executablePath = process.env.PLAYWRIGHT_CHROMIUM;
const browser = await chromium.launch(launchOptions);
const context = await browser.newContext({ viewport: { width: 400, height: 820 } });
const page = await context.newPage();

const failures = [];
const watch = (p, tag) => {
  p.on('console', (m) => { if (m.type() === 'error') failures.push(`${tag} console: ${m.text()}`); });
  p.on('pageerror', (e) => failures.push(`${tag} pageerror: ${e.message}`));
};
watch(page, 'onglet A');

const step = async (label, fn) => {
  try {
    const detail = await fn();
    console.log(`✔ ${label}${detail ? ' — ' + detail : ''}`);
  } catch (err) {
    console.log(`✘ ${label} — ${err.message}`);
    failures.push(`${label}: ${err.message}`);
  }
};

const st = (fn) => page.evaluate(fn);

await page.goto(URL_2D, { waitUntil: 'networkidle' });
await page.waitForTimeout(400);

await step('page chargée', () => page.title());

await step('la tour démarre sur un chantier', async () => {
  const floors = await page.$$eval('.floor, .floor3d-card', (els) => els.length);
  if (floors !== 0) throw new Error('étages déjà bâtis : ' + floors);
  if (!(await page.isVisible('.roof.build, .build3d'))) throw new Error('chantier absent');
  return 'chantier + rez-de-chaussée';
});

await step('construire le premier étage', async () => {
  await page.click('.roof.build, .build3d');
  const count = await st(() => window.SURETE.game.state.floors.accueil.count);
  if (count !== 1) throw new Error('count=' + count);
  if (!(await page.isVisible('[data-floor="accueil"], [data-floor3d="accueil"]'))) throw new Error('étage non dessiné');
  return 'étage « Accueil » bâti';
});

await step('le personnel occupe la pièce', async () => {
  const visual = await page.evaluate(() => ({
    crew: document.querySelectorAll('[data-floor="accueil"] .person').length,
    webgl: Boolean(document.querySelector('.tower3d canvas')),
  }));
  if (visual.crew < 1 && !visual.webgl) throw new Error('pièce vide');
  return visual.webgl ? 'personnel animé en WebGL' : `${visual.crew} silhouette(s) CSS`;
});

await step('lancer un cycle en tapant l\'étage', async () => {
  await page.click('[data-floor="accueil"] .room, [data-floor3d="accueil"] [data-act="tap"]');
  await page.waitForTimeout(900);
  const cash = await st(() => window.SURETE.game.state.cash);
  if (cash <= 0) throw new Error('aucun revenu encaissé');
  return 'trésorerie ' + cash.toFixed(2);
});

await step('panneau d\'étage : achat ×10 puis MAX', async () => {
  await st(() => { window.SURETE.game.state.cash = 1e9; });
  await page.click('[data-floor="accueil"] [data-act="panel"], [data-floor3d="accueil"] [data-act="panel"]');
  await page.waitForTimeout(150);
  if (!(await page.isVisible('#sheet .sheet-card'))) throw new Error('panneau non ouvert');
  await page.click('#sheet [data-act="qty"][data-qty="10"]');
  await page.click('#sheet [data-act="buy"]');
  await page.click('#sheet [data-act="qty"][data-qty="max"]');
  await page.click('#sheet [data-act="buy"]');
  const s = await st(() => ({
    count: window.SURETE.game.state.floors.accueil.count,
    cash: window.SURETE.game.state.cash,
  }));
  if (s.cash < 0) throw new Error('trésorerie négative');
  if (s.count < 11) throw new Error('count=' + s.count);
  return `count=${s.count}, reste ${s.cash.toFixed(2)}`;
});

await step('recruter le chef de service', async () => {
  await st(() => { window.SURETE.game.state.cash = 1e12; });
  await page.waitForTimeout(120);
  await page.click('#sheet [data-act="mgr"]');
  if (!(await st(() => window.SURETE.game.state.floors.accueil.manager))) throw new Error('non recruté');
  await page.waitForTimeout(300);
  if (!(await page.isVisible('#sheet .mgr-hired'))) throw new Error('panneau non mis à jour');
  await page.click('#sheet .sheet-x');
  await page.waitForTimeout(600);
  return 'production automatique : ' + (await page.textContent('#hud-cps'));
});

await step('les gains jaillissent sur l\'étage', async () => {
  await page.waitForTimeout(900);
  const coins = await page.$$eval('[data-floor="accueil"] .coin, .tower3d-gains span', (els) => els.length);
  if (coins < 1) throw new Error('aucune pièce animée');
  return coins + ' gain(s) affiché(s)';
});

await step('les dix étages s\'affichent', async () => {
  await st(() => {
    const s = window.SURETE.game.state;
    s.cash = 1e18;
    for (const id of Object.keys(s.floors)) { s.floors[id].count = 30; s.floors[id].manager = true; }
  });
  await page.click('.tab[data-tab="services"]');
  await page.waitForTimeout(300);
  const n = await page.$$eval('.floor, .floor3d-card', (els) => els.length);
  if (n !== 10) throw new Error('étages dessinés : ' + n);
  if (await page.isVisible('.roof.build, .build3d')) throw new Error('chantier encore présent');
  if (!(await page.isVisible('.roof.done .sign, .tower3d-complete'))) throw new Error('toit non achevé');
  return '10 étages + siège social';
});

await step('l\'ascenseur dessert l\'étage tapé', async () => {
  const is3d = await page.isVisible('.tower3d');
  const before = is3d ? '' : await page.$eval('.cab', (e) => e.style.transform);
  await page.click('[data-floor="cameras"] [data-act="panel"], [data-floor3d="cameras"] [data-act="panel"]');
  await page.waitForTimeout(250);
  const after = is3d ? 'sélection 3D' : await page.$eval('.cab', (e) => e.style.transform);
  if (!is3d && before === after) throw new Error('cabine immobile');
  if (!(await page.isVisible('#sheet .sheet-card'))) throw new Error('étage non sélectionné');
  await page.click('#sheet .sheet-x');
  return is3d ? after : after.replace(/[^0-9-]/g, '') + 'px';
});

for (const tab of ['upgrades', 'contracts', 'research', 'cert']) {
  await step(`onglet ${tab}`, async () => {
    await page.click(`.tab[data-tab="${tab}"]`);
    await page.waitForTimeout(250);
    const n = await page.$$eval('#view .card, #view .empty, #view .ach', (els) => els.length);
    if (n === 0) throw new Error('vue vide');
    return n + ' blocs';
  });
}

await step('acheter du matériel', async () => {
  await page.click('.tab[data-tab="upgrades"]');
  await page.waitForTimeout(250);
  const before = await st(() => Object.keys(window.SURETE.game.state.upgrades).length);
  await page.click('#view [data-act="upg"]:not([disabled])');
  const after = await st(() => Object.keys(window.SURETE.game.state.upgrades).length);
  if (after !== before + 1) throw new Error(`${before} -> ${after}`);
  return 'acquis';
});

await step('contrat : offre, signature, livraison', async () => {
  await st(() => { window.SURETE.game.state.nextOfferAt = window.SURETE.game.state.stats.playTime; });
  await page.click('.tab[data-tab="contracts"]');
  await page.waitForTimeout(1400);
  await page.click('#view [data-act="accept"]:not([disabled])');
  await page.waitForTimeout(300);
  if ((await st(() => window.SURETE.game.state.contracts.length)) !== 1) throw new Error('non signé');
  await st(() => { window.SURETE.game.state.contracts[0].endsAt = 0; });
  await page.waitForTimeout(400);
  const s = await st(() => ({
    done: window.SURETE.game.state.stats.contractsDone,
    rd: window.SURETE.game.state.rd,
  }));
  if (s.done < 1) throw new Error('non livré');
  return `livré, ${s.rd} pts R&D`;
});

await step('lancer une étude de R&D', async () => {
  await st(() => { window.SURETE.game.state.rd = 500; });
  await page.click('.tab[data-tab="research"]');
  await page.waitForTimeout(1300);
  await page.click('#view [data-act="res"]:not([disabled])');
  const n = await st(() => Object.keys(window.SURETE.game.state.research).length);
  if (n < 1) throw new Error('aucune étude acquise');
  return n + ' étude(s)';
});

await step('renfort d\'équipe', async () => {
  await st(() => {
    window.SURETE.game.state.research.renfort = true;
    window.SURETE.game.state.boostReadyAt = 0;
  });
  await page.waitForTimeout(300);
  await page.click('#btn-boost');
  const boost = await st(() => window.SURETE.game.state.boost);
  if (!boost) throw new Error('boost non déclenché');
  return '×' + boost.mult;
});

await step('certification : reset partiel', async () => {
  await st(() => { window.SURETE.game.state.stats.lifetimeEarned = 1e14; });
  await page.click('.tab[data-tab="cert"]');
  await page.waitForTimeout(1300);
  await page.click('#view [data-act="certify"]:not([disabled])');
  await page.waitForTimeout(200);
  await page.click('#modal-actions button:nth-child(2)');
  await page.waitForTimeout(400);
  const s = await st(() => ({
    stars: window.SURETE.game.state.stars,
    cash: window.SURETE.game.state.cash,
    research: Object.keys(window.SURETE.game.state.research).length,
    count: window.SURETE.game.state.floors.accueil.count,
  }));
  if (s.stars <= 0) throw new Error('aucune étoile gagnée');
  if (s.cash !== 4) throw new Error('trésorerie non réinitialisée : ' + s.cash);
  if (s.count !== 0) throw new Error('postes non réinitialisés');
  if (s.research < 1) throw new Error('R&D perdue');
  return `${s.stars} étoiles, R&D conservée`;
});

await step('sauvegarde et rechargement', async () => {
  await st(() => window.SURETE.G.saveNow());
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForTimeout(500);
  const stars = await st(() => window.SURETE.game.state.stars);
  if (stars <= 0) throw new Error('étoiles perdues au rechargement');
  return stars + ' étoiles rechargées';
});

// L'app sauvegarde sur `pagehide` : un rechargement réécrirait `lastSeen`.
// On antidate la sauvegarde depuis cet onglet, puis on en ouvre un second.
await step('revenus hors-ligne', async () => {
  await st(() => {
    const s = window.SURETE.game.state;
    s.cash = 1e15;
    for (const id of Object.keys(s.floors)) { s.floors[id].count = 40; s.floors[id].manager = true; }
    window.SURETE.G.saveNow();
    const raw = JSON.parse(localStorage.getItem('idle-videosurete-save-v1'));
    raw.lastSeen = Date.now() - 3600 * 1000;
    localStorage.setItem('idle-videosurete-save-v1', JSON.stringify(raw));
  });
  const page2 = await context.newPage();
  watch(page2, 'onglet B');
  await page2.goto(URL_2D, { waitUntil: 'networkidle' });
  await page2.waitForTimeout(700);
  if (!(await page2.isVisible('#modal'))) throw new Error('rapport hors-ligne absent');
  const body = (await page2.textContent('#modal-body')).replace(/\s+/g, ' ').trim();
  await page2.close();
  return body.slice(0, 110);
});

await step('la tour 3D prend la main', async () => {
  // Contexte neuf : la partie précédente a laissé une tour complète en
  // sauvegarde et un autre onglet actif.
  const ctx3 = await browser.newContext({ viewport: { width: 400, height: 820 } });
  const p3 = await ctx3.newPage();
  watch(p3, 'tour 3D');
  await p3.goto(URL, { waitUntil: 'networkidle' });
  await p3.waitForTimeout(1800);

  const on = await p3.evaluate(() => !!document.querySelector('.t3d canvas'));
  if (!on) { await ctx3.close(); throw new Error('canvas WebGL absent'); }

  // le chantier reste cliquable : la boucle de jeu ne dépend pas du moteur
  const before = await p3.evaluate(() => window.SURETE.game.state.floors.accueil.count);
  await p3.click('.build3d');
  await p3.waitForTimeout(400);
  const after = await p3.evaluate(() => window.SURETE.game.state.floors.accueil.count);
  if (after <= before) throw new Error('construction impossible en 3D');

  const tags = await p3.$$eval('.t3d-tag', (els) => els.length);
  if (tags < 1) throw new Error('aucune étiquette d\'étage');

  await ctx3.close();
  return `canvas actif, ${tags} étiquette(s), étage bâti`;
});

console.log(failures.length ? '\n' + failures.join('\n') : '\nAucune erreur console.');

await browser.close();
server.close();
process.exit(failures.length ? 1 : 0);
