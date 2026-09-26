import React from 'react';

/** A user-supplied watermark image, centred behind the card content at the chosen transparency and size. */
export const Watermark: React.FC<{ image: string; opacity: number; sizePct: number }> = ({ image, opacity, sizePct }) => {
  if (!image) return null;
  return (
    <div className="absolute inset-0 flex items-center justify-center pointer-events-none select-none overflow-hidden" style={{ zIndex: 1 }}>
      <img src={image} alt="" style={{ width: `${sizePct}%`, opacity, objectFit: 'contain' }} />
    </div>
  );
};
