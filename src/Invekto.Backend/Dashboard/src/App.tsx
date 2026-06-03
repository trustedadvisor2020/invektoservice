import { Routes, Route, Navigate, Outlet, useNavigate, useParams } from 'react-router-dom';
import { lazy, Suspense, useEffect } from 'react';
import { inmaBridge, inmaBootstrap, INMA_ERRORS } from './inma';
import { inmaErrorMessage } from './inma/inmaErrors';
import { api } from './lib/api';
import { useAuth } from './hooks/useAuth';
import { Layout } from './components/Layout';
import { LoginPage } from './pages/LoginPage';
import { DashboardPage } from './pages/DashboardPage';
import { KnowledgePage } from './pages/KnowledgePage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { CampaignsPage } from './pages/CampaignsPage';
import { AppointmentsPage } from './pages/AppointmentsPage';
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
import { VoiceTestPage } from './pages/VoiceTestPage';

const FlowListPage = lazy(() => import('./pages/flow-builder/FlowListPage').then(m => ({ default: m.FlowListPage })));
const FlowEditorPage = lazy(() => import('./pages/flow-builder/FlowEditorPage').then(m => ({ default: m.FlowEditorPage })));
const WizardPage = lazy(() => import('./pages/flow-builder/WizardPage').then(m => ({ default: m.WizardPage })));

const TemplateLibraryPage = lazy(() => import('./pages/TemplateLibraryPage').then(m => ({ default: m.TemplateLibraryPage })));
const TemplateCreatePage = lazy(() => import('./pages/TemplateCreatePage').then(m => ({ default: m.TemplateCreatePage })));
const TemplateDetailPage = lazy(() => import('./pages/TemplateDetailPage').then(m => ({ default: m.TemplateDetailPage })));
const TemplateIngestionPage = lazy(() => import('./pages/TemplateIngestionPage').then(m => ({ default: m.TemplateIngestionPage })));
const TemplateOnboardPage = lazy(() => import('./pages/TemplateOnboardPage').then(m => ({ default: m.TemplateOnboardPage })));
const IntentManagementPage = lazy(() => import('./pages/IntentManagementPage').then(m => ({ default: m.IntentManagementPage })));
const RevenueIntelligencePage = lazy(() => import('./pages/RevenueIntelligencePage').then(m => ({ default: m.RevenueIntelligencePage })));
const RiTemplateManagementPage = lazy(() => import('./pages/RiTemplateManagementPage').then(m => ({ default: m.RiTemplateManagementPage })));
const FlowMonitorPage = lazy(() => import('./pages/FlowMonitorPage').then(m => ({ default: m.FlowMonitorPage })));
const FlowTemplateGalleryPage = lazy(() => import('./pages/FlowTemplateGalleryPage').then(m => ({ default: m.FlowTemplateGalleryPage })));
const RescueDashboardPage = lazy(() => import('./pages/RescueDashboardPage').then(m => ({ default: m.RescueDashboardPage })));

// FEAT-LIW Chunk C: tenant landing settings (lazy).
const LeadIntakeSettingsPage = lazy(() => import('./pages/settings/LeadIntakeSettingsPage').then(m => ({ default: m.LeadIntakeSettingsPage })));

// FEAT-TFM-UI P3: tenant field mapping editor (lazy).
const FieldMappingSettingsPage = lazy(() => import('./pages/settings/FieldMappingSettingsPage').then(m => ({ default: m.FieldMappingSettingsPage })));

// FEAT-EFS Drip Sequence P5: follow-up sequence editor (lazy).
const FollowupSequenceSettingsPage = lazy(() => import('./pages/settings/FollowupSequenceSettingsPage').then(m => ({ default: m.FollowupSequenceSettingsPage })));

// FEAT-MCC Multi-City Campaign P6: campaign config editor (lazy).
const CampaignConfigSettingsPage = lazy(() => import('./pages/settings/CampaignConfigSettingsPage').then(m => ({ default: m.CampaignConfigSettingsPage })));

// Paket B-META: Meta Leadgen webhook settings (lazy).
const MetaLeadgenSettingsPage = lazy(() => import('./pages/settings/MetaLeadgenSettingsPage').then(m => ({ default: m.MetaLeadgenSettingsPage })));

// FEAT-CLINIC-METADATA: clinic_contact + team_members editor (lazy).
const ClinicMetadataSettingsPage = lazy(() => import('./pages/settings/ClinicMetadataSettingsPage').then(m => ({ default: m.ClinicMetadataSettingsPage })));

// FEAT-PILOT-KANBAN: SuperAdmin pilot tracking board (lazy, opsOnly).
const PilotKanbanPage = lazy(() => import('./pages/PilotKanbanPage').then(m => ({ default: m.PilotKanbanPage })));

// FEAT-PHOTO wire-up patch (2026-04-28): Lead detay sayfasi (PhotoTab tuketicisi).
// Parent paket commit 1da0da6 component-ready; runtime route binding burada.
const LeadDetailPage = lazy(() => import('./pages/leads/LeadDetailPage').then(m => ({ default: m.LeadDetailPage })));

// FEAT-OBI Phase 1A: CSV/Excel contact-list import + list->bulk-send (lazy, tenant-scoped).
const DataImportPage = lazy(() => import('./pages/DataImportPage').then(m => ({ default: m.DataImportPage })));

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

// FEAT-PHOTO wire-up patch (2026-04-28): /leads/:id route wrapper.
// LeadDetailPage `leadId: number` prop bekliyor; URL `:id` string'ini integer'a
// cevir. Invalid id (NaN veya <=0) durumunda /tenants ana sayfaya yonlendir.
function LeadDetailRoute() {
  const { id } = useParams<{ id: string }>();
  const leadId = Number(id);
  if (!Number.isFinite(leadId) || leadId <= 0) {
    return <Navigate to="/tenants" replace />;
  }
  return <LeadDetailPage leadId={leadId} />;
}

function TenantDashboardPage() {
  return <DashboardPage />;
}

export default function App() {
  const navigate = useNavigate();
  useEffect(() => {
    const dispose = inmaBridge.init({
      onReady: () => {
        void inmaBootstrap.run();
      },
      onLogout: () => {
        inmaBootstrap.clear();
      },
      onNavigate: (path) => {
        if (!api.isAuthenticated()) {
          console.warn(inmaErrorMessage(INMA_ERRORS.NAVIGATE_REJECTED, 'unauthenticated'));
          return;
        }
        navigate(path);
      },
    });
    return dispose;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/" element={<HomeDashboard />} />
        <Route path="/flow-builder" element={<Suspense><FlowListPage /></Suspense>} />
        <Route path="/flow-builder/editor/:flowId" element={<Suspense><FlowEditorPage /></Suspense>} />
        <Route path="/flow-builder/wizard/:flowId" element={<Suspense><WizardPage /></Suspense>} />
        <Route path="/flow-templates" element={<Suspense><FlowTemplateGalleryPage /></Suspense>} />
        <Route path="/flow-monitor" element={<Suspense><FlowMonitorPage /></Suspense>} />
        <Route path="/logs" element={<LogsPage />} />
        <Route path="/knowledge" element={<KnowledgePage />} />
        <Route path="/analytics" element={<AnalyticsPage />} />
        <Route path="/revenue-intelligence" element={<Suspense><RevenueIntelligencePage /></Suspense>} />
        <Route path="/campaigns" element={<CampaignsPage />} />
        <Route path="/appointments" element={<AppointmentsPage />} />
        <Route path="/marketing" element={<MarketingPage />} />
        <Route path="/rescue" element={<Suspense><RescueDashboardPage /></Suspense>} />
        <Route path="/data-import" element={<Suspense><DataImportPage /></Suspense>} />
        <Route path="/messages" element={<MessagesPage />} />
        <Route path="/webchat" element={<WebChatPage />} />
        {/* FEAT-VFB: Voice Test embedded in-dashboard (was Layout.tsx external new-tab). */}
        <Route path="/voice-test" element={<VoiceTestPage />} />
        <Route path="/tenants" element={<TenantsPage />} />
        {/* FEAT-ROADMAP-V2: yeni URL /yol-haritasi/:boardKey? + backward-compat /pilot-kanban.
            Audit fix D035 (2026-04-29 Batch C): /pilot-kanban alias-render yerine
            canonical URL'e redirect — eski bookmark/link calismaya devam ama URL bar
            /yol-haritasi/inse olarak guncellenir (consistency + analytics).
            Migration 039 (2026-04-29): board_key 'dent-pilot' -> 'inse' rename
            (Q karari: tek INSE platform genel board, icinde pilot kart'lari da). */}
        <Route path="/yol-haritasi" element={<Suspense><PilotKanbanPage /></Suspense>} />
        <Route path="/yol-haritasi/:boardKey" element={<Suspense><PilotKanbanPage /></Suspense>} />
        <Route path="/pilot-kanban" element={<Navigate to="/yol-haritasi/inse" replace />} />
        <Route path="/licenses" element={<LicensesPage />} />
        <Route path="/payment" element={<PaymentPage />} />
        <Route path="/templates" element={<Suspense><TemplateLibraryPage /></Suspense>} />
        <Route path="/templates/new" element={<Suspense><TemplateCreatePage /></Suspense>} />
        <Route path="/templates/ingestion" element={<Suspense><TemplateIngestionPage /></Suspense>} />
        <Route path="/templates/onboard" element={<Suspense><TemplateOnboardPage /></Suspense>} />
        <Route path="/templates/:id" element={<Suspense><TemplateDetailPage /></Suspense>} />
        <Route path="/intents" element={<Suspense><IntentManagementPage /></Suspense>} />
        <Route path="/ri/templates" element={<Suspense><RiTemplateManagementPage /></Suspense>} />
        <Route path="/onboarding" element={<OnboardingWizardPage />} />
        <Route path="/onboarding-guide" element={<OnboardingGuidePage />} />
        <Route path="/settings" element={<SettingsPage />} />
        {/* FEAT-LIW Chunk C: standalone lead intake settings page (entered via SettingsPage 'Lead Kaynaklari' card). */}
        <Route path="/settings/lead-intake" element={<LeadIntakeSettingsPage />} />
        {/* FEAT-TFM-UI P3: standalone field mapping editor (entered via SettingsPage 'Field Mapping' card). */}
        <Route path="/settings/field-mapping" element={<Suspense><FieldMappingSettingsPage /></Suspense>} />
        <Route path="/settings/followup-sequence" element={<Suspense><FollowupSequenceSettingsPage /></Suspense>} />
        {/* FEAT-MCC Multi-City Campaign P6: campaign + city/date editor (entered via SettingsPage). */}
        <Route path="/settings/campaigns" element={<Suspense><CampaignConfigSettingsPage /></Suspense>} />
        {/* Paket B-META: Meta Leadgen webhook settings (entered via SettingsPage). */}
        <Route path="/settings/meta-leadgen" element={<Suspense><MetaLeadgenSettingsPage /></Suspense>} />
        <Route path="/settings/clinic-metadata" element={<Suspense><ClinicMetadataSettingsPage /></Suspense>} />
        {/* FEAT-PHOTO wire-up patch (2026-04-28): Lead detay sayfasi (PhotoTab tuketicisi). */}
        <Route path="/leads/:id" element={<Suspense><LeadDetailRoute /></Suspense>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
