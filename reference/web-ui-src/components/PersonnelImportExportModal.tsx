import React, { useState, useRef } from 'react';
import {
  X,
  FileSpreadsheet,
  Download,
  Upload,
  FileText,
  CheckCircle2,
  AlertCircle,
  FileCode,
  ShieldCheck,
  RefreshCw,
  Copy,
  Layers,
} from 'lucide-react';
import { Personnel, ActivityRecord } from '../types';
import {
  downloadSixCompanyExcel,
  downloadPersonnelFullCsv,
  downloadPersonnelSampleTemplate,
  parsePersonnelImportContent,
  deDuplicateAndMergePersonnel,
  DeDuplicationResult,
  ALL_COMPANIES,
  COMPANY_THEME,
} from '../utils/excelExport';

interface PersonnelImportExportModalProps {
  isOpen: boolean;
  onClose: () => void;
  personnelList: Personnel[];
  activities: ActivityRecord[];
  onImportComplete: (mergedList: Personnel[], stats: DeDuplicationResult) => void;
}

export const PersonnelImportExportModal: React.FC<PersonnelImportExportModalProps> = ({
  isOpen,
  onClose,
  personnelList,
  activities,
  onImportComplete,
}) => {
  const [activeTab, setActiveTab] = useState<'EXPORT' | 'IMPORT'>('EXPORT');
  const [rawImportText, setRawImportText] = useState('');
  const [isParsing, setIsParsing] = useState(false);
  const [previewResult, setPreviewResult] = useState<DeDuplicationResult | null>(null);
  const [importNotice, setImportNotice] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  if (!isOpen) return null;

  // Handle Export 6-Company Multi-Tab Excel
  const handleExportSixCompanyExcel = () => {
    downloadSixCompanyExcel(personnelList, activities);
  };

  // Handle Export Full Personnel CSV
  const handleExportFullCsv = () => {
    downloadPersonnelFullCsv(personnelList);
  };

  // Handle Download Sample Template
  const handleDownloadSample = () => {
    downloadPersonnelSampleTemplate();
  };

  // Process text or file content
  const handleProcessImport = (content: string) => {
    if (!content || !content.trim()) {
      setImportNotice('Please paste or upload valid CSV or tabular data.');
      return;
    }
    setIsParsing(true);
    setImportNotice(null);

    try {
      const parsedRows = parsePersonnelImportContent(content);
      if (parsedRows.length === 0) {
        setImportNotice('Could not parse any valid personnel rows. Check columns format.');
        setIsParsing(false);
        return;
      }

      // Run de-duplication against existing roster
      const result = deDuplicateAndMergePersonnel(personnelList, parsedRows);
      setPreviewResult(result);
      setIsParsing(false);
    } catch (err: any) {
      setImportNotice(`Failed to parse import content: ${err.message}`);
      setIsParsing(false);
    }
  };

  // Handle file drop or selection
  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      const text = event.target?.result as string;
      setRawImportText(text);
      handleProcessImport(text);
    };
    reader.readAsText(file);
  };

  // Commit merged results
  const handleConfirmImport = async () => {
    if (!previewResult) return;

    // Send batch sync to server API
    try {
      await fetch('/api/personnel/batch-import', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(previewResult.mergedList),
      }).catch((err) => console.warn('Server batch import skipped:', err));
    } catch (err) {
      console.warn('Batch import server request failed:', err);
    }

    onImportComplete(previewResult.mergedList, previewResult);
    onClose();
  };

  // Company count breakdown
  const companyCounts = ALL_COMPANIES.map((comp) => {
    const count = personnelList.filter((p) => (p.company || 'Alpha') === comp).length;
    return { comp, count, theme: COMPANY_THEME[comp] };
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm overflow-y-auto animate-fadeIn">
      <div className="relative w-full max-w-3xl bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl overflow-hidden my-6 max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-800 border-b border-slate-700 shrink-0">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center border border-emerald-500/30">
              <FileSpreadsheet className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-white tracking-wide">
                Personnel Data Management (Import &amp; Export)
              </h2>
              <p className="text-xs text-slate-400">
                Multi-Tab Excel Workbook • Anti-Duplicate Import Engine
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-white rounded-lg hover:bg-slate-700 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab Selector */}
        <div className="flex border-b border-slate-800 bg-slate-900/50 px-6 shrink-0">
          <button
            onClick={() => setActiveTab('EXPORT')}
            className={`flex items-center space-x-2 py-3 px-4 font-bold text-xs uppercase tracking-wider border-b-2 transition-all ${
              activeTab === 'EXPORT'
                ? 'border-amber-400 text-amber-400 bg-amber-400/5'
                : 'border-transparent text-slate-400 hover:text-white'
            }`}
          >
            <Download className="w-4 h-4" />
            <span>Export Roster &amp; Excel</span>
          </button>

          <button
            onClick={() => setActiveTab('IMPORT')}
            className={`flex items-center space-x-2 py-3 px-4 font-bold text-xs uppercase tracking-wider border-b-2 transition-all ${
              activeTab === 'IMPORT'
                ? 'border-emerald-400 text-emerald-400 bg-emerald-400/5'
                : 'border-transparent text-slate-400 hover:text-white'
            }`}
          >
            <Upload className="w-4 h-4" />
            <span>Import &amp; De-duplicate</span>
          </button>
        </div>

        {/* Tab Body */}
        <div className="p-6 overflow-y-auto space-y-6">
          {activeTab === 'EXPORT' ? (
            <div className="space-y-6">
              {/* Option 1: 6-Company Multi-Tab Excel */}
              <div className="p-5 rounded-2xl bg-slate-800/50 border border-slate-700/80 hover:border-amber-500/40 transition-all space-y-4">
                <div className="flex items-start justify-between">
                  <div className="space-y-1">
                    <div className="flex items-center space-x-2">
                      <Layers className="w-5 h-5 text-amber-400" />
                      <h3 className="text-base font-bold text-white">
                        Six-Company Multi-Tab Excel Workbook (.xls)
                      </h3>
                      <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-400/20 text-amber-300 border border-amber-400/30">
                        OFFICIAL FORMAT
                      </span>
                    </div>
                    <p className="text-xs text-slate-400 max-w-xl">
                      Generates a true multi-worksheet Microsoft Excel file containing separate tabs for{' '}
                      <strong>Alpha, Bravo, Charlie, Delta, SP, and HQ</strong> companies. Each tab includes complete soldier dossiers and chronological entry/exit gate access records.
                    </p>
                  </div>
                  <button
                    onClick={handleExportSixCompanyExcel}
                    className="flex items-center space-x-2 px-4 py-2.5 bg-amber-400 hover:bg-amber-300 text-slate-950 font-bold text-xs rounded-xl shadow-lg shadow-amber-400/20 transition-all hover:scale-105 shrink-0"
                  >
                    <Download className="w-4 h-4" />
                    <span>Download 6-Tab Excel</span>
                  </button>
                </div>

                {/* Company Breakdown Preview */}
                <div className="pt-3 border-t border-slate-700/60 grid grid-cols-2 sm:grid-cols-6 gap-2 text-center">
                  {companyCounts.map(({ comp, count, theme }) => (
                    <div
                      key={comp}
                      className="p-2 rounded-xl bg-slate-900/60 border border-slate-800"
                    >
                      <span
                        className="text-[10px] font-bold block"
                        style={{ color: theme.badgeBg }}
                      >
                        {comp} Coy Tab
                      </span>
                      <span className="text-sm font-black text-white font-mono">{count}</span>
                      <span className="text-[9px] text-slate-500 block">Soldiers</span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Option 2: Full Personnel Roster CSV */}
              <div className="p-5 rounded-2xl bg-slate-800/50 border border-slate-700/80 hover:border-emerald-500/40 transition-all space-y-3">
                <div className="flex items-start justify-between">
                  <div className="space-y-1">
                    <div className="flex items-center space-x-2">
                      <FileSpreadsheet className="w-5 h-5 text-emerald-400" />
                      <h3 className="text-base font-bold text-white">
                        Full Personnel Master Roster (.csv)
                      </h3>
                    </div>
                    <p className="text-xs text-slate-400 max-w-xl">
                      Exports all {personnelList.length} personnel profiles with Army No, Rank, Name, Company, Unit, Address, Mobile No, Alt Mobile No, I-Card No, Secret Key, and custom attributes.
                    </p>
                  </div>
                  <button
                    onClick={handleExportFullCsv}
                    className="flex items-center space-x-2 px-4 py-2.5 bg-emerald-500 hover:bg-emerald-400 text-slate-950 font-bold text-xs rounded-xl shadow-lg shadow-emerald-500/20 transition-all hover:scale-105 shrink-0"
                  >
                    <Download className="w-4 h-4" />
                    <span>Export CSV Roster</span>
                  </button>
                </div>
              </div>

              {/* Option 3: Download Sample Template */}
              <div className="p-4 rounded-xl bg-slate-800/30 border border-dashed border-slate-700 flex items-center justify-between text-xs text-slate-400">
                <span>
                  Need an Excel / CSV template for importing new personnel? Download the official format template.
                </span>
                <button
                  onClick={handleDownloadSample}
                  className="flex items-center space-x-1 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-sky-400 font-semibold border border-sky-500/30 transition-colors"
                >
                  <FileText className="w-3.5 h-3.5" />
                  <span>Download Sample Template</span>
                </button>
              </div>
            </div>
          ) : (
            <div className="space-y-5">
              {/* Import Upload & Paste Box */}
              <div className="p-4 rounded-2xl bg-slate-800/50 border border-slate-700 space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-sm font-bold text-white flex items-center gap-2">
                      <Upload className="w-4 h-4 text-emerald-400" />
                      Import Personnel Data (CSV / Excel Text)
                    </h3>
                    <p className="text-xs text-slate-400">
                      Upload a file or paste CSV rows below. The system automatically prevents duplicates.
                    </p>
                  </div>
                  <div className="flex items-center space-x-2">
                    <input
                      type="file"
                      ref={fileInputRef}
                      accept=".csv,.txt,.xls,.xml,.json"
                      onChange={handleFileUpload}
                      className="hidden"
                    />
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      className="px-3 py-1.5 bg-slate-700 hover:bg-slate-600 text-white rounded-lg text-xs font-semibold transition-colors"
                    >
                      Browse File (.csv / .xls)
                    </button>
                    <button
                      type="button"
                      onClick={handleDownloadSample}
                      className="text-xs text-sky-400 hover:underline"
                    >
                      Sample Template
                    </button>
                  </div>
                </div>

                {/* Direct Paste Area */}
                <textarea
                  rows={4}
                  value={rawImportText}
                  onChange={(e) => setRawImportText(e.target.value)}
                  placeholder="Paste CSV rows here, e.g.:
Army No,Rank,Name,Company,Unit,Address,Mobile No,Alt Mobile No,I-Card No,Secret Code
ARMY-849201,Major,Johnathan Doe,Alpha,4th Logistics,Qtr 14-B,+91 9810234567,+91 9810234568,IC-849201-IND,SEC-P001-ALPHA"
                  className="w-full p-3 bg-slate-900 border border-slate-700 rounded-xl text-xs font-mono text-slate-200 focus:border-emerald-400 focus:outline-none"
                />

                <div className="flex items-center justify-between">
                  <span className="text-[11px] text-slate-400">
                    Duplicate prevention matches existing soldiers by Army No, I-Card No, or Name + Company.
                  </span>
                  <button
                    type="button"
                    onClick={() => handleProcessImport(rawImportText)}
                    disabled={isParsing || !rawImportText.trim()}
                    className="flex items-center space-x-2 px-4 py-2 bg-emerald-500 hover:bg-emerald-400 disabled:opacity-50 text-slate-950 font-bold text-xs rounded-xl shadow-lg transition-all"
                  >
                    <RefreshCw className={`w-3.5 h-3.5 ${isParsing ? 'animate-spin' : ''}`} />
                    <span>Analyze &amp; Prevent Duplicates</span>
                  </button>
                </div>
              </div>

              {/* Status or Error Notice */}
              {importNotice && (
                <div className="p-3.5 rounded-xl bg-amber-500/10 border border-amber-500/30 text-amber-300 text-xs flex items-center space-x-2">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>{importNotice}</span>
                </div>
              )}

              {/* De-Duplication Live Preview */}
              {previewResult && (
                <div className="p-5 rounded-2xl bg-slate-800/80 border border-emerald-500/40 space-y-4 animate-fadeIn">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-2 text-emerald-400 font-bold text-sm">
                      <ShieldCheck className="w-5 h-5" />
                      <span>De-Duplication Analysis Complete</span>
                    </div>
                    <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                      0 Duplicates Created
                    </span>
                  </div>

                  {/* Summary Metric Counters */}
                  <div className="grid grid-cols-3 gap-3 text-center">
                    <div className="p-3 rounded-xl bg-slate-900 border border-slate-700">
                      <span className="text-xs text-slate-400 block">New Soldiers to Add</span>
                      <span className="text-xl font-extrabold text-emerald-400 font-mono">
                        +{previewResult.addedCount}
                      </span>
                    </div>

                    <div className="p-3 rounded-xl bg-slate-900 border border-slate-700">
                      <span className="text-xs text-slate-400 block">Existing to Update</span>
                      <span className="text-xl font-extrabold text-amber-400 font-mono">
                        {previewResult.updatedCount}
                      </span>
                    </div>

                    <div className="p-3 rounded-xl bg-slate-900 border border-slate-700">
                      <span className="text-xs text-slate-400 block">Duplicates Prevented</span>
                      <span className="text-xl font-extrabold text-sky-400 font-mono">
                        {previewResult.duplicatesPrevented}
                      </span>
                    </div>
                  </div>

                  {/* Log details */}
                  <div className="max-h-36 overflow-y-auto p-3 rounded-xl bg-slate-950/80 border border-slate-800 space-y-1 text-xs font-mono text-slate-300">
                    {previewResult.summaryLogs.map((log, idx) => (
                      <div key={idx} className="flex items-center space-x-2">
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400 shrink-0" />
                        <span className="truncate">{log}</span>
                      </div>
                    ))}
                  </div>

                  {/* Commit button */}
                  <div className="flex justify-end space-x-3 pt-2">
                    <button
                      type="button"
                      onClick={() => setPreviewResult(null)}
                      className="px-4 py-2 text-xs text-slate-400 hover:text-white"
                    >
                      Clear
                    </button>
                    <button
                      type="button"
                      onClick={handleConfirmImport}
                      className="flex items-center space-x-2 px-5 py-2.5 bg-emerald-400 hover:bg-emerald-300 text-slate-950 font-extrabold text-xs rounded-xl shadow-lg shadow-emerald-400/20 transition-all hover:scale-105"
                    >
                      <CheckCircle2 className="w-4 h-4" />
                      <span>Commit &amp; Merge Personnel ({previewResult.mergedList.length} Total)</span>
                    </button>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
