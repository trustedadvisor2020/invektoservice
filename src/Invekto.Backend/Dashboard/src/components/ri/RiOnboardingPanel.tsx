import { useState, useEffect } from 'react';
import {
  Rocket, CheckCircle2, Circle, ArrowRight, Sparkles, BarChart3,
  Clock, Users, Star, TrendingUp,
} from 'lucide-react';
import { api } from '../../lib/api';
import type { RiOnboardingResponse } from '../../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';

interface Props {
  tenantId: number;
  sector: string | null;
}

export function RiOnboardingPanel({ tenantId, sector }: Props) {
  const [data, setData] = useState<RiOnboardingResponse | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!tenantId || !sector) return;
    setLoading(true);
    api.getRiOnboarding(tenantId, sector)
      .then(r => setData(r))
      .catch(err => console.warn('RI onboarding load failed:', err instanceof Error ? err.message : 'Unknown'))
      .finally(() => setLoading(false));
  }, [tenantId, sector]);

  if (!sector) return null;
  if (loading && !data) return null;
  if (!data) return null;

  const hasChecklist = data.checklist.length > 0;
  const hasQuickStart = data.quickStart.length > 0;
  const hasOverview = data.overview != null && data.overview.totalTemplates > 0;
  const hasComparison = data.comparison != null;

  // Nothing to show
  if (!hasChecklist && !hasQuickStart && !hasOverview && !hasComparison) return null;

  return (
    <div className="space-y-4">
      {/* Sector welcome banner */}
      <Card>
        <div className="bg-gradient-to-r from-brand-500/10 via-purple-500/5 to-transparent p-4 rounded-xl">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-brand-500/20 flex items-center justify-center shrink-0">
              <Rocket className="w-5 h-5 text-brand-500" />
            </div>
            <div className="flex-1 min-w-0">
              <h3 className="text-sm font-semibold text-navy-900">
                {data.sectorDisplayName} Sektörü Rehberi
              </h3>
              <p className="text-xs text-navy-400 mt-0.5">
                Sektörünüze özel onboarding adımları ve hızlı başlangıç önerileri
              </p>
            </div>
            {hasOverview && (
              <div className="text-right shrink-0">
                <p className="text-lg font-bold text-brand-600">{data.overview!.totalTemplates}</p>
                <p className="text-[10px] text-navy-400">Hazır Şablon</p>
              </div>
            )}
          </div>
        </div>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Onboarding Checklist */}
        {hasChecklist && (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-sm">
                <CheckCircle2 className="w-4 h-4 text-emerald-500" />
                İlk 7 Gün Planı
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {data.checklist.map(item => (
                  <div key={item.stepNumber} className="flex gap-3">
                    <div className="shrink-0 mt-0.5">
                      {item.isCompleted ? (
                        <CheckCircle2 className="w-4 h-4 text-emerald-500" />
                      ) : (
                        <Circle className="w-4 h-4 text-navy-300" />
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <span className={`text-sm font-medium ${item.isCompleted ? 'text-navy-400 line-through' : 'text-navy-700'}`}>
                          {item.description}
                        </span>
                      </div>
                      <div className="flex items-center gap-2 mt-0.5">
                        {item.dayRange && (
                          <span className="text-[10px] px-1.5 py-0.5 rounded bg-navy-50 text-navy-400">
                            {item.dayRange}
                          </span>
                        )}
                        {item.expectedImpact && (
                          <span className="text-[10px] text-navy-300">
                            {item.expectedImpact}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        )}

        {/* Quick Start Recommendations */}
        {hasQuickStart && (
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center gap-2 text-sm">
                <Sparkles className="w-4 h-4 text-amber-500" />
                Hızlı Başlangıç
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {data.quickStart.map((item, idx) => (
                  <div key={idx} className="p-3 rounded-lg bg-navy-50/50 border border-navy-100">
                    <div className="flex items-start gap-2">
                      <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded shrink-0 ${
                        item.type === 'flow' ? 'bg-blue-100 text-blue-700' :
                        item.type === 'intent' ? 'bg-purple-100 text-purple-700' :
                        'bg-emerald-100 text-emerald-700'
                      }`}>
                        {item.type === 'flow' ? 'Akış' : item.type === 'intent' ? 'Niyet' : 'SSS'}
                      </span>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-navy-700 truncate">{item.title}</p>
                        {item.description && (
                          <p className="text-xs text-navy-400 mt-0.5 line-clamp-2">{item.description}</p>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center justify-between mt-2">
                      {item.impactLabel && (
                        <span className="text-[10px] font-medium text-emerald-600">
                          <TrendingUp className="w-3 h-3 inline mr-0.5" />
                          {item.impactLabel}
                        </span>
                      )}
                      {item.actionUrl && (
                        <a href={item.actionUrl.replace('/app', '')} className="text-[10px] text-brand-500 hover:text-brand-600 flex items-center gap-0.5">
                          Başlat <ArrowRight className="w-3 h-3" />
                        </a>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      {/* Benchmark Comparison */}
      {hasComparison && data.comparison && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm">
              <BarChart3 className="w-4 h-4 text-brand-500" />
              Performans Durumu
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              {data.comparison.tenantAvgResponseMin != null && (
                <ComparisonStat
                  icon={<Clock className="w-4 h-4 text-amber-500" />}
                  label="Ort. Cevap Süresi"
                  value={`${data.comparison.tenantAvgResponseMin.toFixed(0)}dk`}
                />
              )}
              {data.comparison.tenantConversionRate != null && (
                <ComparisonStat
                  icon={<TrendingUp className="w-4 h-4 text-emerald-500" />}
                  label="Dönüşüm Oranı"
                  value={`%${(data.comparison.tenantConversionRate * 100).toFixed(1)}`}
                />
              )}
              {data.comparison.tenantActiveAgents != null && (
                <ComparisonStat
                  icon={<Users className="w-4 h-4 text-blue-500" />}
                  label="Aktif Agent"
                  value={`${data.comparison.tenantActiveAgents}`}
                />
              )}
              {data.comparison.tenantAvgQualityScore != null && (
                <ComparisonStat
                  icon={<Star className="w-4 h-4 text-purple-500" />}
                  label="Kalite Puani"
                  value={`${data.comparison.tenantAvgQualityScore.toFixed(1)}`}
                />
              )}
            </div>
            {data.comparison.recommendation && (
              <div className="mt-3 p-3 rounded-lg bg-amber-50 border border-amber-200/50 text-xs text-amber-800">
                <Sparkles className="w-3.5 h-3.5 inline mr-1.5 text-amber-500" />
                {data.comparison.recommendation}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Sector Overview (template counts) */}
      {hasOverview && data.overview && (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm">
              <BarChart3 className="w-4 h-4 text-navy-500" />
              Sektör Şablon Durumu
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-3 sm:grid-cols-5 gap-3 text-center">
              <OverviewStat label="Intent" count={data.overview.intentCount} />
              <OverviewStat label="SSS" count={data.overview.faqCount} />
              <OverviewStat label="Akış" count={data.overview.flowCount} />
              <OverviewStat label="İtiraz" count={data.overview.objectionCount} />
              <OverviewStat label="Takip" count={data.overview.followupCount} />
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

function ComparisonStat({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="text-center">
      <div className="flex justify-center mb-1">{icon}</div>
      <p className="text-lg font-bold text-navy-900">{value}</p>
      <p className="text-[10px] text-navy-400">{label}</p>
    </div>
  );
}

function OverviewStat({ label, count }: { label: string; count: number }) {
  return (
    <div>
      <p className="text-xl font-bold text-navy-700">{count}</p>
      <p className="text-[10px] text-navy-400">{label}</p>
    </div>
  );
}
