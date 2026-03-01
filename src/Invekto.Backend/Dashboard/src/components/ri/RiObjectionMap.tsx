import { PieChart, AlertTriangle } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import type { RiObjectionMap as ObjectionData } from '../../lib/api';

interface Props {
  data: ObjectionData | null;
}

const COLORS = [
  'bg-red-400', 'bg-amber-400', 'bg-blue-400', 'bg-purple-400', 'bg-emerald-400',
  'bg-pink-400', 'bg-cyan-400', 'bg-orange-400', 'bg-indigo-400', 'bg-lime-400',
];

export function RiObjectionMap({ data }: Props) {
  if (!data || data.totalObjections === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Itiraz Haritasi</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Henuz itiraz verisi yok</CardContent>
      </Card>
    );
  }

  const sorted = [...data.objectionTypes].sort((a, b) => b.count - a.count);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <PieChart className="w-4 h-4 text-red-400" />
          Itiraz Haritasi
        </CardTitle>
        <span className="text-xs text-navy-300">{data.totalObjections} itiraz</span>
      </CardHeader>
      <CardContent>
        {/* Horizontal bar chart */}
        <div className="space-y-3">
          {sorted.map((o, i) => (
            <div key={o.objectionType}>
              <div className="flex justify-between text-sm mb-1">
                <div className="flex items-center gap-1.5">
                  <AlertTriangle className="w-3 h-3 text-navy-300" />
                  <span className="text-navy-700">{o.objectionLabel}</span>
                </div>
                <span className="text-navy-500 font-medium">{o.count} <span className="text-navy-300">(%{(o.percentage * 100).toFixed(0)})</span></span>
              </div>
              <div className="h-2 bg-navy-100 rounded-full overflow-hidden">
                <div
                  className={`h-full rounded-full ${COLORS[i % COLORS.length]} transition-all duration-500`}
                  style={{ width: `${Math.max(o.percentage * 100, 2)}%` }}
                />
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
