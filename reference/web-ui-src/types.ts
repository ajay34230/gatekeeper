/**
 * Core Data Models & Types for Android QR-Based Personnel & Vehicle Entry/Exit System
 */

export type EntityStatus = 'ACTIVE' | 'SUSPENDED' | 'EXPIRED' | 'FLAGGED';
export type PresenceStatus = 'OUTSIDE' | 'INSIDE';
export type ActionDirection = 'ENTRY' | 'EXIT';

export type MilitaryCompany = 'Alpha' | 'Bravo' | 'Charlie' | 'Delta' | 'SP' | 'HQ';

export interface CustomFieldCell {
  id: string;
  label: string;
  value: string;
}

export interface CustomDataTable {
  id: string;
  title: string;
  tableName?: string;
  columns: string[];
  rows: Array<Record<string, string>>;
}

export interface Personnel {
  id: string; // e.g. "P-001"
  secretCode?: string; // QR secret code e.g. "SEC-P001-ALPHA"
  serviceNumber?: string; // Military Army Service No e.g. "ARMY-849201"
  armyNumber?: string; // Alias for Army Service Number
  rank?: string; // e.g. "Major", "Captain", "Subedar", "Havildar"
  name: string;
  company?: MilitaryCompany; // One of six companies: Alpha, Bravo, Charlie, Delta, SP, HQ
  unit?: string; // e.g. "4th Logistics Support Bn"
  address?: string; // Personnel address (residential / base quarters)
  mobileNumber?: string; // Primary phone number
  altMobileNumber?: string; // Alternative contact number
  idCardNumber?: string; // Official I-Card number e.g. "IC-849201-IND"
  role: string;
  department: string;
  status: EntityStatus;
  currentStatus: PresenceStatus;
  lastEntryTime?: string;
  lastEntryTimestamp?: number;
  lastSeenTime?: string;
  lastSeenAction?: ActionDirection;
  lastSeenTimestamp?: number;
  photoUrl?: string;
  accessLocations: string[];
  customCells?: CustomFieldCell[]; // Dynamic customizable fields (e.g. Blood Group, Weapon Issued)
  customTables?: CustomDataTable[]; // Dynamic custom tables added by admin
}

export interface Vehicle {
  id: string; // e.g. "V-014"
  secretCode?: string; // QR secret code e.g. "SEC-V014-TACTICAL"
  plateNumber: string; // e.g. "BA-88B-1092"
  militaryRegNumber?: string;
  type: string; // e.g. "Tactical Supply Truck"
  model: string;
  status: EntityStatus;
  assignedCompany: string;
  unitAssigned?: string;
  currentStatus: PresenceStatus;
  lastEntryTime?: string;
  lastEntryTimestamp?: number;
  authorizedDrivers?: string[];
}

export interface ManifestPerson {
  id: string;
  name: string;
  role?: string;
  verified: boolean;
  type: 'DRIVER' | 'CO_DRIVER' | 'OCCUPANT';
}

export interface ActivityRecord {
  id: string;
  timestamp: string; // "23:41:02"
  timestampMs: number;
  type: 'PERSON' | 'VEHICLE';
  action: ActionDirection;
  targetId: string; // "P-001" or "V-014"
  title: string; // "Major Johnathan Doe" or "V-014"
  subtitle: string; // "ARMY-849201 • 4th Logistics"
  location: string;
  gate: string;
  gatekeeperId: string;
  stayDuration?: string;
  stayDurationFormatted?: string; // "0y 0m 2d 6h 15m"
  stayDurationMs?: number;
  timeSinceLastVisitFormatted?: string; // "14 days 2 hours ago"
  timeSinceLastVisitMs?: number;
  secretCode?: string;
  serviceNumber?: string;
  armyNumber?: string;
  company?: MilitaryCompany;
  idCardNumber?: string;
  rank?: string;
  unit?: string;
  regiment?: string;
  corps?: string;
  militaryRegNumber?: string;
  locationMismatch?: boolean;
  scannedLocation?: string;
  synced: boolean;
  vehicleManifest?: {
    driver: ManifestPerson;
    coDriver?: ManifestPerson;
    occupants: ManifestPerson[];
  };
}

export interface GatekeeperSession {
  id: string; // "GK-04"
  name: string;
  role: string;
  location: string; // "Location 07"
  gate: string; // "Gate 02"
  shiftStartTime: string;
  shiftStartTimestamp?: number;
  token?: string; // server sign-in token
}

export interface SyncStatusData {
  isOnline: boolean;
  lastSyncTime: string;
  pendingCount: number;
  failedCount: number;
  todayTotal: number;
}

export type ScreenId =
  | 'LOGIN'
  | 'HOME'
  | 'SCAN_PERSON'
  | 'PERSON_RESULT'
  | 'SUCCESS_PERSON'
  | 'VEHICLE_FLOW'
  | 'ACTIVITY'
  | 'SYNC'
  | 'OPERATOR'
  | 'DESIGN_SYSTEM'
  | 'PC_DASHBOARD';

export type VehicleStep =
  | 'SCAN_VEHICLE'
  | 'CHOOSE_VEHICLE_ACTION'
  | 'SCAN_DRIVER'
  | 'CO_DRIVER_CHOICE'
  | 'SCAN_CO_DRIVER'
  | 'OCCUPANTS'
  | 'SCAN_OCCUPANT'
  | 'CONFIRMATION'
  | 'SUCCESS';
