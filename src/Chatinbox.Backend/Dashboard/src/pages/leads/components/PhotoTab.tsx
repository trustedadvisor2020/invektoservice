import { useEffect, useState, useCallback } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '../../../components/ui/Card';
import { Badge } from '../../../components/ui/Badge';
import { Button } from '../../../components/ui/Button';
import {
  fetchPhotos,
  requestPhotoRedispatch,
  type PhotoStatus,
  type PhotoThumbnail,
  type PhotosResponse,
  PhotoApiError
} from '../../../api/photos';

// FEAT-PHOTO Lead Detay panelindeki "Fotograflar" sekmesi component.
// Sorumluluklar:
//   * GET /api/v1/leads/{id}/photos sonucunu render et: status badge,
//     photo_count, son alinma tarihi, thumbnail listesi (hash + tarih).
//   * "Tekrar Iste" butonu (POST /photos/request) — koordinator manuel
//     dispatch tetikler. Loading + success + error state.
//   * Cross-tenant 403 / 404 / 503 / 409 (rejected) durumlarini yumusak
//     mesajlarla goster (PhotoApiError uzerinden).
//
// Wiring: LeadDetailPage <PhotoTab leadId={id} /> render eder. Bu pakette
// router/wiring eklenmedigi icin component standalone calismaya hazir
// (post-pilot scope-correction patch'inde Lead Detay sayfasina baglanir).

interface PhotoTabProps {
  leadId: number;
}

const STATUS_LABEL: Record<PhotoStatus, { tr: string; variant: 'default' | 'success' | 'warning' | 'error' | 'info' }> = {
  none:       { tr: 'Henüz istenmedi', variant: 'default' },
  requested:  { tr: 'Foto bekleniyor', variant: 'info' },
  received:   { tr: 'Foto alındı',     variant: 'success' },
  escalated:  { tr: 'Eskalasyon',      variant: 'warning' },
  rejected:   { tr: 'Reddedildi',      variant: 'error' },
};

export function PhotoTab({ leadId }: PhotoTabProps) {
  const [data, setData] = useState<PhotosResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [requesting, setRequesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const loadPhotos = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await fetchPhotos(leadId);
      setData(result);
    } catch (err) {
      if (err instanceof PhotoApiError) {
        setError(mapErrorToUserMessage(err.status));
      } else {
        // eslint-disable-next-line no-console
        console.error('[PhotoTab] fetchPhotos unexpected error', err);
        setError('Beklenmeyen bir hata oluştu — konsolu kontrol edin.');
      }
    } finally {
      setLoading(false);
    }
  }, [leadId]);

  useEffect(() => {
    void loadPhotos();
  }, [loadPhotos]);

  const handleRedispatch = async () => {
    setRequesting(true);
    setError(null);
    setInfo(null);
    try {
      // Backend artik basari path'inde sadece status:'requested' doner;
      // 409 PhotoRequestRejectedLock (INV-AT-085) error envelope ile gelir
      // ve PhotoApiError olarak yakalanir (Codex iter 2 CQ1/CQ2/CQ8 fix).
      await requestPhotoRedispatch(leadId);
      setInfo('Foto isteme tekrar tetiklendi.');
      await loadPhotos();
    } catch (err) {
      if (err instanceof PhotoApiError) {
        setError(mapErrorToUserMessage(err.status));
      } else {
        // Unexpected (network drop, JSON parse) — keep diagnostic in console
        // for ops debugging; user-facing message generic.
        // eslint-disable-next-line no-console
        console.error('[PhotoTab] redispatch unexpected error', err);
        setError('Beklenmeyen bir hata oluştu — konsolu kontrol edin.');
      }
    } finally {
      setRequesting(false);
    }
  };

  if (loading) {
    return (
      <Card className="mt-4">
        <CardContent>
          <p className="text-sm text-navy-500">Foto bilgileri yükleniyor...</p>
        </CardContent>
      </Card>
    );
  }

  if (error && !data) {
    return (
      <Card className="mt-4">
        <CardContent>
          <p className="text-sm text-red-600">{error}</p>
          <Button size="sm" variant="secondary" className="mt-3" onClick={loadPhotos}>
            Tekrar Dene
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (!data) return null;

  const statusMeta = STATUS_LABEL[data.photo_status];
  const isRejected = data.photo_status === 'rejected';

  return (
    <Card className="mt-4">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle>Fotoğraflar</CardTitle>
          <Badge variant={statusMeta.variant}>{statusMeta.tr}</Badge>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        <div className="text-sm text-navy-600 space-y-1">
          <div>Toplam foto: <strong>{data.photo_count}</strong></div>
          {data.photo_received_at && (
            <div>Son foto: {formatDateTime(data.photo_received_at)}</div>
          )}
        </div>

        {info && <p className="text-sm text-emerald-700">{info}</p>}
        {error && <p className="text-sm text-red-600">{error}</p>}

        <div>
          <Button
            size="sm"
            variant="secondary"
            onClick={handleRedispatch}
            disabled={requesting || isRejected}
            title={isRejected ? 'Bu lead reddedildi; istek gönderilemez.' : undefined}
          >
            {requesting ? 'Gönderiliyor...' : 'Tekrar İste'}
          </Button>
        </div>

        <div className="pt-4 border-t border-navy-100">
          <h4 className="text-sm font-medium text-navy-900 mb-2">
            Inbound Foto Listesi ({data.thumbnails.length})
          </h4>
          {data.thumbnails.length === 0 ? (
            <p className="text-sm text-navy-500">Henüz foto alınmadı.</p>
          ) : (
            <ul className="space-y-2">
              {data.thumbnails.map((t: PhotoThumbnail) => (
                <li
                  key={t.id}
                  className="text-xs text-navy-600 flex items-center justify-between border border-navy-100 rounded px-3 py-2"
                >
                  <span className="font-mono truncate max-w-[180px]" title={t.media_url_hash}>
                    {t.media_url_hash.slice(0, 12)}...
                  </span>
                  <span>{formatDateTime(t.received_at)}</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

function formatDateTime(iso: string): string {
  try {
    const d = new Date(iso);
    return d.toLocaleString('tr-TR', {
      year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit'
    });
  } catch {
    return iso;
  }
}

function mapErrorToUserMessage(status: number): string {
  switch (status) {
    case 401: return 'Oturum süresi dolmuş, tekrar giriş yapın.';
    case 403: return 'Bu kayda erişim izniniz yok.';
    case 404: return 'Lead bulunamadı.';
    // 409 INV-AT-085 PhotoRequestRejectedLock: post-pilot opt-out lock
    // (Codex iter 2 CQ12 fix — 409-spesifik mesaj eklendi).
    case 409: return 'Bu lead reddedilmiş durumda; tekrar istek gönderilemez.';
    case 408: return 'İstek zaman aşımı; lütfen tekrar deneyin.';
    case 503: return 'Veritabanı geçici kullanılamıyor; birkaç saniye sonra tekrar deneyin.';
    default:  return 'Foto şu anda görüntülenemiyor; birkaç dakika sonra tekrar deneyin.';
  }
}
