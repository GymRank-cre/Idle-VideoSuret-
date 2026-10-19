// Importe un épisode produit par l'automatisation Grok (instructions : AUTOMATISATION-GROK.md)
// à partir de son lien de partage : télécharge les clips p01…p12, construit episode.json et,
// avec --montage, lance le rendu final.
//
//   node shorts/import-grok.mjs https://grok.com/share/<id>
//   node shorts/import-grok.mjs https://grok.com/share/<id> --montage
//   node shorts/import-grok.mjs <lien> --id 07-le-sujet --force     # imposer l'identifiant, écraser
//
// Lit le bloc ```json de la réponse ; s'il manque, se rabat sur les répliques numérotées du texte.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { LIBRARY } from './lib/sfx.mjs';
import { sameWord } from './lib/timeline.mjs';

const ROOT = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const link = args.find((a) => !a.startsWith('--') && a !== optionValue('id'));
const flag = (n) => args.includes(`--${n}`);
function optionValue(n) {
  return args.includes(`--${n}`) ? args[args.indexOf(`--${n}`) + 1] : undefined;
}

const DEFAULTS = {
  disclaimer: 'Histoire fictive inspirée de situations réelles.',
  voice: { id: 'Xgb3SR8idOHy8scGICeJ', stability: 0.4, style: 0.4, speed: 1.05 },
  music: {
    prompt:
      'playful sneaky cartoon heist music, pizzicato strings, muted trumpet, brushed jazzy drums, comedic suspense, instrumental only, no vocals, steady tempo around 100 bpm',
    volume: 0.32,
  },
  coverAt: 0.7,
};
// Grammaire de montage par défaut : mouvement de caméra et transition d'entrée de chaque plan.
const CAMS = ['punch', 'in', 'in', 'hold', 'punch', 'in', 'out', 'in', 'in', 'punch', 'out', 'in'];
const INS = [null, 'cut', 'whip', 'cut', 'whip-up', 'cut', 'zoom', 'whip', 'cut', 'whip', 'cut', 'whip'];

const slug = (s) =>
  s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 40);

async function getJson(url) {
  const res = await fetch(url, { headers: { 'User-Agent': 'Mozilla/5.0' } });
  if (!res.ok) throw new Error(`${res.status} sur ${url}`);
  return res.json();
}

function parseCards(resp) {
  const cards = (resp.cardAttachmentsJson || []).map((c) => (typeof c === 'string' ? JSON.parse(c) : c));
  const files = {};
  for (const c of cards) {
    const m = (c.file_name || '').match(/^(p\d{2})\.(mp4|png|jpe?g|webp)$/i);
    if (m && c.url && !(m[1] in files && files[m[1]].ext === 'mp4')) files[m[1]] = { url: c.url, ext: m[2].toLowerCase() };
  }
  return files;
}

// Bloc JSON demandé par les instructions, sinon répliques « 1. … » du texte.
function parseEpisode(text) {
  const blocks = [...text.matchAll(/```json\s*([\s\S]*?)```/g)];
  if (blocks.length) return { ...JSON.parse(blocks[blocks.length - 1][1]), source: 'json' };
  const shots = [...text.matchAll(/^\s*(\d{1,2})\.\s+(.+?)\s*$/gm)]
    .map((m) => ({ n: +m[1], say: m[2].replace(/[«»"]/g, '').replace(/\s+/g, ' ').trim() }))
    .filter((s) => s.n >= 1 && s.n <= 12)
    .map(({ say }) => ({ say }));
  const title = (text.match(/\*\*(.+?)\.?\*\*/) || [])[1] || 'episode grok';
  const caption = ((text.match(/L[ée]gende TikTok\.?\*{0,2}\s*([\s\S]*?)(?:\n\s*#|$)/i) || [])[1] || '').trim();
  const hashtags = [...new Set(text.match(/#[\p{L}\p{N}_]+/gu) || [])];
  return { title, hook: title, shots, caption, hashtags, source: 'texte' };
}

function buildEpisode(id, raw, assets) {
  const shots = raw.shots.slice(0, 12).map((s, i) => {
    const key = `p${String(i + 1).padStart(2, '0')}`;
    const emphasis = (s.emphasis || []).filter((e) => s.say.split(/\s+/).some((w) => sameWord(w, e)));
    const shot = { asset: `${key}.${assets[key] || 'mp4'}`, cam: CAMS[i] || 'in', focus: [0.5, 0.42], say: s.say };
    if (INS[i] && INS[i] !== 'cut') shot.in = INS[i];
    if (s.label) shot.label = s.label;
    if (emphasis.length) shot.emphasis = emphasis;
    const sfx = (s.sfx || []).filter((n) => LIBRARY[n]);
    if (s.twist) {
      Object.assign(shot, { in: 'flash', autoSfx: false, musicDrop: 1.2, cam: 'punch' });
      if (!sfx.includes('scratch')) sfx.unshift('scratch');
    }
    if (i === 0 && !sfx.length) sfx.push('impact');
    if (sfx.length) {
      shot.sfx = sfx.map((name, k) =>
        name === 'scratch' ? { name, at: 0, offset: -0.05 } : k === 0 && emphasis[0] ? { name, at: `mot:${emphasis[0]}` } : { name, at: 0.15 },
      );
    }
    return shot;
  });
  return {
    id,
    title: raw.title,
    hook: raw.hook || raw.title,
    ...(raw.lesson ? { lesson: raw.lesson } : {}),
    disclaimer: DEFAULTS.disclaimer,
    voice: DEFAULTS.voice,
    music: DEFAULTS.music,
    coverAt: DEFAULTS.coverAt,
    shots,
    caption: raw.caption,
    hashtags: raw.hashtags,
  };
}

async function main() {
  const shareId = (link || '').split('/share/').pop().split(/[?#]/)[0];
  if (!shareId) throw new Error('Usage : node shorts/import-grok.mjs <lien de partage Grok> [--montage] [--id …] [--force]');

  const data = await getJson(`https://grok.com/rest/app-chat/share_links/${shareId}`);
  const resp = [...data.responses].reverse().find((r) => r.sender?.toUpperCase() === 'ASSISTANT');
  if (!resp) throw new Error('Aucune réponse de Grok dans ce partage.');

  const raw = parseEpisode(resp.message || '');
  const files = parseCards(resp);
  if (raw.shots.length !== 12) console.warn(`  ! ${raw.shots.length} répliques trouvées au lieu de 12`);
  const missing = raw.shots.map((_, i) => `p${String(i + 1).padStart(2, '0')}`).filter((k) => !files[k]);
  if (missing.length) throw new Error(`Clips absents du partage : ${missing.join(', ')}`);

  const id = optionValue('id') || raw.id || `xx-${slug(raw.title)}`;
  const epDir = join(ROOT, 'episodes', id);
  if (existsSync(join(epDir, 'episode.json')) && !flag('force')) throw new Error(`L'épisode ${id} existe déjà (--force pour l'écraser).`);

  // Téléchargement dans un dossier temporaire, puis déplacement une fois tout vérifié.
  const tmp = join(epDir, 'import-tmp');
  rmSync(tmp, { recursive: true, force: true });
  mkdirSync(tmp, { recursive: true });
  const assets = {};
  for (const [key, f] of Object.entries(files)) {
    const res = await fetch(`https://assets.grok.com/${f.url}`, { headers: { 'User-Agent': 'Mozilla/5.0' } });
    if (!res.ok) throw new Error(`Téléchargement ${key} : ${res.status}`);
    const file = join(tmp, `${key}.${f.ext}`);
    writeFileSync(file, Buffer.from(await res.arrayBuffer()));
    const probe = spawnSync('ffprobe', ['-v', 'error', '-show_entries', 'stream=codec_type', '-of', 'csv=p=0', file], { encoding: 'utf8' });
    if (!/video/.test(probe.stdout)) throw new Error(`${key} n'est pas une image ou une vidéo lisible`);
    assets[key] = f.ext;
  }
  mkdirSync(join(epDir, 'assets'), { recursive: true });
  for (const name of readdirSync(tmp)) renameSync(join(tmp, name), join(epDir, 'assets', name));
  rmSync(tmp, { recursive: true, force: true });

  const ep = buildEpisode(id, raw, assets);
  writeFileSync(join(epDir, 'episode.json'), JSON.stringify(ep, null, 2) + '\n');
  console.log(`Épisode ${id} importé (${raw.source === 'json' ? 'bloc JSON' : 'texte, sans bloc JSON'}) : ${ep.shots.length} plans, « ${ep.title} »`);
  console.log(`  ${join(epDir, 'episode.json')}`);

  if (flag('montage')) {
    const r = spawnSync('node', [join(ROOT, 'montage.mjs'), id], { stdio: 'inherit' });
    process.exit(r.status ?? 1);
  }
}

export { parseEpisode, buildEpisode };

if (process.argv[1] === fileURLToPath(import.meta.url)) main().catch((e) => {
  console.error(`\nErreur : ${e.message}`);
  process.exit(1);
});
