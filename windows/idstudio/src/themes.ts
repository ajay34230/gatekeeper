import { CardDesign, CardThemeConfig, ThemeId } from './types';

export const CARD_THEMES: Record<ThemeId, CardThemeConfig> = {
  army: { id: 'army', name: 'Olive & Gold', bgGradient: 'from-[#0d160e] via-[#142316] to-[#0a120b]', cardBorder: 'border-amber-600/40' },
  tactical: { id: 'tactical', name: 'Tactical Steel', bgGradient: 'from-[#0b0e14] via-[#111722] to-[#080b10]', cardBorder: 'border-cyan-500/40' },
  navy: { id: 'navy', name: 'Deep Blue', bgGradient: 'from-[#07111e] via-[#0d1f35] to-[#050c17]', cardBorder: 'border-blue-400/40' },
  airforce: { id: 'airforce', name: 'Aero Slate', bgGradient: 'from-[#0c1524] via-[#162238] to-[#0a101b]', cardBorder: 'border-sky-500/40' },
  desert: { id: 'desert', name: 'Khaki & Bronze', bgGradient: 'from-[#17130c] via-[#241e14] to-[#120f0a]', cardBorder: 'border-amber-700/50' },
};

/** Starting wording (same as the reference design); every line can be changed in the Studio. */
export const DEFAULT_DESIGN: CardDesign = {
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
};

export const ARMY_RANKS = [
  'SEPOY', 'RIFLEMAN', 'LANCE NAIK', 'NAIK', 'HAVILDAR', 'COMPANY QUARTERMASTER HAVILDAR', 'COMPANY HAVILDAR MAJOR', 'NAIB SUBEDAR',
  'SUBEDAR', 'SUBEDAR MAJOR', 'LIEUTENANT', 'CAPTAIN', 'MAJOR', 'LIEUTENANT COLONEL', 'COLONEL', 'BRIGADIER', 'CIVILIAN',
];

export const BLOOD_GROUPS = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'];
