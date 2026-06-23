import { useState, useEffect } from 'react';
import { Languages, Database, Users, Clock, RefreshCw } from 'lucide-react';
import { api, type TranslationStatsResponse } from '../lib/api';
import { Card, CardContent } from './ui/Card';
import { Button } from './ui/Button';

export function TranslationStats() {
  const [stats, setStats] = useState<TranslationStatsResponse | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchStats = async () => {
    setLoading(true);
    try {
      const data = await api.getTranslationStats();
      setStats(data);
    } catch {
      // silently fail — widget is informational
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchStats(); }, []);

  if (loading && !stats) {
    return (
      <Card>
        <CardContent className="py-6 text-center text-navy-300 text-sm">
          Çeviri istatistikleri yükleniyor...
        </CardContent>
      </Card>
    );
  }

  if (!stats) return null;

  const metrics = [
    { label: 'Aktif Cache', value: stats.active_cached, icon: Database, color: 'bg-brand-100 text-brand-600' },
    { label: 'Tenant', value: stats.tenant_count, icon: Users, color: 'bg-green-100 text-green-600' },
    { label: 'Dil', value: stats.language_count, icon: Languages, color: 'bg-purple-100 text-purple-600' },
    { label: 'Süresi Dolmuş', value: stats.expired, icon: Clock, color: 'bg-navy-100 text-navy-500' },
  ];

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Languages className="w-4 h-4 text-navy-400" />
          <h3 className="text-sm font-medium text-navy-700">Çeviri Cache</h3>
        </div>
        <Button variant="ghost" size="sm" onClick={fetchStats} disabled={loading}>
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-2">
        {metrics.map(m => (
          <Card key={m.label}>
            <CardContent className="py-3 px-3">
              <div className="flex items-center gap-2">
                <div className={`w-7 h-7 rounded-md flex items-center justify-center ${m.color}`}>
                  <m.icon className="w-3.5 h-3.5" />
                </div>
                <div>
                  <p className="text-xs text-navy-400">{m.label}</p>
                  <p className="text-base font-semibold text-navy-900">{m.value.toLocaleString('tr-TR')}</p>
                </div>
              </div>
            </CardContent>
          </Card>
        ))}
      </div>

      {stats.top_languages.length > 0 && (
        <Card>
          <CardContent className="py-3 px-3">
            <p className="text-xs text-navy-400 mb-2">Dil Dağılımı</p>
            <div className="flex flex-wrap gap-1.5">
              {stats.top_languages.map(l => (
                <span key={l.language} className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-navy-50 text-xs text-navy-600">
                  <span className="font-medium">{l.language.toUpperCase()}</span>
                  <span className="text-navy-400">{l.count}</span>
                </span>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
