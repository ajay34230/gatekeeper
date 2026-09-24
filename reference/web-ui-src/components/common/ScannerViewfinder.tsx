import React, { useState, useRef, useEffect } from 'react';
import { ArrowLeft, Flashlight, Keyboard, Camera, CheckCircle2 } from 'lucide-react';
import { playScanBeep } from '../../utils/audioFeedback';

interface ScannerViewfinderProps {
  title: string;
  subtitle?: string;
  mode: 'PERSON' | 'VEHICLE' | 'DRIVER' | 'CO_DRIVER' | 'OCCUPANT';
  onScan: (code: string) => void;
  onBack: () => void;
  soundEnabled?: boolean;
}

export const ScannerViewfinder: React.FC<ScannerViewfinderProps> = ({
  title,
  subtitle = 'Align QR code inside the frame',
  mode,
  onScan,
  onBack,
  soundEnabled = true,
}) => {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const [hasCamera, setHasCamera] = useState<boolean>(false);
  const [torchOn, setTorchOn] = useState<boolean>(false);
  const [manualInputOpen, setManualInputOpen] = useState<boolean>(false);
  const [manualCode, setManualCode] = useState<string>('');
  const [scannedFeedback, setScannedFeedback] = useState<boolean>(false);

  // Request actual camera stream if available
  useEffect(() => {
    let stream: MediaStream | null = null;

    async function initCamera() {
      try {
        if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
          stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } },
          });
          if (videoRef.current) {
            videoRef.current.srcObject = stream;
            videoRef.current.play().catch(() => {});
            setHasCamera(true);
          }
        }
      } catch {
        // Camera access denied or not available in iframe sandbox - fallback to simulated viewfinder
        setHasCamera(false);
      }
    }

    initCamera();

    return () => {
      if (stream) {
        stream.getTracks().forEach((track) => track.stop());
      }
    };
  }, []);

  const triggerScan = (code: string) => {
    if (scannedFeedback) return;
    setScannedFeedback(true);
    playScanBeep(soundEnabled);
    setTimeout(() => {
      onScan(code);
    }, 280);
  };

  const handleManualSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (manualCode.trim()) {
      triggerScan(manualCode.trim());
    }
  };

  return (
    <div className="relative flex flex-col h-full w-full bg-zinc-950 text-white select-none overflow-hidden">
      {/* Top Header Bar */}
      <div className="relative z-20 flex items-center justify-between px-4 py-3 bg-zinc-950/80 backdrop-blur-sm border-b border-zinc-900">
        <button
          onClick={onBack}
          className="flex items-center gap-2 p-2 -ml-2 text-zinc-300 hover:text-white rounded-lg active:bg-zinc-800"
          aria-label="Back"
        >
          <ArrowLeft className="w-5 h-5" />
          <span className="text-sm font-medium">Back</span>
        </button>

        <h1 className="text-base font-semibold tracking-tight text-zinc-100">{title}</h1>

        <div className="w-8" />
      </div>

      {/* Main Viewfinder Area */}
      <div className="relative flex-1 flex flex-col items-center justify-center p-6">
        {/* Real Video or Simulated Optical Background */}
        <div className="absolute inset-0 overflow-hidden">
          {hasCamera ? (
            <video
              ref={videoRef}
              playsInline
              muted
              className="w-full h-full object-cover opacity-80"
            />
          ) : (
            <div className="w-full h-full bg-zinc-900/90 flex flex-col items-center justify-center">
              {/* Subtle matrix dots to convey camera sensor waiting */}
              <div className="absolute inset-0 bg-[radial-gradient(#27272a_1px,transparent_1px)] [background-size:16px_16px] opacity-40" />
            </div>
          )}
          {/* Dark Vignette Overlay */}
          <div className="absolute inset-0 bg-black/45" />
        </div>

        {/* QR Reticle / Target Box */}
        <div className="relative z-10 w-64 h-64 sm:w-72 sm:h-72">
          {/* High-Tech Optical HUD Corner Brackets */}
          <div
            className={`absolute inset-0 rounded-2xl transition-all duration-300 pointer-events-none ${
              scannedFeedback
                ? 'border-2 border-emerald-400 shadow-[0_0_30px_rgba(52,211,153,0.8)]'
                : 'border border-zinc-700/60'
            }`}
          >
            {/* Top-Left Bracket */}
            <div className="absolute -top-1 -left-1 w-8 h-8 border-t-3 border-l-3 border-emerald-400 rounded-tl-xl" />
            {/* Top-Right Bracket */}
            <div className="absolute -top-1 -right-1 w-8 h-8 border-t-3 border-r-3 border-emerald-400 rounded-tr-xl" />
            {/* Bottom-Left Bracket */}
            <div className="absolute -bottom-1 -left-1 w-8 h-8 border-b-3 border-l-3 border-emerald-400 rounded-bl-xl" />
            {/* Bottom-Right Bracket */}
            <div className="absolute -bottom-1 -right-1 w-8 h-8 border-b-3 border-r-3 border-emerald-400 rounded-br-xl" />

            {/* Corner Ticks */}
            <div className="absolute top-1/2 -left-2 w-1.5 h-0.5 bg-emerald-400/80" />
            <div className="absolute top-1/2 -right-2 w-1.5 h-0.5 bg-emerald-400/80" />
            <div className="absolute -top-2 left-1/2 w-0.5 h-1.5 bg-emerald-400/80" />
            <div className="absolute -bottom-2 left-1/2 w-0.5 h-1.5 bg-emerald-400/80" />
          </div>

          {/* Animated Laser Sweep Line */}
          {!scannedFeedback && (
            <div className="absolute left-2 right-2 h-0.5 bg-emerald-400 shadow-[0_0_12px_#34d399] animate-scanner-laser pointer-events-none" />
          )}

          {/* Center Crosshair Target */}
          <div className="absolute inset-0 flex items-center justify-center pointer-events-none opacity-30">
            <div className="w-10 h-10 border border-emerald-400 rounded-full flex items-center justify-center">
              <div className="w-1.5 h-1.5 bg-emerald-400 rounded-full" />
            </div>
          </div>

          {/* HUD Status Text in Reticle */}
          <div className="absolute -top-6 left-0 right-0 flex justify-between text-[10px] font-mono text-emerald-400/80 tracking-widest uppercase">
            <span>OPTICAL [SE4710]</span>
            <span>AUTO-FOCUS ON</span>
          </div>

          {/* Success Scan Flash Icon */}
          {scannedFeedback && (
            <div className="absolute inset-0 flex flex-col items-center justify-center bg-emerald-950/70 backdrop-blur-xs rounded-2xl animate-fade-in border-2 border-emerald-400">
              <CheckCircle2 className="w-16 h-16 text-emerald-400 animate-bounce" />
              <span className="text-xs font-mono font-bold text-emerald-300 mt-2 uppercase tracking-widest">
                VERIFIED IDENTIFIER
              </span>
            </div>
          )}
        </div>

        {/* Subtitle instructions */}
        <p className="relative z-10 mt-6 text-xs text-zinc-300 font-medium tracking-wide text-center bg-zinc-900/60 px-3.5 py-1 rounded-full border border-zinc-800 backdrop-blur-xs">
          {subtitle}
        </p>

        {/* Action Controls Bar: Torch, Manual Keyboard, and Cancel */}
        <div className="relative z-10 flex items-center justify-center gap-3 mt-6">
          <button
            onClick={() => setTorchOn(!torchOn)}
            className={`flex items-center gap-1.5 px-4 py-2 rounded-full text-xs font-semibold border transition-all cursor-pointer shadow-xs ${
              torchOn
                ? 'bg-amber-400 text-zinc-950 border-amber-300 shadow-[0_0_12px_rgba(251,191,36,0.5)]'
                : 'bg-zinc-900/90 text-zinc-300 border-zinc-700/80 hover:text-white hover:bg-zinc-800'
            }`}
          >
            <Flashlight className="w-3.5 h-3.5" />
            <span>{torchOn ? 'Torch ON' : 'Torch'}</span>
          </button>

          <button
            onClick={() => setManualInputOpen(true)}
            className="flex items-center gap-1.5 px-4 py-2 rounded-full text-xs font-semibold bg-zinc-900/90 text-zinc-300 border border-zinc-700/80 hover:text-white hover:bg-zinc-800 transition-all cursor-pointer shadow-xs"
          >
            <Keyboard className="w-3.5 h-3.5 text-zinc-400" />
            <span>Manual Keypad</span>
          </button>

          <button
            onClick={onBack}
            className="px-4 py-2 rounded-full text-xs font-semibold bg-zinc-900/90 text-zinc-400 border border-zinc-800 hover:text-zinc-200 hover:bg-zinc-800 transition-all cursor-pointer"
          >
            Cancel
          </button>
        </div>
      </div>

      {/* Physical / Keypad Input Bar */}
      <div className="relative z-20 px-4 py-3 bg-zinc-950 border-t border-zinc-800/90 flex items-center justify-between text-xs text-zinc-400">
        <span className="flex items-center gap-1.5 font-mono text-[11px] text-zinc-400">
          <Camera className="w-3.5 h-3.5 text-emerald-400" />
          <span>Optical Barcode Imager Ready</span>
        </span>
        <button
          onClick={() => setManualInputOpen(true)}
          className="px-3 py-1 rounded-lg bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-zinc-300 text-xs font-semibold flex items-center gap-1.5 transition-colors cursor-pointer"
        >
          <Keyboard className="w-3.5 h-3.5 text-zinc-400" />
          <span>Type ID / QR Code</span>
        </button>
      </div>

      {/* Manual Input Modal */}
      {manualInputOpen && (
        <div className="fixed inset-0 z-50 flex items-end sm:items-center justify-center bg-black/70 backdrop-blur-xs p-4">
          <div className="w-full max-w-sm bg-zinc-900 border border-zinc-800 rounded-2xl p-5 shadow-2xl animate-fade-in">
            <h3 className="text-base font-semibold text-zinc-100 mb-1">Enter Code Manually</h3>
            <p className="text-xs text-zinc-400 mb-4">
              Enter ID or QR payload (e.g. P-001 or P-001|Location 04)
            </p>

            <form onSubmit={handleManualSubmit}>
              <input
                type="text"
                autoFocus
                value={manualCode}
                onChange={(e) => setManualCode(e.target.value)}
                placeholder={mode === 'VEHICLE' ? 'V-014 or V-014|Location 01' : 'P-001 or P-001|Location 04'}
                className="w-full px-3.5 py-3 rounded-lg bg-zinc-950 border border-zinc-700 text-zinc-100 font-mono text-base focus:outline-none focus:border-zinc-400"
              />

              <div className="flex gap-2 mt-4">
                <button
                  type="button"
                  onClick={() => setManualInputOpen(false)}
                  className="flex-1 py-2.5 px-4 rounded-lg bg-zinc-800 text-zinc-300 text-xs font-semibold hover:bg-zinc-700"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!manualCode.trim()}
                  className="flex-1 py-2.5 px-4 rounded-lg bg-zinc-100 text-zinc-900 text-xs font-semibold hover:bg-white disabled:opacity-40"
                >
                  Verify Code
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
