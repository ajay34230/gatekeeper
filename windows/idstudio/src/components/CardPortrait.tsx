import React from 'react';
import { CardDesign, Soldier } from '../types';
import { CARD_THEMES, cardSize, baseWidth } from '../themes';
import { NationalCrest, CrossedSwordsBadge, SmartChip, HolographicSeal, SecurityWatermark } from './MilitaryEmblem';
import { GuillochePattern, MicroPrintBorder } from './GuillochePattern';
import { CredentialQr } from './QrCode';
import { BearerSignature } from './Signature';
import { SlotMark } from './Slot';
import { usePhoto, useSignature } from '../host';
import { mrz } from '../mrz';
import { detailColor, detailField, detailPx, detailScaleVar } from '../detailFields';
import { ShieldAlert, Phone, AlertTriangle, UserCheck, PhoneCall } from 'lucide-react';

const dash = (v: string) => (v && v.trim()) || '—';

/** Layout metrics that follow the card's real proportions (taller cards get a larger photo and QR). */
function metrics(design: CardDesign) {
  const size = cardSize(design);
  const W = baseWidth(true), H = W * size.h / size.w;
  const photoH = Math.round(Math.min(215, Math.max(110, H * (H < 650 ? 0.215 : 0.265))));
  return { W, H, photoH, photoW: Math.round(photoH / 1.267), qr: Math.round(Math.min(100, Math.max(66, H * (H < 650 ? 0.115 : 0.128)))), roomy: H > 650 };
}

/** Shared portrait card shell (neck / lanyard ID): same theme, security pattern and border as the landscape card. */
const Shell: React.FC<{ design: CardDesign; className?: string; children: React.ReactNode; watermark?: boolean; corners?: React.ReactNode }> = ({ design, className = '', children, watermark, corners }) => {
  const theme = CARD_THEMES[design.theme] ?? CARD_THEMES.army;
  const size = cardSize(design);
  const W = baseWidth(true), H = W * size.h / size.w, pxPerMm = W / size.w;
  return (
    <div className={`xv-card relative rounded-2xl overflow-hidden border-2 ${theme.cardBorder} shadow-2xl bg-gradient-to-br ${theme.bgGradient} select-none ${className}`}
      style={{ width: W, height: H, boxShadow: '0 20px 50px rgba(0, 0, 0, 0.6), inset 0 1px 0 rgba(255, 255, 255, 0.15)', ...detailScaleVar(design) }}>
      <GuillochePattern opacity={0.12} strokeColor="#94a3b8" />
      {watermark && <SecurityWatermark className="top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-64 h-64 text-slate-100" />}
      <div className="absolute top-0 right-0 w-40 h-40 bg-gradient-to-bl from-amber-300/10 via-cyan-400/5 to-transparent pointer-events-none"></div>
      {design.showSlot && <SlotMark pxPerMm={pxPerMm} />}
      {corners && <div className="absolute z-20 left-3.5 right-3.5 top-3 flex items-center justify-between">{corners}</div>}
      <div className="relative z-10 w-full h-full flex flex-col p-3.5 text-slate-100" style={{ paddingTop: design.showSlot ? 7 * pxPerMm : corners ? 40 : undefined }}>{children}</div>
    </div>
  );
};

export const PortraitFront: React.FC<{ soldier: Soldier; design: CardDesign; check: string; className?: string }> = ({ soldier, design, check, className }) => {
  const photo = usePhoto(soldier.id, soldier.photoVer);
  const signature = useSignature(soldier.id, soldier.signatureVer);
  const m = metrics(design);
  return (
    <Shell design={design} className={className} watermark corners={<>
      {design.showChip ? <SmartChip className="w-9 h-7" /> : <span />}
      {design.badge !== 'none'
        ? <div className="w-9 h-9 rounded-full bg-black/40 border border-white/20 p-1 flex items-center justify-center text-amber-400 overflow-hidden">
            {design.badge === 'custom' && design.badgeImage ? <img src={design.badgeImage} alt="" className="w-full h-full object-contain" /> : <CrossedSwordsBadge className="w-full h-full" />}
          </div>
        : <span />}
    </>}>
      {/* Header */}
      <div className="flex flex-col items-center text-center border-b border-white/15 pb-1.5">
        <div className="w-10 h-12 flex items-center justify-center text-amber-400 drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
          {design.crest === 'custom' && design.crestImage ? <img src={design.crestImage} alt="" className="w-full h-full object-contain" /> : <NationalCrest className="w-full h-full" />}
        </div>
        <span className="text-[8px] font-mono tracking-widest text-amber-300 font-bold uppercase mt-0.5">{design.govtLine}</span>
        <h1 className="text-[15px] font-extrabold tracking-wider text-white font-serif uppercase leading-tight">{design.title}</h1>
        <span className="text-[7px] font-mono tracking-wide text-slate-300">{design.formLine}</span>
      </div>

      {/* Photo + identity */}
      <div className="flex-1 flex flex-col items-center gap-1.5 py-1.5 min-h-0">
        {/* The photo takes the height that is left, so every card size fits without overlap. */}
        <div className="flex-1 min-h-[70px] w-full flex items-center justify-center">
        <div className="relative h-full max-h-[230px] aspect-[3/3.8] rounded-lg overflow-hidden border-2 border-amber-500/60 shadow-lg bg-black/50">
          {photo ? <img src={photo} alt={soldier.name} className="w-full h-full object-cover object-top" />
            : <div className="w-full h-full flex items-center justify-center text-[9px] font-mono text-slate-500 text-center px-2">AFFIX / UPLOAD PHOTO</div>}
          <div className="absolute top-1 left-1 w-2.5 h-2.5 border-t border-l border-amber-400"></div>
          <div className="absolute top-1 right-1 w-2.5 h-2.5 border-t border-r border-amber-400"></div>
          <div className="absolute bottom-1 left-1 w-2.5 h-2.5 border-b border-l border-amber-400"></div>
          <div className="absolute bottom-1 right-1 w-2.5 h-2.5 border-b border-r border-amber-400"></div>
          <div className="absolute bottom-1 right-1 bg-black/70 px-1 py-0.5 rounded text-[6px] font-mono text-amber-300 border border-amber-500/40">{design.sealText || 'OFFICIAL'}</div>
        </div>
        </div>
        <div className="text-center w-full pb-1 shrink-0">
          <div className="text-[18px] font-black tracking-wide text-white uppercase leading-tight"><span className="text-amber-300">{soldier.rank}</span> {soldier.name}</div>
          <div className="text-[10px] font-bold text-cyan-300 uppercase tracking-wide mt-0.5">{dash(soldier.appointment)}</div>
          <div className="inline-flex items-baseline gap-1.5 mt-1.5">
            <span className="text-[8px] font-mono font-bold tracking-wider text-amber-400 uppercase">ARMY NO:</span>
            <span className="text-[15px] font-mono font-extrabold tracking-widest text-white bg-black/40 px-2 rounded border border-amber-500/30">{dash(soldier.armyNo)}</span>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-1 w-full font-mono shrink-0">
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
        <div className="grid grid-cols-2 gap-1 w-full shrink-0">
          <div className="flex items-center justify-between bg-red-950/60 border border-red-500/40 rounded px-1.5 py-0.5">
            <span style={detailPx(6.5)} className="font-mono text-red-300 flex items-center gap-0.5"><ShieldAlert className="w-2.5 h-2.5 text-red-400" />BLOOD GP</span>
            <span style={{ ...detailPx(8), ...detailColor(design, '#fecaca') }} className="font-bold font-mono">{dash(soldier.bloodGroup)}</span>
          </div>
          <div className="flex items-center justify-between font-mono bg-black/30 border border-white/10 rounded px-1.5 py-0.5">
            <span style={detailPx(6.5)} className="text-slate-400">EXPIRY:</span><span style={{ ...detailPx(6.5), ...detailColor(design, '#fcd34d') }} className="font-bold">{dash(soldier.expiryDate)}</span>
          </div>
        </div>
        {design.showMobile && soldier.mobile && m.roomy && (
          <div className="flex items-center gap-1 font-mono self-start shrink-0"><Phone className="w-2.5 h-2.5 text-amber-400" /><span style={detailPx(7.5)} className="text-slate-400">MOB:</span><span style={{ ...detailPx(7.5), ...detailColor(design, '#f1f5f9') }} className="font-bold">{soldier.mobile}</span></div>
        )}
      </div>

      {/* QR + bearer signature */}
      <div className="grid grid-cols-2 gap-2 items-center bg-black/25 rounded-lg border border-white/10 p-2 relative overflow-hidden shrink-0">
        <div className="absolute -bottom-6 -right-6 pointer-events-none opacity-20"><HolographicSeal className="w-20 h-20" /></div>
        <div className="flex justify-center"><CredentialQr code={soldier.secret} size={m.qr} /></div>
        <BearerSignature imageUrl={signature} name={soldier.name} signedAt={soldier.signedAt} check={check} />
      </div>

      {/* Footer */}
      <div className="border-t border-white/15 pt-1 mt-1.5 shrink-0">
        <MicroPrintBorder text={design.microText} />
        <div className="text-center text-[6.5px] font-mono text-slate-400 pt-0.5 space-y-px">
          <div className="text-amber-400/90 font-semibold uppercase">{design.footerCenter}</div>
          <div>{design.footerLeftPrefix} {soldier.armyNo || soldier.id} • CARD REF {soldier.cardSerial || soldier.id}</div>
          <div>{design.footerRight}</div>
        </div>
      </div>
    </Shell>
  );
};

export const PortraitBack: React.FC<{ soldier: Soldier; design: CardDesign; className?: string }> = ({ soldier, design, className }) => {
  const [l1, l2, l3] = mrz(soldier, design.issuingState);
  const m = metrics(design);
  const photo = usePhoto(soldier.id, soldier.photoVer);
  const box = 'bg-black/30 border border-white/10 rounded px-1.5 py-1';
  return (
    <Shell design={design} className={className}>
      <div className="w-full bg-slate-900 border-y border-white/15 py-1 px-2 flex items-center justify-between rounded gap-2">
        <span className="flex items-center gap-1.5 min-w-0"><span className="w-2 h-2 rounded-full bg-emerald-400 shrink-0"></span>
          <span className="text-[6px] font-mono tracking-wider text-slate-300 uppercase truncate">{design.stripeText}</span></span>
        <span className="text-[6.5px] font-mono text-amber-300 font-bold shrink-0">ID: {soldier.cardSerial || soldier.id}</span>
      </div>

      <div className="flex-1 flex flex-col justify-between gap-2 py-2 min-h-0">
        {/* Identity summary with ghost photo (anti-substitution feature) */}
        <div className="flex gap-2 items-stretch">
          <div className="shrink-0 rounded-md overflow-hidden border border-white/15 bg-black/40" style={{ width: Math.round(m.photoW * 0.42), height: Math.round(m.photoH * 0.42) }}>
            {photo && <img src={photo} alt="" className="w-full h-full object-cover object-top grayscale opacity-60 mix-blend-luminosity" />}
          </div>
          <div className="flex-1 min-w-0 flex flex-col justify-center">
            <div className="text-[7px] font-mono text-slate-400 uppercase">Bearer</div>
            <div className="text-[11px] font-black text-white uppercase leading-tight truncate"><span className="text-amber-300">{soldier.rank}</span> {soldier.name}</div>
            <div className="text-[8px] font-mono text-slate-300 truncate">ARMY NO {dash(soldier.armyNo)} • {dash(soldier.unit)}</div>
          </div>
        </div>

        <div className="space-y-1">
          <div style={detailPx(8)} className="font-mono font-bold text-amber-300 uppercase flex items-center gap-1"><UserCheck className="w-3 h-3 text-amber-400" />IDENTIFICATION &amp; MEDICAL RECORD</div>
          <div className="grid grid-cols-2 gap-1 font-mono">
            {design.backFields.map(k => {
              const f = detailField(k);
              return <div key={k} className={box}><span style={detailPx(6)} className="text-slate-400 block">{f.label}</span><span style={{ ...detailPx(7.5), ...detailColor(design, '#ffffff') }} className="font-bold">{dash(f.get(soldier))}</span></div>;
            })}
          </div>
          {design.showIdMark && (
            <div className={`${box} font-mono`}><span style={detailPx(6)} className="text-slate-400 block">VISIBLE IDENTIFICATION MARKS</span><span style={{ ...detailPx(7), ...detailColor(design, '#fde68a') }}>{dash(soldier.idMark)}</span></div>
          )}
          {design.showNok && (
            <div className={`${box} font-mono`}>
              <span style={detailPx(6)} className="text-slate-400 flex items-center gap-1"><PhoneCall className="w-2.5 h-2.5 text-amber-400" />NEXT OF KIN (EMERGENCY CONTACT)</span>
              <div style={detailPx(7)} className="flex items-center justify-between font-bold pt-0.5 gap-2"><span className="truncate">{dash(soldier.nokName)}{soldier.nokRelation ? ` (${soldier.nokRelation})` : ''}</span><span className="text-amber-300 shrink-0">{soldier.nokPhone}</span></div>
            </div>
          )}
          {design.showAddress && (
            <div className={`${box} font-mono`}><span style={detailPx(6)} className="text-slate-400 block">PERMANENT ADDRESS</span><span style={{ ...detailPx(7), ...detailColor(design, '#e2e8f0') }} className="line-clamp-2">{dash(soldier.address)}</span></div>
          )}
        </div>

        <div className="space-y-0.5 text-[6.5px] font-mono leading-snug border-t border-white/10 pt-1.5">
          <div className="flex items-center gap-1 text-amber-400 font-bold uppercase text-[7.5px]"><AlertTriangle className="w-2.5 h-2.5" />{design.instructionsTitle}</div>
          <ol className="list-decimal pl-3 space-y-0.5 text-slate-400">{design.instructions.filter(i => i.trim()).map((i, n) => <li key={n}>{i}</li>)}</ol>
        </div>

        <div className="flex items-end justify-between border-t border-white/10 pt-1.5">
          <div className="flex flex-col items-center"><HolographicSeal className="w-11 h-11" label={design.sealText || 'OFFICIAL'} /><span className="text-[5.5px] font-mono text-slate-400 mt-0.5">{design.sealLabel}</span></div>
          <div className="text-right flex flex-col items-end">
            <div className="h-8 w-36 flex items-end justify-end border-b border-white/25">{design.coSignature && <img src={design.coSignature} alt="" className="h-8 max-w-[144px] object-contain" />}</div>
            <span className="text-[8px] font-bold text-white uppercase mt-0.5">{design.coName || '\u00a0'}</span>
            <span className="text-[6px] font-mono text-amber-400 uppercase">{design.coTitle}</span>
          </div>
        </div>
      </div>

      {design.showMrz && (
        <div className="bg-black/60 rounded border border-white/15 p-1 font-mono text-[7.5px] tracking-[0.12em] leading-[1.15] text-amber-200/90 text-center font-bold">
          <div>{l1}</div><div>{l2}</div><div>{l3}</div>
        </div>
      )}
      <div className="pt-1 text-center"><span className="text-[5.5px] font-mono text-slate-400 uppercase">{design.bottomNotice}</span></div>
    </Shell>
  );
};
