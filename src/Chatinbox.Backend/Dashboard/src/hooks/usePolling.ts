import { useState, useEffect, useCallback, useRef } from 'react';

interface UsePollingOptions<T> {
  fetcher: () => Promise<T>;
  interval: number;
  enabled?: boolean;
  onError?: (error: Error) => void;
}

export function usePolling<T>({ fetcher, interval, enabled = true, onError }: UsePollingOptions<T>) {
  const [data, setData] = useState<T | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const isMountedRef = useRef(true);
  const fetcherRef = useRef(fetcher);
  const onErrorRef = useRef(onError);

  // Update refs on each render (but don't trigger effects)
  fetcherRef.current = fetcher;
  onErrorRef.current = onError;

  const fetchData = useCallback(async () => {
    try {
      const result = await fetcherRef.current();
      if (isMountedRef.current) {
        setData(result);
        setError(null);
      }
    } catch (err) {
      if (isMountedRef.current) {
        const error = err instanceof Error ? err : new Error('Unknown error');
        setError(error);
        onErrorRef.current?.(error);
      }
    } finally {
      if (isMountedRef.current) {
        setIsLoading(false);
      }
    }
  }, []);

  const refresh = useCallback(() => {
    setIsLoading(true);
    fetchData();
  }, [fetchData]);

  useEffect(() => {
    isMountedRef.current = true;

    if (!enabled) {
      setIsLoading(false);
      return;
    }

    fetchData();

    const intervalId = setInterval(fetchData, interval);

    return () => {
      isMountedRef.current = false;
      clearInterval(intervalId);
    };
  }, [enabled, interval, fetchData]);

  return { data, isLoading, error, refresh };
}
