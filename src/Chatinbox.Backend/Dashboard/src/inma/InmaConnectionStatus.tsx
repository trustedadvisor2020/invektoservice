import { useEffect, useState } from 'react';
import { useInmaSession } from './inmaSession';
import { useInmaBootstrap } from './inmaBootstrap';

// Faz 2: Sadece bootstrap state='error' iken render. Pozitif akisi gizler.
// Error label TR; diagnostic kod (INV-INT-*) + apiBaseUrl title tooltip'te destek icin.
export function InmaConnectionStatus() {
  const bootstrapState = useInmaBootstrap((s) => s.state);
  const bootstrapCode = useInmaBootstrap((s) => s.lastCode);
  const sessionError = useInmaSession((s) => s.error);
  const apiBaseUrl = useInmaSession((s) => s.apiBaseUrl);
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);
  if (!mounted) return null;
  if (bootstrapState !== 'error') return null;

  const code = bootstrapCode ?? sessionError ?? '';
  const diagnostic = [apiBaseUrl ?? '', code ? `kod: ${code}` : ''].filter(Boolean).join(' | ');

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
        color: '#dc2626',
        pointerEvents: 'none',
      }}
      title={diagnostic}
    >
      Baglanti hatasi
    </div>
  );
}
