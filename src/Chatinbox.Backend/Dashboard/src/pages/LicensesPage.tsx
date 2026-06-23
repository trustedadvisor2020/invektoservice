import { useState, useEffect, useCallback } from 'react';
import {
  api,
  type TenantEntry,
  type TenantLicenseInfo,
  type PlanDefinition,
} from '../lib/api';
import {
  RefreshCw,
  X,
  CheckCircle,
  XCircle,
  AlertTriangle,
  Clock,
  Shield,
  Users,
  Zap,
  ChevronDown,
  ChevronUp,
  Save,
} from 'lucide-react';
import { Button } from '../components/ui/Button';
import { Card, CardContent } from '../components/ui/Card';

const PLAN_TIERS = ['baslangic', 'profesyonel', 'kurumsal'] as const;
const PLAN_LABELS: Record<string, string> = {
  baslangic: 'Başlangıç',
  profesyonel: 'Profesyonel',
  kurumsal: 'Kurumsal',
};

export function LicensesPage() {
  const [tenants, setTenants] = useState<TenantEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState<TenantEntry | null>(null);
  const [licenseInfo, setLicenseInfo] = useState<TenantLicenseInfo | null>(null);
  const [licenseLoading, setLicenseLoading] = useState(false);
  const [plans, setPlans] = useState<PlanDefinition[]>([]);
  const [search, setSearch] = useState('');

  // Edit state (Invekto side)
  const [editTier, setEditTier] = useState('');
  const [editFeatJson, setEditFeatJson] = useState('');
  const [featJsonError, setFeatJsonError] = useState('');
  const [saving, setSaving] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  // INMA history accordion
  const [historyOpen, setHistoryOpen] = useState(false);

  const fetchTenants = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.getOpsTenants();
      setTenants(result.tenants);
    } catch (err) {
      console.error('Tenant list fetch failed:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchPlans = useCallback(async () => {
    try {
      const result = await api.getPlans();
      setPlans(result.plans);
    } catch (err) {
      console.error('Plans fetch failed:', err);
    }
  }, []);

  useEffect(() => {
    fetchTenants();
    fetchPlans();
  }, [fetchTenants, fetchPlans]);

  const handleSelectTenant = async (tenant: TenantEntry) => {
    setSelectedTenant(tenant);
    setLicenseInfo(null);
    setLicenseLoading(true);
    setSaveSuccess(false);
    setHistoryOpen(false);
    setFeatJsonError('');
    try {
      const info = await api.getTenantLicense(tenant.tenantId);
      setLicenseInfo(info);
      setEditTier(info.invekto.plan_tier);
      setEditFeatJson(
        info.invekto.has_override
          ? JSON.stringify(info.invekto.effective_features, null, 2)
          : ''
      );
    } catch (err) {
      console.error('License fetch failed:', err);
    } finally {
      setLicenseLoading(false);
    }
  };

  const handleSave = async () => {
    if (!selectedTenant || !licenseInfo) return;

    let featuresJson: Record<string, unknown> | null = null;
    if (editFeatJson.trim()) {
      try {
        featuresJson = JSON.parse(editFeatJson);
        setFeatJsonError('');
      } catch {
        setFeatJsonError('Geçersiz JSON formatı');
        return;
      }
    }

    setSaving(true);
    try {
      await api.updateTenantPlan(selectedTenant.tenantId, {
        plan_tier: editTier !== licenseInfo.invekto.plan_tier ? editTier : undefined,
        features_json: editFeatJson.trim() === '' ? null : featuresJson,
      });
      setSaveSuccess(true);
      // Refresh license info
      const updated = await api.getTenantLicense(selectedTenant.tenantId);
      setLicenseInfo(updated);
      setEditTier(updated.invekto.plan_tier);
      // Update tenant list plan tier badge
      setTenants(prev =>
        prev.map(t =>
          t.tenantId === selectedTenant.tenantId
            ? { ...t, planTier: updated.invekto.plan_tier }
            : t
        )
      );
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (err) {
      console.error('Plan update failed:', err);
    } finally {
      setSaving(false);
    }
  };

  const filteredTenants = tenants.filter(t =>
    search === '' ||
    t.tenantName.toLowerCase().includes(search.toLowerCase()) ||
    t.tenantId.toString().includes(search)
  );

  return (
    <div className="flex h-full gap-4" style={{ minHeight: 0 }}>
      {/* Left: Tenant List */}
      <div className="w-80 flex-shrink-0 flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-lg font-semibold text-navy-900">Lisanslama</h1>
            <p className="text-xs text-navy-400">{tenants.length} firma</p>
          </div>
          <Button variant="ghost" size="sm" onClick={fetchTenants} disabled={loading}>
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
          </Button>
        </div>

        <input
          type="text"
          value={search}
          onChange={e => setSearch(e.target.value)}
          placeholder="Firma ara..."
          className="w-full px-3 py-2 text-sm border border-navy-100 rounded-lg focus:outline-none focus:ring-2 focus:ring-brand-400"
        />

        <div className="flex-1 overflow-y-auto space-y-1" style={{ maxHeight: 'calc(100vh - 200px)' }}>
          {filteredTenants.length === 0 ? (
            <div className="text-center py-8 text-navy-300 text-sm">
              {loading ? 'Yükleniyor...' : 'Firma bulunamadı'}
            </div>
          ) : (
            filteredTenants.map(t => (
              <button
                key={t.tenantId}
                onClick={() => handleSelectTenant(t)}
                className={`w-full text-left px-3 py-2.5 rounded-lg border transition-colors ${
                  selectedTenant?.tenantId === t.tenantId
                    ? 'bg-brand-50 border-brand-200'
                    : 'bg-white border-navy-100 hover:bg-navy-50'
                }`}
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="text-sm font-medium text-navy-900 truncate">{t.tenantName}</span>
                  <span
                    className={`flex-shrink-0 px-1.5 py-0.5 rounded text-xs font-medium ${
                      t.planTier === 'kurumsal'
                        ? 'bg-purple-50 text-purple-700'
                        : t.planTier === 'profesyonel'
                        ? 'bg-blue-50 text-blue-700'
                        : 'bg-navy-100 text-navy-600'
                    }`}
                  >
                    {PLAN_LABELS[t.planTier] ?? t.planTier}
                  </span>
                </div>
                <div className="flex items-center gap-2 mt-0.5">
                  <span className="text-xs text-navy-400">#{t.tenantId}</span>
                  {!t.isActive && (
                    <span className="text-xs text-red-500">Pasif</span>
                  )}
                </div>
              </button>
            ))
          )}
        </div>
      </div>

      {/* Right: Detail Panel */}
      <div className="flex-1 overflow-y-auto">
        {!selectedTenant ? (
          <Card className="h-full">
            <CardContent className="flex flex-col items-center justify-center h-64 text-navy-300">
              <Shield className="w-10 h-10 mb-3 opacity-30" />
              <p className="text-sm">Lisans detayı görmek için bir firma seçin</p>
            </CardContent>
          </Card>
        ) : licenseLoading ? (
          <Card className="h-full">
            <CardContent className="flex items-center justify-center h-64 text-navy-300">
              <RefreshCw className="w-5 h-5 animate-spin" />
            </CardContent>
          </Card>
        ) : licenseInfo ? (
          <div className="space-y-4">
            {/* Header */}
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-semibold text-navy-900">{selectedTenant.tenantName}</h2>
                <p className="text-xs text-navy-400">#{selectedTenant.tenantId} · {selectedTenant.sector ?? 'Sektör belirtilmemiş'}</p>
              </div>
              <button
                onClick={() => setSelectedTenant(null)}
                className="p-1.5 rounded-lg hover:bg-navy-100 text-navy-400"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* INMA Section */}
            <div className="bg-navy-50 rounded-xl border border-navy-100 p-4 space-y-3">
              <div className="flex items-center gap-2 mb-1">
                <div className="w-6 h-6 bg-navy-200 rounded-md flex items-center justify-center">
                  <Shield className="w-3.5 h-3.5 text-navy-600" />
                </div>
                <span className="text-sm font-semibold text-navy-700">INMA Lisansı</span>
                <span className="ml-auto text-xs text-navy-400 italic">Sadece görüntüleme</span>
              </div>

              {licenseInfo.inma ? (
                <>
                  <div className="grid grid-cols-2 gap-3">
                    <InfoBox
                      label="Lisans Tipi"
                      value={licenseInfo.inma.license_type ?? '-'}
                    />
                    <InfoBox
                      label="Bitiş Tarihi"
                      value={licenseInfo.inma.license_expire_date ? formatDate(licenseInfo.inma.license_expire_date) : '-'}
                      highlight={
                        licenseInfo.inma.is_expired
                          ? 'danger'
                          : licenseInfo.inma.days_until_expiry != null && licenseInfo.inma.days_until_expiry <= 30
                          ? 'warning'
                          : 'ok'
                      }
                    />
                    <InfoBox
                      label="Kullanıcı Lisansı"
                      value={licenseInfo.inma.user_license_count != null ? `${licenseInfo.inma.user_license_count} kullanıcı` : '-'}
                      icon={<Users className="w-3 h-3" />}
                    />
                    <InfoBox
                      label="Instance Lisansı"
                      value={licenseInfo.inma.instance_license_count != null ? `${licenseInfo.inma.instance_license_count} hat` : '-'}
                      icon={<Zap className="w-3 h-3" />}
                    />
                    {licenseInfo.inma.message_limit_daily != null && (
                      <InfoBox
                        label="Günlük Mesaj Limiti"
                        value={licenseInfo.inma.message_limit_daily.toLocaleString('tr-TR')}
                      />
                    )}
                    {licenseInfo.inma.license_renewal_period != null && (
                      <InfoBox
                        label="Yenileme Periyodu"
                        value={`${licenseInfo.inma.license_renewal_period} ay`}
                        icon={<Clock className="w-3 h-3" />}
                      />
                    )}
                  </div>

                  {/* Expiry banner */}
                  {licenseInfo.inma.is_expired && (
                    <div className="flex items-center gap-2 px-3 py-2 bg-red-50 border border-red-100 rounded-lg">
                      <XCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                      <span className="text-xs text-red-600 font-medium">Lisans süresi dolmuş</span>
                    </div>
                  )}
                  {!licenseInfo.inma.is_expired && licenseInfo.inma.days_until_expiry != null && licenseInfo.inma.days_until_expiry <= 30 && (
                    <div className="flex items-center gap-2 px-3 py-2 bg-amber-50 border border-amber-100 rounded-lg">
                      <AlertTriangle className="w-4 h-4 text-amber-500 flex-shrink-0" />
                      <span className="text-xs text-amber-700 font-medium">
                        Lisans {licenseInfo.inma.days_until_expiry} gün içinde sona eriyor
                      </span>
                    </div>
                  )}
                  {!licenseInfo.inma.is_expired && licenseInfo.inma.days_until_expiry != null && licenseInfo.inma.days_until_expiry > 30 && (
                    <div className="flex items-center gap-2 px-3 py-2 bg-green-50 border border-green-100 rounded-lg">
                      <CheckCircle className="w-4 h-4 text-green-500 flex-shrink-0" />
                      <span className="text-xs text-green-700 font-medium">
                        Lisans aktif — {licenseInfo.inma.days_until_expiry} gün kaldı
                      </span>
                    </div>
                  )}

                  {/* History accordion */}
                  {licenseInfo.inma.histories.length > 0 && (
                    <div>
                      <button
                        onClick={() => setHistoryOpen(o => !o)}
                        className="flex items-center gap-1.5 text-xs text-navy-500 hover:text-navy-700"
                      >
                        {historyOpen ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
                        Lisans Geçmişi ({licenseInfo.inma.histories.length} kayıt)
                      </button>
                      {historyOpen && (
                        <div className="mt-2 space-y-1">
                          {licenseInfo.inma.histories.map((h, i) => (
                            <div key={i} className="flex items-center gap-3 px-3 py-1.5 bg-white rounded-lg border border-navy-100 text-xs">
                              <span className="text-navy-600">{h.start ?? '-'} → {h.end ?? '-'}</span>
                              <span
                                className={`ml-auto px-1.5 py-0.5 rounded font-medium ${
                                  h.is_paid ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-600'
                                }`}
                              >
                                {h.is_paid ? 'Ödendi' : 'Ödenmedi'}
                              </span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </>
              ) : (
                <div className="flex items-center gap-2 text-xs text-navy-400 py-2">
                  <AlertTriangle className="w-4 h-4" />
                  <span>INMA lisans bilgisi bulunamadı — InvektoCompanyCode eşleşmesi yok ya da bağlantı kurulamadı</span>
                </div>
              )}
            </div>

            {/* Invekto Section */}
            <div className="bg-white rounded-xl border border-navy-100 p-4 space-y-4">
              <div className="flex items-center gap-2 mb-1">
                <div className="w-6 h-6 bg-brand-100 rounded-md flex items-center justify-center">
                  <Zap className="w-3.5 h-3.5 text-brand-600" />
                </div>
                <span className="text-sm font-semibold text-navy-700">Invekto Planı</span>
              </div>

              {/* Plan Tier Selector */}
              <div>
                <label className="block text-xs font-medium text-navy-500 mb-1.5">Plan Tier</label>
                <div className="flex gap-2">
                  {PLAN_TIERS.map(tier => (
                    <button
                      key={tier}
                      onClick={() => setEditTier(tier)}
                      className={`flex-1 py-2 rounded-lg text-xs font-medium border transition-colors ${
                        editTier === tier
                          ? tier === 'kurumsal'
                            ? 'bg-purple-500 text-white border-purple-500'
                            : tier === 'profesyonel'
                            ? 'bg-blue-500 text-white border-blue-500'
                            : 'bg-navy-700 text-white border-navy-700'
                          : 'bg-white text-navy-500 border-navy-200 hover:bg-navy-50'
                      }`}
                    >
                      {PLAN_LABELS[tier]}
                    </button>
                  ))}
                </div>
                {plans.find(p => p.tier_name === editTier) && (
                  <div className="mt-2 px-3 py-2 bg-navy-50 rounded-lg">
                    <p className="text-xs text-navy-500 font-medium mb-1">Plan Kotaları</p>
                    {(() => {
                      const plan = plans.find(p => p.tier_name === editTier);
                      if (!plan) return null;
                      const q = plan.quotas_json;
                      return (
                        <div className="flex gap-4 text-xs text-navy-600">
                          <span>{q.messages_per_month === -1 ? 'Sınırsız mesaj' : `${q.messages_per_month.toLocaleString('tr-TR')} mesaj/ay`}</span>
                          <span>{q.max_users === -1 ? 'Sınırsız kullanıcı' : `${q.max_users} kullanıcı`}</span>
                          <span>{q.max_flows === -1 ? 'Sınırsız flow' : `${q.max_flows} flow`}</span>
                        </div>
                      );
                    })()}
                  </div>
                )}
              </div>

              {/* Usage Bar */}
              {(() => {
                const usage = licenseInfo.invekto.usage;
                const limit = licenseInfo.invekto.quotas.messages_per_month;
                const pct = limit > 0 ? Math.min(100, Math.round((usage.messages_sent / limit) * 100)) : 0;
                return (
                  <div>
                    <div className="flex items-center justify-between mb-1">
                      <label className="text-xs font-medium text-navy-500">
                        Mesaj Kullanımı ({usage.period_month})
                      </label>
                      <span className="text-xs text-navy-400">
                        {limit === -1
                          ? `${usage.messages_sent.toLocaleString('tr-TR')} / Sınırsız`
                          : `${usage.messages_sent.toLocaleString('tr-TR')} / ${limit.toLocaleString('tr-TR')}`}
                      </span>
                    </div>
                    {limit !== -1 && (
                      <div className="w-full bg-navy-100 rounded-full h-2">
                        <div
                          className={`h-2 rounded-full transition-all ${
                            pct >= 90 ? 'bg-red-500' : pct >= 70 ? 'bg-amber-400' : 'bg-green-500'
                          }`}
                          style={{ width: `${pct}%` }}
                        />
                      </div>
                    )}
                  </div>
                );
              })()}

              {/* Feature Override (advanced) */}
              <div>
                <label className="block text-xs font-medium text-navy-500 mb-1">
                  Feature Override (JSON)
                  <span className="ml-1 text-navy-300 font-normal">— boş bırakılırsa plan tier varsayılanı kullanılır</span>
                </label>
                <textarea
                  value={editFeatJson}
                  onChange={e => {
                    setEditFeatJson(e.target.value);
                    setFeatJsonError('');
                  }}
                  placeholder='{ "FlowBuilder": ["basic", "premium"] }'
                  rows={4}
                  className={`w-full px-3 py-2 text-xs font-mono border rounded-lg focus:outline-none focus:ring-2 focus:ring-brand-400 resize-none ${
                    featJsonError ? 'border-red-400 focus:ring-red-400' : 'border-navy-200'
                  }`}
                />
                {featJsonError && (
                  <p className="text-xs text-red-500 mt-1">{featJsonError}</p>
                )}
                {licenseInfo.invekto.has_override && (
                  <p className="text-xs text-amber-600 mt-1">
                    ⚠ Bu tenant için aktif feature override var
                  </p>
                )}
              </div>

              {/* Save Button */}
              <div className="flex items-center gap-3 pt-1">
                <Button
                  variant="primary"
                  size="sm"
                  onClick={handleSave}
                  disabled={saving}
                >
                  <Save className="w-3.5 h-3.5" />
                  {saving ? 'Kaydediliyor...' : 'Kaydet'}
                </Button>
                {saveSuccess && (
                  <span className="flex items-center gap-1 text-xs text-green-600">
                    <CheckCircle className="w-3.5 h-3.5" />
                    Güncellendi
                  </span>
                )}
              </div>
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function InfoBox({
  label,
  value,
  highlight,
  icon,
}: {
  label: string;
  value: string;
  highlight?: 'ok' | 'warning' | 'danger';
  icon?: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-lg border border-navy-100 px-3 py-2">
      <p className="text-xs text-navy-400 mb-0.5">{label}</p>
      <div className="flex items-center gap-1">
        {icon && <span className="text-navy-500">{icon}</span>}
        <span
          className={`text-sm font-medium ${
            highlight === 'danger'
              ? 'text-red-600'
              : highlight === 'warning'
              ? 'text-amber-600'
              : 'text-navy-900'
          }`}
        >
          {value}
        </span>
      </div>
    </div>
  );
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}
