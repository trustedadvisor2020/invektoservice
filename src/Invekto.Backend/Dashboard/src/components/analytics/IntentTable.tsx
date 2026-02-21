import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import type { IntentMetric } from '../../lib/api';

interface IntentTableProps {
  intents: IntentMetric[];
}

export function IntentTable({ intents }: IntentTableProps) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle>Niyet Performansi</CardTitle>
        <span className="text-xs text-navy-300">{intents.length} niyet</span>
      </CardHeader>
      <CardContent>
        {intents.length === 0 ? (
          <div className="py-8 text-center text-navy-300 text-sm">
            Bu donem icin intent verisi bulunamadi.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-navy-100">
                  <th className="text-left py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Niyet</th>
                  <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Toplam</th>
                  <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Insan</th>
                  <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Insan %</th>
                  <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Guven</th>
                  <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Ort. ms</th>
                </tr>
              </thead>
              <tbody>
                {intents.map(intent => {
                  const handoffColor = intent.handoff_rate > 50 ? 'text-red-600 font-semibold' : intent.handoff_rate > 25 ? 'text-yellow-600' : 'text-green-600';
                  return (
                    <tr key={intent.intent} className="border-b border-navy-100/60 hover:bg-navy-50/50 transition-colors">
                      <td className="py-2 px-2 font-medium text-navy-900">{intent.intent}</td>
                      <td className="py-2 px-2 text-right text-navy-600">{intent.total_count.toLocaleString()}</td>
                      <td className="py-2 px-2 text-right text-navy-600">{intent.handoff_count.toLocaleString()}</td>
                      <td className={`py-2 px-2 text-right ${handoffColor}`}>{intent.handoff_rate}%</td>
                      <td className="py-2 px-2 text-right text-navy-600">{intent.avg_confidence.toFixed(2)}</td>
                      <td className="py-2 px-2 text-right text-navy-600">{Math.round(intent.avg_processing_time_ms)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
