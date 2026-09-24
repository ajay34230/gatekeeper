import { ActivityRecord, Personnel, Vehicle, SyncStatusData, GatekeeperSession } from '../types';


const STORAGE_KEY_ACTIVITIES = 'teamxv_gk_activities_v2';
const STORAGE_KEY_PENDING_BUFFER = 'teamxv_gk_offline_buffer_v2';
const STORAGE_KEY_PERSONNEL = 'teamxv_gk_personnel_state_v2';
const STORAGE_KEY_VEHICLES = 'teamxv_gk_vehicles_state_v2';
const STORAGE_KEY_SYNC_STATE = 'teamxv_gk_sync_state_v2';

/**
 * Safely reads parsed JSON from localStorage with fallback
 */
function safeGetItem<T>(key: string, fallback: T): T {
  try {
    if (typeof window === 'undefined' || !window.localStorage) {
      return fallback;
    }
    const item = window.localStorage.getItem(key);
    if (!item) return fallback;
    return JSON.parse(item) as T;
  } catch (error) {
    console.warn(`[OfflineStorage] Error reading localStorage key "${key}":`, error);
    return fallback;
  }
}

/**
 * Safely writes JSON to localStorage
 */
function safeSetItem<T>(key: string, data: T): void {
  try {
    if (typeof window === 'undefined' || !window.localStorage) {
      return;
    }
    window.localStorage.setItem(key, JSON.stringify(data));
  } catch (error) {
    console.warn(`[OfflineStorage] Error writing localStorage key "${key}":`, error);
  }
}

/**
 * Loads all activity records from localStorage or initial defaults
 */
export function loadStoredActivities(): ActivityRecord[] {
  return safeGetItem<ActivityRecord[]>(STORAGE_KEY_ACTIVITIES, []);
}

/**
 * Persists activities list to localStorage
 */
export function persistActivities(activities: ActivityRecord[]): void {
  safeSetItem(STORAGE_KEY_ACTIVITIES, activities);
  
  // Also synchronize the dedicated offline buffer key (unsynced items)
  const pending = activities.filter((a) => !a.synced);
  safeSetItem(STORAGE_KEY_PENDING_BUFFER, pending);
}

/**
 * Retrieves only records queued during offline periods that need syncing
 */
export function getPendingOfflineBuffer(): ActivityRecord[] {
  const cached = safeGetItem<ActivityRecord[]>(STORAGE_KEY_PENDING_BUFFER, []);
  if (cached && cached.length > 0) {
    return cached;
  }
  // Fallback check on full activities list
  const all = loadStoredActivities();
  return all.filter((a) => !a.synced);
}

/**
 * Marks all pending records in localStorage as synced
 */
export function markAllStoredAsSynced(): ActivityRecord[] {
  const activities = loadStoredActivities();
  const updated = activities.map((a) => ({ ...a, synced: true }));
  persistActivities(updated);
  safeSetItem(STORAGE_KEY_PENDING_BUFFER, []);
  return updated;
}

/**
 * Loads cached personnel states (preserves who is currently INSIDE/OUTSIDE)
 */
export function loadStoredPersonnel(): Personnel[] {
  return safeGetItem<Personnel[]>(STORAGE_KEY_PERSONNEL, []);
}

/**
 * Persists personnel state to localStorage
 */
export function persistPersonnel(personnel: Personnel[]): void {
  safeSetItem(STORAGE_KEY_PERSONNEL, personnel);
}

/**
 * Loads cached vehicle fleet states (preserves who is currently INSIDE/OUTSIDE)
 */
export function loadStoredVehicles(): Vehicle[] {
  return safeGetItem<Vehicle[]>(STORAGE_KEY_VEHICLES, []);
}

/**
 * Persists vehicle fleet state to localStorage
 */
export function persistVehicles(vehicles: Vehicle[]): void {
  safeSetItem(STORAGE_KEY_VEHICLES, vehicles);
}

/**
 * Loads saved sync telemetry or default initial state
 */
export function loadStoredSyncState(fallback: SyncStatusData): SyncStatusData {
  const stored = safeGetItem<SyncStatusData | null>(STORAGE_KEY_SYNC_STATE, null);
  if (!stored) {
    // Count how many items currently in activities are unsynced
    const activities = loadStoredActivities();
    const pendingCount = activities.filter((a) => !a.synced).length;
    return {
      ...fallback,
      pendingCount,
    };
  }
  // Never trust a saved 'online' flag: connectivity is re-verified against the server on every start
  return { ...stored, isOnline: false };
}

/**
 * Persists sync telemetry state to localStorage
 */
export function persistSyncState(syncState: SyncStatusData): void {
  safeSetItem(STORAGE_KEY_SYNC_STATE, syncState);
}

/**
 * Completely clears all local storage caches (activities, pending buffer, personnel, vehicles, sync state)
 */
export function purgeAllLocalStorage(): void {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.removeItem(STORAGE_KEY_ACTIVITIES);
      window.localStorage.removeItem(STORAGE_KEY_PENDING_BUFFER);
      window.localStorage.removeItem(STORAGE_KEY_PERSONNEL);
      window.localStorage.removeItem(STORAGE_KEY_VEHICLES);
      window.localStorage.removeItem(STORAGE_KEY_SYNC_STATE);
    }
  } catch (e) {
    console.warn('[OfflineStorage] Error clearing localStorage:', e);
  }
}

const STORAGE_KEY_SESSION = 'xvdac_session_v2';

/** Restores the signed-in gatekeeper (null until someone signs in) */
export function loadStoredSession(): GatekeeperSession | null {
  return safeGetItem<GatekeeperSession | null>(STORAGE_KEY_SESSION, null);
}

export function persistSession(session: GatekeeperSession): void {
  safeSetItem(STORAGE_KEY_SESSION, session);
}

export function clearStoredSession(): void {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.removeItem(STORAGE_KEY_SESSION);
    }
  } catch {
    // ignore
  }
}