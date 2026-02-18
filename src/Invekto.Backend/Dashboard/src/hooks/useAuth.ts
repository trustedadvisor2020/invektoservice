import { useState, useCallback, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type InseSession } from '../lib/api';

export function useAuth() {
  const navigate = useNavigate();
  const [isAuthenticated, setIsAuthenticated] = useState(api.isAuthenticated());
  const [session, setSession] = useState<InseSession | null>(api.getSession());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // URL token detection: ?accesstoken= parametresi varsa otomatik exchange yap
  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const inmaToken = params.get('accesstoken');
    if (!inmaToken) return;

    // URL'den token'i temizle (browser history'de kalmasin)
    const cleanUrl = window.location.pathname;
    window.history.replaceState(null, '', cleanUrl);

    setIsLoading(true);
    setError(null);

    api.exchangeInmaToken(inmaToken)
      .then(resp => {
        api.setSession(resp);
        setSession(api.getSession());
        setIsAuthenticated(true);
        navigate('/', { replace: true });
      })
      .catch(err => {
        setError(err instanceof Error ? err.message : 'Token dogrulanamadi');
        setIsAuthenticated(false);
      })
      .finally(() => setIsLoading(false));
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Ops Basic Auth login (mevcut, degismiyor)
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

  // Ops superadmin hizli giris (MockEnabled gate) — sifre gerekmez
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

  // inma mock login (DEV only — InmaAuth:MockEnabled=true)
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
    api.clearCredentials();
    api.clearSession();
    setIsAuthenticated(false);
    setSession(null);
  }, []);

  return {
    isAuthenticated,
    session,
    isInmaSession: api.isInmaSession(),
    isLoading,
    error,
    loginWithOps,
    loginWithQuickAdmin,
    loginWithInma,
    loginWithMock,
    // legacy alias — LoginPage'deki ops login icin backward compat
    login: loginWithOps,
    logout,
  };
}
