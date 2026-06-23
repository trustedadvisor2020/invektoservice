import { Star, ChevronDown, ChevronUp } from 'lucide-react';
import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card';
import type { RiQualityInsight } from '../../lib/api';

interface Props {
  data: RiQualityInsight | null;
}

function ScoreBar({ label, value }: { label: string; value: number }) {
  const color = value >= 70 ? 'bg-green-400' : value >= 40 ? 'bg-amber-400' : 'bg-red-400';
  return (
    <div className="flex items-center gap-2 text-xs">
      <span className="w-16 text-navy-400 text-right">{label}</span>
      <div className="flex-1 h-1.5 bg-navy-100 rounded-full overflow-hidden">
        <div className={`h-full rounded-full ${color} transition-all duration-500`} style={{ width: `${value}%` }} />
      </div>
      <span className="w-8 text-navy-600 font-medium">{value.toFixed(0)}</span>
    </div>
  );
}

export function RiQualityScore({ data }: Props) {
  const [expanded, setExpanded] = useState(false);

  if (!data || data.totalScored === 0) {
    return (
      <Card>
        <CardHeader><CardTitle>Kalite Skoru</CardTitle></CardHeader>
        <CardContent className="py-8 text-center text-navy-300 text-sm">Henuz kalite verisi yok</CardContent>
      </Card>
    );
  }

  // Aggregate by agent
  const agentMap = new Map<string, { name: string; scores: typeof data.scores }>();
  for (const s of data.scores) {
    const key = s.agentName ?? `Agent ${s.agentId ?? 'N/A'}`;
    const existing = agentMap.get(key);
    if (existing) {
      existing.scores.push(s);
    } else {
      agentMap.set(key, { name: key, scores: [s] });
    }
  }

  const agentAverages = [...agentMap.entries()].map(([, v]) => {
    const avg = (field: 'responseSpeedScore' | 'engagementScore' | 'resolutionScore' | 'sentimentScore' | 'overallScore') =>
      v.scores.reduce((s, e) => s + e[field], 0) / v.scores.length;
    return {
      name: v.name,
      count: v.scores.length,
      speed: avg('responseSpeedScore'),
      engagement: avg('engagementScore'),
      resolution: avg('resolutionScore'),
      sentiment: avg('sentimentScore'),
      overall: avg('overallScore'),
    };
  }).sort((a, b) => b.overall - a.overall);

  const shown = expanded ? agentAverages : agentAverages.slice(0, 4);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="flex items-center gap-2">
          <Star className="w-4 h-4 text-purple-500" />
          Kalite Skoru
        </CardTitle>
        <div className="text-xs text-navy-300">
          Ort: <strong className="text-navy-600">{data.avgOverallScore.toFixed(1)}</strong> / 100
        </div>
      </CardHeader>
      <CardContent>
        <div className="space-y-4">
          {shown.map(a => (
            <div key={a.name} className="space-y-1.5">
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium text-navy-700">{a.name}</span>
                <span className="text-xs text-navy-400">{a.count} konusma | <strong className="text-navy-600">{a.overall.toFixed(1)}</strong></span>
              </div>
              <ScoreBar label="Hiz" value={a.speed} />
              <ScoreBar label="Ilgi" value={a.engagement} />
              <ScoreBar label="Cozum" value={a.resolution} />
              <ScoreBar label="Duygu" value={a.sentiment} />
            </div>
          ))}
        </div>
        {agentAverages.length > 4 && (
          <button
            onClick={() => setExpanded(!expanded)}
            className="mt-3 flex items-center gap-1 text-xs text-brand-500 hover:text-brand-600 transition-colors"
          >
            {expanded ? <ChevronUp className="w-3 h-3" /> : <ChevronDown className="w-3 h-3" />}
            {expanded ? 'Daralt' : `+${agentAverages.length - 4} daha`}
          </button>
        )}
      </CardContent>
    </Card>
  );
}
