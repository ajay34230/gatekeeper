import React from 'react';
import { CardDesign, Soldier } from '../types';
import { CARD_THEMES, cardSize, baseWidth } from '../themes';
import { SlotMark } from './Slot';
import { HolographicSeal } from './MilitaryEmblem';
import { GuillochePattern } from './GuillochePattern';
import { mrz } from '../mrz';
import { AlertTriangle, UserCheck, PhoneCall } from 'lucide-react';

const dash = (v: string) => (v && v.trim()) || '—';

/** Card back — records, next of kin, conditions, issuing authority and a machine-readable zone computed from real data. */
export const CardBack: React.FC<{ soldier: Soldier; design: CardDesign; className?: string }> = ({ soldier, design, className = '' }) => {
  const theme = CARD_THEMES[design.theme] ?? CARD_THEMES.army;
  const size = cardSize(design);
  const pxPerMm = baseWidth(false) / size.w;
  const [l1, l2, l3] = mrz(soldier, design.issuingState);
  return (
    <div
      className={`xv-card relative rounded-2xl overflow-hidden border-2 ${theme.cardBorder} shadow-2xl bg-gradient-to-br ${theme.bgGradient} select-none ${className}`}
      style={{ width: 700, height: 700 * size.h / size.w, boxShadow: '0 20px 50px rgba(0, 0, 0, 0.6), inset 0 1px 0 rgba(255, 255, 255, 0.15)' }}
    >
      <GuillochePattern opacity={0.1} strokeColor="#94a3b8" />
      {design.showSlot && <SlotMark pxPerMm={pxPerMm} />}
      <div className="relative z-10 w-full h-full flex flex-col justify-between p-3.5 text-slate-100" style={design.showSlot ? { paddingTop: 7.5 * pxPerMm } : undefined}>
        <div className="w-full bg-slate-900 border-y border-white/15 py-1 px-3 flex items-center justify-between rounded">
          <div className="flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
            <span className="text-[6.5px] font-mono tracking-wider text-slate-300 uppercase">{design.stripeText}</span>
          </div>
          <span className="text-[6.5px] font-mono text-amber-300 font-bold">CARD ID: {soldier.cardSerial || soldier.id}</span>
        </div>

        <div className="grid grid-cols-12 gap-2 my-auto py-1 items-stretch">
          <div className="col-span-6 space-y-1.5 pr-1 border-r border-white/10">
            <div className="text-[7.5px] font-mono font-bold text-amber-300 uppercase flex items-center gap-1"><UserCheck className="w-3 h-3 text-amber-400" />IDENTIFICATION &amp; MEDICAL RECORD</div>
            <div className="grid grid-cols-2 gap-1 text-[7px] font-mono">
              <div className="bg-black/30 border border-white/10 rounded p-1"><span className="text-slate-400 block text-[6px]">DATE OF BIRTH</span><span className="font-bold text-white">{dash(soldier.dob)}</span></div>
              <div className="bg-black/30 border border-white/10 rounded p-1"><span className="text-slate-400 block text-[6px]">DATE OF ENROLMENT</span><span className="font-bold text-white">{dash(soldier.enrolDate)}</span></div>
            </div>
            <div className="bg-black/30 border border-white/10 rounded p-1 text-[6.5px] font-mono">
              <span className="text-slate-400 block text-[6px]">VISIBLE IDENTIFICATION MARKS:</span>
              <span className="text-amber-200 font-medium">{dash(soldier.idMark)}</span>
            </div>
            <div className="bg-black/30 border border-white/10 rounded p-1 text-[6.5px] font-mono">
              <span className="text-slate-400 text-[6px] flex items-center gap-1"><PhoneCall className="w-2.5 h-2.5 text-amber-400" />NEXT OF KIN (EMERGENCY CONTACT):</span>
              <div className="flex items-center justify-between text-white font-bold pt-0.5 gap-2">
                <span>{dash(soldier.nokName)}{soldier.nokRelation ? ` (${soldier.nokRelation})` : ''}</span>
                <span className="text-amber-300">{soldier.nokPhone}</span>
              </div>
            </div>
          </div>

          <div className="col-span-6 flex flex-col justify-between pl-1">
            <div className="space-y-0.5 text-[6px] font-mono text-slate-300 leading-tight">
              <div className="flex items-center gap-1 text-amber-400 font-bold uppercase text-[7px]"><AlertTriangle className="w-2.5 h-2.5" />{design.instructionsTitle}</div>
              <ol className="list-decimal pl-3 space-y-0.5 text-slate-400">
                {design.instructions.filter(i => i.trim()).map((i, n) => <li key={n}>{i}</li>)}
              </ol>
            </div>
            <div className="flex items-end justify-between pt-1 mt-1 border-t border-white/10">
              <div className="flex flex-col items-center">
                <HolographicSeal className="w-10 h-10" label={design.sealText || 'OFFICIAL'} />
                <span className="text-[5.5px] font-mono text-slate-400 mt-0.5">{design.sealLabel}</span>
              </div>
              <div className="text-right flex flex-col items-end">
                <div className="h-7 w-28 relative flex items-center justify-end border-b border-white/20">
                  {design.coSignature && <img src={design.coSignature} alt="" className="h-7 max-w-[112px] object-contain" />}
                </div>
                <span className="text-[7.5px] font-bold text-white uppercase block leading-none mt-0.5">{design.coName || ' '}</span>
                <span className="text-[6px] font-mono text-amber-400 uppercase">{design.coTitle}</span>
              </div>
            </div>
          </div>
        </div>

        {design.showMrz && (
          <div className="bg-black/60 rounded border border-white/15 p-1 font-mono text-[7px] tracking-[0.2em] leading-[1.1] text-amber-200/90 text-center font-bold">
            <div>{l1}</div><div>{l2}</div><div>{l3}</div>
          </div>
        )}
        <div className="pt-0.5 text-center"><span className="text-[5.5px] font-mono text-slate-400 uppercase">{design.bottomNotice}</span></div>
      </div>
    </div>
  );
};
