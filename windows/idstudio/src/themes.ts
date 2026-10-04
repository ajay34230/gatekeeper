import { CardDesign, CardThemeConfig, SizeId, ThemeId } from './types';

export const CARD_THEMES: Record<ThemeId, CardThemeConfig> = {
  army: { id: 'army', name: 'Olive & Gold', bgGradient: 'from-[#0d160e] via-[#142316] to-[#0a120b]', cardBorder: 'border-amber-600/40', mode: 'dark' },
  tactical: { id: 'tactical', name: 'Tactical Steel', bgGradient: 'from-[#0b0e14] via-[#111722] to-[#080b10]', cardBorder: 'border-cyan-500/40', mode: 'dark' },
  navy: { id: 'navy', name: 'Deep Blue', bgGradient: 'from-[#07111e] via-[#0d1f35] to-[#050c17]', cardBorder: 'border-blue-400/40', mode: 'dark' },
  airforce: { id: 'airforce', name: 'Aero Slate', bgGradient: 'from-[#0c1524] via-[#162238] to-[#0a101b]', cardBorder: 'border-sky-500/40', mode: 'dark' },
  desert: { id: 'desert', name: 'Khaki & Bronze', bgGradient: 'from-[#17130c] via-[#241e14] to-[#120f0a]', cardBorder: 'border-amber-700/50', mode: 'dark' },
  parade: { id: 'parade', name: 'Parade White & Gold', bgGradient: 'from-[#fefdfa] via-[#faf6ea] to-[#f3ecd6]', cardBorder: 'border-amber-600/60', mode: 'light' },
  ceremonial: { id: 'ceremonial', name: 'Ceremonial Silver', bgGradient: 'from-[#fbfcfe] via-[#eef2f7] to-[#e2e8f0]', cardBorder: 'border-slate-400/70', mode: 'light' },
  ivory: { id: 'ivory', name: 'Ivory & Crimson', bgGradient: 'from-[#fffaf2] via-[#fbf0e0] to-[#f6e4c8]', cardBorder: 'border-red-700/50', mode: 'light' },
};

/** Starting wording (same as the reference design); every line can be changed in the Studio. */
export const DEFAULT_DESIGN: CardDesign = {
  layout: 'classic',
  modAccent: 'Emerald',
  modTheme: 'light',
  modTitle: 'IDENTITY CARD',
  modSubtitle: 'AUTHORISED PERSONNEL',
  modFooter: 'PROPERTY OF THE UNIT • IF FOUND PLEASE RETURN',
  modBackNote: 'Scan this code at any gate. If found, return to the nearest unit office.',
  modLogoFront: '',
  modLogoBack: '',
  modLogoSize: 100,
  modShowPhoto: true,
  modExtras: [],
  theme: 'army',
  govtLine: 'GOVERNMENT OF INDIA • MINISTRY OF DEFENCE',
  title: 'ARMED FORCES IDENTITY CARD',
  formLine: 'FORM IAFZ-2041 (DEFENCE SERVICES) • OFFICIAL CREDENTIAL',
  microText: 'ARMED FORCES OF THE NATION • SECURE IDENTITY FORM IAFZ-2041 • MINISTRY OF DEFENCE • OFFICIAL ARMED FORCES CREDENTIAL • NON-TRANSFERABLE • ',
  footerLeftPrefix: 'DEFENCE SERVICES • IDENTITY CARD NO:',
  footerCenter: 'PROPERTY OF THE GOVERNMENT OF INDIA',
  footerRight: 'IF FOUND RETURN TO NEAREST MILITARY / POLICE POST',
  stripeText: 'SECURE QR CREDENTIAL • ISO/IEC 7810 ID-1 (CR-80) FORMAT',
  instructionsTitle: 'INSTRUCTIONS & CONDITIONS',
  instructions: [
    'This card is the property of the Ministry of Defence, Govt. of India.',
    'Loss of this card must be immediately reported to the nearest Unit HQ or Police.',
    'Impersonation or unauthorized possession is punishable under Military Law.',
    'Must be surrendered on discharge, retirement, or transfer.',
  ],
  coName: '',
  coTitle: 'COMMANDING OFFICER / ISSUING AUTH',
  coSignature: '',
  sealLabel: 'SEAL OF UNIT',
  sealText: 'OFFICIAL',
  bottomNotice: 'ISSUED UNDER AUTHORITY OF THE ADJUTANT GENERAL • DEFENCE HEADQUARTERS',
  issuingState: 'IND',
  crest: 'national',
  crestImage: '',
  badge: 'swords',
  badgeImage: '',
  showAddress: true,
  showMobile: true,
  showChip: true,
  showMrz: true,
  size: 'cr80',
  orientation: 'portrait',
  customLong: 85.6,
  customShort: 53.98,
  showSlot: true,
  frontFields: ['unit', 'company', 'platoon', 'section'],
  backFields: ['dob', 'enrolDate', 'bloodGroup', 'expiry'],
  showIdMark: true,
  showNok: true,
  detailFontScale: 1.2,
  detailFontColor: '',
  showBackgroundArt: true,
  watermarkFront: '',
  watermarkFrontOpacity: 0.12,
  watermarkFrontSize: 55,
  watermarkBack: '',
  watermarkBackOpacity: 0.12,
  watermarkBackSize: 55,
};

/** Card sizes as long side × short side in millimetres. */
export const CARD_SIZES: Record<Exclude<SizeId, 'custom'>, { name: string; long: number; short: number }> = {
  cr80: { name: 'Standard ID card — CR-80 (85.6 × 54 mm)', long: 85.6, short: 53.98 },
  cr100: { name: 'Large ID card — CR-100 (98.5 × 67 mm)', long: 98.5, short: 67 },
  badge34: { name: 'Lanyard badge — 3 × 4 in (102 × 76 mm)', long: 101.6, short: 76.2 },
  a7: { name: 'A7 badge (105 × 74 mm)', long: 105, short: 74 },
};

/** Width × height in millimetres of the card as printed. */
export function cardSize(d: CardDesign): { w: number; h: number; portrait: boolean } {
  const clamp = (v: number) => Math.min(200, Math.max(40, Number.isFinite(v) ? v : 0));
  const preset = d.size !== 'custom' ? CARD_SIZES[d.size] ?? CARD_SIZES.cr80 : null;
  let long = preset ? preset.long : clamp(d.customLong), short = preset ? preset.short : clamp(d.customShort);
  if (short > long) [long, short] = [short, long];
  const portrait = d.orientation === 'portrait';
  return portrait ? { w: short, h: long, portrait } : { w: long, h: short, portrait };
}

/** Layout width in CSS px the card is designed at (landscape 700, portrait 440); printing scales it to millimetres. */
export const baseWidth = (portrait: boolean) => (portrait ? 440 : 700);

export const ARMY_RANKS = [
  'SEPOY', 'RIFLEMAN', 'LANCE NAIK', 'NAIK', 'HAVILDAR', 'COMPANY QUARTERMASTER HAVILDAR', 'COMPANY HAVILDAR MAJOR', 'NAIB SUBEDAR',
  'SUBEDAR', 'SUBEDAR MAJOR', 'LIEUTENANT', 'CAPTAIN', 'MAJOR', 'LIEUTENANT COLONEL', 'COLONEL', 'BRIGADIER', 'CIVILIAN',
];

export const BLOOD_GROUPS = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
