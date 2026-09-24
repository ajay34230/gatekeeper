import React from 'react';

/** Lanyard / clip slot guide (13 × 3 mm, centred 3 mm from the top edge) drawn at the card's real scale. */
export const SlotMark: React.FC<{ pxPerMm: number }> = ({ pxPerMm }) => (
  <div className="absolute left-1/2 -translate-x-1/2 z-20 rounded-full border border-white/50 bg-black/60 pointer-events-none"
    style={{ top: 3 * pxPerMm, width: 13 * pxPerMm, height: 3 * pxPerMm, boxShadow: 'inset 0 1px 2px rgba(0,0,0,.8)' }} />
);
