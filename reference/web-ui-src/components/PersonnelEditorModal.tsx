import React, { useState, useEffect } from 'react';
import {
  X,
  UserPlus,
  Plus,
  Trash2,
  Table as TableIcon,
  Shield,
  Save,
  CheckCircle,
  Hash,
  Building2,
  Phone,
  Home,
  CreditCard,
  KeyRound,
  Tag,
} from 'lucide-react';
import { Personnel, MilitaryCompany, CustomFieldCell, CustomDataTable } from '../types';
import { ALL_COMPANIES, COMPANY_THEME } from '../utils/excelExport';

interface PersonnelEditorModalProps {
  isOpen: boolean;
  onClose: () => void;
  personnelToEdit?: Personnel | null;
  onSave: (person: Personnel) => void;
}

const MILITARY_RANKS = [
  'Lieutenant General',
  'Major General',
  'Brigadier',
  'Colonel',
  'Lieutenant Colonel',
  'Major',
  'Captain',
  'Lieutenant',
  'Subedar Major',
  'Subedar',
  'Naib Subedar',
  'Havildar Major',
  'Havildar',
  'Naik',
  'Lance Naik',
  'Sepoy',
  'Civilian Specialist',
];

export const PersonnelEditorModal: React.FC<PersonnelEditorModalProps> = ({
  isOpen,
  onClose,
  personnelToEdit,
  onSave,
}) => {
  const isEditing = Boolean(personnelToEdit);

  // Form states
  const [armyNumber, setArmyNumber] = useState('');
  const [rank, setRank] = useState('Sepoy');
  const [name, setName] = useState('');
  const [company, setCompany] = useState<MilitaryCompany>('Alpha');
  const [unit, setUnit] = useState('Base Support Battalion');
  const [address, setAddress] = useState('');
  const [mobileNumber, setMobileNumber] = useState('');
  const [altMobileNumber, setAltMobileNumber] = useState('');
  const [idCardNumber, setIdCardNumber] = useState('');
  const [secretCode, setSecretCode] = useState('');
  const [role, setRole] = useState('Infantry Guard');
  const [department, setDepartment] = useState('Security Operations');
  const [status, setStatus] = useState<'ACTIVE' | 'SUSPENDED' | 'FLAGGED'>('ACTIVE');

  // Dynamic Custom Cells & Tables
  const [customCells, setCustomCells] = useState<CustomFieldCell[]>([]);
  const [customTables, setCustomTables] = useState<CustomDataTable[]>([]);

  // Feedback
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [savedSuccess, setSavedSuccess] = useState(false);

  useEffect(() => {
    if (personnelToEdit) {
      setArmyNumber(personnelToEdit.armyNumber || personnelToEdit.serviceNumber || '');
      setRank(personnelToEdit.rank || 'Sepoy');
      setName(personnelToEdit.name || '');
      setCompany(personnelToEdit.company || 'Alpha');
      setUnit(personnelToEdit.unit || 'Base Support Battalion');
      setAddress(personnelToEdit.address || '');
      setMobileNumber(personnelToEdit.mobileNumber || '');
      setAltMobileNumber(personnelToEdit.altMobileNumber || '');
      setIdCardNumber(personnelToEdit.idCardNumber || '');
      setSecretCode(personnelToEdit.secretCode || '');
      setRole(personnelToEdit.role || 'Infantry Guard');
      setDepartment(personnelToEdit.department || 'Security Operations');
      setStatus((personnelToEdit.status as any) || 'ACTIVE');
      setCustomCells(personnelToEdit.customCells || []);
      setCustomTables(personnelToEdit.customTables || []);
    } else {
      // Defaults for brand new soldier
      const randomArmy = `ARMY-${Math.floor(100000 + Math.random() * 900000)}`;
      setArmyNumber(randomArmy);
      setRank('Sepoy');
      setName('');
      setCompany('Alpha');
      setUnit('4th Logistics Support Bn');
      setAddress('Garrison Quarters Block B');
      setMobileNumber('+91 98');
      setAltMobileNumber('+91 98');
      setIdCardNumber(`IC-${Math.floor(100000 + Math.random() * 900000)}-IND`);
      setSecretCode(`SEC-${randomArmy.replace('-', '')}-ALPHA`);
      setRole('Infantry Guard');
      setDepartment('Perimeter Security');
      setStatus('ACTIVE');
      setCustomCells([
        { id: 'blood-group', label: 'Blood Group', value: 'O+ Positive' },
        { id: 'weapon-issued', label: 'Weapon Issued', value: 'INSAS 5.56 Rifle' },
      ]);
      setCustomTables([]);
    }
    setSavedSuccess(false);
  }, [personnelToEdit, isOpen]);

  // Keep secret code in sync with company & army number if user hasn't typed custom code
  const handleCompanyChange = (newCompany: MilitaryCompany) => {
    setCompany(newCompany);
    if (!secretCode || secretCode.startsWith('SEC-')) {
      const cleanArmy = (armyNumber || '000').replace(/[^0-9A-Za-z]/g, '');
      setSecretCode(`SEC-${cleanArmy}-${newCompany.toUpperCase()}`);
    }
  };

  const handleArmyNoChange = (newArmy: string) => {
    setArmyNumber(newArmy);
    if (!secretCode || secretCode.startsWith('SEC-')) {
      const cleanArmy = newArmy.replace(/[^0-9A-Za-z]/g, '');
      setSecretCode(`SEC-${cleanArmy}-${company.toUpperCase()}`);
    }
  };

  // Add custom cell
  const addCustomCell = () => {
    setCustomCells([
      ...customCells,
      {
        id: `cell-${Date.now()}`,
        label: 'New Detail',
        value: '',
      },
    ]);
  };

  const updateCustomCell = (index: number, field: 'label' | 'value', text: string) => {
    const updated = [...customCells];
    updated[index][field] = text;
    setCustomCells(updated);
  };

  const removeCustomCell = (index: number) => {
    setCustomCells(customCells.filter((_, i) => i !== index));
  };

  // Add custom table
  const addCustomTable = () => {
    setCustomTables([
      ...customTables,
      {
        id: `tbl-${Date.now()}`,
        title: 'Qualifications & Deployments',
        columns: ['Year', 'Deployment / Course', 'Result / Grade'],
        rows: [{ Year: '2024', 'Deployment / Course': 'High Altitude Warfare', 'Result / Grade': 'Qualified AX' }],
      },
    ]);
  };

  const removeCustomTable = (index: number) => {
    setCustomTables(customTables.filter((_, i) => i !== index));
  };

  const addTableRow = (tableIndex: number) => {
    const updated = [...customTables];
    const table = updated[tableIndex];
    const emptyRow: Record<string, string> = {};
    table.columns.forEach((col) => {
      emptyRow[col] = '';
    });
    table.rows.push(emptyRow);
    setCustomTables(updated);
  };

  const updateTableCell = (
    tableIndex: number,
    rowIndex: number,
    column: string,
    val: string
  ) => {
    const updated = [...customTables];
    updated[tableIndex].rows[rowIndex][column] = val;
    setCustomTables(updated);
  };

  const addTableColumn = (tableIndex: number) => {
    const colName = prompt('Enter new column header name:');
    if (!colName || !colName.trim()) return;
    const updated = [...customTables];
    const table = updated[tableIndex];
    table.columns.push(colName.trim());
    table.rows.forEach((r) => {
      r[colName.trim()] = '';
    });
    setCustomTables(updated);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      alert('Please enter soldier full name.');
      return;
    }

    setIsSubmitting(true);
    const targetId = personnelToEdit?.id || `P-${Math.floor(100 + Math.random() * 900)}`;

    const newPersonnel: Personnel = {
      id: targetId,
      armyNumber: armyNumber.trim() || `ARMY-${Math.floor(100000 + Math.random() * 900000)}`,
      serviceNumber: armyNumber.trim() || `ARMY-${Math.floor(100000 + Math.random() * 900000)}`,
      rank: rank || 'Sepoy',
      name: name.trim(),
      company,
      unit: unit.trim(),
      address: address.trim(),
      mobileNumber: mobileNumber.trim(),
      altMobileNumber: altMobileNumber.trim(),
      idCardNumber: idCardNumber.trim() || `IC-${targetId}-IND`,
      secretCode: secretCode.trim() || `SEC-${targetId}-${company.toUpperCase()}`,
      role: role.trim() || 'Infantry Guard',
      department: department.trim() || 'Security',
      status: status as any,
      currentStatus: personnelToEdit?.currentStatus || 'OUTSIDE',
      photoUrl:
        personnelToEdit?.photoUrl ||
        `https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=240&auto=format&fit=crop&q=80`,
      accessLocations: personnelToEdit?.accessLocations || ['Location 07'],
      customCells: customCells.filter((c) => c.label.trim() && c.value.trim()),
      customTables: customTables.filter((t) => t.title.trim()),
    };

    try {
      // Sync with server API
      await fetch('/api/personnel', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(newPersonnel),
      }).catch((err) => console.warn('Server sync skipped in offline mode:', err));

      onSave(newPersonnel);
      setSavedSuccess(true);
      setTimeout(() => {
        setIsSubmitting(false);
        onClose();
      }, 500);
    } catch (err) {
      console.error('Error saving personnel:', err);
      onSave(newPersonnel);
      setIsSubmitting(false);
      onClose();
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm overflow-y-auto animate-fadeIn">
      <div className="relative w-full max-w-3xl bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl overflow-hidden my-6 max-h-[92vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 bg-slate-800 border-b border-slate-700 shrink-0">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-xl bg-amber-500/20 text-amber-400 flex items-center justify-center border border-amber-500/30">
              <UserPlus className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-white tracking-wide">
                {isEditing ? `Edit Personnel: ${personnelToEdit?.name}` : 'Add New Soldier Details'}
              </h2>
              <p className="text-xs text-slate-400">
                Military Personnel Dossier &amp; ID Card Configuration
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

        {/* Scrollable Form Content */}
        <form onSubmit={handleSubmit} className="p-6 overflow-y-auto space-y-6 text-sm text-slate-200">
          {/* Section 1: Military Identification */}
          <div>
            <div className="flex items-center space-x-2 text-amber-400 font-bold text-xs uppercase tracking-wider mb-3">
              <Shield className="w-4 h-4" />
              <span>Official Military Identification</span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              {/* Army No */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Army Number *
                </label>
                <div className="relative">
                  <Hash className="absolute left-3 top-2.5 w-4 h-4 text-slate-500" />
                  <input
                    type="text"
                    required
                    value={armyNumber}
                    onChange={(e) => handleArmyNoChange(e.target.value)}
                    placeholder="e.g. ARMY-849201"
                    className="w-full pl-9 pr-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-mono focus:border-amber-400 focus:outline-none"
                  />
                </div>
              </div>

              {/* Rank */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Rank *
                </label>
                <select
                  value={rank}
                  onChange={(e) => setRank(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white focus:border-amber-400 focus:outline-none"
                >
                  {MILITARY_RANKS.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
              </div>

              {/* Full Name */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Full Name *
                </label>
                <input
                  type="text"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="e.g. Johnathan Doe"
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-medium focus:border-amber-400 focus:outline-none"
                />
              </div>

              {/* Company Selection - 6 Companies */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Assigned Company * (Excel Tab)
                </label>
                <select
                  value={company}
                  onChange={(e) => handleCompanyChange(e.target.value as MilitaryCompany)}
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-bold focus:border-amber-400 focus:outline-none"
                >
                  {ALL_COMPANIES.map((c) => (
                    <option key={c} value={c}>
                      {c} Company ({COMPANY_THEME[c].fullTitle})
                    </option>
                  ))}
                </select>
              </div>

              {/* Unit / Battalion */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Unit / Battalion
                </label>
                <div className="relative">
                  <Building2 className="absolute left-3 top-2.5 w-4 h-4 text-slate-500" />
                  <input
                    type="text"
                    value={unit}
                    onChange={(e) => setUnit(e.target.value)}
                    placeholder="e.g. 4th Logistics Support Bn"
                    className="w-full pl-9 pr-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white focus:border-amber-400 focus:outline-none"
                  />
                </div>
              </div>

              {/* I-Card Number */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  I-Card Number
                </label>
                <div className="relative">
                  <CreditCard className="absolute left-3 top-2.5 w-4 h-4 text-slate-500" />
                  <input
                    type="text"
                    value={idCardNumber}
                    onChange={(e) => setIdCardNumber(e.target.value)}
                    placeholder="e.g. IC-849201-IND"
                    className="w-full pl-9 pr-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-mono focus:border-amber-400 focus:outline-none"
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Section 2: Contact & Personal Details */}
          <div>
            <div className="flex items-center space-x-2 text-sky-400 font-bold text-xs uppercase tracking-wider mb-3">
              <Phone className="w-4 h-4" />
              <span>Contact &amp; Garrison Details</span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              {/* Address */}
              <div className="sm:col-span-3">
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Address (Residential / Garrison Quarters)
                </label>
                <div className="relative">
                  <Home className="absolute left-3 top-2.5 w-4 h-4 text-slate-500" />
                  <input
                    type="text"
                    value={address}
                    onChange={(e) => setAddress(e.target.value)}
                    placeholder="e.g. Quarter 14-B, Officers Enclave, Base Alpha"
                    className="w-full pl-9 pr-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white focus:border-sky-400 focus:outline-none"
                  />
                </div>
              </div>

              {/* Mobile Number */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Mobile Number (Primary)
                </label>
                <input
                  type="text"
                  value={mobileNumber}
                  onChange={(e) => setMobileNumber(e.target.value)}
                  placeholder="+91 98102 34567"
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-mono focus:border-sky-400 focus:outline-none"
                />
              </div>

              {/* Alt Mobile Number */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Alternative Mobile Number
                </label>
                <input
                  type="text"
                  value={altMobileNumber}
                  onChange={(e) => setAltMobileNumber(e.target.value)}
                  placeholder="+91 98102 34568"
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-mono focus:border-sky-400 focus:outline-none"
                />
              </div>

              {/* Status */}
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Service Status
                </label>
                <select
                  value={status}
                  onChange={(e) => setStatus(e.target.value as any)}
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white font-bold focus:border-sky-400 focus:outline-none"
                >
                  <option value="ACTIVE">ACTIVE (Authorized Access)</option>
                  <option value="SUSPENDED">SUSPENDED (Restricted)</option>
                  <option value="FLAGGED">FLAGGED (Alert Gatekeeper)</option>
                </select>
              </div>
            </div>
          </div>

          {/* Section 3: QR Code & Secret Key */}
          <div>
            <div className="flex items-center space-x-2 text-emerald-400 font-bold text-xs uppercase tracking-wider mb-3">
              <KeyRound className="w-4 h-4" />
              <span>QR Code &amp; Secret Key Authentication</span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  QR Secret Key (Auto-Generated or Custom)
                </label>
                <div className="relative">
                  <Tag className="absolute left-3 top-2.5 w-4 h-4 text-emerald-400" />
                  <input
                    type="text"
                    value={secretCode}
                    onChange={(e) => setSecretCode(e.target.value)}
                    placeholder="e.g. SEC-P001-ALPHA"
                    className="w-full pl-9 pr-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-emerald-300 font-mono font-bold focus:border-emerald-400 focus:outline-none"
                  />
                </div>
                <p className="mt-1 text-[11px] text-slate-400">
                  This key will be encoded in the soldier's printed QR ID Card and automatically scanned by the mobile gatekeeper app.
                </p>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-400 mb-1">
                  Role / Designation
                </label>
                <input
                  type="text"
                  value={role}
                  onChange={(e) => setRole(e.target.value)}
                  placeholder="e.g. Logistics Team Lead"
                  className="w-full px-3 py-2 bg-slate-800/90 border border-slate-700 rounded-xl text-white focus:border-emerald-400 focus:outline-none"
                />
              </div>
            </div>
          </div>

          {/* Section 4: Dynamic Custom Cells (Option to add more cells) */}
          <div className="pt-2 border-t border-slate-800">
            <div className="flex items-center justify-between mb-3">
              <div>
                <h4 className="text-xs font-bold text-amber-300 uppercase tracking-wider flex items-center gap-1.5">
                  <Tag className="w-4 h-4" />
                  Dynamic Custom Cells &amp; Attributes
                </h4>
                <p className="text-[11px] text-slate-400">
                  Add custom personal details (Blood Group, Weapon Issued, Trade, Next of Kin, etc.)
                </p>
              </div>
              <button
                type="button"
                onClick={addCustomCell}
                className="flex items-center space-x-1 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-amber-400 hover:text-amber-300 rounded-lg text-xs font-semibold border border-amber-500/30 transition-colors"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>+ Add Cell</span>
              </button>
            </div>

            {customCells.length === 0 ? (
              <p className="text-xs text-slate-500 italic py-2">
                No custom cells added. Click "+ Add Cell" to add custom attributes.
              </p>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {customCells.map((cell, idx) => (
                  <div
                    key={cell.id || idx}
                    className="flex items-center space-x-2 p-2 rounded-xl bg-slate-800/50 border border-slate-700/80"
                  >
                    <input
                      type="text"
                      value={cell.label}
                      onChange={(e) => updateCustomCell(idx, 'label', e.target.value)}
                      placeholder="Label (e.g. Blood Group)"
                      className="w-1/2 px-2.5 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-amber-300 font-semibold focus:outline-none"
                    />
                    <input
                      type="text"
                      value={cell.value}
                      onChange={(e) => updateCustomCell(idx, 'value', e.target.value)}
                      placeholder="Value (e.g. O+ Positive)"
                      className="w-1/2 px-2.5 py-1.5 bg-slate-900 border border-slate-700 rounded-lg text-xs text-white focus:outline-none"
                    />
                    <button
                      type="button"
                      onClick={() => removeCustomCell(idx)}
                      className="p-1.5 text-slate-400 hover:text-red-400 rounded-lg transition-colors"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Section 5: Dynamic Custom Tables (Option to add more tables) */}
          <div className="pt-2 border-t border-slate-800">
            <div className="flex items-center justify-between mb-3">
              <div>
                <h4 className="text-xs font-bold text-sky-300 uppercase tracking-wider flex items-center gap-1.5">
                  <TableIcon className="w-4 h-4" />
                  Dynamic Custom Tables
                </h4>
                <p className="text-[11px] text-slate-400">
                  Add structured multi-row tables (e.g. Qualifications, Deployments, Gear History)
                </p>
              </div>
              <button
                type="button"
                onClick={addCustomTable}
                className="flex items-center space-x-1 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-sky-400 hover:text-sky-300 rounded-lg text-xs font-semibold border border-sky-500/30 transition-colors"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>+ Add Table</span>
              </button>
            </div>

            {customTables.length === 0 ? (
              <p className="text-xs text-slate-500 italic py-2">
                No custom tables configured. Click "+ Add Table" to attach tabular records to this soldier.
              </p>
            ) : (
              <div className="space-y-4">
                {customTables.map((tbl, tIdx) => (
                  <div
                    key={tbl.id || tIdx}
                    className="p-3.5 rounded-xl bg-slate-800/40 border border-slate-700/80 space-y-3"
                  >
                    <div className="flex items-center justify-between">
                      <input
                        type="text"
                        value={tbl.title}
                        onChange={(e) => {
                          const updated = [...customTables];
                          updated[tIdx].title = e.target.value;
                          setCustomTables(updated);
                        }}
                        className="text-xs font-bold text-white bg-transparent border-b border-slate-600 focus:border-sky-400 focus:outline-none pb-0.5"
                      />
                      <div className="flex items-center space-x-2">
                        <button
                          type="button"
                          onClick={() => addTableColumn(tIdx)}
                          className="text-[11px] text-sky-400 hover:underline"
                        >
                          + Add Column
                        </button>
                        <button
                          type="button"
                          onClick={() => addTableRow(tIdx)}
                          className="text-[11px] text-emerald-400 hover:underline"
                        >
                          + Add Row
                        </button>
                        <button
                          type="button"
                          onClick={() => removeCustomTable(tIdx)}
                          className="text-slate-400 hover:text-red-400 p-1"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>

                    {/* Table Render */}
                    <div className="overflow-x-auto rounded-lg border border-slate-700">
                      <table className="w-full text-xs text-left">
                        <thead className="bg-slate-800 text-slate-400 text-[10px] uppercase font-bold">
                          <tr>
                            {tbl.columns.map((col, cIdx) => (
                              <th key={cIdx} className="px-2.5 py-1.5 border-r border-slate-700 last:border-0">
                                {col}
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-700 bg-slate-900/60">
                          {tbl.rows.map((row, rIdx) => (
                            <tr key={rIdx}>
                              {tbl.columns.map((col, cIdx) => (
                                <td key={cIdx} className="p-1 border-r border-slate-700 last:border-0">
                                  <input
                                    type="text"
                                    value={row[col] || ''}
                                    onChange={(e) =>
                                      updateTableCell(tIdx, rIdx, col, e.target.value)
                                    }
                                    className="w-full px-2 py-1 bg-transparent text-white text-xs focus:bg-slate-800 focus:outline-none rounded"
                                  />
                                </td>
                              ))}
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Footer Save Button */}
          <div className="pt-4 border-t border-slate-800 flex items-center justify-between">
            <span className="text-xs text-slate-400">
              Personal entries are automatically segregated into the selected Company ledger.
            </span>
            <div className="flex items-center space-x-3">
              <button
                type="button"
                onClick={onClose}
                className="px-4 py-2 text-sm text-slate-400 hover:text-white rounded-xl hover:bg-slate-800 transition-colors"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                className="flex items-center space-x-2 px-6 py-2.5 text-sm font-bold text-slate-950 bg-amber-400 hover:bg-amber-300 disabled:opacity-50 rounded-xl shadow-lg shadow-amber-400/20 transition-all hover:scale-105"
              >
                {savedSuccess ? (
                  <>
                    <CheckCircle className="w-4 h-4 text-slate-950" />
                    <span>Saved!</span>
                  </>
                ) : (
                  <>
                    <Save className="w-4 h-4" />
                    <span>{isEditing ? 'Update Soldier' : 'Add Soldier'}</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};
