import React, { useState, useMemo } from 'react';
import {
  X,
  Search,
  Users,
  ShieldCheck,
  Building2,
  ArrowDownLeft,
  ArrowUpRight,
  Clock,
  IdCard,
  QrCode,
  CheckCircle2,
  ExternalLink,
  Download,
} from 'lucide-react';
import { Personnel } from '../../types';
import { downloadPersonnelRosterCsv } from '../../utils/csvExport';

interface PersonnelRosterModalProps {
  isOpen: boolean;
  onClose: () => void;
  personnelList: Personnel[];
  onSelectPerson: (person: Personnel) => void;
  initialFilter?: 'ALL' | 'INSIDE' | 'OUTSIDE';
}

export const PersonnelRosterModal: React.FC<PersonnelRosterModalProps> = ({
  isOpen,
  onClose,
  personnelList,
  onSelectPerson,
  initialFilter = 'ALL',
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'INSIDE' | 'OUTSIDE'>(initialFilter);
  const [departmentFilter, setDepartmentFilter] = useState<string>('ALL');

  const departments = useMemo(() => {
    const set = new Set<string>();
    personnelList.forEach((p) => {
      if (p.department) set.add(p.department);
      if (p.company) set.add(`${p.company} Co`);
    });
    return Array.from(set);
  }, [personnelList]);

  const filteredList = useMemo(() => {
    return personnelList.filter((p) => {
      // Status filter
      if (statusFilter === 'INSIDE' && p.currentStatus !== 'INSIDE') return false;
      if (statusFilter === 'OUTSIDE' && p.currentStatus !== 'OUTSIDE') return false;

      // Department filter
      if (departmentFilter !== 'ALL') {
        const matchesDept = p.department === departmentFilter || `${p.company} Co` === departmentFilter;
        if (!matchesDept) return false;
      }

      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.trim().toLowerCase();
        const matchName = p.name.toLowerCase().includes(query);
        const matchId = p.id.toLowerCase().includes(query);
        const matchArmy = (p.armyNumber || '').toLowerCase().includes(query);
        const matchCard = (p.idCardNumber || '').toLowerCase().includes(query);
        const matchRole = p.role.toLowerCase().includes(query);
        const matchDept = (p.department || '').toLowerCase().includes(query);
        return matchName || matchId || matchArmy || matchCard || matchRole || matchDept;
      }

      return true;
    });
  }, [personnelList, statusFilter, departmentFilter, searchQuery]);

  const insideCount = personnelList.filter((p) => p.currentStatus === 'INSIDE').length;
  const outsideCount = personnelList.filter((p) => p.currentStatus === 'OUTSIDE').length;

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-black/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div className="w-full max-w-lg bg-white rounded-3xl shadow-2xl border border-zinc-200 overflow-hidden flex flex-col max-h-[92vh] sm:max-h-[85vh]">
        {/* Header */}
        <div className="px-5 py-4 bg-zinc-900 text-white flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-zinc-800 border border-zinc-700 flex items-center justify-center text-emerald-400">
              <Users className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-base tracking-tight text-white leading-tight">
                  Personnel Gate Registry
                </h3>
                <span className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-zinc-800 text-zinc-300 border border-zinc-700">
                  {personnelList.length} TOTAL
                </span>
              </div>
              <p className="text-[11px] text-zinc-400 mt-0.5">
                Live roster status, security clearance & verification
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => {
                downloadPersonnelRosterCsv(filteredList);
              }}
              className="flex items-center gap-1 px-2.5 py-1 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 hover:text-white rounded-lg text-xs font-semibold transition-all border border-zinc-700 cursor-pointer shadow-xs"
              title="Export current personnel roster to CSV"
            >
              <Download className="w-3.5 h-3.5 text-emerald-400" />
              <span>Export CSV</span>
            </button>
            <button
              onClick={onClose}
              className="w-8 h-8 rounded-full bg-zinc-800 hover:bg-zinc-700 text-zinc-400 hover:text-white flex items-center justify-center transition-colors cursor-pointer"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Search & Presence Filter Chips */}
        <div className="p-4 bg-zinc-50 border-b border-zinc-200 space-y-3 shrink-0">
          {/* Search Box */}
          <div className="relative">
            <Search className="w-4 h-4 text-zinc-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by name, ID, Army number, role..."
              className="w-full pl-9 pr-8 py-2.5 bg-white border border-zinc-200 rounded-xl text-xs text-zinc-900 placeholder:text-zinc-400 focus:outline-none focus:ring-2 focus:ring-zinc-900/10 focus:border-zinc-800 shadow-2xs font-medium"
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-zinc-400 hover:text-zinc-600 cursor-pointer"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          {/* Quick Filter Pill Buttons */}
          <div className="flex items-center justify-between gap-1.5 overflow-x-auto pb-0.5 text-xs">
            <div className="flex items-center gap-1.5 shrink-0">
              <button
                onClick={() => setStatusFilter('ALL')}
                className={`px-3 py-1.5 rounded-xl font-bold transition-all cursor-pointer text-xs ${
                  statusFilter === 'ALL'
                    ? 'bg-zinc-900 text-white shadow-2xs'
                    : 'bg-white border border-zinc-200 text-zinc-600 hover:bg-zinc-100'
                }`}
              >
                All ({personnelList.length})
              </button>
              <button
                onClick={() => setStatusFilter('INSIDE')}
                className={`px-3 py-1.5 rounded-xl font-bold transition-all cursor-pointer flex items-center gap-1.5 text-xs ${
                  statusFilter === 'INSIDE'
                    ? 'bg-emerald-600 text-white shadow-2xs'
                    : 'bg-emerald-50 text-emerald-800 border border-emerald-200/90 hover:bg-emerald-100/70'
                }`}
              >
                <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse" />
                Inside ({insideCount})
              </button>
              <button
                onClick={() => setStatusFilter('OUTSIDE')}
                className={`px-3 py-1.5 rounded-xl font-bold transition-all cursor-pointer flex items-center gap-1.5 text-xs ${
                  statusFilter === 'OUTSIDE'
                    ? 'bg-zinc-700 text-white shadow-2xs'
                    : 'bg-white border border-zinc-200 text-zinc-600 hover:bg-zinc-100'
                }`}
              >
                <span className="w-2 h-2 rounded-full bg-zinc-400" />
                Outside ({outsideCount})
              </button>
            </div>

            {/* Department Dropdown */}
            {departments.length > 0 && (
              <select
                value={departmentFilter}
                onChange={(e) => setDepartmentFilter(e.target.value)}
                className="bg-white border border-zinc-200 rounded-xl px-2.5 py-1.5 text-xs font-semibold text-zinc-700 focus:outline-none cursor-pointer shrink-0"
              >
                <option value="ALL">All Units</option>
                {departments.map((dept) => (
                  <option key={dept} value={dept}>
                    {dept}
                  </option>
                ))}
              </select>
            )}
          </div>
        </div>

        {/* Scrollable Personnel Cards List */}
        <div className="flex-1 overflow-y-auto p-4 space-y-2.5">
          {filteredList.length === 0 ? (
            <div className="p-8 text-center text-zinc-400">
              <Users className="w-10 h-10 mx-auto text-zinc-300 stroke-1 mb-2" />
              <p className="text-sm font-semibold text-zinc-600">No personnel found</p>
              <p className="text-xs text-zinc-400 mt-1">Try clearing search terms or status filters</p>
            </div>
          ) : (
            filteredList.map((p) => {
              const isInside = p.currentStatus === 'INSIDE';
              const lastSeenText = p.lastSeenTime || p.lastEntryTime || 'No recent scan';
              const lastSeenAction = p.lastSeenAction || (isInside ? 'ENTRY' : 'EXIT');

              return (
                <div
                  key={p.id}
                  onClick={() => {
                    onSelectPerson(p);
                    onClose();
                  }}
                  className="flex items-center justify-between p-3.5 bg-white hover:bg-zinc-50 border border-zinc-200/90 hover:border-zinc-300 rounded-2xl transition-all cursor-pointer shadow-2xs group active:scale-[0.99]"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="relative shrink-0">
                      <img
                        src={
                          p.photoUrl ||
                          'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=240&auto=format&fit=crop&q=80'
                        }
                        alt={p.name}
                        className="w-11 h-11 rounded-xl object-cover border border-zinc-200 shadow-2xs"
                      />
                      <span
                        className={`absolute -bottom-1 -right-1 w-3 h-3 rounded-full border-2 border-white ${
                          isInside ? 'bg-emerald-500' : 'bg-zinc-400'
                        }`}
                      />
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-bold text-zinc-900 truncate">
                          {p.name}
                        </span>
                        <span className="text-[10px] font-mono font-bold px-1.5 py-0.2 rounded bg-zinc-100 text-zinc-600 border border-zinc-200 shrink-0">
                          {p.id}
                        </span>
                      </div>
                      <div className="flex items-center gap-1.5 text-xs text-zinc-500 truncate mt-0.5">
                        <span>{p.rank ? `${p.rank} • ` : ''}{p.role}</span>
                        {p.company && (
                          <span className="text-zinc-400 font-mono text-[10px]">
                            ({p.company} Co)
                          </span>
                        )}
                      </div>
                      {p.armyNumber && (
                        <span className="text-[10px] font-mono text-zinc-400 block mt-0.5">
                          Army No: {p.armyNumber}
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Right side verification & status badge */}
                  <div className="shrink-0 flex flex-col items-end gap-1.5">
                    <span
                      className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-mono font-bold border ${
                        isInside
                          ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
                          : 'bg-zinc-100 text-zinc-600 border-zinc-200'
                      }`}
                    >
                      {lastSeenAction === 'ENTRY' ? (
                        <ArrowDownLeft className="w-3 h-3 text-emerald-600" />
                      ) : (
                        <ArrowUpRight className="w-3 h-3 text-zinc-500" />
                      )}
                      <span>{isInside ? 'ON SITE' : 'OFF SITE'}</span>
                    </span>
                    <span className="text-[10px] font-mono text-zinc-400 flex items-center gap-1">
                      <Clock className="w-2.5 h-2.5" />
                      {lastSeenText}
                    </span>
                  </div>
                </div>
              );
            })
          )}
        </div>

        {/* Footer info note */}
        <div className="p-3 bg-zinc-50 border-t border-zinc-200 flex items-center justify-between text-xs text-zinc-500 shrink-0">
          <span className="text-[11px]">
            Tap any personnel card to open immediate gate clearance action
          </span>
          <button
            onClick={onClose}
            className="px-3 py-1 bg-zinc-200 hover:bg-zinc-300 text-zinc-800 font-bold rounded-lg text-xs cursor-pointer transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
