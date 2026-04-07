import { useState, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, GitBranch, ShoppingCart, Activity, Building2, Sparkles, GraduationCap, Smartphone, Shield, LayoutTemplate } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { api } from '../lib/api';
import { FLOW_TEMPLATES, NICHE_LABELS, type FlowTemplate } from '../data/flow-templates';

const NICHE_TABS = [
  { key: '', label: 'Tumunu', icon: LayoutTemplate },
  { key: 'ecommerce', label: 'E-Ticaret', icon: ShoppingCart },
  { key: 'health', label: 'Saglik', icon: Activity, niches: ['health', 'dental', 'aesthetic'] },
  { key: 'hotel', label: 'Otel', icon: Building2 },
  { key: 'beauty', label: 'Guzellik', icon: Sparkles },
  { key: 'education', label: 'Egitim', icon: GraduationCap },
  { key: 'mobile', label: 'Mobil', icon: Smartphone },
  { key: 'universal', label: 'Evrensel', icon: Shield, niches: ['universal', 'crossSector'] },
] as const;

const NICHE_COLORS: Record<string, string> = {
  ecommerce: 'bg-amber-100 text-amber-800',
  dental: 'bg-blue-100 text-blue-800',
  aesthetic: 'bg-pink-100 text-pink-800',
  hotel: 'bg-emerald-100 text-emerald-800',
  beauty: 'bg-purple-100 text-purple-800',
  education: 'bg-indigo-100 text-indigo-800',
  mobile: 'bg-cyan-100 text-cyan-800',
  universal: 'bg-slate-100 text-slate-700',
  crossSector: 'bg-slate-100 text-slate-700',
  health: 'bg-rose-100 text-rose-800',
};

export function FlowTemplateGalleryPage() {
  const [search, setSearch] = useState('');
  const [nicheFilter, setNicheFilter] = useState('');
  const [creating, setCreating] = useState<string | null>(null);
  const navigate = useNavigate();
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 0;

  const filtered = useMemo(() => {
    return FLOW_TEMPLATES.filter(t => {
      if (nicheFilter) {
        const tab = NICHE_TABS.find(n => n.key === nicheFilter);
        const niches: readonly string[] = tab && 'niches' in tab ? (tab as { niches: readonly string[] }).niches : [nicheFilter];
        if (!niches.includes(t.niche)) return false;
      }
      if (search) {
        const q = search.toLowerCase();
        return t.title.toLowerCase().includes(q) || t.description.toLowerCase().includes(q);
      }
      return true;
    });
  }, [search, nicheFilter]);

  const handleUse = async (tpl: FlowTemplate) => {
    if (!tenantId || creating) return;
    setCreating(tpl.id);
    try {
      const config = { ...tpl.flowConfig, metadata: { ...tpl.flowConfig.metadata, name: tpl.title } };
      const created = await api.createFlow(tenantId, { flow_name: tpl.title, flow_config: config });
      navigate(`/flow-builder/editor/${created.flow_id}?template=${tpl.id}`);
    } catch {
      setCreating(null);
    }
  };

  return (
    <div className="max-w-7xl mx-auto">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-slate-900">Sablon Galerisi</h1>
        <p className="text-sm text-slate-500 mt-1">Hazir sablonlardan secin, AI ile isletmenize gore ozellestirin.</p>
      </div>

      {/* Filter bar */}
      <div className="flex flex-col sm:flex-row gap-4 mb-6">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            placeholder="Sablon ara..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full pl-9 pr-4 py-2 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-teal-500 focus:border-transparent"
          />
        </div>
        <div className="flex flex-wrap gap-1.5">
          {NICHE_TABS.map(tab => {
            const Icon = tab.icon;
            const active = nicheFilter === tab.key;
            return (
              <button
                key={tab.key}
                onClick={() => setNicheFilter(active ? '' : tab.key)}
                className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-full border transition-colors ${
                  active
                    ? 'bg-teal-600 text-white border-teal-600'
                    : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                }`}
              >
                <Icon className="w-3.5 h-3.5" />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      {/* Count */}
      <p className="text-xs text-slate-400 mb-4">{filtered.length} sablon</p>

      {/* Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {filtered.map(tpl => (
          <div
            key={tpl.id}
            className="group bg-white border border-slate-200 rounded-xl p-5 hover:border-teal-300 hover:shadow-md transition-all"
          >
            <div className="flex items-start justify-between mb-3">
              <div className="flex items-center gap-2">
                <GitBranch className="w-4 h-4 text-teal-600" />
                <span className={`text-[10px] font-bold uppercase px-2 py-0.5 rounded-full ${NICHE_COLORS[tpl.niche] || NICHE_COLORS.universal}`}>
                  {NICHE_LABELS[tpl.niche] || tpl.niche}
                </span>
              </div>
              <span className="text-[10px] text-slate-400 font-mono">{tpl.nodeCount} node</span>
            </div>
            <h3 className="text-sm font-bold text-slate-900 mb-1 line-clamp-1">{tpl.title}</h3>
            <p className="text-xs text-slate-500 mb-4 line-clamp-2 leading-relaxed">{tpl.description}</p>
            {tpl.tags.length > 0 && (
              <div className="flex flex-wrap gap-1 mb-4">
                {tpl.tags.slice(0, 3).map((tag, i) => (
                  <span key={i} className="text-[10px] bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full">{tag}</span>
                ))}
              </div>
            )}
            <button
              onClick={() => handleUse(tpl)}
              disabled={!!creating}
              className="w-full py-2 text-xs font-bold rounded-lg border transition-colors bg-teal-50 text-teal-700 border-teal-200 hover:bg-teal-600 hover:text-white hover:border-teal-600 disabled:opacity-50"
            >
              {creating === tpl.id ? 'Olusturuluyor...' : 'Bu Sablonu Kullan'}
            </button>
          </div>
        ))}
      </div>

      {filtered.length === 0 && (
        <div className="text-center py-20 text-slate-400">
          <LayoutTemplate className="w-12 h-12 mx-auto mb-3 opacity-30" />
          <p className="text-sm">Aramanizla eslesen sablon bulunamadi.</p>
        </div>
      )}
    </div>
  );
}
