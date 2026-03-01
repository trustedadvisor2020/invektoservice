import { Bell, AlertCircle } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import { Badge } from '../ui/Badge';
import type { RiRescueInsight } from '../../lib/api';

interface Props {
  data: RiRescueInsight | null;
}

const LABEL_COLORS: Record<string, string> = {
  offered: 'bg-blue-100 text-blue-700',
  offer_lost: 'bg-red-100 text-red-700',
  no_response: 'bg-amber-100 text-amber-700',
};

export function RiRescueAlerts({ data }: Props) {
  if (!data || data.totalCandidates === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Rescue Alarmlari</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Kurtarilabilecek konusma bulunamadi</CardContent>
      </Card>
    );
  }

  const topCandidates = data.candidates.slice(0, 8);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <Bell className="w-4 h-4 text-orange-500" />
          Rescue Alarmlari
        </CardTitle>
        <Badge variant="warning">{data.totalCandidates} aday</Badge>
      </CardHeader>
      <CardContent>
        <div className="space-y-2 max-h-72 overflow-y-auto">
          {topCandidates.map(c => (
            <div
              key={c.conversationId}
              className="flex items-center justify-between p-2 rounded-lg bg-navy-50/60 hover:bg-navy-50 transition-colors"
            >
              <div className="flex items-center gap-2 min-w-0">
                <AlertCircle className="w-3.5 h-3.5 text-orange-400 flex-shrink-0" />
                <span className="text-sm text-navy-700 truncate">{c.conversationId.slice(0, 20)}...</span>
                <span className={`text-xs px-1.5 py-0.5 rounded-full font-medium ${LABEL_COLORS[c.outcomeLabel] ?? 'bg-navy-100 text-navy-600'}`}>
                  {c.outcomeLabel}
                </span>
              </div>
              <div className="flex items-center gap-3 text-xs text-navy-400 flex-shrink-0">
                <span>{c.daysSince} gun once</span>
                <span className="font-medium text-brand-600">skor: {c.rescuePriorityScore.toFixed(0)}</span>
              </div>
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
