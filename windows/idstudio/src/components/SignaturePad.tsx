import React, { useEffect, useRef, useState } from 'react';
import { X, RotateCcw, Check, PenTool } from 'lucide-react';

/** Draw a real signature with mouse, pen or touch. Returns a transparent PNG (light ink for the dark card). */
export const SignaturePad: React.FC<{ open: boolean; title: string; onClose: () => void; onSave: (png: string) => void }> = ({ open, title, onClose, onSave }) => {
  const ref = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const [hasInk, setHasInk] = useState(false);

  useEffect(() => {
    if (!open) return;
    const c = ref.current; if (!c) return;
    const ctx = c.getContext('2d')!;
    ctx.clearRect(0, 0, c.width, c.height);
    ctx.strokeStyle = '#e0f2fe'; ctx.lineWidth = 3.2; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    setHasInk(false);
  }, [open]);

  if (!open) return null;

  const pos = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const r = e.currentTarget.getBoundingClientRect();
    return [(e.clientX - r.left) * (e.currentTarget.width / r.width), (e.clientY - r.top) * (e.currentTarget.height / r.height)];
  };
  const down = (e: React.PointerEvent<HTMLCanvasElement>) => {
    e.currentTarget.setPointerCapture(e.pointerId);
    drawing.current = true;
    const ctx = e.currentTarget.getContext('2d')!; const [x, y] = pos(e);
    ctx.beginPath(); ctx.moveTo(x, y);
  };
  const move = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return;
    const ctx = e.currentTarget.getContext('2d')!; const [x, y] = pos(e);
    ctx.lineTo(x, y); ctx.stroke(); setHasInk(true);
  };
  const clear = () => { const c = ref.current!; c.getContext('2d')!.clearRect(0, 0, c.width, c.height); setHasInk(false); };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 no-print">
      <div className="w-full max-w-xl bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl overflow-hidden">
        <div className="flex items-center justify-between px-5 py-3 border-b border-slate-800">
          <div className="flex items-center gap-2 text-amber-300 font-bold"><PenTool className="w-4 h-4" />{title}</div>
          <button onClick={onClose} className="text-slate-400 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <div className="p-5">
          <p className="text-xs text-slate-400 font-mono mb-3">Sign inside the box with a mouse, pen or finger. The signature is stored encrypted on this PC.</p>
          <canvas ref={ref} width={900} height={300} onPointerDown={down} onPointerMove={move} onPointerUp={() => (drawing.current = false)} onPointerLeave={() => (drawing.current = false)}
            className="w-full h-44 rounded-xl border-2 border-dashed border-slate-600 bg-slate-950 touch-none cursor-crosshair" />
          <div className="flex justify-between mt-4">
            <button onClick={clear} className="flex items-center gap-1.5 px-3 py-2 rounded-lg border border-slate-700 text-slate-300 text-sm hover:bg-slate-800"><RotateCcw className="w-4 h-4" />Clear</button>
            <button disabled={!hasInk} onClick={() => { onSave(ref.current!.toDataURL('image/png')); onClose(); }}
              className="flex items-center gap-1.5 px-4 py-2 rounded-lg bg-amber-500 text-slate-950 font-bold text-sm disabled:opacity-40"><Check className="w-4 h-4" />Save signature</button>
          </div>
        </div>
      </div>
    </div>
  );
};
