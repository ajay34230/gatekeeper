import React from 'react';

export const NationalCrest: React.FC<{ className?: string }> = ({ className = 'w-10 h-10' }) => {
  return (
    <svg viewBox="0 0 100 120" fill="currentColor" className={className}>
      {/* Ashoka Lion Capital stylized vector representation */}
      <g opacity="0.95">
        {/* Central Pedestal */}
        <path d="M25,88 L75,88 L80,98 L20,98 Z" />
        <rect x="22" y="99" width="56" height="5" rx="1.5" />
        <rect x="18" y="105" width="64" height="4" rx="1" />
        
        {/* Ashoka Chakra Wheel */}
        <circle cx="50" cy="93" r="4.5" fill="none" stroke="#000" strokeWidth="1" />
        <circle cx="50" cy="93" r="1.5" />
        {/* Galloping horse & bull accents */}
        <path d="M30,94 C33,92 36,94 38,95" fill="none" stroke="#000" strokeWidth="0.8" />
        <path d="M62,95 C64,94 67,92 70,94" fill="none" stroke="#000" strokeWidth="0.8" />

        {/* Center Lion Head */}
        <path d="M42,22 C42,15 46,12 50,12 C54,12 58,15 58,22 C61,24 63,28 62,34 C61,40 58,45 58,52 C58,58 60,65 58,72 L42,72 C40,65 42,58 42,52 C42,45 39,40 38,34 C37,28 39,24 42,22 Z" />
        {/* Center Lion Mane details */}
        <path d="M46,26 C48,24 52,24 54,26 C56,29 55,34 50,34 C45,34 44,29 46,26 Z" />
        <circle cx="47" cy="27" r="1" />
        <circle cx="53" cy="27" r="1" />
        <path d="M48,31 L52,31 L50,33 Z" />

        {/* Left Lion Profile */}
        <path d="M38,28 C34,22 28,24 25,28 C22,34 22,40 25,48 C28,54 32,60 38,65 L41,60 C36,54 32,48 31,42 C30,36 33,31 38,28 Z" />
        <circle cx="28" cy="30" r="1" />

        {/* Right Lion Profile */}
        <path d="M62,28 C66,22 72,24 75,28 C78,34 78,40 75,48 C72,54 68,60 62,65 L59,60 C64,54 68,48 69,42 C70,36 67,31 62,28 Z" />
        <circle cx="72" cy="30" r="1" />

        {/* Base Lotus stylized arches */}
        <path d="M35,76 Q50,70 65,76 L63,82 Q50,77 37,82 Z" />
      </g>
    </svg>
  );
};

export const CrossedSwordsBadge: React.FC<{ className?: string }> = ({ className = 'w-6 h-6' }) => {
  return (
    <svg viewBox="0 0 48 48" fill="currentColor" className={className}>
      {/* Sword 1 (NW to SE) */}
      <path d="M8,10 L38,40 L40,38 L10,8 Z" />
      <rect x="33" y="33" width="10" height="2.5" transform="rotate(-45 38 34)" />
      <circle cx="41" cy="41" r="2" />
      <path d="M7,7 L11,11 L9,12 L6,9 Z" />

      {/* Sword 2 (NE to SW) */}
      <path d="M40,10 L10,40 L8,38 L38,8 Z" />
      <rect x="5" y="33" width="10" height="2.5" transform="rotate(45 10 34)" />
      <circle cx="7" cy="41" r="2" />
      <path d="M41,7 L37,11 L39,12 L42,9 Z" />

      {/* Central 5-pointed star */}
      <polygon points="24,14 26.5,21 34,21 28,25.5 30.5,33 24,28.5 17.5,33 20,25.5 14,21 21.5,21" fill="#f59e0b" />
    </svg>
  );
};

export const SmartChip: React.FC<{ className?: string }> = ({ className = 'w-9 h-7' }) => {
  return (
    <div className={`relative rounded bg-gradient-to-br from-amber-200 via-amber-400 to-amber-600 p-[1.5px] shadow-sm ${className}`}>
      <div className="w-full h-full rounded-[3px] bg-gradient-to-br from-amber-300 via-amber-500 to-amber-700 relative overflow-hidden flex flex-col justify-between p-0.5 border border-amber-600/40">
        <div className="flex justify-between w-full h-1/3">
          <div className="w-2.5 h-full border-b border-r border-amber-800/40 rounded-br"></div>
          <div className="w-2 h-full border-b border-amber-800/40"></div>
          <div className="w-2.5 h-full border-b border-l border-amber-800/40 rounded-bl"></div>
        </div>
        <div className="w-full h-1/3 flex items-center justify-center">
          <div className="w-3 h-3 rounded-full border border-amber-900/40 bg-amber-400/60 flex items-center justify-center">
            <div className="w-1.5 h-1.5 rounded-full bg-amber-800/60"></div>
          </div>
        </div>
        <div className="flex justify-between w-full h-1/3">
          <div className="w-2.5 h-full border-t border-r border-amber-800/40 rounded-tr"></div>
          <div className="w-2 h-full border-t border-amber-800/40"></div>
          <div className="w-2.5 h-full border-t border-l border-amber-800/40 rounded-tl"></div>
        </div>
      </div>
    </div>
  );
};

export const HolographicSeal: React.FC<{ className?: string; label?: string }> = ({ 
  className = 'w-12 h-12',
  label = 'AUTHENTIC'
}) => {
  return (
    <div className={`relative rounded-full p-0.5 flex items-center justify-center overflow-hidden select-none ${className}`}>
      {/* Holographic Iridescent Shimmer */}
      <div className="absolute inset-0 bg-gradient-to-tr from-amber-400/40 via-emerald-400/40 to-cyan-400/40 animate-pulse opacity-85"></div>
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_center,_var(--tw-gradient-stops))] from-yellow-300/30 via-pink-400/20 to-indigo-500/30 mix-blend-color-dodge"></div>
      
      {/* Outer Tooled Border */}
      <div className="relative w-full h-full rounded-full border border-amber-300/70 flex flex-col items-center justify-center bg-black/35 backdrop-blur-[1px] p-1">
        <svg viewBox="0 0 100 100" className="w-full h-full" fill="none">
          {/* Star rays */}
          <circle cx="50" cy="50" r="46" stroke="#fcd34d" strokeWidth="0.75" strokeDasharray="2,2" />
          <circle cx="50" cy="50" r="38" stroke="#67e8f9" strokeWidth="0.5" />
          
          {/* Center Ashoka Emblem Outline */}
          <circle cx="50" cy="50" r="22" fill="#000000" fillOpacity="0.4" stroke="#fef08a" strokeWidth="0.8" />
          <path d="M43,43 L57,43 L55,57 L45,57 Z" fill="#fef08a" opacity="0.8" />
          <circle cx="50" cy="40" r="3" fill="#fef08a" />
        </svg>
        <span className="absolute bottom-1.5 text-[5.5px] font-extrabold tracking-widest text-amber-200 uppercase scale-90">
          {label}
        </span>
      </div>
    </div>
  );
};

export const SecurityWatermark: React.FC<{ className?: string }> = ({ className = 'w-48 h-48' }) => {
  return (
    <div className={`absolute pointer-events-none select-none flex items-center justify-center opacity-7 ${className}`}>
      <NationalCrest className="w-full h-full text-white" />
    </div>
  );
};
