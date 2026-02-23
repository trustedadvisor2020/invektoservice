import { create } from 'zustand';
import { api } from '../lib/api';
import type { FlowExecutionSummary, FlowExecutionDetail } from '../types/flow';

export interface FlowLogState {
  isOpen: boolean;
  isLoading: boolean;
  executions: FlowExecutionSummary[];
  total: number;
  selectedExecution: FlowExecutionDetail | null;
  isLoadingDetail: boolean;
  error: string | null;

  open: () => void;
  close: () => void;
  loadExecutions: (tenantId: number, flowId: number) => Promise<void>;
  selectExecution: (tenantId: number, flowId: number, logId: number) => Promise<void>;
  clearSelection: () => void;
  refresh: (tenantId: number, flowId: number) => Promise<void>;
}

export const useFlowLogStore = create<FlowLogState>((set, get) => ({
  isOpen: false,
  isLoading: false,
  executions: [],
  total: 0,
  selectedExecution: null,
  isLoadingDetail: false,
  error: null,

  open: () => set({ isOpen: true }),
  close: () => set({ isOpen: false, selectedExecution: null, error: null }),

  loadExecutions: async (tenantId, flowId) => {
    set({ isLoading: true, error: null });
    try {
      const res = await api.getFlowExecutions(tenantId, flowId, { limit: 50 });
      set({ executions: res.items, total: res.total, isLoading: false });
    } catch (err) {
      const msg = err instanceof Error ? err.message : '[INV-AT-041] Log listesi yuklenemedi';
      set({ error: msg, isLoading: false });
    }
  },

  selectExecution: async (tenantId, flowId, logId) => {
    set({ isLoadingDetail: true });
    try {
      const detail = await api.getFlowExecution(tenantId, flowId, logId);
      set({ selectedExecution: detail, isLoadingDetail: false });
    } catch (err) {
      const msg = err instanceof Error ? err.message : '[INV-AT-041] Log detayi yuklenemedi';
      set({ error: msg, isLoadingDetail: false });
    }
  },

  clearSelection: () => set({ selectedExecution: null }),

  refresh: async (tenantId, flowId) => {
    await get().loadExecutions(tenantId, flowId);
  },
}));
