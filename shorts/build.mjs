// Génère des shorts verticaux 1080x1920 : voix ElevenLabs + sous-titres mot à mot + fond animé.
//
//   ELEVENLABS_API_KEY=... ELEVENLABS_VOICE_ID=... node shorts/build.mjs
//   node shorts/build.mjs voices            # liste les voix disponibles
//   node shorts/build.mjs --dry             # sans API : voix muette, pour tester le rendu
//   node shorts/build.mjs --only 03-epargne-automatique
//
// Sorties dans shorts/out/ : <id>.mp4 et <id>.json (légende + hashtags à publier).

import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const outDir = join(root, 'out');
const args = process.argv.slice(2);
const dry = args.includes('--dry');
const only = args.includes('--only') ? args[args.indexOf('--only') + 1] : null;

const API = 'https://api.elevenlabs.io/v1';
const MODEL = process.env.ELEVENLABS_MODEL || 'eleven_multilingual_v2';

loadDotEnv(join(root, '..', '.env'));

function loadDotEnv(path) {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, 'utf8').split('\n')) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*?)\s*$/);
    if (m && !(m[1] in process.env)) process.env[m[1]] = m[2].replace(/^['"]|['"]$/g, '');
  }
}

function requireEnv(name) {
  if (!process.env[name]) {
    console.error(`Variable ${name} manquante (voir shorts/README.md).`);
    process.exit(1);
  }
  return process.env[name];
}

async function listVoices() {
  const res = await fetch(`${API}/voices`, { headers: { 'xi-api-key': requireEnv('ELEVENLABS_API_KEY') } });
  if (!res.ok) throw new Error(`ElevenLabs ${res.status}: ${await res.text()}`);
  const { voices } = await res.json();
  for (const v of voices) {
    console.log(`${v.voice_id}  ${v.name}  ${(v.labels && v.labels.language) || ''}`);
  }
}

// Retourne { audioPath, words: [{ text, start, end }] }
async function synthesize(ep) {
  const audioPath = join(outDir, `${ep.id}.mp3`);
  if (dry) return dryAudio(ep, audioPath);

  const res = await fetch(`${API}/text-to-speech/${requireEnv('ELEVENLABS_VOICE_ID')}/with-timestamps`, {
    method: 'POST',
    headers: { 'xi-api-key': requireEnv('ELEVENLABS_API_KEY'), 'Content-Type': 'application/json' },
    body: JSON.stringify({ text: ep.script, model_id: MODEL, voice_settings: { stability: 0.45, similarity_boost: 0.8, style: 0.3 } }),
  });
  if (!res.ok) throw new Error(`ElevenLabs ${res.status}: ${await res.text()}`);
  const data = await res.json();
  writeFileSync(audioPath, Buffer.from(data.audio_base64, 'base64'));
  return { audioPath, words: wordsFromAlignment(data.alignment) };
}

function wordsFromAlignment({ characters, character_start_times_seconds: starts, character_end_times_seconds: ends }) {
  const words = [];
  let cur = null;
  characters.forEach((ch, i) => {
    if (/\s/.test(ch)) {
      cur = null;
      return;
    }
    if (!cur) words.push((cur = { text: '', start: starts[i], end: ends[i] }));
    cur.text += ch;
    cur.end = ends[i];
  });
  return words;
}

// Mode test : durée estimée (~2,7 mots/s) et audio silencieux.
function dryAudio(ep, audioPath) {
  const tokens = ep.script.split(/\s+/).filter(Boolean);
  const per = 1 / 2.7;
  const words = tokens.map((text, i) => ({ text, start: i * per, end: (i + 1) * per }));
  const duration = tokens.length * per + 0.5;
  run('ffmpeg', ['-y', '-v', 'error', '-f', 'lavfi', '-i', 'anullsrc=r=44100:cl=mono', '-t', duration.toFixed(2), '-c:a', 'libmp3lame', audioPath]);
  return { audioPath, words };
}

const cs = (t) => Math.max(1, Math.round(t * 100));
const stamp = (t) => {
  const c = Math.round(t * 100);
  const h = Math.floor(c / 360000);
  const m = Math.floor((c % 360000) / 6000);
  const s = Math.floor((c % 6000) / 100);
  return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}.${String(c % 100).padStart(2, '0')}`;
};
const esc = (s) => s.replace(/[{}\\]/g, '');

// Sous-titres karaoké : groupes de 3 mots, le mot prononcé passe en jaune.
function buildAss(ep, words, totalDuration, disclaimer) {
  const head = `[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 0

[V4+ Styles]
Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding
Style: Cap,DejaVu Sans,96,&H0000F0FF,&H00FFFFFF,&H00000000,&H80000000,1,0,0,0,100,100,0,0,1,8,2,5,70,70,0,1
Style: Hook,DejaVu Sans,72,&H00FFFFFF,&H00FFFFFF,&H00000000,&H80000000,1,0,0,0,100,100,0,0,1,6,2,8,70,70,260,1
Style: Foot,DejaVu Sans,34,&H00DDDDDD,&H00DDDDDD,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,3,0,2,60,60,110,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
`;

  const lines = [head.trimEnd()];
  lines.push(`Dialogue: 0,${stamp(0)},${stamp(3.2)},Hook,,0,0,0,,${esc(ep.hook.toUpperCase())}`);
  lines.push(`Dialogue: 0,${stamp(0)},${stamp(totalDuration)},Foot,,0,0,0,,${esc(disclaimer)}`);

  for (let i = 0; i < words.length; i += 3) {
    const group = words.slice(i, i + 3);
    const start = group[0].start;
    const end = i + 3 < words.length ? words[i + 3].start : group[group.length - 1].end + 0.3;
    const text = group
      .map((w, j) => {
        const next = group[j + 1] ? group[j + 1].start : end;
        return `{\\k${cs(next - w.start)}}${esc(w.text.toUpperCase())}`;
      })
      .join(' ');
    lines.push(`Dialogue: 1,${stamp(start)},${stamp(end)},Cap,,0,0,0,,${text}`);
  }
  return lines.join('\n') + '\n';
}

function run(cmd, cmdArgs, opts = {}) {
  const r = spawnSync(cmd, cmdArgs, { stdio: ['ignore', 'inherit', 'inherit'], ...opts });
  if (r.status !== 0) throw new Error(`${cmd} a échoué (code ${r.status})`);
}

function probeDuration(path) {
  const r = spawnSync('ffprobe', ['-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', path], { encoding: 'utf8' });
  return parseFloat(r.stdout);
}

const PALETTES = [
  ['0x0f2027', '0x2c5364'],
  ['0x1a1a2e', '0x0f3460'],
  ['0x232526', '0x414345'],
  ['0x134e5e', '0x71b280'],
  ['0x2b1055', '0x7597de'],
];

async function build(ep, idx, disclaimer) {
  console.log(`→ ${ep.id}`);
  const { audioPath, words } = await synthesize(ep);
  const duration = probeDuration(audioPath) + 0.4;
  writeFileSync(join(outDir, `${ep.id}.ass`), buildAss(ep, words, duration, disclaimer));
  const [c0, c1] = PALETTES[idx % PALETTES.length];

  run(
    'ffmpeg',
    [
      '-y', '-v', 'error',
      '-f', 'lavfi', '-i', `gradients=s=1080x1920:c0=${c0}:c1=${c1}:speed=0.015:rate=30:d=${duration.toFixed(2)}`,
      '-i', `${ep.id}.mp3`,
      '-vf', `subtitles=${ep.id}.ass`,
      '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '21', '-pix_fmt', 'yuv420p',
      '-c:a', 'aac', '-b:a', '160k', '-shortest', '-movflags', '+faststart',
      `${ep.id}.mp4`,
    ],
    { cwd: outDir },
  );

  writeFileSync(
    join(outDir, `${ep.id}.json`),
    JSON.stringify({ id: ep.id, duration: +duration.toFixed(1), text: `${ep.caption}\n\n${disclaimer}\n\n${ep.hashtags.join(' ')}` }, null, 2),
  );
}

async function main() {
  if (args[0] === 'voices') return listVoices();
  mkdirSync(outDir, { recursive: true });
  const { disclaimer, episodes } = JSON.parse(readFileSync(join(root, 'content', 'episodes.json'), 'utf8'));
  const todo = episodes.filter((e) => !only || e.id === only);
  if (!todo.length) throw new Error(`Épisode introuvable : ${only}`);
  for (const [i, ep] of todo.entries()) await build(ep, i, disclaimer);
  console.log(`\n${todo.length} short(s) dans ${outDir}`);
}

main().catch((e) => {
  console.error(e.message);
  process.exit(1);
});
