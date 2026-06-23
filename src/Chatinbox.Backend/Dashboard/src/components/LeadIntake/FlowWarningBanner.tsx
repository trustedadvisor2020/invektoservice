// FEAT-LIW Chunk C: banner renders ONLY when flow_status.exists=false.
// Resolves the silent INV-AT-069 skip observed on Chunk A+B prod smoke — tenant
// sees the configuration gap BEFORE a real lead arrives. Dismissible per-session
// via the X icon; re-mounts on page refresh so the gap is never permanently hidden.
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, X } from 'lucide-react';
import type { FlowStatusDto } from '../../types/leadIntake';

interface FlowWarningBannerProps {
  flowStatus: FlowStatusDto;
}

export function FlowWarningBanner({ flowStatus }: FlowWarningBannerProps) {
  const [dismissed, setDismissed] = useState(false);
  if (flowStatus.exists || dismissed) return null;

  return (
    <div
      role="alert"
      className="mb-4 rounded-lg border border-amber-300 bg-amber-50 p-4 relative"
    >
      <button
        type="button"
        aria-label="Uyarıyı bu oturum için kapat"
        onClick={() => setDismissed(true)}
        className="absolute top-2 right-2 p-1 rounded hover:bg-amber-100 text-amber-700"
      >
        <X className="w-4 h-4" />
      </button>
      <div className="flex gap-3 pr-6">
        <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
        <div className="flex-1">
          <p className="text-sm font-semibold text-amber-900">
            Uyarı: Welcome akışı <code className="bg-amber-100 px-1 rounded">{flowStatus.resolved_slug}</code> tenant&#39;ın
            aktif chatbot_flows listesinde bulunamadı.
          </p>
          <p className="text-xs text-amber-800 mt-1 leading-relaxed">
            Yeni lead kayıtları oluşacak ama welcome mesajı tetiklenmeyecek.{' '}
            <code className="bg-amber-100 px-1 rounded">{flowStatus.resolved_slug}</code> adlı bir flow&#39;u
            Flow Builder&#39;da aktif hale getirin veya Ayarlar üzerinden welcome slug&#39;ını değiştirin.
          </p>
          <div className="mt-2">
            <Link
              to="/flow-builder"
              className="inline-block text-xs font-medium text-amber-900 underline hover:text-amber-700"
            >
              Flow Builder&#39;a Git
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}
