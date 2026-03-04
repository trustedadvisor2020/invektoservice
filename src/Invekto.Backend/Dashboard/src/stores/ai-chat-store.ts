import { create } from 'zustand';
import type { FlowConfigV2 } from '../types/flow';
import type { WizardMessage, WizardOption } from '../types/wizard';
import { streamMessage, getWizardState } from '../lib/wizard-api';

/** Check if a JSON string looks like FlowConfigV2 */
function isFlowConfigJson(json: string): boolean {
  try {
    const obj = JSON.parse(json);
    return obj?.version === 2 && Array.isArray(obj?.nodes) && Array.isArray(obj?.edges);
  } catch { return false; }
}

/** Strip ```options, ```flowconfig, and FlowConfigV2-containing ```json blocks from text */
function stripCodeBlocks(text: string): string {
  return text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m)
    .trimEnd();
}

/** Strip only incomplete (unterminated) code blocks from streaming text */
function stripStreamingBlocks(text: string): string {
  let clean = text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m);
  // Strip incomplete block at the end (started but not closed)
  clean = clean.replace(/```(?:options|flowconfig|json)\s*[\s\S]*$/g, '');
  return clean.trimEnd();
}

interface AiChatStore {
  isOpen: boolean;
  flowId: number | null;
  tenantId: number;
  messages: WizardMessage[];
  isStreaming: boolean;
  streamingText: string;
  pendingFlowConfig: FlowConfigV2 | null;
  pendingOptions: WizardOption[] | null;
  error: string | null;
  /** Set to true when AI generates a new flow_config — triggers auto-apply in AiChatPanel */
  autoApplyPending: boolean;

  open: (flowId: number, tenantId: number) => Promise<void>;
  close: () => void;
  sendMessage: (message: string, currentFlowConfig: FlowConfigV2) => Promise<void>;
  acceptChanges: () => FlowConfigV2 | null;
  rejectChanges: () => void;
  clearAutoApply: () => void;
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
  pendingOptions: null,
  error: null,
  autoApplyPending: false,

  open: async (flowId: number, tenantId: number) => {
    const state = get();
    // Already open for this flow — just toggle visibility
    if (state.flowId === flowId && state.messages.length > 0) {
      set({ isOpen: true });
      return;
    }

    set({ isOpen: true, flowId, tenantId, messages: [], error: null, pendingFlowConfig: null, pendingOptions: null });

    // Load existing wizard_history if any
    try {
      const data = await getWizardState(tenantId, flowId);
      const raw = Array.isArray(data.wizard_history) ? data.wizard_history : [];
      // Normalize: handle both PascalCase (legacy DB) and camelCase property names
      const history: WizardMessage[] = raw.map((m: Record<string, unknown>) => ({
        role: (m.role ?? m.Role ?? 'user') as WizardMessage['role'],
        content: (m.content ?? m.Content ?? '') as string,
        timestamp: (m.timestamp ?? m.Timestamp ?? '') as string,
        flow_config_snapshot: (m.flow_config_snapshot ?? m.FlowConfigSnapshot) as WizardMessage['flow_config_snapshot'],
        options: (m.options ?? m.Options) as WizardMessage['options'],
      }));
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
      pendingOptions: null,
      error: null,
    });

    let fullText = '';
    try {
      for await (const event of streamMessage(flowId, tenantId, message, undefined, currentFlowConfig)) {
        if (event.type === 'text') {
          fullText += event.content || '';
          // Strip options/flowconfig blocks from live streaming display
          set({ streamingText: stripStreamingBlocks(fullText) });
        } else if (event.type === 'error') {
          set({ error: event.content || 'AI servisi yanit veremedi. Lutfen tekrar deneyin.', isStreaming: false });
          return;
        } else if (event.type === 'done') {
          // Prefer clean content from backend (options block already stripped)
          const cleanContent = event.content || stripCodeBlocks(fullText);
          const assistantMsg: WizardMessage = {
            role: 'assistant',
            content: cleanContent,
            timestamp: new Date().toISOString(),
            flow_config_snapshot: event.flow_config,
            options: event.options,
          };

          const hasNewFlowConfig = !!event.flow_config;
          set(state => ({
            messages: [...state.messages, assistantMsg],
            isStreaming: false,
            streamingText: '',
            pendingFlowConfig: event.flow_config || state.pendingFlowConfig,
            pendingOptions: event.options ?? null,
            autoApplyPending: hasNewFlowConfig,
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

  rejectChanges: () => set({ pendingFlowConfig: null, autoApplyPending: false }),

  clearAutoApply: () => set({ autoApplyPending: false }),

  reset: () => set({
    messages: [],
    isStreaming: false,
    streamingText: '',
    pendingFlowConfig: null,
    pendingOptions: null,
    error: null,
    autoApplyPending: false,
  }),
}));
