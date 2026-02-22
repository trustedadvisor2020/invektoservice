import { useState, useEffect, useCallback } from 'react';
import { api, MessageLogEntry, MessageStoryResponse, TenantEntry } from '../lib/api';
import { ArrowDownLeft, ArrowUpRight, Search, RefreshCw, ChevronLeft, ChevronRight, ChevronDown, ChevronUp, Loader2 } from 'lucide-react';

const PAGE_SIZE = 50;

const ICON_MAP: Record<string, string> = {
  incoming: '\u{1F4E9}',
  flow: '\u{26A1}',
  ai: '\u{1F916}',
  reply: '\u{1F4AC}',
  callback: '\u{1F4E4}',
  faq: '\u{1F4D6}',
};

export function MessagesPage() {
  const [messages, setMessages] = useState<MessageLogEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);

  // Filters
  const [filterTenant, setFilterTenant] = useState('');
  const [filterPhone, setFilterPhone] = useState('');
  const [filterDirection, setFilterDirection] = useState('');
  const [filterFrom, setFilterFrom] = useState('');
  const [filterTo, setFilterTo] = useState('');

  // Tenants for dropdown
  const [tenants, setTenants] = useState<TenantEntry[]>([]);

  // Expanded story
  const [expandedId, setExpandedId] = useState<number | null>(null);
  const [storyLoading, setStoryLoading] = useState(false);
  const [story, setStory] = useState<MessageStoryResponse | null>(null);

  const fetchMessages = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.getOpsMessages({
        tenantId: filterTenant ? parseInt(filterTenant) : undefined,
        phone: filterPhone || undefined,
        direction: filterDirection || undefined,
        from: filterFrom || undefined,
        to: filterTo || undefined,
        limit: PAGE_SIZE,
        offset: page * PAGE_SIZE,
      });
      setMessages(result.messages);
      setTotal(result.total);
    } catch (err) {
      console.error('Messages fetch failed:', err);
    } finally {
      setLoading(false);
    }
  }, [filterTenant, filterPhone, filterDirection, filterFrom, filterTo, page]);

  useEffect(() => {
    fetchMessages();
  }, [fetchMessages]);

  useEffect(() => {
    api.getOpsTenants().then(r => setTenants(r.tenants)).catch(() => {});
  }, []);

  // Auto-refresh every 30s
  useEffect(() => {
    const interval = setInterval(fetchMessages, 30000);
    return () => clearInterval(interval);
  }, [fetchMessages]);

  const handleSearch = () => {
    setPage(0);
    fetchMessages();
  };

  const handleRowClick = async (msg: MessageLogEntry) => {
    if (msg.direction !== 'in') return;

    if (expandedId === msg.id) {
      setExpandedId(null);
      setStory(null);
      return;
    }

    setExpandedId(msg.id);
    setStory(null);
    setStoryLoading(true);
    try {
      const result = await api.getMessageStory(msg.id);
      setStory(result);
    } catch (err) {
      console.error('Story fetch failed:', err);
      setStory(null);
    } finally {
      setStoryLoading(false);
    }
  };

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-navy-900">Tum Mesajlar</h1>
          <p className="text-sm text-navy-400">
            Tum firmalara gelen ve giden WhatsApp mesajlari
            {total > 0 && <span className="ml-2 text-navy-300">({total} kayit)</span>}
          </p>
        </div>
        <button
          onClick={fetchMessages}
          disabled={loading}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-white border border-navy-100 rounded-lg hover:bg-navy-50 disabled:opacity-50"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
          Yenile
        </button>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg border border-navy-100 p-4">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-navy-400 mb-1">Firma</label>
            <select
              value={filterTenant}
              onChange={e => setFilterTenant(e.target.value)}
              className="w-44 px-2.5 py-1.5 text-sm border border-navy-100 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white"
            >
              <option value="">Tumu</option>
              {tenants.map(t => (
                <option key={t.tenantId} value={t.tenantId}>
                  #{t.tenantId} — {t.tenantName}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-navy-400 mb-1">Telefon</label>
            <input
              type="text"
              value={filterPhone}
              onChange={e => setFilterPhone(e.target.value)}
              placeholder="905..."
              className="w-36 px-2.5 py-1.5 text-sm border border-navy-100 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-navy-400 mb-1">Yon</label>
            <select
              value={filterDirection}
              onChange={e => setFilterDirection(e.target.value)}
              className="w-28 px-2.5 py-1.5 text-sm border border-navy-100 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500 bg-white"
            >
              <option value="">Tumu</option>
              <option value="in">Gelen</option>
              <option value="out">Giden</option>
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-navy-400 mb-1">Baslangic</label>
            <input
              type="date"
              value={filterFrom}
              onChange={e => setFilterFrom(e.target.value)}
              className="px-2.5 py-1.5 text-sm border border-navy-100 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-navy-400 mb-1">Bitis</label>
            <input
              type="date"
              value={filterTo}
              onChange={e => setFilterTo(e.target.value)}
              className="px-2.5 py-1.5 text-sm border border-navy-100 rounded-md focus:outline-none focus:ring-1 focus:ring-brand-500"
            />
          </div>
          <button
            onClick={handleSearch}
            className="flex items-center gap-1.5 px-4 py-1.5 text-sm bg-brand-500 text-white rounded-md hover:bg-brand-600"
          >
            <Search className="w-3.5 h-3.5" />
            Ara
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-lg border border-navy-100 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-navy-50 border-b border-navy-100">
                <th className="w-8 px-2 py-2.5"></th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Tarih</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Firma</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Telefon</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Yon</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Gonderen</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500 w-[35%]">Mesaj</th>
                <th className="text-left px-4 py-2.5 font-medium text-navy-500">Tur</th>
              </tr>
            </thead>
            <tbody>
              {loading && messages.length === 0 ? (
                <tr>
                  <td colSpan={8} className="text-center py-12 text-navy-300">
                    Yukleniyor...
                  </td>
                </tr>
              ) : messages.length === 0 ? (
                <tr>
                  <td colSpan={8} className="text-center py-12 text-navy-300">
                    Mesaj bulunamadi
                  </td>
                </tr>
              ) : (
                messages.map(msg => (
                  <>
                    <tr
                      key={msg.id}
                      onClick={() => handleRowClick(msg)}
                      className={`border-b border-navy-100 ${
                        msg.direction === 'in'
                          ? 'cursor-pointer hover:bg-blue-50/50'
                          : 'hover:bg-navy-50/50'
                      } ${expandedId === msg.id ? 'bg-blue-50/30' : ''}`}
                    >
                      <td className="px-2 py-2.5 text-center">
                        {msg.direction === 'in' && (
                          expandedId === msg.id
                            ? <ChevronUp className="w-3.5 h-3.5 text-blue-500 mx-auto" />
                            : <ChevronDown className="w-3.5 h-3.5 text-navy-300 mx-auto" />
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-navy-400 whitespace-nowrap text-xs">
                        {formatDate(msg.createdAt)}
                      </td>
                      <td className="px-4 py-2.5">
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-navy-100 text-navy-700">
                          #{msg.tenantId}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 font-mono text-xs text-navy-500">
                        {msg.phone}
                      </td>
                      <td className="px-4 py-2.5">
                        {msg.direction === 'in' ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-green-50 text-green-700">
                            <ArrowDownLeft className="w-3 h-3" />
                            Gelen
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-blue-50 text-blue-700">
                            <ArrowUpRight className="w-3 h-3" />
                            Giden
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-2.5 text-xs text-navy-400 max-w-[120px] truncate">
                        {msg.senderName || '-'}
                      </td>
                      <td className="px-4 py-2.5 text-navy-700 max-w-[400px]">
                        <p className="truncate text-xs" title={msg.messageText || ''}>
                          {msg.messageText || <span className="text-navy-300 italic">[medya]</span>}
                        </p>
                      </td>
                      <td className="px-4 py-2.5 text-xs text-navy-300">
                        {msg.messageType || 'text'}
                      </td>
                    </tr>
                    {expandedId === msg.id && (
                      <tr key={`story-${msg.id}`}>
                        <td colSpan={8} className="px-6 py-4 bg-navy-50/70 border-b border-navy-100">
                          <StoryTimeline loading={storyLoading} story={story} />
                        </td>
                      </tr>
                    )}
                  </>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-navy-100 bg-navy-50">
            <span className="text-xs text-navy-400">
              Sayfa {page + 1} / {totalPages}
            </span>
            <div className="flex gap-2">
              <button
                onClick={() => setPage(p => Math.max(0, p - 1))}
                disabled={page === 0}
                className="flex items-center gap-1 px-3 py-1 text-xs border border-navy-100 rounded-md hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <ChevronLeft className="w-3.5 h-3.5" />
                Onceki
              </button>
              <button
                onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
                disabled={page >= totalPages - 1}
                className="flex items-center gap-1 px-3 py-1 text-xs border border-navy-100 rounded-md hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Sonraki
                <ChevronRight className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

/** Timeline component for message story */
function StoryTimeline({ loading, story }: { loading: boolean; story: MessageStoryResponse | null }) {
  if (loading) {
    return (
      <div className="flex items-center gap-2 py-4 text-navy-300 text-sm">
        <Loader2 className="w-4 h-4 animate-spin" />
        Hikaye yukleniyor...
      </div>
    );
  }

  if (!story || story.timeline.length === 0) {
    return (
      <div className="py-4 text-sm text-navy-300">
        Bu mesaj icin hikaye bulunamadi. (Henuz islem yapilmamis olabilir)
      </div>
    );
  }

  return (
    <div className="flex gap-8">
      {/* Timeline */}
      <div className="flex-1 relative">
        <div className="absolute left-[18px] top-3 bottom-3 w-0.5 bg-navy-100" />
        <div className="space-y-3">
          {story.timeline.map((item, i) => (
            <div key={i} className="flex items-start gap-3 relative">
              <div className="w-9 h-9 rounded-full bg-white border-2 border-navy-100 flex items-center justify-center text-sm z-10 shrink-0">
                {ICON_MAP[item.icon] || '\u{2022}'}
              </div>
              <div className="pt-1 min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-semibold text-navy-700">{item.title}</span>
                  <span className="text-[10px] text-navy-300 font-mono">{item.time}</span>
                </div>
                <p className="text-xs text-navy-400 mt-0.5 break-words">{item.detail}</p>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Summary card */}
      {story.summary && (
        <div className="w-56 shrink-0 bg-white rounded-lg border border-navy-100 p-3 h-fit">
          <h4 className="text-xs font-semibold text-navy-700 mb-2">Ozet</h4>
          <dl className="space-y-1.5 text-xs">
            {story.summary.flow_name && (
              <>
                <dt className="text-navy-300">Flow</dt>
                <dd className="text-navy-500 font-medium">{story.summary.flow_name}</dd>
              </>
            )}
            {story.summary.intent && (
              <>
                <dt className="text-navy-300">Intent</dt>
                <dd className="text-navy-500 font-medium">{story.summary.intent}</dd>
              </>
            )}
            {story.summary.confidence != null && (
              <>
                <dt className="text-navy-300">Confidence</dt>
                <dd className="text-navy-500 font-medium">{(story.summary.confidence * 1).toFixed(2)}</dd>
              </>
            )}
            {story.summary.processing_time_ms != null && (
              <>
                <dt className="text-navy-300">Sure</dt>
                <dd className="text-navy-500 font-medium">{story.summary.processing_time_ms}ms</dd>
              </>
            )}
            <dt className="text-navy-300">Otomatik Yanit</dt>
            <dd className="text-navy-500 font-medium">{story.summary.auto_reply_count} yanit, {story.summary.outgoing_count} giden</dd>
          </dl>
        </div>
      )}
    </div>
  );
}

function formatDate(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString('tr-TR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  } catch {
    return iso;
  }
}
