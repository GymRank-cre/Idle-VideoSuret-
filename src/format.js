// Mise en forme des nombres, durées et pourcentages.
// Échelle longue française : k, M, Md (milliard), Bn (billion), Bd (billiard)...

const SUFFIXES = [
  '', 'k', 'M', 'Md', 'Bn', 'Bd', 'Tn', 'Td',
  'Qa', 'Qd', 'Qi', 'Qs', 'Sx', 'Sd', 'Sp', 'Od', 'Nn', 'Dc',
];

/** Abrège un nombre : 1234 -> "1.23k", 4.5e9 -> "4.50Md". */
export function fmt(n) {
  if (!isFinite(n)) return '∞';
  if (n < 0) return '-' + fmt(-n);
  if (n < 1000) return n < 10 ? trim(n.toFixed(2)) : Math.floor(n).toString();

  let tier = Math.floor(Math.log10(n) / 3);
  if (tier >= SUFFIXES.length) tier = SUFFIXES.length - 1;
  const scaled = n / Math.pow(1000, tier);
  // Garde 3 chiffres significatifs : 999 -> "999", 12.3 -> "12.3", 1.23 -> "1.23"
  const digits = scaled >= 100 ? 0 : scaled >= 10 ? 1 : 2;
  return trim(scaled.toFixed(digits)) + SUFFIXES[tier];
}

/** Somme d'argent, toujours suffixée de l'euro. */
export function money(n) {
  return fmt(n) + ' €';
}

/** Durée en secondes -> "1j 04h", "12m 30s", "4.2s". */
export function duration(s) {
  if (!isFinite(s) || s < 0) return '—';
  if (s < 10) return trim(s.toFixed(1)) + 's';
  s = Math.floor(s);
  if (s < 60) return s + 's';
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ${pad(s % 60)}s`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ${pad(m % 60)}m`;
  const d = Math.floor(h / 24);
  return `${d}j ${pad(h % 24)}h`;
}

/** Multiplicateur lisible : 1.25 -> "+25 %", 3 -> "×3". */
export function multiplier(m) {
  return m >= 2 ? '×' + trim(m.toFixed(2)) : '+' + Math.round((m - 1) * 100) + ' %';
}

export function percent(p) {
  return Math.round(p * 100) + ' %';
}

function trim(s) {
  return s.includes('.') ? s.replace(/\.?0+$/, '') : s;
}

function pad(n) {
  return n < 10 ? '0' + n : String(n);
}
