import { useState, useCallback } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import {
  FileText,
  Power,
  LayoutDashboard,
  BookOpen,
  GraduationCap,
  BarChart3,
  TrendingUp,
  GitBranch,
  Megaphone,
  CalendarDays,
  Link2,
  Star,
  Settings,
  MessageSquare,
  Building2,
  PanelLeftClose,
  LayoutTemplate,
  Upload,
  Brain,
  Rocket,
  Layers,
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
}

const ALL_NAV_ITEMS: NavItem[] = [
  { path: '/',                label: 'Kontrol Paneli',   tenantLabel: 'Ana Sayfa', icon: LayoutDashboard },
  { path: '/flow-builder', label: 'Flow Builder', tenantLabel: 'Flow Builder', icon: GitBranch,    feature: 'FlowBuilder' },
  { path: '/knowledge',       label: 'Bilgi Bankasi',    icon: BookOpen,     feature: 'Knowledge' },
  { path: '/campaigns',       label: 'Kampanyalar',      icon: Megaphone,    feature: 'Outbound' },
  { path: '/appointments',    label: 'Randevular',       icon: CalendarDays, feature: 'Appointments' },
  { path: '/analytics',       label: 'Analizler',        icon: BarChart3,    feature: 'Analytics' },
  { path: '/revenue-intelligence', label: 'Revenue Intelligence', tenantLabel: 'Gelir Analizi', icon: TrendingUp, feature: 'Analytics' },
  { path: '/integrations',    label: 'Entegrasyonlar',   icon: Link2,        feature: 'Integrations' },
  { path: '/marketing',       label: 'Pazarlama',        icon: Star,         feature: 'Marketing' },
  { path: '/tenants',         label: 'Firmalar',         icon: Building2,     opsOnly: true },
  { path: '/messages',        label: 'Mesajlar',         icon: MessageSquare, opsOnly: true },
  { path: '/logs',            label: 'Loglar',            icon: FileText,     opsOnly: true },
  { path: '/templates',        label: 'Sablon Sistemi',    icon: LayoutTemplate,  opsOnly: true },
  { path: '/templates/ingestion', label: 'Veri Besleme',  icon: Upload,          opsOnly: true },
  { path: '/intents',             label: 'Intent Yonetimi', icon: Brain,          opsOnly: true },
  { path: '/ri/templates',        label: 'RI Sablonlari',   icon: Layers,         opsOnly: true },
  { path: '/onboarding',       label: 'Onboarding',       tenantLabel: 'Kurulum Sihirbazi', icon: Rocket },
  { path: '/onboarding-guide', label: 'Onboarding Rehberi', icon: GraduationCap, opsOnly: true },
  { path: '/settings',        label: 'Ayarlar',          icon: Settings },
];

export function Layout({ children }: LayoutProps) {
  const location = useLocation();
  const { logout, session } = useAuth();
  const isFullscreen = location.pathname.startsWith('/flow-builder');
  const isImpersonating = session && api.isImpersonating();

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
    const isActive = item.path === '/' ? location.pathname === '/' : location.pathname.startsWith(item.path);

    return (
      <Link
        key={item.path}
        to={item.path}
        title={collapsed ? item.label : undefined}
        className={cn(
          'group flex items-center h-10 rounded-xl text-sm font-medium transition-all duration-200',
          collapsed ? 'justify-center px-0' : 'gap-2.5 px-2.5',
          isActive
            ? 'glass-nav-active text-brand-600 font-semibold'
            : 'text-navy-400 hover:glass-nav-hover hover:text-navy-700 hover:translate-x-0.5'
        )}
      >
        <div className={cn(
          'w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0 transition-all duration-200',
          isActive
            ? 'bg-brand-500/12 text-brand-500 shadow-[0_0_0_1px_rgba(99,91,255,0.1)]'
            : 'text-navy-300 group-hover:bg-navy-100/60 group-hover:text-navy-500'
        )}>
          <Icon className="w-[17px] h-[17px]" />
        </div>
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
        {/* Sidebar — Glass Morphism */}
        <aside className={cn(
          'h-screen sticky top-0 glass-sidebar shadow-glass z-10 flex flex-col transition-[width] duration-300 ease-out',
          collapsed ? 'w-[3.5rem]' : 'w-60'
        )}>
          {/* Logo + Toggle */}
          <div className={cn('h-14 flex items-center', collapsed ? 'px-1.5' : 'px-3')}>
            {collapsed ? (
              <button
                onClick={toggleSidebar}
                className="w-10 h-10 mx-auto flex items-center justify-center rounded-xl hover:bg-white/60 transition-all duration-200"
                title="Menuyu ac"
              >
                <img src={`${import.meta.env.BASE_URL}logo.png`} alt="Invekto" className="w-7 h-7 rounded-lg" />
              </button>
            ) : (
              <>
                <img src={`${import.meta.env.BASE_URL}logo.png`} alt="" className="w-8 h-8 flex-shrink-0 rounded-lg" />
                <div className="min-w-0 ml-2.5 flex-1">
                  <InvektoLogo size="sm" className="block leading-tight" />
                  {session?.companyCode && (
                    <span className="text-2xs text-navy-300 truncate block leading-tight mt-0.5 font-medium">
                      {session.companyCode}
                    </span>
                  )}
                </div>
              </>
            )}
          </div>

          {/* Glass divider */}
          <div className="glass-divider mx-3" />

          {/* Main Navigation */}
          <nav className={cn('flex-1 py-3 space-y-0.5 overflow-y-auto overflow-x-hidden', collapsed ? 'px-1' : 'px-2')}>
            {mainItems.map(item => renderNavLink(item))}
          </nav>

          {/* Version + Collapse */}
          {!collapsed && (
            <div className="px-4 py-1.5 flex items-center justify-between">
              <span className="text-2xs text-navy-200 font-medium">v{__BUILD_TIME__}</span>
              <button
                onClick={toggleSidebar}
                className="w-6 h-6 flex items-center justify-center rounded-lg text-navy-200 hover:bg-white/50 hover:text-navy-500 transition-all duration-200"
                title="Menuyu kapat"
              >
                <PanelLeftClose className="w-3.5 h-3.5" />
              </button>
            </div>
          )}

          {/* Glass divider */}
          <div className="glass-divider mx-3" />

          {/* Bottom: Onboarding + Settings + Logout */}
          <div className={cn('py-2 space-y-0.5', collapsed ? 'px-1' : 'px-2')}>
            {onboardingItem && renderNavLink(onboardingItem)}
            {settingsItem && renderNavLink(settingsItem)}
            <button
              className={cn(
                'w-full flex items-center h-10 rounded-xl text-sm font-medium text-navy-400 hover:glass-nav-hover hover:text-navy-700 transition-all duration-200 group',
                collapsed ? 'justify-center px-0' : 'gap-2.5 px-2.5'
              )}
              onClick={logout}
              title={collapsed ? 'Cikis Yap' : undefined}
            >
              <div className="w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0 text-navy-300 group-hover:bg-red-50 group-hover:text-red-400 transition-all duration-200">
                <Power className="w-[17px] h-[17px]" strokeWidth={1.8} />
              </div>
              {!collapsed && <span>Cikis Yap</span>}
            </button>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 overflow-auto">
          {isFullscreen ? children : (
            <div className="p-8 max-w-7xl mx-auto">
              {children}
            </div>
          )}
        </main>
      </div>
    </>
  );
}
