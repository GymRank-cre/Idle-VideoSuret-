// Bibliothèque de bruitages partagée entre épisodes. Un son absent est généré une fois par
// ElevenLabs puis conservé dans shorts/audio/sfx/. Un épisode peut en ajouter dans son JSON
// ("sfxLibrary": { "nom": { "prompt": "...", "duration": 1.5, "volume": 0.8 } }).

import { join } from 'node:path';
import { soundEffect } from './eleven.mjs';

export const LIBRARY = {
  whoosh: { prompt: 'fast clean cinematic whoosh swish transition, very short, no tail', duration: 0.8, volume: 0.55 },
  impact: { prompt: 'deep cinematic boom impact hit with short sub bass tail', duration: 1.5, volume: 0.8 },
  scratch: { prompt: 'vinyl record scratch stop, comedic', duration: 0.8, volume: 0.8 },
  pop: { prompt: 'cartoon bubble pop, short and bright', duration: 0.5, volume: 0.6 },
  meow: { prompt: 'single lazy satisfied house cat meow, close up', duration: 1.3, volume: 0.9 },
  purr: { prompt: 'cat purring softly, close microphone', duration: 2.5, volume: 0.7 },
  alarm: { prompt: 'warehouse intrusion alarm siren, electronic wailing, indoor', duration: 2.5, volume: 0.45 },
  beep: { prompt: 'security camera motion detection double beep, electronic', duration: 0.7, volume: 0.6 },
  sip: { prompt: 'man slurping hot coffee from a thermos cup then a tired sigh', duration: 2, volume: 0.85 },
  sigh: { prompt: 'tired middle aged man long bored sigh', duration: 1.5, volume: 0.85 },
  tiptoe: { prompt: 'cartoon sneaky tiptoe footsteps with pizzicato plucks', duration: 2.5, volume: 0.6 },
  door: { prompt: 'heavy metal warehouse door creaking open slowly', duration: 2, volume: 0.55 },
  glitch: { prompt: 'short digital video glitch stutter, electronic', duration: 0.6, volume: 0.5 },
  shutter: { prompt: 'camera lens motor zoom servo whir and click', duration: 1, volume: 0.6 },
  tap: { prompt: 'soft user interface taps and confirmation beeps on a tablet', duration: 1.2, volume: 0.55 },
  ding: { prompt: 'bright positive notification ding, clean', duration: 0.8, volume: 0.6 },
  riser: { prompt: 'short tension riser swelling up before a drop', duration: 1.5, volume: 0.45 },
  tick: { prompt: 'clock ticking in an empty room, close', duration: 2, volume: 0.5 },
};

export const SFX_DIR = new URL('../audio/sfx/', import.meta.url).pathname;

export async function ensureSfx(names, extra = {}) {
  const lib = { ...LIBRARY, ...extra };
  const out = {};
  for (const name of new Set(names)) {
    const def = lib[name];
    if (!def) throw new Error(`Bruitage inconnu : ${name} (ajoute-le dans "sfxLibrary")`);
    const file = join(SFX_DIR, `${name}.mp3`);
    await soundEffect(def.prompt, def.duration, file);
    out[name] = { file, volume: def.volume ?? 0.7 };
  }
  return out;
}
