import { create } from 'zustand';
import type { FlowConfigV2 } from '../types/flow';
import type { WizardMessage } from '../types/wizard';
import { streamMessage, getWizardState } from '../lib/wizard-api';

interface AiChatStore {
  isOpen: boolean;
  flowId: number | null;
  tenantId: number;
  messages: WizardMessage[];
  isStreaming: boolean;
  streamingText: string;
  pendingFlowConfig: FlowConfigV2 | null;
  error: string | null;

  open: (flowId: number, tenantId: number) => Promise<void>;
  close: () => void;
  sendMessage: (message: string, currentFlowConfig: FlowConfigV2) => Promise<void>;
  acceptChanges: () => FlowConfigV2 | null;
  rejectChanges: () => void;
  reset: () => void;
}

export const useAiChatStore = create<AiChatStore>((set, get) => ({
  isOpen: false,
  flowId: null,
  tenantId: 0,
  messages: [],
  isStreaming: false,
  streamingText: '',
  pendingFlowConfig: null,
  error: null,

  open: async (flowId: number, tenantId: number) => {
    const state = get();
    // Already open for this flow — just toggle visibility
    if (state.flowId === flowId && state.messages.length > 0) {
      set({ isOpen: true });
      return;
    }

    set({ isOpen: true, flowId, tenantId, messages: [], error: null, pendingFlowConfig: null });

    // Load existing wizard_history if any
    try {
      const data = await getWizardState(tenantId, flowId);
      const history: WizardMessage[] = Array.isArray(data.wizard_history) ? data.wizard_history : [];
      set({ messages: history });
    } catch (err: unknown) {
      // 404 = no history yet (normal for non-wizard flows), other errors surface to user
      const isNotFound = err instanceof Error && err.message.includes('404');
      if (!isNotFound) {
        set({ error: err instanceof Error ? err.message : 'Sohbet gecmisi yuklenemedi. Yeni sohbet baslatildi.' });
      }
    }
  },

  close: () => set({ isOpen: false }),

  sendMessage: async (message: string, currentFlowConfig: FlowConfigV2) => {
    const { flowId, tenantId, messages } = get();
    if (!flowId || !tenantId) return;

    const userMsg: WizardMessage = {
      role: 'user',
      content: message,
      timestamp: new Date().toISOString(),
    };

    set({
      messages: [...messages, userMsg],
      isStreaming: true,
      streamingText: '',
      error: null,
    });

    let fullText = '';
    try {
      for await (const event of streamMessage(flowId, tenantId, message, undefined, currentFlowConfig)) {
        if (event.type === 'text') {
          fullText += event.content || '';
          set({ streamingText: fullText });
        } else if (event.type === 'error') {
          set({ error: event.content || 'AI servisi yanit veremedi. Lutfen tekrar deneyin.', isStreaming: false });
          return;
        } else if (event.type === 'done') {
          const assistantMsg: WizardMessage = {
            role: 'assistant',
            content: fullText || event.content || '',
            timestamp: new Date().toISOString(),
            flow_config_snapshot: event.flow_config,
          };

          set(state => ({
            messages: [...state.messages, assistantMsg],
            isStreaming: false,
            streamingText: '',
            pendingFlowConfig: event.flow_config || state.pendingFlowConfig,
          }));
        }
      }
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') return;
      const msg = err instanceof Error ? err.message : 'Baglanti kesildi. Tekrar deneyin.';
      set({ error: msg, isStreaming: false });
    }
  },

  acceptChanges: () => {
    const config = get().pendingFlowConfig;
    set({ pendingFlowConfig: null });
    return config;
  },

  rejectChanges: () => set({ pendingFlowConfig: null }),

  reset: () => set({
    messages: [],
    isStreaming: false,
    streamingText: '',
    pendingFlowConfig: null,
    error: null,
  }),
}));
