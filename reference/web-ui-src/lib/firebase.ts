import { initializeApp, getApps, getApp } from 'firebase/app';
import { initializeFirestore, getFirestore, Firestore } from 'firebase/firestore';
import firebaseConfig from '../../firebase-applet-config.json';

// Initialize Firebase App
const app = !getApps().length ? initializeApp(firebaseConfig) : getApp();

// Initialize Firestore with long-polling to prevent WebSocket connection failures in sandbox/iframe environments
export const db: Firestore = (() => {
  const databaseId = firebaseConfig.firestoreDatabaseId || undefined;
  const settings = {
    experimentalAutoDetectLongPolling: true,
  };

  try {
    return databaseId
      ? initializeFirestore(app, settings, databaseId)
      : initializeFirestore(app, settings);
  } catch (_err) {
    // If already initialized, fallback to getFirestore
    return databaseId
      ? getFirestore(app, databaseId)
      : getFirestore(app);
  }
})();

export default app;

