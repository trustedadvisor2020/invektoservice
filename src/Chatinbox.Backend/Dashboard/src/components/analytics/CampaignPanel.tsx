import { CampaignStat } from '../../lib/api';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';

interface Props {
  campaigns: CampaignStat[];
}

const statusColors: Record<string, string> = {
  active: 'bg-green-100 text-green-800',
  completed: 'bg-brand-50 text-brand-700',
  draft: 'bg-navy-50 text-navy-700',
  paused: 'bg-yellow-100 text-yellow-800',
  scheduled: 'bg-purple-100 text-purple-800',
  archived: 'bg-navy-50 text-navy-500',
};

function parseStats(json: string): Record<string, number> {
  try { return JSON.parse(json); } catch (e) { console.warn('CampaignPanel: malformed stats_json', e instanceof Error ? e.message : e); return {}; }
}

export default function CampaignPanel({ campaigns }: Props) {
  if (campaigns.length === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Kampanya İstatistikleri</CardTitle></CardHeader>
        <CardContent><p className="text-sm text-navy-300">Henüz kampanya verisi yok.</p></CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader><CardTitle>Kampanya İstatistikleri</CardTitle></CardHeader>
      <CardContent>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-navy-300 border-b">
                <th className="pb-2 font-medium">Kampanya</th>
                <th className="pb-2 font-medium">Tip</th>
                <th className="pb-2 font-medium">Durum</th>
                <th className="pb-2 font-medium text-right">Gönderilen</th>
                <th className="pb-2 font-medium text-right">İletilen</th>
                <th className="pb-2 font-medium text-right">Okunan</th>
                <th className="pb-2 font-medium text-right">Dönüşüm</th>
                <th className="pb-2 font-medium">Tarih</th>
              </tr>
            </thead>
            <tbody>
              {campaigns.map(c => {
                const stats = parseStats(c.stats_json);
                return (
                  <tr key={c.id} className="border-b last:border-0 hover:bg-navy-50/50">
                    <td className="py-2">
                      <div className="font-medium">{c.name}</div>
                      {c.template_name && <div className="text-xs text-navy-300">{c.template_name}</div>}
                    </td>
                    <td className="py-2 text-xs">{c.trigger_type}</td>
                    <td className="py-2">
                      <span className={`px-2 py-0.5 rounded-full text-xs ${statusColors[c.status] || 'bg-navy-100'}`}>
                        {c.status}
                      </span>
                    </td>
                    <td className="py-2 text-right">{stats.sent || 0}</td>
                    <td className="py-2 text-right">{stats.delivered || 0}</td>
                    <td className="py-2 text-right">{stats.read || 0}</td>
                    <td className="py-2 text-right font-medium">{stats.converted || 0}</td>
                    <td className="py-2 text-xs text-navy-300">{c.created_at}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}
