import {
  useRef,
  useEffect,
  useLayoutEffect,
  useState,
  useMemo,
  useCallback,
  type KeyboardEvent,
  type MouseEvent as ReactMouseEvent,
  type ReactNode,
} from 'react';
import { useAiChatStore } from '../../../stores/ai-chat-store';
import { useFlowStore } from '../../../stores/flow-store';
import { renderWithNodeChips, NODE_COLORS } from './NodeChip';
import { cn } from '../../../lib/utils';
import type { WizardMessage, WizardOption } from '../../../types/wizard';
import type { FlowConfigV2 } from '../../../types/flow';

const MIN_WIDTH = 240;
const MAX_WIDTH = 800;
const DEFAULT_WIDTH = 320;
const EXPANDED_WIDTH = 600;
const STORAGE_KEY = 'invekto_ai_chat_width';
const EXPANDED_STORAGE_KEY = 'invekto_ai_chat_expanded';
const TEXTAREA_MIN_HEIGHT = 44; // ~2 lines
const TEXTAREA_MAX_HEIGHT = 160; // ~7 lines

const thinkingMessages = [
  'Bir saniye, dusunuyorum...',
  'Hmm, guzel bir seyler geliyor...',
  'Fikirlerimi topluyorum...',
  'Yaratici moduma gectim...',
  'Hemen hazirliyorum...',
  'Sizin icin en iyisini dusunuyorum...',
];

interface SlashCommand {
  key: string;
  label: string;
  description: string;
  prompt: string;
}

const SLASH_COMMANDS: SlashCommand[] = [
  { key: 'yeni', label: '/yeni', description: 'Sifirdan yeni bir akis olusturmak istiyorum', prompt: 'Sifirdan yeni bir akis olusturmak istiyorum.' },
  { key: 'duzelt', label: '/duzelt', description: 'Akistaki hatalari ve eksikleri duzelt', prompt: 'Akistaki hatalari ve eksik baglantilari duzelt.' },
  { key: 'optimize', label: '/optimize', description: 'Akisi sadelestir ve performansini iyilestir', prompt: 'Akisi sadelestir, gereksiz dugumleri kaldir ve performansini iyilestir.' },
  { key: 'aciklat', label: '/aciklat', description: 'Mevcut akisi adim adim aciklat', prompt: 'Mevcut akisi adim adim, sade bir dille aciklat.' },
  { key: 'dal', label: '/dal', description: 'Yeni bir dal/senaryo ekle', prompt: 'Mevcut akisa yeni bir dal ekleyelim. Once ne tur bir senaryo eklemek istedigimi sor.' },
  { key: 'faq', label: '/faq', description: 'FAQ dugumu ve bilgi bankasi entegre et', prompt: 'Akisa bir FAQ dugumu ekle ve dogru baglantilari kur.' },
];

/** Check if a JSON string looks like FlowConfigV2 */
function isFlowConfigJson(json: string): boolean {
  try {
    const obj = JSON.parse(json);
    return obj?.version === 2 && Array.isArray(obj?.nodes) && Array.isArray(obj?.edges);
  } catch { return false; }
}

/** Strip ```options, ```flowconfig, and FlowConfigV2-containing ```json blocks from assistant text */
function cleanAssistantText(text: string): string {
  return text
    .replace(/```options\s*[\s\S]*?```/g, '')
    .replace(/```flowconfig\s*[\s\S]*?```/g, '')
    .replace(/```json\s*([\s\S]*?)```/g, (m, json) => isFlowConfigJson(json.trim()) ? '' : m)
    .trimEnd();
}

function getStoredWidth(): number {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    if (v) {
      const n = parseInt(v, 10);
      if (n >= MIN_WIDTH && n <= MAX_WIDTH) return n;
    }
  } catch {}
  return DEFAULT_WIDTH;
}

/** Format ISO timestamp → relative ("3 dk once") or short clock */
function formatTimestamp(iso?: string): string {
  if (!iso) return '';
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return '';
  const diffSec = Math.floor((Date.now() - t) / 1000);
  if (diffSec < 60) return 'simdi';
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)} dk once`;
  if (diffSec < 86400) return `${Math.floor(diffSec / 3600)} sa once`;
  return new Date(iso).toLocaleString('tr-TR', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
}

interface SuggestionChip {
  label: string;
  prompt: string;
  tone: 'default' | 'warning' | 'success';
}

/** Generate contextual quick-action chips based on current flow state */
function useContextualSuggestions(): SuggestionChip[] {
  const nodes = useFlowStore(s => s.nodes);
  const selectedNodeId = useFlowStore(s => s.selectedNodeId);
  const validationErrors = useFlowStore(s => s.validationErrors);

  return useMemo(() => {
    const out: SuggestionChip[] = [];
    const errorCount = Array.from(validationErrors.values()).reduce((a, b) => a + b.length, 0);

    if (nodes.length === 0) {
      out.push(
        { label: 'Karsilama akisi olustur', prompt: 'Yeni musteri icin sade bir karsilama akisi olustur.', tone: 'default' },
        { label: 'FAQ botu kur', prompt: 'Sik sorulan sorulara cevap veren bir FAQ botu olustur.', tone: 'default' },
        { label: 'Randevu alma akisi', prompt: 'Musteriden ad, telefon ve tarih alip temsilciye aktaran randevu akisi olustur.', tone: 'default' },
      );
      return out;
    }

    if (errorCount > 0) {
      out.push({
        label: `${errorCount} hatayi duzelt`,
        prompt: 'Akistaki tum hatalari ve eksik baglantilari duzelt.',
        tone: 'warning',
      });
    }

    if (selectedNodeId) {
      const sel = nodes.find(n => n.id === selectedNodeId);
      const label = (sel?.data as { label?: string } | undefined)?.label ?? sel?.type ?? 'secili dugum';
      out.push(
        { label: `"${label}" dugumune dal ekle`, prompt: `Secili dugume (${sel?.type}, "${label}") bir alternatif dal ekle.`, tone: 'default' },
        { label: `"${label}" sonrasi yeni adim`, prompt: `Secili dugumden (${sel?.type}, "${label}") sonra mantikli bir sonraki adim ekle.`, tone: 'default' },
      );
    }

    out.push(
      { label: 'FAQ dugumu ekle', prompt: 'Akisin uygun bir yerine FAQ dugumu ekle.', tone: 'default' },
      { label: 'Akisi optimize et', prompt: 'Akisi sadelestir ve gereksiz dugumleri birlestir.', tone: 'success' },
    );

    return out.slice(0, 5);
  }, [nodes, selectedNodeId, validationErrors]);
}

interface AiChatPanelProps {
  onApply: (config: FlowConfigV2) => void;
}

export function AiChatPanel({ onApply }: AiChatPanelProps) {
  const isOpen = useAiChatStore(s => s.isOpen);
  const messages = useAiChatStore(s => s.messages);
  const isStreaming = useAiChatStore(s => s.isStreaming);
  const streamingText = useAiChatStore(s => s.streamingText);
  const pendingFlowConfig = useAiChatStore(s => s.pendingFlowConfig);
  const pendingOptions = useAiChatStore(s => s.pendingOptions);
  const error = useAiChatStore(s => s.error);
  const autoApplyPending = useAiChatStore(s => s.autoApplyPending);
  const lastAppliedSnapshot = useAiChatStore(s => s.lastAppliedSnapshot);
  const templateContext = useAiChatStore(s => s.templateContext);
  const sendMessage = useAiChatStore(s => s.sendMessage);
  const close = useAiChatStore(s => s.close);
  const acceptChanges = useAiChatStore(s => s.acceptChanges);
  const clearAutoApply = useAiChatStore(s => s.clearAutoApply);
  const consumeUndoSnapshot = useAiChatStore(s => s.consumeUndoSnapshot);
  const clearUndoSnapshot = useAiChatStore(s => s.clearUndoSnapshot);
  const reset = useAiChatStore(s => s.reset);

  const [input, setInput] = useState('');
  const [freeInput, setFreeInput] = useState('');
  const [panelWidth, setPanelWidth] = useState(getStoredWidth);
  const [isExpanded, setIsExpanded] = useState<boolean>(() => {
    try { return localStorage.getItem(EXPANDED_STORAGE_KEY) === 'true'; }
    catch { return false; }
  });
  const [showResetConfirm, setShowResetConfirm] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const isDragging = useRef(false);
  const latestWidth = useRef(panelWidth);
  const resetConfirmTimerRef = useRef<number | null>(null);

  // Clear reset-confirm timer on unmount to avoid setState-after-unmount
  useEffect(() => () => {
    if (resetConfirmTimerRef.current !== null) window.clearTimeout(resetConfirmTimerRef.current);
  }, []);
  const thinkingText = useMemo(
    () => thinkingMessages[Math.floor(Math.random() * thinkingMessages.length)],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [isStreaming],
  );
  const suggestions = useContextualSuggestions();
  const flowNodes = useFlowStore(s => s.nodes);

  // Effective width: when "expanded" toggle is on, snap to EXPANDED_WIDTH (unless the user's
  // manually-resized width is already wider — preserve it). When off, use the manual width.
  const effectiveWidth = isExpanded ? Math.max(panelWidth, EXPANDED_WIDTH) : panelWidth;

  // Keep ref in sync with the rendered width (manual drag persists the stored width — not the
  // ephemeral expanded snap).
  useEffect(() => { latestWidth.current = panelWidth; }, [panelWidth]);

  const handleToggleExpand = useCallback(() => {
    setIsExpanded(v => {
      const next = !v;
      try { localStorage.setItem(EXPANDED_STORAGE_KEY, String(next)); } catch {}
      return next;
    });
  }, []);

  // --- Resize handlers ---
  const handleResizeStart = useCallback((e: ReactMouseEvent) => {
    e.preventDefault();
    isDragging.current = true;
    const startX = e.clientX;
    const startW = latestWidth.current;
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';

    const handleMouseMove = (ev: globalThis.MouseEvent) => {
      if (!isDragging.current) return;
      const delta = ev.clientX - startX;
      const w = Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, startW + delta));
      latestWidth.current = w;
      setPanelWidth(w);
    };

    const handleMouseUp = () => {
      isDragging.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
      try { localStorage.setItem(STORAGE_KEY, String(latestWidth.current)); } catch {}
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
  }, []);

  // Auto-apply: when AI generates a new flow_config, apply it directly to canvas
  useEffect(() => {
    if (!autoApplyPending || !pendingFlowConfig) return;
    clearAutoApply();
    const config = acceptChanges();
    if (config) onApply(config);
  }, [autoApplyPending, pendingFlowConfig, clearAutoApply, acceptChanges, onApply]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, streamingText]);

  useEffect(() => {
    if (!isStreaming && isOpen) inputRef.current?.focus();
  }, [isStreaming, isOpen]);

  // Auto-resize textarea (min 2 rows, max ~7 rows)
  useLayoutEffect(() => {
    const ta = inputRef.current;
    if (!ta) return;
    ta.style.height = 'auto';
    const next = Math.min(TEXTAREA_MAX_HEIGHT, Math.max(TEXTAREA_MIN_HEIGHT, ta.scrollHeight));
    ta.style.height = `${next}px`;
  }, [input]);

  const handleSend = useCallback((textOverride?: string) => {
    const text = (textOverride ?? input).trim();
    if (!text || isStreaming) return;
    if (textOverride === undefined) setInput('');
    const flowConfig = useFlowStore.getState().toFlowConfig();
    sendMessage(text, flowConfig);
  }, [input, isStreaming, sendMessage]);

  // --- Command popup (slash + @) ---
  type PopupKind = 'slash' | 'mention' | null;
  const [popupKind, setPopupKind] = useState<PopupKind>(null);
  const [popupIndex, setPopupIndex] = useState(0);
  const [popupQuery, setPopupQuery] = useState('');

  const detectPopup = useCallback((value: string) => {
    // Slash: only at the very start of input
    const slashMatch = /^\/([a-zA-Z0-9]*)$/.exec(value);
    if (slashMatch) {
      setPopupKind('slash');
      setPopupQuery(slashMatch[1].toLowerCase());
      setPopupIndex(0);
      return;
    }
    // Mention: last word starts with @
    const tail = value.split(/\s+/).pop() ?? '';
    if (tail.startsWith('@') && flowNodes.length > 0) {
      setPopupKind('mention');
      setPopupQuery(tail.slice(1).toLowerCase());
      setPopupIndex(0);
      return;
    }
    if (popupKind !== null) setPopupKind(null);
  }, [flowNodes.length, popupKind]);

  const filteredSlash = useMemo(
    () => SLASH_COMMANDS.filter(c => c.key.startsWith(popupQuery) || c.description.toLowerCase().includes(popupQuery)),
    [popupQuery],
  );

  const mentionItems = useMemo(() => {
    if (popupKind !== 'mention') return [] as Array<{ id: string; label: string; type: string; color: string }>;
    return flowNodes
      .map(n => {
        const label = (n.data as { label?: string } | undefined)?.label ?? n.type ?? n.id;
        return { id: n.id, label: String(label), type: n.type ?? 'unknown', color: NODE_COLORS[n.type ?? ''] ?? '#6b7280' };
      })
      .filter(it => it.label.toLowerCase().includes(popupQuery) || it.type.toLowerCase().includes(popupQuery))
      .slice(0, 8);
  }, [popupKind, popupQuery, flowNodes]);

  const popupCount = popupKind === 'slash' ? filteredSlash.length : popupKind === 'mention' ? mentionItems.length : 0;

  const applyPopupSelection = useCallback(() => {
    if (popupKind === 'slash') {
      const cmd = filteredSlash[popupIndex];
      if (!cmd) return;
      setInput('');
      setPopupKind(null);
      handleSend(cmd.prompt);
    } else if (popupKind === 'mention') {
      const item = mentionItems[popupIndex];
      if (!item) return;
      // Replace last @word with @{label}
      setInput(prev => prev.replace(/@[^\s]*$/, `@${item.label} `));
      setPopupKind(null);
      // Refocus
      requestAnimationFrame(() => inputRef.current?.focus());
    }
  }, [popupKind, filteredSlash, popupIndex, mentionItems, handleSend]);

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (popupKind && popupCount > 0) {
      if (e.key === 'ArrowDown') { e.preventDefault(); setPopupIndex(i => (i + 1) % popupCount); return; }
      if (e.key === 'ArrowUp') { e.preventDefault(); setPopupIndex(i => (i - 1 + popupCount) % popupCount); return; }
      if (e.key === 'Enter' || e.key === 'Tab') { e.preventDefault(); applyPopupSelection(); return; }
      if (e.key === 'Escape') { e.preventDefault(); setPopupKind(null); return; }
    }
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleOptionClick = useCallback((option: WizardOption) => {
    if (isStreaming) return;
    const flowConfig = useFlowStore.getState().toFlowConfig();
    sendMessage(option.label, flowConfig);
  }, [isStreaming, sendMessage]);

  const handleFreeSend = useCallback(() => {
    const text = freeInput.trim();
    if (!text || isStreaming) return;
    setFreeInput('');
    const flowConfig = useFlowStore.getState().toFlowConfig();
    sendMessage(text, flowConfig);
  }, [freeInput, isStreaming, sendMessage]);

  // Undo last AI apply: restore snapshot without re-capturing
  const handleUndoLastApply = useCallback(() => {
    const snap = consumeUndoSnapshot();
    if (!snap) return;
    const flowState = useFlowStore.getState();
    flowState.loadFlow(snap, flowState.wizardHistory);
  }, [consumeUndoSnapshot]);

  // Retry last user message after error
  const handleRetry = useCallback(() => {
    const lastUser = [...messages].reverse().find(m => m.role === 'user');
    if (!lastUser) return;
    const flowConfig = useFlowStore.getState().toFlowConfig();
    sendMessage(lastUser.content, flowConfig);
  }, [messages, sendMessage]);

  const handleResetClick = useCallback(() => {
    if (resetConfirmTimerRef.current !== null) {
      window.clearTimeout(resetConfirmTimerRef.current);
      resetConfirmTimerRef.current = null;
    }
    if (showResetConfirm) {
      reset();
      clearUndoSnapshot();
      setShowResetConfirm(false);
    } else {
      setShowResetConfirm(true);
      resetConfirmTimerRef.current = window.setTimeout(() => {
        setShowResetConfirm(false);
        resetConfirmTimerRef.current = null;
      }, 3000);
    }
  }, [showResetConfirm, reset, clearUndoSnapshot]);

  if (!isOpen) return null;

  return (
    <div className="flex-shrink-0 border-r border-navy-100 bg-navy-50 flex flex-col relative" style={{ width: effectiveWidth }}>
      {/* Resize handle */}
      <div
        onMouseDown={handleResizeStart}
        className="absolute top-0 right-0 w-1 h-full cursor-col-resize z-10 hover:bg-purple-300 active:bg-purple-400 transition-colors"
      />

      {/* Header */}
      <div className="h-10 bg-purple-600 flex items-center px-3 gap-1.5 flex-shrink-0">
        <div className="w-6 h-6 rounded-full bg-purple-400 flex items-center justify-center">
          <svg className="w-3.5 h-3.5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
          </svg>
        </div>
        <span className="text-white text-xs font-medium flex-1 truncate">AI ile Gelistir</span>

        {/* Undo last AI change */}
        {lastAppliedSnapshot && (
          <button
            onClick={handleUndoLastApply}
            className="flex items-center gap-1 px-2 py-0.5 rounded-md bg-purple-500 hover:bg-purple-400 text-white text-[10px] font-medium transition-colors"
            title="Son AI degisikligini geri al"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3 h-3">
              <path d="M3 7v6h6" />
              <path d="M21 17a9 9 0 0 0-15-6.7L3 13" />
            </svg>
            Geri al
          </button>
        )}

        {/* Expand / collapse panel width */}
        <button
          onClick={handleToggleExpand}
          className="p-1 rounded hover:bg-purple-500 transition-colors text-purple-200 hover:text-white"
          title={isExpanded ? 'Paneli daralt' : 'Paneli genislet'}
          aria-label={isExpanded ? 'Paneli daralt' : 'Paneli genislet'}
          aria-pressed={isExpanded}
        >
          {isExpanded ? (
            // Collapse: arrows pointing inward
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
              <path d="M20 9h-4V5" />
              <path d="m21 4-5 5" />
              <path d="M4 15h4v4" />
              <path d="m3 20 5-5" />
            </svg>
          ) : (
            // Expand: arrows pointing outward
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
              <path d="M15 3h6v6" />
              <path d="m21 3-7 7" />
              <path d="M9 21H3v-6" />
              <path d="m3 21 7-7" />
            </svg>
          )}
        </button>

        {/* Reset (with confirm) */}
        <button
          onClick={handleResetClick}
          className={cn(
            'p-1 rounded transition-colors',
            showResetConfirm
              ? 'bg-red-500 hover:bg-red-400 text-white'
              : 'hover:bg-purple-500 text-purple-200 hover:text-white',
          )}
          title={showResetConfirm ? 'Tikrar tikla, sohbet silinecek' : 'Sohbeti sifirla'}
        >
          {showResetConfirm ? (
            <span className="text-[10px] font-medium px-1">Emin misin?</span>
          ) : (
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
              <polyline points="1 4 1 10 7 10" />
              <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
            </svg>
          )}
        </button>

        {/* Close */}
        <button
          onClick={close}
          className="p-1 rounded hover:bg-purple-500 transition-colors text-purple-200 hover:text-white"
          title="Kapat"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      {/* Template banner — shown when user started this flow from a template */}
      {templateContext && (
        <div className="flex-shrink-0 bg-purple-50 border-b border-purple-100 px-3 py-2 flex items-center gap-2">
          <svg className="w-3.5 h-3.5 text-purple-600 flex-shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2M9 5a2 2 0 0 0 2 2h2a2 2 0 0 0 2-2M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2" />
          </svg>
          <div className="flex-1 min-w-0">
            <div className="text-[9px] uppercase tracking-wide text-purple-500 font-semibold leading-none">Sablon</div>
            <div className="text-[11px] text-purple-800 font-medium truncate leading-tight mt-0.5">{templateContext.title}</div>
          </div>
        </div>
      )}

      {/* Messages area */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto px-3 py-3 min-h-0 space-y-3">
        {messages.length === 0 && !isStreaming && (
          <EmptyState suggestions={suggestions} onPick={(prompt) => handleSend(prompt)} />
        )}

        {messages.map((msg, i) => (
          <ChatBubble key={i} message={msg} showOptions={i < messages.length - 1} />
        ))}

        {/* Streaming text */}
        {isStreaming && streamingText && (
          <div className="flex gap-2">
            <AssistantAvatar />
            <div className="bg-white border border-navy-100 rounded-lg px-3 py-2 max-w-[88%] text-xs text-navy-800 whitespace-pre-wrap break-words leading-relaxed">
              {renderWithNodeChips(streamingText)}
              <span className="inline-block w-1 h-3 bg-purple-400 ml-0.5 animate-pulse align-middle" />
            </div>
          </div>
        )}

        {/* Loading indicator */}
        {isStreaming && !streamingText && (
          <div className="flex gap-2">
            <div className="w-6 h-6 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0">
              <svg className="w-3 h-3 text-purple-600 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
            </div>
            <div className="bg-white border border-navy-100 rounded-lg px-3 py-2 text-xs text-navy-400 italic">
              {thinkingText}
            </div>
          </div>
        )}

        {/* Error with retry */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-xs rounded-lg px-3 py-2 space-y-2">
            <div className="flex items-start gap-1.5">
              <svg className="w-3.5 h-3.5 flex-shrink-0 mt-0.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="8" x2="12" y2="12" />
                <line x1="12" y1="16" x2="12.01" y2="16" />
              </svg>
              <span className="flex-1 leading-relaxed">{error}</span>
            </div>
            {messages.some(m => m.role === 'user') && (
              <button
                onClick={handleRetry}
                disabled={isStreaming}
                className="w-full px-2 py-1 text-[10px] font-medium rounded border border-red-300 bg-white text-red-700 hover:bg-red-50 disabled:opacity-50 transition-colors flex items-center justify-center gap-1"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3 h-3">
                  <polyline points="1 4 1 10 7 10" />
                  <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
                </svg>
                Tekrar dene
              </button>
            )}
          </div>
        )}

        {/* Option buttons (AskUserQuestion style) */}
        {!isStreaming && pendingOptions && pendingOptions.length > 0 && (
          <div className="space-y-1.5 pt-1">
            {pendingOptions.map((opt, i) => (
              <button
                key={i}
                onClick={() => handleOptionClick(opt)}
                className="w-full text-left px-3 py-2.5 bg-white border border-navy-100 rounded-lg shadow-soft hover:border-brand-500 hover:shadow-focus transition-all duration-150 group"
              >
                <div className="flex items-start gap-2">
                  <span className="flex-shrink-0 w-5 h-5 rounded-md bg-purple-50 text-purple-600 text-[10px] font-semibold flex items-center justify-center mt-0.5 group-hover:bg-purple-100">
                    {i + 1}
                  </span>
                  <div className="flex-1 min-w-0">
                    <div className="text-xs font-medium text-navy-900 group-hover:text-brand-600">{opt.label}</div>
                    {opt.description && (
                      <div className="text-[10px] text-navy-400 mt-0.5 leading-relaxed">{opt.description}</div>
                    )}
                  </div>
                </div>
              </button>
            ))}
            <div className="flex items-center gap-2 pt-0.5">
              <div className="flex-1 h-px bg-navy-100" />
              <span className="text-[10px] text-navy-300">veya kendi cevabin</span>
              <div className="flex-1 h-px bg-navy-100" />
            </div>
            <div className="flex gap-1.5">
              <input
                value={freeInput}
                onChange={e => setFreeInput(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleFreeSend(); } }}
                placeholder="Kendi cevabinizi yazin..."
                className="flex-1 px-2.5 py-1.5 bg-white border border-navy-100 rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-purple-300 focus:border-purple-300 placeholder:text-navy-300"
              />
              <button
                onClick={handleFreeSend}
                disabled={!freeInput.trim()}
                className={cn(
                  'w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0 transition-colors',
                  freeInput.trim()
                    ? 'bg-purple-500 hover:bg-purple-600 text-white'
                    : 'bg-navy-50 text-navy-200 cursor-not-allowed',
                )}
              >
                <svg viewBox="0 0 24 24" fill="currentColor" className="w-3 h-3">
                  <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
                </svg>
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Input area */}
      <div className="border-t border-navy-100 bg-white flex-shrink-0 relative">
        {/* Popup (slash or mention) */}
        {popupKind && popupCount > 0 && (
          <CommandPopup
            kind={popupKind}
            slashItems={filteredSlash}
            mentionItems={mentionItems}
            activeIndex={popupIndex}
            onHover={setPopupIndex}
            onPick={applyPopupSelection}
          />
        )}

        <div className="px-3 pt-2 pb-1">
          <div className="flex gap-1.5 items-end">
            <textarea
              ref={inputRef}
              value={input}
              onChange={e => { setInput(e.target.value); detectPopup(e.target.value); }}
              onKeyDown={handleKeyDown}
              placeholder="Ne degistirmek istiyorsunuz? ( / komutlar  ·  @ dugum )"
              rows={2}
              disabled={isStreaming}
              style={{ minHeight: TEXTAREA_MIN_HEIGHT, maxHeight: TEXTAREA_MAX_HEIGHT }}
              className="flex-1 px-2.5 py-2 bg-navy-25 border border-navy-100 rounded-lg text-xs resize-none focus:outline-none focus:ring-2 focus:ring-purple-300 focus:border-purple-300 disabled:opacity-50 placeholder:text-navy-300 leading-relaxed"
            />
            <button
              onClick={() => handleSend()}
              disabled={isStreaming || !input.trim()}
              className={cn(
                'w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0 transition-colors',
                input.trim() && !isStreaming
                  ? 'bg-purple-500 hover:bg-purple-600 text-white'
                  : 'bg-navy-100 text-navy-200 cursor-not-allowed',
              )}
              title="Gonder (Enter)"
            >
              <svg viewBox="0 0 24 24" fill="currentColor" className="w-3.5 h-3.5">
                <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
              </svg>
            </button>
          </div>
        </div>

        {/* Keyboard hint */}
        <div className="px-3 pb-1.5 flex items-center gap-2 text-[9px] text-navy-300 leading-none">
          <Kbd>Enter</Kbd>
          <span>gonder</span>
          <span className="text-navy-200">·</span>
          <Kbd>Shift</Kbd>
          <span>+</span>
          <Kbd>Enter</Kbd>
          <span>yeni satir</span>
          <span className="text-navy-200">·</span>
          <Kbd>/</Kbd>
          <span>komut</span>
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Sub-components
// ============================================================================

function AssistantAvatar() {
  return (
    <div className="w-6 h-6 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0 mt-0.5">
      <svg className="w-3 h-3 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
      </svg>
    </div>
  );
}

function Kbd({ children }: { children: ReactNode }) {
  return (
    <kbd className="inline-flex items-center px-1 py-px rounded bg-navy-50 border border-navy-100 text-navy-400 font-mono text-[9px]">
      {children}
    </kbd>
  );
}

function EmptyState({ suggestions, onPick }: { suggestions: SuggestionChip[]; onPick: (prompt: string) => void }) {
  const nodeCount = useFlowStore(s => s.nodes.length);
  const errorCount = useFlowStore(s => Array.from(s.validationErrors.values()).reduce((a, b) => a + b.length, 0));

  let contextLabel: string;
  if (nodeCount === 0) contextLabel = 'Bos canvas';
  else if (errorCount > 0) contextLabel = `${nodeCount} dugum · ${errorCount} hata`;
  else contextLabel = `${nodeCount} dugum`;

  return (
    <div className="flex flex-col items-center justify-center h-full text-center px-2 py-6">
      <div className="w-12 h-12 rounded-xl bg-purple-50 flex items-center justify-center mb-3">
        <svg className="w-6 h-6 text-purple-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
        </svg>
      </div>
      <p className="text-xs text-navy-500 font-medium mb-0.5">AI ile akisinizi gelistirin</p>
      <p className="text-[10px] text-navy-300 mb-3">{contextLabel}</p>

      <div className="w-full space-y-1.5">
        {suggestions.map((s, i) => (
          <button
            key={i}
            onClick={() => onPick(s.prompt)}
            className={cn(
              'w-full text-left px-2.5 py-1.5 text-[11px] font-medium rounded-lg border transition-all duration-150',
              s.tone === 'warning' && 'bg-amber-50 border-amber-200 text-amber-800 hover:border-amber-400 hover:bg-amber-100',
              s.tone === 'success' && 'bg-emerald-50 border-emerald-200 text-emerald-700 hover:border-emerald-400 hover:bg-emerald-100',
              s.tone === 'default' && 'bg-white border-navy-100 text-navy-700 hover:border-purple-300 hover:bg-purple-50',
            )}
          >
            {s.label}
          </button>
        ))}
      </div>

      <p className="text-[9px] text-navy-300 mt-3">
        Ipucu: <Kbd>/</Kbd> ile hizli komutlar, <Kbd>@</Kbd> ile dugum bahsedebilirsin
      </p>
    </div>
  );
}

interface CommandPopupProps {
  kind: 'slash' | 'mention';
  slashItems: SlashCommand[];
  mentionItems: Array<{ id: string; label: string; type: string; color: string }>;
  activeIndex: number;
  onHover: (i: number) => void;
  onPick: () => void;
}

function CommandPopup({ kind, slashItems, mentionItems, activeIndex, onHover, onPick }: CommandPopupProps) {
  const items = kind === 'slash' ? slashItems : mentionItems;
  if (items.length === 0) return null;
  return (
    <div className="absolute bottom-full left-3 right-3 mb-1 bg-white border border-navy-200 rounded-lg shadow-elevated max-h-56 overflow-y-auto z-20">
      <div className="px-2 py-1 bg-navy-25 border-b border-navy-100 text-[9px] uppercase tracking-wide text-navy-400 font-semibold">
        {kind === 'slash' ? 'Hizli komut' : 'Dugum bahset'}
      </div>
      {kind === 'slash' && slashItems.map((cmd, i) => (
        <button
          key={cmd.key}
          type="button"
          onMouseDown={(e) => { e.preventDefault(); onPick(); }}
          onMouseEnter={() => onHover(i)}
          className={cn(
            'w-full text-left px-2.5 py-1.5 flex items-start gap-2 transition-colors',
            i === activeIndex ? 'bg-purple-50' : 'hover:bg-navy-25',
          )}
        >
          <span className="text-[10px] font-mono font-semibold text-purple-600 flex-shrink-0 mt-0.5">{cmd.label}</span>
          <span className="text-[10px] text-navy-500 leading-relaxed">{cmd.description}</span>
        </button>
      ))}
      {kind === 'mention' && mentionItems.map((it, i) => (
        <button
          key={it.id}
          type="button"
          onMouseDown={(e) => { e.preventDefault(); onPick(); }}
          onMouseEnter={() => onHover(i)}
          className={cn(
            'w-full text-left px-2.5 py-1.5 flex items-center gap-2 transition-colors',
            i === activeIndex ? 'bg-purple-50' : 'hover:bg-navy-25',
          )}
        >
          <span className="w-2 h-2 rounded-full flex-shrink-0" style={{ backgroundColor: it.color }} />
          <span className="text-[11px] font-medium text-navy-700 truncate flex-1">{it.label}</span>
          <span className="text-[9px] text-navy-300 font-mono flex-shrink-0">{it.type}</span>
        </button>
      ))}
    </div>
  );
}

type CopyState = 'idle' | 'success' | 'error';

function CopyButton({ text, position }: { text: string; position: 'left' | 'right' }) {
  const [state, setState] = useState<CopyState>('idle');
  const timerRef = useRef<number | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (timerRef.current !== null) {
        window.clearTimeout(timerRef.current);
        timerRef.current = null;
      }
    };
  }, []);

  const handleClick = useCallback(async () => {
    let nextState: CopyState;
    try {
      if (!navigator.clipboard?.writeText) throw new Error('Clipboard API yok');
      await navigator.clipboard.writeText(text);
      nextState = 'success';
    } catch (err) {
      console.warn('[AiChatPanel] clipboard copy failed:', err);
      nextState = 'error';
    }
    // Guard: component may have unmounted while clipboard promise was pending
    if (!mountedRef.current) return;
    setState(nextState);
    if (timerRef.current !== null) window.clearTimeout(timerRef.current);
    timerRef.current = window.setTimeout(() => {
      if (!mountedRef.current) return;
      setState('idle');
      timerRef.current = null;
    }, 1800);
  }, [text]);

  const baseTitle = state === 'success' ? 'Kopyalandi' : state === 'error' ? 'Kopyalanamadi — manuel sec' : 'Mesaji kopyala';
  const tone =
    state === 'success'
      ? 'border-emerald-300 text-emerald-600 bg-emerald-50'
      : state === 'error'
        ? 'border-red-300 text-red-600 bg-red-50'
        : 'border-navy-200 text-navy-400 hover:text-purple-600 hover:border-purple-300 bg-white';

  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        'absolute -top-1.5 w-5 h-5 rounded-md border transition-all flex items-center justify-center shadow-sm',
        position === 'left' ? '-left-1.5' : '-right-1.5',
        state === 'idle' ? 'opacity-0 group-hover/bubble:opacity-100' : 'opacity-100',
        tone,
      )}
      title={baseTitle}
      aria-label={baseTitle}
    >
      {state === 'success' && (
        <svg className="w-2.5 h-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
        </svg>
      )}
      {state === 'error' && (
        <svg className="w-2.5 h-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
          <line x1="18" y1="6" x2="6" y2="18" />
          <line x1="6" y1="6" x2="18" y2="18" />
        </svg>
      )}
      {state === 'idle' && (
        <svg className="w-2.5 h-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <rect x="9" y="9" width="11" height="11" rx="2" />
          <path d="M5 15V5a2 2 0 0 1 2-2h10" />
        </svg>
      )}
    </button>
  );
}

function ChatBubble({ message, showOptions = true }: { message: WizardMessage; showOptions?: boolean }) {
  const isUser = message.role === 'user';
  const ts = formatTimestamp(message.timestamp);
  const copyText = isUser ? message.content : cleanAssistantText(message.content);

  if (isUser) {
    return (
      <div className="flex gap-2 justify-end group/bubble">
        <div className="relative">
          <div
            className="bg-purple-500 text-white rounded-lg px-3 py-2 max-w-[88%] text-xs whitespace-pre-wrap break-words leading-relaxed"
            title={ts}
          >
            {message.content}
          </div>
          <CopyButton text={copyText} position="left" />
        </div>
      </div>
    );
  }

  const cleaned = cleanAssistantText(message.content);

  return (
    <div className="flex gap-2 group/bubble">
      <AssistantAvatar />
      <div className="relative bg-white border border-navy-100 rounded-lg px-3 py-2 max-w-[88%] text-xs text-navy-800 leading-relaxed">
        <CopyButton text={copyText} position="right" />
        <div className="whitespace-pre-wrap break-words">{renderWithNodeChips(cleaned)}</div>
        {showOptions && message.options && message.options.length > 0 && (
          <div className="mt-1.5 pt-1.5 border-t border-navy-50 space-y-1">
            {message.options.map((opt, j) => (
              <div key={j} className="px-2 py-1.5 bg-navy-25 border border-navy-100 rounded text-[10px] text-navy-500">
                <span className="font-medium text-navy-600">{opt.label}</span>
                {opt.description && <span className="text-navy-400"> — {opt.description}</span>}
              </div>
            ))}
          </div>
        )}
        {message.flow_config_snapshot && (
          <div className="mt-1.5 pt-1.5 border-t border-navy-100 text-[10px] text-green-600 font-medium flex items-center gap-1">
            <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Degisiklik uygulandi
          </div>
        )}
        {ts && (
          <div className="mt-1 text-[9px] text-navy-300">{ts}</div>
        )}
      </div>
    </div>
  );
}
