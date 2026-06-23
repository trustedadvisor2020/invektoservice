import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api, ApiClientError, type TenantFieldMappingEntryDto } from '../lib/api';

// FEAT-TFM-FLOW (P4): lazy-load tenant field_mapping for PlaceholderPicker's
// Semantic Alanlar group. Module-level cache + single-flight mirror the
// useDynamicFields pattern so multiple picker mounts share one GET per
// editor session (backend has its own DbTenantFieldMappingResolver cache
// with 5dk TTL behind /api/v1/tenant-settings/field-mapping).
//
// Intentional split from useDynamicFields: they describe different contracts
// (tenant mapping is tenant-scope config, INMA dynamic fields are upstream
// metadata); sharing a cache key would hide refresh semantics. Separate hook,
// separate module cache.

export type TenantFieldMappingErrorKind =
  | 'not_loaded'    // Server returned 404/empty shape — tenant has no row yet, treated as empty mapping.
  | 'upstream_fail' // 5xx or network error — transient, refresh may help.
  | 'unknown';      // 401/other — user should check session/permissions.

/** Flat {semantic -> source} projection used by PlaceholderPicker to emit `{{<semantic>}}` tokens. */
export type FieldMapping = Record<string, string>;

/** Flat entry row (semantic name + full descriptor) for Semantic Alanlar group rendering. */
export interface SemanticEntry {
  semanticName: string;
  source: string;
  type: string;
  required: boolean;
}

let moduleCache: { mapping: FieldMapping; entries: SemanticEntry[] } | null = null;
let inflight: Promise<{ mapping: FieldMapping; entries: SemanticEntry[] }> | null = null;

async function fetchMapping(): Promise<{ mapping: FieldMapping; entries: SemanticEntry[] }> {
  if (moduleCache) return moduleCache;
  if (inflight) return inflight;
  inflight = (async () => {
    try {
      const resp = await api.getTenantFieldMapping();
      const rawMap = resp.data.field_mapping ?? {};
      const mapping: FieldMapping = {};
      const entries: SemanticEntry[] = [];
      for (const [semanticName, entry] of Object.entries(rawMap)) {
        const e = entry as TenantFieldMappingEntryDto;
        mapping[semanticName] = e.source;
        entries.push({
          semanticName,
          source: e.source,
          type: e.type,
          required: e.required ?? false,
        });
      }
      // Sort entries by semantic name for stable dropdown order.
      entries.sort((a, b) => a.semanticName.localeCompare(b.semanticName));
      moduleCache = { mapping, entries };
      return moduleCache;
    } finally {
      inflight = null;
    }
  })();
  return inflight;
}

/** Map an ApiClientError to the UI error kinds. */
function classifyError(err: unknown): TenantFieldMappingErrorKind {
  if (err instanceof ApiClientError) {
    if (err.status === 404) return 'not_loaded';
    if (err.status >= 500) return 'upstream_fail';
  }
  return 'unknown';
}

/**
 * Normalise any error into a coded Error instance so telemetry and
 * consumer surfaces carry a stable INV-XX-NNN handle. ApiClientError
 * already exposes the backend-issued code via `.errorCode`; for
 * non-Api errors (transport/DOM/JSON parse) we fall back to
 * `INV-OB-037` to match the existing Dashboard convention used by
 * useDynamicFields (see arch/errors.md INV-OB-037 — upstream
 * dynamic-config fetch failed / transient). The hook refuses to
 * fabricate project-unknown codes per lessons-learned 2026-04-22.
 */
function wrapError(err: unknown, fallbackMessage: string): Error {
  if (err instanceof ApiClientError) {
    const code = err.errorCode && err.errorCode !== 'UNKNOWN' ? err.errorCode : 'INV-OB-037';
    const wrapped = new Error(`[${code}] ${err.message || fallbackMessage}`);
    return Object.assign(wrapped, { code, requestId: err.requestId });
  }
  const baseMsg = err instanceof Error ? err.message : fallbackMessage;
  const wrapped = new Error(`[INV-OB-037] ${baseMsg}`);
  return Object.assign(wrapped, { code: 'INV-OB-037' });
}

export interface FieldMappingState {
  mapping: FieldMapping;
  entries: SemanticEntry[];
  isLoading: boolean;
  error: Error | null;
  errorKind: TenantFieldMappingErrorKind | null;
  refresh: () => Promise<void>;
}

/**
 * React hooks must be called unconditionally to preserve call order across renders,
 * so consumers that want TFM semantic data only when opted-in pass `enabled=false`
 * rather than calling the hook conditionally (e.g. PlaceholderPicker with
 * `tfmAware=false` keeps FEAT-DMP cost/behaviour). When disabled the hook is a
 * cheap no-op: no fetch, no module cache touch, and returns a stable empty shape.
 */
export function useFieldMapping(enabled: boolean = true): FieldMappingState {
  const [data, setData] = useState<{ mapping: FieldMapping; entries: SemanticEntry[] } | null>(
    enabled ? moduleCache : null,
  );
  const [isLoading, setIsLoading] = useState(enabled ? moduleCache === null : false);
  const [error, setError] = useState<Error | null>(null);
  const [errorKind, setErrorKind] = useState<TenantFieldMappingErrorKind | null>(null);
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;
    if (!enabled) return () => { isMountedRef.current = false; };
    if (moduleCache) {
      setData(moduleCache);
      setIsLoading(false);
      return () => { isMountedRef.current = false; };
    }
    (async () => {
      try {
        const fresh = await fetchMapping();
        if (isMountedRef.current) {
          setData(fresh);
          setError(null);
          setErrorKind(null);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setError(wrapError(err, 'Tenant field mapping yüklenemedi.'));
          setErrorKind(classifyError(err));
        }
      } finally {
        if (isMountedRef.current) setIsLoading(false);
      }
    })();
    return () => { isMountedRef.current = false; };
  }, [enabled]);

  const refresh = useCallback(async () => {
    if (!enabled) return;
    setIsLoading(true);
    moduleCache = null;
    try {
      const fresh = await fetchMapping();
      if (isMountedRef.current) {
        setData(fresh);
        setError(null);
        setErrorKind(null);
      }
    } catch (err) {
      if (isMountedRef.current) {
        setError(wrapError(err, 'Tenant field mapping yenilenemedi.'));
        setErrorKind(classifyError(err));
      }
    } finally {
      if (isMountedRef.current) setIsLoading(false);
    }
  }, [enabled]);

  const result = useMemo<FieldMappingState>(() => ({
    mapping: data?.mapping ?? {},
    entries: data?.entries ?? [],
    isLoading,
    error,
    errorKind,
    refresh,
  }), [data, isLoading, error, errorKind, refresh]);

  return result;
}
