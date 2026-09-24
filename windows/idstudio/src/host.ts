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

export const photoUrl = (id: string, ver: number) => ver ? `media/photo/${encodeURIComponent(id)}?v=${ver}` : '';
export const signatureUrl = (id: string, ver: number) => ver ? `media/signature/${encodeURIComponent(id)}?v=${ver}` : '';
