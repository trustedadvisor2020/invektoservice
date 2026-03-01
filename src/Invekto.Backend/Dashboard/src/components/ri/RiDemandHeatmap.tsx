import { Flame } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import type { RiDemandHeatmap as HeatmapData } from '../../lib/api';

interface Props {
  data: HeatmapData | null;
}

const HOURS = Array.from({ length: 24 }, (_, i) => i);
const DAYS = ['Pzt', 'Sal', 'Car', 'Per', 'Cum', 'Cmt', 'Paz'];

function getHeatColor(value: number, max: number): string {
  if (max === 0 || value === 0) return 'bg-navy-50';
  const ratio = value / max;
  if (ratio > 0.75) return 'bg-red-400';
  if (ratio > 0.5) return 'bg-orange-300';
  if (ratio > 0.25) return 'bg-amber-200';
  if (ratio > 0) return 'bg-emerald-100';
  return 'bg-navy-50';
}

export function RiDemandHeatmap({ data }: Props) {
  if (!data || data.totalConversations === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Talep Haritasi</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Henuz talep verisi yok</CardContent>
      </Card>
    );
  }

  // Build lookup: [day][hour] -> cell
  const cellMap = new Map<string, typeof data.cells[0]>();
  let maxConv = 0;
  for (const c of data.cells) {
    cellMap.set(`${c.dayOfWeek}-${c.hourOfDay}`, c);
    if (c.totalConversations > maxConv) maxConv = c.totalConversations;
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <Flame className="w-4 h-4 text-orange-500" />
          Talep Haritasi
        </CardTitle>
        <span className="text-xs text-navy-300">{data.totalConversations} konusma</span>
      </CardHeader>
      <CardContent>
        <div className="overflow-x-auto">
          <div className="min-w-[600px]">
            {/* Hour headers */}
            <div className="flex items-center mb-1">
              <div className="w-10 flex-shrink-0" />
              {HOURS.filter(h => h % 2 === 0).map(h => (
                <div key={h} className="flex-1 text-center text-[10px] text-navy-300" style={{ minWidth: '24px' }}>
                  {String(h).padStart(2, '0')}
                </div>
              ))}
            </div>
            {/* Rows */}
            {DAYS.map((dayLabel, dayIdx) => (
              <div key={dayIdx} className="flex items-center gap-0.5 mb-0.5">
                <div className="w-10 flex-shrink-0 text-[10px] text-navy-400 text-right pr-1.5">{dayLabel}</div>
                {HOURS.map(h => {
                  const cell = cellMap.get(`${dayIdx}-${h}`);
                  const count = cell?.totalConversations ?? 0;
                  return (
                    <div
                      key={h}
                      className={`flex-1 h-5 rounded-sm ${getHeatColor(count, maxConv)} transition-colors cursor-default`}
                      style={{ minWidth: '20px' }}
                      title={`${dayLabel} ${String(h).padStart(2, '0')}:00 — ${count} konusma${cell ? `, conv: %${(cell.conversionRate * 100).toFixed(0)}` : ''}`}
                    />
                  );
                })}
              </div>
            ))}
            {/* Legend */}
            <div className="flex items-center justify-end gap-2 mt-2">
              <span className="text-[10px] text-navy-300">Az</span>
              <div className="flex gap-0.5">
                {['bg-navy-50', 'bg-emerald-100', 'bg-amber-200', 'bg-orange-300', 'bg-red-400'].map(c => (
                  <div key={c} className={`w-4 h-3 rounded-sm ${c}`} />
                ))}
              </div>
              <span className="text-[10px] text-navy-300">Cok</span>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
