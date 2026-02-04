import { useState, useCallback, useEffect } from 'react';
import { api } from '../lib/api';

export function useAuth() {
  const [isAuthenticated, setIsAuthenticated] = useState(api.isAuthenticated());
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setIsAuthenticated(api.isAuthenticated());
  }, []);

  const login = useCallback(async (username: string, password: string): Promise<boolean> => {
    setIsLoading(true);
    setError(null);

    try {
      api.setCredentials(username, password);
      // Test credentials by making a request
      await api.getOpsStatus();
      setIsAuthenticated(true);
      return true;
    } catch (err) {
      api.clearCredentials();
      setIsAuthenticated(false);
      setError(err instanceof Error ? err.message : 'Login failed');
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const logout = useCallback(() => {
    api.clearCredentials();
    setIsAuthenticated(false);
  }, []);

  return {
    isAuthenticated,
    isLoading,
    error,
    login,
    logout,
  };
}
