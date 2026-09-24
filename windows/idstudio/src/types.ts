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

export type ThemeId = 'army' | 'tactical' | 'navy' | 'airforce' | 'desert';

export interface CardThemeConfig {
  id: ThemeId;
  name: string;
  bgGradient: string;
  cardBorder: string;
}

/** Card layout and wording chosen by the administrator (saved on the PC, applies to every card). */
export interface CardDesign {
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
}
