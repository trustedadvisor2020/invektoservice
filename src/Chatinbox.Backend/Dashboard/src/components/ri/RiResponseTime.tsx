import { Clock } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Cell, ReferenceLine } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import type { RiResponseTime as RTData } from '../../lib/api';

interface Props {
  data: RTData | null;
}

const BUCKET_COLORS: Record<string, string> = {
  '0-5m': '#10B981',
  '5-15m': '#34D399',
  '15-60m': '#FBBF24',
  '1-4h': '#F59E0B',
  '4h+': '#EF4444',
  'no_response': '#9CA3AF',
};

export function RiResponseTime({ data }: Props) {
  if (!data || data.totalConversations === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Cevap Süresi Korelasyonu</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Henüz cevap süresi verisi yok</CardContent>
      </Card>
    );
  }

  const chartData = data.buckets.map(b => ({
    name: b.bucketLabel,
    bucket: b.bucket,
    conversations: b.conversationCount,
    conversionRate: Math.round(b.conversionRate * 100 * 10) / 10,
  }));

  const avgMinutes = data.avgResponseTimeMs ? Math.round(data.avgResponseTimeMs / 60_000) : null;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <Clock className="w-4 h-4 text-amber-500" />
          Cevap Süresi Korelasyonu
        </CardTitle>
        <div className="text-xs text-navy-300">
          {avgMinutes != null && <span>Ort: <strong className="text-navy-600">{avgMinutes}dk</strong></span>}
          <span className="ml-3">{data.totalConversations} konuşma</span>
        </div>
      </CardHeader>
      <CardContent>
        <div className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData}>
              <CartesianGrid strokeDasharray="3 3" stroke="#E3E8EE" />
              <XAxis dataKey="name" stroke="#8898AA" fontSize={10} tickLine={false} axisLine={false} />
              <YAxis yAxisId="left" stroke="#8898AA" fontSize={10} tickLine={false} axisLine={false} width={35} />
              <YAxis yAxisId="right" orientation="right" stroke="#8898AA" fontSize={10} tickLine={false} axisLine={false} width={35}
                tickFormatter={v => `%${v}`} />
              <Tooltip
                contentStyle={{ backgroundColor: 'white', border: '1px solid #E3E8EE', borderRadius: '0.5rem', fontSize: '12px' }}
                formatter={(value: number, name: string) =>
                  name === 'conversionRate' ? [`%${value}`, 'Conversion'] : [value, 'Konuşma']
                }
              />
              <Bar yAxisId="left" dataKey="conversations" name="Konuşma" radius={[2, 2, 0, 0]}>
                {chartData.map((entry) => (
                  <Cell key={entry.bucket} fill={BUCKET_COLORS[entry.bucket] ?? '#8898AA'} />
                ))}
              </Bar>
              <ReferenceLine yAxisId="right" y={0} stroke="transparent" />
            </BarChart>
          </ResponsiveContainer>
        </div>
        {/* Conversion rate pills */}
        <div className="flex flex-wrap gap-2 mt-3">
          {chartData.map(d => (
            <span key={d.bucket} className="text-xs px-2 py-0.5 rounded-full bg-navy-50 text-navy-600">
              {d.name}: <strong>%{d.conversionRate}</strong>
            </span>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
