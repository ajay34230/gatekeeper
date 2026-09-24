import React, { useState } from 'react';
import { Truck, User, Search, Filter, Clock, X, CheckCircle2, RefreshCw, Download, Check, AlertTriangle, ArrowDownLeft, ArrowUpRight } from 'lucide-react';
import { ActivityRecord } from '../types';
import { downloadActivityCsv } from '../utils/csvExport';

interface ActivityScreenProps {
  activities: ActivityRecord[];
  isOnline?: boolean;
  pendingCount?: number;
  onFlushSync?: () => void;
  initialActionFilter?: 'ALL' | 'ENTRY' | 'EXIT';
  initialTypeFilter?: 'ALL' | 'PERSON' | 'VEHICLE';
}

export const ActivityScreen: React.FC<ActivityScreenProps> = ({
  activities,
  isOnline = false,
  pendingCount = 0,
  onFlushSync,
  initialActionFilter = 'ALL',
  initialTypeFilter = 'ALL',
}) => {
  const [filterType, setFilterType] = useState<'ALL' | 'PERSON' | 'VEHICLE'>(initialTypeFilter);
  const [filterAction, setFilterAction] = useState<'ALL' | 'ENTRY' | 'EXIT'>(initialActionFilter);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedRecord, setSelectedRecord] = useState<ActivityRecord | null>(null);
  const [isSyncing, setIsSyncing] = useState(false);
  const [isExported, setIsExported] = useState(false);

  const handleSyncAll = () => {
    if (!onFlushSync || !isOnline || pendingCount <= 0 || isSyncing) return;
    setIsSyncing(true);
    setTimeout(() => {
      onFlushSync();
      setIsSyncing(false);
    }, 700);
  };

  const handleExportCsv = () => {
    if (activities.length === 0) return;
    downloadActivityCsv(activities);
    setIsExported(true);
    setTimeout(() => {
      setIsExported(false);
    }, 2500);
  };

  const filtered = activities.filter((act) => {
    if (filterType !== 'ALL' && act.type !== filterType) return false;
    if (filterAction !== 'ALL' && act.action !== filterAction) return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase().trim();
      
      // Check personnel name, vehicle ID/plate, gate name, target ID, location
      const matchesTarget = act.targetId.toLowerCase().includes(q);
      const matchesTitle = act.title.toLowerCase().includes(q);
      const matchesSubtitle = act.subtitle.toLowerCase().includes(q);
      const matchesGate = act.gate.toLowerCase().includes(q);
      const matchesLocation = act.location.toLowerCase().includes(q);

      // Check vehicle manifest names (driver, co-driver, occupants) if present
      const matchesDriver = act.vehicleManifest?.driver?.name.toLowerCase().includes(q) || false;
      const matchesCoDriver = act.vehicleManifest?.coDriver?.name.toLowerCase().includes(q) || false;
      const matchesOccupants = act.vehicleManifest?.occupants?.some((occ) => occ.name.toLowerCase().includes(q)) || false;

      return (
        matchesTarget ||
        matchesTitle ||
        matchesSubtitle ||
        matchesGate ||
        matchesLocation ||
        matchesDriver ||
        matchesCoDriver ||
        matchesOccupants
      );
    }
    return true;
  });

  return (
    <div className="flex flex-col flex-1 px-5 py-4 max-w-lg mx-auto w-full select-none overflow-hidden">
      {/* Top Header */}
      <div className="flex items-center justify-between pb-3 border-b border-zinc-200/80 shrink-0 gap-2">
        <div className="min-w-0 flex-1">
          <h1 className="text-lg font-bold text-zinc-900 tracking-tight truncate">Recent Activity</h1>
          <p className="text-xs text-zinc-500 font-medium truncate">Logged events for current operational shift</p>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          {/* Sync All Button - appears only when online and pending sync count > 0 */}
          {isOnline && pendingCount > 0 && (
            <button
              id="activity-sync-all-btn"
              onClick={handleSyncAll}
              disabled={isSyncing}
              className={`flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold shadow-2xs transition-all cursor-pointer ${
                isSyncing
                  ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                  : 'bg-emerald-600 hover:bg-emerald-700 active:bg-emerald-800 text-white'
              }`}
              title={`Flush and sync ${pendingCount} offline record(s) to central server`}
              aria-label={`Sync All ${pendingCount} pending records`}
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isSyncing ? 'animate-spin' : ''}`} />
              <span>{isSyncing ? 'Syncing...' : `Sync All (${pendingCount})`}</span>
            </button>
          )}

          {/* Export to CSV Button */}
          <button
            id="activity-export-csv-btn"
            onClick={handleExportCsv}
            disabled={activities.length === 0}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold transition-all border shadow-2xs cursor-pointer ${
              isExported
                ? 'bg-emerald-50 text-emerald-700 border-emerald-300'
                : 'bg-white hover:bg-zinc-100 active:bg-zinc-200 text-zinc-700 border-zinc-200 hover:border-zinc-300'
            }`}
            title="Download full activity log as CSV file for supervisors"
            aria-label="Export activity log to CSV"
          >
            {isExported ? (
              <>
                <Check className="w-3.5 h-3.5 text-emerald-600" />
                <span className="text-emerald-700 font-bold text-[11px]">Downloaded</span>
              </>
            ) : (
              <>
                <Download className="w-3.5 h-3.5 text-zinc-500" />
                <span className="text-[11px]">Export CSV</span>
              </>
            )}
          </button>

          <span className="text-xs font-mono font-bold px-2 py-0.5 rounded-full bg-zinc-100 text-zinc-700">
            {filtered.length} total
          </span>
        </div>
      </div>

      {/* Filter Chips & Search Bar */}
      <div className="my-3 space-y-2.5 shrink-0">
        <div className="relative">
          <Search className="w-4 h-4 text-zinc-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
          <input
            id="activity-search-input"
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search by personnel, vehicle ID, or gate..."
            className="w-full pl-9 pr-8 py-2.5 bg-white border border-zinc-200 rounded-xl text-xs font-medium text-zinc-800 placeholder:text-zinc-400 focus:outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-400 transition-all shadow-2xs"
          />
          {searchQuery && (
            <button
              id="activity-search-clear-btn"
              onClick={() => setSearchQuery('')}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 p-1 text-zinc-400 hover:text-zinc-700 rounded-md hover:bg-zinc-100 transition-colors"
              title="Clear search"
              aria-label="Clear search query"
            >
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>

        {/* Filter Pills */}
        <div className="flex flex-col gap-1.5">
          <div id="activity-filter-chips" className="flex items-center gap-1.5 overflow-x-auto pb-0.5">
            {(
              [
                { id: 'ALL', label: 'All Types', icon: null, count: activities.length },
                { id: 'PERSON', label: 'Personnel', icon: User, count: activities.filter((a) => a.type === 'PERSON').length },
                { id: 'VEHICLE', label: 'Vehicles', icon: Truck, count: activities.filter((a) => a.type === 'VEHICLE').length },
              ] as const
            ).map(({ id, label, icon: IconComponent, count }) => {
              const isActive = filterType === id;
              return (
                <button
                  key={id}
                  id={`filter-chip-${id.toLowerCase()}`}
                  onClick={() => setFilterType(id)}
                  className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold tracking-tight transition-all cursor-pointer select-none shrink-0 ${
                    isActive
                      ? 'bg-zinc-900 text-white shadow-xs'
                      : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200/80 hover:text-zinc-900 border border-zinc-200/60'
                  }`}
                >
                  {IconComponent && (
                    <IconComponent className={`w-3.5 h-3.5 ${isActive ? 'text-white' : 'text-zinc-500'}`} />
                  )}
                  <span>{label}</span>
                  <span
                    className={`text-[10px] font-mono px-1.5 py-0.2 rounded-full font-bold ${
                      isActive ? 'bg-zinc-800 text-zinc-300' : 'bg-zinc-200/70 text-zinc-600'
                    }`}
                  >
                    {count}
                  </span>
                </button>
              );
            })}
          </div>

          <div id="activity-direction-chips" className="flex items-center gap-1.5 overflow-x-auto">
            <button
              onClick={() => setFilterAction('ALL')}
              className={`px-2.5 py-1 rounded-lg text-[11px] font-semibold transition-all cursor-pointer ${
                filterAction === 'ALL'
                  ? 'bg-zinc-800 text-white'
                  : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200'
              }`}
            >
              All Actions
            </button>
            <button
              onClick={() => setFilterAction('ENTRY')}
              className={`flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-semibold transition-all cursor-pointer ${
                filterAction === 'ENTRY'
                  ? 'bg-emerald-600 text-white shadow-2xs'
                  : 'bg-emerald-50 text-emerald-700 border border-emerald-200 hover:bg-emerald-100/70'
              }`}
            >
              <ArrowDownLeft className="w-3 h-3 text-emerald-500" />
              <span>Entries ({activities.filter((a) => a.action === 'ENTRY').length})</span>
            </button>
            <button
              onClick={() => setFilterAction('EXIT')}
              className={`flex items-center gap-1 px-2.5 py-1 rounded-lg text-[11px] font-semibold transition-all cursor-pointer ${
                filterAction === 'EXIT'
                  ? 'bg-zinc-700 text-white shadow-2xs'
                  : 'bg-zinc-100 text-zinc-700 border border-zinc-200 hover:bg-zinc-200/80'
              }`}
            >
              <ArrowUpRight className="w-3 h-3 text-zinc-500" />
              <span>Exits ({activities.filter((a) => a.action === 'EXIT').length})</span>
            </button>
          </div>
        </div>
      </div>

      {/* Activity List or Empty State */}
      <div className="flex-1 overflow-y-auto space-y-2 pr-0.5">
        {filtered.length === 0 ? (
          <div className="h-52 flex flex-col items-center justify-center text-center p-6 bg-zinc-50 border border-zinc-200 rounded-2xl my-4">
            <Clock className="w-8 h-8 text-zinc-300 mb-2" />
            <span className="text-sm font-bold text-zinc-700">
              {searchQuery ? `No results for "${searchQuery}"` : 'No events matched'}
            </span>
            <p className="text-xs text-zinc-400 mt-1 max-w-xs">
              {searchQuery
                ? 'Try searching by a different name, vehicle plate, or gate number.'
                : 'Events recorded during your shift will appear here.'}
            </p>
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                className="mt-3 px-3 py-1 bg-zinc-200/80 hover:bg-zinc-300 text-zinc-800 text-xs font-semibold rounded-lg transition-colors cursor-pointer"
              >
                Clear Search
              </button>
            )}
          </div>
        ) : (
          filtered.map((act) => {
            const isEntry = act.action === 'ENTRY';
            return (
              <div
                key={act.id}
                onClick={() => setSelectedRecord(act)}
                className="flex items-center justify-between p-3.5 bg-white border border-zinc-200/80 hover:border-zinc-300 rounded-xl transition-all cursor-pointer active:bg-zinc-50"
              >
                {/* Time & Identity */}
                <div className="flex items-center gap-3">
                  <span className="font-mono text-zinc-400 font-bold text-xs w-11">
                    {act.timestamp}
                  </span>

                  <div className="w-7 h-7 rounded-lg bg-zinc-100 flex items-center justify-center text-zinc-700 shrink-0">
                    {act.type === 'VEHICLE' ? (
                      <Truck className="w-4 h-4" />
                    ) : (
                      <User className="w-4 h-4" />
                    )}
                  </div>

                  <div className="min-w-0">
                    <div className="flex items-center gap-1.5">
                      <span className="font-bold text-zinc-900 text-xs truncate">
                        {act.title}
                      </span>
                      <span className="font-mono text-[11px] font-semibold text-zinc-500">
                        {act.targetId}
                      </span>
                    </div>
                    <span className="text-[11px] text-zinc-400 truncate block">
                      {act.location} • {act.gate}
                    </span>
                  </div>
                </div>

                {/* Action Badge */}
                <div className="flex items-center gap-1.5">
                  {act.locationMismatch && (
                    <span
                      title={`Location Mismatch: Scanned QR indicated ${act.scannedLocation || 'cross-location'}`}
                      className="font-mono text-[9px] font-bold px-1.5 py-0.5 rounded bg-amber-100 text-amber-800 border border-amber-300 flex items-center gap-0.5"
                    >
                      <AlertTriangle className="w-2.5 h-2.5 text-amber-600 shrink-0" />
                      <span className="hidden sm:inline">LOC MISMATCH</span>
                    </span>
                  )}
                  {act.stayDuration && (
                    <span className="text-[10px] font-mono text-zinc-400 hidden sm:inline">
                      {act.stayDuration}
                    </span>
                  )}
                  <span
                    className={`font-mono text-[10px] font-bold px-2.5 py-1 rounded-full whitespace-nowrap ${
                      isEntry
                        ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                        : 'bg-amber-50 text-amber-700 border border-amber-200'
                    }`}
                  >
                    {act.action}
                  </span>
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Record Inspection Bottom Sheet */}
      {selectedRecord && (
        <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/60 backdrop-blur-xs p-0 sm:p-4">
          <div className="w-full max-w-md bg-white rounded-t-3xl sm:rounded-2xl p-6 border-t sm:border border-zinc-200 shadow-2xl animate-fade-in">
            <div className="flex items-center justify-between pb-3 border-b border-zinc-200">
              <span className="font-mono text-xs font-bold text-zinc-400">
                LOG ENTRY #{selectedRecord.id}
              </span>
              <button
                onClick={() => setSelectedRecord(null)}
                className="p-1 text-zinc-400 hover:text-zinc-700 rounded"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="mt-4 space-y-3 text-xs">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-base font-bold text-zinc-900">{selectedRecord.title}</h3>
                  <span className="text-zinc-500 font-mono">{selectedRecord.subtitle}</span>
                </div>
                <span
                  className={`font-mono text-xs font-bold px-3 py-1 rounded-full ${
                    selectedRecord.action === 'ENTRY'
                      ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                      : 'bg-amber-50 text-amber-700 border border-amber-200'
                  }`}
                >
                  {selectedRecord.action}
                </span>
              </div>

              {selectedRecord.locationMismatch && (
                <div
                  id="activity-detail-mismatch-banner"
                  className="p-3 bg-amber-50 border border-amber-300 rounded-xl space-y-1 text-xs"
                >
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-amber-900 flex items-center gap-1.5 uppercase text-[11px] tracking-wider">
                      <AlertTriangle className="w-3.5 h-3.5 text-amber-600" />
                      Location Mismatch Flagged
                    </span>
                    <span className="font-mono font-bold text-[10px] px-1.5 py-0.5 rounded bg-amber-200 text-amber-950 border border-amber-400">
                      MISMATCH
                    </span>
                  </div>
                  <p className="text-[11px] text-amber-800">
                    Scanned badge QR indicated <span className="font-mono font-bold">{selectedRecord.scannedLocation || 'different location'}</span>, which was processed at station <span className="font-mono font-bold">{selectedRecord.location}</span>.
                  </p>
                </div>
              )}

              <div className="p-3.5 bg-zinc-50 border border-zinc-200 rounded-xl space-y-2">
                <div className="flex justify-between">
                  <span className="text-zinc-400">Recorded Time</span>
                  <span className="font-mono font-bold text-zinc-800">{selectedRecord.timestamp}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-zinc-400">Location / Gate</span>
                  <span className="font-semibold text-zinc-800">
                    {selectedRecord.location} • {selectedRecord.gate}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-zinc-400">Gatekeeper Operator</span>
                  <span className="font-mono text-zinc-800">{selectedRecord.gatekeeperId}</span>
                </div>
                {selectedRecord.stayDuration && (
                  <div className="flex justify-between pt-1 border-t border-zinc-200">
                    <span className="text-zinc-500">Duration on Site</span>
                    <span className="font-mono font-bold text-amber-700">
                      {selectedRecord.stayDurationFormatted || selectedRecord.stayDuration}
                    </span>
                  </div>
                )}
                {selectedRecord.timeSinceLastVisitFormatted && (
                  <div className="flex justify-between pt-1 border-t border-zinc-200">
                    <span className="text-zinc-500">Time Since Last Visit</span>
                    <span className="font-mono font-bold text-zinc-700">
                      {selectedRecord.timeSinceLastVisitFormatted}
                    </span>
                  </div>
                )}
                {selectedRecord.armyNumber && (
                  <div className="flex justify-between">
                    <span className="text-zinc-400">Army Number</span>
                    <span className="font-mono font-semibold text-zinc-800">{selectedRecord.armyNumber}</span>
                  </div>
                )}
                {selectedRecord.company && (
                  <div className="flex justify-between">
                    <span className="text-zinc-400">Company</span>
                    <span className="font-semibold text-zinc-800">{selectedRecord.company} Co</span>
                  </div>
                )}
              </div>

              {/* Vehicle Manifest Details if available */}
              {selectedRecord.vehicleManifest && (
                <div className="p-3.5 bg-zinc-50 border border-zinc-200 rounded-xl space-y-1.5">
                  <span className="font-bold text-zinc-800 block text-[11px] uppercase tracking-wider">
                    Manifest Verification
                  </span>
                  <div className="flex justify-between">
                    <span className="text-zinc-500">Driver</span>
                    <span className="font-semibold text-zinc-900">
                      {selectedRecord.vehicleManifest.driver.name} ({selectedRecord.vehicleManifest.driver.id})
                    </span>
                  </div>
                  {selectedRecord.vehicleManifest.coDriver && (
                    <div className="flex justify-between">
                      <span className="text-zinc-500">Co-driver</span>
                      <span className="font-semibold text-zinc-900">
                        {selectedRecord.vehicleManifest.coDriver.name} ({selectedRecord.vehicleManifest.coDriver.id})
                      </span>
                    </div>
                  )}
                  {selectedRecord.vehicleManifest.occupants.length > 0 && (
                    <div className="flex justify-between pt-1 border-t border-zinc-200">
                      <span className="text-zinc-500">Additional Passengers</span>
                      <span className="font-semibold text-zinc-900">
                        {selectedRecord.vehicleManifest.occupants.map((o) => o.name).join(', ')}
                      </span>
                    </div>
                  )}
                </div>
              )}
            </div>

            <button
              onClick={() => setSelectedRecord(null)}
              className="w-full mt-5 py-3 bg-zinc-900 text-white font-semibold text-xs rounded-xl hover:bg-black"
            >
              Dismiss
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
