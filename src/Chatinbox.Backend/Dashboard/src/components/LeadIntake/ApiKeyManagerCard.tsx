// FEAT-LIW Chunk C: API key lifecycle card (generate + rotate + revoke).
// Three visual states: (A) not initialized -> 'Yeni Olustur' button. (B) active
// key -> masked display + 'Yenile' + 'Iptal Et'. (C) active + grace in progress
// -> adds 'Eski key: ***xxxx — N saat icinde gecersiz' chip. Rotate/Revoke fire
// confirmation dialogs first (Q rule: no Iptal text button, use X + Vazgec).
import { useState } from 'react';
import { KeyRound, RefreshCw, ShieldOff, Clock, X } from 'lucide-react';
import { Card, CardTitle } from '../ui/Card';
import { Button } from '../ui/Button';
import type { TenantLandingSettingsDto } from '../../types/leadIntake';

interface ApiKeyManagerCardProps {
  settings: TenantLandingSettingsDto;
  onRotate: () => Promise<void>;
  onRevoke: () => Promise<void>;
  busy: boolean;
}

type ConfirmMode = 'rotate' | 'revoke' | null;

export function ApiKeyManagerCard({ settings, onRotate, onRevoke, busy }: ApiKeyManagerCardProps) {
  const [confirmMode, setConfirmMode] = useState<ConfirmMode>(null);

  const hasKey = settings.has_active_key;
  const gracePending = settings.masked_old_key && settings.old_expires_at
    ? new Date(settings.old_expires_at) > new Date()
    : false;

  const graceHoursLeft = gracePending && settings.old_expires_at
    ? Math.max(0, Math.ceil((new Date(settings.old_expires_at).getTime() - Date.now()) / (1000 * 60 * 60)))
    : 0;

  async function handleConfirmedAction() {
    if (confirmMode === 'rotate') {
      setConfirmMode(null);
      await onRotate();
    } else if (confirmMode === 'revoke') {
      setConfirmMode(null);
      await onRevoke();
    }
  }

  async function handleFirstTimeGenerate() {
    await onRotate();
  }

  return (
    <Card className="p-5">
      <CardTitle className="flex items-center gap-2 mb-4">
        <KeyRound className="w-4 h-4 text-navy-600" />
        API Anahtarı
      </CardTitle>

      {!hasKey ? (
        <div>
          <p className="text-sm text-navy-500 mb-3 leading-relaxed">
            Bu tenant için henüz bir landing API anahtarı oluşturulmamış. Anahtar oluşturulduktan
            sonra aşağıda maskelenmiş hali görünecek ve &#34;Yenile&#34; ile 24 saatlik geçiş
            süresiyle yenileyebilirsiniz.
          </p>
          <Button variant="primary" onClick={handleFirstTimeGenerate} disabled={busy}>
            {busy ? 'Oluşturuluyor...' : 'Yeni API Anahtarı Oluştur'}
          </Button>
        </div>
      ) : (
        <div>
          <div className="mb-3">
            <div className="text-xs text-navy-500 mb-1">Aktif Anahtar</div>
            <div className="font-mono text-sm text-navy-800 bg-navy-50 border border-navy-200 rounded px-3 py-2 inline-block">
              {settings.masked_active_key}
            </div>
            {settings.updated_at && (
              <div className="text-xs text-navy-400 mt-1">
                Son güncelleme: {new Date(settings.updated_at).toLocaleString('tr-TR')}
              </div>
            )}
          </div>

          {gracePending && settings.masked_old_key && (
            <div className="mb-4 inline-flex items-center gap-2 bg-amber-50 border border-amber-200 text-amber-900 rounded px-3 py-1.5 text-xs">
              <Clock className="w-3.5 h-3.5" />
              <span>
                Eski anahtar: <code className="font-mono">{settings.masked_old_key}</code>
                {' — '}
                {graceHoursLeft} saat içinde geçersiz
              </span>
            </div>
          )}

          <div className="flex gap-3">
            <Button variant="secondary" onClick={() => setConfirmMode('rotate')} disabled={busy}>
              <RefreshCw className="w-4 h-4 mr-1" />
              Yenile
            </Button>
            <Button variant="danger" onClick={() => setConfirmMode('revoke')} disabled={busy}>
              <ShieldOff className="w-4 h-4 mr-1" />
              İptal Et
            </Button>
          </div>
        </div>
      )}

      {confirmMode && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onMouseDown={e => { if (e.target === e.currentTarget && !busy) setConfirmMode(null); }}
        >
          <div className="bg-white rounded-xl shadow-card w-full max-w-md p-6 relative">
            <button
              type="button"
              aria-label="Kapat"
              onClick={() => setConfirmMode(null)}
              disabled={busy}
              className="absolute top-3 right-3 p-1.5 rounded-lg text-navy-400 hover:bg-navy-50 hover:text-navy-700 disabled:opacity-40"
            >
              <X className="w-4 h-4" />
            </button>
            <h3 className="text-lg font-semibold text-navy-900 mb-2">
              {confirmMode === 'rotate' ? 'Anahtarı yenilemek istiyor musunuz?' : 'Anahtarı iptal etmek istiyor musunuz?'}
            </h3>
            <p className="text-sm text-navy-500 mb-5 leading-relaxed">
              {confirmMode === 'rotate'
                ? 'Mevcut anahtar 24 saat boyunca çalışmaya devam edecek, bu süre içinde landing sayfanızı yeni anahtarla güncelleyin.'
                : 'Bu tenant artık landing webhook üzerinden lead alamayacak. Field map ve welcome slug ayarlarınız korunacak; yeniden aktifleştirmek için yeni anahtar oluşturmanız gerekecek.'}
            </p>
            <div className="flex justify-end gap-3">
              <Button variant="secondary" onClick={() => setConfirmMode(null)} disabled={busy}>
                Vazgeç
              </Button>
              <Button
                variant={confirmMode === 'rotate' ? 'primary' : 'danger'}
                onClick={handleConfirmedAction}
                disabled={busy}
              >
                {busy ? 'İşleniyor...' : confirmMode === 'rotate' ? 'Evet, Yenile' : 'Evet, İptal Et'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </Card>
  );
}
