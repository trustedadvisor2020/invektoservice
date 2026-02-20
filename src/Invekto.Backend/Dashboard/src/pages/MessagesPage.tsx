import { useState, useEffect, useCallback } from 'react';
import { api, MessageLogEntry } from '../lib/api';
import { ArrowDownLeft, ArrowUpRight, Search, RefreshCw, ChevronLeft, ChevronRight } from 'lucide-react';

const PAGE_SIZE = 50;

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

  // Auto-refresh every 30s
  useEffect(() => {
    const interval = setInterval(fetchMessages, 30000);
    return () => clearInterval(interval);
  }, [fetchMessages]);

  const handleSearch = () => {
    setPage(0);
    fetchMessages();
  };

  const totalPages = Math.ceil(total / PAGE_SIZE);

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-800">Tum Mesajlar</h1>
          <p className="text-sm text-slate-500">
            Tum firmalara gelen ve giden WhatsApp mesajlari
            {total > 0 && <span className="ml-2 text-slate-400">({total} kayit)</span>}
          </p>
        </div>
        <button
          onClick={fetchMessages}
          disabled={loading}
          className="flex items-center gap-1.5 px-3 py-1.5 text-sm bg-white border border-slate-200 rounded-lg hover:bg-slate-50 disabled:opacity-50"
        >
          <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
          Yenile
        </button>
      </div>

      {/* Filters */}
      <div className="bg-white rounded-lg border border-slate-200 p-4">
        <div className="flex flex-wrap gap-3 items-end">
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">Firma ID</label>
            <input
              type="number"
              value={filterTenant}
              onChange={e => setFilterTenant(e.target.value)}
              placeholder="Tum"
              className="w-24 px-2.5 py-1.5 text-sm border border-slate-200 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">Telefon</label>
            <input
              type="text"
              value={filterPhone}
              onChange={e => setFilterPhone(e.target.value)}
              placeholder="905..."
              className="w-36 px-2.5 py-1.5 text-sm border border-slate-200 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">Yon</label>
            <select
              value={filterDirection}
              onChange={e => setFilterDirection(e.target.value)}
              className="w-28 px-2.5 py-1.5 text-sm border border-slate-200 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500 bg-white"
            >
              <option value="">Tumu</option>
              <option value="in">Gelen</option>
              <option value="out">Giden</option>
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">Baslangic</label>
            <input
              type="date"
              value={filterFrom}
              onChange={e => setFilterFrom(e.target.value)}
              className="px-2.5 py-1.5 text-sm border border-slate-200 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-slate-500 mb-1">Bitis</label>
            <input
              type="date"
              value={filterTo}
              onChange={e => setFilterTo(e.target.value)}
              className="px-2.5 py-1.5 text-sm border border-slate-200 rounded-md focus:outline-none focus:ring-1 focus:ring-blue-500"
            />
          </div>
          <button
            onClick={handleSearch}
            className="flex items-center gap-1.5 px-4 py-1.5 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700"
          >
            <Search className="w-3.5 h-3.5" />
            Ara
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-lg border border-slate-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-200">
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Tarih</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Firma</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Telefon</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Yon</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Gonderen</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600 w-[40%]">Mesaj</th>
                <th className="text-left px-4 py-2.5 font-medium text-slate-600">Tur</th>
              </tr>
            </thead>
            <tbody>
              {loading && messages.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-12 text-slate-400">
                    Yukleniyor...
                  </td>
                </tr>
              ) : messages.length === 0 ? (
                <tr>
                  <td colSpan={7} className="text-center py-12 text-slate-400">
                    Mesaj bulunamadi
                  </td>
                </tr>
              ) : (
                messages.map(msg => (
                  <tr key={msg.id} className="border-b border-slate-100 hover:bg-slate-50/50">
                    <td className="px-4 py-2.5 text-slate-500 whitespace-nowrap text-xs">
                      {formatDate(msg.createdAt)}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-slate-100 text-slate-700">
                        #{msg.tenantId}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-mono text-xs text-slate-600">
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
                    <td className="px-4 py-2.5 text-xs text-slate-500 max-w-[120px] truncate">
                      {msg.senderName || '-'}
                    </td>
                    <td className="px-4 py-2.5 text-slate-700 max-w-[400px]">
                      <p className="truncate text-xs" title={msg.messageText || ''}>
                        {msg.messageText || <span className="text-slate-400 italic">[medya]</span>}
                      </p>
                    </td>
                    <td className="px-4 py-2.5 text-xs text-slate-400">
                      {msg.messageType || 'text'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-slate-200 bg-slate-50">
            <span className="text-xs text-slate-500">
              Sayfa {page + 1} / {totalPages}
            </span>
            <div className="flex gap-2">
              <button
                onClick={() => setPage(p => Math.max(0, p - 1))}
                disabled={page === 0}
                className="flex items-center gap-1 px-3 py-1 text-xs border border-slate-200 rounded-md hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                <ChevronLeft className="w-3.5 h-3.5" />
                Onceki
              </button>
              <button
                onClick={() => setPage(p => Math.min(totalPages - 1, p + 1))}
                disabled={page >= totalPages - 1}
                className="flex items-center gap-1 px-3 py-1 text-xs border border-slate-200 rounded-md hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
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
