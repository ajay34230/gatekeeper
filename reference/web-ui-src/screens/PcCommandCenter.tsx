import React, { useState, useEffect } from 'react';
import {
  Monitor,
  Shield,
  Activity,
  MapPin,
  Users,
  Truck,
  AlertTriangle,
  Radio,
  Server,
  Database,
  ArrowUpRight,
  ArrowDownLeft,
  Search,
  Filter,
  RefreshCw,
  ExternalLink,
  Sliders,
  Maximize2,
  Terminal,
  Clock,
  CheckCircle2,
  AlertOctagon,
  Download,
  User,
  FileText,
  FileSpreadsheet,
  Wifi,
  Smartphone,
  Check,
  X,
  QrCode,
  UserPlus,
  Edit3,
  Layers,
  Building2,
  Phone,
  Home,
  CreditCard,
  Tag,
  Printer,
  Table as TableIcon,
  FolderDown,
  KeyRound,
  Trash2,
} from 'lucide-react';
import { ActivityRecord, Personnel, Vehicle, GatekeeperSession, MilitaryCompany } from '../types';
import { formatCurrentTime } from '../data/mockDatabase';
import { downloadPersonnelHistoryCsv } from '../utils/csvExport';
import {
  downloadPersonnelHistoryExcel,
  downloadSixCompanyExcel,
  ALL_COMPANIES,
  COMPANY_THEME,
  DeDuplicationResult,
} from '../utils/excelExport';
import { NativeInstallModal } from '../components/NativeInstallModal';
import { MilitaryIdCardModal } from '../components/MilitaryIdCardModal';
import { PersonnelEditorModal } from '../components/PersonnelEditorModal';
import { PersonnelImportExportModal } from '../components/PersonnelImportExportModal';

interface GateMetrics {
  id: string;
  name: string;
  location: string;
  status: 'ONLINE' | 'STANDBY' | 'OFFLINE';
  operator: string;
  recentActivityCount: number;
  todayEntries: number;
  todayExits: number;
  mismatchesFlagged: number;
  lastActivity: string;
}

interface PcCommandCenterProps {
  activities: ActivityRecord[];
  personnelList: Personnel[];
  vehicleList: Vehicle[];
  session: GatekeeperSession | null;
  onExitPcMode: () => void;
  onSelectPerson?: (personId: string) => void;
  onPurgeData?: () => Promise<void>;
}

export const PcCommandCenter: React.FC<PcCommandCenterProps> = ({
  activities,
  personnelList,
  vehicleList,
  session,
  onExitPcMode,
  onPurgeData,
}) => {
  const [gates, setGates] = useState<GateMetrics[]>([]);
  const [loadingGates, setLoadingGates] = useState<boolean>(true);
  const [selectedGateFilter, setSelectedGateFilter] = useState<string>('ALL');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedTab, setSelectedTab] = useState<'LIVE_FEED' | 'PERSONNEL' | 'VEHICLES' | 'SERVER_API'>('LIVE_FEED');
  const [apiHealth, setApiHealth] = useState<any>(null);
  const [serverLatency, setServerLatency] = useState<number>(24);
  const [lastHeartbeatTime, setLastHeartbeatTime] = useState<string>(formatCurrentTime());
  const [selectedPerson, setSelectedPerson] = useState<Personnel | null>(null);
  const [downloadSuccessId, setDownloadSuccessId] = useState<string | null>(null);
  const [downloadFormatSuccess, setDownloadFormatSuccess] = useState<'excel' | 'csv' | null>(null);
  const [showNativeModal, setShowNativeModal] = useState<boolean>(false);
  const [isPurging, setIsPurging] = useState<boolean>(false);
  const [purgeSuccess, setPurgeSuccess] = useState<boolean>(false);

  // Personnel Management & 6-Company Support
  const [localPersonnelList, setLocalPersonnelList] = useState<Personnel[]>(personnelList);
  const [selectedCompanyFilter, setSelectedCompanyFilter] = useState<'ALL' | MilitaryCompany>('ALL');
  const [idCardPerson, setIdCardPerson] = useState<Personnel | null>(null);
  const [showEditorModal, setShowEditorModal] = useState<boolean>(false);
  const [personnelToEdit, setPersonnelToEdit] = useState<Personnel | null>(null);
  const [showImportExportModal, setShowImportExportModal] = useState<boolean>(false);
  const [excelExportSuccess, setExcelExportSuccess] = useState<boolean>(false);

  // Synchronize when personnelList prop updates
  useEffect(() => {
    if (personnelList && personnelList.length > 0) {
      setLocalPersonnelList((prev) => {
        const merged = [...prev];
        personnelList.forEach((incoming) => {
          const idx = merged.findIndex(
            (m) =>
              m.id === incoming.id ||
              (m.armyNumber && incoming.armyNumber && m.armyNumber.toLowerCase() === incoming.armyNumber.toLowerCase())
          );
          if (idx >= 0) {
            merged[idx] = { ...merged[idx], ...incoming };
          } else {
            merged.push(incoming);
          }
        });
        return merged;
      });
    }
  }, [personnelList]);

  // Handler to add or edit personnel
  const handleSavePersonnel = (person: Personnel) => {
    setLocalPersonnelList((prev) => {
      const idx = prev.findIndex(
        (p) =>
          p.id === person.id ||
          (p.armyNumber && person.armyNumber && p.armyNumber.toLowerCase() === person.armyNumber.toLowerCase())
      );
      if (idx >= 0) {
        const updated = [...prev];
        updated[idx] = person;
        return updated;
      }
      return [person, ...prev];
    });

    if (selectedPerson && selectedPerson.id === person.id) {
      setSelectedPerson(person);
    }
  };

  // Handler for batch import
  const handleImportComplete = (mergedList: Personnel[], _stats: DeDuplicationResult) => {
    setLocalPersonnelList(mergedList);
  };

  // Handler for 6-Company Excel download
  const handleDownloadSixCompanyExcelAll = () => {
    downloadSixCompanyExcel(localPersonnelList, activities);
    setExcelExportSuccess(true);
    setTimeout(() => setExcelExportSuccess(false), 3000);
  };

  const handleDownloadPersonReport = (person: Personnel, e?: React.MouseEvent) => {
    if (e) {
      e.stopPropagation();
    }
    downloadPersonnelHistoryCsv(person, activities);
    setDownloadSuccessId(person.id);
    setDownloadFormatSuccess('csv');
    setTimeout(() => {
      setDownloadSuccessId(null);
      setDownloadFormatSuccess(null);
    }, 2500);
  };

  const handleDownloadPersonExcel = (person: Personnel, e?: React.MouseEvent) => {
    if (e) {
      e.stopPropagation();
    }
    downloadPersonnelHistoryExcel(person, activities);
    setDownloadSuccessId(person.id);
    setDownloadFormatSuccess('excel');
    setTimeout(() => {
      setDownloadSuccessId(null);
      setDownloadFormatSuccess(null);
    }, 2500);
  };

  // Fetch gate status & metrics from Express server API
  const fetchGateTelemetry = async () => {
    try {
      const start = Date.now();
      const res = await fetch('/api/gates/status');
      if (res.ok) {
        const data = await res.json();
        setGates(data.gates || []);
      }
      setServerLatency(Date.now() - start);
    } catch (e) {
      console.warn('[PC Command] Server telemetry fetch error, using fallback');
    } finally {
      setLoadingGates(false);
      setLastHeartbeatTime(formatCurrentTime());
    }
  };

  // Fetch Express health stats
  const fetchApiHealth = async () => {
    try {
      const res = await fetch('/api/health');
      if (res.ok) {
        const data = await res.json();
        setApiHealth(data);
      }
    } catch (e) {
      // offline
    }
  };

  useEffect(() => {
    fetchGateTelemetry();
    fetchApiHealth();
    const interval = setInterval(() => {
      fetchGateTelemetry();
      fetchApiHealth();
    }, 8000);
    return () => clearInterval(interval);
  }, []);

  // Compute live presence totals
  const insidePersonnel = localPersonnelList.filter((p) => p.currentStatus === 'INSIDE');
  const outsidePersonnel = localPersonnelList.filter((p) => p.currentStatus === 'OUTSIDE');
  const insideVehicles = vehicleList.filter((v) => v.currentStatus === 'INSIDE');
  const totalMismatches = activities.filter((a) => a.locationMismatch).length;
  const recentActivities = activities.slice(0, 50);

  const filteredActivities = recentActivities.filter((act) => {
    const matchesGate =
      selectedGateFilter === 'ALL' ||
      act.location === selectedGateFilter ||
      act.gate.toLowerCase().includes(selectedGateFilter.toLowerCase());

    const matchesQuery =
      !searchQuery ||
      act.targetId.toLowerCase().includes(searchQuery.toLowerCase()) ||
      act.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      act.location.toLowerCase().includes(searchQuery.toLowerCase());

    return matchesGate && matchesQuery;
  });

  return (
    <div id="pc-command-center" className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col font-sans selection:bg-amber-500/30">
      {/* Top PC Master Command Bar */}
      <header className="h-16 border-b border-zinc-800 bg-zinc-900/90 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-40">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2.5">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-500 to-amber-600 flex items-center justify-center text-zinc-950 shadow-md">
              <Monitor className="w-5 h-5 stroke-[2.5]" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="font-extrabold text-sm tracking-wider uppercase text-zinc-100">
                  HQ Central Command
                </span>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-mono font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                  PC SERVER ACTIVE
                </span>
              </div>
              <p className="text-[11px] text-zinc-400 font-mono">
                XV DIGITAL ACCESS CONTROL Multi-Gate Telemetry • Express Node.js & Cloud Firestore
              </p>
            </div>
          </div>

          <div className="hidden lg:flex items-center gap-1.5 bg-zinc-950/80 px-3 py-1.5 rounded-lg border border-zinc-800 text-xs font-mono">
            <Server className="w-3.5 h-3.5 text-zinc-400" />
            <span className="text-zinc-400">Server Latency:</span>
            <span className="text-emerald-400 font-bold">{serverLatency}ms</span>
            <span className="text-zinc-600 mx-1">|</span>
            <Database className="w-3.5 h-3.5 text-amber-400" />
            <span className="text-zinc-400">DB:</span>
            <span className="text-amber-400 font-bold">Firestore Cloud</span>
          </div>
        </div>

        {/* Action Controls */}
        <div className="flex items-center gap-2">
          {/* Quick Action: Add Soldier */}
          <button
            onClick={() => {
              setPersonnelToEdit(null);
              setShowEditorModal(true);
            }}
            className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-400 hover:bg-amber-300 text-slate-950 text-xs font-bold shadow-sm transition-all hover:scale-105 cursor-pointer"
            id="btn-add-soldier-header"
            title="Add Soldier Personal Details with Custom Tables & Cells"
          >
            <UserPlus className="w-3.5 h-3.5" />
            <span>+ Add Soldier</span>
          </button>

          {/* Quick Action: 6-Company Multi-Tab Excel */}
          <button
            onClick={handleDownloadSixCompanyExcelAll}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer shadow-sm ${
              excelExportSuccess
                ? 'bg-emerald-500 text-slate-950 font-black'
                : 'bg-emerald-950/80 hover:bg-emerald-900 text-emerald-300 border border-emerald-700/70'
            }`}
            id="btn-download-6tab-excel-header"
            title="Download Microsoft Excel Workbook (.xls) with 6 Company Tabs: Alpha, Bravo, Charlie, Delta, SP, HQ"
          >
            <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-400" />
            <span className="hidden md:inline">
              {excelExportSuccess ? '6-Tab Excel Saved!' : 'Excel (6 Tabs)'}
            </span>
            <span className="md:hidden">Excel</span>
          </button>

          {/* Quick Action: Import / Export */}
          <button
            onClick={() => setShowImportExportModal(true)}
            className="hidden md:flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs font-semibold border border-zinc-700 transition-colors cursor-pointer"
            id="btn-import-export-header"
            title="Import or Export Personnel Details (Excel & CSV with Anti-Duplicate Engine)"
          >
            <FolderDown className="w-3.5 h-3.5 text-sky-400" />
            <span>Import / Export</span>
          </button>

          {/* Local Wi-Fi & Native .EXE / .APK */}
          <button
            onClick={() => setShowNativeModal(true)}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-amber-500/10 hover:bg-amber-500/20 text-amber-300 text-xs font-semibold border border-amber-500/30 transition-colors cursor-pointer shadow-xs"
            id="btn-open-native-hub"
            title="Open Native Windows .EXE, Android .APK, and Local Wi-Fi Pairing Center"
          >
            <Wifi className="w-3.5 h-3.5 text-amber-400" />
            <span className="hidden xl:inline">Local Wi-Fi & Native (.EXE / .APK)</span>
            <span className="xl:hidden">Installers</span>
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
          </button>

          <button
            onClick={() => {
              fetchGateTelemetry();
              fetchApiHealth();
            }}
            title="Refresh Server Data"
            className="p-2 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-300 transition-colors cursor-pointer"
          >
            <RefreshCw className="w-4 h-4" />
          </button>

          <button
            onClick={onExitPcMode}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs font-semibold border border-zinc-700 transition-colors cursor-pointer"
          >
            <Radio className="w-3.5 h-3.5 text-amber-400" />
            <span className="hidden sm:inline">Switch to Gatekeeper Device</span>
            <span className="sm:hidden">Mobile</span>
          </button>
        </div>
      </header>

      {/* Main Command Console Layout */}
      <div className="flex-1 flex flex-col xl:flex-row overflow-hidden">
        {/* Left Side: Real-Time Multi-Gate Monitor & Topology */}
        <aside className="w-full xl:w-96 border-b xl:border-b-0 xl:border-r border-zinc-800 bg-zinc-900/40 p-5 flex flex-col gap-5 shrink-0 overflow-y-auto">
          {/* Live High-Level Metrics Bento */}
          <div>
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-xs font-bold uppercase tracking-wider text-zinc-400 flex items-center gap-1.5">
                <Activity className="w-3.5 h-3.5 text-amber-500" />
                Base Presence Telemetry
              </h2>
              <span className="text-[10px] font-mono text-zinc-500">Live Sync</span>
            </div>

            <div className="grid grid-cols-2 gap-2.5">
              <div className="p-3.5 bg-zinc-900/80 rounded-xl border border-zinc-800/80">
                <div className="flex items-center justify-between text-zinc-400 mb-1">
                  <span className="text-xs font-medium">Inside Facility</span>
                  <Users className="w-4 h-4 text-emerald-400" />
                </div>
                <div className="text-2xl font-black font-mono text-emerald-400">
                  {insidePersonnel.length}
                </div>
                <div className="text-[10px] text-zinc-500 mt-1">
                  {outsidePersonnel.length} currently outside
                </div>
              </div>

              <div className="p-3.5 bg-zinc-900/80 rounded-xl border border-zinc-800/80">
                <div className="flex items-center justify-between text-zinc-400 mb-1">
                  <span className="text-xs font-medium">Fleet On-Base</span>
                  <Truck className="w-4 h-4 text-sky-400" />
                </div>
                <div className="text-2xl font-black font-mono text-sky-400">
                  {insideVehicles.length}
                </div>
                <div className="text-[10px] text-zinc-500 mt-1">
                  {vehicleList.length} tactical units registered
                </div>
              </div>

              <div className="p-3.5 bg-zinc-900/80 rounded-xl border border-zinc-800/80">
                <div className="flex items-center justify-between text-zinc-400 mb-1">
                  <span className="text-xs font-medium">Location Flags</span>
                  <AlertTriangle className="w-4 h-4 text-amber-400" />
                </div>
                <div className="text-2xl font-black font-mono text-amber-400">
                  {totalMismatches}
                </div>
                <div className="text-[10px] text-amber-400/80 mt-1">
                  Cross-location alerts recorded
                </div>
              </div>

              <div className="p-3.5 bg-zinc-900/80 rounded-xl border border-zinc-800/80">
                <div className="flex items-center justify-between text-zinc-400 mb-1">
                  <span className="text-xs font-medium">Total Events</span>
                  <Shield className="w-4 h-4 text-purple-400" />
                </div>
                <div className="text-2xl font-black font-mono text-purple-400">
                  {activities.length}
                </div>
                <div className="text-[10px] text-zinc-500 mt-1 font-mono">
                  Server ACK Verified
                </div>
              </div>
            </div>
          </div>

          {/* Multi-Gate Station Grid */}
          <div className="flex-1">
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-xs font-bold uppercase tracking-wider text-zinc-400 flex items-center gap-1.5">
                <MapPin className="w-3.5 h-3.5 text-sky-400" />
                Gate Station Network
              </h2>
              <span className="text-[10px] font-mono text-emerald-400 font-bold">
                {gates.filter((g) => g.status === 'ONLINE').length}/{gates.length || 5} ONLINE
              </span>
            </div>

            <div className="space-y-2.5">
              {(gates.length > 0 ? gates : [
                { id: 'G-01', name: 'Main HQ Gate 01', location: 'Location 07', status: 'ONLINE', operator: 'Sgt. D. Miller', recentActivityCount: 18, todayEntries: 12, todayExits: 6, mismatchesFlagged: 0, lastActivity: '23:41:02' },
                { id: 'G-02', name: 'Forward Gate 02', location: 'Location 07', status: 'ONLINE', operator: 'GK-04 Miller', recentActivityCount: 14, todayEntries: 8, todayExits: 6, mismatchesFlagged: 2, lastActivity: '23:35:10' },
                { id: 'G-03', name: 'Perimeter Checkpoint 03', location: 'Location 04', status: 'ONLINE', operator: 'Cpl. J. Ray', recentActivityCount: 7, todayEntries: 4, todayExits: 3, mismatchesFlagged: 1, lastActivity: '22:50:18' },
                { id: 'G-04', name: 'Logistics Depo Gate 04', location: 'Location 01', status: 'ONLINE', operator: 'Hav. K. Singh', recentActivityCount: 9, todayEntries: 6, todayExits: 3, mismatchesFlagged: 0, lastActivity: '22:15:40' },
                { id: 'G-05', name: 'Airfield Gate 05', location: 'Location 02', status: 'STANDBY', operator: 'Unassigned', recentActivityCount: 0, todayEntries: 0, todayExits: 0, mismatchesFlagged: 0, lastActivity: 'Inactive' },
              ]).map((gate) => {
                const isSelected = selectedGateFilter === gate.location;
                return (
                  <div
                    key={gate.id}
                    onClick={() => setSelectedGateFilter(isSelected ? 'ALL' : gate.location)}
                    className={`p-3.5 rounded-xl border transition-all cursor-pointer ${
                      isSelected
                        ? 'bg-amber-500/10 border-amber-500/80 shadow-md'
                        : 'bg-zinc-900/60 border-zinc-800 hover:border-zinc-700 hover:bg-zinc-900'
                    }`}
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-xs font-bold text-zinc-100">{gate.id}</span>
                        <span className="text-xs font-semibold text-zinc-300">{gate.name}</span>
                      </div>
                      <span
                        className={`text-[9px] font-mono px-1.5 py-0.5 rounded font-bold ${
                          gate.status === 'ONLINE'
                            ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                            : 'bg-zinc-800 text-zinc-400'
                        }`}
                      >
                        {gate.status}
                      </span>
                    </div>

                    <div className="flex items-center justify-between text-[11px] text-zinc-400 mt-2 font-mono">
                      <span>{gate.location}</span>
                      <span>Op: {gate.operator}</span>
                    </div>

                    <div className="flex items-center justify-between pt-2 mt-2 border-t border-zinc-800/80 text-[10px] text-zinc-500 font-mono">
                      <span className="text-emerald-400/90">↓ {gate.todayEntries} In</span>
                      <span className="text-amber-400/90">↑ {gate.todayExits} Out</span>
                      {gate.mismatchesFlagged > 0 ? (
                        <span className="text-rose-400 font-bold">⚠️ {gate.mismatchesFlagged} Flag</span>
                      ) : (
                        <span className="text-zinc-600">0 Flags</span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </aside>

        {/* Center/Right Section: Interactive Command Console */}
        <main className="flex-1 flex flex-col min-w-0 bg-zinc-950 overflow-hidden">
          {/* Navigation Bar & Filters */}
          <div className="border-b border-zinc-800 bg-zinc-900/50 p-4 flex flex-wrap items-center justify-between gap-3 shrink-0">
            {/* Tab Switches */}
            <div className="flex items-center bg-zinc-950 p-1 rounded-xl border border-zinc-800">
              <button
                onClick={() => setSelectedTab('LIVE_FEED')}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer flex items-center gap-1.5 ${
                  selectedTab === 'LIVE_FEED' ? 'bg-zinc-800 text-zinc-100 shadow-sm' : 'text-zinc-400 hover:text-zinc-200'
                }`}
              >
                <Activity className="w-3.5 h-3.5 text-amber-400" />
                Live Feed ({filteredActivities.length})
              </button>
              <button
                onClick={() => setSelectedTab('PERSONNEL')}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer flex items-center gap-1.5 ${
                  selectedTab === 'PERSONNEL' ? 'bg-zinc-800 text-zinc-100 shadow-sm' : 'text-zinc-400 hover:text-zinc-200'
                }`}
              >
                <Users className="w-3.5 h-3.5 text-emerald-400" />
                Personnel Registry ({personnelList.length})
              </button>
              <button
                onClick={() => setSelectedTab('VEHICLES')}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer flex items-center gap-1.5 ${
                  selectedTab === 'VEHICLES' ? 'bg-zinc-800 text-zinc-100 shadow-sm' : 'text-zinc-400 hover:text-zinc-200'
                }`}
              >
                <Truck className="w-3.5 h-3.5 text-sky-400" />
                Vehicle Fleet ({vehicleList.length})
              </button>
              <button
                onClick={() => setSelectedTab('SERVER_API')}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-colors cursor-pointer flex items-center gap-1.5 ${
                  selectedTab === 'SERVER_API' ? 'bg-zinc-800 text-zinc-100 shadow-sm' : 'text-zinc-400 hover:text-zinc-200'
                }`}
              >
                <Terminal className="w-3.5 h-3.5 text-purple-400" />
                Server Endpoints
              </button>
            </div>

            {/* Search & Location Filter Indicator */}
            <div className="flex items-center gap-2.5">
              {selectedGateFilter !== 'ALL' && (
                <div className="flex items-center gap-1 px-2.5 py-1 bg-amber-500/10 border border-amber-500/40 text-amber-300 rounded-lg text-xs font-mono">
                  <span>Filter: {selectedGateFilter}</span>
                  <button onClick={() => setSelectedGateFilter('ALL')} className="hover:text-amber-100 ml-1">×</button>
                </div>
              )}

              <div className="relative w-64">
                <Search className="w-3.5 h-3.5 text-zinc-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Filter by ID, Name, Location..."
                  className="w-full bg-zinc-950 border border-zinc-800 rounded-lg pl-8 pr-3 py-1.5 text-xs text-zinc-200 font-mono focus:outline-none focus:border-zinc-600"
                />
              </div>
            </div>
          </div>

          {/* Content Area */}
          <div className="flex-1 overflow-y-auto p-6">
            {selectedTab === 'LIVE_FEED' && (
              <div className="space-y-3">
                <div className="flex items-center justify-between text-xs text-zinc-400 pb-2 border-b border-zinc-800">
                  <span>REAL-TIME GATE AUDIT STREAM</span>
                  <span className="font-mono text-zinc-500">Refreshed: {lastHeartbeatTime}</span>
                </div>

                {filteredActivities.length === 0 ? (
                  <div className="text-center py-16 text-zinc-500 text-xs">
                    No matching activity events recorded for current filters.
                  </div>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 2xl:grid-cols-3 gap-3">
                    {filteredActivities.map((act) => {
                      const isEntry = act.action === 'ENTRY';
                      return (
                        <div
                          key={act.id}
                          onClick={() => {
                            if (act.type === 'PERSON') {
                              const found = personnelList.find(
                                (p) => p.id.toUpperCase() === act.targetId.toUpperCase()
                              );
                              if (found) {
                                setSelectedPerson(found);
                              } else {
                                setSelectedPerson({
                                  id: act.targetId,
                                  name: act.title,
                                  role: act.rank ? `${act.rank}` : 'Personnel',
                                  department: act.unit || 'Base Command',
                                  serviceNumber: act.serviceNumber,
                                  rank: act.rank,
                                  status: 'ACTIVE',
                                  currentStatus: act.action === 'ENTRY' ? 'INSIDE' : 'OUTSIDE',
                                  accessLocations: [act.location],
                                  lastSeenTime: act.timestamp,
                                });
                              }
                            }
                          }}
                          className={`p-4 rounded-xl border transition-all ${
                            act.type === 'PERSON' ? 'cursor-pointer hover:ring-1 hover:ring-amber-500/30' : ''
                          } ${
                            act.locationMismatch
                              ? 'bg-amber-950/20 border-amber-500/60 shadow-xs'
                              : 'bg-zinc-900/70 border-zinc-800 hover:border-zinc-700'
                          }`}
                        >
                          <div className="flex items-start justify-between gap-2">
                            <div className="flex items-center gap-2">
                              <span
                                className={`w-7 h-7 rounded-lg flex items-center justify-center font-bold text-xs ${
                                  isEntry
                                    ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                                    : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                                }`}
                              >
                                {isEntry ? <ArrowDownLeft className="w-4 h-4" /> : <ArrowUpRight className="w-4 h-4" />}
                              </span>
                              <div>
                                <span className="font-mono font-bold text-xs text-zinc-200">{act.targetId}</span>
                                <h3 className="font-semibold text-xs text-zinc-100">{act.title}</h3>
                              </div>
                            </div>

                            <span className="font-mono text-[11px] text-zinc-400">{act.timestamp}</span>
                          </div>

                          <div className="mt-2.5 flex items-center justify-between text-[11px] text-zinc-400 font-mono">
                            <span className="flex items-center gap-1">
                              <MapPin className="w-3 h-3 text-zinc-500" />
                              {act.location} • {act.gate}
                            </span>
                            {act.stayDuration && (
                              <span className="text-zinc-300">Stayed: {act.stayDuration}</span>
                            )}
                          </div>

                          {/* Location Mismatch Flag */}
                          {act.locationMismatch && (
                            <div className="mt-2 p-2 rounded-lg bg-amber-500/20 border border-amber-500/40 text-[10px] text-amber-300 flex items-center justify-between font-mono">
                              <span className="flex items-center gap-1 font-bold">
                                <AlertTriangle className="w-3 h-3 text-amber-400" />
                                LOCATION MISMATCH
                              </span>
                              <span>QR: {act.scannedLocation || 'Diff Loc'}</span>
                            </div>
                          )}

                          {act.vehicleManifest && (
                            <div className="mt-2 pt-2 border-t border-zinc-800/80 text-[10px] font-mono text-zinc-400 flex items-center justify-between">
                              <span>Driver: {act.vehicleManifest.driver.name}</span>
                              <span>{act.vehicleManifest.occupants.length} Passengers</span>
                            </div>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

            {selectedTab === 'PERSONNEL' && (() => {
              // Company filtering
              const companyFilteredPersonnel = localPersonnelList.filter((p) => {
                if (selectedCompanyFilter !== 'ALL') {
                  if ((p.company || 'Alpha') !== selectedCompanyFilter) return false;
                }
                if (!searchQuery) return true;
                const q = searchQuery.toLowerCase();
                return (
                  p.name.toLowerCase().includes(q) ||
                  p.id.toLowerCase().includes(q) ||
                  (p.armyNumber && p.armyNumber.toLowerCase().includes(q)) ||
                  (p.company && p.company.toLowerCase().includes(q)) ||
                  (p.idCardNumber && p.idCardNumber.toLowerCase().includes(q)) ||
                  (p.mobileNumber && p.mobileNumber.toLowerCase().includes(q)) ||
                  (p.secretCode && p.secretCode.toLowerCase().includes(q))
                );
              });

              return (
                <div className="space-y-4">
                  {/* Top Header & Actions */}
                  <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 pb-3 border-b border-zinc-800">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-xs uppercase tracking-wider text-zinc-300">
                          Military &amp; Civilian Personnel Dossier
                        </span>
                        <span className="px-2 py-0.5 rounded-full font-mono text-[10px] font-bold bg-amber-500/10 text-amber-400 border border-amber-500/20">
                          {companyFilteredPersonnel.length} / {localPersonnelList.length} Personnel
                        </span>
                      </div>
                      <p className="text-[11px] text-zinc-400 mt-0.5">
                        Entries organized company-wise with printable QR ID Cards &amp; dynamic custom fields
                      </p>
                    </div>

                    <div className="flex flex-wrap items-center gap-2">
                      {/* Add Soldier Button */}
                      <button
                        onClick={() => {
                          setPersonnelToEdit(null);
                          setShowEditorModal(true);
                        }}
                        className="flex items-center gap-1.5 px-3 py-1.5 bg-amber-400 hover:bg-amber-300 text-slate-950 text-xs font-bold rounded-lg shadow-sm transition-all hover:scale-105 cursor-pointer"
                        id="btn-add-soldier-tab"
                      >
                        <UserPlus className="w-3.5 h-3.5" />
                        <span>+ Add Soldier Details</span>
                      </button>

                      {/* Import / Export Button */}
                      <button
                        onClick={() => setShowImportExportModal(true)}
                        className="flex items-center gap-1.5 px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 text-xs font-semibold rounded-lg border border-zinc-700 transition-colors cursor-pointer"
                        id="btn-import-export-tab"
                      >
                        <FolderDown className="w-3.5 h-3.5 text-emerald-400" />
                        <span>Import / Export</span>
                      </button>

                      {/* Export 6-Company Excel */}
                      <button
                        onClick={handleDownloadSixCompanyExcelAll}
                        className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                          excelExportSuccess
                            ? 'bg-emerald-500 text-slate-950 font-extrabold'
                            : 'bg-emerald-950/70 hover:bg-emerald-900 text-emerald-300 border border-emerald-700/60'
                        }`}
                        title="Download Multi-Tab Excel (.xls) with 6 Company tabs"
                      >
                        <FileSpreadsheet className="w-3.5 h-3.5 text-emerald-400" />
                        <span>{excelExportSuccess ? 'Excel Saved!' : '6-Company Excel'}</span>
                      </button>
                    </div>
                  </div>

                  {/* 6-Company Tab Filters */}
                  <div className="flex items-center gap-1.5 overflow-x-auto pb-1.5 scrollbar-thin">
                    <button
                      onClick={() => setSelectedCompanyFilter('ALL')}
                      className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 cursor-pointer ${
                        selectedCompanyFilter === 'ALL'
                          ? 'bg-amber-400 text-slate-950 shadow-md'
                          : 'bg-zinc-900 hover:bg-zinc-800 text-zinc-400 border border-zinc-800'
                      }`}
                    >
                      <Layers className="w-3.5 h-3.5" />
                      <span>All Companies</span>
                      <span className="ml-1 px-1.5 py-0.2 rounded text-[10px] bg-black/20 font-mono">
                        {localPersonnelList.length}
                      </span>
                    </button>

                    {ALL_COMPANIES.map((coy) => {
                      const count = localPersonnelList.filter(
                        (p) => (p.company || 'Alpha') === coy
                      ).length;
                      const isSelected = selectedCompanyFilter === coy;
                      const theme = COMPANY_THEME[coy];

                      return (
                        <button
                          key={coy}
                          onClick={() => setSelectedCompanyFilter(coy)}
                          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all shrink-0 cursor-pointer ${
                            isSelected
                              ? 'bg-zinc-100 text-slate-950 shadow-md'
                              : 'bg-zinc-900 hover:bg-zinc-800 text-zinc-300 border border-zinc-800'
                          }`}
                        >
                          <span
                            className="w-2.5 h-2.5 rounded-full shrink-0"
                            style={{ backgroundColor: theme.badgeBg }}
                          />
                          <span>{coy} Coy</span>
                          <span
                            className="ml-1 px-1.5 py-0.2 rounded text-[10px] font-mono"
                            style={{
                              backgroundColor: isSelected ? '#00000020' : theme.badgeBg + '30',
                              color: isSelected ? '#000' : theme.badgeBg,
                            }}
                          >
                            {count}
                          </span>
                        </button>
                      );
                    })}
                  </div>

                  {/* Personnel Cards Grid */}
                  {companyFilteredPersonnel.length === 0 ? (
                    <div className="p-12 text-center rounded-2xl bg-zinc-900/50 border border-zinc-800 space-y-3">
                      <p className="text-zinc-400 text-sm font-medium">
                        No personnel found matching the selected filter.
                      </p>
                      <button
                        onClick={() => {
                          setPersonnelToEdit(null);
                          setShowEditorModal(true);
                        }}
                        className="inline-flex items-center gap-1.5 px-4 py-2 bg-amber-400 text-slate-950 rounded-xl text-xs font-bold shadow hover:bg-amber-300 transition-colors"
                      >
                        <UserPlus className="w-4 h-4" />
                        <span>Add Soldier to {selectedCompanyFilter === 'ALL' ? 'Alpha' : selectedCompanyFilter} Company</span>
                      </button>
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3.5">
                      {companyFilteredPersonnel.map((p) => {
                        const isInside = p.currentStatus === 'INSIDE';
                        const coy = (p.company || 'Alpha') as MilitaryCompany;
                        const theme = COMPANY_THEME[coy] || COMPANY_THEME.Alpha;

                        const personActivitiesCount = activities.filter(
                          (act) =>
                            act.targetId.toUpperCase() === p.id.toUpperCase() ||
                            (act.vehicleManifest &&
                              (act.vehicleManifest.driver?.id?.toUpperCase() === p.id.toUpperCase() ||
                                act.vehicleManifest.coDriver?.id?.toUpperCase() === p.id.toUpperCase() ||
                                act.vehicleManifest.occupants?.some(
                                  (occ) => occ.id.toUpperCase() === p.id.toUpperCase()
                                )))
                        ).length;

                        const isDownloaded = downloadSuccessId === p.id;

                        return (
                          <div
                            key={p.id}
                            onClick={() => setSelectedPerson(p)}
                            className="p-4 rounded-xl bg-zinc-900/90 hover:bg-zinc-900 border border-zinc-800 hover:border-zinc-700 flex flex-col justify-between gap-3 cursor-pointer transition-all group shadow-sm hover:shadow-md"
                          >
                            {/* Card Top: Photo, Identification, Status */}
                            <div>
                              <div className="flex items-start gap-3">
                                <img
                                  src={p.photoUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=240'}
                                  alt={p.name}
                                  referrerPolicy="no-referrer"
                                  className="w-12 h-12 rounded-xl object-cover border border-zinc-700 shrink-0 group-hover:border-amber-500/50 transition-colors"
                                />

                                <div className="flex-1 min-w-0">
                                  <div className="flex items-center justify-between gap-1">
                                    <div className="flex items-center gap-1.5 truncate">
                                      <span className="font-mono text-xs font-bold text-amber-400 truncate">
                                        {p.armyNumber || p.serviceNumber || p.id}
                                      </span>
                                    </div>

                                    {/* Company Badge */}
                                    <span
                                      className="text-[9px] font-bold uppercase tracking-wider px-2 py-0.5 rounded border shrink-0"
                                      style={{
                                        backgroundColor: theme.badgeBg + '20',
                                        color: theme.badgeBg,
                                        borderColor: theme.badgeBg + '50',
                                      }}
                                    >
                                      {coy} Coy
                                    </span>
                                  </div>

                                  <div className="flex items-center justify-between mt-1">
                                    <h4 className="font-bold text-xs text-zinc-100 truncate group-hover:text-amber-300 transition-colors">
                                      {p.rank ? `${p.rank} ` : ''}{p.name}
                                    </h4>

                                    <span
                                      className={`text-[9px] font-mono px-1.5 py-0.5 rounded font-bold shrink-0 ${
                                        isInside
                                          ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                                          : 'bg-zinc-800 text-zinc-400'
                                      }`}
                                    >
                                      {p.currentStatus}
                                    </span>
                                  </div>

                                  <p className="text-[11px] text-zinc-400 truncate mt-0.5">
                                    {p.unit || p.department || 'Infantry Unit'}
                                  </p>
                                </div>
                              </div>

                              {/* Personal Details Snapshot */}
                              <div className="mt-3 pt-2.5 border-t border-zinc-800/80 grid grid-cols-2 gap-2 text-[11px] font-mono text-zinc-400">
                                {p.mobileNumber && (
                                  <div className="flex items-center gap-1 truncate" title={`Primary: ${p.mobileNumber}`}>
                                    <Phone className="w-3 h-3 text-sky-400 shrink-0" />
                                    <span className="truncate">{p.mobileNumber}</span>
                                  </div>
                                )}

                                {p.idCardNumber && (
                                  <div className="flex items-center gap-1 truncate" title={`I-Card: ${p.idCardNumber}`}>
                                    <CreditCard className="w-3 h-3 text-emerald-400 shrink-0" />
                                    <span className="truncate">{p.idCardNumber}</span>
                                  </div>
                                )}

                                {p.address && (
                                  <div className="col-span-2 flex items-center gap-1 text-[10px] text-zinc-500 truncate" title={p.address}>
                                    <Home className="w-3 h-3 text-amber-500/70 shrink-0" />
                                    <span className="truncate">{p.address}</span>
                                  </div>
                                )}
                              </div>

                              {/* Custom Cells Preview if any */}
                              {p.customCells && p.customCells.length > 0 && (
                                <div className="mt-2 flex flex-wrap gap-1">
                                  {p.customCells.slice(0, 2).map((cell, idx) => (
                                    <span
                                      key={idx}
                                      className="px-2 py-0.5 bg-zinc-800/90 text-zinc-300 rounded text-[10px] border border-zinc-700/60 font-mono"
                                    >
                                      {cell.label}: <strong className="text-amber-300">{cell.value}</strong>
                                    </span>
                                  ))}
                                  {p.customCells.length > 2 && (
                                    <span className="px-1.5 py-0.5 bg-zinc-800/50 text-zinc-500 rounded text-[10px]">
                                      +{p.customCells.length - 2} more
                                    </span>
                                  )}
                                </div>
                              )}
                            </div>

                            {/* Card Footer Actions */}
                            <div className="pt-2.5 border-t border-zinc-800/80 flex items-center justify-between gap-1.5">
                              {/* Left: Log Count & Edit button */}
                              <div className="flex items-center gap-1.5">
                                <button
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setPersonnelToEdit(p);
                                    setShowEditorModal(true);
                                  }}
                                  className="flex items-center gap-1 px-2 py-1 rounded-lg text-[11px] font-medium bg-zinc-800 hover:bg-zinc-700 text-zinc-300 hover:text-white border border-zinc-700 transition-colors cursor-pointer"
                                  title="Edit soldier details, add tables, cells"
                                >
                                  <Edit3 className="w-3 h-3 text-amber-400" />
                                  <span>Edit</span>
                                </button>

                                <span className="text-[10px] text-zinc-500 font-mono hidden sm:inline">
                                  {personActivitiesCount} Logs
                                </span>
                              </div>

                              {/* Right: ID Card (QR) + Excel + CSV */}
                              <div className="flex items-center gap-1">
                                {/* Print ID Card with QR button */}
                                <button
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    setIdCardPerson(p);
                                  }}
                                  className="flex items-center gap-1 px-2 py-1 rounded-lg text-[11px] font-bold bg-amber-400 hover:bg-amber-300 text-slate-950 transition-all hover:scale-105 cursor-pointer shadow-xs"
                                  title={`Create Military ID Card with QR code for ${p.name}`}
                                >
                                  <QrCode className="w-3 h-3 text-slate-950" />
                                  <span>ID Card</span>
                                </button>

                                {/* Excel Button */}
                                <button
                                  onClick={(e) => handleDownloadPersonExcel(p, e)}
                                  className={`flex items-center gap-0.5 px-2 py-1 rounded-lg text-[11px] font-mono font-bold transition-all cursor-pointer ${
                                    isDownloaded && downloadFormatSuccess === 'excel'
                                      ? 'bg-emerald-500 text-slate-950'
                                      : 'bg-emerald-950/60 hover:bg-emerald-900/80 text-emerald-300 border border-emerald-800/80'
                                  }`}
                                  title={`Download Excel report for ${p.name}`}
                                >
                                  <FileSpreadsheet className="w-3 h-3 text-emerald-400" />
                                  <span className="hidden sm:inline">
                                    {isDownloaded && downloadFormatSuccess === 'excel' ? 'Saved' : 'XLS'}
                                  </span>
                                </button>

                                {/* CSV Button */}
                                <button
                                  onClick={(e) => handleDownloadPersonReport(p, e)}
                                  className={`flex items-center gap-0.5 px-2 py-1 rounded-lg text-[11px] font-mono font-medium transition-all cursor-pointer ${
                                    isDownloaded && downloadFormatSuccess === 'csv'
                                      ? 'bg-zinc-700 text-zinc-100'
                                      : 'bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700'
                                  }`}
                                  title={`Download CSV table for ${p.name}`}
                                >
                                  <Download className="w-3 h-3 text-amber-400" />
                                  <span className="hidden sm:inline">CSV</span>
                                </button>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              );
            })()}

            {selectedTab === 'VEHICLES' && (
              <div className="space-y-3">
                <div className="flex items-center justify-between text-xs text-zinc-400 pb-2 border-b border-zinc-800">
                  <span>TACTICAL VEHICLE FLEET REGISTRY</span>
                  <span className="font-mono text-zinc-500">Total: {vehicleList.length} Vehicles</span>
                </div>

                {vehicleList.length === 0 ? (
                  <div className="p-12 text-center text-zinc-500 text-xs bg-zinc-900/50 rounded-2xl border border-zinc-800">
                    <Truck className="w-8 h-8 text-zinc-600 mx-auto mb-2" />
                    <p className="font-semibold text-zinc-300">Vehicle Fleet Registry is Empty</p>
                    <p className="text-zinc-500 mt-1">Vehicles will register automatically when scanned at the gates.</p>
                  </div>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-3">
                    {vehicleList.map((v) => {
                      const isInside = v.currentStatus === 'INSIDE';
                      return (
                        <div
                          key={v.id}
                          className="p-4 rounded-xl bg-zinc-900/80 border border-zinc-800 space-y-2"
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex items-center gap-2">
                              <Truck className="w-4 h-4 text-sky-400" />
                              <span className="font-mono font-bold text-xs text-zinc-100">{v.id}</span>
                              <span className="px-2 py-0.5 bg-yellow-400 text-zinc-950 font-mono font-bold text-[10px] rounded">
                                {v.plateNumber}
                              </span>
                            </div>
                            <span
                              className={`text-[9px] font-mono px-1.5 py-0.5 rounded font-bold ${
                                isInside
                                  ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                                  : 'bg-zinc-800 text-zinc-400'
                              }`}
                            >
                              {v.currentStatus}
                            </span>
                          </div>

                          <div className="text-xs text-zinc-300 font-medium">
                            {v.type} ({v.model})
                          </div>

                          <div className="flex items-center justify-between text-[10px] font-mono text-zinc-400 pt-2 border-t border-zinc-800/80">
                            <span>{v.assignedCompany}</span>
                            <span>Auth Drivers: {v.authorizedDrivers?.length || 0}</span>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            )}

            {selectedTab === 'SERVER_API' && (
              <div className="space-y-4 max-w-4xl">
                <div className="p-4 rounded-xl bg-zinc-900 border border-zinc-800 space-y-2">
                  <h3 className="text-xs font-bold uppercase tracking-wider text-zinc-300 flex items-center gap-1.5">
                    <Server className="w-4 h-4 text-emerald-400" />
                    Express Node.js REST API Architecture
                  </h3>
                  <p className="text-xs text-zinc-400 leading-relaxed">
                    The application runs a dedicated Express backend (server.ts) on port 3000 handling mobile ingestion,
                    cross-gate synchronization, military registry resolution, and cloud storage persistence.
                  </p>
                </div>

                <div className="space-y-2 font-mono text-xs">
                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded font-bold">GET</span>
                      <span className="text-zinc-200">/api/health</span>
                    </div>
                    <span className="text-zinc-500">Service health & uptime diagnostics</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded font-bold">GET</span>
                      <span className="text-zinc-200">/api/personnel?q=...</span>
                    </div>
                    <span className="text-zinc-500">Query personnel registry & clearances</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded font-bold">GET</span>
                      <span className="text-zinc-200">/api/vehicles?q=...</span>
                    </div>
                    <span className="text-zinc-500">Fleet lookup & assigned driver checks</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-sky-500/20 text-sky-400 rounded font-bold">POST</span>
                      <span className="text-zinc-200">/api/scan/verify</span>
                    </div>
                    <span className="text-zinc-500">Server QR decode & Location Mismatch verify</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-sky-500/20 text-sky-400 rounded font-bold">POST</span>
                      <span className="text-zinc-200">/api/activities</span>
                    </div>
                    <span className="text-zinc-500">Log entry/exit event with duration breakdown</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-sky-500/20 text-sky-400 rounded font-bold">POST</span>
                      <span className="text-zinc-200">/api/activities/batch</span>
                    </div>
                    <span className="text-zinc-500">Batch upload buffered offline records</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded font-bold">GET</span>
                      <span className="text-zinc-200">/api/gates/status</span>
                    </div>
                    <span className="text-zinc-500">Multi-gate network telemetry & stats</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded font-bold">GET</span>
                      <span className="text-zinc-200">/api/reports/personnel/:id/csv</span>
                    </div>
                    <span className="text-zinc-500">Download single person complete entry/exit log CSV</span>
                  </div>

                  <div className="p-3 bg-zinc-900/90 rounded-lg border border-zinc-800 flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <span className="px-2 py-0.5 bg-rose-500/20 text-rose-400 rounded font-bold">POST</span>
                      <span className="text-zinc-200">/api/activities/reset</span>
                    </div>
                    <span className="text-zinc-500">Purge server record store to empty state</span>
                  </div>
                </div>

                {/* Database Maintenance Card */}
                {onPurgeData && (
                  <div className="p-4 rounded-xl bg-zinc-900 border border-zinc-800 space-y-3">
                    <div className="flex items-center justify-between">
                      <h4 className="text-xs font-bold uppercase tracking-wider text-zinc-200 flex items-center gap-2">
                        <Database className="w-4 h-4 text-rose-400" />
                        Clean State Database Maintenance
                      </h4>
                      <span className="text-[10px] font-mono text-zinc-500">No Fake Data Policy</span>
                    </div>
                    <p className="text-xs text-zinc-400">
                      Wipes all buffered local storage, in-memory client state, server activity JSON storage, and remote Firestore entries.
                    </p>
                    {purgeSuccess ? (
                      <div className="p-3 bg-emerald-950/60 border border-emerald-800 rounded-lg text-emerald-300 text-xs flex items-center gap-2 font-medium">
                        <Check className="w-4 h-4 text-emerald-400" />
                        <span>Database purged successfully. All fake/mock and stale records deleted.</span>
                      </div>
                    ) : (
                      <button
                        type="button"
                        disabled={isPurging}
                        onClick={async () => {
                          if (window.confirm('Wipe all local and server databases clean?')) {
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
                        className="px-4 py-2 bg-rose-950/60 hover:bg-rose-900 text-rose-300 border border-rose-800/80 rounded-lg text-xs font-semibold flex items-center gap-2 transition-colors cursor-pointer disabled:opacity-50"
                      >
                        {isPurging ? (
                          <RefreshCw className="w-4 h-4 animate-spin" />
                        ) : (
                          <Trash2 className="w-4 h-4" />
                        )}
                        <span>{isPurging ? 'Purging All Storage...' : 'Wipe & Reset Entire Database'}</span>
                      </button>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        </main>
      </div>

      {/* Individual Personnel Profile View & Full History Dossier Modal */}
      {selectedPerson && (
        <div
          id="personnel-profile-modal"
          className="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4 sm:p-6"
          onClick={() => setSelectedPerson(null)}
        >
          <div
            className="bg-zinc-900 border border-zinc-700/80 rounded-2xl w-full max-w-4xl max-h-[90vh] flex flex-col shadow-2xl overflow-hidden"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div className="px-6 py-4 border-b border-zinc-800 bg-zinc-950/60 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-9 h-9 rounded-xl bg-amber-500/20 text-amber-400 flex items-center justify-center border border-amber-500/30">
                  <User className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-zinc-100 flex items-center gap-2">
                    <span>Individual Personnel Dossier</span>
                    <span className="font-mono text-xs px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20">
                      {selectedPerson.id}
                    </span>
                  </h3>
                  <p className="text-xs text-zinc-400">Security Clearance & Multi-Gate Access Record</p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                {/* Generate ID Card with QR Button */}
                <button
                  id="btn-modal-generate-id-card"
                  onClick={() => setIdCardPerson(selectedPerson)}
                  className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-mono font-bold bg-amber-400 hover:bg-amber-300 text-slate-950 transition-all cursor-pointer shadow-sm hover:scale-105"
                  title="Generate Military ID Card with secure QR code"
                >
                  <QrCode className="w-4 h-4" />
                  <span>Generate ID Card (QR)</span>
                </button>

                {/* Edit Soldier Dossier Button */}
                <button
                  id="btn-modal-edit-soldier"
                  onClick={() => {
                    setPersonnelToEdit(selectedPerson);
                    setShowEditorModal(true);
                  }}
                  className="flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-mono font-medium bg-zinc-800 hover:bg-zinc-700 text-zinc-200 border border-zinc-700 transition-all cursor-pointer shadow-sm"
                  title="Edit personnel details, add tables, cells"
                >
                  <Edit3 className="w-3.5 h-3.5 text-amber-400" />
                  <span>Edit</span>
                </button>

                {/* Excel Report Button with Color Coding & Title */}
                <button
                  id="btn-download-personnel-excel"
                  onClick={() => handleDownloadPersonExcel(selectedPerson)}
                  className={`flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-mono font-bold transition-all cursor-pointer shadow-sm ${
                    downloadSuccessId === selectedPerson.id && downloadFormatSuccess === 'excel'
                      ? 'bg-emerald-600 text-white'
                      : 'bg-emerald-600 hover:bg-emerald-500 text-white hover:shadow-md'
                  }`}
                  title="Download Color-Coded Excel Worksheet (.xls) with custom location highlights and badges"
                >
                  {downloadSuccessId === selectedPerson.id && downloadFormatSuccess === 'excel' ? (
                    <>
                      <Check className="w-4 h-4 stroke-[3]" />
                      <span>Excel Saved!</span>
                    </>
                  ) : (
                    <>
                      <FileSpreadsheet className="w-4 h-4 text-emerald-200" />
                      <span>Excel Report (.xls)</span>
                    </>
                  )}
                </button>

                {/* CSV Download Button */}
                <button
                  id="btn-download-personnel-csv"
                  onClick={() => handleDownloadPersonReport(selectedPerson)}
                  className={`flex items-center gap-1.5 px-3 py-2 rounded-xl text-xs font-mono font-semibold transition-all cursor-pointer shadow-sm ${
                    downloadSuccessId === selectedPerson.id && downloadFormatSuccess === 'csv'
                      ? 'bg-zinc-700 text-white'
                      : 'bg-zinc-800 hover:bg-zinc-700 text-zinc-300 border border-zinc-700'
                  }`}
                  title="Download raw CSV table"
                >
                  {downloadSuccessId === selectedPerson.id && downloadFormatSuccess === 'csv' ? (
                    <>
                      <Check className="w-3.5 h-3.5 text-emerald-400" />
                      <span>CSV Saved</span>
                    </>
                  ) : (
                    <>
                      <Download className="w-3.5 h-3.5 text-amber-400" />
                      <span>CSV</span>
                    </>
                  )}
                </button>

                <button
                  onClick={() => setSelectedPerson(null)}
                  className="p-2 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-400 hover:text-zinc-200 transition-colors cursor-pointer ml-1"
                  title="Close Profile"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>

            {/* Modal Body */}
            <div className="p-6 overflow-y-auto space-y-6 flex-1">
              {/* Profile Snapshot Header */}
              <div className="p-5 rounded-xl bg-zinc-950/70 border border-zinc-800 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-5">
                <div className="flex items-start gap-4">
                  <img
                    src={selectedPerson.photoUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=240'}
                    alt={selectedPerson.name}
                    referrerPolicy="no-referrer"
                    className="w-18 h-18 rounded-2xl object-cover border-2 border-amber-500/40 shrink-0 shadow-md"
                  />
                  <div>
                    <div className="flex items-center gap-2">
                      <h2 className="text-base font-bold text-zinc-100">
                        {selectedPerson.rank ? `${selectedPerson.rank} ` : ''}
                        {selectedPerson.name}
                      </h2>

                      {/* Company Badge */}
                      <span
                        className="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded border"
                        style={{
                          backgroundColor:
                            (COMPANY_THEME[selectedPerson.company as MilitaryCompany]?.badgeBg || '#1e3a8a') + '25',
                          color:
                            COMPANY_THEME[selectedPerson.company as MilitaryCompany]?.badgeBg || '#3b82f6',
                          borderColor:
                            (COMPANY_THEME[selectedPerson.company as MilitaryCompany]?.badgeBg || '#1e3a8a') + '60',
                        }}
                      >
                        {selectedPerson.company || 'Alpha'} Company
                      </span>

                      <span
                        className={`text-[10px] font-mono px-2 py-0.5 rounded-full font-bold ${
                          selectedPerson.currentStatus === 'INSIDE'
                            ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                            : 'bg-zinc-800 text-zinc-400'
                        }`}
                      >
                        {selectedPerson.currentStatus}
                      </span>
                    </div>

                    <p className="text-xs text-zinc-400 mt-1">
                      {selectedPerson.unit || selectedPerson.department || 'Military Command'} • {selectedPerson.role || 'Personnel'}
                    </p>

                    {/* Military & Personal Identification Details */}
                    <div className="flex flex-wrap items-center gap-2 mt-2.5 text-[11px] font-mono text-zinc-300">
                      <span className="bg-zinc-900 px-2.5 py-1 rounded border border-zinc-800">
                        Army No: <strong className="text-amber-300">{selectedPerson.armyNumber || selectedPerson.serviceNumber || selectedPerson.id}</strong>
                      </span>
                      {selectedPerson.idCardNumber && (
                        <span className="bg-zinc-900 px-2.5 py-1 rounded border border-zinc-800">
                          I-Card: <strong className="text-emerald-300">{selectedPerson.idCardNumber}</strong>
                        </span>
                      )}
                      {selectedPerson.mobileNumber && (
                        <span className="bg-zinc-900 px-2.5 py-1 rounded border border-zinc-800 flex items-center gap-1">
                          <Phone className="w-3 h-3 text-sky-400" />
                          <span>{selectedPerson.mobileNumber}</span>
                        </span>
                      )}
                      {selectedPerson.altMobileNumber && (
                        <span className="bg-zinc-900 px-2.5 py-1 rounded border border-zinc-800 text-zinc-400">
                          Alt: {selectedPerson.altMobileNumber}
                        </span>
                      )}
                      {selectedPerson.secretCode && (
                        <span className="bg-amber-500/10 text-amber-300 px-2.5 py-1 rounded border border-amber-500/30 flex items-center gap-1">
                          <KeyRound className="w-3 h-3 text-amber-400" />
                          <span>Key: {selectedPerson.secretCode}</span>
                        </span>
                      )}
                    </div>

                    {selectedPerson.address && (
                      <p className="text-[11px] text-zinc-400 font-mono mt-1.5 flex items-center gap-1">
                        <Home className="w-3.5 h-3.5 text-zinc-500 shrink-0" />
                        <span>Address: {selectedPerson.address}</span>
                      </p>
                    )}
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3 w-full sm:w-auto text-left sm:text-right font-mono text-xs shrink-0">
                  <div className="p-3 bg-zinc-900 rounded-lg border border-zinc-800/80">
                    <span className="text-[10px] text-zinc-500 block uppercase">Last Seen</span>
                    <span className="text-zinc-200 font-bold">{selectedPerson.lastSeenTime || 'N/A'}</span>
                  </div>
                  <div className="p-3 bg-zinc-900 rounded-lg border border-zinc-800/80">
                    <span className="text-[10px] text-zinc-500 block uppercase">Authorized Gates</span>
                    <span className="text-amber-400 font-bold">
                      {selectedPerson.accessLocations?.length || 1} Locations
                    </span>
                  </div>
                </div>
              </div>

              {/* Custom Dynamic Cells (if present) */}
              {selectedPerson.customCells && selectedPerson.customCells.length > 0 && (
                <div className="p-4 rounded-xl bg-zinc-950/40 border border-zinc-800">
                  <h4 className="text-xs font-bold text-zinc-300 uppercase tracking-wider mb-2.5">
                    Custom Attributes &amp; Parameters
                  </h4>
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5 font-mono text-xs">
                    {selectedPerson.customCells.map((c, i) => (
                      <div key={i} className="p-2 rounded bg-zinc-900 border border-zinc-800">
                        <span className="text-[10px] text-zinc-500 block">{c.label}</span>
                        <span className="text-amber-300 font-bold">{c.value}</span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Custom Dynamic Tables (if present) */}
              {selectedPerson.customTables && selectedPerson.customTables.length > 0 && (
                <div className="space-y-4">
                  {selectedPerson.customTables.map((tbl) => (
                    <div key={tbl.id} className="p-4 rounded-xl bg-zinc-950/40 border border-zinc-800 space-y-2">
                      <h4 className="text-xs font-bold text-zinc-300 uppercase tracking-wider">
                        {tbl.tableName}
                      </h4>
                      <div className="overflow-x-auto rounded-lg border border-zinc-800">
                        <table className="w-full text-xs text-left font-mono">
                          <thead className="bg-zinc-900 text-zinc-400 text-[10px] uppercase">
                            <tr>
                              {tbl.columns.map((col, idx) => (
                                <th key={idx} className="p-2 border-b border-zinc-800">{col}</th>
                              ))}
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-zinc-800/50">
                            {tbl.rows.map((row, rIdx) => (
                              <tr key={rIdx} className="hover:bg-zinc-900/40">
                                {tbl.columns.map((col, cIdx) => (
                                  <td key={cIdx} className="p-2 text-zinc-200">{row[col] || '-'}</td>
                                ))}
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {/* Entry / Exit Activity History Log */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <div className="flex items-center gap-2">
                    <Clock className="w-4 h-4 text-amber-400" />
                    <h3 className="text-xs font-bold uppercase tracking-wider text-zinc-300">
                      Entry & Exit Log History
                    </h3>
                  </div>
                  <span className="text-xs font-mono text-zinc-500">
                    {
                      activities.filter(
                        (act) =>
                          act.targetId.toUpperCase() === selectedPerson.id.toUpperCase() ||
                          (act.vehicleManifest &&
                            (act.vehicleManifest.driver?.id?.toUpperCase() === selectedPerson.id.toUpperCase() ||
                              act.vehicleManifest.coDriver?.id?.toUpperCase() === selectedPerson.id.toUpperCase() ||
                              act.vehicleManifest.occupants?.some((occ) => occ.id.toUpperCase() === selectedPerson.id.toUpperCase())))
                      ).length
                    }{' '}
                    Total Recorded Events
                  </span>
                </div>

                {(() => {
                  const personEvents = activities.filter(
                    (act) =>
                      act.targetId.toUpperCase() === selectedPerson.id.toUpperCase() ||
                      (act.vehicleManifest &&
                        (act.vehicleManifest.driver?.id?.toUpperCase() === selectedPerson.id.toUpperCase() ||
                          act.vehicleManifest.coDriver?.id?.toUpperCase() === selectedPerson.id.toUpperCase() ||
                          act.vehicleManifest.occupants?.some((occ) => occ.id.toUpperCase() === selectedPerson.id.toUpperCase())))
                  );

                  if (personEvents.length === 0) {
                    return (
                      <div className="p-8 text-center rounded-xl bg-zinc-950/40 border border-zinc-800 text-xs text-zinc-500 font-mono">
                        No entry or exit logs found for {selectedPerson.name} ({selectedPerson.id}).
                      </div>
                    );
                  }

                  return (
                    <div className="border border-zinc-800 rounded-xl overflow-hidden bg-zinc-950/40">
                      <div className="overflow-x-auto">
                        <table className="w-full text-left text-xs font-mono">
                          <thead className="bg-zinc-900/90 text-zinc-400 text-[11px] border-b border-zinc-800">
                            <tr>
                              <th className="py-2.5 px-3">Action</th>
                              <th className="py-2.5 px-3">Timestamp</th>
                              <th className="py-2.5 px-3">Gate / Location</th>
                              <th className="py-2.5 px-3">Duration</th>
                              <th className="py-2.5 px-3">Prev. Visit Gap</th>
                              <th className="py-2.5 px-3">Gatekeeper</th>
                              <th className="py-2.5 px-3">Status</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-zinc-800/60">
                            {personEvents.map((act) => {
                              const isEntry = act.action === 'ENTRY';
                              return (
                                <tr key={act.id} className="hover:bg-zinc-900/40 transition-colors">
                                  <td className="py-2.5 px-3">
                                    <span
                                      className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-bold ${
                                        isEntry
                                          ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
                                          : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                                      }`}
                                    >
                                      {isEntry ? <ArrowDownLeft className="w-3 h-3" /> : <ArrowUpRight className="w-3 h-3" />}
                                      {act.action}
                                    </span>
                                  </td>
                                  <td className="py-2.5 px-3 text-zinc-300">{act.timestamp}</td>
                                  <td className="py-2.5 px-3 text-zinc-300">
                                    {act.location} • {act.gate}
                                    {act.locationMismatch && (
                                      <span className="block text-[10px] text-amber-400 flex items-center gap-1 mt-0.5 font-bold">
                                        <AlertTriangle className="w-2.5 h-2.5" /> Mismatch ({act.scannedLocation})
                                      </span>
                                    )}
                                  </td>
                                  <td className="py-2.5 px-3 text-zinc-300">
                                    {act.stayDurationFormatted || act.stayDuration || '—'}
                                  </td>
                                  <td className="py-2.5 px-3 text-zinc-400">
                                    {act.timeSinceLastVisitFormatted || '—'}
                                  </td>
                                  <td className="py-2.5 px-3 text-zinc-400">{act.gatekeeperId}</td>
                                  <td className="py-2.5 px-3">
                                    <span
                                      className={`text-[10px] px-1.5 py-0.5 rounded ${
                                        act.synced
                                          ? 'bg-emerald-500/10 text-emerald-400'
                                          : 'bg-zinc-800 text-zinc-400'
                                      }`}
                                    >
                                      {act.synced ? 'Synced' : 'Buffered'}
                                    </span>
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  );
                })()}
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-6 py-3.5 border-t border-zinc-800 bg-zinc-950/60 flex flex-wrap items-center justify-between gap-2 text-xs font-mono text-zinc-500">
              <span className="flex items-center gap-1.5">
                <span>Formats available:</span>
                <span className="px-1.5 py-0.5 rounded bg-emerald-950 text-emerald-400 border border-emerald-800/80 text-[10px] font-bold">
                  Excel Color-Coded (.xls)
                </span>
                <span className="px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-400 text-[10px]">
                  Standard CSV
                </span>
              </span>
              <div className="flex items-center gap-3">
                <button
                  onClick={() => handleDownloadPersonExcel(selectedPerson)}
                  className="text-emerald-400 hover:text-emerald-300 underline font-medium cursor-pointer flex items-center gap-1"
                >
                  <FileSpreadsheet className="w-3.5 h-3.5" />
                  <span>Download Color Excel</span>
                </button>
                <span className="text-zinc-600">|</span>
                <button
                  onClick={() => handleDownloadPersonReport(selectedPerson)}
                  className="text-amber-400 hover:text-amber-300 underline font-medium cursor-pointer"
                >
                  Export CSV ({selectedPerson.id})
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Native Installers & Local Wi-Fi Pairing Hub Modal */}
      <NativeInstallModal
        isOpen={showNativeModal}
        onClose={() => setShowNativeModal(false)}
        serverPort={3000}
      />
    </div>
  );
};
