import React, { useState, useEffect } from 'react';
import { Wifi, WifiOff, Battery, Signal, CloudOff, Info } from 'lucide-react';
import { formatShortTime } from '../../data/mockDatabase';

interface AndroidStatusBarProps {
  isOnline: boolean;
  pendingCount?: number;
  onOpenDesignDoc?: () => void;
}

export const AndroidStatusBar: React.FC<AndroidStatusBarProps> = ({
  isOnline,
  pendingCount = 0,
  onOpenDesignDoc,
}) => {
  const [timeStr, setTimeStr] = useState(formatShortTime());

  useEffect(() => {
    const updateTime = () => setTimeStr(formatShortTime());
    updateTime();
    const timer = setInterval(updateTime, 10000);
    return () => clearInterval(timer);
  }, []);

  return (
    <header className="bg-zinc-950 text-zinc-300 w-full select-none z-30 shrink-0 border-b border-zinc-900">
      {/* Top Android Status Bar */}
      <div className="flex items-center justify-between px-5 py-2 text-xs font-mono tracking-tight">
        <div className="flex items-center gap-2">
          <span className="font-bold text-zinc-100 text-[13px]">{timeStr}</span>
          <span className="text-[10px] text-zinc-500 font-sans font-medium uppercase tracking-wider hidden sm:inline">
            Zebra SE4710
          </span>
        </div>
        
        <div className="flex items-center gap-2.5">
          {onOpenDesignDoc && (
            <button
              onClick={onOpenDesignDoc}
              className="text-[11px] text-zinc-300 hover:text-white flex items-center gap-1.5 bg-zinc-800/90 hover:bg-zinc-700/90 px-2 py-0.5 rounded-full border border-zinc-700/70 transition-all cursor-pointer font-sans"
              title="Design System & Architecture Spec"
            >
              <Info className="w-3 h-3 text-emerald-400" />
              <span className="font-semibold">Spec</span>
            </button>
          )}

          <div className="flex items-center gap-2 text-zinc-300">
            {isOnline ? (
              <div className="flex items-center gap-1 text-emerald-400" title="Connected to Central Hub">
                <Wifi className="w-3.5 h-3.5" />
                <span className="text-[10px] font-bold">5G</span>
              </div>
            ) : (
              <div className="flex items-center gap-1 text-amber-400" title="Offline Mode">
                <WifiOff className="w-3.5 h-3.5" />
                <span className="text-[10px] font-bold">OFFLINE</span>
              </div>
            )}
            <Signal className="w-3.5 h-3.5 text-zinc-300" />
            <div className="flex items-center gap-1 bg-zinc-900 px-1.5 py-0.5 rounded border border-zinc-800">
              <span className="text-[10px] font-bold text-zinc-200">96%</span>
              <Battery className="w-3.5 h-3.5 text-emerald-400 fill-emerald-400" />
            </div>
          </div>
        </div>
      </div>

      {/* Offline Alert Strip if disconnected */}
      {!isOnline && (
        <div className="bg-amber-500 text-zinc-950 px-4 py-1.5 text-xs flex items-center justify-between font-bold animate-pulse">
          <div className="flex items-center gap-2">
            <CloudOff className="w-4 h-4 text-zinc-950" />
            <span>OFFLINE BUFFERING ACTIVE</span>
          </div>
          <span className="text-[11px] bg-zinc-950 text-amber-400 font-mono px-2 py-0.5 rounded-full font-bold">
            {pendingCount} QUEUED
          </span>
        </div>
      )}
    </header>
  );
};
