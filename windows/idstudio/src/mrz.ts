import { Soldier } from './types';

// ICAO 9303 TD1 (ID-1 card) machine-readable zone computed from the soldier's real details.
const value = (c: string) => c === '<' ? 0 : /[0-9]/.test(c) ? +c : c.charCodeAt(0) - 55;
export const checkDigit = (s: string) => String([...s].reduce((sum, c, i) => sum + value(c) * [7, 3, 1][i % 3], 0) % 10);
const clean = (s: string) => (s || '').toUpperCase().normalize('NFD').replace(/[^A-Z0-9]+/g, '<');
const pad = (s: string, n: number) => (s + '<'.repeat(n)).slice(0, n);

/** DD-MM-YYYY / DD/MM/YYYY / YYYY-MM-DD → YYMMDD, or '<<<<<<' when not a date. */
export function yymmdd(raw: string): string {
  const t = (raw || '').trim();
  let m = t.match(/^(\d{1,2})[-/. ](\d{1,2})[-/. ](\d{4})$/);
  if (m) return m[3].slice(2) + m[2].padStart(2, '0') + m[1].padStart(2, '0');
  m = t.match(/^(\d{4})[-/. ](\d{1,2})[-/. ](\d{1,2})$/);
  if (m) return m[1].slice(2) + m[2].padStart(2, '0') + m[3].padStart(2, '0');
  return '<<<<<<';
}

export function mrz(s: Soldier, state: string): [string, string, string] {
  const st = pad(clean(state) || 'XXX', 3);
  const docRaw = clean(s.armyNo || s.id).replace(/</g, '');
  const doc = pad(docRaw.slice(0, 9), 9);
  const opt1 = pad(docRaw.length > 9 ? docRaw.slice(9) + '<' + clean(s.id) : clean(s.id), 15);
  const line1 = pad('ID' + st + doc + checkDigit(doc) + opt1, 30);
  const dob = yymmdd(s.dob), exp = yymmdd(s.expiryDate);
  const body2 = dob + checkDigit(dob) + '<' + exp + checkDigit(exp) + st + '<'.repeat(11);
  const composite = checkDigit(line1.slice(5, 30) + body2.slice(0, 7) + body2.slice(8, 15) + body2.slice(18, 29));
  const line2 = pad(body2, 29) + composite;
  const parts = (s.name || '').trim().split(/\s+/).filter(Boolean);
  const surname = parts.length > 1 ? parts[parts.length - 1] : parts[0] || '';
  const given = parts.length > 1 ? parts.slice(0, -1).join(' ') : '';
  const line3 = pad(clean(surname) + '<<' + clean(given), 30);
  return [line1, line2, line3];
}

/** SHA-256 fingerprint of the printed identity details (changes if any printed detail changes). */
export async function cardCheck(s: Soldier): Promise<string> {
  const data = [s.id, s.armyNo, s.rank, s.name, s.unit, s.company, s.platoon, s.section, s.dob, s.expiryDate, s.bloodGroup, s.secret].join('|');
  const hash = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(data));
  return [...new Uint8Array(hash)].slice(0, 8).map(b => b.toString(16).padStart(2, '0')).join('').toUpperCase().replace(/(.{4})/g, '$1-').slice(0, -1);
}
