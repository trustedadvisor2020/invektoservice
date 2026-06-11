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
  const deps = services.filter(s => !s.name.includes('Backend'));

  const backendColor = backend ? getStatusColor(backend.status) : getStatusColor('unavailable');

  // Layout: Backend on left, deps in 3-column grid on right
  const cols = 3;
  const nodeW = 140;
  const nodeH = 56;
  const gapX = 16;
  const gapY = 14;
  const gridStartX = 220;
  const gridStartY = 16;
  const backendX = 10;
  const backendY = Math.max(40, (deps.length / cols) * (nodeH + gapY) / 2 - 20);

  const svgW = gridStartX + cols * (nodeW + gapX);
  const svgH = Math.max(180, gridStartY + Math.ceil(deps.length / cols) * (nodeH + gapY) + 40);

  return (
    <Card>
      <CardHeader>
        <CardTitle>Servis Bağımlılıkları</CardTitle>
      </CardHeader>
      <CardContent>
        <svg viewBox={`0 0 ${svgW} ${svgH}`} className="w-full" style={{ maxHeight: '420px' }}>
          <defs>
            <filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
              <feGaussianBlur stdDeviation="3" result="coloredBlur"/>
              <feMerge>
                <feMergeNode in="coloredBlur"/>
                <feMergeNode in="SourceGraphic"/>
              </feMerge>
            </filter>
          </defs>

          {/* Backend Node */}
          <g>
            <rect
              x={backendX} y={backendY}
              width="170" height="60" rx="10"
              fill="white" stroke={backendColor.stroke} strokeWidth="2.5"
              style={{ filter: `drop-shadow(0 0 6px ${backendColor.glow})` }}
            />
            <circle cx={backendX + 18} cy={backendY + 24} r="5" fill={backendColor.fill} />
            <text x={backendX + 30} y={backendY + 28} fill="#111827" fontSize="14" fontWeight="600">Backend</text>
            <text x={backendX + 18} y={backendY + 46} fill="#6b7280" fontSize="11">localhost:5000</text>
          </g>

          {/* Dependency Nodes + Arrows */}
          {deps.map((svc, i) => {
            const col = i % cols;
            const row = Math.floor(i / cols);
            const x = gridStartX + col * (nodeW + gapX);
            const y = gridStartY + row * (nodeH + gapY);
            const color = getStatusColor(svc.status);
            const shortName = svc.name.replace('Invekto.', '');
            const port = svc.name.includes('ChatAnalysis') ? 7101
              : svc.name.includes('Appointments') ? 7102
              : svc.name.includes('Knowledge') ? 7104
              : svc.name.includes('AgentAI') ? 7105
              : svc.name.includes('Integrations') ? 7106
              : svc.name.includes('Outbound') ? 7107
              : svc.name.includes('Automation') ? 7108
              : svc.name.includes('WhatsAppAnalytics') ? 7109
              : svc.name.includes('Marketing') ? 7112
              : 0;

            // Arrow from backend right edge to node left edge
            const arrowStartX = backendX + 170;
            const arrowStartY = backendY + 30;
            const arrowEndX = x;
            const arrowEndY = y + nodeH / 2;

            return (
              <g key={svc.name}>
                {/* Arrow line */}
                <line
                  x1={arrowStartX} y1={arrowStartY}
                  x2={arrowEndX} y2={arrowEndY}
                  stroke="#e5e7eb" strokeWidth="1.5" strokeDasharray="6,4"
                />
                <polygon
                  points={`${arrowEndX},${arrowEndY} ${arrowEndX - 8},${arrowEndY - 4} ${arrowEndX - 8},${arrowEndY + 4}`}
                  fill="#d1d5db"
                />

                {/* Service Node */}
                <rect
                  x={x} y={y}
                  width={nodeW} height={nodeH} rx="8"
                  fill="white" stroke={color.stroke} strokeWidth="2"
                  style={{ filter: `drop-shadow(0 0 5px ${color.glow})` }}
                />
                <circle cx={x + 14} cy={y + 20} r="4" fill={color.fill} />
                <text x={x + 24} y={y + 23} fill="#111827" fontSize="12" fontWeight="600">
                  {shortName.length > 14 ? shortName.slice(0, 13) + '…' : shortName}
                </text>
                <text x={x + 14} y={y + 42} fill="#6b7280" fontSize="10">
                  :{port}
                </text>
              </g>
            );
          })}

          {/* Legend */}
          <g transform={`translate(10, ${svgH - 24})`}>
            <circle cx="6" cy="6" r="4" fill="#10b981" />
            <text x="16" y="10" fill="#6b7280" fontSize="11">OK</text>
            <circle cx="52" cy="6" r="4" fill="#f59e0b" />
            <text x="62" y="10" fill="#6b7280" fontSize="11">Düşük</text>
            <circle cx="120" cy="6" r="4" fill="#ef4444" />
            <text x="130" y="10" fill="#6b7280" fontSize="11">Kapalı</text>
          </g>
        </svg>
      </CardContent>
    </Card>
  );
}
