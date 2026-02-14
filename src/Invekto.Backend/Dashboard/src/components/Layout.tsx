import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Server, FileText, LogOut, LayoutDashboard, BookOpen } from 'lucide-react';
import { cn } from '../lib/utils';

interface LayoutProps {
  children: React.ReactNode;
}

export function Layout({ children }: LayoutProps) {
  const location = useLocation();
  const { logout } = useAuth();

  const navItems = [
    { path: '/', label: 'Dashboard', icon: LayoutDashboard },
    { path: '/logs', label: 'Logs', icon: FileText },
    { path: '/knowledge', label: 'Knowledge', icon: BookOpen },
  ];

  return (
    <div className="min-h-screen flex bg-slate-100">
      {/* Sidebar */}
      <aside className="w-60 h-screen sticky top-0 bg-slate-800 flex flex-col shadow-lg">
        {/* Logo */}
        <div className="h-14 px-4 flex items-center border-b border-slate-700">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 bg-slate-600 rounded-lg flex items-center justify-center">
              <Server className="w-4 h-4 text-slate-200" />
            </div>
            <span className="font-semibold text-slate-100">Invekto</span>
          </div>
        </div>

        {/* Navigation */}
        <nav className="flex-1 p-3 space-y-1">
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

        {/* Build Time */}
        <div className="px-4 py-2 text-sm text-slate-400">
          Build: {__BUILD_TIME__}
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
        <div className="p-6 max-w-7xl mx-auto">
          {children}
        </div>
      </main>
    </div>
  );
}
