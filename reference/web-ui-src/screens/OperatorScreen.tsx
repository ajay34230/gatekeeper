import React, { useState } from 'react';
import { UserCheck, Smartphone, Volume2, VolumeX, LogOut, ShieldAlert, MapPin, Clock, QrCode, ArrowRightLeft, Check, Shield, FileCheck2, Trash2, AlertTriangle, RefreshCw } from 'lucide-react';
import { GatekeeperSession, Personnel, ActivityRecord, Vehicle, SyncStatusData } from '../types';
import { MilitaryIdCardModal } from '../components/MilitaryIdCardModal';
import { ShiftHandoverModal } from '../components/common/ShiftHandoverModal';

interface OperatorScreenProps {
  session: GatekeeperSession;
  soundEnabled: boolean;
  onToggleSound: () => void;
  onLogout: () => void;
  onUpdateSession?: (updated: Partial<GatekeeperSession>) => void;
  onOpenApkModal?: () => void;
  onPurgeData?: () => Promise<void>;
  activities?: ActivityRecord[];
  personnelList?: Personnel[];
  vehicleList?: Vehicle[];
  syncState?: SyncStatusData;
}

export const OperatorScreen: React.FC<OperatorScreenProps> = ({
  session,
  soundEnabled,
  onToggleSound,
  onLogout,
  onUpdateSession,
  onOpenApkModal,
  onPurgeData,
  activities = [],
  personnelList = [],
  vehicleList = [],
  syncState = { isOnline: true, lastSyncTime: 'Just now', pendingCount: 0, failedCount: 0, todayTotal: 0 },
}) => {
  const [showIdCard, setShowIdCard] = useState<boolean>(false);
  const [showHandoverModal, setShowHandoverModal] = useState<boolean>(false);
  const [isEditingPost, setIsEditingPost] = useState<boolean>(false);
  const [selectedLocation, setSelectedLocation] = useState<string>(session.location);
  const [selectedGate, setSelectedGate] = useState<string>(session.gate);
  const [isPurging, setIsPurging] = useState<boolean>(false);
  const [purgeSuccess, setPurgeSuccess] = useState<boolean>(false);

  // Available station locations & gates in forward operational garrison
  const LOCATIONS = [
    'Location 07',
    'Location 01',
    'Location 02',
    'Location 03',
    'Location 05',
    'Location 10',
  ];

  const GATES = [
    'Gate 01',
    'Gate 02',
    'Gate 03',
    'Gate 04',
    'North Checkpoint',
    'South Sallyport',
  ];

  const handleSavePost = () => {
    if (onUpdateSession) {
      onUpdateSession({
        location: selectedLocation,
        gate: selectedGate,
      });
    }
    setIsEditingPost(false);
  };

  // Synthesize operator personnel profile for ID badge display & QR printing
  const operatorPerson: Personnel = {
    id: session.id,
    secretCode: `SEC-${session.id}-OFFICER`,
    serviceNumber: `GK-OP-${session.id}`,
    armyNumber: `ARMY-GK-${session.id}`,
    rank: 'Sergeant',
    name: session.name,
    role: session.role,
    department: 'Base Security',
    company: 'Alpha',
    unit: 'Gate Security Detachment',
    idCardNumber: `IC-GK-${session.id}-HQ`,
    accessLocations: [session.location],
    status: 'ACTIVE',
    photoUrl: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=240&auto=format&fit=crop&q=80',
    currentStatus: 'INSIDE',
  };
  return (
    <div className="flex flex-col flex-1 px-5 py-4 max-w-md mx-auto w-full select-none justify-between overflow-y-auto">
      <div>
        <div className="pb-3 border-b border-zinc-200/80">
          <h1 className="text-lg font-bold text-zinc-900 tracking-tight">Gatekeeper Profile</h1>
          <p className="text-xs text-zinc-500 font-medium">Active terminal operator credentials</p>
        </div>

        {/* Operator Badge Card */}
        <div className="mt-5 p-5 bg-white border border-zinc-200 rounded-2xl shadow-xs">
          <div className="flex items-center gap-3.5">
            <div className="w-12 h-12 rounded-xl bg-zinc-900 text-white flex items-center justify-center font-mono font-bold text-base">
              {session.id}
            </div>
            <div>
              <span className="text-[11px] uppercase tracking-wider text-zinc-400 font-semibold block">
                Field Gatekeeper
              </span>
              <h2 className="text-base font-bold text-zinc-900">{session.name}</h2>
              <span className="text-xs text-zinc-500">{session.role}</span>
            </div>
          </div>

          <div className="mt-4 pt-4 border-t border-zinc-100 space-y-2.5 text-xs">
            <div className="flex justify-between items-center">
              <span className="text-zinc-400 flex items-center gap-1.5">
                <MapPin className="w-3.5 h-3.5 text-zinc-500" />
                Assigned Post
              </span>
              <div className="flex items-center gap-2">
                <span className="font-bold text-zinc-800">
                  {session.location} • {session.gate}
                </span>
                <button
                  type="button"
                  onClick={() => setIsEditingPost(!isEditingPost)}
                  className="px-2 py-0.5 rounded-md bg-zinc-100 hover:bg-zinc-200 text-zinc-700 text-[10px] font-semibold flex items-center gap-1 cursor-pointer transition-colors"
                >
                  <ArrowRightLeft className="w-2.5 h-2.5" />
                  <span>{isEditingPost ? 'Cancel' : 'Change Post'}</span>
                </button>
              </div>
            </div>

            {/* Relocation Drawer */}
            {isEditingPost && (
              <div className="p-3 bg-zinc-50 rounded-xl border border-zinc-200 space-y-2.5 my-2">
                <span className="text-[11px] font-bold text-zinc-800 uppercase tracking-wider block">
                  Reassign Terminal Post
                </span>
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="text-[10px] text-zinc-500 block mb-0.5 font-medium">Location</label>
                    <select
                      value={selectedLocation}
                      onChange={(e) => setSelectedLocation(e.target.value)}
                      className="w-full px-2 py-1.5 bg-white border border-zinc-300 rounded-lg text-xs font-semibold text-zinc-800 focus:outline-none focus:ring-1 focus:ring-zinc-800"
                    >
                      {LOCATIONS.map((loc) => (
                        <option key={loc} value={loc}>
                          {loc}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="text-[10px] text-zinc-500 block mb-0.5 font-medium">Gate</label>
                    <select
                      value={selectedGate}
                      onChange={(e) => setSelectedGate(e.target.value)}
                      className="w-full px-2 py-1.5 bg-white border border-zinc-300 rounded-lg text-xs font-semibold text-zinc-800 focus:outline-none focus:ring-1 focus:ring-zinc-800"
                    >
                      {GATES.map((g) => (
                        <option key={g} value={g}>
                          {g}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                <button
                  onClick={handleSavePost}
                  className="w-full py-1.5 px-3 bg-zinc-900 hover:bg-zinc-800 text-white rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 cursor-pointer transition-colors shadow-2xs"
                >
                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                  <span>Apply Post Relocation</span>
                </button>
              </div>
            )}

            <div className="flex justify-between items-center">
              <span className="text-zinc-400 flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5 text-zinc-500" />
                Shift Commenced
              </span>
              <span className="font-mono font-bold text-zinc-800">{session.shiftStartTime}</span>
            </div>
          </div>

          {/* View / Print Official Military ID Card Button */}
          <div className="mt-3.5 pt-3 border-t border-zinc-100">
            <button
              onClick={() => setShowIdCard(true)}
              className="w-full py-2.5 px-3 bg-zinc-900 hover:bg-zinc-800 text-white rounded-xl text-xs font-semibold flex items-center justify-center gap-2 cursor-pointer transition-all shadow-xs"
            >
              <QrCode className="w-4 h-4 text-emerald-400" />
              <span>View Official Military ID Card & QR</span>
            </button>
          </div>
        </div>

        {/* Terminal Device Info */}
        <div className="mt-4 p-4 bg-white border border-zinc-200 rounded-xl space-y-2.5 text-xs shadow-2xs">
          <div className="flex items-center justify-between text-zinc-700 font-bold mb-1">
            <div className="flex items-center gap-2">
              <Smartphone className="w-4 h-4 text-emerald-600" />
              <span>Terminal & APK Deployment</span>
            </div>
            {onOpenApkModal && (
              <button
                onClick={onOpenApkModal}
                className="text-[11px] font-bold text-emerald-700 hover:text-emerald-800 bg-emerald-50 hover:bg-emerald-100 border border-emerald-300 px-2.5 py-1 rounded-lg transition-colors cursor-pointer"
              >
                Install / Build APK
              </button>
            )}
          </div>
          <div className="flex justify-between text-zinc-600">
            <span className="text-zinc-400">Device Model</span>
            <span className="font-mono text-zinc-800 font-semibold">Zebra TC26 Android</span>
          </div>
          <div className="flex justify-between text-zinc-600">
            <span className="text-zinc-400">Client Build</span>
            <span className="font-mono text-zinc-800">v2.4.1-prod (Build 741)</span>
          </div>
          <div className="flex justify-between text-zinc-600">
            <span className="text-zinc-400">Barcode Engine</span>
            <span className="text-zinc-800">SE4710 1D/2D Imager</span>
          </div>
        </div>

        {/* Operational Preferences */}
        <div className="mt-4 p-4 bg-white border border-zinc-200 rounded-xl space-y-3 text-xs">
          <span className="font-bold text-zinc-800 block text-[11px] uppercase tracking-wider">
            Scanner Feedback
          </span>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              {soundEnabled ? (
                <Volume2 className="w-4 h-4 text-emerald-600" />
              ) : (
                <VolumeX className="w-4 h-4 text-zinc-400" />
              )}
              <span className="text-zinc-800 font-medium">Audio Beep Feedback</span>
            </div>
            <button
              onClick={onToggleSound}
              className={`w-11 h-6 rounded-full transition-colors relative cursor-pointer ${
                soundEnabled ? 'bg-zinc-900' : 'bg-zinc-300'
              }`}
            >
              <div
                className={`w-4 h-4 rounded-full bg-white absolute top-1 transition-transform ${
                  soundEnabled ? 'right-1' : 'left-1'
                }`}
              />
            </button>
          </div>
        </div>

        {/* Database & Activity Ledger Reset */}
        {onPurgeData && (
          <div className="mt-4 p-4 bg-white border border-zinc-200 rounded-xl space-y-2 text-xs">
            <div className="flex items-center justify-between">
              <span className="font-bold text-zinc-800 block text-[11px] uppercase tracking-wider">
                Sovereign Database Maintenance
              </span>
              <span className="text-[10px] font-mono text-zinc-400">Zero Fake Data</span>
            </div>
            <p className="text-[11px] text-zinc-500">
              Purge all local cache, buffered scans, server records, and cloud Firestore entries to start with a 100% clean ledger.
            </p>
            {purgeSuccess ? (
              <div className="p-2.5 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-800 text-[11px] flex items-center gap-2 font-medium">
                <Check className="w-4 h-4 text-emerald-600 shrink-0" />
                <span>All local, server, and cloud records wiped clean.</span>
              </div>
            ) : (
              <button
                type="button"
                disabled={isPurging}
                onClick={async () => {
                  if (window.confirm('Wipe all scan records and reset database to clean empty state?')) {
                    setIsPurging(true);
                    try {
                      await onPurgeData();
                      setPurgeSuccess(true);
                      setTimeout(() => setPurgeSuccess(false), 4000);
                    } catch (e) {
                      console.error(e);
                    } finally {
                      setIsPurging(false);
                    }
                  }
                }}
                className="w-full py-2.5 px-3 bg-zinc-100 hover:bg-rose-50 text-zinc-700 hover:text-rose-700 border border-zinc-200 hover:border-rose-200 font-semibold rounded-lg text-[11px] transition-colors flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
              >
                {isPurging ? (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Trash2 className="w-3.5 h-3.5" />
                )}
                <span>{isPurging ? 'Purging All Storage...' : 'Wipe & Reset Database Clean'}</span>
              </button>
            )}
          </div>
        )}
      </div>

      {/* Logout & Shift Handover Actions */}
      <div className="mt-6 space-y-2.5">
        <button
          onClick={() => setShowHandoverModal(true)}
          className="w-full py-3.5 px-4 bg-amber-50 hover:bg-amber-100 text-amber-900 border border-amber-300 font-bold rounded-xl text-xs transition-colors flex items-center justify-center gap-2 cursor-pointer shadow-2xs"
        >
          <FileCheck2 className="w-4 h-4 text-amber-600" />
          <span>Prepare Shift Handover Dossier & Report</span>
        </button>

        <button
          onClick={onLogout}
          className="w-full py-3.5 px-4 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 font-bold rounded-xl text-xs transition-colors flex items-center justify-center gap-2 cursor-pointer"
        >
          <LogOut className="w-4 h-4" />
          <span>Exit Shift / Log Out</span>
        </button>
      </div>

      {/* Shift Handover Dossier Modal */}
      {showHandoverModal && (
        <ShiftHandoverModal
          isOpen={showHandoverModal}
          onClose={() => setShowHandoverModal(false)}
          session={session}
          syncState={syncState}
          activities={activities}
          personnelList={personnelList}
          vehicleList={vehicleList}
          onConfirmHandover={() => {
            setShowHandoverModal(false);
            onLogout();
          }}
        />
      )}

      {/* Military ID Card Modal for Active Operator */}
      {showIdCard && (
        <MilitaryIdCardModal
          person={operatorPerson}
          isOpen={showIdCard}
          onClose={() => setShowIdCard(false)}
        />
      )}
    </div>
  );
};
