import { useEffect, useState } from 'react';

// FEAT-VFB: Voice Test embedded in-dashboard (previously a new-tab window.open from
// Layout.tsx). The voice PoC is served from a different origin (voice.invekto.com:8443,
// the VoiceRuntime service), so it is embedded as a cross-origin <iframe>. The JWT is
// bridged via the ?token= URL param (the page's existing window.INVEKTO_VOICE_JWT
// URL-bridge, which scrubs it from its own URL with history.replaceState). Microphone is
// delegated to the cross-origin frame via allow="microphone".
//
// Two auth paths (identical to the old Layout.tsx button, just relocated here):
//  (1) INMA session -> JWT in localStorage.access_token, used directly.
//  (2) Ops mode      -> Basic Auth in sessionStorage.ops_auth, exchanged for a short-lived
//      (30min) tenant=0 superadmin JWT via GET /ops/voice-jwt. Ops login (useAuth.ts
//      loginWithOps) stores Basic credentials only, not a JWT — without this exchange the
//      Ops-mode caller would see "JWT yok" and bounce.

const VOICE_POC_ORIGIN = 'https://voice.invekto.com:8443';

// Browser-side (Dashboard wrapper) diagnostic codes — disjoint from the voice-poc.js
// child-page codes (INV-VR-CLIENT-001..010). Display-only, no server logic dependency.
// Documented in arch/errors.md. These cover the parent-side token acquisition the wrapper
// performs before the iframe loads.
const CLIENT_ERR = {
  NO_SESSION: 'INV-VR-CLIENT-011',      // neither localStorage JWT nor ops_auth present
  JWT_EXCHANGE_FAILED: 'INV-VR-CLIENT-012', // GET /ops/voice-jwt non-2xx or network/runtime error
  JWT_MALFORMED: 'INV-VR-CLIENT-013',   // /ops/voice-jwt 2xx but token field missing/empty
} as const;

type State =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; src: string };

export function VoiceTestPage() {
  const [state, setState] = useState<State>({ status: 'loading' });

  useEffect(() => {
    let cancelled = false;
    const toSrc = (jwt: string) =>
      VOICE_POC_ORIGIN + '/voice-poc.html?token=' + encodeURIComponent(jwt);

    const run = async () => {
      const t = localStorage.getItem('access_token');
      if (t) {
        if (!cancelled) setState({ status: 'ready', src: toSrc(t) });
        return;
      }
      const opsAuth = sessionStorage.getItem('ops_auth');
      if (!opsAuth) {
        if (!cancelled) {
          setState({ status: 'error', message: `[${CLIENT_ERR.NO_SESSION}] Oturum bulunamadı (ne JWT ne Ops giriş). Dashboard'a login olun.` });
        }
        return;
      }
      try {
        const r = await fetch('/ops/voice-jwt', {
          method: 'GET',
          headers: { Authorization: 'Basic ' + opsAuth },
        });
        if (!r.ok) {
          const body = await r.text().catch(() => '');
          if (!cancelled) setState({ status: 'error', message: `[${CLIENT_ERR.JWT_EXCHANGE_FAILED}] Voice JWT alınamadı: ` + (body || r.status) });
          return;
        }
        const data = await r.json();
        if (!data || typeof data.token !== 'string' || data.token.length === 0) {
          if (!cancelled) setState({ status: 'error', message: `[${CLIENT_ERR.JWT_MALFORMED}] Voice JWT yanıtı geçersiz (token alanı yok).` });
          return;
        }
        if (!cancelled) setState({ status: 'ready', src: toSrc(data.token) });
      } catch (e) {
        if (!cancelled) {
          setState({ status: 'error', message: `[${CLIENT_ERR.JWT_EXCHANGE_FAILED}] Voice Test giriş hatası: ` + (e instanceof Error ? e.message : String(e)) });
        }
      }
    };
    void run();
    return () => { cancelled = true; };
  }, []);

  if (state.status === 'loading') {
    return (
      <div className="h-screen flex items-center justify-center bg-navy-50 text-navy-400">
        Voice Test yükleniyor…
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <div className="h-screen flex flex-col items-center justify-center bg-navy-50 gap-3 px-6 text-center">
        <span className="text-navy-700 font-medium">Voice Test açılamadı</span>
        <span className="text-sm text-navy-400 max-w-md">{state.message}</span>
      </div>
    );
  }

  return (
    <iframe
      title="Voice Test"
      src={state.src}
      allow="microphone; autoplay"
      className="w-full h-screen border-0 block"
    />
  );
}
