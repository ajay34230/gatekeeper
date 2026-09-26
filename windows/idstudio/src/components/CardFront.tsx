import React from 'react';
import { CardDesign, Soldier } from '../types';
import { CARD_THEMES, cardSize, baseWidth } from '../themes';
import { SlotMark } from './Slot';
import { NationalCrest, CrossedSwordsBadge, SmartChip, HolographicSeal, SecurityWatermark } from './MilitaryEmblem';
import { GuillochePattern, MicroPrintBorder } from './GuillochePattern';
import { CredentialQr } from './QrCode';
import { BearerSignature } from './Signature';
import { usePhoto, useSignature } from '../host';
import { detailColor, detailField, detailPx, detailScaleVar } from '../detailFields';
import { Phone, MapPin, ShieldAlert } from 'lucide-react';

const dash = (v: string) => (v && v.trim()) || '—';

/** Card front — same layout as the reference design, filled only with the soldier's real register details. */
export const CardFront: React.FC<{ soldier: Soldier; design: CardDesign; check: string; className?: string }> = ({ soldier, design, check, className = '' }) => {
  const theme = CARD_THEMES[design.theme] ?? CARD_THEMES.army;
  const size = cardSize(design);
  const pxPerMm = baseWidth(false) / size.w;
  const photo = usePhoto(soldier.id, soldier.photoVer);
  const signature = useSignature(soldier.id, soldier.signatureVer);
  return (
    <div
      className={`xv-card relative rounded-2xl overflow-hidden border-2 ${theme.cardBorder} shadow-2xl bg-gradient-to-br ${theme.bgGradient} select-none ${className}`}
      style={{ width: 700, height: 700 * size.h / size.w, boxShadow: '0 20px 50px rgba(0, 0, 0, 0.6), inset 0 1px 0 rgba(255, 255, 255, 0.15)', ...detailScaleVar(design) }}
    >
      <GuillochePattern opacity={0.12} strokeColor="#94a3b8" />
      <SecurityWatermark className="top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-64 h-64 text-slate-100" />
      <div className="absolute top-0 right-0 w-48 h-48 bg-gradient-to-bl from-amber-300/10 via-cyan-400/5 to-transparent pointer-events-none"></div>

      {design.showSlot && <SlotMark pxPerMm={pxPerMm} />}
      <div className="relative z-10 w-full h-full flex flex-col justify-between p-3.5 text-slate-100" style={design.showSlot ? { paddingTop: 7.5 * pxPerMm } : undefined}>
        {/* Header */}
        <div className="flex items-center justify-between border-b border-white/15 pb-1.5 gap-2">
          <div className="flex items-center gap-2">
            <div className="w-9 h-11 flex items-center justify-center text-amber-400 drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
              {design.crest === 'custom' && design.crestImage
                ? <img src={design.crestImage} alt="" className="w-full h-full object-contain" />
                : <NationalCrest className="w-full h-full" />}
            </div>
            <div className="flex flex-col">
              <span className="text-[8px] font-mono tracking-widest text-amber-300 font-bold uppercase">{design.govtLine}</span>
              <h1 className="text-sm font-extrabold tracking-wider text-white font-serif uppercase drop-shadow-sm leading-tight">{design.title}</h1>
              <span className="text-[7.5px] font-mono tracking-wide text-slate-300">{design.formLine}</span>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className="flex flex-col items-end text-right">
              <span className="text-[6.5px] font-mono text-slate-400">CARD REF NO.</span>
              <span className="text-[8px] font-mono font-bold text-amber-300 tracking-wider">{dash(soldier.cardSerial || soldier.id)}</span>
            </div>
            {design.showChip && <SmartChip className="w-8 h-6" />}
            {design.badge !== 'none' && (
              <div className="w-8 h-8 rounded-full bg-black/40 border border-white/20 p-1 flex items-center justify-center text-amber-400 overflow-hidden">
                {design.badge === 'custom' && design.badgeImage
                  ? <img src={design.badgeImage} alt="" className="w-full h-full object-contain" />
                  : <CrossedSwordsBadge className="w-full h-full" />}
              </div>
            )}
          </div>
        </div>

        {/* Body */}
        <div className="grid grid-cols-12 gap-3 flex-1 py-2 items-center min-h-0">
          {/* Photo, blood group, expiry */}
          <div className="col-span-3 flex flex-col justify-between items-center">
            <div className="relative w-full aspect-[3/3.8] max-w-[125px] rounded-lg overflow-hidden border-2 border-amber-500/60 shadow-lg bg-black/50">
              {photo
                ? <img src={photo} alt={soldier.name} className="w-full h-full object-cover object-top" />
                : <div className="w-full h-full flex items-center justify-center text-[8px] font-mono text-slate-500 text-center px-2">AFFIX / UPLOAD PHOTO</div>}
              <div className="absolute top-1 left-1 w-2 h-2 border-t border-l border-amber-400"></div>
              <div className="absolute top-1 right-1 w-2 h-2 border-t border-r border-amber-400"></div>
              <div className="absolute bottom-1 left-1 w-2 h-2 border-b border-l border-amber-400"></div>
              <div className="absolute bottom-1 right-1 w-2 h-2 border-b border-r border-amber-400"></div>
              <div className="absolute bottom-1 right-1 bg-black/70 px-1 py-0.5 rounded text-[5.5px] font-mono text-amber-300 border border-amber-500/40">{design.sealText || 'OFFICIAL'}</div>
            </div>
            <div className="w-full mt-1.5 space-y-1">
              <div className="flex items-center justify-between bg-red-950/60 border border-red-500/40 rounded px-1.5 py-0.5">
                <span style={detailPx(6.5)} className="font-mono font-medium text-red-300 flex items-center gap-0.5"><ShieldAlert className="w-2.5 h-2.5 text-red-400" />BLOOD GP</span>
                <span style={{ ...detailPx(8), ...detailColor(design, '#fecaca') }} className="font-bold font-mono">{dash(soldier.bloodGroup)}</span>
              </div>
              <div className="flex items-center justify-between text-slate-300 bg-black/30 border border-white/10 rounded px-1.5 py-0.5">
                <span style={detailPx(6.5)} className="font-mono text-slate-400">EXPIRY:</span>
                <span style={{ ...detailPx(6.5), ...detailColor(design, '#fcd34d') }} className="font-mono font-bold">{dash(soldier.expiryDate)}</span>
              </div>
            </div>
          </div>

          {/* Identity & hierarchy */}
          <div className="col-span-6 flex flex-col justify-between pl-1">
            <div className="space-y-0.5 border-b border-white/10 pb-1.5">
              <div className="flex items-baseline gap-1.5">
                <span className="text-[8px] font-mono font-bold tracking-wider text-amber-400 uppercase">ARMY NO:</span>
                <span className="text-sm font-mono font-extrabold tracking-widest text-white bg-black/40 px-1.5 rounded border border-amber-500/30">{dash(soldier.armyNo)}</span>
              </div>
              <div className="pt-0.5">
                <span className="text-[7.5px] font-mono text-emerald-300 font-semibold uppercase">RANK &amp; NAME:</span>
                <div className="text-sm font-black tracking-wide text-white uppercase font-sans leading-tight">
                  <span className="text-amber-300 mr-1">{soldier.rank}</span><span>{soldier.name}</span>
                </div>
              </div>
              <div className="flex items-baseline gap-1">
                <span className="text-[7px] font-mono text-slate-400 uppercase">APPOINTMENT:</span>
                <span className="text-[9.5px] font-bold text-cyan-300 uppercase tracking-wide">{dash(soldier.appointment)}</span>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-1 py-1 font-mono">
              {design.frontFields.map(k => {
                const f = detailField(k);
                return (
                  <div key={k} className="bg-black/30 border border-white/10 rounded px-1.5 py-0.5">
                    <span style={detailPx(6)} className="text-slate-400 block">{f.label}</span>
                    <span style={{ ...detailPx(7.5), ...detailColor(design, '#ffffff') }} className="font-bold uppercase truncate block">{dash(f.get(soldier))}</span>
                  </div>
                );
              })}
            </div>
            <div className="space-y-0.5 pt-0.5">
              {design.showMobile && (
                <div className="flex items-center gap-1 font-mono">
                  <Phone className="w-2.5 h-2.5 text-amber-400 shrink-0" /><span style={detailPx(7.5)} className="text-slate-400">MOB:</span>
                  <span style={{ ...detailPx(7.5), ...detailColor(design, '#f1f5f9') }} className="font-bold">{dash(soldier.mobile)}</span>
                </div>
              )}
              {design.showAddress && (
                <div className="flex items-start gap-1 font-mono leading-tight">
                  <MapPin className="w-2.5 h-2.5 text-amber-400 shrink-0 mt-0.5" /><span style={detailPx(7.5)} className="text-slate-400 shrink-0">ADDR:</span>
                  <span style={{ ...detailPx(7.5), ...detailColor(design, '#cbd5e1') }} className="line-clamp-2">{dash(soldier.address)}</span>
                </div>
              )}
            </div>
          </div>

          {/* QR + bearer signature */}
          <div className="col-span-3 flex flex-col justify-between items-center bg-black/25 rounded-lg border border-white/10 p-1.5 relative overflow-hidden">
            <div className="absolute -bottom-6 -right-6 pointer-events-none opacity-20"><HolographicSeal className="w-20 h-20" /></div>
            <div className="w-full flex flex-col items-center"><CredentialQr code={soldier.secret} size={85} className="w-full max-w-[95px]" /></div>
            <div className="w-full mt-1">
              <BearerSignature imageUrl={signature} name={soldier.name} signedAt={soldier.signedAt} check={check} />
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="border-t border-white/15 pt-1 mt-auto">
          <MicroPrintBorder text={design.microText} />
          <div className="flex items-center justify-between text-[6.5px] font-mono text-slate-400 pt-0.5 px-0.5 gap-2">
            <span>{design.footerLeftPrefix} {soldier.armyNo || soldier.id}</span>
            <span className="text-amber-400/90 font-semibold uppercase">{design.footerCenter}</span>
            <span>{design.footerRight}</span>
          </div>
        </div>
      </div>
    </div>
  );
};
