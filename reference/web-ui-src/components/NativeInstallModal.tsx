import React, { useState, useEffect } from 'react';
import {
  Monitor,
  Smartphone,
  Wifi,
  Cloud,
  QrCode,
  Copy,
  Check,
  Download,
  Terminal,
  Shield,
  ExternalLink,
  Radio,
  Zap,
  HardDrive,
  RefreshCw,
  Info,
  CheckCircle2,
  FolderDown,
  X,
} from 'lucide-react';
import { fetchServerNetworkInfo, NetworkHostInfo, pingServerEndpoint } from '../utils/networkSync';

interface NativeInstallModalProps {
  isOpen: boolean;
  onClose: () => void;
  serverPort?: number;
}

export const NativeInstallModal: React.FC<NativeInstallModalProps> = ({
  isOpen,
  onClose,
  serverPort = 3000,
}) => {
  const [activeTab, setActiveTab] = useState<'wifi_sync' | 'windows_exe' | 'android_apk'>('wifi_sync');
  const [networkInfo, setNetworkInfo] = useState<NetworkHostInfo | null>(null);
  const [loadingNet, setLoadingNet] = useState(false);
  const [copiedUrl, setCopiedUrl] = useState(false);
  const [pingStatus, setPingStatus] = useState<{ testing: boolean; result?: string; success?: boolean }>({
    testing: false,
  });
  const [deferredPrompt, setDeferredPrompt] = useState<any>(null);
  const [installSuccess, setInstallSuccess] = useState(false);
  const [downloadingFile, setDownloadingFile] = useState<string | null>(null);
  const [downloadSuccess, setDownloadSuccess] = useState<string | null>(null);

  const handleTriggerDownload = async (url: string, filename: string) => {
    setDownloadingFile(filename);
    try {
      const res = await fetch(url);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const blob = await res.blob();
      const objectUrl = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.style.display = 'none';
      a.href = objectUrl;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      setTimeout(() => {
        document.body.removeChild(a);
        window.URL.revokeObjectURL(objectUrl);
      }, 5000);
      setDownloadSuccess(filename);
      setTimeout(() => setDownloadSuccess(null), 4000);
    } catch (err) {
      console.warn('Direct blob download failed, falling back to window.open:', err);
      window.open(url, '_blank', 'noopener,noreferrer');
    } finally {
      setDownloadingFile(null);
    }
  };

  // Capture PWA install prompt for Android
  useEffect(() => {
    const handlePrompt = (e: Event) => {
      e.preventDefault();
      setDeferredPrompt(e);
    };
    window.addEventListener('beforeinstallprompt', handlePrompt);
    return () => window.removeEventListener('beforeinstallprompt', handlePrompt);
  }, []);

  // Load network info on open
  useEffect(() => {
    if (!isOpen) return;
    setLoadingNet(true);
    fetchServerNetworkInfo()
      .then((info) => {
        setNetworkInfo(info);
      })
      .finally(() => setLoadingNet(false));
  }, [isOpen]);

  if (!isOpen) return null;

  const currentOrigin = typeof window !== 'undefined' ? window.location.origin : `http://localhost:${serverPort}`;
  const localUrl = networkInfo?.fullLocalUrl || currentOrigin;
  const localIp = networkInfo?.localIpv4 || (typeof window !== 'undefined' ? window.location.hostname : '192.168.1.100');

  const handleCopyUrl = () => {
    navigator.clipboard.writeText(localUrl);
    setCopiedUrl(true);
    setTimeout(() => setCopiedUrl(false), 2000);
  };

  const handleTestPing = async () => {
    setPingStatus({ testing: true });
    const res = await pingServerEndpoint(localUrl);
    if (res.ok) {
      setPingStatus({
        testing: false,
        success: true,
        result: `✓ Ping Successful: ${res.latencyMs}ms response from PC server (${localUrl})`,
      });
    } else {
      setPingStatus({
        testing: false,
        success: false,
        result: `Connection test: ${res.error || 'Server unreachable at this address'}`,
      });
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-in fade-in"
      id="modal-native-install"
    >
      <div className="relative w-full max-w-4xl max-h-[92vh] flex flex-col bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-950 border-b border-slate-800">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center text-amber-400">
              <Shield className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-white tracking-wide flex items-center gap-2">
                Native Software & Local Wi-Fi Hub
                <span className="text-xs px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-mono">
                  v2.0.0
                </span>
              </h2>
              <p className="text-xs text-slate-400">
                Installable Windows .EXE installer, Android .APK, and zero-internet Local Wi-Fi pairing
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition-colors"
            id="btn-close-native-modal"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="flex border-b border-slate-800 bg-slate-950/60 px-6 pt-2">
          <button
            onClick={() => setActiveTab('wifi_sync')}
            className={`flex items-center gap-2 px-4 py-3 text-xs font-semibold uppercase tracking-wider border-b-2 transition-all ${
              activeTab === 'wifi_sync'
                ? 'border-amber-500 text-amber-400 bg-slate-900/50'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
            id="tab-btn-wifi-sync"
          >
            <Wifi className="w-4 h-4" />
            Local Wi-Fi Hub & Phone Pairing
          </button>
          <button
            onClick={() => setActiveTab('windows_exe')}
            className={`flex items-center gap-2 px-4 py-3 text-xs font-semibold uppercase tracking-wider border-b-2 transition-all ${
              activeTab === 'windows_exe'
                ? 'border-cyan-500 text-cyan-400 bg-slate-900/50'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
            id="tab-btn-windows-exe"
          >
            <Monitor className="w-4 h-4" />
            Windows PC Native (.EXE)
          </button>
          <button
            onClick={() => setActiveTab('android_apk')}
            className={`flex items-center gap-2 px-4 py-3 text-xs font-semibold uppercase tracking-wider border-b-2 transition-all ${
              activeTab === 'android_apk'
                ? 'border-emerald-500 text-emerald-400 bg-slate-900/50'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
            id="tab-btn-android-apk"
          >
            <Smartphone className="w-4 h-4" />
            Android Mobile App (.APK)
          </button>
        </div>

        {/* Content Area */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* TAB 1: LOCAL WI-FI & PHONE PAIRING */}
          {activeTab === 'wifi_sync' && (
            <div className="space-y-6">
              {/* Status Banner */}
              <div className="p-4 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-start gap-3">
                <Radio className="w-5 h-5 text-amber-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="text-sm font-semibold text-amber-300">
                    Direct Local Network / Wi-Fi Synchronization Active
                  </h4>
                  <p className="text-xs text-slate-300 mt-1 leading-relaxed">
                    Mobile guards on handhelds can synchronize entries/exits directly with this Windows PC over the
                    local Wi-Fi or base LAN. <strong>No cloud internet access is required.</strong> Check-ins appear
                    instantly on this PC Command Center in real-time.
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 items-center">
                {/* QR Code Card */}
                <div className="p-6 rounded-xl bg-slate-950 border border-slate-800 flex flex-col items-center text-center">
                  <div className="text-xs font-mono text-slate-400 mb-3 uppercase tracking-wider flex items-center gap-1.5">
                    <QrCode className="w-4 h-4 text-amber-400" />
                    Mobile Fast-Pairing QR Code
                  </div>

                  {/* QR Code Container */}
                  <div className="p-4 bg-white rounded-xl shadow-lg border border-slate-300 flex items-center justify-center">
                    <img
                      src={`https://api.qrserver.com/v1/create-qr-code/?size=180x180&data=${encodeURIComponent(localUrl)}`}
                      alt="Local Server Pairing QR"
                      className="w-44 h-44"
                      onError={(e) => {
                        // Fallback display if offline from external QR CDN
                        (e.target as HTMLElement).style.display = 'none';
                      }}
                    />
                    <div className="hidden only:block text-slate-900 font-mono text-xs p-4 text-center">
                      Point mobile browser or camera to:<br />
                      <strong>{localUrl}</strong>
                    </div>
                  </div>

                  <p className="text-xs text-slate-400 mt-3 max-w-xs">
                    Point your Android camera or the XV DIGITAL ACCESS CONTROL mobile scanner at this code to link with this PC
                    automatically.
                  </p>
                </div>

                {/* Local Connection Details */}
                <div className="space-y-4">
                  <div>
                    <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider block mb-1.5">
                      PC Command Center Local Address
                    </label>
                    <div className="flex items-center gap-2">
                      <div className="flex-1 px-3.5 py-2.5 bg-slate-950 border border-slate-800 rounded-lg text-amber-300 font-mono text-sm select-all">
                        {localUrl}
                      </div>
                      <button
                        onClick={handleCopyUrl}
                        className="px-3 py-2.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg flex items-center gap-1.5 text-xs font-medium transition-colors"
                        id="btn-copy-local-ip"
                      >
                        {copiedUrl ? <Check className="w-4 h-4 text-emerald-400" /> : <Copy className="w-4 h-4" />}
                        {copiedUrl ? 'Copied' : 'Copy'}
                      </button>
                    </div>
                    <p className="text-[11px] text-slate-500 mt-1">
                      Detected Local IP: <code className="text-slate-300">{localIp}</code> &bull; Port:{' '}
                      <code className="text-slate-300">{serverPort}</code>
                    </p>
                  </div>

                  {/* Ping Test Button */}
                  <div className="p-3.5 rounded-lg bg-slate-950/60 border border-slate-800 space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="text-xs font-medium text-slate-300 flex items-center gap-1.5">
                        <Zap className="w-3.5 h-3.5 text-amber-400" />
                        Handshake Diagnostic Test
                      </span>
                      <button
                        onClick={handleTestPing}
                        disabled={pingStatus.testing}
                        className="px-3 py-1 bg-amber-500/20 hover:bg-amber-500/30 text-amber-300 border border-amber-500/30 rounded text-xs font-medium flex items-center gap-1 transition-colors"
                        id="btn-test-ping-local"
                      >
                        {pingStatus.testing ? <RefreshCw className="w-3 h-3 animate-spin" /> : null}
                        {pingStatus.testing ? 'Testing...' : 'Ping Local Server'}
                      </button>
                    </div>
                    {pingStatus.result && (
                      <div
                        className={`text-xs p-2 rounded border font-mono ${
                          pingStatus.success
                            ? 'bg-emerald-950/40 text-emerald-300 border-emerald-800'
                            : 'bg-rose-950/40 text-rose-300 border-rose-800'
                        }`}
                      >
                        {pingStatus.result}
                      </div>
                    )}
                  </div>

                  {/* Step instructions */}
                  <div className="space-y-2 text-xs text-slate-300">
                    <div className="font-semibold text-slate-200">How to Connect Handheld Mobile Checkpoints:</div>
                    <ol className="list-decimal list-inside space-y-1 text-slate-400">
                      <li>Ensure this PC and the Android device are connected to the same Wi-Fi.</li>
                      <li>On the Android app, tap the <strong>Sync Status / Network</strong> icon.</li>
                      <li>Select <strong>Local PC Wi-Fi</strong> and scan the QR code above or type the IP.</li>
                      <li>All gate entries and exits will now sync to this PC screen instantly.</li>
                    </ol>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* TAB 2: WINDOWS PC NATIVE SOFTWARE (.EXE) */}
          {activeTab === 'windows_exe' && (
            <div className="space-y-6">
              <div className="p-4 rounded-xl bg-cyan-500/10 border border-cyan-500/20 flex items-start gap-3">
                <Monitor className="w-5 h-5 text-cyan-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="text-sm font-semibold text-cyan-300">
                    Windows Desktop Software (.EXE / NSIS Installer)
                  </h4>
                  <p className="text-xs text-slate-300 mt-1 leading-relaxed">
                    Packages into a standard Windows installer (<code>XV-Digital-Access-Control-Setup.exe</code>). It installs
                    directly into <code>Program Files</code> with desktop shortcuts, start menu integration, and native
                    desktop notifications.
                  </p>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="w-8 h-8 rounded-lg bg-cyan-500/10 text-cyan-400 flex items-center justify-center">
                    <HardDrive className="w-4 h-4" />
                  </div>
                  <h5 className="text-sm font-semibold text-white">Standalone Desktop App</h5>
                  <p className="text-xs text-slate-400">
                    No browser tabs or web address bars needed. Runs as a dedicated command station on Windows 10 & 11.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
                    <Wifi className="w-4 h-4" />
                  </div>
                  <h5 className="text-sm font-semibold text-white">Embedded Wi-Fi Server</h5>
                  <p className="text-xs text-slate-400">
                    When you double-click the .EXE, it boots the local server automatically so mobile guards can sync
                    over LAN right away.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="w-8 h-8 rounded-lg bg-amber-500/10 text-amber-400 flex items-center justify-center">
                    <Shield className="w-4 h-4" />
                  </div>
                  <h5 className="text-sm font-semibold text-white">Security Alerts & Audio</h5>
                  <p className="text-xs text-slate-400">
                    Native sound chimes and pop-up Windows notifications when unauthorized gate mismatches are detected.
                  </p>
                </div>
              </div>

              {/* Action Buttons for Windows */}
              <div className="flex flex-wrap items-center gap-3">
                <button
                  onClick={() => handleTriggerDownload('/api/download/windows-setup', 'XV-Digital-Access-Control-PC-Setup.bat')}
                  disabled={downloadingFile !== null}
                  className="px-4 py-2.5 bg-cyan-600 hover:bg-cyan-500 text-white rounded-xl font-medium text-xs flex items-center gap-2 transition-all shadow-lg shadow-cyan-900/20 disabled:opacity-50"
                  id="btn-download-windows-bat"
                >
                  <Download className="w-4 h-4" />
                  {downloadingFile === 'XV-Digital-Access-Control-PC-Setup.bat' ? 'Downloading...' : 'Download Windows Launcher (.BAT)'}
                </button>
                <button
                  onClick={() => handleTriggerDownload('/api/download/project-zip', 'XV-Digital-Access-Control-Full-Source.zip')}
                  disabled={downloadingFile !== null}
                  className="px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-xl font-medium text-xs flex items-center gap-2 transition-all disabled:opacity-50"
                  id="btn-download-full-project-zip"
                >
                  <FolderDown className="w-4 h-4 text-cyan-400" />
                  {downloadingFile === 'XV-Digital-Access-Control-Full-Source.zip' ? 'Downloading...' : 'Download Full PC Source (.ZIP)'}
                </button>
              </div>

              {/* Build Command Box */}
              <div className="p-5 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-mono text-cyan-400 uppercase tracking-wider flex items-center gap-1.5">
                    <Terminal className="w-4 h-4" />
                    Packaging One-Line Command (Windows .EXE)
                  </span>
                  <span className="text-[11px] text-slate-500 font-mono">electron-builder + NSIS</span>
                </div>

                <div className="p-3 bg-black/70 rounded-lg border border-slate-800 font-mono text-xs text-emerald-400 flex items-center justify-between">
                  <code>npm run dist:win</code>
                  <button
                    onClick={() => {
                      navigator.clipboard.writeText('npm run dist:win');
                      setCopiedUrl(true);
                      setTimeout(() => setCopiedUrl(false), 2000);
                    }}
                    className="p-1 hover:text-white transition-colors"
                  >
                    <Copy className="w-4 h-4" />
                  </button>
                </div>

                <div className="text-xs text-slate-400 space-y-1">
                  <p>
                    <strong>Output File:</strong> <code>release/XV-Digital-Access-Control-Setup.exe</code>
                  </p>
                  <p>
                    <strong>Installation:</strong> Double-click the <code>.exe</code> file on any Windows PC. Click Next
                    to install like any standard desktop software.
                  </p>
                </div>
              </div>
            </div>
          )}

          {/* TAB 3: ANDROID MOBILE APP (.APK) */}
          {activeTab === 'android_apk' && (
            <div className="space-y-6">
              <div className="p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/20 flex items-start gap-3">
                <Smartphone className="w-5 h-5 text-emerald-400 shrink-0 mt-0.5" />
                <div>
                  <h4 className="text-sm font-semibold text-emerald-300">
                    Android Mobile Checkpoint App (.APK & WebAPK)
                  </h4>
                  <p className="text-xs text-slate-300 mt-1 leading-relaxed">
                    Native Android checkpoint application with instant QR camera verification, offline buffer vault,
                    and cleartext Wi-Fi networking to sync with the PC Command Center.
                  </p>
                </div>
              </div>

              {/* Direct Download & Install Action Bar */}
              <div className="p-5 rounded-xl bg-slate-950 border border-emerald-500/30 space-y-3">
                <div className="text-xs font-semibold text-emerald-400 uppercase tracking-wider flex items-center gap-1.5">
                  <Download className="w-4 h-4" />
                  Get Android Application Now
                </div>

                <div className="flex flex-wrap items-center gap-3">
                  <button
                    onClick={() =>
                      handleTriggerDownload(
                        '/api/download/android-package',
                        'XV-Digital-Access-Control-Android-APK-Project.zip'
                      )
                    }
                    disabled={downloadingFile !== null}
                    className="px-4 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl font-medium text-xs flex items-center gap-2 transition-all shadow-lg shadow-emerald-900/30 disabled:opacity-50"
                    id="btn-download-android-apk-project"
                  >
                    <Download className="w-4 h-4" />
                    {downloadingFile === 'XV-Digital-Access-Control-Android-APK-Project.zip'
                      ? 'Downloading Package (127 KB)...'
                      : downloadSuccess === 'XV-Digital-Access-Control-Android-APK-Project.zip'
                      ? '✓ Package Downloaded!'
                      : 'Download Android APK Project (.ZIP)'}
                  </button>

                  <a
                    href="/gatekeeper-android-package.zip"
                    target="_blank"
                    rel="noopener noreferrer"
                    download="XV-Digital-Access-Control-Android-APK-Project.zip"
                    className="px-3 py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 rounded-xl font-medium text-xs flex items-center gap-1.5 transition-all"
                    id="btn-direct-tab-android-zip"
                  >
                    <ExternalLink className="w-3.5 h-3.5 text-emerald-400" />
                    Direct Link
                  </a>

                  <button
                    onClick={async () => {
                      if (deferredPrompt) {
                        deferredPrompt.prompt();
                        const { outcome } = await deferredPrompt.userChoice;
                        if (outcome === 'accepted') {
                          setInstallSuccess(true);
                        }
                        setDeferredPrompt(null);
                      } else {
                        alert(
                          'To install the WebAPK on your Android device right now:\n\n1. Open this website in Chrome on your phone.\n2. Tap the ⋮ (three dots) menu in top right.\n3. Tap "Install app" or "Add to Home screen".\n\nAndroid will immediately mint and install the native checkpoint app!'
                        );
                      }
                    }}
                    className="px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-emerald-300 border border-emerald-500/30 rounded-xl font-medium text-xs flex items-center gap-2 transition-all"
                    id="btn-trigger-android-webapk"
                  >
                    <Smartphone className="w-4 h-4 text-emerald-400" />
                    {installSuccess ? 'App Installed!' : 'Install Instantly on Android (WebAPK)'}
                  </button>
                </div>

                <p className="text-[11px] text-slate-400">
                  <strong>Option A (Instant WebAPK):</strong> Installs a real native APK directly onto your Android device
                  via Google Chrome without needing developer mode or sideloading warnings.
                  <br />
                  <strong>Option B (Source APK Project):</strong> Unzip the download and double-click{' '}
                  <code className="text-emerald-300">build_debug_safe.cmd</code> or open in Android Studio to build{' '}
                  <code className="text-emerald-300">app-debug.apk</code> directly on your PC.
                </p>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div className="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
                    <QrCode className="w-4 h-4" />
                  </div>
                  <h5 className="text-sm font-semibold text-white">Full-Speed Camera Reader</h5>
                  <p className="text-xs text-slate-400">
                    Ultra-fast barcode/QR scanning with hardware acceleration and vibration haptics.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="w-8 h-8 rounded-lg bg-amber-500/10 text-amber-400 flex items-center justify-center">
                    <HardDrive className="w-4 h-4" />
                  </div>
                  <h5 className="text-sm font-semibold text-white">Offline Buffer Vault</h5>
                  <p className="text-xs text-slate-400">
                    Guards can scan even during total network blackouts. Scans queue safely and flush once connected to
                    PC or cloud.
                  </p>
                </div>

                <div className="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-2">
                  <div className="w-8 h-8 rounded-lg bg-cyan-500/10 text-cyan-400 flex items-center justify-center">
                    <Wifi className="w-4 h-4" />
                  </div>
                  <h5 className="text-sm font-semibold text-white">LAN Cleartext Traffic Enabled</h5>
                  <p className="text-xs text-slate-400">
                    Allowed to connect directly to <code>http://192.168.x.x:3000</code> without SSL certificate blocks.
                  </p>
                </div>
              </div>

              {/* Build Command Box */}
              <div className="p-5 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-mono text-emerald-400 uppercase tracking-wider flex items-center gap-1.5">
                    <Terminal className="w-4 h-4" />
                    Packaging One-Line Command (Android .APK)
                  </span>
                  <span className="text-[11px] text-slate-500 font-mono">Android Studio / Gradle</span>
                </div>

                <div className="p-3 bg-black/70 rounded-lg border border-slate-800 font-mono text-xs text-emerald-400 flex items-center justify-between">
                  <code>build_debug_safe.cmd</code>
                  <button
                    onClick={() => {
                      navigator.clipboard.writeText('build_debug_safe.cmd');
                      setCopiedUrl(true);
                      setTimeout(() => setCopiedUrl(false), 2000);
                    }}
                    className="p-1 hover:text-white transition-colors"
                  >
                    <Copy className="w-4 h-4" />
                  </button>
                </div>

                <div className="text-xs text-slate-400 space-y-1">
                  <p>
                    <strong>1. Extract:</strong> Unzip <code>XV-Digital-Access-Control-Android-APK-Project.zip</code> on your PC.
                  </p>
                  <p>
                    <strong>2. Produce APK:</strong> Run <code>build_debug_safe.cmd</code> or open the folder in Android Studio and click <strong>Build &gt; Build APK</strong> to generate <code>app-debug.apk</code>.
                  </p>
                  <p>
                    <strong>3. Sideload:</strong> Transfer <code>app-debug.apk</code> to any Android phone, tap to install, and grant camera permission.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-950 border-t border-slate-800 text-xs text-slate-400">
          <div className="flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span>Local PC IP: <strong className="text-slate-200">{localIp}</strong></span>
          </div>
          <button
            onClick={onClose}
            className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-white rounded-lg font-medium transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
