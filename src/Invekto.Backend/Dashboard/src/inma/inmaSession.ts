import { create } from 'zustand';

export type InmaStatus = 'idle' | 'ready' | 'error' | 'ended';

export interface InmaSessionState {
  accessToken: string | null;
  apiBaseUrl: string | null;
  status: InmaStatus;
  error: string | null;
  trustedParentOrigin: string | null;

  setAuth: (accessToken: string, apiBaseUrl: string, parentOrigin: string) => void;
  setAccessToken: (accessToken: string) => void;
  setStatus: (status: InmaStatus) => void;
  setError: (reason: string) => void;
  clear: () => void;
}

const initial = {
  accessToken: null,
  apiBaseUrl: null,
  status: 'idle' as InmaStatus,
  error: null,
  trustedParentOrigin: null,
};

export const useInmaSession = create<InmaSessionState>((set) => ({
  ...initial,
  setAuth: (accessToken, apiBaseUrl, parentOrigin) =>
    set({ accessToken, apiBaseUrl, trustedParentOrigin: parentOrigin, status: 'ready', error: null }),
  setAccessToken: (accessToken) => set({ accessToken }),
  setStatus: (status) => set({ status }),
  setError: (reason) => set({ status: 'error', error: reason }),
  clear: () => set({ ...initial, status: 'ended' }),
}));

export const inmaSession = {
  get: () => useInmaSession.getState(),
  subscribe: useInmaSession.subscribe,
};
