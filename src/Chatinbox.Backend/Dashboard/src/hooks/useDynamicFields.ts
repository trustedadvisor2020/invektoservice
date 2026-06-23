import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api, ApiClientError, type InmaDynamicFieldDto, type DynamicFieldsErrorKind } from '../lib/api';

// FEAT-DMP: lazy-load INMA tenant placeholder list for PlaceholderPicker.
// Module-level cache shares the fetch across component instances in the same editor
// session — server-side cache (InmaDynamicFieldsCache, 1h TTL) already exists; this
// just prevents N parallel editor opens from triggering N fetches within the session.
let moduleCache: InmaDynamicFieldDto[] | null = null;
let inflight: Promise<InmaDynamicFieldDto[]> | null = null;

async function fetchFields(): Promise<InmaDynamicFieldDto[]> {
  if (moduleCache) return moduleCache;
  if (inflight) return inflight;
  inflight = (async () => {
    try {
      const response = await api.getInmaDynamicFields();
      moduleCache = response.data ?? [];
      return moduleCache;
    } finally {
      inflight = null;
    }
  })();
  return inflight;
}

/** Map an ApiClientError to the three distinct UI error kinds. */
function classifyError(err: unknown): DynamicFieldsErrorKind {
  if (err instanceof ApiClientError) {
    if (err.status === 422) return 'not_configured';
    if (err.status === 503) return 'upstream_fail';
  }
  return 'unknown';
}

export interface DynamicFieldsState {
  fields: InmaDynamicFieldDto[];
  keys: string[];
  isLoading: boolean;
  error: Error | null;
  errorKind: DynamicFieldsErrorKind | null;
  refresh: () => Promise<void>;
}

export function useDynamicFields(): DynamicFieldsState {
  const [data, setData] = useState<InmaDynamicFieldDto[] | null>(moduleCache);
  const [isLoading, setIsLoading] = useState(moduleCache === null);
  const [error, setError] = useState<Error | null>(null);
  const [errorKind, setErrorKind] = useState<DynamicFieldsErrorKind | null>(null);
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;
    if (moduleCache) {
      setData(moduleCache);
      setIsLoading(false);
      return () => { isMountedRef.current = false; };
    }
    (async () => {
      try {
        const fields = await fetchFields();
        if (isMountedRef.current) {
          setData(fields);
          setError(null);
          setErrorKind(null);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setError(err instanceof Error ? err : new Error('dynamic_fields_load_failed'));
          setErrorKind(classifyError(err));
        }
      } finally {
        if (isMountedRef.current) setIsLoading(false);
      }
    })();
    return () => { isMountedRef.current = false; };
  }, []);

  const refresh = useCallback(async () => {
    setIsLoading(true);
    moduleCache = null;
    let invalidatePartialWarning: Error | null = null;
    try {
      // Invalidate is a best-effort hint. If it fails we still refetch, but we surface
      // a user-visible warning (errorKind='invalidate_partial') so the editor knows the
      // returned list may be up to 1h stale rather than guaranteed fresh.
      try {
        await api.invalidateInmaDynamicFieldsCache();
      } catch (invalidateErr) {
        // Structured error-code surface per INV-XX-NNN quality bar:
        // - Durable server-log trail: api.request() already threw ApiClientError with
        //   server-side INV code + request_id when the server responded, so the backend
        //   log already captured it at the source. Client-side we rethrow through an
        //   Error instance that carries INV-OB-037 explicitly (message + .code attribute)
        //   so the consumer state object and telemetry surfaces keep the contract.
        console.warn('[INV-OB-037] dynamic-fields cache invalidate failed (non-fatal):', invalidateErr);
        const baseMsg = invalidateErr instanceof ApiClientError
          ? `${invalidateErr.errorCode ?? 'INV-OB-037'}: ${invalidateErr.message}`
          : 'INV-OB-037: dynamic_fields_invalidate_failed';
        invalidatePartialWarning = Object.assign(new Error(baseMsg), {
          code: 'INV-OB-037',
          requestId: invalidateErr instanceof ApiClientError ? invalidateErr.requestId : undefined,
        });
      }
      const fields = await fetchFields();
      if (isMountedRef.current) {
        setData(fields);
        if (invalidatePartialWarning) {
          // Fetch succeeded but the drop hint failed — show soft warning, keep the list usable.
          setError(invalidatePartialWarning);
          setErrorKind('invalidate_partial');
        } else {
          setError(null);
          setErrorKind(null);
        }
      }
    } catch (err) {
      if (isMountedRef.current) {
        setError(err instanceof Error ? err : new Error('dynamic_fields_refresh_failed'));
        setErrorKind(classifyError(err));
      }
    } finally {
      if (isMountedRef.current) setIsLoading(false);
    }
  }, []);

  const keys = useMemo(() => (data ?? []).map((f) => f.FieldKey.toLowerCase()), [data]);

  return { fields: data ?? [], keys, isLoading, error, errorKind, refresh };
}
