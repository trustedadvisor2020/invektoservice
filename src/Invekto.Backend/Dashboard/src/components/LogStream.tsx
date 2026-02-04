import { useState, useEffect, useCallback } from 'react';
import { Search, Filter, RefreshCw, ChevronDown, ChevronUp } from 'lucide-react';
import type { LogEntry, LogContextResponse } from '../lib/api';
import { api } from '../lib/api';
import { Card, CardHeader, CardTitle, CardContent } from './ui/Card';
import { Badge } from './ui/Badge';
import { Button } from './ui/Button';
import { Input } from './ui/Input';
import { Select } from './ui/Select';
import { formatTimestamp, cn } from '../lib/utils';

interface LogStreamProps {
  initialEntries?: LogEntry[];
  initialFilter?: {
    levels?: string[];
    service?: string;
    search?: string;
    after?: string;
  };
}

export function LogStream({ initialEntries = [], initialFilter }: LogStreamProps) {
  const [entries, setEntries] = useState<LogEntry[]>(initialEntries);
  const [isLoading, setIsLoading] = useState(false);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [context, setContext] = useState<LogContextResponse | null>(null);
  const [contextLoading, setContextLoading] = useState(false);

  // Filters
  const [levels, setLevels] = useState<string[]>(initialFilter?.levels || ['ERROR', 'WARN', 'INFO']);
  const [service, setService] = useState(initialFilter?.service || '');
  const [search, setSearch] = useState(initialFilter?.search || '');

  const fetchLogs = useCallback(async () => {
    setIsLoading(true);
    try {
      const response = await api.getLogs({
        level: levels,
        service: service || undefined,
        search: search || undefined,
        limit: 100,
      });
      setEntries(response.entries);
    } catch (error) {
      console.error('Failed to fetch logs:', error);
    } finally {
      setIsLoading(false);
    }
  }, [levels, service, search]);

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

  const handleExpand = async (entry: LogEntry, index: number) => {
    const id = entry.id || `${index}`;
    if (expandedId === id) {
      setExpandedId(null);
      setContext(null);
      return;
    }

    setExpandedId(id);
    setContextLoading(true);

    try {
      const parts = entry.id?.split('_line_') || [];
      if (parts.length === 2) {
        const file = `${parts[0]}.jsonl`;
        const line = parseInt(parts[1], 10);
        const ctx = await api.getLogContext(file, line, 5);
        setContext(ctx);
      }
    } catch (error) {
      console.error('Failed to fetch context:', error);
    } finally {
      setContextLoading(false);
    }
  };

  const getLevelVariant = (level: string): 'error' | 'warning' | 'info' => {
    switch (level) {
      case 'ERROR': return 'error';
      case 'WARN': return 'warning';
      default: return 'info';
    }
  };

  return (
    <Card className="h-full flex flex-col">
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between mb-4">
          <CardTitle className="flex items-center gap-2">
            <Filter className="w-4 h-4 flex-shrink-0" />
            <span>Log Stream</span>
          </CardTitle>
          <Button variant="ghost" size="sm" onClick={fetchLogs} disabled={isLoading}>
            <RefreshCw className={cn("w-4 h-4 flex-shrink-0", isLoading && "animate-spin")} />
          </Button>
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
        {isLoading && entries.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-gray-500">
            Loading...
          </div>
        ) : entries.length === 0 ? (
          <div className="flex items-center justify-center h-32 text-gray-500">
            No logs found
          </div>
        ) : (
          <div className="space-y-1.5">
            {entries.map((entry, index) => {
              const id = entry.id || `${index}`;
              const isExpanded = expandedId === id;

              return (
                <div
                  key={id}
                  className={cn(
                    "border rounded-lg overflow-hidden transition-all duration-150",
                    isExpanded ? "border-blue-200 bg-blue-50/30" : "border-gray-200 hover:border-gray-300"
                  )}
                >
                  <button
                    className="w-full px-3 py-2.5 flex items-center gap-3 text-left hover:bg-gray-50 transition-colors"
                    onClick={() => handleExpand(entry, index)}
                  >
                    <Badge variant={getLevelVariant(entry.level)} className="shrink-0 w-14 justify-center">
                      {entry.level}
                    </Badge>
                    <span className="text-xs text-gray-500 shrink-0 w-28 font-mono">
                      {formatTimestamp(entry.timestamp)}
                    </span>
                    <span className="text-xs text-gray-400 shrink-0 w-24 truncate">
                      {entry.service.replace('Invekto.', '')}
                    </span>
                    <span className="flex-1 truncate text-sm text-gray-700">
                      {entry.message}
                    </span>
                    {isExpanded ? (
                      <ChevronUp className="w-4 h-4 text-gray-400 shrink-0" />
                    ) : (
                      <ChevronDown className="w-4 h-4 text-gray-400 shrink-0" />
                    )}
                  </button>

                  {isExpanded && (
                    <div className="px-4 py-3 bg-gray-50 border-t border-gray-200 text-sm">
                      <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-3">
                        {entry.requestId && entry.requestId !== '-' && (
                          <div>
                            <span className="text-xs text-gray-500">Request ID</span>
                            <p className="font-mono text-xs text-gray-700 truncate">{entry.requestId}</p>
                          </div>
                        )}
                        {entry.route && (
                          <div>
                            <span className="text-xs text-gray-500">Route</span>
                            <p className="font-mono text-xs text-gray-700">{entry.route}</p>
                          </div>
                        )}
                        {entry.durationMs !== undefined && (
                          <div>
                            <span className="text-xs text-gray-500">Duration</span>
                            <p className="text-gray-700">{entry.durationMs}ms</p>
                          </div>
                        )}
                        {entry.errorCode && (
                          <div>
                            <span className="text-xs text-gray-500">Error Code</span>
                            <Badge variant="error">{entry.errorCode}</Badge>
                          </div>
                        )}
                      </div>

                      {/* Context window */}
                      {contextLoading ? (
                        <div className="text-gray-500 text-center py-3">Loading context...</div>
                      ) : context && (
                        <div className="mt-3 pt-3 border-t border-gray-200">
                          <div className="text-xs text-gray-500 mb-2">Context (+-5 lines)</div>
                          <div className="space-y-0.5 font-mono text-xs bg-white rounded-lg border border-gray-200 p-2">
                            {context.before.map((ctx, i) => (
                              <div key={`before-${i}`} className="text-gray-500 truncate py-0.5">
                                {ctx.message}
                              </div>
                            ))}
                            <div className="bg-amber-100 px-2 py-1 -mx-2 text-amber-800 truncate font-medium">
                              {context.target.message}
                            </div>
                            {context.after.map((ctx, i) => (
                              <div key={`after-${i}`} className="text-gray-500 truncate py-0.5">
                                {ctx.message}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}
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
