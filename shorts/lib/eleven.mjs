// Accès ElevenLabs : voix horodatée, bruitages, musique. Tout est mis en cache sur disque
// pour ne jamais repayer une génération identique.

import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

const API = 'https://api.elevenlabs.io/v1';

export const hash = (o) => createHash('sha1').update(JSON.stringify(o)).digest('hex').slice(0, 12);

function apiKey() {
  const key = process.env.ELEVENLABS_API_KEY;
  if (!key) throw new Error('ELEVENLABS_API_KEY manquante (voir shorts/README.md).');
  return key;
}

async function post(path, body, { binary = false } = {}) {
  const res = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: { 'xi-api-key': apiKey(), 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`ElevenLabs ${path} ${res.status}: ${await res.text()}`);
  return binary ? Buffer.from(await res.arrayBuffer()) : res.json();
}

function save(file, data) {
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, data);
}

// Retourne l'alignement caractère par caractère ; l'audio est écrit dans `file`.
export async function tts(text, voice, file) {
  const meta = `${file}.json`;
  if (existsSync(file) && existsSync(meta)) return JSON.parse(readFileSync(meta, 'utf8'));
  const data = await post(`/text-to-speech/${voice.id}/with-timestamps`, {
    text,
    model_id: voice.model || 'eleven_multilingual_v2',
    voice_settings: {
      stability: voice.stability ?? 0.4,
      similarity_boost: voice.similarity ?? 0.8,
      style: voice.style ?? 0.35,
      speed: voice.speed ?? 1.0,
      use_speaker_boost: true,
    },
  });
  save(file, Buffer.from(data.audio_base64, 'base64'));
  save(meta, JSON.stringify(data.alignment));
  return data.alignment;
}

export async function soundEffect(prompt, duration, file) {
  if (existsSync(file)) return;
  save(file, await post('/sound-generation', { text: prompt, duration_seconds: duration, prompt_influence: 0.6 }, { binary: true }));
}

export async function music(prompt, seconds, file) {
  if (existsSync(file)) return;
  save(file, await post('/music', { prompt, music_length_ms: Math.round(seconds * 1000) }, { binary: true }));
}
