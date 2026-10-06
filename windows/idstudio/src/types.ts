/** A soldier as held by the Command Center's encrypted register (sent by the host, never invented here). */
export interface Soldier {
  id: string;            // Personnel ID, e.g. P001
  secret: string;        // QR secret code read by the gate terminals
  armyNo: string;
  rank: string;
  name: string;
  appointment: string;
  unit: string;
  company: string;
  platoon: string;
  section: string;
  category: string;
  status: string;
  mobile: string;
  bloodGroup: string;
  address: string;
  dob: string;
  enrolDate: string;
  expiryDate: string;
  idMark: string;
  nokName: string;
  nokRelation: string;
  nokPhone: string;
  cardSerial: string;
  photoVer: number;      // 0 = no photo; otherwise a cache-busting version
  signatureVer: number;  // 0 = no bearer signature
  signedAt: string;      // DD-MM-YYYY the bearer signature was captured
}

export type ThemeId = 'army' | 'tactical' | 'navy' | 'airforce' | 'desert' | 'parade' | 'ceremonial' | 'ivory';

/** A small "detail box" (label + one soldier field) that can be shown on the card's front and/or back grid. */
export type DetailFieldKey =
  | 'unit' | 'company' | 'platoon' | 'section' | 'appointment' | 'bloodGroup' | 'mobile' | 'expiry'
  | 'dob' | 'enrolDate' | 'cardSerial';

export interface CardThemeConfig {
  id: ThemeId;
  name: string;
  bgGradient: string;
  cardBorder: string;
  /** 'light' themes swap the card's ink/panel colours (still built with dark: variants) for readable dark text. */
  mode: 'dark' | 'light';
}

/** Card layout and wording chosen by the administrator (saved on the PC, applies to every card). */
export type CardLayout = 'classic' | 'modernSplit' | 'modernSingle' | 'simple';

export interface CardDesign {
  /** classic = the formal government card; modernSplit = details on the front, QR on the back; modernSingle = everything on one side. */
  layout: CardLayout;
  modAccent: string;
  modTheme: 'light' | 'dark';
  modTitle: string;
  modSubtitle: string;
  modFooter: string;
  modBackNote: string;
  modLogoFront: string;   // data URL, '' = none
  modLogoBack: string;    // data URL, '' = none (on the single-sided card this is the right-hand logo)
  modLogoSize: number;    // % of the standard logo height, 50-160
  modShowPhoto: boolean;
  modExtras: string[];
  theme: ThemeId;
  govtLine: string;
  title: string;
  formLine: string;
  microText: string;
  footerLeftPrefix: string;
  footerCenter: string;
  footerRight: string;
  stripeText: string;
  instructionsTitle: string;
  instructions: string[];
  coName: string;
  coTitle: string;
  coSignature: string;   // data URL of the drawn signature ('' = signature line only)
  sealLabel: string;
  sealText: string;
  bottomNotice: string;
  issuingState: string;  // 3-letter code used in the machine-readable zone
  crest: 'national' | 'custom';
  crestImage: string;    // data URL when crest = custom
  badge: 'swords' | 'custom' | 'none';
  badgeImage: string;
  showAddress: boolean;
  showMobile: boolean;
  showChip: boolean;
  showMrz: boolean;
  /** Physical card size: a preset (long × short side) or custom millimetres, printed in the chosen orientation. */
  size: SizeId;
  orientation: 'portrait' | 'landscape';
  customLong: number;
  customShort: number;
  /** Mark where to punch the lanyard / clip slot. */
  showSlot: boolean;
  /** Which small "detail boxes" appear on the front identity grid and the back record grid, and in what order.
   * Adding or removing one re-flows the rest of the grid automatically — nothing else needs to move. */
  frontFields: DetailFieldKey[];
  backFields: DetailFieldKey[];
  showIdMark: boolean;
  showNok: boolean;
  /** Multiplies the small label/value text in the detail boxes (1 = standard size). */
  detailFontScale: number;
  /** Hex colour for detail box values; '' keeps each theme's own colour. */
  detailFontColor: string;
  /** Guilloche security pattern + ghost crest + corner glow behind the card content. Off = a plain themed card. */
  showBackgroundArt: boolean;
  /** A user-supplied watermark image behind the card content, independent front and back. */
  watermarkFront: string;    // data URL, '' = none
  watermarkFrontOpacity: number; // 0–1
  watermarkFrontSize: number;    // % of card width, 15–100
  watermarkBack: string;
  watermarkBackOpacity: number;
  watermarkBackSize: number;
}

export type SizeId = 'cr80' | 'cr100' | 'badge34' | 'a7' | 'custom';
