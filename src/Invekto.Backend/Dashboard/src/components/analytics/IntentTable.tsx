import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import type { IntentMetric } from '../../lib/api';

interface IntentTableProps {
  intents: IntentMetric[];
}

export function IntentTable({ intents }: IntentTableProps) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle>Intent Performansi</CardTitle>
        <span className="text-xs text-gray-400">{intents.length} intent</span>
      </CardHeader>
      <CardContent>
        {intents.length === 0 ? (
          <div className="py-8 text-center text-gray-400 text-sm">
            Bu donem icin intent verisi bulunamadi.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-gray-200">
                  <th className="text-left py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Intent</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Toplam</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Handoff</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Handoff %</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Confidence</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Ort. ms</th>
                </tr>
              </thead>
              <tbody>
                {intents.map(intent => {
                  const handoffColor = intent.handoff_rate > 50 ? 'text-red-600 font-semibold' : intent.handoff_rate > 25 ? 'text-yellow-600' : 'text-green-600';
                  return (
                    <tr key={intent.intent} className="border-b border-gray-100 hover:bg-gray-50 transition-colors">
                      <td className="py-2 px-2 font-medium text-gray-900">{intent.intent}</td>
                      <td className="py-2 px-2 text-right text-gray-700">{intent.total_count.toLocaleString()}</td>
                      <td className="py-2 px-2 text-right text-gray-700">{intent.handoff_count.toLocaleString()}</td>
                      <td className={`py-2 px-2 text-right ${handoffColor}`}>{intent.handoff_rate}%</td>
                      <td className="py-2 px-2 text-right text-gray-700">{intent.avg_confidence.toFixed(2)}</td>
                      <td className="py-2 px-2 text-right text-gray-700">{Math.round(intent.avg_processing_time_ms)}</td>
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
