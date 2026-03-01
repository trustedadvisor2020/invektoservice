import { useState, useEffect, useCallback } from 'react';
import { RefreshCw, TrendingUp } from 'lucide-react';
import { api } from '../lib/api';
import type { RiDashboardResponse } from '../lib/api';
import { useAuth } from '../hooks/useAuth';
import { Button } from '../components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card';
import { RiKpiCards } from '../components/ri/RiKpiCards';
import { RiRevenueCard } from '../components/ri/RiRevenueCard';
import { RiAgentLeaderboard } from '../components/ri/RiAgentLeaderboard';
import { RiObjectionMap } from '../components/ri/RiObjectionMap';
import { RiResponseTime } from '../components/ri/RiResponseTime';
import { RiRescueAlerts } from '../components/ri/RiRescueAlerts';
import { RiQualityScore } from '../components/ri/RiQualityScore';
import { RiDemandHeatmap } from '../components/ri/RiDemandHeatmap';
import { RiOnboardingPanel } from '../components/ri/RiOnboardingPanel';

export function RevenueIntelligencePage() {
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 0;

  const [data, setData] = useState<RiDashboardResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Sector: auto-detect from settings
  const [sector, setSector] = useState<string | null>(null);

  // Load sector setting
  useEffect(() => {
    if (!tenantId) return;
    api.getSector()
      .then(r => { if (r.sector) setSector(r.sector); })
      .catch(err => console.warn('Sector load failed:', err instanceof Error ? err.message : 'Unknown'));
  }, [tenantId]);

  const fetchDashboard = useCallback(async () => {
    if (!tenantId) return;
    setLoading(true);
    setError(null);
    try {
      const result = await api.getRiDashboard(tenantId, sector ?? undefined);
      setData(result);
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Bilinmeyen hata';
      setError(msg);
      console.warn('RI dashboard fetch failed:', msg);
    } finally {
      setLoading(false);
    }
  }, [tenantId, sector]);

  useEffect(() => {
    fetchDashboard();
  }, [fetchDashboard]);

  if (!tenantId) {
    return (
      <div className="space-y-6">
        <h1 className="text-xl font-semibold text-navy-900">Revenue Intelligence</h1>
        <Card>
          <CardContent className="py-8 text-center text-navy-300">
            Bu sayfa tenant olarak giris yaptiginizda kullanilabilir.
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-navy-900 flex items-center gap-2">
            <TrendingUp className="w-5 h-5 text-brand-500" />
            Revenue Intelligence
          </h1>
          <p className="text-sm text-navy-400 mt-0.5">
            Gelir, agent performansi, itirazlar ve talep analizi
            {sector && <span className="ml-1 text-brand-500 font-medium">({sector})</span>}
          </p>
        </div>
        <Button variant="secondary" size="sm" onClick={fetchDashboard} disabled={loading}>
          <RefreshCw className={`w-4 h-4 flex-shrink-0 ${loading ? 'animate-spin' : ''}`} />
          <span>Yenile</span>
        </Button>
      </div>

      {/* Error */}
      {error && (
        <Card>
          <CardContent className="py-4 text-center text-red-500 text-sm">
            Veri yuklenirken hata: {error}
          </CardContent>
        </Card>
      )}

      {/* Loading state */}
      {loading && !data && (
        <Card>
          <CardContent className="py-8 text-center text-navy-300">
            <RefreshCw className="w-5 h-5 animate-spin mx-auto mb-2" />
            Veriler yukleniyor...
          </CardContent>
        </Card>
      )}

      {/* Dashboard content */}
      {data && (
        <>
          {/* KPI Cards */}
          <RiKpiCards data={data} />

          {/* Onboarding Panel (sector guide + checklist + quick start) */}
          <RiOnboardingPanel tenantId={tenantId} sector={sector} />

          {/* Row 1: Revenue + Agent Leaderboard */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <RiRevenueCard data={data.revenue} />
            <RiAgentLeaderboard data={data.agentLeaderboard} />
          </div>

          {/* Row 2: Objection Map + Response Time */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <RiObjectionMap data={data.objectionMap} />
            <RiResponseTime data={data.responseTime} />
          </div>

          {/* Row 3: Rescue Alerts + Quality Score */}
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <RiRescueAlerts data={data.rescueAlerts} />
            <RiQualityScore data={data.qualityScore} />
          </div>

          {/* Row 4: Demand Heatmap (full width) */}
          <RiDemandHeatmap data={data.demandHeatmap} />

          {/* Benchmarks */}
          {data.benchmarks && (
            <Card>
              <CardHeader>
                <CardTitle>Sektor Benchmark: {data.benchmarks.displayName}</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-center">
                  <div>
                    <p className="text-2xl font-bold text-navy-900">{data.benchmarks.totalTemplates}</p>
                    <p className="text-xs text-navy-400">Toplam Sablon</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-brand-600">{data.benchmarks.intentCount}</p>
                    <p className="text-xs text-navy-400">Intent</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-green-600">{data.benchmarks.faqCount}</p>
                    <p className="text-xs text-navy-400">FAQ</p>
                  </div>
                  <div>
                    <p className="text-2xl font-bold text-purple-600">{data.benchmarks.flowCount}</p>
                    <p className="text-xs text-navy-400">Flow</p>
                  </div>
                </div>
              </CardContent>
            </Card>
          )}

          {/* No data message */}
          {!data.revenue && !data.agentLeaderboard && !data.responseTime && (
            <Card>
              <CardContent className="py-8 text-center text-navy-300 text-sm">
                Henuz insight verisi hesaplanmamis. Nightly batch islendikten sonra veriler burada gorunecek.
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}
