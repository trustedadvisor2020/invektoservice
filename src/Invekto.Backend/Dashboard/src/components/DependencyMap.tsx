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
  const automation = services.find(s => s.name.includes('Automation'));
  const agentAI = services.find(s => s.name.includes('AgentAI'));

  const backendColor = backend ? getStatusColor(backend.status) : getStatusColor('unavailable');
  const chatColor = chatAnalysis ? getStatusColor(chatAnalysis.status) : getStatusColor('unavailable');
  const autoColor = automation ? getStatusColor(automation.status) : getStatusColor('unavailable');
  const agentAIColor = agentAI ? getStatusColor(agentAI.status) : getStatusColor('unavailable');

  return (
    <Card>
      <CardHeader>
        <CardTitle>Service Dependencies</CardTitle>
      </CardHeader>
      <CardContent>
        <svg viewBox="0 0 500 380" className="w-full h-72">
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

          {/* Backend Node (center-left) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '100px 100px' }}>
            <rect
              x="20"
              y="60"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={backendColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${backendColor.glow})` }}
            />
            <circle cx="44" cy="92" r="6" fill={backendColor.fill} />
            <text x="60" y="96" fill="#111827" fontSize="16" fontWeight="600">
              Backend
            </text>
            <text x="44" y="120" fill="#6b7280" fontSize="13">
              localhost:5000
            </text>
          </g>

          {/* Arrow to ChatAnalysis (top-right) */}
          <g>
            <line
              x1="180"
              y1="85"
              x2="310"
              y2="55"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,55 296,52 300,64"
              fill="#9ca3af"
            />
            <rect x="218" y="54" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="243" y="71" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* ChatAnalysis Node (top-right) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 45px' }}>
            <rect
              x="320"
              y="15"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={chatColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${chatColor.glow})` }}
            />
            <circle cx="344" cy="47" r="6" fill={chatColor.fill} />
            <text x="360" y="51" fill="#111827" fontSize="16" fontWeight="600">
              ChatAnalysis
            </text>
            <text x="344" y="75" fill="#6b7280" fontSize="13">
              localhost:7101
            </text>
          </g>

          {/* Arrow to Automation (bottom-right) */}
          <g>
            <line
              x1="180"
              y1="115"
              x2="310"
              y2="155"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,155 296,148 300,160"
              fill="#9ca3af"
            />
            <rect x="218" y="120" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="243" y="137" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* Automation Node (bottom-right) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 165px' }}>
            <rect
              x="320"
              y="125"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={autoColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${autoColor.glow})` }}
            />
            <circle cx="344" cy="157" r="6" fill={autoColor.fill} />
            <text x="360" y="161" fill="#111827" fontSize="16" fontWeight="600">
              Automation
            </text>
            <text x="344" y="185" fill="#6b7280" fontSize="13">
              localhost:7108
            </text>
          </g>

          {/* Arrow to AgentAI (bottom-far-right) */}
          <g>
            <line
              x1="180"
              y1="130"
              x2="310"
              y2="265"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,265 296,258 300,270"
              fill="#9ca3af"
            />
            <rect x="218" y="182" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="243" y="199" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* AgentAI Node (bottom-far-right) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 275px' }}>
            <rect
              x="320"
              y="235"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={agentAIColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${agentAIColor.glow})` }}
            />
            <circle cx="344" cy="267" r="6" fill={agentAIColor.fill} />
            <text x="360" y="271" fill="#111827" fontSize="16" fontWeight="600">
              AgentAI
            </text>
            <text x="344" y="295" fill="#6b7280" fontSize="13">
              localhost:7105
            </text>
          </g>

          {/* Legend */}
          <g transform="translate(20, 340)">
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
