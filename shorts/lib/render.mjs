// Rendu vidéo et audio avec ffmpeg.

import { spawn } from 'node:child_process';
import { cpus } from 'node:os';
import { FPS, TRANSITIONS } from './timeline.mjs';

const FONTS_DIR = new URL('../fonts/', import.meta.url).pathname;
const VIDEO_EXT = /\.(mp4|mov|webm|mkv)$/i;

export function ffmpeg(args, cwd) {
  return new Promise((resolve, reject) => {
    const p = spawn('ffmpeg', ['-y', '-hide_banner', '-v', 'error', ...args], { cwd, stdio: ['ignore', 'inherit', 'inherit'] });
    p.on('close', (code) => (code === 0 ? resolve() : reject(new Error(`ffmpeg a échoué (code ${code})`))));
  });
}

async function pool(tasks, size = Math.max(1, cpus().length - 1)) {
  let i = 0;
  const worker = async () => {
    while (i < tasks.length) await tasks[i++]();
  };
  await Promise.all(Array.from({ length: Math.min(size, tasks.length) }, worker));
}

// --- Mouvements de caméra -------------------------------------------------------------
// Un mouvement va de [zoom, centreX, centreY] à [zoom, centreX, centreY] (coordonnées 0-1),
// avec un amorti, un éventuel « punch » (coup de zoom qui retombe) et un tremblement amorti.

const EASE = {
  linear: (p) => p,
  inout: (p) => `(${p}*${p}*(3-2*${p}))`,
  out: (p) => `(1-pow(1-${p},3))`,
};

function preset(shot) {
  const [fx, fy] = shot.focus || [0.5, 0.5];
  const P = {
    in: { from: [1.0, 0.5, 0.5], to: [1.2, fx, fy], ease: 'inout' },
    out: { from: [1.32, fx, fy], to: [1.04, 0.5, 0.5], ease: 'out' },
    punch: { from: [1.16, fx, fy], to: [1.24, fx, fy], ease: 'linear', kick: 0.22, shake: 16 },
    close: { from: [1.75, fx, fy], to: [1.95, fx, fy], ease: 'inout' },
    up: { from: [1.22, fx, 0.66], to: [1.22, fx, 0.36], ease: 'inout' },
    down: { from: [1.22, fx, 0.36], to: [1.22, fx, 0.66], ease: 'inout' },
    left: { from: [1.28, 0.62, fy], to: [1.28, 0.38, fy], ease: 'inout' },
    right: { from: [1.28, 0.38, fy], to: [1.28, 0.62, fy], ease: 'inout' },
    hold: { from: [1.04, fx, fy], to: [1.09, fx, fy], ease: 'linear' },
  };
  const base = P[shot.cam || 'in'];
  if (!base) throw new Error(`Mouvement inconnu : ${shot.cam}`);
  const m = { ...base, ...(shot.move || {}) };
  if (shot.zoom) {
    m.from = [shot.zoom[0], m.from[1], m.from[2]];
    m.to = [shot.zoom[1], m.to[1], m.to[2]];
  }
  if (shot.shake !== undefined) m.shake = shot.shake;
  if (shot.kick !== undefined) m.kick = shot.kick;
  return m;
}

function zoompan(shot, W, H, perFrame) {
  const m = preset(shot);
  const N = shot.frames;
  const P = `min(on/${Math.max(1, N - 1)},1)`;
  const e = EASE[m.ease](P);
  const lerp = (a, b) => (a === b ? `${a}` : `(${a}+(${(b - a).toFixed(4)})*${e})`);
  const kick = m.kick ? `+${m.kick}*exp(-on/${(FPS * 0.09).toFixed(2)})` : '';
  const shakeAmp = m.shake || 0;
  const shake = (fn, speed) => (shakeAmp ? `+${shakeAmp}*${fn}(on*${speed})*exp(-on/${(FPS * 0.45).toFixed(1)})*(iw/zoom)/${W}` : '');
  const z = `max(1,${lerp(m.from[0], m.to[0])}${kick})`;
  const x = `max(0,min(iw-iw/zoom,${lerp(m.from[1], m.to[1])}*iw-iw/zoom/2${shake('sin', 2.9)}))`;
  const y = `max(0,min(ih-ih/zoom,${lerp(m.from[2], m.to[2])}*ih-ih/zoom/2${shake('cos', 3.7)}))`;
  return `zoompan=z='${z}':x='${x}':y='${y}':d=${perFrame ? 1 : N}:s=${W}x${H}:fps=${FPS}`;
}

// Rend chaque plan en clip muet de la durée exacte (transitions comprises).
export async function renderShots(timeline, assetsDir, cacheDir, q) {
  const SW = q.W * q.ss;
  const SH = q.H * q.ss;
  const tasks = timeline.map((shot) => async () => {
    const src = `${assetsDir}/${shot.asset}`;
    const out = `shot-${String(shot.k).padStart(2, '0')}.mp4`;
    const isVideo = VIDEO_EXT.test(shot.asset);
    const prep = `crop='min(iw,ih*9/16)':'min(ih,iw*16/9)',scale=${SW}:${SH}:flags=lanczos`;
    const vf = isVideo
      ? `fps=${FPS},${prep},${zoompan(shot, q.W, q.H, true)}`
      : `${prep},${zoompan(shot, q.W, q.H, false)}`;
    const input = isVideo ? ['-stream_loop', '-1', '-i', src] : ['-i', src];
    await ffmpeg([...input, '-vf', `${vf},setsar=1,format=yuv420p`, '-frames:v', String(shot.frames), '-r', String(FPS), '-an', '-c:v', 'libx264', '-preset', q.preset, '-crf', '12', out], cacheDir);
    shot.clip = out;
  });
  await pool(tasks);
}

// --- Audio ------------------------------------------------------------------------------

export async function mixAudio({ voice, music, cues, drops, duration, cacheDir }) {
  const inputs = ['-i', voice];
  const chains = [];
  const D = duration.toFixed(3);
  chains.push(
    `[0:a]aresample=48000,aformat=channel_layouts=stereo,highpass=f=75,equalizer=f=3200:t=q:w=1.2:g=2.5,` +
      `acompressor=threshold=-20dB:ratio=3:attack=5:release=90:makeup=2,apad,atrim=0:${D},asplit=2[voice][key]`,
  );
  const mixIn = ['[voice]'];

  if (music) {
    inputs.push('-stream_loop', '-1', '-i', music.file);
    const gain = drops.length
      ? drops.map(([t0, len]) => `if(lt(t,${(t0 - 0.03).toFixed(3)}),1,if(lt(t,${(t0 + len).toFixed(3)}),0,min(1,(t-${(t0 + len).toFixed(3)})/0.5)))`).join('*')
      : '1';
    chains.push(
      `[1:a]aresample=48000,aformat=channel_layouts=stereo,atrim=0:${D},volume=${music.volume ?? 0.3},volume='${gain}':eval=frame,` +
        `afade=t=in:d=0.1,afade=t=out:st=${(duration - 1.6).toFixed(3)}:d=1.6[mraw]`,
      `[mraw][key]sidechaincompress=threshold=0.03:ratio=7:attack=10:release=400[music]`,
    );
    mixIn.push('[music]');
  } else {
    chains.push('[key]anullsink');
  }

  const first = inputs.filter((x) => x === '-i').length;
  cues.forEach((c, i) => {
    inputs.push('-i', c.file);
    const ms = Math.max(0, Math.round(c.time * 1000));
    chains.push(`[${first + i}:a]aresample=48000,aformat=channel_layouts=stereo,volume=${c.volume},adelay=${ms}|${ms}[s${i}]`);
    mixIn.push(`[s${i}]`);
  });

  chains.push(`${mixIn.join('')}amix=inputs=${mixIn.length}:normalize=0:duration=first,loudnorm=I=-14:TP=-1.5:LRA=9,aresample=48000[out]`);
  await ffmpeg([...inputs, '-filter_complex', chains.join(';'), '-map', '[out]', '-c:a', 'pcm_s16le', 'mix.wav'], cacheDir);
}

// --- Assemblage final -------------------------------------------------------------------

export async function assemble({ timeline, duration, cacheDir, output, q }) {
  const inputs = timeline.flatMap((s) => ['-i', s.clip]);
  inputs.push('-i', 'mix.wav');
  inputs.push('-f', 'lavfi', '-i', `color=c=0xFFD400:s=${q.W}x${Math.round(q.H / 240)}:r=${FPS}`);
  const audioIdx = timeline.length;
  const barIdx = timeline.length + 1;

  const parts = [];
  let last = '[0:v]';
  for (let k = 1; k < timeline.length; k++) {
    const t = TRANSITIONS[timeline[k].in || 'cut'] || TRANSITIONS.cut;
    const offset = (timeline[k].from - t.frames / 2) / FPS;
    parts.push(`${last}[${k}:v]xfade=transition=${t.xfade}:duration=${(t.frames / FPS).toFixed(4)}:offset=${offset.toFixed(4)}[x${k}]`);
    last = `[x${k}]`;
  }
  const grain = q.grain ? ',noise=alls=4:allf=t' : '';
  parts.push(
    `${last}eq=contrast=1.05:saturation=1.12,unsharp=5:5:0.35,vignette=angle=PI/6${grain},` +
      `subtitles=captions.ass:fontsdir=${FONTS_DIR}[graded]`,
    `[graded][${barIdx}:v]overlay=x='-w+w*t/${duration.toFixed(3)}':y=0:shortest=1,format=yuv420p[v]`,
  );

  await ffmpeg(
    [
      ...inputs,
      '-filter_complex', parts.join(';'),
      '-map', '[v]', '-map', `${audioIdx}:a`,
      '-t', duration.toFixed(3), '-r', String(FPS),
      '-c:v', 'libx264', '-preset', q.finalPreset, '-crf', String(q.crf), '-profile:v', 'high', '-pix_fmt', 'yuv420p',
      '-c:a', 'aac', '-b:a', '192k', '-ar', '48000', '-movflags', '+faststart',
      output,
    ],
    cacheDir,
  );
}
