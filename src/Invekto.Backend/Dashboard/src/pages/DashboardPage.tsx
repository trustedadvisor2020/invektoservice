import { useState, useCallback } from 'react';
import { RefreshCw, AlertTriangle, Download } from 'lucide-react';
import { api, type HealthResponse, type ErrorStatsResponse, type ServiceHealth } from '../lib/api';
import { usePolling } from '../hooks/usePolling';
import { HealthCard } from '../components/HealthCard';
import { ErrorTimeline } from '../components/ErrorTimeline';
import { DependencyMap } from '../components/DependencyMap';
import { TestPanel } from '../components/TestPanel';
import { Button } from '../components/ui/Button';
import { Card, CardContent } from '../components/ui/Card';

export function DashboardPage() {
  const [restartingService, setRestartingService] = useState<string | null>(null);

  // Fetch health data every 30 seconds (reduced from 10s to prevent connection exhaustion)
  const { data: healthData, isLoading: healthLoading, refresh: refreshHealth } = usePolling<HealthResponse>({
    fetcher: () => api.getHealth(),
    interval: 30000,
  });

  // Fetch error stats every 60 seconds (reduced from 30s)
  const { data: errorStats } = usePolling<ErrorStatsResponse>({
    fetcher: () => api.getErrorStats(24),
    interval: 60000,
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
        alert(`Restart başarısız: ${result.message}`);
      }
    } catch (error) {
      alert(`Restart hatası: ${error instanceof Error ? error.message : 'Unknown error'}`);
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
          <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-0.5">Servis durumu ve metrikler</p>
        </div>
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              const link = document.createElement('a');
              link.href = '/api/ops/postman';
              link.download = 'InvektoServis.postman_collection.json';
              // Add basic auth header via fetch, then trigger download
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
        <div className="p-4 bg-red-50 border border-red-200 rounded-xl flex items-center gap-3 shadow-sm">
          <div className="w-8 h-8 bg-red-100 rounded-lg flex items-center justify-center flex-shrink-0">
            <AlertTriangle className="w-4 h-4 text-red-600" />
          </div>
          <span className="text-sm text-red-700 font-medium">Bazı servisler çalışmıyor!</span>
        </div>
      )}

      {/* Health Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
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
            <CardContent className="py-8 text-center text-gray-500">
              {healthLoading ? 'Loading services...' : 'No services found'}
            </CardContent>
          </Card>
        )}
      </div>

      {/* Charts Row */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Error Timeline */}
        {errorStats && (
          <ErrorTimeline
            buckets={errorStats.buckets}
            total={errorStats.total}
            onBucketClick={(hour) => {
              window.location.href = `/logs?after=${hour}`;
            }}
          />
        )}

        {/* Dependency Map */}
        <DependencyMap services={services} />
      </div>

      {/* Test Panel */}
      <TestPanel />

      {/* System Info */}
      {healthData?.info && (
        <Card>
          <CardContent className="py-4">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
              <div className="space-y-1">
                <span className="text-xs text-gray-500 uppercase tracking-wide">Stage</span>
                <p className="text-sm font-semibold text-gray-900">{healthData.info.stage}</p>
              </div>
              <div className="space-y-1">
                <span className="text-xs text-gray-500 uppercase tracking-wide">Timeout</span>
                <p className="text-sm font-semibold text-gray-900">{healthData.info.timeout_ms}ms</p>
              </div>
              <div className="space-y-1">
                <span className="text-xs text-gray-500 uppercase tracking-wide">Retry Count</span>
                <p className="text-sm font-semibold text-gray-900">{healthData.info.retry_count}</p>
              </div>
              <div className="space-y-1">
                <span className="text-xs text-gray-500 uppercase tracking-wide">Slow Threshold</span>
                <p className="text-sm font-semibold text-gray-900">{healthData.info.slow_threshold_ms}ms</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
