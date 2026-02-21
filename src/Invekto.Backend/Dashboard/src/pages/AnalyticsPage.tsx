import { useState, useEffect, useCallback } from 'react';
import { RefreshCw } from 'lucide-react';
import { api } from '../lib/api';
import type {
  TenantMetricsInfo,
  AutomationSummary,
  DailyMetric,
  IntentMetric,
  WaAnalysisInfo,
  WaSummary,
  WaAgentMetric,
  WaTrend,
  AttributionSummary,
  CostPerLead,
  CampaignStat,
} from '../lib/api';
import { usePolling } from '../hooks/usePolling';
import { MetricCards } from '../components/analytics/MetricCards';
import { DeflectionChart } from '../components/analytics/DeflectionChart';
import { IntentTable } from '../components/analytics/IntentTable';
import { WaTrendsChart } from '../components/analytics/WaTrendsChart';
import { WaAgentTable } from '../components/analytics/WaAgentTable';
import AttributionPanel from '../components/analytics/AttributionPanel';
import CampaignPanel from '../components/analytics/CampaignPanel';
import PlaceholderPanel from '../components/analytics/PlaceholderPanel';
import { Button } from '../components/ui/Button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/Card';

export function AnalyticsPage() {
  // Tenant selection
  const [tenants, setTenants] = useState<TenantMetricsInfo[]>([]);
  const [selectedTenant, setSelectedTenant] = useState<number | null>(null);

  // Date range (default: 7 days)
  const today = new Date().toISOString().split('T')[0];
  const weekAgo = new Date(Date.now() - 7 * 86400000).toISOString().split('T')[0];
  const [fromDate, setFromDate] = useState(weekAgo);
  const [toDate, setToDate] = useState(today);

  // Automation data
  const [summary, setSummary] = useState<AutomationSummary | null>(null);
  const [trends, setTrends] = useState<DailyMetric[]>([]);
  const [intents, setIntents] = useState<IntentMetric[]>([]);

  // WA data
  const [waAnalyses, setWaAnalyses] = useState<WaAnalysisInfo[]>([]);
  const [selectedAnalysis, setSelectedAnalysis] = useState<number | null>(null);
  const [waSummary, setWaSummary] = useState<WaSummary | null>(null);
  const [waAgents, setWaAgents] = useState<WaAgentMetric[]>([]);
  const [waTrends, setWaTrends] = useState<WaTrend[]>([]);

  // GR-3.18: Attribution + Campaign data
  const [attrSummary, setAttrSummary] = useState<AttributionSummary | null>(null);
  const [costPerLead, setCostPerLead] = useState<CostPerLead[]>([]);
  const [campaigns, setCampaigns] = useState<CampaignStat[]>([]);

  const [loading, setLoading] = useState(false);

  // Fetch tenant list
  const { data: tenantData } = usePolling<{ tenants: TenantMetricsInfo[] }>({
    fetcher: () => api.getAnalyticsTenants(),
    interval: 60000,
  });

  useEffect(() => {
    if (tenantData?.tenants) {
      setTenants(tenantData.tenants);
      if (!selectedTenant && tenantData.tenants.length > 0) {
        setSelectedTenant(tenantData.tenants[0].tenant_id);
      }
    }
  }, [tenantData, selectedTenant]);

  // Fetch automation data when tenant or date changes
  const fetchAutomationData = useCallback(async () => {
    if (!selectedTenant) return;
    setLoading(true);
    try {
      const [s, t, i] = await Promise.all([
        api.getAutomationSummary(selectedTenant, fromDate, toDate),
        api.getAutomationTrends(selectedTenant, fromDate, toDate),
        api.getAutomationIntents(selectedTenant, fromDate, toDate),
      ]);
      setSummary(s);
      setTrends(t.trends);
      setIntents(i.intents);
    } catch (err) {
      console.warn('Analytics fetch failed:', err instanceof Error ? err.message : 'Unknown');
    } finally {
      setLoading(false);
    }
  }, [selectedTenant, fromDate, toDate]);

  useEffect(() => {
    fetchAutomationData();
  }, [fetchAutomationData]);

  // Fetch WA analyses when tenant changes
  useEffect(() => {
    if (!selectedTenant) return;
    const tenant = tenants.find(t => t.tenant_id === selectedTenant);
    if (!tenant?.has_wa_data) {
      setWaAnalyses([]);
      setSelectedAnalysis(null);
      return;
    }
    api.getWaAnalyses(selectedTenant)
      .then(data => {
        setWaAnalyses(data.analyses);
        if (data.analyses.length > 0) {
          setSelectedAnalysis(data.analyses[0].analysis_id);
        }
      })
      .catch(err => console.warn('WA analyses fetch failed:', err instanceof Error ? err.message : 'Unknown'));
  }, [selectedTenant, tenants]);

  // Fetch WA detail data when analysis changes
  useEffect(() => {
    if (!selectedTenant || !selectedAnalysis) {
      setWaSummary(null);
      setWaAgents([]);
      setWaTrends([]);
      return;
    }
    Promise.all([
      api.getWaSummary(selectedTenant, selectedAnalysis),
      api.getWaAgents(selectedTenant, selectedAnalysis),
      api.getWaTrends(selectedTenant, selectedAnalysis),
    ])
      .then(([s, a, t]) => {
        setWaSummary(s);
        setWaAgents(a.agents);
        setWaTrends(t.trends);
      })
      .catch(err => console.warn('WA detail fetch failed:', err instanceof Error ? err.message : 'Unknown'));
  }, [selectedTenant, selectedAnalysis]);

  // GR-3.18: Fetch attribution + campaign data (independently error-bounded)
  useEffect(() => {
    if (!selectedTenant) return;
    api.getAttributionSummary(selectedTenant, fromDate, toDate)
      .then(attr => setAttrSummary(attr))
      .catch(err => console.warn('Attribution summary fetch failed:', err instanceof Error ? err.message : 'Unknown'));
    api.getCostPerLead(selectedTenant, fromDate, toDate)
      .then(cpl => setCostPerLead(cpl.cost_per_lead))
      .catch(err => console.warn('Cost-per-lead fetch failed:', err instanceof Error ? err.message : 'Unknown'));
    api.getCampaignStats(selectedTenant)
      .then(camp => setCampaigns(camp.campaigns))
      .catch(err => console.warn('Campaign stats fetch failed:', err instanceof Error ? err.message : 'Unknown'));
  }, [selectedTenant, fromDate, toDate]);

  const currentTenant = tenants.find(t => t.tenant_id === selectedTenant);

  const inputClasses = 'block rounded-lg border border-navy-100 px-3 py-1.5 text-sm focus:outline-none focus:border-brand-500 focus:shadow-focus hover:border-navy-200 transition-all';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-navy-900">Analizler</h1>
          <p className="text-sm text-navy-400 mt-0.5">Otomasyon metrikleri, duygu analizi ve WA analiz raporlari</p>
        </div>
        <Button variant="secondary" size="sm" onClick={fetchAutomationData} disabled={loading}>
          <RefreshCw className={`w-4 h-4 flex-shrink-0 ${loading ? 'animate-spin' : ''}`} />
          <span>Yenile</span>
        </Button>
      </div>

      {/* Filters */}
      <Card>
        <CardContent className="py-4">
          <div className="flex flex-wrap gap-4 items-end">
            <div className="space-y-1.5">
              <label className="text-xs text-navy-300 uppercase tracking-wider font-medium">Tenant</label>
              <select
                className={`${inputClasses} w-48`}
                value={selectedTenant ?? ''}
                onChange={e => setSelectedTenant(Number(e.target.value))}
              >
                {tenants.map(t => (
                  <option key={t.tenant_id} value={t.tenant_id}>
                    {t.tenant_name || `Tenant ${t.tenant_id}`}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-xs text-navy-300 uppercase tracking-wider font-medium">Baslangic</label>
              <input
                type="date"
                className={inputClasses}
                value={fromDate}
                onChange={e => setFromDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <label className="text-xs text-navy-300 uppercase tracking-wider font-medium">Bitis</label>
              <input
                type="date"
                className={inputClasses}
                value={toDate}
                onChange={e => setToDate(e.target.value)}
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Automation Section */}
      {currentTenant?.has_automation_data && summary && (
        <>
          <MetricCards summary={summary} />
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <DeflectionChart trends={trends} />
            <IntentTable intents={intents} />
          </div>
        </>
      )}

      {!currentTenant?.has_automation_data && selectedTenant && (
        <Card>
          <CardContent className="py-8 text-center text-navy-300">
            Bu tenant icin otomasyon verisi bulunamadi.
          </CardContent>
        </Card>
      )}

      {/* WA Section */}
      {currentTenant?.has_wa_data && waAnalyses.length > 0 && (
        <>
          <Card>
            <CardHeader>
              <CardTitle>WhatsApp Analizi</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="flex items-center gap-4">
                <div className="space-y-1.5">
                  <label className="text-xs text-navy-300 uppercase tracking-wider font-medium">Analiz</label>
                  <select
                    className={`${inputClasses} w-72`}
                    value={selectedAnalysis ?? ''}
                    onChange={e => setSelectedAnalysis(Number(e.target.value))}
                  >
                    {waAnalyses.map(a => (
                      <option key={a.analysis_id} value={a.analysis_id}>
                        {a.source_file_name ?? `Analysis #${a.analysis_id}`} ({a.total_conversations.toLocaleString()} konusma)
                      </option>
                    ))}
                  </select>
                </div>
                {waSummary && (
                  <div className="flex gap-6 ml-auto text-sm">
                    <div>
                      <span className="text-navy-300">Mesaj: </span>
                      <span className="font-semibold text-navy-900">{waSummary.total_messages.toLocaleString()}</span>
                    </div>
                    <div>
                      <span className="text-navy-300">Konusma: </span>
                      <span className="font-semibold text-navy-900">{waSummary.total_conversations.toLocaleString()}</span>
                    </div>
                    <div>
                      <span className="text-navy-300">Ort. FRT: </span>
                      <span className="font-semibold text-navy-900">{waSummary.avg_first_response_minutes}dk</span>
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          {waSummary && (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <WaTrendsChart trends={waTrends} />
              <WaAgentTable agents={waAgents} />
            </div>
          )}
        </>
      )}

      {/* GR-3.18: Campaign Stats */}
      {selectedTenant && <CampaignPanel campaigns={campaigns} />}

      {/* GR-3.18: Lead Attribution */}
      {selectedTenant && <AttributionPanel summary={attrSummary} costPerLead={costPerLead} />}

      {/* GR-3.18: Placeholder panels */}
      {selectedTenant && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <PlaceholderPanel title="Iade Takibi" description="Iade surecleri ve istatistikleri bu alanda goruntulenecek." />
          <PlaceholderPanel title="Duygu Analizi" description="AI sentiment node aktif — musteri duygu skorlari ve pozitif/negatif dagilimi biriktikce burada goruntulenecek." />
        </div>
      )}
    </div>
  );
}
