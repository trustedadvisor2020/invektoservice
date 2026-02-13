import { useCallback, useEffect, useRef, useState } from 'react';
import { useSimulationStore } from '../store/simulation-store';
import { ChatBubble } from './ChatBubble';
import { cn } from '../lib/utils';

export function SimulationPanel() {
  const isOpen = useSimulationStore((s) => s.isOpen);
  const isLoading = useSimulationStore((s) => s.isLoading);
  const messages = useSimulationStore((s) => s.messages);
  const status = useSimulationStore((s) => s.status);
  const pendingInput = useSimulationStore((s) => s.pendingInput);
  const error = useSimulationStore((s) => s.error);
  const sendMessage = useSimulationStore((s) => s.sendMessage);
  const close = useSimulationStore((s) => s.close);
  const reset = useSimulationStore((s) => s.reset);

  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Focus input when panel opens or loading finishes
  useEffect(() => {
    if (isOpen && !isLoading) {
      inputRef.current?.focus();
    }
  }, [isOpen, isLoading]);

  const handleSend = useCallback(() => {
    const trimmed = input.trim();
    if (!trimmed || isLoading) return;
    setInput('');
    sendMessage(trimmed);
  }, [input, isLoading, sendMessage]);

  const handleMenuOption = useCallback((option: string) => {
    if (isLoading) return;
    sendMessage(option);
  }, [isLoading, sendMessage]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  }, [handleSend]);

  if (!isOpen) return null;

  const isTerminal = status === 'completed' || status === 'error' || status === 'handed_off';
  const showMenuButtons = pendingInput?.type === 'menu' && pendingInput.options && !isTerminal;

  return (
    <div className="w-[280px] flex-shrink-0 border-l border-slate-200 bg-slate-50 flex flex-col">
      {/* Header */}
      <div className="h-10 bg-emerald-600 flex items-center px-3 gap-2 flex-shrink-0">
        <div className="w-6 h-6 rounded-full bg-emerald-400 flex items-center justify-center">
          <svg viewBox="0 0 24 24" fill="white" className="w-3.5 h-3.5">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
        </div>
        <span className="text-white text-xs font-medium flex-1">Simulasyon</span>

        {/* Restart button */}
        <button
          onClick={reset}
          className="p-1 rounded hover:bg-emerald-500 transition-colors text-emerald-200 hover:text-white"
          title="Yeniden Baslat"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
            <polyline points="1 4 1 10 7 10" />
            <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
          </svg>
        </button>

        {/* Close button */}
        <button
          onClick={close}
          className="p-1 rounded hover:bg-emerald-500 transition-colors text-emerald-200 hover:text-white"
          title="Kapat"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="w-3.5 h-3.5">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      {/* Messages area */}
      <div className="flex-1 overflow-y-auto px-3 py-3 min-h-0">
        {messages.length === 0 && !isLoading && (
          <div className="text-xs text-slate-400 text-center mt-8">
            Simulasyon baslatiliyor...
          </div>
        )}

        {messages.map((msg, idx) => (
          <ChatBubble key={idx} role={msg.role} text={msg.text} />
        ))}

        {isLoading && (
          <div className="flex justify-start mb-2">
            <div className="bg-white border border-slate-200 rounded-lg rounded-tl-none px-3 py-2">
              <div className="flex gap-1">
                <span className="w-1.5 h-1.5 bg-slate-300 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                <span className="w-1.5 h-1.5 bg-slate-300 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                <span className="w-1.5 h-1.5 bg-slate-300 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Error banner */}
      {error && (
        <div className="px-3 py-2 bg-red-50 border-t border-red-200 text-xs text-red-600">
          {error}
        </div>
      )}

      {/* Terminal state banner */}
      {isTerminal && (
        <div className="px-3 py-2 bg-slate-100 border-t border-slate-200 text-xs text-slate-500 text-center">
          {status === 'completed' && 'Akis tamamlandi.'}
          {status === 'error' && 'Akis hata ile sonlandi.'}
          {status === 'handed_off' && 'Musteri temsilcisine yonlendirildi.'}
          <button
            onClick={reset}
            className="ml-2 text-blue-600 hover:text-blue-500 underline"
          >
            Yeniden Baslat
          </button>
        </div>
      )}

      {/* Menu options */}
      {showMenuButtons && (
        <div className="px-3 py-2 border-t border-slate-200 bg-white flex flex-wrap gap-1.5">
          {pendingInput!.options!.map((opt) => (
            <button
              key={opt}
              onClick={() => handleMenuOption(opt)}
              disabled={isLoading}
              className={cn(
                'px-2.5 py-1 text-xs rounded-full border transition-colors',
                isLoading
                  ? 'border-slate-200 text-slate-300 cursor-not-allowed'
                  : 'border-emerald-300 text-emerald-700 bg-emerald-50 hover:bg-emerald-100'
              )}
            >
              {opt}
            </button>
          ))}
        </div>
      )}

      {/* Input area */}
      {!isTerminal && (
        <div className="px-3 py-2 border-t border-slate-200 bg-white flex gap-2 flex-shrink-0">
          <input
            ref={inputRef}
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={pendingInput?.type === 'menu' ? 'Secenek secin veya yazin...' : 'Mesaj yazin...'}
            disabled={isLoading}
            className="flex-1 text-xs border border-slate-200 rounded-full px-3 py-1.5 outline-none focus:border-emerald-400 focus:ring-1 focus:ring-emerald-400/30 disabled:bg-slate-50"
          />
          <button
            onClick={handleSend}
            disabled={isLoading || !input.trim()}
            className={cn(
              'w-7 h-7 rounded-full flex items-center justify-center flex-shrink-0 transition-colors',
              input.trim() && !isLoading
                ? 'bg-emerald-500 hover:bg-emerald-400 text-white'
                : 'bg-slate-100 text-slate-300 cursor-not-allowed'
            )}
          >
            <svg viewBox="0 0 24 24" fill="currentColor" className="w-3.5 h-3.5">
              <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
            </svg>
          </button>
        </div>
      )}
    </div>
  );
}
