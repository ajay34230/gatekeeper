import React, { useEffect, useState } from 'react';
import {
  Calendar,
  Users,
  Truck,
  Building2,
  RefreshCw,
  TrendingUp,
  ArrowDownLeft,
  ArrowUpRight,
  Shield,
  Search,
  ExternalLink,
  ChevronRight,
  CheckCircle2,
} from 'lucide-react';

interface MilitaryPeriodicSummaryProps {
  onSelectIndividual: (targetId: string) => void;
}

export const MilitaryPeriodicSummary: React.FC<MilitaryPeriodicSummaryProps> = ({
  onSelectIndividual,
}) => {
  const [data, setData] = useState<any>(null);
  const [selectedPeriod, setSelectedPeriod] = useState<'daily' | 'weekly' | 'monthly'>('daily');
  const [filterType, setFilterType] = useState<'ALL' | 'PERSON' | 'VEHICLE'>('ALL');
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(true);

  const fetchSummaries = async () => {
    setIsLoading(true);
    try {
      const res = await fetch('/api/activities/summaries');
      const json = await res.json();
      if (json.status === 'ok') {
        setData(json.summaries);
      }
    } catch (err) {
      console.error('Failed to fetch periodic summaries', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchSummaries();
  }, []);

  const currentSummary = data ? data[selectedPeriod] : null;

  return (
    <div className="space-y-4 text-xs">
      {/* Top Header & Period Selector */}
      <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-zinc-100 flex items-center gap-2">
            <Calendar className="w-4 h-4 text-emerald-400" />
            Military Periodic Activity Summaries
          </h3>
          <p className="text-[11px] text-zinc-400">
            Automated Daily, Weekly, and Monthly intelligence reporting for all Army personnel and military fleet vehicles
          </p>
        </div>

        {/* Period Selector Toggle */}
        <div className="flex items-center gap-1.5 p-1 bg-zinc-900 border border-zinc-700/80 rounded-lg">
          <button
            onClick={() => setSelectedPeriod('daily')}
            className={`px-3 py-1.5 rounded-md font-medium text-xs transition-colors cursor-pointer ${
              selectedPeriod === 'daily'
                ? 'bg-emerald-600 text-white shadow-sm'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            Daily (Today)
          </button>
          <button
            onClick={() => setSelectedPeriod('weekly')}
            className={`px-3 py-1.5 rounded-md font-medium text-xs transition-colors cursor-pointer ${
              selectedPeriod === 'weekly'
                ? 'bg-emerald-600 text-white shadow-sm'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            Weekly (7 Days)
          </button>
          <button
            onClick={() => setSelectedPeriod('monthly')}
            className={`px-3 py-1.5 rounded-md font-medium text-xs transition-colors cursor-pointer ${
              selectedPeriod === 'monthly'
                ? 'bg-emerald-600 text-white shadow-sm'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            Monthly (30 Days)
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="p-12 text-center text-zinc-400 space-y-2 bg-zinc-950/40 rounded-xl border border-zinc-800">
          <RefreshCw className="w-6 h-6 animate-spin text-emerald-400 mx-auto" />
          <p>Compiling military activity aggregates...</p>
        </div>
      ) : currentSummary ? (
        <>
          {/* Key Metrics Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            {/* Total Period Activity */}
            <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
              <span className="text-[11px] text-zinc-400 uppercase tracking-wider font-mono">
                Total Checkpoint Operations
              </span>
              <div className="text-xl font-bold font-mono text-zinc-100">
                {currentSummary.totalRecords} Records
              </div>
              <p className="text-[11px] text-emerald-400 font-medium">
                {currentSummary.period}
              </p>
            </div>

            {/* Personnel Breakdown */}
            <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
              <div className="flex items-center justify-between text-zinc-400">
                <span className="flex items-center gap-1.5 text-[11px] uppercase tracking-wider font-mono">
                  <Users className="w-3.5 h-3.5 text-blue-400" />
                  Personnel
                </span>
                <span className="text-emerald-400 font-bold font-mono">
                  {currentSummary.personnel.uniqueCount} Unique
                </span>
              </div>
              <div className="text-xl font-bold font-mono text-zinc-100">
                {currentSummary.personnel.totalScans} Total Scans
              </div>
              <div className="flex items-center gap-3 text-[11px] text-zinc-400">
                <span className="flex items-center gap-0.5 text-emerald-400">
                  <ArrowDownLeft className="w-3 h-3" /> {currentSummary.personnel.entries} In
                </span>
                <span className="flex items-center gap-0.5 text-amber-400">
                  <ArrowUpRight className="w-3 h-3" /> {currentSummary.personnel.exits} Out
                </span>
              </div>
            </div>

            {/* Vehicles Breakdown */}
            <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
              <div className="flex items-center justify-between text-zinc-400">
                <span className="flex items-center gap-1.5 text-[11px] uppercase tracking-wider font-mono">
                  <Truck className="w-3.5 h-3.5 text-amber-400" />
                  Vehicles
                </span>
                <span className="text-amber-400 font-bold font-mono">
                  {currentSummary.vehicles.uniqueCount} Unique
                </span>
              </div>
              <div className="text-xl font-bold font-mono text-zinc-100">
                {currentSummary.vehicles.totalScans} Total Scans
              </div>
              <div className="flex items-center gap-3 text-[11px] text-zinc-400">
                <span className="flex items-center gap-0.5 text-emerald-400">
                  <ArrowDownLeft className="w-3 h-3" /> {currentSummary.vehicles.entries} In
                </span>
                <span className="flex items-center gap-0.5 text-amber-400">
                  <ArrowUpRight className="w-3 h-3" /> {currentSummary.vehicles.exits} Out
                </span>
              </div>
            </div>

            {/* Top Active Base Locations */}
            <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-1">
              <span className="text-[11px] text-zinc-400 uppercase tracking-wider font-mono flex items-center gap-1">
                <Building2 className="w-3.5 h-3.5 text-purple-400" />
                Top Active Locations
              </span>
              <div className="space-y-1 pt-1">
                {currentSummary.topLocations?.slice(0, 2).map((loc: any) => (
                  <div key={loc.location} className="flex items-center justify-between text-[11px]">
                    <span className="text-zinc-200 font-medium truncate max-w-[120px]">{loc.location}</span>
                    <span className="font-mono text-zinc-400">{loc.count} scans</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Detailed Entity Listing & Stay Inspection */}
          <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-3">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5">
              <div>
                <h4 className="text-xs font-semibold text-zinc-100">
                  Active Entity Directory ({selectedPeriod.toUpperCase()})
                </h4>
                <p className="text-[11px] text-zinc-400">
                  Click any soldier or vehicle to inspect how long they stayed, how long before they came, and time stayed per location
                </p>
              </div>

              {/* Filters */}
              <div className="flex items-center gap-2">
                <div className="flex items-center gap-1 p-0.5 bg-zinc-900 border border-zinc-800 rounded-lg">
                  <button
                    onClick={() => setFilterType('ALL')}
                    className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                      filterType === 'ALL' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'
                    }`}
                  >
                    All Entities
                  </button>
                  <button
                    onClick={() => setFilterType('PERSON')}
                    className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                      filterType === 'PERSON' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'
                    }`}
                  >
                    Personnel ({currentSummary.personnel.uniqueIds.length})
                  </button>
                  <button
                    onClick={() => setFilterType('VEHICLE')}
                    className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                      filterType === 'VEHICLE' ? 'bg-zinc-800 text-white' : 'text-zinc-400 hover:text-zinc-200'
                    }`}
                  >
                    Vehicles ({currentSummary.vehicles.uniqueIds.length})
                  </button>
                </div>
              </div>
            </div>

            {/* Entity Quick Access Cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 pt-1">
              {(filterType === 'ALL' || filterType === 'PERSON'
                ? currentSummary.personnel.uniqueIds
                : []
              ).map((id: string) => (
                <div
                  key={`person-${id}`}
                  onClick={() => onSelectIndividual(id)}
                  className="p-3 bg-zinc-900/80 hover:bg-zinc-900 border border-zinc-800 hover:border-emerald-700/60 rounded-xl transition-all cursor-pointer flex items-center justify-between group"
                >
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-blue-500/10 border border-blue-500/20 text-blue-400">
                      <Users className="w-4 h-4" />
                    </div>
                    <div>
                      <div className="font-semibold text-zinc-100 group-hover:text-emerald-400 transition-colors flex items-center gap-1.5">
                        <span>{id}</span>
                        <span className="text-[9px] px-1.5 py-0.2 rounded bg-zinc-800 text-zinc-400 font-mono">
                          ARMY
                        </span>
                      </div>
                      <div className="text-[10px] text-zinc-400">
                        Click to view time-stay breakdown
                      </div>
                    </div>
                  </div>
                  <ChevronRight className="w-4 h-4 text-zinc-600 group-hover:text-emerald-400 transition-colors" />
                </div>
              ))}

              {(filterType === 'ALL' || filterType === 'VEHICLE'
                ? currentSummary.vehicles.uniqueIds
                : []
              ).map((id: string) => (
                <div
                  key={`veh-${id}`}
                  onClick={() => onSelectIndividual(id)}
                  className="p-3 bg-zinc-900/80 hover:bg-zinc-900 border border-zinc-800 hover:border-amber-700/60 rounded-xl transition-all cursor-pointer flex items-center justify-between group"
                >
                  <div className="flex items-center gap-2.5">
                    <div className="p-2 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-400">
                      <Truck className="w-4 h-4" />
                    </div>
                    <div>
                      <div className="font-semibold text-zinc-100 group-hover:text-amber-400 transition-colors flex items-center gap-1.5">
                        <span>{id}</span>
                        <span className="text-[9px] px-1.5 py-0.2 rounded bg-zinc-800 text-zinc-400 font-mono">
                          FLEET
                        </span>
                      </div>
                      <div className="text-[10px] text-zinc-400">
                        Click to view stay record & history
                      </div>
                    </div>
                  </div>
                  <ChevronRight className="w-4 h-4 text-zinc-600 group-hover:text-amber-400 transition-colors" />
                </div>
              ))}
            </div>
          </div>
        </>
      ) : null}
    </div>
  );
};
