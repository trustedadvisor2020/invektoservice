import { useState, useEffect, useCallback } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { api, TemplateCatalogItem, AvailableTemplate } from '../lib/api';
import { useAuth } from '../hooks/useAuth';
import {
  RefreshCw, Plus, Search, Eye, Trash2, Check, Download,
  LayoutTemplate, FileQuestion, MessageCircle, Lightbulb, GitBranch, Layers,
} from 'lucide-react';

const TYPE_ICONS: Record<string, React.ComponentType<{ className?: string }>> = {
  faq: FileQuestion,
  message: MessageCircle,
  intent: Lightbulb,
  flow: GitBranch,
  scenario: Layers,
};

const TYPE_COLORS: Record<string, string> = {
  faq: 'bg-blue-50 text-blue-700',
  message: 'bg-green-50 text-green-700',
  intent: 'bg-amber-50 text-amber-700',
  flow: 'bg-purple-50 text-purple-700',
  scenario: 'bg-pink-50 text-pink-700',
};

const SCOPE_COLORS: Record<string, string> = {
  platform: 'bg-indigo-50 text-indigo-700',
  sector: 'bg-teal-50 text-teal-700',
  tenant: 'bg-orange-50 text-orange-700',
};

const TEMPLATE_TYPES = ['', 'faq', 'message', 'intent', 'flow', 'scenario'];
const SCOPES = ['', 'platform', 'sector', 'tenant'];

export function TemplateLibraryPage() {
  const { session } = useAuth();
  const isTenant = session != null && session.tenantId > 0;

  if (isTenant) return <TenantTemplateView />;
  return <OpsTemplateView />;
}

/* ─── Tenant View: card grid with adopt buttons ──────────── */

function TenantTemplateView() {
  const [items, setItems] = useState<AvailableTemplate[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [adoptingId, setAdoptingId] = useState<number | null>(null);
  const [adoptedIds, setAdoptedIds] = useState<Set<number>>(new Set());
  const [filterType, setFilterType] = useState('');

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.getAvailableTemplates();
      setItems(res.items);
      setAdoptedIds(new Set(res.items.filter(t => t.is_adopted).map(t => t.id)));
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      setError(`Sablonlar yuklenemedi: ${msg}`);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchTemplates(); }, [fetchTemplates]);

  const handleAdopt = async (templateId: number) => {
    setAdoptingId(templateId);
    setError(null);
    try {
      await api.adoptTemplate(templateId);
      setAdoptedIds(prev => new Set([...prev, templateId]));
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      if (msg.includes('409') || msg.includes('already')) {
        setAdoptedIds(prev => new Set([...prev, templateId]));
      } else {
        setError(`Sablon benimsenemedi: ${msg}`);
      }
    } finally {
      setAdoptingId(null);
    }
  };

  const filtered = filterType ? items.filter(t => t.template_type === filterType) : items;

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
            <LayoutTemplate className="w-5 h-5" />
            Sablon Kutuphanesi
          </h1>
          <p className="text-xs text-navy-400">Sektorunuze uygun sablonlari icerik kutuphanenize ekleyin.</p>
        </div>
        <button onClick={fetchTemplates} className="p-1.5 rounded hover:bg-navy-50" disabled={loading}>
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
        </button>
      </div>

      {/* Filter */}
      <div className="flex items-center gap-3">
        <select
          value={filterType}
          onChange={e => setFilterType(e.target.value)}
          className="text-xs border border-navy-200 rounded px-2 py-1.5 bg-white"
        >
          <option value="">Tum Tipler</option>
          {TEMPLATE_TYPES.filter(Boolean).map(t => (
            <option key={t} value={t}>{t.toUpperCase()}</option>
          ))}
        </select>
        <span className="text-xs text-navy-400">{filtered.length} sablon</span>
      </div>

      {/* Error */}
      {error && (
        <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
        </div>
      )}

      {/* Loading */}
      {loading && (
        <div className="py-12 text-center text-navy-300">Sablonlar yukleniyor...</div>
      )}

      {/* Empty */}
      {!loading && filtered.length === 0 && !error && (
        <div className="py-12 text-center text-navy-400">
          <LayoutTemplate className="w-8 h-8 mx-auto mb-2 text-navy-200" />
          <p className="text-sm">Henuz sablon bulunamadi.</p>
          <p className="text-xs text-navy-300 mt-1">Once sektorunuzu Ayarlar sayfasindan secin.</p>
        </div>
      )}

      {/* Card Grid */}
      {!loading && filtered.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
          {filtered.map(tpl => {
            const TypeIcon = TYPE_ICONS[tpl.template_type] || LayoutTemplate;
            const isAdopted = adoptedIds.has(tpl.id);
            const isAdopting = adoptingId === tpl.id;

            return (
              <div key={tpl.id} className="bg-white rounded-xl border border-navy-100 p-4 flex flex-col">
                {/* Type badge + name */}
                <div className="flex items-start gap-2 mb-2">
                  <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium flex-shrink-0 ${TYPE_COLORS[tpl.template_type] || 'bg-gray-50 text-gray-700'}`}>
                    <TypeIcon className="w-3 h-3" />
                    {tpl.template_type}
                  </span>
                  {tpl.scope === 'sector' && tpl.sector && (
                    <span className={`inline-flex px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS.sector}`}>
                      {tpl.sector}
                    </span>
                  )}
                </div>
                <h3 className="text-sm font-medium text-navy-800 mb-1">{tpl.name}</h3>
                {tpl.description && (
                  <p className="text-xs text-navy-400 mb-3 line-clamp-2 flex-1">{tpl.description}</p>
                )}
                {!tpl.description && <div className="flex-1" />}

                {/* Confidence + Tags */}
                <div className="flex items-center gap-2 mb-3">
                  <div className="w-16 bg-navy-100 rounded-full h-1.5">
                    <div className="bg-emerald-500 h-1.5 rounded-full" style={{ width: `${tpl.confidence_score * 100}%` }} />
                  </div>
                  <span className="text-[10px] text-navy-400">{(tpl.confidence_score * 100).toFixed(0)}%</span>
                </div>

                {/* Adopt button */}
                {isAdopted ? (
                  <div className="flex items-center gap-1.5 text-emerald-600 text-xs font-medium">
                    <Check className="w-3.5 h-3.5" />
                    Benimsendi
                  </div>
                ) : (
                  <button
                    onClick={() => handleAdopt(tpl.id)}
                    disabled={isAdopting}
                    className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg bg-brand-50 text-brand-700 hover:bg-brand-100 transition-colors disabled:opacity-50"
                  >
                    <Download className="w-3.5 h-3.5" />
                    {isAdopting ? 'Benimseniyor...' : 'Benimsem'}
                  </button>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

/* ─── Ops View: existing admin table (unchanged) ─────────── */

function OpsTemplateView() {
  const navigate = useNavigate();
  const [items, setItems] = useState<TemplateCatalogItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);

  // Filters
  const [filterType, setFilterType] = useState('');
  const [filterScope, setFilterScope] = useState('');
  const [filterSearch, setFilterSearch] = useState('');

  const fetchTemplates = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.getTemplateCatalog({
        type: filterType || undefined,
        scope: filterScope || undefined,
        search: filterSearch || undefined,
        page,
        limit: 20,
      });
      setItems(result.items);
      setTotal(result.total);
    } catch (err) {
      console.error('Failed to fetch templates:', err);
      setError('Sablonlar yuklenirken hata olustu. Tekrar deneyin.');
    } finally {
      setLoading(false);
    }
  }, [filterType, filterScope, filterSearch, page]);

  useEffect(() => { fetchTemplates(); }, [fetchTemplates]);

  const handlePublish = async (id: number) => {
    setError(null);
    try {
      await api.publishTemplate(id);
      fetchTemplates();
    } catch (err) {
      console.error('Publish failed:', err);
      setError('Sablon yayinlanirken hata olustu.');
    }
  };

  const handleDelete = async (id: number) => {
    setError(null);
    try {
      await api.deleteTemplate(id);
      fetchTemplates();
    } catch (err) {
      console.error('Delete failed:', err);
      setError('Sablon silinirken hata olustu.');
    }
  };

  const totalPages = Math.ceil(total / 20);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
            <LayoutTemplate className="w-5 h-5" />
            Sablon Kutuphanesi
          </h1>
          <p className="text-xs text-navy-400">{total} sablon kayitli</p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={fetchTemplates} className="p-1.5 rounded hover:bg-navy-50">
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
          <Link
            to="/templates/ingestion"
            className="flex items-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700"
          >
            <Plus className="w-3.5 h-3.5" />
            Veri Besleme
          </Link>
        </div>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-3 bg-white rounded-lg border border-navy-100 p-3">
        <div className="flex items-center gap-1.5">
          <Search className="w-3.5 h-3.5 text-navy-400" />
          <input
            type="text"
            placeholder="Ara (slug, isim)..."
            value={filterSearch}
            onChange={e => { setFilterSearch(e.target.value); setPage(1); }}
            className="text-xs border-0 outline-none w-40 bg-transparent"
          />
        </div>
        <select
          value={filterType}
          onChange={e => { setFilterType(e.target.value); setPage(1); }}
          className="text-xs border border-navy-200 rounded px-2 py-1"
        >
          <option value="">Tum Tipler</option>
          {TEMPLATE_TYPES.filter(Boolean).map(t => (
            <option key={t} value={t}>{t.toUpperCase()}</option>
          ))}
        </select>
        <select
          value={filterScope}
          onChange={e => { setFilterScope(e.target.value); setPage(1); }}
          className="text-xs border border-navy-200 rounded px-2 py-1"
        >
          <option value="">Tum Scope</option>
          {SCOPES.filter(Boolean).map(s => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
      </div>

      {/* Error Banner */}
      {error && (
        <div className="flex items-center justify-between bg-red-50 border border-red-200 rounded-lg px-3 py-2 text-xs text-red-700">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-600 ml-2">&times;</button>
        </div>
      )}

      {/* Table */}
      <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
        <table className="w-full text-xs">
          <thead className="bg-navy-50 text-navy-500">
            <tr>
              <th className="text-left px-3 py-2 font-medium">Sablon</th>
              <th className="text-left px-3 py-2 font-medium">Tip</th>
              <th className="text-left px-3 py-2 font-medium">Scope</th>
              <th className="text-center px-3 py-2 font-medium">v</th>
              <th className="text-center px-3 py-2 font-medium">Guven</th>
              <th className="text-center px-3 py-2 font-medium">Kaynak</th>
              <th className="text-center px-3 py-2 font-medium">Durum</th>
              <th className="text-right px-3 py-2 font-medium">Islem</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-navy-50">
            {items.map(item => {
              const TypeIcon = TYPE_ICONS[item.template_type] || LayoutTemplate;
              return (
                <tr key={item.id} className="hover:bg-navy-25 cursor-pointer" onClick={() => navigate(`/templates/${item.id}`)}>
                  <td className="px-3 py-2">
                    <div className="font-medium text-navy-800">{item.name}</div>
                    <div className="text-navy-400 text-[10px]">{item.slug}</div>
                  </td>
                  <td className="px-3 py-2">
                    <span className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium ${TYPE_COLORS[item.template_type] || 'bg-gray-50 text-gray-700'}`}>
                      <TypeIcon className="w-3 h-3" />
                      {item.template_type}
                    </span>
                  </td>
                  <td className="px-3 py-2">
                    <span className={`inline-flex px-1.5 py-0.5 rounded text-[10px] font-medium ${SCOPE_COLORS[item.scope] || 'bg-gray-50'}`}>
                      {item.scope}
                      {item.sector && ` / ${item.sector}`}
                    </span>
                  </td>
                  <td className="px-3 py-2 text-center text-navy-500">v{item.version}</td>
                  <td className="px-3 py-2 text-center">
                    <div className="w-12 bg-navy-100 rounded-full h-1.5 mx-auto">
                      <div className="bg-emerald-500 h-1.5 rounded-full" style={{ width: `${item.confidence_score * 100}%` }} />
                    </div>
                    <div className="text-[10px] text-navy-400 mt-0.5">{(item.confidence_score * 100).toFixed(0)}%</div>
                  </td>
                  <td className="px-3 py-2 text-center text-navy-500">{item.source_count}</td>
                  <td className="px-3 py-2 text-center">
                    {item.is_published ? (
                      <span className="inline-flex items-center gap-0.5 text-emerald-600 text-[10px]">
                        <Check className="w-3 h-3" /> Yayinda
                      </span>
                    ) : (
                      <span className="text-amber-500 text-[10px]">Taslak</span>
                    )}
                  </td>
                  <td className="px-3 py-2 text-right" onClick={e => e.stopPropagation()}>
                    <div className="flex items-center justify-end gap-1">
                      <button onClick={() => navigate(`/templates/${item.id}`)} className="p-1 hover:bg-navy-100 rounded" title="Goruntule">
                        <Eye className="w-3.5 h-3.5 text-navy-500" />
                      </button>
                      {!item.is_published && (
                        <button onClick={() => handlePublish(item.id)} className="p-1 hover:bg-emerald-50 rounded" title="Yayinla">
                          <Check className="w-3.5 h-3.5 text-emerald-600" />
                        </button>
                      )}
                      <button onClick={() => handleDelete(item.id)} className="p-1 hover:bg-red-50 rounded" title="Sil">
                        <Trash2 className="w-3.5 h-3.5 text-red-400" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
            {items.length === 0 && !loading && (
              <tr>
                <td colSpan={8} className="px-3 py-8 text-center text-navy-400">
                  Sablon bulunamadi
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 text-xs">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="px-2 py-1 rounded border border-navy-200 disabled:opacity-40"
          >
            Onceki
          </button>
          <span className="text-navy-500">{page} / {totalPages}</span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
            className="px-2 py-1 rounded border border-navy-200 disabled:opacity-40"
          >
            Sonraki
          </button>
        </div>
      )}
    </div>
  );
}
