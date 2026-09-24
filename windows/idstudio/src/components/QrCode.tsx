import React, { useEffect, useState } from 'react';
import QRCode from 'qrcode';

/** The soldier's genuine credential QR: it carries only the secret code that XV gate terminals verify. */
export const CredentialQr: React.FC<{ code: string; size?: number; className?: string }> = ({ code, size = 85, className = '' }) => {
  const [svg, setSvg] = useState('');
  useEffect(() => {
    let live = true;
    if (!code) { setSvg(''); return; }
    QRCode.toString(code, { type: 'svg', margin: 0, errorCorrectionLevel: 'M', color: { dark: '#0f172a', light: '#ffffff' } })
      .then(s => { if (live) setSvg(s); });
    return () => { live = false; };
  }, [code]);
  return (
    <div className={`relative flex flex-col items-center justify-center ${className}`}>
      <div className="absolute -top-1 -left-1 w-2.5 h-2.5 border-t-2 border-l-2 border-amber-400"></div>
      <div className="absolute -top-1 -right-1 w-2.5 h-2.5 border-t-2 border-r-2 border-amber-400"></div>
      <div className="absolute -bottom-1 -left-1 w-2.5 h-2.5 border-b-2 border-l-2 border-amber-400"></div>
      <div className="absolute -bottom-1 -right-1 w-2.5 h-2.5 border-b-2 border-r-2 border-amber-400"></div>
      <div className="p-1.5 bg-white rounded shadow-md border border-slate-300/80">
        {svg
          ? <div style={{ width: size, height: size }} dangerouslySetInnerHTML={{ __html: svg.replace('<svg', '<svg width="100%" height="100%"') }} />
          : <div style={{ width: size, height: size }} className="flex items-center justify-center bg-slate-100 text-[8px] text-slate-500 font-mono text-center">NO SECRET CODE</div>}
      </div>
      <div className="mt-1 text-center">
        <span className="text-[6.5px] font-mono tracking-widest text-slate-300 font-semibold uppercase block">SECURE SCAN CODE</span>
      </div>
    </div>
  );
};
