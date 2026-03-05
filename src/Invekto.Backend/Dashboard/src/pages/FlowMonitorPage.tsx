import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../hooks/useAuth';
import { useFlowMonitorStore } from '../stores/flow-monitor-store';
import { getNodeTypeInfo } from '../types/flow';
import type { MonitorExecutionSummary, FlowExecutionDetail, NodeTraceEntry } from '../types/flow';
import { cn } from '../lib/utils';

const POLL_INTERVAL = 5000;

const STATUS_CONFIG: Record<string, { label: string; bg: string; text: string; dot: string }> = {
  running:    { label: 'Calisiyor',   bg: 'bg-amber-100',   text: 'text-amber-700',   dot: 'bg-amber-500' },
  completed:  { label: 'Tamamlandi',  bg: 'bg-emerald-100', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  error:      { label: 'Hata',        bg: 'bg-red-100',     text: 'text-red-700',     dot: 'bg-red-500' },
  handed_off: { label: 'Devredildi',  bg: 'bg-sky-100',     text: 'text-sky-700',     dot: 'bg-sky-500' },
  waiting:    { label: 'Bekliyor',    bg: 'bg-navy-100',    text: 'text-navy-500',    dot: 'bg-navy-400' },
};

const STATUS_OPTIONS = [
  { value: '', label: 'Tum Durumlar' },
  { value: 'running', label: 'Calisiyor' },
  { value: 'completed', label: 'Tamamlandi' },
  { value: 'error', label: 'Hata' },
  { value: 'handed_off', label: 'Devredildi' },
  { value: 'waiting', label: 'Bekliyor' },
];

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

function formatDateTime(iso: string): string {
  if (!iso) return '-';
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '-';
  return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' }) + ' ' + formatTime(iso);
}

function formatDateInput(d: Date): string {
  return d.toISOString().split('T')[0];
}

export function FlowMonitorPage() {
  const { session } = useAuth();
  const tenantId = session?.tenantId ?? 0;

  const executions = useFlowMonitorStore(s => s.executions);
  const total = useFlowMonitorStore(s => s.total);
  const selectedExecution = useFlowMonitorStore(s => s.selectedExecution);
  const isLoading = useFlowMonitorStore(s => s.isLoading);
  const isLoadingDetail = useFlowMonitorStore(s => s.isLoadingDetail);
  const error = useFlowMonitorStore(s => s.error);
  const filters = useFlowMonitorStore(s => s.filters);
  const flows = useFlowMonitorStore(s => s.flows);
  const page = useFlowMonitorStore(s => s.page);
  const setFilters = useFlowMonitorStore(s => s.setFilters);
  const setPage = useFlowMonitorStore(s => s.setPage);
  const loadExecutions = useFlowMonitorStore(s => s.loadExecutions);
  const selectExecution = useFlowMonitorStore(s => s.selectExecution);
  const clearSelection = useFlowMonitorStore(s => s.clearSelection);
  const loadFlows = useFlowMonitorStore(s => s.loadFlows);

  // Load flows for dropdown on mount
  useEffect(() => {
    if (tenantId > 0) loadFlows(tenantId);
  }, [tenantId, loadFlows]);

  // Load executions when filters/page change
  useEffect(() => {
    if (tenantId > 0) loadExecutions(tenantId);
  }, [tenantId, filters, page, loadExecutions]);

  // 5s polling
  useEffect(() => {
    if (tenantId <= 0) return;
    const interval = setInterval(() => loadExecutions(tenantId), POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [tenantId, filters, page, loadExecutions]);

  if (tenantId <= 0) {
    return <div className="p-8 text-center text-navy-400">Tenant bilgisi bulunamadi.</div>;
  }

  return (
    <div className="flex flex-col h-full bg-slate-50">
      {/* Filter bar */}
      <MonitorFilterBar
        flows={flows}
        filters={filters}
        onFilterChange={setFilters}
        total={total}
        isLoading={isLoading}
        onRefresh={() => loadExecutions(tenantId)}
      />

      {/* Main 3-panel layout */}
      <div className="flex flex-1 overflow-hidden">
        {/* Left panel: Execution list */}
        <div className="w-[300px] flex-shrink-0 border-r border-slate-200 bg-white flex flex-col overflow-hidden">
          <ExecutionListPanel
            executions={executions}
            total={total}
            page={page}
            isLoading={isLoading}
            error={error}
            selectedId={selectedExecution?.id ?? null}
            onSelect={(exec) => selectExecution(tenantId, exec.flow_id, exec.id)}
            onPageChange={setPage}
          />
        </div>

        {/* Center panel: Timeline / Trace detail */}
        <div className="flex-1 overflow-y-auto">
          <ExecutionTimeline
            detail={selectedExecution}
            isLoading={isLoadingDetail}
            onBack={clearSelection}
          />
        </div>

        {/* Right panel: AI Chat placeholder (FM-1c) */}
        <div className="w-[300px] flex-shrink-0 border-l border-slate-200 bg-white flex flex-col items-center justify-center">
          <div className="text-center px-6">
            <div className="w-12 h-12 mx-auto mb-3 rounded-xl bg-gradient-to-br from-violet-100 to-indigo-100 flex items-center justify-center">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="w-6 h-6 text-indigo-500">
                <path d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
                <path d="M18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456z" />
              </svg>
            </div>
            <h3 className="text-sm font-semibold text-slate-700 mb-1">AI Asistan</h3>
            <p className="text-xs text-slate-400">Flow analizi ve iyilestirme onerileri icin AI asistan yaklasimda.</p>
          </div>
        </div>
      </div>
    </div>
  );
}

// ============================================================
// MonitorFilterBar
// ============================================================

interface FilterBarProps {
  flows: { flow_id: number; flow_name: string }[];
  filters: import('../types/flow').MonitorFilters;
  onFilterChange: (f: Partial<import('../types/flow').MonitorFilters>) => void;
  total: number;
  isLoading: boolean;
  onRefresh: () => void;
}

function MonitorFilterBar({ flows, filters, onFilterChange, total, isLoading, onRefresh }: FilterBarProps) {
  const [phoneInput, setPhoneInput] = useState(filters.phone ?? '');
  const phoneTimer = useRef<ReturnType<typeof setTimeout>>();

  const handlePhoneChange = useCallback((value: string) => {
    setPhoneInput(value);
    clearTimeout(phoneTimer.current);
    phoneTimer.current = setTimeout(() => {
      onFilterChange({ phone: value || undefined });
    }, 500);
  }, [onFilterChange]);

  return (
    <div className="flex items-center gap-3 px-4 py-2.5 bg-white border-b border-slate-200 flex-shrink-0">
      {/* Flow dropdown */}
      <select
        className="text-sm border border-slate-200 rounded-md px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-400 min-w-[180px]"
        value={filters.flow_id ?? ''}
        onChange={(e) => onFilterChange({ flow_id: e.target.value ? Number(e.target.value) : undefined })}
      >
        <option value="">Tum Flow&apos;lar</option>
        {flows.map(f => (
          <option key={f.flow_id} value={f.flow_id}>{f.flow_name}</option>
        ))}
      </select>

      {/* Status dropdown */}
      <select
        className="text-sm border border-slate-200 rounded-md px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-400"
        value={filters.status ?? ''}
        onChange={(e) => onFilterChange({ status: e.target.value || undefined })}
      >
        {STATUS_OPTIONS.map(o => (
          <option key={o.value} value={o.value}>{o.label}</option>
        ))}
      </select>

      {/* Date range */}
      <div className="flex items-center gap-1.5">
        <input
          type="date"
          className="text-sm border border-slate-200 rounded-md px-2 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-400"
          value={filters.date_from?.split('T')[0] ?? ''}
          onChange={(e) => onFilterChange({ date_from: e.target.value ? e.target.value + 'T00:00:00Z' : undefined })}
        />
        <span className="text-slate-300 text-xs">-</span>
        <input
          type="date"
          className="text-sm border border-slate-200 rounded-md px-2 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-400"
          value={filters.date_to?.split('T')[0] ?? ''}
          max={formatDateInput(new Date())}
          onChange={(e) => onFilterChange({ date_to: e.target.value ? e.target.value + 'T23:59:59Z' : undefined })}
        />
      </div>

      {/* Phone search */}
      <div className="relative">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2">
          <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
        </svg>
        <input
          type="text"
          placeholder="Telefon ara..."
          className="text-sm border border-slate-200 rounded-md pl-8 pr-2.5 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-sky-400 w-[140px]"
          value={phoneInput}
          onChange={(e) => handlePhoneChange(e.target.value)}
        />
      </div>

      <div className="flex-1" />

      {/* Total count */}
      <span className="text-xs text-slate-400">{total} kayit</span>

      {/* Refresh button */}
      <button
        onClick={onRefresh}
        disabled={isLoading}
        className="p-1.5 rounded-md hover:bg-slate-100 text-slate-500 transition-colors disabled:opacity-50"
        title="Yenile"
      >
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className={cn('w-4 h-4', isLoading && 'animate-spin')}>
          <path d="M1 4v6h6" /><path d="M23 20v-6h-6" />
          <path d="M20.49 9A9 9 0 0 0 5.64 5.64L1 10m22 4l-4.64 4.36A9 9 0 0 1 3.51 15" />
        </svg>
      </button>
    </div>
  );
}

// ============================================================
// ExecutionListPanel
// ============================================================

const PAGE_SIZE = 30;

interface ListPanelProps {
  executions: MonitorExecutionSummary[];
  total: number;
  page: number;
  isLoading: boolean;
  error: string | null;
  selectedId: number | null;
  onSelect: (exec: MonitorExecutionSummary) => void;
  onPageChange: (page: number) => void;
}

function ExecutionListPanel({ executions, total, page, isLoading, error, selectedId, onSelect, onPageChange }: ListPanelProps) {
  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <>
      {/* List header */}
      <div className="px-3 py-2 border-b border-slate-100 flex-shrink-0">
        <div className="text-xs font-medium text-slate-500">Son Yurutmeler</div>
      </div>

      {/* Error */}
      {error && (
        <div className="px-3 py-2 text-xs text-red-600 bg-red-50 border-b border-red-100">{error}</div>
      )}

      {/* Loading */}
      {isLoading && executions.length === 0 && (
        <div className="flex items-center justify-center py-12">
          <div className="animate-spin w-5 h-5 border-2 border-sky-300 border-t-sky-600 rounded-full" />
        </div>
      )}

      {/* Empty */}
      {!isLoading && executions.length === 0 && !error && (
        <div className="px-3 py-12 text-center text-sm text-slate-400">Henuz yurutme kaydI yok</div>
      )}

      {/* Execution list */}
      <div className="flex-1 overflow-y-auto">
        {executions.map(exec => {
          const cfg = STATUS_CONFIG[exec.status] ?? STATUS_CONFIG.running;
          const isSelected = exec.id === selectedId;

          return (
            <button
              key={exec.id}
              onClick={() => onSelect(exec)}
              className={cn(
                'w-full text-left px-3 py-2.5 border-b border-slate-50 transition-colors',
                isSelected ? 'bg-sky-50 border-l-2 border-l-sky-500' : 'hover:bg-slate-50'
              )}
            >
              <div className="flex items-center gap-2 mb-1">
                <span className="text-[10px] font-medium text-slate-500 truncate max-w-[120px]">{exec.flow_name}</span>
                <span className={cn('text-[10px] px-1.5 py-0.5 rounded-full font-medium', cfg.bg, cfg.text)}>
                  {cfg.label}
                </span>
              </div>
              <div className="flex items-center gap-2 text-xs">
                <span className="text-slate-500 font-mono">{maskPhone(exec.phone)}</span>
                <span className="text-slate-300">{exec.node_count} node</span>
                <span className="text-slate-300 ml-auto">{formatDateTime(exec.started_at)}</span>
              </div>
              {exec.trigger_message && (
                <div className="text-xs text-slate-400 mt-0.5 truncate">
                  {exec.trigger_message.length > 50 ? exec.trigger_message.slice(0, 50) + '...' : exec.trigger_message}
                </div>
              )}
            </button>
          );
        })}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between px-3 py-2 border-t border-slate-100 flex-shrink-0">
          <button
            disabled={page === 0}
            onClick={() => onPageChange(page - 1)}
            className="text-xs text-slate-500 hover:text-slate-700 disabled:opacity-30"
          >
            Onceki
          </button>
          <span className="text-xs text-slate-400">{page + 1} / {totalPages}</span>
          <button
            disabled={page >= totalPages - 1}
            onClick={() => onPageChange(page + 1)}
            className="text-xs text-slate-500 hover:text-slate-700 disabled:opacity-30"
          >
            Sonraki
          </button>
        </div>
      )}
    </>
  );
}

// ============================================================
// ExecutionTimeline
// ============================================================

interface TimelineProps {
  detail: FlowExecutionDetail | null;
  isLoading: boolean;
  onBack: () => void;
}

function ExecutionTimeline({ detail, isLoading, onBack }: TimelineProps) {
  if (!detail && !isLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="text-center">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className="w-10 h-10 text-slate-300 mx-auto mb-3">
            <path d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25H12" />
          </svg>
          <p className="text-sm text-slate-400">Detay gormek icin sol listeden bir yurutme secin</p>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="animate-spin w-6 h-6 border-2 border-sky-300 border-t-sky-600 rounded-full" />
      </div>
    );
  }

  if (!detail) return null;

  const cfg = STATUS_CONFIG[detail.status] ?? STATUS_CONFIG.running;

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center gap-3 px-5 py-3 border-b border-slate-200 bg-white flex-shrink-0">
        <button
          onClick={onBack}
          className="p-1 rounded hover:bg-slate-100 text-slate-400"
          title="Listeye don"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-4 h-4">
            <path d="M19 12H5M12 19l-7-7 7-7" />
          </svg>
        </button>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-slate-700">#{detail.id}</span>
            <span className={cn('text-[10px] px-1.5 py-0.5 rounded-full font-medium', cfg.bg, cfg.text)}>
              {cfg.label}
            </span>
          </div>
          <div className="flex items-center gap-3 text-xs text-slate-400 mt-0.5">
            <span>{formatDateTime(detail.started_at)}</span>
            {detail.completed_at && <span>- {formatTime(detail.completed_at)}</span>}
            <span className="font-mono">{maskPhone(detail.phone)}</span>
          </div>
        </div>
      </div>

      {/* Trigger message */}
      {detail.trigger_message && (
        <div className="px-5 py-2.5 bg-blue-50 border-b border-blue-100 flex-shrink-0">
          <div className="text-[10px] text-blue-400 font-medium mb-0.5">Tetikleyen Mesaj</div>
          <div className="text-sm text-blue-700">{detail.trigger_message}</div>
        </div>
      )}

      {/* Node trace timeline */}
      <div className="flex-1 overflow-y-auto px-5 py-4">
        <div className="text-xs font-medium text-slate-500 mb-3">
          Node Izleme ({detail.node_trace.length} adim)
        </div>
        <div className="space-y-0">
          {detail.node_trace.map((entry, i) => (
            <TimelineNode key={i} entry={entry} index={i} isLast={i === detail.node_trace.length - 1} />
          ))}
        </div>
      </div>

      {/* Error detail */}
      {detail.error_detail && (
        <div className="px-5 py-3 border-t border-red-200 bg-red-50 flex-shrink-0">
          <div className="text-[10px] text-red-400 font-medium mb-0.5">Hata Detayi</div>
          <div className="text-sm text-red-700 font-mono whitespace-pre-wrap">{detail.error_detail}</div>
        </div>
      )}

      {/* Variables */}
      {detail.variables_final && Object.keys(detail.variables_final).length > 0 && (
        <div className="px-5 py-3 border-t border-slate-200 flex-shrink-0 max-h-40 overflow-y-auto bg-slate-50">
          <div className="text-[10px] text-slate-400 font-medium mb-1">Degiskenler (Final)</div>
          <div className="grid grid-cols-2 gap-x-4 gap-y-0.5">
            {Object.entries(detail.variables_final)
              .filter(([k]) => !k.startsWith('__'))
              .map(([k, v]) => (
                <div key={k} className="flex gap-2 text-xs">
                  <span className="text-slate-500 font-mono">{k}:</span>
                  <span className="text-slate-700 truncate">{v}</span>
                </div>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}

// ============================================================
// TimelineNode
// ============================================================

function TimelineNode({ entry, index, isLast }: { entry: NodeTraceEntry; index: number; isLast: boolean }) {
  const typeInfo = getNodeTypeInfo(entry.node_type as import('../types/flow').FlowNodeType);
  const color = typeInfo?.color ?? '#6b7280';

  return (
    <div className="flex items-stretch gap-3">
      {/* Timeline connector */}
      <div className="flex flex-col items-center w-5 flex-shrink-0">
        <div
          className="w-3 h-3 rounded-full border-2 flex-shrink-0 mt-1"
          style={{ borderColor: color, backgroundColor: `${color}20` }}
        />
        {!isLast && (
          <div className="w-0.5 flex-1 bg-slate-200 mt-0.5" style={{ minHeight: 16 }} />
        )}
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0 pb-4">
        <div className="flex items-center gap-2">
          <span className="text-[10px] text-slate-300 font-mono">#{index + 1}</span>
          <span className="text-sm font-medium text-slate-700">{entry.label ?? entry.node_type}</span>
          <span className="text-[10px] text-slate-400 px-1.5 py-0.5 bg-slate-100 rounded">{entry.node_type}</span>
          {entry.duration_ms != null && (
            <span className="text-[10px] text-slate-400">{entry.duration_ms}ms</span>
          )}
          {entry.exit_handle && entry.exit_handle !== 'default' && (
            <span className="text-[10px] text-slate-400 italic">{entry.exit_handle}</span>
          )}
        </div>

        {/* User input */}
        {entry.user_input && (
          <div className="mt-1.5 px-3 py-1.5 bg-blue-50 border border-blue-100 rounded-md text-xs text-blue-700">
            <span className="text-blue-400 font-medium mr-1">Gelen:</span>{entry.user_input}
          </div>
        )}

        {/* Bot messages */}
        {entry.bot_messages && entry.bot_messages.length > 0 && (
          <div className="mt-1.5 space-y-1">
            {entry.bot_messages.map((msg, mi) => (
              <div key={mi} className="px-3 py-1.5 bg-emerald-50 border border-emerald-100 rounded-md text-xs text-emerald-700">
                <span className="text-emerald-400 font-medium mr-1">Giden:</span>{msg}
              </div>
            ))}
          </div>
        )}

        {/* Variables snapshot */}
        {entry.variables && Object.keys(entry.variables).length > 0 && (
          <div className="mt-1.5 px-3 py-1.5 bg-slate-50 border border-slate-100 rounded-md">
            <div className="text-[9px] text-slate-400 mb-0.5">Degiskenler</div>
            {Object.entries(entry.variables)
              .filter(([k]) => !k.startsWith('__'))
              .map(([k, v]) => (
                <div key={k} className="text-[10px] flex gap-1">
                  <span className="text-slate-500 font-mono">{k}:</span>
                  <span className="text-slate-600 truncate">{v}</span>
                </div>
              ))}
          </div>
        )}
      </div>
    </div>
  );
}
