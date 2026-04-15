// Adim 3 Paket 3-C: Super-admin batch retry confirm modal.
// Q kural: 'Iptal' YASAK — sag ust X + 'Vazgec' + 'Tekrar Dene' primary.
import { X } from 'lucide-react';
import { useEffect } from 'react';
import { Button } from '../ui/Button';

interface OpsZohoRetryBatchModalProps {
  open: boolean;
  busy: boolean;
  count: number;
  onClose: () => void;
  onConfirm: () => void;
}

export function OpsZohoRetryBatchModal({ open, busy, count, onClose, onConfirm }: OpsZohoRetryBatchModalProps) {
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, busy, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="ops-zoho-retry-title"
    >
      <div className="bg-white rounded-xl shadow-card w-full max-w-md p-6 relative">
        <button
          type="button"
          aria-label="Kapat"
          onClick={onClose}
          disabled={busy}
          className="absolute top-3 right-3 p-1.5 rounded-lg text-navy-400 hover:bg-navy-50 hover:text-navy-700 transition-colors disabled:opacity-40"
        >
          <X className="w-4 h-4" />
        </button>
        <h2 id="ops-zoho-retry-title" className="text-lg font-semibold text-navy-900 mb-2">
          {count} senkronizasyon kaydı tekrar denensin mi?
        </h2>
        <p className="text-sm text-navy-500 mb-5 leading-relaxed">
          Seçtiğiniz kayıtlar 'pending' durumuna alınacak ve arka plan worker bir sonraki tikte
          tekrar sync edecek. Sadece 'failed' durumundaki kayıtlar işlenecek; diğerleri atlanır
          ve rapor olarak döner. Maksimum 50 kayıt/gönderim.
        </p>
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Vazgeç
          </Button>
          <Button variant="primary" onClick={onConfirm} disabled={busy}>
            {busy ? 'Gönderiliyor...' : 'Tekrar Dene'}
          </Button>
        </div>
      </div>
    </div>
  );
}
