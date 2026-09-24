import React, { useState, useEffect, useRef } from 'react';
import {
  GatekeeperSession,
  ScreenId,
  SyncStatusData,
  ActivityRecord,
  Personnel,
  Vehicle,
  ManifestPerson,
  ActionDirection,
} from './types';
import {
  INITIAL_PERSONNEL,
  INITIAL_VEHICLES,
  findPersonnelById,
  findVehicleById,
  formatShortTime,
} from './data/mockDatabase';
import {
  loadStoredActivities,
  persistActivities,
  loadStoredPersonnel,
  persistPersonnel,
  loadStoredVehicles,
  persistVehicles,
  loadStoredSyncState,
  persistSyncState,
  markAllStoredAsSynced,
  purgeAllLocalStorage,
  loadStoredSession,
  persistSession,
  clearStoredSession,
} from './utils/offlineStorage';
import { playSuccessChime, playErrorTone } from './utils/audioFeedback';
import { parseScannedQr } from './utils/qrLocationParser';
import { apiUrl, getApiBase, pingServerEndpoint } from './utils/networkSync';

import { AndroidStatusBar } from './components/common/AndroidStatusBar';
import { AndroidNavBar } from './components/common/AndroidNavBar';
import { LoginScreen } from './screens/LoginScreen';
import { HomeScreen } from './screens/HomeScreen';
import { ScannerViewfinder } from './components/common/ScannerViewfinder';
import { PersonResultScreen } from './screens/PersonResultScreen';
import { SuccessScreen } from './screens/SuccessScreen';
import { VehicleScanFlow } from './screens/VehicleScanFlow';
import { ActivityScreen } from './screens/ActivityScreen';
import { SyncStatusScreen } from './screens/SyncStatusScreen';
import { OperatorScreen } from './screens/OperatorScreen';
import { DesignSystemDocModal } from './screens/DesignSystemDocModal';
import { ServerApiModal } from './components/common/ServerApiModal';
import { ApkBuildModal } from './components/common/ApkBuildModal';
import { PcCommandCenter } from './screens/PcCommandCenter';
import {
  seedFirestoreIfEmpty,
  syncActivityToCloud,
  updatePersonnelPresenceInCloud,
  updateVehiclePresenceInCloud,
  subscribeToLiveActivities,
  syncOfflineBuffer,
  clearAllCloudAndFirestoreData,
} from './lib/cloudSync';
import { calculatePreciseBreakdown } from './utils/durationCalculator';
import { Smartphone, Monitor, Download, Server, LayoutDashboard } from 'lucide-react';

export default function App() {
  // No default operator: the gatekeeper must sign in. A saved session is restored on restart.
  const [session, setSession] = useState<GatekeeperSession | null>(() => loadStoredSession());

  const [currentScreen, setCurrentScreen] = useState<ScreenId>(() => (loadStoredSession() ? 'HOME' : 'LOGIN'));
  const [soundEnabled, setSoundEnabled] = useState<boolean>(true);
  const [deviceFrameMode, setDeviceFrameMode] = useState<boolean>(true);
  const [showSpecModal, setShowSpecModal] = useState<boolean>(false);
  const [showServerApiModal, setShowServerApiModal] = useState<boolean>(false);
  const [showApkModal, setShowApkModal] = useState<boolean>(false);

  // Operational activity feed & personnel data restored from localStorage
  const [activities, setActivities] = useState<ActivityRecord[]>(() => loadStoredActivities());
  const [personnelList, setPersonnelList] = useState<Personnel[]>(() => loadStoredPersonnel());
  const [vehicleList, setVehicleList] = useState<Vehicle[]>(() => loadStoredVehicles());

  // Sync state initialized with localStorage persistence
  const [syncState, setSyncState] = useState<SyncStatusData>(() =>
    loadStoredSyncState({
      isOnline: false,
      lastSyncTime: 'Never',
      pendingCount: 0,
      failedCount: 0,
      todayTotal: 0,
    })
  );

  // True only while the PC server is actually answering
  const onlineRef = useRef<boolean>(false);

  // Pull the real roster (personnel + vehicles) from the PC server
  const hydrateFromServer = async () => {
    try {
      const [pRes, vRes] = await Promise.all([
        fetch(apiUrl('/api/personnel')),
        fetch(apiUrl('/api/vehicles')),
      ]);

      if (pRes.ok) {
        const data = await pRes.json();
        if (data && Array.isArray(data.personnel)) {
          setPersonnelList((prev) => {
            const statusMap = new Map(prev.map((p) => [p.id, p]));
            const serverList = data.personnel.map((sp: any) => {
              const local = statusMap.get(sp.id);
              return {
                id: sp.id,
                name: sp.fullName || sp.name,
                role: `${sp.rank || ''} • ${sp.company || ''} Co`.trim(),
                rank: sp.rank,
                serviceNumber: sp.serviceNumber || sp.armyNumber,
                unit: sp.unit,
                company: sp.company,
                department: sp.company ? `${sp.company} Company` : sp.department || 'Military Personnel',
                status: 'ACTIVE',
                currentStatus: local ? local.currentStatus : 'OUTSIDE',
                lastEntryTime: local?.lastEntryTime,
                lastEntryTimestamp: local?.lastEntryTimestamp,
                accessLocations: sp.accessLocations || [],
                secretCode: sp.secretCode,
                armyNumber: sp.armyNumber,
                idCardNumber: sp.idCardNumber,
              } as Personnel;
            });

            // Retain any locally added personnel not present on server
            const serverIds = new Set(serverList.map((p: Personnel) => p.id.toUpperCase()));
            const serverServiceNums = new Set(
              serverList.map((p: Personnel) => (p.serviceNumber || '').toUpperCase()).filter(Boolean)
            );
            const extraLocal = prev.filter(
              (p: Personnel) =>
                !serverIds.has(p.id.toUpperCase()) &&
                (!p.serviceNumber || !serverServiceNums.has(p.serviceNumber.toUpperCase()))
            );

            return [...serverList, ...extraLocal];
          });
        }
      }

      if (vRes.ok) {
        const data = await vRes.json();
        if (data && Array.isArray(data.vehicles)) {
          setVehicleList((prev) => {
            const localMap = new Map(prev.map((v) => [v.id, v]));
            return data.vehicles.map((sv: any) => {
              const local = localMap.get(sv.id);
              return {
                id: sv.id,
                plateNumber: sv.militaryRegistrationNumber || sv.plateNumber || sv.id,
                type: sv.vehicleType || sv.type || 'Vehicle',
                model: sv.model || '',
                status:
                  sv.status === 'OPERATIONAL' || sv.status === 'ACTIVE'
                    ? 'ACTIVE'
                    : sv.status === 'RESTRICTED'
                    ? 'FLAGGED'
                    : 'SUSPENDED',
                assignedCompany: sv.unitAssigned || sv.assignedCompany || '',
                currentStatus: local ? local.currentStatus : 'OUTSIDE',
                lastEntryTime: local?.lastEntryTime,
                lastEntryTimestamp: local?.lastEntryTimestamp,
                secretCode: sv.secretCode,
                militaryRegNumber: sv.militaryRegistrationNumber,
              } as Vehicle;
            });
          });
        }
      }
    } catch (err) {
      console.warn('[App] Could not load roster from server:', err);
    }
  };

  // Real connectivity: ask the server. On reconnect, load the roster and upload anything queued offline.
  const checkConnection = async () => {
    const res = await pingServerEndpoint(getApiBase());
    const nowOnline = res.ok;
    const wasOnline = onlineRef.current;
    onlineRef.current = nowOnline;

    setSyncState((prev) =>
      prev.isOnline === nowOnline
        ? prev
        : { ...prev, isOnline: nowOnline, lastSyncTime: nowOnline ? formatShortTime() : prev.lastSyncTime }
    );

    if (nowOnline && !wasOnline) {
      await hydrateFromServer();
      const pending = loadStoredActivities().filter((a) => !a.synced);
      if (pending.length > 0) {
        const result = await syncOfflineBuffer(pending);
        if (result.syncedCount > 0) {
          setActivities(markAllStoredAsSynced());
          setSyncState((prev) => ({ ...prev, pendingCount: 0, lastSyncTime: formatShortTime() }));
        }
      }
    }
  };

  useEffect(() => {
    seedFirestoreIfEmpty().catch(console.warn);

    const unsubscribe = subscribeToLiveActivities((cloudActivities) => {
      // Ignore cached cloud data while the server is unreachable
      if (cloudActivities && onlineRef.current) {
        setActivities((prevLocal) => {
          const localUnsynced = prevLocal.filter((a) => !a.synced);
          const combinedMap = new Map<string, ActivityRecord>();

          cloudActivities.forEach((act) => combinedMap.set(act.id, act));
          localUnsynced.forEach((act) => combinedMap.set(act.id, act));

          return Array.from(combinedMap.values()).sort((a, b) => b.timestampMs - a.timestampMs);
        });
      }
    });

    checkConnection();
    const timer = setInterval(checkConnection, 8000);
    const onVisible = () => {
      if (document.visibilityState === 'visible') checkConnection();
    };
    document.addEventListener('visibilitychange', onVisible);

    return () => {
      unsubscribe();
      clearInterval(timer);
      document.removeEventListener('visibilitychange', onVisible);
    };
  }, []);

  // Synchronize activities whenever they change
  useEffect(() => {
    persistActivities(activities);
    // Keep pendingCount aligned with unsynced activities in local storage
    const unSyncedCount = activities.filter((a) => !a.synced).length;
    setSyncState((prev) => {
      if (prev.pendingCount !== unSyncedCount) {
        const next = { ...prev, pendingCount: unSyncedCount };
        persistSyncState(next);
        return next;
      }
      return prev;
    });
  }, [activities]);

  // Synchronize personnel states to localStorage
  useEffect(() => {
    persistPersonnel(personnelList);
  }, [personnelList]);

  // Synchronize vehicle fleet states to localStorage
  useEffect(() => {
    persistVehicles(vehicleList);
  }, [vehicleList]);

  // Synchronize syncState to localStorage
  useEffect(() => {
    persistSyncState(syncState);
  }, [syncState]);

  // Temporary scanning states
  const [scannedPerson, setScannedPerson] = useState<Personnel | null>(null);
  const [scannedPersonCode, setScannedPersonCode] = useState<string>('');
  const [scannedPersonLocation, setScannedPersonLocation] = useState<string | undefined>(undefined);
  const [personLocationMismatch, setPersonLocationMismatch] = useState<boolean>(false);
  const [selectedVehicleForFlow, setSelectedVehicleForFlow] = useState<Vehicle | null>(null);
  const [lastSuccessActivity, setLastSuccessActivity] = useState<ActivityRecord | null>(null);
  const [activityFilterAction, setActivityFilterAction] = useState<'ALL' | 'ENTRY' | 'EXIT'>('ALL');
  const [activityFilterType, setActivityFilterType] = useState<'ALL' | 'PERSON' | 'VEHICLE'>('ALL');

  // ----------------------------------------------------
  // Handlers for Person Workflow
  // ----------------------------------------------------
  const handleStartScanPerson = () => {
    setCurrentScreen('SCAN_PERSON');
  };

  const handlePersonCodeScanned = (code: string) => {
    setScannedPersonCode(code);
    const parsed = parseScannedQr(code, session?.location);
    setScannedPersonLocation(parsed.locationId);
    setPersonLocationMismatch(parsed.isLocationMismatch);

    const targetEntityId = parsed.entityId || code;
    const cleanTarget = targetEntityId.trim().toUpperCase();
    const cleanRaw = code.trim().toUpperCase();

    const found =
      personnelList.find((p) => {
        const pId = p.id.toUpperCase();
        const pSec = p.secretCode ? p.secretCode.toUpperCase() : '';
        const pSrv = p.serviceNumber ? p.serviceNumber.toUpperCase() : '';
        const pArmy = p.armyNumber ? p.armyNumber.toUpperCase() : '';
        const pCard = p.idCardNumber ? p.idCardNumber.toUpperCase() : '';

        return (
          pId === cleanTarget ||
          pId.replace('-', '') === cleanTarget.replace('-', '') ||
          (pSec && (pSec === cleanTarget || pSec === cleanRaw)) ||
          (pSrv && (pSrv === cleanTarget || pSrv === cleanRaw)) ||
          (pArmy && (pArmy === cleanTarget || pArmy === cleanRaw)) ||
          (pCard && (pCard === cleanTarget || pCard === cleanRaw))
        );
      }) ||
      findPersonnelById(targetEntityId) ||
      findPersonnelById(code);

    if (found) {
      setScannedPerson(found);
    } else {
      setScannedPerson(null);
      playErrorTone(soundEnabled);
    }
    setCurrentScreen('PERSON_RESULT');
  };

  const handleConfirmPersonAction = (
    person: Personnel,
    action: ActionDirection,
    stayDuration?: string,
    locationMismatch?: boolean,
    scannedLocation?: string
  ) => {
    const isMismatch = locationMismatch !== undefined ? locationMismatch : personLocationMismatch;
    const locInQr = scannedLocation || scannedPersonLocation;
    const nowTimeStr = formatShortTime();
    const nowTimestamp = Date.now();

    let computedStayFormatted: string | undefined;
    let computedStayMs: number | undefined;
    let computedGapFormatted: string | undefined;
    let computedGapMs: number | undefined;

    if (action === 'EXIT') {
      const entryTs = person.lastEntryTimestamp;
      if (entryTs) {
        const breakdown = calculatePreciseBreakdown(entryTs, nowTimestamp);
        computedStayFormatted = breakdown.formatted;
        computedStayMs = breakdown.totalMs;
      }
    } else if (action === 'ENTRY') {
      const prevTs = person.lastSeenTimestamp || person.lastEntryTimestamp;
      if (prevTs) {
        const breakdown = calculatePreciseBreakdown(prevTs, nowTimestamp);
        computedGapFormatted = `${breakdown.humanReadable} ago`;
        computedGapMs = breakdown.totalMs;
      } else {
        computedGapFormatted = 'First recorded visit';
      }
    }

    const newRecord: ActivityRecord = {
      id: `ACT-${Date.now().toString().slice(-4)}`,
      timestamp: nowTimeStr,
      timestampMs: nowTimestamp,
      type: 'PERSON',
      action,
      targetId: person.id,
      title: person.name,
      subtitle: `${person.id} • ${person.role}`,
      location: session ? session.location : 'Location 07',
      gate: session ? session.gate : 'Gate 02',
      gatekeeperId: session ? session.id : 'GK-04',
      stayDuration: computedStayFormatted || stayDuration,
      stayDurationFormatted: computedStayFormatted,
      stayDurationMs: computedStayMs,
      timeSinceLastVisitFormatted: computedGapFormatted,
      timeSinceLastVisitMs: computedGapMs,
      locationMismatch: isMismatch,
      scannedLocation: locInQr,
      secretCode: person.secretCode || scannedPersonCode,
      serviceNumber: person.serviceNumber || person.armyNumber,
      armyNumber: person.armyNumber || person.serviceNumber,
      company: person.company,
      idCardNumber: person.idCardNumber,
      rank: person.rank,
      unit: person.unit,
      synced: syncState.isOnline,
    };

    setPersonnelList((prev) =>
      prev.map((p) => {
        if (p.id === person.id) {
          return {
            ...p,
            currentStatus: action === 'ENTRY' ? 'INSIDE' : 'OUTSIDE',
            lastEntryTime: action === 'ENTRY' ? nowTimeStr : p.lastEntryTime,
            lastEntryTimestamp: action === 'ENTRY' ? nowTimestamp : p.lastEntryTimestamp,
            lastSeenTime: nowTimeStr,
            lastSeenAction: action,
            lastSeenTimestamp: nowTimestamp,
          };
        }
        return p;
      })
    );

    // Append to activity log
    setActivities((prev) => [newRecord, ...prev]);

    // Send to Firestore and Node.js Express server if online
    if (syncState.isOnline) {
      syncActivityToCloud(newRecord).catch(console.warn);
      const updatedP = personnelList.find((p) => p.id === person.id);
      if (updatedP) {
        updatePersonnelPresenceInCloud({
          ...updatedP,
          currentStatus: action === 'ENTRY' ? 'INSIDE' : 'OUTSIDE',
        }).catch(console.warn);
      }
    }

    // Update sync metrics
    setSyncState((prev) => ({
      ...prev,
      todayTotal: prev.todayTotal + 1,
      pendingCount: prev.isOnline ? prev.pendingCount : prev.pendingCount + 1,
      lastSyncTime: prev.isOnline ? formatShortTime() : prev.lastSyncTime,
    }));

    playSuccessChime(soundEnabled);
    setLastSuccessActivity(newRecord);
    setCurrentScreen('SUCCESS_PERSON');
  };

  // ----------------------------------------------------
  // Handlers for Vehicle Workflow
  // ----------------------------------------------------
  const handleStartScanVehicle = () => {
    setCurrentScreen('VEHICLE_FLOW');
  };

  const handleCompleteVehicleAction = (
    vehicle: Vehicle,
    driver: ManifestPerson,
    coDriver: ManifestPerson | undefined,
    occupants: ManifestPerson[],
    action: ActionDirection,
    stayDuration?: string,
    locationMismatch?: boolean,
    scannedLocation?: string
  ) => {
    const nowTimeStr = formatShortTime();
    const nowTimestamp = Date.now();

    let computedStayFormatted: string | undefined;
    let computedStayMs: number | undefined;

    if (action === 'EXIT' && vehicle.lastEntryTimestamp) {
      const breakdown = calculatePreciseBreakdown(vehicle.lastEntryTimestamp, nowTimestamp);
      computedStayFormatted = breakdown.formatted;
      computedStayMs = breakdown.totalMs;
    }

    const newRecord: ActivityRecord = {
      id: `ACT-${Date.now().toString().slice(-4)}`,
      timestamp: nowTimeStr,
      timestampMs: nowTimestamp,
      type: 'VEHICLE',
      action,
      targetId: vehicle.id,
      title: vehicle.id,
      subtitle: `${vehicle.plateNumber} • ${vehicle.type}`,
      location: session ? session.location : 'Location 07',
      gate: session ? session.gate : 'Gate 02',
      gatekeeperId: session ? session.id : 'GK-04',
      stayDuration: computedStayFormatted || stayDuration,
      stayDurationFormatted: computedStayFormatted,
      stayDurationMs: computedStayMs,
      militaryRegNumber: vehicle.militaryRegNumber || vehicle.plateNumber,
      locationMismatch,
      scannedLocation,
      secretCode: vehicle.secretCode || `SEC-${vehicle.id}-TACTICAL`,
      synced: syncState.isOnline,
      vehicleManifest: {
        driver,
        coDriver,
        occupants,
      },
    };

    setActivities((prev) => [newRecord, ...prev]);

    // Update vehicle state
    vehicle.currentStatus = action === 'ENTRY' ? 'INSIDE' : 'OUTSIDE';
    if (action === 'ENTRY') {
      vehicle.lastEntryTime = nowTimeStr;
      vehicle.lastEntryTimestamp = nowTimestamp;
    }
    setVehicleList((prev) =>
      prev.map((v) => {
        if (v.id === vehicle.id) {
          return {
            ...v,
            currentStatus: action === 'ENTRY' ? 'INSIDE' : 'OUTSIDE',
            lastEntryTime: action === 'ENTRY' ? nowTimeStr : v.lastEntryTime,
            lastEntryTimestamp: action === 'ENTRY' ? nowTimestamp : v.lastEntryTimestamp,
          };
        }
        return v;
      })
    );
    if (syncState.isOnline) {
      updateVehiclePresenceInCloud(vehicle).catch(console.warn);
    }

    // Update personnel last seen state for manifest individuals if registered
    const manifestIds = [driver.id, coDriver?.id, ...occupants.map((o) => o.id)].filter(Boolean) as string[];
    if (manifestIds.length > 0) {
      const nowTimeStr = formatShortTime();
      const nowTimestamp = Date.now();
      setPersonnelList((prev) =>
        prev.map((p) => {
          if (manifestIds.includes(p.id)) {
            return {
              ...p,
              currentStatus: action === 'ENTRY' ? 'INSIDE' : 'OUTSIDE',
              lastEntryTime: action === 'ENTRY' ? nowTimeStr : p.lastEntryTime,
              lastEntryTimestamp: action === 'ENTRY' ? nowTimestamp : p.lastEntryTimestamp,
              lastSeenTime: nowTimeStr,
              lastSeenAction: action,
              lastSeenTimestamp: nowTimestamp,
            };
          }
          return p;
        })
      );
    }

    // Send to Firestore and Node.js Express server if online
    if (syncState.isOnline) {
      syncActivityToCloud(newRecord).catch(console.warn);
    }

    setSyncState((prev) => ({
      ...prev,
      todayTotal: prev.todayTotal + 1,
      pendingCount: prev.isOnline ? prev.pendingCount : prev.pendingCount + 1,
      lastSyncTime: prev.isOnline ? formatShortTime() : prev.lastSyncTime,
    }));

    setLastSuccessActivity(newRecord);
    setCurrentScreen('SUCCESS_PERSON');
  };

  // ----------------------------------------------------
  // Sync Controls
  // ----------------------------------------------------
  const handleToggleOnline = () => {
    checkConnection();
  };

  const handleFlushSync = () => {
    const pending = activities.filter((a) => !a.synced);
    if (pending.length > 0) {
      syncOfflineBuffer(pending).catch(console.warn);
    }
    const updated = markAllStoredAsSynced();
    setActivities(updated);
    setSyncState((prev) => ({
      ...prev,
      pendingCount: 0,
      lastSyncTime: formatShortTime(),
    }));
  };

  // ----------------------------------------------------
  // Session & Navigation Controls
  // ----------------------------------------------------
  const handleLogin = (newSession: GatekeeperSession) => {
    persistSession(newSession);
    setSession(newSession);
    setCurrentScreen('HOME');
  };

  const handleLogout = () => {
    if (session?.token) {
      fetch(apiUrl('/api/auth/logout'), { method: 'POST', headers: { Authorization: `Bearer ${session.token}` } }).catch(() => {});
    }
    clearStoredSession();
    setSession(null);
    setCurrentScreen('LOGIN');
  };

  const handlePurgeAllData = async () => {
    // 1. Wipe remote server JSON activity records
    try {
      await fetch(apiUrl('/api/activities/reset'), { method: 'POST' });
    } catch (e) {
      console.warn('Server reset call failed:', e);
    }

    // 2. Wipe remote Firestore activities collection
    try {
      await clearAllCloudAndFirestoreData();
    } catch (e) {
      console.warn('Cloud Firestore reset call failed:', e);
    }

    // 3. Wipe browser localStorage (activities, pending buffer, cached registry, sync states)
    purgeAllLocalStorage();

    // 4. Reset React in-memory states to pure clean empty state
    setActivities([]);
    setSyncState({
      isOnline: false,
      lastSyncTime: 'Never',
      pendingCount: 0,
      failedCount: 0,
      todayTotal: 0,
    });
  };

  // Determine if bottom navigation bar is visible
  const isBottomNavVisible =
    session !== null &&
    ['HOME', 'ACTIVITY', 'SYNC', 'OPERATOR'].includes(currentScreen);

  // If in PC Dashboard mode, render full-screen PC Command Center layout
  if (currentScreen === 'PC_DASHBOARD') {
    return (
      <PcCommandCenter
        activities={activities}
        personnelList={personnelList}
        vehicleList={vehicleList}
        session={session}
        onExitPcMode={() => setCurrentScreen('HOME')}
        onPurgeData={handlePurgeAllData}
      />
    );
  }

  return (
    <div className="w-full min-h-screen h-full bg-radial from-zinc-900 via-zinc-950 to-black text-zinc-900 flex flex-col items-center justify-start sm:justify-center p-0 sm:py-4 sm:px-3 select-none font-sans overflow-hidden">
      {/* Top Desktop Helper Toolbar (Frame Toggle & Spec) */}
      <aside className="hidden sm:flex items-center justify-between w-full max-w-[440px] mb-2 px-3 text-xs text-zinc-400 shrink-0">
        <div className="flex items-center gap-2">
          <div className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
          <span className="font-bold text-zinc-200 tracking-tight">Android Field Terminal</span>
          <span className="text-zinc-600">•</span>
          <span className="font-mono text-[11px] text-zinc-400">GK-OS v4.2</span>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setCurrentScreen('PC_DASHBOARD')}
            className="flex items-center gap-1.5 px-3 py-1 rounded-full bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/40 transition-all cursor-pointer shadow-xs font-mono text-[11px] font-bold"
            title="Open Full Desktop PC Command Center for Large Displays"
          >
            <LayoutDashboard className="w-3.5 h-3.5 text-amber-400" />
            <span>PC Command Center</span>
          </button>

          <button
            onClick={() => setShowServerApiModal(true)}
            className="flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-950/80 hover:bg-blue-900 text-blue-300 border border-blue-700/80 transition-all cursor-pointer shadow-xs"
            title="Inspect Node.js Express Ingestion API & Stored Records"
          >
            <Server className="w-3.5 h-3.5 text-blue-400" />
            <span className="font-semibold text-[11px]">Server API</span>
          </button>

          <button
            onClick={() => setShowApkModal(true)}
            className="flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-950/80 hover:bg-emerald-900 text-emerald-300 border border-emerald-600/80 transition-all cursor-pointer shadow-xs font-semibold text-[11px]"
            title="Download APK / Install on Android Device"
          >
            <Smartphone className="w-3.5 h-3.5 text-emerald-400" />
            <span>Install / APK</span>
          </button>

          <a
            href="/gatekeeper-terminal-source.zip"
            download="gatekeeper-terminal-source.zip"
            className="flex items-center gap-1.5 px-3 py-1 rounded-full bg-zinc-900/90 hover:bg-zinc-800 text-zinc-300 border border-zinc-700/80 transition-all cursor-pointer shadow-xs"
            title="Download complete source code as ZIP"
          >
            <Download className="w-3.5 h-3.5 text-zinc-400" />
            <span className="font-semibold text-[11px]">Source ZIP</span>
          </a>

          <button
            onClick={() => setDeviceFrameMode(!deviceFrameMode)}
            className="flex items-center gap-1.5 px-3 py-1 rounded-full bg-zinc-900/90 hover:bg-zinc-800 text-zinc-300 border border-zinc-700/80 transition-all cursor-pointer shadow-xs"
            title="Toggle Handheld Chassis View"
          >
            {deviceFrameMode ? (
              <>
                <Monitor className="w-3.5 h-3.5 text-zinc-400" />
                <span className="font-medium text-[11px]">Fluid View</span>
              </>
            ) : (
              <>
                <Smartphone className="w-3.5 h-3.5 text-emerald-400" />
                <span className="font-medium text-[11px]">Chassis View</span>
              </>
            )}
          </button>
        </div>
      </aside>

      {/* Android Device Shell Container */}
      <main
        className={`relative flex flex-col bg-zinc-100 overflow-hidden transition-all duration-200 ${
          deviceFrameMode
            ? 'w-full h-full sm:h-[870px] sm:max-h-[95vh] sm:max-w-[436px] sm:rounded-[40px] sm:border-[8px] sm:border-zinc-800 sm:ring-1 sm:ring-zinc-600/50 shadow-[0_25px_70px_rgba(0,0,0,0.85)]'
            : 'w-full h-full sm:h-[870px] sm:max-h-[95vh] max-w-2xl sm:rounded-2xl border border-zinc-800 shadow-2xl'
        }`}
      >
        {/* Top Speaker Earpiece & Sensor Notch (Shown in Chassis Mode on Desktop) */}
        {deviceFrameMode && (
          <div className="hidden sm:flex items-center justify-center h-4 w-full bg-zinc-950 shrink-0 relative">
            <div className="w-14 h-1 bg-zinc-800 rounded-full" />
            <div className="absolute right-12 w-2 h-2 rounded-full bg-zinc-900 border border-zinc-800" />
          </div>
        )}

        {/* Android Status Bar */}
        <AndroidStatusBar
          isOnline={syncState.isOnline}
          pendingCount={syncState.pendingCount}
          onOpenDesignDoc={() => setShowSpecModal(true)}
        />

        {/* Screen Viewport Switcher */}
        <div className="flex-1 flex flex-col overflow-hidden relative">
          {!session || currentScreen === 'LOGIN' ? (
            <LoginScreen onLogin={handleLogin} isOnline={syncState.isOnline} onServerChanged={checkConnection} />
          ) : currentScreen === 'HOME' ? (
            <HomeScreen
              session={session}
              syncState={syncState}
              recentActivities={activities}
              personnelList={personnelList}
              vehicleList={vehicleList}
              onScanPerson={handleStartScanPerson}
              onScanVehicle={() => {
                setSelectedVehicleForFlow(null);
                handleStartScanVehicle();
              }}
              onViewAllActivity={(filterAction, filterType) => {
                setActivityFilterAction(filterAction || 'ALL');
                setActivityFilterType(filterType || 'ALL');
                setCurrentScreen('ACTIVITY');
              }}
              onOpenPcDashboard={() => setCurrentScreen('PC_DASHBOARD')}
              onSelectPerson={(person) => {
                setScannedPerson(person);
                setScannedPersonCode(person.secretCode || person.id);
                setScannedPersonLocation(session.location);
                setPersonLocationMismatch(false);
                setCurrentScreen('PERSON_RESULT');
              }}
              onSelectVehicle={(vehicle) => {
                setSelectedVehicleForFlow(vehicle);
                setCurrentScreen('VEHICLE_FLOW');
              }}
            />
          ) : currentScreen === 'SCAN_PERSON' ? (
            <ScannerViewfinder
              title="Scan Person QR"
              subtitle="Align identity badge QR inside the reticle"
              mode="PERSON"
              onScan={handlePersonCodeScanned}
              onBack={() => setCurrentScreen('HOME')}
              soundEnabled={soundEnabled}
            />
          ) : currentScreen === 'PERSON_RESULT' ? (
            <PersonResultScreen
              person={scannedPerson}
              scannedCode={scannedPersonCode}
              session={session}
              scannedLocation={scannedPersonLocation}
              isLocationMismatch={personLocationMismatch}
              onConfirmAction={handleConfirmPersonAction}
              onScanAgain={() => setCurrentScreen('SCAN_PERSON')}
              onBack={() => setCurrentScreen('HOME')}
            />
          ) : currentScreen === 'SUCCESS_PERSON' && lastSuccessActivity ? (
            <SuccessScreen
              activity={lastSuccessActivity}
              onScanNext={() => {
                if (lastSuccessActivity.type === 'VEHICLE') {
                  setSelectedVehicleForFlow(null);
                  setCurrentScreen('VEHICLE_FLOW');
                } else {
                  setCurrentScreen('SCAN_PERSON');
                }
              }}
              onGoHome={() => setCurrentScreen('HOME')}
            />
          ) : currentScreen === 'VEHICLE_FLOW' ? (
            <VehicleScanFlow
              session={session}
              personnelList={personnelList}
              initialVehicle={selectedVehicleForFlow}
              onCancel={() => {
                setSelectedVehicleForFlow(null);
                setCurrentScreen('HOME');
              }}
              onCompleteVehicleAction={(veh, drv, coDrv, occs, act, dur, mis, scanLoc) => {
                setSelectedVehicleForFlow(null);
                handleCompleteVehicleAction(veh, drv, coDrv, occs, act, dur, mis, scanLoc);
              }}
              soundEnabled={soundEnabled}
            />
          ) : currentScreen === 'ACTIVITY' ? (
            <ActivityScreen
              key={`${activityFilterAction}-${activityFilterType}`}
              activities={activities}
              isOnline={syncState.isOnline}
              pendingCount={syncState.pendingCount}
              onFlushSync={handleFlushSync}
              initialActionFilter={activityFilterAction}
              initialTypeFilter={activityFilterType}
            />
          ) : currentScreen === 'SYNC' ? (
            <SyncStatusScreen
              syncState={syncState}
              activities={activities}
              onToggleOnline={handleToggleOnline}
              onFlushSync={handleFlushSync}
              soundEnabled={soundEnabled}
            />
          ) : currentScreen === 'OPERATOR' ? (
            <OperatorScreen
              session={session}
              soundEnabled={soundEnabled}
              onToggleSound={() => setSoundEnabled(!soundEnabled)}
              onLogout={handleLogout}
              onUpdateSession={(updated) => {
                setSession((prev) => (prev ? { ...prev, ...updated } : null));
              }}
              onOpenApkModal={() => setShowApkModal(true)}
              onPurgeData={handlePurgeAllData}
              activities={activities}
              personnelList={personnelList}
              vehicleList={vehicleList}
              syncState={syncState}
            />
          ) : (
            <HomeScreen
              session={session}
              syncState={syncState}
              recentActivities={activities}
              personnelList={personnelList}
              onScanPerson={handleStartScanPerson}
              onScanVehicle={handleStartScanVehicle}
              onViewAllActivity={(filterAction, filterType) => {
                setActivityFilterAction(filterAction || 'ALL');
                setActivityFilterType(filterType || 'ALL');
                setCurrentScreen('ACTIVITY');
              }}
              onOpenPcDashboard={() => setCurrentScreen('PC_DASHBOARD')}
              onSelectPerson={(person) => {
                setScannedPerson(person);
                setScannedPersonCode(person.secretCode || person.id);
                setScannedPersonLocation(session.location);
                setPersonLocationMismatch(false);
                setCurrentScreen('PERSON_RESULT');
              }}
            />
          )}
        </div>

        {/* Bottom Android Navigation Bar (only when on top-level tabs) */}
        {isBottomNavVisible && (
          <AndroidNavBar
            currentScreen={currentScreen}
            onNavigate={(screen) => setCurrentScreen(screen)}
            pendingSyncCount={syncState.pendingCount}
          />
        )}

        {/* Android Gesture Bar Pill */}
        {deviceFrameMode && (
          <div className="hidden sm:flex items-center justify-center py-1 bg-white shrink-0 border-t border-zinc-100">
            <div className="w-28 h-1 bg-zinc-300 rounded-full" />
          </div>
        )}
      </main>

      {/* Specification & Architecture Modal */}
      {showSpecModal && (
        <DesignSystemDocModal onClose={() => setShowSpecModal(false)} />
      )}

      {/* Node.js Express Server API & Storage Inspector Modal */}
      {showServerApiModal && (
        <ServerApiModal onClose={() => setShowServerApiModal(false)} />
      )}

      {/* Android APK & Installation Modal */}
      {showApkModal && (
        <ApkBuildModal onClose={() => setShowApkModal(false)} />
      )}
    </div>
  );
}
