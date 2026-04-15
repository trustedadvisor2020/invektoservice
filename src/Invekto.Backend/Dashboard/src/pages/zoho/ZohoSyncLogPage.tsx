// Adim 3 Paket 3-B2: Zoho sync log (paginated + filter + manuel retry).
import { useEffect, useMemo } from 'react';
import { RefreshCw, ChevronLeft, ChevronRight } from 'lucide-react';
import { useZohoStore } from '../../stores/zoho-store';
import type { ZohoSyncLogStatus } from '../../lib/api';
import { Card, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import { Badge } from '../../components/ui/Badge';
import { Input } from '../../components/ui/Input';
import { Select } from '../../components/ui/Select';

const STATUS_OPTIONS = [
  { value: '', label: 'Tumu' },
  { value: 'pending', label: 'Beklemede' },
  { value: 'failed', label: 'Basarisiz' },
  { value: 'success', label: 'Basarili' },
];

const PAGE_SIZE_OPTIONS = [
  { value: '20', label: '20 / sayfa' },
  { value: '50', label: '50 / sayfa' },
  { value: '100', label: '100 / sayfa' },
];

function statusBadge(status: ZohoSyncLogStatus) {
  if (status === 'success') return <Badge variant="success">Basarili</Badge>;
  if (status === 'failed') return <Badge variant="error">Basarisiz</Badge>;
  return <Badge variant="warning">Beklemede</Badge>;
}

function truncate(text: string, max = 60): string {
  return text.length > max ? text.slice(0, max) + '...' : text;
}

export function ZohoSyncLogPage() {
  const {
    syncLogItems,
    syncLogPage,
    syncLogPageSize,
    syncLogTotalCount,
    syncLogFilters,
    syncLogLoading,
    syncLogError,
    syncLogRetryingId,
    loadSyncLog,
    updateSyncLogFilters,
    resetSyncLogFilters,
    retrySyncLog,
  } = useZohoStore();

  useEffect(() => {
    void loadSyncLog({ page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil(syncLogTotalCount / Math.max(1, syncLogPageSize))),
    [syncLogTotalCount, syncLogPageSize],
  );

  const handleApply = () => {
    void loadSyncLog({ page: 1 });
  };

  const handleReset = () => {
    resetSyncLogFilters();
    void loadSyncLog({ page: 1, status: '', event: '', from: '', to: '' });
  };

  const handleRetry = async (id: number) => {
    try {
      await retrySyncLog(id);
      alert('[INV-INT-FE-009] Tekrar deneme kuyruga alindi. Worker bir sonraki tick\'te (~60sn) denenecek; durumu takip icin birkac dakika sonra listeyi yenileyin.');
    } catch (err) {
      alert(err instanceof Error ? err.message : '[INV-INT-FE-006] Tekrar deneme gonderilemedi. Kaydin hala "Basarisiz" durumunda oldugundan emin olun ve yeniden deneyin.');
    }
  };

  return (
    <div>
      <div className="mb-5">
        <h1 className="text-xl font-semibold text-navy-900">Senkron Kaydi</h1>
        <p className="text-sm text-navy-500 mt-1">
          Zoho Blueprint gecis denemelerinin durumu. Basarisiz kayitlar manuel olarak tekrar denenebilir.
        </p>
      </div>

      <Card className="mb-4">
        <div className="grid grid-cols-1 md:grid-cols-5 gap-3 items-end">
          <Select
            label="Durum"
            value={syncLogFilters.status}
            onChange={(e) => updateSyncLogFilters({ status: e.target.value as ZohoSyncLogStatus | '' })}
            options={STATUS_OPTIONS}
          />
          <Input
            label="Olay"
            placeholder="lead.qualified..."
            value={syncLogFilters.event}
            onChange={(e) => updateSyncLogFilters({ event: e.target.value })}
          />
          <Input
            label="Baslangic"
            type="date"
            value={syncLogFilters.from}
            onChange={(e) => updateSyncLogFilters({ from: e.target.value })}
          />
          <Input
            label="Bitis"
            type="date"
            value={syncLogFilters.to}
            onChange={(e) => updateSyncLogFilters({ to: e.target.value })}
          />
          <div className="flex gap-2">
            <Button onClick={handleApply} disabled={syncLogLoading}>Uygula</Button>
            <Button variant="secondary" onClick={handleReset} disabled={syncLogLoading}>Sifirla</Button>
          </div>
        </div>
      </Card>

      {syncLogError && (
        <Card className="border-red-100 bg-red-50/40 mb-3">
          <CardTitle className="text-red-700 mb-1">Liste alinamadi</CardTitle>
          <p className="text-sm text-red-600">{syncLogError}</p>
        </Card>
      )}

      <Card className="p-0 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-navy-50 text-navy-500 text-xs uppercase tracking-wide">
              <tr>
                <th className="text-left font-semibold px-4 py-3">Tarih</th>
                <th className="text-left font-semibold px-4 py-3">Olay</th>
                <th className="text-left font-semibold px-4 py-3">Lead</th>
                <th className="text-left font-semibold px-4 py-3">Durum</th>
                <th className="text-left font-semibold px-4 py-3">Deneme</th>
                <th className="text-left font-semibold px-4 py-3">Hata</th>
                <th className="text-right font-semibold px-4 py-3 w-32">Aksiyon</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-100">
              {syncLogLoading && syncLogItems.length === 0 && (
                <tr><td colSpan={7} className="px-4 py-8 text-center text-navy-400">Yukleniyor...</td></tr>
              )}
              {!syncLogLoading && syncLogItems.length === 0 && !syncLogError && (
                <tr><td colSpan={7} className="px-4 py-8 text-center text-navy-400">Kayit bulunamadi.</td></tr>
              )}
              {syncLogItems.map((row) => (
                <tr key={row.id}>
                  <td className="px-4 py-3 text-navy-500 whitespace-nowrap">
                    {new Date(row.updatedAt).toLocaleString('tr-TR')}
                  </td>
                  <td className="px-4 py-3 font-medium text-navy-900">{row.zohoEvent}</td>
                  <td className="px-4 py-3 text-navy-700">
                    <div className="flex flex-col">
                      <span className="font-mono text-xs">{row.sourceLeadId}</span>
                      {row.zohoLeadId && (
                        <span className="font-mono text-xs text-navy-400">-&gt; {row.zohoLeadId}</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3">{statusBadge(row.status)}</td>
                  <td className="px-4 py-3 text-navy-700">{row.attemptCount}</td>
                  <td className="px-4 py-3 text-navy-500 max-w-xs">
                    {row.lastErrorMessage ? (
                      <span title={row.lastErrorMessage}>
                        {row.lastErrorCode && (
                          <span className="font-mono text-xs text-red-600 mr-1">[{row.lastErrorCode}]</span>
                        )}
                        {truncate(row.lastErrorMessage)}
                      </span>
                    ) : (
                      <span className="text-navy-300">-</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right">
                    {row.status === 'failed' ? (
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => void handleRetry(row.id)}
                        disabled={syncLogRetryingId === row.id}
                      >
                        <RefreshCw className={`w-3 h-3 ${syncLogRetryingId === row.id ? 'animate-spin' : ''}`} />
                        {syncLogRetryingId === row.id ? 'Gonderiliyor' : 'Tekrar Dene'}
                      </Button>
                    ) : (
                      <span className="text-xs text-navy-300">-</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex items-center justify-between px-4 py-3 border-t border-navy-100 bg-navy-50/50 text-sm">
          <div className="text-navy-500">
            Toplam <strong className="text-navy-900">{syncLogTotalCount}</strong> kayit
          </div>
          <div className="flex items-center gap-3">
            <Select
              value={syncLogPageSize.toString()}
              onChange={(e) => void loadSyncLog({ page: 1, pageSize: parseInt(e.target.value, 10) })}
              options={PAGE_SIZE_OPTIONS}
              className="h-8 py-0"
            />
            <Button
              size="sm"
              variant="ghost"
              disabled={syncLogPage <= 1 || syncLogLoading}
              onClick={() => void loadSyncLog({ page: syncLogPage - 1 })}
            >
              <ChevronLeft className="w-4 h-4" />
            </Button>
            <span className="text-navy-700">
              Sayfa {syncLogPage} / {totalPages}
            </span>
            <Button
              size="sm"
              variant="ghost"
              disabled={syncLogPage >= totalPages || syncLogLoading}
              onClick={() => void loadSyncLog({ page: syncLogPage + 1 })}
            >
              <ChevronRight className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </Card>
    </div>
  );
}
