import { DollarSign, TrendingDown, ArrowRight } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import type { RiRevenueAttribution } from '../../lib/api';

interface Props {
  data: RiRevenueAttribution | null;
}

export function RiRevenueCard({ data }: Props) {
  if (!data || data.totalConversations === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Kayip Gelir</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Henuz gelir verisi yok</CardContent>
      </Card>
    );
  }

  const lostEntries = data.entries.filter(e => e.dimension === 'outcome' && e.dimensionKey !== 'sale');
  const lostRevenue = lostEntries.reduce((sum, e) => sum + e.attributedRevenue, 0);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <DollarSign className="w-4 h-4 text-red-400" />
          Kayip Gelir
        </CardTitle>
        <span className="text-xs text-navy-300">{data.totalConversations} konusma</span>
      </CardHeader>
      <CardContent>
        <div className="text-3xl font-bold text-red-500 mb-3">
          {lostRevenue > 0 ? `€${lostRevenue.toLocaleString('tr-TR', { minimumFractionDigits: 0 })}` : '—'}
        </div>
        <div className="space-y-2">
          {data.entries.slice(0, 5).map(e => (
            <div key={`${e.dimension}-${e.dimensionKey}`} className="flex items-center justify-between text-sm">
              <div className="flex items-center gap-2">
                {e.dimensionKey === 'sale' ? (
                  <ArrowRight className="w-3 h-3 text-green-500" />
                ) : (
                  <TrendingDown className="w-3 h-3 text-red-400" />
                )}
                <span className="text-navy-600">{e.dimensionLabel ?? e.dimensionKey}</span>
              </div>
              <span className="font-medium text-navy-800">
                €{e.attributedRevenue.toLocaleString('tr-TR', { minimumFractionDigits: 0 })}
                <span className="text-navy-300 text-xs ml-1">({e.totalConversations})</span>
              </span>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
