import { useState, useEffect, useCallback } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import {
  FileText,
  LogOut,
  LayoutDashboard,
  BookOpen,
  GraduationCap,
  BarChart3,
  GitBranch,
  Megaphone,
  CalendarDays,
  Link2,
  Star,
  Settings,
  MessageSquare,
  Building2,
  PanelLeftClose,
  Type,
  RotateCcw,
  LayoutTemplate,
  Upload,
} from 'lucide-react';
import { cn } from '../lib/utils';
import { InvektoLogo, InvektoMark } from './ui/InvektoLogo';

const SIDEBAR_KEY = 'inse-sidebar-collapsed';
const FONT_SIZE_KEY = 'inse-font-size';
const FONT_MIN = 12;
const FONT_MAX = 25;
const FONT_DEFAULT = 14;
const FONT_STEP = 1;

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
  { path: '/integrations',    label: 'Entegrasyonlar',   icon: Link2,        feature: 'Integrations' },
  { path: '/marketing',       label: 'Pazarlama',        icon: Star,         feature: 'Marketing' },
  { path: '/tenants',         label: 'Firmalar',         icon: Building2,     opsOnly: true },
  { path: '/messages',        label: 'Mesajlar',         icon: MessageSquare, opsOnly: true },
  { path: '/logs',            label: 'Loglar',            icon: FileText,     opsOnly: true },
  { path: '/templates',        label: 'Sablon Sistemi',    icon: LayoutTemplate,  opsOnly: true },
  { path: '/templates/ingestion', label: 'Veri Besleme',  icon: Upload,          opsOnly: true },
  { path: '/onboarding-guide', label: 'Onboarding Rehberi', icon: GraduationCap, opsOnly: true },
  { path: '/settings',        label: 'Ayarlar',          icon: Settings },
];

export function Layout({ children }: LayoutProps) {
  const location = useLocation();
  const { logout, session } = useAuth();
  const isFullscreen = location.pathname.startsWith('/flow-builder');
  const isImpersonating = session && api.isImpersonating();

  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDEBAR_KEY) === 'true');

  const [fontSize, setFontSize] = useState(() => {
    const stored = localStorage.getItem(FONT_SIZE_KEY);
    return stored ? Math.min(FONT_MAX, Math.max(FONT_MIN, Number(stored))) : FONT_DEFAULT;
  });

  useEffect(() => {
    document.documentElement.style.fontSize = `${fontSize}px`;
    localStorage.setItem(FONT_SIZE_KEY, String(fontSize));
  }, [fontSize]);

  const adjustFontSize = useCallback((delta: number) => {
    setFontSize(prev => Math.min(FONT_MAX, Math.max(FONT_MIN, prev + delta)));
  }, []);

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
  const navItems = ALL_NAV_ITEMS.filter(item => {
    if (!session) {
      // Ops mode: sadece opsOnly items + Ayarlar
      return item.opsOnly || item.path === '/settings';
    }
    // Tenant mode
    if (item.opsOnly && session.tenantId !== 0) return false;
    if (!item.feature) return true;
    return api.hasFeature(item.feature);
  }).map(item => ({
    ...item,
    label: session && item.tenantLabel ? item.tenantLabel : item.label,
  }));

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
        {/* Sidebar — collapsible, light, Stripe-inspired */}
        <aside className={cn(
          'h-screen sticky top-0 bg-[#f8f9fb] shadow-[3px_0_16px_-4px_rgba(0,0,0,0.10)] z-10 flex flex-col transition-[width] duration-200',
          collapsed ? 'w-[3.5rem]' : 'w-56'
        )}>
          {/* Logo + Toggle */}
          <div className="h-14 px-2 flex items-center border-b border-navy-100/60">
            {collapsed ? (
              <button
                onClick={toggleSidebar}
                className="w-10 h-10 mx-auto flex items-center justify-center rounded-lg hover:bg-navy-50 transition-colors"
                title="Menüyü aç"
              >
                <img src={`${import.meta.env.BASE_URL}logo.png`} alt="Invekto" className="w-7 h-7 rounded-lg" />
              </button>
            ) : (
              <>
                <img src={`${import.meta.env.BASE_URL}logo.png`} alt="" className="w-7 h-7 flex-shrink-0 rounded-lg ml-2" />
                <div className="min-w-0 ml-2 flex-1">
                  <InvektoLogo size="sm" className="block leading-tight" />
                  {session?.companyCode && (
                    <span className="text-2xs text-navy-300 truncate block leading-tight mt-0.5">
                      {session.companyCode}
                    </span>
                  )}
                </div>
              </>
            )}
          </div>

          {/* Navigation */}
          <nav className={cn('flex-1 py-3 space-y-0.5 overflow-y-auto overflow-x-hidden', collapsed ? 'px-1.5' : 'px-2')}>
            {navItems.map(item => {
              const Icon = item.icon;
              const isActive = item.path === '/' ? location.pathname === '/' : location.pathname.startsWith(item.path);
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  title={collapsed ? item.label : undefined}
                  className={cn(
                    'flex items-center h-9 rounded-lg text-[13px] font-medium transition-colors duration-150',
                    collapsed ? 'justify-center px-0' : 'gap-2.5 px-3',
                    isActive
                      ? 'bg-brand-50 text-brand-600'
                      : 'text-navy-400 hover:bg-navy-50 hover:text-navy-700'
                  )}
                >
                  <Icon className={cn(
                    'w-4 h-4 flex-shrink-0',
                    isActive ? 'text-brand-500' : 'text-navy-300'
                  )} />
                  {!collapsed && <span className="truncate">{item.label}</span>}
                </Link>
              );
            })}
          </nav>

          {/* Font Size — hidden when collapsed */}
          {!collapsed && (
            <div className="px-3 py-1.5">
              <div className="flex items-center gap-1.5">
                <button
                  onClick={() => adjustFontSize(-FONT_STEP)}
                  disabled={fontSize <= FONT_MIN}
                  className="w-7 h-7 flex items-center justify-center rounded-md border border-navy-100 text-navy-400 hover:bg-navy-50 hover:text-navy-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                  title="Yazı küçült"
                >
                  <Type className="w-3 h-3" />
                </button>
                <span className="text-2xs text-navy-300 min-w-[2.5rem] text-center select-none">
                  {fontSize}px
                </span>
                <button
                  onClick={() => adjustFontSize(FONT_STEP)}
                  disabled={fontSize >= FONT_MAX}
                  className="w-7 h-7 flex items-center justify-center rounded-md border border-navy-100 text-navy-400 hover:bg-navy-50 hover:text-navy-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                  title="Yazı büyüt"
                >
                  <Type className="w-3.5 h-3.5" />
                </button>
                {fontSize !== FONT_DEFAULT && (
                  <button
                    onClick={() => setFontSize(FONT_DEFAULT)}
                    className="w-7 h-7 flex items-center justify-center rounded-md border border-navy-100 text-navy-400 hover:bg-navy-50 hover:text-navy-600 transition-colors ml-1"
                    title={`Sıfırla (${FONT_DEFAULT}px)`}
                  >
                    <RotateCcw className="w-3 h-3" />
                  </button>
                )}
              </div>
            </div>
          )}

          {/* Version + Collapse */}
          {!collapsed && (
            <div className="px-4 py-1.5 flex items-center justify-between">
              <span className="text-2xs text-navy-200">v{__BUILD_TIME__}</span>
              <button
                onClick={toggleSidebar}
                className="w-5 h-5 flex items-center justify-center rounded text-navy-200 hover:bg-navy-50 hover:text-navy-500 transition-colors"
                title="Menüyü kapat"
              >
                <PanelLeftClose className="w-3.5 h-3.5" />
              </button>
            </div>
          )}

          {/* Logout */}
          <div className={cn('py-2 border-t border-navy-100/60', collapsed ? 'px-1.5' : 'px-2')}>
            <button
              className={cn(
                'w-full flex items-center h-9 rounded-lg text-[13px] font-medium text-navy-400 hover:bg-navy-50 hover:text-navy-700 transition-colors duration-150',
                collapsed ? 'justify-center px-0' : 'gap-2.5 px-3'
              )}
              onClick={logout}
              title={collapsed ? 'Çıkış Yap' : undefined}
            >
              <LogOut className="w-4 h-4 flex-shrink-0 text-navy-300" />
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
