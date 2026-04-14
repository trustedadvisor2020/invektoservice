import { useEffect, useState } from 'react';
import { useInmaSession, type InmaStatus } from './inmaSession';

// Kullanici-yonelik TR etiketler; debug component olsa da uretimde gozukebilir.
// Raw error code (INV-INT-*) UI'da gosterilmez — sadece title tooltip'te diagnostic olarak.
const LABELS: Record<InmaStatus, string> = {
  idle: 'Ana pencere baglantisi kuruluyor...',
  ready: 'Ana pencere baglantisi hazir',
  error: 'Baglanti hatasi',
  ended: 'Oturum sona erdi',
};

const COLORS: Record<InmaStatus, string> = {
  idle: '#999',
  ready: '#16a34a',
  error: '#dc2626',
  ended: '#6b7280',
};

export function InmaConnectionStatus() {
  const status = useInmaSession((s) => s.status);
  const error = useInmaSession((s) => s.error);
  const apiBaseUrl = useInmaSession((s) => s.apiBaseUrl);
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);
  if (!mounted) return null;

  const label = LABELS[status];
  // Diagnostic kod/apiBaseUrl sadece tooltip (title)'da dev/destek icin gosterilir.
  const diagnostic = [apiBaseUrl ?? '', status === 'error' && error ? `kod: ${error}` : ''].filter(Boolean).join(' | ');

  return (
    <div
      data-testid="inma-connection-status"
      style={{
        position: 'fixed',
        bottom: 8,
        right: 8,
        zIndex: 9999,
        padding: '4px 8px',
        fontSize: 12,
        fontFamily: 'monospace',
        borderRadius: 4,
        background: 'rgba(0,0,0,0.75)',
        color: COLORS[status],
        pointerEvents: 'none',
      }}
      title={diagnostic}
    >
      {label}
    </div>
  );
}
