/**
 * Network Sync Manager
 * Facilitates dual connectivity:
 * 1. Cloud Sync (Firebase / Cloud Server)
 * 2. Local Wi-Fi / LAN Sync (Direct connection between handheld mobile devices and Windows PC Command Center)
 */

export type NetworkSyncMode = 'cloud' | 'local_lan' | 'auto';
export type SyncTargetMode = 'CLOUD' | 'LOCAL_LAN' | 'AUTO' | 'cloud' | 'local_lan' | 'auto';

export interface LocalServerConfig {
  mode: NetworkSyncMode;
  localServerUrl: string; // e.g. "http://192.168.1.105:3000"
  fallbackToCloud: boolean;
  lastConnectedAt?: string;
  lastPingLatencyMs?: number;
}

export interface NetworkHostInfo {
  status: string;
  hostname: string;
  platform: string;
  port: number;
  localIpv4: string;
  fullLocalUrl: string;
  availableIps: string[];
  pairingCode: string;
}

const STORAGE_KEY_NETWORK_CONFIG = 'teamxv_gk_network_sync_config_v1';
const STORAGE_KEY_SYNC_TARGET = 'teamxv_gk_sync_target_mode_v1';
const STORAGE_KEY_LOCAL_SERVER_URL = 'teamxv_gk_local_server_url_v1';

export const DEFAULT_NETWORK_CONFIG: LocalServerConfig = {
  mode: 'auto',
  localServerUrl: typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:3000` : 'http://localhost:3000',
  fallbackToCloud: true,
};

/**
 * Gets currently active sync target mode ('cloud' | 'local_lan' | 'auto')
 */
export function getSyncTargetMode(): SyncTargetMode {
  if (typeof window === 'undefined' || !window.localStorage) {
    return 'CLOUD';
  }
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY_SYNC_TARGET);
    if (saved) {
      return saved as SyncTargetMode;
    }
    return 'CLOUD';
  } catch {
    return 'CLOUD';
  }
}

/**
 * Saves sync target mode
 */
export function saveSyncTargetMode(mode: SyncTargetMode): void {
  if (typeof window !== 'undefined' && window.localStorage) {
    try {
      window.localStorage.setItem(STORAGE_KEY_SYNC_TARGET, mode);
    } catch {
      // ignore
    }
  }
}

/**
 * Gets configured local PC server URL for Wi-Fi sync
 */
export function getLocalServerUrl(): string {
  if (typeof window === 'undefined' || !window.localStorage) {
    return 'http://192.168.1.100:3000';
  }
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY_LOCAL_SERVER_URL);
    if (saved && saved.trim()) {
      return saved.trim();
    }
    if (isNativeApp()) return '';
    const host = window.location.hostname;
    return `${window.location.protocol}//${host}:3000`;
  } catch {
    return 'http://192.168.1.100:3000';
  }
}

/**
 * Saves configured local PC server URL
 */
export function saveLocalServerUrl(url: string): void {
  if (typeof window !== 'undefined' && window.localStorage) {
    try {
      window.localStorage.setItem(STORAGE_KEY_LOCAL_SERVER_URL, url.trim());
    } catch {
      // ignore
    }
  }
}

/**
 * Gets saved network configuration
 */
export function getSavedNetworkConfig(): LocalServerConfig {
  if (typeof window === 'undefined' || !window.localStorage) {
    return DEFAULT_NETWORK_CONFIG;
  }
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY_NETWORK_CONFIG);
    if (!raw) return DEFAULT_NETWORK_CONFIG;
    return { ...DEFAULT_NETWORK_CONFIG, ...JSON.parse(raw) };
  } catch {
    return DEFAULT_NETWORK_CONFIG;
  }
}

/**
 * Saves network configuration
 */
export function saveNetworkConfig(cfg: Partial<LocalServerConfig>): LocalServerConfig {
  const current = getSavedNetworkConfig();
  const updated: LocalServerConfig = { ...current, ...cfg };
  if (typeof window !== 'undefined' && window.localStorage) {
    window.localStorage.setItem(STORAGE_KEY_NETWORK_CONFIG, JSON.stringify(updated));
  }
  return updated;
}

/**
 * Pings a server endpoint to verify local Wi-Fi / LAN connection and calculate latency
 */
export async function pingServerEndpoint(targetBaseUrl: string): Promise<{
  ok: boolean;
  latencyMs: number;
  data?: any;
  error?: string;
}> {
  const cleanUrl = targetBaseUrl.replace(/\/+$/, '');
  const startTime = performance.now();

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 4000); // 4-second timeout for local Wi-Fi

    const res = await fetch(`${cleanUrl}/api/sync/ping`, {
      method: 'GET',
      headers: {
        Accept: 'application/json',
      },
      signal: controller.signal,
    });

    clearTimeout(timeoutId);
    const latencyMs = Math.round(performance.now() - startTime);

    if (res.ok) {
      const data = await res.json();
      return { ok: true, latencyMs, data };
    } else {
      return { ok: false, latencyMs, error: `HTTP ${res.status}: ${res.statusText}` };
    }
  } catch (err: any) {
    const latencyMs = Math.round(performance.now() - startTime);
    return {
      ok: false,
      latencyMs,
      error: err.name === 'AbortError' ? 'Connection timed out (PC not reachable on this Wi-Fi)' : err.message,
    };
  }
}

/**
 * Fetches the PC server's local network interfaces from /api/network-info
 */
export async function fetchServerNetworkInfo(): Promise<NetworkHostInfo | null> {
  try {
    const res = await fetch(apiUrl('/api/network-info'));
    if (!res.ok) return null;
    const json = await res.json();
    return json.network;
  } catch {
    return null;
  }
}

/**
 * Parses a QR code for local Wi-Fi connection data.
 * Supports:
 * - "http://192.168.1.100:3000"
 * - "gk_lan://192.168.1.100:3000"
 * - JSON: {"type":"GK_LAN_PAIR","url":"http://192.168.1.100:3000"}
 */
export function parseLocalPairingQr(qrText: string): string | null {
  const trimmed = qrText.trim();
  if (!trimmed) return null;

  if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
    return trimmed.replace(/\/+$/, '');
  }

  if (trimmed.startsWith('gk_lan://')) {
    return `http://${trimmed.replace('gk_lan://', '')}`.replace(/\/+$/, '');
  }

  try {
    const parsed = JSON.parse(trimmed);
    if (parsed.url && typeof parsed.url === 'string') {
      return parsed.url.replace(/\/+$/, '');
    }
  } catch {
    // Not JSON
  }

  // Check if raw IP:Port like "192.168.1.50:3000"
  if (/^(\d{1,3}\.){3}\d{1,3}(:\d+)?$/.test(trimmed)) {
    return `http://${trimmed.includes(':') ? trimmed : `${trimmed}:3000`}`;
  }

  return null;
}

/** True when running inside the installed Android/iOS app (not a browser tab served by the PC) */
export function isNativeApp(): boolean {
  try {
    return typeof window !== 'undefined' && (window as any).Capacitor?.isNativePlatform?.() === true;
  } catch {
    return false;
  }
}

/**
 * Base URL for API calls. Browser served by the PC: '' (same origin).
 * Installed app: the PC address saved on the Sync screen; until one is saved
 * the address is unresolvable, so every call fails fast and the app stays offline.
 */
export function getApiBase(): string {
  if (!isNativeApp()) return '';
  try {
    const saved = window.localStorage.getItem(STORAGE_KEY_LOCAL_SERVER_URL);
    if (saved && saved.trim()) return saved.trim().replace(/\/+$/, '');
  } catch {
    // ignore
  }
  return 'http://xv-server-not-configured.invalid';
}

export function apiUrl(path: string): string {
  return `${getApiBase()}${path}`;
}