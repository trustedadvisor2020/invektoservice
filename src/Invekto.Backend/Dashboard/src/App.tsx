import { lazy, Suspense } from 'react';
import { Routes, Route, Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './hooks/useAuth';
import { LoginPage } from './pages/LoginPage';
import { TenantDashboardPage } from './pages/TenantDashboardPage';
import { LogsPage } from './pages/LogsPage';
import { KnowledgePage } from './pages/KnowledgePage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { CampaignsPage } from './pages/CampaignsPage';
import { AppointmentsPage } from './pages/AppointmentsPage';
import { IntegrationsPage } from './pages/IntegrationsPage';
import { MarketingPage } from './pages/MarketingPage';
import { MessagesPage } from './pages/MessagesPage';
import { TenantsPage } from './pages/TenantsPage';
import { SettingsPage } from './pages/SettingsPage';
import { Layout } from './components/Layout';

const FlowListPage = lazy(() => import('./pages/flow-builder/FlowListPage').then(m => ({ default: m.FlowListPage })));
const FlowEditorPage = lazy(() => import('./pages/flow-builder/FlowEditorPage').then(m => ({ default: m.FlowEditorPage })));
const WizardPage = lazy(() => import('./pages/flow-builder/WizardPage').then(m => ({ default: m.WizardPage })));

function ProtectedRoute() {
  const { isAuthenticated } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return (
    <Layout>
      <Outlet />
    </Layout>
  );
}

function HomeDashboard() {
  const { session } = useAuth();
  // Ops mode: /tenants acilis sayfasi. Tenant mode: TenantDashboardPage.
  return session ? <TenantDashboardPage /> : <Navigate to="/tenants" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<HomeDashboard />} />
        <Route path="/flow-builder" element={<Suspense><FlowListPage /></Suspense>} />
        <Route path="/flow-builder/editor/:flowId" element={<Suspense><FlowEditorPage /></Suspense>} />
        <Route path="/flow-builder/wizard/:flowId" element={<Suspense><WizardPage /></Suspense>} />
        <Route path="/logs" element={<LogsPage />} />
        <Route path="/knowledge" element={<KnowledgePage />} />
        <Route path="/analytics" element={<AnalyticsPage />} />
        <Route path="/campaigns" element={<CampaignsPage />} />
        <Route path="/appointments" element={<AppointmentsPage />} />
        <Route path="/integrations" element={<IntegrationsPage />} />
        <Route path="/marketing" element={<MarketingPage />} />
        <Route path="/messages" element={<MessagesPage />} />
        <Route path="/tenants" element={<TenantsPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
