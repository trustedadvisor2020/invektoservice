import { createContext, useContext } from 'react';

// -- Types --

export interface AuthSession {
  token: string;
  tenant_id: number;
  expires_at: number; // epoch ms
}

export interface AuthContextValue {
  session: AuthSession | null;
  login: (tenantId: number, apiKey: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
}

// -- Storage --

const STORAGE_KEY = 'fb_session';

export function loadSession(): AuthSession | null {
  // Check localStorage first (Dashboard writes fb_session there for iframe sharing)
  // then fall back to sessionStorage (own login)
  for (const store of [localStorage, sessionStorage]) {
    try {
      const raw = store.getItem(STORAGE_KEY);
      if (!raw) continue;
      const s: AuthSession = JSON.parse(raw);
      if (Date.now() >= s.expires_at) {
        store.removeItem(STORAGE_KEY);
        continue;
      }
      return s;
    } catch (err: unknown) {
      console.warn('[FlowBuilder] fb_session parse failed, clearing:', err);
      store.removeItem(STORAGE_KEY);
    }
  }
  return null;
}

export function saveSession(session: AuthSession): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(): void {
  sessionStorage.removeItem(STORAGE_KEY);
}

// -- Context --

export const AuthContext = createContext<AuthContextValue>({
  session: null,
  login: async () => {},
  logout: () => {},
  isAuthenticated: false,
});

export function useAuth(): AuthContextValue {
  return useContext(AuthContext);
}
