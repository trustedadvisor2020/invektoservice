import { useState, useEffect, useCallback } from 'react';
import { Search, Filter, RefreshCw, ChevronDown, ChevronUp, Clock, Layers, Activity, List, Trash2 } from 'lucide-react';
import type { LogGroup, LogEntry } from '../lib/api';
import { api } from '../lib/api';
import { Card, CardHeader, CardTitle, CardContent } from './ui/Card';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { Input } from './ui/Input';
import { Select } from './ui/Select';
import { formatTimestamp, cn } from '../lib/utils';

type ViewMode = 'business' | 'all';

interface LogStreamProps {
  initialFilter?: {
    levels?: string[];
    service?: string;
    search?: string;
    after?: string;
  };
}

function formatDurationMs(ms: number | null): string {
  if (ms == null) return '-';
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`;
  return `${(ms / 60000).toFixed(1)}m`;
}

function formatDurationMsDetailed(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${ms.toLocaleString('tr-TR')}ms (~${(ms / 1000).toFixed(0)}s)`;
  return `${(ms / 60000).toFixed(1)}m`;
}

function formatTimeWithMs(timestamp: string): string {
  const d = new Date(timestamp);
  const hh = d.getHours().toString().padStart(2, '0');
  const mm = d.getMinutes().toString().padStart(2, '0');
  const ss = d.getSeconds().toString().padStart(2, '0');
  const ms = d.getMilliseconds();
  if (ms === 0) return `${hh}:${mm}:${ss}`;
  return `${hh}:${mm}:${ss}.${ms.toString().padStart(3, '0')}`;
}

function computeStepDuration(
  entry: LogEntry,
  index: number,
  entries: LogEntry[]
): number | null {
  // If entry has its own durationMs, use it
  if (entry.durationMs != null && entry.durationMs > 0) return entry.durationMs;
  // For first entry, no delta to compute
  if (index === 0) return null;
  // Compute delta from previous entry's timestamp
  const prev = new Date(entries[index - 1].timestamp).getTime();
  const curr = new Date(entry.timestamp).getTime();
  const delta = curr - prev;
  if (delta <= 0) return null;
  return delta;
}

function getLevelVariant(level: string): 'error' | 'warning' | 'info' {
  switch (level) {
    case 'ERROR': return 'error';
    case 'WARN': return 'warning';
    default: return 'info';
  }
}

function shortenRoute(route?: string): string {
  if (!route) return '';
  // "/api/v1/chat/analyze" → "analyze"
  // "/api/v1/webhook/event" → "webhook/event"
  const parts = route.replace(/^\/api\/v\d+\//, '').split('/');
  return parts.length > 2 ? parts.slice(-2).join('/') : parts.join('/');
}

function formatSmartSummary(group: LogGroup): { service: string; action: string; detail: string; isError: boolean } {
  const service = group.service.replace('Chatinbox.', '');
  const action = shortenRoute(group.route) || '-';
  const isError = group.level === 'ERROR';

  // Count step entries for detail
  const stepCount = group.entryCount > 1 ? `${group.entryCount} adım` : '';

  let detail: string;
  if (isError) {
    // Show error code or error summary
    detail = group.errorCode || group.summary;
  } else {
    const status = group.status === 'ok' ? 'OK' : group.status || 'OK';
    const parts: string[] = [];
    if (stepCount) parts.push(stepCount);
    parts.push(status);
    detail = parts.join(' | ');
  }

  return { service, action, detail, isError };
}

export function LogStream({ initialFilter }: LogStreamProps) {
  const [groups, setGroups] = useState<LogGroup[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [viewMode, setViewMode] = useState<ViewMode>('business');

  // Filters
  const [levels, setLevels] = useState<string[]>(initialFilter?.levels || ['ERROR', 'WARN', 'INFO']);
  const [service, setService] = useState(initialFilter?.service || '');
  const [search, setSearch] = useState(initialFilter?.search || '');

  const fetchLogs = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await api.getLogsGrouped({
        level: levels,
        service: service || undefined,
        search: search || undefined,
        limit: 50,
        category: viewMode === 'business' ? 'api,step' : 'all',
      });
      setGroups(response.groups);
    } catch (error) {
      console.error('Failed to fetch logs:', error);
    } finally {
      setIsLoading(false);
    }
  }, [levels, service, search, viewMode]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const [isClearing, setIsClearing] = useState(false);
  const [showClearConfirm, setShowClearConfirm] = useState(false);

  const handleClearLogs = async () => {
    setIsClearing(true);
    try {
      const svc = service ? service.replace('Chatinbox.', '') : undefined;
      await api.clearLogs(svc);
      setShowClearConfirm(false);
      setGroups([]);
      await fetchLogs();
    } catch (error) {
      console.error('Failed to clear logs:', error);
    } finally {
      setIsClearing(false);
    }
  };

  const toggleLevel = (level: string) => {
    setLevels(prev =>
      prev.includes(level)
        ? prev.filter(l => l !== level)
        : [...prev, level]
    );
  };

  const toggleExpand = (requestId: string) => {
    setExpandedId(prev => prev === requestId ? null : requestId);
  };

  return (
    <Card className="h-full flex flex-col">
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between mb-4">
          <CardTitle className="flex items-center gap-2">
            <Filter className="w-4 h-4 flex-shrink-0" />
            <span>Log Akisi</span>
          </CardTitle>
          <div className="flex items-center gap-2">
            {/* Business / All toggle */}
            <div className="flex bg-navy-100 rounded-lg p-0.5">
              <button
                className={cn(
                  "flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors",
                  viewMode === 'business'
                    ? "bg-white text-brand-700 shadow-sm"
                    : "text-navy-400 hover:text-navy-700"
                )}
                onClick={() => setViewMode('business')}
              >
                <Activity className="w-3 h-3" />
                İş Süreci
              </button>
              <button
                className={cn(
                  "flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors",
                  viewMode === 'all'
                    ? "bg-white text-brand-700 shadow-sm"
                    : "text-navy-400 hover:text-navy-700"
                )}
                onClick={() => setViewMode('all')}
              >
                <List className="w-3 h-3" />
                Tümü
              </button>
            </div>
            <Button variant="ghost" size="sm" onClick={fetchLogs} disabled={isLoading}>
              <RefreshCw className={cn("w-4 h-4 flex-shrink-0", isLoading && "animate-spin")} />
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowClearConfirm(true)}
              disabled={isClearing || groups.length === 0}
              className="text-red-500 hover:text-red-700 hover:bg-red-50"
            >
              <Trash2 className="w-4 h-4 flex-shrink-0" />
            </Button>
          </div>
        </div>

        {/* Clear confirm */}
        {showClearConfirm && (
          <div className="flex items-center gap-2 px-3 py-2 bg-red-50 border border-red-200 rounded-lg text-sm">
            <Trash2 className="w-4 h-4 text-red-500 shrink-0" />
            <span className="text-red-700">
              {service ? `${service.replace('Chatinbox.', '')} loglarını` : 'Tüm logları'} silmek istediğinize emin misiniz?
            </span>
            <div className="flex gap-1 ml-auto">
              <Button size="sm" variant="secondary" onClick={() => setShowClearConfirm(false)}>
                İptal
              </Button>
              <Button
                size="sm"
                variant="primary"
                onClick={handleClearLogs}
                disabled={isClearing}
                className="bg-red-600 hover:bg-red-700 text-white"
              >
                {isClearing ? 'Siliniyor...' : 'Evet, Sil'}
              </Button>
            </div>
          </div>
        )}

        {/* Filters */}
        <div className="flex flex-wrap items-center gap-3">
          {/* Level filters */}
          <div className="flex gap-1">
            {['ERROR', 'WARN', 'INFO'].map(level => (
              <Button
                key={level}
                variant={levels.includes(level) ? 'primary' : 'secondary'}
                size="sm"
                onClick={() => toggleLevel(level)}
              >
                {level}
              </Button>
            ))}
          </div>

          {/* Service filter */}
          <Select
            value={service}
            onChange={e => setService(e.target.value)}
            options={[
              { value: '', label: 'Tüm Servisler' },
              { value: 'Chatinbox.Backend', label: 'Backend' },
              { value: 'Chatinbox.ChatAnalysis', label: 'ChatAnalysis' },
              { value: 'Chatinbox.Automation', label: 'Automation' },
              { value: 'Chatinbox.AgentAI', label: 'AgentAI' },
              { value: 'Chatinbox.Outbound', label: 'Outbound' },
              { value: 'Chatinbox.Knowledge', label: 'Knowledge' },
              { value: 'Chatinbox.Appointments', label: 'Appointments' },
              { value: 'Chatinbox.Integrations', label: 'Integrations' },
              { value: 'Chatinbox.WhatsAppAnalytics', label: 'WA Analytics' },
              { value: 'Chatinbox.Marketing', label: 'Marketing' },
            ]}
            className="w-36"
          />

          {/* Search */}
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-navy-300" />
            <Input
              placeholder="Log ara..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>
        </div>
      </CardHeader>

      <CardContent className="flex-1 overflow-auto">
        {isLoading && groups.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-navy-400">
            Yükleniyor...
          </div>
        ) : groups.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-navy-400">
            Log bulunamadı
          </div>
        ) : (
          <div className="space-y-2">
            {groups.map((group) => {
              const isExpanded = expandedId === group.requestId;

              return (
                <div
                  key={group.requestId}
                  className={cn(
                    "border rounded-lg overflow-hidden transition-all duration-150",
                    isExpanded ? "border-brand-200 bg-brand-50/30" : "border-navy-100 hover:border-navy-200"
                  )}
                >
                  {/* Group header */}
                  <button
                    className="w-full px-3 py-2.5 flex items-center gap-3 text-left hover:bg-navy-50 transition-colors"
                    onClick={() => toggleExpand(group.requestId)}
                  >
                    <Badge variant={getLevelVariant(group.level)} className="shrink-0 w-14 justify-center">
                      {group.level}
                    </Badge>
                    <span className="text-xs text-navy-400 shrink-0 w-28 font-mono">
                      {formatTimestamp(group.startTime)}
                    </span>
                    {viewMode === 'business' ? (
                      /* Smart summary: Service > action > detail */
                      (() => {
                        const smart = formatSmartSummary(group);
                        return (
                          <span className="flex-1 flex items-center gap-1.5 truncate text-sm">
                            <span className="font-medium text-navy-900">{smart.service}</span>
                            <span className="text-navy-300">&rsaquo;</span>
                            <span className="text-navy-500">{smart.action}</span>
                            <span className="text-navy-300">&rsaquo;</span>
                            {smart.isError ? (
                              <span className="text-red-600 font-medium truncate">{smart.detail}</span>
                            ) : (
                              <span className="text-green-700">{smart.detail}</span>
                            )}
                          </span>
                        );
                      })()
                    ) : (
                      /* All mode: original layout */
                      <>
                        <span className="text-xs text-navy-300 shrink-0 w-24 truncate">
                          {group.service.replace('Chatinbox.', '')}
                        </span>
                        <span className="flex-1 truncate text-sm text-navy-700">
                          {group.summary}
                        </span>
                      </>
                    )}
                    <div className="flex items-center gap-2 shrink-0">
                      {/* Duration */}
                      {group.durationMs != null && (
                        <span className={cn(
                          "text-xs font-mono px-1.5 py-0.5 rounded",
                          group.durationMs > 5000 ? "bg-red-100 text-red-700" :
                          group.durationMs > 1000 ? "bg-amber-100 text-amber-700" :
                          "bg-green-100 text-green-700"
                        )}>
                          <Clock className="w-3 h-3 inline mr-0.5" />
                          {formatDurationMs(group.durationMs)}
                        </span>
                      )}
                      {/* Entry count */}
                      {group.entryCount > 1 && (
                        <span className="text-xs text-navy-300 font-mono">
                          <Layers className="w-3 h-3 inline mr-0.5" />
                          {group.entryCount}
                        </span>
                      )}
                      {isExpanded ? (
                        <ChevronUp className="w-4 h-4 text-navy-300" />
                      ) : (
                        <ChevronDown className="w-4 h-4 text-navy-300" />
                      )}
                    </div>
                  </button>

                  {/* Expanded: operation timeline */}
                  {isExpanded && (
                    <div className="px-4 py-3 bg-navy-50 border-t border-navy-100 text-sm">
                      {/* Operation header */}
                      <div className="flex items-center gap-2 mb-3">
                        <span className="text-xs text-navy-400">İşlem:</span>
                        <span className="font-mono text-xs text-navy-700 break-all select-all">
                          {group.requestId}
                        </span>
                        {group.route && (
                          <>
                            <span className="text-xs text-navy-300">|</span>
                            <span className="font-mono text-xs text-navy-400">{group.route}</span>
                          </>
                        )}
                      </div>

                      {/* Timeline table */}
                      <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
                        <table className="w-full text-xs font-mono">
                          <thead>
                            <tr className="bg-navy-100 text-navy-400">
                              <th className="text-left px-3 py-1.5 w-32">Zaman</th>
                              <th className="text-left px-3 py-1.5">Adım</th>
                              <th className="text-right px-3 py-1.5 w-32">Süre</th>
                            </tr>
                          </thead>
                          <tbody>
                            {group.entries.map((entry: LogEntry, i: number) => {
                              const stepDuration = computeStepDuration(entry, i, group.entries);
                              return (
                                <tr
                                  key={i}
                                  className={cn(
                                    "border-t border-navy-100",
                                    entry.level === 'ERROR' ? "bg-red-50/50" :
                                    entry.level === 'WARN' ? "bg-amber-50/50" : ""
                                  )}
                                >
                                  <td className="px-3 py-1.5 text-navy-300 whitespace-nowrap align-top">
                                    {formatTimeWithMs(entry.timestamp)}
                                  </td>
                                  <td className="px-3 py-1.5 text-navy-700 align-top">
                                    <div className="flex items-center gap-1.5">
                                      {entry.level !== 'INFO' && (
                                        <Badge
                                          variant={getLevelVariant(entry.level)}
                                          className="shrink-0 text-[10px] px-1 py-0"
                                        >
                                          {entry.level}
                                        </Badge>
                                      )}
                                      <span>{entry.message}</span>
                                    </div>
                                  </td>
                                  <td className="px-3 py-1.5 text-right whitespace-nowrap align-top">
                                    {stepDuration != null ? (
                                      <span className={cn(
                                        "px-1.5 py-0.5 rounded",
                                        stepDuration > 5000 ? "bg-red-100 text-red-700" :
                                        stepDuration > 1000 ? "bg-amber-100 text-amber-700" :
                                        "bg-green-100 text-green-700"
                                      )}>
                                        {formatDurationMsDetailed(stepDuration)}
                                      </span>
                                    ) : (
                                      <span className="text-navy-200">-</span>
                                    )}
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

