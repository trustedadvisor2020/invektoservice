import { useState, useEffect, useCallback } from 'react';
import { RefreshCw, Shield, AlertTriangle, CheckCircle2, XCircle, Star, Plus, Pencil, Trash2, X } from 'lucide-react';
import { api } from '../lib/api';
import type {
  RescueStatsResponse,
  ReviewRiskResponse,
  RescueTemplateResponse,
  RescueTemplateCreateRequest,
  RescueTemplateUpdateRequest,
} from '../lib/api';
import { useAuth } from '../hooks/useAuth';
import { Button } from '../components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card';

type TabKey = 'risks' | 'templates';

const RISK_LEVELS = ['critical', 'high', 'medium', 'low'] as const;
const RESCUE_STATUSES = ['pending', 'in_progress', 'rescued', 'failed', 'expired'] as const;
const STRATEGIES = ['apology', 'discount', 'free_return', 'exchange', 'full_refund'] as const;

const RISK_COLORS: Record<string, string> = {
  critical: 'bg-red-100 text-red-800',
  high: 'bg-orange-100 text-orange-800',
  medium: 'bg-yellow-100 text-yellow-800',
  low: 'bg-green-100 text-green-800',
};

const STATUS_COLORS: Record<string, string> = {
  pending: 'bg-slate-100 text-slate-700',
  in_progress: 'bg-blue-100 text-blue-700',
  rescued: 'bg-emerald-100 text-emerald-700',
  failed: 'bg-red-100 text-red-700',
  expired: 'bg-gray-100 text-gray-500',
};

const STRATEGY_LABELS: Record<string, string> = {
  apology: 'Özür',
  discount: 'İndirim',
  free_return: 'Ücretsiz İade',
  exchange: 'Değişim',
  full_refund: 'Tam İade',
};

// ================================================================
// KPI Cards
// ================================================================

function RescueKpiCards({ stats }: { stats: RescueStatsResponse }) {
  const cards = [
    { label: 'Toplam Risk', value: stats.total, icon: Shield, color: 'text-slate-600' },
    { label: 'Bekleyen', value: stats.pending, icon: AlertTriangle, color: 'text-amber-600' },
    { label: 'Kurtarılan', value: stats.rescued, icon: CheckCircle2, color: 'text-emerald-600' },
    { label: 'Başarısız', value: stats.failed, icon: XCircle, color: 'text-red-600' },
    { label: 'Kurtarma Oranı', value: `%${(stats.rescueRate * 100).toFixed(1)}`, icon: Star, color: 'text-teal-600' },
    { label: 'Yorum Sayısı', value: stats.reviewsPosted, icon: Star, color: 'text-indigo-600' },
  ];

  return (
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
      {cards.map(c => {
        const Icon = c.icon;
        return (
          <Card key={c.label}>
            <CardContent className="p-4">
              <div className="flex items-center gap-2 mb-1">
                <Icon className={`w-4 h-4 ${c.color}`} />
                <span className="text-xs text-slate-500">{c.label}</span>
              </div>
              <p className="text-xl font-semibold text-slate-900">{c.value}</p>
            </CardContent>
          </Card>
        );
      })}
    </div>
  );
}

// ================================================================
// Risk Table
// ================================================================

function RiskTable({ risks, onUpdate }: { risks: ReviewRiskResponse[]; onUpdate: () => void }) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editStatus, setEditStatus] = useState('');
  const [editStrategy, setEditStrategy] = useState('');

  const handleSave = async (id: number) => {
    await api.updateRescueRisk(id, {
      rescueStatus: editStatus || undefined,
      rescueStrategy: editStrategy || undefined,
    });
    setEditingId(null);
    onUpdate();
  };

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 text-left text-xs text-slate-500 uppercase">
            <th className="px-3 py-2">ID</th>
            <th className="px-3 py-2">Telefon</th>
            <th className="px-3 py-2">Risk</th>
            <th className="px-3 py-2">Skor</th>
            <th className="px-3 py-2">Durum</th>
            <th className="px-3 py-2">Strateji</th>
            <th className="px-3 py-2">Tarih</th>
            <th className="px-3 py-2">İşlem</th>
          </tr>
        </thead>
        <tbody>
          {risks.length === 0 && (
            <tr><td colSpan={8} className="px-3 py-8 text-center text-slate-400">Kayıt bulunamadı</td></tr>
          )}
          {risks.map(r => (
            <tr key={r.id} className="border-b border-slate-100 hover:bg-slate-50">
              <td className="px-3 py-2 font-mono text-xs">{r.id}</td>
              <td className="px-3 py-2">{r.customerPhone}</td>
              <td className="px-3 py-2">
                <span className={`px-2 py-0.5 rounded text-xs font-medium ${RISK_COLORS[r.riskLevel] ?? 'bg-slate-100'}`}>
                  {r.riskLevel}
                </span>
              </td>
              <td className="px-3 py-2 font-mono">{r.riskScore}</td>
              <td className="px-3 py-2">
                {editingId === r.id ? (
                  <select value={editStatus} onChange={e => setEditStatus(e.target.value)} className="border rounded px-1 py-0.5 text-xs">
                    <option value="">—</option>
                    {RESCUE_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
                  </select>
                ) : (
                  <span className={`px-2 py-0.5 rounded text-xs font-medium ${STATUS_COLORS[r.rescueStatus] ?? 'bg-slate-100'}`}>
                    {r.rescueStatus}
                  </span>
                )}
              </td>
              <td className="px-3 py-2">
                {editingId === r.id ? (
                  <select value={editStrategy} onChange={e => setEditStrategy(e.target.value)} className="border rounded px-1 py-0.5 text-xs">
                    <option value="">—</option>
                    {STRATEGIES.map(s => <option key={s} value={s}>{STRATEGY_LABELS[s]}</option>)}
                  </select>
                ) : (
                  r.rescueStrategy ? STRATEGY_LABELS[r.rescueStrategy] ?? r.rescueStrategy : '—'
                )}
              </td>
              <td className="px-3 py-2 text-xs text-slate-500">
                {new Date(r.createdAt).toLocaleDateString('tr-TR')}
              </td>
              <td className="px-3 py-2">
                {editingId === r.id ? (
                  <div className="flex gap-1">
                    <button onClick={() => handleSave(r.id)} className="text-emerald-600 hover:text-emerald-800 text-xs font-medium">Kaydet</button>
                    <button onClick={() => setEditingId(null)} className="text-slate-400 hover:text-slate-600 text-xs">İptal</button>
                  </div>
                ) : (
                  <button
                    onClick={() => { setEditingId(r.id); setEditStatus(r.rescueStatus); setEditStrategy(r.rescueStrategy ?? ''); }}
                    className="text-slate-400 hover:text-slate-600"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ================================================================
// Template Manager
// ================================================================

interface TemplateFormData {
  templateName: string;
  riskLevel: string;
  strategy: string;
  messageTemplate: string;
  maxDiscountPct: string;
}

const EMPTY_FORM: TemplateFormData = { templateName: '', riskLevel: 'medium', strategy: 'apology', messageTemplate: '', maxDiscountPct: '' };

function TemplateManager({ templates, onRefresh }: { templates: RescueTemplateResponse[]; onRefresh: () => void }) {
  const [showForm, setShowForm] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<RescueTemplateResponse | null>(null);
  const [form, setForm] = useState<TemplateFormData>(EMPTY_FORM);

  const openCreate = () => {
    setEditingTemplate(null);
    setForm(EMPTY_FORM);
    setShowForm(true);
  };

  const openEdit = (t: RescueTemplateResponse) => {
    setEditingTemplate(t);
    setForm({
      templateName: t.templateName,
      riskLevel: t.riskLevel,
      strategy: t.strategy,
      messageTemplate: t.messageTemplate,
      maxDiscountPct: t.maxDiscountPct?.toString() ?? '',
    });
    setShowForm(true);
  };

  const handleSubmit = async () => {
    if (editingTemplate) {
      const payload: RescueTemplateUpdateRequest = {
        templateName: form.templateName || undefined,
        messageTemplate: form.messageTemplate || undefined,
        maxDiscountPct: form.maxDiscountPct ? Number(form.maxDiscountPct) : undefined,
      };
      await api.updateRescueTemplate(editingTemplate.id, payload);
    } else {
      const payload: RescueTemplateCreateRequest = {
        templateName: form.templateName,
        riskLevel: form.riskLevel,
        strategy: form.strategy,
        messageTemplate: form.messageTemplate,
        maxDiscountPct: form.maxDiscountPct ? Number(form.maxDiscountPct) : undefined,
      };
      await api.createRescueTemplate(payload);
    }
    setShowForm(false);
    onRefresh();
  };

  const handleDelete = async (id: number) => {
    await api.deleteRescueTemplate(id);
    onRefresh();
  };

  const handleToggle = async (t: RescueTemplateResponse) => {
    await api.updateRescueTemplate(t.id, { isActive: !t.isActive });
    onRefresh();
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-3">
        <h3 className="text-sm font-medium text-slate-700">Kurtarma Şablonları</h3>
        <Button size="sm" variant="secondary" onClick={openCreate}>
          <Plus className="w-3.5 h-3.5 mr-1" /> Yeni Şablon
        </Button>
      </div>

      {showForm && (
        <Card className="mb-4">
          <CardHeader className="pb-2 flex flex-row items-center justify-between">
            <CardTitle className="text-sm">{editingTemplate ? 'Şablonu Düzenle' : 'Yeni Şablon'}</CardTitle>
            <button onClick={() => setShowForm(false)} className="text-slate-400 hover:text-slate-600">
              <X className="w-4 h-4" />
            </button>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="text-xs text-slate-500 block mb-1">Şablon Adı</label>
                <input
                  value={form.templateName}
                  onChange={e => setForm(f => ({ ...f, templateName: e.target.value }))}
                  className="w-full border rounded px-2 py-1.5 text-sm"
                  placeholder="Kritik Risk Özür Mesajı"
                />
              </div>
              <div>
                <label className="text-xs text-slate-500 block mb-1">Risk Seviyesi</label>
                <select
                  value={form.riskLevel}
                  onChange={e => setForm(f => ({ ...f, riskLevel: e.target.value }))}
                  className="w-full border rounded px-2 py-1.5 text-sm"
                  disabled={!!editingTemplate}
                >
                  {RISK_LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
                </select>
              </div>
              <div>
                <label className="text-xs text-slate-500 block mb-1">Strateji</label>
                <select
                  value={form.strategy}
                  onChange={e => setForm(f => ({ ...f, strategy: e.target.value }))}
                  className="w-full border rounded px-2 py-1.5 text-sm"
                  disabled={!!editingTemplate}
                >
                  {STRATEGIES.map(s => <option key={s} value={s}>{STRATEGY_LABELS[s]}</option>)}
                </select>
              </div>
              <div>
                <label className="text-xs text-slate-500 block mb-1">Maks. İndirim %</label>
                <input
                  type="number"
                  value={form.maxDiscountPct}
                  onChange={e => setForm(f => ({ ...f, maxDiscountPct: e.target.value }))}
                  className="w-full border rounded px-2 py-1.5 text-sm"
                  min={0}
                  max={100}
                  placeholder="25"
                />
              </div>
            </div>
            <div>
              <label className="text-xs text-slate-500 block mb-1">Mesaj Şablonu</label>
              <textarea
                value={form.messageTemplate}
                onChange={e => setForm(f => ({ ...f, messageTemplate: e.target.value }))}
                className="w-full border rounded px-2 py-1.5 text-sm h-20 resize-none"
                placeholder="Merhaba {customer_name}, deneyiminizden dolayı çok üzgünüz..."
              />
            </div>
            <div className="flex justify-end">
              <Button size="sm" onClick={handleSubmit} disabled={!form.templateName || !form.messageTemplate}>
                {editingTemplate ? 'Güncelle' : 'Oluştur'}
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-left text-xs text-slate-500 uppercase">
              <th className="px-3 py-2">Ad</th>
              <th className="px-3 py-2">Risk</th>
              <th className="px-3 py-2">Strateji</th>
              <th className="px-3 py-2">İndirim</th>
              <th className="px-3 py-2">Aktif</th>
              <th className="px-3 py-2">İşlem</th>
            </tr>
          </thead>
          <tbody>
            {templates.length === 0 && (
              <tr><td colSpan={6} className="px-3 py-8 text-center text-slate-400">Şablon bulunamadı</td></tr>
            )}
            {templates.map(t => (
              <tr key={t.id} className="border-b border-slate-100 hover:bg-slate-50">
                <td className="px-3 py-2 font-medium">{t.templateName}</td>
                <td className="px-3 py-2">
                  <span className={`px-2 py-0.5 rounded text-xs font-medium ${RISK_COLORS[t.riskLevel] ?? 'bg-slate-100'}`}>
                    {t.riskLevel}
                  </span>
                </td>
                <td className="px-3 py-2">{STRATEGY_LABELS[t.strategy] ?? t.strategy}</td>
                <td className="px-3 py-2">{t.maxDiscountPct != null ? `%${t.maxDiscountPct}` : '—'}</td>
                <td className="px-3 py-2">
                  <button
                    onClick={() => handleToggle(t)}
                    className={`w-8 h-4 rounded-full relative transition-colors ${t.isActive ? 'bg-emerald-500' : 'bg-slate-300'}`}
                  >
                    <span className={`absolute top-0.5 w-3 h-3 rounded-full bg-white transition-transform ${t.isActive ? 'left-4' : 'left-0.5'}`} />
                  </button>
                </td>
                <td className="px-3 py-2">
                  <div className="flex gap-2">
                    <button onClick={() => openEdit(t)} className="text-slate-400 hover:text-slate-600">
                      <Pencil className="w-3.5 h-3.5" />
                    </button>
                    <button onClick={() => handleDelete(t.id)} className="text-slate-400 hover:text-red-600">
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
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

// ================================================================
// Main Page
// ================================================================

export function RescueDashboardPage() {
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 0;

  const [tab, setTab] = useState<TabKey>('risks');
  const [stats, setStats] = useState<RescueStatsResponse | null>(null);
  const [risks, setRisks] = useState<ReviewRiskResponse[]>([]);
  const [templates, setTemplates] = useState<RescueTemplateResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [filterLevel, setFilterLevel] = useState('');
  const [filterStatus, setFilterStatus] = useState('');

  const fetchAll = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const [statsRes, risksRes, templatesRes] = await Promise.all([
        api.getRescueStats(),
        api.listRescueRisks(filterLevel || undefined, filterStatus || undefined),
        api.listRescueTemplates(),
      ]);
      setStats(statsRes);
      setRisks(risksRes);
      setTemplates(templatesRes);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(`Rescue verileri yüklenemedi: ${msg}`);
    } finally {
      setLoading(false);
    }
  }, [tenantId, filterLevel, filterStatus]);

  useEffect(() => { fetchAll(); }, [fetchAll]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Shield className="w-6 h-6 text-teal-600" />
          <h1 className="text-xl font-semibold text-slate-900">Review Rescue AI</h1>
        </div>
        <Button size="sm" variant="secondary" onClick={fetchAll} disabled={loading}>
          <RefreshCw className={`w-4 h-4 mr-1.5 ${loading ? 'animate-spin' : ''}`} /> Yenile
        </Button>
      </div>

      {/* Error */}
      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="p-3 text-sm text-red-700">{error}</CardContent>
        </Card>
      )}

      {/* KPI Cards */}
      {stats && <RescueKpiCards stats={stats} />}

      {/* Tabs */}
      <div className="flex gap-1 border-b border-slate-200">
        <button
          onClick={() => setTab('risks')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            tab === 'risks' ? 'border-teal-500 text-teal-700' : 'border-transparent text-slate-500 hover:text-slate-700'
          }`}
        >
          Riskler ({risks.length})
        </button>
        <button
          onClick={() => setTab('templates')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            tab === 'templates' ? 'border-teal-500 text-teal-700' : 'border-transparent text-slate-500 hover:text-slate-700'
          }`}
        >
          Şablonlar ({templates.length})
        </button>
      </div>

      {/* Content */}
      {tab === 'risks' && (
        <Card>
          <CardHeader className="pb-3">
            <div className="flex items-center gap-3">
              <select
                value={filterLevel}
                onChange={e => setFilterLevel(e.target.value)}
                className="border rounded px-2 py-1 text-sm"
              >
                <option value="">Tüm Seviyeler</option>
                {RISK_LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
              </select>
              <select
                value={filterStatus}
                onChange={e => setFilterStatus(e.target.value)}
                className="border rounded px-2 py-1 text-sm"
              >
                <option value="">Tüm Durumlar</option>
                {RESCUE_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <RiskTable risks={risks} onUpdate={fetchAll} />
          </CardContent>
        </Card>
      )}

      {tab === 'templates' && (
        <Card>
          <CardContent className="p-4">
            <TemplateManager templates={templates} onRefresh={fetchAll} />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
