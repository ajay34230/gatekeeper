import React, { useState } from 'react';
import {
  X,
  FileCheck2,
  Clock,
  ArrowDownLeft,
  ArrowUpRight,
  ShieldCheck,
  CheckCircle2,
  AlertTriangle,
  Download,
  Users,
  Truck,
  Printer,
  FileText,
  UserCheck,
} from 'lucide-react';
import { GatekeeperSession, ActivityRecord, Personnel, Vehicle, SyncStatusData } from '../../types';
import { downloadShiftHandoverReportCsv } from '../../utils/csvExport';

interface ShiftHandoverModalProps {
  isOpen: boolean;
  onClose: () => void;
  session: GatekeeperSession;
  syncState: SyncStatusData;
  activities: ActivityRecord[];
  personnelList: Personnel[];
  vehicleList: Vehicle[];
  onConfirmHandover?: (handoverNotes: string, relievingOfficer: string) => void;
}

export const ShiftHandoverModal: React.FC<ShiftHandoverModalProps> = ({
  isOpen,
  onClose,
  session,
  syncState,
  activities,
  personnelList,
  vehicleList,
  onConfirmHandover,
}) => {
  const [relievingOfficer, setRelievingOfficer] = useState('');
  const [handoverNotes, setHandoverNotes] = useState('');
  const [isCopied, setIsCopied] = useState(false);
  const [hasExported, setHasExported] = useState(false);
  const [isHandoverFinished, setIsHandoverFinished] = useState(false);

  if (!isOpen) return null;

  // Filter activities to current shift or active activities
  const shiftActivities = activities;
  const entriesCount = shiftActivities.filter((a) => a.action === 'ENTRY').length;
  const exitsCount = shiftActivities.filter((a) => a.action === 'EXIT').length;
  const flaggedCount = shiftActivities.filter((a) => a.locationMismatch).length;
  const pendingSyncCount = shiftActivities.filter((a) => !a.synced).length;

  const insidePersonnelCount = personnelList.filter((p) => p.currentStatus === 'INSIDE').length;
  const insideVehicleCount = vehicleList.filter((v) => v.currentStatus === 'INSIDE').length;

  const shiftDateStr = new Date().toLocaleDateString('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
  const shiftTimeNow = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  const handleExportCsv = () => {
    downloadShiftHandoverReportCsv({
      session,
      entriesCount,
      exitsCount,
      flaggedCount,
      insidePersonnelCount,
      insideVehicleCount,
      pendingSyncCount,
      handoverNotes,
      relievingOfficer,
      activities: shiftActivities,
    });
    setHasExported(true);
    setTimeout(() => setHasExported(false), 3000);
  };

  const handlePrint = () => {
    window.print();
  };

  const handleCompleteHandover = () => {
    setIsHandoverFinished(true);
    if (onConfirmHandover) {
      onConfirmHandover(handoverNotes, relievingOfficer);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-black/75 backdrop-blur-xs animate-in fade-in duration-200">
      <div className="relative w-full max-w-lg bg-white rounded-3xl shadow-2xl border border-zinc-200 overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="p-4 bg-zinc-900 text-white flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-zinc-800 border border-zinc-700 flex items-center justify-center text-amber-400">
              <FileCheck2 className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-sm font-bold tracking-tight">Shift Handover Dossier</span>
                <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-400/20 text-amber-300 border border-amber-400/30">
                  OFFICIAL
                </span>
              </div>
              <p className="text-[11px] text-zinc-400">
                {session.location} • {session.gate} • Operator {session.name} ({session.id})
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full bg-zinc-800 hover:bg-zinc-700 text-zinc-400 hover:text-white flex items-center justify-center transition-colors cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-4 space-y-4 text-xs">
          {/* Shift Metadata Banner */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
            <div className="p-2.5 bg-zinc-50 border border-zinc-200 rounded-xl">
              <span className="text-[10px] text-zinc-400 font-semibold block uppercase tracking-wider">Date</span>
              <span className="font-mono font-bold text-zinc-800 text-xs">{shiftDateStr}</span>
            </div>
            <div className="p-2.5 bg-zinc-50 border border-zinc-200 rounded-xl">
              <span className="text-[10px] text-zinc-400 font-semibold block uppercase tracking-wider">Shift Start</span>
              <span className="font-mono font-bold text-zinc-800 text-xs">{session.shiftStartTime}</span>
            </div>
            <div className="p-2.5 bg-zinc-50 border border-zinc-200 rounded-xl">
              <span className="text-[10px] text-zinc-400 font-semibold block uppercase tracking-wider">Handover Time</span>
              <span className="font-mono font-bold text-zinc-800 text-xs">{shiftTimeNow}</span>
            </div>
            <div className="p-2.5 bg-zinc-50 border border-zinc-200 rounded-xl">
              <span className="text-[10px] text-zinc-400 font-semibold block uppercase tracking-wider">Sync State</span>
              <span className={`font-bold text-xs ${pendingSyncCount === 0 ? 'text-emerald-700' : 'text-amber-700'}`}>
                {pendingSyncCount === 0 ? 'Clean (0 queued)' : `${pendingSyncCount} Pending`}
              </span>
            </div>
          </div>

          {/* Quantitative Performance Grid */}
          <div className="p-3.5 bg-zinc-900 text-white rounded-2xl shadow-xs">
            <span className="text-[10px] font-bold text-zinc-400 uppercase tracking-wider block mb-2.5">
              Shift Movement Ledger Summary
            </span>
            <div className="grid grid-cols-3 gap-2 text-center">
              <div className="p-2.5 bg-zinc-800/90 rounded-xl border border-zinc-700/60">
                <span className="text-zinc-400 text-[10px] block font-medium flex items-center justify-center gap-1">
                  <ArrowDownLeft className="w-3 h-3 text-emerald-400" />
                  Entries
                </span>
                <span className="text-xl font-mono font-bold text-emerald-400 mt-0.5 block">{entriesCount}</span>
                <span className="text-[9px] text-zinc-400">Total in-gate</span>
              </div>
              <div className="p-2.5 bg-zinc-800/90 rounded-xl border border-zinc-700/60">
                <span className="text-zinc-400 text-[10px] block font-medium flex items-center justify-center gap-1">
                  <ArrowUpRight className="w-3 h-3 text-amber-400" />
                  Exits
                </span>
                <span className="text-xl font-mono font-bold text-amber-400 mt-0.5 block">{exitsCount}</span>
                <span className="text-[9px] text-zinc-400">Cleared out</span>
              </div>
              <div className="p-2.5 bg-zinc-800/90 rounded-xl border border-zinc-700/60">
                <span className="text-zinc-400 text-[10px] block font-medium flex items-center justify-center gap-1">
                  <AlertTriangle className="w-3 h-3 text-rose-400" />
                  Alerts / Mismatch
                </span>
                <span className="text-xl font-mono font-bold text-rose-400 mt-0.5 block">{flaggedCount}</span>
                <span className="text-[9px] text-zinc-400">Gate exceptions</span>
              </div>
            </div>
          </div>

          {/* On-Site Presence at Handover Time */}
          <div className="p-3 bg-zinc-50 border border-zinc-200 rounded-xl space-y-2">
            <span className="text-[11px] font-bold text-zinc-800 uppercase tracking-wider block">
              Garrison State at Time of Handover
            </span>
            <div className="grid grid-cols-2 gap-2 text-xs">
              <div className="flex items-center justify-between p-2.5 bg-white rounded-lg border border-zinc-200">
                <div className="flex items-center gap-2">
                  <Users className="w-4 h-4 text-emerald-600" />
                  <span className="font-medium text-zinc-700">Personnel Inside</span>
                </div>
                <span className="font-mono font-bold text-zinc-900 bg-emerald-50 px-2 py-0.5 rounded text-xs border border-emerald-200">
                  {insidePersonnelCount}
                </span>
              </div>
              <div className="flex items-center justify-between p-2.5 bg-white rounded-lg border border-zinc-200">
                <div className="flex items-center gap-2">
                  <Truck className="w-4 h-4 text-amber-600" />
                  <span className="font-medium text-zinc-700">Vehicles In Yard</span>
                </div>
                <span className="font-mono font-bold text-zinc-900 bg-amber-50 px-2 py-0.5 rounded text-xs border border-amber-200">
                  {insideVehicleCount}
                </span>
              </div>
            </div>
          </div>

          {/* Relieving Officer & Handover Log Notes */}
          <div className="space-y-3">
            <div>
              <label className="text-[11px] font-bold text-zinc-800 uppercase tracking-wider block mb-1">
                Relieving Officer / Incoming Gatekeeper
              </label>
              <div className="relative">
                <UserCheck className="w-4 h-4 text-zinc-400 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  value={relievingOfficer}
                  onChange={(e) => setRelievingOfficer(e.target.value)}
                  placeholder="e.g. Sgt. Marcus Flint (GK-09)"
                  className="w-full pl-9 pr-3 py-2 bg-white border border-zinc-300 rounded-xl text-xs text-zinc-900 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-800"
                />
              </div>
            </div>

            <div>
              <label className="text-[11px] font-bold text-zinc-800 uppercase tracking-wider block mb-1">
                Security & Facility Notes for Incoming Shift
              </label>
              <textarea
                value={handoverNotes}
                onChange={(e) => setHandoverNotes(e.target.value)}
                rows={3}
                placeholder="Log any pending contractor clearances, barrier inspection notes, or convoy notices..."
                className="w-full p-2.5 bg-white border border-zinc-300 rounded-xl text-xs text-zinc-900 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-800 resize-none font-medium"
              />
            </div>
          </div>

          {/* Handover Completed Confirmation Notice */}
          {isHandoverFinished && (
            <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl flex items-center gap-2.5 text-emerald-800">
              <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
              <div>
                <p className="font-bold text-xs">Shift Handover Completed</p>
                <p className="text-[11px] text-emerald-700">
                  Dossier signed off. Terminal ready for relieving operator authentication.
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="p-3.5 bg-zinc-100 border-t border-zinc-200 flex items-center justify-between gap-2 shrink-0">
          <div className="flex items-center gap-1.5">
            <button
              onClick={handleExportCsv}
              className="flex items-center gap-1 px-3 py-2 bg-white hover:bg-zinc-50 border border-zinc-300 text-zinc-700 font-bold rounded-xl text-xs transition-colors cursor-pointer shadow-2xs"
              title="Download formal shift audit dossier (CSV)"
            >
              <Download className="w-3.5 h-3.5 text-emerald-600" />
              <span>{hasExported ? 'Exported!' : 'Export CSV'}</span>
            </button>
            <button
              onClick={handlePrint}
              className="hidden sm:flex items-center gap-1 px-3 py-2 bg-white hover:bg-zinc-50 border border-zinc-300 text-zinc-700 font-bold rounded-xl text-xs transition-colors cursor-pointer shadow-2xs"
              title="Print Dossier"
            >
              <Printer className="w-3.5 h-3.5 text-zinc-600" />
              <span>Print</span>
            </button>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={onClose}
              className="px-3 py-2 bg-zinc-200 hover:bg-zinc-300 text-zinc-800 font-bold rounded-xl text-xs cursor-pointer transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleCompleteHandover}
              className="flex items-center gap-1.5 px-4 py-2 bg-zinc-900 hover:bg-zinc-800 text-white font-bold rounded-xl text-xs cursor-pointer transition-colors shadow-xs"
            >
              <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
              <span>Confirm Shift Handover</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
