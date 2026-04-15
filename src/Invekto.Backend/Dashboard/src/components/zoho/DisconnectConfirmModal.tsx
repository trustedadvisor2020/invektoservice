// Adim 3 Paket 3-B2: Zoho disconnect confirm modal.
// Q kural: 'Iptal' text butonu YASAK — sag ust X ikonu + 'Vazgec' sekonder buton.
import { X } from 'lucide-react';
import { useEffect } from 'react';
import { Button } from '../ui/Button';

interface DisconnectConfirmModalProps {
  open: boolean;
  busy: boolean;
  onClose: () => void;
  onConfirm: () => void;
}

export function DisconnectConfirmModal({ open, busy, onClose, onConfirm }: DisconnectConfirmModalProps) {
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
      aria-labelledby="zoho-disconnect-title"
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
        <h2 id="zoho-disconnect-title" className="text-lg font-semibold text-navy-900 mb-2">
          Zoho baglantisini kesmek istiyor musunuz?
        </h2>
        <p className="text-sm text-navy-500 mb-5 leading-relaxed">
          Baglantiyi kestiginizde Zoho'ya yeni senkronizasyon gonderilmeyecek. Refresh token
          iptal edilmeye calisilacak ve yerel baglanti kaydi silinecek. Tekrar baglanmak icin
          OAuth akisi bastan yapilmalidir.
        </p>
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Vazgec
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={busy}>
            {busy ? 'Kesiliyor...' : 'Evet, Kes'}
          </Button>
        </div>
      </div>
    </div>
  );
}
