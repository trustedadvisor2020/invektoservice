import { useState, useEffect } from 'react';
import { RefreshCw, Power, Globe, Link2, Server, MessageSquare, X, ExternalLink, List, Loader2 } from 'lucide-react';
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
        <CardContent className="space-y-4">
          {/* Header with Icon */}
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${
                service.status === 'ok' ? 'bg-emerald-100 text-emerald-600' :
                service.status === 'degraded' ? 'bg-amber-100 text-amber-600' :
                'bg-red-100 text-red-600'
              }`}>
                <ServiceIcon className="w-5 h-5" />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-semibold text-gray-900">{service.name.replace('Invekto.', '')}</span>
                  <div className={`w-2 h-2 rounded-full ${statusDot}`} style={{ boxShadow: `0 0 6px ${service.status === 'ok' ? '#10b981' : service.status === 'degraded' ? '#f59e0b' : '#ef4444'}` }} />
                </div>
                <span className="text-xs text-gray-500">Port {port}</span>
              </div>
            </div>
            <Badge variant={statusVariant}>
              {service.status.toUpperCase()}
            </Badge>
          </div>

          {/* Connection Details */}
          <div className="p-3 bg-slate-50 rounded-lg space-y-2">
            <div className="flex items-center gap-2 text-sm">
              <Globe className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
              <span className="text-slate-500">URL:</span>
              <a
                href={baseUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-blue-600 hover:text-blue-700 hover:underline"
              >
                {baseUrl}
              </a>
            </div>
            <div className="flex items-center gap-2 text-sm">
              <Link2 className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
              <span className="text-slate-500">Health:</span>
              <a
                href={`${baseUrl}${config.healthEndpoint}`}
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-blue-600 hover:text-blue-700 hover:underline"
              >
                {config.healthEndpoint}
              </a>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1">
              <span className="text-xs text-slate-500 uppercase tracking-wide">Response</span>
              <p className="text-lg font-semibold text-slate-900">
                {service.responseTimeMs !== null ? `${service.responseTimeMs}ms` : '--'}
              </p>
            </div>
            <div className="space-y-1">
              <span className="text-xs text-slate-500 uppercase tracking-wide">Endpoints</span>
              <p className="text-lg font-semibold text-slate-900">
                {endpoints.length || '--'}
              </p>
            </div>
          </div>

          {/* Error message */}
          {service.error && (
            <div className="p-2.5 bg-red-50 border border-red-100 rounded-lg text-xs text-red-700">
              {service.error}
            </div>
          )}

          {/* Action buttons */}
          <div className="flex gap-2">
            <Button
              variant="ghost"
              size="sm"
              className="flex-1"
              onClick={() => setShowEndpoints(true)}
            >
              <List className="w-3.5 h-3.5 flex-shrink-0" />
              <span>Endpoints</span>
            </Button>
            {onRestart && (
              <Button
                variant="secondary"
                size="sm"
                className="flex-1"
                onClick={onRestart}
                disabled={isRestarting}
              >
                {isRestarting ? (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin flex-shrink-0" />
                ) : (
                  <Power className="w-3.5 h-3.5 flex-shrink-0" />
                )}
                <span>{isRestarting ? 'Restarting...' : 'Restart'}</span>
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
                  <span>Loading endpoints...</span>
                </div>
              ) : endpoints.length === 0 ? (
                <div className="text-center py-8 text-slate-400 text-sm">
                  No endpoints discovered
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
