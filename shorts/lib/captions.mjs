// Habillage texte en ASS (rendu par libass) : sous-titres mot à mot, hook, étiquettes, mentions.
// Zones sûres TikTok/Reels : rien au-dessus de y=250, rien sous y=1450, marge droite élargie
// (colonne des boutons).

import { FPS, sameWord } from './timeline.mjs';

const YELLOW = '&H0000D4FF&';
const RED = '&H00303BFF&';
const WHITE = '&H00FFFFFF&';

const CAPTION_Y = 1170;
const LABEL_Y = 1370;
const MAX_WORDS = 3;
const MAX_CHARS = 16;

const stamp = (t) => {
  const c = Math.max(0, Math.round(t * 100));
  const h = Math.floor(c / 360000);
  const m = Math.floor((c % 360000) / 6000);
  const s = Math.floor((c % 6000) / 100);
  return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}.${String(c % 100).padStart(2, '0')}`;
};
const clean = (s) => s.replace(/[{}\\]/g, '');
const display = (word) => clean(word).replace(/[.,;:…]+$/g, '').replace(/^[«"]|[»"]$/g, '').toUpperCase();
const dialogue = (layer, start, end, style, text) => `Dialogue: ${layer},${stamp(start)},${stamp(end)},${style},,0,0,0,,${text}`;

function wrap(text, max) {
  const lines = [];
  let line = '';
  for (const w of text.split(/\s+/)) {
    if (line && (line + ' ' + w).length > max) {
      lines.push(line);
      line = w;
    } else line = line ? `${line} ${w}` : w;
  }
  if (line) lines.push(line);
  return lines.join('\\N');
}

function groupWords(words) {
  const groups = [];
  let cur = [];
  for (const w of words) {
    const len = cur.reduce((n, x) => n + display(x.text).length + 1, 0) + display(w.text).length;
    const prev = cur[cur.length - 1];
    if (cur.length && (cur.length >= MAX_WORDS || len > MAX_CHARS || /[.,!?;:…]$/.test(prev.text))) {
      groups.push(cur);
      cur = [];
    }
    cur.push(w);
  }
  if (cur.length) groups.push(cur);
  return groups;
}

function captionEvents(shot, nextStart) {
  const emphasis = shot.emphasis || [];
  const groups = groupWords(shot.words);
  const events = [];
  groups.forEach((g, gi) => {
    const gEnd = gi + 1 < groups.length ? groups[gi + 1][0].start : Math.min(g[g.length - 1].end + 0.35, nextStart);
    g.forEach((w, wi) => {
      const start = wi === 0 ? g[0].start : w.start;
      const end = wi + 1 < g.length ? g[wi + 1].start : gEnd;
      const text = g
        .map((x, xi) => {
          const color = xi === wi ? YELLOW : emphasis.some((e) => sameWord(x.text, e)) ? RED : WHITE;
          return `{\\c${color}}${display(x.text)}`;
        })
        .join(' ');
      const pop = wi === 0 ? '\\fscx78\\fscy78\\t(0,90,\\fscx104\\fscy104)\\t(90,150,\\fscx100\\fscy100)' : '';
      events.push(dialogue(1, start, end, 'Cap', `{\\an5\\pos(540,${CAPTION_Y})${pop}}${text}`));
    });
  });
  return events;
}

export function buildAss(ep, timeline, duration) {
  const lines = [
    '[Script Info]',
    'ScriptType: v4.00+',
    'PlayResX: 1080',
    'PlayResY: 1920',
    'WrapStyle: 2',
    'ScaledBorderAndShadow: yes',
    '',
    '[V4+ Styles]',
    'Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding',
    'Style: Cap,Anton,104,&H00FFFFFF,&H00FFFFFF,&H00000000,&H96000000,0,0,0,0,100,100,1,0,1,8,5,5,110,170,0,1',
    'Style: Hook,Bangers,96,&H00101010,&H00101010,&H0000D4FF,&H64000000,0,0,0,0,100,100,2,0,3,20,0,8,90,90,0,1',
    'Style: Name,Bangers,64,&H00FFFFFF,&H00FFFFFF,&H00303BFF,&H00000000,0,0,0,0,100,100,2,0,3,12,0,4,0,0,0,1',
    'Style: Role,Anton,36,&H00101010,&H00101010,&H00FFFFFF,&H00000000,0,0,0,0,100,100,1,0,3,8,0,4,0,0,0,1',
    'Style: Note,Anton,30,&H40FFFFFF,&H40FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,1,0,1,2,0,8,90,90,0,1',
    '',
    '[Events]',
    'Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text',
  ];

  const first = timeline[0];
  const hookEnd = Math.min(first.to / FPS + 0.2, 3.2);
  lines.push(
    dialogue(2, 0, hookEnd, 'Hook', `{\\an8\\pos(540,290)\\frz-2.5\\fscx40\\fscy40\\t(0,120,\\fscx108\\fscy108)\\t(120,200,\\fscx100\\fscy100)\\fad(0,120)}${wrap(clean(ep.hook.toUpperCase()), 18)}`),
  );

  for (const shot of timeline) {
    const next = shot.k + 1 < timeline.length ? timeline[shot.k + 1].from / FPS : duration;
    lines.push(...captionEvents(shot, next));
    if (shot.label) {
      const [name, role] = shot.label.split('|').map((s) => clean(s.trim()));
      const s = shot.from / FPS + 0.25;
      const e = Math.min(shot.to / FPS - 0.1, s + 2.6);
      lines.push(dialogue(3, s, e, 'Name', `{\\an4\\move(-600,${LABEL_Y},70,${LABEL_Y},0,220)\\frz2\\fad(0,150)}${name.toUpperCase()}`));
      if (role) lines.push(dialogue(3, s + 0.12, e, 'Role', `{\\an4\\move(-600,${LABEL_Y + 78},84,${LABEL_Y + 78},0,240)\\fad(0,150)}${role.toUpperCase()}`));
    }
  }

  if (ep.disclaimer) {
    const last = timeline[timeline.length - 1];
    lines.push(dialogue(1, last.from / FPS, duration, 'Note', `{\\an8\\pos(540,1400)\\fad(200,0)}${clean(ep.disclaimer)}`));
  }
  return lines.join('\n') + '\n';
}
