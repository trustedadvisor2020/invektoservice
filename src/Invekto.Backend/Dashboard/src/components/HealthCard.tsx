import { useState, useEffect } from 'react';
import { RefreshCw, Power, Server, MessageSquare, Bot, Sparkles, Send, BookOpen, Calendar, Plug, BarChart3, Megaphone, X, ExternalLink, List, Loader2 } from 'lucide-react';
import type { ServiceHealth, EndpointInfo } from '../lib/api';
import { api } from '../lib/api';
import { Card, CardContent } from './ui/Card';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';

interface HealthCardProps {
  service: ServiceHealth;
  onRestart?: () => void;
  isRestarting?: boolean;
}

// Static config for icons and ports (fallback)
const serviceConfig: Record<string, {
  port: number;
  host: string;
  healthEndpoint: string;
  icon: typeof Server;
}> = {
  'Invekto.Backend': {
    port: 5000,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Server,
  },
  'Invekto.ChatAnalysis': {
    port: 7101,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: MessageSquare,
  },
  'Invekto.Automation': {
    port: 7108,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Bot,
  },
  'Invekto.AgentAI': {
    port: 7105,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Sparkles,
  },
  'Invekto.Outbound': {
    port: 7107,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Send,
  },
  'Invekto.Knowledge': {
    port: 7104,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: BookOpen,
  },
  'Invekto.Appointments': {
    port: 7102,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Calendar,
  },
  'Invekto.Integrations': {
    port: 7106,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Plug,
  },
  'Invekto.WhatsAppAnalytics': {
    port: 7109,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: BarChart3,
  },
  'Invekto.Marketing': {
    port: 7112,
    host: 'localhost',
    healthEndpoint: '/health',
    icon: Megaphone,
  },
};

const defaultConfig = {
  port: 0,
  host: 'unknown',
  healthEndpoint: '/health',
  icon: Server,
};

// Shared endpoint cache (loaded once, used by all cards)
let endpointCache: Record<string, { endpoints: EndpointInfo[]; port: number }> | null = null;
let endpointPromise: Promise<void> | null = null;

async function loadEndpoints(): Promise<void> {
  if (endpointCache) return;
  if (endpointPromise) return endpointPromise;

  endpointPromise = (async () => {
    try {
      const result = await api.getAllEndpoints();
      endpointCache = {};
      for (const svc of result.services) {
        endpointCache[svc.service] = { endpoints: svc.endpoints, port: svc.port };
      }
    } catch {
      // If discovery fails, leave cache null (cards show fallback)
      endpointCache = null;
    } finally {
      endpointPromise = null;
    }
  })();

  return endpointPromise;
}

// Method color mapping
function getMethodColor(method: string): string {
  switch (method) {
    case 'GET': return 'bg-emerald-100 text-emerald-700';
    case 'POST': return 'bg-blue-100 text-blue-700';
    case 'PUT': return 'bg-amber-100 text-amber-700';
    case 'DELETE': return 'bg-red-100 text-red-700';
    default: return 'bg-slate-200 text-slate-700';
  }
}

export function HealthCard({ service, onRestart, isRestarting }: HealthCardProps) {
  const [showEndpoints, setShowEndpoints] = useState(false);
  const [endpoints, setEndpoints] = useState<EndpointInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [dynamicPort, setDynamicPort] = useState<number | null>(null);

  const config = serviceConfig[service.name] || defaultConfig;
  const port = dynamicPort ?? config.port;
  const baseUrl = `http://${config.host}:${port}`;
  const ServiceIcon = config.icon;

  const statusVariant = service.status === 'ok' ? 'success' : service.status === 'degraded' ? 'warning' : 'error';
  const statusDot = service.status === 'ok' ? 'bg-emerald-500' : service.status === 'degraded' ? 'bg-amber-500' : 'bg-red-500';

  // Load endpoints when popup opens
  useEffect(() => {
    if (!showEndpoints) return;

    // Check cache first
    if (endpointCache && endpointCache[service.name]) {
      setEndpoints(endpointCache[service.name].endpoints);
      setDynamicPort(endpointCache[service.name].port);
      return;
    }

    setLoading(true);
    loadEndpoints().then(() => {
      if (endpointCache && endpointCache[service.name]) {
        setEndpoints(endpointCache[service.name].endpoints);
        setDynamicPort(endpointCache[service.name].port);
      }
      setLoading(false);
    });
  }, [showEndpoints, service.name]);

  // Preload count for the card display
  useEffect(() => {
    loadEndpoints().then(() => {
      if (endpointCache && endpointCache[service.name]) {
        setEndpoints(endpointCache[service.name].endpoints);
        setDynamicPort(endpointCache[service.name].port);
      }
    });
  }, [service.name]);

  // Group endpoints by category
  const groupedEndpoints = endpoints.reduce<Record<string, EndpointInfo[]>>((acc, ep) => {
    const cat = ep.category || 'Other';
    if (!acc[cat]) acc[cat] = [];
    acc[cat].push(ep);
    return acc;
  }, {});

  // Category display order
  const categoryOrder = ['API', 'Health', 'Ops', 'Legacy', 'Other'];
  const sortedCategories = Object.keys(groupedEndpoints).sort(
    (a, b) => (categoryOrder.indexOf(a) === -1 ? 99 : categoryOrder.indexOf(a)) -
              (categoryOrder.indexOf(b) === -1 ? 99 : categoryOrder.indexOf(b))
  );

  return (
    <>
      <Card>
        <CardContent className="space-y-2">
          {/* Header with Icon */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className={`w-7 h-7 rounded-lg flex items-center justify-center ${
                service.status === 'ok' ? 'bg-emerald-100 text-emerald-600' :
                service.status === 'degraded' ? 'bg-amber-100 text-amber-600' :
                'bg-red-100 text-red-600'
              }`}>
                <ServiceIcon className="w-3.5 h-3.5" />
              </div>
              <div>
                <div className="flex items-center gap-1.5">
                  <span className="font-semibold text-sm text-gray-900">{service.name.replace('Invekto.', '')}</span>
                  <div className={`w-1.5 h-1.5 rounded-full ${statusDot}`} style={{ boxShadow: `0 0 4px ${service.status === 'ok' ? '#10b981' : service.status === 'degraded' ? '#f59e0b' : '#ef4444'}` }} />
                </div>
                <span className="text-[10px] text-gray-500">:{port}</span>
              </div>
            </div>
            <Badge variant={statusVariant}>
              {service.status.toUpperCase()}
            </Badge>
          </div>

          {/* Compact Stats */}
          <div className="flex items-center gap-3 text-xs text-slate-500">
            <span>{service.responseTimeMs !== null ? `${service.responseTimeMs}ms` : '--'}</span>
            <span className="text-slate-300">|</span>
            <span className="flex items-center gap-0.5">
              <List className="w-3 h-3" />
              {endpoints.length || '--'}
            </span>
          </div>

          {/* Error message */}
          {service.error && (
            <div className="p-1.5 bg-red-50 border border-red-100 rounded text-[10px] text-red-700 truncate">
              {service.error}
            </div>
          )}

          {/* Action buttons */}
          <div className="flex gap-1">
            <Button
              variant="ghost"
              size="sm"
              className="flex-1 text-xs py-1"
              onClick={() => setShowEndpoints(true)}
            >
              <List className="w-3 h-3 flex-shrink-0" />
              <span>API</span>
            </Button>
            {onRestart && (
              <Button
                variant="secondary"
                size="sm"
                className="flex-1 text-xs py-1"
                onClick={onRestart}
                disabled={isRestarting}
              >
                {isRestarting ? (
                  <RefreshCw className="w-3 h-3 animate-spin flex-shrink-0" />
                ) : (
                  <Power className="w-3 h-3 flex-shrink-0" />
                )}
                <span>{isRestarting ? '...' : 'Restart'}</span>
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Endpoints Popup */}
      {showEndpoints && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm" onClick={() => setShowEndpoints(false)}>
          <div className="bg-white rounded-2xl shadow-2xl max-w-lg w-full max-h-[80vh] overflow-hidden" onClick={e => e.stopPropagation()}>
            {/* Popup Header */}
            <div className="p-4 border-b border-slate-200 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center">
                  <ServiceIcon className="w-4 h-4 text-slate-600" />
                </div>
                <div>
                  <h3 className="font-semibold text-slate-900">{service.name.replace('Invekto.', '')} Endpoints</h3>
                  <p className="text-xs text-slate-500">{baseUrl}</p>
                </div>
              </div>
              <button
                onClick={() => setShowEndpoints(false)}
                className="w-8 h-8 rounded-lg hover:bg-slate-100 flex items-center justify-center text-slate-400 hover:text-slate-600 transition-colors"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Endpoints List */}
            <div className="p-4 overflow-y-auto max-h-[60vh]">
              {loading ? (
                <div className="flex items-center justify-center py-8 text-slate-400">
                  <Loader2 className="w-5 h-5 animate-spin mr-2" />
                  <span>Yuklen iyor...</span>
                </div>
              ) : endpoints.length === 0 ? (
                <div className="text-center py-8 text-slate-400 text-sm">
                  Endpoint bulunamadi
                </div>
              ) : (
                <div className="space-y-4">
                  {sortedCategories.map(category => (
                    <div key={category}>
                      <h4 className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2 px-1">
                        {category}
                      </h4>
                      <div className="space-y-1.5">
                        {groupedEndpoints[category].map((endpoint, idx) => (
                          <div key={idx} className="p-3 rounded-lg bg-slate-50 hover:bg-slate-100 transition-colors group">
                            <div className="flex items-start justify-between gap-3">
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2 mb-1">
                                  <span className={`px-1.5 py-0.5 rounded text-xs font-mono font-semibold ${getMethodColor(endpoint.method)}`}>
                                    {endpoint.method}
                                  </span>
                                  <span className="font-mono text-sm text-slate-700 truncate">{endpoint.path}</span>
                                </div>
                                <div className="flex items-center gap-2">
                                  <p className="text-xs text-slate-500">{endpoint.description}</p>
                                  {endpoint.auth && endpoint.auth !== 'none' && (
                                    <span className="px-1 py-0.5 rounded text-[10px] bg-amber-50 text-amber-600 border border-amber-200">
                                      {endpoint.auth}
                                    </span>
                                  )}
                                </div>
                              </div>
                              {endpoint.method === 'GET' && (
                                <a
                                  href={`${baseUrl}${endpoint.path}`}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="opacity-0 group-hover:opacity-100 transition-opacity p-1.5 rounded hover:bg-slate-200"
                                >
                                  <ExternalLink className="w-3.5 h-3.5 text-slate-500" />
                                </a>
                              )}
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}
