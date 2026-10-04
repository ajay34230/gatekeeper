import React, { useEffect, useState } from 'react';
import QRCode from 'qrcode';
import { CardDesign, Soldier } from '../types';
import { baseWidth, cardSize } from '../themes';
import { usePhoto } from '../host';

/** Colour pairs offered for the modern card (main colour, second colour of the gradient). */
export const MODERN_ACCENTS: { label: string; a: string; b: string }[] = [
  { label: 'Emerald', a: '#047857', b: '#10b981' },
  { label: 'Royal blue', a: '#1d4ed8', b: '#38bdf8' },
  { label: 'Crimson', a: '#9f1239', b: '#fb7185' },
  { label: 'Gold', a: '#b45309', b: '#fbbf24' },
  { label: 'Violet', a: '#5b21b6', b: '#a78bfa' },
  { label: 'Slate', a: '#0f172a', b: '#64748b' },
];

/** Optional details that can be added to the modern card (rank, name, army no, unit and mobile are always there). */
export const MODERN_EXTRAS: { key: string; label: string; get: (s: Soldier) => string }[] = [
  { key: 'company', label: 'COMPANY', get: s => s.company },
  { key: 'platoon', label: 'PLATOON', get: s => s.platoon },
  { key: 'appointment', label: 'APPOINTMENT', get: s => s.appointment },
  { key: 'bloodGroup', label: 'BLOOD GROUP', get: s => s.bloodGroup },
  { key: 'expiry', label: 'VALID TILL', get: s => s.expiryDate },
  { key: 'dob', label: 'DATE OF BIRTH', get: s => s.dob },
];

const ModernQr: React.FC<{ code: string; size: number; ink: string }> = ({ code, size, ink }) => {
  const [svg, setSvg] = useState('');
  useEffect(() => {
    let live = true;
    if (!code) { setSvg(''); return; }
    QRCode.toString(code, { type: 'svg', margin: 0, errorCorrectionLevel: 'M', color: { dark: ink, light: '#ffffff' } }).then(s => { if (live) setSvg(s); });
    return () => { live = false; };
  }, [code, ink]);
  return svg
    ? <div style={{ width: size, height: size }} dangerouslySetInnerHTML={{ __html: svg.replace('<svg', '<svg width="100%" height="100%"') }} />
    : <div style={{ width: size, height: size, fontSize: 9 }} className="flex items-center justify-center bg-slate-100 text-slate-500 font-mono text-center">NO SECRET CODE</div>;
};

const v = (s: string) => (s && s.trim()) || '—';

/** The modern ID card: "split" = details on the front and the QR on the back; "single" = everything on one side. */
export const ModernCard: React.FC<{ side: 'front' | 'back'; soldier: Soldier; design: CardDesign; className?: string }> = ({ side, soldier, design, className = '' }) => {
  const size = cardSize(design);
  const portrait = size.portrait;
  const W = baseWidth(portrait), H = W * size.h / size.w;
  const single = design.layout === 'modernSingle';
  const pal = MODERN_ACCENTS.find(p => p.label === design.modAccent) ?? MODERN_ACCENTS[0];
  const dark = design.modTheme === 'dark';
  const bg = dark ? '#0b1220' : '#ffffff', ink = dark ? '#f1f5f9' : '#0f172a', sub = dark ? '#94a3b8' : '#64748b', panel = dark ? 'rgba(255,255,255,0.06)' : '#f1f5f9';
  const photo = usePhoto(soldier.id, soldier.photoVer);
  const cardNo = soldier.cardSerial || soldier.id;
  const grad = `linear-gradient(135deg, ${pal.a}, ${pal.b})`;
  const logoBox = (img: string, h: number) => img
    ? <img src={img} alt="" style={{ height: h, maxWidth: h * 3, objectFit: 'contain' }} />
    : null;
  const logoH = (portrait ? 30 : 38) * (design.modLogoSize / 100);
  const qr = (n: number) => (
    <div className="flex flex-col items-center">
      <div style={{ padding: 7, background: '#fff', borderRadius: 12, border: `3px solid ${pal.a}`, boxShadow: '0 6px 16px rgba(0,0,0,.18)' }}>
        <ModernQr code={soldier.secret} size={n} ink="#0f172a" />
      </div>
      <div style={{ fontSize: 9, letterSpacing: 2, color: sub, marginTop: 5, fontWeight: 700 }}>SCAN AT GATE</div>
      <div style={{ fontSize: 11, fontFamily: 'monospace', color: ink, fontWeight: 700, letterSpacing: 1 }}>{cardNo}</div>
    </div>
  );
  const extras = MODERN_EXTRAS.filter(e => design.modExtras.includes(e.key) && e.get(soldier).trim());
  const field = (label: string, value: string, big = false) => (
    <div style={{ minWidth: 0 }}>
      <div style={{ fontSize: 8, letterSpacing: 1.6, color: sub, fontWeight: 700 }}>{label}</div>
      <div style={{ fontSize: (big ? 15 : 12.5) * (portrait ? 1.15 : 1.2), color: ink, fontWeight: 700, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{value}</div>
    </div>
  );
  const photoBox = (w: number, h: number) => (
    <div style={{ width: w, height: h, borderRadius: 14, overflow: 'hidden', background: panel, border: `3px solid ${pal.a}`, flexShrink: 0 }}>
      {photo ? <img src={photo} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover', objectPosition: 'top' }} />
        : <div className="flex items-center justify-center w-full h-full" style={{ color: sub, fontSize: 9 }}>NO PHOTO</div>}
    </div>
  );
  const header = (left: string, right: string) => (
    <div style={{ background: grad, color: '#fff', padding: '8px 14px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, minHeight: 48 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, minWidth: 0 }}>
        {left && <div style={{ background: 'rgba(255,255,255,.92)', borderRadius: 8, padding: 3 }}>{logoBox(left, logoH)}</div>}
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: portrait ? 13 : 16, fontWeight: 800, letterSpacing: 1.2, lineHeight: 1.1 }}>{design.modTitle}</div>
          {design.modSubtitle && <div style={{ fontSize: 9, letterSpacing: 1.4, opacity: .9 }}>{design.modSubtitle}</div>}
        </div>
      </div>
      {right && <div style={{ background: 'rgba(255,255,255,.92)', borderRadius: 8, padding: 3 }}>{logoBox(right, logoH)}</div>}
    </div>
  );
  const footer = (
    <div style={{ background: grad, color: '#fff', fontSize: 8.5, letterSpacing: 1.3, padding: '5px 14px', display: 'flex', justifyContent: 'space-between', gap: 8 }}>
      <span>{design.modFooter}</span><span style={{ fontFamily: 'monospace' }}>{cardNo}</span>
    </div>
  );
  const details = (
    <div style={{ minWidth: 0, flex: 1, width: portrait ? '100%' : undefined, textAlign: portrait ? 'center' : 'left' }}>
      <div style={{ fontSize: 11.5, letterSpacing: 2.4, color: pal.a, fontWeight: 800 }}>{v(soldier.rank).toUpperCase()}</div>
      <div style={{ fontSize: portrait ? 23 : 27, fontWeight: 800, color: ink, lineHeight: 1.1, margin: '2px 0 8px' }}>{v(soldier.name)}</div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: portrait ? '10px 12px' : '9px 14px', justifyItems: portrait ? 'center' : 'start' }}>
        {field('ARMY NO', v(soldier.armyNo), true)}
        {field('MOBILE', v(soldier.mobile))}
        {field('UNIT', v(soldier.unit))}
        {extras.map(e => <React.Fragment key={e.key}>{field(e.label, v(e.get(soldier)))}</React.Fragment>)}
      </div>
    </div>
  );

  const frame = (children: React.ReactNode) => (
    <div className={`xv-card ${className}`} style={{ width: W, height: H, background: bg, color: ink, borderRadius: 22, overflow: 'hidden', display: 'flex', flexDirection: 'column', position: 'relative', boxShadow: '0 20px 50px rgba(0,0,0,.45)', border: `1px solid ${pal.a}55` }}>
      <div style={{ position: 'absolute', right: -50, bottom: 20, width: 190, height: 190, borderRadius: '50%', background: grad, opacity: .08 }} />
      {children}
    </div>
  );

  if (single) {
    return frame(<>
      {header(design.modLogoFront, design.modLogoBack)}
      <div style={{ flex: 1, display: 'flex', flexDirection: portrait ? 'column' : 'row', alignItems: 'center', justifyContent: 'space-evenly', gap: portrait ? 10 : 18, padding: portrait ? '12px 18px' : '10px 20px', position: 'relative' }}>
        {design.modShowPhoto && photoBox(portrait ? 120 : 124, portrait ? 150 : 156)}
        {details}
        {qr(portrait ? 140 : 118)}
      </div>
      {footer}
    </>);
  }
  if (side === 'front') {
    return frame(<>
      {header(design.modLogoFront, '')}
      <div style={{ flex: 1, display: 'flex', flexDirection: portrait ? 'column' : 'row', alignItems: portrait ? 'center' : 'center', justifyContent: 'space-evenly', gap: portrait ? 12 : 24, padding: portrait ? '16px 18px' : '12px 22px', position: 'relative' }}>
        {design.modShowPhoto && photoBox(portrait ? 150 : 140, portrait ? 188 : 178)}
        <div style={{ display: 'flex', alignItems: 'center', flex: 1, minWidth: 0, width: portrait ? '100%' : undefined }}>{details}</div>
      </div>
      {footer}
    </>);
  }
  return frame(<>
    <div style={{ background: grad, height: 10 }} />
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 8, position: 'relative' }}>
      {design.modLogoBack && <div>{logoBox(design.modLogoBack, logoH * 1.3)}</div>}
      {qr(portrait ? 190 : 150)}
      <div style={{ fontSize: 10, color: sub, textAlign: 'center', maxWidth: '80%' }}>{design.modBackNote}</div>
    </div>
    {footer}
  </>);
};
