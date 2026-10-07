// Monte un épisode « Histoires de sûreté » à partir des visuels modif.ai et du script.
//
//   node shorts/montage.mjs 01-le-chat             # rendu final 1080x1920
//   node shorts/montage.mjs 01-le-chat --preview   # rendu rapide 540x960 pour itérer
//   node shorts/montage.mjs voices                 # liste les voix ElevenLabs
//
// Sorties : shorts/out/<id>.mp4, <id>.jpg (couverture), <id>.txt (légende à publier).

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hash, music, tts } from './lib/eleven.mjs';
import { buildAss } from './lib/captions.mjs';
import { assemble, ffmpeg, mixAudio, renderShots } from './lib/render.mjs';
import { ensureSfx } from './lib/sfx.mjs';
import { FPS, TRANSITIONS, buildTimeline, cueTime, joinScript, wordsFromAlignment } from './lib/timeline.mjs';

const ROOT = fileURLToPath(new URL('.', import.meta.url));
const args = process.argv.slice(2);
const preview = args.includes('--preview');
const id = args.find((a) => !a.startsWith('--'));

const QUALITY = preview
  ? { W: 540, H: 960, ss: 2, preset: 'ultrafast', finalPreset: 'veryfast', crf: 23, grain: false }
  : { W: 1080, H: 1920, ss: 3, preset: 'fast', finalPreset: 'slow', crf: 17, grain: true };

function loadDotEnv(path) {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2].replace(/^['"]|['"]$/g, '');
  }
}

async function listVoices() {
  const res = await fetch('https://api.elevenlabs.io/v1/voices', { headers: { 'xi-api-key': process.env.ELEVENLABS_API_KEY } });
  if (!res.ok) throw new Error(`ElevenLabs ${res.status}: ${await res.text()}`);
  for (const v of (await res.json()).voices) console.log(`${v.voice_id}  ${v.name}  ${v.labels?.language || ''}`);
}

async function main() {
  loadDotEnv(join(ROOT, '..', '.env'));
  if (id === 'voices') return listVoices();
  if (!id) throw new Error('Usage : node shorts/montage.mjs <episode> [--preview]');

  const epDir = join(ROOT, 'episodes', id);
  const ep = JSON.parse(readFileSync(join(epDir, 'episode.json'), 'utf8'));
  const cacheDir = join(epDir, 'cache');
  const outDir = join(ROOT, 'out');
  mkdirSync(cacheDir, { recursive: true });
  mkdirSync(outDir, { recursive: true });

  for (const s of ep.shots) {
    if (existsSync(join(epDir, 'assets', s.asset))) continue;
    // Animation absente : on retombe sur l'image fixe du même plan, animée par la caméra du montage.
    const still = s.asset.replace(/\.(mp4|mov|webm)$/i, '.png');
    if (still !== s.asset && existsSync(join(epDir, 'assets', still))) {
      console.warn(`  ! ${s.asset} absent, image fixe ${still} utilisée`);
      s.asset = still;
    } else throw new Error(`Visuel manquant : assets/${s.asset}`);
  }

  console.log('1/6 Voix');
  const { text, offsets } = joinScript(ep.shots);
  const voiceFile = join(cacheDir, `voice-${hash({ text, voice: ep.voice })}.mp3`);
  const words = wordsFromAlignment(await tts(text, ep.voice, voiceFile));

  console.log('2/6 Timeline');
  const timeline = buildTimeline(ep.shots, offsets, words);
  const duration = timeline[timeline.length - 1].to / FPS;
  console.log(`    ${timeline.length} plans, ${duration.toFixed(1)} s`);

  console.log('3/6 Sous-titres');
  writeFileSync(join(cacheDir, 'captions.ass'), buildAss(ep, timeline, duration));

  console.log('4/6 Bruitages et musique');
  const wanted = [];
  for (const shot of timeline) {
    const t = TRANSITIONS[shot.in || 'cut'];
    if (shot.k > 0 && t?.sfx && shot.autoSfx !== false) wanted.push({ name: t.sfx, time: shot.from / FPS - (t.sfx === 'whoosh' ? 0.18 : 0) });
    for (const s of shot.sfx || []) {
      const cue = typeof s === 'string' ? { name: s } : s;
      wanted.push({ name: cue.name, time: cueTime(shot, cue.at) + (cue.offset || 0), volume: cue.volume });
    }
  }
  const lib = await ensureSfx(wanted.map((c) => c.name), ep.sfxLibrary);
  const cues = wanted.map((c) => ({ file: lib[c.name].file, time: c.time, volume: c.volume ?? lib[c.name].volume }));

  let bed = null;
  if (ep.music?.file) bed = { file: join(epDir, 'assets', ep.music.file), volume: ep.music.volume };
  else if (ep.music?.prompt) {
    const seconds = Math.ceil(duration) + 2;
    const file = join(cacheDir, `music-${hash({ p: ep.music.prompt, seconds })}.mp3`);
    await music(ep.music.prompt, seconds, file);
    bed = { file, volume: ep.music.volume };
  }
  const drops = timeline.filter((s) => s.musicDrop).map((s) => [s.from / FPS, s.musicDrop]);
  await mixAudio({ voice: voiceFile, music: bed, cues, drops, duration, cacheDir });

  console.log(`5/6 Plans (${QUALITY.W}x${QUALITY.H})`);
  await renderShots(timeline, join(epDir, 'assets'), cacheDir, QUALITY);

  console.log('6/6 Assemblage, étalonnage, export');
  const name = preview ? `${id}-preview` : id;
  const output = join(outDir, `${name}.mp4`);
  await assemble({ timeline, duration, cacheDir, output, q: QUALITY });
  await ffmpeg(['-ss', String(ep.coverAt ?? 0.6), '-i', output, '-frames:v', '1', '-q:v', '2', join(outDir, `${name}.jpg`)], cacheDir);
  writeFileSync(join(outDir, `${name}.txt`), `${ep.caption}\n\n${ep.disclaimer || ''}\n\n${(ep.hashtags || []).join(' ')}\n`);

  console.log(`\n→ ${output}`);
}

main().catch((e) => {
  console.error(`\nErreur : ${e.message}`);
  process.exit(1);
});
