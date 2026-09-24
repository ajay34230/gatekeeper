import React from 'react';
import { EntityStatus, PresenceStatus } from '../../types';

type ChipStatus = EntityStatus | PresenceStatus | 'ONLINE' | 'OFFLINE' | 'SYNCED' | 'PENDING';

interface StatusChipProps {
  status: ChipStatus;
  size?: 'sm' | 'md';
}

export const StatusChip: React.FC<StatusChipProps> = ({ status, size = 'md' }) => {
  const isSmall = size === 'sm';

  const config: Record<ChipStatus, { bg: string; dot: string; label: string }> = {
    ACTIVE: {
      bg: 'bg-emerald-50 border-emerald-200 text-emerald-700',
      dot: 'bg-emerald-500',
      label: 'Active',
    },
    ONLINE: {
      bg: 'bg-emerald-50 border-emerald-200 text-emerald-700',
      dot: 'bg-emerald-500',
      label: 'Online',
    },
    SYNCED: {
      bg: 'bg-emerald-50 border-emerald-200 text-emerald-700',
      dot: 'bg-emerald-500',
      label: 'Synced',
    },
    INSIDE: {
      bg: 'bg-blue-50 border-blue-200 text-blue-700',
      dot: 'bg-blue-500',
      label: 'On-Site (Inside)',
    },
    OUTSIDE: {
      bg: 'bg-zinc-100 border-zinc-300 text-zinc-700',
      dot: 'bg-zinc-400',
      label: 'Outside',
    },
    PENDING: {
      bg: 'bg-amber-50 border-amber-200 text-amber-700',
      dot: 'bg-amber-500',
      label: 'Pending Sync',
    },
    OFFLINE: {
      bg: 'bg-amber-50 border-amber-300 text-amber-800',
      dot: 'bg-amber-500',
      label: 'Offline',
    },
    SUSPENDED: {
      bg: 'bg-rose-50 border-rose-200 text-rose-700',
      dot: 'bg-rose-500',
      label: 'Suspended',
    },
    EXPIRED: {
      bg: 'bg-zinc-100 border-zinc-300 text-zinc-600',
      dot: 'bg-zinc-500',
      label: 'Expired',
    },
    FLAGGED: {
      bg: 'bg-rose-50 border-rose-300 text-rose-800',
      dot: 'bg-rose-600',
      label: 'Security Flag',
    },
  };

  const current = config[status] || config.ACTIVE;

  return (
    <span
      className={`inline-flex items-center gap-1.5 font-medium border rounded-full whitespace-nowrap ${
        isSmall ? 'text-xs px-2 py-0.5' : 'text-xs px-2.5 py-1'
      } ${current.bg}`}
    >
      <span className={`w-1.5 h-1.5 rounded-full ${current.dot}`} />
      <span>{current.label}</span>
    </span>
  );
};
