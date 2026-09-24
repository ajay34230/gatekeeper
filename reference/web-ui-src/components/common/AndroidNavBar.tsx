import React from 'react';
import { Home, History, RefreshCw, UserCheck } from 'lucide-react';
import { ScreenId } from '../../types';

interface AndroidNavBarProps {
  currentScreen: ScreenId;
  onNavigate: (screen: ScreenId) => void;
  pendingSyncCount?: number;
}

export const AndroidNavBar: React.FC<AndroidNavBarProps> = ({
  currentScreen,
  onNavigate,
  pendingSyncCount = 0,
}) => {
  const tabs: Array<{ id: ScreenId; label: string; icon: React.ComponentType<{ className?: string }> }> = [
    { id: 'HOME', label: 'Terminal', icon: Home },
    { id: 'ACTIVITY', label: 'Log Feed', icon: History },
    { id: 'SYNC', label: 'Sync Hub', icon: RefreshCw },
    { id: 'OPERATOR', label: 'Profile', icon: UserCheck },
  ];

  return (
    <nav className="bg-white/95 backdrop-blur-md border-t border-zinc-200/90 w-full shrink-0 z-20 select-none pb-safe shadow-[0_-4px_16px_rgba(0,0,0,0.02)]">
      <div className="grid grid-cols-4 h-16 max-w-md mx-auto px-2">
        {tabs.map((tab) => {
          const isActive = currentScreen === tab.id;
          const IconComponent = tab.icon;

          return (
            <button
              key={tab.id}
              onClick={() => onNavigate(tab.id)}
              className="flex flex-col items-center justify-center py-1 transition-all cursor-pointer group"
            >
              <div
                className={`relative flex items-center justify-center w-14 h-7 rounded-full transition-all duration-200 ${
                  isActive
                    ? 'bg-zinc-900 text-white shadow-xs'
                    : 'text-zinc-500 hover:text-zinc-800 hover:bg-zinc-100'
                }`}
              >
                <IconComponent className={`w-4 h-4 transition-transform ${isActive ? 'scale-105' : ''}`} />
                {tab.id === 'SYNC' && pendingSyncCount > 0 && (
                  <span className="absolute -top-0.5 right-2 w-2.5 h-2.5 bg-amber-500 rounded-full ring-2 ring-white animate-pulse" />
                )}
              </div>
              <span
                className={`text-[11px] tracking-tight mt-1 transition-colors ${
                  isActive ? 'text-zinc-900 font-bold' : 'text-zinc-500 font-medium'
                }`}
              >
                {tab.label}
              </span>
            </button>
          );
        })}
      </div>
    </nav>
  );
};
