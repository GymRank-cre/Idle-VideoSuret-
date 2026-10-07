// Monte un épisode tourné en plans-séquences continus (ex. 2 × 15 s générés sur Grok,
// la partie 2 prolongeant la partie 1) : la vidéo garde son propre découpage, on pose
// dessus la voix off par parties, la musique, les bruitages, les sous-titres et l'accroche.
//
//   node shorts/montage-continu.mjs 07-le-post-it             # rendu final 1080x1920
//   node shorts/montage-continu.mjs 07-le-post-it --preview   # rendu rapide 540x960
//
// Lit episodes/<id>/continu.json. Sorties : shorts/out/<id>.mp4, -partage.mp4, .jpg, .txt.

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hash, music, tts } from './lib/eleven.mjs';
import { buildAss } from './lib/captions.mjs';
import { ffmpeg, mixAudio } from './lib/render.mjs';
import { ensureSfx } from './lib/sfx.mjs';
import { FPS, wordsFromAlignment } from './lib/timeline.mjs';

const ROOT = fileURLToPath(new URL('.', import.meta.url));
const FONTS_DIR = join(ROOT, 'fonts');
const args = process.argv.slice(2);
const preview = args.includes('--preview');
const id = args.find((a) => !a.startsWith('--'));
const LEAD = 3;

const QUALITY = preview
  ? { W: 540, H: 960, preset: 'veryfast', crf: 23, grain: '' }
  : { W: 1080, H: 1920, preset: 'slow', crf: 17, grain: ',noise=alls=3:allf=t' };

function loadDotEnv(path) {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2].replace(/^['"]|['"]$/g, '');
  }
}

const probe = (file) =>
  parseFloat(spawnSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', file], { encoding: 'utf8' }).stdout) || 0;

async function main() {
  loadDotEnv(join(ROOT, '..', '.env'));
  if (!id) throw new Error('Usage : node shorts/montage-continu.mjs <episode> [--preview]');
  const epDir = join(ROOT, 'episodes', id);
  const ep = JSON.parse(readFileSync(join(epDir, 'continu.json'), 'utf8'));
  const cacheDir = join(epDir, 'cache');
  const outDir = join(ROOT, 'out');
  mkdirSync(outDir, { recursive: true });

  // Début de chaque partie sur la timeline finale (les transitions se chevauchent).
  let t = 0;
  const parts = ep.parts.map((p, i) => {
    const file = join(epDir, 'assets', p.asset);
    if (!existsSync(file)) throw new Error(`Visuel manquant : assets/${p.asset}`);
    const len = probe(file);
    const trans = i > 0 ? p.transition || { type: 'fadeblack', duration: 0.4 } : null;
    const start = i > 0 ? t - trans.duration : 0;
    t = start + len;
    return { ...p, file, len, start, trans };
  });
  const duration = t;

  console.log('1/5 Voix');
  const lines = [];
  for (const [i, p] of parts.entries()) {
    const text = p.lines.map((l) => l.say).join(' ');
    const prev = parts[i - 1]?.lines.map((l) => l.say).join(' ');
    const next = parts[i + 1]?.lines.map((l) => l.say).join(' ');
    const alignment = await tts(text, ep.voice, join(cacheDir, `${p.voice}.mp3`), { previousText: prev, nextText: next });
    const words = wordsFromAlignment(alignment).map((w) => ({ ...w, start: w.start + p.start + p.voiceAt, end: w.end + p.start + p.voiceAt }));
    let k = 0;
    for (const l of p.lines) {
      const n = l.say.trim().split(/\s+/).length;
      lines.push({ ...l, words: words.slice(k, k + n) });
      k += n;
    }
    if (k !== words.length) console.warn(`  ! ${p.voice} : ${words.length} mots alignés pour ${k} attendus`);
  }

  // Une « réplique » joue le rôle d'un plan pour l'habillage (sous-titres, étiquettes, mention).
  const timeline = lines.map((l, k) => ({ ...l, k, from: k === 0 ? 0 : Math.max(0, Math.round(l.words[0].start * FPS) - LEAD) }));
  timeline.forEach((s, k) => (s.to = k + 1 < timeline.length ? timeline[k + 1].from : Math.round(duration * FPS)));
  console.log(`    ${parts.length} parties, ${lines.length} répliques, ${duration.toFixed(1)} s`);

  console.log('2/5 Sous-titres');
  writeFileSync(join(cacheDir, 'captions.ass'), buildAss(ep, timeline, duration));

  console.log('3/5 Bruitages et musique');
  const voiceArgs = parts.flatMap((p) => ['-i', `${p.voice}.mp3`]);
  const delays = parts.map((p, i) => {
    const ms = Math.round((p.start + p.voiceAt) * 1000);
    return `[${i}:a]aresample=48000,aformat=channel_layouts=mono,adelay=${ms}[v${i}]`;
  });
  await ffmpeg(
    [...voiceArgs, '-filter_complex', `${delays.join(';')};${parts.map((_, i) => `[v${i}]`).join('')}amix=inputs=${parts.length}:normalize=0:duration=longest,asetpts=N/SR/TB[out]`, '-map', '[out]', '-c:a', 'pcm_s16le', 'voice.wav'],
    cacheDir,
  );

  const lib = await ensureSfx((ep.sfx || []).map((c) => c.name), ep.sfxLibrary);
  const cues = (ep.sfx || []).map((c) => ({ file: lib[c.name].file, time: c.at, volume: c.volume ?? lib[c.name].volume }));
  let bed = null;
  if (ep.music?.prompt) {
    const seconds = Math.ceil(duration) + 2;
    const file = join(cacheDir, `music-${hash({ p: ep.music.prompt, seconds })}.mp3`);
    await music(ep.music.prompt, seconds, file);
    bed = { file, volume: ep.music.volume };
  }
  await mixAudio({ voice: 'voice.wav', music: bed, cues, drops: ep.musicDrops || [], duration, cacheDir });

  console.log(`4/5 Image (${QUALITY.W}x${QUALITY.H})`);
  const { W, H } = QUALITY;
  const chains = parts.map(
    (p, i) => `[${i}:v:0]fps=${FPS},scale=${W}:${H}:force_original_aspect_ratio=increase:flags=lanczos,crop=${W}:${H},setsar=1,format=yuv420p[p${i}]`,
  );
  let last = '[p0]';
  for (let i = 1; i < parts.length; i++) {
    const p = parts[i];
    chains.push(`${last}[p${i}]xfade=transition=${p.trans.type}:duration=${p.trans.duration}:offset=${p.start.toFixed(3)}[x${i}]`);
    last = `[x${i}]`;
  }
  const barIdx = parts.length + 1;
  chains.push(
    `${last}eq=contrast=1.05:saturation=1.12,unsharp=5:5:0.45,vignette=angle=PI/6${QUALITY.grain},subtitles=captions.ass:fontsdir=${FONTS_DIR}[graded]`,
    `[graded][${barIdx}:v]overlay=x='-w+w*t/${duration.toFixed(3)}':y=0:shortest=1,format=yuv420p[v]`,
  );

  console.log('5/5 Export');
  const name = preview ? `${id}-preview` : id;
  const output = join(outDir, `${name}.mp4`);
  await ffmpeg(
    [
      ...parts.flatMap((p) => ['-i', p.file]),
      '-i', 'mix.wav',
      '-f', 'lavfi', '-i', `color=c=0xFFD400:s=${W}x${Math.round(H / 240)}:r=${FPS}`,
      '-filter_complex', chains.join(';'),
      '-map', '[v]', '-map', `${parts.length}:a`,
      '-t', duration.toFixed(3), '-r', String(FPS),
      '-c:v', 'libx264', '-preset', QUALITY.preset, '-crf', String(QUALITY.crf), '-maxrate', '14M', '-bufsize', '28M', '-profile:v', 'high', '-pix_fmt', 'yuv420p',
      '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-movflags', '+faststart',
      output,
    ],
    cacheDir,
  );
  await ffmpeg(['-ss', String(ep.coverAt ?? 0.6), '-i', output, '-frames:v', '1', '-q:v', '2', join(outDir, `${name}.jpg`)], cacheDir);
  if (!preview) {
    // Version légère pour l'envoi sur téléphone.
    await ffmpeg(['-i', output, '-c:v', 'libx264', '-preset', 'slow', '-crf', '23', '-maxrate', '5M', '-bufsize', '10M', '-c:a', 'copy', '-movflags', '+faststart', join(outDir, `${id}-partage.mp4`)], cacheDir);
  }
  writeFileSync(join(outDir, `${name}.txt`), `${ep.caption}\n\n${ep.disclaimer || ''}\n\n${(ep.hashtags || []).join(' ')}\n`);
  console.log(`\n→ ${output}`);
}

main().catch((e) => {
  console.error(`\nErreur : ${e.message}`);
  process.exit(1);
});
