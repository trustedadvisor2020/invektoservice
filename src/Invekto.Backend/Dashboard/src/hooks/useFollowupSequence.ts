import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api, ApiClientError, type FollowupSequenceConfig } from '../lib/api';

// FEAT-EFS Drip Sequence — opt-in hook for the Dashboard editor page.
//
// Conditional-call safety (lessons 2026-04-22 P4 FAIL): hook is invoked
// UNCONDITIONALLY at every render; the `enabled` flag controls early-return
// (no fetch, no module cache touch, stable empty shape) when consumers want
// to defer loading. This mirrors useFieldMapping(enabled).
//
// Module cache: list result shared across mounts within the same editor
// session so re-opening the page does not re-hit Backend → Marketing for
// every navigation. Backend's MarketingFollowupProxyClient has no cache,
// so the only de-dup happens here.

export type FollowupSequenceErrorKind =
  | 'not_loaded'    // 404 — tenant has no sequences yet, treated as empty list
  | 'upstream_fail' // 5xx / network — Marketing or Backend transient
  | 'unknown';      // 401 / other — session/permission issue

interface FollowupCacheEntry {
  sequences: FollowupSequenceConfig[];
  testMode: boolean;
  noReplyThresholdDays: number;
}

let moduleCache: FollowupCacheEntry | null = null;
let inflight: Promise<FollowupCacheEntry> | null = null;

async function fetchSequences(): Promise<FollowupCacheEntry> {
  if (moduleCache) return moduleCache;
  if (inflight) return inflight;
  inflight = (async () => {
    try {
      const resp = await api.listFollowupSequences();
      moduleCache = {
        sequences: resp.data ?? [],
        testMode: resp.test_mode ?? false,
        noReplyThresholdDays: resp.no_reply_threshold_days ?? 3,
      };
      return moduleCache;
    } finally {
      inflight = null;
    }
  })();
  return inflight;
}

function classifyError(err: unknown): FollowupSequenceErrorKind {
  if (err instanceof ApiClientError) {
    if (err.status === 404) return 'not_loaded';
    if (err.status >= 500) return 'upstream_fail';
  }
  return 'unknown';
}

/**
 * wrapError canonical pattern — preserves the upstream INV-MK-* code from
 * ApiClientError verbatim (except UNKNOWN), falls back to INV-OB-037 for
 * non-Api errors. NEVER fabricates an INV-FE-* code (lessons 2026-04-22 P3).
 * Bracket format `[CODE] message` is the agreed log convention so ops can grep.
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

export interface FollowupSequenceState {
  sequences: FollowupSequenceConfig[];
  /** Tenant efs_test_mode flag — when true, stage delays are interpreted as MINUTES
   *  by the backend, so the editor must label units as "dk" instead of "gun". */
  testMode: boolean;
  /** Operator-tunable threshold for NoReplyCheckJob scheduling (shown in page hints). */
  noReplyThresholdDays: number;
  isLoading: boolean;
  error: Error | null;
  errorKind: FollowupSequenceErrorKind | null;
  refresh: () => Promise<void>;
  /**
   * Upsert a sequence and refresh the local cache. Throws (wrapped via
   * wrapError) on failure so the page can render the bracketed message
   * inline instead of swallowing it.
   */
  upsert: (config: FollowupSequenceConfig) => Promise<FollowupSequenceConfig>;
}

export function useFollowupSequence(enabled: boolean = true): FollowupSequenceState {
  const [data, setData] = useState<FollowupCacheEntry | null>(
    enabled ? moduleCache : null,
  );
  const [isLoading, setIsLoading] = useState(enabled ? moduleCache === null : false);
  const [error, setError] = useState<Error | null>(null);
  const [errorKind, setErrorKind] = useState<FollowupSequenceErrorKind | null>(null);
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
        const fresh = await fetchSequences();
        if (isMountedRef.current) {
          setData(fresh);
          setError(null);
          setErrorKind(null);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setError(wrapError(err, 'Followup sequence listesi yüklenemedi.'));
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
      const fresh = await fetchSequences();
      if (isMountedRef.current) {
        setData(fresh);
        setError(null);
        setErrorKind(null);
      }
    } catch (err) {
      if (isMountedRef.current) {
        setError(wrapError(err, 'Followup sequence listesi yenilenemedi.'));
        setErrorKind(classifyError(err));
      }
    } finally {
      if (isMountedRef.current) setIsLoading(false);
    }
  }, [enabled]);

  const upsert = useCallback(async (config: FollowupSequenceConfig): Promise<FollowupSequenceConfig> => {
    try {
      const resp = await api.upsertFollowupSequence(config);
      // Invalidate module cache so the next list fetch sees the new row.
      moduleCache = null;
      // Also refresh local state immediately for snappy UX.
      await refresh();
      return resp.data;
    } catch (err) {
      throw wrapError(err, 'Followup sequence kaydedilemedi.');
    }
  }, [refresh]);

  const result = useMemo<FollowupSequenceState>(() => ({
    sequences: data?.sequences ?? [],
    testMode: data?.testMode ?? false,
    noReplyThresholdDays: data?.noReplyThresholdDays ?? 3,
    isLoading,
    error,
    errorKind,
    refresh,
    upsert,
  }), [data, isLoading, error, errorKind, refresh, upsert]);

  return result;
}
