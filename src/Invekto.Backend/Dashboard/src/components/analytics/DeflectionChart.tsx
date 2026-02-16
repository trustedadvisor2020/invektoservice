import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import type { DailyMetric } from '../../lib/api';

interface DeflectionChartProps {
  trends: DailyMetric[];
}

export function DeflectionChart({ trends }: DeflectionChartProps) {
  const chartData = trends.map(t => ({
    date: t.date.slice(5), // MM-DD format
    deflection_rate: t.deflection_rate,
    total: t.total_replies,
    deflected: t.deflected_count,
    handoff: t.handoff_count,
  }));

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle>Deflection Trendi</CardTitle>
        {trends.length > 0 && (
          <span className="text-xs text-gray-400">{trends.length} gun</span>
        )}
      </CardHeader>
      <CardContent>
        {trends.length === 0 ? (
          <div className="h-52 flex items-center justify-center text-gray-400 text-sm">
            Bu donem icin veri bulunamadi.
          </div>
        ) : (
          <div className="h-52">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={chartData}>
                <defs>
                  <linearGradient id="deflectionGrad" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#22c55e" stopOpacity={0.3} />
                    <stop offset="95%" stopColor="#22c55e" stopOpacity={0.05} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                <XAxis
                  dataKey="date"
                  stroke="#9ca3af"
                  fontSize={10}
                  tickLine={false}
                  axisLine={false}
                />
                <YAxis
                  stroke="#9ca3af"
                  fontSize={10}
                  tickLine={false}
                  axisLine={false}
                  domain={[0, 100]}
                  width={35}
                  tickFormatter={(v: number) => `${v}%`}
                />
                <Tooltip
                  contentStyle={{
                    backgroundColor: 'white',
                    border: '1px solid #e5e7eb',
                    borderRadius: '0.5rem',
                    boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
                    fontSize: '12px',
                  }}
                  formatter={(value: number, name: string) => {
                    const labels: Record<string, string> = {
                      deflection_rate: 'Deflection Rate',
                      total: 'Toplam',
                      deflected: 'Deflected',
                      handoff: 'Handoff',
                    };
                    return [name === 'deflection_rate' ? `${value}%` : value, labels[name] ?? name];
                  }}
                />
                <Area
                  type="monotone"
                  dataKey="deflection_rate"
                  stroke="#22c55e"
                  fill="url(#deflectionGrad)"
                  strokeWidth={2}
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
