import { useState, useEffect, useCallback } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import {
  Zap,
  FileText,
  LogOut,
  LayoutDashboard,
  BookOpen,
  BarChart3,
  GitBranch,
  Megaphone,
  CalendarDays,
  Link2,
  Star,
  Settings,
  MessageSquare,
  Building2,
  Minus,
  Plus,
} from 'lucide-react';
import { cn } from '../lib/utils';

const FONT_SIZE_KEY = 'inse-font-size';
const FONT_MIN = 13;
const FONT_MAX = 20;
const FONT_DEFAULT = 16;
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
  { path: '/flow-builder-ui', label: 'Flow Builder', tenantLabel: 'Flow Builder', icon: GitBranch,    feature: 'FlowBuilder' },
  { path: '/knowledge',       label: 'Bilgi Bankasi',    icon: BookOpen,     feature: 'Knowledge' },
  { path: '/campaigns',       label: 'Kampanyalar',      icon: Megaphone,    feature: 'Outbound' },
  { path: '/appointments',    label: 'Randevular',       icon: CalendarDays, feature: 'Appointments' },
  { path: '/analytics',       label: 'Analizler',        icon: BarChart3,    feature: 'Analytics' },
  { path: '/integrations',    label: 'Entegrasyonlar',   icon: Link2,        feature: 'Integrations' },
  { path: '/marketing',       label: 'Pazarlama',        icon: Star,         feature: 'Marketing' },
  { path: '/tenants',         label: 'Firmalar',         icon: Building2,     opsOnly: true },
  { path: '/messages',        label: 'Mesajlar',         icon: MessageSquare, opsOnly: true },
  { path: '/logs',            label: 'Loglar',            icon: FileText,     opsOnly: true },
  { path: '/settings',        label: 'Ayarlar',          icon: Settings },
];

export function Layout({ children }: LayoutProps) {
  const location = useLocation();
  const { logout, session } = useAuth();
  const isFullscreen = location.pathname === '/flow-builder-ui';
  const isImpersonating = session && api.isImpersonating();

  const [fontSize, setFontSize] = useState(() => {
    const stored = localStorage.getItem(FONT_SIZE_KEY);
    return stored ? Math.min(FONT_MAX, Math.max(FONT_MIN, Number(stored))) : FONT_DEFAULT;
  });

  useEffect(() => {
    document.documentElement.style.fontSize = `${fontSize}px`;
    localStorage.setItem(FONT_SIZE_KEY, String(fontSize));
  }, [fontSize]);

  const adjustFont = useCallback((delta: number) => {
    setFontSize(prev => Math.min(FONT_MAX, Math.max(FONT_MIN, prev + delta)));
  }, []);

  const exitImpersonation = () => {
    api.removeTokens();
    window.location.href = '/';
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
        {/* Sidebar — light, clean, Stripe-inspired */}
        <aside className="w-56 h-screen sticky top-0 bg-white border-r border-navy-100 flex flex-col">
          {/* Logo */}
          <div className="h-14 px-4 flex items-center gap-2.5 border-b border-navy-100/60">
            <div className="w-7 h-7 flex-shrink-0 bg-brand-500 rounded-lg flex items-center justify-center">
              <Zap className="w-3.5 h-3.5 text-white" />
            </div>
            <div className="min-w-0">
              <span className="font-semibold text-navy-900 text-sm block truncate leading-tight">
                {session?.fullName ? session.fullName : 'Invekto'}
              </span>
              {session && (
                <span className="text-2xs text-navy-300 truncate block leading-tight">
                  Firma #{session.tenantId}
                </span>
              )}
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex-1 px-2 py-3 space-y-0.5 overflow-y-auto">
            {navItems.map(item => {
              const Icon = item.icon;
              const isActive = location.pathname === item.path;
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  className={cn(
                    'flex items-center gap-2.5 h-9 px-3 rounded-lg text-[13px] font-medium',
                    'transition-colors duration-150',
                    isActive
                      ? 'bg-brand-50 text-brand-600'
                      : 'text-navy-400 hover:bg-navy-50 hover:text-navy-700'
                  )}
                >
                  <Icon className={cn(
                    'w-4 h-4 flex-shrink-0',
                    isActive ? 'text-brand-500' : 'text-navy-300'
                  )} />
                  <span>{item.label}</span>
                </Link>
              );
            })}
          </nav>

          {/* Font Size */}
          <div className="px-3 py-1.5 flex items-center gap-1.5">
            <button
              onClick={() => adjustFont(-FONT_STEP)}
              disabled={fontSize <= FONT_MIN}
              className="w-7 h-7 flex items-center justify-center rounded-md border border-navy-100 text-navy-400 hover:bg-navy-50 hover:text-navy-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
              title="Yazı küçült"
            >
              <Minus className="w-3 h-3" />
            </button>
            <span className="text-2xs text-navy-300 min-w-[2.5rem] text-center select-none">
              {fontSize}px
            </span>
            <button
              onClick={() => adjustFont(FONT_STEP)}
              disabled={fontSize >= FONT_MAX}
              className="w-7 h-7 flex items-center justify-center rounded-md border border-navy-100 text-navy-400 hover:bg-navy-50 hover:text-navy-600 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
              title="Yazı büyüt"
            >
              <Plus className="w-3 h-3" />
            </button>
          </div>

          {/* Version */}
          <div className="px-4 py-1.5 text-2xs text-navy-200">
            v{__BUILD_TIME__}
          </div>

          {/* Logout */}
          <div className="px-2 py-2 border-t border-navy-100/60">
            <button
              className="w-full flex items-center gap-2.5 h-9 px-3 rounded-lg text-[13px] font-medium text-navy-400 hover:bg-navy-50 hover:text-navy-700 transition-colors duration-150"
              onClick={logout}
            >
              <LogOut className="w-4 h-4 flex-shrink-0 text-navy-300" />
              <span>Cikis Yap</span>
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
