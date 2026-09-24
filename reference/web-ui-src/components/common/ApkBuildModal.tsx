import React, { useState } from 'react';
import {
  Smartphone,
  X,
  Download,
  Terminal,
  CheckCircle2,
  Copy,
  Check,
  ExternalLink,
  Shield,
  Layers,
  ArrowRight,
  Sparkles,
} from 'lucide-react';

interface ApkBuildModalProps {
  onClose: () => void;
}

export const ApkBuildModal: React.FC<ApkBuildModalProps> = ({ onClose }) => {
  const [copiedCmd, setCopiedCmd] = useState<string | null>(null);

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedCmd(id);
    setTimeout(() => setCopiedCmd(null), 2000);
  };

  const currentOrigin = typeof window !== 'undefined' ? window.location.origin : 'https://ais-dev-...run.app';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-black/80 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-zinc-900 border border-zinc-700/80 rounded-2xl w-full max-w-2xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden text-zinc-100">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-zinc-800 bg-zinc-950/70">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-emerald-500/20 border border-emerald-500/40 flex items-center justify-center text-emerald-400">
              <Smartphone className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-base font-bold text-zinc-100">Install on Android Device / Build APK</h2>
                <span className="px-2 py-0.5 rounded-full text-[10px] font-mono bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                  PWA + Native Ready
                </span>
              </div>
              <p className="text-xs text-zinc-400">Two seamless ways to run XV DIGITAL ACCESS CONTROL on Android handsets or rugged scanners</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Scrollable Content */}
        <div className="p-5 space-y-6 overflow-y-auto custom-scrollbar text-sm">
          {/* Method 1: Instant Install PWA (No APK Compile Needed) */}
          <div className="bg-linear-to-br from-emerald-950/40 via-zinc-900 to-zinc-900 border border-emerald-500/30 rounded-xl p-4.5 space-y-3.5">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-emerald-400 font-bold text-sm">
                <Sparkles className="w-4 h-4 text-emerald-400" />
                <span>Method 1: Instant 10-Second Install (Direct PWA APK)</span>
              </div>
              <span className="text-[11px] bg-emerald-500/20 text-emerald-300 font-semibold px-2 py-0.5 rounded-full">
                Recommended
              </span>
            </div>
            <p className="text-xs text-zinc-300 leading-relaxed">
              This app is already fully configured with an offline-capable <strong>Web App Manifest</strong> and <strong>Service Worker</strong>. Android natively packages PWAs into direct APKs (WebAPKs) on the phone with zero compilation required!
            </p>

            <div className="space-y-2 bg-zinc-950/60 rounded-lg p-3 border border-zinc-800/80">
              <div className="flex items-start gap-2.5 text-xs text-zinc-300">
                <span className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold shrink-0 text-[11px]">1</span>
                <div>
                  Open Chrome on your Android phone and navigate to:
                  <div className="font-mono text-[11px] text-emerald-300 mt-1 bg-black/40 px-2 py-1 rounded border border-zinc-800 break-all select-all">
                    {currentOrigin}
                  </div>
                </div>
              </div>
              <div className="flex items-start gap-2.5 text-xs text-zinc-300">
                <span className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold shrink-0 text-[11px]">2</span>
                <span>Tap Chrome's <strong>Menu (⋮)</strong> &rarr; Select <strong>"Install app"</strong> or <strong>"Add to Home Screen"</strong>.</span>
              </div>
              <div className="flex items-start gap-2.5 text-xs text-zinc-300">
                <span className="w-5 h-5 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold shrink-0 text-[11px]">3</span>
                <span>Android automatically generates the full standalone launcher icon, runs in immersive fullscreen, enables the camera for QR scanning, and operates offline!</span>
              </div>
            </div>
          </div>

          {/* Method 2: Compile Standalone Native APK using Capacitor */}
          <div className="bg-zinc-950/40 border border-zinc-800 rounded-xl p-4.5 space-y-3.5">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-zinc-200 font-bold text-sm">
                <Terminal className="w-4 h-4 text-amber-400" />
                <span>Method 2: Compile Native Android .APK (Capacitor / Android Studio)</span>
              </div>
              <span className="text-[11px] bg-zinc-800 text-zinc-400 font-mono px-2 py-0.5 rounded-full">
                Developer Build
              </span>
            </div>
            <p className="text-xs text-zinc-400 leading-relaxed">
              If you need a signed `.apk` file for MDM (Mobile Device Management) enterprise deployment or offline sideloading on dedicated Zebra/Honeywell barcode hardware:
            </p>

            <div className="space-y-3">
              {/* Step 1: Download code */}
              <div className="space-y-1.5">
                <div className="text-xs font-semibold text-zinc-300 flex items-center justify-between">
                  <span>Step 1: Download pre-configured Android Studio project</span>
                  <a
                    href="/gatekeeper-android-package.zip"
                    download="XV-Digital-Access-Control-Android-APK-Project.zip"
                    className="inline-flex items-center gap-1.5 text-xs font-bold text-emerald-400 hover:text-emerald-300 bg-emerald-950/60 border border-emerald-500/40 px-3 py-1.5 rounded-lg transition-colors"
                  >
                    <Download className="w-3.5 h-3.5" />
                    <span>Download Android Studio ZIP (Direct)</span>
                  </a>
                </div>
              </div>

              {/* Step 2: One-command build */}
              <div className="space-y-1.5">
                <div className="text-xs font-semibold text-zinc-300 flex items-center justify-between">
                  <span>Step 2: Generate Android Studio project with Capacitor</span>
                  <button
                    onClick={() =>
                      copyToClipboard(
                        `npm install @capacitor/core @capacitor/cli @capacitor/android
npx cap init "XV DIGITAL ACCESS CONTROL" "com.teamxv.gatekeeper" --web-dir dist
npm run build
npx cap add android
npx cap open android`,
                        'cap_cmd'
                      )
                    }
                    className="inline-flex items-center gap-1 text-[11px] text-zinc-400 hover:text-zinc-200 cursor-pointer"
                  >
                    {copiedCmd === 'cap_cmd' ? (
                      <>
                        <Check className="w-3 h-3 text-emerald-400" />
                        <span className="text-emerald-400">Copied</span>
                      </>
                    ) : (
                      <>
                        <Copy className="w-3 h-3" />
                        <span>Copy Commands</span>
                      </>
                    )}
                  </button>
                </div>
                <pre className="p-3 bg-black/60 rounded-lg text-emerald-400 font-mono text-xs overflow-x-auto border border-zinc-800/80 leading-relaxed">
{`npm install @capacitor/core @capacitor/cli @capacitor/android
npx cap init "XV DIGITAL ACCESS CONTROL" "com.teamxv.gatekeeper" --web-dir dist
npm run build
npx cap add android
npx cap open android`}
                </pre>
              </div>

              {/* Step 3: Android Studio */}
              <div className="text-xs text-zinc-400 flex items-start gap-2 bg-zinc-900/60 p-2.5 rounded-lg border border-zinc-800/60">
                <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0 mt-0.5" />
                <span>
                  In Android Studio, click <strong>Build &rarr; Build Bundle(s) / APK(s) &rarr; Build APK(s)</strong>. The output APK will be placed in <code className="text-zinc-200 font-mono bg-zinc-800 px-1 py-0.5 rounded">android/app/build/outputs/apk/debug/app-debug.apk</code>.
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="px-5 py-3.5 border-t border-zinc-800 bg-zinc-950/80 flex items-center justify-between text-xs text-zinc-400">
          <div className="flex items-center gap-1.5">
            <Shield className="w-4 h-4 text-emerald-400" />
            <span>Military-Grade Offline Access Control Terminal</span>
          </div>
          <button
            onClick={onClose}
            className="px-4 py-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 text-zinc-200 font-semibold transition-colors cursor-pointer"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
