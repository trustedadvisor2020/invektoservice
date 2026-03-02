import { useState, useEffect, useCallback, useRef } from 'react';
import { api, WebChatConversation, WebChatMessage } from '../lib/api';
import {
  MessageCircle,
  Send,
  RefreshCw,
  X,
  User,
  Bot,
  Headphones,
  Clock,
  Loader2,
  AlertCircle,
} from 'lucide-react';
import { cn } from '../lib/utils';

const POLL_INTERVAL = 5000;

export function WebChatPage() {
  const [conversations, setConversations] = useState<WebChatConversation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Active chat
  const [activeConvId, setActiveConvId] = useState<number | null>(null);
  const [messages, setMessages] = useState<WebChatMessage[]>([]);
  const [msgLoading, setMsgLoading] = useState(false);
  const [msgInput, setMsgInput] = useState('');
  const [sending, setSending] = useState(false);
  const [closing, setClosing] = useState(false);
  const [chatError, setChatError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  // Auto-dismiss chat errors after 4 seconds
  useEffect(() => {
    if (!chatError) return;
    const t = setTimeout(() => setChatError(null), 4000);
    return () => clearTimeout(t);
  }, [chatError]);

  // Fetch conversations
  const fetchConversations = useCallback(async () => {
    try {
      const result = await api.getWebChatConversations();
      setConversations(result.conversations);
      setError(null);
    } catch (err) {
      console.error('WebChat conversations fetch failed:', err);
      setError('Sohbetler yuklenemedi');
    } finally {
      setLoading(false);
    }
  }, []);

  // Fetch messages for active conversation
  const fetchMessages = useCallback(async (convId: number) => {
    try {
      const result = await api.getWebChatMessages(convId);
      setMessages(result.messages);
    } catch (err) {
      console.error('WebChat messages fetch failed:', err);
      setChatError('Mesajlar yuklenemedi');
    }
  }, []);

  // Initial load + polling
  useEffect(() => {
    fetchConversations();
    const interval = setInterval(fetchConversations, POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [fetchConversations]);

  // Poll messages for active conversation
  useEffect(() => {
    if (!activeConvId) return;
    const interval = setInterval(() => fetchMessages(activeConvId), POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [activeConvId, fetchMessages]);

  // Auto-scroll to bottom when messages change
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Select conversation
  const handleSelectConv = async (conv: WebChatConversation) => {
    setActiveConvId(conv.id);
    setMessages([]);
    setMsgLoading(true);
    setMsgInput('');
    await fetchMessages(conv.id);
    setMsgLoading(false);
    setTimeout(() => inputRef.current?.focus(), 100);
  };

  // Send message
  const handleSend = async () => {
    if (!activeConvId || !msgInput.trim() || sending) return;
    const content = msgInput.trim();
    setMsgInput('');
    setSending(true);
    try {
      const result = await api.sendWebChatMessage(activeConvId, content);
      setMessages(prev => [...prev, result.message]);
    } catch (err) {
      console.error('Send failed:', err);
      setChatError('Mesaj gonderilemedi');
      setMsgInput(content); // restore on failure
    } finally {
      setSending(false);
      inputRef.current?.focus();
    }
  };

  // Close conversation
  const handleClose = async () => {
    if (!activeConvId || closing) return;
    setClosing(true);
    try {
      await api.closeWebChatConversation(activeConvId);
      setActiveConvId(null);
      setMessages([]);
      await fetchConversations();
    } catch (err) {
      console.error('Close failed:', err);
      setChatError('Sohbet kapatilmadi');
    } finally {
      setClosing(false);
    }
  };

  // Key handler for textarea
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const activeConv = conversations.find(c => c.id === activeConvId);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-navy-900">WebChat</h1>
          <p className="text-sm text-navy-400">
            Aktif web sohbetleri
            {conversations.length > 0 && (
              <span className="ml-2 text-navy-300">({conversations.length} sohbet)</span>
            )}
          </p>
        </div>
        <button
          onClick={fetchConversations}
          disabled={loading}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-white border border-navy-100 rounded-lg hover:bg-navy-50 disabled:opacity-50"
        >
          <RefreshCw className={cn('w-3.5 h-3.5', loading && 'animate-spin')} />
          Yenile
        </button>
      </div>

      {/* Main layout: conversation list + chat window */}
      <div className="flex gap-4 h-[calc(100vh-12rem)]">
        {/* Left: Conversation list */}
        <div className="w-80 flex-shrink-0 bg-white rounded-lg border border-navy-100 flex flex-col overflow-hidden">
          <div className="px-4 py-3 border-b border-navy-100 bg-navy-50">
            <h2 className="text-sm font-semibold text-navy-700 flex items-center gap-2">
              <MessageCircle className="w-4 h-4" />
              Sohbetler
            </h2>
          </div>
          <div className="flex-1 overflow-y-auto">
            {loading && conversations.length === 0 ? (
              <div className="flex items-center justify-center py-12 text-navy-300 text-sm">
                <Loader2 className="w-4 h-4 animate-spin mr-2" />
                Yukleniyor...
              </div>
            ) : error ? (
              <div className="p-4 text-sm text-red-500">{error}</div>
            ) : conversations.length === 0 ? (
              <div className="p-4 text-sm text-navy-300 text-center">
                Aktif sohbet yok
              </div>
            ) : (
              conversations.map(conv => (
                <button
                  key={conv.id}
                  onClick={() => handleSelectConv(conv)}
                  className={cn(
                    'w-full text-left px-4 py-3 border-b border-navy-50 hover:bg-navy-50 transition-colors',
                    activeConvId === conv.id && 'bg-brand-50 border-l-2 border-l-brand-500'
                  )}
                >
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-sm font-medium text-navy-800 truncate">
                      {conv.visitor_name || conv.visitor_email || `Ziyaretci #${conv.visitor_id.slice(0, 8)}`}
                    </span>
                    <StatusBadge status={conv.status} />
                  </div>
                  {conv.visitor_email && conv.visitor_name && (
                    <p className="text-xs text-navy-400 truncate mb-1">{conv.visitor_email}</p>
                  )}
                  {conv.last_message && (
                    <p className="text-xs text-navy-400 truncate">
                      <SenderIcon type={conv.last_message.sender_type} />
                      {conv.last_message.content}
                    </p>
                  )}
                  <div className="flex items-center gap-1 mt-1.5 text-[10px] text-navy-300">
                    <Clock className="w-3 h-3" />
                    {formatTime(conv.last_message_at || conv.started_at)}
                  </div>
                </button>
              ))
            )}
          </div>
        </div>

        {/* Right: Chat window */}
        <div className="flex-1 bg-white rounded-lg border border-navy-100 flex flex-col overflow-hidden">
          {!activeConvId ? (
            <div className="flex-1 flex items-center justify-center text-navy-300">
              <div className="text-center">
                <MessageCircle className="w-12 h-12 mx-auto mb-3 opacity-30" />
                <p className="text-sm">Bir sohbet secin</p>
              </div>
            </div>
          ) : (
            <>
              {/* Chat header */}
              <div className="px-4 py-3 border-b border-navy-100 bg-navy-50 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-brand-100 flex items-center justify-center">
                    <User className="w-4 h-4 text-brand-600" />
                  </div>
                  <div>
                    <h3 className="text-sm font-semibold text-navy-800">
                      {activeConv?.visitor_name || activeConv?.visitor_email || `Ziyaretci #${activeConv?.visitor_id.slice(0, 8)}`}
                    </h3>
                    {activeConv?.visitor_email && activeConv?.visitor_name && (
                      <p className="text-xs text-navy-400">{activeConv.visitor_email}</p>
                    )}
                  </div>
                  {activeConv && <StatusBadge status={activeConv.status} />}
                </div>
                {activeConv?.status !== 'closed' && (
                  <button
                    onClick={handleClose}
                    disabled={closing}
                    className="flex items-center gap-1.5 px-3 py-1.5 text-xs bg-red-50 text-red-600 rounded-md hover:bg-red-100 disabled:opacity-50 transition-colors"
                  >
                    {closing ? <Loader2 className="w-3 h-3 animate-spin" /> : <X className="w-3 h-3" />}
                    Sohbeti Kapat
                  </button>
                )}
              </div>

              {/* Messages */}
              <div className="flex-1 overflow-y-auto px-4 py-4 space-y-2 bg-navy-50/30">
                {msgLoading ? (
                  <div className="flex items-center justify-center py-12 text-navy-300 text-sm">
                    <Loader2 className="w-4 h-4 animate-spin mr-2" />
                    Mesajlar yukleniyor...
                  </div>
                ) : messages.length === 0 ? (
                  <div className="text-center text-sm text-navy-300 py-12">Henuz mesaj yok</div>
                ) : (
                  messages.map(msg => <ChatBubble key={msg.id} message={msg} />)
                )}
                <div ref={messagesEndRef} />
              </div>

              {/* Error banner */}
              {chatError && (
                <div className="mx-4 mb-1 flex items-center gap-2 px-3 py-2 bg-red-50 border border-red-200 rounded-lg text-sm text-red-600">
                  <AlertCircle className="w-4 h-4 flex-shrink-0" />
                  {chatError}
                </div>
              )}

              {/* Input area */}
              {activeConv?.status !== 'closed' && (
                <div className="px-4 py-3 border-t border-navy-100 bg-white">
                  <div className="flex gap-2">
                    <textarea
                      ref={inputRef}
                      value={msgInput}
                      onChange={e => setMsgInput(e.target.value)}
                      onKeyDown={handleKeyDown}
                      placeholder="Mesajinizi yazin... (Enter ile gonderin)"
                      rows={1}
                      className="flex-1 resize-none rounded-lg border border-navy-200 px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-brand-500 focus:border-brand-500"
                    />
                    <button
                      onClick={handleSend}
                      disabled={!msgInput.trim() || sending}
                      className="flex items-center justify-center w-10 h-10 rounded-lg bg-brand-500 text-white hover:bg-brand-600 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                    >
                      {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// --- Sub-components ---

function ChatBubble({ message }: { message: WebChatMessage }) {
  const isVisitor = message.sender_type === 'visitor';
  const isAi = message.sender_type === 'ai';
  const isOperator = message.sender_type === 'operator';

  return (
    <div className={cn('flex mb-1', isVisitor ? 'justify-start' : 'justify-end')}>
      <div className="flex items-end gap-1.5 max-w-[75%]">
        {isVisitor && (
          <div className="w-6 h-6 rounded-full bg-navy-200 flex items-center justify-center flex-shrink-0 mb-0.5">
            <User className="w-3 h-3 text-navy-500" />
          </div>
        )}
        <div>
          <div
            className={cn(
              'rounded-xl px-3.5 py-2 text-sm whitespace-pre-wrap break-words',
              isVisitor && 'bg-white text-navy-900 border border-navy-100 rounded-bl-none',
              isOperator && 'bg-brand-500 text-white rounded-br-none',
              isAi && 'bg-emerald-500 text-white rounded-br-none'
            )}
          >
            {message.content}
          </div>
          <div className={cn('text-[10px] text-navy-300 mt-0.5', !isVisitor && 'text-right')}>
            {isAi && 'AI · '}{isOperator && 'Operator · '}{formatTime(message.created_at)}
          </div>
        </div>
        {(isOperator || isAi) && (
          <div className={cn(
            'w-6 h-6 rounded-full flex items-center justify-center flex-shrink-0 mb-0.5',
            isAi ? 'bg-emerald-100' : 'bg-brand-100'
          )}>
            {isAi ? <Bot className="w-3 h-3 text-emerald-600" /> : <Headphones className="w-3 h-3 text-brand-600" />}
          </div>
        )}
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const config: Record<string, { label: string; cls: string }> = {
    active: { label: 'Aktif', cls: 'bg-green-50 text-green-700' },
    ai: { label: 'AI', cls: 'bg-emerald-50 text-emerald-700' },
    closed: { label: 'Kapali', cls: 'bg-navy-100 text-navy-500' },
  };
  const c = config[status] || { label: status, cls: 'bg-navy-100 text-navy-500' };
  return (
    <span className={cn('inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium', c.cls)}>
      {c.label}
    </span>
  );
}

function SenderIcon({ type }: { type: string }) {
  if (type === 'ai') return <span className="mr-1">🤖</span>;
  if (type === 'operator') return <span className="mr-1">🎧</span>;
  return null;
}

function formatTime(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}
