import { useState, useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type InseSession } from '../lib/api';

export function useAuth() {
  const navigate = useNavigate();

  // Synchronous URL token extraction — runs BEFORE first render
  // so ProtectedRoute sees isAuthenticated=true immediately
  const [urlTokenHandled] = useState(() => {
    const params = new URLSearchParams(window.location.search);
    const accessToken = params.get('accesstoken');
    const refreshToken = params.get('refreshtoken');
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

  // Post-mount: navigate + welcome fetch (effects can't run during init)
  useEffect(() => {
    if (!urlTokenHandled) return;

    if (session) {
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
