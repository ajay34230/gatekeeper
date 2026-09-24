import React, { useState, useEffect, useMemo } from 'react';
import {
  Server,
  X,
  RefreshCw,
  Send,
  Database,
  Search,
  Filter,
  Calendar,
  MapPin,
  Tag,
  User,
  Truck,
  RotateCcw,
  Copy,
  Check,
  ChevronRight,
  SlidersHorizontal,
  BarChart2,
  TrendingUp,
  Clock,
  ExternalLink,
  History,
  Shield,
} from 'lucide-react';
import { HourlyActivityChart } from './HourlyActivityChart';
import { IndividualHistoryModal } from './IndividualHistoryModal';
import { MilitaryPeriodicSummary } from './MilitaryPeriodicSummary';

interface ServerApiModalProps {
  onClose: () => void;
}

export const ServerApiModal: React.FC<ServerApiModalProps> = ({ onClose }) => {
  const [healthData, setHealthData] = useState<any>(null);
  const [statsData, setStatsData] = useState<any>(null);
  const [records, setRecords] = useState<any[]>([]);
  const [totalServerCount, setTotalServerCount] = useState<number>(0);
  const [isLoading, setIsLoading] = useState(false);
  const [isQueryingRecords, setIsQueryingRecords] = useState(false);
  const [testSending, setTestSending] = useState(false);
  const [lastTestResult, setLastTestResult] = useState<any>(null);
  const [activeTab, setActiveTab] = useState<'OVERVIEW' | 'ENDPOINTS' | 'LIVE_DATA' | 'CHARTS' | 'SUMMARIES' | 'CURL'>('LIVE_DATA');
  const [selectedIndividualId, setSelectedIndividualId] = useState<string | null>(null);
  const [showInlineChart, setShowInlineChart] = useState(true);
  const [copiedEndpoint, setCopiedEndpoint] = useState<string | null>(null);

  // Search & Filter state
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<'ALL' | 'PERSON' | 'VEHICLE'>('ALL');
  const [actionFilter, setActionFilter] = useState<'ALL' | 'ENTRY' | 'EXIT'>('ALL');
  const [gateFilter, setGateFilter] = useState<string>('ALL');
  const [locationFilter, setLocationFilter] = useState<string>('ALL');
  const [dateRangeFilter, setDateRangeFilter] = useState<'ALL' | 'TODAY' | 'LAST_24H' | 'LAST_7D' | 'CUSTOM'>('ALL');
  const [customStartDate, setCustomStartDate] = useState<string>('');
  const [customEndDate, setCustomEndDate] = useState<string>('');
  const [showAdvancedFilters, setShowAdvancedFilters] = useState<boolean>(true);

  // Fetch telemetry overview data
  const fetchOverview = async () => {
    setIsLoading(true);
    try {
      const [hRes, sRes] = await Promise.all([
        fetch('/api/health').then((r) => r.json()).catch(() => null),
        fetch('/api/activities/stats').then((r) => r.json()).catch(() => null),
      ]);
      setHealthData(hRes);
      if (sRes && sRes.stats) setStatsData(sRes.stats);
    } catch (err) {
      console.warn('Error fetching overview info:', err);
    } finally {
      setIsLoading(false);
    }
  };

  // Fetch filtered activity records from GET /api/activities
  const fetchActivities = async () => {
    setIsQueryingRecords(true);
    try {
      const params = new URLSearchParams();

      if (searchQuery.trim()) {
        params.set('q', searchQuery.trim());
      }
      if (typeFilter !== 'ALL') {
        params.set('type', typeFilter);
      }
      if (actionFilter !== 'ALL') {
        params.set('action', actionFilter);
      }
      if (gateFilter !== 'ALL') {
        params.set('gate', gateFilter);
      }
      if (locationFilter !== 'ALL') {
        params.set('location', locationFilter);
      }

      // Date calculations
      const now = Date.now();
      if (dateRangeFilter === 'TODAY') {
        const startOfDay = new Date();
        startOfDay.setHours(0, 0, 0, 0);
        params.set('startDate', startOfDay.getTime().toString());
      } else if (dateRangeFilter === 'LAST_24H') {
        params.set('startDate', (now - 24 * 60 * 60 * 1000).toString());
      } else if (dateRangeFilter === 'LAST_7D') {
        params.set('startDate', (now - 7 * 24 * 60 * 60 * 1000).toString());
      } else if (dateRangeFilter === 'CUSTOM') {
        if (customStartDate) {
          const s = new Date(customStartDate).getTime();
          if (!isNaN(s)) params.set('startDate', s.toString());
        }
        if (customEndDate) {
          const e = new Date(customEndDate);
          e.setHours(23, 59, 59, 999);
          const eMs = e.getTime();
          if (!isNaN(eMs)) params.set('endDate', eMs.toString());
        }
      }

      const queryString = params.toString();
      const url = queryString ? `/api/activities?${queryString}` : '/api/activities';

      const res = await fetch(url);
      const data = await res.json();
      if (data && data.records) {
        setRecords(data.records);
        setTotalServerCount(data.total !== undefined ? data.total : data.records.length);
      }
    } catch (err) {
      console.warn('Error fetching activities:', err);
    } finally {
      setIsQueryingRecords(false);
    }
  };

  useEffect(() => {
    fetchOverview();
  }, []);

  // Re-fetch records whenever filters change with debouncing for search
  useEffect(() => {
    const handler = setTimeout(() => {
      fetchActivities();
    }, 150);

    return () => clearTimeout(handler);
  }, [
    searchQuery,
    typeFilter,
    actionFilter,
    gateFilter,
    locationFilter,
    dateRangeFilter,
    customStartDate,
    customEndDate,
  ]);

  // Reset all filters to default
  const handleResetFilters = () => {
    setSearchQuery('');
    setTypeFilter('ALL');
    setActionFilter('ALL');
    setGateFilter('ALL');
    setLocationFilter('ALL');
    setDateRangeFilter('ALL');
    setCustomStartDate('');
    setCustomEndDate('');
  };

  const hasActiveFilters =
    searchQuery.trim() !== '' ||
    typeFilter !== 'ALL' ||
    actionFilter !== 'ALL' ||
    gateFilter !== 'ALL' ||
    locationFilter !== 'ALL' ||
    dateRangeFilter !== 'ALL' ||
    customStartDate !== '' ||
    customEndDate !== '';

  const handleSendTestRecord = async () => {
    setTestSending(true);
    setLastTestResult(null);
    try {
      const randomId = Math.floor(100 + Math.random() * 900);
      const isEntry = Math.random() > 0.5;
      const isPerson = Math.random() > 0.35;
      const payload = {
        id: `ACT-TEST-${Date.now().toString().slice(-4)}`,
        timestamp: new Date().toTimeString().split(' ')[0],
        timestampMs: Date.now(),
        type: isPerson ? 'PERSON' : 'VEHICLE',
        action: isEntry ? 'ENTRY' : 'EXIT',
        targetId: isPerson ? `P-${randomId}` : `V-${randomId}`,
        title: isPerson ? `Operator John-${randomId}` : `V-${randomId}`,
        subtitle: isPerson ? `Terminal Staff Scan` : `Site Patrol Truck`,
        location: Math.random() > 0.4 ? 'Location 07' : 'Location 03',
        gate: Math.random() > 0.5 ? 'Gate 01' : 'Gate 02',
        gatekeeperId: 'GK-04',
        tag: isPerson ? 'Staff' : 'Fleet',
      };

      const res = await fetch('/api/activities', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      const data = await res.json();
      setLastTestResult({ status: res.status, data });
      fetchOverview();
      fetchActivities();
    } catch (err: any) {
      setLastTestResult({ status: 'error', data: { message: err.message } });
    } finally {
      setTestSending(false);
    }
  };

  const handleCopy = (text: string, id: string) => {
    navigator.clipboard?.writeText(text);
    setCopiedEndpoint(id);
    setTimeout(() => setCopiedEndpoint(null), 2000);
  };

  // Derive unique gate and location options from existing data
  const availableGates = useMemo(() => {
    const defaultGates = ['Gate 01', 'Gate 02', 'Gate 03', 'South Gate'];
    const fromRecords = records.map((r) => r.gate).filter(Boolean);
    return Array.from(new Set([...defaultGates, ...fromRecords])).sort();
  }, [records]);

  const availableLocations = useMemo(() => {
    const defaultLocs = ['Location 07', 'Location 03', 'Main Terminal'];
    const fromRecords = records.map((r) => r.location).filter(Boolean);
    return Array.from(new Set([...defaultLocs, ...fromRecords])).sort();
  }, [records]);

  // Aggregate hourly activity breakdown for tabular reporting
  const hourlyTableRows = useMemo(() => {
    const map: Record<
      number,
      {
        hour: number;
        hourLabel: string;
        entries: number;
        exits: number;
        total: number;
        personnel: number;
        vehicles: number;
      }
    > = {};

    for (let i = 0; i < 24; i++) {
      const hStr = i.toString().padStart(2, '0');
      map[i] = {
        hour: i,
        hourLabel: `${hStr}:00 - ${hStr}:59`,
        entries: 0,
        exits: 0,
        total: 0,
        personnel: 0,
        vehicles: 0,
      };
    }

    records.forEach((r) => {
      let h = -1;
      if (r.timestamp && typeof r.timestamp === 'string') {
        const p = parseInt(r.timestamp.split(':')[0], 10);
        if (!isNaN(p) && p >= 0 && p <= 23) h = p;
      }
      if (h === -1 && r.timestampMs) {
        const d = new Date(r.timestampMs);
        if (!isNaN(d.getTime())) h = d.getHours();
      }
      if (h >= 0 && h <= 23) {
        map[h].total += 1;
        if (r.action === 'ENTRY') map[h].entries += 1;
        else map[h].exits += 1;

        if (r.type === 'VEHICLE') map[h].vehicles += 1;
        else map[h].personnel += 1;
      }
    });

    return Object.values(map)
      .filter((row) => row.total > 0)
      .sort((a, b) => b.total - a.total);
  }, [records]);

  const endpointsList = [
    {
      method: 'GET',
      path: '/api/activities',
      desc: 'Retrieves stored records with optional filtering (?q=...&type=PERSON&gate=Gate 02&startDate=...&limit=50).',
    },
    {
      method: 'POST',
      path: '/api/activities',
      desc: 'Receives and stores an incoming activity record (or array) from the mobile terminal.',
      payload: `{\n  "id": "ACT-105",\n  "type": "PERSON",\n  "action": "ENTRY",\n  "targetId": "P-001",\n  "title": "John Doe",\n  "location": "Location 07",\n  "gate": "Gate 02"\n}`,
    },
    {
      method: 'POST',
      path: '/api/activities/batch',
      desc: 'Bulk batch upload for offline buffers when mobile device re-establishes connectivity.',
      payload: `{\n  "deviceId": "TAB-01",\n  "operatorId": "GK-04",\n  "records": [ { "id": "ACT-1", ... }, { "id": "ACT-2", ... } ]\n}`,
    },
    {
      method: 'GET',
      path: '/api/activities/stats',
      desc: 'Returns aggregated telemetry, entry/exit breakdown, and unique entity tallies.',
    },
    {
      method: 'GET',
      path: '/api/activities/summaries',
      desc: 'Compiles Daily, Weekly, and Monthly military summaries for every vehicle and person.',
    },
    {
      method: 'GET',
      path: '/api/individuals/:targetId',
      desc: 'Retrieves complete Army military dossier, stay history breakdown (Y/M/D/H/M), prior visit interval, and time stayed per location.',
    },
    {
      method: 'GET',
      path: '/api/activities/:id',
      desc: 'Retrieves details for a single activity record by unique ID.',
    },
    {
      method: 'GET',
      path: '/api/health',
      desc: 'Service health check endpoint reporting process uptime and storage count.',
    },
  ];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-2.5 sm:p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="relative flex flex-col w-full max-w-4xl max-h-[92vh] bg-zinc-900 border border-zinc-700/80 rounded-2xl shadow-2xl text-zinc-100 overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-3.5 border-b border-zinc-800 bg-zinc-950/70 shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="p-2 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-400">
              <Server className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-sm font-semibold text-zinc-100 flex items-center gap-2">
                Node.js Express Server API & Storage
                <span className="inline-flex items-center gap-1 text-[10px] font-mono px-2 py-0.5 rounded-full bg-emerald-950 text-emerald-300 border border-emerald-700/50">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse" />
                  Port 3000 Active
                </span>
              </h2>
              <p className="text-[11px] text-zinc-400">
                Activity ingestion engine, filtered retrieval, and persistent record store
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors cursor-pointer"
            aria-label="Close modal"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="flex items-center gap-2 px-5 py-2 border-b border-zinc-800 bg-zinc-900/90 text-xs shrink-0 overflow-x-auto">
          <button
            onClick={() => setActiveTab('LIVE_DATA')}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors flex items-center gap-1.5 cursor-pointer shrink-0 ${
              activeTab === 'LIVE_DATA'
                ? 'bg-zinc-800 text-white border border-zinc-700'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            <Database className="w-3.5 h-3.5 text-emerald-400" />
            <span>Activity Records</span>
            <span className="text-[10px] px-1.5 py-0.2 rounded-full bg-zinc-700/80 text-zinc-200 font-mono">
              {totalServerCount}
            </span>
          </button>
          <button
            onClick={() => setActiveTab('CHARTS')}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors flex items-center gap-1.5 cursor-pointer shrink-0 ${
              activeTab === 'CHARTS'
                ? 'bg-zinc-800 text-white border border-zinc-700'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            <BarChart2 className="w-3.5 h-3.5 text-blue-400" />
            <span>Hourly Activity Chart</span>
          </button>
          <button
            onClick={() => setActiveTab('SUMMARIES')}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors flex items-center gap-1.5 cursor-pointer shrink-0 ${
              activeTab === 'SUMMARIES'
                ? 'bg-zinc-800 text-white border border-zinc-700'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            <Calendar className="w-3.5 h-3.5 text-purple-400" />
            <span>Daily / Weekly / Monthly</span>
          </button>
          <button
            onClick={() => setActiveTab('OVERVIEW')}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors cursor-pointer shrink-0 ${
              activeTab === 'OVERVIEW'
                ? 'bg-zinc-800 text-white border border-zinc-700'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            Overview & Stats
          </button>
          <button
            onClick={() => setActiveTab('ENDPOINTS')}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors cursor-pointer shrink-0 ${
              activeTab === 'ENDPOINTS'
                ? 'bg-zinc-800 text-white border border-zinc-700'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            API Endpoints
          </button>
          <button
            onClick={() => setActiveTab('CURL')}
            className={`px-3 py-1.5 rounded-md font-medium transition-colors cursor-pointer shrink-0 ${
              activeTab === 'CURL'
                ? 'bg-zinc-800 text-white border border-zinc-700'
                : 'text-zinc-400 hover:text-zinc-200'
            }`}
          >
            cURL Examples
          </button>

          <button
            onClick={() => {
              fetchOverview();
              fetchActivities();
            }}
            disabled={isLoading || isQueryingRecords}
            className="ml-auto p-1.5 text-zinc-400 hover:text-white hover:bg-zinc-800 rounded transition-colors disabled:opacity-50 cursor-pointer"
            title="Refresh server data"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading || isQueryingRecords ? 'animate-spin' : ''}`} />
          </button>
        </div>

        {/* Content Area */}
        <div className="flex-1 overflow-y-auto p-4 sm:p-5 space-y-4">
          {activeTab === 'LIVE_DATA' && (
            <div className="space-y-3.5">
              {/* Search Bar & Filter Controls Container */}
              <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-3">
                {/* Search Input Bar */}
                <div className="flex items-center gap-2">
                  <div className="relative flex-1">
                    <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
                    <input
                      type="text"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="Search by name, ID (e.g., P-001, V-014), title, or keyword..."
                      className="w-full pl-9 pr-8 py-2 bg-zinc-900 border border-zinc-700/80 rounded-lg text-xs text-zinc-100 placeholder:text-zinc-500 focus:outline-hidden focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 transition-all"
                    />
                    {searchQuery && (
                      <button
                        onClick={() => setSearchQuery('')}
                        className="absolute right-2.5 top-1/2 -translate-y-1/2 text-zinc-500 hover:text-zinc-300 p-0.5"
                      >
                        <X className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </div>

                  <button
                    onClick={() => setShowInlineChart(!showInlineChart)}
                    className={`flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium border transition-colors cursor-pointer shrink-0 ${
                      showInlineChart
                        ? 'bg-zinc-800 text-emerald-300 border-zinc-600'
                        : 'bg-zinc-900 text-zinc-400 border-zinc-800 hover:text-zinc-200'
                    }`}
                    title={showInlineChart ? 'Hide Hourly Activity Chart' : 'Show Hourly Activity Chart'}
                  >
                    <BarChart2 className="w-3.5 h-3.5 text-emerald-400" />
                    <span className="hidden sm:inline">Chart</span>
                  </button>

                  <button
                    onClick={() => setShowAdvancedFilters(!showAdvancedFilters)}
                    className={`flex items-center gap-1.5 px-3 py-2 rounded-lg text-xs font-medium border transition-colors cursor-pointer shrink-0 ${
                      showAdvancedFilters || hasActiveFilters
                        ? 'bg-zinc-800 text-zinc-100 border-zinc-600'
                        : 'bg-zinc-900 text-zinc-400 border-zinc-800 hover:text-zinc-200'
                    }`}
                  >
                    <SlidersHorizontal className="w-3.5 h-3.5 text-emerald-400" />
                    <span>Filters</span>
                    {hasActiveFilters && (
                      <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
                    )}
                  </button>

                  {hasActiveFilters && (
                    <button
                      onClick={handleResetFilters}
                      className="flex items-center gap-1 px-2.5 py-2 text-xs text-zinc-400 hover:text-rose-400 hover:bg-zinc-900 rounded-lg transition-colors cursor-pointer shrink-0"
                      title="Reset all filters"
                    >
                      <RotateCcw className="w-3.5 h-3.5" />
                      <span className="hidden sm:inline">Reset</span>
                    </button>
                  )}
                </div>

                {/* Filter Dropdowns Grid */}
                {showAdvancedFilters && (
                  <div className="pt-2 border-t border-zinc-800/80 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-2.5">
                    {/* Record Type Dropdown */}
                    <div className="space-y-1">
                      <label className="text-[11px] font-medium text-zinc-400 flex items-center gap-1">
                        <Tag className="w-3 h-3 text-zinc-500" />
                        Record Type
                      </label>
                      <select
                        value={typeFilter}
                        onChange={(e) => setTypeFilter(e.target.value as any)}
                        className="w-full px-2.5 py-1.5 bg-zinc-900 border border-zinc-700/80 rounded-lg text-xs text-zinc-200 focus:outline-hidden focus:border-emerald-500 cursor-pointer"
                      >
                        <option value="ALL">All Types (Personnel & Vehicle)</option>
                        <option value="PERSON">Personnel Only (P-###)</option>
                        <option value="VEHICLE">Vehicles Only (V-###)</option>
                      </select>
                    </div>

                    {/* Direction / Action Dropdown */}
                    <div className="space-y-1">
                      <label className="text-[11px] font-medium text-zinc-400 flex items-center gap-1">
                        <Filter className="w-3 h-3 text-zinc-500" />
                        Movement Direction
                      </label>
                      <select
                        value={actionFilter}
                        onChange={(e) => setActionFilter(e.target.value as any)}
                        className="w-full px-2.5 py-1.5 bg-zinc-900 border border-zinc-700/80 rounded-lg text-xs text-zinc-200 focus:outline-hidden focus:border-emerald-500 cursor-pointer"
                      >
                        <option value="ALL">All Actions (Entry & Exit)</option>
                        <option value="ENTRY">Entry Scans Only</option>
                        <option value="EXIT">Exit Scans Only</option>
                      </select>
                    </div>

                    {/* Gate Location Dropdown */}
                    <div className="space-y-1">
                      <label className="text-[11px] font-medium text-zinc-400 flex items-center gap-1">
                        <MapPin className="w-3 h-3 text-zinc-500" />
                        Gate Location
                      </label>
                      <select
                        value={gateFilter}
                        onChange={(e) => setGateFilter(e.target.value)}
                        className="w-full px-2.5 py-1.5 bg-zinc-900 border border-zinc-700/80 rounded-lg text-xs text-zinc-200 focus:outline-hidden focus:border-emerald-500 cursor-pointer"
                      >
                        <option value="ALL">All Gate Checkpoints</option>
                        {availableGates.map((gate) => (
                          <option key={gate} value={gate}>
                            {gate}
                          </option>
                        ))}
                      </select>
                    </div>

                    {/* Date Range Dropdown */}
                    <div className="space-y-1">
                      <label className="text-[11px] font-medium text-zinc-400 flex items-center gap-1">
                        <Calendar className="w-3 h-3 text-zinc-500" />
                        Date Range
                      </label>
                      <select
                        value={dateRangeFilter}
                        onChange={(e) => setDateRangeFilter(e.target.value as any)}
                        className="w-full px-2.5 py-1.5 bg-zinc-900 border border-zinc-700/80 rounded-lg text-xs text-zinc-200 focus:outline-hidden focus:border-emerald-500 cursor-pointer"
                      >
                        <option value="ALL">All Historic Dates</option>
                        <option value="TODAY">Today Only</option>
                        <option value="LAST_24H">Last 24 Hours</option>
                        <option value="LAST_7D">Last 7 Days</option>
                        <option value="CUSTOM">Custom Date Range...</option>
                      </select>
                    </div>

                    {/* Custom Date Pickers (visible when CUSTOM selected) */}
                    {dateRangeFilter === 'CUSTOM' && (
                      <div className="sm:col-span-2 lg:col-span-4 grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1 bg-zinc-900/50 p-2.5 rounded-lg border border-zinc-800">
                        <div className="space-y-1">
                          <span className="text-[10px] text-zinc-400">From Date:</span>
                          <input
                            type="date"
                            value={customStartDate}
                            onChange={(e) => setCustomStartDate(e.target.value)}
                            className="w-full px-2 py-1 bg-zinc-900 border border-zinc-700 rounded text-xs text-zinc-200"
                          />
                        </div>
                        <div className="space-y-1">
                          <span className="text-[10px] text-zinc-400">To Date:</span>
                          <input
                            type="date"
                            value={customEndDate}
                            onChange={(e) => setCustomEndDate(e.target.value)}
                            className="w-full px-2 py-1 bg-zinc-900 border border-zinc-700 rounded text-xs text-zinc-200"
                          />
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* Filter Summary Tags / Status */}
                <div className="flex items-center justify-between text-[11px] text-zinc-400 pt-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span>
                      Showing <strong className="text-zinc-200 font-semibold">{records.length}</strong> matching of{' '}
                      <strong className="text-zinc-200 font-semibold">{totalServerCount}</strong> stored records
                    </span>
                    {hasActiveFilters && (
                      <span className="px-2 py-0.5 rounded-full bg-emerald-950/80 border border-emerald-800 text-emerald-300 text-[10px]">
                        Filtered Search Active
                      </span>
                    )}
                  </div>

                  <span className="font-mono text-[10px] text-zinc-500">
                    GET /api/activities
                  </span>
                </div>
              </div>

              {/* Hourly Activity Volume Chart Visualization */}
              {showInlineChart && (
                <HourlyActivityChart records={records} isLoading={isQueryingRecords} />
              )}

              {/* Records List Container */}
              <div className="space-y-2 max-h-[460px] overflow-y-auto pr-1">
                {isQueryingRecords ? (
                  <div className="p-8 text-center text-zinc-400 text-xs flex items-center justify-center gap-2 bg-zinc-950/40 rounded-xl border border-zinc-800">
                    <RefreshCw className="w-4 h-4 animate-spin text-emerald-400" />
                    <span>Querying stored activity records...</span>
                  </div>
                ) : records.length === 0 ? (
                  <div className="p-8 text-center text-zinc-400 text-xs space-y-2 bg-zinc-950/40 rounded-xl border border-zinc-800">
                    <Database className="w-8 h-8 text-zinc-600 mx-auto" />
                    <p className="font-medium text-zinc-300">No activity records match the selected filters.</p>
                    <p className="text-[11px] text-zinc-500">
                      Try broadening your search term or clearing date/gate filters.
                    </p>
                    {hasActiveFilters && (
                      <button
                        onClick={handleResetFilters}
                        className="px-3 py-1 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded-lg text-xs mt-2 transition-colors cursor-pointer"
                      >
                        Clear All Filters
                      </button>
                    )}
                  </div>
                ) : (
                  records.map((r) => (
                    <div
                      key={r.id}
                      className="p-3 bg-zinc-950/70 hover:bg-zinc-950/90 border border-zinc-800/90 hover:border-zinc-700 rounded-xl flex items-center justify-between text-xs transition-colors"
                    >
                      <div className="flex items-center gap-3">
                        <div
                          className={`p-2 rounded-lg font-mono text-[10px] font-bold shrink-0 ${
                            r.action === 'ENTRY'
                              ? 'bg-emerald-950/80 text-emerald-300 border border-emerald-800'
                              : 'bg-amber-950/80 text-amber-300 border border-amber-800'
                          }`}
                        >
                          {r.action}
                        </div>
                        <div>
                          <div className="font-semibold text-zinc-100 flex items-center gap-2 flex-wrap">
                            <button
                              onClick={() => setSelectedIndividualId(r.targetId)}
                              className="flex items-center gap-1.5 hover:text-emerald-400 hover:underline cursor-pointer text-left transition-colors font-semibold"
                              title="Click to view military stay dossier & location breakdown"
                            >
                              {r.type === 'VEHICLE' ? (
                                <Truck className="w-3.5 h-3.5 text-amber-400 shrink-0" />
                              ) : (
                                <User className="w-3.5 h-3.5 text-blue-400 shrink-0" />
                              )}
                              <span>{r.title}</span>
                              <ExternalLink className="w-3 h-3 text-zinc-500 hover:text-emerald-400" />
                            </button>
                            <span className="font-mono text-[10px] text-zinc-400 px-1.5 py-0.5 rounded bg-zinc-800">
                              {r.targetId}
                            </span>
                            {r.serviceNumber && (
                              <span className="font-mono text-[10px] text-emerald-400 px-1.5 py-0.5 rounded bg-emerald-950/70 border border-emerald-800/60">
                                {r.serviceNumber}
                              </span>
                            )}
                            {r.tag && (
                              <span className="text-[9px] px-1.5 py-0.2 rounded-full bg-zinc-800 text-zinc-300">
                                {r.tag}
                              </span>
                            )}
                          </div>
                          <div className="text-[11px] text-zinc-400 mt-0.5">
                            <span className="text-zinc-300">{r.location}</span> •{' '}
                            <span className="text-emerald-400/90 font-medium">{r.gate}</span> •{' '}
                            <span className="text-zinc-500">Operator: {r.gatekeeperId}</span>
                          </div>

                          {/* Specific Year / Month / Days / Hours / Mins stayed & prior arrival interval */}
                          {r.action === 'EXIT' && (r.stayDurationFormatted || r.stayDuration) && (
                            <div className="text-[11px] text-emerald-400 font-mono flex items-center gap-1.5 mt-1 bg-emerald-950/40 px-2 py-0.5 rounded border border-emerald-800/40 w-fit">
                              <Clock className="w-3 h-3 text-emerald-400 shrink-0" />
                              <span>Time Stayed: <strong>{r.stayDurationFormatted || r.stayDuration}</strong></span>
                            </div>
                          )}

                          {r.action === 'ENTRY' && r.timeSinceLastVisitFormatted && (
                            <div className="text-[11px] text-amber-400/90 font-mono flex items-center gap-1.5 mt-1 bg-amber-950/30 px-2 py-0.5 rounded border border-amber-800/30 w-fit">
                              <History className="w-3 h-3 text-amber-400 shrink-0" />
                              <span>How long before arrival: <strong>{r.timeSinceLastVisitFormatted}</strong></span>
                            </div>
                          )}
                        </div>
                      </div>

                      <div className="text-right shrink-0 flex items-center gap-2">
                        <div>
                          <div className="font-mono text-xs text-zinc-200">{r.timestamp}</div>
                          <div className="font-mono text-[10px] text-zinc-500">
                            #{r.serverSequence !== undefined ? r.serverSequence : r.id}
                          </div>
                        </div>
                        <button
                          onClick={() => setSelectedIndividualId(r.targetId)}
                          className="p-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white transition-colors cursor-pointer"
                          title="Open full individual dossier"
                        >
                          <ExternalLink className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>
          )}

          {activeTab === 'CHARTS' && (
            <div className="space-y-4">
              {/* Quick Analytics Header */}
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl">
                <div>
                  <h3 className="text-xs font-semibold text-zinc-100 flex items-center gap-1.5">
                    <BarChart2 className="w-4 h-4 text-emerald-400" />
                    Hourly Activity Volume Visualizer
                  </h3>
                  <p className="text-[11px] text-zinc-400">
                    Real-time hourly histogram showing throughput volume, movement direction, and entity classification
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-medium text-zinc-300">
                    {records.length} of {totalServerCount} records analyzed
                  </span>
                  <button
                    onClick={() => setActiveTab('LIVE_DATA')}
                    className="px-2.5 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded-lg text-xs transition-colors cursor-pointer flex items-center gap-1 shrink-0"
                  >
                    <span>View Record List</span>
                    <ChevronRight className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>

              {/* Chart Component with Recharts */}
              <HourlyActivityChart records={records} isLoading={isQueryingRecords} />

              {/* Hourly Traffic Breakdown Table */}
              <div className="p-3.5 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h4 className="text-xs font-semibold text-zinc-200 flex items-center gap-1.5">
                      <Clock className="w-3.5 h-3.5 text-zinc-400" />
                      Hourly Volume Breakdown Matrix
                    </h4>
                    <p className="text-[11px] text-zinc-400">
                      Ranked breakdown of scan throughput and direction flux by hour
                    </p>
                  </div>
                  <span className="text-[10px] font-mono text-zinc-500">
                    {hourlyTableRows.length} Active {hourlyTableRows.length === 1 ? 'Hour' : 'Hours'}
                  </span>
                </div>

                {hourlyTableRows.length === 0 ? (
                  <div className="p-6 text-center text-xs text-zinc-500 bg-zinc-900/40 rounded-lg border border-zinc-800">
                    No activity records found matching the active filters.
                  </div>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full text-xs text-left">
                      <thead>
                        <tr className="border-b border-zinc-800 text-[11px] text-zinc-400 font-medium">
                          <th className="py-2 px-2.5">Time Interval</th>
                          <th className="py-2 px-2.5">Entries</th>
                          <th className="py-2 px-2.5">Exits</th>
                          <th className="py-2 px-2.5">Personnel / Vehicle</th>
                          <th className="py-2 px-2.5 text-right">Total Scans</th>
                          <th className="py-2 px-2.5 text-right w-36">Volume Share</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-zinc-800/60 font-mono text-[11px]">
                        {hourlyTableRows.map((row) => {
                          const maxTotal = hourlyTableRows[0]?.total || 1;
                          const pctOfMax = Math.round((row.total / maxTotal) * 100);
                          const totalVolume = records.length || 1;
                          const pctOfTotal = Math.round((row.total / totalVolume) * 100);

                          return (
                            <tr key={row.hour} className="hover:bg-zinc-900/50 transition-colors">
                              <td className="py-2 px-2.5 text-zinc-200 font-semibold flex items-center gap-1.5">
                                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
                                {row.hourLabel}
                              </td>
                              <td className="py-2 px-2.5 text-emerald-400 font-medium">{row.entries}</td>
                              <td className="py-2 px-2.5 text-amber-400 font-medium">{row.exits}</td>
                              <td className="py-2 px-2.5 text-zinc-300">
                                <span className="text-blue-400">{row.personnel}P</span> /{' '}
                                <span className="text-orange-400">{row.vehicles}V</span>
                              </td>
                              <td className="py-2 px-2.5 text-right font-bold text-zinc-100">
                                {row.total}
                              </td>
                              <td className="py-2 px-2.5 text-right">
                                <div className="flex items-center gap-2 justify-end">
                                  <div className="w-20 bg-zinc-800 rounded-full h-1.5 overflow-hidden">
                                    <div
                                      className="bg-emerald-400 h-full rounded-full"
                                      style={{ width: `${pctOfMax}%` }}
                                    />
                                  </div>
                                  <span className="text-[10px] text-zinc-400 w-8 text-right">
                                    {pctOfTotal}%
                                  </span>
                                </div>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}

          {activeTab === 'SUMMARIES' && (
            <MilitaryPeriodicSummary
              onSelectIndividual={(targetId) => setSelectedIndividualId(targetId)}
            />
          )}

          {activeTab === 'OVERVIEW' && (
            <div className="space-y-4">
              {/* Telemetry Stat Cards */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
                <div className="p-3 bg-zinc-950/70 border border-zinc-800 rounded-xl">
                  <span className="text-[11px] text-zinc-400 block font-medium">Total Stored</span>
                  <div className="text-xl font-bold text-white mt-0.5">
                    {statsData ? statsData.totalRecords : '—'}
                  </div>
                  <span className="text-[10px] text-emerald-400">Persistent Disk JSON</span>
                </div>
                <div className="p-3 bg-zinc-950/70 border border-zinc-800 rounded-xl">
                  <span className="text-[11px] text-zinc-400 block font-medium">Entries / Exits</span>
                  <div className="text-xl font-bold text-white mt-0.5">
                    {statsData ? `${statsData.entriesCount} / ${statsData.exitsCount}` : '—'}
                  </div>
                  <span className="text-[10px] text-zinc-400">Direction breakdown</span>
                </div>
                <div className="p-3 bg-zinc-950/70 border border-zinc-800 rounded-xl">
                  <span className="text-[11px] text-zinc-400 block font-medium">Personnel / Vehicles</span>
                  <div className="text-xl font-bold text-white mt-0.5">
                    {statsData ? `${statsData.personnelRecords} / ${statsData.vehicleRecords}` : '—'}
                  </div>
                  <span className="text-[10px] text-zinc-400">Entity breakdown</span>
                </div>
                <div className="p-3 bg-zinc-950/70 border border-zinc-800 rounded-xl">
                  <span className="text-[11px] text-zinc-400 block font-medium">Server Uptime</span>
                  <div className="text-xl font-bold text-emerald-400 mt-0.5">
                    {healthData ? `${healthData.uptimeSeconds}s` : '—'}
                  </div>
                  <span className="text-[10px] text-zinc-400">Node.js Express 4.x</span>
                </div>
              </div>

              {/* Hourly Traffic Visualizer Shortcut Card */}
              <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div className="flex items-center gap-3">
                  <div className="p-2.5 rounded-lg bg-blue-500/10 border border-blue-500/20 text-blue-400 shrink-0">
                    <BarChart2 className="w-5 h-5" />
                  </div>
                  <div>
                    <h4 className="text-xs font-semibold text-zinc-200 flex items-center gap-1.5">
                      Hourly Activity Volume Visualizer
                      <span className="text-[10px] font-mono px-1.5 py-0.2 rounded-full bg-blue-950 text-blue-300 border border-blue-850">
                        Interactive
                      </span>
                    </h4>
                    <p className="text-[11px] text-zinc-400">
                      Visualize 24-hour scan distribution, rush hours, entry/exit directional flux, and entity types.
                    </p>
                  </div>
                </div>
                <button
                  onClick={() => setActiveTab('CHARTS')}
                  className="px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 rounded-lg text-xs font-medium transition-colors cursor-pointer flex items-center gap-1.5 shrink-0 self-start sm:self-auto"
                >
                  <span>Open Chart</span>
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>

              {/* Interactive Ingestion Ping Tool */}
              <div className="p-4 bg-zinc-950/80 border border-zinc-800 rounded-xl space-y-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-xs font-semibold text-white flex items-center gap-1.5">
                      <Send className="w-3.5 h-3.5 text-emerald-400" />
                      Test Incoming Ingestion Endpoint
                    </h3>
                    <p className="text-[11px] text-zinc-400">
                      Dispatches a simulated mobile activity payload to <code className="text-emerald-300">POST /api/activities</code>
                    </p>
                  </div>
                  <button
                    onClick={handleSendTestRecord}
                    disabled={testSending}
                    className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-xs transition-colors cursor-pointer"
                  >
                    {testSending ? (
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <Send className="w-3.5 h-3.5" />
                    )}
                    Send Ingestion Ping
                  </button>
                </div>

                {lastTestResult && (
                  <div className="p-3 bg-zinc-900 border border-zinc-800 rounded-lg text-xs space-y-1">
                    <div className="flex items-center justify-between text-[11px]">
                      <span className="font-semibold text-emerald-400">
                        Response HTTP {lastTestResult.status}
                      </span>
                      <span className="text-zinc-500 font-mono text-[10px]">
                        {new Date().toLocaleTimeString()}
                      </span>
                    </div>
                    <pre className="text-[11px] font-mono text-zinc-300 bg-black/40 p-2 rounded overflow-x-auto max-h-36">
                      {JSON.stringify(lastTestResult.data, null, 2)}
                    </pre>
                  </div>
                )}
              </div>

              {/* Server Architecture Note */}
              <div className="p-3.5 bg-zinc-950/50 border border-zinc-800/80 rounded-xl text-xs space-y-1.5">
                <h4 className="font-semibold text-zinc-200 flex items-center gap-1.5">
                  <Database className="w-3.5 h-3.5 text-zinc-400" />
                  Persistence & Storage Architecture
                </h4>
                <p className="text-[11px] text-zinc-400 leading-relaxed">
                  Incoming records sent from mobile clients are validated, assigned an authoritative
                  monotonic sequence number, and stored in JSON persistent storage at <code className="text-zinc-300">data/activity_records.json</code>.
                  Duplicate submissions with the same record ID are automatically detected and acknowledged idempotently.
                </p>
              </div>
            </div>
          )}

          {activeTab === 'ENDPOINTS' && (
            <div className="space-y-3">
              {endpointsList.map((ep, idx) => (
                <div
                  key={idx}
                  className="p-3.5 bg-zinc-950/70 border border-zinc-800 rounded-xl space-y-2 text-xs"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <span
                        className={`font-mono text-[10px] font-bold px-2 py-0.5 rounded ${
                          ep.method === 'POST'
                            ? 'bg-emerald-950 text-emerald-300 border border-emerald-800'
                            : 'bg-blue-950 text-blue-300 border border-blue-800'
                        }`}
                      >
                        {ep.method}
                      </span>
                      <code className="font-mono text-zinc-100 font-semibold">{ep.path}</code>
                    </div>
                    <button
                      onClick={() => handleCopy(`${ep.method} ${ep.path}`, `ep-${idx}`)}
                      className="p-1 text-zinc-400 hover:text-white rounded"
                      title="Copy endpoint"
                    >
                      {copiedEndpoint === `ep-${idx}` ? (
                        <Check className="w-3.5 h-3.5 text-emerald-400" />
                      ) : (
                        <Copy className="w-3.5 h-3.5" />
                      )}
                    </button>
                  </div>
                  <p className="text-[11px] text-zinc-400">{ep.desc}</p>
                  {ep.payload && (
                    <div className="bg-black/60 p-2 rounded text-[11px] font-mono text-zinc-300">
                      <div className="text-[10px] text-zinc-500 mb-1">Example Request Body:</div>
                      <pre className="overflow-x-auto">{ep.payload}</pre>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}

          {activeTab === 'CURL' && (
            <div className="space-y-3 text-xs">
              <div className="space-y-1.5">
                <span className="font-semibold text-zinc-200">1. Query Records with Filters (Search, Gate & Type)</span>
                <pre className="p-3 bg-black/60 border border-zinc-800 rounded-xl font-mono text-[11px] text-emerald-300 overflow-x-auto">
{`curl -s "http://localhost:3000/api/activities?type=PERSON&gate=Gate%2002&q=Logistics"`}
                </pre>
              </div>

              <div className="space-y-1.5">
                <span className="font-semibold text-zinc-200">2. Ingest Single Activity Record</span>
                <pre className="p-3 bg-black/60 border border-zinc-800 rounded-xl font-mono text-[11px] text-emerald-300 overflow-x-auto">
{`curl -X POST http://localhost:3000/api/activities \\
  -H "Content-Type: application/json" \\
  -d '{
    "id": "ACT-105",
    "timestamp": "18:30:00",
    "type": "PERSON",
    "action": "ENTRY",
    "targetId": "P-001",
    "title": "John Doe",
    "location": "Location 07",
    "gate": "Gate 02",
    "gatekeeperId": "GK-04",
    "tag": "Staff"
  }'`}
                </pre>
              </div>

              <div className="space-y-1.5">
                <span className="font-semibold text-zinc-200">3. Batch Synchronization</span>
                <pre className="p-3 bg-black/60 border border-zinc-800 rounded-xl font-mono text-[11px] text-emerald-300 overflow-x-auto">
{`curl -X POST http://localhost:3000/api/activities/batch \\
  -H "Content-Type: application/json" \\
  -d '{
    "deviceId": "TAB-GATE-04",
    "operatorId": "GK-04",
    "records": [
      { "id": "ACT-201", "type": "PERSON", "action": "ENTRY", "targetId": "P-002" },
      { "id": "ACT-202", "type": "VEHICLE", "action": "EXIT", "targetId": "V-014" }
    ]
  }'`}
                </pre>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between px-5 py-3 border-t border-zinc-800 bg-zinc-950/70 text-xs text-zinc-400 shrink-0">
          <span className="flex items-center gap-1.5">
            <span className="w-2 h-2 rounded-full bg-emerald-400" />
            Backend Ready & Running on Port 3000
          </span>
          <button
            onClick={onClose}
            className="px-3.5 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-white rounded-lg transition-colors cursor-pointer text-xs font-medium"
          >
            Close
          </button>
        </div>
      </div>

      {selectedIndividualId && (
        <IndividualHistoryModal
          targetId={selectedIndividualId}
          onClose={() => setSelectedIndividualId(null)}
        />
      )}
    </div>
  );
};
