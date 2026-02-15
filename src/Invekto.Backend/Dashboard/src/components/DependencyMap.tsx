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
  const outbound = services.find(s => s.name.includes('Outbound'));
  const knowledge = services.find(s => s.name.includes('Knowledge'));

  const backendColor = backend ? getStatusColor(backend.status) : getStatusColor('unavailable');
  const chatColor = chatAnalysis ? getStatusColor(chatAnalysis.status) : getStatusColor('unavailable');
  const autoColor = automation ? getStatusColor(automation.status) : getStatusColor('unavailable');
  const agentAIColor = agentAI ? getStatusColor(agentAI.status) : getStatusColor('unavailable');
  const outboundColor = outbound ? getStatusColor(outbound.status) : getStatusColor('unavailable');
  const knowledgeColor = knowledge ? getStatusColor(knowledge.status) : getStatusColor('unavailable');

  return (
    <Card>
      <CardHeader>
        <CardTitle>Service Dependencies</CardTitle>
      </CardHeader>
      <CardContent>
        <svg viewBox="0 0 500 560" className="w-full h-96">
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

          {/* Arrow to Automation (mid-right) */}
          <g>
            <line
              x1="180"
              y1="105"
              x2="310"
              y2="145"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,145 296,138 300,150"
              fill="#9ca3af"
            />
            <rect x="218" y="110" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="243" y="127" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* Automation Node (mid-right) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 155px' }}>
            <rect
              x="320"
              y="115"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={autoColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${autoColor.glow})` }}
            />
            <circle cx="344" cy="147" r="6" fill={autoColor.fill} />
            <text x="360" y="151" fill="#111827" fontSize="16" fontWeight="600">
              Automation
            </text>
            <text x="344" y="175" fill="#6b7280" fontSize="13">
              localhost:7108
            </text>
          </g>

          {/* Arrow to AgentAI (bottom-right) */}
          <g>
            <line
              x1="180"
              y1="125"
              x2="310"
              y2="245"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,245 296,238 300,250"
              fill="#9ca3af"
            />
            <rect x="218" y="170" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="243" y="187" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* AgentAI Node (bottom-right) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 255px' }}>
            <rect
              x="320"
              y="215"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={agentAIColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${agentAIColor.glow})` }}
            />
            <circle cx="344" cy="247" r="6" fill={agentAIColor.fill} />
            <text x="360" y="251" fill="#111827" fontSize="16" fontWeight="600">
              AgentAI
            </text>
            <text x="344" y="275" fill="#6b7280" fontSize="13">
              localhost:7105
            </text>
          </g>

          {/* Arrow to Outbound (bottom-left) */}
          <g>
            <line
              x1="60"
              y1="140"
              x2="60"
              y2="335"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="60,335 54,321 66,321"
              fill="#9ca3af"
            />
            <rect x="35" y="225" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="60" y="242" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* Outbound Node (bottom-left) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '80px 375px' }}>
            <rect
              x="0"
              y="340"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={outboundColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${outboundColor.glow})` }}
            />
            <circle cx="24" cy="372" r="6" fill={outboundColor.fill} />
            <text x="40" y="376" fill="#111827" fontSize="16" fontWeight="600">
              Outbound
            </text>
            <text x="24" y="400" fill="#6b7280" fontSize="13">
              localhost:7107
            </text>
          </g>

          {/* Arrow to Knowledge (bottom-right-far) */}
          <g>
            <line
              x1="140"
              y1="140"
              x2="310"
              y2="355"
              stroke="#d1d5db"
              strokeWidth="2.5"
              strokeDasharray="8,5"
            />
            <polygon
              points="310,355 296,348 300,360"
              fill="#9ca3af"
            />
            <rect x="200" y="238" width="50" height="24" rx="6" fill="white" stroke="#e5e7eb" strokeWidth="1.5" />
            <text x="225" y="255" textAnchor="middle" fill="#6b7280" fontSize="12" fontWeight="500">
              HTTP
            </text>
          </g>

          {/* Knowledge Node (bottom-right) */}
          <g className="transition-transform duration-200 hover:scale-105" style={{ transformOrigin: '400px 375px' }}>
            <rect
              x="320"
              y="340"
              width="160"
              height="80"
              rx="12"
              fill="white"
              stroke={knowledgeColor.stroke}
              strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 8px ${knowledgeColor.glow})` }}
            />
            <circle cx="344" cy="372" r="6" fill={knowledgeColor.fill} />
            <text x="360" y="376" fill="#111827" fontSize="16" fontWeight="600">
              Knowledge
            </text>
            <text x="344" y="400" fill="#6b7280" fontSize="13">
              localhost:7104
            </text>
          </g>

          {/* Legend */}
          <g transform="translate(20, 525)">
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
