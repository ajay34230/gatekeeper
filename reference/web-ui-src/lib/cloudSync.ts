/**
 * Cloud Sync Service with Firebase Firestore and Express Server APIs
 * Bridges local offline storage with real-time Firestore database and PC backend.
 */

import {
  collection,
  doc,
  setDoc,
  getDocs,
  onSnapshot,
  query,
  orderBy,
  limit,
  writeBatch,
  deleteDoc,
} from 'firebase/firestore';
import { db } from './firebase';
import { ActivityRecord, Personnel, Vehicle } from '../types';
import { INITIAL_ACTIVITIES, INITIAL_PERSONNEL, INITIAL_VEHICLES } from '../data/mockDatabase';
import { apiUrl } from '../utils/networkSync';

const ACTIVITIES_COLLECTION = 'activities';
const PERSONNEL_COLLECTION = 'personnel';
const VEHICLES_COLLECTION = 'vehicles';

/**
 * Strips undefined properties recursively so Firestore does not throw Unsupported field value: undefined
 */
function sanitizeForFirestore<T>(data: T): T {
  if (data === null || data === undefined) return null as any;
  if (Array.isArray(data)) {
    return data.map(sanitizeForFirestore) as any;
  }
  if (typeof data === 'object') {
    const cleaned: Record<string, any> = {};
    for (const [key, value] of Object.entries(data)) {
      if (value !== undefined) {
        cleaned[key] = sanitizeForFirestore(value);
      }
    }
    return cleaned as T;
  }
  return data;
}

/**
 * Seed initial data to Firestore if collections are empty.
 * Disabled for production so no mock/fake records are written.
 */
export async function seedFirestoreIfEmpty(): Promise<void> {
  // Pure production: zero fake records seeded
  return;
}

/**
 * Purges all records from Firestore and tells the server to reset storage
 */
export async function clearAllCloudAndFirestoreData(): Promise<{ success: boolean; message: string }> {
  try {
    // 1. Wipe Firestore activities
    const actSnap = await getDocs(collection(db, ACTIVITIES_COLLECTION));
    const batch = writeBatch(db);
    actSnap.forEach((d) => batch.delete(d.ref));
    await batch.commit();

    // 2. Wipe Server Storage
    try {
      await fetch(apiUrl('/api/activities/reset'), { method: 'POST' });
    } catch (e) {
      console.warn('[CloudSync] Express storage reset notice:', e);
    }

    return { success: true, message: 'All cloud & server records cleared.' };
  } catch (error: any) {
    console.error('[CloudSync] Error purging cloud data:', error);
    return { success: false, message: error?.message || 'Error purging records' };
  }
}

/**
 * Save an activity record to Firestore and forward to Express /api/activities
 */
export async function syncActivityToCloud(record: ActivityRecord): Promise<{ success: boolean; cloudId?: string }> {
  try {
    // 1. Save directly to Firestore collection
    const recordDocRef = doc(db, ACTIVITIES_COLLECTION, record.id);
    const cleaned = sanitizeForFirestore({
      ...record,
      synced: true,
      syncedAt: new Date().toISOString(),
    });
    await setDoc(recordDocRef, cleaned, { merge: true });

    // 2. Also forward to Express server REST API endpoint
    try {
      fetch(apiUrl('/api/activities'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(record),
      }).catch((e) => console.warn('[CloudSync] Express API background notification:', e));
    } catch (e) {
      // Non-blocking
    }

    return { success: true, cloudId: record.id };
  } catch (error) {
    console.error('[CloudSync] Failed to save record to Firestore:', error);
    return { success: false };
  }
}

/**
 * Update personnel presence state in Firestore
 */
export async function updatePersonnelPresenceInCloud(personnel: Personnel): Promise<void> {
  try {
    const personRef = doc(db, PERSONNEL_COLLECTION, personnel.id);
    await setDoc(personRef, sanitizeForFirestore(personnel), { merge: true });
  } catch (error) {
    console.warn('[CloudSync] Failed to update personnel presence in Firestore:', error);
  }
}

/**
 * Update vehicle presence state in Firestore
 */
export async function updateVehiclePresenceInCloud(vehicle: Vehicle): Promise<void> {
  try {
    const vehRef = doc(db, VEHICLES_COLLECTION, vehicle.id);
    await setDoc(vehRef, sanitizeForFirestore(vehicle), { merge: true });
  } catch (error) {
    console.warn('[CloudSync] Failed to update vehicle presence in Firestore:', error);
  }
}

/**
 * Subscribe to real-time activity stream from Firestore
 */
export function subscribeToLiveActivities(
  onUpdate: (activities: ActivityRecord[]) => void,
  onError?: (err: Error) => void
) {
  const q = query(collection(db, ACTIVITIES_COLLECTION), orderBy('timestampMs', 'desc'), limit(100));
  return onSnapshot(
    q,
    (snapshot) => {
      const items: ActivityRecord[] = [];
      snapshot.forEach((doc) => {
        items.push(doc.data() as ActivityRecord);
      });
      onUpdate(items);
    },
    (err) => {
      console.warn('[Firestore] Realtime subscription notice:', err);
      if (onError) onError(err);
    }
  );
}

/**
 * Batch synchronize offline buffer to both Firestore and Express Server API
 */
export async function syncOfflineBuffer(pendingRecords: ActivityRecord[]): Promise<{ syncedCount: number; failedCount: number }> {
  if (!pendingRecords.length) return { syncedCount: 0, failedCount: 0 };

  let syncedCount = 0;
  let failedCount = 0;

  // Sync to Express batch endpoint
  try {
    const res = await fetch(apiUrl('/api/activities/batch'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        records: pendingRecords,
      }),
    });
    if (res.ok) {
      syncedCount = pendingRecords.length;
    }
  } catch (err) {
    console.warn('[CloudSync] Server batch endpoint not reachable, falling back to Firestore writeBatch');
  }

  // Sync to Firestore
  try {
    const batch = writeBatch(db);
    pendingRecords.forEach((rec) => {
      const ref = doc(db, ACTIVITIES_COLLECTION, rec.id);
      batch.set(ref, sanitizeForFirestore({ ...rec, synced: true, syncedAt: new Date().toISOString() }), { merge: true });
    });
    await batch.commit();
    syncedCount = pendingRecords.length;
  } catch (error) {
    console.error('[CloudSync] Firestore batch commit error:', error);
    failedCount = pendingRecords.length - syncedCount;
  }

  return { syncedCount, failedCount };
}
