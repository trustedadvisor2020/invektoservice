import { useState, useEffect, useCallback } from 'react';
import { Plus, Pencil, Trash2, RefreshCw, MessageSquare } from 'lucide-react';
import { api, FaqDto } from '../../lib/api';
import { FaqEditModal } from './FaqEditModal';
import { cn } from '../../lib/utils';

interface Props {
  tenantId: number;
}

export function FaqManager({ tenantId }: Props) {
  const [faqs, setFaqs] = useState<FaqDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editFaq, setEditFaq] = useState<FaqDto | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [deleting, setDeleting] = useState<number | null>(null);
  const [categoryFilter, setCategoryFilter] = useState('');

  const fetchFaqs = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.getFaqs(tenantId, {
        page,
        limit: 20,
        category: categoryFilter || undefined,
      });
      setFaqs(result.faqs);
      setTotal(result.total);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'SSS yüklenemedi');
    } finally {
      setLoading(false);
    }
  }, [tenantId, page, categoryFilter]);

  useEffect(() => { fetchFaqs(); }, [fetchFaqs]);

  const handleDelete = async (faqId: number) => {
    if (!confirm('Bu SSS silinsin mi?')) return;
    setDeleting(faqId);
    try {
      await api.deleteFaq(tenantId, faqId);
      fetchFaqs();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'SSS silinemedi');
    } finally {
      setDeleting(null);
    }
  };

  const handleSave = () => {
    setEditFaq(null);
    setShowCreate(false);
    fetchFaqs();
  };

  const totalPages = Math.ceil(total / 20);

  return (
    <div className="bg-white rounded-lg border border-navy-100">
      <div className="flex items-center justify-between px-4 py-3 border-b border-navy-100">
        <div className="flex items-center gap-3">
          <h3 className="text-sm font-medium text-navy-700">
            SSS {total > 0 && <span className="text-navy-300">({total})</span>}
          </h3>
          <input
            type="text"
            value={categoryFilter}
            onChange={e => { setCategoryFilter(e.target.value); setPage(1); }}
            placeholder="Kategoriye göre filtrele"
            className="px-2 py-1 text-xs border border-navy-100 rounded"
          />
        </div>
        <div className="flex items-center gap-2">
          <button onClick={fetchFaqs} className="text-navy-300 hover:text-navy-500">
            <RefreshCw className={cn('w-4 h-4', loading && 'animate-spin')} />
          </button>
          <button
            onClick={() => setShowCreate(true)}
            className="flex items-center gap-1 px-3 py-1.5 text-xs bg-brand-500 text-white rounded-md hover:bg-brand-600 transition-colors"
          >
            <Plus className="w-3.5 h-3.5" />
            SSS Ekle
          </button>
        </div>
      </div>

      {error && <p className="px-4 py-2 text-sm text-red-600 bg-red-50">{error}</p>}

      {faqs.length === 0 && !error ? (
        <div className="p-8 text-center text-sm text-navy-300">
          <MessageSquare className="w-8 h-8 mx-auto mb-2 opacity-50" />
          SSS bulunamadı
        </div>
      ) : (
        <div className="divide-y divide-navy-100">
          {faqs.map(faq => (
            <div key={faq.id} className="px-4 py-3 hover:bg-navy-50">
              <div className="flex items-start justify-between gap-3">
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-navy-900 line-clamp-1">{faq.question}</p>
                  <p className="text-xs text-navy-400 mt-0.5 line-clamp-2">{faq.answer}</p>
                  <div className="flex items-center gap-2 mt-1">
                    {faq.category && (
                      <span className="px-1.5 py-0.5 text-xs bg-navy-100 text-navy-500 rounded">{faq.category}</span>
                    )}
                    <span className="text-xs text-navy-300">{faq.lang}</span>
                    <span className="text-xs text-navy-300">{faq.source}</span>
                    {faq.keywords.length > 0 && (
                      <span className="text-xs text-navy-300">{faq.keywords.length} anahtar kelime</span>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-1 flex-shrink-0">
                  <button
                    onClick={() => setEditFaq(faq)}
                    className="p-1.5 text-navy-300 hover:text-blue-600 transition-colors"
                  >
                    <Pencil className="w-3.5 h-3.5" />
                  </button>
                  <button
                    onClick={() => handleDelete(faq.id)}
                    disabled={deleting === faq.id}
                    className="p-1.5 text-navy-300 hover:text-red-600 transition-colors"
                  >
                    <Trash2 className={cn('w-3.5 h-3.5', deleting === faq.id && 'animate-pulse')} />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 px-4 py-3 border-t border-navy-100">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            className="px-3 py-1 text-xs text-navy-500 bg-navy-100 rounded disabled:opacity-50"
          >
            Önceki
          </button>
          <span className="text-xs text-navy-400">{page} / {totalPages}</span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
            className="px-3 py-1 text-xs text-navy-500 bg-navy-100 rounded disabled:opacity-50"
          >
            Sonraki
          </button>
        </div>
      )}

      {/* Modals */}
      {showCreate && (
        <FaqEditModal
          tenantId={tenantId}
          onClose={() => setShowCreate(false)}
          onSave={handleSave}
        />
      )}
      {editFaq && (
        <FaqEditModal
          tenantId={tenantId}
          faq={editFaq}
          onClose={() => setEditFaq(null)}
          onSave={handleSave}
        />
      )}
    </div>
  );
}
