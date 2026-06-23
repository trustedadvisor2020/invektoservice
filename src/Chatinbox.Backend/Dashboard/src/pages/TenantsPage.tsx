import { useState, useEffect, useCallback } from 'react';
import { api, TenantEntry, type HealthResponse, type ServiceHealth } from '../lib/api';
import { RefreshCw, LogIn, CheckCircle, XCircle, AlertTriangle, Download } from 'lucide-react';
import { usePolling } from '../hooks/usePolling';
import { HealthCard } from '../components/HealthCard';
import { Button } from '../components/ui/Button';
import { Card, CardContent } from '../components/ui/Card';

export function TenantsPage() {
  const [tenants, setTenants] = useState<TenantEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [impersonating, setImpersonating] = useState<number | null>(null);
  const [restartingService, setRestartingService] = useState<string | null>(null);

  // Health polling (30s)
  const { data: healthData, isLoading: healthLoading, refresh: refreshHealth } = usePolling<HealthResponse>({
    fetcher: () => api.getHealth(),
    interval: 30000,
  });

  const services = healthData?.services || [];
  const hasErrors = services.some(s => s.status !== 'ok');

  const handleRestart = useCallback(async (service: ServiceHealth) => {
    if (!confirm(`${service.name} servisini yeniden başlatmak istediğinize emin misiniz?`)) {
      return;
    }

    setRestartingService(service.name);
    try {
      const result = await api.restartService(service.name);
      if (result.success) {
        alert(`${service.name} yeniden başlatıldı.`);
        setTimeout(refreshHealth, 5000);
      } else {
        alert(`Yeniden başlatma başarısız: ${result.message}`);
      }
    } catch (error) {
      alert(`Yeniden başlatma hatası: ${error instanceof Error ? error.message : 'Bilinmeyen hata'}`);
    } finally {
      setRestartingService(null);
    }
  }, [refreshHealth]);

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

  useEffect(() => {
    fetchTenants();
  }, [fetchTenants]);

  const handleImpersonate = async (tenantId: number) => {
    setImpersonating(tenantId);
    try {
      const resp = await api.impersonateTenant(tenantId);
      api.setSession(resp);
      // Full page reload — reinitializes all hooks with new session context
      window.location.href = '/';
    } catch (err) {
      console.error('Impersonate failed:', err);
      setImpersonating(null);
    }
  };

  return (
    <div className="space-y-6">
      {/* Service Health Section */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="text-lg font-semibold text-navy-900">Servis Durumu</h2>
            <p className="text-xs text-navy-400">Servis sağlığı ve metrikler</p>
          </div>
          <div className="flex gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                fetch('/api/ops/postman', { headers: api.getAuthHeaders() })
                  .then(r => r.blob())
                  .then(blob => {
                    const url = URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = 'InvektoServis.postman_collection.json';
                    link.click();
                    URL.revokeObjectURL(url);
                  });
              }}
            >
              <Download className="w-4 h-4 flex-shrink-0" />
              <span>Postman</span>
            </Button>
            <Button variant="secondary" size="sm" onClick={refreshHealth} disabled={healthLoading}>
              <RefreshCw className={`w-4 h-4 flex-shrink-0 ${healthLoading ? 'animate-spin' : ''}`} />
              <span>Yenile</span>
            </Button>
          </div>
        </div>

        {/* Alert banner */}
        {hasErrors && (
          <div className="p-3 mb-3 bg-red-50 border border-red-100 rounded-xl flex items-center gap-3">
            <div className="w-7 h-7 bg-red-100 rounded-lg flex items-center justify-center flex-shrink-0">
              <AlertTriangle className="w-3.5 h-3.5 text-red-500" />
            </div>
            <span className="text-sm text-red-600 font-medium">Bazı servisler çalışmıyor!</span>
          </div>
        )}

        {/* Health Cards */}
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3">
          {services.length > 0 ? (
            services.map(service => (
              <HealthCard
                key={service.name}
                service={service}
                onRestart={() => handleRestart(service)}
                isRestarting={restartingService === service.name}
              />
            ))
          ) : (
            <Card className="col-span-full">
              <CardContent className="py-6 text-center text-navy-300 text-sm">
                {healthLoading ? 'Servisler yükleniyor...' : 'Servis bulunamadı'}
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      {/* Tenants Section */}
      <div>
        <div className="flex items-center justify-between mb-3">
          <div>
            <h1 className="text-xl font-semibold text-navy-900">Firmalar</h1>
            <p className="text-sm text-navy-400">
              Tüm kayıtlı firmalar — firma admini olarak giriş yapın
              {tenants.length > 0 && (
                <span className="ml-2 text-navy-300">({tenants.length} firma)</span>
              )}
            </p>
          </div>
          <button
            onClick={fetchTenants}
            disabled={loading}
            className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-white border border-navy-100 rounded-lg hover:bg-navy-50 disabled:opacity-50"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
            Yenile
          </button>
        </div>

        {/* Table */}
        <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-navy-50 border-b border-navy-100">
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">ID</th>
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">Firma Adı</th>
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">Durum</th>
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">Sektor</th>
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">Plan</th>
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">Kayit Tarihi</th>
                  <th className="text-left px-4 py-2.5 font-medium text-navy-500">İşlem</th>
                </tr>
              </thead>
              <tbody>
                {loading && tenants.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="text-center py-12 text-navy-300">
                      Yükleniyor...
                    </td>
                  </tr>
                ) : tenants.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="text-center py-12 text-navy-300">
                      Firma bulunamadı
                    </td>
                  </tr>
                ) : (
                  tenants.map(t => (
                    <tr key={t.tenantId} className="border-b border-navy-100 hover:bg-navy-50/50">
                      <td className="px-4 py-2.5">
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-navy-100 text-navy-700">
                          #{t.tenantId}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 font-medium text-navy-900">
                        {t.tenantName}
                      </td>
                      <td className="px-4 py-2.5">
                        {t.isActive ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-green-50 text-green-700">
                            <CheckCircle className="w-3 h-3" />
                            Aktif
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-red-50 text-red-700">
                            <XCircle className="w-3 h-3" />
                            Pasif
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-xs text-navy-400">
                        {t.sector ?? '-'}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="px-2 py-0.5 rounded text-xs font-medium bg-blue-50 text-blue-700">
                          {t.planTier}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-xs text-navy-400">
                        {formatDate(t.createdAt)}
                      </td>
                      <td className="px-4 py-2.5">
                        {t.isActive ? (
                          <button
                            onClick={() => handleImpersonate(t.tenantId)}
                            disabled={impersonating === t.tenantId}
                            className="inline-flex items-center gap-1.5 px-3 py-1 text-xs font-medium bg-brand-500 text-white rounded-md hover:bg-brand-600 disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            <LogIn className="w-3 h-3" />
                            {impersonating === t.tenantId ? 'Giriliyor...' : 'Giriş Yap'}
                          </button>
                        ) : (
                          <span className="text-xs text-navy-300">Devre dışı</span>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
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
