import { Routes, Route, Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './hooks/useAuth';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { TenantDashboardPage } from './pages/TenantDashboardPage';
import { LogsPage } from './pages/LogsPage';
import { KnowledgePage } from './pages/KnowledgePage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { FlowBuilderPage } from './pages/FlowBuilderPage';
import { CampaignsPage } from './pages/CampaignsPage';
import { AppointmentsPage } from './pages/AppointmentsPage';
import { IntegrationsPage } from './pages/IntegrationsPage';
import { MarketingPage } from './pages/MarketingPage';
import { MessagesPage } from './pages/MessagesPage';
import { SettingsPage } from './pages/SettingsPage';
import { Layout } from './components/Layout';

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
  return session ? <TenantDashboardPage /> : <DashboardPage />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<HomeDashboard />} />
        <Route path="/flow-builder-ui" element={<FlowBuilderPage />} />
        <Route path="/logs" element={<LogsPage />} />
        <Route path="/knowledge" element={<KnowledgePage />} />
        <Route path="/analytics" element={<AnalyticsPage />} />
        <Route path="/campaigns" element={<CampaignsPage />} />
        <Route path="/appointments" element={<AppointmentsPage />} />
        <Route path="/integrations" element={<IntegrationsPage />} />
        <Route path="/marketing" element={<MarketingPage />} />
        <Route path="/messages" element={<MessagesPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
