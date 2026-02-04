import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import type { ErrorStatsBucket } from '../lib/api';
import { Card, CardHeader, CardTitle, CardContent } from './ui/Card';

interface ErrorTimelineProps {
  buckets: ErrorStatsBucket[];
  total: number;
  onBucketClick?: (hour: string) => void;
}

export function ErrorTimeline({ buckets, total, onBucketClick }: ErrorTimelineProps) {
  const chartData = buckets.map(b => ({
    hour: new Date(b.hour).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
    count: b.count,
    fullHour: b.hour,
  }));

  const handleClick = (data: { activePayload?: Array<{ payload: { fullHour: string } }> }) => {
    if (data.activePayload?.[0]?.payload?.fullHour && onBucketClick) {
      onBucketClick(data.activePayload[0].payload.fullHour);
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle>Error Timeline (24h)</CardTitle>
        <div className="flex items-center gap-2">
          <span className="text-xs text-gray-500">Total</span>
          <span className="text-xl font-bold text-red-600">{total}</span>
        </div>
      </CardHeader>
      <CardContent>
        <div className="h-44">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={chartData} onClick={handleClick}>
              <defs>
                <linearGradient id="errorGradient" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3} />
                  <stop offset="95%" stopColor="#ef4444" stopOpacity={0.05} />
                </linearGradient>
              </defs>
              <XAxis
                dataKey="hour"
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
                allowDecimals={false}
                width={30}
              />
              <Tooltip
                contentStyle={{
                  backgroundColor: 'white',
                  border: '1px solid #e5e7eb',
                  borderRadius: '0.5rem',
                  boxShadow: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
                  color: '#111827',
                  fontSize: '12px',
                }}
                labelStyle={{ color: '#6b7280', marginBottom: '4px' }}
              />
              <Area
                type="monotone"
                dataKey="count"
                stroke="#ef4444"
                fill="url(#errorGradient)"
                strokeWidth={2}
                style={{ cursor: 'pointer' }}
              />
            </AreaChart>
          </ResponsiveContainer>
        </div>
        <p className="text-xs text-gray-400 mt-3 text-center">
          Click on a point to filter logs for that hour
        </p>
      </CardContent>
    </Card>
  );
}
