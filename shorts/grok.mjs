// Génère automatiquement les visuels d'un épisode avec l'API xAI (Grok Imagine) :
// une image par plan à partir des personnages de référence, puis son animation.
//
//   node shorts/grok.mjs 04-le-faux-technicien             # images + animations
//   node shorts/grok.mjs 04-le-faux-technicien --images    # images seulement (valider avant de payer)
//   node shorts/grok.mjs 04-le-faux-technicien --only p03,p07 --force   # refaire des plans
//   node shorts/grok.mjs 04-le-faux-technicien --montage   # enchaîne sur le montage final
//
// Lit episodes/<id>/visuels.json, écrit assets/pXX.png et assets/pXX.mp4. Un plan déjà présent
// est sauté (reprise après interruption), sauf avec --force.
// Clé : XAI_API_KEY dans .env (https://console.x.ai). Coût indicatif : voir shorts/README.md.

import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('.', import.meta.url));
const API = process.env.XAI_BASE_URL || 'https://api.x.ai/v1';
const IMAGE_MODEL = process.env.XAI_IMAGE_MODEL || 'grok-imagine-image-2.0';
const VIDEO_MODEL = process.env.XAI_VIDEO_MODEL || 'grok-imagine-video-1.5';
const PARALLEL = 4;
const POLL_MS = Number(process.env.XAI_POLL_MS || 5000);
const TIMEOUT_MS = 15 * 60 * 1000;

const STYLE =
  '2D cartoon illustration, adult animated sitcom style, thick clean black outlines, flat colors with soft cel shading, ' +
  'slightly exaggerated proportions, night scene, deep blue moonlight palette with warm orange streetlight glow, ' +
  'cinematic lighting, vertical 9:16, no text, no letters, no numbers, no watermark';
const SAME_CHARACTERS = 'Keep the characters exactly identical to the reference images: same faces, colors and clothes.';

const args = process.argv.slice(2);
const id = args.find((a) => !a.startsWith('--') && !/^p\d/.test(a));
const flag = (name) => args.includes(`--${name}`);
const option = (name, fallback) => (args.includes(`--${name}`) ? args[args.indexOf(`--${name}`) + 1] : fallback);
const only = option('only', '').split(',').filter(Boolean);
const resolution = option('resolution', '720p');

function loadDotEnv(path) {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2].replace(/^['"]|['"]$/g, '');
  }
}

async function api(path, { method = 'POST', body } = {}) {
  const res = await fetch(`${API}${path}`, {
    method,
    headers: { Authorization: `Bearer ${process.env.XAI_API_KEY}`, 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  if (!res.ok) {
    const err = new Error(`xAI ${method} ${path} ${res.status}: ${text.slice(0, 400)}`);
    err.status = res.status;
    throw err;
  }
  return JSON.parse(text);
}

const dataUri = (file) => `data:image/png;base64,${readFileSync(file).toString('base64')}`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function download(url, file) {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`Téléchargement ${res.status} : ${url}`);
  writeFileSync(file, Buffer.from(await res.arrayBuffer()));
}

// Durée utile de chaque plan, d'après la voix déjà générée (on ne paie que les secondes montées).
async function shotDurations(epDir) {
  const epFile = join(epDir, 'episode.json');
  if (!existsSync(epFile)) return {};
  try {
    const [{ tts, hash }, T] = await Promise.all([import('./lib/eleven.mjs'), import('./lib/timeline.mjs')]);
    const ep = JSON.parse(readFileSync(epFile, 'utf8'));
    const { text, offsets } = T.joinScript(ep.shots);
    const voiceFile = join(epDir, 'cache', `voice-${hash({ text, voice: ep.voice })}.mp3`);
    if (!existsSync(`${voiceFile}.json`)) return {};
    const words = T.wordsFromAlignment(await tts(text, ep.voice, voiceFile));
    const out = {};
    T.buildTimeline(ep.shots, offsets, words).forEach((s) => {
      out[`p${String(s.k + 1).padStart(2, '0')}`] = Math.min(15, Math.max(4, Math.ceil(s.frames / T.FPS + 0.4)));
    });
    return out;
  } catch {
    return {};
  }
}

async function makeImage(shot, cfg, file) {
  const refs = shot.refs.map((r) => join(ROOT, 'references', `${r}.png`));
  for (const r of refs) if (!existsSync(r)) throw new Error(`Référence absente : ${r}`);
  const costume = shot.costume ? ` ${cfg.costumes[shot.costume]}.` : '';
  const prompt = `${shot.image}.${costume} ${SAME_CHARACTERS} ${STYLE}`;
  const image = (f) => ({ url: dataUri(f), type: 'image_url' });
  const base = { model: IMAGE_MODEL, prompt, n: 1, aspect_ratio: '9:16' };
  let res;
  try {
    res = await api('/images/edits', refs.length > 1 ? { body: { ...base, images: refs.map(image) } } : { body: { ...base, image: image(refs[0]) } });
  } catch (e) {
    if (refs.length < 2 || e.status !== 400) throw e;
    console.warn(`  ${shot.id} : plusieurs références refusées, je réessaie avec ${shot.refs[0]} seul`);
    res = await api('/images/edits', { body: { ...base, image: image(refs[0]) } });
  }
  const out = res.data?.[0];
  if (out?.b64_json) writeFileSync(file, Buffer.from(out.b64_json, 'base64'));
  else if (out?.url) await download(out.url, file);
  else throw new Error(`Réponse image inattendue : ${JSON.stringify(res).slice(0, 300)}`);
}

async function makeVideo(shot, imageFile, duration, file) {
  const prompt = `2D cartoon animation, ${shot.animate}. Static camera, no camera movement, keep the exact cartoon style and characters, no text.`;
  const base = { model: VIDEO_MODEL, prompt, duration, aspect_ratio: '9:16', resolution, generate_audio: false };
  let job;
  try {
    job = await api('/videos/generations', { body: { ...base, image: dataUri(imageFile) } });
  } catch (e) {
    if (e.status !== 400 && e.status !== 422) throw e;
    job = await api('/videos/generations', { body: { ...base, image: { url: dataUri(imageFile) } } });
  }
  const started = Date.now();
  for (;;) {
    await sleep(POLL_MS);
    const st = await api(`/videos/${job.request_id}`, { method: 'GET' });
    if (st.status === 'done') return download(st.video.url, file);
    if (st.status === 'failed' || st.status === 'expired') throw new Error(`animation ${st.status}`);
    if (Date.now() - started > TIMEOUT_MS) throw new Error('animation trop longue (15 min)');
  }
}

async function pool(items, worker) {
  let i = 0;
  await Promise.all(
    Array.from({ length: Math.min(PARALLEL, items.length) }, async () => {
      while (i < items.length) await worker(items[i++]);
    }),
  );
}

async function main() {
  loadDotEnv(join(ROOT, '..', '.env'));
  if (!id) throw new Error('Usage : node shorts/grok.mjs <episode> [--images] [--only p01,p02] [--force] [--montage]');
  if (!process.env.XAI_API_KEY) throw new Error('XAI_API_KEY manquante dans .env (clé à créer sur https://console.x.ai).');

  const epDir = join(ROOT, 'episodes', id);
  const cfg = JSON.parse(readFileSync(join(epDir, 'visuels.json'), 'utf8'));
  const shots = cfg.shots.filter((s) => !only.length || only.includes(s.id));
  const durations = await shotDurations(epDir);
  const force = flag('force');

  const failures = [];
  await pool(shots, async (shot) => {
    const png = join(epDir, 'assets', `${shot.id}.png`);
    const mp4 = join(epDir, 'assets', `${shot.id}.mp4`);
    try {
      if (force || !existsSync(png)) {
        console.log(`  ${shot.id} image…`);
        await makeImage(shot, cfg, png);
      }
      if (flag('images')) return;
      if (force || !existsSync(mp4)) {
        const duration = shot.duration || durations[shot.id] || 6;
        console.log(`  ${shot.id} animation ${duration} s…`);
        await makeVideo(shot, png, duration, mp4);
      }
      console.log(`  ${shot.id} ok`);
    } catch (e) {
      failures.push(shot.id);
      console.error(`  ${shot.id} ÉCHEC : ${e.message}`);
    }
  });

  if (failures.length) console.warn(`\nPlans en échec : ${failures.join(', ')} (relancer la même commande pour réessayer).`);
  else console.log(`\n${shots.length} plan(s) prêts dans ${join(epDir, 'assets')}`);

  if (flag('montage') && !flag('images')) {
    console.log('\nMontage…');
    const r = spawnSync('node', [join(ROOT, 'montage.mjs'), id], { stdio: 'inherit' });
    process.exit(r.status ?? 1);
  }
  if (failures.length) process.exit(1);
}

main().catch((e) => {
  console.error(`\nErreur : ${e.message}`);
  process.exit(1);
});
