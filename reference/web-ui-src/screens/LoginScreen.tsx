import React, { useState } from 'react';
import { Shield, Lock, User, ArrowRight, UserPlus, Server, Loader2 } from 'lucide-react';
import { GatekeeperSession } from '../types';
import {
  apiUrl,
  getLocalServerUrl,
  isNativeApp,
  parseLocalPairingQr,
  pingServerEndpoint,
  saveLocalServerUrl,
} from '../utils/networkSync';

interface LoginScreenProps {
  onLogin: (session: GatekeeperSession) => void;
  isOnline: boolean;
  onServerChanged?: () => void;
}

type Mode = 'signin' | 'signup';

const inputClass =
  'w-full pl-10 pr-3.5 py-3 rounded-lg border border-zinc-300 bg-white text-zinc-900 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-zinc-900 focus:border-zinc-900 transition-all';
const labelClass = 'block text-xs font-semibold text-zinc-700 uppercase tracking-wider mb-1.5';

async function postJson(path: string, body: unknown): Promise<{ ok: boolean; data: any }> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 10000);
  try {
    const res = await fetch(apiUrl(path), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
      signal: controller.signal,
    });
    let data: any = {};
    try {
      data = await res.json();
    } catch {
      // non-JSON error body
    }
    return { ok: res.ok, data };
  } finally {
    clearTimeout(timer);
  }
}

export const LoginScreen: React.FC<LoginScreenProps> = ({ onLogin, isOnline, onServerChanged }) => {
  const [mode, setMode] = useState<Mode>('signin');
  const [userId, setUserId] = useState('');
  const [password, setPassword] = useState('');
  const [name, setName] = useState('');
  const [confirm, setConfirm] = useState('');
  const [location, setLocation] = useState('Location 07');
  const [gate, setGate] = useState('Gate 02');
  const [errorMsg, setErrorMsg] = useState('');
  const [infoMsg, setInfoMsg] = useState('');
  const [busy, setBusy] = useState(false);

  const native = isNativeApp();
  const [showServer, setShowServer] = useState<boolean>(native && !getLocalServerUrl());
  const [serverInput, setServerInput] = useState<string>(getLocalServerUrl());
  const [serverStatus, setServerStatus] = useState('');
  const [serverBusy, setServerBusy] = useState(false);

  const finishLogin = (account: { id: string; name: string; role?: string }, token?: string) => {
    const now = new Date();
    onLogin({
      id: account.id,
      name: account.name,
      role: account.role || 'Gatekeeper Operator',
      location,
      gate,
      shiftStartTime: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
      shiftStartTimestamp: now.getTime(),
      token,
    });
  };

  const explainNetworkError = () =>
    'Cannot reach the server. Check the PC address below and that this device and the PC are on the same Wi-Fi.';

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg('');
    setInfoMsg('');

    if (mode === 'signup') {
      if (name.trim().length < 2) return setErrorMsg('Enter your full name');
      if (password.length < 6) return setErrorMsg('Password must be at least 6 characters');
      if (password !== confirm) return setErrorMsg('Passwords do not match');
    } else if (!userId.trim() || !password) {
      return setErrorMsg('Please enter both Gatekeeper ID and password');
    }

    setBusy(true);
    try {
      if (mode === 'signin') {
        const { ok, data } = await postJson('/api/auth/login', { id: userId.trim(), password });
        if (!ok) return setErrorMsg(data?.message || 'Sign-in failed');
        finishLogin(data.account, data.token);
      } else {
        const { ok, data } = await postJson('/api/auth/register', {
          name: name.trim(),
          id: userId.trim() || undefined,
          password,
        });
        if (!ok) return setErrorMsg(data?.message || 'Sign-up failed');
        if (data.status === 'pending') {
          setMode('signin');
          setUserId(data.account.id);
          setPassword('');
          setConfirm('');
          return setInfoMsg(`Account ${data.account.id} created. Sign in once it has been approved on the PC.`);
        }
        finishLogin(data.account, data.token);
      }
    } catch {
      setErrorMsg(explainNetworkError());
    } finally {
      setBusy(false);
    }
  };

  const handleSaveServer = async () => {
    const raw = serverInput.trim();
    if (!raw) return setServerStatus('Enter the PC address, e.g. 192.168.1.20:3000');
    const hostPort = /^[A-Za-z0-9.-]+(:\d{1,5})?$/.test(raw)
      ? 'http://' + (raw.includes(':') ? raw : raw + ':3000')
      : null;
    const parsed = parseLocalPairingQr(raw) || (/^https?:\/\//i.test(raw) ? raw.replace(/\/+$/, '') : hostPort);
    if (!parsed) return setServerStatus('Not a valid address. Use something like 192.168.1.20:3000');

    setServerBusy(true);
    setServerStatus('Testing connection...');
    saveLocalServerUrl(parsed);
    setServerInput(parsed);
    const res = await pingServerEndpoint(parsed);
    setServerStatus(
      res.ok ? `Connected to ${parsed} (${res.latencyMs} ms)` : `Could not reach ${parsed}: ${res.error || 'no response'}`
    );
    setServerBusy(false);
    onServerChanged?.();
  };

  const switchMode = (next: Mode) => {
    setMode(next);
    setErrorMsg('');
    setInfoMsg('');
  };

  return (
    <div className="flex flex-col flex-1 overflow-y-auto px-6 py-8 max-w-md mx-auto w-full select-none">
      <div className="flex flex-col items-center text-center">
        <div className="w-16 h-16 rounded-2xl bg-zinc-900 text-white flex items-center justify-center shadow-md mb-4 border border-zinc-800">
          <Shield className="w-8 h-8 text-zinc-100 stroke-[1.75]" />
        </div>
        <h1 className="text-xl font-bold text-zinc-900 tracking-tight">XV DIGITAL ACCESS CONTROL</h1>
        <p className="text-xs font-medium text-zinc-500 mt-1 uppercase tracking-wider">Field Access Control System</p>
      </div>

      <div className="grid grid-cols-2 gap-1 p-1 bg-zinc-200 rounded-lg mt-5">
        {(['signin', 'signup'] as Mode[]).map((m) => (
          <button
            key={m}
            type="button"
            onClick={() => switchMode(m)}
            className={`py-2 rounded-md text-xs font-bold uppercase tracking-wider transition-all cursor-pointer ${
              mode === m ? 'bg-white text-zinc-900 shadow-sm' : 'text-zinc-500'
            }`}
          >
            {m === 'signin' ? 'Sign In' : 'Create Account'}
          </button>
        ))}
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 mt-5">
        {infoMsg && (
          <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-xs font-medium text-emerald-800">
            {infoMsg}
          </div>
        )}
        {errorMsg && (
          <div className="p-3 bg-rose-50 border border-rose-200 rounded-lg text-xs font-medium text-rose-700">
            {errorMsg}
          </div>
        )}

        {mode === 'signup' && (
          <div>
            <label className={labelClass}>Full Name</label>
            <div className="relative">
              <UserPlus className="w-4 h-4 text-zinc-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Rank and name"
                maxLength={60}
                autoComplete="name"
                className={inputClass}
              />
            </div>
          </div>
        )}

        <div>
          <label className={labelClass}>
            Gatekeeper ID{mode === 'signup' ? ' (optional)' : ''}
          </label>
          <div className="relative">
            <User className="w-4 h-4 text-zinc-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={userId}
              onChange={(e) => setUserId(e.target.value.toUpperCase())}
              placeholder={mode === 'signup' ? 'Leave blank to be assigned one' : 'Enter your Gatekeeper ID'}
              maxLength={20}
              autoCapitalize="characters"
              autoComplete="username"
              className={inputClass}
            />
          </div>
        </div>

        <div>
          <label className={labelClass}>Password</label>
          <div className="relative">
            <Lock className="w-4 h-4 text-zinc-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder={mode === 'signup' ? 'At least 6 characters' : 'Your password'}
              autoComplete={mode === 'signup' ? 'new-password' : 'current-password'}
              className={inputClass}
            />
          </div>
        </div>

        {mode === 'signup' && (
          <div>
            <label className={labelClass}>Confirm Password</label>
            <div className="relative">
              <Lock className="w-4 h-4 text-zinc-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                placeholder="Repeat password"
                autoComplete="new-password"
                className={inputClass}
              />
            </div>
          </div>
        )}

        <div className="grid grid-cols-2 gap-3 pt-1">
          <div>
            <label className="block text-[11px] font-medium text-zinc-500 mb-1">Station Location</label>
            <select
              value={location}
              onChange={(e) => setLocation(e.target.value)}
              className="w-full px-2.5 py-2 rounded-lg border border-zinc-200 bg-zinc-50 text-xs font-medium text-zinc-800 focus:outline-none focus:bg-white"
            >
              <option value="Location 07">Location 07 (Main Hub)</option>
              <option value="Location 01">Location 01 (North Depot)</option>
              <option value="Location 03">Location 03 (Logistics Dock)</option>
              <option value="Location 10">Location 10 (Perimeter)</option>
            </select>
          </div>
          <div>
            <label className="block text-[11px] font-medium text-zinc-500 mb-1">Active Gate</label>
            <select
              value={gate}
              onChange={(e) => setGate(e.target.value)}
              className="w-full px-2.5 py-2 rounded-lg border border-zinc-200 bg-zinc-50 text-xs font-medium text-zinc-800 focus:outline-none focus:bg-white"
            >
              <option value="Gate 02">Gate 02 (Primary)</option>
              <option value="Gate 01">Gate 01 (Heavy Transit)</option>
              <option value="Gate 03">Gate 03 (Staff Entry)</option>
            </select>
          </div>
        </div>

        <button
          type="submit"
          disabled={busy}
          className="w-full mt-2 flex items-center justify-center gap-2 py-3.5 px-4 bg-zinc-900 hover:bg-black disabled:opacity-60 active:scale-[0.99] text-white rounded-lg font-semibold text-sm shadow-sm transition-all cursor-pointer"
        >
          {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : null}
          <span>{mode === 'signin' ? 'Sign In to Terminal' : 'Create Account & Sign In'}</span>
          {!busy && <ArrowRight className="w-4 h-4" />}
        </button>
      </form>

      <div className="mt-5 border border-zinc-200 rounded-lg bg-white">
        <button
          type="button"
          onClick={() => setShowServer((v) => !v)}
          className="w-full flex items-center justify-between px-3.5 py-2.5 text-xs font-semibold text-zinc-700 cursor-pointer"
        >
          <span className="flex items-center gap-2">
            <Server className="w-3.5 h-3.5 text-zinc-500" />
            PC Server Connection
          </span>
          <span className="flex items-center gap-1.5 text-[11px] font-medium text-zinc-500">
            <span className={`w-2 h-2 rounded-full ${isOnline ? 'bg-emerald-500' : 'bg-amber-500'}`} />
            {isOnline ? 'Connected' : 'Not connected'}
          </span>
        </button>

        {showServer && (
          <div className="px-3.5 pb-3.5 space-y-2">
            <p className="text-[11px] text-zinc-500">
              Enter the address of the PC running the server, e.g. <span className="font-mono">192.168.1.20:3000</span>.
              Both must be on the same Wi-Fi.
            </p>
            <div className="flex gap-2">
              <input
                type="text"
                value={serverInput}
                onChange={(e) => setServerInput(e.target.value)}
                placeholder="192.168.1.20:3000"
                inputMode="url"
                autoCapitalize="none"
                className="flex-1 min-w-0 px-3 py-2 rounded-lg border border-zinc-300 bg-white text-xs font-mono focus:outline-none focus:ring-2 focus:ring-zinc-900"
              />
              <button
                type="button"
                onClick={handleSaveServer}
                disabled={serverBusy}
                className="px-3 py-2 rounded-lg bg-zinc-900 text-white text-xs font-semibold disabled:opacity-60 cursor-pointer"
              >
                Save & Test
              </button>
            </div>
            {serverStatus && <p className="text-[11px] font-medium text-zinc-600 break-words">{serverStatus}</p>}
          </div>
        )}
      </div>
    </div>
  );
};
