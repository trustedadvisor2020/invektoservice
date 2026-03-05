import { create } from 'zustand';
import { api } from '../lib/api';
import type { MonitorExecutionSummary, MonitorFilters, FlowExecutionDetail } from '../types/flow';

interface FlowOption {
  flow_id: number;
  flow_name: string;
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

  // Actions
  setFilters: (filters: Partial<MonitorFilters>) => void;
  setPage: (page: number) => void;
  loadExecutions: (tenantId: number) => Promise<void>;
  selectExecution: (tenantId: number, flowId: number, logId: number) => Promise<void>;
  clearSelection: () => void;
  loadFlows: (tenantId: number) => Promise<void>;
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
      const msg = err instanceof Error ? err.message : '[INV-AT-049] Monitor verileri yuklenemedi';
      set({ error: msg, isLoading: false });
    }
  },

  selectExecution: async (tenantId, flowId, logId) => {
    set({ isLoadingDetail: true });
    try {
      const detail = await api.getFlowExecution(tenantId, flowId, logId);
      set({ selectedExecution: detail, isLoadingDetail: false });
    } catch (err) {
      const msg = err instanceof Error ? err.message : '[INV-AT-041] Yurutme detayi yuklenemedi';
      set({ error: msg, isLoadingDetail: false });
    }
  },

  clearSelection: () => set({ selectedExecution: null }),

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
}));
