import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../hooks/useAuth';
import { useFlowMonitorStore } from '../stores/flow-monitor-store';
import { getNodeTypeInfo } from '../types/flow';
import type { MonitorExecutionSummary, FlowExecutionDetail, NodeTraceEntry } from '../types/flow';
import type { WizardMessage, WizardOption } from '../types/wizard';
import { cn } from '../lib/utils';

const POLL_INTERVAL = 5000;

const STATUS_CONFIG: Record<string, { label: string; bg: string; text: string; dot: string }> = {
  running:    { label: 'Çalışıyor',   bg: 'bg-amber-100',   text: 'text-amber-700',   dot: 'bg-amber-500' },
  completed:  { label: 'Tamamlandı',  bg: 'bg-emerald-100', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  error:      { label: 'Hata',        bg: 'bg-red-100',     text: 'text-red-700',     dot: 'bg-red-500' },
  handed_off: { label: 'Devredildi',  bg: 'bg-sky-100',     text: 'text-sky-700',     dot: 'bg-sky-500' },
  waiting:    { label: 'Bekliyor',    bg: 'bg-navy-100',    text: 'text-navy-500',    dot: 'bg-navy-400' },
};

const STATUS_OPTIONS = [
  { value: '', label: 'Tüm Durumlar' },
  { value: 'running', label: 'Çalışıyor' },
  { value: 'completed', label: 'Tamamlandı' },
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
    return <div className="p-8 text-center text-navy-400">Tenant bilgisi bulunamadı.</div>;
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

        {/* Right panel: AI Chat */}
        <div className="w-[320px] flex-shrink-0 border-l border-slate-200 bg-white flex flex-col overflow-hidden">
          <MonitorAiPanel
            tenantId={tenantId}
            selectedExecution={selectedExecution}
          />
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
      <span className="text-xs text-slate-400">{total} kayıt</span>

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
        <div className="text-xs font-medium text-slate-500">Son Yürütmeler</div>
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
        <div className="px-3 py-12 text-center text-sm text-slate-400">Henüz yürütme kaydı yok</div>
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
            Önceki
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
          <p className="text-sm text-slate-400">Detay görmek için sol listeden bir yürütme seçin</p>
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
          title="Listeye dön"
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
          Node İzleme ({detail.node_trace.length} adım)
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
          <div className="text-[10px] text-red-400 font-medium mb-0.5">Hata Detayı</div>
          <div className="text-sm text-red-700 font-mono whitespace-pre-wrap">{detail.error_detail}</div>
        </div>
      )}

      {/* Variables */}
      {detail.variables_final && Object.keys(detail.variables_final).length > 0 && (
        <div className="px-5 py-3 border-t border-slate-200 flex-shrink-0 max-h-40 overflow-y-auto bg-slate-50">
          <div className="text-[10px] text-slate-400 font-medium mb-1">Değişkenler (Final)</div>
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
            <div className="text-[9px] text-slate-400 mb-0.5">Değişkenler</div>
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

// ============================================================
// MonitorAiPanel
// ============================================================

interface AiPanelProps {
  tenantId: number;
  selectedExecution: FlowExecutionDetail | null;
}

function MonitorAiPanel({ tenantId, selectedExecution }: AiPanelProps) {
  const messages = useFlowMonitorStore(s => s.aiMessages);
  const isStreaming = useFlowMonitorStore(s => s.aiIsStreaming);
  const streamingText = useFlowMonitorStore(s => s.aiStreamingText);
  const pendingFlowConfig = useFlowMonitorStore(s => s.aiPendingFlowConfig);
  const pendingOptions = useFlowMonitorStore(s => s.aiPendingOptions);
  const aiError = useFlowMonitorStore(s => s.aiError);
  const isSaving = useFlowMonitorStore(s => s.aiIsSaving);
  const sendAiMessage = useFlowMonitorStore(s => s.sendAiMessage);
  const acceptAiChanges = useFlowMonitorStore(s => s.acceptAiChanges);
  const rejectAiChanges = useFlowMonitorStore(s => s.rejectAiChanges);

  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const proactiveSentRef = useRef<number | null>(null);

  // Auto-scroll on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, streamingText]);

  // Proactive analysis: auto-send when error/waiting execution is selected
  useEffect(() => {
    if (!selectedExecution) return;
    if (proactiveSentRef.current === selectedExecution.id) return;
    if (selectedExecution.status !== 'error' && selectedExecution.status !== 'waiting') return;

    proactiveSentRef.current = selectedExecution.id;
    const autoMsg = selectedExecution.status === 'error'
      ? 'Bu yürütme hata ile sonuçlandı. Hatanın nedenini analiz et ve çözüm öner.'
      : 'Bu yürütme bekleme durumunda kaldı. Neden bekliyor, analiz et.';
    sendAiMessage(tenantId, autoMsg);
  }, [selectedExecution, tenantId, sendAiMessage]);

  const handleSend = useCallback(() => {
    const text = input.trim();
    if (!text || isStreaming) return;
    setInput('');
    sendAiMessage(tenantId, text);
  }, [input, isStreaming, tenantId, sendAiMessage]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  }, [handleSend]);

  const handleOptionClick = useCallback((option: WizardOption) => {
    if (isStreaming) return;
    sendAiMessage(tenantId, option.label);
  }, [isStreaming, tenantId, sendAiMessage]);

  // No execution selected — show placeholder
  if (!selectedExecution) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <div className="text-center px-6">
          <AiSparkleIcon />
          <h3 className="text-sm font-semibold text-slate-700 mb-1">AI Asistan</h3>
          <p className="text-xs text-slate-400">Bir yürütme seçin, AI analiz ve iyileştirme önerileri sunacak.</p>
        </div>
      </div>
    );
  }

  return (
    <>
      {/* Header */}
      <div className="px-3 py-2 border-b border-slate-100 flex-shrink-0 flex items-center gap-2">
        <AiSparkleIcon size="sm" />
        <span className="text-xs font-medium text-slate-600">AI Asistan</span>
        <span className="text-[10px] text-slate-400 ml-auto">#{selectedExecution.id}</span>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto px-3 py-3 space-y-3">
        {messages.map((msg, i) => (
          <AiMessageBubble key={i} message={msg} onOptionClick={handleOptionClick} />
        ))}

        {/* Streaming text */}
        {isStreaming && streamingText && (
          <div className="flex gap-2">
            <div className="w-5 h-5 rounded-full bg-gradient-to-br from-violet-100 to-indigo-100 flex items-center justify-center flex-shrink-0 mt-0.5">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3 h-3 text-indigo-500">
                <path d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
              </svg>
            </div>
            <div className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap">{streamingText}</div>
          </div>
        )}

        {/* Streaming indicator */}
        {isStreaming && !streamingText && (
          <div className="flex items-center gap-2 text-xs text-slate-400">
            <div className="animate-spin w-3.5 h-3.5 border border-indigo-300 border-t-indigo-600 rounded-full" />
            <span>Düşünüyor...</span>
          </div>
        )}

        {/* Error */}
        {aiError && (
          <div className="px-3 py-2 bg-red-50 border border-red-100 rounded-md text-xs text-red-600">{aiError}</div>
        )}

        {/* Pending flow config — Accept/Reject buttons */}
        {pendingFlowConfig && (
          <div className="px-3 py-2.5 bg-violet-50 border border-violet-200 rounded-lg">
            <div className="text-xs font-medium text-violet-700 mb-2">AI akış değişikliği önerdi</div>
            <div className="flex gap-2">
              <button
                onClick={() => acceptAiChanges(tenantId)}
                disabled={isSaving}
                className="flex-1 text-xs font-medium px-3 py-1.5 bg-violet-600 text-white rounded-md hover:bg-violet-700 disabled:opacity-50 transition-colors"
              >
                {isSaving ? 'Kaydediliyor...' : 'Uygula'}
              </button>
              <button
                onClick={rejectAiChanges}
                disabled={isSaving}
                className="flex-1 text-xs font-medium px-3 py-1.5 bg-white text-slate-600 border border-slate-200 rounded-md hover:bg-slate-50 disabled:opacity-50 transition-colors"
              >
                Reddet
              </button>
            </div>
          </div>
        )}

        {/* Options buttons */}
        {pendingOptions && pendingOptions.length > 0 && !pendingFlowConfig && (
          <div className="space-y-1.5">
            {pendingOptions.map((opt, i) => (
              <button
                key={i}
                onClick={() => handleOptionClick(opt)}
                disabled={isStreaming}
                className="w-full text-left px-3 py-2 text-xs bg-slate-50 border border-slate-200 rounded-md hover:bg-slate-100 hover:border-slate-300 disabled:opacity-50 transition-colors"
              >
                <span className="font-medium text-slate-700">{opt.label}</span>
                {opt.description && <span className="text-slate-400 ml-1">— {opt.description}</span>}
              </button>
            ))}
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="px-3 py-2.5 border-t border-slate-100 flex-shrink-0">
        <div className="flex gap-2">
          <textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Soru sor veya değişiklik iste..."
            rows={1}
            className="flex-1 text-xs border border-slate-200 rounded-md px-2.5 py-1.5 bg-white text-slate-700 placeholder-slate-400 focus:outline-none focus:ring-1 focus:ring-indigo-400 resize-none"
            disabled={isStreaming}
          />
          <button
            onClick={handleSend}
            disabled={isStreaming || !input.trim()}
            className="p-1.5 rounded-md bg-indigo-500 text-white hover:bg-indigo-600 disabled:opacity-40 transition-colors"
            title="Gönder"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
              <path d="M22 2L11 13" /><path d="M22 2l-7 20-4-9-9-4 20-7z" />
            </svg>
          </button>
        </div>
      </div>
    </>
  );
}

// ============================================================
// AiMessageBubble
// ============================================================

function AiMessageBubble({ message, onOptionClick }: { message: WizardMessage; onOptionClick: (opt: WizardOption) => void }) {
  if (message.role === 'user') {
    return (
      <div className="flex justify-end">
        <div className="max-w-[85%] px-3 py-1.5 bg-indigo-50 border border-indigo-100 rounded-lg rounded-br-sm text-xs text-indigo-800 whitespace-pre-wrap">
          {message.content}
        </div>
      </div>
    );
  }

  return (
    <div className="flex gap-2">
      <div className="w-5 h-5 rounded-full bg-gradient-to-br from-violet-100 to-indigo-100 flex items-center justify-center flex-shrink-0 mt-0.5">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3 h-3 text-indigo-500">
          <path d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
        </svg>
      </div>
      <div className="flex-1 min-w-0">
        <div className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap">{message.content}</div>
        {message.options && message.options.length > 0 && (
          <div className="mt-2 space-y-1">
            {message.options.map((opt, i) => (
              <button
                key={i}
                onClick={() => onOptionClick(opt)}
                className="w-full text-left px-2.5 py-1.5 text-[11px] bg-slate-50 border border-slate-200 rounded hover:bg-slate-100 transition-colors"
              >
                <span className="font-medium text-slate-700">{opt.label}</span>
                {opt.description && <span className="text-slate-400 ml-1">— {opt.description}</span>}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// ============================================================
// AiSparkleIcon
// ============================================================

function AiSparkleIcon({ size = 'lg' }: { size?: 'sm' | 'lg' }) {
  const wrapperCn = size === 'sm'
    ? 'w-5 h-5 rounded-md'
    : 'w-12 h-12 mx-auto mb-3 rounded-xl';
  const iconCn = size === 'sm' ? 'w-3 h-3' : 'w-6 h-6';

  return (
    <div className={cn(wrapperCn, 'bg-gradient-to-br from-violet-100 to-indigo-100 flex items-center justify-center')}>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" className={cn(iconCn, 'text-indigo-500')}>
        <path d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
        <path d="M18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456z" />
      </svg>
    </div>
  );
}
