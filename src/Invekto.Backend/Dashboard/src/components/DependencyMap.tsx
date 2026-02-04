import type { ServiceHealth } from '../lib/api';
import { Card, CardHeader, CardTitle, CardContent } from './ui/Card';

interface DependencyMapProps {
  services: ServiceHealth[];
}

export function DependencyMap({ services }: DependencyMapProps) {
  const getStatusColor = (status: string) => {
    switch (status) {
      case 'ok': return { fill: '#10b981', stroke: '#059669', glow: 'rgba(16, 185, 129, 0.3)' };
      case 'degraded': return { fill: '#f59e0b', stroke: '#d97706', glow: 'rgba(245, 158, 11, 0.3)' };
      default: return { fill: '#ef4444', stroke: '#dc2626', glow: 'rgba(239, 68, 68, 0.3)' };
    }
  };

  const backend = services.find(s => s.name.includes('Backend'));
  const chatAnalysis = services.find(s => s.name.includes('ChatAnalysis'));

  const backendColor = backend ? getStatusColor(backend.status) : getStatusColor('unavailable');
  const chatColor = chatAnalysis ? getStatusColor(chatAnalysis.status) : getStatusColor('unavailable');

  return (
    <Card>
      <CardHeader>
        <CardTitle>Service Dependencies</CardTitle>
      </CardHeader>
      <CardContent>
        <svg viewBox="0 0 500 200" className="w-full h-44">
          {/* Glow filters */}
          <defs>
            <filter id="glow-green" x="-50%" y="-50%" width="200%" height="200%">
              <feGaussianBlur stdDeviation="4" result="coloredBlur"/>
              <feMerge>
                <feMergeNode in="coloredBlur"/>
                <feMergeNode in="SourceGraphic"/>
              </feMerge>
            </filter>
          </defs>

          {/* Backend Node */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '100px 80px' }}>
            <rect
              x="20"
              y="40"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={backendColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${backendColor.glow})` }}
            />
            <circle cx="44" cy="72" r="6" fill={backendColor.fill} />
            <text x="60" y="76" fill="#111827" fontSize="16" fontWeight="600">
              Backend
            </text>
            <text x="44" y="100" fill="#6b7280" fontSize="13">
              localhost:5000
            </text>
          </g>

          {/* Arrow */}
          <g>
            <line
              x1="180"
              y1="80"
              x2="310"
              y2="80"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,80 298,73 298,87"
              fill="#9ca3af"
            />
            <rect x="220" y="64" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="245" y="81" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* ChatAnalysis Node */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 80px' }}>
            <rect
              x="320"
              y="40"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={chatColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${chatColor.glow})` }}
            />
            <circle cx="344" cy="72" r="6" fill={chatColor.fill} />
            <text x="360" y="76" fill="#111827" fontSize="16" fontWeight="600">
              ChatAnalysis
            </text>
            <text x="344" y="100" fill="#6b7280" fontSize="13">
              localhost:7101
            </text>
          </g>

          {/* Legend */}
          <g transform="translate(20, 150)">
            <circle cx="8" cy="8" r="5" fill="#10b981" />
            <text x="20" y="13" fill="#6b7280" fontSize="13">OK</text>
            <circle cx="70" cy="8" r="5" fill="#f59e0b" />
            <text x="82" y="13" fill="#6b7280" fontSize="13">Degraded</text>
            <circle cx="170" cy="8" r="5" fill="#ef4444" />
            <text x="182" y="13" fill="#6b7280" fontSize="13">Down</text>
          </g>
        </svg>
      </CardContent>
    </Card>
  );
}
