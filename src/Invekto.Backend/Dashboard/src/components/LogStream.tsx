import { useState, useEffect, useCallback } from 'react';
import { Search, Filter, RefreshCw, ChevronDown, ChevronUp, Clock, Layers, Activity, List } from 'lucide-react';
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
  const service = group.service.replace('Invekto.', '');
  const action = shortenRoute(group.route) || '-';
  const isError = group.level === 'ERROR';

  // Count step entries for detail
  const stepCount = group.entryCount > 1 ? `${group.entryCount} adim` : '';

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
            <span>Log Stream</span>
          </CardTitle>
          <div className="flex items-center gap-2">
            {/* Business / All toggle */}
            <div className="flex bg-gray-100 rounded-lg p-0.5">
              <button
                className={cn(
                  "flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors",
                  viewMode === 'business'
                    ? "bg-white text-blue-700 shadow-sm"
                    : "text-gray-500 hover:text-gray-700"
                )}
                onClick={() => setViewMode('business')}
              >
                <Activity className="w-3 h-3" />
                Business
              </button>
              <button
                className={cn(
                  "flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-medium transition-colors",
                  viewMode === 'all'
                    ? "bg-white text-blue-700 shadow-sm"
                    : "text-gray-500 hover:text-gray-700"
                )}
                onClick={() => setViewMode('all')}
              >
                <List className="w-3 h-3" />
                All
              </button>
            </div>
            <Button variant="ghost" size="sm" onClick={fetchLogs} disabled={isLoading}>
              <RefreshCw className={cn("w-4 h-4 flex-shrink-0", isLoading && "animate-spin")} />
            </Button>
          </div>
        </div>

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
              { value: '', label: 'All Services' },
              { value: 'Invekto.Backend', label: 'Backend' },
              { value: 'Invekto.ChatAnalysis', label: 'ChatAnalysis' },
              { value: 'Invekto.Automation', label: 'Automation' },
              { value: 'Invekto.AgentAI', label: 'AgentAI' },
            ]}
            className="w-36"
          />

          {/* Search */}
          <div className="relative flex-1 min-w-[200px]">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <Input
              placeholder="Search logs..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>
        </div>
      </CardHeader>

      <CardContent className="flex-1 overflow-auto">
        {isLoading && groups.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-gray-500">
            Loading...
          </div>
        ) : groups.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-gray-500">
            No logs found
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
                    isExpanded ? "border-blue-200 bg-blue-50/30" : "border-gray-200 hover:border-gray-300"
                  )}
                >
                  {/* Group header */}
                  <button
                    className="w-full px-3 py-2.5 flex items-center gap-3 text-left hover:bg-gray-50 transition-colors"
                    onClick={() => toggleExpand(group.requestId)}
                  >
                    <Badge variant={getLevelVariant(group.level)} className="shrink-0 w-14 justify-center">
                      {group.level}
                    </Badge>
                    <span className="text-xs text-gray-500 shrink-0 w-28 font-mono">
                      {formatTimestamp(group.startTime)}
                    </span>
                    {viewMode === 'business' ? (
                      /* Smart summary: Service > action > detail */
                      (() => {
                        const smart = formatSmartSummary(group);
                        return (
                          <span className="flex-1 flex items-center gap-1.5 truncate text-sm">
                            <span className="font-medium text-gray-800">{smart.service}</span>
                            <span className="text-gray-400">&rsaquo;</span>
                            <span className="text-gray-600">{smart.action}</span>
                            <span className="text-gray-400">&rsaquo;</span>
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
                        <span className="text-xs text-gray-400 shrink-0 w-24 truncate">
                          {group.service.replace('Invekto.', '')}
                        </span>
                        <span className="flex-1 truncate text-sm text-gray-700">
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
                        <span className="text-xs text-gray-400 font-mono">
                          <Layers className="w-3 h-3 inline mr-0.5" />
                          {group.entryCount}
                        </span>
                      )}
                      {isExpanded ? (
                        <ChevronUp className="w-4 h-4 text-gray-400" />
                      ) : (
                        <ChevronDown className="w-4 h-4 text-gray-400" />
                      )}
                    </div>
                  </button>

                  {/* Expanded: operation timeline */}
                  {isExpanded && (
                    <div className="px-4 py-3 bg-gray-50 border-t border-gray-200 text-sm">
                      {/* Operation header */}
                      <div className="flex items-center gap-2 mb-3">
                        <span className="text-xs text-gray-500">İşlem:</span>
                        <span className="font-mono text-xs text-gray-700 break-all select-all">
                          {group.requestId}
                        </span>
                        {group.route && (
                          <>
                            <span className="text-xs text-gray-400">|</span>
                            <span className="font-mono text-xs text-gray-500">{group.route}</span>
                          </>
                        )}
                      </div>

                      {/* Timeline table */}
                      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
                        <table className="w-full text-xs font-mono">
                          <thead>
                            <tr className="bg-gray-100 text-gray-500">
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
                                    "border-t border-gray-100",
                                    entry.level === 'ERROR' ? "bg-red-50/50" :
                                    entry.level === 'WARN' ? "bg-amber-50/50" : ""
                                  )}
                                >
                                  <td className="px-3 py-1.5 text-gray-400 whitespace-nowrap align-top">
                                    {formatTimeWithMs(entry.timestamp)}
                                  </td>
                                  <td className="px-3 py-1.5 text-gray-700 align-top">
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
                                      <span className="text-gray-300">-</span>
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

