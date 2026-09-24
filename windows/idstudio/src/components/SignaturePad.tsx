import React, { useEffect, useRef, useState } from 'react';
import { X, RotateCcw, Check, PenTool, Upload, Image as ImageIcon } from 'lucide-react';

const INK = [224, 242, 254]; // light ink that stays readable on the dark card

/**
 * Turns an uploaded signature (scan, photo or digital signature image) into a transparent PNG:
 * the paper background becomes transparent and the ink is recoloured for the dark card.
 * Images that already have transparency keep their shape and are only recoloured.
 */
export async function prepareSignature(file: File, keepColours: boolean): Promise<string> {
  const url = URL.createObjectURL(file);
  try {
    const img = new Image();
    img.src = url;
    await img.decode();
    // Trim to a sensible resolution for a card signature.
    const scale = Math.min(1, 1200 / img.naturalWidth, 500 / img.naturalHeight);
    const w = Math.max(1, Math.round(img.naturalWidth * scale)), h = Math.max(1, Math.round(img.naturalHeight * scale));
    const c = document.createElement('canvas'); c.width = w; c.height = h;
    const ctx = c.getContext('2d')!;
    ctx.drawImage(img, 0, 0, w, h);
    const data = ctx.getImageData(0, 0, w, h);
    const px = data.data;
    // Paper brightness: the brightest 10 % of opaque pixels.
    const lum: number[] = [];
    for (let i = 0; i < px.length; i += 16) if (px[i + 3] > 200) lum.push(0.299 * px[i] + 0.587 * px[i + 1] + 0.114 * px[i + 2]);
    lum.sort((a, b) => a - b);
    const paper = lum.length ? lum[Math.floor(lum.length * 0.9)] : 255;
    let minX = w, minY = h, maxX = -1, maxY = -1;
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const i = (y * w + x) * 4;
      const l = 0.299 * px[i] + 0.587 * px[i + 1] + 0.114 * px[i + 2];
      // Ink strength: how much darker than the paper the pixel is (soft edge keeps strokes smooth).
      const ink = Math.max(0, Math.min(1, (paper - 25 - l) / 90));
      const a = Math.round(ink * px[i + 3]);
      if (!keepColours) { px[i] = INK[0]; px[i + 1] = INK[1]; px[i + 2] = INK[2]; }
      px[i + 3] = a;
      if (a > 40) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
    }
    if (maxX < 0) throw new Error('No signature strokes were found in this image.');
    ctx.putImageData(data, 0, 0);
    // Crop to the strokes with a small margin.
    const m = 8, cx = Math.max(0, minX - m), cy = Math.max(0, minY - m), cw = Math.min(w, maxX + m) - cx, ch = Math.min(h, maxY + m) - cy;
    const out = document.createElement('canvas'); out.width = cw; out.height = ch;
    out.getContext('2d')!.drawImage(c, cx, cy, cw, ch, 0, 0, cw, ch);
    return out.toDataURL('image/png');
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** Capture a real signature: draw it (mouse, pen, touch) or upload an image of it. Returns a transparent PNG. */
export const SignaturePad: React.FC<{ open: boolean; title: string; onClose: () => void; onSave: (png: string) => void }> = ({ open, title, onClose, onSave }) => {
  const ref = useRef<HTMLCanvasElement>(null);
  const drawing = useRef(false);
  const [mode, setMode] = useState<'draw' | 'upload'>('draw');
  const [hasInk, setHasInk] = useState(false);
  const [file, setFile] = useState<File | null>(null);
  const [keepColours, setKeepColours] = useState(false);
  const [uploaded, setUploaded] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (!open) return;
    setFile(null); setUploaded(''); setError(''); setHasInk(false);
  }, [open]);

  useEffect(() => {
    if (!open || mode !== 'draw') return;
    const c = ref.current; if (!c) return;
    const ctx = c.getContext('2d')!;
    ctx.clearRect(0, 0, c.width, c.height);
    ctx.strokeStyle = '#e0f2fe'; ctx.lineWidth = 3.2; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
    setHasInk(false);
  }, [open, mode]);

  useEffect(() => {
    if (!file) return;
    let live = true;
    setError('');
    prepareSignature(file, keepColours).then(u => { if (live) setUploaded(u); }).catch(e => { if (live) { setUploaded(''); setError(e?.message || 'This file could not be read as an image.'); } });
    return () => { live = false; };
  }, [file, keepColours]);

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
  const canSave = mode === 'draw' ? hasInk : !!uploaded;
  const save = () => { onSave(mode === 'draw' ? ref.current!.toDataURL('image/png') : uploaded); onClose(); };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 no-print">
      <div className="w-full max-w-xl bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl overflow-hidden">
        <div className="flex items-center justify-between px-5 py-3 border-b border-slate-800">
          <div className="flex items-center gap-2 text-amber-300 font-bold"><PenTool className="w-4 h-4" />{title}</div>
          <button onClick={onClose} className="text-slate-400 hover:text-white"><X className="w-5 h-5" /></button>
        </div>
        <div className="p-5">
          <div className="grid grid-cols-2 gap-1 p-1 rounded-xl bg-slate-950 border border-slate-800 mb-4">
            <button onClick={() => setMode('draw')} className={`flex items-center justify-center gap-2 py-2 rounded-lg text-sm ${mode === 'draw' ? 'bg-amber-500/15 text-amber-300 border border-amber-500/50' : 'text-slate-400'}`}><PenTool className="w-4 h-4" />Draw signature</button>
            <button onClick={() => setMode('upload')} className={`flex items-center justify-center gap-2 py-2 rounded-lg text-sm ${mode === 'upload' ? 'bg-amber-500/15 text-amber-300 border border-amber-500/50' : 'text-slate-400'}`}><Upload className="w-4 h-4" />Upload signature image</button>
          </div>

          {mode === 'draw' ? (
            <>
              <p className="text-xs text-slate-400 font-mono mb-3">Sign inside the box with a mouse, pen or finger.</p>
              <canvas ref={ref} width={900} height={300} onPointerDown={down} onPointerMove={move} onPointerUp={() => (drawing.current = false)} onPointerLeave={() => (drawing.current = false)}
                className="w-full h-44 rounded-xl border-2 border-dashed border-slate-600 bg-slate-950 touch-none cursor-crosshair" />
            </>
          ) : (
            <>
              <p className="text-xs text-slate-400 font-mono mb-3">A scan or photo of the signature on white paper, or a digital signature image (PNG / JPG). The paper background is removed automatically.</p>
              <label className="flex flex-col items-center justify-center gap-2 w-full h-44 rounded-xl border-2 border-dashed border-slate-600 bg-slate-950 cursor-pointer hover:border-amber-500/60">
                {uploaded
                  ? <img src={uploaded} alt="Signature preview" className="max-h-36 max-w-[90%] object-contain" />
                  : <><ImageIcon className="w-7 h-7 text-slate-500" /><span className="text-sm text-slate-400">{file ? 'Processing…' : 'Choose signature image…'}</span></>}
                <input type="file" accept="image/png,image/jpeg,image/bmp,image/webp" className="hidden" onChange={e => { const f = e.target.files?.[0]; if (f) setFile(f); e.target.value = ''; }} />
              </label>
              <label className="flex items-center gap-2 mt-3 text-sm text-slate-300 cursor-pointer">
                <input type="checkbox" checked={keepColours} onChange={e => setKeepColours(e.target.checked)} className="accent-amber-500 w-4 h-4" />
                Keep the original ink colour (otherwise light ink for the dark card)
              </label>
              {error && <p className="text-sm text-rose-300 mt-2">{error}</p>}
            </>
          )}

          <div className="flex justify-between mt-4">
            {mode === 'draw'
              ? <button onClick={clear} className="flex items-center gap-1.5 px-3 py-2 rounded-lg border border-slate-700 text-slate-300 text-sm hover:bg-slate-800"><RotateCcw className="w-4 h-4" />Clear</button>
              : <span className="text-xs text-slate-500 font-mono self-center">Stored encrypted on this PC</span>}
            <button disabled={!canSave} onClick={save}
              className="flex items-center gap-1.5 px-4 py-2 rounded-lg bg-amber-500 text-slate-950 font-bold text-sm disabled:opacity-40"><Check className="w-4 h-4" />Save signature</button>
          </div>
        </div>
      </div>
    </div>
  );
};
