import { create } from 'zustand';
import type { FlowConfigV2 } from '../types/flow';
import type { WizardMessage, WizardOption, FlowPrerequisite } from '../types/wizard';
import { startWizard, streamMessage, getWizardState, confirmWizard } from '../lib/wizard-api';

/** Strip completed and incomplete code blocks from streaming text */
function stripStreamingBlocks(text: string): string {
  let clean = text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*[\s\S]*?```/g, '');
  // Strip incomplete block at the end
  clean = clean.replace(/```(?:options|flowconfig|json)\s*[\s\S]*$/g, '');
  return clean.trimEnd();
}

interface WizardStore {
  flowId: number | null;
  tenantId: number;
  messages: WizardMessage[];
  isStreaming: boolean;
  streamingText: string;
  currentFlowPreview: FlowConfigV2 | null;
  previousFlowPreview: FlowConfigV2 | null;
  prerequisites: FlowPrerequisite[] | null;
  pendingOptions: WizardOption[] | null;
  wizardStatus: 'drafting' | 'completed' | null;
  error: string | null;
  flowName: string;

  initWizard: (tenantId: number) => Promise<number>;
  loadWizard: (tenantId: number, flowId: number) => Promise<void>;
  sendMessage: (message: string) => Promise<void>;
  confirmFlow: (name: string) => Promise<void>;
  setFlowName: (name: string) => void;
  reset: () => void;
}

export const useWizardStore = create<WizardStore>((set, get) => ({
  flowId: null,
  tenantId: 0,
  messages: [],
  isStreaming: false,
  streamingText: '',
  currentFlowPreview: null,
  previousFlowPreview: null,
  prerequisites: null,
  pendingOptions: null,
  wizardStatus: null,
  error: null,
  flowName: '',

  initWizard: async (tenantId: number) => {
    set({ error: null, tenantId });
    try {
      const result = await startWizard(tenantId);
      set({ flowId: result.flow_id, wizardStatus: 'drafting', messages: [] });
      return result.flow_id;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Wizard baslatilamadi. Sayfayi yenileyip tekrar deneyin.';
      set({ error: msg });
      throw err;
    }
  },

  loadWizard: async (tenantId: number, flowId: number) => {
    set({ error: null, tenantId, flowId });
    try {
      const data = await getWizardState(tenantId, flowId);
      const history: WizardMessage[] = Array.isArray(data.wizard_history) ? data.wizard_history : [];
      const lastSnapshot = [...history].reverse().find(m => m.flow_config_snapshot);

      set({
        messages: history,
        wizardStatus: data.wizard_status || 'drafting',
        flowName: data.flow_name || '',
        currentFlowPreview: lastSnapshot?.flow_config_snapshot || null,
      });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Wizard verisi yuklenemedi. Internet baglantinizi kontrol edin.';
      set({ error: msg });
    }
  },

  sendMessage: async (message: string) => {
    const { flowId, tenantId, messages, currentFlowPreview } = get();
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
      for await (const event of streamMessage(flowId, tenantId, message)) {
        if (event.type === 'text') {
          fullText += event.content || '';
          set({ streamingText: stripStreamingBlocks(fullText) });
        } else if (event.type === 'error') {
          set({ error: event.content || 'AI hatasi', isStreaming: false });
          return;
        } else if (event.type === 'done') {
          const assistantMsg: WizardMessage = {
            role: 'assistant',
            content: event.content || fullText,
            timestamp: new Date().toISOString(),
            flow_config_snapshot: event.flow_config,
            options: event.options,
          };

          set(state => ({
            messages: [...state.messages, assistantMsg],
            isStreaming: false,
            streamingText: '',
            previousFlowPreview: currentFlowPreview,
            currentFlowPreview: event.flow_config || state.currentFlowPreview,
            prerequisites: event.prerequisites || state.prerequisites,
            pendingOptions: event.options ?? null,
          }));
        }
      }
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') return;
      const msg = err instanceof Error ? err.message : 'Sunucu baglantisi kesildi. Mesajiniz gonderilmedi, tekrar deneyin.';
      set({ error: msg, isStreaming: false });
    }
  },

  confirmFlow: async (name: string) => {
    const { flowId, tenantId, currentFlowPreview } = get();
    if (!flowId || !tenantId || !currentFlowPreview) return;

    set({ error: null });
    try {
      await confirmWizard(flowId, tenantId, name, currentFlowPreview);
      set({ wizardStatus: 'completed', flowName: name });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Akis olusturulamadi. Tekrar deneyin veya destek ile iletisime gecin.';
      set({ error: msg });
    }
  },

  setFlowName: (name: string) => set({ flowName: name }),

  reset: () => set({
    flowId: null,
    tenantId: 0,
    messages: [],
    isStreaming: false,
    streamingText: '',
    currentFlowPreview: null,
    previousFlowPreview: null,
    prerequisites: null,
    pendingOptions: null,
    wizardStatus: null,
    error: null,
    flowName: '',
  }),
}));
