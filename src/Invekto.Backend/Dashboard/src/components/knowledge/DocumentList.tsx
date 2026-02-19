import { useState, useEffect, useCallback } from 'react';
import { Trash2, RefreshCw, FileText, AlertCircle, CheckCircle, Clock, Loader2 } from 'lucide-react';
import { api, DocumentDto } from '../../lib/api';
import { cn } from '../../lib/utils';

interface Props {
  tenantId: number;
  refreshKey: number;
}

const STATUS_CONFIG: Record<string, { icon: typeof Clock; color: string; label: string }> = {
  pending: { icon: Clock, color: 'text-yellow-600 bg-yellow-50', label: 'Bekliyor' },
  processing: { icon: Loader2, color: 'text-blue-600 bg-blue-50', label: 'Isleniyor' },
  ready: { icon: CheckCircle, color: 'text-green-600 bg-green-50', label: 'Hazir' },
  error: { icon: AlertCircle, color: 'text-red-600 bg-red-50', label: 'Hata' },
};

export function DocumentList({ tenantId, refreshKey }: Props) {
  const [documents, setDocuments] = useState<DocumentDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<number | null>(null);

  const fetchDocuments = useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.getDocuments(tenantId, { page, limit: 20 });
      setDocuments(result.documents);
      setTotal(result.total);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Dokumanlar yuklenemedi');
    } finally {
      setLoading(false);
    }
  }, [tenantId, page, refreshKey]);

  useEffect(() => { fetchDocuments(); }, [fetchDocuments]);

  // Poll for processing documents
  useEffect(() => {
    const hasProcessing = documents.some(d => d.status === 'pending' || d.status === 'processing');
    if (!hasProcessing) return;
    const interval = setInterval(fetchDocuments, 5000);
    return () => clearInterval(interval);
  }, [documents, fetchDocuments]);

  const handleDelete = async (docId: number) => {
    if (!confirm('Bu dokuman ve tum parcalari silinsin mi?')) return;
    setDeleting(docId);
    try {
      await api.deleteDocument(tenantId, docId);
      fetchDocuments();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Dokuman silinemedi');
    } finally {
      setDeleting(null);
    }
  };

  const totalPages = Math.ceil(total / 20);

  return (
    <div className="bg-white rounded-lg border border-slate-200">
      <div className="flex items-center justify-between px-4 py-3 border-b border-slate-200">
        <h3 className="text-sm font-medium text-slate-700">
          Dokumanlar {total > 0 && <span className="text-slate-400">({total})</span>}
        </h3>
        <button onClick={fetchDocuments} className="text-slate-400 hover:text-slate-600">
          <RefreshCw className={cn('w-4 h-4', loading && 'animate-spin')} />
        </button>
      </div>

      {error && <p className="px-4 py-2 text-sm text-red-600 bg-red-50">{error}</p>}

      {documents.length === 0 && !error ? (
        <div className="p-8 text-center text-sm text-slate-400">
          <FileText className="w-8 h-8 mx-auto mb-2 opacity-50" />
          Henuz dokuman yuklenmemis
        </div>
      ) : (
        <div className="divide-y divide-slate-100">
          {documents.map(doc => {
            const cfg = STATUS_CONFIG[doc.status] || STATUS_CONFIG.error;
            const Icon = cfg.icon;
            return (
              <div key={doc.id} className="flex items-center px-4 py-3 hover:bg-slate-50">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-slate-800 truncate">{doc.title}</span>
                    <span className={cn('inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium', cfg.color)}>
                      <Icon className={cn('w-3 h-3', doc.status === 'processing' && 'animate-spin')} />
                      {cfg.label}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 mt-0.5 text-xs text-slate-400">
                    <span>{doc.sourceType.toUpperCase()}</span>
                    {doc.chunkCount > 0 && <span>{doc.chunkCount} parca</span>}
                    <span>{new Date(doc.createdAt).toLocaleDateString()}</span>
                  </div>
                </div>
                <button
                  onClick={() => handleDelete(doc.id)}
                  disabled={deleting === doc.id}
                  className="ml-2 p-1.5 text-slate-400 hover:text-red-600 transition-colors"
                >
                  <Trash2 className={cn('w-4 h-4', deleting === doc.id && 'animate-pulse')} />
                </button>
              </div>
            );
          })}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 px-4 py-3 border-t border-slate-200">
          <button
            onClick={() => setPage(p => Math.max(1, p - 1))}
            disabled={page === 1}
            className="px-3 py-1 text-xs text-slate-600 bg-slate-100 rounded disabled:opacity-50"
          >
            Onceki
          </button>
          <span className="text-xs text-slate-500">{page} / {totalPages}</span>
          <button
            onClick={() => setPage(p => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
            className="px-3 py-1 text-xs text-slate-600 bg-slate-100 rounded disabled:opacity-50"
          >
            Sonraki
          </button>
        </div>
      )}
    </div>
  );
}
