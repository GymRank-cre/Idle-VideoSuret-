// Remplace la voix d'une vidéo déjà montée (sous-titres incrustés) par une voix ElevenLabs.
// Chaque phrase est régénérée puis posée à l'instant exact où la phrase d'origine commençait,
// légèrement accélérée si besoin, pour que les sous-titres restent synchronisés.
//
//   node shorts/revoix.mjs shorts/revoix/le-renard-ep2.json            # rendu
//   node shorts/revoix.mjs shorts/revoix/le-renard-ep2.json --phrases  # liste les phrases
//
// Config : { video, transcript (JSON speech-to-text ElevenLabs avec mots horodatés),
//            output, voice, music: { prompt, volume }, fix: { "<index>": "texte corrigé" } }

import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { hash, music, tts } from './lib/eleven.mjs';
import { ffmpeg, mixAudio } from './lib/render.mjs';
import { ensureSfx } from './lib/sfx.mjs';

const ROOT = dirname(new URL(import.meta.url).pathname);
const MAX_TEMPO = 1.3; // au-delà, la voix devient caricaturale : on préfère déborder un peu

function loadDotEnv(path) {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2].replace(/^['"]|['"]$/g, '');
  }
}

const probe = (file, entries) =>
  spawnSync('ffprobe', ['-v', 'error', '-show_entries', entries, '-of', 'csv=p=0', file], { encoding: 'utf8' }).stdout.trim();

function sentences(words) {
  const out = [];
  let cur = null;
  for (const w of words) {
    if (w.type === 'audio_event') continue;
    if (w.type === 'spacing') {
      if (cur) cur.text += ' ';
      continue;
    }
    if (!cur) cur = { text: '', start: w.start, end: w.end };
    cur.text += w.text;
    cur.end = w.end;
    if (/[.!?…»]$/.test(w.text)) {
      out.push(cur);
      cur = null;
    }
  }
  if (cur) out.push(cur);
  return out.map((s) => ({ ...s, text: s.text.replace(/\s+/g, ' ').trim() }));
}

// Coupes franches de la vidéo, espacées d'au moins `gap` secondes (pour les whooshes).
function cuts(video, gap = 1.5) {
  const r = spawnSync('ffmpeg', ['-v', 'info', '-i', video, '-vf', 'scdet=threshold=12', '-an', '-f', 'null', '-'], { encoding: 'utf8' });
  const times = [...r.stderr.matchAll(/lavfi\.scd\.time: ([0-9.]+)/g)].map((m) => parseFloat(m[1]));
  return times.filter((t, i) => i === 0 || t - times[i - 1] >= gap).filter((t) => t > 0.5);
}

async function main() {
  loadDotEnv(join(ROOT, '..', '.env'));
  const cfgPath = resolve(process.argv[2]);
  const base = dirname(cfgPath);
  const cfg = JSON.parse(readFileSync(cfgPath, 'utf8'));
  const video = resolve(base, cfg.video);
  const phrases = sentences(JSON.parse(readFileSync(resolve(base, cfg.transcript), 'utf8')).words);
  phrases.forEach((p, i) => (p.say = cfg.fix?.[i] ?? p.text));

  if (process.argv.includes('--phrases')) {
    phrases.forEach((p, i) => console.log(`${i}  ${p.start.toFixed(2)}-${p.end.toFixed(2)}  ${p.say}`));
    return;
  }

  const cacheDir = join(base, 'cache', cfg.name);
  mkdirSync(cacheDir, { recursive: true });
  const duration = parseFloat(probe(video, 'format=duration'));

  console.log(`1/4 Voix (${phrases.length} phrases)`);
  const segments = [];
  for (const [i, p] of phrases.entries()) {
    const context = { previousText: phrases[i - 1]?.say, nextText: phrases[i + 1]?.say };
    const file = join(cacheDir, `phrase-${hash({ say: p.say, voice: cfg.voice, context })}.mp3`);
    const al = await tts(p.say, cfg.voice, file, context);
    const idx = al.characters.map((c, k) => (/\s/.test(c) ? -1 : k)).filter((k) => k >= 0);
    const a0 = al.character_start_times_seconds[idx[0]];
    const a1 = al.character_end_times_seconds[idx[idx.length - 1]] + 0.05;
    const next = phrases[i + 1];
    const window = (next ? next.start - 0.06 : Math.min(p.end + 0.8, duration - 0.3)) - p.start;
    const tempo = Math.min(MAX_TEMPO, Math.max(1, (a1 - a0) / window));
    if ((a1 - a0) / tempo > window + 0.05) console.warn(`  ! phrase ${i} déborde de ${((a1 - a0) / tempo - window).toFixed(2)} s`);
    segments.push({ file, a0, a1, tempo, at: p.start });
  }

  const voiceWav = join(cacheDir, 'voix.wav');
  const chains = segments.map((s, i) => {
    const ms = Math.round(s.at * 1000);
    return `[${i}:a]atrim=${s.a0.toFixed(3)}:${s.a1.toFixed(3)},asetpts=PTS-STARTPTS,atempo=${s.tempo.toFixed(3)},aresample=48000,aformat=channel_layouts=mono,adelay=delays=${ms}:all=1,apad=whole_dur=${duration.toFixed(3)}[s${i}]`;
  });
  chains.push(`${segments.map((_, i) => `[s${i}]`).join('')}amix=inputs=${segments.length}:normalize=0:duration=longest,asetpts=N/SR/TB,atrim=0:${duration.toFixed(3)}[v]`);
  await ffmpeg([...segments.flatMap((s) => ['-i', s.file]), '-filter_complex', chains.join(';'), '-map', '[v]', voiceWav], cacheDir);

  console.log('2/4 Musique et bruitages');
  let bed = null;
  if (cfg.music?.prompt) {
    const seconds = Math.ceil(duration) + 2;
    const file = join(cacheDir, `music-${hash({ p: cfg.music.prompt, seconds })}.mp3`);
    await music(cfg.music.prompt, seconds, file);
    bed = { file, volume: cfg.music.volume };
  }
  const lib = await ensureSfx(['whoosh']);
  const cues = cfg.whooshOnCuts === false ? [] : cuts(video).map((t) => ({ file: lib.whoosh.file, time: t - 0.15, volume: 0.3 }));

  console.log('3/4 Mixage');
  await mixAudio({ voice: voiceWav, music: bed, cues, drops: [], duration, cacheDir });

  console.log('4/4 Export');
  const output = resolve(base, cfg.output);
  await ffmpeg(['-i', video, '-i', 'mix.wav', '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-shortest', '-movflags', '+faststart', output], cacheDir);
  console.log(`\n→ ${output}`);
}

main().catch((e) => {
  console.error(`\nErreur : ${e.message}`);
  process.exit(1);
});
