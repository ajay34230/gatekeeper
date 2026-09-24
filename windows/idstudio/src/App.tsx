import React, { useEffect, useMemo, useRef, useState } from 'react';
import { CardDesign, Soldier, ThemeId } from './types';
import { CARD_THEMES, DEFAULT_DESIGN, ARMY_RANKS, BLOOD_GROUPS } from './themes';
import { CardFront } from './components/CardFront';
import { CardBack } from './components/CardBack';
import { SignaturePad } from './components/SignaturePad';
import { NationalCrest } from './components/MilitaryEmblem';
import { host } from './host';
import { cardCheck } from './mrz';
import { Printer, RotateCw, FileDown, Users, Search, Sliders, User, Type, Shield, Palette, Upload, PenTool, Save, Trash2, Database, CheckSquare, Square } from 'lucide-react';

type Mode = 'INDIVIDUAL' | 'MULTIPLE' | 'COMPANY' | 'PLATOON' | 'SECTION' | 'ALL';
const MODES: [Mode, string][] = [['INDIVIDUAL', 'Individual'], ['MULTIPLE', 'Multiple'], ['COMPANY', 'Company'], ['PLATOON', 'Platoon'], ['SECTION', 'Section'], ['ALL', 'Entire']];
const eq = (a: string, b: string) => (a || '').trim().toLowerCase() === (b || '').trim().toLowerCase();
const uniq = (xs: string[]) => [...new Set(xs.filter(x => x.trim()))].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));

export default function App() {
  const [soldiers, setSoldiers] = useState<Soldier[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [design, setDesign] = useState<CardDesign>(DEFAULT_DESIGN);
  const [designDirty, setDesignDirty] = useState(false);
  const [mode, setMode] = useState<Mode>('INDIVIDUAL');
  const [company, setCompany] = useState('');
  const [platoon, setPlatoon] = useState('');
  const [section, setSection] = useState('');
  const [ticked, setTicked] = useState<Set<string>>(new Set());
  const [focus, setFocus] = useState('');
  const [query, setQuery] = useState('');
  const [flipped, setFlipped] = useState(false);
  const [view, setView] = useState<'flip' | 'dual'>('dual');
  const [tab, setTab] = useState<'soldier' | 'text' | 'back' | 'theme'>('soldier');
  const [draft, setDraft] = useState<Soldier | null>(null);
  const [pad, setPad] = useState<'' | 'bearer' | 'co'>('');
  const [toast, setToast] = useState('');
  const [printIds, setPrintIds] = useState<string[]>([]);
  const printPurpose = useRef<'print' | 'pdf'>('print');
  const [checks, setChecks] = useState<Record<string, string>>({});

  // ---------------------------------------------------------------- host bridge
  useEffect(() => {
    const off = host.on(m => {
      if (m.t === 'init' || m.t === 'soldiers') { setSoldiers(m.soldiers); setLoaded(true); }
      if (m.t === 'init' && m.design) { setDesign({ ...DEFAULT_DESIGN, ...m.design }); setDesignDirty(false); }
      if (m.t === 'init' && m.preselect?.length) { setMode('MULTIPLE'); setTicked(new Set(m.preselect)); setFocus(m.preselect[0]); }
      if (m.t === 'saved' || m.t === 'error') { setToast(m.message); setTimeout(() => setToast(''), 3500); }
      if (m.t === 'designSaved') setDesignDirty(false);
      if (m.t === 'preparePrint') { printPurpose.current = m.purpose; setPrintIds(m.ids); }
      if (m.t === 'printDone') setPrintIds([]);
    });
    host.send({ t: 'ready' });
    return off;
  }, []);

  // SHA-256 check value for every soldier (shown on the card, changes when a printed detail changes)
  useEffect(() => {
    let live = true;
    Promise.all(soldiers.map(async s => [s.id, await cardCheck(s)] as const)).then(r => { if (live) setChecks(Object.fromEntries(r)); });
    return () => { live = false; };
  }, [soldiers]);

  // ---------------------------------------------------------------- selection
  const companies = useMemo(() => uniq(soldiers.map(s => s.company)), [soldiers]);
  const platoons = useMemo(() => uniq(soldiers.filter(s => eq(s.company, company)).map(s => s.platoon)), [soldiers, company]);
  const sections = useMemo(() => uniq(soldiers.filter(s => eq(s.company, company) && eq(s.platoon, platoon)).map(s => s.section)), [soldiers, company, platoon]);
  useEffect(() => { if (!companies.includes(company)) setCompany(companies[0] ?? ''); }, [companies]);
  useEffect(() => { if (!platoons.includes(platoon)) setPlatoon(platoons[0] ?? ''); }, [platoons]);
  useEffect(() => { if (!sections.includes(section)) setSection(sections[0] ?? ''); }, [sections]);

  const selected = useMemo(() => soldiers.filter(s => {
    switch (mode) {
      case 'INDIVIDUAL': return s.id === focus;
      case 'MULTIPLE': return ticked.has(s.id);
      case 'COMPANY': return eq(s.company, company);
      case 'PLATOON': return eq(s.company, company) && eq(s.platoon, platoon);
      case 'SECTION': return eq(s.company, company) && eq(s.platoon, platoon) && eq(s.section, section);
      default: return true;
    }
  }), [soldiers, mode, focus, ticked, company, platoon, section]);

  const listed = useMemo(() => {
    const q = query.trim().toLowerCase();
    const base = mode === 'INDIVIDUAL' || mode === 'MULTIPLE' ? soldiers : selected;
    return base.filter(s => !q || [s.id, s.armyNo, s.rank, s.name, s.company, s.platoon, s.section, s.unit].join(' ').toLowerCase().includes(q));
  }, [soldiers, selected, mode, query]);

  const current = soldiers.find(s => s.id === focus) ?? selected[0] ?? soldiers[0];
  useEffect(() => { if (current && focus !== current.id) setFocus(current.id); }, [current?.id]);
  useEffect(() => { setDraft(current ? { ...current } : null); }, [current?.id, soldiers]);

  const setD = (patch: Partial<CardDesign>) => { setDesign(d => ({ ...d, ...patch })); setDesignDirty(true); };
  const toggle = (id: string) => setTicked(t => { const n = new Set(t); n.has(id) ? n.delete(id) : n.add(id); return n; });

  // ---------------------------------------------------------------- printing (layout rendered here, printed / saved as PDF by the host)
  const printList = useMemo(() => printIds.map(id => soldiers.find(s => s.id === id)).filter(Boolean) as Soldier[], [printIds, soldiers]);
  useEffect(() => {
    if (printList.length === 0) return;
    let cancelled = false;
    (async () => {
      await new Promise(r => setTimeout(r, 300));
      const imgs = [...document.querySelectorAll<HTMLImageElement>('.print-area img')];
      await Promise.all(imgs.map(i => i.complete ? Promise.resolve() : i.decode().catch(() => undefined)));
      await new Promise(r => setTimeout(r, 200));
      if (cancelled) return;
      if (printPurpose.current === 'pdf') host.send({ t: 'printReady' });
      else { window.print(); setPrintIds([]); }
    })();
    return () => { cancelled = true; };
  }, [printList]);

  const ids = selected.map(s => s.id);
  const readFile = (f: File, cb: (url: string) => void) => { const r = new FileReader(); r.onload = () => cb(String(r.result)); r.readAsDataURL(f); };

  if (!host.available) return <div className="p-10 text-slate-400 font-mono">The ID Card Studio runs inside the XV Command Center.</div>;

  return (
    <div className="app-root min-h-screen bg-[#070b10] text-slate-100">
      {/* ============ HEADER ============ */}
      <header className="no-print border-b border-slate-800/80 bg-slate-950/80 backdrop-blur-md sticky top-0 z-40">
        <div className="px-6 h-16 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-500/20 to-amber-950/40 border border-amber-500/40 flex items-center justify-center text-amber-400 shadow-md"><NationalCrest className="w-6 h-7" /></div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-lg font-serif font-bold tracking-wider text-white">ID CARD STUDIO</h1>
                <span className="text-[10px] font-mono font-bold px-2 py-0.5 rounded bg-amber-500/15 text-amber-300 border border-amber-500/40">XV COMMAND CENTER</span>
              </div>
              <p className="text-xs font-mono text-slate-400">Soldier identity cards from the encrypted register • genuine gate QR codes</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button onClick={() => host.send({ t: 'exportData', ids })} className="flex items-center gap-2 px-3 py-2 rounded-lg border border-slate-700 bg-slate-900 text-slate-200 text-sm font-semibold hover:bg-slate-800"><Database className="w-4 h-4" />Export data</button>
            <button disabled={!selected.length} onClick={() => host.send({ t: 'pdf', ids })} className="flex items-center gap-2 px-3 py-2 rounded-lg border border-cyan-700/60 bg-cyan-950/40 text-cyan-300 text-sm font-semibold hover:bg-cyan-900/40 disabled:opacity-40"><FileDown className="w-4 h-4" />Save PDF ({selected.length})</button>
            <button disabled={!selected.length} onClick={() => host.send({ t: 'print', ids })} className="flex items-center gap-2 px-4 py-2 rounded-lg bg-amber-500 text-slate-950 text-sm font-bold shadow-lg shadow-amber-500/20 hover:bg-amber-400 disabled:opacity-40"><Printer className="w-4 h-4" />Print cards ({selected.length})</button>
          </div>
        </div>
      </header>

      <main className="no-print px-6 py-6 grid grid-cols-[360px_1fr] gap-6">
        {/* ============ SELECTION ============ */}
        <aside className="rounded-2xl border border-slate-800 bg-slate-900/50 p-4 flex flex-col gap-3 h-[calc(100vh-7rem)] sticky top-20">
          <div className="flex items-center gap-2 text-sm font-bold text-white"><Users className="w-4 h-4 text-amber-400" />SELECT SOLDIERS</div>
          <div className="grid grid-cols-3 gap-1 p-1 rounded-xl bg-slate-950 border border-slate-800">
            {MODES.map(([m, label]) => (
              <button key={m} onClick={() => setMode(m)} className={`text-xs py-1.5 rounded-lg font-semibold ${mode === m ? 'bg-amber-500/20 text-amber-300 border border-amber-500/50' : 'text-slate-400 hover:text-white'}`}>{label}</button>
            ))}
          </div>
          {(mode === 'COMPANY' || mode === 'PLATOON' || mode === 'SECTION') && (
            <div className="grid grid-cols-1 gap-2">
              <Select label="Company" value={company} options={companies} onChange={setCompany} />
              {(mode === 'PLATOON' || mode === 'SECTION') && <Select label="Platoon" value={platoon} options={platoons} onChange={setPlatoon} />}
              {mode === 'SECTION' && <Select label="Section" value={section} options={sections} onChange={setSection} />}
            </div>
          )}
          <div className="relative">
            <Search className="w-4 h-4 text-slate-500 absolute left-3 top-2.5" />
            <input value={query} onChange={e => setQuery(e.target.value)} placeholder="Search ID, army no, name, rank…" className="w-full pl-9 pr-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-sm focus:border-amber-500 outline-none" />
          </div>
          {mode === 'MULTIPLE' && (
            <div className="flex gap-2 text-xs">
              <button onClick={() => setTicked(new Set([...ticked, ...listed.map(s => s.id)]))} className="px-2 py-1 rounded border border-slate-700 text-slate-300 hover:bg-slate-800">Tick all shown</button>
              <button onClick={() => setTicked(new Set())} className="px-2 py-1 rounded border border-slate-700 text-slate-300 hover:bg-slate-800">Clear</button>
            </div>
          )}
          <div className="flex-1 overflow-y-auto -mx-1 px-1 space-y-1">
            {loaded && soldiers.length === 0 && <p className="text-sm text-slate-500 p-3">The register is empty. Add soldiers in Personnel Registry or import an Excel / CSV file.</p>}
            {listed.map(s => (
              <div key={s.id} onClick={() => { setFocus(s.id); if (mode === 'MULTIPLE') toggle(s.id); }}
                className={`flex items-center gap-2 px-2.5 py-2 rounded-lg cursor-pointer border ${s.id === current?.id ? 'border-amber-500/60 bg-amber-500/10' : 'border-transparent hover:bg-slate-800/60'}`}>
                {mode === 'MULTIPLE' && (ticked.has(s.id) ? <CheckSquare className="w-4 h-4 text-amber-400 shrink-0" /> : <Square className="w-4 h-4 text-slate-500 shrink-0" />)}
                <div className="min-w-0">
                  <div className="text-sm font-semibold truncate"><span className="text-amber-300">{s.rank}</span> {s.name}</div>
                  <div className="text-[11px] font-mono text-slate-400 truncate">{s.id} • {s.armyNo || 'no army no'} • {[s.company, s.platoon, s.section].filter(Boolean).join(' / ') || 'no company'}</div>
                </div>
                {!s.photoVer && <span className="ml-auto text-[9px] font-mono text-rose-300 border border-rose-500/40 rounded px-1 shrink-0">NO PHOTO</span>}
              </div>
            ))}
          </div>
          <div className="text-xs font-mono text-amber-300 border-t border-slate-800 pt-2">{selected.length} card(s) selected for printing</div>
        </aside>

        <section className="min-w-0 space-y-6">
          {/* ============ PREVIEW ============ */}
          <div className="rounded-2xl border border-slate-800 bg-slate-900/40 p-4 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <span className="text-xs font-mono text-slate-400">DISPLAY MODE:</span>
              <div className="flex p-1 rounded-lg bg-slate-950 border border-slate-800 text-sm">
                <button onClick={() => setView('flip')} className={`px-3 py-1 rounded ${view === 'flip' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/50' : 'text-slate-400'}`}>Front / Back</button>
                <button onClick={() => setView('dual')} className={`px-3 py-1 rounded ${view === 'dual' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/50' : 'text-slate-400'}`}>Dual Face</button>
              </div>
            </div>
            {view === 'flip' && <button onClick={() => setFlipped(f => !f)} className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-slate-700 text-sm text-slate-200 hover:bg-slate-800"><RotateCw className="w-4 h-4" />Flip to {flipped ? 'front' : 'back'}</button>}
          </div>

          {current ? (
            <div className={`flex ${view === 'dual' ? 'flex-wrap' : ''} gap-8 justify-center`}>
              {(view === 'dual' || !flipped) && <div className="space-y-2"><div className="text-center text-sm font-mono font-bold text-amber-300 tracking-widest">CARD FRONT</div><CardFront soldier={current} design={design} check={checks[current.id] ?? ''} /></div>}
              {(view === 'dual' || flipped) && <div className="space-y-2"><div className="text-center text-sm font-mono font-bold text-cyan-300 tracking-widest">CARD BACK</div><CardBack soldier={current} design={design} /></div>}
            </div>
          ) : <div className="text-center text-slate-500 py-24 font-mono">{loaded ? 'No soldier to show.' : 'Loading register…'}</div>}

          {/* ============ CUSTOMIZER ============ */}
          <div className="rounded-2xl border border-slate-800 bg-slate-900/50 p-5">
            <div className="flex items-center justify-between border-b border-slate-800 pb-3 mb-4">
              <div className="flex items-center gap-2 font-bold text-white"><Sliders className="w-4 h-4 text-amber-400" />SOLDIER ID CARD FORMATTER &amp; CUSTOMIZER</div>
              {tab !== 'soldier' && (
                <div className="flex gap-2">
                  <button onClick={() => { setDesign(DEFAULT_DESIGN); setDesignDirty(true); }} className="px-3 py-1.5 rounded-lg border border-slate-700 text-xs text-slate-300 hover:bg-slate-800">Reset to standard</button>
                  <button disabled={!designDirty} onClick={() => host.send({ t: 'saveDesign', design })} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-amber-500 text-slate-950 text-xs font-bold disabled:opacity-40"><Save className="w-3.5 h-3.5" />Save card design</button>
                </div>
              )}
            </div>
            <div className="grid grid-cols-4 gap-1 p-1 rounded-xl bg-slate-950 border border-slate-800 mb-5">
              {([['soldier', 'Soldier Details', User], ['text', 'Card Text', Type], ['back', 'Back & Authority', Shield], ['theme', 'Theme & Emblems', Palette]] as const).map(([k, label, Icon]) => (
                <button key={k} onClick={() => setTab(k)} className={`flex items-center justify-center gap-2 py-2 rounded-lg text-sm ${tab === k ? 'bg-amber-500/15 text-amber-300 border border-amber-500/50' : 'text-slate-400 hover:text-white'}`}><Icon className="w-4 h-4" />{label}</button>
              ))}
            </div>

            {tab === 'soldier' && draft && (
              <div className="space-y-4">
                <div className="grid grid-cols-3 gap-4">
                  <Field label="Army number" value={draft.armyNo} onChange={v => setDraft({ ...draft, armyNo: v })} mono />
                  <Field label="Rank" value={draft.rank} onChange={v => setDraft({ ...draft, rank: v })} list={ARMY_RANKS} />
                  <Field label="Full name" value={draft.name} onChange={v => setDraft({ ...draft, name: v })} />
                  <Field label="Appointment" value={draft.appointment} onChange={v => setDraft({ ...draft, appointment: v })} />
                  <Field label="Unit" value={draft.unit} onChange={v => setDraft({ ...draft, unit: v })} />
                  <Field label="Company" value={draft.company} onChange={v => setDraft({ ...draft, company: v })} list={['Alpha', 'Bravo', 'Charlie', 'Delta', 'SP', 'HQ']} />
                  <Field label="Platoon" value={draft.platoon} onChange={v => setDraft({ ...draft, platoon: v })} list={platoons} />
                  <Field label="Section" value={draft.section} onChange={v => setDraft({ ...draft, section: v })} list={sections} />
                  <Field label="Blood group" value={draft.bloodGroup} onChange={v => setDraft({ ...draft, bloodGroup: v })} list={BLOOD_GROUPS} />
                  <Field label="Mobile" value={draft.mobile} onChange={v => setDraft({ ...draft, mobile: v })} mono />
                  <Field label="Date of birth (DD-MM-YYYY)" value={draft.dob} onChange={v => setDraft({ ...draft, dob: v })} mono />
                  <Field label="Date of enrolment" value={draft.enrolDate} onChange={v => setDraft({ ...draft, enrolDate: v })} mono />
                  <Field label="Card expiry" value={draft.expiryDate} onChange={v => setDraft({ ...draft, expiryDate: v })} mono />
                  <Field label="Card serial / ref no." value={draft.cardSerial} onChange={v => setDraft({ ...draft, cardSerial: v })} mono />
                  <Field label="Identification mark" value={draft.idMark} onChange={v => setDraft({ ...draft, idMark: v })} />
                  <Field label="Next of kin — name" value={draft.nokName} onChange={v => setDraft({ ...draft, nokName: v })} />
                  <Field label="Next of kin — relation" value={draft.nokRelation} onChange={v => setDraft({ ...draft, nokRelation: v })} />
                  <Field label="Next of kin — phone" value={draft.nokPhone} onChange={v => setDraft({ ...draft, nokPhone: v })} mono />
                </div>
                <Field label="Permanent address" value={draft.address} onChange={v => setDraft({ ...draft, address: v })} />
                <div className="flex flex-wrap items-center gap-3 pt-2 border-t border-slate-800">
                  <label className="flex items-center gap-2 px-4 py-2 rounded-lg bg-amber-500 text-slate-950 font-bold text-sm cursor-pointer"><Upload className="w-4 h-4" />Upload photo
                    <input type="file" accept="image/*" className="hidden" onChange={e => { const f = e.target.files?.[0]; if (f) readFile(f, url => host.send({ t: 'savePhoto', id: draft.id, dataUrl: url })); e.target.value = ''; }} />
                  </label>
                  {draft.photoVer > 0 && <button onClick={() => host.send({ t: 'savePhoto', id: draft.id, dataUrl: '' })} className="flex items-center gap-1.5 px-3 py-2 rounded-lg border border-rose-800 text-rose-300 text-sm"><Trash2 className="w-4 h-4" />Remove photo</button>}
                  <button onClick={() => setPad('bearer')} className="flex items-center gap-2 px-4 py-2 rounded-lg border border-cyan-700 text-cyan-300 text-sm font-semibold"><PenTool className="w-4 h-4" />{draft.signatureVer ? 'Replace signature (draw / upload)' : 'Add signature (draw / upload)'}</button>
                  {draft.signatureVer > 0 && <button onClick={() => host.send({ t: 'saveSignature', id: draft.id, dataUrl: '' })} className="px-3 py-2 rounded-lg border border-slate-700 text-slate-300 text-sm">Clear signature</button>}
                  <button onClick={() => host.send({ t: 'saveSoldier', soldier: draft })} className="ml-auto flex items-center gap-2 px-4 py-2 rounded-lg bg-emerald-600 text-white font-bold text-sm"><Save className="w-4 h-4" />Save details to register</button>
                </div>
                <p className="text-xs text-slate-500 font-mono">Saved details update the encrypted register, exports and gate terminals. The QR code is the soldier's secret code; re-issue it from Personnel Registry if a card is lost.</p>
              </div>
            )}

            {tab === 'text' && (
              <div className="grid grid-cols-2 gap-4">
                <Field label="Top line" value={design.govtLine} onChange={v => setD({ govtLine: v })} />
                <Field label="Card title" value={design.title} onChange={v => setD({ title: v })} />
                <Field label="Form / sub-title line" value={design.formLine} onChange={v => setD({ formLine: v })} />
                <Field label="Security micro-print text" value={design.microText} onChange={v => setD({ microText: v })} />
                <Field label="Footer left (followed by the army no.)" value={design.footerLeftPrefix} onChange={v => setD({ footerLeftPrefix: v })} />
                <Field label="Footer centre" value={design.footerCenter} onChange={v => setD({ footerCenter: v })} />
                <Field label="Footer right" value={design.footerRight} onChange={v => setD({ footerRight: v })} />
                <Field label="Photo stamp / seal word" value={design.sealText} onChange={v => setD({ sealText: v })} />
                <Field label="Back: top band text" value={design.stripeText} onChange={v => setD({ stripeText: v })} />
                <Field label="Back: bottom notice" value={design.bottomNotice} onChange={v => setD({ bottomNotice: v })} />
                <Field label="Issuing state code (3 letters, machine-readable zone)" value={design.issuingState} onChange={v => setD({ issuingState: v.toUpperCase().slice(0, 3) })} mono />
              </div>
            )}

            {tab === 'back' && (
              <div className="grid grid-cols-2 gap-6">
                <div className="space-y-3">
                  <Field label="Instructions heading" value={design.instructionsTitle} onChange={v => setD({ instructionsTitle: v })} />
                  <label className="block">
                    <span className="block text-xs font-mono font-bold text-amber-400 uppercase mb-1.5">Instructions (one per line)</span>
                    <textarea value={design.instructions.join('\n')} onChange={e => setD({ instructions: e.target.value.split('\n') })} rows={6}
                      className="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-sm focus:border-amber-500 outline-none" />
                  </label>
                </div>
                <div className="space-y-3">
                  <Field label="Commanding officer / issuing authority — name" value={design.coName} onChange={v => setD({ coName: v })} />
                  <Field label="Authority title" value={design.coTitle} onChange={v => setD({ coTitle: v })} />
                  <Field label="Seal caption" value={design.sealLabel} onChange={v => setD({ sealLabel: v })} />
                  <div className="flex items-center gap-3">
                    <button onClick={() => setPad('co')} className="flex items-center gap-2 px-4 py-2 rounded-lg border border-cyan-700 text-cyan-300 text-sm font-semibold"><PenTool className="w-4 h-4" />{design.coSignature ? 'Replace authority signature (draw / upload)' : 'Add authority signature (draw / upload)'}</button>
                    {design.coSignature && <button onClick={() => setD({ coSignature: '' })} className="px-3 py-2 rounded-lg border border-slate-700 text-slate-300 text-sm">Remove</button>}
                  </div>
                </div>
              </div>
            )}

            {tab === 'theme' && (
              <div className="space-y-5">
                <div className="grid grid-cols-5 gap-3">
                  {(Object.keys(CARD_THEMES) as ThemeId[]).map(id => (
                    <button key={id} onClick={() => setD({ theme: id })} className={`rounded-xl p-3 border text-left ${design.theme === id ? 'border-amber-500 bg-amber-500/10' : 'border-slate-700 hover:border-slate-500'}`}>
                      <div className={`h-10 rounded-lg bg-gradient-to-br ${CARD_THEMES[id].bgGradient} border ${CARD_THEMES[id].cardBorder}`}></div>
                      <div className="text-xs font-semibold mt-2">{CARD_THEMES[id].name}</div>
                    </button>
                  ))}
                </div>
                <div className="grid grid-cols-2 gap-6">
                  <ImagePick label="Left crest" mode={design.crest} image={design.crestImage} options={[['national', 'Standard crest'], ['custom', 'Your emblem']]}
                    onMode={m => setD({ crest: m as CardDesign['crest'] })} onImage={u => setD({ crestImage: u, crest: 'custom' })} read={readFile} />
                  <ImagePick label="Right badge" mode={design.badge} image={design.badgeImage} options={[['swords', 'Crossed swords'], ['custom', 'Unit badge'], ['none', 'None']]}
                    onMode={m => setD({ badge: m as CardDesign['badge'] })} onImage={u => setD({ badgeImage: u, badge: 'custom' })} read={readFile} />
                </div>
                <div className="flex flex-wrap gap-5 text-sm">
                  <Check label="Show mobile number" v={design.showMobile} on={v => setD({ showMobile: v })} />
                  <Check label="Show address" v={design.showAddress} on={v => setD({ showAddress: v })} />
                  <Check label="Show gold chip graphic" v={design.showChip} on={v => setD({ showChip: v })} />
                  <Check label="Show machine-readable zone" v={design.showMrz} on={v => setD({ showMrz: v })} />
                </div>
              </div>
            )}
          </div>
        </section>
      </main>

      <SignaturePad open={pad !== ''} title={pad === 'co' ? 'Issuing authority signature' : `Signature of ${draft?.rank ?? ''} ${draft?.name ?? ''}`} onClose={() => setPad('')}
        onSave={png => pad === 'co' ? setD({ coSignature: png }) : draft && host.send({ t: 'saveSignature', id: draft.id, dataUrl: png })} />

      {toast && <div className="no-print fixed bottom-6 right-6 z-50 px-4 py-3 rounded-xl bg-slate-900 border border-amber-500/50 text-amber-200 text-sm shadow-2xl">{toast}</div>}

      {/* ============ PRINT LAYOUT: front + back of each soldier at true card size (85.6 × 54 mm) ============ */}
      <div className="print-area">
        {printList.map(s => (
          <div key={s.id} className="print-row">
            <div className="print-card"><div className="print-zoom"><CardFront soldier={s} design={design} check={checks[s.id] ?? ''} /></div></div>
            <div className="print-card"><div className="print-zoom"><CardBack soldier={s} design={design} /></div></div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- small form controls (demo styling)

const Field: React.FC<{ label: string; value: string; onChange: (v: string) => void; mono?: boolean; list?: string[] }> = ({ label, value, onChange, mono, list }) => {
  const id = useMemo(() => 'l' + Math.random().toString(36).slice(2), []);
  return (
    <label className="block">
      <span className="block text-xs font-mono font-bold text-amber-400 uppercase mb-1.5">{label}</span>
      <input value={value} onChange={e => onChange(e.target.value)} list={list ? id : undefined}
        className={`w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-sm focus:border-amber-500 outline-none ${mono ? 'font-mono' : ''}`} />
      {list && <datalist id={id}>{list.map(o => <option key={o} value={o} />)}</datalist>}
    </label>
  );
};

const Select: React.FC<{ label: string; value: string; options: string[]; onChange: (v: string) => void }> = ({ label, value, options, onChange }) => (
  <label className="block">
    <span className="block text-[11px] font-mono font-bold text-slate-400 uppercase mb-1">{label}</span>
    <select value={value} onChange={e => onChange(e.target.value)} className="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-sm">
      {options.length === 0 && <option value="">— none in register —</option>}
      {options.map(o => <option key={o} value={o}>{o}</option>)}
    </select>
  </label>
);

const Check: React.FC<{ label: string; v: boolean; on: (v: boolean) => void }> = ({ label, v, on }) => (
  <label className="flex items-center gap-2 cursor-pointer text-slate-300"><input type="checkbox" checked={v} onChange={e => on(e.target.checked)} className="accent-amber-500 w-4 h-4" />{label}</label>
);

const ImagePick: React.FC<{ label: string; mode: string; image: string; options: [string, string][]; onMode: (m: string) => void; onImage: (url: string) => void; read: (f: File, cb: (u: string) => void) => void }> =
  ({ label, mode, image, options, onMode, onImage, read }) => (
    <div className="rounded-xl border border-slate-800 p-3 space-y-2">
      <div className="text-xs font-mono font-bold text-amber-400 uppercase">{label}</div>
      <div className="flex flex-wrap gap-2">
        {options.map(([k, t]) => <button key={k} onClick={() => onMode(k)} className={`px-3 py-1 rounded-lg text-xs border ${mode === k ? 'border-amber-500 text-amber-300 bg-amber-500/10' : 'border-slate-700 text-slate-400'}`}>{t}</button>)}
        <label className="px-3 py-1 rounded-lg text-xs border border-slate-700 text-slate-300 cursor-pointer hover:bg-slate-800">Upload image…
          <input type="file" accept="image/png,image/jpeg,image/svg+xml" className="hidden" onChange={e => { const f = e.target.files?.[0]; if (f) read(f, onImage); e.target.value = ''; }} />
        </label>
      </div>
      {image && <img src={image} alt="" className="h-12 object-contain" />}
    </div>
  );
