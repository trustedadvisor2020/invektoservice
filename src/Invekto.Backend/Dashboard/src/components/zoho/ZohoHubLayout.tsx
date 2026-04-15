// Adim 3 Paket 3-B2: /integrations/* hub layout. Sol sub-nav (Zoho + ileride digerleri), sag Outlet.
// Feature gate: 'Integrations' ozelligi yoksa direkt URL erisimini engelle (CQ8/Q6).
import { NavLink, Navigate, Outlet } from 'react-router-dom';
import { Link2 } from 'lucide-react';
import { cn } from '../../lib/utils';
import { api } from '../../lib/api';

interface ZohoSubNavItem {
  path: string;
  label: string;
}

const ZOHO_NAV: ZohoSubNavItem[] = [
  { path: '/integrations/zoho/connection', label: 'Baglanti' },
  { path: '/integrations/zoho/stage-mappings', label: 'Asama Eslesmeleri' },
  { path: '/integrations/zoho/sync-log', label: 'Senkron Kaydi' },
];

export function ZohoHubLayout() {
  if (!api.hasFeature('Integrations')) {
    return <Navigate to="/" replace />;
  }
  return (
    <div className="flex gap-6 h-full">
      <aside className="w-56 shrink-0 border-r border-navy-100 pr-4">
        <div className="mb-4">
          <h2 className="text-xs font-semibold uppercase tracking-wide text-navy-400 mb-2">Entegrasyonlar</h2>
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-brand-50 text-brand-700">
            <Link2 className="w-4 h-4" />
            <span className="text-sm font-medium">Zoho CRM</span>
          </div>
        </div>
        <nav className="flex flex-col gap-1 pl-2">
          {ZOHO_NAV.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              className={({ isActive }) =>
                cn(
                  'px-3 py-2 rounded-lg text-sm transition-colors',
                  isActive
                    ? 'bg-navy-50 text-navy-900 font-medium'
                    : 'text-navy-500 hover:bg-navy-50 hover:text-navy-700',
                )
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex-1 min-w-0">
        <Outlet />
      </div>
    </div>
  );
}
