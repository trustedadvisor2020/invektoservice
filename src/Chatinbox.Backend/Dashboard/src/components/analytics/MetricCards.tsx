import { TrendingUp, TrendingDown, MessageSquare, Clock } from 'lucide-react';
import { Card, CardContent } from '../ui/Card';
import type { AutomationSummary } from '../../lib/api';

interface MetricCardsProps {
  summary: AutomationSummary;
}

export function MetricCards({ summary }: MetricCardsProps) {
  const deflectionColor = summary.deflection_rate >= 80 ? 'text-emerald-600' : summary.deflection_rate >= 50 ? 'text-amber-600' : 'text-red-500';
  const deflectionBg = summary.deflection_rate >= 80 ? 'bg-emerald-50' : summary.deflection_rate >= 50 ? 'bg-amber-50' : 'bg-red-50';

  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
      {/* Deflection Rate */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center gap-3">
            <div className={`w-10 h-10 ${deflectionBg} rounded-lg flex items-center justify-center flex-shrink-0`}>
              <TrendingUp className={`w-5 h-5 ${deflectionColor}`} />
            </div>
            <div>
              <p className="text-xs text-navy-300 uppercase tracking-wide">Oto. Cozum</p>
              <p className={`text-2xl font-bold ${deflectionColor}`}>{summary.deflection_rate}%</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Handoff Rate */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-orange-50 rounded-lg flex items-center justify-center flex-shrink-0">
              <TrendingDown className="w-5 h-5 text-orange-600" />
            </div>
            <div>
              <p className="text-xs text-navy-300 uppercase tracking-wide">Insan Destek</p>
              <p className="text-2xl font-bold text-orange-600">{summary.handoff_rate}%</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Total Replies */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-brand-50 rounded-lg flex items-center justify-center flex-shrink-0">
              <MessageSquare className="w-5 h-5 text-brand-500" />
            </div>
            <div>
              <p className="text-xs text-navy-300 uppercase tracking-wide">Toplam</p>
              <p className="text-2xl font-bold text-navy-900">{summary.total_replies.toLocaleString()}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Avg Processing Time */}
      <Card>
        <CardContent className="py-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 bg-purple-50 rounded-lg flex items-center justify-center flex-shrink-0">
              <Clock className="w-5 h-5 text-purple-600" />
            </div>
            <div>
              <p className="text-xs text-navy-300 uppercase tracking-wide">Ort. Sure</p>
              <p className="text-2xl font-bold text-navy-900">{Math.round(summary.avg_processing_time_ms)}ms</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
