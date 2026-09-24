/**
 * Utility to parse QR code payloads and detect Location IDs & Mismatches
 */

export interface ParsedQrResult {
  rawCode: string;
  entityId: string;
  locationId?: string;
  isLocationMismatch: boolean;
}

/**
 * Normalizes location string into a canonical token for robust comparison.
 * e.g., "Location 07", "Location 7", "LOC-07", "loc_7", "Post 07" -> "LOC_7"
 */
export function normalizeLocation(loc?: string): string {
  if (!loc) return '';
  const trimmed = loc.trim().toLowerCase();

  // Check if string contains standard location/loc/post/site pattern with a number
  const numMatch = trimmed.match(/(?:location|loc|post|site|station)[\s\-_#:]*0*(\d+)/i);
  if (numMatch) {
    return `LOC_${parseInt(numMatch[1], 10)}`;
  }

  // Check for bare numbers if loc is like "07" or "7"
  if (/^0*(\d+)$/.test(trimmed)) {
    return `LOC_${parseInt(trimmed, 10)}`;
  }

  // Fallback: strip punctuation and spaces
  return trimmed.replace(/[^a-z0-9]/g, '');
}

/**
 * Compares a scanned location against the active session's location.
 * Returns true only if a location ID is present in the QR code AND differs from session location.
 */
export function checkLocationMismatch(
  scannedLocation?: string,
  sessionLocation?: string
): boolean {
  if (!scannedLocation || !sessionLocation) {
    return false;
  }
  const normScanned = normalizeLocation(scannedLocation);
  const normSession = normalizeLocation(sessionLocation);

  if (!normScanned || !normSession) {
    return false;
  }

  return normScanned !== normSession;
}

/**
 * Parses raw QR code string to extract entity identifier and any embedded location ID.
 *
 * Supported formats:
 * 1. JSON: `{"id": "P-001", "location": "Location 04"}`
 * 2. Delimited: `P-001|Location 04`, `P-001:Location 04`, `P-001@Location 04`, `P-001#LOC-04`
 * 3. Reverse Delimited: `Location 04|P-001`
 * 4. URL/Query: `https://military.base/scan?id=P-001&loc=Location 04`
 * 5. Key-Value: `ID=P-001;LOC=Location 04` or `CODE:P-001,LOCATION:Location 04`
 * 6. Standard bare ID: `P-001`, `V-014`
 */
export function parseScannedQr(rawCode: string, sessionLocation?: string): ParsedQrResult {
  const clean = (rawCode || '').trim();
  if (!clean) {
    return {
      rawCode: '',
      entityId: '',
      isLocationMismatch: false,
    };
  }

  let extractedId = clean;
  let extractedLocation: string | undefined = undefined;

  // 1. Try parsing JSON format
  if ((clean.startsWith('{') && clean.endsWith('}')) || (clean.startsWith('[') && clean.endsWith(']'))) {
    try {
      const data = JSON.parse(clean);
      if (data && typeof data === 'object') {
        extractedId =
          data.id ||
          data.code ||
          data.targetId ||
          data.badgeId ||
          data.vehicleId ||
          data.personId ||
          data.secCode ||
          data.secretCode ||
          clean;

        extractedLocation =
          data.location ||
          data.locationId ||
          data.loc ||
          data.location_id ||
          data.post ||
          data.site ||
          data.station;
      }
    } catch {
      // Not JSON, continue to other parsers
    }
  }

  // 2. Try URL / Query format
  if (!extractedLocation && (clean.includes('?') || clean.startsWith('http') || clean.startsWith('scan://'))) {
    try {
      const queryString = clean.includes('?') ? clean.split('?')[1] : clean;
      const params = new URLSearchParams(queryString);
      const urlId = params.get('id') || params.get('code') || params.get('target');
      const urlLoc = params.get('location') || params.get('locationId') || params.get('loc') || params.get('site');
      if (urlId) extractedId = urlId;
      if (urlLoc) extractedLocation = urlLoc;
    } catch {
      // Continue
    }
  }

  // 3. Try Key-Value format (e.g. ID=P-001;LOC=Location 04 or ID:P-001,LOCATION:Location 04)
  if (!extractedLocation && (/id\s*[:=]/i.test(clean) && /(?:loc|location|site)\s*[:=]/i.test(clean))) {
    const idMatch = clean.match(/(?:id|code)\s*[:=]\s*([^;,|&\s]+)/i);
    const locMatch = clean.match(/(?:loc|location|site|station)\s*[:=]\s*([^;,|&]+)/i);
    if (idMatch && idMatch[1]) extractedId = idMatch[1].trim();
    if (locMatch && locMatch[1]) extractedLocation = locMatch[1].trim();
  }

  // 4. Try Delimited formats (| , : , @ , # , ;)
  if (!extractedLocation && /[|:@#;]/.test(clean)) {
    const delimiters = ['|', '@', '#', ';', ':'];
    for (const delim of delimiters) {
      if (clean.includes(delim)) {
        const parts = clean.split(delim).map((p) => p.trim()).filter(Boolean);
        if (parts.length >= 2) {
          const partA = parts[0];
          const partB = parts[1];

          // Check if partA is entity ID and partB is location
          const isPartBLocation = /(?:location|loc|site|post|station)[\s\-_0-9]*/i.test(partB) || /^loc\d+/i.test(partB);
          const isPartALocation = /(?:location|loc|site|post|station)[\s\-_0-9]*/i.test(partA) || /^loc\d+/i.test(partA);

          if (isPartBLocation && !isPartALocation) {
            extractedId = partA;
            extractedLocation = partB;
            break;
          } else if (isPartALocation && !isPartBLocation) {
            extractedId = partB;
            extractedLocation = partA;
            break;
          } else if (parts.length === 2 && !extractedLocation) {
            // General heuristic: If one side matches standard ID like P-001, V-014, SEC-xxx
            if (/^[pv]-?\d+/i.test(partA) || /^sec-/i.test(partA)) {
              extractedId = partA;
              extractedLocation = partB;
              break;
            }
          }
        }
      }
    }
  }

  const isMismatch = checkLocationMismatch(extractedLocation, sessionLocation);

  return {
    rawCode: clean,
    entityId: extractedId.trim(),
    locationId: extractedLocation ? extractedLocation.trim() : undefined,
    isLocationMismatch: isMismatch,
  };
}
