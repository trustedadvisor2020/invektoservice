import { create } from 'zustand';
import { api } from '../lib/api';
import { streamMessage } from '../lib/wizard-api';
import type { MonitorExecutionSummary, MonitorFilters, FlowExecutionDetail, FlowConfigV2 } from '../types/flow';
import type { WizardMessage, WizardOption } from '../types/wizard';

interface FlowOption {
  flow_id: number;
  flow_name: string;
}

/** Check if a JSON string looks like FlowConfigV2 */
function isFlowConfigJson(json: string): boolean {
  try {
    const obj = JSON.parse(json);
    return obj?.version === 2 && Array.isArray(obj?.nodes) && Array.isArray(obj?.edges);
  } catch (_e: unknown) { return false; }
}

/** Strip code blocks from text for display */
function stripCodeBlocks(text: string): string {
  return text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m)
    .trimEnd();
}

/** Strip only incomplete code blocks from streaming text */
function stripStreamingBlocks(text: string): string {
  let clean = text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m);
  clean = clean.replace(/```(?:options|flowconfig|json)\s*[\s\S]*$/g, '');
  return clean.trimEnd();
}

export interface FlowMonitorState {
  // Data
  executions: MonitorExecutionSummary[];
  total: number;
  selectedExecution: FlowExecutionDetail | null;
  flows: FlowOption[];

  // UI state
  isLoading: boolean;
  isLoadingDetail: boolean;
  error: string | null;
  filters: MonitorFilters;
  page: number;

  // AI Chat state
  aiMessages: WizardMessage[];
  aiIsStreaming: boolean;
  aiStreamingText: string;
  aiPendingFlowConfig: FlowConfigV2 | null;
  aiPendingOptions: WizardOption[] | null;
  aiError: string | null;
  aiIsSaving: boolean;

  // Actions — monitor
  setFilters: (filters: Partial<MonitorFilters>) => void;
  setPage: (page: number) => void;
  loadExecutions: (tenantId: number) => Promise<void>;
  selectExecution: (tenantId: number, flowId: number, logId: number) => Promise<void>;
  clearSelection: () => void;
  loadFlows: (tenantId: number) => Promise<void>;

  // Actions — AI chat
  sendAiMessage: (tenantId: number, message: string) => Promise<void>;
  acceptAiChanges: (tenantId: number) => Promise<void>;
  rejectAiChanges: () => void;
  resetAiChat: () => void;
}

const PAGE_SIZE = 30;

export const useFlowMonitorStore = create<FlowMonitorState>((set, get) => ({
  executions: [],
  total: 0,
  selectedExecution: null,
  flows: [],
  isLoading: false,
  isLoadingDetail: false,
  error: null,
  filters: {},
  page: 0,

  // AI Chat initial state
  aiMessages: [],
  aiIsStreaming: false,
  aiStreamingText: '',
  aiPendingFlowConfig: null,
  aiPendingOptions: null,
  aiError: null,
  aiIsSaving: false,

  setFilters: (filters) => set((s) => ({ filters: { ...s.filters, ...filters }, page: 0 })),

  setPage: (page) => set({ page }),

  loadExecutions: async (tenantId) => {
    const { filters, page } = get();
    set({ isLoading: true, error: null });
    try {
      const res = await api.getMonitorExecutions(tenantId, {
        ...filters,
        limit: PAGE_SIZE,
        offset: page * PAGE_SIZE,
      });
      set({ executions: res.items, total: res.total, isLoading: false });
    } catch (err) {
      const msg = err instanceof Error ? err.message : '[INV-AT-049] Monitor verileri yüklenemedi';
      set({ error: msg, isLoading: false });
    }
  },

  selectExecution: async (tenantId, flowId, logId) => {
    set({ isLoadingDetail: true, aiMessages: [], aiStreamingText: '', aiPendingFlowConfig: null, aiPendingOptions: null, aiError: null });
    try {
      const detail = await api.getFlowExecution(tenantId, flowId, logId);
      set({ selectedExecution: detail, isLoadingDetail: false });
    } catch (err) {
      const msg = err instanceof Error ? err.message : '[INV-AT-041] Yürütme detayı yüklenemedi';
      set({ error: msg, isLoadingDetail: false });
    }
  },

  clearSelection: () => set({ selectedExecution: null, aiMessages: [], aiStreamingText: '', aiPendingFlowConfig: null, aiPendingOptions: null, aiError: null }),

  loadFlows: async (tenantId) => {
    try {
      const res = await api.listFlows(tenantId);
      const flows = res.map((f) => ({
        flow_id: f.flow_id,
        flow_name: f.flow_name,
      }));
      set({ flows });
    } catch (err) {
      console.warn('[INV-AT-049] Flow listesi yuklenemedi:', err instanceof Error ? err.message : err);
    }
  },

  // ---- AI Chat Actions ----

  sendAiMessage: async (tenantId, message) => {
    const { selectedExecution, aiMessages } = get();
    if (!selectedExecution) return;

    const flowId = selectedExecution.flow_id;
    const userMsg: WizardMessage = { role: 'user', content: message, timestamp: new Date().toISOString() };

    set({
      aiMessages: [...aiMessages, userMsg],
      aiIsStreaming: true,
      aiStreamingText: '',
      aiPendingOptions: null,
      aiError: null,
    });

    // Build execution context for the AI
    const executionDetail = {
      id: selectedExecution.id,
      status: selectedExecution.status,
      phone: selectedExecution.phone,
      started_at: selectedExecution.started_at,
      completed_at: selectedExecution.completed_at,
      trigger_message: selectedExecution.trigger_message,
      error_detail: selectedExecution.error_detail,
      node_trace: selectedExecution.node_trace,
      variables_final: selectedExecution.variables_final,
    };

    // Get flow config for context — fetch from Automation
    let flowConfig: object | undefined;
    try {
      const flowData = await api.getFlow(tenantId, flowId);
      if (flowData?.flow_config) flowConfig = flowData.flow_config;
    } catch (err: unknown) {
      console.warn('[FM-1c] Flow config fetch failed, continuing without config:', err instanceof Error ? err.message : err);
    }

    let fullText = '';
    try {
      for await (const event of streamMessage(flowId, tenantId, message, undefined, flowConfig, executionDetail)) {
        if (event.type === 'text') {
          fullText += event.content || '';
          set({ aiStreamingText: stripStreamingBlocks(fullText) });
        } else if (event.type === 'error') {
          set({ aiError: event.content || '[INV-AT-050] AI servisi yanıt veremedi.', aiIsStreaming: false });
          return;
        } else if (event.type === 'done') {
          const cleanContent = event.content || stripCodeBlocks(fullText);
          const assistantMsg: WizardMessage = {
            role: 'assistant',
            content: cleanContent,
            timestamp: new Date().toISOString(),
            flow_config_snapshot: event.flow_config,
            options: event.options,
          };

          set(state => ({
            aiMessages: [...state.aiMessages, assistantMsg],
            aiIsStreaming: false,
            aiStreamingText: '',
            aiPendingFlowConfig: event.flow_config || state.aiPendingFlowConfig,
            aiPendingOptions: event.options ?? null,
          }));
        }
      }
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') return;
      const msg = err instanceof Error ? err.message : '[INV-AT-051] AI bağlantısı kesildi.';
      set({ aiError: msg, aiIsStreaming: false });
    }
  },

  acceptAiChanges: async (tenantId) => {
    const { aiPendingFlowConfig, selectedExecution } = get();
    if (!aiPendingFlowConfig || !selectedExecution) return;

    set({ aiIsSaving: true });
    try {
      await api.updateFlow(tenantId, selectedExecution.flow_id, { flow_config: aiPendingFlowConfig });
      const successMsg: WizardMessage = {
        role: 'assistant',
        content: 'Değişiklikler başarıyla kaydedildi ve yeni sürüm oluşturuldu.',
        timestamp: new Date().toISOString(),
      };
      set(state => ({
        aiPendingFlowConfig: null,
        aiIsSaving: false,
        aiMessages: [...state.aiMessages, successMsg],
      }));
    } catch (err) {
      const msg = err instanceof Error ? err.message : '[INV-AT-052] AI değişiklik kaydetme başarısız.';
      set({ aiError: msg, aiIsSaving: false });
    }
  },

  rejectAiChanges: () => set({ aiPendingFlowConfig: null }),

  resetAiChat: () => set({
    aiMessages: [],
    aiIsStreaming: false,
    aiStreamingText: '',
    aiPendingFlowConfig: null,
    aiPendingOptions: null,
    aiError: null,
    aiIsSaving: false,
  }),
}));
