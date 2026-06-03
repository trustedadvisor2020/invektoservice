import { lazy, Suspense } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Database, Download, Loader2 } from 'lucide-react';

// =============================================================
// FEAT-OBI — Veri Yönetimi: one page, two tabs.
//   import = Veri İçe Aktarma (Listeler)  -> DataImportPage  (Plan A)
//   export = Veri Dışa Aktarma (Export)   -> ExportManagerPage (Plan B + v2)
// Each tab's page is lazy-loaded and only the ACTIVE one is mounted, so a tab
// switch loads just that bundle (the heavy DataImport/Export chunks never both
// load at once). The tab is path-based (/data-management/:tab) so it is
// deep-linkable and survives refresh without touching the inner pages' own state.
// =============================================================

const DataImportPage = lazy(() => import('./DataImportPage').then(m => ({ default: m.DataImportPage })));
const ExportManagerPage = lazy(() => import('./ExportManagerPage').then(m => ({ default: m.ExportManagerPage })));

type TabKey = 'import' | 'export';

const TABS: { key: TabKey; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
  { key: 'import', label: 'Listeler', icon: Database },
  { key: 'export', label: 'Export', icon: Download },
];

export function DataManagementPage() {
  const { tab } = useParams<{ tab?: string }>();
  const navigate = useNavigate();
  const active: TabKey = tab === 'export' ? 'export' : 'import';

  return (
    <div className="flex flex-col h-full">
      {/* Tab bar */}
      <div className="px-6 pt-5 border-b border-navy-100 bg-white">
        <div className="flex items-center gap-1">
          {TABS.map((t) => {
            const isActive = t.key === active;
            const Icon = t.icon;
            return (
              <button
                key={t.key}
                onClick={() => navigate(`/data-management/${t.key}`)}
                aria-selected={isActive}
                role="tab"
                className={[
                  'inline-flex items-center gap-2 px-4 py-2.5 text-sm font-medium rounded-t-lg border-b-2 -mb-px transition-colors',
                  isActive
                    ? 'border-brand-500 text-brand-600'
                    : 'border-transparent text-navy-500 hover:text-navy-800 hover:border-navy-200',
                ].join(' ')}
              >
                <Icon className="w-4 h-4" />
                {t.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Active tab content (only the active page is mounted) */}
      <div className="flex-1 min-h-0 overflow-auto">
        <Suspense
          fallback={
            <div className="px-4 py-16 text-center text-navy-400 text-sm flex items-center justify-center gap-2">
              <Loader2 className="w-4 h-4 animate-spin" /> Yükleniyor…
            </div>
          }
        >
          {active === 'export' ? <ExportManagerPage /> : <DataImportPage />}
        </Suspense>
      </div>
    </div>
  );
}
