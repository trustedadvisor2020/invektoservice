import { useState, useCallback, type ReactNode } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import {
  AuthContext,
  type AuthSession,
  loadSession,
  saveSession,
  clearSession,
} from './lib/auth';
import { login as apiLogin } from './lib/api';
import { LoginPage } from './pages/LoginPage';
import { FlowListPage } from './pages/FlowListPage';
import { FlowEditorPage } from './pages/FlowEditorPage';

// -- Auth Provider --

function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(loadSession);

  const login = useCallback(async (tenantId: number, apiKey: string) => {
    const res = await apiLogin(tenantId, apiKey);
    const newSession: AuthSession = {
      token: res.token,
      tenant_id: res.tenant_id,
      expires_at: Date.now() + res.expires_in * 1000,
    };
    saveSession(newSession);
    setSession(newSession);
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  return (
    <AuthContext.Provider value={{ session, login, logout, isAuthenticated: session !== null }}>
      {children}
    </AuthContext.Provider>
  );
}

// -- Protected Route --

function RequireAuth({ children }: { children: ReactNode }) {
  const session = loadSession();
  if (!session) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

// -- App --

export default function App() {
  return (
    <BrowserRouter basename="/flow-builder">
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/"
            element={
              <RequireAuth>
                <FlowListPage />
              </RequireAuth>
            }
          />
          <Route
            path="/editor/:flowId"
            element={
              <RequireAuth>
                <FlowEditorPage />
              </RequireAuth>
            }
          />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  );
}
