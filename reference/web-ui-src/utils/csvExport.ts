import { ActivityRecord } from '../types';

/**
 * Escapes a string for CSV formatting according to RFC 4180 rules.
 */
function escapeCsvCell(value: string | number | boolean | null | undefined): string {
  if (value === null || value === undefined) return '';
  const stringValue = String(value);
  // If string contains quotes, commas, or newlines, wrap in quotes and escape internal quotes
  if (/[",\n\r]/.test(stringValue)) {
    return `"${stringValue.replace(/"/g, '""')}"`;
  }
  return stringValue;
}

/**
 * Converts a list of ActivityRecord items into a structured CSV string.
 */
export function generateActivityCsv(activities: ActivityRecord[]): string {
  const headers = [
    'Activity ID',
    'Timestamp',
    'Type',
    'Direction',
    'Target ID',
    'Title',
    'Subtitle / Details',
    'Gate',
    'Location',
    'Gatekeeper ID',
    'Location Mismatch',
    'Scanned QR Location',
    'Duration',
    'Sync Status',
    'Manifest Driver',
    'Manifest Co-Driver',
    'Manifest Occupants',
  ];

  const rows = activities.map((act) => {
    const driver = act.vehicleManifest?.driver
      ? `${act.vehicleManifest.driver.name} (${act.vehicleManifest.driver.id})`
      : '';
    const coDriver = act.vehicleManifest?.coDriver
      ? `${act.vehicleManifest.coDriver.name} (${act.vehicleManifest.coDriver.id})`
      : '';
    const occupants = act.vehicleManifest?.occupants?.length
      ? act.vehicleManifest.occupants.map((occ) => `${occ.name} (${occ.id})`).join('; ')
      : '';

    return [
      escapeCsvCell(act.id),
      escapeCsvCell(act.timestamp),
      escapeCsvCell(act.type),
      escapeCsvCell(act.action),
      escapeCsvCell(act.targetId),
      escapeCsvCell(act.title),
      escapeCsvCell(act.subtitle),
      escapeCsvCell(act.gate),
      escapeCsvCell(act.location),
      escapeCsvCell(act.gatekeeperId),
      escapeCsvCell(act.locationMismatch ? 'FLAGGED_MISMATCH' : 'NORMAL'),
      escapeCsvCell(act.scannedLocation || ''),
      escapeCsvCell(act.stayDuration || 'N/A'),
      escapeCsvCell(act.synced ? 'SYNCED' : 'PENDING_OFFLINE'),
      escapeCsvCell(driver),
      escapeCsvCell(coDriver),
      escapeCsvCell(occupants),
    ].join(',');
  });

  return [headers.join(','), ...rows].join('\r\n');
}

/**
 * Triggers a browser download of the CSV text file.
 */
export function downloadActivityCsv(activities: ActivityRecord[], filename?: string): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;

  const csvContent = generateActivityCsv(activities);
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const defaultFilename = `xv_digital_access_control_activity_log_${new Date().toISOString().slice(0, 10)}.csv`;
  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename || defaultFilename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Generates and triggers download of an individual personnel member's complete entry/exit history.
 */
export function downloadPersonnelHistoryCsv(
  person: { id: string; name: string; role?: string; department?: string; serviceNumber?: string; rank?: string },
  activities: ActivityRecord[]
): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;

  const personActivities = activities.filter(
    (act) =>
      act.targetId.toUpperCase() === person.id.toUpperCase() ||
      (act.vehicleManifest &&
        (act.vehicleManifest.driver?.id?.toUpperCase() === person.id.toUpperCase() ||
          act.vehicleManifest.coDriver?.id?.toUpperCase() === person.id.toUpperCase() ||
          act.vehicleManifest.occupants?.some((occ) => occ.id.toUpperCase() === person.id.toUpperCase())))
  );

  const headers = [
    'Record ID',
    'Date/Time',
    'Event Action',
    'Entity Type',
    'Personnel ID',
    'Full Name',
    'Rank / Role',
    'Military Service No',
    'Location',
    'Gate',
    'Gatekeeper ID',
    'Stay Duration',
    'Stay Duration (Detailed Y/M/D/H/M)',
    'Time Since Previous Visit',
    'Location Mismatch Flag',
    'Scanned QR Location',
    'Secret Code Payload',
    'Sync Status',
  ];

  const rows = personActivities.map((act) => [
    escapeCsvCell(act.id),
    escapeCsvCell(act.timestamp),
    escapeCsvCell(act.action),
    escapeCsvCell(act.type),
    escapeCsvCell(person.id),
    escapeCsvCell(person.name),
    escapeCsvCell(person.rank ? `${person.rank} • ${person.role}` : person.role || ''),
    escapeCsvCell(act.serviceNumber || person.serviceNumber || ''),
    escapeCsvCell(act.location),
    escapeCsvCell(act.gate),
    escapeCsvCell(act.gatekeeperId),
    escapeCsvCell(act.stayDuration || 'N/A'),
    escapeCsvCell(act.stayDurationFormatted || act.stayDuration || 'N/A'),
    escapeCsvCell(act.timeSinceLastVisitFormatted || 'N/A'),
    escapeCsvCell(act.locationMismatch ? 'FLAGGED_MISMATCH' : 'AUTHORIZED'),
    escapeCsvCell(act.scannedLocation || ''),
    escapeCsvCell(act.secretCode || ''),
    escapeCsvCell(act.synced ? 'SYNCED_CLOUD' : 'LOCAL_BUFFER'),
  ]);

  const csvContent = [headers.join(','), ...rows.map((r) => r.join(','))].join('\r\n');
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const sanitizedName = person.name.replace(/[^a-zA-Z0-9_-]/g, '_').toLowerCase();
  const dateStr = new Date().toISOString().slice(0, 10);
  const filename = `personnel_report_${person.id.toLowerCase()}_${sanitizedName}_${dateStr}.csv`;

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Exports vehicle fleet registry list as an RFC 4180 compliant CSV file
 */
export function downloadVehicleFleetCsv(vehicles: any[]): void {
  const headers = [
    'Vehicle ID',
    'Plate Number',
    'Military Reg No',
    'Type / Category',
    'Chassis Model',
    'Assigned Unit / Company',
    'Status (Yard Presence)',
    'Last Gate Entry Time',
    'Authorized Drivers Count',
    'Special Cargo Permits',
  ];

  const rows = vehicles.map((v) => [
    escapeCsvCell(v.id),
    escapeCsvCell(v.plateNumber),
    escapeCsvCell(v.militaryRegNumber || v.plateNumber),
    escapeCsvCell(v.type),
    escapeCsvCell(v.model),
    escapeCsvCell(v.assignedCompany),
    escapeCsvCell(v.currentStatus === 'INSIDE' ? 'IN_YARD' : 'DISPATCHED_OUT'),
    escapeCsvCell(v.lastEntryTime || 'None'),
    escapeCsvCell(v.authorizedDrivers ? v.authorizedDrivers.length : 0),
    escapeCsvCell(v.specialPermits ? v.specialPermits.join('; ') : 'Standard'),
  ]);

  const csvContent = [headers.join(','), ...rows.map((r) => r.join(','))].join('\r\n');
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const dateStr = new Date().toISOString().slice(0, 10);
  const filename = `vehicle_fleet_registry_${dateStr}.csv`;

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Exports personnel roster registry list as an RFC 4180 compliant CSV file
 */
export function downloadPersonnelRosterCsv(personnel: any[]): void {
  const headers = [
    'Personnel ID',
    'Full Name',
    'Rank',
    'Role / Title',
    'Company',
    'Department / Unit',
    'Status (Base Presence)',
    'Last Gate Entry Time',
    'Service Number',
    'Army Number',
    'IC / Badge ID',
  ];

  const rows = personnel.map((p) => [
    escapeCsvCell(p.id),
    escapeCsvCell(p.name),
    escapeCsvCell(p.rank || ''),
    escapeCsvCell(p.role || ''),
    escapeCsvCell(p.company || ''),
    escapeCsvCell(p.department || p.unit || ''),
    escapeCsvCell(p.currentStatus === 'INSIDE' ? 'ON_SITE_INSIDE' : 'OFF_SITE_OUTSIDE'),
    escapeCsvCell(p.lastEntryTime || 'None'),
    escapeCsvCell(p.serviceNumber || ''),
    escapeCsvCell(p.armyNumber || ''),
    escapeCsvCell(p.idCardNumber || ''),
  ]);

  const csvContent = [headers.join(','), ...rows.map((r) => r.join(','))].join('\r\n');
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const dateStr = new Date().toISOString().slice(0, 10);
  const filename = `personnel_roster_registry_${dateStr}.csv`;

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

export interface ShiftHandoverData {
  session: {
    id: string;
    name: string;
    role: string;
    location: string;
    gate: string;
    shiftStartTime: string;
  };
  entriesCount: number;
  exitsCount: number;
  flaggedCount: number;
  insidePersonnelCount: number;
  insideVehicleCount: number;
  pendingSyncCount: number;
  handoverNotes: string;
  relievingOfficer: string;
  activities: any[];
}

/**
 * Exports comprehensive Shift Handover Dossier as an RFC 4180 compliant CSV file
 */
export function downloadShiftHandoverReportCsv(data: ShiftHandoverData): void {
  const dateStr = new Date().toISOString().slice(0, 10);
  const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  const summaryLines = [
    ['--- SHIFT HANDOVER DOSSIER ---', ''],
    ['Report Generated Date', escapeCsvCell(dateStr)],
    ['Report Generated Time', escapeCsvCell(timeStr)],
    ['Garrison Post Location', escapeCsvCell(data.session.location)],
    ['Checkpoint Gate', escapeCsvCell(data.session.gate)],
    ['Off-Going Operator ID', escapeCsvCell(data.session.id)],
    ['Off-Going Operator Name', escapeCsvCell(data.session.name)],
    ['Shift Commenced Time', escapeCsvCell(data.session.shiftStartTime)],
    ['Relieving Officer Name', escapeCsvCell(data.relievingOfficer || 'Unassigned / Open')],
    ['Total In-Gate Entries', escapeCsvCell(data.entriesCount)],
    ['Total Cleared Exits', escapeCsvCell(data.exitsCount)],
    ['Location Mismatch Alerts', escapeCsvCell(data.flaggedCount)],
    ['Personnel Inside Base at Handover', escapeCsvCell(data.insidePersonnelCount)],
    ['Vehicles in Yard at Handover', escapeCsvCell(data.insideVehicleCount)],
    ['Pending Offline Sync Queue', escapeCsvCell(data.pendingSyncCount)],
    ['Security / Shift Notes', escapeCsvCell(data.handoverNotes || 'None recorded')],
    ['', ''],
    ['--- SHIFT ACTIVITY LEDGER LOG ---', ''],
    ['Activity ID', 'Timestamp', 'Type', 'Action', 'Target ID', 'Title', 'Subtitle', 'Gatekeeper', 'Sync Status'],
  ];

  const activityRows = data.activities.map((a) => [
    escapeCsvCell(a.id),
    escapeCsvCell(a.timestamp),
    escapeCsvCell(a.type),
    escapeCsvCell(a.action),
    escapeCsvCell(a.targetId),
    escapeCsvCell(a.title),
    escapeCsvCell(a.subtitle || ''),
    escapeCsvCell(a.gatekeeperId || data.session.id),
    escapeCsvCell(a.synced ? 'SYNCED' : 'PENDING'),
  ]);

  const allRows = [...summaryLines, ...activityRows];
  const csvContent = allRows.map((r) => r.join(',')).join('\r\n');

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const reportFilename = `shift_handover_${data.session.gate.replace(/\s+/g, '_')}_${dateStr}.csv`;

  const reportLink = document.createElement('a');
  reportLink.href = url;
  reportLink.setAttribute('download', reportFilename);
  document.body.appendChild(reportLink);
  reportLink.click();
  document.body.removeChild(reportLink);
  URL.revokeObjectURL(url);
}

