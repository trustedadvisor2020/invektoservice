import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  api,
  ApiClientError,
  type CampaignConfigDto,
  type CampaignConfigPutRequest,
} from '../lib/api';

// FEAT-MCC Multi-City Campaign — opt-in hook for the Dashboard editor page.
// Mirrors useFollowupSequence(enabled) pattern: hook is invoked unconditionally
// at every render; the `enabled` flag controls early-return so consumers can
// defer loading without violating React's rules-of-hooks.
//
// Module cache: shared across mounts within the same editor session so navigating
// away/back does not re-hit Backend for every visit.

export type CampaignConfigErrorKind =
  | 'not_loaded'    // 404 — tenant has no row yet, treated as empty config
  | 'upstream_fail' // 5xx / network — Backend transient (INV-BE-121)
  | 'unknown';      // 401 / other — session/permission issue

let moduleCache: { config: CampaignConfigDto; updatedAt: string | null } | null = null;
let inflight: Promise<{ config: CampaignConfigDto; updatedAt: string | null }> | null = null;

async function fetchConfig(): Promise<{ config: CampaignConfigDto; updatedAt: string | null }> {
  if (moduleCache) return moduleCache;
  if (inflight) return inflight;
  inflight = (async () => {
    try {
      const resp = await api.getCampaignConfig();
      moduleCache = {
        config: resp.data?.campaign_config ?? { campaigns: [] },
        updatedAt: resp.data?.updated_at ?? null,
      };
      return moduleCache;
    } finally {
      inflight = null;
    }
  })();
  return inflight;
}

function classifyError(err: unknown): CampaignConfigErrorKind {
  if (err instanceof ApiClientError) {
    if (err.status === 404) return 'not_loaded';
    if (err.status >= 500) return 'upstream_fail';
  }
  return 'unknown';
}

/**
 * wrapError canonical pattern (lessons 2026-04-22 P3) — preserves the upstream
 * INV-BE-* code from ApiClientError verbatim (except UNKNOWN), falls back to
 * INV-OB-037 for non-Api errors. NEVER fabricates an INV-FE-* code. Bracket
 * format `[CODE] message` so ops can grep.
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

export interface CampaignConfigState {
  config: CampaignConfigDto;
  updatedAt: string | null;
  isLoading: boolean;
  error: Error | null;
  errorKind: CampaignConfigErrorKind | null;
  refresh: () => Promise<void>;
  /**
   * Upsert the full config and refresh local cache. Throws (wrapped) on failure.
   * Editor page renders the bracketed message inline.
   */
  save: (config: CampaignConfigDto) => Promise<CampaignConfigDto>;
}

export function useCampaignConfig(enabled: boolean = true): CampaignConfigState {
  const [data, setData] = useState<{ config: CampaignConfigDto; updatedAt: string | null } | null>(
    enabled ? moduleCache : null,
  );
  const [isLoading, setIsLoading] = useState(enabled ? moduleCache === null : false);
  const [error, setError] = useState<Error | null>(null);
  const [errorKind, setErrorKind] = useState<CampaignConfigErrorKind | null>(null);
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
        const fresh = await fetchConfig();
        if (isMountedRef.current) {
          setData(fresh);
          setError(null);
          setErrorKind(null);
        }
      } catch (err) {
        if (isMountedRef.current) {
          setError(wrapError(err, 'Kampanya ayarlari yuklenemedi.'));
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
      const fresh = await fetchConfig();
      if (isMountedRef.current) {
        setData(fresh);
        setError(null);
        setErrorKind(null);
      }
    } catch (err) {
      if (isMountedRef.current) {
        setError(wrapError(err, 'Kampanya ayarlari yenilenemedi.'));
        setErrorKind(classifyError(err));
      }
    } finally {
      if (isMountedRef.current) setIsLoading(false);
    }
  }, [enabled]);

  const save = useCallback(async (config: CampaignConfigDto): Promise<CampaignConfigDto> => {
    try {
      const req: CampaignConfigPutRequest = { campaign_config: config };
      const resp = await api.putCampaignConfig(req);
      moduleCache = null;
      await refresh();
      return resp.data?.campaign_config ?? config;
    } catch (err) {
      throw wrapError(err, 'Kampanya ayarlari kaydedilemedi.');
    }
  }, [refresh]);

  const result = useMemo<CampaignConfigState>(() => ({
    config: data?.config ?? { campaigns: [] },
    updatedAt: data?.updatedAt ?? null,
    isLoading,
    error,
    errorKind,
    refresh,
    save,
  }), [data, isLoading, error, errorKind, refresh, save]);

  return result;
}
