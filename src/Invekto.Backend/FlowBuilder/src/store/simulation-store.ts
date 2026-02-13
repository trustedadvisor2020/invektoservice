import { create } from 'zustand';
import {
  simulationStart,
  simulationStep,
  simulationCleanup,
  type SimulationMessage,
  type SimulationPendingInput,
  ApiClientError,
} from '../lib/api';

export interface SimulationState {
  // Session state
  isOpen: boolean;
  isLoading: boolean;
  sessionId: string | null;
  messages: SimulationMessage[];
  currentNodeId: string | null;
  variables: Record<string, string>;
  executionPath: string[];
  status: string | null; // 'active' | 'completed' | 'error' | 'handed_off'
  pendingInput: SimulationPendingInput | null;
  error: string | null;

  // Actions
  open: () => void;
  close: () => void;
  start: (tenantId: number, flowId: number) => Promise<void>;
  sendMessage: (message: string) => Promise<void>;
  reset: () => void;
}

export const useSimulationStore = create<SimulationState>((set, get) => ({
  isOpen: false,
  isLoading: false,
  sessionId: null,
  messages: [],
  currentNodeId: null,
  variables: {},
  executionPath: [],
  status: null,
  pendingInput: null,
  error: null,

  open: () => set({ isOpen: true }),

  close: () => {
    const { sessionId } = get();
    // Fire-and-forget cleanup — session will expire via 30min TTL if this fails
    if (sessionId) {
      simulationCleanup(sessionId).catch((err) => {
        console.warn('[SimulationStore] cleanup failed (session will expire via TTL):', err);
      });
    }
    set({
      isOpen: false,
      sessionId: null,
      messages: [],
      currentNodeId: null,
      variables: {},
      executionPath: [],
      status: null,
      pendingInput: null,
      error: null,
      isLoading: false,
    });
  },

  start: async (tenantId, flowId) => {
    set({ isLoading: true, error: null, messages: [] });

    try {
      const res = await simulationStart(tenantId, flowId);
      set({
        sessionId: res.session_id,
        messages: res.messages,
        currentNodeId: res.current_node_id,
        variables: res.variables,
        executionPath: res.execution_path,
        status: res.status,
        pendingInput: res.pending_input,
        isLoading: false,
        isOpen: true,
      });
    } catch (err) {
      const msg = err instanceof ApiClientError
        ? `[${err.errorCode}] ${err.message}`
        : err instanceof Error ? err.message : 'Simulasyon baslatma hatasi';
      set({ error: msg, isLoading: false });
    }
  },

  sendMessage: async (message) => {
    const { sessionId, messages } = get();
    if (!sessionId) {
      console.warn('[SimulationStore] sendMessage called without active session');
      return;
    }

    set({ isLoading: true, error: null });

    try {
      const res = await simulationStep(sessionId, message);
      set({
        messages: [...messages, ...res.messages],
        currentNodeId: res.current_node_id,
        variables: res.variables,
        executionPath: res.execution_path,
        status: res.status,
        pendingInput: res.pending_input,
        isLoading: false,
      });
    } catch (err) {
      const msg = err instanceof ApiClientError
        ? `[${err.errorCode}] ${err.message}`
        : err instanceof Error ? err.message : 'Simulasyon adim hatasi';
      set({ error: msg, isLoading: false });
    }
  },

  reset: () => {
    const { sessionId } = get();
    if (sessionId) {
      simulationCleanup(sessionId).catch((err) => {
        console.warn('[SimulationStore] reset cleanup failed (session will expire via TTL):', err);
      });
    }
    set({
      sessionId: null,
      messages: [],
      currentNodeId: null,
      variables: {},
      executionPath: [],
      status: null,
      pendingInput: null,
      error: null,
      isLoading: false,
    });
  },
}));
