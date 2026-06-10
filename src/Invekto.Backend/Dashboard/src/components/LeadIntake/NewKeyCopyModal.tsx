// FEAT-LIW Chunk C: one-shot plaintext key display modal.
// Plaintext is shown exactly once on generate/rotate; closing the modal wipes
// it from React state (parent uses a null reset on onClose). Q rule: no "Iptal"
// text button — X icon top-right + primary "Kopyaladim, kapat" button.
import { useEffect, useState } from 'react';
import { X, Copy, Check } from 'lucide-react';
import { Button } from '../ui/Button';

interface NewKeyCopyModalProps {
  open: boolean;
  plaintext: string | null;
  oldKeyExpiresAt: string | null;
  onClose: () => void;
}

export function NewKeyCopyModal({ open, plaintext, oldKeyExpiresAt, onClose }: NewKeyCopyModalProps) {
  const [copied, setCopied] = useState(false);
  const [copyFailed, setCopyFailed] = useState(false);

  useEffect(() => {
    if (!open) {
      setCopied(false);
      setCopyFailed(false);
      return;
    }
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  if (!open || !plaintext) return null;

  async function handleCopy() {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(plaintext ?? '');
        setCopied(true);
        setCopyFailed(false);
      } else {
        throw new Error('clipboard_unavailable');
      }
    } catch (err) {
      // Typed-catch compliance. Codex iter 2 CQ5: logged reason helps future triage
      // when clipboard access is blocked by an iframe/CSP/permission policy.
      const reason = err instanceof Error ? err.message : 'unknown';
      console.warn(`[LIW-FE-CLIP] NewKeyCopyModal: clipboard.writeText failed (${reason}); falling back to manual copy`);
      setCopyFailed(true);
      setCopied(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-navy-900/40 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-labelledby="liw-new-key-title"
      onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div className="bg-white rounded-xl shadow-card w-full max-w-lg p-6 relative">
        <button
          type="button"
          aria-label="Kapat"
          onClick={onClose}
          className="absolute top-3 right-3 p-1.5 rounded-lg text-navy-400 hover:bg-navy-50 hover:text-navy-700 transition-colors"
        >
          <X className="w-4 h-4" />
        </button>
        <h2 id="liw-new-key-title" className="text-lg font-semibold text-navy-900 mb-2">
          Yeni API Anahtari Olusturuldu
        </h2>
        <p className="text-sm text-navy-500 mb-4 leading-relaxed">
          Bu anahtar size <strong>yalnizca bir kez</strong> gosterilmektedir.
          Asagidaki degeri kopyalayip landing sayfaniza veya form tarafiniza yerlestirin.
          Pencere kapandiktan sonra anahtarin tam hali bir daha gosterilmeyecek.
        </p>
        {oldKeyExpiresAt && (
          <p className="text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded-md p-2 mb-4">
            Eski anahtar 24 saat boyunca (son gecerlilik: {new Date(oldKeyExpiresAt).toLocaleString('tr-TR')})
            calismaya devam edecek. Landing sayfanizi bu sure icinde yeni anahtarla guncelleyin.
          </p>
        )}
        <div className="bg-navy-50 border border-navy-200 rounded-md p-3 mb-4 font-mono text-sm text-navy-800 break-all select-all">
          {plaintext}
        </div>
        {copyFailed && (
          <p className="text-xs text-red-600 mb-3">
            Pano erisimi engellendi. Yukaridaki metni manuel olarak secip kopyalayin.
          </p>
        )}
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={handleCopy}>
            {copied ? (
              <><Check className="w-4 h-4 mr-1" /> Kopyalandi</>
            ) : (
              <><Copy className="w-4 h-4 mr-1" /> Kopyala</>
            )}
          </Button>
          <Button variant="primary" onClick={onClose}>
            Kopyaladim, kapat
          </Button>
        </div>
      </div>
    </div>
  );
}
