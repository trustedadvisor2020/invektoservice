// Adim 3 Paket 3-C: Super-admin cross-tenant Zoho ops dashboard.
// Iki tab: Baglantilar (list + force-disconnect) ve Senkron Kaydi (filter + batch retry).
// Tum UI TR. API: /api/ops/zoho/* (ValidateOpsAuth). Max batch retry: 50 row.
import { useCallback, useEffect, useMemo, useState } from 'react';
import { RefreshCw, AlertCircle } from 'lucide-react';
import {
  api,
  ApiClientError,
  type ZohoOpsConnectionEntryDto,
  type ZohoOpsConnectionListResponse,
  type ZohoOpsSyncLogPageResponse,
  type ZohoOpsSyncLogQuery,
  type ZohoOpsBatchRetryResponse,
  type ZohoSyncLogStatus,
} from '../lib/api';
import { formatDateTr } from '../lib/utils';
import { Button } from '../components/ui/Button';
import { OpsZohoDisconnectModal } from '../components/zoho/OpsZohoDisconnectModal';
import { OpsZohoRetryBatchModal } from '../components/zoho/OpsZohoRetryBatchModal';

const MAX_BATCH_SIZE = 50;
const DEFAULT_PAGE_SIZE = 20;

type Tab = 'connections' | 'sync-log';

const STATUS_BADGE: Record<ZohoSyncLogStatus, string> = {
  pending: 'bg-yellow-100 text-yellow-800',
  failed: 'bg-red-100 text-red-800',
  success: 'bg-green-100 text-green-800',
};

const STATUS_LABEL: Record<ZohoSyncLogStatus, string> = {
  pending: 'Beklemede',
  failed: 'Başarısız',
  success: 'Başarılı',
};

// P3-C frontend error display: actionable message with INV-* code when present.
// ApiClientError yapisi (lib/api.ts): status + errorCode + message + requestId.
// Upstream (Integrations) INV-INT-* kodlari buraya kadar bozulmadan ulasir.
function extractError(err: unknown, fallback: string): string {
  if (err instanceof ApiClientError) {
    const code = err.errorCode && err.errorCode !== 'UNKNOWN' ? err.errorCode : 'INV-INT-FE-131';
    const msg = err.message && err.message !== `HTTP ${err.status}` ? err.message : fallback;
    return `[${code}] ${msg}`;
  }
  if (err && typeof err === 'object' && 'message' in err && typeof (err as { message: unknown }).message === 'string') {
    const msg = (err as { message: string }).message;
    return msg ? `[INV-INT-FE-131] ${msg}` : `[INV-INT-FE-131] ${fallback}`;
  }
  return `[INV-INT-FE-131] ${fallback}`;
}

export function OpsZohoPage() {
  const [tab, setTab] = useState<Tab>('connections');
  const [connections, setConnections] = useState<ZohoOpsConnectionListResponse | null>(null);
  const [connectionsLoading, setConnectionsLoading] = useState(false);
  const [connectionsError, setConnectionsError] = useState<string | null>(null);

  const [syncLog, setSyncLog] = useState<ZohoOpsSyncLogPageResponse | null>(null);
  const [syncLogLoading, setSyncLogLoading] = useState(false);
  const [syncLogError, setSyncLogError] = useState<string | null>(null);
  const [filters, setFilters] = useState<ZohoOpsSyncLogQuery>({ page: 1, pageSize: DEFAULT_PAGE_SIZE });
  const [tenantFilterInput, setTenantFilterInput] = useState('');

  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());

  const [disconnectTarget, setDisconnectTarget] = useState<number | null>(null);
  const [disconnectBusy, setDisconnectBusy] = useState(false);

  const [retryModalOpen, setRetryModalOpen] = useState(false);
  const [retryBusy, setRetryBusy] = useState(false);
  const [retryResult, setRetryResult] = useState<ZohoOpsBatchRetryResponse | null>(null);

  const fetchConnections = useCallback(async () => {
    setConnectionsLoading(true);
    setConnectionsError(null);
    try {
      const res = await api.getOpsZohoConnections();
      setConnections(res);
    } catch (err) {
      setConnectionsError(extractError(err, 'Bağlantı listesi yüklenemedi.'));
    } finally {
      setConnectionsLoading(false);
    }
  }, []);

  const fetchSyncLog = useCallback(async (q: ZohoOpsSyncLogQuery) => {
    setSyncLogLoading(true);
    setSyncLogError(null);
    try {
      const res = await api.getOpsZohoSyncLog(q);
      setSyncLog(res);
      setSelectedIds(new Set());
    } catch (err) {
      setSyncLogError(extractError(err, 'Senkron kaydı yüklenemedi.'));
    } finally {
      setSyncLogLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchConnections();
  }, [fetchConnections]);

  useEffect(() => {
    if (tab === 'sync-log') fetchSyncLog(filters);
  }, [tab, filters, fetchSyncLog]);

  const handleApplyFilters = () => {
    const parsedTenant = tenantFilterInput.trim() === '' ? null : Number.parseInt(tenantFilterInput.trim(), 10);
    setFilters((prev) => ({ ...prev, tenantId: Number.isFinite(parsedTenant) ? parsedTenant : null, page: 1 }));
  };

  const handleConfirmDisconnect = async () => {
    if (disconnectTarget === null) return;
    setDisconnectBusy(true);
    try {
      await api.forceDisconnectOpsZoho(disconnectTarget);
      setDisconnectTarget(null);
      await fetchConnections();
    } catch (err) {
      setConnectionsError(extractError(err, 'Force-disconnect başarısız.'));
    } finally {
      setDisconnectBusy(false);
    }
  };

  const handleConfirmBatchRetry = async () => {
    const ids = Array.from(selectedIds);
    if (ids.length === 0) return;
    setRetryBusy(true);
    try {
      const res = await api.batchRetryOpsZoho(ids);
      setRetryResult(res);
      setRetryModalOpen(false);
      await fetchSyncLog(filters);
    } catch (err) {
      setSyncLogError(extractError(err, 'Toplu tekrar deneme başarısız.'));
    } finally {
      setRetryBusy(false);
    }
  };

  const toggleRow = (id: number) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else if (next.size >= MAX_BATCH_SIZE) return prev;
      else next.add(id);
      return next;
    });
  };

  const failedRowsVisible = useMemo(
    () => (syncLog?.items ?? []).filter((r) => r.status === 'failed'),
    [syncLog],
  );

  const totalPages = syncLog ? Math.max(1, Math.ceil(syncLog.totalCount / syncLog.pageSize)) : 1;

  return (
    <div className="p-6">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-2xl font-semibold text-navy-900">Zoho Yönetim (Super-Admin)</h1>
        <Button
          variant="secondary"
          onClick={() => (tab === 'connections' ? fetchConnections() : fetchSyncLog(filters))}
          disabled={connectionsLoading || syncLogLoading}
        >
          <RefreshCw className="w-4 h-4 mr-2" />
          Yenile
        </Button>
      </div>

      {/* Status cards */}
      {connections && (
        <div className="grid grid-cols-3 gap-4 mb-6">
          <div className="bg-white rounded-lg shadow-card p-4">
            <div className="text-sm text-navy-500">Aktif Bağlantı</div>
            <div className="text-3xl font-semibold text-green-700 mt-1">{connections.connectedCount}</div>
          </div>
          <div className="bg-white rounded-lg shadow-card p-4">
            <div className="text-sm text-navy-500">Bağlantısı Kesilmiş</div>
            <div className="text-3xl font-semibold text-navy-700 mt-1">{connections.disconnectedCount}</div>
          </div>
          <div className="bg-white rounded-lg shadow-card p-4">
            <div className="text-sm text-navy-500">Son 24s Başarısız Sync</div>
            <div className="text-3xl font-semibold text-red-700 mt-1">{connections.failedLast24hCount}</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="flex gap-2 border-b border-navy-100 mb-4">
        <button
          type="button"
          onClick={() => setTab('connections')}
          className={`px-4 py-2 text-sm font-medium ${tab === 'connections' ? 'border-b-2 border-primary-600 text-primary-700' : 'text-navy-500 hover:text-navy-700'}`}
        >
          Bağlantılar
        </button>
        <button
          type="button"
          onClick={() => setTab('sync-log')}
          className={`px-4 py-2 text-sm font-medium ${tab === 'sync-log' ? 'border-b-2 border-primary-600 text-primary-700' : 'text-navy-500 hover:text-navy-700'}`}
        >
          Senkron Kaydı
        </button>
      </div>

      {tab === 'connections' && (
        <ConnectionsTab
          items={connections?.items ?? []}
          loading={connectionsLoading}
          error={connectionsError}
          onForceDisconnect={setDisconnectTarget}
        />
      )}

      {tab === 'sync-log' && (
        <SyncLogTab
          data={syncLog}
          loading={syncLogLoading}
          error={syncLogError}
          filters={filters}
          tenantFilterInput={tenantFilterInput}
          onTenantInput={setTenantFilterInput}
          onFilterChange={(patch) => setFilters((prev) => ({ ...prev, ...patch, page: 1 }))}
          onApplyFilters={handleApplyFilters}
          onPageChange={(p) => setFilters((prev) => ({ ...prev, page: p }))}
          totalPages={totalPages}
          selectedIds={selectedIds}
          onToggle={toggleRow}
          onSelectAllFailed={() => {
            const capped = failedRowsVisible.slice(0, MAX_BATCH_SIZE);
            setSelectedIds(new Set(capped.map((r) => r.id)));
          }}
          onClearSelection={() => setSelectedIds(new Set())}
          onOpenRetryModal={() => setRetryModalOpen(true)}
          retryResult={retryResult}
          onDismissRetryResult={() => setRetryResult(null)}
        />
      )}

      <OpsZohoDisconnectModal
        open={disconnectTarget !== null}
        busy={disconnectBusy}
        tenantId={disconnectTarget}
        onClose={() => setDisconnectTarget(null)}
        onConfirm={handleConfirmDisconnect}
      />

      <OpsZohoRetryBatchModal
        open={retryModalOpen}
        busy={retryBusy}
        count={selectedIds.size}
        onClose={() => setRetryModalOpen(false)}
        onConfirm={handleConfirmBatchRetry}
      />
    </div>
  );
}

function ConnectionsTab({
  items,
  loading,
  error,
  onForceDisconnect,
}: {
  items: ZohoOpsConnectionEntryDto[];
  loading: boolean;
  error: string | null;
  onForceDisconnect: (tenantId: number) => void;
}) {
  if (loading && items.length === 0) return <div className="text-navy-500">Yükleniyor...</div>;
  if (error) return <ErrorBanner message={error} />;
  if (items.length === 0) return <div className="text-navy-500">Hiç Zoho bağlantısı bulunamadı.</div>;

  return (
    <div className="bg-white rounded-lg shadow-card overflow-hidden">
      <table className="w-full text-sm">
        <thead className="bg-navy-50 text-navy-600 text-xs uppercase">
          <tr>
            <th className="px-4 py-2 text-left">Tenant</th>
            <th className="px-4 py-2 text-left">Bölge</th>
            <th className="px-4 py-2 text-left">Zoho Kullanıcı</th>
            <th className="px-4 py-2 text-left">Durum</th>
            <th className="px-4 py-2 text-left">Bağlandı</th>
            <th className="px-4 py-2 text-left">Son Yenileme</th>
            <th className="px-4 py-2 text-left">Aksiyon</th>
          </tr>
        </thead>
        <tbody>
          {items.map((c) => {
            const active = !c.disconnectedAt;
            return (
              <tr key={c.tenantId} className="border-t border-navy-50">
                <td className="px-4 py-2 font-mono">{c.tenantId}</td>
                <td className="px-4 py-2">{c.region}</td>
                <td className="px-4 py-2 text-navy-600">{c.zohoUserEmail ?? '—'}</td>
                <td className="px-4 py-2">
                  <span className={`inline-block px-2 py-0.5 rounded text-xs ${active ? 'bg-green-100 text-green-800' : 'bg-navy-100 text-navy-600'}`}>
                    {active ? 'Aktif' : 'Kesildi'}
                  </span>
                </td>
                <td className="px-4 py-2 text-navy-600">{formatDateTr(c.connectedAt)}</td>
                <td className="px-4 py-2 text-navy-600">{formatDateTr(c.lastRefreshedAt)}</td>
                <td className="px-4 py-2">
                  {active && (
                    <Button variant="danger" onClick={() => onForceDisconnect(c.tenantId)}>
                      Bağı Kes
                    </Button>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function SyncLogTab(props: {
  data: ZohoOpsSyncLogPageResponse | null;
  loading: boolean;
  error: string | null;
  filters: ZohoOpsSyncLogQuery;
  tenantFilterInput: string;
  onTenantInput: (v: string) => void;
  onFilterChange: (patch: Partial<ZohoOpsSyncLogQuery>) => void;
  onApplyFilters: () => void;
  onPageChange: (p: number) => void;
  totalPages: number;
  selectedIds: Set<number>;
  onToggle: (id: number) => void;
  onSelectAllFailed: () => void;
  onClearSelection: () => void;
  onOpenRetryModal: () => void;
  retryResult: ZohoOpsBatchRetryResponse | null;
  onDismissRetryResult: () => void;
}) {
  const { data, loading, error, filters, tenantFilterInput, onTenantInput, onFilterChange, onApplyFilters, onPageChange, totalPages, selectedIds, onToggle, onSelectAllFailed, onClearSelection, onOpenRetryModal, retryResult, onDismissRetryResult } = props;

  return (
    <div>
      {/* Filters */}
      <div className="bg-white rounded-lg shadow-card p-4 mb-4 grid grid-cols-5 gap-3">
        <input
          type="text"
          placeholder="Tenant ID"
          value={tenantFilterInput}
          onChange={(e) => onTenantInput(e.target.value)}
          className="border border-navy-200 rounded px-3 py-2 text-sm"
        />
        <select
          value={filters.status ?? ''}
          onChange={(e) => onFilterChange({ status: (e.target.value || undefined) as ZohoSyncLogStatus | undefined })}
          className="border border-navy-200 rounded px-3 py-2 text-sm"
        >
          <option value="">Tüm Durumlar</option>
          <option value="pending">Beklemede</option>
          <option value="failed">Başarısız</option>
          <option value="success">Başarılı</option>
        </select>
        <input
          type="text"
          placeholder="Olay (örn. offer_sent)"
          value={filters.event ?? ''}
          onChange={(e) => onFilterChange({ event: e.target.value || undefined })}
          className="border border-navy-200 rounded px-3 py-2 text-sm"
        />
        <input
          type="datetime-local"
          value={filters.from ?? ''}
          onChange={(e) => onFilterChange({ from: e.target.value || undefined })}
          className="border border-navy-200 rounded px-3 py-2 text-sm"
        />
        <div className="flex gap-2">
          <input
            type="datetime-local"
            value={filters.to ?? ''}
            onChange={(e) => onFilterChange({ to: e.target.value || undefined })}
            className="border border-navy-200 rounded px-3 py-2 text-sm flex-1"
          />
          <Button variant="primary" onClick={onApplyFilters}>
            Filtrele
          </Button>
        </div>
      </div>

      {retryResult && (
        <div className="bg-blue-50 border border-blue-200 rounded p-3 mb-4 text-sm text-navy-700 flex items-start justify-between">
          <div>
            <div className="font-semibold mb-1">Toplu tekrar deneme sonucu</div>
            <div>
              İstenen: {retryResult.requested} · Güncellenen: {retryResult.updated} · Atlanan: {retryResult.skipped.length}
            </div>
            {retryResult.skipped.length > 0 && (
              <ul className="mt-2 list-disc pl-5 text-xs text-navy-600">
                {retryResult.skipped.slice(0, 10).map((s) => (
                  <li key={s.id}>
                    <span className="font-mono">#{s.id}</span> — {s.reason}
                  </li>
                ))}
                {retryResult.skipped.length > 10 && <li>...ve {retryResult.skipped.length - 10} tane daha</li>}
              </ul>
            )}
          </div>
          <button type="button" onClick={onDismissRetryResult} className="text-navy-500 hover:text-navy-700" aria-label="Kapat">
            ×
          </button>
        </div>
      )}

      {/* Batch action bar */}
      <div className="flex items-center justify-between mb-2">
        <div className="text-sm text-navy-500">
          {selectedIds.size > 0
            ? `${selectedIds.size} kayıt seçildi (max ${MAX_BATCH_SIZE})`
            : 'Başarısız kayıtları seçip toplu retry yapabilirsiniz.'}
        </div>
        <div className="flex gap-2">
          <Button variant="ghost" onClick={onSelectAllFailed} disabled={loading}>
            Ekrandaki Failed'ları Seç
          </Button>
          <Button variant="ghost" onClick={onClearSelection} disabled={selectedIds.size === 0}>
            Seçimi Temizle
          </Button>
          <Button variant="primary" onClick={onOpenRetryModal} disabled={selectedIds.size === 0}>
            Seçileni Tekrar Dene
          </Button>
        </div>
      </div>

      {error && <ErrorBanner message={error} />}
      {loading && !data ? (
        <div className="text-navy-500">Yükleniyor...</div>
      ) : !data || data.items.length === 0 ? (
        <div className="text-navy-500">Bu filtreyle kayıt bulunamadı.</div>
      ) : (
        <div className="bg-white rounded-lg shadow-card overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-navy-50 text-navy-600 text-xs uppercase">
              <tr>
                <th className="px-3 py-2"></th>
                <th className="px-3 py-2 text-left">ID</th>
                <th className="px-3 py-2 text-left">Tenant</th>
                <th className="px-3 py-2 text-left">Olay</th>
                <th className="px-3 py-2 text-left">Kaynak Lead</th>
                <th className="px-3 py-2 text-left">Durum</th>
                <th className="px-3 py-2 text-left">Deneme</th>
                <th className="px-3 py-2 text-left">Hata</th>
                <th className="px-3 py-2 text-left">Güncellendi</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((r) => {
                const canSelect = r.status === 'failed';
                const isSelected = selectedIds.has(r.id);
                return (
                  <tr key={r.id} className={`border-t border-navy-50 ${isSelected ? 'bg-blue-50' : ''}`}>
                    <td className="px-3 py-2">
                      <input
                        type="checkbox"
                        disabled={!canSelect}
                        checked={isSelected}
                        onChange={() => onToggle(r.id)}
                      />
                    </td>
                    <td className="px-3 py-2 font-mono text-xs">{r.id}</td>
                    <td className="px-3 py-2 font-mono">{r.tenantId}</td>
                    <td className="px-3 py-2">{r.zohoEvent}</td>
                    <td className="px-3 py-2 font-mono text-xs">{r.sourceLeadId}</td>
                    <td className="px-3 py-2">
                      <span className={`inline-block px-2 py-0.5 rounded text-xs ${STATUS_BADGE[r.status]}`}>
                        {STATUS_LABEL[r.status]}
                      </span>
                    </td>
                    <td className="px-3 py-2">{r.attemptCount}</td>
                    <td className="px-3 py-2 text-xs text-navy-600">
                      {r.lastErrorCode ? (
                        <span title={r.lastErrorMessage ?? ''}>
                          {r.lastErrorCode}
                        </span>
                      ) : '—'}
                    </td>
                    <td className="px-3 py-2 text-navy-600">{formatDateTr(r.updatedAt)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {data && data.totalCount > data.pageSize && (
        <div className="flex items-center justify-between mt-3 text-sm">
          <div className="text-navy-500">
            Toplam: {data.totalCount} · Sayfa {data.page}/{totalPages}
          </div>
          <div className="flex gap-2">
            <Button
              variant="secondary"
              onClick={() => onPageChange(Math.max(1, data.page - 1))}
              disabled={data.page <= 1 || loading}
            >
              ← Önceki
            </Button>
            <Button
              variant="secondary"
              onClick={() => onPageChange(Math.min(totalPages, data.page + 1))}
              disabled={data.page >= totalPages || loading}
            >
              Sonraki →
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function ErrorBanner({ message }: { message: string }) {
  return (
    <div className="bg-red-50 border border-red-200 rounded p-3 mb-4 text-sm text-red-700 flex items-start gap-2">
      <AlertCircle className="w-4 h-4 mt-0.5 flex-shrink-0" />
      <span>{message}</span>
    </div>
  );
}
