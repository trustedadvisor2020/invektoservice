import { Routes, Route, Navigate, Outlet } from 'react-router-dom';
import { lazy, Suspense } from 'react';
import { useAuth } from './hooks/useAuth';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { KnowledgePage } from './pages/KnowledgePage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { CampaignsPage } from './pages/CampaignsPage';
import { AppointmentsPage } from './pages/AppointmentsPage';
import { IntegrationsPage } from './pages/IntegrationsPage';
import { MarketingPage } from './pages/MarketingPage';
import { MessagesPage } from './pages/MessagesPage';
import { TenantsPage } from './pages/TenantsPage';
import { SettingsPage } from './pages/SettingsPage';
import { OnboardingGuidePage } from './pages/OnboardingGuidePage';
import { OnboardingWizardPage } from './pages/OnboardingWizardPage';
import { LogsPage } from './pages/LogsPage';
import { WebChatPage } from './pages/WebChatPage';
import { LicensesPage } from './pages/LicensesPage';
import { PaymentPage } from './pages/PaymentPage';

const FlowListPage = lazy(() => import('./pages/flow-builder/FlowListPage').then(m => ({ default: m.FlowListPage })));
const FlowEditorPage = lazy(() => import('./pages/flow-builder/FlowEditorPage').then(m => ({ default: m.FlowEditorPage })));
const WizardPage = lazy(() => import('./pages/flow-builder/WizardPage').then(m => ({ default: m.WizardPage })));

const TemplateLibraryPage = lazy(() => import('./pages/TemplateLibraryPage').then(m => ({ default: m.TemplateLibraryPage })));
const TemplateDetailPage = lazy(() => import('./pages/TemplateDetailPage').then(m => ({ default: m.TemplateDetailPage })));
const TemplateIngestionPage = lazy(() => import('./pages/TemplateIngestionPage').then(m => ({ default: m.TemplateIngestionPage })));
const TemplateOnboardPage = lazy(() => import('./pages/TemplateOnboardPage').then(m => ({ default: m.TemplateOnboardPage })));
const IntentManagementPage = lazy(() => import('./pages/IntentManagementPage').then(m => ({ default: m.IntentManagementPage })));
const RevenueIntelligencePage = lazy(() => import('./pages/RevenueIntelligencePage').then(m => ({ default: m.RevenueIntelligencePage })));
const RiTemplateManagementPage = lazy(() => import('./pages/RiTemplateManagementPage').then(m => ({ default: m.RiTemplateManagementPage })));
const FlowMonitorPage = lazy(() => import('./pages/FlowMonitorPage').then(m => ({ default: m.FlowMonitorPage })));
const RescueDashboardPage = lazy(() => import('./pages/RescueDashboardPage').then(m => ({ default: m.RescueDashboardPage })));

function ProtectedRoute() {
  const { isAuthenticated } = useAuth();

  if (!isAuthenticated) {
    const isSuper = window.location.hostname === 'super.invekto.com';
    document.title = isSuper ? 'Invekto Super' : 'Invekto AI';
    return <Navigate to="/login" replace />;
  }

  const isSuper = window.location.hostname === 'super.invekto.com';
  document.title = isSuper ? 'Invekto Super - OPS' : 'Invekto AI';

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

function TenantDashboardPage() {
  return <DashboardPage />;
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
        <Route path="/flow-monitor" element={<Suspense><FlowMonitorPage /></Suspense>} />
        <Route path="/logs" element={<LogsPage />} />
        <Route path="/knowledge" element={<KnowledgePage />} />
        <Route path="/analytics" element={<AnalyticsPage />} />
        <Route path="/revenue-intelligence" element={<Suspense><RevenueIntelligencePage /></Suspense>} />
        <Route path="/campaigns" element={<CampaignsPage />} />
        <Route path="/appointments" element={<AppointmentsPage />} />
        <Route path="/integrations" element={<IntegrationsPage />} />
        <Route path="/marketing" element={<MarketingPage />} />
        <Route path="/rescue" element={<Suspense><RescueDashboardPage /></Suspense>} />
        <Route path="/messages" element={<MessagesPage />} />
        <Route path="/webchat" element={<WebChatPage />} />
        <Route path="/tenants" element={<TenantsPage />} />
        <Route path="/licenses" element={<LicensesPage />} />
        <Route path="/payment" element={<PaymentPage />} />
        <Route path="/templates" element={<Suspense><TemplateLibraryPage /></Suspense>} />
        <Route path="/templates/ingestion" element={<Suspense><TemplateIngestionPage /></Suspense>} />
        <Route path="/templates/onboard" element={<Suspense><TemplateOnboardPage /></Suspense>} />
        <Route path="/templates/:id" element={<Suspense><TemplateDetailPage /></Suspense>} />
        <Route path="/intents" element={<Suspense><IntentManagementPage /></Suspense>} />
        <Route path="/ri/templates" element={<Suspense><RiTemplateManagementPage /></Suspense>} />
        <Route path="/onboarding" element={<OnboardingWizardPage />} />
        <Route path="/onboarding-guide" element={<OnboardingGuidePage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
