import React, { useState, useEffect } from 'react';
import { ArrowLeft, User, MapPin, Clock, AlertTriangle, ShieldCheck, CheckCircle2, AlertOctagon } from 'lucide-react';
import { Personnel, GatekeeperSession, ActionDirection } from '../types';
import { StatusChip } from '../components/common/StatusChip';
import { formatCurrentTime } from '../data/mockDatabase';
import { parseScannedQr } from '../utils/qrLocationParser';

import { calculatePreciseBreakdown } from '../utils/durationCalculator';

/**
 * Calculates duration since last ENTRY timestamp or last entry time string.
 */
function computeDurationSinceEntry(
  startTimestamp?: number,
  lastEntryTime?: string
): string {
  let startMs = startTimestamp;

  if (!startMs && lastEntryTime) {
    const parts = lastEntryTime.split(':').map((p) => parseInt(p, 10));
    if (parts.length >= 2 && !isNaN(parts[0]) && !isNaN(parts[1])) {
      const now = new Date();
      const entryDate = new Date(now.getFullYear(), now.getMonth(), now.getDate(), parts[0], parts[1], parts[2] || 0);
      if (entryDate.getTime() > now.getTime()) {
        entryDate.setDate(entryDate.getDate() - 1);
      }
      startMs = entryDate.getTime();
    }
  }

  if (!startMs) {
    return '0m';
  }

  const breakdown = calculatePreciseBreakdown(startMs, Date.now());
  return breakdown.formatted || '0m';
}

interface PersonResultScreenProps {
  person: Personnel | null;
  scannedCode: string;
  session: GatekeeperSession;
  scannedLocation?: string;
  isLocationMismatch?: boolean;
  onConfirmAction: (
    person: Personnel,
    action: ActionDirection,
    stayDuration?: string,
    locationMismatch?: boolean,
    scannedLocation?: string
  ) => void;
  onScanAgain: () => void;
  onBack: () => void;
}

export const PersonResultScreen: React.FC<PersonResultScreenProps> = ({
  person,
  scannedCode,
  session,
  scannedLocation,
  isLocationMismatch,
  onConfirmAction,
  onScanAgain,
  onBack,
}) => {
  const [showConfirmSheet, setShowConfirmSheet] = useState<boolean>(false);
  const currentTime = formatCurrentTime();

  // Parse QR payload to detect embedded location ID and evaluate mismatch
  const parsedQr = parseScannedQr(scannedCode, session.location);
  const effectiveScannedLocation = scannedLocation || parsedQr.locationId;
  const hasLocationMismatch = isLocationMismatch !== undefined ? isLocationMismatch : parsedQr.isLocationMismatch;

  // If person is null / unrecognized
  if (!person) {
    return (
      <div className="flex flex-col flex-1 px-6 py-6 max-w-md mx-auto w-full select-none justify-between">
        <div className="flex items-center gap-2 pb-3 border-b border-zinc-200">
          <button
            onClick={onBack}
            className="p-1.5 -ml-1.5 rounded-lg text-zinc-600 hover:text-zinc-900 active:bg-zinc-100"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <span className="text-sm font-bold text-zinc-800">Scan Result</span>
        </div>

        <div className="flex flex-col items-center text-center my-auto px-4">
          <div className="w-16 h-16 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center border border-amber-200 mb-4">
            <AlertTriangle className="w-8 h-8 stroke-[1.75]" />
          </div>

          <h2 className="text-lg font-bold text-zinc-900 tracking-tight">
            QR Not Recognized
          </h2>
          <p className="text-xs text-zinc-500 mt-2 max-w-xs leading-relaxed">
            Scanned code <span className="font-mono font-bold text-zinc-700">{scannedCode}</span> could not be matched with any registered personnel.
          </p>

          <div className="mt-4 p-3 bg-zinc-50 border border-zinc-200 rounded-lg text-xs text-zinc-600 font-mono">
            Location: {session.location} • {session.gate}
          </div>
        </div>

        <div className="space-y-2">
          <button
            onClick={onScanAgain}
            className="w-full py-3.5 px-4 bg-zinc-900 hover:bg-black text-white font-semibold rounded-xl text-sm transition-all"
          >
            Scan Again
          </button>
          <button
            onClick={onBack}
            className="w-full py-3 px-4 bg-zinc-100 hover:bg-zinc-200 text-zinc-700 font-semibold rounded-xl text-sm transition-all"
          >
            Back to Home
          </button>
        </div>
      </div>
    );
  }

  const isSuspended = person.status === 'SUSPENDED';
  // Determine logical action based on currentStatus
  const targetAction: ActionDirection = person.currentStatus === 'INSIDE' ? 'EXIT' : 'ENTRY';
  
  // Dynamic duration calculation since last ENTRY action
  const [elapsedDuration, setElapsedDuration] = useState<string>(() =>
    computeDurationSinceEntry(person.lastEntryTimestamp, person.lastEntryTime)
  );

  useEffect(() => {
    if (person.currentStatus !== 'INSIDE') return;

    const update = () => {
      setElapsedDuration(computeDurationSinceEntry(person.lastEntryTimestamp, person.lastEntryTime));
    };

    update();
    const interval = setInterval(update, 10000); // live recalculation
    return () => clearInterval(interval);
  }, [person.currentStatus, person.lastEntryTimestamp, person.lastEntryTime]);

  const stayDuration = targetAction === 'EXIT' ? elapsedDuration : undefined;

  return (
    <div className="relative flex flex-col flex-1 px-5 py-4 max-w-md mx-auto w-full select-none justify-between overflow-y-auto">
      {/* Top Header */}
      <div>
        <div className="flex items-center justify-between pb-3 border-b border-zinc-200/80">
          <button
            onClick={onBack}
            className="flex items-center gap-1.5 p-1 -ml-1 text-zinc-600 hover:text-zinc-900 rounded-lg"
          >
            <ArrowLeft className="w-5 h-5" />
            <span className="text-xs font-semibold">Back</span>
          </button>
          <span className="text-xs font-mono uppercase tracking-wider text-zinc-400 font-bold">
            Person Identified
          </span>
          <div className="w-8" />
        </div>

        {/* Identity Credential Card */}
        <div className="mt-4 relative overflow-hidden bg-white border border-zinc-200/90 rounded-2xl shadow-sm">
          {/* Top Security Stripe */}
          <div className="h-1.5 w-full bg-gradient-to-r from-zinc-800 via-emerald-500 to-zinc-900" />

          <div className="p-5">
            <div className="flex items-start gap-4">
              {/* Avatar Photo with Security Frame */}
              <div className="relative w-20 h-20 rounded-xl overflow-hidden bg-zinc-100 border-2 border-zinc-200 shrink-0 shadow-inner">
                {person.photoUrl ? (
                  <img
                    src={person.photoUrl}
                    alt={person.name}
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center text-zinc-400">
                    <User className="w-9 h-9" />
                  </div>
                )}
                <div className="absolute bottom-0 inset-x-0 bg-black/60 backdrop-blur-xs py-0.5 text-center">
                  <span className="text-[9px] font-mono text-emerald-300 font-bold tracking-wider">
                    VERIFIED
                  </span>
                </div>
              </div>

              {/* Name & ID */}
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-mono text-xs font-bold text-zinc-900 bg-zinc-100 px-2 py-0.5 rounded border border-zinc-200">
                    {person.id}
                  </span>
                  <StatusChip status={person.status} size="sm" />
                  
                  {/* Visual tag in header if currently INSIDE */}
                  {person.currentStatus === 'INSIDE' && (
                    <span
                      id="person-header-inside-tag"
                      className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-mono font-bold bg-emerald-50 text-emerald-700 border border-emerald-300/80 shadow-2xs"
                      title="Duration since last entry"
                    >
                      <span className="relative flex h-1.5 w-1.5">
                        <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                        <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-emerald-500"></span>
                      </span>
                      <Clock className="w-3 h-3 text-emerald-600" />
                      <span>{elapsedDuration}</span>
                    </span>
                  )}
                </div>

                <h2 className="text-lg font-bold text-zinc-900 tracking-tight mt-1.5 truncate">
                  {person.name}
                </h2>
                <p className="text-xs font-semibold text-zinc-600 truncate">
                  {person.role}
                </p>
                <p className="text-[11px] text-zinc-400 truncate mt-0.5">
                  {person.department}
                </p>
              </div>
            </div>

            {/* Operational Details Grid */}
            <div className="mt-4 pt-4 border-t border-zinc-100 grid grid-cols-2 gap-3 text-xs">
              <div className="p-2 bg-zinc-50 rounded-lg border border-zinc-100">
                <span className="text-zinc-400 font-medium block text-[10px] uppercase tracking-wider">
                  Post Authorization
                </span>
                <span className="font-semibold text-zinc-800 flex items-center gap-1 mt-0.5">
                  <MapPin className="w-3 h-3 text-zinc-500" />
                  {session.location}
                </span>
              </div>

              <div className="p-2 bg-zinc-50 rounded-lg border border-zinc-100 flex flex-col justify-between">
                <span className="text-zinc-400 font-medium block text-[10px] uppercase tracking-wider">
                  Current Presence
                </span>
                <div className="mt-1 flex items-center gap-1.5 flex-wrap">
                  <StatusChip status={person.currentStatus} size="sm" />
                  {/* Presence visual tag */}
                  {person.currentStatus === 'INSIDE' && (
                    <span
                      id="person-presence-duration-tag"
                      className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-emerald-100 text-emerald-800 border border-emerald-300"
                    >
                      <Clock className="w-2.5 h-2.5 text-emerald-700" />
                      {elapsedDuration}
                    </span>
                  )}
                </div>
              </div>

              {/* Prominent Visual Tag & Active Facility Stay Card */}
              {person.currentStatus === 'INSIDE' && (
                <div
                  id="person-inside-duration-visual-tag"
                  className="col-span-2 bg-gradient-to-r from-emerald-50/90 via-emerald-50/60 to-teal-50/50 p-3.5 rounded-xl border border-emerald-300/80 shadow-2xs flex items-center justify-between"
                >
                  <div className="space-y-0.5">
                    <div className="flex items-center gap-2">
                      <span className="text-[10px] font-bold text-emerald-800 uppercase tracking-wider block">
                        Active Facility Stay
                      </span>
                      <span
                        id="person-inside-status-badge"
                        className="px-1.5 py-0.2 rounded-full bg-emerald-600 text-white text-[9px] font-mono font-bold tracking-wide flex items-center gap-1"
                      >
                        <span className="w-1.5 h-1.5 rounded-full bg-white animate-pulse" />
                        INSIDE
                      </span>
                    </div>
                    <span className="text-xs text-emerald-900 flex items-center gap-1 mt-0.5 font-medium">
                      <Clock className="w-3.5 h-3.5 text-emerald-700 shrink-0" />
                      {person.lastEntryTime ? `Entered today at ${person.lastEntryTime}` : 'Last action: ENTRY'}
                    </span>
                  </div>
                  <div className="text-right">
                    <span className="text-[10px] text-emerald-700 font-mono block font-semibold uppercase">
                      Duration Since Entry
                    </span>
                    <span
                      id="person-duration-since-entry-value"
                      className="font-mono font-bold text-sm text-emerald-950 bg-white/95 px-2.5 py-0.5 rounded-lg border border-emerald-300 shadow-2xs inline-block mt-0.5"
                    >
                      {elapsedDuration}
                    </span>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Location Mismatch Warning if QR location differs from session location */}
        {hasLocationMismatch && (
          <div
            id="location-mismatch-warning"
            className="mt-4 p-3.5 bg-amber-500/10 border-2 border-amber-500 rounded-2xl text-amber-950 shadow-xs animate-fade-in"
          >
            <div className="flex items-start gap-2.5">
              <div className="w-8 h-8 rounded-xl bg-amber-500 text-white flex items-center justify-center shrink-0 shadow-2xs">
                <AlertTriangle className="w-4 h-4 stroke-[2.5]" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <span className="font-bold text-xs uppercase tracking-wider text-amber-950 flex items-center gap-1.5">
                    Location Mismatch Warning
                  </span>
                  <span
                    id="location-mismatch-badge"
                    className="font-mono text-[9px] font-bold px-2 py-0.5 rounded-md bg-amber-200 text-amber-950 border border-amber-400 shadow-2xs"
                  >
                    LOCATION MISMATCH
                  </span>
                </div>
                <p className="text-[11px] text-amber-900 mt-1 leading-relaxed">
                  The scanned QR credential specifies a location ID that differs from this gate&apos;s active session.
                </p>

                {/* Location Comparison Grid */}
                <div className="mt-2.5 grid grid-cols-2 gap-2 text-[11px] font-mono">
                  <div className="p-2 bg-white/95 rounded-lg border border-amber-200/80 shadow-2xs">
                    <span className="text-[9px] uppercase tracking-wider text-zinc-500 font-bold block">
                      Active Terminal
                    </span>
                    <span className="font-bold text-zinc-900 mt-0.5 flex items-center gap-1">
                      <MapPin className="w-3 h-3 text-emerald-600 shrink-0" />
                      {session.location}
                    </span>
                  </div>
                  <div className="p-2 bg-amber-100/90 rounded-lg border border-amber-400 shadow-2xs">
                    <span className="text-[9px] uppercase tracking-wider text-amber-800 font-bold block">
                      Scanned QR Location
                    </span>
                    <span className="font-bold text-amber-950 mt-0.5 flex items-center gap-1">
                      <AlertOctagon className="w-3 h-3 text-amber-700 shrink-0" />
                      {effectiveScannedLocation || 'Unknown'}
                    </span>
                  </div>
                </div>

                <p className="text-[10px] text-amber-800/90 mt-2 font-medium">
                  Verify authorization before logging cross-station entry or exit.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Suspended Notice if applicable */}
        {isSuspended && (
          <div className="mt-4 p-3.5 bg-rose-50 border border-rose-200 rounded-xl text-rose-800 text-xs flex items-start gap-2.5">
            <AlertTriangle className="w-4 h-4 text-rose-600 shrink-0 mt-0.5" />
            <div>
              <span className="font-bold">Access Denied: Account Suspended</span>
              <p className="text-rose-700 text-[11px] mt-0.5">
                Security flag active. Direct individual to the central security desk.
              </p>
            </div>
          </div>
        )}
      </div>

      {/* Primary Action Button */}
      <div className="mt-6 space-y-2.5">
        {isSuspended ? (
          <button
            onClick={onScanAgain}
            className="w-full py-3.5 px-4 bg-zinc-900 hover:bg-black text-white font-semibold rounded-xl text-sm transition-all"
          >
            Scan Next Person
          </button>
        ) : (
          <button
            onClick={() => setShowConfirmSheet(true)}
            className={`w-full py-4 px-4 font-bold rounded-xl text-sm shadow-sm transition-all flex items-center justify-center gap-2 cursor-pointer ${
              targetAction === 'ENTRY'
                ? 'bg-zinc-900 hover:bg-black text-white'
                : 'bg-zinc-800 hover:bg-zinc-900 text-white'
            }`}
          >
            <ShieldCheck className="w-4 h-4" />
            <span>
              {targetAction === 'ENTRY' ? 'RECORD ENTRY' : 'RECORD EXIT'}
            </span>
          </button>
        )}

        <button
          onClick={onScanAgain}
          className="w-full py-2.5 px-4 text-xs font-semibold text-zinc-500 hover:text-zinc-800 transition-colors"
        >
          Cancel & Scan Again
        </button>
      </div>

      {/* Bottom Sheet: Entry/Exit Confirmation */}
      {showConfirmSheet && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/60 backdrop-blur-xs p-0 sm:p-4">
          <div className="w-full max-w-md bg-white rounded-t-3xl sm:rounded-2xl p-6 border-t sm:border border-zinc-200 shadow-2xl animate-fade-in">
            <div className="w-10 h-1 bg-zinc-300 rounded-full mx-auto mb-4" />

            <span className="text-[11px] font-mono uppercase tracking-wider text-zinc-400 font-bold block text-center">
              Confirmation Required
            </span>
            <h3 className="text-lg font-bold text-zinc-900 text-center tracking-tight mt-0.5">
              CONFIRM {targetAction}
            </h3>

            {/* Structured review parameters */}
            <div className="mt-5 p-4 bg-zinc-50 border border-zinc-200 rounded-xl space-y-2.5 text-xs">
              <div className="flex justify-between items-center pb-2 border-b border-zinc-200">
                <span className="text-zinc-500">Personnel</span>
                <span className="font-bold text-zinc-900">{person.name} ({person.id})</span>
              </div>
              <div className="flex justify-between items-center pb-2 border-b border-zinc-200">
                <span className="text-zinc-500">Location / Gate</span>
                <span className="font-semibold text-zinc-800">{session.location} • {session.gate}</span>
              </div>
              <div className="flex justify-between items-center pb-2 border-b border-zinc-200">
                <span className="text-zinc-500">Timestamp</span>
                <span className="font-mono font-bold text-zinc-900">{currentTime}</span>
              </div>
              {stayDuration && (
                <div className="flex justify-between items-center">
                  <span className="text-zinc-500">Duration Since Entry</span>
                  <span
                    id="confirm-stay-duration-tag"
                    className="font-mono font-bold text-emerald-700 bg-emerald-50 px-2.5 py-0.5 rounded-md border border-emerald-200 text-xs flex items-center gap-1 shadow-2xs"
                  >
                    <Clock className="w-3.5 h-3.5 text-emerald-600" />
                    {stayDuration}
                  </span>
                </div>
              )}

              {/* Location Mismatch Flag in Confirmation Sheet */}
              {hasLocationMismatch && (
                <div
                  id="confirm-location-mismatch-banner"
                  className="pt-2 mt-1 border-t border-amber-200 text-xs"
                >
                  <div className="p-2.5 bg-amber-100/80 border border-amber-300 rounded-lg flex items-start gap-2 text-amber-950">
                    <AlertTriangle className="w-4 h-4 text-amber-700 shrink-0 mt-0.5" />
                    <div className="space-y-0.5 min-w-0">
                      <div className="font-bold flex items-center justify-between gap-1">
                        <span>Location Mismatch Flagged</span>
                        <span className="text-[9px] font-mono px-1.5 py-0.2 bg-amber-200 text-amber-900 rounded font-bold">
                          WARNING
                        </span>
                      </div>
                      <p className="text-[11px] text-amber-900">
                        Scanned QR specifies <span className="font-mono font-bold">{effectiveScannedLocation}</span>, differing from current station <span className="font-mono font-bold">{session.location}</span>.
                      </p>
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="grid grid-cols-2 gap-3 mt-6">
              <button
                onClick={() => setShowConfirmSheet(false)}
                className="py-3 px-4 rounded-xl bg-zinc-100 hover:bg-zinc-200 text-zinc-700 text-xs font-semibold transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  setShowConfirmSheet(false);
                  onConfirmAction(person, targetAction, stayDuration, hasLocationMismatch, effectiveScannedLocation);
                }}
                className="py-3 px-4 rounded-xl bg-zinc-900 hover:bg-black text-white text-xs font-bold transition-colors flex items-center justify-center gap-1.5"
              >
                <CheckCircle2 className="w-4 h-4 text-emerald-400" />
                <span>Confirm {targetAction}</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
