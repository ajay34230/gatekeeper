import { ActivityRecord, Personnel, MilitaryCompany } from '../types';

export const ALL_COMPANIES: MilitaryCompany[] = ['Alpha', 'Bravo', 'Charlie', 'Delta', 'SP', 'HQ'];

export const COMPANY_THEME: Record<MilitaryCompany, { name: string; fullTitle: string; color: string; bg: string; badgeBg: string }> = {
  Alpha: { name: 'Alpha', fullTitle: 'ALPHA COMPANY (1st Rifle Co)', color: '#B45309', bg: '#FEF3C7', badgeBg: '#F59E0B' },
  Bravo: { name: 'Bravo', fullTitle: 'BRAVO COMPANY (2nd Tactical Co)', color: '#047857', bg: '#D1FAE5', badgeBg: '#10B981' },
  Charlie: { name: 'Charlie', fullTitle: 'CHARLIE COMPANY (Signals & Cyber Co)', color: '#0369A1', bg: '#E0F2FE', badgeBg: '#0284C7' },
  Delta: { name: 'Delta', fullTitle: 'DELTA COMPANY (Armour & Heavy EME)', color: '#4338CA', bg: '#E0E7FF', badgeBg: '#6366F1' },
  SP: { name: 'SP', fullTitle: 'SUPPORT COMPANY / SP (Heavy Weapons & QRF)', color: '#B91C1C', bg: '#FEE2E2', badgeBg: '#EF4444' },
  HQ: { name: 'HQ', fullTitle: 'HEADQUARTERS COMPANY (Command & Logistics)', color: '#6D28D9', bg: '#EDE9FE', badgeBg: '#8B5CF6' },
};

/**
 * Returns a high-contrast color scheme for various base locations.
 * Ensures each checkpoint / location has its own distinct visual identity in Excel.
 */
export function getLocationExcelStyle(location: string): { bg: string; text: string; border: string } {
  const loc = (location || '').toLowerCase();

  if (loc.includes('north') || loc.includes('alpha') || loc.includes('sector a')) {
    return { bg: '#DBEAFE', text: '#1E40AF', border: '#93C5FD' }; // Soft Blue
  }
  if (loc.includes('south') || loc.includes('bravo') || loc.includes('sector b') || loc.includes('depot')) {
    return { bg: '#D1FAE5', text: '#065F46', border: '#6EE7B7' }; // Soft Emerald
  }
  if (loc.includes('east') || loc.includes('charlie') || loc.includes('sector c') || loc.includes('bay')) {
    return { bg: '#FFEDD5', text: '#9A3412', border: '#FDBA74' }; // Soft Amber / Orange
  }
  if (loc.includes('west') || loc.includes('delta') || loc.includes('sector d') || loc.includes('annex')) {
    return { bg: '#EDE9FE', text: '#5B21B6', border: '#C4B5FD' }; // Soft Purple
  }
  if (loc.includes('hq') || loc.includes('command') || loc.includes('central') || loc.includes('main')) {
    return { bg: '#CCFBF1', text: '#115E59', border: '#5EEAD4' }; // Soft Teal
  }
  if (loc.includes('armory') || loc.includes('arsenal') || loc.includes('weapons')) {
    return { bg: '#FEE2E2', text: '#991B1B', border: '#FCA5A5' }; // Soft Rose / Warning
  }
  if (loc.includes('flight') || loc.includes('hangar') || loc.includes('airstrip')) {
    return { bg: '#E0E7FF', text: '#3730A3', border: '#A5B4FC' }; // Soft Indigo
  }

  // Fallback hash-based pastel color generator for custom location names
  const colors = [
    { bg: '#E0F2FE', text: '#0369A1', border: '#7DD3FC' },
    { bg: '#FCE7F3', text: '#9D174D', border: '#F472B6' },
    { bg: '#FEF3C7', text: '#92400E', border: '#FCD34D' },
    { bg: '#F3F4F6', text: '#374151', border: '#D1D5DB' },
  ];
  let hash = 0;
  for (let i = 0; i < loc.length; i++) {
    hash = (hash + loc.charCodeAt(i)) % colors.length;
  }
  return colors[hash];
}

/**
 * Escapes XML/HTML text for Excel compatibility
 */
function escapeXml(str: any): string {
  if (str === null || str === undefined) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

/**
 * Builds an Excel-compliant HTML/XML document with full styles, military title banner,
 * personnel dossier summary card, distinct location color coding, and detailed movement rows.
 */
export function buildPersonnelExcelHtml(
  person: { id: string; name: string; role?: string; department?: string; serviceNumber?: string; rank?: string; status?: string },
  activities: ActivityRecord[]
): string {
  const personActivities = activities.filter(
    (act) =>
      act.targetId.toUpperCase() === person.id.toUpperCase() ||
      (act.vehicleManifest &&
        (act.vehicleManifest.driver?.id?.toUpperCase() === person.id.toUpperCase() ||
          act.vehicleManifest.coDriver?.id?.toUpperCase() === person.id.toUpperCase() ||
          act.vehicleManifest.occupants?.some((occ) => occ.id.toUpperCase() === person.id.toUpperCase())))
  );

  const totalVisits = personActivities.length;
  const entriesCount = personActivities.filter((a) => a.action === 'ENTRY').length;
  const exitsCount = personActivities.filter((a) => a.action === 'EXIT').length;
  const mismatchesCount = personActivities.filter((a) => a.locationMismatch).length;
  const nowStr = new Date().toLocaleString('en-US', {
    dateStyle: 'full',
    timeStyle: 'medium',
  });

  let rowsHtml = '';
  if (personActivities.length === 0) {
    rowsHtml = `
      <tr>
        <td colspan="15" style="text-align: center; padding: 24px; color: #64748B; font-style: italic; background-color: #F8FAFC;">
          No checkpoint movement records found for personnel ${escapeXml(person.name)} (${escapeXml(person.id)}).
        </td>
      </tr>
    `;
  } else {
    rowsHtml = personActivities
      .map((act, index) => {
        const zebraBg = index % 2 === 0 ? '#FFFFFF' : '#F8FAFC';
        const locStyle = getLocationExcelStyle(act.location);
        const isEntry = act.action === 'ENTRY';
        const actionBg = isEntry ? '#DCFCE7' : '#FFEDD5';
        const actionText = isEntry ? '#166534' : '#9A3412';
        const actionBorder = isEntry ? '#86EFAC' : '#FDBA74';

        const mismatchStyle = act.locationMismatch
          ? 'background-color: #FEE2E2; color: #991B1B; font-weight: bold;'
          : 'background-color: #F0FDF4; color: #15803D;';

        const syncBadgeStyle = act.synced
          ? 'background-color: #EFF6FF; color: #1D4ED8;'
          : 'background-color: #FEF3C7; color: #B45309; font-weight: bold;';

        return `
        <tr style="background-color: ${zebraBg};">
          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-family: monospace; font-size: 11px; text-align: center;">${escapeXml(act.id)}</td>
          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 12px; white-space: nowrap; font-weight: 500;">${escapeXml(act.timestamp)}</td>
          
          <!-- Event Action Badge -->
          <td style="padding: 8px; border: 1px solid #CBD5E1; text-align: center;">
            <span style="display: inline-block; padding: 4px 10px; border-radius: 4px; background-color: ${actionBg}; color: ${actionText}; border: 1px solid ${actionBorder}; font-weight: bold; font-size: 11px;">
              ${escapeXml(act.action)}
            </span>
          </td>

          <!-- Distinct Location Badge -->
          <td style="padding: 8px; border: 1px solid #CBD5E1; text-align: center;">
            <span style="display: inline-block; padding: 4px 12px; border-radius: 4px; background-color: ${locStyle.bg}; color: ${locStyle.text}; border: 1px solid ${locStyle.border}; font-weight: 600; font-size: 11px;">
              ${escapeXml(act.location)}
            </span>
          </td>

          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 12px; font-weight: 500;">${escapeXml(act.gate)}</td>
          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 11px; color: #475569; font-family: monospace;">${escapeXml(act.gatekeeperId)}</td>
          
          <!-- Stay Duration Detailed -->
          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 12px; background-color: #F1F5F9; font-weight: 600; color: #0F172A;">
            ${escapeXml(act.stayDurationFormatted || act.stayDuration || 'N/A')}
          </td>

          <!-- Time Since Previous Visit -->
          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 11px; color: #334155;">
            ${escapeXml(act.timeSinceLastVisitFormatted || 'N/A')}
          </td>

          <!-- Security Mismatch Warning -->
          <td style="padding: 8px; border: 1px solid #CBD5E1; text-align: center; ${mismatchStyle}">
            ${act.locationMismatch ? '⚠ FLAGGED MISMATCH' : '✓ AUTHORIZED'}
          </td>

          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 11px; color: #64748B;">${escapeXml(act.scannedLocation || '—')}</td>
          <td style="padding: 10px 8px; border: 1px solid #CBD5E1; font-size: 11px; font-family: monospace; color: #475569;">${escapeXml(act.secretCode || '—')}</td>

          <!-- Cloud Sync Status -->
          <td style="padding: 8px; border: 1px solid #CBD5E1; text-align: center;">
            <span style="display: inline-block; padding: 3px 8px; border-radius: 4px; ${syncBadgeStyle} font-size: 10px;">
              ${act.synced ? 'CLOUD VERIFIED' : 'LOCAL BUFFER'}
            </span>
          </td>
        </tr>
      `;
      })
      .join('');
  }

  return `<!DOCTYPE html>
<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
<head>
  <meta http-equiv="Content-Type" content="text/html; charset=UTF-8">
  <!--[if gte mso 9]>
  <xml>
    <x:ExcelWorkbook>
      <x:ExcelWorksheets>
        <x:ExcelWorksheet>
          <x:Name>Personnel Movement Report</x:Name>
          <x:WorksheetOptions>
            <x:DisplayGridlines/>
            <x:Print>
              <x:ValidPrinterInfo/>
              <x:HorizontalResolution>600</x:HorizontalResolution>
              <x:VerticalResolution>600</x:VerticalResolution>
            </x:Print>
          </x:WorksheetOptions>
        </x:ExcelWorksheet>
      </x:ExcelWorksheets>
    </x:ExcelWorkbook>
  </xml>
  <![endif]-->
  <style>
    body { font-family: 'Segoe UI', Calibri, Arial, sans-serif; color: #0F172A; }
    table { border-collapse: collapse; width: 100%; }
    th, td { vertical-align: middle; }
  </style>
</head>
<body>
  <table>
    <!-- TITLE BANNER -->
    <tr>
      <th colspan="12" style="background-color: #0F172A; color: #F8FAFC; padding: 18px 12px; font-size: 18px; font-weight: bold; text-align: left; border-bottom: 3px solid #F59E0B;">
        ⚔️ MILITARY GATE SECURITY & PERSONNEL MOVEMENT AUDIT DOSSIER
      </th>
    </tr>
    <tr>
      <td colspan="12" style="background-color: #1E293B; color: #94A3B8; padding: 6px 12px; font-size: 11px; border-bottom: 2px solid #334155;">
        REPORT GENERATED: ${escapeXml(nowStr)} &nbsp;|&nbsp; CLASSIFICATION: <strong>RESTRICTED // INTERNAL AUDIT ONLY</strong> &nbsp;|&nbsp; SYSTEM: <strong>XV DIGITAL ACCESS CONTROL TELEMETRY</strong>
      </td>
    </tr>

    <!-- SPACER -->
    <tr><td colspan="12" style="height: 12px;"></td></tr>

    <!-- PERSONNEL DOSSIER SUMMARY CARD -->
    <tr>
      <th colspan="12" style="background-color: #334155; color: #F1F5F9; padding: 8px 12px; font-size: 13px; text-align: left; font-weight: bold; border: 1px solid #475569;">
        📋 PERSONNEL BIOGRAPHICAL & CREDENTIAL DATA
      </th>
    </tr>
    <tr style="background-color: #F8FAFC;">
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569; width: 140px;">FULL NAME:</td>
      <td colspan="3" style="padding: 10px; border: 1px solid #CBD5E1; font-size: 14px; font-weight: bold; color: #0F172A;">${escapeXml(person.name)}</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569; width: 140px;">PERSONNEL ID:</td>
      <td colspan="2" style="padding: 10px; border: 1px solid #CBD5E1; font-family: monospace; font-size: 13px; font-weight: bold; color: #1E40AF;">${escapeXml(person.id)}</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569; width: 140px;">MILITARY SERVICE NO:</td>
      <td colspan="4" style="padding: 10px; border: 1px solid #CBD5E1; font-family: monospace; font-size: 13px; font-weight: bold; color: #047857;">${escapeXml(person.serviceNumber || 'N/A')}</td>
    </tr>
    <tr style="background-color: #FFFFFF;">
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">RANK / TITLE:</td>
      <td colspan="3" style="padding: 10px; border: 1px solid #CBD5E1; font-size: 13px; color: #0F172A;">${escapeXml(person.rank || 'OFFICER')} • ${escapeXml(person.role || 'Personnel')}</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">UNIT / CORPS:</td>
      <td colspan="2" style="padding: 10px; border: 1px solid #CBD5E1; font-size: 13px; color: #0F172A;">${escapeXml(person.department || 'Base Operations Command')}</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">CURRENT STATUS:</td>
      <td colspan="4" style="padding: 10px; border: 1px solid #CBD5E1;">
        <span style="display: inline-block; padding: 4px 12px; border-radius: 4px; font-weight: bold; font-size: 11px; ${person.status === 'INSIDE' ? 'background-color: #DCFCE7; color: #166534; border: 1px solid #86EFAC;' : 'background-color: #F1F5F9; color: #475569; border: 1px solid #CBD5E1;'}">
          ${person.status === 'INSIDE' ? '● CURRENTLY INSIDE BASE' : '○ OUTSIDE BASE'}
        </span>
      </td>
    </tr>
    <tr style="background-color: #F8FAFC;">
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">TOTAL AUDITED LOGS:</td>
      <td colspan="3" style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #0F172A;">${totalVisits} Recorded Events</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">TOTAL ENTRIES:</td>
      <td colspan="2" style="padding: 10px; border: 1px solid #CBD5E1; color: #166534; font-weight: bold;">${entriesCount} Check-ins</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">TOTAL EXITS:</td>
      <td colspan="2" style="padding: 10px; border: 1px solid #CBD5E1; color: #9A3412; font-weight: bold;">${exitsCount} Check-outs</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: #475569;">MISMATCH FLAGS:</td>
      <td style="padding: 10px; border: 1px solid #CBD5E1; font-weight: bold; color: ${mismatchesCount > 0 ? '#DC2626' : '#16A34A'};">
        ${mismatchesCount > 0 ? `⚠ ${mismatchesCount} Detected` : '✓ 0 None'}
      </td>
    </tr>

    <!-- SPACER -->
    <tr><td colspan="12" style="height: 16px;"></td></tr>

    <!-- MOVEMENT TABLE HEADER -->
    <tr>
      <th colspan="12" style="background-color: #0F172A; color: #F8FAFC; padding: 10px 12px; font-size: 13px; text-align: left; font-weight: bold; border: 1px solid #1E293B;">
        📊 CHRONOLOGICAL GATE ENTRY & EXIT EVENT LEDGER (LOCATION COLOR CODED)
      </th>
    </tr>
    <tr style="background-color: #1E293B; color: #FFFFFF; font-size: 11px; text-transform: uppercase;">
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: center; width: 85px;">Record ID</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 140px;">Timestamp</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: center; width: 90px;">Action</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: center; width: 150px;">Location (Color Coded)</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 110px;">Gate / Terminal</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 110px;">Gatekeeper ID</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 180px;">Stay Duration (Y/M/D/H/M)</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 150px;">Time Since Prev Visit</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: center; width: 140px;">Security Verification</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 140px;">QR Scanned Loc</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: left; width: 130px;">Security Code</th>
      <th style="padding: 10px 8px; border: 1px solid #334155; text-align: center; width: 110px;">Sync Status</th>
    </tr>

    <!-- ACTIVITY ROWS -->
    ${rowsHtml}

    <!-- FOOTER SIGN-OFF -->
    <tr><td colspan="12" style="height: 20px;"></td></tr>
    <tr>
      <td colspan="6" style="padding: 14px; border: 1px solid #CBD5E1; background-color: #F8FAFC; font-size: 11px; color: #64748B;">
        <strong>AUDITING OFFICER SIGN-OFF:</strong> ____________________________________ &nbsp;&nbsp;&nbsp; <strong>DATE:</strong> ______________
      </td>
      <td colspan="6" style="padding: 14px; border: 1px solid #CBD5E1; background-color: #F8FAFC; font-size: 11px; color: #64748B; text-align: right;">
        <strong>COMMAND APPROVAL STAMP:</strong> [ CLASSIFIED BASE RECORD // OFFICIALLY VERIFIED ]
      </td>
    </tr>
  </table>
</body>
</html>`;
}

/**
 * Generates and triggers instant browser download of the color-coded Excel spreadsheet (.xls)
 */
export function downloadPersonnelHistoryExcel(
  person: { id: string; name: string; role?: string; department?: string; serviceNumber?: string; rank?: string; status?: string },
  activities: ActivityRecord[]
): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;

  const htmlContent = buildPersonnelExcelHtml(person, activities);
  const blob = new Blob([htmlContent], { type: 'application/vnd.ms-excel;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const sanitizedName = person.name.replace(/[^a-zA-Z0-9_-]/g, '_').toLowerCase();
  const dateStr = new Date().toISOString().slice(0, 10);
  const filename = `personnel_report_${person.id.toLowerCase()}_${sanitizedName}_${dateStr}.xls`;

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Builds a true Multi-Sheet Microsoft Office Excel XML Workbook containing six tabs:
 * 1. Alpha
 * 2. Bravo
 * 3. Charlie
 * 4. Delta
 * 5. SP
 * 6. HQ
 *
 * Each tab features the respective company's personnel master ledger
 * and chronological entry/exit gate movements.
 */
export function buildSixCompanyExcelWorkbookXml(
  personnelList: Personnel[],
  activities: ActivityRecord[]
): string {
  const generatedDate = new Date().toISOString();

  const worksheetsXml = ALL_COMPANIES.map((company) => {
    const theme = COMPANY_THEME[company];
    // Filter personnel belonging to this company
    const companyPersonnel = personnelList.filter((p) => {
      if (p.company) return p.company.toLowerCase() === company.toLowerCase();
      return p.secretCode?.toUpperCase().includes(company.toUpperCase());
    });

    const companyPersonIds = new Set(companyPersonnel.map((p) => p.id.toUpperCase()));
    const companyArmyNos = new Set(
      companyPersonnel.map((p) => (p.armyNumber || p.serviceNumber || '').toUpperCase()).filter(Boolean)
    );

    // Filter activities associated with personnel in this company
    const companyActivities = activities.filter((act) => {
      if (act.company && act.company.toLowerCase() === company.toLowerCase()) return true;
      if (act.targetId && companyPersonIds.has(act.targetId.toUpperCase())) return true;
      if (act.serviceNumber && companyArmyNos.has(act.serviceNumber.toUpperCase())) return true;
      if (act.armyNumber && companyArmyNos.has(act.armyNumber.toUpperCase())) return true;
      return false;
    });

    const insideCount = companyPersonnel.filter((p) => p.currentStatus === 'INSIDE').length;
    const outsideCount = companyPersonnel.filter((p) => p.currentStatus === 'OUTSIDE').length;

    // Build Personnel Rows
    let personnelRowsXml = '';
    if (companyPersonnel.length === 0) {
      personnelRowsXml = `
        <Row>
          <Cell ss:MergeAcross="11" ss:StyleID="EmptyNotice">
            <Data ss:Type="String">No personnel currently registered in ${escapeXml(theme.fullTitle)}.</Data>
          </Cell>
        </Row>
      `;
    } else {
      personnelRowsXml = companyPersonnel
        .map((p, idx) => {
          const styleId = idx % 2 === 0 ? 'DataRowEven' : 'DataRowOdd';
          const statusStyle = p.currentStatus === 'INSIDE' ? 'StatusInside' : 'StatusOutside';
          const customStr = (p.customCells || []).map((c) => `${c.label}: ${c.value}`).join(' | ');

          return `
            <Row>
              <Cell ss:StyleID="CodeCell"><Data ss:Type="String">${escapeXml(p.armyNumber || p.serviceNumber || p.id)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(p.rank || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(p.name)}</Data></Cell>
              <Cell ss:StyleID="CompanyHighlight"><Data ss:Type="String">${escapeXml(company)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(p.unit || 'Base Support')}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(p.address || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(p.mobileNumber || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(p.altMobileNumber || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="CodeCell"><Data ss:Type="String">${escapeXml(p.idCardNumber || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="${statusStyle}"><Data ss:Type="String">${escapeXml(p.currentStatus)}</Data></Cell>
              <Cell ss:StyleID="SecretKeyCell"><Data ss:Type="String">${escapeXml(p.secretCode || p.id)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(customStr || 'None')}</Data></Cell>
            </Row>
          `;
        })
        .join('\n');
    }

    // Build Activity Movement Rows
    let activityRowsXml = '';
    if (companyActivities.length === 0) {
      activityRowsXml = `
        <Row>
          <Cell ss:MergeAcross="11" ss:StyleID="EmptyNotice">
            <Data ss:Type="String">No movement logs recorded yet for ${escapeXml(company)} Company personnel.</Data>
          </Cell>
        </Row>
      `;
    } else {
      activityRowsXml = companyActivities
        .map((act, idx) => {
          const styleId = idx % 2 === 0 ? 'DataRowEven' : 'DataRowOdd';
          const actionStyle = act.action === 'ENTRY' ? 'ActionEntry' : 'ActionExit';
          return `
            <Row>
              <Cell ss:StyleID="CodeCell"><Data ss:Type="String">${escapeXml(act.id)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.timestamp)}</Data></Cell>
              <Cell ss:StyleID="${actionStyle}"><Data ss:Type="String">${escapeXml(act.action)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.title)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.rank || 'Soldier')}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.serviceNumber || act.armyNumber || act.targetId)}</Data></Cell>
              <Cell ss:StyleID="LocationCell"><Data ss:Type="String">${escapeXml(act.location)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.gate)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.gatekeeperId)}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.stayDurationFormatted || act.stayDuration || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="${styleId}"><Data ss:Type="String">${escapeXml(act.timeSinceLastVisitFormatted || 'N/A')}</Data></Cell>
              <Cell ss:StyleID="SecretKeyCell"><Data ss:Type="String">${escapeXml(act.secretCode || act.targetId)}</Data></Cell>
            </Row>
          `;
        })
        .join('\n');
    }

    return `
  <Worksheet ss:Name="${escapeXml(company)}">
    <Table ss:DefaultRowHeight="20">
      <Column ss:Width="110"/>
      <Column ss:Width="80"/>
      <Column ss:Width="150"/>
      <Column ss:Width="75"/>
      <Column ss:Width="160"/>
      <Column ss:Width="180"/>
      <Column ss:Width="110"/>
      <Column ss:Width="110"/>
      <Column ss:Width="120"/>
      <Column ss:Width="90"/>
      <Column ss:Width="150"/>
      <Column ss:Width="220"/>

      <!-- Company Banner Header -->
      <Row ss:Height="30">
        <Cell ss:MergeAcross="11" ss:StyleID="MainTitle">
          <Data ss:Type="String">⚔️ ${escapeXml(theme.fullTitle.toUpperCase())} - MILITARY GATE ACCESS LEDGER</Data>
        </Cell>
      </Row>
      <Row ss:Height="22">
        <Cell ss:MergeAcross="11" ss:StyleID="Subtitle">
          <Data ss:Type="String">Classification: SECRET // BASE DEFENSE PERIMETER SYSTEM // GENERATED: ${escapeXml(new Date().toLocaleString())}</Data>
        </Cell>
      </Row>

      <!-- KPI Summary Cards -->
      <Row ss:Height="24">
        <Cell ss:MergeAcross="2" ss:StyleID="KpiCard"><Data ss:Type="String">Total Personnel: ${companyPersonnel.length}</Data></Cell>
        <Cell ss:MergeAcross="2" ss:StyleID="KpiInside"><Data ss:Type="String">Currently Inside Base: ${insideCount}</Data></Cell>
        <Cell ss:MergeAcross="2" ss:StyleID="KpiOutside"><Data ss:Type="String">Currently Outside: ${outsideCount}</Data></Cell>
        <Cell ss:MergeAcross="2" ss:StyleID="KpiCard"><Data ss:Type="String">Recorded Gate Events: ${companyActivities.length}</Data></Cell>
      </Row>
      <Row ss:Height="12"><Cell ss:MergeAcross="11"/></Row>

      <!-- Section 1: Personnel Master Ledger -->
      <Row ss:Height="24">
        <Cell ss:MergeAcross="11" ss:StyleID="SectionHeader">
          <Data ss:Type="String">📋 SECTION 1: ${escapeXml(company.toUpperCase())} COMPANY PERSONNEL ROSTER</Data>
        </Cell>
      </Row>
      <Row ss:Height="22">
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Army No</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Rank</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Full Name</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Company</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Unit</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Address</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Mobile No</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Alt Mobile</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">I-Card No</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Status</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">QR Secret Key</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Custom Cells / Attributes</Data></Cell>
      </Row>
      ${personnelRowsXml}

      <Row ss:Height="16"><Cell ss:MergeAcross="11"/></Row>

      <!-- Section 2: Gate Entry/Exit Events -->
      <Row ss:Height="24">
        <Cell ss:MergeAcross="11" ss:StyleID="SectionHeader">
          <Data ss:Type="String">🛡️ SECTION 2: ${escapeXml(company.toUpperCase())} COMPANY GATE ENTRY &amp; EXIT LOGS</Data>
        </Cell>
      </Row>
      <Row ss:Height="22">
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Record ID</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Timestamp</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Action</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Soldier Name</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Rank</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Army No</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Location</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Gate</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Gatekeeper</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Stay Duration</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Prev Visit Interval</Data></Cell>
        <Cell ss:StyleID="TableHeader"><Data ss:Type="String">Scanned QR Code</Data></Cell>
      </Row>
      ${activityRowsXml}
    </Table>
  </Worksheet>
    `;
  });

  return `<?xml version="1.0"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:o="urn:schemas-microsoft-com:office:office"
 xmlns:x="urn:schemas-microsoft-com:office:excel"
 xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet"
 xmlns:html="http://www.w3.org/TR/REC-html40">
 <DocumentProperties xmlns="urn:schemas-microsoft-com:office:office">
  <Title>Military Company XV DIGITAL ACCESS CONTROL Ledger</Title>
  <Author>Base Defense Command</Author>
  <Created>${escapeXml(generatedDate)}</Created>
  <Company>Armed Forces Defense Network</Company>
 </DocumentProperties>
 <Styles>
  <Style ss:ID="Default" ss:Name="Normal">
   <Alignment ss:Vertical="Center"/>
   <Borders/>
   <Font ss:FontName="Calibri" x:Family="Swiss" ss:Size="11" ss:Color="#1E293B"/>
   <Interior/>
   <NumberFormat/>
   <Protection/>
  </Style>
  <Style ss:ID="MainTitle">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="14" ss:Color="#FFFFFF" ss:Bold="1"/>
   <Interior ss:Color="#0F172A" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="Subtitle">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#94A3B8" ss:Italic="1"/>
   <Interior ss:Color="#1E293B" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="KpiCard">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="11" ss:Color="#0F172A" ss:Bold="1"/>
   <Interior ss:Color="#F1F5F9" ss:Pattern="Solid"/>
   <Borders>
    <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#CBD5E1"/>
    <Border ss:Position="Top" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#CBD5E1"/>
   </Borders>
  </Style>
  <Style ss:ID="KpiInside">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="11" ss:Color="#065F46" ss:Bold="1"/>
   <Interior ss:Color="#D1FAE5" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="KpiOutside">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="11" ss:Color="#9A3412" ss:Bold="1"/>
   <Interior ss:Color="#FFEDD5" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="SectionHeader">
   <Alignment ss:Horizontal="Left" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="12" ss:Color="#FFFFFF" ss:Bold="1"/>
   <Interior ss:Color="#334155" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="TableHeader">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#FFFFFF" ss:Bold="1"/>
   <Interior ss:Color="#475569" ss:Pattern="Solid"/>
   <Borders>
    <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#334155"/>
    <Border ss:Position="Right" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#334155"/>
   </Borders>
  </Style>
  <Style ss:ID="DataRowEven">
   <Interior ss:Color="#FFFFFF" ss:Pattern="Solid"/>
   <Borders>
    <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#E2E8F0"/>
   </Borders>
  </Style>
  <Style ss:ID="DataRowOdd">
   <Interior ss:Color="#F8FAFC" ss:Pattern="Solid"/>
   <Borders>
    <Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1" ss:Color="#E2E8F0"/>
   </Borders>
  </Style>
  <Style ss:ID="CodeCell">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Consolas" ss:Size="10" ss:Color="#0F172A" ss:Bold="1"/>
   <Interior ss:Color="#F1F5F9" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="SecretKeyCell">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Consolas" ss:Size="9" ss:Color="#B45309" ss:Bold="1"/>
   <Interior ss:Color="#FEF3C7" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="CompanyHighlight">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#1E293B" ss:Bold="1"/>
   <Interior ss:Color="#E2E8F0" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="StatusInside">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#065F46" ss:Bold="1"/>
   <Interior ss:Color="#DCFCE7" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="StatusOutside">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#64748B" ss:Bold="1"/>
   <Interior ss:Color="#F1F5F9" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="ActionEntry">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#047857" ss:Bold="1"/>
   <Interior ss:Color="#D1FAE5" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="ActionExit">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#B45309" ss:Bold="1"/>
   <Interior ss:Color="#FEF3C7" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="LocationCell">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="10" ss:Color="#1E40AF" ss:Bold="1"/>
   <Interior ss:Color="#DBEAFE" ss:Pattern="Solid"/>
  </Style>
  <Style ss:ID="EmptyNotice">
   <Alignment ss:Horizontal="Center" ss:Vertical="Center"/>
   <Font ss:FontName="Calibri" ss:Size="11" ss:Color="#64748B" ss:Italic="1"/>
   <Interior ss:Color="#F8FAFC" ss:Pattern="Solid"/>
  </Style>
 </Styles>
${worksheetsXml.join('\n')}
</Workbook>`;
}

/**
 * Triggers instant browser download of the multi-sheet (6 Company tabs) Excel Workbook
 */
export function downloadSixCompanyExcel(
  personnelList: Personnel[],
  activities: ActivityRecord[]
): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;

  const xmlContent = buildSixCompanyExcelWorkbookXml(personnelList, activities);
  const blob = new Blob([xmlContent], { type: 'application/vnd.ms-excel;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const dateStr = new Date().toISOString().slice(0, 10);
  const filename = `company_ledger_6_tabs_${dateStr}.xls`;

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Generates Full Personnel CSV String with all personal fields & custom cells
 */
export function exportPersonnelFullCsv(personnelList: Personnel[]): string {
  const headers = [
    'Army No',
    'Rank',
    'Name',
    'Company',
    'Unit',
    'Address',
    'Mobile No',
    'Alt Mobile No',
    'I-Card No',
    'Secret Code',
    'Role',
    'Department',
    'Status',
    'Current Presence',
    'Custom Attributes',
  ];

  const escapeCsv = (val: any) => {
    if (val === null || val === undefined) return '';
    const str = String(val);
    return /[",\n\r]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str;
  };

  const rows = personnelList.map((p) => {
    const customStr = (p.customCells || []).map((c) => `${c.label}:${c.value}`).join(';');
    return [
      escapeCsv(p.armyNumber || p.serviceNumber || p.id),
      escapeCsv(p.rank || ''),
      escapeCsv(p.name),
      escapeCsv(p.company || 'Alpha'),
      escapeCsv(p.unit || ''),
      escapeCsv(p.address || ''),
      escapeCsv(p.mobileNumber || ''),
      escapeCsv(p.altMobileNumber || ''),
      escapeCsv(p.idCardNumber || ''),
      escapeCsv(p.secretCode || p.id),
      escapeCsv(p.role || ''),
      escapeCsv(p.department || ''),
      escapeCsv(p.status || 'ACTIVE'),
      escapeCsv(p.currentStatus || 'OUTSIDE'),
      escapeCsv(customStr),
    ].join(',');
  });

  return [headers.join(','), ...rows].join('\r\n');
}

/**
 * Downloads Personnel Master Roster CSV
 */
export function downloadPersonnelFullCsv(personnelList: Personnel[]): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;

  const csv = exportPersonnelFullCsv(personnelList);
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const dateStr = new Date().toISOString().slice(0, 10);
  const filename = `personnel_master_roster_${dateStr}.csv`;

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', filename);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Generates ready-to-use sample CSV template for import
 */
export function generatePersonnelSampleTemplateCsv(): string {
  const headers = [
    'Army No',
    'Rank',
    'Name',
    'Company',
    'Unit',
    'Address',
    'Mobile No',
    'Alt Mobile No',
    'I-Card No',
    'Secret Code',
    'Role',
    'Department',
    'Status',
    'Blood Group',
  ];

  const sampleRows = [
    'ARMY-849201,Major,Johnathan Doe,Alpha,4th Logistics Support Bn,"Qtr 14-B, Base Alpha",+91 98102 34567,+91 98102 34568,IC-849201-IND,SEC-P001-ALPHA,Logistics Lead,Supply Chain,ACTIVE,O+ Positive',
    'ARMY-773194,Captain,Jane Smith,Bravo,Special Tactical Escort Bn,"Bldg 07, Suite 3",+91 98234 56789,+91 98234 56790,IC-773194-IND,SEC-P002-BRAVO,Tactical Escort,Security,ACTIVE,A+ Positive',
    'ARMY-619024,Lieutenant,Michael Chang,Charlie,Signal Intelligence Div,"Signals Qtr 42",+91 98345 67890,+91 98345 67891,IC-619024-IND,SEC-P003-CHARLIE,Signal Specialist,Field Comms,ACTIVE,B+ Positive',
    'ARMY-920412,Subedar Major,Sarah Jenkins,Delta,Armour Inspection Div,"EME Barracks 10",+91 98456 78901,+91 98456 78902,IC-920412-IND,SEC-P004-DELTA,QA Inspector,Operations,ACTIVE,AB+ Positive',
    'ARMY-302198,Havildar,Robert Vance,SP,Perimeter Defense Co,"Support Lines Block 04",+91 98567 89012,+91 98567 89013,IC-302198-IND,SEC-P027-ECHO,Support Platoon,Facility Services,ACTIVE,O- Negative',
    'ARMY-109283,Naik,Viktor Reznov,HQ,Military Works Services,"Depot Barracks 9",+91 98678 90123,+91 98678 90124,IC-109283-IND,SEC-P099-ZULU,Works Specialist,Civil Works,ACTIVE,A- Negative',
  ];

  return [headers.join(','), ...sampleRows].join('\r\n');
}

/**
 * Downloads a sample template CSV
 */
export function downloadPersonnelSampleTemplate(): void {
  if (typeof window === 'undefined' || typeof document === 'undefined') return;

  const template = generatePersonnelSampleTemplateCsv();
  const blob = new Blob([template], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);

  const link = document.createElement('a');
  link.href = url;
  link.setAttribute('download', 'personnel_import_sample_template.csv');
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Flexible Parser that accepts CSV, Excel XML, or JSON text
 * Extracts personnel rows and normalizes them into Partial<Personnel> objects.
 */
export function parsePersonnelImportContent(rawContent: string): Partial<Personnel>[] {
  const clean = rawContent.trim();
  if (!clean) return [];

  // 1. JSON Array format
  if (clean.startsWith('[') && clean.endsWith(']')) {
    try {
      const parsed = JSON.parse(clean);
      if (Array.isArray(parsed)) {
        return parsed.map((item) => normalizeImportedRow(item));
      }
    } catch {
      // Continue to CSV
    }
  }

  // 2. CSV / Delimited parser
  const lines = clean.split(/\r?\n/).filter((l) => l.trim().length > 0);
  if (lines.length < 2) return [];

  const parseCsvLine = (line: string): string[] => {
    const result: string[] = [];
    let current = '';
    let inQuotes = false;
    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      if (char === '"') {
        if (inQuotes && line[i + 1] === '"') {
          current += '"';
          i++;
        } else {
          inQuotes = !inQuotes;
        }
      } else if (char === ',' && !inQuotes) {
        result.push(current.trim());
        current = '';
      } else {
        current += char;
      }
    }
    result.push(current.trim());
    return result;
  };

  const headerLine = lines[0];
  const headers = parseCsvLine(headerLine).map((h) => h.toLowerCase().replace(/[^a-z0-9]/g, ''));

  const findIdx = (...names: string[]): number => {
    return headers.findIndex((h) => names.some((n) => h.includes(n)));
  };

  const armyNoIdx = findIdx('armyno', 'servicenumber', 'serviceno', 'army', 'id');
  const rankIdx = findIdx('rank');
  const nameIdx = findIdx('name', 'fullname', 'soldier');
  const companyIdx = findIdx('company', 'coy');
  const unitIdx = findIdx('unit', 'regiment', 'battalion');
  const addressIdx = findIdx('address', 'residence', 'qtr');
  const mobileIdx = findIdx('mobile', 'phone', 'contact');
  const altMobileIdx = findIdx('altmobile', 'altphone', 'alternative', 'secondary');
  const idCardIdx = findIdx('icard', 'cardno', 'identity', 'badge');
  const secretIdx = findIdx('secret', 'qrcode', 'key', 'code');
  const roleIdx = findIdx('role', 'designation', 'trade');
  const deptIdx = findIdx('dept', 'department', 'branch');
  const statusIdx = findIdx('status');
  const bloodIdx = findIdx('blood', 'bloodgroup');

  const rows: Partial<Personnel>[] = [];

  for (let i = 1; i < lines.length; i++) {
    const cols = parseCsvLine(lines[i]);
    if (cols.length === 0 || cols.every((c) => !c)) continue;

    const armyNo = armyNoIdx >= 0 ? cols[armyNoIdx] : '';
    const name = nameIdx >= 0 ? cols[nameIdx] : '';
    if (!armyNo && !name) continue;

    let companyRaw = companyIdx >= 0 ? cols[companyIdx] : 'Alpha';
    let company: MilitaryCompany = 'Alpha';
    const compUpper = companyRaw.toUpperCase();
    if (compUpper.includes('BRAVO')) company = 'Bravo';
    else if (compUpper.includes('CHARLIE')) company = 'Charlie';
    else if (compUpper.includes('DELTA')) company = 'Delta';
    else if (compUpper.includes('SP') || compUpper.includes('SUPPORT')) company = 'SP';
    else if (compUpper.includes('HQ') || compUpper.includes('HEADQUARTER')) company = 'HQ';
    else if (compUpper.includes('ALPHA')) company = 'Alpha';

    const customCells: { id: string; label: string; value: string }[] = [];
    if (bloodIdx >= 0 && cols[bloodIdx]) {
      customCells.push({ id: 'blood', label: 'Blood Group', value: cols[bloodIdx] });
    }

    rows.push({
      armyNumber: armyNo || undefined,
      serviceNumber: armyNo || undefined,
      rank: rankIdx >= 0 ? cols[rankIdx] : 'Sepoy',
      name: name || 'Soldier',
      company,
      unit: unitIdx >= 0 ? cols[unitIdx] : undefined,
      address: addressIdx >= 0 ? cols[addressIdx] : undefined,
      mobileNumber: mobileIdx >= 0 ? cols[mobileIdx] : undefined,
      altMobileNumber: altMobileIdx >= 0 ? cols[altMobileIdx] : undefined,
      idCardNumber: idCardIdx >= 0 ? cols[idCardIdx] : undefined,
      secretCode: secretIdx >= 0 && cols[secretIdx] ? cols[secretIdx] : undefined,
      role: roleIdx >= 0 ? cols[roleIdx] : 'Duty Personnel',
      department: deptIdx >= 0 ? cols[deptIdx] : 'Infantry Support',
      status: (statusIdx >= 0 && cols[statusIdx]?.toUpperCase() === 'FLAGGED' ? 'FLAGGED' : 'ACTIVE') as any,
      customCells: customCells.length > 0 ? customCells : undefined,
    });
  }

  return rows;
}

function normalizeImportedRow(item: any): Partial<Personnel> {
  const armyNo = item.armyNumber || item.serviceNumber || item.ArmyNo || item.id || '';
  let company: MilitaryCompany = 'Alpha';
  const c = String(item.company || '').toUpperCase();
  if (c.includes('BRAVO')) company = 'Bravo';
  else if (c.includes('CHARLIE')) company = 'Charlie';
  else if (c.includes('DELTA')) company = 'Delta';
  else if (c.includes('SP') || c.includes('SUPPORT')) company = 'SP';
  else if (c.includes('HQ')) company = 'HQ';

  return {
    armyNumber: armyNo,
    serviceNumber: armyNo,
    rank: item.rank || 'Sepoy',
    name: item.name || item.fullName || 'Soldier',
    company,
    unit: item.unit,
    address: item.address,
    mobileNumber: item.mobileNumber || item.mobile,
    altMobileNumber: item.altMobileNumber || item.altMobile,
    idCardNumber: item.idCardNumber || item.idCard,
    secretCode: item.secretCode || item.secret,
    role: item.role || 'Duty Personnel',
    department: item.department || 'Infantry Support',
    status: item.status || 'ACTIVE',
    customCells: item.customCells || [],
  };
}

export interface DeDuplicationResult {
  mergedList: Personnel[];
  addedCount: number;
  updatedCount: number;
  duplicatesPrevented: number;
  summaryLogs: string[];
}

/**
 * Merges imported personnel with existing personnel list without duplicating records.
 * Matches by:
 * 1. Army No / Service Number
 * 2. I-Card Number
 * 3. Exact Personnel ID
 * 4. Full Name + Company combination
 */
export function deDuplicateAndMergePersonnel(
  existingList: Personnel[],
  importedRows: Partial<Personnel>[]
): DeDuplicationResult {
  const result: Personnel[] = [...existingList];
  let addedCount = 0;
  let updatedCount = 0;
  let duplicatesPrevented = 0;
  const summaryLogs: string[] = [];

  for (const imported of importedRows) {
    const importArmyNo = (imported.armyNumber || imported.serviceNumber || '').trim().toUpperCase();
    const importIdCard = (imported.idCardNumber || '').trim().toUpperCase();
    const importName = (imported.name || '').trim().toLowerCase();
    const importCompany = imported.company || 'Alpha';

    // Find if already exists
    const existingIndex = result.findIndex((p) => {
      const pArmyNo = (p.armyNumber || p.serviceNumber || '').trim().toUpperCase();
      const pIdCard = (p.idCardNumber || '').trim().toUpperCase();
      const pId = p.id.trim().toUpperCase();
      const pName = p.name.trim().toLowerCase();

      if (importArmyNo && (pArmyNo === importArmyNo || pId === importArmyNo)) return true;
      if (importIdCard && pIdCard === importIdCard) return true;
      if (imported.id && pId === imported.id.trim().toUpperCase()) return true;
      if (importName && pName === importName && p.company === importCompany) return true;
      return false;
    });

    if (existingIndex >= 0) {
      // De-duplication match found! Update existing record with new details instead of adding a duplicate
      const existing = result[existingIndex];
      const mergedCustom = [...(existing.customCells || [])];

      if (imported.customCells) {
        for (const cell of imported.customCells) {
          const cIdx = mergedCustom.findIndex((c) => c.label.toLowerCase() === cell.label.toLowerCase());
          if (cIdx >= 0) {
            mergedCustom[cIdx] = cell;
          } else {
            mergedCustom.push(cell);
          }
        }
      }

      result[existingIndex] = {
        ...existing,
        armyNumber: imported.armyNumber || existing.armyNumber,
        serviceNumber: imported.serviceNumber || existing.serviceNumber,
        rank: imported.rank || existing.rank,
        name: imported.name || existing.name,
        company: imported.company || existing.company,
        unit: imported.unit || existing.unit,
        address: imported.address || existing.address,
        mobileNumber: imported.mobileNumber || existing.mobileNumber,
        altMobileNumber: imported.altMobileNumber || existing.altMobileNumber,
        idCardNumber: imported.idCardNumber || existing.idCardNumber,
        secretCode: imported.secretCode || existing.secretCode,
        role: imported.role || existing.role,
        department: imported.department || existing.department,
        status: imported.status || existing.status,
        customCells: mergedCustom,
      };

      updatedCount++;
      duplicatesPrevented++;
      summaryLogs.push(`Updated existing: ${result[existingIndex].name} (${result[existingIndex].armyNumber || result[existingIndex].id})`);
    } else {
      // New record! Generate unique ID and Secret Code
      const nextNum = result.length + addedCount + 1;
      const newId = imported.id || `P-${String(nextNum).padStart(3, '0')}`;
      const armyNo = imported.armyNumber || imported.serviceNumber || `ARMY-${850000 + nextNum}`;
      const comp = imported.company || 'Alpha';
      const secretCode =
        imported.secretCode || `SEC-${newId.replace('-', '')}-${comp.toUpperCase()}`;

      const newPerson: Personnel = {
        id: newId,
        armyNumber: armyNo,
        serviceNumber: armyNo,
        rank: imported.rank || 'Sepoy',
        name: imported.name || `Soldier ${newId}`,
        company: comp,
        unit: imported.unit || 'Base Support Bn',
        address: imported.address || 'Garrison Quarters',
        mobileNumber: imported.mobileNumber || '+91 98000 00000',
        altMobileNumber: imported.altMobileNumber || '+91 98000 00001',
        idCardNumber: imported.idCardNumber || `IC-${850000 + nextNum}-IND`,
        secretCode,
        role: imported.role || 'Infantry Member',
        department: imported.department || 'Active Service',
        status: imported.status || 'ACTIVE',
        currentStatus: 'OUTSIDE',
        photoUrl:
          imported.photoUrl ||
          `https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=240&auto=format&fit=crop&q=80`,
        accessLocations: imported.accessLocations || ['Location 07'],
        customCells: imported.customCells || [],
      };

      result.push(newPerson);
      addedCount++;
      summaryLogs.push(`Added new soldier: ${newPerson.name} (${newPerson.armyNumber}) to ${comp} Company`);
    }
  }

  return {
    mergedList: result,
    addedCount,
    updatedCount,
    duplicatesPrevented,
    summaryLogs,
  };
}
