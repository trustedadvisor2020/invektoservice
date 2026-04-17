import { useState, useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type InseSession } from '../lib/api';
import { INMA_SESSION_UPDATED_EVENT, INMA_SESSION_CLEARED_EVENT } from '../inma';

export function useAuth() {
  const navigate = useNavigate();

  // Synchronous URL token extraction — runs BEFORE first render
  // so ProtectedRoute sees isAuthenticated=true immediately.
  // INMA may send camelCase or lowercase param names — check both.
  const [urlTokenHandled] = useState(() => {
    const params = new URLSearchParams(window.location.search);
    const accessToken = params.get('accesstoken') || params.get('accessToken');
    const refreshToken = params.get('refreshtoken') || params.get('refreshToken');
    if (!accessToken) return false;

    // URL'den token'lari temizle (browser history'de kalmasin)
    window.history.replaceState(null, '', window.location.pathname);

    // Token'lari localStorage'a kaydet
    api.storeTokens(accessToken, refreshToken ?? '');
    return true;
  });

  const [isAuthenticated, setIsAuthenticated] = useState(api.isAuthenticated());
  const [session, setSession] = useState<InseSession | null>(api.getSession());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [welcomeData, setWelcomeData] = useState<unknown>(null);

  // Post-mount: navigate + welcome fetch + INMA token exchange for URL SSO flow
  useEffect(() => {
    if (!urlTokenHandled) return;

    if (session) {
      // Exchange INMA JWT for INSE JWT so FlowBuilder endpoints can validate token.
      // Rejection = backend rejected the raw INMA JWT (e.g. missing CompanyCode);
      // clear stale tokens so a stale raw-INMA session cannot masquerade as authenticated.
      api.exchangeInmaToken()
        .then(() => setSession(api.getSession()))
        .catch(err => {
          console.warn('[useAuth] URL SSO exchange failed:', err);
          api.removeTokens();
          setIsAuthenticated(false);
          setSession(null);
          setError('INMA oturumu dogrulanamadi');
        });

      api.getWelcome()
        .then(data => setWelcomeData(data))
        .catch(err => console.warn('[useAuth] welcome fetch failed:', err));
      navigate('/', { replace: true });
    } else {
      api.removeTokens();
      setError('Token gecersiz veya suresi dolmus');
      setIsAuthenticated(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Auto-exchange on mount: existing sessions may hold raw INMA JWT.
  // exchangeInmaToken is a no-op when token is already INSE (no CompanyCode claim).
  useEffect(() => {
    if (urlTokenHandled) return; // URL SSO effect handles this separately
    if (!isAuthenticated || !session) return;

    api.exchangeInmaToken()
      .then(() => {
        const updated = api.getSession();
        if (updated) setSession(updated);
      })
      .catch(err => {
        // Stored token is raw INMA JWT and backend rejected it (e.g. expired
        // or malformed claim). Clear so the UI cannot keep a bogus session.
        console.warn('[useAuth] auto-exchange failed, clearing stale token:', err);
        api.removeTokens();
        setIsAuthenticated(false);
        setSession(null);
      });
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Faz 2: postMessage bootstrap session sync. inmaBootstrap.run/clear
  // CustomEvent firar; bu listener existing state hook'larini yeniden okur.
  useEffect(() => {
    console.log('[inma-debug] useAuth registering window event listeners', {
      updated: INMA_SESSION_UPDATED_EVENT,
      cleared: INMA_SESSION_CLEARED_EVENT,
    });
    const onUpdated = () => {
      const auth = api.isAuthenticated();
      const sess = api.getSession();
      console.log('[inma-debug] useAuth onUpdated fired', { isAuthenticated: auth, hasSession: !!sess, session: sess });
      setIsAuthenticated(auth);
      setSession(sess);
    };
    const onCleared = () => {
      console.log('[inma-debug] useAuth onCleared fired');
      setIsAuthenticated(false);
      setSession(null);
      setWelcomeData(null);
    };
    window.addEventListener(INMA_SESSION_UPDATED_EVENT, onUpdated);
    window.addEventListener(INMA_SESSION_CLEARED_EVENT, onCleared);
    return () => {
      window.removeEventListener(INMA_SESSION_UPDATED_EVENT, onUpdated);
      window.removeEventListener(INMA_SESSION_CLEARED_EVENT, onCleared);
    };
  }, []);

  // Ops Basic Auth login (degismiyor)
  const loginWithOps = useCallback(async (username: string, password: string): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    try {
      api.setCredentials(username, password);
      await api.getOpsStatus();
      setIsAuthenticated(true);
      setSession(null);
      return true;
    } catch (err: unknown) {
      api.clearCredentials();
      setIsAuthenticated(false);
      setError(err instanceof Error ? err.message : 'Login failed');
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Ops superadmin hizli giris (MockEnabled gate)
  const loginWithQuickAdmin = useCallback(async (): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    try {
      const resp = await api.quickAdminLogin();
      api.setSession(resp);
      setSession(api.getSession());
      setIsAuthenticated(true);
      return true;
    } catch (err: unknown) {
      api.clearSession();
      setIsAuthenticated(false);
      setError(err instanceof Error ? err.message : 'Hizli giris basarisiz');
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  // inma mock login (DEV only)
  const loginWithMock = useCallback(async (
    scenario: 'full' | 'klinik' | 'otel'
  ): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    try {
      const resp = await api.mockLogin(scenario);
      api.setSession(resp);
      setSession(api.getSession());
      setIsAuthenticated(true);
      return true;
    } catch (err: unknown) {
      api.clearSession();
      setIsAuthenticated(false);
      setError(err instanceof Error ? err.message : 'Mock login basarisiz');
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  // inma SSO login (firma adi + kullanici + parola)
  const loginWithInma = useCallback(async (
    companyName: string,
    username: string,
    password: string
  ): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    try {
      const resp = await api.loginWithInmaCredentials(companyName, username, password);
      api.setSession(resp);

      // Exchange raw INMA JWT → INSE JWT so FlowBuilder + other JWT endpoints work
      await api.exchangeInmaToken();

      setSession(api.getSession());
      setIsAuthenticated(true);

      // Welcome endpoint'ini cagir (non-critical — log and continue)
      api.getWelcome()
        .then(data => setWelcomeData(data))
        .catch(err => console.warn('[useAuth] welcome fetch failed:', err));

      return true;
    } catch (err: unknown) {
      api.clearSession();
      setIsAuthenticated(false);
      setError(err instanceof Error ? err.message : 'Giris basarisiz');
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    api.removeTokens();
    api.clearCredentials();
    setIsAuthenticated(false);
    setSession(null);
    setWelcomeData(null);
    navigate('/login', { replace: true });
  }, [navigate]);

  return {
    isAuthenticated,
    session,
    isInmaSession: api.isInmaSession(),
    isLoading,
    error,
    welcomeData,
    loginWithOps,
    loginWithQuickAdmin,
    loginWithInma,
    loginWithMock,
    login: loginWithOps,
    logout,
  };
}
