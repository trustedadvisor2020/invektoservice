import { useRef, useEffect, useState, useCallback, type KeyboardEvent, type MouseEvent as ReactMouseEvent } from 'react';
import { useAiChatStore } from '../../../stores/ai-chat-store';
import { useFlowStore } from '../../../stores/flow-store';
import { renderWithNodeChips } from './NodeChip';
import { cn } from '../../../lib/utils';
import type { WizardMessage, WizardOption } from '../../../types/wizard';
import type { FlowConfigV2 } from '../../../types/flow';

const MIN_WIDTH = 240;
const MAX_WIDTH = 520;
const DEFAULT_WIDTH = 320;
const STORAGE_KEY = 'invekto_ai_chat_width';

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
  const sendMessage = useAiChatStore(s => s.sendMessage);
  const close = useAiChatStore(s => s.close);
  const acceptChanges = useAiChatStore(s => s.acceptChanges);
  const rejectChanges = useAiChatStore(s => s.rejectChanges);
  const reset = useAiChatStore(s => s.reset);

  const [input, setInput] = useState('');
  const [showDiff, setShowDiff] = useState(false);
  const [panelWidth, setPanelWidth] = useState(getStoredWidth);
  const scrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const isDragging = useRef(false);
  const latestWidth = useRef(panelWidth);

  // Keep ref in sync
  useEffect(() => { latestWidth.current = panelWidth; }, [panelWidth]);

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

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, streamingText]);

  useEffect(() => {
    if (!isStreaming && isOpen) inputRef.current?.focus();
  }, [isStreaming, isOpen]);

  const handleSend = useCallback(() => {
    const text = input.trim();
    if (!text || isStreaming) return;
    setInput('');
    const flowConfig = useFlowStore.getState().toFlowConfig();
    sendMessage(text, flowConfig);
  }, [input, isStreaming, sendMessage]);

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleAccept = useCallback(() => {
    const config = acceptChanges();
    if (config) {
      onApply(config);
      setShowDiff(false);
    }
  }, [acceptChanges, onApply]);

  const handleOptionClick = useCallback((option: WizardOption) => {
    if (isStreaming) return;
    const flowConfig = useFlowStore.getState().toFlowConfig();
    sendMessage(option.label, flowConfig);
  }, [isStreaming, sendMessage]);

  if (!isOpen) return null;

  return (
    <div className="flex-shrink-0 border-r border-navy-100 bg-navy-50 flex flex-col relative" style={{ width: panelWidth }}>
      {/* Resize handle */}
      <div
        onMouseDown={handleResizeStart}
        className="absolute top-0 right-0 w-1 h-full cursor-col-resize z-10 hover:bg-purple-300 active:bg-purple-400 transition-colors"
      />
      {/* Header */}
      <div className="h-10 bg-purple-600 flex items-center px-3 gap-2 flex-shrink-0">
        <div className="w-6 h-6 rounded-full bg-purple-400 flex items-center justify-center">
          <svg className="w-3.5 h-3.5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
          </svg>
        </div>
        <span className="text-white text-xs font-medium flex-1">AI ile Gelistir</span>

        {/* Reset button */}
        <button
          onClick={reset}
          className="p-1 rounded hover:bg-purple-500 transition-colors text-purple-200 hover:text-white"
          title="Sohbeti Sifirla"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
            <polyline points="1 4 1 10 7 10" />
            <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
          </svg>
        </button>

        {/* Close button */}
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

      {/* Messages area */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto px-3 py-3 min-h-0 space-y-3">
        {messages.length === 0 && !isStreaming && (
          <div className="flex flex-col items-center justify-center h-full text-center px-4">
            <div className="w-12 h-12 rounded-xl bg-purple-50 flex items-center justify-center mb-3">
              <svg className="w-6 h-6 text-purple-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
              </svg>
            </div>
            <p className="text-xs text-navy-400 mb-3">
              Mevcut akisinizi AI ile gelistirin. Ne degistirmek istediginizi anlatin.
            </p>
            <div className="flex flex-wrap gap-1.5 justify-center">
              {['Yeni dal ekle', 'FAQ dugumu ekle', 'Hata yollarini duzelt', 'Akisi optimize et'].map(s => (
                <button
                  key={s}
                  onClick={() => setInput(s)}
                  className="px-2 py-1 text-[10px] bg-purple-50 text-purple-600 rounded-full hover:bg-purple-100 transition-colors"
                >
                  {s}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((msg, i) => (
          <ChatBubble key={i} message={msg} />
        ))}

        {/* Streaming text */}
        {isStreaming && streamingText && (
          <div className="flex gap-2">
            <div className="w-6 h-6 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0 mt-0.5">
              <svg className="w-3 h-3 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
              </svg>
            </div>
            <div className="bg-white border border-navy-100 rounded-lg px-3 py-2 max-w-[85%] text-xs text-navy-800 whitespace-pre-wrap">
              {renderWithNodeChips(streamingText)}
              <span className="inline-block w-1 h-3 bg-purple-400 ml-0.5 animate-pulse" />
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
            <div className="bg-white border border-navy-100 rounded-lg px-3 py-2 text-xs text-navy-400">
              AI dusunuyor...
            </div>
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-xs rounded-lg px-3 py-2">
            {error}
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
                <div className="text-xs font-medium text-navy-900 group-hover:text-brand-600">{opt.label}</div>
                {opt.description && (
                  <div className="text-[10px] text-navy-400 mt-0.5 leading-relaxed">{opt.description}</div>
                )}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Pending changes banner */}
      {pendingFlowConfig && (
        <div className="border-t border-amber-200 bg-amber-50 px-3 py-2 flex-shrink-0">
          <div className="flex items-center gap-1.5 mb-2">
            <svg className="w-3.5 h-3.5 text-amber-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" />
            </svg>
            <span className="text-[10px] font-medium text-amber-800">AI degisiklik onerdi</span>
          </div>

          {showDiff && (
            <DiffSummary pending={pendingFlowConfig} />
          )}

          <div className="flex gap-1.5">
            <button
              onClick={() => setShowDiff(!showDiff)}
              className="flex-1 px-2 py-1 text-[10px] font-medium rounded border border-amber-300 text-amber-700 bg-white hover:bg-amber-50 transition-colors"
            >
              {showDiff ? 'Gizle' : 'Onizle'}
            </button>
            <button
              onClick={handleAccept}
              className="flex-1 px-2 py-1 text-[10px] font-medium rounded bg-emerald-500 hover:bg-emerald-600 text-white transition-colors"
            >
              Uygula
            </button>
            <button
              onClick={rejectChanges}
              className="px-2 py-1 text-[10px] font-medium rounded border border-navy-200 text-navy-400 bg-white hover:bg-navy-50 transition-colors"
            >
              Reddet
            </button>
          </div>
        </div>
      )}

      {/* Input area */}
      <div className="border-t border-navy-100 px-3 py-2 bg-white flex-shrink-0">
        <div className="flex gap-1.5">
          <textarea
            ref={inputRef}
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ne degistirmek istiyorsunuz?"
            rows={2}
            disabled={isStreaming}
            className="flex-1 px-2.5 py-2 bg-navy-25 border border-navy-100 rounded-lg text-xs resize-none focus:outline-none focus:ring-2 focus:ring-purple-300 focus:border-purple-300 disabled:opacity-50 placeholder:text-navy-300"
          />
          <button
            onClick={handleSend}
            disabled={isStreaming || !input.trim()}
            className={cn(
              'w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0 self-end transition-colors',
              input.trim() && !isStreaming
                ? 'bg-purple-500 hover:bg-purple-600 text-white'
                : 'bg-navy-100 text-navy-200 cursor-not-allowed'
            )}
          >
            <svg viewBox="0 0 24 24" fill="currentColor" className="w-3.5 h-3.5">
              <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
}

function ChatBubble({ message }: { message: WizardMessage }) {
  const isUser = message.role === 'user';

  if (isUser) {
    return (
      <div className="flex gap-2 justify-end">
        <div className="bg-purple-500 text-white rounded-lg px-3 py-2 max-w-[85%] text-xs whitespace-pre-wrap">
          {message.content}
        </div>
      </div>
    );
  }

  return (
    <div className="flex gap-2">
      <div className="w-6 h-6 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0 mt-0.5">
        <svg className="w-3 h-3 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
        </svg>
      </div>
      <div className="bg-white border border-navy-100 rounded-lg px-3 py-2 max-w-[85%] text-xs text-navy-800 whitespace-pre-wrap">
        {renderWithNodeChips(cleanAssistantText(message.content))}
        {message.flow_config_snapshot && (
          <div className="mt-1.5 pt-1.5 border-t border-navy-100 text-[10px] text-green-600 font-medium flex items-center gap-1">
            <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Degisiklik onerisi hazir
          </div>
        )}
      </div>
    </div>
  );
}

function DiffSummary({ pending }: { pending: FlowConfigV2 }) {
  const currentNodes = useFlowStore(s => s.nodes);
  const currentEdges = useFlowStore(s => s.edges);

  const pendingNodeIds = new Set(pending.nodes.map(n => n.id));
  const currentNodeIds = new Set(currentNodes.map(n => n.id));

  const added = pending.nodes.filter(n => !currentNodeIds.has(n.id));
  const removed = currentNodes.filter(n => !pendingNodeIds.has(n.id));
  const kept = pending.nodes.filter(n => currentNodeIds.has(n.id));

  const edgeDiff = pending.edges.length - currentEdges.length;

  return (
    <div className="mb-2 text-[10px] space-y-0.5">
      {added.length > 0 && (
        <div className="text-green-700">+ {added.length} yeni dugum: {added.map(n => n.data?.label || n.type).join(', ')}</div>
      )}
      {removed.length > 0 && (
        <div className="text-red-600">- {removed.length} silinen dugum: {removed.map(n => (n.data as Record<string, string>)?.label || n.type).join(', ')}</div>
      )}
      {kept.length > 0 && (
        <div className="text-navy-400">{kept.length} mevcut dugum korunuyor</div>
      )}
      {edgeDiff !== 0 && (
        <div className={edgeDiff > 0 ? 'text-green-700' : 'text-red-600'}>
          {edgeDiff > 0 ? '+' : ''}{edgeDiff} baglanti degisikligi
        </div>
      )}
    </div>
  );
}
