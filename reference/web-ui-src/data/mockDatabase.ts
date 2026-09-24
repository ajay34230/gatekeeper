import { Personnel, Vehicle, ActivityRecord } from '../types';
import { parseScannedQr } from '../utils/qrLocationParser';
import { calculatePreciseBreakdown } from '../utils/durationCalculator';
import { loadStoredPersonnel, loadStoredVehicles } from '../utils/offlineStorage';

// Clean initial empty datasets - no fake or mock records
export const INITIAL_PERSONNEL: Personnel[] = [];

export const INITIAL_VEHICLES: Vehicle[] = [];

export const INITIAL_ACTIVITIES: ActivityRecord[] = [];

export function findPersonnelById(id: string): Personnel | null {
  if (!id) return null;
  const parsed = parseScannedQr(id);
  let cleanId = (parsed.entityId || id).trim().toUpperCase();
  if (/^P\d+$/i.test(cleanId)) {
    cleanId = cleanId.replace(/^P(\d+)$/i, (_, digits) => `P-${digits.padStart(3, '0')}`);
  } else if (/^P-\d+$/i.test(cleanId)) {
    cleanId = cleanId.replace(/^P-(\d+)$/i, (_, digits) => `P-${digits.padStart(3, '0')}`);
  }

  const found = loadStoredPersonnel().find(
    (p) =>
      p.id.toUpperCase() === cleanId ||
      p.id.toUpperCase().replace('-', '') === cleanId.replace('-', '') ||
      (p.secretCode && p.secretCode.toUpperCase() === cleanId) ||
      (p.serviceNumber && p.serviceNumber.toUpperCase() === cleanId) ||
      (p.armyNumber && p.armyNumber.toUpperCase() === cleanId) ||
      (p.idCardNumber && p.idCardNumber.toUpperCase() === cleanId)
  );
  return found || null;
}

export function findVehicleById(id: string): Vehicle | null {
  if (!id) return null;
  const parsed = parseScannedQr(id);
  let cleanId = (parsed.entityId || id).trim().toUpperCase();
  if (/^V\d+$/i.test(cleanId)) {
    cleanId = cleanId.replace(/^V(\d+)$/i, (_, digits) => `V-${digits.padStart(3, '0')}`);
  } else if (/^V-\d+$/i.test(cleanId)) {
    cleanId = cleanId.replace(/^V-(\d+)$/i, (_, digits) => `V-${digits.padStart(3, '0')}`);
  }

  const found = loadStoredVehicles().find(
    (v) =>
      v.id.toUpperCase() === cleanId ||
      v.id.toUpperCase().replace('-', '') === cleanId.replace('-', '') ||
      (v.secretCode && v.secretCode.toUpperCase() === cleanId) ||
      (v.militaryRegNumber && v.militaryRegNumber.toUpperCase() === cleanId) ||
      v.plateNumber.toUpperCase() === cleanId ||
      v.plateNumber.toUpperCase().replace(/[-\s]/g, '') === cleanId.replace(/[-\s]/g, '')
  );
  return found || null;
}

export function calculateDuration(startTimestamp?: number): string {
  if (!startTimestamp) return '0m';
  const breakdown = calculatePreciseBreakdown(startTimestamp, Date.now());
  return breakdown.formatted || '0m';
}

export function formatCurrentTime(): string {
  const now = new Date();
  return now.toTimeString().split(' ')[0]; // "HH:MM:SS"
}

export function formatShortTime(): string {
  const now = new Date();
  const hours = String(now.getHours()).padStart(2, '0');
  const minutes = String(now.getMinutes()).padStart(2, '0');
  return `${hours}:${minutes}`;
}
