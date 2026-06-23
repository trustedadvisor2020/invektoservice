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
}

export const useInmaBootstrap = create<InmaBootstrapStore>((set) => ({
  state: 'idle',
  lastCode: null,
  setState: (state, lastCode = null) => set({ state, lastCode }),
}));

class InmaBootstrapImpl {
  async run(): Promise<void> {
    const store = useInmaBootstrap.getState();
    const { accessToken } = useInmaSession.getState();
    if (!accessToken) {
      return;
    }

    // Idempotency: same token already active -> skip network calls, just resync consumers.
    if (store.state === 'authenticated' && api.getAccessToken() === accessToken) {
      window.dispatchEvent(new CustomEvent(INMA_SESSION_UPDATED_EVENT));
      return;
    }

    store.setState('bootstrapping');
    api.storeTokens(accessToken, '');

    try {
      await api.exchangeInmaToken();
    } catch (err) {
      // Stale token/session artifact cleanup: exchange basarisizsa onceden yazilan
      // raw INMA JWT'yi localStorage'da birakma (auth kacagi riski).
      api.removeTokens();
      useInmaSession.getState().setError(INMA_ERRORS.EXCHANGE_FAILED);
      useInmaBootstrap.getState().setState('error', INMA_ERRORS.EXCHANGE_FAILED);
      console.warn('[inmaBootstrap] exchangeInmaToken failed:', err);
      return;
    }

    useInmaBootstrap.getState().setState('authenticated');
    window.dispatchEvent(new CustomEvent(INMA_SESSION_UPDATED_EVENT));
  }

  clear(): void {
    api.removeTokens();
    useInmaSession.getState().clear();
    useInmaBootstrap.getState().setState('idle');
    window.dispatchEvent(new CustomEvent(INMA_SESSION_CLEARED_EVENT));
  }
}

export const inmaBootstrap = new InmaBootstrapImpl();
