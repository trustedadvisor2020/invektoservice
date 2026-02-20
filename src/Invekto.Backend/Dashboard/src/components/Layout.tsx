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
} from 'lucide-react';
import { cn } from '../lib/utils';

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
  { path: '/logs',            label: 'Kayitlar',         icon: FileText,     opsOnly: true },
  { path: '/settings',        label: 'Ayarlar',          icon: Settings },
];

export function Layout({ children }: LayoutProps) {
  const location = useLocation();
  const { logout, session } = useAuth();
  const isFullscreen = location.pathname === '/flow-builder-ui';
  const isImpersonating = session && api.isImpersonating();

  const exitImpersonation = () => {
    api.removeTokens();
    window.location.href = '/';
  };

  // Ops mode (no session): tum nav items gorunur.
  // Tenant mode (session var): feature flag'e gore filtrele, opsOnly gizle, Turkce label.
  const navItems = ALL_NAV_ITEMS.filter(item => {
    // opsOnly: ops mode (no session) veya superadmin (tenant_id=0) gorebilir
    if (item.opsOnly && session && session.tenantId !== 0) return false;
    if (!item.feature) return true;
    if (!session) return true;
    return api.hasFeature(item.feature);
  }).map(item => ({
    ...item,
    label: session && item.tenantLabel ? item.tenantLabel : item.label,
  }));

  return (
    <>
      {/* Impersonation banner — fixed top bar when SuperAdmin is viewing a tenant */}
      {isImpersonating && (
        <div className="fixed top-0 left-0 right-0 bg-amber-500 text-white px-4 py-2 flex items-center justify-between text-sm font-medium z-50">
          <span>Firma #{session.tenantId} — SuperAdmin olarak goruntuleniyor</span>
          <button
            onClick={exitImpersonation}
            className="px-3 py-1 bg-amber-600 hover:bg-amber-700 rounded text-xs font-medium transition-colors"
          >
            Cikis
          </button>
        </div>
      )}

      <div className={cn('min-h-screen flex bg-slate-100', isImpersonating && 'pt-10')}>
        {/* Sidebar */}
        <aside className="w-60 h-screen sticky top-0 bg-slate-800 flex flex-col shadow-lg">
          {/* Logo + kullanici bilgisi */}
          <div className="h-14 px-4 flex items-center border-b border-slate-700">
            <div className="flex items-center gap-2.5 min-w-0">
              <div className="w-8 h-8 flex-shrink-0 bg-blue-600 rounded-lg flex items-center justify-center">
                <Zap className="w-4 h-4 text-white" />
              </div>
              <div className="min-w-0">
                <span className="font-semibold text-slate-100 block truncate leading-tight">
                  {session?.fullName ? session.fullName : 'Invekto'}
                </span>
                {session && (
                  <span className="text-xs text-slate-400 truncate block leading-tight">
                    Firma #{session.tenantId}
                  </span>
                )}
              </div>
            </div>
          </div>

          {/* Navigation */}
          <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
            {navItems.map(item => {
              const Icon = item.icon;
              const isActive = location.pathname === item.path;
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  className={cn(
                    'flex items-center gap-2.5 h-9 px-3 rounded-lg text-sm font-medium',
                    'transition-all duration-150',
                    isActive
                      ? 'bg-slate-700 text-white'
                      : 'text-slate-400 hover:bg-slate-700/50 hover:text-slate-200'
                  )}
                >
                  <Icon className="w-4 h-4 flex-shrink-0" />
                  <span>{item.label}</span>
                </Link>
              );
            })}
          </nav>

          {/* Version */}
          <div className="px-4 py-2 text-xs text-slate-500">
            v{__BUILD_TIME__}
          </div>

          {/* Logout */}
          <div className="p-3 border-t border-slate-700">
            <button
              className="w-full flex items-center gap-2.5 h-9 px-3 rounded-lg text-sm font-medium text-slate-400 hover:bg-slate-700/50 hover:text-slate-200 transition-all duration-150"
              onClick={logout}
            >
              <LogOut className="w-4 h-4 flex-shrink-0" />
              <span>Cikis Yap</span>
            </button>
          </div>
        </aside>

        {/* Main content */}
        <main className="flex-1 overflow-auto">
          {isFullscreen ? children : (
            <div className="p-6 max-w-7xl mx-auto">
              {children}
            </div>
          )}
        </main>
      </div>
    </>
  );
}
