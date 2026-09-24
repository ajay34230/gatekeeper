import React, { useEffect, useState, useRef } from 'react';
import QRCode from 'qrcode';
import {
  X,
  Printer,
  Download,
  Copy,
  Check,
  ShieldAlert,
  QrCode as QrIcon,
  Building2,
  Phone,
  Home,
  CreditCard,
  Hash,
  Award,
} from 'lucide-react';
import { Personnel, MilitaryCompany } from '../types';
import { COMPANY_THEME } from '../utils/excelExport';

interface MilitaryIdCardModalProps {
  person: Personnel | null;
  isOpen: boolean;
  onClose: () => void;
}

export const MilitaryIdCardModal: React.FC<MilitaryIdCardModalProps> = ({
  person,
  isOpen,
  onClose,
}) => {
  const [qrDataUrl, setQrDataUrl] = useState<string>('');
  const [copied, setCopied] = useState(false);
  const cardRef = useRef<HTMLDivElement>(null);

  const company: MilitaryCompany = person?.company || 'Alpha';
  const theme = COMPANY_THEME[company] || COMPANY_THEME.Alpha;
  const secretKey = person?.secretCode || `SEC-${person?.id?.replace('-', '') || 'P000'}-${company.toUpperCase()}`;
  const armyNumber = person?.armyNumber || person?.serviceNumber || person?.id || 'ARMY-000000';
  const idCardNumber = person?.idCardNumber || `IC-${armyNumber.replace(/[^0-9]/g, '') || '999999'}-IND`;

  useEffect(() => {
    if (!person || !isOpen) return;

    // Generate high-resolution QR code containing secret key and digital identification string
    // Format: "P-001|Location 07|SEC-P001-ALPHA" or pure secret code for gatekeeper scan verification
    const qrPayload = person.secretCode || `${person.id}|${person.accessLocations?.[0] || 'Location 07'}`;

    QRCode.toDataURL(qrPayload, {
      width: 320,
      margin: 1,
      color: {
        dark: '#0F172A',
        light: '#FFFFFF',
      },
      errorCorrectionLevel: 'H',
    })
      .then((url) => setQrDataUrl(url))
      .catch((err) => console.error('Failed to generate QR:', err));
  }, [person, isOpen]);

  if (!isOpen || !person) return null;

  const handleCopySecret = () => {
    navigator.clipboard.writeText(secretKey);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handlePrint = () => {
    window.print();
  };

  const handleDownload = () => {
    if (!cardRef.current) return;
    // Simple print/save trigger
    window.print();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm overflow-y-auto animate-fadeIn">
      {/* Modal Card Wrapper */}
      <div className="relative w-full max-w-2xl bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl overflow-hidden my-8">
        {/* Header Bar */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-800/80 border-b border-slate-700">
          <div className="flex items-center space-x-3">
            <div className="w-9 h-9 rounded-lg bg-amber-500/20 text-amber-400 flex items-center justify-center border border-amber-500/30">
              <ShieldAlert className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-white tracking-wide flex items-center gap-2">
                Official Military ID Card
                <span
                  className="text-xs px-2 py-0.5 rounded-full font-semibold"
                  style={{ backgroundColor: `${theme.color}25`, color: theme.color }}
                >
                  {company} Company
                </span>
              </h2>
              <p className="text-xs text-slate-400">
                Armed Forces Defense Network • Authorized Gate Access Credential
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-white rounded-lg hover:bg-slate-700/50 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 space-y-6">
          {/* Printable / Displayable ID Card */}
          <div
            id="printable-id-card"
            ref={cardRef}
            className="relative mx-auto w-full max-w-lg rounded-2xl overflow-hidden shadow-2xl border-2 transition-all"
            style={{
              borderColor: theme.color,
              background: 'linear-gradient(135deg, #0F172A 0%, #1E293B 50%, #0F172A 100%)',
            }}
          >
            {/* Top Banner with Company Color Ribbon */}
            <div
              className="px-5 py-3 text-white flex items-center justify-between border-b"
              style={{
                backgroundColor: theme.color,
                borderColor: 'rgba(255,255,255,0.2)',
              }}
            >
              <div className="flex items-center space-x-2">
                <span className="text-lg font-black tracking-wider">DEFENSE IDENTITY CARD</span>
              </div>
              <div className="text-right">
                <span className="text-[10px] font-bold tracking-widest uppercase bg-black/30 px-2 py-0.5 rounded">
                  {company} COY
                </span>
              </div>
            </div>

            {/* Sub-header text */}
            <div className="px-5 py-1.5 bg-slate-950/70 border-b border-slate-800 flex justify-between items-center text-[10px] text-slate-300">
              <span className="font-mono tracking-wider">PERIMETER SECURITY PASS</span>
              <span className="font-semibold text-amber-400">CLASS: SECRET // ARMED FORCES</span>
            </div>

            {/* Main ID Card Content */}
            <div className="p-5 space-y-4">
              {/* Profile Row: Photo, Personal Details & QR */}
              <div className="grid grid-cols-12 gap-4 items-center">
                {/* Photo Column (3 cols) */}
                <div className="col-span-4 sm:col-span-3 flex flex-col items-center">
                  <div
                    className="w-24 h-28 rounded-xl overflow-hidden border-2 shadow-md bg-slate-800 relative group"
                    style={{ borderColor: theme.color }}
                  >
                    <img
                      src={
                        person.photoUrl ||
                        `https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=240&auto=format&fit=crop&q=80`
                      }
                      alt={person.name}
                      referrerPolicy="no-referrer"
                      className="w-full h-full object-cover"
                    />
                    <div className="absolute bottom-0 inset-x-0 bg-black/70 text-[9px] text-center text-slate-200 py-0.5 font-bold uppercase tracking-wider">
                      {person.rank || 'Sepoy'}
                    </div>
                  </div>
                  <span className="mt-2 text-[10px] font-mono font-bold text-slate-400">
                    {person.id}
                  </span>
                </div>

                {/* Info Column (5 cols) */}
                <div className="col-span-8 sm:col-span-5 space-y-1.5 text-left">
                  <div>
                    <span className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold block">
                      Army Number
                    </span>
                    <span className="font-mono text-base font-extrabold text-amber-400 tracking-wider">
                      {armyNumber}
                    </span>
                  </div>

                  <div>
                    <span className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold block">
                      Rank & Full Name
                    </span>
                    <h3 className="text-sm font-bold text-white leading-tight">
                      {person.rank ? `${person.rank} ` : ''}{person.name}
                    </h3>
                  </div>

                  <div>
                    <span className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold block">
                      Company & Unit
                    </span>
                    <p className="text-xs text-slate-200 font-semibold">
                      {company} Company • {person.unit || 'Base Support'}
                    </p>
                  </div>

                  <div>
                    <span className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold block">
                      I-Card Number
                    </span>
                    <span className="font-mono text-xs font-bold text-sky-400">
                      {idCardNumber}
                    </span>
                  </div>
                </div>

                {/* QR Code Column (4 cols) */}
                <div className="col-span-12 sm:col-span-4 flex flex-col items-center justify-center p-2 rounded-xl bg-white/5 border border-slate-700/60 text-center">
                  <div className="p-1.5 bg-white rounded-lg shadow-inner">
                    {qrDataUrl ? (
                      <img
                        src={qrDataUrl}
                        alt="Security QR Code"
                        className="w-24 h-24 sm:w-28 sm:h-28 object-contain"
                      />
                    ) : (
                      <div className="w-24 h-24 flex items-center justify-center text-slate-800">
                        <QrIcon className="w-8 h-8 animate-spin" />
                      </div>
                    )}
                  </div>
                  <span className="mt-1.5 text-[9px] font-mono font-bold text-amber-300 tracking-tight">
                    {secretKey}
                  </span>
                  <span className="text-[8px] text-slate-400 tracking-tighter">
                    SCAN AT GATE FOR AUTO ENTRY
                  </span>
                </div>
              </div>

              {/* Extended Details Grid */}
              <div className="pt-3 border-t border-slate-800 grid grid-cols-2 sm:grid-cols-3 gap-2 text-xs">
                <div className="flex items-start space-x-1.5">
                  <Home className="w-3.5 h-3.5 text-slate-400 shrink-0 mt-0.5" />
                  <div>
                    <span className="text-[9px] text-slate-400 uppercase block">Address / Qtr</span>
                    <span className="text-slate-200 text-[11px] font-medium leading-tight line-clamp-1">
                      {person.address || 'Garrison Quarters'}
                    </span>
                  </div>
                </div>

                <div className="flex items-start space-x-1.5">
                  <Phone className="w-3.5 h-3.5 text-slate-400 shrink-0 mt-0.5" />
                  <div>
                    <span className="text-[9px] text-slate-400 uppercase block">Mobile Number</span>
                    <span className="text-slate-200 text-[11px] font-mono font-medium">
                      {person.mobileNumber || '+91 98000 00000'}
                    </span>
                  </div>
                </div>

                <div className="flex items-start space-x-1.5">
                  <Phone className="w-3.5 h-3.5 text-slate-400 shrink-0 mt-0.5" />
                  <div>
                    <span className="text-[9px] text-slate-400 uppercase block">Alt Contact</span>
                    <span className="text-slate-200 text-[11px] font-mono font-medium">
                      {person.altMobileNumber || '+91 98000 00001'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Custom Attributes Preview if available */}
              {person.customCells && person.customCells.length > 0 && (
                <div className="pt-2 border-t border-slate-800 flex flex-wrap gap-1.5">
                  {person.customCells.map((cell) => (
                    <span
                      key={cell.id}
                      className="px-2 py-0.5 text-[10px] rounded bg-slate-800/90 text-slate-300 border border-slate-700"
                    >
                      <strong className="text-amber-400">{cell.label}:</strong> {cell.value}
                    </span>
                  ))}
                </div>
              )}

              {/* Security Bottom Watermark */}
              <div className="pt-2 border-t border-slate-800/80 flex items-center justify-between text-[8px] text-slate-500 uppercase tracking-widest font-mono">
                <span>AUTHENTICATED BY DEFENSE COMMAND</span>
                <span>CHIP-COMPLIANT QR ID</span>
              </div>
            </div>
          </div>

          {/* Quick Explanatory Banner */}
          <div className="p-3.5 rounded-xl bg-slate-800/50 border border-slate-700/60 flex items-center justify-between text-xs text-slate-300">
            <div className="flex items-center space-x-2.5">
              <QrIcon className="w-4 h-4 text-amber-400 shrink-0" />
              <span>
                <strong>Secret Key embedded in QR:</strong>{' '}
                <code className="px-1.5 py-0.5 rounded bg-slate-900 text-amber-300 font-mono text-[11px]">
                  {secretKey}
                </code>
              </span>
            </div>
            <button
              onClick={handleCopySecret}
              className="flex items-center space-x-1 px-2.5 py-1 rounded bg-slate-700 hover:bg-slate-600 text-white font-medium text-xs transition-colors"
            >
              {copied ? (
                <>
                  <Check className="w-3.5 h-3.5 text-emerald-400" />
                  <span className="text-emerald-400">Copied!</span>
                </>
              ) : (
                <>
                  <Copy className="w-3.5 h-3.5" />
                  <span>Copy Key</span>
                </>
              )}
            </button>
          </div>
        </div>

        {/* Modal Action Bar */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-800/90 border-t border-slate-700">
          <span className="text-xs text-slate-400">
            Press Print to print on standard card stock or PDF.
          </span>
          <div className="flex items-center space-x-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-slate-300 hover:text-white rounded-xl hover:bg-slate-700 transition-colors"
            >
              Close
            </button>
            <button
              onClick={handlePrint}
              className="flex items-center space-x-2 px-5 py-2 text-sm font-bold text-slate-950 bg-amber-400 hover:bg-amber-300 rounded-xl shadow-lg shadow-amber-400/20 transition-all hover:scale-105"
            >
              <Printer className="w-4 h-4" />
              <span>Print ID Card</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
