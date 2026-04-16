export const INMA_ERRORS = {
  BRIDGE_NOT_READY: 'INV-INT-100',
  BRIDGE_DISPOSED: 'INV-INT-101',
  REFRESH_TIMEOUT: 'INV-INT-102',
  REFRESH_FAILED: 'INV-INT-103',
  INVALID_ACCESS_TOKEN: 'INV-INT-104',
  INVALID_API_BASE_URL: 'INV-INT-105',
  HTTP_REQUEST_FAILED: 'INV-INT-106',
  EXCHANGE_FAILED: 'INV-INT-107',
  // INV-INT-108 (WELCOME_FAILED) removed 2026-04-17 — getWelcome diagnostic call dropped.
  // Code number reserved in arch/errors.md (deprecated), not reused.
  NAVIGATE_REJECTED: 'INV-INT-109',
} as const;

export type InmaErrorCode = typeof INMA_ERRORS[keyof typeof INMA_ERRORS];

const INMA_ERROR_DESCRIPTIONS: Record<InmaErrorCode, string> = {
  'INV-INT-100': 'bridge not ready; parent handshake pending',
  'INV-INT-101': 'bridge disposed during pending refresh',
  'INV-INT-102': 'parent did not respond to refresh within 15s',
  'INV-INT-103': 'parent returned refresh error',
  'INV-INT-104': 'parent sent invalid access token',
  'INV-INT-105': 'parent sent invalid apiBaseUrl (regex mismatch)',
  'INV-INT-106': 'HTTP request failed after refresh retry',
  'INV-INT-107': 'INMA -> INSE token exchange failed',
  'INV-INT-109': 'navigate postMessage rejected (invalid path or unauthenticated)',
};

export function inmaErrorMessage(code: InmaErrorCode, detail?: string): string {
  const base = `${code}: ${INMA_ERROR_DESCRIPTIONS[code]}`;
  return detail ? `${base} (${detail})` : base;
}
