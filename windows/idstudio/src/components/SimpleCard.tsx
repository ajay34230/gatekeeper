import React, { useEffect, useState } from 'react';
import QRCode from 'qrcode';
import { CardDesign, Soldier } from '../types';
import { baseWidth, cardSize } from '../themes';

const SimpleQr: React.FC<{ code: string; size: number }> = ({ code, size }) => {
  const [svg, setSvg] = useState('');
  useEffect(() => {
    let live = true;
    if (!code) { setSvg(''); return; }
    QRCode.toString(code, { type: 'svg', margin: 1, errorCorrectionLevel: 'H', color: { dark: '#000000', light: '#ffffff' } }).then(s => { if (live) setSvg(s); });
    return () => { live = false; };
  }, [code]);
  return svg
    ? <div style={{ width: size, height: size }} dangerouslySetInnerHTML={{ __html: svg.replace('<svg', '<svg width="100%" height="100%"') }} />
    : <div style={{ width: size, height: size, fontSize: 9 }} className="flex items-center justify-center bg-slate-100 text-slate-500 font-mono text-center">NO CODE</div>;
};

const v = (s: string) => (s && s.trim()) || '—';

export const SimpleCard: React.FC<{ side: 'front' | 'back'; soldier: Soldier; design: CardDesign; className?: string }> = ({ side, soldier, design, className = '' }) => {
  const size = cardSize(design);
  const portrait = size.portrait;
  const W = baseWidth(portrait), H = W * size.h / size.w;

  const frame = (children: React.ReactNode) => (
    <div className={`xv-card ${className}`} style={{ width: W, height: H, background: '#ffffff', color: '#000000', borderRadius: 12, overflow: 'hidden', display: 'flex', flexDirection: 'column', position: 'relative', boxShadow: '0 8px 24px rgba(0,0,0,.3)', border: '1px solid #e5e7eb' }}>
      {children}
    </div>
  );

  if (side === 'front') {
    return frame(
      <div style={{ flex: 1, display: 'flex', flexDirection: portrait ? 'column' : 'row', alignItems: 'center', justifyContent: 'center', gap: portrait ? 16 : 24, padding: portrait ? '20px 16px' : '16px 24px', position: 'relative' }}>
        {/* Logo area */}
        <div style={{ textAlign: 'center' }}>
          <div style={{ fontSize: portrait ? 28 : 32, fontWeight: 800, color: '#1f2937', marginBottom: 8 }}>XV</div>
          <div style={{ fontSize: 9, letterSpacing: 1.2, color: '#6b7280', fontWeight: 600 }}>COMMAND CENTER</div>
        </div>

        {/* Soldier info */}
        <div style={{ flex: 1, minWidth: 0, textAlign: portrait ? 'center' : 'left' }}>
          <div style={{ fontSize: 10, letterSpacing: 1, color: '#6b7280', fontWeight: 700, marginBottom: 4 }}>{v(soldier.rank).toUpperCase()}</div>
          <div style={{ fontSize: portrait ? 18 : 20, fontWeight: 800, color: '#111827', marginBottom: 12, lineHeight: 1.2 }}>{v(soldier.name)}</div>

          <div style={{ display: 'grid', gridTemplateColumns: portrait ? '1fr' : '1fr 1fr', gap: portrait ? '8px' : '8px 16px', fontSize: 11, color: '#374151', lineHeight: 1.4 }}>
            <div>
              <div style={{ fontSize: 8, color: '#9ca3af', fontWeight: 700, letterSpacing: 0.5 }}>ARMY NO</div>
              <div style={{ fontWeight: 700, fontFamily: 'monospace' }}>{v(soldier.armyNo)}</div>
            </div>
            <div>
              <div style={{ fontSize: 8, color: '#9ca3af', fontWeight: 700, letterSpacing: 0.5 }}>COMPANY</div>
              <div style={{ fontWeight: 700 }}>{v(soldier.company)}</div>
            </div>
            <div>
              <div style={{ fontSize: 8, color: '#9ca3af', fontWeight: 700, letterSpacing: 0.5 }}>UNIT</div>
              <div style={{ fontWeight: 700 }}>{v(soldier.unit)}</div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // Back side: Big QR code
  return frame(
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 12, padding: '20px 16px', position: 'relative' }}>
      <div style={{ padding: 12, background: '#ffffff', borderRadius: 8, border: '2px solid #1f2937' }}>
        <SimpleQr code={soldier.secret} size={portrait ? 200 : 160} />
      </div>
      <div style={{ fontSize: 10, letterSpacing: 2, color: '#4b5563', fontWeight: 700 }}>SCAN AT GATE</div>
      <div style={{ fontSize: 9, fontFamily: 'monospace', color: '#1f2937', fontWeight: 700, letterSpacing: 1 }}>{soldier.cardSerial || soldier.id}</div>
    </div>
  );
};
