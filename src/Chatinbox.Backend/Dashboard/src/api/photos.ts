// FEAT-PHOTO Dashboard SPA fetch wrappers. Backend Endpoints/PhotoEndpoints.cs
// uzerinden iki route'u tuketir:
//   GET  /api/v1/leads/{id}/photos          -> photo state + thumbnail timeline
//   POST /api/v1/leads/{id}/photos/request  -> "Tekrar Iste" trigger
//
// JWT auth: bearer token main app session'indan alinir; mevcut SPA fetch
// yardimcilari pattern'i (fetch + credentials: 'include' veya Authorization
// header) burada da kullanilir. Bu pilot pakette yeni auth helper acmak
// yerine standart `fetch`'i sariyoruz; SPA'nin global wrapper'i token'i
// otomatik ekliyor (cookie/session uyumu).
//
// PhotoTab.tsx component'i bu modulden import edip render eder.

export type PhotoStatus = 'none' | 'requested' | 'received' | 'escalated' | 'rejected';

export interface PhotoThumbnail {
  /** photo_inbound_idempotency.id — frontend list key. */
  id: number;
  /** SHA-256 hex (lowercase). Raw URL Backend'da tutulmuyor. */
  media_url_hash: string;
  /** ISO8601 UTC. */
  received_at: string;
}

export interface PhotosResponse {
  lead_id: number;
  tenant_id: number;
  photo_status: PhotoStatus;
  photo_count: number;
  photo_received_at: string | null;
  thumbnails: PhotoThumbnail[];
}

export interface RequestRedispatchResponse {
  status: 'requested';
  lead_id: number;
  tenant_id: number;
}

/**
 * GET /api/v1/leads/{id}/photos
 * Lead detay panelindeki "Fotograflar" sekmesi acildiginda cagrilir.
 * 401 -> SPA logout flow tetikler. 403 -> "Bu kayda erisim yok" warning.
 * 503 -> "DB gecici, refresh deneyin" toast.
 */
export async function fetchPhotos(leadId: number): Promise<PhotosResponse> {
  const response = await fetch(`/api/v1/leads/${leadId}/photos`, {
    method: 'GET',
    credentials: 'include',
    headers: { 'Accept': 'application/json' }
  });
  if (!response.ok) {
    const body = await response.text();
    throw new PhotoApiError(response.status, body || response.statusText);
  }
  return response.json();
}

/**
 * POST /api/v1/leads/{id}/photos/request
 * "Tekrar Iste" butonu. Backend leads.photo_status='requested' flip eder;
 * Automation tarafindaki dispatch trigger post-pilot wiring'e bagli.
 *
 * Errors:
 *   401 / 403 / 404 / 409 / 503 → throws PhotoApiError. UI mapErrorToUserMessage
 *   ile Turkish kullanici mesajina cevirilir. 409 = INV-AT-085
 *   PhotoRequestRejectedLock (post-pilot opt-out lock).
 *   200 → RequestRedispatchResponse (status='requested').
 *
 * (Codex iter 2 CQ1/CQ2/CQ8 fix: 409 artik error path; eski {status:'skipped'}
 * shape Backend'tan donmuyor — ErrorResponse envelope ile uyumlu.)
 */
export async function requestPhotoRedispatch(leadId: number): Promise<RequestRedispatchResponse> {
  const response = await fetch(`/api/v1/leads/${leadId}/photos/request`, {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    }
  });
  if (!response.ok) {
    const body = await response.text();
    throw new PhotoApiError(response.status, body || response.statusText);
  }
  return response.json();
}

/** API hatasi — UI tarafi status code'a gore toast/banner secer. */
export class PhotoApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(`PhotoApiError(${status}): ${message}`);
    this.name = 'PhotoApiError';
  }
}
