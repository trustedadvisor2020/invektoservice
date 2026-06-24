import { useRef, useState, useCallback, useEffect, type MouseEvent as ReactMouseEvent } from 'react';
import { useFlowLogStore } from '../../../stores/flow-log-store';
import { getNodeTypeInfo } from '../../../types/flow';
import type { FlowExecutionSummary, NodeTraceEntry } from '../../../types/flow';
import { cn } from '../../../lib/utils';

const MIN_WIDTH = 260;
const MAX_WIDTH = 480;
const DEFAULT_WIDTH = 320;
const STORAGE_KEY = 'chatinbox_flow_log_width';

function getStoredWidth(): number {
  const v = localStorage.getItem(STORAGE_KEY);
  if (v) {
    const n = parseInt(v, 10);
    if (n >= MIN_WIDTH && n <= MAX_WIDTH) return n;
  }
  return DEFAULT_WIDTH;
}

function maskPhone(phone: string | null): string {
  if (!phone) return '-';
  if (phone.length <= 6) return phone;
  return phone.slice(0, 4) + '***' + phone.slice(-4);
}

function formatTime(iso: string): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '-';
  return d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function formatDate(iso: string): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '-';
  return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' }) + ' ' + formatTime(iso);
}

const STATUS_CONFIG: Record<string, { label: string; bg: string; text: string }> = {
  running: { label: 'Çalışıyor', bg: 'bg-amber-100', text: 'text-amber-700' },
  completed: { label: 'Tamamlandı', bg: 'bg-emerald-100', text: 'text-emerald-700' },
  error: { label: 'Hata', bg: 'bg-red-100', text: 'text-red-700' },
  handed_off: { label: 'Devredildi', bg: 'bg-sky-100', text: 'text-sky-700' },
  waiting: { label: 'Bekliyor', bg: 'bg-navy-100', text: 'text-navy-500' },
};

interface FlowLogPanelProps {
  tenantId: number;
  flowId: number;
}

export function FlowLogPanel({ tenantId, flowId }: FlowLogPanelProps) {
  const isOpen = useFlowLogStore(s => s.isOpen);
  const isLoading = useFlowLogStore(s => s.isLoading);
  const executions = useFlowLogStore(s => s.executions);
  const total = useFlowLogStore(s => s.total);
  const selectedExecution = useFlowLogStore(s => s.selectedExecution);
  const isLoadingDetail = useFlowLogStore(s => s.isLoadingDetail);
  const error = useFlowLogStore(s => s.error);
  const close = useFlowLogStore(s => s.close);
  const loadExecutions = useFlowLogStore(s => s.loadExecutions);
  const selectExecution = useFlowLogStore(s => s.selectExecution);
  const clearSelection = useFlowLogStore(s => s.clearSelection);
  const refresh = useFlowLogStore(s => s.refresh);

  const [width, setWidth] = useState(getStoredWidth);
  const dragging = useRef(false);
  const startX = useRef(0);
  const startW = useRef(0);

  // Load executions when panel opens
  useEffect(() => {
    if (isOpen && tenantId > 0 && flowId > 0) {
      loadExecutions(tenantId, flowId);
    }
  }, [isOpen, tenantId, flowId, loadExecutions]);

  // Resize drag handlers
  const onDragStart = useCallback((e: ReactMouseEvent) => {
    e.preventDefault();
    dragging.current = true;
    startX.current = e.clientX;
    startW.current = width;

    const onMove = (ev: MouseEvent) => {
      if (!dragging.current) return;
      const delta = ev.clientX - startX.current;
      const next = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, startW.current + delta));
      setWidth(next);
    };
    const onUp = () => {
      dragging.current = false;
      localStorage.setItem(STORAGE_KEY, String(width));
      document.removeEventListener('mousemove', onMove);
      document.removeEventListener('mouseup', onUp);
    };
    document.addEventListener('mousemove', onMove);
    document.addEventListener('mouseup', onUp);
  }, [width]);

  if (!isOpen) return null;

  return (
    <div className="flex-shrink-0 border-r border-navy-100 flex flex-col bg-white" style={{ width }}>
      {/* Header */}
      <div className="flex items-center gap-2 px-3 py-2 bg-sky-600 text-white flex-shrink-0">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
          <path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2" />
          <rect x="8" y="2" width="8" height="4" rx="1" ry="1" />
          <line x1="9" y1="12" x2="15" y2="12" />
          <line x1="9" y1="16" x2="15" y2="16" />
        </svg>
        <span className="text-sm font-medium flex-1">Akış Logları</span>
        <span className="text-xs opacity-75">{total}</span>
        <button
          onClick={() => refresh(tenantId, flowId)}
          className="p-1 rounded hover:bg-sky-500 transition-colors"
          title="Yenile"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
            <path d="M1 4v6h6" /><path d="M23 20v-6h-6" />
            <path d="M20.49 9A9 9 0 0 0 5.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 0 1 3.51 15" />
          </svg>
        </button>
        <button
          onClick={close}
          className="p-1 rounded hover:bg-sky-500 transition-colors"
          title="Kapat"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
            <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto">
        {error && (
          <div className="px-3 py-2 text-xs text-red-600 bg-red-50 border-b border-red-100">{error}</div>
        )}

        {isLoading && (
          <div className="flex items-center justify-center py-8">
            <div className="animate-spin w-5 h-5 border-2 border-sky-300 border-t-sky-600 rounded-full" />
          </div>
        )}

        {!isLoading && executions.length === 0 && !error && (
          <div className="px-3 py-8 text-center text-sm text-navy-300">Henüz log yok</div>
        )}

        {/* Detail view */}
        {selectedExecution && (
          <ExecutionDetail
            detail={selectedExecution}
            isLoading={isLoadingDetail}
            onBack={clearSelection}
          />
        )}

        {/* List view */}
        {!selectedExecution && !isLoading && executions.length > 0 && (
          <div className="divide-y divide-navy-50">
            {executions.map(exec => (
              <ExecutionRow
                key={exec.id}
                exec={exec}
                onClick={() => selectExecution(tenantId, flowId, exec.id)}
              />
            ))}
          </div>
        )}
      </div>

      {/* Resize handle */}
      <div
        className="absolute top-0 right-0 w-1.5 h-full cursor-col-resize hover:bg-sky-200 transition-colors z-10"
        style={{ right: -3 }}
        onMouseDown={onDragStart}
      />
    </div>
  );
}

function ExecutionRow({ exec, onClick }: { exec: FlowExecutionSummary; onClick: () => void }) {
  const cfg = STATUS_CONFIG[exec.status] ?? STATUS_CONFIG.running;

  return (
    <button
      onClick={onClick}
      className="w-full text-left px-3 py-2.5 hover:bg-navy-25 transition-colors"
    >
      <div className="flex items-center gap-2 mb-1">
        <span className="text-xs text-navy-400">{formatDate(exec.started_at)}</span>
        <span className={cn('text-[10px] px-1.5 py-0.5 rounded-full font-medium', cfg.bg, cfg.text)}>
          {cfg.label}
        </span>
      </div>
      <div className="flex items-center gap-2 text-xs">
        <span className="text-navy-500 font-mono">{maskPhone(exec.phone)}</span>
        <span className="text-navy-300">{exec.node_count} node</span>
      </div>
      {exec.trigger_message && (
        <div className="text-xs text-navy-400 mt-0.5 truncate max-w-full">
          {exec.trigger_message.length > 60
            ? exec.trigger_message.slice(0, 60) + '...'
            : exec.trigger_message}
        </div>
      )}
    </button>
  );
}

function ExecutionDetail({
  detail,
  isLoading,
  onBack,
}: {
  detail: import('../../../types/flow').FlowExecutionDetail;
  isLoading: boolean;
  onBack: () => void;
}) {
  const cfg = STATUS_CONFIG[detail.status] ?? STATUS_CONFIG.running;

  return (
    <div className="flex flex-col h-full">
      {/* Back header */}
      <div className="flex items-center gap-2 px-3 py-2 border-b border-navy-100 flex-shrink-0">
        <button onClick={onBack} className="p-1 rounded hover:bg-navy-50 text-navy-400">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
        </button>
        <div className="flex-1 min-w-0">
          <div className="text-xs text-navy-400">{formatDate(detail.started_at)}</div>
          <div className="text-xs font-mono text-navy-500">{maskPhone(detail.phone)}</div>
        </div>
        <span className={cn('text-[10px] px-1.5 py-0.5 rounded-full font-medium', cfg.bg, cfg.text)}>
          {cfg.label}
        </span>
      </div>

      {isLoading && (
        <div className="flex items-center justify-center py-8">
          <div className="animate-spin w-5 h-5 border-2 border-sky-300 border-t-sky-600 rounded-full" />
        </div>
      )}

      {/* Trigger message */}
      {detail.trigger_message && (
        <div className="px-3 py-2 border-b border-navy-50 bg-navy-25">
          <div className="text-[10px] text-navy-300 mb-0.5">Tetikleyen mesaj</div>
          <div className="text-xs text-navy-600">{detail.trigger_message}</div>
        </div>
      )}

      {/* Node trace */}
      <div className="flex-1 overflow-y-auto px-3 py-2">
        <div className="text-[10px] text-navy-300 mb-2">Node izleme ({detail.node_trace.length})</div>
        <div className="space-y-1">
          {detail.node_trace.map((entry, i) => (
            <TraceNode key={i} entry={entry} index={i} />
          ))}
        </div>
      </div>

      {/* Error detail */}
      {detail.error_detail && (
        <div className="px-3 py-2 border-t border-red-100 bg-red-50 flex-shrink-0">
          <div className="text-[10px] text-red-400 mb-0.5">Hata</div>
          <div className="text-xs text-red-600">{detail.error_detail}</div>
        </div>
      )}

      {/* Variables */}
      {detail.variables_final && Object.keys(detail.variables_final).length > 0 && (
        <div className="px-3 py-2 border-t border-navy-100 flex-shrink-0 max-h-32 overflow-y-auto">
          <div className="text-[10px] text-navy-300 mb-1">Değişkenler</div>
          {Object.entries(detail.variables_final)
            .filter(([k]) => !k.startsWith('__'))
            .map(([k, v]) => (
              <div key={k} className="flex gap-2 text-xs">
                <span className="text-navy-400 font-mono">{k}:</span>
                <span className="text-navy-600 truncate">{v}</span>
              </div>
            ))}
        </div>
      )}
    </div>
  );
}

function TraceNode({ entry, index }: { entry: NodeTraceEntry; index: number }) {
  const typeInfo = getNodeTypeInfo(entry.node_type as import('../../../types/flow').FlowNodeType);
  const color = typeInfo?.color ?? '#6b7280';

  return (
    <div className="flex items-start gap-2 py-1">
      {/* Connector line + dot */}
      <div className="flex flex-col items-center flex-shrink-0 w-4">
        <div
          className="w-2.5 h-2.5 rounded-full border-2 flex-shrink-0"
          style={{ borderColor: color, backgroundColor: `${color}20` }}
        />
        {/* vertical line continues below */}
        <div className="w-0.5 flex-1 bg-navy-100 mt-0.5" style={{ minHeight: 8 }} />
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0 -mt-0.5">
        <div className="flex items-center gap-1.5">
          <span className="text-[10px] text-navy-300">#{index + 1}</span>
          <span className="text-xs font-medium text-navy-600 truncate">
            {entry.label ?? entry.node_type}
          </span>
        </div>
        <div className="flex items-center gap-2 text-[10px] text-navy-400">
          <span>{entry.node_type}</span>
          {entry.duration_ms != null && <span>{entry.duration_ms}ms</span>}
          {entry.exit_handle && entry.exit_handle !== 'default' && (
            <span className="text-navy-300">{entry.exit_handle}</span>
          )}
        </div>

        {/* User input */}
        {entry.user_input && (
          <div className="mt-1 px-2 py-1 bg-blue-50 border border-blue-100 rounded text-[11px] text-blue-700">
            <span className="text-blue-400 font-medium">Gelen:</span> {entry.user_input}
          </div>
        )}

        {/* Bot messages */}
        {entry.bot_messages && entry.bot_messages.length > 0 && (
          <div className="mt-1 space-y-0.5">
            {entry.bot_messages.map((msg, mi) => (
              <div key={mi} className="px-2 py-1 bg-emerald-50 border border-emerald-100 rounded text-[11px] text-emerald-700">
                <span className="text-emerald-400 font-medium">Giden:</span> {msg}
              </div>
            ))}
          </div>
        )}

        {/* Variables snapshot */}
        {entry.variables && Object.keys(entry.variables).length > 0 && (
          <div className="mt-1 px-2 py-1 bg-navy-25 border border-navy-100 rounded">
            <div className="text-[9px] text-navy-300 mb-0.5">Değişkenler</div>
            {Object.entries(entry.variables)
              .filter(([k]) => !k.startsWith('__'))
              .map(([k, v]) => (
                <div key={k} className="text-[10px] flex gap-1">
                  <span className="text-navy-400 font-mono">{k}:</span>
                  <span className="text-navy-600 truncate">{v}</span>
                </div>
              ))}
          </div>
        )}
      </div>
    </div>
  );
}
