import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Legend } from 'recharts';
import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import type { WaTrend } from '../../lib/api';

interface WaTrendsChartProps {
  trends: WaTrend[];
}

export function WaTrendsChart({ trends }: WaTrendsChartProps) {
  // Sample down to max 60 data points for readability
  const step = trends.length > 60 ? Math.ceil(trends.length / 60) : 1;
  const chartData = trends
    .filter((_, i) => i % step === 0)
    .map(t => ({
      date: t.date.slice(5), // MM-DD
      conversations: t.conversation_count,
      sales: t.sale_count,
      offered: t.offered_count,
    }));

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle>WA Konusma Trendi</CardTitle>
        <span className="text-xs text-navy-300">{trends.length} gun</span>
      </CardHeader>
      <CardContent>
        {trends.length === 0 ? (
          <div className="h-52 flex items-center justify-center text-navy-300 text-sm">
            Bu analiz icin trend verisi bulunamadi.
          </div>
        ) : (
          <div className="h-52">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={chartData}>
                <CartesianGrid strokeDasharray="3 3" stroke="#E3E8EE" />
                <XAxis
                  dataKey="date"
                  stroke="#8898AA"
                  fontSize={10}
                  tickLine={false}
                  axisLine={false}
                />
                <YAxis
                  stroke="#8898AA"
                  fontSize={10}
                  tickLine={false}
                  axisLine={false}
                  width={40}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: 'white',
                    border: '1px solid #E3E8EE',
                    borderRadius: '0.5rem',
                    boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
                    fontSize: '12px',
                  }}
                />
                <Legend wrapperStyle={{ fontSize: '11px' }} />
                <Bar dataKey="conversations" name="Konusma" fill="#635BFF" radius={[2, 2, 0, 0]} />
                <Bar dataKey="sales" name="Satis" fill="#22c55e" radius={[2, 2, 0, 0]} />
                <Bar dataKey="offered" name="Teklif" fill="#f59e0b" radius={[2, 2, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
