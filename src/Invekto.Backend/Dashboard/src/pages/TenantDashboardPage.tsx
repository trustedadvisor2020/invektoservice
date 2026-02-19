import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { api, type AutomationSummary } from '../lib/api';
import { Card, CardContent } from '../components/ui/Card';
import {
  MessageSquare,
  CheckCircle2,
  UserCheck,
  TrendingUp,
  GitBranch,
  BookOpen,
  Megaphone,
  CalendarDays,
  BarChart3,
  Star,
} from 'lucide-react';

// --- Stat Card ---

interface StatCardProps {
  label: string;
  value: string | number | undefined;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
  bgColor: string;
}

function StatCard({ label, value, icon: Icon, color, bgColor }: StatCardProps) {
  return (
    <Card>
      <CardContent className="py-5">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs font-medium text-slate-500">{label}</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">{value ?? '\u2014'}</p>
          </div>
          <div className={`w-10 h-10 rounded-xl ${bgColor} flex items-center justify-center`}>
            <Icon className={`w-5 h-5 ${color}`} />
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// --- Feature Card ---

interface FeatureCardDef {
  feature: string;
  path: string;
  label: string;
  desc: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
  bgColor: string;
}

const FEATURE_CARDS: FeatureCardDef[] = [
  { feature: 'FlowBuilder', path: '/flow-builder-ui', label: 'Flow Builder', desc: 'Akis tasarimi', icon: GitBranch, color: 'text-blue-600', bgColor: 'bg-blue-50' },
  { feature: 'Knowledge', path: '/knowledge', label: 'Bilgi Bankasi', desc: 'Dokuman ve SSS', icon: BookOpen, color: 'text-emerald-600', bgColor: 'bg-emerald-50' },
  { feature: 'Outbound', path: '/campaigns', label: 'Kampanyalar', desc: 'Toplu mesaj gonderin', icon: Megaphone, color: 'text-violet-600', bgColor: 'bg-violet-50' },
  { feature: 'Appointments', path: '/appointments', label: 'Randevular', desc: 'Randevu yonetimi', icon: CalendarDays, color: 'text-amber-600', bgColor: 'bg-amber-50' },
  { feature: 'Analytics', path: '/analytics', label: 'Analizler', desc: 'Performans raporlari', icon: BarChart3, color: 'text-cyan-600', bgColor: 'bg-cyan-50' },
  { feature: 'Marketing', path: '/marketing', label: 'Pazarlama', desc: 'Yorum ve yonlendirme', icon: Star, color: 'text-rose-600', bgColor: 'bg-rose-50' },
];

// --- Page ---

export function TenantDashboardPage() {
  const { session, welcomeData } = useAuth();
  const [stats, setStats] = useState<AutomationSummary | null>(null);

  useEffect(() => {
    if (!session) return;
    api.getAutomationSummary(session.tenantId)
      .then(setStats)
      .catch(err => console.warn('[TenantDashboard] stats fetch failed:', err));
  }, [session]);

  const features = FEATURE_CARDS.filter(c => api.hasFeature(c.feature));

  return (
    <div className="space-y-8">
      {/* Hosgeldiniz */}
      <div>
        <h1 className="text-2xl font-bold text-slate-900">
          Hosgeldiniz{session?.fullName ? `, ${session.fullName}` : ''}!
        </h1>
        <p className="text-sm text-slate-500 mt-1">
          {session?.companyCode ? `${session.companyCode} — ` : ''}Firma #{session?.tenantId}
        </p>
      </div>

      {/* Welcome response from INMA */}
      {welcomeData != null && (
        <Card>
          <CardContent className="py-4">
            <h3 className="text-sm font-semibold text-slate-600 mb-2">INMA Welcome</h3>
            <pre className="text-xs text-slate-700 bg-slate-50 rounded-lg p-3 overflow-auto max-h-48 whitespace-pre-wrap">
              {String(typeof welcomeData === 'string' ? welcomeData : JSON.stringify(welcomeData, null, 2))}
            </pre>
          </CardContent>
        </Card>
      )}

      {/* Istatistikler */}
      <div>
        <h2 className="text-sm font-semibold text-slate-400 uppercase tracking-wider mb-3">Genel Bakis</h2>
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          <StatCard
            label="Toplam Gorusme"
            value={stats?.total_replies}
            icon={MessageSquare}
            color="text-blue-600"
            bgColor="bg-blue-50"
          />
          <StatCard
            label="Otomatik Cozulen"
            value={stats?.deflected_count}
            icon={CheckCircle2}
            color="text-emerald-600"
            bgColor="bg-emerald-50"
          />
          <StatCard
            label="Insan Destek"
            value={stats?.handoff_count}
            icon={UserCheck}
            color="text-amber-600"
            bgColor="bg-amber-50"
          />
          <StatCard
            label="Basari Orani"
            value={stats ? `%${Math.round(stats.deflection_rate * 100)}` : undefined}
            icon={TrendingUp}
            color="text-violet-600"
            bgColor="bg-violet-50"
          />
        </div>
      </div>

      {/* Hizli Erisim */}
      {features.length > 0 && (
        <div>
          <h2 className="text-sm font-semibold text-slate-400 uppercase tracking-wider mb-3">Hizli Erisim</h2>
          <div className="grid grid-cols-2 lg:grid-cols-3 gap-3">
            {features.map(f => {
              const Icon = f.icon;
              return (
                <Link key={f.path} to={f.path} className="block">
                  <Card className="hover:shadow-md hover:border-slate-300 transition-all cursor-pointer h-full">
                    <CardContent className="py-5 flex items-center gap-4">
                      <div className={`w-11 h-11 rounded-xl ${f.bgColor} flex items-center justify-center flex-shrink-0`}>
                        <Icon className={`w-5 h-5 ${f.color}`} />
                      </div>
                      <div className="min-w-0">
                        <p className="font-semibold text-slate-900 text-sm">{f.label}</p>
                        <p className="text-xs text-slate-500 mt-0.5">{f.desc}</p>
                      </div>
                    </CardContent>
                  </Card>
                </Link>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
