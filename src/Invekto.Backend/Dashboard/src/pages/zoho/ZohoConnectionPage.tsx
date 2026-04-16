// Adim 3 Paket 3-B2: Zoho baglanti sayfasi (status + Bagla/Kes).
import { useEffect, useState } from 'react';
import { Link2, CheckCircle2, AlertCircle, X } from 'lucide-react';
import { useZohoStore } from '../../stores/zoho-store';
import { api } from '../../lib/api';
import { Card, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import { Badge } from '../../components/ui/Badge';
import { DisconnectConfirmModal } from '../../components/zoho/DisconnectConfirmModal';
import { formatDateTr } from '../../lib/utils';

export function ZohoConnectionPage() {
  const {
    connection,
    connectionLoading,
    connectionError,
    loadConnection,
    disconnect,
  } = useZohoStore();

  const [modalOpen, setModalOpen] = useState(false);
  const [disconnectBusy, setDisconnectBusy] = useState(false);
  const [connectBusy, setConnectBusy] = useState(false);
  const [statusMessage, setStatusMessage] = useState<{ kind: 'ok' | 'warn' | 'err'; text: string } | null>(null);

  useEffect(() => {
    void loadConnection();
  }, [loadConnection]);

  const handleConnect = async () => {
    setStatusMessage(null);
    setConnectBusy(true);
    try {
      const res = await api.createZohoConnectUrl();
      window.location.href = res.authorizeUrl;
    } catch (err) {
      setConnectBusy(false);
      setStatusMessage({
        kind: 'err',
        text: err instanceof Error && err.message
          ? err.message
          : '[INV-INT-FE-004] Zoho baglanti URL alinamadi. Birkac saniye sonra tekrar deneyin.',
      });
    }
  };

  const handleDisconnect = async () => {
    setStatusMessage(null);
    setDisconnectBusy(true);
    try {
      const { tokenRevoked } = await disconnect();
      setModalOpen(false);
      setStatusMessage(tokenRevoked
        ? { kind: 'ok',   text: '[INV-INT-FE-007] Zoho baglantisi kesildi ve refresh token iptal edildi. Tekrar baglanmak icin "Zoho\'ya Baglan" butonunu kullanin.' }
        : { kind: 'warn', text: '[INV-INT-FE-008] Zoho baglantisi yerel olarak kesildi. Refresh token iptal edilemedi — Zoho hesabinizda Ayarlar > Baglanti Izinleri\'nden uygulamayi manuel kaldirmaniz onerilir.' });
    } catch (err) {
      setStatusMessage({
        kind: 'err',
        text: err instanceof Error && err.message
          ? err.message
          : '[INV-INT-FE-005] Baglanti kesilemedi. Birkac dakika sonra tekrar deneyin.',
      });
    } finally {
      setDisconnectBusy(false);
    }
  };

  if (connectionLoading && !connection) {
    return <div className="text-sm text-navy-400">Yukleniyor...</div>;
  }

  if (connectionError) {
    return (
      <Card className="border-red-100 bg-red-50/40">
        <div className="flex items-start gap-3">
          <AlertCircle className="w-5 h-5 text-red-500 shrink-0 mt-0.5" />
          <div>
            <CardTitle className="text-red-700 mb-1">Baglanti bilgisi alinamadi</CardTitle>
            <p className="text-sm text-red-600">{connectionError}</p>
            <Button className="mt-3" variant="secondary" size="sm" onClick={() => void loadConnection()}>
              Tekrar Dene
            </Button>
          </div>
        </div>
      </Card>
    );
  }

  const connected = connection?.connected === true;

  return (
    <div className="max-w-2xl">
      <div className="mb-5">
        <h1 className="text-xl font-semibold text-navy-900">Zoho CRM Baglantisi</h1>
        <p className="text-sm text-navy-500 mt-1">
          Tenant'inizin Zoho CRM hesabiyla OAuth 2.0 uzerinden guvenli baglantisi.
        </p>
      </div>

      {statusMessage && (
        <div className={`flex items-start justify-between gap-2 rounded-lg px-4 py-3 mb-4 text-sm ${
          statusMessage.kind === 'ok'
            ? 'bg-green-50 border border-green-200 text-green-800'
            : statusMessage.kind === 'warn'
              ? 'bg-yellow-50 border border-yellow-200 text-yellow-800'
              : 'bg-red-50 border border-red-200 text-red-700'
        }`}>
          <div className="flex items-start gap-2">
            {statusMessage.kind === 'ok'
              ? <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
              : <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />}
            <span>{statusMessage.text}</span>
          </div>
          <button
            type="button"
            onClick={() => setStatusMessage(null)}
            aria-label="Kapat"
            className="text-current/70 hover:text-current shrink-0"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {connected ? (
        <Card>
          <div className="flex items-start gap-3 mb-4">
            <CheckCircle2 className="w-5 h-5 text-emerald-500 shrink-0 mt-0.5" />
            <div>
              <div className="flex items-center gap-2">
                <CardTitle className="text-emerald-700">Bagli</CardTitle>
                <Badge variant="success">Aktif</Badge>
              </div>
              <p className="text-sm text-navy-500 mt-0.5">
                Lifecycle olaylari Zoho CRM'e senkronize ediliyor.
              </p>
            </div>
          </div>

          <dl className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-5 text-sm">
            <div>
              <dt className="text-navy-400">Bolge</dt>
              <dd className="text-navy-900 font-medium">{connection?.region ?? '-'}</dd>
            </div>
            <div>
              <dt className="text-navy-400">Zoho Kullanici</dt>
              <dd className="text-navy-900 font-medium">{connection?.zohoUserEmail ?? '-'}</dd>
            </div>
            <div>
              <dt className="text-navy-400">Baglanma Zamani</dt>
              <dd className="text-navy-900 font-medium">{formatDateTr(connection?.connectedAt)}</dd>
            </div>
            <div>
              <dt className="text-navy-400">Son Token Yenileme</dt>
              <dd className="text-navy-900 font-medium">{formatDateTr(connection?.lastRefreshedAt)}</dd>
            </div>
          </dl>

          <div className="flex justify-end">
            <Button variant="danger" onClick={() => setModalOpen(true)}>
              Baglantiyi Kes
            </Button>
          </div>
        </Card>
      ) : (
        <Card>
          <div className="flex items-start gap-3 mb-4">
            <Link2 className="w-5 h-5 text-navy-400 shrink-0 mt-0.5" />
            <div>
              <CardTitle>Henuz bagli degil</CardTitle>
              <p className="text-sm text-navy-500 mt-0.5">
                Zoho CRM hesabinizi baglayarak lifecycle olaylarini Zoho Blueprint'e senkronize edin.
              </p>
            </div>
          </div>
          <div className="flex justify-end">
            <Button onClick={handleConnect} disabled={connectBusy}>
              {connectBusy ? 'Yonlendiriliyor...' : "Zoho'ya Baglan"}
            </Button>
          </div>
        </Card>
      )}

      <DisconnectConfirmModal
        open={modalOpen}
        busy={disconnectBusy}
        onClose={() => !disconnectBusy && setModalOpen(false)}
        onConfirm={handleDisconnect}
      />
    </div>
  );
}
