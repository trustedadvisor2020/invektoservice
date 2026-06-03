import { useState, useCallback } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import { InmaConnectionStatus } from '../inma';
import {
  FileText,
  Power,
  LayoutDashboard,
  BookOpen,
  GraduationCap,
  GitBranch,
  Activity,
  Settings,
  MessageSquare,
  Building2,
  PanelLeftClose,
  LayoutTemplate,
  Upload,
  Brain,
  Rocket,
  Layers,
  Globe,
  Key,
  Clock,
  Kanban,
  Mic,
  Database,
} from 'lucide-react';
import { cn } from '../lib/utils';
import { InvektoLogo } from './ui/InvektoLogo';

const SIDEBAR_KEY = 'inse-sidebar-collapsed';

interface LayoutProps {
  children: React.ReactNode;
}

interface NavItem {
  path: string;
  label: string;
  tenantLabel?: string;
  icon: React.ComponentType<{ className?: string }>;
  feature?: string; // InseFeatures key — undefined = always visible
  opsOnly?: boolean;
  section?: string;
}

const ALL_NAV_ITEMS: NavItem[] = [
  // — Tenant: Çalışma Alanı —
  { path: '/',                label: 'Kontrol Paneli',   tenantLabel: 'Ana Sayfa', icon: LayoutDashboard, section: 'Çalışma Alanı' },
  { path: '/flow-builder', label: 'Flow Builder', tenantLabel: 'Flow Builder', icon: GitBranch,    feature: 'FlowBuilder' },
  { path: '/flow-templates', label: 'Sablon Galerisi', tenantLabel: 'Sablon Galerisi', icon: LayoutTemplate, feature: 'FlowBuilder' },
  { path: '/knowledge',       label: 'Bilgi Bankasi',    icon: BookOpen,     feature: 'Knowledge' },
  // FEAT-OBI: Veri Yönetimi — İçe Aktarma (Listeler) + Dışa Aktarma (Export) tek sayfada
  // iki sekme (tenant-visible; "Outbound" plan + ExportOptions gate'leri server-side enforce).
  { path: '/data-management', label: 'Veri Yönetimi',     icon: Database },
  // — Tenant: Analiz —
  { path: '/flow-monitor',    label: 'Flow Monitor',     tenantLabel: 'Flow Izleme', icon: Activity, feature: 'FlowBuilder', section: 'Analiz' },
  // — Ops: Yönetim —
  { path: '/tenants',         label: 'Firmalar',         icon: Building2,     opsOnly: true, section: 'Yönetim' },
  { path: '/yol-haritasi',    label: 'Yol Haritası',      icon: Kanban,        opsOnly: true },
  { path: '/licenses',        label: 'Lisanslama',        icon: Key,           opsOnly: true },
  // — Ops: İçerik —
  { path: '/templates',        label: 'Sablon Sistemi',    icon: LayoutTemplate,  opsOnly: true, section: 'İçerik' },
  { path: '/templates/ingestion', label: 'Veri Besleme',  icon: Upload,          opsOnly: true },
  { path: '/intents',             label: 'Intent Yonetimi', icon: Brain,          opsOnly: true },
  { path: '/ri/templates',        label: 'RI Sablonlari',   icon: Layers,         opsOnly: true },
  // — Ops: İletişim —
  { path: '/webchat',         label: 'WebChat',          icon: Globe,         opsOnly: true, section: 'İletişim' },
  { path: '/messages',        label: 'Mesajlar',         icon: MessageSquare, opsOnly: true },
  { path: '/logs',            label: 'Loglar',            icon: FileText,     opsOnly: true },
  { path: 'external:hangfire', label: 'Hangfire',         icon: Clock,        opsOnly: true },
  { path: '/voice-test',       label: 'Voice Test',     icon: Mic,          opsOnly: true },
  // — Shared —
  { path: '/onboarding',       label: 'Onboarding',       tenantLabel: 'Kurulum Sihirbazi', icon: Rocket },
  { path: '/onboarding-guide', label: 'Onboarding Rehberi', icon: GraduationCap, opsOnly: true },
  { path: '/settings',        label: 'Ayarlar',          icon: Settings },
];

export function Layout({ children }: LayoutProps) {
  const location = useLocation();
  const { logout, session } = useAuth();
  const isFullscreen = location.pathname.startsWith('/flow-builder') || location.pathname.startsWith('/voice-test');
  const isImpersonating = session && api.isImpersonating();
  // INMA parent shell (WapCRM) already renders an outer sidebar with equivalent
  // menu items; hide Dashboard's own sidebar when running inside an iframe.
  const isEmbedded = window !== window.parent;

  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDEBAR_KEY) === 'true');

  const toggleSidebar = useCallback(() => {
    setCollapsed(prev => {
      localStorage.setItem(SIDEBAR_KEY, String(!prev));
      return !prev;
    });
  }, []);

  const exitImpersonation = () => {
    api.removeTokens();
    window.location.href = '/app/';
  };

  // Ops mode (no session): sadece opsOnly + Ayarlar gorunur (Firmalar, Mesajlar, Loglar, Ayarlar).
  // Tenant mode (session var): feature flag'e gore filtrele, opsOnly gizle, Turkce label.
  const allFiltered = ALL_NAV_ITEMS.filter(item => {
    if (!session) {
      return item.opsOnly || item.path === '/settings';
    }
    if (item.opsOnly && session.tenantId !== 0) return false;
    if (!item.feature) return true;
    return api.hasFeature(item.feature);
  }).map(item => ({
    ...item,
    label: session && item.tenantLabel ? item.tenantLabel : item.label,
  }));

  // Split nav into main, secondary (onboarding/settings), and bottom (logout)
  const mainItems = allFiltered.filter(i => i.path !== '/settings' && i.path !== '/onboarding' && i.path !== '/onboarding-guide');
  const onboardingItem = allFiltered.find(i => i.path === '/onboarding');
  const settingsItem = allFiltered.find(i => i.path === '/settings');

  const renderNavLink = (item: NavItem & { label: string }) => {
    const Icon = item.icon;
    const isExternal = item.path.startsWith('external:');
    const isActive = !isExternal && (item.path === '/' ? location.pathname === '/' : location.pathname.startsWith(item.path));
    const commonClasses = cn(
      'group flex items-center rounded-lg text-sm font-medium transition-all duration-150',
      collapsed ? 'h-10 justify-center px-0' : 'h-[42px] gap-3 px-4 mx-1 mb-0.5',
      isActive
        ? 'nav-item-active'
        : 'text-slate-500 hover:bg-slate-100 hover:text-slate-900'
    );

    // FEAT-VFB: Voice Test now renders in-dashboard at /voice-test (VoiceTestPage embeds
    // voice.invekto.com:8443/voice-poc.html in a cross-origin iframe with the JWT bridged
    // via ?token=). It falls through to the default <Link> branch below — the token
    // exchange logic that used to live here moved into VoiceTestPage.

    // G7: Hangfire external link — cookie bridge via /ops/hangfire-login.
    // Uses current access_token from localStorage; backend validates (tenant_id=0
    // required) and redirects to /hangfire with an HttpOnly cookie.
    if (item.path === 'external:hangfire') {
      return (
        <button
          key={item.path}
          type="button"
          title={collapsed ? item.label : undefined}
          onClick={async () => {
            // Two auth paths accepted by /ops/hangfire-login:
            //  (1) INMA session -> JWT in localStorage.access_token as query param.
            //  (2) Ops mode -> Basic Auth from sessionStorage.ops_auth as header.
            const t = localStorage.getItem('access_token');
            if (t) {
              window.open('/ops/hangfire-login?token=' + encodeURIComponent(t), '_blank', 'noopener');
              return;
            }
            const opsAuth = sessionStorage.getItem('ops_auth');
            if (!opsAuth) { alert('Oturum bulunamadı (ne JWT ne Ops giriş)'); return; }
            // Manual fetch so we can attach Authorization header; follow redirect, then navigate.
            try {
              const r = await fetch('/ops/hangfire-login', {
                method: 'GET',
                headers: { Authorization: 'Basic ' + opsAuth },
                credentials: 'include',
                redirect: 'manual',
              });
              // Cookie is set by the response; open the dashboard directly.
              if (r.status === 401 || r.status === 403) {
                const body = await r.text().catch(() => '');
                alert('Hangfire girişi reddedildi: ' + (body || r.status));
                return;
              }
              window.open('/hangfire', '_blank', 'noopener');
            } catch (e) {
              alert('Hangfire giriş hatası: ' + (e instanceof Error ? e.message : String(e)));
            }
          }}
          className={cn(commonClasses, 'w-full text-left cursor-pointer bg-transparent')}
        >
          <Icon className={cn('w-5 h-5 flex-shrink-0', 'text-slate-500 group-hover:text-slate-900')} />
          {!collapsed && <span className="truncate">{item.label}</span>}
        </button>
      );
    }

    return (
      <Link
        key={item.path}
        to={item.path}
        title={collapsed ? item.label : undefined}
        className={commonClasses}
      >
        <Icon className={cn('w-5 h-5 flex-shrink-0', isActive ? '' : 'text-slate-500 group-hover:text-slate-900')} />
        {!collapsed && <span className="truncate">{item.label}</span>}
      </Link>
    );
  };

  return (
    <>
      {/* Impersonation banner */}
      {isImpersonating && (
        <div className="fixed top-0 left-0 right-0 bg-amber-500 text-white px-4 py-2 flex items-center justify-between text-sm font-medium z-50">
          <span>Firma #{session.tenantId} — SuperAdmin olarak goruntuleniyor</span>
          <button
            onClick={exitImpersonation}
            className="px-3 py-1 bg-white/20 hover:bg-white/30 rounded-md text-xs font-medium transition-colors"
          >
            Cikis
          </button>
        </div>
      )}

      <div className={cn('min-h-screen flex bg-navy-50', isImpersonating && 'pt-10')}>
        {/* Sidebar — hidden when embedded in INMA iframe (parent shell provides nav) */}
        {!isEmbedded && (
        <aside className={cn(
          'h-screen sticky top-0 bg-white border-r border-slate-200 z-10 flex flex-col transition-[width] duration-300 ease-out shadow-[4px_0_24px_rgba(0,0,0,0.02)]',
          collapsed ? 'w-[3.5rem]' : 'w-64'
        )}>
          {/* Logo + Toggle */}
          <div className={cn('h-[72px] flex items-center border-b border-slate-100 flex-shrink-0', collapsed ? 'px-1.5' : 'px-5')}>
            {collapsed ? (
              <button
                onClick={toggleSidebar}
                className="w-10 h-10 mx-auto flex items-center justify-center rounded-xl hover:bg-slate-50 transition-all duration-200"
                title="Menuyu ac"
              >
                <img src={`${import.meta.env.BASE_URL}logo.png`} alt="Invekto" className="w-8 h-8 rounded-xl" />
              </button>
            ) : (
              <>
                <img src={`${import.meta.env.BASE_URL}logo.png`} alt="" className="w-10 h-10 flex-shrink-0 rounded-xl" />
                <div className="min-w-0 ml-3 flex-1">
                  <InvektoLogo size="sm" className="block leading-tight" />
                  {session?.companyCode && (
                    <span className="text-[10px] text-slate-400 truncate block leading-tight mt-0.5 font-medium uppercase tracking-widest">
                      {session.companyCode}
                    </span>
                  )}
                </div>
              </>
            )}
          </div>

          {/* Main Navigation */}
          <nav className={cn('flex-1 overflow-y-auto overflow-x-hidden', collapsed ? 'px-1 py-3' : 'px-3 py-5')}>
            {mainItems.map((item, i) => {
              const showSection = item.section && !collapsed;
              const isFirst = i === 0;
              return (
                <div key={item.path}>
                  {showSection && (
                    <div className={cn(
                      'text-[11px] font-bold text-slate-400 uppercase tracking-widest px-4',
                      isFirst ? 'pb-2' : 'pt-6 pb-2'
                    )}>
                      {item.section}
                    </div>
                  )}
                  {renderNavLink(item)}
                </div>
              );
            })}
          </nav>

          {/* Version + Collapse */}
          {!collapsed && (
            <div className="px-5 py-1.5 flex items-center justify-between">
              <span className="text-2xs text-slate-300 font-medium">v{__BUILD_TIME__}</span>
              <button
                onClick={toggleSidebar}
                className="w-6 h-6 flex items-center justify-center rounded-lg text-slate-300 hover:bg-slate-100 hover:text-slate-500 transition-all duration-200"
                title="Menuyu kapat"
              >
                <PanelLeftClose className="w-3.5 h-3.5" />
              </button>
            </div>
          )}

          {/* Divider */}
          <div className="mx-4 h-px bg-slate-100" />

          {/* Bottom: Onboarding + Settings + Logout */}
          <div className={cn('py-2', collapsed ? 'px-1' : 'px-3')}>
            {onboardingItem && renderNavLink(onboardingItem)}
            {settingsItem && renderNavLink(settingsItem)}
            <button
              className={cn(
                'w-full flex items-center rounded-lg text-sm font-medium text-slate-400 hover:bg-slate-50 hover:text-slate-700 transition-all duration-150 group',
                collapsed ? 'h-10 justify-center px-0' : 'h-[42px] gap-3 px-4 mx-1 mb-0.5'
              )}
              onClick={logout}
              title={collapsed ? 'Cikis Yap' : undefined}
            >
              <Power className={cn('w-5 h-5 flex-shrink-0 text-slate-300 group-hover:text-red-400 transition-colors', collapsed ? '' : '')} strokeWidth={1.8} />
              {!collapsed && <span>Cikis Yap</span>}
            </button>
          </div>
        </aside>
        )}

        {/* Main content */}
        <main className="flex-1 overflow-auto">
          {isFullscreen ? children : (
            <div className="p-8 max-w-7xl mx-auto">
              {children}
            </div>
          )}
        </main>
      </div>

      <InmaConnectionStatus />
    </>
  );
}
