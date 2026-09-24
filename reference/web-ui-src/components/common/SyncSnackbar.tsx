import React, { useEffect, useState, useRef } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { Wifi, CheckCircle2 } from 'lucide-react';

export interface SyncSnackbarProps {
  /** Current connection status */
  isOnline: boolean;
  /** Number of pending unsynced records flushed or to be flushed */
  pendingCount?: number;
  /** Auto-dismiss duration in milliseconds (default: 3500) */
  duration?: number;
  /** Optional callback fired when the snackbar dismisses */
  onDismiss?: () => void;
}

/**
 * A subtle, non-intrusive notification snackbar that surfaces at the bottom
 * of the screen whenever the terminal transitions from offline to online.
 * Reassures the field operator that the network link is re-established and
 * records are synchronizing.
 */
export const SyncSnackbar: React.FC<SyncSnackbarProps> = ({
  isOnline,
  pendingCount = 0,
  duration = 3500,
  onDismiss,
}) => {
  const [visible, setVisible] = useState(false);
  const wasOnlineRef = useRef<boolean | null>(null);

  useEffect(() => {
    // Detect transition from offline (false) to online (true)
    if (wasOnlineRef.current === false && isOnline) {
      setVisible(true);
      const timer = setTimeout(() => {
        setVisible(false);
        if (onDismiss) onDismiss();
      }, duration);
      return () => clearTimeout(timer);
    }
    wasOnlineRef.current = isOnline;
  }, [isOnline, duration, onDismiss]);

  return (
    <AnimatePresence>
      {visible && (
        <motion.aside
          id="sync-reconnect-snackbar"
          role="status"
          aria-live="polite"
          initial={{ opacity: 0, y: 20, scale: 0.95 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: 12, scale: 0.95 }}
          transition={{
            type: 'spring',
            damping: 26,
            stiffness: 340,
            mass: 0.8,
          }}
          className="fixed bottom-20 left-1/2 -translate-x-1/2 z-50 w-auto max-w-[calc(100%-2rem)] sm:max-w-sm pointer-events-none select-none"
        >
          <div className="flex items-center gap-2.5 px-3.5 py-2.5 bg-zinc-900/95 backdrop-blur-md text-white border border-zinc-700/80 rounded-full shadow-lg shadow-black/30">
            {/* Pulsing Emerald Connectivity Dot */}
            <div className="relative flex items-center justify-center w-5 h-5 rounded-full bg-emerald-500/20 border border-emerald-500/40 shrink-0">
              <span className="w-2 h-2 rounded-full bg-emerald-400 animate-ping absolute opacity-75" />
              <span className="w-2 h-2 rounded-full bg-emerald-400" />
            </div>

            {/* Message & Status Details */}
            <div className="flex items-center gap-1.5 text-xs tracking-tight">
              <span className="font-semibold text-emerald-400 flex items-center gap-1">
                <Wifi className="w-3 h-3" />
                Back Online
              </span>
              <span className="text-zinc-500">•</span>
              <span className="text-zinc-200 font-medium truncate max-w-[200px] sm:max-w-[230px]">
                {pendingCount > 0
                  ? `Syncing ${pendingCount} pending record${pendingCount > 1 ? 's' : ''}...`
                  : 'Syncing live with Central Operations'}
              </span>
            </div>
          </div>
        </motion.aside>
      )}
    </AnimatePresence>
  );
};
