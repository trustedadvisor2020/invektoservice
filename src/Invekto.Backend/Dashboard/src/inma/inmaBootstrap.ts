import { create } from 'zustand';
import { api } from '../lib/api';
import { useInmaSession } from './inmaSession';
import { INMA_ERRORS, type InmaErrorCode } from './inmaErrors';

export const INMA_SESSION_UPDATED_EVENT = 'inma:session-updated';
export const INMA_SESSION_CLEARED_EVENT = 'inma:session-cleared';

export type InmaBootstrapState = 'idle' | 'bootstrapping' | 'authenticated' | 'error';

interface InmaBootstrapStore {
  state: InmaBootstrapState;
  lastCode: InmaErrorCode | null;
  setState: (state: InmaBootstrapState, lastCode?: InmaErrorCode | null) => void;
  setDiagnosticCode: (code: InmaErrorCode) => void;
}

export const useInmaBootstrap = create<InmaBootstrapStore>((set) => ({
  state: 'idle',
  lastCode: null,
  setState: (state, lastCode = null) => set({ state, lastCode }),
  // Non-blocking diagnostic (welcome failure) — state sabit kalir, lastCode raporlanir.
  setDiagnosticCode: (code) => set({ lastCode: code }),
}));

class InmaBootstrapImpl {
  async run(): Promise<void> {
    console.log('[inma-debug] bootstrap.run called');
    const store = useInmaBootstrap.getState();
    const { accessToken } = useInmaSession.getState();
    if (!accessToken) {
      console.warn('[inma-debug] bootstrap.run EARLY EXIT: no accessToken in inmaSession store');
      return;
    }

    // Idempotency: same token already active -> skip network calls, just resync consumers.
    if (store.state === 'authenticated' && api.getAccessToken() === accessToken) {
      console.log('[inma-debug] bootstrap.run IDEMPOTENT SKIP: same token already authenticated, re-firing session-updated event only');
      window.dispatchEvent(new CustomEvent(INMA_SESSION_UPDATED_EVENT));
      return;
    }

    console.log('[inma-debug] bootstrap state -> bootstrapping, writing tokens to localStorage');
    store.setState('bootstrapping');
    api.storeTokens(accessToken, '');

    try {
      console.log('[inma-debug] calling api.exchangeInmaToken()');
      await api.exchangeInmaToken();
      console.log('[inma-debug] exchangeInmaToken SUCCESS', {
        newSession: api.getSession(),
        isAuthenticated: api.isAuthenticated(),
      });
    } catch (err) {
      // Stale token/session artifact cleanup: exchange basarisizsa onceden yazilan
      // raw INMA JWT'yi localStorage'da birakma (auth kacagi riski).
      console.error('[inma-debug] exchangeInmaToken FAILED', err);
      api.removeTokens();
      useInmaSession.getState().setError(INMA_ERRORS.EXCHANGE_FAILED);
      useInmaBootstrap.getState().setState('error', INMA_ERRORS.EXCHANGE_FAILED);
      console.warn('[inmaBootstrap] exchangeInmaToken failed:', err);
      return;
    }

    console.log('[inma-debug] bootstrap state -> authenticated');
    useInmaBootstrap.getState().setState('authenticated');

    api.getWelcome().catch((err) => {
      // Non-critical: session state='authenticated' kalir, sadece diagnostic kod
      // raporlanir (INV-INT-108) — InmaConnectionStatus arıza overlay'i tetiklenmez.
      console.warn('[inma-debug] getWelcome FAILED (non-critical)', err);
      useInmaBootstrap.getState().setDiagnosticCode(INMA_ERRORS.WELCOME_FAILED);
      console.warn(`[inmaBootstrap] welcome fetch failed (${INMA_ERRORS.WELCOME_FAILED}):`, err);
    });

    console.log('[inma-debug] dispatching window event', { event: INMA_SESSION_UPDATED_EVENT });
    window.dispatchEvent(new CustomEvent(INMA_SESSION_UPDATED_EVENT));
  }

  clear(): void {
    console.log('[inma-debug] bootstrap.clear called -> removeTokens + session.clear + cleared event');
    api.removeTokens();
    useInmaSession.getState().clear();
    useInmaBootstrap.getState().setState('idle');
    window.dispatchEvent(new CustomEvent(INMA_SESSION_CLEARED_EVENT));
  }
}

export const inmaBootstrap = new InmaBootstrapImpl();
