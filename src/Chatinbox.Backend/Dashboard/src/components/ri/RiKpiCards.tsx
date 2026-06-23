import { DollarSign, Users, AlertTriangle, Clock } from 'lucide-react';
import type { RiDashboardResponse } from '../../lib/api';

interface Props {
  data: RiDashboardResponse;
}

function KpiCard({ icon: Icon, iconColor, label, value, sub }: {
  icon: React.ComponentType<{ className?: string }>;
  iconColor: string;
  label: string;
  value: string;
  sub?: string;
}) {
  return (
    <div className="bg-white rounded-xl p-4 shadow-card border border-navy-100/50">
      <div className="flex items-center gap-3">
        <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${iconColor}`}>
          <Icon className="w-5 h-5" />
        </div>
        <div>
          <p className="text-xs text-navy-400 font-medium uppercase tracking-wide">{label}</p>
          <p className="text-xl font-bold text-navy-900">{value}</p>
          {sub && <p className="text-xs text-navy-300 mt-0.5">{sub}</p>}
        </div>
      </div>
    </div>
  );
}

export function RiKpiCards({ data }: Props) {
  const revenue = data.revenue;
  const agents = data.agentLeaderboard;
  const rescue = data.rescueAlerts;
  const rt = data.responseTime;

  const totalRevenue = revenue?.totalRevenue ?? 0;
  const agentCount = agents?.totalAgents ?? 0;
  const rescueCount = rescue?.totalCandidates ?? 0;
  const avgFrt = rt?.avgResponseTimeMs ? Math.round(rt.avgResponseTimeMs / 60_000) : null;

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      <KpiCard
        icon={DollarSign}
        iconColor="bg-green-50 text-green-500"
        label="Toplam Gelir"
        value={totalRevenue > 0 ? `€${totalRevenue.toLocaleString('tr-TR', { minimumFractionDigits: 0 })}` : '—'}
        sub={revenue ? `${revenue.totalConversations} konusma` : undefined}
      />
      <KpiCard
        icon={Users}
        iconColor="bg-brand-50 text-brand-500"
        label="Aktif Agent"
        value={agentCount.toString()}
        sub={agents?.agents?.[0] ? `En iyi: ${agents.agents.sort((a, b) => b.weightedScore - a.weightedScore)[0].agentName}` : undefined}
      />
      <KpiCard
        icon={AlertTriangle}
        iconColor="bg-orange-50 text-orange-500"
        label="Rescue Bekleyen"
        value={rescueCount.toString()}
        sub={rescueCount > 0 ? 'Kurtarilabilir konusma' : undefined}
      />
      <KpiCard
        icon={Clock}
        iconColor="bg-amber-50 text-amber-500"
        label="Ort. Cevap Suresi"
        value={avgFrt != null ? `${avgFrt}dk` : '—'}
        sub={rt ? `${rt.totalConversations} konusma` : undefined}
      />
    </div>
  );
}
