import React, { useState, useEffect } from 'react';
import {
  QrCode,
  Truck,
  ArrowRight,
  UserCheck,
  ArrowDownLeft,
  ArrowUpRight,
  Shield,
  Sparkles,
  Database,
  Users,
  Building2,
  ExternalLink,
  Clock,
  Filter,
  Timer,
  AlertTriangle,
  LayoutDashboard,
  FileCheck2,
} from 'lucide-react';
import { GatekeeperSession, ActivityRecord, SyncStatusData, Personnel, Vehicle } from '../types';
import { SyncSnackbar } from '../components/common/SyncSnackbar';
import { PersonnelRosterModal } from '../components/common/PersonnelRosterModal';
import { VehicleFleetModal } from '../components/common/VehicleFleetModal';
import { ShiftHandoverModal } from '../components/common/ShiftHandoverModal';

const SHIFT_DURATION_MS = 8 * 60 * 60 * 1000; // 8 hours in milliseconds

function computeShiftTimes(shiftStartTime: string, shiftStartTimestamp?: number, currentTimeMs = Date.now()) {
  let startMs: number;
  if (shiftStartTimestamp && !isNaN(shiftStartTimestamp)) {
    startMs = shiftStartTimestamp;
  } else {
    // Parse time string e.g. "18:00" or "06:00 PM"
    const match = shiftStartTime.match(/(\d{1,2}):(\d{2})(?::(\d{2}))?\s*(AM|PM)?/i);
    if (match) {
      let hours = parseInt(match[1], 10);
      const minutes = parseInt(match[2], 10);
      const ampm = match[4]?.toUpperCase();
      if (ampm === 'PM' && hours < 12) hours += 12;
      if (ampm === 'AM' && hours === 12) hours = 0;

      const startDate = new Date(currentTimeMs);
      startDate.setHours(hours, minutes, 0, 0);

      // If the parsed start time is ahead of current time by > 1 hour, it likely started yesterday
      if (startDate.getTime() - currentTimeMs > 60 * 60 * 1000) {
        startDate.setDate(startDate.getDate() - 1);
      }
      startMs = startDate.getTime();
    } else {
      startMs = currentTimeMs - 2 * 3600 * 1000;
    }
  }

  const endMs = startMs + SHIFT_DURATION_MS;
  const remainingMs = endMs - currentTimeMs;
  const elapsedMs = currentTimeMs - startMs;
  const progressPercent = Math.min(100, Math.max(0, (elapsedMs / SHIFT_DURATION_MS) * 100));

  const isOvertime = remainingMs <= 0;
  const effectiveMs = isOvertime ? Math.abs(remainingMs) : remainingMs;

  const totalSec = Math.floor(effectiveMs / 1000);
  const hours = Math.floor(totalSec / 3600);
  const minutes = Math.floor((totalSec % 3600) / 60);
  const seconds = totalSec % 60;

  const pad = (n: number) => String(n).padStart(2, '0');
  const endDate = new Date(endMs);
  const endTimeStr = endDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  return {
    startMs,
    endMs,
    remainingMs,
    elapsedMs,
    isOvertime,
    isUrgent: !isOvertime && remainingMs < 60 * 60 * 1000, // less than 1 hour remaining
    hoursStr: pad(hours),
    minutesStr: pad(minutes),
    secondsStr: pad(seconds),
    progressPercent,
    endTimeStr,
  };
}

interface HomeScreenProps {
  session: GatekeeperSession;
  syncState: SyncStatusData;
  recentActivities: ActivityRecord[];
  personnelList?: Personnel[];
  vehicleList?: Vehicle[];
  onScanPerson: () => void;
  onScanVehicle: () => void;
  onViewAllActivity: (filterAction?: 'ALL' | 'ENTRY' | 'EXIT', filterType?: 'ALL' | 'PERSON' | 'VEHICLE') => void;
  onOpenPcDashboard?: () => void;
  onSelectPerson?: (person: Personnel) => void;
  onSelectVehicle?: (vehicle: Vehicle) => void;
}

export const HomeScreen: React.FC<HomeScreenProps> = ({
  session,
  syncState,
  recentActivities,
  personnelList = [],
  vehicleList = [],
  onScanPerson,
  onScanVehicle,
  onViewAllActivity,
  onOpenPcDashboard,
  onSelectPerson,
  onSelectVehicle,
}) => {
  // Dynamic tick for real-time 8-hour shift countdown
  const [currentTick, setCurrentTick] = useState<number>(() => Date.now());
  const [showRosterModal, setShowRosterModal] = useState<boolean>(false);
  const [rosterInitialFilter, setRosterInitialFilter] = useState<'ALL' | 'INSIDE' | 'OUTSIDE'>('ALL');
  const [showFleetModal, setShowFleetModal] = useState<boolean>(false);
  const [fleetInitialFilter, setFleetInitialFilter] = useState<'ALL' | 'INSIDE' | 'OUTSIDE'>('ALL');
  const [showHandoverModal, setShowHandoverModal] = useState<boolean>(false);

  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentTick(Date.now());
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const shiftInfo = computeShiftTimes(session.shiftStartTime, session.shiftStartTimestamp, currentTick);

  // Compute real-time personnel inside vs outside from local database
  const insidePersonnel = personnelList.filter((p) => p.currentStatus === 'INSIDE');
  const outsidePersonnel = personnelList.filter((p) => p.currentStatus === 'OUTSIDE');
  const totalPersonnel = personnelList.length;
  const insidePercentage = totalPersonnel > 0 ? Math.round((insidePersonnel.length / totalPersonnel) * 100) : 0;

  // Compute real-time vehicle fleet status
  const insideVehicles = vehicleList.filter((v) => v.currentStatus === 'INSIDE');
  const outsideVehicles = vehicleList.filter((v) => v.currentStatus === 'OUTSIDE');
  const totalVehicles = vehicleList.length;
  const vehicleInsidePercentage = totalVehicles > 0 ? Math.round((insideVehicles.length / totalVehicles) * 100) : 0;

  // Compute today shift scan counts from real activity logs
  const entriesCount = recentActivities.filter((a) => a.action === 'ENTRY').length;
  const exitsCount = recentActivities.filter((a) => a.action === 'EXIT').length;

  return (
    <div className="flex flex-col flex-1 px-5 py-4 max-w-md mx-auto w-full select-none overflow-y-auto">
      {/* 1. Operator Identity & Shift Header Banner */}
      <div className="p-4 bg-white border border-zinc-200/90 rounded-2xl shadow-xs transition-all">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="relative">
              <div className="w-11 h-11 rounded-xl bg-zinc-900 text-white flex items-center justify-center font-mono font-bold text-sm shadow-xs border border-zinc-800">
                {session.id}
              </div>
              <span
                className={`absolute -bottom-0.5 -right-0.5 w-3 h-3 rounded-full border-2 border-white ${
                  syncState.isOnline ? 'bg-emerald-500 ring-2 ring-emerald-500/20' : 'bg-amber-500'
                }`}
              />
            </div>
            <div>
              <div className="flex items-center gap-1.5">
                <span className="text-xs font-bold text-zinc-900">{session.name}</span>
                <span className="text-[10px] font-mono text-zinc-400 font-semibold">
                  ({session.id})
                </span>
              </div>
              <p className="text-[11px] font-semibold text-zinc-600 flex items-center gap-1 mt-0.5">
                <span>{session.location}</span>
                <span className="text-zinc-300">•</span>
                <span className="text-zinc-900 font-bold">{session.gate}</span>
              </p>
            </div>
          </div>

          {/* Connection Status Pill */}
          <div className="flex flex-col items-end">
            <div
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full border text-[11px] font-bold ${
                syncState.isOnline
                  ? 'bg-emerald-50 text-emerald-700 border-emerald-200/90'
                  : 'bg-amber-50 text-amber-700 border-amber-200'
              }`}
            >
              <span
                className={`w-1.5 h-1.5 rounded-full ${
                  syncState.isOnline ? 'bg-emerald-500 animate-pulse' : 'bg-amber-500'
                }`}
              />
              <span>{syncState.isOnline ? 'Live Cloud' : 'Offline'}</span>
            </div>
            <span className="text-[10px] text-zinc-400 font-mono mt-1">
              Sync {syncState.lastSyncTime}
            </span>
          </div>
        </div>

        {/* Dynamic 8-Hour Shift Countdown Timer */}
        <div
          id="shift-countdown-timer"
          className={`mt-3.5 pt-3 border-t transition-colors ${
            shiftInfo.isOvertime
              ? 'border-rose-100 bg-rose-50/30 -mx-4 -mb-4 p-3.5 rounded-b-2xl'
              : shiftInfo.isUrgent
              ? 'border-amber-100 bg-amber-50/30 -mx-4 -mb-4 p-3.5 rounded-b-2xl'
              : 'border-zinc-100'
          }`}
        >
          <div className="flex items-center justify-between gap-2">
            <div className="flex items-center gap-2 min-w-0">
              <div
                className={`w-7 h-7 rounded-lg flex items-center justify-center shrink-0 ${
                  shiftInfo.isOvertime
                    ? 'bg-rose-100 text-rose-700'
                    : shiftInfo.isUrgent
                    ? 'bg-amber-100 text-amber-700'
                    : 'bg-zinc-100 text-zinc-700'
                }`}
              >
                <Clock className="w-3.5 h-3.5" />
              </div>
              <div className="min-w-0">
                <div className="flex items-center gap-1.5 flex-wrap">
                  <span className="text-[11px] font-bold text-zinc-900 leading-tight">
                    {shiftInfo.isOvertime ? 'Shift Over (Overtime)' : 'Shift Remaining'}
                  </span>
                  <span
                    className={`text-[9px] font-bold px-1.5 py-0.2 rounded-full border ${
                      shiftInfo.isOvertime
                        ? 'bg-rose-50 text-rose-700 border-rose-200'
                        : shiftInfo.isUrgent
                        ? 'bg-amber-50 text-amber-700 border-amber-200'
                        : 'bg-emerald-50 text-emerald-700 border-emerald-200'
                    }`}
                  >
                    8h Shift
                  </span>
                </div>
                <span className="text-[10px] text-zinc-400 font-medium block truncate">
                  Start {session.shiftStartTime} • Ends ~{shiftInfo.endTimeStr}
                </span>
              </div>
            </div>

            {/* Dynamic Countdown Digital Readout */}
            <div className="shrink-0 flex items-center font-mono">
              <div
                className={`flex items-baseline px-2 py-1 rounded-lg border font-mono font-bold tracking-wider shadow-2xs ${
                  shiftInfo.isOvertime
                    ? 'bg-rose-950 text-rose-200 border-rose-800'
                    : shiftInfo.isUrgent
                    ? 'bg-amber-950 text-amber-200 border-amber-800'
                    : 'bg-zinc-900 text-white border-zinc-800'
                }`}
              >
                {shiftInfo.isOvertime && <span className="text-rose-400 mr-0.5 text-xs font-bold">+</span>}
                <span className="text-sm">{shiftInfo.hoursStr}</span>
                <span className="text-[10px] text-zinc-400 mx-0.5">h</span>
                <span className="text-zinc-500 font-bold mx-0.5">:</span>
                <span className="text-sm">{shiftInfo.minutesStr}</span>
                <span className="text-[10px] text-zinc-400 mx-0.5">m</span>
                <span className="text-zinc-500 font-bold mx-0.5">:</span>
                <span
                  className={`text-sm ${
                    shiftInfo.isOvertime
                      ? 'text-rose-400'
                      : shiftInfo.isUrgent
                      ? 'text-amber-400'
                      : 'text-emerald-400'
                  }`}
                >
                  {shiftInfo.secondsStr}
                </span>
                <span className="text-[10px] text-zinc-400 ml-0.5">s</span>
              </div>
            </div>
          </div>

          {/* Shift Duration Progress Bar */}
          <div className="mt-2.5 flex items-center gap-2">
            <div className="flex-1 bg-zinc-200/80 rounded-full h-1.5 overflow-hidden">
              <div
                className={`h-1.5 rounded-full transition-all duration-300 ${
                  shiftInfo.isOvertime
                    ? 'bg-rose-600'
                    : shiftInfo.isUrgent
                    ? 'bg-amber-500'
                    : 'bg-emerald-600'
                }`}
                style={{ width: `${shiftInfo.progressPercent}%` }}
              />
            </div>
            <span className="text-[10px] font-mono font-semibold text-zinc-500 shrink-0">
              {Math.round(shiftInfo.progressPercent)}% elapsed
            </span>
          </div>

          {/* Quick Shift Handover Dossier Trigger */}
          <div className="mt-2.5 pt-2 border-t border-zinc-100 flex items-center justify-between">
            <span className="text-[10px] text-zinc-400 font-medium">
              Relieving operator arriving?
            </span>
            <button
              type="button"
              onClick={() => setShowHandoverModal(true)}
              className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-zinc-100 hover:bg-zinc-200/80 text-zinc-800 text-[11px] font-bold transition-all cursor-pointer border border-zinc-200"
              title="Open Official Shift Handover Dossier"
            >
              <FileCheck2 className="w-3.5 h-3.5 text-amber-600" />
              <span>Shift Handover Dossier</span>
            </button>
          </div>
        </div>
      </div>

      {/* Offline Storage Active Notice Banner */}
      {(!syncState.isOnline || syncState.pendingCount > 0) && (
        <div className="mt-3 px-3.5 py-2.5 bg-amber-500/10 border border-amber-500/30 rounded-xl flex items-center justify-between text-xs text-amber-900 animate-in fade-in duration-200">
          <div className="flex items-center gap-2">
            <Database className="w-4 h-4 text-amber-700 shrink-0" />
            <span className="text-[11px] font-medium text-amber-900">
              {!syncState.isOnline
                ? 'Offline mode: Scans are saved safely to device storage'
                : 'Pending records in local storage ready to sync'}
            </span>
          </div>
          {syncState.pendingCount > 0 && (
            <span className="font-mono text-[10px] font-bold px-2 py-0.5 bg-amber-200/80 text-amber-900 border border-amber-300 rounded-full shrink-0">
              {syncState.pendingCount} QUEUED
            </span>
          )}
        </div>
      )}

      {/* 2. Priority Scanning Actions (Primary Touch Targets 76dp) */}
      <div className="space-y-3.5 my-4">
        {/* PRIORITY 1: SCAN PERSON */}
        <button
          onClick={onScanPerson}
          className="w-full relative overflow-hidden flex items-center justify-between p-5 bg-gradient-to-br from-zinc-900 via-zinc-900 to-zinc-950 hover:to-black active:scale-[0.985] text-white rounded-2xl shadow-md transition-all text-left cursor-pointer group border border-zinc-800"
        >
          {/* Subtle Emerald Laser Accent Line */}
          <div className="absolute top-0 left-0 right-0 h-0.5 bg-gradient-to-r from-transparent via-emerald-400/80 to-transparent" />

          <div className="flex items-center gap-4">
            <div className="relative w-13 h-13 rounded-xl bg-zinc-800/90 border border-zinc-700/80 flex items-center justify-center text-white shrink-0 group-hover:bg-zinc-700 transition-colors shadow-inner">
              <QrCode className="w-7 h-7 text-emerald-400 stroke-[1.8]" />
              <div className="absolute -top-1 -right-1 w-2 h-2 rounded-full bg-emerald-400 animate-ping opacity-75" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-mono tracking-wider text-emerald-400 uppercase font-bold px-1.5 py-0.2 rounded bg-emerald-950/80 border border-emerald-800/60">
                  PRIORITY 1
                </span>
                <span className="text-[10px] font-mono text-zinc-400">NFC • QR</span>
              </div>
              <h3 className="text-lg font-bold text-white tracking-tight leading-tight mt-0.5">
                SCAN PERSON
              </h3>
              <p className="text-xs text-zinc-400 mt-0.5">
                Staff, contractor & visitor entry/exit
              </p>
            </div>
          </div>

          <div className="w-9 h-9 rounded-full bg-zinc-800 flex items-center justify-center text-zinc-400 group-hover:text-white group-hover:translate-x-0.5 transition-all">
            <ArrowRight className="w-4 h-4" />
          </div>
        </button>

        {/* PRIORITY 2: SCAN VEHICLE */}
        <button
          onClick={onScanVehicle}
          className="w-full relative overflow-hidden flex items-center justify-between p-5 bg-white hover:bg-zinc-50 active:scale-[0.985] text-zinc-900 border border-zinc-200/90 hover:border-zinc-300 rounded-2xl shadow-xs transition-all text-left cursor-pointer group"
        >
          {/* Subtle Accent Line */}
          <div className="absolute top-0 left-0 right-0 h-0.5 bg-gradient-to-r from-transparent via-zinc-400/40 to-transparent" />

          <div className="flex items-center gap-4">
            <div className="w-13 h-13 rounded-xl bg-zinc-100 border border-zinc-200 flex items-center justify-center text-zinc-800 shrink-0 group-hover:bg-zinc-200/70 transition-colors">
              <Truck className="w-7 h-7 text-zinc-800 stroke-[1.8]" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-mono tracking-wider text-zinc-600 uppercase font-bold px-1.5 py-0.2 rounded bg-zinc-100 border border-zinc-200">
                  PRIORITY 2
                </span>
                <span className="text-[10px] font-mono text-zinc-400">MANIFEST</span>
              </div>
              <h3 className="text-lg font-bold text-zinc-900 tracking-tight leading-tight mt-0.5">
                SCAN VEHICLE
              </h3>
              <p className="text-xs text-zinc-500 mt-0.5">
                Trucks, delivery cargo & multi-occupants
              </p>
            </div>
          </div>

          <div className="w-9 h-9 rounded-full bg-zinc-100 flex items-center justify-center text-zinc-500 group-hover:text-zinc-900 group-hover:translate-x-0.5 transition-all">
            <ArrowRight className="w-4 h-4" />
          </div>
        </button>
      </div>

      {/* 3. Real-Time Personnel Presence Summary (Inside vs Outside) */}
      <div id="personnel-presence-summary-card" className="p-4 bg-white border border-zinc-200/90 rounded-2xl shadow-xs mb-3.5 transition-all">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <div className="w-6 h-6 rounded-lg bg-zinc-100 flex items-center justify-center text-zinc-700">
              <Users className="w-3.5 h-3.5" />
            </div>
            <div>
              <span className="text-xs font-bold text-zinc-900 tracking-tight block leading-tight">
                Personnel On-Site Presence
              </span>
              <span className="text-[10px] text-zinc-400 font-medium block">
                Tracked in local registry database
              </span>
            </div>
          </div>
          <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded-full bg-zinc-100 text-zinc-600 border border-zinc-200">
            {totalPersonnel} registered
          </span>
        </div>

        {/* Dual Presence Metric Blocks */}
        <div className="grid grid-cols-2 gap-2.5">
          {/* INSIDE Tile */}
          <div
            onClick={() => {
              setRosterInitialFilter('INSIDE');
              setShowRosterModal(true);
            }}
            className="p-3 bg-emerald-50/60 hover:bg-emerald-50 active:scale-[0.985] rounded-xl border border-emerald-200/80 hover:border-emerald-300 transition-all cursor-pointer shadow-2xs"
            title="Click to view all personnel currently inside"
          >
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-bold text-emerald-800 uppercase tracking-wider flex items-center gap-1.5">
                <span className="w-2 h-2 rounded-full bg-emerald-500 ring-2 ring-emerald-500/20 animate-pulse" />
                Inside Facility
              </span>
              <span className="text-[10px] font-mono font-bold text-emerald-700">
                {insidePercentage}%
              </span>
            </div>
            <div className="flex items-baseline gap-1 mt-1.5">
              <span className="text-2xl font-bold font-mono text-emerald-950">
                {insidePersonnel.length}
              </span>
              <span className="text-[11px] font-medium text-emerald-700">
                active on site
              </span>
            </div>
            <div className="mt-2 w-full bg-emerald-200/60 rounded-full h-1.5 overflow-hidden">
              <div
                className="bg-emerald-600 h-1.5 rounded-full transition-all duration-500"
                style={{ width: `${insidePercentage}%` }}
              />
            </div>
          </div>

          {/* OUTSIDE Tile */}
          <div
            onClick={() => {
              setRosterInitialFilter('OUTSIDE');
              setShowRosterModal(true);
            }}
            className="p-3 bg-zinc-50 hover:bg-zinc-100/70 active:scale-[0.985] rounded-xl border border-zinc-200/80 hover:border-zinc-300 transition-all cursor-pointer shadow-2xs"
            title="Click to view all personnel currently outside"
          >
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-bold text-zinc-700 uppercase tracking-wider flex items-center gap-1.5">
                <span className="w-2 h-2 rounded-full bg-zinc-400" />
                Outside / Off-Site
              </span>
              <span className="text-[10px] font-mono font-bold text-zinc-500">
                {100 - insidePercentage}%
              </span>
            </div>
            <div className="flex items-baseline gap-1 mt-1.5">
              <span className="text-2xl font-bold font-mono text-zinc-900">
                {outsidePersonnel.length}
              </span>
              <span className="text-[11px] font-medium text-zinc-500">
                departed / off
              </span>
            </div>
            <div className="mt-2 w-full bg-zinc-200 rounded-full h-1.5 overflow-hidden">
              <div
                className="bg-zinc-400 h-1.5 rounded-full transition-all duration-500"
                style={{ width: `${100 - insidePercentage}%` }}
              />
            </div>
          </div>
        </div>

        {/* Personnel Quick Verification Cards with 'Last Seen' Badge */}
        <div className="mt-3 pt-3 border-t border-zinc-100">
          <div className="flex items-center justify-between mb-2">
            <span className="text-[10px] font-bold text-zinc-500 uppercase tracking-wider flex items-center gap-1">
              <Clock className="w-3 h-3 text-zinc-400" />
              Recent Personnel Status & Last Seen
            </span>
            <button
              onClick={() => {
                setRosterInitialFilter('ALL');
                setShowRosterModal(true);
              }}
              className="text-[11px] font-bold text-emerald-700 hover:text-emerald-900 flex items-center gap-1 cursor-pointer transition-colors"
            >
              <span>View Roster ({totalPersonnel})</span>
              <ArrowRight className="w-3 h-3" />
            </button>
          </div>

          <div className="space-y-1.5 max-h-44 overflow-y-auto pr-0.5">
            {personnelList.length === 0 ? (
              <div className="p-4 text-center text-zinc-400 text-xs bg-zinc-50/60 rounded-xl border border-dashed border-zinc-200">
                <p className="font-medium text-zinc-600">Personnel Registry is Empty</p>
                <p className="text-[10px] text-zinc-400 mt-0.5">Add personnel via PC Command Center or scan new credentials</p>
              </div>
            ) : (
              personnelList.slice(0, 4).map((p) => {
                const isInside = p.currentStatus === 'INSIDE';
                const lastSeenText = p.lastSeenTime || p.lastEntryTime || 'No recent activity';
                const lastSeenAction = p.lastSeenAction || (isInside ? 'ENTRY' : 'EXIT');

                return (
                  <div
                    key={p.id}
                    onClick={() => onSelectPerson && onSelectPerson(p)}
                    className={`flex items-center justify-between p-2 rounded-xl bg-zinc-50/90 border border-zinc-200/70 hover:border-zinc-300 transition-colors ${
                      onSelectPerson ? 'cursor-pointer active:bg-zinc-100 hover:shadow-2xs' : ''
                    }`}
                    title={onSelectPerson ? `View and verify ${p.name} (${p.id})` : undefined}
                  >
                    <div className="flex items-center gap-2 min-w-0">
                      <div className="relative shrink-0">
                        <img
                          src={p.photoUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=240&auto=format&fit=crop&q=80'}
                          alt={p.name}
                          className="w-7 h-7 rounded-lg object-cover border border-zinc-200"
                        />
                        <span
                          className={`absolute -bottom-0.5 -right-0.5 w-2 h-2 rounded-full border border-white ${
                            isInside ? 'bg-emerald-500' : 'bg-zinc-400'
                          }`}
                        />
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5">
                          <span className="text-xs font-bold text-zinc-900 truncate">
                            {p.name}
                          </span>
                          <span className="text-[10px] font-mono text-zinc-400 font-semibold">
                            {p.id}
                          </span>
                        </div>
                        <span className="text-[10px] text-zinc-500 truncate block">
                          {p.role}
                        </span>
                      </div>
                    </div>

                    {/* Last Seen Timestamp Badge */}
                    <div className="shrink-0 flex flex-col items-end gap-0.5">
                      <span
                        className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-mono font-bold border ${
                          lastSeenAction === 'ENTRY'
                            ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                            : 'bg-zinc-100 text-zinc-600 border-zinc-200'
                        }`}
                        title={`Last seen at gate: ${lastSeenText} (${lastSeenAction})`}
                      >
                        {lastSeenAction === 'ENTRY' ? (
                          <ArrowDownLeft className="w-2.5 h-2.5 text-emerald-600" />
                        ) : (
                          <ArrowUpRight className="w-2.5 h-2.5 text-zinc-500" />
                        )}
                        <span>Seen {lastSeenText}</span>
                      </span>
                      <span className="text-[9px] font-semibold text-zinc-400 uppercase tracking-tight">
                        {isInside ? 'On Site' : 'Off Site'}
                      </span>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>

      {/* 3b. Real-Time Vehicle Fleet Presence Summary */}
      {totalVehicles > 0 && (
        <div id="vehicle-fleet-presence-summary-card" className="p-4 bg-white border border-zinc-200/90 rounded-2xl shadow-xs mb-3.5 transition-all">
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-2">
              <div className="w-6 h-6 rounded-lg bg-amber-50 border border-amber-200/60 flex items-center justify-center text-amber-700">
                <Truck className="w-3.5 h-3.5" />
              </div>
              <div>
                <span className="text-xs font-bold text-zinc-900 tracking-tight block leading-tight">
                  Vehicle Fleet Presence
                </span>
                <span className="text-[10px] text-zinc-400 font-medium block">
                  Commercial & tactical yard staging
                </span>
              </div>
            </div>
            <button
              onClick={() => {
                setFleetInitialFilter('ALL');
                setShowFleetModal(true);
              }}
              className="text-[11px] font-bold text-amber-800 hover:text-amber-950 flex items-center gap-1 cursor-pointer transition-colors"
            >
              <span>View Fleet ({totalVehicles})</span>
              <ArrowRight className="w-3 h-3" />
            </button>
          </div>

          {/* Dual Vehicle Fleet Presence Blocks */}
          <div className="grid grid-cols-2 gap-2.5">
            {/* IN YARD Tile */}
            <div
              onClick={() => {
                setFleetInitialFilter('INSIDE');
                setShowFleetModal(true);
              }}
              className="p-3 bg-amber-50/70 hover:bg-amber-100/60 active:scale-[0.985] rounded-xl border border-amber-200/90 hover:border-amber-300 transition-all cursor-pointer shadow-2xs"
              title="Click to view all vehicles currently inside yard"
            >
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-amber-900 uppercase tracking-wider flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-amber-500 ring-2 ring-amber-500/20 animate-pulse" />
                  In Yard / Base
                </span>
                <span className="text-[10px] font-mono font-bold text-amber-800">
                  {vehicleInsidePercentage}%
                </span>
              </div>
              <div className="flex items-baseline gap-1 mt-1.5">
                <span className="text-2xl font-bold font-mono text-amber-950">
                  {insideVehicles.length}
                </span>
                <span className="text-[11px] font-medium text-amber-800">
                  staged on site
                </span>
              </div>
              <div className="mt-2 w-full bg-amber-200/60 rounded-full h-1.5 overflow-hidden">
                <div
                  className="bg-amber-600 h-1.5 rounded-full transition-all duration-500"
                  style={{ width: `${vehicleInsidePercentage}%` }}
                />
              </div>
            </div>

            {/* DISPATCHED Tile */}
            <div
              onClick={() => {
                setFleetInitialFilter('OUTSIDE');
                setShowFleetModal(true);
              }}
              className="p-3 bg-zinc-50 hover:bg-zinc-100/70 active:scale-[0.985] rounded-xl border border-zinc-200/80 hover:border-zinc-300 transition-all cursor-pointer shadow-2xs"
              title="Click to view all vehicles currently dispatched"
            >
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-zinc-700 uppercase tracking-wider flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-zinc-400" />
                  Dispatched / Off
                </span>
                <span className="text-[10px] font-mono font-bold text-zinc-500">
                  {100 - vehicleInsidePercentage}%
                </span>
              </div>
              <div className="flex items-baseline gap-1 mt-1.5">
                <span className="text-2xl font-bold font-mono text-zinc-900">
                  {outsideVehicles.length}
                </span>
                <span className="text-[11px] font-medium text-zinc-500">
                  in transit
                </span>
              </div>
              <div className="mt-2 w-full bg-zinc-200 rounded-full h-1.5 overflow-hidden">
                <div
                  className="bg-zinc-400 h-1.5 rounded-full transition-all duration-500"
                  style={{ width: `${100 - vehicleInsidePercentage}%` }}
                />
              </div>
            </div>
          </div>

          {/* Quick Vehicle Roster list preview */}
          <div className="mt-3 pt-3 border-t border-zinc-100">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[10px] font-bold text-zinc-500 uppercase tracking-wider flex items-center gap-1">
                <Clock className="w-3 h-3 text-zinc-400" />
                Fleet Status & Staging
              </span>
              <span className="text-[10px] text-zinc-400 font-mono">
                {insideVehicles.length} Active / {totalVehicles} Total
              </span>
            </div>

            <div className="space-y-1.5 max-h-36 overflow-y-auto pr-0.5">
              {vehicleList.slice(0, 3).map((v) => {
                const isInside = v.currentStatus === 'INSIDE';
                return (
                  <div
                    key={v.id}
                    onClick={() => {
                      if (onSelectVehicle) onSelectVehicle(v);
                    }}
                    className={`flex items-center justify-between p-2 rounded-xl border transition-all ${
                      onSelectVehicle ? 'cursor-pointer hover:bg-zinc-50 active:scale-[0.99]' : ''
                    } ${
                      isInside
                        ? 'bg-amber-50/40 border-amber-200/70'
                        : 'bg-zinc-50/60 border-zinc-200/60'
                    }`}
                    title={onSelectVehicle ? `Scan or verify ${v.plateNumber} (${v.id})` : undefined}
                  >
                    <div className="flex items-center gap-2 min-w-0">
                      <div className="w-7 h-7 rounded-lg bg-zinc-100 border border-zinc-200 flex items-center justify-center text-zinc-700 shrink-0">
                        <Truck className="w-3.5 h-3.5" />
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5">
                          <span className="text-xs font-bold text-zinc-900 truncate">
                            {v.plateNumber}
                          </span>
                          <span className="text-[10px] font-mono text-zinc-500 font-semibold">
                            {v.id}
                          </span>
                        </div>
                        <span className="text-[10px] text-zinc-500 truncate block">
                          {v.model} • {v.assignedCompany}
                        </span>
                      </div>
                    </div>

                    <div className="shrink-0 flex items-center gap-1.5">
                      <span
                        className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-mono font-bold border ${
                          isInside
                            ? 'bg-amber-100 text-amber-900 border-amber-300'
                            : 'bg-zinc-100 text-zinc-600 border-zinc-200'
                        }`}
                      >
                        {isInside ? (
                          <ArrowDownLeft className="w-2.5 h-2.5 text-amber-700" />
                        ) : (
                          <ArrowUpRight className="w-2.5 h-2.5 text-zinc-500" />
                        )}
                        <span>{isInside ? 'IN YARD' : 'OUT'}</span>
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      )}

      {/* 4. Shift Performance & Activity Metrics */}
      <div className="p-4 bg-zinc-50/80 border border-zinc-200 rounded-2xl mb-4">
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-1.5">
            <Shield className="w-3.5 h-3.5 text-zinc-700" />
            <span className="text-xs font-bold text-zinc-800 uppercase tracking-wider">
              Shift Counters
            </span>
          </div>
          <span className="text-[11px] font-mono text-zinc-500 font-semibold">
            Shift Started {session.shiftStartTime}
          </span>
        </div>

        <div className="grid grid-cols-2 gap-3">
          {/* Entries Tile */}
          <div
            onClick={() => onViewAllActivity('ENTRY')}
            className="p-3 bg-white hover:bg-emerald-50/40 rounded-xl border border-zinc-200/90 hover:border-emerald-300 shadow-2xs cursor-pointer active:scale-[0.985] transition-all"
            title="Click to view all entry events in full activity log"
          >
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-bold text-emerald-700 uppercase tracking-wider flex items-center gap-1">
                <ArrowDownLeft className="w-3 h-3 text-emerald-600" />
                Entries
              </span>
              <span className="w-2 h-2 rounded-full bg-emerald-500" />
            </div>
            <div className="text-2xl font-bold font-mono text-zinc-900 mt-1">
              {entriesCount}
            </div>
            <span className="text-[10px] text-zinc-400 block mt-0.5">Personnel & vehicles</span>
          </div>

          {/* Exits Tile */}
          <div
            onClick={() => onViewAllActivity('EXIT')}
            className="p-3 bg-white hover:bg-amber-50/40 rounded-xl border border-zinc-200/90 hover:border-amber-300 shadow-2xs cursor-pointer active:scale-[0.985] transition-all"
            title="Click to view all exit events in full activity log"
          >
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-bold text-amber-700 uppercase tracking-wider flex items-center gap-1">
                <ArrowUpRight className="w-3 h-3 text-amber-600" />
                Exits
              </span>
              <span className="w-2 h-2 rounded-full bg-amber-500" />
            </div>
            <div className="text-2xl font-bold font-mono text-zinc-900 mt-1">
              {exitsCount}
            </div>
            <span className="text-[10px] text-zinc-400 block mt-0.5">Cleared off-site</span>
          </div>
        </div>
      </div>

      {/* 4. Recent Activity Log Feed */}
      <div className="mt-auto pt-1">
        <div className="flex items-center justify-between mb-2.5">
          <span className="text-xs font-bold text-zinc-800 uppercase tracking-wider">
            Recent Activity
          </span>
          <button
            onClick={() => onViewAllActivity('ALL', 'ALL')}
            className="text-xs font-semibold text-zinc-900 hover:text-zinc-600 flex items-center gap-1 cursor-pointer transition-colors"
          >
            <span>Full Log</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="space-y-2">
          {recentActivities.length === 0 ? (
            <div className="p-6 text-center text-zinc-400 text-xs bg-white rounded-xl border border-dashed border-zinc-200">
              <p className="font-semibold text-zinc-700">No Scan Events Recorded</p>
              <p className="text-[11px] text-zinc-400 mt-0.5">
                Ready for gate entries and exits. Tap Scan Person or Scan Vehicle to begin.
              </p>
            </div>
          ) : (
            recentActivities.slice(0, 3).map((act) => {
              const isEntry = act.action === 'ENTRY';
              return (
                <div
                  key={act.id}
                  onClick={() => onViewAllActivity(act.action, act.type)}
                  className="flex items-center justify-between p-3 bg-white border border-zinc-200/80 hover:border-zinc-300 rounded-xl text-xs transition-all cursor-pointer shadow-2xs active:bg-zinc-50"
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <span className="font-mono text-zinc-400 font-bold text-[11px] shrink-0 w-11">
                      {act.timestamp}
                    </span>
                    <div className="w-6 h-6 rounded-md bg-zinc-100 flex items-center justify-center text-zinc-700 shrink-0">
                      {act.type === 'VEHICLE' ? (
                        <Truck className="w-3.5 h-3.5" />
                      ) : (
                        <UserCheck className="w-3.5 h-3.5" />
                      )}
                    </div>
                    <div className="min-w-0 truncate">
                      <span className="font-bold text-zinc-900 truncate block">
                        {act.title}
                      </span>
                      <span className="text-[11px] text-zinc-400 font-mono">
                        {act.targetId}
                      </span>
                    </div>
                  </div>

                  <span
                    className={`font-mono text-[10px] font-bold px-2.5 py-0.5 rounded-full shrink-0 ${
                      isEntry
                        ? 'bg-emerald-50 text-emerald-700 border border-emerald-200/80'
                        : 'bg-amber-50 text-amber-700 border border-amber-200/80'
                    }`}
                  >
                    {act.action}
                  </span>
                </div>
              );
            })
          )}
        </div>
      </div>

      {/* Subtle, Non-Intrusive Online Sync Reassurance Snackbar */}
      <SyncSnackbar
        isOnline={syncState.isOnline}
        pendingCount={syncState.pendingCount}
        duration={3500}
      />

      {/* PC Command Center Access Banner */}
      {onOpenPcDashboard && (
        <button
          onClick={onOpenPcDashboard}
          className="w-full mt-1 mb-2 p-3 bg-gradient-to-r from-zinc-900 to-zinc-950 hover:from-black hover:to-zinc-900 text-left rounded-xl border border-zinc-800 flex items-center justify-between group cursor-pointer transition-all shadow-sm"
        >
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-amber-500/20 border border-amber-500/30 flex items-center justify-center text-amber-400">
              <LayoutDashboard className="w-4 h-4" />
            </div>
            <div>
              <span className="text-xs font-bold text-zinc-100 flex items-center gap-1.5">
                PC Command Center View
                <span className="text-[9px] font-mono font-bold px-1.5 py-0.2 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30">
                  DESKTOP
                </span>
              </span>
              <span className="text-[10px] text-zinc-400 block">
                Multi-gate live telemetry & base-wide monitoring
              </span>
            </div>
          </div>
          <ArrowRight className="w-4 h-4 text-zinc-400 group-hover:text-white group-hover:translate-x-0.5 transition-all" />
        </button>
      )}

      {/* Personnel Gate Registry Roster Modal */}
      {showRosterModal && (
        <PersonnelRosterModal
          isOpen={showRosterModal}
          onClose={() => setShowRosterModal(false)}
          personnelList={personnelList}
          initialFilter={rosterInitialFilter}
          onSelectPerson={(person) => {
            if (onSelectPerson) {
              onSelectPerson(person);
            }
          }}
        />
      )}

      {/* Vehicle Fleet Registry Modal */}
      {showFleetModal && (
        <VehicleFleetModal
          isOpen={showFleetModal}
          onClose={() => setShowFleetModal(false)}
          vehicleList={vehicleList}
          initialFilter={fleetInitialFilter}
          onSelectVehicle={(v) => {
            if (onSelectVehicle) {
              onSelectVehicle(v);
            }
          }}
        />
      )}

      {/* Shift Handover Dossier Modal */}
      {showHandoverModal && (
        <ShiftHandoverModal
          isOpen={showHandoverModal}
          onClose={() => setShowHandoverModal(false)}
          session={session}
          syncState={syncState}
          activities={recentActivities}
          personnelList={personnelList}
          vehicleList={vehicleList}
        />
      )}
    </div>
  );
};
