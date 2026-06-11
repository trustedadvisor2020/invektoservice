import { AttributionSummary, CostPerLead } from '../../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';

interface Props {
  summary: AttributionSummary | null;
  costPerLead: CostPerLead[];
}

export default function AttributionPanel({ summary, costPerLead }: Props) {
  if (!summary) {
    return (
      <Card>
        <CardHeader><CardTitle>Lead Kaynak Analizi</CardTitle></CardHeader>
        <CardContent><p className="text-sm text-navy-300">Kaynak verisi yükleniyor...</p></CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      {/* Summary cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <MetricCard label="Toplam Lead" value={summary.total_leads} color="bg-brand-50 text-brand-700" />
        <MetricCard label="Dönüşüm" value={summary.converted_leads} color="bg-green-50 text-green-700" />
        <MetricCard label="Dönüşüm Oranı" value={`${summary.conversion_rate}%`} color="bg-purple-50 text-purple-700" />
        <MetricCard label="Toplam Gelir" value={`${summary.total_revenue.toLocaleString()} TRY`} color="bg-amber-50 text-amber-700" />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* By Source */}
        <Card>
          <CardHeader><CardTitle>Kaynak Bazında</CardTitle></CardHeader>
          <CardContent>
            {summary.by_source.length === 0 ? (
              <p className="text-sm text-navy-300">Kaynak verisi yok.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-navy-300 border-b">
                    <th className="pb-2 font-medium">Kaynak</th>
                    <th className="pb-2 font-medium text-right">Lead</th>
                    <th className="pb-2 font-medium text-right">Dönüşüm</th>
                    <th className="pb-2 font-medium text-right">Oran</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.by_source.map(s => (
                    <tr key={s.lead_source} className="border-b last:border-0">
                      <td className="py-1.5 font-medium">{s.lead_source}</td>
                      <td className="py-1.5 text-right">{s.lead_count}</td>
                      <td className="py-1.5 text-right">{s.converted_count}</td>
                      <td className="py-1.5 text-right">{s.conversion_rate}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>

        {/* Cost per Lead */}
        <Card>
          <CardHeader><CardTitle>Maliyet / Lead (Platform)</CardTitle></CardHeader>
          <CardContent>
            {costPerLead.length === 0 ? (
              <p className="text-sm text-navy-300">Maliyet verisi yok.</p>
            ) : (
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-left text-navy-300 border-b">
                    <th className="pb-2 font-medium">Platform</th>
                    <th className="pb-2 font-medium text-right">Harcama</th>
                    <th className="pb-2 font-medium text-right">Lead</th>
                    <th className="pb-2 font-medium text-right">CPL</th>
                  </tr>
                </thead>
                <tbody>
                  {costPerLead.map(c => (
                    <tr key={c.platform} className="border-b last:border-0">
                      <td className="py-1.5 font-medium">{c.platform}</td>
                      <td className="py-1.5 text-right">{c.total_cost.toLocaleString()}</td>
                      <td className="py-1.5 text-right">{c.lead_count}</td>
                      <td className="py-1.5 text-right font-medium">{c.cost_per_lead.toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function MetricCard({ label, value, color }: { label: string; value: string | number; color: string }) {
  return (
    <Card>
      <CardContent className="py-4">
        <p className="text-xs text-navy-300 uppercase tracking-wide">{label}</p>
        <p className={`text-2xl font-bold mt-1 ${color}`}>{value}</p>
      </CardContent>
    </Card>
  );
}
