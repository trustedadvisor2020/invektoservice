import { useRef, useEffect, useState, useMemo, useCallback, type KeyboardEvent } from 'react';
import { useWizardStore } from '../../../stores/wizard-store';
import { renderWithNodeChips } from './NodeChip';
import type { WizardMessage, WizardOption } from '../../../types/wizard';

const thinkingMessages = [
  'Bir saniye, dusunuyorum...',
  'Hmm, guzel bir seyler geliyor...',
  'Fikirlerimi topluyorum...',
  'Yaratici moduma gectim...',
  'Hemen hazirliyorum...',
  'Sizin icin en iyisini dusunuyorum...',
];

/** Split long assistant messages into summary + collapsible detail.
 *  If the last paragraph contains a question (?), keep it visible in the summary
 *  so the user always sees what's being asked. */
function splitContent(text: string): { summary: string; detail: string | null } {
  const paragraphs = text.split(/\n\n+/);
  if (paragraphs.length <= 2 && text.length < 300) {
    return { summary: text, detail: null };
  }
  const last = paragraphs[paragraphs.length - 1];
  if (last.includes('?') && paragraphs.length > 2) {
    return {
      summary: paragraphs[0] + '\n\n' + last,
      detail: paragraphs.slice(1, -1).join('\n\n'),
    };
  }
  return { summary: paragraphs[0], detail: paragraphs.slice(1).join('\n\n') };
}

export function WizardChat() {
  const messages = useWizardStore(s => s.messages);
  const isStreaming = useWizardStore(s => s.isStreaming);
  const streamingText = useWizardStore(s => s.streamingText);
  const pendingOptions = useWizardStore(s => s.pendingOptions);
  const error = useWizardStore(s => s.error);
  const sendMessage = useWizardStore(s => s.sendMessage);

  const [input, setInput] = useState('');
  const [freeInput, setFreeInput] = useState('');
  const scrollRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const thinkingText = useMemo(
    () => thinkingMessages[Math.floor(Math.random() * thinkingMessages.length)],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [isStreaming],
  );

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, streamingText]);

  useEffect(() => {
    if (!isStreaming) inputRef.current?.focus();
  }, [isStreaming]);

  const handleSend = () => {
    const text = input.trim();
    if (!text || isStreaming) return;
    setInput('');
    sendMessage(text);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleOptionClick = useCallback((option: WizardOption) => {
    if (isStreaming) return;
    sendMessage(option.label);
  }, [isStreaming, sendMessage]);

  const handleFreeSend = useCallback(() => {
    const text = freeInput.trim();
    if (!text || isStreaming) return;
    setFreeInput('');
    sendMessage(text);
  }, [freeInput, isStreaming, sendMessage]);

  return (
    <div className="flex flex-col h-full">
      {/* Messages area */}
      <div ref={scrollRef} className="flex-1 overflow-y-auto p-4 space-y-4">
        {messages.length === 0 && !isStreaming && (
          <div className="flex flex-col items-center justify-center h-full text-center px-8">
            <div className="w-16 h-16 rounded-2xl bg-purple-50 flex items-center justify-center mb-4">
              <svg className="w-8 h-8 text-purple-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456z" />
              </svg>
            </div>
            <h3 className="text-lg font-semibold text-navy-900 mb-2">AI Flow Wizard</h3>
            <p className="text-sm text-navy-400 max-w-md">
              Olusturmak istediginiz chatbot akisini dogal dille anlatın.
              AI size sorular sorarak en uygun akisi tasarlayacak.
            </p>
            <div className="mt-4 flex flex-wrap gap-2 justify-center">
              {['Musteri karsilama akisi', 'Randevu alma botu', 'Siparis takip sistemi', 'FAQ cevaplama'].map(s => (
                <button
                  key={s}
                  onClick={() => setInput(s)}
                  className="px-3 py-1.5 text-xs bg-purple-50 text-purple-600 rounded-full hover:bg-purple-100 transition-colors"
                >
                  {s}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((msg, i) => (
          <MessageBubble key={i} message={msg} />
        ))}

        {/* Streaming text */}
        {isStreaming && streamingText && (
          <div className="flex gap-3">
            <div className="w-7 h-7 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0 mt-0.5">
              <svg className="w-4 h-4 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
              </svg>
            </div>
            <div className="bg-white border border-navy-100 rounded-xl px-4 py-3 max-w-[85%] text-sm text-navy-800 whitespace-pre-wrap">
              {renderWithNodeChips(streamingText)}
              <span className="inline-block w-1.5 h-4 bg-purple-400 ml-0.5 animate-pulse" />
            </div>
          </div>
        )}

        {/* Loading indicator */}
        {isStreaming && !streamingText && (
          <div className="flex gap-3">
            <div className="w-7 h-7 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0">
              <svg className="w-4 h-4 text-purple-600 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
            </div>
            <div className="bg-white border border-navy-100 rounded-xl px-4 py-3 text-sm text-navy-400">
              {thinkingText}
            </div>
          </div>
        )}

        {/* Error */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 text-sm rounded-xl px-4 py-3">
            {error}
          </div>
        )}

        {/* Option buttons */}
        {!isStreaming && pendingOptions && pendingOptions.length > 0 && (
          <div className="space-y-2 pt-1">
            {pendingOptions.map((opt, i) => (
              <button
                key={i}
                onClick={() => handleOptionClick(opt)}
                className="w-full text-left px-4 py-3 bg-white border border-navy-100 rounded-xl shadow-soft hover:border-purple-400 hover:shadow-focus transition-all duration-150 group"
              >
                <div className="text-sm font-medium text-navy-900 group-hover:text-purple-600">{opt.label}</div>
                {opt.description && (
                  <div className="text-xs text-navy-400 mt-0.5 leading-relaxed">{opt.description}</div>
                )}
              </button>
            ))}
            <div className="flex items-center gap-2 pt-0.5">
              <div className="flex-1 h-px bg-navy-100" />
              <span className="text-xs text-navy-300">veya</span>
              <div className="flex-1 h-px bg-navy-100" />
            </div>
            <div className="flex gap-2">
              <input
                value={freeInput}
                onChange={e => setFreeInput(e.target.value)}
                onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); handleFreeSend(); } }}
                placeholder="Kendi cevabinizi yazin..."
                className="flex-1 px-3 py-2 bg-white border border-navy-100 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-purple-300 focus:border-purple-300 placeholder:text-navy-300"
              />
              <button
                onClick={handleFreeSend}
                disabled={!freeInput.trim()}
                className={`w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0 transition-colors ${
                  freeInput.trim()
                    ? 'bg-purple-500 hover:bg-purple-600 text-white'
                    : 'bg-navy-50 text-navy-200 cursor-not-allowed'
                }`}
              >
                <svg viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
                  <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z" />
                </svg>
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Input area */}
      <div className="border-t border-navy-100 p-4 bg-white">
        <div className="flex gap-2">
          <textarea
            ref={inputRef}
            value={input}
            onChange={e => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Akisinizi anlatin... (Shift+Enter = yeni satir)"
            rows={2}
            disabled={isStreaming}
            className="flex-1 px-3 py-2.5 bg-navy-25 border border-navy-100 rounded-xl text-sm resize-none focus:outline-none focus:ring-2 focus:ring-purple-300 focus:border-purple-300 disabled:opacity-50 placeholder:text-navy-300"
          />
          <button
            onClick={handleSend}
            disabled={isStreaming || !input.trim()}
            className="px-4 py-2 bg-purple-500 hover:bg-purple-600 text-white rounded-xl text-sm font-medium transition-colors disabled:opacity-40 disabled:cursor-not-allowed self-end"
          >
            Gonder
          </button>
        </div>
      </div>
    </div>
  );
}

function CollapsibleContent({ text }: { text: string }) {
  const { summary, detail } = splitContent(text);
  return (
    <>
      <div className="whitespace-pre-wrap">{renderWithNodeChips(summary)}</div>
      {detail && (
        <details className="mt-2 group/detail">
          <summary className="text-xs text-purple-500 cursor-pointer hover:text-purple-600 select-none list-none flex items-center gap-1">
            <svg className="w-3.5 h-3.5 transition-transform group-open/detail:rotate-90" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M8.25 4.5l7.5 7.5-7.5 7.5" />
            </svg>
            Detaylari goster
          </summary>
          <div className="mt-1.5 pt-1.5 border-t border-navy-50 whitespace-pre-wrap text-navy-600">
            {renderWithNodeChips(detail)}
          </div>
        </details>
      )}
    </>
  );
}

function MessageBubble({ message }: { message: WizardMessage }) {
  const isUser = message.role === 'user';

  if (isUser) {
    return (
      <div className="flex gap-3 justify-end">
        <div className="bg-purple-500 text-white rounded-xl px-4 py-3 max-w-[85%] text-sm whitespace-pre-wrap">
          {message.content}
        </div>
        <div className="w-7 h-7 rounded-full bg-navy-100 flex items-center justify-center flex-shrink-0 mt-0.5">
          <svg className="w-4 h-4 text-navy-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 6a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0zM4.501 20.118a7.5 7.5 0 0114.998 0" />
          </svg>
        </div>
      </div>
    );
  }

  return (
    <div className="flex gap-3">
      <div className="w-7 h-7 rounded-full bg-purple-100 flex items-center justify-center flex-shrink-0 mt-0.5">
        <svg className="w-4 h-4 text-purple-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.5}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09z" />
        </svg>
      </div>
      <div className="bg-white border border-navy-100 rounded-xl px-4 py-3 max-w-[85%] text-sm text-navy-800">
        <CollapsibleContent text={message.content} />
        {message.flow_config_snapshot && (
          <div className="mt-2 pt-2 border-t border-navy-100 text-xs text-green-600 font-medium flex items-center gap-1">
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Akis yapisi olusturuldu — on izlemeye bakin
          </div>
        )}
      </div>
    </div>
  );
}
