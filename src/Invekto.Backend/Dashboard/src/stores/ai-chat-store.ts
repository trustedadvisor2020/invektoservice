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

/** Strip multi-line JSON object/array blocks that the AI emits WITHOUT a code fence.
 *  Heuristic: a line that opens with `{` or `[` and is NOT closed on the same line
 *  starts a JSON block; count brackets (string-aware) until balanced, drop the block.
 *  If the block never closes (streaming truncated), drop the tail. */
function stripRawJsonBlocks(text: string): string {
  const lines = text.split('\n');
  const out: string[] = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();
    if (trimmed[0] !== '{' && trimmed[0] !== '[') {
      out.push(line);
      i++;
      continue;
    }
    let depth = 0;
    let inStr = false;
    let esc = false;
    const count = (s: string) => {
      for (const ch of s) {
        if (esc) { esc = false; continue; }
        if (ch === '\\' && inStr) { esc = true; continue; }
        if (ch === '"') { inStr = !inStr; continue; }
        if (inStr) continue;
        if (ch === '{' || ch === '[') depth++;
        else if (ch === '}' || ch === ']') depth--;
      }
    };
    count(trimmed);
    if (depth <= 0) {
      // Same-line balanced. Distinguish:
      //  - keep: markdown link `[text](url)`, markdown image `![alt](url)` (no `"` inside)
      //  - strip: ANY bracket-wrapped content that contains a `"` quote — this catches
      //    JSON objects (`{"k":1}`), JSON-string arrays (`["a","b"]`), and minified
      //    FlowConfigV2 (`{"version":2,"nodes":[...]}`).
      if (trimmed.includes('"')) {
        i++;
        continue;
      }
      out.push(line);
      i++;
      continue;
    }
    let endIdx = -1;
    for (let j = i + 1; j < lines.length; j++) {
      count(lines[j]);
      if (depth <= 0) { endIdx = j; break; }
    }
    if (endIdx === -1) {
      // Unterminated (streaming): drop the entire tail
      i = lines.length;
    } else {
      i = endIdx + 1;
    }
  }
  return out.join('\n');
}

/** Strip sequences of 2+ consecutive lines that look like raw JSON properties
 *  (`"key":` start) or stray bracket-only lines. Defense for cases where the AI
 *  drops outer braces but still spills JSON-shaped lines into the message body. */
function stripJsonKeywordSequences(text: string): string {
  const lines = text.split('\n');
  const isJsonLine = (l: string) => {
    const t = l.trim();
    return /^"[^"]+"\s*:/.test(t) || /^[{}\[\]],?$/.test(t);
  };
  const out: string[] = [];
  let i = 0;
  while (i < lines.length) {
    if (!isJsonLine(lines[i])) { out.push(lines[i]); i++; continue; }
    let j = i;
    while (j < lines.length && isJsonLine(lines[j])) j++;
    if (j - i >= 2) { i = j; } else { out.push(lines[i]); i++; }
  }
  return out.join('\n');
}

/** Collapse 3+ blank lines (left over after stripping) into 2. */
function collapseBlankLines(text: string): string {
  return text.replace(/\n{3,}/g, '\n\n').trim();
}

/** Strip ```options, ```flowconfig, and FlowConfigV2-containing ```json blocks from text */
export function stripCodeBlocks(text: string): string {
  let clean = text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m);
  // Defensive: AI sometimes ignores the fence rule and emits raw JSON in plain text.
  // These users are non-technical (dentists, restaurant owners); JSON in chat loses them instantly.
  clean = stripRawJsonBlocks(clean);
  clean = stripJsonKeywordSequences(clean);
  return collapseBlankLines(clean);
}

/** Strip only incomplete (unterminated) code blocks from streaming text */
function stripStreamingBlocks(text: string): string {
  let clean = text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m);
  // Strip incomplete block at the end (started but not closed)
  clean = clean.replace(/```(?:options|flowconfig|json)\s*[\s\S]*$/g, '');
  // Same defensive JSON strip as in stripCodeBlocks — applied during streaming too so the
  // user never sees JSON flash on the screen even briefly.
  clean = stripRawJsonBlocks(clean);
  clean = stripJsonKeywordSequences(clean);
  return collapseBlankLines(clean);
}

/** Optional context about the template the user started from. Shown as a banner above the chat. */
export interface TemplateContext {
  title: string;
}

interface SendMessageOptions {
  /** If true, the user message is NOT shown in the chat (sent to backend silently — used for template seed greeting). */
  hidden?: boolean;
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
  /** Pre-apply snapshot of canvas; non-null means "Geri al" is available */
  lastAppliedSnapshot: FlowConfigV2 | null;
  /** Template the user started this flow from — shown as a header banner. */
  templateContext: TemplateContext | null;

  open: (flowId: number, tenantId: number) => Promise<void>;
  /** Open + set template banner + auto-send hidden TEMPLATE_SEED greeting so AI welcomes the user first. */
  openWithTemplate: (flowId: number, tenantId: number, template: TemplateContext, currentFlowConfig: FlowConfigV2) => Promise<void>;
  close: () => void;
  sendMessage: (message: string, currentFlowConfig: FlowConfigV2, options?: SendMessageOptions) => Promise<void>;
  acceptChanges: () => FlowConfigV2 | null;
  rejectChanges: () => void;
  clearAutoApply: () => void;
  captureSnapshot: (config: FlowConfigV2) => void;
  consumeUndoSnapshot: () => FlowConfigV2 | null;
  clearUndoSnapshot: () => void;
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
  lastAppliedSnapshot: null,
  templateContext: null,

  open: async (flowId: number, tenantId: number) => {
    const state = get();
    // Already open for this flow AND same tenant — just toggle visibility (preserve templateContext).
    // tenantId check prevents reusing another tenant's stored messages/templateContext when the SPA
    // state survives a tenant/session switch and the new tenant happens to open a flow with the same id.
    if (state.flowId === flowId && state.tenantId === tenantId && state.messages.length > 0) {
      set({ isOpen: true });
      return;
    }

    // New/different flow OR different tenant: reset chat state and clear any stale template banner.
    set({ isOpen: true, flowId, tenantId, messages: [], error: null, pendingFlowConfig: null, pendingOptions: null, templateContext: null });

    // Load existing wizard_history if any
    try {
      const data = await getWizardState(tenantId, flowId);
      const raw = Array.isArray(data.wizard_history) ? data.wizard_history : [];
      // Normalize: handle both PascalCase (legacy DB) and camelCase property names
      const normalized: WizardMessage[] = raw.map((m: Record<string, unknown>) => ({
        role: (m.role ?? m.Role ?? 'user') as WizardMessage['role'],
        content: (m.content ?? m.Content ?? '') as string,
        timestamp: (m.timestamp ?? m.Timestamp ?? '') as string,
        flow_config_snapshot: (m.flow_config_snapshot ?? m.FlowConfigSnapshot) as WizardMessage['flow_config_snapshot'],
        options: (m.options ?? m.Options) as WizardMessage['options'],
      }));
      // Restore template banner from persisted history: TEMPLATE_SEED user message acts as the
      // single source of truth for "this flow was started from template X". The seed is hidden
      // from display below but its presence keeps the banner alive across refresh/re-open.
      const seedMsg = normalized.find(m => m.role === 'user' && m.content.startsWith('TEMPLATE_SEED:'));
      const restoredTemplate: TemplateContext | null = seedMsg
        ? { title: seedMsg.content.substring('TEMPLATE_SEED:'.length).trim() || 'Sablon' }
        : null;
      // Hide all TEMPLATE_SEED user messages from the visible chat (they remain in backend history
      // so Claude API conversation context stays intact with alternating user/assistant turns).
      const history = normalized.filter(m => !(m.role === 'user' && m.content.startsWith('TEMPLATE_SEED:')));
      set({ messages: history, templateContext: restoredTemplate });
    } catch (err: unknown) {
      // 404 = no history yet (normal for non-wizard flows), other errors surface to user
      const isNotFound = err instanceof Error && err.message.includes('404');
      if (!isNotFound) {
        set({ error: err instanceof Error ? err.message : 'Sohbet gecmisi yuklenemedi. Yeni sohbet baslatildi.' });
      }
    }
  },

  openWithTemplate: async (flowId, tenantId, template, currentFlowConfig) => {
    const state = get();
    // Skip re-seed only when: SAME tenant AND same flow with visible messages, OR seed/stream in flight,
    // OR templateContext already set for this same-tenant flow. tenantId check prevents reusing another
    // tenant's in-memory state after a tenant/session switch.
    const alreadySeeded =
      state.flowId === flowId &&
      state.tenantId === tenantId &&
      (state.messages.length > 0 || state.isStreaming || state.templateContext !== null);
    if (alreadySeeded) {
      set({ isOpen: true, templateContext: template });
      return;
    }
    set({
      isOpen: true,
      flowId,
      tenantId,
      messages: [],
      error: null,
      pendingFlowConfig: null,
      pendingOptions: null,
      templateContext: template,
    });
    // Send a hidden TEMPLATE_SEED signal so the AI greets first.
    // The backend prompt recognizes this prefix and responds with a warm onboarding message + sector question.
    await get().sendMessage(`TEMPLATE_SEED: ${template.title}`, currentFlowConfig, { hidden: true });
  },

  close: () => set({ isOpen: false }),

  sendMessage: async (message: string, currentFlowConfig: FlowConfigV2, options?: SendMessageOptions) => {
    const { flowId, tenantId, messages } = get();
    if (!flowId || !tenantId) return;

    const hidden = options?.hidden === true;
    const userMsg: WizardMessage = {
      role: 'user',
      content: message,
      timestamp: new Date().toISOString(),
    };

    set({
      // Hidden seed messages bypass the visible message list but still go to backend
      messages: hidden ? messages : [...messages, userMsg],
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

  captureSnapshot: (config: FlowConfigV2) => set({ lastAppliedSnapshot: config }),

  consumeUndoSnapshot: () => {
    const snap = get().lastAppliedSnapshot;
    if (!snap) return null;
    set({ lastAppliedSnapshot: null });
    return snap;
  },

  clearUndoSnapshot: () => set({ lastAppliedSnapshot: null }),

  reset: () => set({
    messages: [],
    isStreaming: false,
    streamingText: '',
    pendingFlowConfig: null,
    pendingOptions: null,
    error: null,
    autoApplyPending: false,
    lastAppliedSnapshot: null,
    templateContext: null,
  }),
}));
