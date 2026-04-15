// Adim 3 Paket 3-B2: Zoho stage mapping (read-only; PUT P4'te).
import { useEffect } from 'react';
import { Info } from 'lucide-react';
import { useZohoStore } from '../../stores/zoho-store';
import { Card, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import { Badge } from '../../components/ui/Badge';
import { formatDateTr } from '../../lib/utils';

export function ZohoStageMappingPage() {
  const { stageMappings, stageMappingsLoading, stageMappingsError, loadStageMappings } = useZohoStore();

  useEffect(() => {
    void loadStageMappings();
  }, [loadStageMappings]);

  return (
    <div className="max-w-4xl">
      <div className="mb-4">
        <h1 className="text-xl font-semibold text-navy-900">Asama Eslesmeleri</h1>
        <p className="text-sm text-navy-500 mt-1">
          WhatsApp yasam dongusu olaylari ile Zoho Blueprint gecisleri arasindaki eslesme.
        </p>
      </div>

      <div className="flex items-start gap-2 bg-brand-50 border border-brand-100 rounded-lg px-4 py-3 mb-4 text-sm text-brand-700">
        <Info className="w-4 h-4 shrink-0 mt-0.5" />
        <div>
          <strong className="font-semibold">Duzenleme Paket 4 (P4) ile acilacak.</strong>
          <span className="ml-1 text-brand-700/80">
            Su an mappingler salt-okunur goruntuleniyor; degisiklik icin daha sonra tekrar ziyaret edin.
          </span>
        </div>
      </div>

      {stageMappingsLoading && !stageMappings && (
        <div className="text-sm text-navy-400">Yukleniyor...</div>
      )}

      {stageMappingsError && (
        <Card className="border-red-100 bg-red-50/40 mb-3">
          <CardTitle className="text-red-700 mb-1">Mapping listesi alinamadi</CardTitle>
          <p className="text-sm text-red-600 mb-2">{stageMappingsError}</p>
          <Button variant="secondary" size="sm" onClick={() => void loadStageMappings()}>
            Tekrar Dene
          </Button>
        </Card>
      )}

      {stageMappings && stageMappings.length === 0 && !stageMappingsError && (
        <Card>
          <p className="text-sm text-navy-500">
            Henuz mapping yok. P4 ile bu tablodan yeni eslesme eklenebilecek.
          </p>
        </Card>
      )}

      {stageMappings && stageMappings.length > 0 && (
        <Card className="p-0 overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-navy-50 text-navy-500 text-xs uppercase tracking-wide">
              <tr>
                <th className="text-left font-semibold px-4 py-3">WhatsApp Olayi</th>
                <th className="text-left font-semibold px-4 py-3">Zoho Gecisi</th>
                <th className="text-left font-semibold px-4 py-3">Guncelleme</th>
                <th className="text-left font-semibold px-4 py-3 w-24">Mod</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-navy-100">
              {stageMappings.map((m) => (
                <tr key={m.zohoEvent}>
                  <td className="px-4 py-3 font-medium text-navy-900">{m.zohoEvent}</td>
                  <td className="px-4 py-3 text-navy-700">
                    <div className="flex flex-col">
                      <span>{m.zohoTransitionName ?? m.zohoTransitionId}</span>
                      {m.zohoTransitionName && (
                        <span className="text-xs text-navy-400 font-mono">{m.zohoTransitionId}</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-navy-500">{formatDateTr(m.updatedAt)}</td>
                  <td className="px-4 py-3">
                    <Badge variant="default">Kilitli</Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}
