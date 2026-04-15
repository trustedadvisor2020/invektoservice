// Adim 3 Paket 3-C: Super-admin force-disconnect confirm modal.
// Q kural: 'Iptal' YASAK — sag ust X ikonu + 'Vazgec' sekonder buton + 'Bagi Kes' primary.
import { X } from 'lucide-react';
import { useEffect } from 'react';
import { Button } from '../ui/Button';

interface OpsZohoDisconnectModalProps {
  open: boolean;
  busy: boolean;
  tenantId: number | null;
  onClose: () => void;
  onConfirm: () => void;
}

export function OpsZohoDisconnectModal({ open, busy, tenantId, onClose, onConfirm }: OpsZohoDisconnectModalProps) {
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, busy, onClose]);

  if (!open || tenantId === null) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="ops-zoho-disconnect-title"
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
        <h2 id="ops-zoho-disconnect-title" className="text-lg font-semibold text-navy-900 mb-2">
          Tenant {tenantId} için Zoho bağlantısını kesmek istiyor musunuz?
        </h2>
        <p className="text-sm text-navy-500 mb-5 leading-relaxed">
          Super-admin aksiyonu olarak kaydedilecek. Refresh token iptali denenecek ve bağlantı kaydı
          soft-delete edilecek. Tenant bu işlemden sonra yeniden OAuth akışıyla bağlanmalıdır.
        </p>
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={onClose} disabled={busy}>
            Vazgeç
          </Button>
          <Button variant="danger" onClick={onConfirm} disabled={busy}>
            {busy ? 'Kesiliyor...' : 'Bağı Kes'}
          </Button>
        </div>
      </div>
    </div>
  );
}
