import { useEffect, useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../hooks/useAuth';
import { api, type FlowSummary, ApiClientError } from '../../lib/api';
import { createDefaultFlow, type FlowConfigV2 } from '../../types/flow';
import {
  ShoppingBag, MessageCircleHeart, Headphones, CalendarCheck,
  Package, CreditCard, HelpCircle, Megaphone, Star, Bell,
  UserCheck, PhoneCall, Workflow, Search, Plus, Sparkles,
  Pencil, Copy, Pause, Play, Trash2, X, Phone, GitBranch, LayoutTemplate,
  type LucideIcon,
} from 'lucide-react';
import { FLOW_TEMPLATES, NICHE_LABELS, type FlowTemplate } from '../../data/flow-templates';

/* ── Flow Icon Mapper ──────────────────────────────────────── */

const FLOW_ICON_RULES: { keywords: string[]; icon: LucideIcon; gradient: string }[] = [
  { keywords: ['satış', 'satis', 'sales', 'satiş'],        icon: ShoppingBag,        gradient: 'from-violet-500 to-purple-600' },
  { keywords: ['karşılama', 'karsilama', 'hoşgeldin', 'hosgeldin', 'welcome'], icon: MessageCircleHeart, gradient: 'from-pink-500 to-rose-600' },
  { keywords: ['destek', 'support', 'yardım', 'yardim'],   icon: Headphones,         gradient: 'from-sky-500 to-blue-600' },
  { keywords: ['randevu', 'appointment', 'rezerv'],         icon: CalendarCheck,      gradient: 'from-teal-500 to-emerald-600' },
  { keywords: ['sipariş', 'siparis', 'order'],              icon: Package,            gradient: 'from-amber-500 to-orange-600' },
  { keywords: ['ödeme', 'odeme', 'payment', 'fatura'],      icon: CreditCard,         gradient: 'from-emerald-500 to-green-600' },
  { keywords: ['bilgi', 'faq', 'sss'],                      icon: HelpCircle,         gradient: 'from-cyan-500 to-sky-600' },
  { keywords: ['kampanya', 'campaign', 'promo'],             icon: Megaphone,          gradient: 'from-fuchsia-500 to-pink-600' },
  { keywords: ['geri bildirim', 'feedback', 'anket'],        icon: Star,               gradient: 'from-yellow-500 to-amber-600' },
  { keywords: ['hatırlatma', 'hatirlatma', 'reminder'],      icon: Bell,               gradient: 'from-indigo-500 to-violet-600' },
  { keywords: ['onboard', 'kayıt', 'kayit', 'register'],    icon: UserCheck,          gradient: 'from-lime-500 to-emerald-600' },
  { keywords: ['arama', 'call', 'telefon'],                  icon: PhoneCall,          gradient: 'from-blue-500 to-indigo-600' },
];

function getFlowIcon(name: string, description?: string | null): { Icon: LucideIcon; gradient: string } {
  const text = `${name} ${description ?? ''}`.toLowerCase();
  for (const rule of FLOW_ICON_RULES) {
    if (rule.keywords.some(kw => text.includes(kw))) {
      return { Icon: rule.icon, gradient: rule.gradient };
    }
  }
  return { Icon: Workflow, gradient: 'from-slate-500 to-navy-600' };
}

/* ── Main Component ──────────────────────────────────────── */

export function FlowListPage() {
  const { session } = useAuth();
  const navigate = useNavigate();
  const tenantId = session?.tenantId ?? 0;

  const [flows, setFlows] = useState<FlowSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<number | null>(null);
  const [search, setSearch] = useState('');

  const [showNewDialog, setShowNewDialog] = useState(false);
  const [newFlowName, setNewFlowName] = useState('');
  const [newFlowError, setNewFlowError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState<FlowSummary | null>(null);
  const [wizardLoading, setWizardLoading] = useState(false);
  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [templateSearch, setTemplateSearch] = useState('');
  const [templateCreating, setTemplateCreating] = useState<string | null>(null);
  const [selectedNiche, setSelectedNiche] = useState<string>('all');
  const [selectedTemplate, setSelectedTemplate] = useState<FlowTemplate | null>(null);

  const fetchFlows = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const data = await api.listFlows(tenantId);
      setFlows(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Flow listesi alinamadi');
    } finally {
      setLoading(false);
    }
  }, [tenantId]);

  useEffect(() => { fetchFlows(); }, [fetchFlows]);

  const openNewDialog = () => {
    setTemplateSearch('');
    setTemplateCreating(null);
    setSelectedNiche('all');
    setSelectedTemplate(null);
    setShowTemplateModal(true);
  };

  const openBlankDialog = () => {
    setShowTemplateModal(false);
    setNewFlowName('');
    setNewFlowError(null);
    setShowNewDialog(true);
  };

  const handleCreateFromTemplate = async (tpl: FlowTemplate) => {
    if (!tenantId || templateCreating) return;
    setTemplateCreating(tpl.id);
    try {
      const config = { ...tpl.flowConfig, metadata: { ...tpl.flowConfig.metadata, name: tpl.title } };
      const created = await api.createFlow(tenantId, { flow_name: tpl.title, flow_config: config });
      setShowTemplateModal(false);
      navigate(`/flow-builder/editor/${created.flow_id}?template=${tpl.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Sablon olusturma basarisiz');
      setTemplateCreating(null);
    }
  };

  const handleCreate = async () => {
    if (!newFlowName.trim()) {
      setNewFlowError('Flow adi bos olamaz.');
      return;
    }
    setCreating(true);
    setNewFlowError(null);
    try {
      const defaultConfig = createDefaultFlow();
      defaultConfig.metadata.name = newFlowName.trim();
      const created = await api.createFlow(tenantId, {
        flow_name: newFlowName.trim(),
        flow_config: defaultConfig,
      });
      setShowNewDialog(false);
      setNewFlowName('');
      navigate(`/flow-builder/editor/${created.flow_id}`);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setNewFlowError('Bu isimde bir flow zaten mevcut.');
      } else {
        setNewFlowError(err instanceof Error ? err.message : 'Olusturma basarisiz');
      }
    } finally {
      setCreating(false);
    }
  };

  const handleDelete = async (flow: FlowSummary) => {
    setDeleteTarget(null);
    setActionLoading(flow.flow_id);
    try {
      await api.deleteFlow(tenantId, flow.flow_id);
      setFlows((prev) => prev.filter((f) => f.flow_id !== flow.flow_id));
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setError('Aktif flow silinemez. Once deaktif edin.');
      } else {
        setError(err instanceof Error ? err.message : 'Silme basarisiz');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleActivate = async (flowId: number) => {
    setActionLoading(flowId);
    setError(null);
    try {
      await api.activateFlow(tenantId, flowId);
      await fetchFlows();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Aktivasyon basarisiz');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDeactivate = async (flowId: number) => {
    setActionLoading(flowId);
    setError(null);
    try {
      await api.deactivateFlow(tenantId, flowId);
      await fetchFlows();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deaktivasyon basarisiz');
    } finally {
      setActionLoading(null);
    }
  };

  const handleDuplicate = async (flow: FlowSummary) => {
    setActionLoading(flow.flow_id);
    setError(null);
    try {
      const detail = await api.getFlow(tenantId, flow.flow_id);
      const config = detail.flow_config as FlowConfigV2;
      const baseName = flow.flow_name;
      const existingNames = new Set(flows.map((f) => f.flow_name));
      let dupName = `${baseName} - Kopya`;
      if (existingNames.has(dupName)) {
        let counter = 2;
        while (existingNames.has(`${baseName} - Kopya (${counter})`)) counter++;
        dupName = `${baseName} - Kopya (${counter})`;
      }
      const dupConfig: FlowConfigV2 = {
        ...config,
        metadata: { ...config.metadata, name: dupName },
      };
      const created = await api.createFlow(tenantId, {
        flow_name: dupName,
        flow_config: dupConfig,
      });
      navigate(`/flow-builder/editor/${created.flow_id}`);
    } catch (err) {
      if (err instanceof ApiClientError && err.status === 409) {
        setError('Bu isimde bir flow zaten mevcut. Lutfen tekrar deneyin.');
      } else {
        setError(err instanceof Error ? err.message : 'Kopyalama basarisiz');
      }
    } finally {
      setActionLoading(null);
    }
  };

  const handleStartWizard = async () => {
    if (!tenantId || wizardLoading) return;
    setWizardLoading(true);
    setError(null);
    try {
      const { startWizard } = await import('../../lib/wizard-api');
      const result = await startWizard(tenantId);
      navigate(`/flow-builder/wizard/${result.flow_id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'AI Wizard baslatilamadi');
    } finally {
      setWizardLoading(false);
    }
  };

  /* ── Derived ──────────────────────────────── */

  const filteredFlows = flows.filter(f => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return f.flow_name.toLowerCase().includes(q) ||
      (f.flow_description?.toLowerCase().includes(q) ?? false);
  });

  const activeCount = flows.filter(f => f.is_active).length;

  /* ── Render ──────────────────────────────── */

  return (
    <div className="min-h-screen bg-navy-50 text-navy-900">
      {/* ── Sticky Header ── */}
      <header className="bg-white/80 backdrop-blur-xl border-b border-navy-100 px-6 py-3 sticky top-0 z-30">
        <div className="max-w-6xl mx-auto flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <h1 className="text-lg font-display font-bold text-navy-900 tracking-tight">
              Flow Builder
            </h1>
            {!loading && flows.length > 0 && (
              <div className="flex items-center gap-3 text-2xs text-navy-400">
                <span className="flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 flow-status-pulse" />
                  {activeCount} aktif
                </span>
                <span className="text-navy-200">/</span>
                <span>{flows.length} toplam</span>
              </div>
            )}
          </div>

          <div className="flex items-center gap-2.5">
            {!loading && flows.length > 0 && (
              <div className="relative">
                <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-navy-300 pointer-events-none" />
                <input
                  type="text"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Ara..."
                  className="w-40 pl-8 pr-7 py-1.5 bg-navy-50 border border-transparent rounded-lg text-sm text-navy-900 placeholder-navy-300 focus:outline-none focus:bg-white focus:border-brand-200 focus:shadow-focus focus:w-56 transition-all"
                />
                {search && (
                  <button
                    onClick={() => setSearch('')}
                    className="absolute right-2 top-1/2 -translate-y-1/2 p-0.5 rounded text-navy-300 hover:text-navy-500"
                  >
                    <X className="w-3 h-3" />
                  </button>
                )}
              </div>
            )}
            <button
              onClick={handleStartWizard}
              disabled={wizardLoading}
              className="flex items-center gap-1.5 px-3.5 py-1.5 bg-gradient-to-r from-violet-500 via-purple-500 to-fuchsia-500 hover:from-violet-600 hover:via-purple-600 hover:to-fuchsia-600 text-white text-sm font-semibold rounded-lg shadow-sm hover:shadow-md transition-all disabled:opacity-40 disabled:pointer-events-none"
            >
              <Sparkles className="w-3.5 h-3.5" />
              {wizardLoading ? 'Hazirlaniyor...' : 'AI ile Olustur'}
            </button>
            <button
              onClick={openNewDialog}
              className="flex items-center gap-1.5 px-3.5 py-1.5 bg-navy-900 hover:bg-navy-800 text-white text-sm font-medium rounded-lg transition-colors"
            >
              <LayoutTemplate className="w-3.5 h-3.5" />
              Sablondan Olustur
            </button>
          </div>
        </div>
      </header>

      {/* ── Content ── */}
      <main className="max-w-6xl mx-auto px-6 py-5">
        {/* Error banner */}
        {error && (
          <div className="mb-4 text-sm text-red-600 bg-red-50 border border-red-100 rounded-xl px-4 py-2.5 flex items-center justify-between flow-card-enter">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="p-1 rounded-lg text-red-300 hover:text-red-500 hover:bg-red-100 transition-colors ml-3">
              <X className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        {/* Loading skeleton */}
        {loading && <SkeletonTable />}

        {/* Empty state */}
        {!loading && flows.length === 0 && !error && (
          <div className="text-center py-24 max-w-sm mx-auto flow-card-enter">
            <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-brand-100 to-brand-50 flex items-center justify-center mx-auto mb-4 shadow-sm">
              <Workflow className="w-7 h-7 text-brand-400" strokeWidth={1.5} />
            </div>
            <h3 className="text-base font-display font-semibold text-navy-900 mb-1.5">Henuz flow yok</h3>
            <p className="text-sm text-navy-400 mb-5 leading-relaxed">
              Ilk chatbot flow'unuzu olusturun.
            </p>
            <div className="flex items-center justify-center gap-2.5">
              <button
                onClick={handleStartWizard}
                disabled={wizardLoading}
                className="flex items-center gap-1.5 px-4 py-2 bg-gradient-to-r from-violet-500 via-purple-500 to-fuchsia-500 hover:from-violet-600 hover:via-purple-600 hover:to-fuchsia-600 text-white text-sm font-semibold rounded-lg shadow-sm hover:shadow-md transition-all disabled:opacity-40"
              >
                <Sparkles className="w-3.5 h-3.5" />
                AI ile Baslat
              </button>
              <button
                onClick={openNewDialog}
                className="flex items-center gap-1.5 px-4 py-2 bg-navy-900 hover:bg-navy-800 text-white text-sm font-medium rounded-lg transition-colors"
              >
                <LayoutTemplate className="w-3.5 h-3.5" />
                Sablondan Olustur
              </button>
            </div>
          </div>
        )}

        {/* Flow Table */}
        {!loading && filteredFlows.length > 0 && (
          <div className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden flow-card-enter">
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead className="bg-navy-50/50 text-navy-500 text-xs">
                  <tr>
                    <th className="text-left font-medium px-4 py-2.5">Akış</th>
                    <th className="text-left font-medium px-4 py-2.5">Sağlık</th>
                    <th className="text-left font-medium px-4 py-2.5 whitespace-nowrap">Sürüm</th>
                    <th className="text-left font-medium px-4 py-2.5">Atanan Hat</th>
                    <th className="text-right font-medium px-4 py-2.5">İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredFlows.map((flow) => {
                    const { Icon } = getFlowIcon(flow.flow_name, flow.flow_description);
                    const isRowLoading = actionLoading === flow.flow_id;

                    return (
                      <tr
                        key={flow.flow_id}
                        onDoubleClick={() => navigate(`/flow-builder/editor/${flow.flow_id}`)}
                        className="group border-t border-navy-50 hover:bg-navy-50/40 transition-colors cursor-pointer select-none"
                      >
                        {/* Akış (wireframe icon + name) */}
                        <td className="px-4 py-2.5">
                          <div className="flex items-center gap-3 min-w-0">
                            <div className="w-7 h-7 rounded-md border border-navy-200 bg-white flex items-center justify-center flex-shrink-0">
                              <Icon className="w-3.5 h-3.5 text-navy-400" strokeWidth={1.75} />
                            </div>
                            <span className="font-semibold text-navy-900 truncate max-w-[22rem]">{flow.flow_name}</span>
                          </div>
                        </td>

                        {/* Sağlık */}
                        <td className="px-4 py-2.5">
                          {flow.health_score != null
                            ? <HealthBadge score={flow.health_score} issues={flow.health_issues} />
                            : <span className="text-navy-300">&mdash;</span>}
                        </td>

                        {/* Sürüm */}
                        <td className="px-4 py-2.5 text-navy-500 whitespace-nowrap">v{flow.config_version}</td>

                        {/* Atanan Hat */}
                        <td className="px-4 py-2.5 text-navy-500">
                          {flow.assigned_instances && flow.assigned_instances.length > 0 ? (
                            <span className="inline-flex items-center gap-1 text-sky-500">
                              <Phone className="w-3 h-3 flex-shrink-0" />
                              <span className="truncate max-w-[12rem]">{flow.assigned_instances.map(i => i.instanceName).join(', ')}</span>
                            </span>
                          ) : (
                            <span className="text-navy-300">&mdash;</span>
                          )}
                        </td>

                        {/* İşlem */}
                        <td className="px-4 py-2.5">
                          <div className="flex items-center justify-end gap-0.5 opacity-70 group-hover:opacity-100 transition-opacity">
                            <IconBtn
                              icon={Pencil}
                              title="Duzenle"
                              onClick={() => navigate(`/flow-builder/editor/${flow.flow_id}`)}
                              className="text-brand-500 hover:bg-brand-50"
                            />
                            {flow.is_active ? (
                              <IconBtn
                                icon={Pause}
                                title="Deaktif Et"
                                onClick={() => handleDeactivate(flow.flow_id)}
                                disabled={isRowLoading}
                                className="text-amber-500 hover:bg-amber-50"
                              />
                            ) : (
                              <IconBtn
                                icon={Play}
                                title="Aktif Et"
                                onClick={() => handleActivate(flow.flow_id)}
                                disabled={isRowLoading}
                                className="text-emerald-500 hover:bg-emerald-50"
                              />
                            )}
                            <IconBtn
                              icon={Copy}
                              title="Kopyala"
                              onClick={() => handleDuplicate(flow)}
                              disabled={isRowLoading}
                              className="text-navy-400 hover:text-brand-500 hover:bg-brand-50"
                            />
                            <IconBtn
                              icon={Trash2}
                              title={flow.is_active ? 'Aktif flow silinemez' : 'Sil'}
                              onClick={() => setDeleteTarget(flow)}
                              disabled={isRowLoading || flow.is_active}
                              className="text-red-400 hover:bg-red-50"
                            />
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Search no results */}
        {!loading && search && filteredFlows.length === 0 && flows.length > 0 && (
          <div className="text-center py-16 flow-card-enter">
            <Search className="w-8 h-8 text-navy-200 mx-auto mb-2" strokeWidth={1.5} />
            <p className="text-sm text-navy-400">
              &ldquo;<span className="font-medium text-navy-500">{search}</span>&rdquo; ile eslesen flow bulunamadi
            </p>
          </div>
        )}
      </main>

      {/* ── New Flow Dialog ── */}
      {showNewDialog && (
        <div className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50" onClick={() => !creating && setShowNewDialog(false)}>
          <div
            className="bg-white border border-navy-100 rounded-2xl w-full max-w-md p-6 shadow-elevated flow-card-enter"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between mb-5">
              <h2 className="text-lg font-display font-semibold text-navy-900">Yeni Flow Olustur</h2>
              <button
                onClick={() => setShowNewDialog(false)}
                disabled={creating}
                className="p-1.5 rounded-lg text-navy-300 hover:text-navy-500 hover:bg-navy-50 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <input
              type="text"
              value={newFlowName}
              onChange={(e) => setNewFlowName(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
              placeholder="Flow adi (ornek: Satis Chatbot)"
              className="w-full px-3.5 py-2.5 bg-white border border-navy-100 rounded-xl text-navy-900 placeholder-navy-300 focus:outline-none focus:border-brand-300 focus:shadow-focus transition-all mb-3"
              autoFocus
              disabled={creating}
            />
            {newFlowError && (
              <p className="text-sm text-red-500 mb-3">{newFlowError}</p>
            )}
            <div className="flex justify-end">
              <button
                onClick={handleCreate}
                disabled={creating}
                className="flex items-center gap-1.5 px-5 py-2.5 bg-brand-500 hover:bg-brand-600 text-white text-sm font-medium rounded-xl transition-colors disabled:opacity-40"
              >
                {creating ? 'Olusturuluyor...' : 'Olustur'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Delete Confirm Dialog ── */}
      {deleteTarget && (
        <div className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50" onClick={() => setDeleteTarget(null)}>
          <div
            className="bg-white border border-navy-100 rounded-2xl w-full max-w-sm p-6 shadow-elevated flow-card-enter"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-display font-semibold text-navy-900">Flow'u Sil</h2>
              <button
                onClick={() => setDeleteTarget(null)}
                className="p-1.5 rounded-lg text-navy-300 hover:text-navy-500 hover:bg-navy-50 transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <p className="text-sm text-navy-400 mb-5 leading-relaxed">
              <strong className="text-navy-900">{deleteTarget.flow_name}</strong> flow'u kalici olarak silinecek. Bu islem geri alinamaz.
            </p>
            <div className="flex justify-end">
              <button
                onClick={() => handleDelete(deleteTarget)}
                className="px-5 py-2.5 text-sm bg-red-500 hover:bg-red-600 text-white font-medium rounded-xl transition-colors"
              >
                Evet, Sil
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Template Selection Modal (3-column) ── */}
      {showTemplateModal && (() => {
        const niches = Array.from(new Set(FLOW_TEMPLATES.map(t => t.niche)));
        const filteredTemplates = FLOW_TEMPLATES.filter(t => {
          const matchNiche = selectedNiche === 'all' || t.niche === selectedNiche;
          if (!templateSearch) return matchNiche;
          const q = templateSearch.toLowerCase();
          return matchNiche && (t.title.toLowerCase().includes(q) || t.description.toLowerCase().includes(q));
        });
        const nodeTypes = selectedTemplate
          ? Array.from(new Set(selectedTemplate.flowConfig.nodes.map(n => n.type))).filter(t => t !== 'trigger_start')
          : [];

        return (
          <div className="fixed inset-0 bg-navy-900/40 backdrop-blur-sm flex items-center justify-center z-50" onClick={() => !templateCreating && setShowTemplateModal(false)}>
            <div
              className="bg-white border border-navy-100 rounded-2xl w-full max-w-5xl h-[82vh] flex flex-col shadow-elevated flow-card-enter"
              onClick={(e) => e.stopPropagation()}
            >
              {/* Header */}
              <div className="flex items-center justify-between px-6 py-4 border-b border-navy-100">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-brand-500 to-brand-600 flex items-center justify-center">
                    <LayoutTemplate className="w-4 h-4 text-white" />
                  </div>
                  <div>
                    <h2 className="text-lg font-display font-semibold text-navy-900">Sablondan Olustur</h2>
                    <p className="text-xs text-navy-400">{FLOW_TEMPLATES.length} hazir sablon</p>
                  </div>
                </div>
                <button
                  onClick={() => setShowTemplateModal(false)}
                  disabled={!!templateCreating}
                  className="p-1.5 rounded-lg text-navy-300 hover:text-navy-500 hover:bg-navy-50 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* 3-Column Body */}
              <div className="flex-1 flex min-h-0">
                {/* Col 1: Niche Filters */}
                <div className="w-44 flex-shrink-0 border-r border-navy-100 py-3 px-3 overflow-y-auto">
                  <button
                    onClick={openBlankDialog}
                    className="w-full flex items-center gap-2 px-3 py-2 mb-3 border-2 border-dashed border-navy-200 rounded-lg text-navy-500 hover:border-brand-300 hover:text-brand-600 hover:bg-brand-50/30 transition-all text-xs font-medium"
                  >
                    <Plus className="w-3.5 h-3.5" strokeWidth={2} />
                    Bos Akis
                  </button>
                  <p className="text-[10px] font-bold uppercase text-navy-300 px-2 mb-1.5">Sektor</p>
                  <button
                    onClick={() => { setSelectedNiche('all'); setSelectedTemplate(null); }}
                    className={`w-full text-left px-3 py-1.5 rounded-lg text-xs transition-colors mb-0.5 ${selectedNiche === 'all' ? 'bg-brand-50 text-brand-700 font-semibold' : 'text-navy-500 hover:bg-navy-50'}`}
                  >
                    Tumunu Goster
                  </button>
                  {niches.map(niche => {
                    const count = FLOW_TEMPLATES.filter(t => t.niche === niche).length;
                    return (
                      <button
                        key={niche}
                        onClick={() => { setSelectedNiche(niche); setSelectedTemplate(null); }}
                        className={`w-full text-left px-3 py-1.5 rounded-lg text-xs transition-colors mb-0.5 flex items-center justify-between ${selectedNiche === niche ? 'bg-brand-50 text-brand-700 font-semibold' : 'text-navy-500 hover:bg-navy-50'}`}
                      >
                        <span>{NICHE_LABELS[niche] || niche}</span>
                        <span className="text-[10px] text-navy-300">{count}</span>
                      </button>
                    );
                  })}
                </div>

                {/* Col 2: Template Cards */}
                <div className={`flex-1 flex flex-col min-w-0 border-r border-navy-100 ${selectedTemplate ? '' : 'border-r-0'}`}>
                  {/* Search */}
                  <div className="px-4 py-3 border-b border-navy-50">
                    <div className="relative">
                      <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-navy-300" />
                      <input
                        type="text"
                        placeholder="Sablon ara..."
                        value={templateSearch}
                        onChange={e => { setTemplateSearch(e.target.value); setSelectedTemplate(null); }}
                        className="w-full pl-9 pr-4 py-2 text-sm border border-navy-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-brand-300 focus:border-transparent"
                        autoFocus
                      />
                    </div>
                  </div>

                  {/* Cards */}
                  <div className="flex-1 overflow-y-auto p-4">
                    <div className="grid grid-cols-2 gap-2.5">
                      {filteredTemplates.slice(0, 60).map(tpl => (
                        <button
                          key={tpl.id}
                          onClick={() => setSelectedTemplate(tpl)}
                          disabled={!!templateCreating}
                          className={`text-left p-3 border rounded-xl transition-all disabled:opacity-50 group ${
                            selectedTemplate?.id === tpl.id
                              ? 'border-brand-400 bg-brand-50/40 ring-1 ring-brand-200'
                              : 'border-navy-100 hover:border-brand-300 hover:bg-brand-50/20'
                          }`}
                        >
                          <div className="flex items-center gap-2 mb-1.5">
                            <GitBranch className="w-3 h-3 text-brand-500 flex-shrink-0" />
                            <span className="text-[10px] font-bold uppercase text-navy-400 truncate">
                              {NICHE_LABELS[tpl.niche] || tpl.niche}
                            </span>
                            <span className="text-[10px] text-navy-300 ml-auto flex-shrink-0">{tpl.nodeCount} node</span>
                          </div>
                          <p className="text-sm font-semibold text-navy-800 line-clamp-1 group-hover:text-brand-700 transition-colors">
                            {tpl.title}
                          </p>
                          <p className="text-[11px] text-navy-400 line-clamp-2 mt-0.5 leading-relaxed">{tpl.description}</p>
                        </button>
                      ))}
                    </div>
                    {filteredTemplates.length === 0 && (
                      <div className="text-center py-14">
                        <LayoutTemplate className="w-8 h-8 text-navy-200 mx-auto mb-2" />
                        <p className="text-sm text-navy-400">Eslesen sablon bulunamadi</p>
                      </div>
                    )}
                  </div>
                </div>

                {/* Col 3: Detail Panel */}
                {selectedTemplate && (
                  <div className="w-80 flex-shrink-0 flex flex-col overflow-y-auto">
                    <div className="p-5 flex-1">
                      {/* Title & badge */}
                      <div className="flex items-start gap-3 mb-4">
                        <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-brand-500 to-brand-600 flex items-center justify-center flex-shrink-0">
                          <GitBranch className="w-5 h-5 text-white" />
                        </div>
                        <div className="min-w-0">
                          <h3 className="text-base font-display font-bold text-navy-900 leading-snug">{selectedTemplate.title}</h3>
                          <div className="flex items-center gap-2 mt-1">
                            <span className="text-[10px] font-bold uppercase text-brand-600 bg-brand-50 px-1.5 py-0.5 rounded">
                              {NICHE_LABELS[selectedTemplate.niche] || selectedTemplate.niche}
                            </span>
                            <span className="text-[10px] text-navy-400">{selectedTemplate.nodeCount} node</span>
                          </div>
                        </div>
                      </div>

                      {/* Description */}
                      <div className="mb-5">
                        <h4 className="text-xs font-bold uppercase text-navy-400 mb-1.5">Aciklama</h4>
                        <p className="text-sm text-navy-600 leading-relaxed">{selectedTemplate.description}</p>
                      </div>

                      {/* Benefits */}
                      <div className="mb-5">
                        <h4 className="text-xs font-bold uppercase text-navy-400 mb-1.5">Ne Kazandirir</h4>
                        <ul className="space-y-1.5">
                          <li className="flex items-start gap-2 text-sm text-navy-600">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mt-1.5 flex-shrink-0" />
                            Hazir akis — sifirdan tasarim gerektirmez
                          </li>
                          <li className="flex items-start gap-2 text-sm text-navy-600">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mt-1.5 flex-shrink-0" />
                            {selectedTemplate.nodeCount} adimlik senaryo kurulumu
                          </li>
                          <li className="flex items-start gap-2 text-sm text-navy-600">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 mt-1.5 flex-shrink-0" />
                            AI Wizard ile sektorunuze ozellestirilir
                          </li>
                        </ul>
                      </div>

                      {/* Node Types */}
                      {nodeTypes.length > 0 && (
                        <div className="mb-5">
                          <h4 className="text-xs font-bold uppercase text-navy-400 mb-1.5">Kullanilan Node'lar</h4>
                          <div className="flex flex-wrap gap-1.5">
                            {nodeTypes.map(t => (
                              <span key={t} className="text-[10px] px-2 py-0.5 bg-navy-50 text-navy-500 rounded-full border border-navy-100">
                                {t.replace(/_/g, ' ')}
                              </span>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Tags */}
                      {selectedTemplate.tags.length > 0 && (
                        <div className="mb-5">
                          <h4 className="text-xs font-bold uppercase text-navy-400 mb-1.5">Etiketler</h4>
                          <div className="flex flex-wrap gap-1.5">
                            {selectedTemplate.tags.map(tag => (
                              <span key={tag} className="text-[10px] px-2 py-0.5 bg-brand-50 text-brand-600 rounded-full">
                                {tag}
                              </span>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>

                    {/* Action Button */}
                    <div className="p-5 pt-0 mt-auto">
                      <button
                        onClick={() => handleCreateFromTemplate(selectedTemplate)}
                        disabled={!!templateCreating}
                        className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-600 hover:to-brand-700 text-white text-sm font-semibold rounded-xl shadow-sm hover:shadow-md transition-all disabled:opacity-50"
                      >
                        {templateCreating === selectedTemplate.id ? (
                          'Olusturuluyor...'
                        ) : (
                          <>
                            <Sparkles className="w-4 h-4" />
                            Sablonu Kullan
                          </>
                        )}
                      </button>
                      <p className="text-[10px] text-navy-400 text-center mt-2">
                        Sablon yuklenecek ve AI Wizard ile ozellestirmeye hazir olacak
                      </p>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        );
      })()}
    </div>
  );
}

/* ── Sub-Components ──────────────────────────────────────── */

function IconBtn({
  icon: Icon,
  title,
  onClick,
  disabled,
  className = '',
}: {
  icon: LucideIcon;
  title: string;
  onClick: () => void;
  disabled?: boolean;
  className?: string;
}) {
  return (
    <button
      onClick={(e) => { e.stopPropagation(); onClick(); }}
      disabled={disabled}
      title={title}
      className={`p-1.5 rounded-lg transition-colors disabled:opacity-25 disabled:pointer-events-none ${className}`}
    >
      <Icon className="w-4 h-4" />
    </button>
  );
}

function HealthBadge({ score, issues }: { score: number; issues: string[] | null }) {
  let bg: string, text: string, border: string, label: string, summary: string;

  if (score >= 80) {
    bg = 'bg-emerald-50'; text = 'text-emerald-600'; border = 'border-emerald-100'; label = 'Saglikli';
    summary = 'Flow duzgun calisiyor, buyuk bir sorun yok.';
  } else if (score >= 50) {
    bg = 'bg-amber-50'; text = 'text-amber-600'; border = 'border-amber-100'; label = 'Dikkat';
    summary = 'Flow calisiyor ama iyilestirme gereken noktalar var.';
  } else {
    bg = 'bg-red-50'; text = 'text-red-600'; border = 'border-red-100'; label = 'Sorunlu';
    summary = 'Flow\'da kritik sorunlar var, duzenlenmesi gerekiyor.';
  }

  const buildTooltip = (): string => {
    const lines: string[] = [`Saglik Puani: ${score}/100 — ${summary}`];

    if (issues && issues.length > 0) {
      lines.push('');
      const errors = issues.filter(i => !i.startsWith('Orphan') && !i.startsWith('Dead-end') && !i.startsWith('Potansiyel') && !i.startsWith('Menu') && !i.startsWith('Kosul') && !i.startsWith('Intent') && !i.startsWith('FAQ') && !i.startsWith('Sentiment') && !i.startsWith('API dali') && !i.startsWith('Alt flow') && !i.startsWith('Switch'));
      const warnings = issues.filter(i => !errors.includes(i));

      if (errors.length > 0) {
        lines.push(`Hatalar (${errors.length}):`);
        errors.slice(0, 3).forEach(e => lines.push(`  • ${simplifyIssue(e)}`));
        if (errors.length > 3) lines.push(`  ... ve ${errors.length - 3} hata daha`);
      }
      if (warnings.length > 0) {
        if (errors.length > 0) lines.push('');
        lines.push(`Uyarilar (${warnings.length}):`);
        warnings.slice(0, 3).forEach(w => lines.push(`  • ${simplifyIssue(w)}`));
        if (warnings.length > 3) lines.push(`  ... ve ${warnings.length - 3} uyari daha`);
      }

      lines.push('');
      lines.push('Cift tiklayarak flow\'u duzenleyebilirsiniz.');
    }

    return lines.join('\n');
  };

  return (
    <span
      className={`px-1.5 py-px text-2xs font-medium ${bg} ${text} border ${border} rounded-full cursor-default`}
      title={buildTooltip()}
    >
      {score} &middot; {label}
    </span>
  );
}

/** Shorten validator messages into customer-friendly one-liners */
function simplifyIssue(raw: string): string {
  // Extract node label from patterns like: "... node 'My Label' (node-id) ..."
  const labelMatch = raw.match(/node '([^']+)'/i) ?? raw.match(/['']([^'']+)['']/);
  const nodeName = labelMatch?.[1];

  if (raw.includes('Trigger node bulunamadi'))
    return 'Baslangic adimi eksik — flow\'un nereden baslayacagi belirsiz';
  if (raw.includes('Birden fazla trigger'))
    return 'Birden fazla baslangic adimi var — sadece 1 tane olmali';
  if (raw.includes('Orphan'))
    return nodeName ? `"${nodeName}" adimina hicbir yerden ulasilamiyor` : 'Erisilemeyen adim var';
  if (raw.includes('Dead-end'))
    return nodeName ? `"${nodeName}" adiminda akis duruyor, devami yok` : 'Akisin devam etmedigi adim var';
  if (raw.includes('Zorunlu alan eksik'))
    return nodeName ? `"${nodeName}" adiminda eksik alan var` : 'Bir adimda zorunlu alan eksik';
  if (raw.includes('baglantisiz'))
    return nodeName ? `"${nodeName}" adiminda baglanti yapilmamis secenek var` : 'Baglantisiz secenek var';
  if (raw.includes('sonsuz dongu'))
    return nodeName ? `"${nodeName}" adiminda tekrar dongusu tespit edildi` : 'Tekrar dongusu tespit edildi';
  if (raw.includes('JSON parse'))
    return 'Ayar formati bozuk — adimi tekrar duzenleyin';

  return raw.split('—')[0].trim();
}

function SkeletonTable() {
  return (
    <div className="bg-white border border-navy-100 rounded-xl shadow-soft overflow-hidden flow-card-enter">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-navy-50/50 text-navy-500 text-xs">
            <tr>
              <th className="text-left font-medium px-4 py-2.5">Akış</th>
              <th className="text-left font-medium px-4 py-2.5">Sağlık</th>
              <th className="text-left font-medium px-4 py-2.5">Sürüm</th>
              <th className="text-left font-medium px-4 py-2.5">Atanan Hat</th>
              <th className="text-right font-medium px-4 py-2.5">İşlem</th>
            </tr>
          </thead>
          <tbody>
            {[0, 1, 2, 3].map(i => (
              <tr key={i} className="border-t border-navy-50">
                <td className="px-4 py-2.5">
                  <div className="flex items-center gap-3">
                    <div className="w-7 h-7 rounded-md flow-skeleton flex-shrink-0" />
                    <div className="h-4 w-40 flow-skeleton rounded" />
                  </div>
                </td>
                <td className="px-4 py-2.5"><div className="h-4 w-16 flow-skeleton rounded-full" /></td>
                <td className="px-4 py-2.5"><div className="h-3 w-8 flow-skeleton rounded" /></td>
                <td className="px-4 py-2.5"><div className="h-3 w-20 flow-skeleton rounded" /></td>
                <td className="px-4 py-2.5">
                  <div className="flex items-center justify-end gap-1">
                    <div className="w-7 h-7 flow-skeleton rounded-lg" />
                    <div className="w-7 h-7 flow-skeleton rounded-lg" />
                    <div className="w-7 h-7 flow-skeleton rounded-lg" />
                    <div className="w-7 h-7 flow-skeleton rounded-lg" />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
