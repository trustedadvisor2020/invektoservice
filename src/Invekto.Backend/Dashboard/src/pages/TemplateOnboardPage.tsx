import { useState, useEffect } from 'react';
import { api, TenantEntry, TemplateOnboardResult } from '../lib/api';
import { Users, RefreshCw, CheckCircle, AlertCircle } from 'lucide-react';

export function TemplateOnboardPage() {
  const [tenants, setTenants] = useState<TenantEntry[]>([]);
  const [selectedTenant, setSelectedTenant] = useState('');
  const [sector, setSector] = useState('eticaret');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<TemplateOnboardResult | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchTenants = async () => {
      try {
        const data = await api.getOpsTenants();
        setTenants(data.tenants);
      } catch (err) {
        console.error('Failed to fetch tenants:', err);
        setError('Tenant listesi yüklenemedi. Sayfayı yenileyin.');
      }
    };
    fetchTenants();
  }, []);

  const handleOnboard = async () => {
    if (!selectedTenant) return;
    setLoading(true);
    setError('');
    setResult(null);
    try {
      const res = await api.onboardTemplates(parseInt(selectedTenant), { sector });
      setResult(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Onboarding failed';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-4">
      {/* Header */}
      <div>
        <h1 className="text-lg font-semibold text-navy-900 flex items-center gap-2">
          <Users className="w-5 h-5" />
          Şablon Dağıtımı (Onboarding)
        </h1>
        <p className="text-xs text-navy-400">Sektöre ait yayınlanmış şablonları bir tenant'a toplu dağıt</p>
      </div>

      {/* Form */}
      <div className="bg-white rounded-lg border border-navy-100 p-4">
        <div className="grid grid-cols-3 gap-3">
          <div>
            <label className="text-[10px] text-navy-400 block mb-1">Tenant</label>
            <select
              value={selectedTenant}
              onChange={e => setSelectedTenant(e.target.value)}
              className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
            >
              <option value="">Tenant seçin...</option>
              {tenants.map(t => (
                <option key={t.tenantId} value={t.tenantId}>
                  {t.tenantName} (#{t.tenantId})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="text-[10px] text-navy-400 block mb-1">Sektör</label>
            <select
              value={sector}
              onChange={e => setSector(e.target.value)}
              className="w-full text-xs border border-navy-200 rounded px-2 py-1.5"
            >
              <option value="eticaret">E-Ticaret</option>
              <option value="dis_klinik">Diş Kliniği</option>
              <option value="estetik">Estetik</option>
            </select>
          </div>
          <div className="flex items-end">
            <button
              onClick={handleOnboard}
              disabled={loading || !selectedTenant}
              className="w-full flex items-center justify-center gap-1 px-3 py-1.5 text-xs font-medium rounded bg-navy-800 text-white hover:bg-navy-700 disabled:opacity-50"
            >
              {loading ? <RefreshCw className="w-3.5 h-3.5 animate-spin" /> : <Users className="w-3.5 h-3.5" />}
              {loading ? 'Dağıtılıyor...' : 'Şablonları Dağıt'}
            </button>
          </div>
        </div>

        {error && (
          <div className="mt-3 flex items-center gap-1 text-xs text-red-600">
            <AlertCircle className="w-3.5 h-3.5" /> {error}
          </div>
        )}
      </div>

      {/* Results */}
      {result && (
        <div className="bg-white rounded-lg border border-navy-100 p-4">
          <h2 className="text-sm font-medium text-navy-800 mb-3 flex items-center gap-1">
            <CheckCircle className="w-4 h-4 text-emerald-600" />
            Dağıtım Tamamlandı
          </h2>
          <div className="grid grid-cols-4 gap-3">
            <div className="bg-emerald-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-emerald-700">{result.adopted_count}</div>
              <div className="text-[10px] text-emerald-600">Benimsenen</div>
            </div>
            <div className="bg-amber-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-amber-700">{result.skipped_count}</div>
              <div className="text-[10px] text-amber-600">Atlanan</div>
            </div>
            <div className="bg-red-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-red-700">{result.failed_count}</div>
              <div className="text-[10px] text-red-600">Başarısız</div>
            </div>
            <div className="bg-navy-50 rounded-lg p-3 text-center">
              <div className="text-2xl font-bold text-navy-700">{result.duration_ms}</div>
              <div className="text-[10px] text-navy-500">ms süre</div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
