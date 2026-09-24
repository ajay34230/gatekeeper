import React from 'react';
import { X, Shield, BookOpen, Layers, GitFork, Layout, CheckCircle, Sparkles } from 'lucide-react';

interface DesignSystemDocModalProps {
  onClose: () => void;
}

export const DesignSystemDocModal: React.FC<DesignSystemDocModalProps> = ({ onClose }) => {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-xs p-3 sm:p-6 overflow-y-auto">
      <div className="bg-white w-full max-w-2xl max-h-[90vh] rounded-2xl shadow-2xl flex flex-col border border-zinc-200 overflow-hidden animate-fade-in">
        {/* Header */}
        <div className="px-6 py-4 bg-zinc-950 text-white flex items-center justify-between border-b border-zinc-800">
          <div className="flex items-center gap-2.5">
            <Shield className="w-5 h-5 text-emerald-400" />
            <div>
              <h2 className="text-sm font-bold tracking-tight">
                Android Operational UI Specification
              </h2>
              <p className="text-[11px] text-zinc-400 font-mono">
                QR Personnel & Vehicle Entry/Exit System • v2.4.1
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Scrollable Content */}
        <div className="p-6 overflow-y-auto space-y-6 text-xs text-zinc-700 leading-relaxed font-sans">
          {/* Section 1: Design Direction */}
          <section className="space-y-2">
            <h3 className="text-xs font-mono uppercase font-bold text-zinc-900 tracking-wider flex items-center gap-1.5">
              <Sparkles className="w-4 h-4 text-emerald-600" />
              1. Design Direction: Disciplined Operational UI
            </h3>
            <p className="text-zinc-600">
              Built as high-reliability field software for gatekeepers at 10 locations managing 700 personnel and 100 vehicles. Rejects decorative consumer gadgets, artificial glassmorphism, or card-soup. Prioritizes <strong>speed, legibility under sunlight, one-handed thumb ergonomics, and zero ambiguity</strong>.
            </p>
          </section>

          {/* Section 2: Design Tokens */}
          <section className="space-y-2.5 border-t border-zinc-100 pt-4">
            <h3 className="text-xs font-mono uppercase font-bold text-zinc-900 tracking-wider flex items-center gap-1.5">
              <Layers className="w-4 h-4 text-zinc-700" />
              2. Design Tokens
            </h3>
            <div className="grid grid-cols-2 gap-3 font-mono text-[11px]">
              <div className="p-3 bg-zinc-50 border border-zinc-200 rounded-xl space-y-1">
                <span className="font-bold text-zinc-900 block font-sans">Semantic Colors</span>
                <div>Primary: <span className="text-zinc-900 font-bold">#18181B (Zinc-900)</span></div>
                <div>Success: <span className="text-emerald-700 font-bold">#059669 (Emerald-600)</span></div>
                <div>Warning: <span className="text-amber-700 font-bold">#D97706 (Amber-600)</span></div>
                <div>Error: <span className="text-rose-700 font-bold">#E11D48 (Rose-600)</span></div>
                <div>Surface: <span className="text-zinc-600 font-bold">#FAFAFA / #FFFFFF</span></div>
              </div>

              <div className="p-3 bg-zinc-50 border border-zinc-200 rounded-xl space-y-1">
                <span className="font-bold text-zinc-900 block font-sans">Metrics & Geometry</span>
                <div>Touch Targets: <span className="text-zinc-900 font-bold">Min 48dp - 76dp</span></div>
                <div>Card Radii: <span className="text-zinc-900 font-bold">12dp - 16dp</span></div>
                <div>Button Radii: <span className="text-zinc-900 font-bold">12dp (Pill for chips)</span></div>
                <div>Body Font: <span className="text-zinc-900 font-bold">Plus Jakarta Sans</span></div>
                <div>ID/Data Font: <span className="text-zinc-900 font-bold">JetBrains Mono</span></div>
              </div>
            </div>
          </section>

          {/* Section 3: Component Library */}
          <section className="space-y-2 border-t border-zinc-100 pt-4">
            <h3 className="text-xs font-mono uppercase font-bold text-zinc-900 tracking-wider flex items-center gap-1.5">
              <BookOpen className="w-4 h-4 text-zinc-700" />
              3. Component Library Specifications
            </h3>
            <ul className="list-disc pl-4 space-y-1 text-zinc-600">
              <li><strong>Primary Button:</strong> High-contrast Zinc-900 fill, 2x horizontal padding, 48-56px height, font-bold.</li>
              <li><strong>Optical Scanner Frame:</strong> 4 crisp corner brackets, 0.5px laser sweep, Web Audio 920Hz synth tone + haptic vibration.</li>
              <li><strong>Status Chip:</strong> Dot indicator + high contrast label (e.g. Active, Inside, Outside, Offline).</li>
              <li><strong>Confirmation Bottom Sheet:</strong> Non-disruptive drawer with structured table parameters for zero-mistake review.</li>
            </ul>
          </section>

          {/* Section 4: Screen Map & Wireframes */}
          <section className="space-y-2 border-t border-zinc-100 pt-4">
            <h3 className="text-xs font-mono uppercase font-bold text-zinc-900 tracking-wider flex items-center gap-1.5">
              <Layout className="w-4 h-4 text-zinc-700" />
              4. Complete Navigation & Screen Architecture
            </h3>
            <div className="p-3 bg-zinc-900 text-zinc-200 font-mono text-[11px] rounded-xl whitespace-pre overflow-x-auto leading-relaxed">
{`LOGIN (Gatekeeper ID + PIN + Post Selection)
  ↓
HOME (Who Am I • Where Am I • Connectivity)
  ├── [PRIORITY 1] SCAN PERSON ──► IDENTIFY ──► CONFIRM ──► SUCCESS
  ├── [PRIORITY 2] SCAN VEHICLE ──► DRIVER ──► CO-DRIVER ──► OCCUPANTS ──► CONFIRM ──► SUCCESS
  ├── RECENT ACTIVITY (Chronological Filterable Shift Log)
  ├── SYNC STATUS (Offline Buffer & Realtime Socket Link)
  └── OPERATOR (Handheld Hardware & Shift Handover)`}
            </div>
          </section>

          {/* Section 5: Interaction Flow */}
          <section className="space-y-2 border-t border-zinc-100 pt-4">
            <h3 className="text-xs font-mono uppercase font-bold text-zinc-900 tracking-wider flex items-center gap-1.5">
              <GitFork className="w-4 h-4 text-zinc-700" />
              5. Verified Operational Interaction Flows
            </h3>
            <div className="space-y-2">
              <div className="p-2.5 bg-zinc-50 border border-zinc-200 rounded-lg">
                <span className="font-bold text-zinc-900 block mb-0.5">Personnel Loop:</span>
                <span className="font-mono text-emerald-800">Home → Scan Person → Verify Photo & Status → Record Entry/Exit → Synced Success → Rapid Return</span>
              </div>
              <div className="p-2.5 bg-zinc-50 border border-zinc-200 rounded-lg">
                <span className="font-bold text-zinc-900 block mb-0.5">Vehicle Manifest Loop:</span>
                <span className="font-mono text-zinc-800">Home → Scan Vehicle Plate/QR → Scan Driver → Optional Co-Driver → Add Passengers → Review Manifest → Confirm</span>
              </div>
            </div>
          </section>

          {/* Section 6: UI Verification Checklist */}
          <section className="space-y-2 border-t border-zinc-100 pt-4">
            <h3 className="text-xs font-mono uppercase font-bold text-zinc-900 tracking-wider flex items-center gap-1.5">
              <CheckCircle className="w-4 h-4 text-emerald-600" />
              6. Verification Checklist
            </h3>
            <div className="grid grid-cols-2 gap-2 text-[11px]">
              <div className="flex items-center gap-1.5 text-zinc-800">
                <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                <span>Zero unsolicited PC features</span>
              </div>
              <div className="flex items-center gap-1.5 text-zinc-800">
                <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                <span>Priority 1 & 2 scanner focus</span>
              </div>
              <div className="flex items-center gap-1.5 text-zinc-800">
                <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                <span>Field offline resilience</span>
              </div>
              <div className="flex items-center gap-1.5 text-zinc-800">
                <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                <span>Stay duration calculation</span>
              </div>
              <div className="flex items-center gap-1.5 text-zinc-800">
                <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                <span>48dp+ touch targets</span>
              </div>
              <div className="flex items-center gap-1.5 text-zinc-800">
                <CheckCircle className="w-3.5 h-3.5 text-emerald-600" />
                <span>Immediate acoustic feedback</span>
              </div>
            </div>
          </section>
        </div>

        {/* Footer */}
        <div className="px-6 py-3 bg-zinc-50 border-t border-zinc-200 flex justify-end">
          <button
            onClick={onClose}
            className="py-2 px-4 bg-zinc-900 hover:bg-black text-white font-semibold text-xs rounded-xl"
          >
            Close Specification
          </button>
        </div>
      </div>
    </div>
  );
};
