import React from 'react';

export const GuillochePattern: React.FC<{
  opacity?: number;
  strokeColor?: string;
  className?: string;
}> = ({
  opacity = 0.12,
  strokeColor = '#e2e8f0',
  className = 'absolute inset-0 pointer-events-none overflow-hidden',
}) => {
  return (
    <div className={className} style={{ opacity }}>
      <svg
        width="100%"
        height="100%"
        xmlns="http://www.w3.org/2000/svg"
        className="w-full h-full"
      >
        <defs>
          <pattern
            id="guilloche-waves"
            width="80"
            height="40"
            patternUnits="userSpaceOnUse"
          >
            {/* Wave Harmonic 1 */}
            <path
              d="M 0 20 Q 20 0, 40 20 T 80 20"
              fill="none"
              stroke={strokeColor}
              strokeWidth="0.5"
            />
            {/* Wave Harmonic 2 */}
            <path
              d="M 0 10 Q 20 30, 40 10 T 80 10"
              fill="none"
              stroke={strokeColor}
              strokeWidth="0.5"
            />
            {/* Wave Harmonic 3 */}
            <path
              d="M 0 25 Q 20 45, 40 25 T 80 25"
              fill="none"
              stroke={strokeColor}
              strokeWidth="0.35"
            />
            {/* Geometric diamond rosette */}
            <circle cx="40" cy="20" r="12" fill="none" stroke={strokeColor} strokeWidth="0.3" strokeDasharray="1 2" />
            <circle cx="40" cy="20" r="6" fill="none" stroke={strokeColor} strokeWidth="0.2" />
          </pattern>
        </defs>
        <rect width="100%" height="100%" fill="url(#guilloche-waves)" />
      </svg>
    </div>
  );
};

export const MicroPrintBorder: React.FC<{ text?: string }> = ({
  text = '',
}) => {
  return (
    <div className="w-full overflow-hidden whitespace-nowrap text-[5px] font-mono tracking-widest text-slate-400/40 select-none uppercase py-0.5 border-t border-b border-white/5">
      <span>{text.repeat(4)}</span>
    </div>
  );
};
