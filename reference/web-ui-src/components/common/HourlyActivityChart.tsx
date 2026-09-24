import React, { useMemo, useState } from 'react';
import {
  ResponsiveContainer,
  BarChart,
  Bar,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  Legend,
  CartesianGrid,
} from 'recharts';
import {
  BarChart2,
  TrendingUp,
  Clock,
  User,
  Truck,
  ArrowDownLeft,
  ArrowUpRight,
  Maximize2,
  Minimize2,
} from 'lucide-react';

interface ActivityItem {
  id: string;
  timestamp: string;
  timestampMs?: number;
  type: 'PERSON' | 'VEHICLE' | string;
  action: 'ENTRY' | 'EXIT' | string;
  targetId?: string;
  title?: string;
  gate?: string;
  location?: string;
  tag?: string;
  [key: string]: any;
}

interface HourlyActivityChartProps {
  records: ActivityItem[];
  isLoading?: boolean;
}

export const HourlyActivityChart: React.FC<HourlyActivityChartProps> = ({
  records,
  isLoading = false,
}) => {
  const [metricMode, setMetricMode] = useState<'MOVEMENT' | 'ENTITY' | 'TOTAL'>('MOVEMENT');
  const [chartType, setChartType] = useState<'BAR' | 'AREA'>('BAR');
  const [hourRange, setHourRange] = useState<'ACTIVE_ONLY' | 'FULL_24H'>('FULL_24H');
  const [isCompact, setIsCompact] = useState(false);

  // Compute hourly aggregation
  const { hourlyData, stats } = useMemo(() => {
    // Initialize 24-hour slots
    const hoursMap: Record<
      number,
      {
        hour: number;
        hourLabel: string;
        displayHour: string;
        entries: number;
        exits: number;
        personnel: number;
        vehicles: number;
        total: number;
        sampleItems: { id: string; title?: string; type: string; action: string; time: string }[];
      }
    > = {};

    for (let i = 0; i < 24; i++) {
      const hStr = i.toString().padStart(2, '0');
      hoursMap[i] = {
        hour: i,
        hourLabel: `${hStr}:00`,
        displayHour: `${hStr}:00`,
        entries: 0,
        exits: 0,
        personnel: 0,
        vehicles: 0,
        total: 0,
        sampleItems: [],
      };
    }

    let totalEntries = 0;
    let totalExits = 0;
    let totalPersonnel = 0;
    let totalVehicles = 0;

    // Process all provided records
    records.forEach((rec) => {
      let hour = -1;

      // Extract hour from timestamp "HH:MM:SS" or timestampMs
      if (rec.timestamp && typeof rec.timestamp === 'string') {
        const parts = rec.timestamp.split(':');
        const parsed = parseInt(parts[0], 10);
        if (!isNaN(parsed) && parsed >= 0 && parsed <= 23) {
          hour = parsed;
        }
      }

      if (hour === -1 && rec.timestampMs) {
        const d = new Date(rec.timestampMs);
        if (!isNaN(d.getTime())) {
          hour = d.getHours();
        }
      }

      // Default fallback to 0 if indeterminate
      if (hour < 0 || hour > 23) {
        hour = 0;
      }

      const slot = hoursMap[hour];
      slot.total += 1;

      if (rec.action === 'ENTRY') {
        slot.entries += 1;
        totalEntries += 1;
      } else {
        slot.exits += 1;
        totalExits += 1;
      }

      if (rec.type === 'VEHICLE') {
        slot.vehicles += 1;
        totalVehicles += 1;
      } else {
        slot.personnel += 1;
        totalPersonnel += 1;
      }

      if (slot.sampleItems.length < 3) {
        slot.sampleItems.push({
          id: rec.id,
          title: rec.title || rec.targetId || rec.id,
          type: rec.type,
          action: rec.action,
          time: rec.timestamp,
        });
      }
    });

    const allSlots = Object.values(hoursMap);

    // Identify Peak Hour
    let peakHour = { hourLabel: 'None', total: 0 };
    allSlots.forEach((slot) => {
      if (slot.total > peakHour.total) {
        peakHour = { hourLabel: slot.hourLabel, total: slot.total };
      }
    });

    // Filter for active hours if user selected ACTIVE_ONLY
    const dataToRender =
      hourRange === 'ACTIVE_ONLY'
        ? allSlots.filter((slot) => slot.total > 0)
        : allSlots;

    return {
      hourlyData: dataToRender,
      stats: {
        totalRecords: records.length,
        totalEntries,
        totalExits,
        totalPersonnel,
        totalVehicles,
        peakHour,
        activeHoursCount: allSlots.filter((slot) => slot.total > 0).length,
      },
    };
  }, [records, hourRange]);

  // Custom Recharts Tooltip
  const CustomTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length) {
      const data = payload[0].payload;
      return (
        <div className="bg-zinc-950/95 border border-zinc-700/90 rounded-xl p-3 shadow-2xl backdrop-blur-md text-xs space-y-2 min-w-[200px] z-50">
          <div className="flex items-center justify-between border-b border-zinc-800 pb-1.5 font-mono">
            <span className="font-semibold text-zinc-100 flex items-center gap-1.5">
              <Clock className="w-3.5 h-3.5 text-emerald-400" />
              {label} - {label.replace(':00', ':59')}
            </span>
            <span className="px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-200 text-[10px] font-bold">
              {data.total} {data.total === 1 ? 'scan' : 'scans'}
            </span>
          </div>

          <div className="space-y-1 text-[11px]">
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1 text-emerald-400">
                <ArrowDownLeft className="w-3 h-3" /> Entries:
              </span>
              <strong className="text-zinc-100 font-mono">{data.entries}</strong>
            </div>
            <div className="flex items-center justify-between">
              <span className="flex items-center gap-1 text-amber-400">
                <ArrowUpRight className="w-3 h-3" /> Exits:
              </span>
              <strong className="text-zinc-100 font-mono">{data.exits}</strong>
            </div>
            <div className="pt-1 border-t border-zinc-800/80 flex items-center justify-between text-zinc-400">
              <span className="flex items-center gap-1">
                <User className="w-3 h-3 text-blue-400" /> Personnel:
              </span>
              <span className="font-mono text-zinc-200">{data.personnel}</span>
            </div>
            <div className="flex items-center justify-between text-zinc-400">
              <span className="flex items-center gap-1">
                <Truck className="w-3 h-3 text-orange-400" /> Vehicles:
              </span>
              <span className="font-mono text-zinc-200">{data.vehicles}</span>
            </div>
          </div>

          {data.sampleItems && data.sampleItems.length > 0 && (
            <div className="pt-1.5 border-t border-zinc-800/80 text-[10px] text-zinc-400 space-y-1">
              <span className="text-[9px] uppercase tracking-wider text-zinc-500 font-semibold block">
                Recent Scans in Hour:
              </span>
              {data.sampleItems.map((item: any, i: number) => (
                <div key={i} className="truncate text-zinc-300 flex items-center gap-1">
                  <span
                    className={`w-1.5 h-1.5 rounded-full ${
                      item.action === 'ENTRY' ? 'bg-emerald-400' : 'bg-amber-400'
                    }`}
                  />
                  <span className="truncate">{item.title}</span>
                  <span className="text-zinc-500 ml-auto font-mono text-[9px]">{item.time}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      );
    }
    return null;
  };

  return (
    <div className="bg-zinc-950/80 border border-zinc-800 rounded-xl p-3.5 space-y-3">
      {/* Header & Controls */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5 pb-2.5 border-b border-zinc-800/80">
        <div className="flex items-center gap-2">
          <div className="p-1.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-400">
            <BarChart2 className="w-4 h-4" />
          </div>
          <div>
            <h3 className="text-xs font-semibold text-zinc-100 flex items-center gap-1.5">
              Hourly Activity Volume
              <span className="text-[10px] font-normal text-zinc-400">
                ({records.length} {records.length === 1 ? 'record' : 'records'})
              </span>
            </h3>
            <p className="text-[10px] text-zinc-400">
              Distribution of checkpoint scans across hourly intervals
            </p>
          </div>
        </div>

        {/* View & Grouping Toggles */}
        <div className="flex items-center gap-1.5 flex-wrap text-xs">
          {/* Metric Mode */}
          <div className="flex items-center bg-zinc-900 border border-zinc-800 rounded-lg p-0.5">
            <button
              onClick={() => setMetricMode('MOVEMENT')}
              className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                metricMode === 'MOVEMENT'
                  ? 'bg-zinc-800 text-emerald-400 font-semibold shadow-xs'
                  : 'text-zinc-400 hover:text-zinc-200'
              }`}
            >
              Entry / Exit
            </button>
            <button
              onClick={() => setMetricMode('ENTITY')}
              className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                metricMode === 'ENTITY'
                  ? 'bg-zinc-800 text-blue-400 font-semibold shadow-xs'
                  : 'text-zinc-400 hover:text-zinc-200'
              }`}
            >
              Person / Vehicle
            </button>
            <button
              onClick={() => setMetricMode('TOTAL')}
              className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                metricMode === 'TOTAL'
                  ? 'bg-zinc-800 text-purple-400 font-semibold shadow-xs'
                  : 'text-zinc-400 hover:text-zinc-200'
              }`}
            >
              Total
            </button>
          </div>

          {/* Chart Style (Bar vs Area) */}
          <div className="flex items-center bg-zinc-900 border border-zinc-800 rounded-lg p-0.5">
            <button
              onClick={() => setChartType('BAR')}
              className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                chartType === 'BAR'
                  ? 'bg-zinc-800 text-zinc-100 font-semibold shadow-xs'
                  : 'text-zinc-400 hover:text-zinc-200'
              }`}
            >
              Bar
            </button>
            <button
              onClick={() => setChartType('AREA')}
              className={`px-2 py-1 rounded text-[11px] font-medium transition-colors cursor-pointer ${
                chartType === 'AREA'
                  ? 'bg-zinc-800 text-zinc-100 font-semibold shadow-xs'
                  : 'text-zinc-400 hover:text-zinc-200'
              }`}
            >
              Area
            </button>
          </div>

          {/* 24H vs Active Hours */}
          <button
            onClick={() => setHourRange(hourRange === 'FULL_24H' ? 'ACTIVE_ONLY' : 'FULL_24H')}
            className="px-2 py-1 bg-zinc-900 hover:bg-zinc-850 border border-zinc-800 rounded-lg text-[11px] text-zinc-300 transition-colors cursor-pointer"
            title="Toggle between full 24h timeline and active hours only"
          >
            {hourRange === 'FULL_24H' ? '24h Timeline' : 'Active Hours Only'}
          </button>

          {/* Compact Toggle */}
          <button
            onClick={() => setIsCompact(!isCompact)}
            className="p-1 text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 rounded transition-colors cursor-pointer"
            title={isCompact ? 'Expand chart height' : 'Compact view'}
          >
            {isCompact ? <Maximize2 className="w-3.5 h-3.5" /> : <Minimize2 className="w-3.5 h-3.5" />}
          </button>
        </div>
      </div>

      {/* Summary KPI Badges */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-0.5 text-xs">
        <div className="px-3 py-1.5 rounded-lg bg-zinc-900/60 border border-zinc-850 flex items-center justify-between">
          <span className="text-[11px] text-zinc-400 flex items-center gap-1">
            <TrendingUp className="w-3 h-3 text-emerald-400" />
            Peak Hour:
          </span>
          <span className="font-mono font-semibold text-zinc-200">
            {stats.peakHour.total > 0 ? `${stats.peakHour.hourLabel} (${stats.peakHour.total})` : '—'}
          </span>
        </div>

        <div className="px-3 py-1.5 rounded-lg bg-zinc-900/60 border border-zinc-850 flex items-center justify-between">
          <span className="text-[11px] text-zinc-400 flex items-center gap-1">
            <ArrowDownLeft className="w-3 h-3 text-emerald-400" />
            Entries / Exits:
          </span>
          <span className="font-mono font-semibold text-zinc-200">
            {stats.totalEntries} / {stats.totalExits}
          </span>
        </div>

        <div className="px-3 py-1.5 rounded-lg bg-zinc-900/60 border border-zinc-850 flex items-center justify-between">
          <span className="text-[11px] text-zinc-400 flex items-center gap-1">
            <User className="w-3 h-3 text-blue-400" />
            Personnel / Vehicles:
          </span>
          <span className="font-mono font-semibold text-zinc-200">
            {stats.totalPersonnel} / {stats.totalVehicles}
          </span>
        </div>

        <div className="px-3 py-1.5 rounded-lg bg-zinc-900/60 border border-zinc-850 flex items-center justify-between">
          <span className="text-[11px] text-zinc-400 flex items-center gap-1">
            <Clock className="w-3 h-3 text-zinc-400" />
            Active Windows:
          </span>
          <span className="font-mono font-semibold text-zinc-200">
            {stats.activeHoursCount} {stats.activeHoursCount === 1 ? 'hour' : 'hours'}
          </span>
        </div>
      </div>

      {/* Chart Canvas Area */}
      <div className={`w-full transition-all duration-200 ${isCompact ? 'h-40' : 'h-56 sm:h-64'}`}>
        {isLoading ? (
          <div className="w-full h-full flex items-center justify-center text-xs text-zinc-400 gap-2">
            <span className="w-4 h-4 border-2 border-emerald-400 border-t-transparent rounded-full animate-spin" />
            Loading volume telemetry...
          </div>
        ) : hourlyData.length === 0 || (hourRange === 'ACTIVE_ONLY' && stats.totalRecords === 0) ? (
          <div className="w-full h-full flex flex-col items-center justify-center text-xs text-zinc-500 space-y-1">
            <BarChart2 className="w-8 h-8 text-zinc-700" />
            <p>No activity records available for this period.</p>
          </div>
        ) : (
          <ResponsiveContainer width="100%" height="100%">
            {chartType === 'BAR' ? (
              <BarChart
                data={hourlyData}
                margin={{ top: 10, right: 10, left: -20, bottom: 0 }}
              >
                <CartesianGrid strokeDasharray="3 3" stroke="#27272a" vertical={false} />
                <XAxis
                  dataKey="displayHour"
                  stroke="#71717a"
                  fontSize={10}
                  tickLine={false}
                  interval={hourRange === 'FULL_24H' ? 2 : 0}
                />
                <YAxis
                  stroke="#71717a"
                  fontSize={10}
                  tickLine={false}
                  allowDecimals={false}
                />
                <Tooltip content={<CustomTooltip />} />
                <Legend
                  wrapperStyle={{ fontSize: '11px', paddingTop: '6px' }}
                  iconType="circle"
                />

                {metricMode === 'MOVEMENT' && (
                  <>
                    <Bar
                      dataKey="entries"
                      name="Entries"
                      fill="#10b981"
                      radius={[4, 4, 0, 0]}
                      maxBarSize={28}
                    />
                    <Bar
                      dataKey="exits"
                      name="Exits"
                      fill="#f59e0b"
                      radius={[4, 4, 0, 0]}
                      maxBarSize={28}
                    />
                  </>
                )}

                {metricMode === 'ENTITY' && (
                  <>
                    <Bar
                      dataKey="personnel"
                      name="Personnel"
                      fill="#3b82f6"
                      radius={[4, 4, 0, 0]}
                      maxBarSize={28}
                    />
                    <Bar
                      dataKey="vehicles"
                      name="Vehicles"
                      fill="#f97316"
                      radius={[4, 4, 0, 0]}
                      maxBarSize={28}
                    />
                  </>
                )}

                {metricMode === 'TOTAL' && (
                  <Bar
                    dataKey="total"
                    name="Total Activity"
                    fill="#8b5cf6"
                    radius={[4, 4, 0, 0]}
                    maxBarSize={32}
                  />
                )}
              </BarChart>
            ) : (
              <AreaChart
                data={hourlyData}
                margin={{ top: 10, right: 10, left: -20, bottom: 0 }}
              >
                <defs>
                  <linearGradient id="colorEntries" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#10b981" stopOpacity={0.7} />
                    <stop offset="95%" stopColor="#10b981" stopOpacity={0.05} />
                  </linearGradient>
                  <linearGradient id="colorExits" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#f59e0b" stopOpacity={0.7} />
                    <stop offset="95%" stopColor="#f59e0b" stopOpacity={0.05} />
                  </linearGradient>
                  <linearGradient id="colorPersonnel" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.7} />
                    <stop offset="95%" stopColor="#3b82f6" stopOpacity={0.05} />
                  </linearGradient>
                  <linearGradient id="colorVehicles" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#f97316" stopOpacity={0.7} />
                    <stop offset="95%" stopColor="#f97316" stopOpacity={0.05} />
                  </linearGradient>
                  <linearGradient id="colorTotal" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#8b5cf6" stopOpacity={0.7} />
                    <stop offset="95%" stopColor="#8b5cf6" stopOpacity={0.05} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#27272a" vertical={false} />
                <XAxis
                  dataKey="displayHour"
                  stroke="#71717a"
                  fontSize={10}
                  tickLine={false}
                  interval={hourRange === 'FULL_24H' ? 2 : 0}
                />
                <YAxis
                  stroke="#71717a"
                  fontSize={10}
                  tickLine={false}
                  allowDecimals={false}
                />
                <Tooltip content={<CustomTooltip />} />
                <Legend
                  wrapperStyle={{ fontSize: '11px', paddingTop: '6px' }}
                  iconType="circle"
                />

                {metricMode === 'MOVEMENT' && (
                  <>
                    <Area
                      type="monotone"
                      dataKey="entries"
                      name="Entries"
                      stroke="#10b981"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorEntries)"
                    />
                    <Area
                      type="monotone"
                      dataKey="exits"
                      name="Exits"
                      stroke="#f59e0b"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorExits)"
                    />
                  </>
                )}

                {metricMode === 'ENTITY' && (
                  <>
                    <Area
                      type="monotone"
                      dataKey="personnel"
                      name="Personnel"
                      stroke="#3b82f6"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorPersonnel)"
                    />
                    <Area
                      type="monotone"
                      dataKey="vehicles"
                      name="Vehicles"
                      stroke="#f97316"
                      strokeWidth={2}
                      fillOpacity={1}
                      fill="url(#colorVehicles)"
                    />
                  </>
                )}

                {metricMode === 'TOTAL' && (
                  <Area
                    type="monotone"
                    dataKey="total"
                    name="Total Activity"
                    stroke="#8b5cf6"
                    strokeWidth={2}
                    fillOpacity={1}
                    fill="url(#colorTotal)"
                  />
                )}
              </AreaChart>
            )}
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
};
