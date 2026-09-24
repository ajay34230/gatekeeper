import React from 'react';
import { Check, Clock, CheckCircle2, ArrowRight, ShieldCheck, Home, AlertTriangle } from 'lucide-react';
import { ActionDirection, ActivityRecord } from '../types';

interface SuccessScreenProps {
  activity: ActivityRecord;
  onScanNext: () => void;
  onGoHome: () => void;
}

export const SuccessScreen: React.FC<SuccessScreenProps> = ({
  activity,
  onScanNext,
  onGoHome,
}) => {
  const isExit = activity.action === 'EXIT';
  const isVehicle = activity.type === 'VEHICLE';

  return (
    <div className="flex flex-col flex-1 px-5 py-6 max-w-md mx-auto w-full select-none justify-between overflow-y-auto">
      {/* Top Success Badge with Concentric Ripple */}
      <div className="flex flex-col items-center text-center mt-2">
        <div className="relative mb-3">
          <div className="w-18 h-18 rounded-full bg-emerald-500/10 border-2 border-emerald-400 flex items-center justify-center text-emerald-600 shadow-sm animate-fade-in">
            <Check className="w-9 h-9 stroke-[2.75]" />
          </div>
          <div className="absolute inset-0 rounded-full border border-emerald-400/40 animate-ping opacity-60 pointer-events-none" />
        </div>

        <div className="flex items-center gap-1.5 px-3 py-0.5 rounded-full bg-emerald-50 border border-emerald-200">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
          <span className="text-[11px] font-mono uppercase tracking-wider text-emerald-700 font-bold">
            {activity.synced ? 'CLOUD RECORD LOGGED' : 'LOCAL BUFFER STORED'}
          </span>
        </div>

        <h1 className="text-2xl font-bold text-zinc-900 tracking-tight mt-2">
          {activity.action} CONFIRMED
        </h1>

        <p className="text-xs text-zinc-500 mt-0.5 font-medium">
          XV DIGITAL ACCESS CONTROL audit record generated
        </p>
      </div>

      {/* Main Digital Pass Receipt Card */}
      <div className="my-auto py-3">
        <div className="relative bg-white border border-zinc-200/90 rounded-2xl shadow-sm overflow-hidden text-xs">
          {/* Top colored strip */}
          <div className={`h-1.5 w-full ${isExit ? 'bg-amber-500' : 'bg-emerald-500'}`} />

          <div className="p-5 space-y-3.5">
            {/* Identity Row */}
            <div className="pb-3 border-b border-zinc-100 flex items-center justify-between">
              <div>
                <span className="text-[10px] text-zinc-400 uppercase font-bold tracking-wider block">
                  {isVehicle ? 'Vehicle Unit' : 'Personnel Record'}
                </span>
                <span className="text-base font-bold text-zinc-900 mt-0.5 block">
                  {activity.title}
                </span>
                <span className="text-xs font-mono text-zinc-500">
                  {activity.subtitle}
                </span>
              </div>
              <span className="font-mono text-xs font-bold px-2.5 py-1 bg-zinc-100 border border-zinc-200 text-zinc-800 rounded-lg shadow-2xs">
                {activity.targetId}
              </span>
            </div>

            {/* Location & Gate */}
            <div className="grid grid-cols-2 gap-2 text-zinc-600">
              <div className="p-2 bg-zinc-50 rounded-lg border border-zinc-100">
                <span className="text-zinc-400 block text-[10px] uppercase tracking-wider">Facility Post</span>
                <span className="font-semibold text-zinc-900">{activity.location}</span>
              </div>
              <div className="p-2 bg-zinc-50 rounded-lg border border-zinc-100">
                <span className="text-zinc-400 block text-[10px] uppercase tracking-wider">Access Point</span>
                <span className="font-semibold text-zinc-900">{activity.gate}</span>
              </div>
            </div>

            {/* Location Mismatch Flag if applicable */}
            {activity.locationMismatch && (
              <div
                id="success-location-mismatch-banner"
                className="p-3 bg-amber-50 border border-amber-300 rounded-xl space-y-1"
              >
                <div className="flex items-center gap-1.5 text-xs text-amber-900 font-bold">
                  <AlertTriangle className="w-3.5 h-3.5 text-amber-600 shrink-0" />
                  <span>Cross-Location Flag Recorded</span>
                </div>
                <p className="text-[11px] text-amber-800">
                  Scanned QR indicated <span className="font-mono font-bold">{activity.scannedLocation || 'different location'}</span>, logged at {activity.location}.
                </p>
              </div>
            )}

            {/* Exit Specific Duration Block */}
            {isExit && activity.stayDuration && (
              <div className="p-3 bg-amber-50/80 border border-amber-200/80 rounded-xl space-y-1">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-amber-900 font-bold flex items-center gap-1.5">
                    <Clock className="w-3.5 h-3.5 text-amber-700" />
                    Total Verified Stay
                  </span>
                  <span className="font-mono font-bold text-base text-amber-950">
                    {activity.stayDuration}
                  </span>
                </div>
              </div>
            )}

            {/* Vehicle Manifest Summary if applicable */}
            {isVehicle && activity.vehicleManifest && (
              <div className="p-3 bg-zinc-50 border border-zinc-200/80 rounded-xl space-y-1 text-xs">
                <div className="flex justify-between">
                  <span className="text-zinc-500 font-medium">Verified Driver:</span>
                  <span className="font-bold text-zinc-900">
                    {activity.vehicleManifest.driver.name} ({activity.vehicleManifest.driver.id})
                  </span>
                </div>
                {activity.vehicleManifest.coDriver && (
                  <div className="flex justify-between">
                    <span className="text-zinc-500 font-medium">Co-driver:</span>
                    <span className="font-semibold text-zinc-800">
                      {activity.vehicleManifest.coDriver.name} ({activity.vehicleManifest.coDriver.id})
                    </span>
                  </div>
                )}
                <div className="flex justify-between text-zinc-500 pt-1 border-t border-zinc-200">
                  <span>Occupant Manifest:</span>
                  <span className="font-mono font-bold text-zinc-900">
                    {(activity.vehicleManifest.coDriver ? 2 : 1) + activity.vehicleManifest.occupants.length} persons total
                  </span>
                </div>
              </div>
            )}

            {/* Timestamp & Sync Status */}
            <div className="pt-2 border-t border-zinc-100 flex items-center justify-between text-zinc-500">
              <span className="font-mono text-zinc-700 font-semibold">{activity.timestamp}</span>
              <div className="flex items-center gap-1.5 text-emerald-600 font-bold text-[11px]">
                <CheckCircle2 className="w-3.5 h-3.5" />
                <span>{activity.synced ? 'Synchronized' : 'Buffered Offline'}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Rapid Return Actions */}
      <div className="space-y-2.5">
        <button
          onClick={onScanNext}
          className="w-full py-4 px-4 bg-zinc-900 hover:bg-black active:scale-[0.99] text-white font-bold rounded-xl text-sm shadow-sm transition-all flex items-center justify-center gap-2 cursor-pointer"
        >
          <span>SCAN NEXT TARGET</span>
          <ArrowRight className="w-4 h-4" />
        </button>

        <button
          onClick={onGoHome}
          className="w-full py-3 px-4 bg-white hover:bg-zinc-50 active:bg-zinc-100 border border-zinc-200/90 text-zinc-700 font-semibold rounded-xl text-xs transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-2xs"
        >
          <Home className="w-4 h-4 text-zinc-500" />
          <span>Return to Terminal Home</span>
        </button>
      </div>
    </div>
  );
};
