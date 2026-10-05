import React, { useState } from 'react';
import {
  Truck,
  User,
  ArrowLeft,
  ArrowRight,
  Plus,
  Trash2,
  CheckCircle2,
  AlertTriangle,
  Users,
  ShieldCheck,
  Check,
} from 'lucide-react';
import {
  Vehicle,
  Personnel,
  GatekeeperSession,
  ManifestPerson,
  VehicleStep,
  ActionDirection,
} from '../types';
import { StatusChip } from '../components/common/StatusChip';
import { ScannerViewfinder } from '../components/common/ScannerViewfinder';
import {
  findVehicleById,
  findPersonnelById,
  formatCurrentTime,
  calculateDuration,
} from '../data/mockDatabase';
import { playSuccessChime } from '../utils/audioFeedback';
import { parseScannedQr } from '../utils/qrLocationParser';

interface VehicleScanFlowProps {
  session: GatekeeperSession;
  personnelList?: Personnel[];
  initialVehicle?: Vehicle | null;
  onCancel: () => void;
  onCompleteVehicleAction: (
    vehicle: Vehicle,
    driver: ManifestPerson,
    coDriver: ManifestPerson | undefined,
    occupants: ManifestPerson[],
    action: ActionDirection,
    stayDuration?: string,
    locationMismatch?: boolean,
    scannedLocation?: string
  ) => void;
  soundEnabled?: boolean;
}

export const VehicleScanFlow: React.FC<VehicleScanFlowProps> = ({
  session,
  personnelList = [],
  initialVehicle = null,
  onCancel,
  onCompleteVehicleAction,
  soundEnabled = true,
}) => {
  const [step, setStep] = useState<VehicleStep>(initialVehicle ? 'SCAN_DRIVER' : 'SCAN_VEHICLE');
  const [vehicle, setVehicle] = useState<Vehicle | null>(initialVehicle);
  const [vehicleAction, setVehicleAction] = useState<ActionDirection | null>(null);
  const [unrecognizedVehicleId, setUnrecognizedVehicleId] = useState<string>('');
  const [scannedVehicleLocation, setScannedVehicleLocation] = useState<string | undefined>(undefined);
  const [vehicleLocationMismatch, setVehicleLocationMismatch] = useState<boolean>(false);

  const [driver, setDriver] = useState<ManifestPerson | null>(null);
  const [scannedDriverLocation, setScannedDriverLocation] = useState<string | undefined>(undefined);
  const [driverLocationMismatch, setDriverLocationMismatch] = useState<boolean>(false);

  const [coDriver, setCoDriver] = useState<ManifestPerson | undefined>(undefined);
  const [occupants, setOccupants] = useState<ManifestPerson[]>([]);
  const [scannerMode, setScannerMode] = useState<'DRIVER' | 'CO_DRIVER' | 'OCCUPANT'>('DRIVER');

  const lookupPerson = (targetId: string, rawCode: string): Personnel | null => {
    const cleanTarget = targetId.trim().toUpperCase();
    const cleanRaw = rawCode.trim().toUpperCase();
    const foundInList = personnelList.find((p) => {
      const pId = p.id.toUpperCase();
      const pSec = p.secretCode ? p.secretCode.toUpperCase() : '';
      const pSrv = p.serviceNumber ? p.serviceNumber.toUpperCase() : '';
      const pArmy = p.armyNumber ? p.armyNumber.toUpperCase() : '';
      const pCard = p.idCardNumber ? p.idCardNumber.toUpperCase() : '';

      return (
        pId === cleanTarget ||
        pId.replace('-', '') === cleanTarget.replace('-', '') ||
        (pSec && (pSec === cleanTarget || pSec === cleanRaw)) ||
        (pSrv && (pSrv === cleanTarget || pSrv === cleanRaw)) ||
        (pArmy && (pArmy === cleanTarget || pArmy === cleanRaw)) ||
        (pCard && (pCard === cleanTarget || pCard === cleanRaw))
      );
    });
    return foundInList || findPersonnelById(targetId) || findPersonnelById(rawCode);
  };

  // Handle scanned vehicle code
  const handleVehicleScan = (code: string) => {
    const parsed = parseScannedQr(code, session.location);
    const targetId = parsed.entityId || code;
    const v = findVehicleById(targetId) || findVehicleById(code);
    if (v) {
      setVehicle(v);
      setScannedVehicleLocation(parsed.locationId);
      setVehicleLocationMismatch(parsed.isLocationMismatch);
      setVehicleAction(null);
      setStep('CHOOSE_VEHICLE_ACTION');
    } else {
      setUnrecognizedVehicleId(code);
    }
  };

  // Handle scanned driver code
  const handleDriverScan = (code: string) => {
    const parsed = parseScannedQr(code, session.location);
    const targetId = parsed.entityId || code;
    const p = lookupPerson(targetId, code);
    if (p) {
      setDriver({
        id: p.id,
        name: p.name,
        role: p.role,
        verified: p.status === 'ACTIVE',
        type: 'DRIVER',
      });
      setScannedDriverLocation(parsed.locationId);
      setDriverLocationMismatch(parsed.isLocationMismatch);
      setStep('CO_DRIVER_CHOICE');
    }
  };

  // Handle scanned co-driver code
  const handleCoDriverScan = (code: string) => {
    const parsed = parseScannedQr(code, session.location);
    const targetId = parsed.entityId || code;
    const p = lookupPerson(targetId, code);
    if (p) {
      setCoDriver({
        id: p.id,
        name: p.name,
        role: p.role,
        verified: p.status === 'ACTIVE',
        type: 'CO_DRIVER',
      });
      setStep('OCCUPANTS');
    }
  };

  // Handle scanned occupant code
  const handleOccupantScan = (code: string) => {
    const parsed = parseScannedQr(code, session.location);
    const targetId = parsed.entityId || code;
    const p = lookupPerson(targetId, code);
    if (p) {
      // prevent duplicate addition
      if (!occupants.some((o) => o.id === p.id) && driver?.id !== p.id && coDriver?.id !== p.id) {
        setOccupants((prev) => [
          ...prev,
          {
            id: p.id,
            name: p.name,
            role: p.role,
            verified: p.status === 'ACTIVE',
            type: 'OCCUPANT',
          },
        ]);
      }
    }
    setStep('OCCUPANTS');
  };

  const removeOccupant = (id: string) => {
    setOccupants((prev) => prev.filter((o) => o.id !== id));
  };

  // ==========================================
  // STEP 1: SCAN VEHICLE
  // ==========================================
  if (step === 'SCAN_VEHICLE') {
    if (unrecognizedVehicleId) {
      return (
        <div className="flex flex-col flex-1 px-6 py-6 max-w-md mx-auto w-full select-none justify-between">
          <div className="flex items-center gap-2 pb-3 border-b border-zinc-200">
            <button
              onClick={onCancel}
              className="p-1.5 -ml-1.5 rounded-lg text-zinc-600 hover:text-zinc-900 active:bg-zinc-100"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <span className="text-sm font-bold text-zinc-800">Vehicle Scan Result</span>
          </div>

          <div className="flex flex-col items-center text-center my-auto px-4">
            <div className="w-16 h-16 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center border border-amber-200 mb-4">
              <AlertTriangle className="w-8 h-8 stroke-[1.75]" />
            </div>
            <h2 className="text-lg font-bold text-zinc-900 tracking-tight">
              Vehicle Not Recognized
            </h2>
            <p className="text-xs text-zinc-500 mt-2 max-w-xs leading-relaxed">
              Target barcode <span className="font-mono font-bold text-zinc-800">{unrecognizedVehicleId}</span> is not registered in the fleet database.
            </p>
          </div>

          <div className="space-y-2">
            <button
              onClick={() => setUnrecognizedVehicleId('')}
              className="w-full py-3.5 px-4 bg-zinc-900 hover:bg-black text-white font-semibold rounded-xl text-sm"
            >
              Scan Again
            </button>
            <button
              onClick={onCancel}
              className="w-full py-3 px-4 bg-zinc-100 hover:bg-zinc-200 text-zinc-700 font-semibold rounded-xl text-sm"
            >
              Cancel
            </button>
          </div>
        </div>
      );
    }

    return (
      <ScannerViewfinder
        title="Scan Vehicle QR"
        subtitle="Align vehicle windshield or registration QR in frame"
        mode="VEHICLE"
        onScan={handleVehicleScan}
        onBack={onCancel}
        soundEnabled={soundEnabled}
      />
    );
  }

  // Guard: vehicle must exist from step 2 onward
  if (!vehicle) return null;

  const targetAction: ActionDirection = vehicle.currentStatus === 'INSIDE' ? 'EXIT' : 'ENTRY';
  const stayDuration = targetAction === 'EXIT' ? calculateDuration(vehicle.lastEntryTimestamp) : undefined;

  // ==========================================
  // STEP 2: CHOOSE VEHICLE ACTION (ENTRY/EXIT)
  // ==========================================
  if (step === 'CHOOSE_VEHICLE_ACTION') {
    return (
      <div className="flex flex-col flex-1 px-6 py-6 max-w-md mx-auto w-full select-none justify-between">
        <div className="flex items-center gap-2 pb-3 border-b border-zinc-200">
          <button
            onClick={() => {
              setVehicle(null);
              setStep('SCAN_VEHICLE');
            }}
            className="p-1.5 -ml-1.5 rounded-lg text-zinc-600 hover:text-zinc-900 active:bg-zinc-100"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <span className="text-sm font-bold text-zinc-800">Vehicle Action</span>
        </div>

        <div className="flex flex-col items-center text-center my-auto px-4">
          <div className="w-16 h-16 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center border border-blue-200 mb-4">
            <Truck className="w-8 h-8" />
          </div>

          <h2 className="text-lg font-bold text-zinc-900 tracking-tight">
            {vehicle?.plateNumber}
          </h2>
          <p className="text-sm text-zinc-600 mt-1">
            {vehicle?.id}
          </p>
          <p className="text-xs text-zinc-500 mt-2 max-w-xs">
            Is this vehicle entering or exiting?
          </p>
        </div>

        <div className="space-y-2">
          <button
            onClick={() => {
              setVehicleAction('ENTRY');
              setStep('SCAN_DRIVER');
            }}
            className="w-full py-3.5 px-4 bg-green-600 hover:bg-green-700 text-white font-semibold rounded-xl text-sm transition-all"
          >
            ↓ Vehicle Entering
          </button>
          <button
            onClick={() => {
              setVehicleAction('EXIT');
              setStep('SCAN_DRIVER');
            }}
            className="w-full py-3.5 px-4 bg-orange-600 hover:bg-orange-700 text-white font-semibold rounded-xl text-sm transition-all"
          >
            ↑ Vehicle Exiting
          </button>
        </div>
      </div>
    );
  }

  // ==========================================
  // STEP 3: SCAN DRIVER
  // ==========================================
  if (step === 'SCAN_DRIVER') {
    return (
      <div className="flex flex-col flex-1 h-full bg-zinc-900 text-white select-none">
        {/* Compact Vehicle Strip Header */}
        <div className="px-4 py-3 bg-zinc-950 border-b border-zinc-800 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Truck className="w-4 h-4 text-emerald-400" />
            <span className="font-mono font-bold text-xs">{vehicle.id}</span>
            <span className="text-zinc-400 text-xs">• {vehicle.plateNumber}</span>
          </div>
          <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-zinc-800 text-zinc-300">
            Step 2/5: Driver
          </span>
        </div>

        {/* Vehicle Location Mismatch Warning in Driver Step */}
        {vehicleLocationMismatch && (
          <div
            id="vehicle-location-mismatch-warning"
            className="mx-4 mt-2.5 p-3 bg-amber-500/20 border border-amber-500/80 rounded-xl text-amber-200 text-xs flex items-start gap-2.5 shadow-sm animate-fade-in"
          >
            <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0 mt-0.5" />
            <div className="flex-1 min-w-0">
              <div className="flex items-center justify-between gap-1">
                <span className="font-bold text-amber-300 uppercase tracking-wider text-[11px]">
                  Vehicle Location Mismatch
                </span>
                <span className="text-[9px] font-mono px-1.5 py-0.2 bg-amber-400/20 text-amber-300 rounded font-bold border border-amber-400/40">
                  FLAGGED
                </span>
              </div>
              <p className="text-[11px] text-zinc-300 mt-1 leading-relaxed">
                Vehicle QR specifies location <span className="font-mono text-amber-300 font-bold">{scannedVehicleLocation}</span>, differing from this station (<span className="font-mono text-zinc-100 font-bold">{session.location}</span>).
              </p>
            </div>
          </div>
        )}

        {/* If vehicle is exiting, allow 1-tap auto-load of active entry manifest */}
        {targetAction === 'EXIT' && (
          <div className="bg-zinc-950 px-4 py-2.5 border-b border-zinc-800 flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Users className="w-4 h-4 text-emerald-400" />
              <span className="text-xs text-zinc-300 font-medium">Active entry manifest on file</span>
            </div>
            <button
              onClick={() => {
                const autoDriver = findPersonnelById(vehicle.authorizedDrivers?.[0] || 'P-001');
                if (autoDriver) {
                  setDriver({
                    id: autoDriver.id,
                    name: autoDriver.name,
                    role: autoDriver.role,
                    verified: autoDriver.status === 'ACTIVE',
                    type: 'DRIVER',
                  });
                  if (vehicle.authorizedDrivers?.[1]) {
                    const autoCo = findPersonnelById(vehicle.authorizedDrivers[1]);
                    if (autoCo) {
                      setCoDriver({
                        id: autoCo.id,
                        name: autoCo.name,
                        role: autoCo.role,
                        verified: autoCo.status === 'ACTIVE',
                        type: 'CO_DRIVER',
                      });
                    }
                  }
                  setStep('OCCUPANTS');
                }
              }}
              className="px-3 py-1 bg-emerald-500 hover:bg-emerald-400 text-zinc-950 font-bold rounded-lg text-xs transition-all cursor-pointer shadow-xs"
            >
              Auto-fill Manifest
            </button>
          </div>
        )}

        <ScannerViewfinder
          title="Scan Driver QR"
          subtitle="Scan authorized driver identity badge"
          mode="DRIVER"
          onScan={handleDriverScan}
          onBack={() => setStep('SCAN_VEHICLE')}
          soundEnabled={soundEnabled}
        />
      </div>
    );
  }

  // ==========================================
  // STEP 3: CO-DRIVER (Optional)
  // ==========================================
  if (step === 'CO_DRIVER_CHOICE') {
    return (
      <div className="flex flex-col flex-1 px-5 py-6 max-w-md mx-auto w-full select-none justify-between">
        {/* Top Header */}
        <div>
          <div className="flex items-center justify-between pb-3 border-b border-zinc-200/80">
            <button
              onClick={() => setStep('SCAN_DRIVER')}
              className="flex items-center gap-1 text-zinc-600 hover:text-zinc-900 text-xs font-semibold"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>Back</span>
            </button>
            <span className="text-xs font-mono uppercase tracking-wider text-zinc-400 font-bold">
              Co-Driver (Optional)
            </span>
            <div className="w-8" />
          </div>

          {/* Progress Strip */}
          <div className="mt-4 p-3.5 bg-zinc-50 border border-zinc-200 rounded-xl space-y-2 text-xs">
            <div className="flex justify-between items-center text-zinc-600">
              <span className="text-zinc-400">Vehicle:</span>
              <span className="font-bold text-zinc-900 font-mono">{vehicle.id} ({vehicle.plateNumber})</span>
            </div>
            <div className="flex justify-between items-center text-zinc-600 pt-2 border-t border-zinc-200/60">
              <span className="text-zinc-400">Verified Driver:</span>
              <span className="font-bold text-zinc-900 flex items-center gap-1">
                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                {driver?.name} ({driver?.id})
              </span>
            </div>
          </div>

          <div className="text-center mt-10 px-4">
            <div className="w-14 h-14 rounded-2xl bg-zinc-100 border border-zinc-200 flex items-center justify-center mx-auto text-zinc-700 mb-3">
              <Users className="w-7 h-7 stroke-[1.75]" />
            </div>
            <h3 className="text-base font-bold text-zinc-900 tracking-tight">
              Is there a Co-Driver?
            </h3>
            <p className="text-xs text-zinc-500 mt-1 max-w-xs mx-auto">
              Scan co-driver badge if operating a dual-driver commercial transit, or proceed directly.
            </p>
          </div>
        </div>

        {/* 2 Clear Choices */}
        <div className="space-y-3">
          <button
            onClick={() => {
              setScannerMode('CO_DRIVER');
              setStep('SCAN_CO_DRIVER');
            }}
            className="w-full py-4 px-4 bg-zinc-900 hover:bg-black text-white font-bold rounded-xl text-sm transition-all flex items-center justify-center gap-2 cursor-pointer"
          >
            <span>Scan Co-Driver QR</span>
            <ArrowRight className="w-4 h-4" />
          </button>

          <button
            onClick={() => {
              setCoDriver(undefined);
              setStep('OCCUPANTS');
            }}
            className="w-full py-3.5 px-4 bg-zinc-100 hover:bg-zinc-200 text-zinc-800 font-semibold rounded-xl text-xs transition-all cursor-pointer"
          >
            No Co-Driver (Continue)
          </button>
        </div>
      </div>
    );
  }

  // Scanning Co-Driver
  if (step === 'SCAN_CO_DRIVER') {
    return (
      <ScannerViewfinder
        title="Scan Co-Driver QR"
        subtitle="Align co-driver identity badge inside frame"
        mode="CO_DRIVER"
        onScan={handleCoDriverScan}
        onBack={() => setStep('CO_DRIVER_CHOICE')}
        soundEnabled={soundEnabled}
      />
    );
  }

  // Scanning Additional Occupant
  if (step === 'SCAN_OCCUPANT') {
    return (
      <ScannerViewfinder
        title="Scan Occupant QR"
        subtitle="Align passenger identity badge inside frame"
        mode="OCCUPANT"
        onScan={handleOccupantScan}
        onBack={() => setStep('OCCUPANTS')}
        soundEnabled={soundEnabled}
      />
    );
  }

  // ==========================================
  // STEP 4: OCCUPANTS LIST & ADD
  // ==========================================
  if (step === 'OCCUPANTS') {
    return (
      <div className="flex flex-col flex-1 px-5 py-4 max-w-md mx-auto w-full select-none justify-between overflow-y-auto">
        <div>
          <div className="flex items-center justify-between pb-3 border-b border-zinc-200/80">
            <button
              onClick={() => setStep('CO_DRIVER_CHOICE')}
              className="flex items-center gap-1 text-zinc-600 hover:text-zinc-900 text-xs font-semibold"
            >
              <ArrowLeft className="w-4 h-4" />
              <span>Back</span>
            </button>
            <span className="text-xs font-mono uppercase tracking-wider text-zinc-400 font-bold">
              Occupant Manifest
            </span>
            <div className="w-8" />
          </div>

          {/* Occupants Overview List */}
          <div className="mt-4 space-y-2.5">
            {/* Driver Card */}
            <div className="p-3 bg-white border border-zinc-200 rounded-xl flex items-center justify-between text-xs">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-zinc-100 flex items-center justify-center text-zinc-800">
                  <User className="w-4 h-4" />
                </div>
                <div>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-zinc-400 block">
                    Driver
                  </span>
                  <span className="font-bold text-zinc-900">{driver?.name}</span>
                  <span className="text-zinc-500 font-mono ml-1.5 text-[11px]">({driver?.id})</span>
                </div>
              </div>
              <span className="flex items-center gap-1 text-emerald-600 font-bold text-[11px]">
                <Check className="w-3.5 h-3.5 stroke-[2.5]" />
                Verified
              </span>
            </div>

            {/* Co-Driver Card if present */}
            {coDriver && (
              <div className="p-3 bg-white border border-zinc-200 rounded-xl flex items-center justify-between text-xs">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-zinc-100 flex items-center justify-center text-zinc-800">
                    <User className="w-4 h-4" />
                  </div>
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-wider text-zinc-400 block">
                      Co-Driver
                    </span>
                    <span className="font-bold text-zinc-900">{coDriver.name}</span>
                    <span className="text-zinc-500 font-mono ml-1.5 text-[11px]">({coDriver.id})</span>
                  </div>
                </div>
                <button
                  onClick={() => setCoDriver(undefined)}
                  className="p-1.5 text-zinc-400 hover:text-rose-600 rounded"
                  title="Remove Co-driver"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            )}

            {/* Additional Occupants List */}
            {occupants.map((occ, idx) => (
              <div
                key={occ.id}
                className="p-3 bg-white border border-zinc-200 rounded-xl flex items-center justify-between text-xs"
              >
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-zinc-50 border border-zinc-200 flex items-center justify-center text-zinc-600 font-mono text-xs">
                    #{idx + 1}
                  </div>
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-wider text-zinc-400 block">
                      Additional Occupant
                    </span>
                    <span className="font-bold text-zinc-900">{occ.name}</span>
                    <span className="text-zinc-500 font-mono ml-1.5 text-[11px]">({occ.id})</span>
                  </div>
                </div>
                <button
                  onClick={() => removeOccupant(occ.id)}
                  className="p-1.5 text-zinc-400 hover:text-rose-600 rounded"
                  title="Remove occupant"
                >
                  <Trash2 className="w-4 h-4" />
                </button>
              </div>
            ))}

            {/* Empty occupant hint */}
            {occupants.length === 0 && !coDriver && (
              <div className="p-4 border border-dashed border-zinc-200 rounded-xl text-center text-zinc-400 text-xs">
                No additional passengers scanned.
              </div>
            )}
          </div>

          {/* Add Passenger Button */}
          <button
            onClick={() => {
              setScannerMode('OCCUPANT');
              setStep('SCAN_OCCUPANT');
            }}
            className="w-full mt-3 py-3 px-4 border border-zinc-300 hover:border-zinc-400 bg-white hover:bg-zinc-50 rounded-xl text-xs font-semibold text-zinc-800 flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
          >
            <Plus className="w-4 h-4" />
            <span>Add Additional Person QR</span>
          </button>
        </div>

        {/* Continue Button */}
        <div className="mt-6 pt-3 border-t border-zinc-100">
          <button
            onClick={() => setStep('CONFIRMATION')}
            className="w-full py-4 px-4 bg-zinc-900 hover:bg-black text-white font-bold rounded-xl text-sm transition-all flex items-center justify-center gap-2 cursor-pointer shadow-sm"
          >
            <span>Review Vehicle Entry/Exit</span>
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>
      </div>
    );
  }

  // ==========================================
  // STEP 5: VEHICLE CONFIRMATION SCREEN (#19 in Spec)
  // ==========================================
  const currentTime = formatCurrentTime();
  const totalPersons = (driver ? 1 : 0) + (coDriver ? 1 : 0) + occupants.length;

  return (
    <div className="flex flex-col flex-1 px-5 py-4 max-w-md mx-auto w-full select-none justify-between overflow-y-auto">
      <div>
        <div className="flex items-center justify-between pb-3 border-b border-zinc-200/80">
          <button
            onClick={() => setStep('OCCUPANTS')}
            className="flex items-center gap-1.5 text-zinc-600 hover:text-zinc-900 text-xs font-semibold"
          >
            <ArrowLeft className="w-4 h-4" />
            <span>Back</span>
          </button>
          <span className="text-[11px] font-mono uppercase tracking-wider text-emerald-700 font-bold bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200">
            Step 4 of 4 • Review
          </span>
          <div className="w-8" />
        </div>

        <h2 className="text-lg font-bold text-zinc-900 tracking-tight mt-3">
          CONFIRM VEHICLE {targetAction}
        </h2>
        <p className="text-xs text-zinc-500 mt-0.5">
          Verify vehicle and passenger manifest before committing to registry.
        </p>

        {/* Location Mismatch Alert if detected in Vehicle or Driver QR */}
        {(vehicleLocationMismatch || driverLocationMismatch) && (
          <div
            id="vehicle-confirmation-location-mismatch"
            className="mt-3.5 p-3.5 bg-amber-500/10 border-2 border-amber-500 rounded-2xl text-amber-950 text-xs shadow-xs space-y-2 animate-fade-in"
          >
            <div className="flex items-center justify-between gap-2">
              <div className="flex items-center gap-2">
                <div className="w-6 h-6 rounded-lg bg-amber-500 text-white flex items-center justify-center shrink-0">
                  <AlertTriangle className="w-3.5 h-3.5 stroke-[2.5]" />
                </div>
                <span className="font-bold text-xs uppercase tracking-wider text-amber-950">
                  Location Mismatch Warning
                </span>
              </div>
              <span
                id="vehicle-mismatch-badge"
                className="font-mono text-[9px] font-bold px-2 py-0.5 rounded-md bg-amber-200 text-amber-950 border border-amber-400"
              >
                CROSS-LOCATION
              </span>
            </div>

            <p className="text-[11px] text-amber-900 leading-relaxed">
              One or more scanned credentials specify a location ID differing from this station (<span className="font-mono font-bold">{session.location}</span>):
            </p>

            <div className="p-2.5 bg-white/90 rounded-xl border border-amber-300 font-mono text-[11px] space-y-1">
              {vehicleLocationMismatch && (
                <div className="flex justify-between items-center text-amber-950">
                  <span>• Vehicle QR Location:</span>
                  <span className="font-bold text-amber-900 bg-amber-100 px-1.5 py-0.5 rounded">
                    {scannedVehicleLocation}
                  </span>
                </div>
              )}
              {driverLocationMismatch && (
                <div className="flex justify-between items-center text-amber-950">
                  <span>• Driver QR Location:</span>
                  <span className="font-bold text-amber-900 bg-amber-100 px-1.5 py-0.5 rounded">
                    {scannedDriverLocation}
                  </span>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Structured Summary Table */}
        <div className="mt-4 p-5 bg-white border border-zinc-200/90 rounded-2xl shadow-sm space-y-3.5 text-xs">
          {/* Vehicle Info with Reflective License Plate Badge */}
          <div className="flex items-center justify-between pb-3 border-b border-zinc-100">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-xl bg-zinc-100 border border-zinc-200 flex items-center justify-center text-zinc-800 shrink-0">
                <Truck className="w-5 h-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="bg-amber-100/90 text-amber-950 font-mono font-bold px-2 py-0.5 rounded border border-amber-300 text-xs tracking-wider shadow-2xs">
                    {vehicle.plateNumber}
                  </span>
                  <span className="font-mono text-zinc-400 font-bold text-xs">{vehicle.id}</span>
                </div>
                <div className="text-zinc-500 text-[11px] mt-1 font-medium">{vehicle.type}</div>
              </div>
            </div>
            <StatusChip status={vehicle.status} size="sm" />
          </div>

          {/* Personnel Breakdown */}
          <div className="space-y-2 py-1 border-b border-zinc-100">
            <div className="flex justify-between items-center">
              <span className="text-zinc-500 font-medium">Primary Driver</span>
              <span className="font-bold text-zinc-900 flex items-center gap-1.5">
                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                {driver?.name} ({driver?.id})
              </span>
            </div>

            {coDriver && (
              <div className="flex justify-between items-center">
                <span className="text-zinc-500 font-medium">Co-driver</span>
                <span className="font-bold text-zinc-900 flex items-center gap-1.5">
                  <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                  {coDriver.name} ({coDriver.id})
                </span>
              </div>
            )}

            <div className="flex justify-between items-center">
              <span className="text-zinc-500 font-medium">Additional Occupants</span>
              <span className="font-semibold text-zinc-800 font-mono">
                {occupants.length > 0 ? `${occupants.length} person(s)` : 'None'}
              </span>
            </div>

            <div className="flex justify-between items-center text-zinc-900 font-bold pt-1.5 border-t border-zinc-100">
              <span className="flex items-center gap-1.5">
                <Users className="w-3.5 h-3.5 text-zinc-600" />
                Total Persons Onboard:
              </span>
              <span className="font-mono text-sm bg-zinc-100 px-2 py-0.5 rounded border border-zinc-200">
                {totalPersons} individuals
              </span>
            </div>
          </div>

          {/* Station & Timing */}
          <div className="space-y-1.5 text-zinc-600 pt-1">
            <div className="flex justify-between">
              <span className="text-zinc-400">Station Post</span>
              <span className="font-semibold text-zinc-800">{session.location} • {session.gate}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-400">Timestamp</span>
              <span className="font-mono font-bold text-zinc-900">{currentTime}</span>
            </div>
            {stayDuration && (
              <div className="flex justify-between pt-1.5 border-t border-zinc-100">
                <span className="text-amber-800 font-bold">On-site Duration</span>
                <span className="font-mono font-bold text-amber-950 bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
                  {stayDuration}
                </span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Confirm Buttons */}
      <div className="grid grid-cols-2 gap-3 mt-6">
        <button
          onClick={onCancel}
          className="py-3.5 px-4 bg-zinc-100 hover:bg-zinc-200 text-zinc-700 font-semibold rounded-xl text-xs transition-colors"
        >
          Cancel
        </button>
        <button
          onClick={() => {
            playSuccessChime(soundEnabled);
            onCompleteVehicleAction(
              vehicle,
              driver!,
              coDriver,
              occupants,
              targetAction,
              stayDuration,
              vehicleLocationMismatch || driverLocationMismatch,
              scannedVehicleLocation || scannedDriverLocation
            );
          }}
          className="py-3.5 px-4 bg-zinc-900 hover:bg-black text-white font-bold rounded-xl text-xs transition-all flex items-center justify-center gap-1.5 shadow-sm cursor-pointer"
        >
          <ShieldCheck className="w-4 h-4 text-emerald-400" />
          <span>Confirm {targetAction}</span>
        </button>
      </div>
    </div>
  );
};
