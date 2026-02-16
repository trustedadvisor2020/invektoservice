import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card';
import type { WaAgentMetric } from '../../lib/api';

interface WaAgentTableProps {
  agents: WaAgentMetric[];
}

export function WaAgentTable({ agents }: WaAgentTableProps) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle>Agent Performansi</CardTitle>
        <span className="text-xs text-gray-400">{agents.length} agent</span>
      </CardHeader>
      <CardContent>
        {agents.length === 0 ? (
          <div className="py-8 text-center text-gray-400 text-sm">
            Bu analiz icin agent verisi bulunamadi.
          </div>
        ) : (
          <div className="overflow-x-auto max-h-72 overflow-y-auto">
            <table className="w-full text-sm">
              <thead className="sticky top-0 bg-white">
                <tr className="border-b border-gray-200">
                  <th className="text-left py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Agent</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Konusma</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Satis</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Teklif</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Conv %</th>
                  <th className="text-right py-2 px-2 text-xs text-gray-500 uppercase tracking-wide font-medium">Ort. FRT</th>
                </tr>
              </thead>
              <tbody>
                {agents.map(agent => {
                  const convColor = agent.conversion_rate >= 20 ? 'text-green-600 font-semibold' : agent.conversion_rate >= 10 ? 'text-yellow-600' : 'text-red-600';
                  return (
                    <tr key={agent.agent_name} className="border-b border-gray-100 hover:bg-gray-50 transition-colors">
                      <td className="py-2 px-2 font-medium text-gray-900">{agent.agent_name}</td>
                      <td className="py-2 px-2 text-right text-gray-700">{agent.total_conversations.toLocaleString()}</td>
                      <td className="py-2 px-2 text-right text-gray-700">{agent.sale_count.toLocaleString()}</td>
                      <td className="py-2 px-2 text-right text-gray-700">{agent.offered_count.toLocaleString()}</td>
                      <td className={`py-2 px-2 text-right ${convColor}`}>{agent.conversion_rate}%</td>
                      <td className="py-2 px-2 text-right text-gray-700">{agent.avg_first_response_minutes}dk</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
