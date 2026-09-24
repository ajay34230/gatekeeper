import React from 'react';
import { CardDesign, Soldier } from '../types';
import { CARD_THEMES, cardSize, baseWidth } from '../themes';
import { NationalCrest, CrossedSwordsBadge, SmartChip, HolographicSeal, SecurityWatermark } from './MilitaryEmblem';
import { GuillochePattern, MicroPrintBorder } from './GuillochePattern';
import { CredentialQr } from './QrCode';
import { BearerSignature } from './Signature';
import { SlotMark } from './Slot';
import { photoUrl, signatureUrl } from '../host';
import { mrz } from '../mrz';
import { ShieldAlert, Phone, AlertTriangle, UserCheck, PhoneCall } from 'lucide-react';

const dash = (v: string) => (v && v.trim()) || '—';

/** Shared portrait card shell (neck / lanyard ID): same theme, security pattern and border as the landscape card. */
const Shell: React.FC<{ design: CardDesign; className?: string; children: React.ReactNode; watermark?: boolean }> = ({ design, className = '', children, watermark }) => {
  const theme = CARD_THEMES[design.theme] ?? CARD_THEMES.army;
  const size = cardSize(design);
  const W = baseWidth(true), H = W * size.h / size.w, pxPerMm = W / size.w;
  return (
    <div className={`xv-card relative rounded-2xl overflow-hidden border-2 ${theme.cardBorder} shadow-2xl bg-gradient-to-br ${theme.bgGradient} select-none ${className}`}
      style={{ width: W, height: H, boxShadow: '0 20px 50px rgba(0, 0, 0, 0.6), inset 0 1px 0 rgba(255, 255, 255, 0.15)' }}>
      <GuillochePattern opacity={0.12} strokeColor="#94a3b8" />
      {watermark && <SecurityWatermark className="top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-64 h-64 text-slate-100" />}
      <div className="absolute top-0 right-0 w-40 h-40 bg-gradient-to-bl from-amber-300/10 via-cyan-400/5 to-transparent pointer-events-none"></div>
      {design.showSlot && <SlotMark pxPerMm={pxPerMm} />}
      <div className="relative z-10 w-full h-full flex flex-col p-3.5 text-slate-100" style={{ paddingTop: design.showSlot ? 7.5 * pxPerMm : undefined }}>{children}</div>
    </div>
  );
};

export const PortraitFront: React.FC<{ soldier: Soldier; design: CardDesign; check: string; className?: string }> = ({ soldier, design, check, className }) => {
  const photo = photoUrl(soldier.id, soldier.photoVer);
  return (
    <Shell design={design} className={className} watermark>
      {/* Header */}
      <div className="flex flex-col items-center text-center border-b border-white/15 pb-2">
        <div className="flex items-center justify-between w-full">
          {design.showChip ? <SmartChip className="w-8 h-6" /> : <span className="w-8" />}
          <div className="w-10 h-12 flex items-center justify-center text-amber-400 drop-shadow-[0_2px_4px_rgba(0,0,0,0.5)]">
            {design.crest === 'custom' && design.crestImage ? <img src={design.crestImage} alt="" className="w-full h-full object-contain" /> : <NationalCrest className="w-full h-full" />}
          </div>
          {design.badge !== 'none'
            ? <div className="w-8 h-8 rounded-full bg-black/40 border border-white/20 p-1 flex items-center justify-center text-amber-400 overflow-hidden">
                {design.badge === 'custom' && design.badgeImage ? <img src={design.badgeImage} alt="" className="w-full h-full object-contain" /> : <CrossedSwordsBadge className="w-full h-full" />}
              </div>
            : <span className="w-8" />}
        </div>
        <span className="text-[8px] font-mono tracking-widest text-amber-300 font-bold uppercase mt-1">{design.govtLine}</span>
        <h1 className="text-[15px] font-extrabold tracking-wider text-white font-serif uppercase leading-tight">{design.title}</h1>
        <span className="text-[7px] font-mono tracking-wide text-slate-300">{design.formLine}</span>
      </div>

      {/* Photo + identity */}
      <div className="flex-1 flex flex-col items-center justify-evenly py-1.5 min-h-0">
        <div className="relative w-[150px] aspect-[3/3.8] rounded-lg overflow-hidden border-2 border-amber-500/60 shadow-lg bg-black/50">
          {photo ? <img src={photo} alt={soldier.name} className="w-full h-full object-cover object-top" />
            : <div className="w-full h-full flex items-center justify-center text-[9px] font-mono text-slate-500 text-center px-2">AFFIX / UPLOAD PHOTO</div>}
          <div className="absolute top-1 left-1 w-2.5 h-2.5 border-t border-l border-amber-400"></div>
          <div className="absolute top-1 right-1 w-2.5 h-2.5 border-t border-r border-amber-400"></div>
          <div className="absolute bottom-1 left-1 w-2.5 h-2.5 border-b border-l border-amber-400"></div>
          <div className="absolute bottom-1 right-1 w-2.5 h-2.5 border-b border-r border-amber-400"></div>
          <div className="absolute bottom-1 right-1 bg-black/70 px-1 py-0.5 rounded text-[6px] font-mono text-amber-300 border border-amber-500/40">{design.sealText || 'OFFICIAL'}</div>
        </div>
        <div className="text-center w-full pb-1">
          <div className="text-[18px] font-black tracking-wide text-white uppercase leading-tight"><span className="text-amber-300">{soldier.rank}</span> {soldier.name}</div>
          <div className="text-[10px] font-bold text-cyan-300 uppercase tracking-wide mt-0.5">{dash(soldier.appointment)}</div>
          <div className="inline-flex items-baseline gap-1.5 mt-1.5">
            <span className="text-[8px] font-mono font-bold tracking-wider text-amber-400 uppercase">ARMY NO:</span>
            <span className="text-[15px] font-mono font-extrabold tracking-widest text-white bg-black/40 px-2 rounded border border-amber-500/30">{dash(soldier.armyNo)}</span>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-1 w-full text-[7.5px] font-mono">
          {([['UNIT', soldier.unit], ['COMPANY (COY)', soldier.company], ['PLATOON', soldier.platoon], ['SECTION (SEC)', soldier.section]] as const).map(([k, v]) => (
            <div key={k} className="bg-black/30 border border-white/10 rounded px-1.5 py-0.5">
              <span className="text-slate-400 block text-[6px]">{k}</span><span className="font-bold text-white uppercase truncate block">{dash(v)}</span>
            </div>
          ))}
        </div>
        <div className="grid grid-cols-2 gap-1 w-full">
          <div className="flex items-center justify-between bg-red-950/60 border border-red-500/40 rounded px-1.5 py-0.5">
            <span className="text-[6.5px] font-mono text-red-300 flex items-center gap-0.5"><ShieldAlert className="w-2.5 h-2.5 text-red-400" />BLOOD GP</span>
            <span className="text-[8px] font-bold text-red-100 font-mono">{dash(soldier.bloodGroup)}</span>
          </div>
          <div className="flex items-center justify-between text-[6.5px] font-mono bg-black/30 border border-white/10 rounded px-1.5 py-0.5">
            <span className="text-slate-400">EXPIRY:</span><span className="font-bold text-amber-300">{dash(soldier.expiryDate)}</span>
          </div>
        </div>
        {design.showMobile && soldier.mobile && (
          <div className="flex items-center gap-1 font-mono text-[7.5px] self-start"><Phone className="w-2.5 h-2.5 text-amber-400" /><span className="text-slate-400">MOB:</span><span className="font-bold">{soldier.mobile}</span></div>
        )}
      </div>

      {/* QR + bearer signature */}
      <div className="grid grid-cols-2 gap-2 items-center bg-black/25 rounded-lg border border-white/10 p-1.5 relative overflow-hidden">
        <div className="absolute -bottom-6 -right-6 pointer-events-none opacity-20"><HolographicSeal className="w-20 h-20" /></div>
        <div className="flex justify-center"><CredentialQr code={soldier.secret} size={92} /></div>
        <BearerSignature imageUrl={signatureUrl(soldier.id, soldier.signatureVer)} name={soldier.name} signedAt={soldier.signedAt} check={check} />
      </div>

      {/* Footer */}
      <div className="border-t border-white/15 pt-1 mt-1.5">
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
  return (
    <Shell design={design} className={className}>
      <div className="w-full bg-slate-900 border-y border-white/15 py-1 px-2 flex items-center justify-between rounded gap-2">
        <span className="flex items-center gap-1.5 min-w-0"><span className="w-2 h-2 rounded-full bg-emerald-400 shrink-0"></span>
          <span className="text-[6px] font-mono tracking-wider text-slate-300 uppercase truncate">{design.stripeText}</span></span>
        <span className="text-[6.5px] font-mono text-amber-300 font-bold shrink-0">ID: {soldier.cardSerial || soldier.id}</span>
      </div>

      <div className="flex-1 flex flex-col justify-evenly gap-1.5 py-1.5 min-h-0">
        <div className="space-y-1">
          <div className="text-[8px] font-mono font-bold text-amber-300 uppercase flex items-center gap-1"><UserCheck className="w-3 h-3 text-amber-400" />IDENTIFICATION &amp; MEDICAL RECORD</div>
          <div className="grid grid-cols-2 gap-1 text-[7.5px] font-mono">
            <div className="bg-black/30 border border-white/10 rounded p-1"><span className="text-slate-400 block text-[6px]">DATE OF BIRTH</span><span className="font-bold text-white">{dash(soldier.dob)}</span></div>
            <div className="bg-black/30 border border-white/10 rounded p-1"><span className="text-slate-400 block text-[6px]">DATE OF ENROLMENT</span><span className="font-bold text-white">{dash(soldier.enrolDate)}</span></div>
          </div>
          <div className="bg-black/30 border border-white/10 rounded p-1 text-[7px] font-mono"><span className="text-slate-400 block text-[6px]">VISIBLE IDENTIFICATION MARKS:</span><span className="text-amber-200">{dash(soldier.idMark)}</span></div>
          <div className="bg-black/30 border border-white/10 rounded p-1 text-[7px] font-mono">
            <span className="text-slate-400 text-[6px] flex items-center gap-1"><PhoneCall className="w-2.5 h-2.5 text-amber-400" />NEXT OF KIN (EMERGENCY CONTACT):</span>
            <div className="flex items-center justify-between font-bold pt-0.5 gap-2"><span>{dash(soldier.nokName)}{soldier.nokRelation ? ` (${soldier.nokRelation})` : ''}</span><span className="text-amber-300">{soldier.nokPhone}</span></div>
          </div>
          {design.showAddress && soldier.address && (
            <div className="bg-black/30 border border-white/10 rounded p-1 text-[7px] font-mono"><span className="text-slate-400 block text-[6px]">PERMANENT ADDRESS:</span><span className="text-slate-200">{soldier.address}</span></div>
          )}
        </div>

        <div className="space-y-0.5 text-[6.5px] font-mono leading-tight">
          <div className="flex items-center gap-1 text-amber-400 font-bold uppercase text-[7.5px]"><AlertTriangle className="w-2.5 h-2.5" />{design.instructionsTitle}</div>
          <ol className="list-decimal pl-3 space-y-0.5 text-slate-400">{design.instructions.filter(i => i.trim()).map((i, n) => <li key={n}>{i}</li>)}</ol>
        </div>

        <div className="flex items-end justify-between pt-1 border-t border-white/10">
          <div className="flex flex-col items-center"><HolographicSeal className="w-11 h-11" label={design.sealText || 'OFFICIAL'} /><span className="text-[5.5px] font-mono text-slate-400 mt-0.5">{design.sealLabel}</span></div>
          <div className="text-right flex flex-col items-end">
            <div className="h-8 w-32 flex items-center justify-end border-b border-white/20">{design.coSignature && <img src={design.coSignature} alt="" className="h-8 max-w-[128px] object-contain" />}</div>
            <span className="text-[8px] font-bold text-white uppercase mt-0.5">{design.coName || ' '}</span>
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
