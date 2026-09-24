import React, { useEffect, useState } from 'react';
import {
  X,
  User,
  Truck,
  Shield,
  Clock,
  MapPin,
  Calendar,
  Award,
  ChevronRight,
  AlertTriangle,
  CheckCircle2,
  RefreshCw,
  BarChart2,
  History,
  ArrowRight,
  Building2,
} from 'lucide-react';

interface IndividualHistoryModalProps {
  targetId: string;
  onClose: () => void;
}

export const IndividualHistoryModal: React.FC<IndividualHistoryModalProps> = ({
  targetId,
  onClose,
}) => {
  const [data, setData] = useState<any>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchDossier = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await fetch(`/api/individuals/${encodeURIComponent(targetId)}`);
      if (!res.ok) {
        throw new Error(`Failed to load military record for "${targetId}"`);
      }
      const json = await res.json();
      if (json.status === 'ok') {
        setData(json.individual);
      } else {
        throw new Error(json.message || 'Unknown server error');
      }
    } catch (err: any) {
      setError(err.message || 'Failed to fetch individual dossier');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (targetId) {
      fetchDossier();
    }
  }, [targetId]);

  const profile = data?.profile;
  const isPerson = data?.isPerson !== false;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-2.5 sm:p-4 bg-black/85 backdrop-blur-md animate-in fade-in duration-200">
      <div className="relative flex flex-col w-full max-w-4xl max-h-[94vh] bg-zinc-900 border border-zinc-700/90 rounded-2xl shadow-2xl text-zinc-100 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-zinc-800 bg-zinc-950/90 shrink-0">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-400">
              <Shield className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[10px] font-mono uppercase tracking-wider px-2 py-0.5 rounded bg-emerald-950/90 text-emerald-300 border border-emerald-800/80 font-semibold">
                  CONFIDENTIAL • ARMY REGISTRY
                </span>
                <span className="text-xs text-zinc-400 font-mono">ID: {targetId}</span>
              </div>
              <h2 className="text-base font-bold text-zinc-100 mt-0.5">
                {profile?.fullName || (profile ? `${profile.rank} ${profile.fullName}` : targetId)}
              </h2>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={fetchDossier}
              disabled={isLoading}
              className="p-2 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors cursor-pointer"
              title="Refresh military stay record"
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
            <button
              onClick={onClose}
              className="p-2 rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors cursor-pointer"
              aria-label="Close dossier"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Content Body */}
        <div className="flex-1 overflow-y-auto p-5 space-y-5">
          {isLoading ? (
            <div className="py-20 text-center space-y-3">
              <RefreshCw className="w-8 h-8 animate-spin text-emerald-400 mx-auto" />
              <p className="text-xs text-zinc-400 font-mono">
                Querying military registry and computing location stay durations...
              </p>
            </div>
          ) : error ? (
            <div className="p-6 bg-red-950/30 border border-red-800/60 rounded-xl text-center space-y-2">
              <AlertTriangle className="w-8 h-8 text-red-400 mx-auto" />
              <p className="text-sm font-semibold text-red-200">Unable to retrieve military record</p>
              <p className="text-xs text-red-300/80">{error}</p>
              <button
                onClick={fetchDossier}
                className="px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded-lg text-xs transition-colors cursor-pointer mt-2"
              >
                Retry Request
              </button>
            </div>
          ) : data ? (
            <>
              {/* Profile Overview Card */}
              <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
                <div className="flex items-center gap-4">
                  {profile?.photoUrl ? (
                    <img
                      src={profile.photoUrl}
                      alt={profile.fullName || targetId}
                      className="w-16 h-16 rounded-xl object-cover border border-zinc-700 shadow-md shrink-0"
                    />
                  ) : (
                    <div className="w-16 h-16 rounded-xl bg-zinc-800 border border-zinc-700 flex items-center justify-center shrink-0">
                      {isPerson ? (
                        <User className="w-8 h-8 text-zinc-400" />
                      ) : (
                        <Truck className="w-8 h-8 text-zinc-400" />
                      )}
                    </div>
                  )}

                  <div className="space-y-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      <span className="text-sm font-bold text-zinc-100">
                        {isPerson
                          ? `${profile?.rank || 'Personnel'} ${profile?.fullName || targetId}`
                          : profile?.id || targetId}
                      </span>
                      {profile?.securityClearance && (
                        <span className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-red-950/80 text-red-300 border border-red-800">
                          {profile.securityClearance}
                        </span>
                      )}
                      <span
                        className={`text-[10px] font-mono px-2 py-0.5 rounded-full flex items-center gap-1 ${
                          data.currentStatus === 'INSIDE'
                            ? 'bg-emerald-950/90 text-emerald-300 border border-emerald-800'
                            : 'bg-zinc-800 text-zinc-400 border border-zinc-700'
                        }`}
                      >
                        <span
                          className={`w-1.5 h-1.5 rounded-full ${
                            data.currentStatus === 'INSIDE'
                              ? 'bg-emerald-400 animate-pulse'
                              : 'bg-zinc-500'
                          }`}
                        />
                        {data.currentStatus === 'INSIDE'
                          ? `PRESENT AT ${data.currentLocation || 'BASE'}`
                          : 'OFF-SITE / OUTSIDE'}
                      </span>
                    </div>

                    <div className="text-xs text-zinc-400 flex flex-wrap gap-x-3 gap-y-0.5">
                      {isPerson ? (
                        <>
                          <span>
                            Army No:{' '}
                            <strong className="text-zinc-200 font-mono">
                              {profile?.serviceNumber || 'ARMY-UNRESOLVED'}
                            </strong>
                          </span>
                          <span>•</span>
                          <span>
                            Unit: <strong className="text-zinc-200">{profile?.unit || 'N/A'}</strong>
                          </span>
                          <span>•</span>
                          <span>
                            Corps:{' '}
                            <strong className="text-zinc-200">{profile?.corps || 'General'}</strong>
                          </span>
                          {profile?.bloodGroup && (
                            <>
                              <span>•</span>
                              <span>
                                Blood:{' '}
                                <strong className="text-zinc-200 font-mono">
                                  {profile.bloodGroup}
                                </strong>
                              </span>
                            </>
                          )}
                        </>
                      ) : (
                        <>
                          <span>
                            Mil Reg No:{' '}
                            <strong className="text-zinc-200 font-mono">
                              {profile?.militaryRegistrationNumber || 'N/A'}
                            </strong>
                          </span>
                          <span>•</span>
                          <span>
                            Type: <strong className="text-zinc-200">{profile?.vehicleType || 'Fleet'}</strong>
                          </span>
                          <span>•</span>
                          <span>
                            Unit Assigned:{' '}
                            <strong className="text-zinc-200">{profile?.unitAssigned || 'N/A'}</strong>
                          </span>
                        </>
                      )}
                    </div>
                  </div>
                </div>

                <div className="text-left sm:text-right shrink-0 border-t sm:border-t-0 border-zinc-800 pt-2 sm:pt-0 w-full sm:w-auto">
                  <div className="text-[10px] text-zinc-400 font-mono uppercase">Secret QR Code Reference</div>
                  <div className="text-xs font-mono font-bold text-emerald-400">
                    {profile?.secretCode || targetId}
                  </div>
                  <div className="text-[10px] text-zinc-500 mt-0.5">
                    Server verified & auto-enriched
                  </div>
                </div>
              </div>

              {/* Core Stay Duration Analytics Grid */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                {/* 1. Cumulative Stay Duration */}
                <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
                  <div className="flex items-center justify-between text-zinc-400 text-xs">
                    <span className="flex items-center gap-1.5">
                      <Clock className="w-3.5 h-3.5 text-blue-400" />
                      Cumulative Time Stayed
                    </span>
                  </div>
                  <div className="text-base font-bold font-mono text-zinc-100">
                    {data.cumulativeStayFormatted || '0y 0m 0d 0h 0m'}
                  </div>
                  <p className="text-[11px] text-zinc-400">
                    {data.cumulativeStayHumanReadable || 'No stay time recorded yet'}
                  </p>
                </div>

                {/* 2. Active Stay Duration (if currently inside) */}
                <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
                  <div className="flex items-center justify-between text-zinc-400 text-xs">
                    <span className="flex items-center gap-1.5">
                      <MapPin className="w-3.5 h-3.5 text-emerald-400" />
                      Current Stay Status
                    </span>
                  </div>
                  <div className="text-base font-bold font-mono text-emerald-400">
                    {data.currentStatus === 'INSIDE'
                      ? data.activeStayDuration || '0y 0m 0d 0h 0m'
                      : 'Outside Base'}
                  </div>
                  <p className="text-[11px] text-zinc-400">
                    {data.currentStatus === 'INSIDE'
                      ? `Active at ${data.currentLocation} (${data.activeStayHumanReadable || 'present'})`
                      : 'Not currently logged inside gate'}
                  </p>
                </div>

                {/* 3. How Long Before He Came There (Prior interval) */}
                <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
                  <div className="flex items-center justify-between text-zinc-400 text-xs">
                    <span className="flex items-center gap-1.5">
                      <History className="w-3.5 h-3.5 text-amber-400" />
                      How Long Before He Came
                    </span>
                  </div>
                  <div className="text-base font-bold text-zinc-200">
                    {data.staySessions[0]?.timeBeforeVisitFormatted || 'First recorded visit'}
                  </div>
                  <p className="text-[11px] text-zinc-500">
                    Interval since previous base entry
                  </p>
                </div>

                {/* 4. Total Visits / Movements */}
                <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
                  <div className="flex items-center justify-between text-zinc-400 text-xs">
                    <span className="flex items-center gap-1.5">
                      <Award className="w-3.5 h-3.5 text-purple-400" />
                      Total Visits Logged
                    </span>
                  </div>
                  <div className="text-base font-bold font-mono text-zinc-100">
                    {data.totalRecordedVisits} Visits
                  </div>
                  <p className="text-[11px] text-zinc-500">
                    Across {data.locationStayBreakdown?.length || 0} Army military installations
                  </p>
                </div>
              </div>

              {/* Time Stayed Per Location Section */}
              <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-xs font-semibold text-zinc-100 flex items-center gap-1.5">
                      <Building2 className="w-4 h-4 text-emerald-400" />
                      Time Stayed Per Military Location
                    </h3>
                    <p className="text-[11px] text-zinc-400">
                      Detailed breakdown of hours, days, months, and years spent at each specific army post
                    </p>
                  </div>
                  <span className="text-[10px] font-mono text-zinc-500">
                    {data.locationStayBreakdown?.length || 0} Locations
                  </span>
                </div>

                {data.locationStayBreakdown?.length === 0 ? (
                  <div className="p-4 text-center text-xs text-zinc-500 bg-zinc-900/40 rounded-lg">
                    No completed stay intervals recorded yet for individual locations.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {data.locationStayBreakdown.map((loc: any) => {
                      const totalMs = data.locationStayBreakdown.reduce(
                        (acc: number, item: any) => acc + (item.totalMs || 0),
                        0
                      );
                      const pct = totalMs > 0 ? Math.round((loc.totalMs / totalMs) * 100) : 0;

                      return (
                        <div
                          key={loc.location}
                          className="p-3 bg-zinc-900/80 border border-zinc-800 rounded-lg space-y-1.5"
                        >
                          <div className="flex items-center justify-between text-xs">
                            <div className="flex items-center gap-2">
                              <span className="font-semibold text-zinc-200">{loc.location}</span>
                              <span className="text-[10px] font-mono px-1.5 py-0.2 rounded bg-zinc-800 text-zinc-400">
                                {loc.visitCount} {loc.visitCount === 1 ? 'visit' : 'visits'}
                              </span>
                            </div>
                            <div className="text-right font-mono font-bold text-emerald-400">
                              {loc.durationFormatted}
                            </div>
                          </div>

                          {/* Progress bar */}
                          <div className="flex items-center gap-3">
                            <div className="flex-1 bg-zinc-800 h-1.5 rounded-full overflow-hidden">
                              <div
                                className="bg-emerald-400 h-full rounded-full"
                                style={{ width: `${Math.max(5, pct)}%` }}
                              />
                            </div>
                            <span className="text-[10px] font-mono text-zinc-400 w-12 text-right">
                              {pct}% share
                            </span>
                          </div>

                          <div className="text-[10px] text-zinc-400 font-mono">
                            {loc.humanReadable}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Time-Stay Chart & Historical Sessions (Date From -> Date To) */}
              <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-xs font-semibold text-zinc-100 flex items-center gap-1.5">
                      <BarChart2 className="w-4 h-4 text-blue-400" />
                      Time-Stay Sessions & Movement Log (Date From → Date To)
                    </h3>
                    <p className="text-[11px] text-zinc-400">
                      Chronological intervals showing entry date/time, exit date/time, exact stay breakdown, and prior gap
                    </p>
                  </div>
                  <span className="text-[10px] font-mono text-zinc-500">
                    {data.staySessions?.length || 0} Sessions
                  </span>
                </div>

                {data.staySessions?.length === 0 ? (
                  <div className="p-4 text-center text-xs text-zinc-500 bg-zinc-900/40 rounded-lg">
                    No stay sessions logged yet.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {data.staySessions.map((session: any, idx: number) => (
                      <div
                        key={session.id || idx}
                        className="p-3 bg-zinc-900/70 hover:bg-zinc-900 border border-zinc-800 rounded-lg transition-colors text-xs space-y-2"
                      >
                        <div className="flex items-center justify-between flex-wrap gap-2">
                          <div className="flex items-center gap-2">
                            <span className="font-semibold text-zinc-200">{session.location}</span>
                            <span
                              className={`text-[10px] font-mono px-2 py-0.5 rounded-full ${
                                session.status === 'COMPLETED'
                                  ? 'bg-zinc-800 text-zinc-300 border border-zinc-700'
                                  : 'bg-emerald-950 text-emerald-300 border border-emerald-800 animate-pulse'
                              }`}
                            >
                              {session.status === 'COMPLETED' ? 'CYCLE COMPLETED' : 'ACTIVE ON-SITE'}
                            </span>
                          </div>

                          <div className="text-right">
                            <span className="text-[10px] text-zinc-400 mr-1.5 font-mono">Time Stayed:</span>
                            <span className="font-mono font-bold text-emerald-400">
                              {session.durationFormatted}
                            </span>
                          </div>
                        </div>

                        {/* From -> To Date Timeline */}
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 p-2 bg-zinc-950/60 rounded border border-zinc-800/80 font-mono text-[11px]">
                          <div>
                            <span className="text-zinc-500 text-[10px] block uppercase">Date From (Entry):</span>
                            <span className="text-emerald-400 font-semibold">{session.entryTime}</span>
                            <span className="text-zinc-500 text-[10px] block mt-0.5">
                              Via {session.gateIn || 'Gate 01'}
                            </span>
                          </div>

                          <div>
                            <span className="text-zinc-500 text-[10px] block uppercase">Date To (Exit):</span>
                            {session.exitTime ? (
                              <>
                                <span className="text-amber-400 font-semibold">{session.exitTime}</span>
                                <span className="text-zinc-500 text-[10px] block mt-0.5">
                                  Via {session.gateOut || 'Gate 02'}
                                </span>
                              </>
                            ) : (
                              <span className="text-zinc-400 italic">Currently Inside Gate</span>
                            )}
                          </div>
                        </div>

                        {/* How long before he came */}
                        {session.timeBeforeVisitFormatted && (
                          <div className="text-[11px] text-zinc-400 flex items-center gap-1.5 pt-0.5">
                            <History className="w-3.5 h-3.5 text-zinc-500" />
                            <span>
                              How long before he came:{' '}
                              <strong className="text-zinc-300 font-medium">
                                {session.timeBeforeVisitFormatted}
                              </strong>
                            </span>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          ) : null}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between px-5 py-3 border-t border-zinc-800 bg-zinc-950/90 shrink-0 text-xs">
          <div className="text-zinc-500 font-mono text-[11px]">
            Army Sovereign Server System • Restricted Military Access
          </div>
          <button
            onClick={onClose}
            className="px-4 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded-lg font-medium transition-colors cursor-pointer"
          >
            Close Dossier
          </button>
        </div>
      </div>
    </div>
  );
};
