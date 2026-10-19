// Calage du montage sur la voix : chaque plan commence sur le premier mot de sa réplique,
// avec une légère avance de l'image sur le son (réflexe de monteur : l'œil précède l'oreille).

export const FPS = 30;
const LEAD = 3; // images d'avance de la coupe sur la voix
const TAIL = 0.8; // respiration après le dernier mot (s)

// Durée des transitions en images (toujours paires : la moitié de chaque côté de la coupe).
export const TRANSITIONS = {
  cut: { frames: 2, xfade: 'fade' },
  whip: { frames: 6, xfade: 'smoothleft', sfx: 'whoosh' },
  'whip-up': { frames: 6, xfade: 'smoothup', sfx: 'whoosh' },
  flash: { frames: 8, xfade: 'fadewhite', sfx: 'impact' },
  zoom: { frames: 10, xfade: 'zoomin', sfx: 'whoosh' },
  glitch: { frames: 8, xfade: 'pixelize', sfx: 'glitch' },
  fade: { frames: 14, xfade: 'fadeblack' },
};

export const normalize = (s) =>
  s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^\p{L}\p{N}']/gu, '')
    .toUpperCase();

// Correspondance tolérante : « l'alarme » répond à « alarme » comme à « l'alarme ».
export const sameWord = (word, target) => {
  const w = normalize(word);
  const t = normalize(target);
  return w === t || w.split("'").pop() === t;
};

export function wordsFromAlignment({ characters, character_start_times_seconds: starts, character_end_times_seconds: ends }) {
  const words = [];
  let cur = null;
  characters.forEach((ch, i) => {
    if (/\s/.test(ch)) {
      cur = null;
      return;
    }
    if (!cur) words.push((cur = { text: '', start: starts[i], end: ends[i], index: i }));
    cur.text += ch;
    cur.end = ends[i];
  });
  return words;
}

export function joinScript(shots) {
  const offsets = [];
  let text = '';
  for (const shot of shots) {
    if (text) text += ' ';
    offsets.push(text.length);
    text += shot.say.trim();
  }
  return { text, offsets };
}

// Retourne les plans enrichis : frames de début/fin, transitions, mots de la réplique.
export function buildTimeline(shots, offsets, words) {
  const starts = offsets.map((off, k) => {
    if (k === 0) return 0;
    const w = words.find((x) => x.index >= off);
    return Math.max(0, Math.round(w.start * FPS) - LEAD);
  });
  const end = Math.round((words[words.length - 1].end + TAIL) * FPS);

  return shots.map((shot, k) => {
    const from = starts[k];
    const to = k + 1 < shots.length ? starts[k + 1] : end;
    const tin = k === 0 ? 0 : (TRANSITIONS[shot.in || 'cut'] || TRANSITIONS.cut).frames;
    const next = shots[k + 1];
    const tout = next ? (TRANSITIONS[next.in || 'cut'] || TRANSITIONS.cut).frames : 0;
    const own = words.filter((w) => w.index >= offsets[k] && (k + 1 >= offsets.length || w.index < offsets[k + 1]));
    if (to - from < FPS * 0.7) console.warn(`  ! plan ${k + 1} très court (${((to - from) / FPS).toFixed(2)} s)`);
    return { ...shot, k, from, to, tin, tout, frames: to - from + tin / 2 + tout / 2, words: own };
  });
}

// Instant (s) d'un repère de bruitage : "start", nombre de secondes après le début du plan,
// ou "mot:<texte>" pour caler sur un mot précis de la réplique.
export function cueTime(shot, at) {
  const base = shot.from / FPS;
  if (at === undefined || at === 'start') return base;
  if (typeof at === 'number') return base + at;
  if (typeof at === 'string' && at.startsWith('mot:')) {
    const target = normalize(at.slice(4));
    const w = shot.words.find((x) => normalize(x.text).startsWith(target) || normalize(x.text).split("'").pop().startsWith(target));
    if (!w) throw new Error(`Mot introuvable dans le plan ${shot.k + 1} : ${at}`);
    return w.start;
  }
  if (at === 'end') return shot.to / FPS;
  throw new Error(`Repère inconnu : ${at}`);
}
