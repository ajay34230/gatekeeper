import { useEffect, useState } from 'react';
// Bridge to the XV Command Center (WebView2). All data comes from and goes to the PC's encrypted database.
type Listener = (m: any) => void;
const listeners: Listener[] = [];
const wv = (window as any).chrome?.webview;

export const host = {
  available: !!wv,
  send(message: object) { wv?.postMessage(JSON.stringify(message)); },
  on(fn: Listener) { listeners.push(fn); return () => { const i = listeners.indexOf(fn); if (i >= 0) listeners.splice(i, 1); }; },
};

wv?.addEventListener('message', (e: MessageEvent) => {
  const m = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
  listeners.forEach(l => l(m));
});

// ---------------------------------------------------------------- photos & signatures
// Sent by the host over the message channel as data: URLs (straight from the encrypted database). No network request is
// involved, so the images always show, print and export (html-to-image embeds data: URLs as they are).
type Kind = 'photo' | 'signature';
const cache = new Map<string, string>();          // kind|id|ver -> data URL ('' = none)
const waiting = new Map<string, ((url: string) => void)[]>();
const key = (kind: Kind, id: string, ver: number) => `${kind}|${id}|${ver}`;

host.on(m => {
  if (m.t !== 'media') return;
  const k = key(m.kind, m.id, m.ver);
  cache.set(k, m.dataUrl || '');
  (waiting.get(k) ?? []).forEach(r => r(m.dataUrl || ''));
  waiting.delete(k);
});

export function loadMedia(kind: Kind, id: string, ver: number): Promise<string> {
  if (!ver || !id) return Promise.resolve('');
  const k = key(kind, id, ver);
  const hit = cache.get(k);
  if (hit !== undefined) return Promise.resolve(hit);
  return new Promise(resolve => {
    const list = waiting.get(k);
    if (list) { list.push(resolve); return; }
    waiting.set(k, [resolve]);
    host.send({ t: 'media', kind, id, ver });
    // Never hang a print or export if the host does not answer.
    setTimeout(() => { const l = waiting.get(k); if (l) { waiting.delete(k); l.forEach(r => r('')); } }, 8000);
  });
}

/** Loads the images of these soldiers before a print or export is captured. */
export const preloadMedia = (list: { id: string; photoVer: number; signatureVer: number }[]) =>
  Promise.all(list.flatMap(s => [loadMedia('photo', s.id, s.photoVer), loadMedia('signature', s.id, s.signatureVer)]));

function useMedia(kind: Kind, id: string, ver: number): string {
  const [url, setUrl] = useState(() => (ver ? cache.get(key(kind, id, ver)) ?? '' : ''));
  useEffect(() => {
    let live = true;
    const hit = ver ? cache.get(key(kind, id, ver)) : '';
    setUrl(hit ?? '');
    if (hit === undefined) loadMedia(kind, id, ver).then(u => { if (live) setUrl(u); });
    return () => { live = false; };
  }, [kind, id, ver]);
  return url;
}

export const usePhoto = (id: string, ver: number) => useMedia('photo', id, ver);
export const useSignature = (id: string, ver: number) => useMedia('signature', id, ver);
