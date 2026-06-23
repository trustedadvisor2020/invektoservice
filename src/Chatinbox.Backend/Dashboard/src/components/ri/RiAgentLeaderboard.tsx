import { Trophy } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import type { RiAgentLeaderboard as AgentData } from '../../lib/api';

interface Props {
  data: AgentData | null;
}

function formatMs(ms: number | null): string {
  if (ms == null) return '—';
  const min = Math.round(ms / 60_000);
  if (min < 60) return `${min}dk`;
  return `${Math.round(min / 60)}sa ${min % 60}dk`;
}

export function RiAgentLeaderboard({ data }: Props) {
  if (!data || data.totalAgents === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Agent Siralamasi</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Henuz agent verisi yok</CardContent>
      </Card>
    );
  }

  const sorted = [...data.agents].sort((a, b) => b.weightedScore - a.weightedScore);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <Trophy className="w-4 h-4 text-amber-500" />
          Agent Siralamasi
        </CardTitle>
        <span className="text-xs text-navy-300">{data.totalAgents} agent</span>
      </CardHeader>
      <CardContent>
        <div className="overflow-x-auto max-h-72 overflow-y-auto">
          <table className="w-full text-sm">
            <thead className="sticky top-0 bg-white">
              <tr className="border-b border-navy-100">
                <th className="text-left py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">#</th>
                <th className="text-left py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Agent</th>
                <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Konusma</th>
                <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Satis</th>
                <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Conv%</th>
                <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Ort. FRT</th>
                <th className="text-right py-2 px-2 text-xs text-navy-300 uppercase tracking-wide font-medium">Skor</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((a, i) => (
                <tr key={a.agentId} className="border-b border-navy-100/60 hover:bg-navy-50/50 transition-colors">
                  <td className="py-2 px-2 text-navy-300">{i + 1}</td>
                  <td className="py-2 px-2 font-medium text-navy-700">{a.agentName}</td>
                  <td className="py-2 px-2 text-right text-navy-600">{a.totalConversations}</td>
                  <td className="py-2 px-2 text-right text-green-600 font-medium">{a.saleCount}</td>
                  <td className="py-2 px-2 text-right">
                    <span className={a.conversionRate >= 20 ? 'text-green-600 font-medium' : 'text-navy-600'}>
                      %{(a.conversionRate * 100).toFixed(1)}
                    </span>
                  </td>
                  <td className="py-2 px-2 text-right text-navy-500">{formatMs(a.avgResponseTimeMs)}</td>
                  <td className="py-2 px-2 text-right">
                    <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-brand-50 text-brand-600">
                      {a.weightedScore.toFixed(1)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}
