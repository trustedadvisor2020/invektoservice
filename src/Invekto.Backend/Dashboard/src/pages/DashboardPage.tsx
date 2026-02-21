import { useState, useCallback } from 'react';
import { RefreshCw, AlertTriangle, Download } from 'lucide-react';
import { api, type HealthResponse, type ServiceHealth } from '../lib/api';
import { usePolling } from '../hooks/usePolling';
import { HealthCard } from '../components/HealthCard';
import { Button } from '../components/ui/Button';
import { Card, CardContent } from '../components/ui/Card';

export function DashboardPage() {
  const [restartingService, setRestartingService] = useState<string | null>(null);

  // Fetch health data every 30 seconds (reduced from 10s to prevent connection exhaustion)
  const { data: healthData, isLoading: healthLoading, refresh: refreshHealth } = usePolling<HealthResponse>({
    fetcher: () => api.getHealth(),
    interval: 30000,
  });

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
        alert(`Yeniden baslatma basarisiz: ${result.message}`);
      }
    } catch (error) {
      alert(`Yeniden baslatma hatasi: ${error instanceof Error ? error.message : 'Bilinmeyen hata'}`);
    } finally {
      setRestartingService(null);
    }
  }, [refreshHealth]);

  const services = healthData?.services || [];
  const hasErrors = services.some(s => s.status !== 'ok');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-navy-900">Kontrol Paneli</h1>
          <p className="text-sm text-navy-400 mt-0.5">Servis durumu ve metrikler</p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              const link = document.createElement('a');
              link.href = '/api/ops/postman';
              link.download = 'InvektoServis.postman_collection.json';
              fetch('/api/ops/postman', { headers: api.getAuthHeaders() })
                .then(r => r.blob())
                .then(blob => {
                  const url = URL.createObjectURL(blob);
                  link.href = url;
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
        <div className="p-4 bg-red-50 border border-red-100 rounded-xl flex items-center gap-3">
          <div className="w-8 h-8 bg-red-100 rounded-lg flex items-center justify-center flex-shrink-0">
            <AlertTriangle className="w-4 h-4 text-red-500" />
          </div>
          <span className="text-sm text-red-600 font-medium">Bazi servisler calismiyor!</span>
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
            <CardContent className="py-8 text-center text-navy-300">
              {healthLoading ? 'Servisler yukleniyor...' : 'Servis bulunamadi'}
            </CardContent>
          </Card>
        )}
      </div>

    </div>
  );
}
