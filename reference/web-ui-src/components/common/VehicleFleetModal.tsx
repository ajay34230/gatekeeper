import React, { useState, useMemo } from 'react';
import {
  X,
  Search,
  Truck,
  Building2,
  ArrowDownLeft,
  ArrowUpRight,
  Clock,
  Car,
  ShieldCheck,
  CheckCircle2,
  Download,
  Check,
} from 'lucide-react';
import { Vehicle } from '../../types';
import { downloadVehicleFleetCsv } from '../../utils/csvExport';

interface VehicleFleetModalProps {
  isOpen: boolean;
  onClose: () => void;
  vehicleList: Vehicle[];
  onSelectVehicle: (vehicle: Vehicle) => void;
  initialFilter?: 'ALL' | 'INSIDE' | 'OUTSIDE';
}

export const VehicleFleetModal: React.FC<VehicleFleetModalProps> = ({
  isOpen,
  onClose,
  vehicleList,
  onSelectVehicle,
  initialFilter = 'ALL',
}) => {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | 'INSIDE' | 'OUTSIDE'>(initialFilter);
  const [companyFilter, setCompanyFilter] = useState<string>('ALL');

  const companies = useMemo(() => {
    const set = new Set<string>();
    vehicleList.forEach((v) => {
      if (v.assignedCompany) set.add(v.assignedCompany);
    });
    return Array.from(set);
  }, [vehicleList]);

  const filteredList = useMemo(() => {
    return vehicleList.filter((v) => {
      // Status filter
      if (statusFilter === 'INSIDE' && v.currentStatus !== 'INSIDE') return false;
      if (statusFilter === 'OUTSIDE' && v.currentStatus !== 'OUTSIDE') return false;

      // Company filter
      if (companyFilter !== 'ALL' && v.assignedCompany !== companyFilter) return false;

      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.trim().toLowerCase();
        const matchId = v.id.toLowerCase().includes(query);
        const matchPlate = v.plateNumber.toLowerCase().includes(query);
        const matchModel = (v.model || '').toLowerCase().includes(query);
        const matchType = (v.type || '').toLowerCase().includes(query);
        const matchCompany = (v.assignedCompany || '').toLowerCase().includes(query);
        const matchMil = (v.militaryRegNumber || '').toLowerCase().includes(query);
        return matchId || matchPlate || matchModel || matchType || matchCompany || matchMil;
      }

      return true;
    });
  }, [vehicleList, statusFilter, companyFilter, searchQuery]);

  const insideCount = vehicleList.filter((v) => v.currentStatus === 'INSIDE').length;
  const outsideCount = vehicleList.filter((v) => v.currentStatus === 'OUTSIDE').length;

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-black/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div className="w-full max-w-lg bg-white rounded-3xl shadow-2xl border border-zinc-200 overflow-hidden flex flex-col max-h-[92vh] sm:max-h-[85vh]">
        {/* Header */}
        <div className="px-5 py-4 bg-zinc-900 text-white flex items-center justify-between shrink-0">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-zinc-800 border border-zinc-700 flex items-center justify-center text-amber-400">
              <Truck className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-base tracking-tight text-white leading-tight">
                  Vehicle Fleet Registry
                </h3>
                <span className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-zinc-800 text-zinc-300 border border-zinc-700">
                  {vehicleList.length} TOTAL
                </span>
              </div>
              <p className="text-[11px] text-zinc-400 mt-0.5">
                Tactical trucks, transport vans & fleet status
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => {
                downloadVehicleFleetCsv(filteredList);
              }}
              className="flex items-center gap-1 px-2.5 py-1 bg-zinc-800 hover:bg-zinc-700 text-zinc-200 hover:text-white rounded-lg text-xs font-semibold transition-all border border-zinc-700 cursor-pointer shadow-xs"
              title="Export current fleet list to CSV"
            >
              <Download className="w-3.5 h-3.5 text-amber-400" />
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

        {/* Search & Filter Bar */}
        <div className="p-4 bg-zinc-50 border-b border-zinc-200 space-y-3 shrink-0">
          <div className="relative">
            <Search className="w-4 h-4 text-zinc-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Search by ID, plate number, model, company..."
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
                All ({vehicleList.length})
              </button>
              <button
                onClick={() => setStatusFilter('INSIDE')}
                className={`px-3 py-1.5 rounded-xl font-bold transition-all cursor-pointer flex items-center gap-1.5 text-xs ${
                  statusFilter === 'INSIDE'
                    ? 'bg-amber-600 text-white shadow-2xs'
                    : 'bg-amber-50 text-amber-900 border border-amber-200/90 hover:bg-amber-100/70'
                }`}
              >
                <span className="w-2 h-2 rounded-full bg-amber-400 animate-pulse" />
                In Yard ({insideCount})
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
                Dispatched ({outsideCount})
              </button>
            </div>

            {companies.length > 0 && (
              <select
                value={companyFilter}
                onChange={(e) => setCompanyFilter(e.target.value)}
                className="bg-white border border-zinc-200 rounded-xl px-2.5 py-1.5 text-xs font-semibold text-zinc-700 focus:outline-none cursor-pointer shrink-0"
              >
                <option value="ALL">All Companies</option>
                {companies.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            )}
          </div>
        </div>

        {/* Scrollable Vehicle List */}
        <div className="flex-1 overflow-y-auto p-4 space-y-2.5">
          {filteredList.length === 0 ? (
            <div className="p-8 text-center text-zinc-400">
              <Truck className="w-10 h-10 mx-auto text-zinc-300 stroke-1 mb-2" />
              <p className="text-sm font-semibold text-zinc-600">No vehicles found</p>
              <p className="text-xs text-zinc-400 mt-1">Try clearing search terms or status filters</p>
            </div>
          ) : (
            filteredList.map((v) => {
              const isInside = v.currentStatus === 'INSIDE';
              const lastSeenText = v.lastEntryTime || 'No gate scan recorded';

              return (
                <div
                  key={v.id}
                  onClick={() => {
                    onSelectVehicle(v);
                    onClose();
                  }}
                  className="flex items-center justify-between p-3.5 bg-white hover:bg-zinc-50 border border-zinc-200/90 hover:border-zinc-300 rounded-2xl transition-all cursor-pointer shadow-2xs group active:scale-[0.99]"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="w-11 h-11 rounded-xl bg-zinc-100 border border-zinc-200 flex items-center justify-center text-zinc-700 shrink-0 relative">
                      <Truck className="w-5 h-5" />
                      <span
                        className={`absolute -bottom-1 -right-1 w-3 h-3 rounded-full border-2 border-white ${
                          isInside ? 'bg-amber-500' : 'bg-zinc-400'
                        }`}
                      />
                    </div>
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-bold text-zinc-900 truncate">
                          {v.plateNumber}
                        </span>
                        <span className="text-[10px] font-mono font-bold px-1.5 py-0.2 rounded bg-zinc-100 text-zinc-700 border border-zinc-200 shrink-0">
                          {v.id}
                        </span>
                      </div>
                      <div className="text-xs text-zinc-500 truncate mt-0.5">
                        <span>{v.model} • {v.type}</span>
                      </div>
                      <span className="text-[10px] font-mono text-zinc-400 block mt-0.5">
                        {v.assignedCompany}
                      </span>
                    </div>
                  </div>

                  <div className="shrink-0 flex flex-col items-end gap-1.5">
                    <span
                      className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-mono font-bold border ${
                        isInside
                          ? 'bg-amber-50 text-amber-800 border-amber-200'
                          : 'bg-zinc-100 text-zinc-600 border-zinc-200'
                      }`}
                    >
                      {isInside ? (
                        <ArrowDownLeft className="w-3 h-3 text-amber-600" />
                      ) : (
                        <ArrowUpRight className="w-3 h-3 text-zinc-500" />
                      )}
                      <span>{isInside ? 'IN YARD' : 'OUT / DISP'}</span>
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

        {/* Footer */}
        <div className="p-3 bg-zinc-50 border-t border-zinc-200 flex items-center justify-between text-xs text-zinc-500 shrink-0">
          <span className="text-[11px]">
            Tap vehicle card to start driver manifest verification flow
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
