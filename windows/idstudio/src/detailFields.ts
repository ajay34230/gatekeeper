import type { CSSProperties } from 'react';
import { CardDesign, DetailFieldKey, Soldier } from './types';

/** Every field a "detail box" can show, in a fixed catalogue order. The front/back grids show whichever of these
 * are listed in design.frontFields / design.backFields — add or remove one there and the rest of the grid
 * (Tailwind's grid-cols-2, wrapping automatically) re-flows on its own. */
export const DETAIL_FIELD_CATALOG: { key: DetailFieldKey; label: string; get: (s: Soldier) => string }[] = [
  { key: 'unit', label: 'UNIT', get: s => s.unit },
  { key: 'company', label: 'COMPANY (COY)', get: s => s.company },
  { key: 'platoon', label: 'PLATOON', get: s => s.platoon },
  { key: 'section', label: 'SECTION (SEC)', get: s => s.section },
  { key: 'appointment', label: 'APPOINTMENT', get: s => s.appointment },
  { key: 'bloodGroup', label: 'BLOOD GROUP', get: s => s.bloodGroup },
  { key: 'mobile', label: 'MOBILE', get: s => s.mobile },
  { key: 'expiry', label: 'VALID TILL / EXPIRY', get: s => s.expiryDate },
  { key: 'dob', label: 'DATE OF BIRTH', get: s => s.dob },
  { key: 'enrolDate', label: 'DATE OF ENROLMENT', get: s => s.enrolDate },
  { key: 'cardSerial', label: 'CARD SERIAL', get: s => s.cardSerial },
];

export const detailField = (key: DetailFieldKey) => DETAIL_FIELD_CATALOG.find(f => f.key === key) ?? DETAIL_FIELD_CATALOG[0];

/** CSS custom property carrying the chosen detail text scale, read by every detail box's inline font-size. */
export const detailScaleVar = (design: CardDesign): CSSProperties => ({ ['--xvs' as any]: design.detailFontScale ?? 1 } as CSSProperties);
/** A detail box's own font-size (px at scale 1) scaled by --xvs. */
export const detailPx = (px: number) => ({ fontSize: `calc(${px}px * var(--xvs, 1))` });
/** A detail box value's colour: the design override if set, otherwise the caller's own theme colour. */
export const detailColor = (design: CardDesign, fallback: string) => (design.detailFontColor ? { color: design.detailFontColor } : { color: fallback });

/** Text sizes offered for the detail boxes (multiplies each box's own base size, so the label/value stay in step). */
export const DETAIL_FONT_SCALES: { label: string; value: number }[] = [
  { label: 'Small', value: 0.85 },
  { label: 'Standard (previous size)', value: 1 },
  { label: 'Large (recommended)', value: 1.2 },
  { label: 'Extra Large', value: 1.45 },
];

/** Preset colours offered for detail box text, plus a custom picker in the Studio. */
export const DETAIL_COLOR_PRESETS: { label: string; value: string }[] = [
  { label: 'Theme default', value: '' },
  { label: 'White', value: '#f8fafc' },
  { label: 'Amber', value: '#fbbf24' },
  { label: 'Cyan', value: '#67e8f9' },
  { label: 'Emerald', value: '#6ee7b7' },
  { label: 'Slate', value: '#cbd5e1' },
];
