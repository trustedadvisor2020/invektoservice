import { inmaBridge } from './inmaBridge';
import { INMA_ERRORS, inmaErrorMessage } from './inmaErrors';
import { useInmaSession } from './inmaSession';

export interface InmaRequestOptions extends Omit<RequestInit, 'headers' | 'body'> {
  headers?: Record<string, string>;
  body?: unknown;
  query?: Record<string, string | number | boolean | undefined>;
}

export class InmaApiError extends Error {
  constructor(public readonly status: number, public readonly body: unknown, message: string) {
    super(message);
    this.name = 'InmaApiError';
  }
}

function buildUrl(apiBaseUrl: string, path: string, query?: InmaRequestOptions['query']): string {
  const base = apiBaseUrl.replace(/\/+$/, '');
  const rel = path.startsWith('/') ? path : `/${path}`;
  const url = `${base}${rel}`;
  if (!query) return url;
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(query)) {
    if (v !== undefined) search.append(k, String(v));
  }
  const qs = search.toString();
  return qs ? `${url}?${qs}` : url;
}

async function doFetch(path: string, options: InmaRequestOptions, token: string, apiBaseUrl: string): Promise<Response> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    Authorization: `Bearer ${token}`,
    ...(options.headers ?? {}),
  };
  let body: BodyInit | undefined;
  if (options.body !== undefined && options.body !== null) {
    if (typeof options.body === 'string' || options.body instanceof FormData || options.body instanceof Blob) {
      body = options.body as BodyInit;
    } else {
      headers['Content-Type'] = headers['Content-Type'] ?? 'application/json';
      body = JSON.stringify(options.body);
    }
  }
  return fetch(buildUrl(apiBaseUrl, path, options.query), {
    ...options,
    headers,
    body,
  });
}

async function request<T>(path: string, options: InmaRequestOptions = {}): Promise<T> {
  const session = useInmaSession.getState();
  if (!session.accessToken || !session.apiBaseUrl) {
    throw new InmaApiError(0, null, inmaErrorMessage(INMA_ERRORS.BRIDGE_NOT_READY));
  }

  let response = await doFetch(path, options, session.accessToken, session.apiBaseUrl);

  if (response.status === 401) {
    const newToken = await inmaBridge.requestRefresh();
    const latest = useInmaSession.getState();
    response = await doFetch(path, options, newToken, latest.apiBaseUrl ?? session.apiBaseUrl);
  }

  const contentType = response.headers.get('content-type') ?? '';
  const isJson = contentType.includes('application/json');
  const payload = isJson ? await response.json().catch(() => null) : await response.text().catch(() => null);

  if (!response.ok) {
    throw new InmaApiError(response.status, payload, inmaErrorMessage(INMA_ERRORS.HTTP_REQUEST_FAILED, `http_${response.status}`));
  }
  return payload as T;
}

export const inmaApiClient = {
  get: <T>(path: string, options?: Omit<InmaRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'GET' }),
  post: <T>(path: string, body?: unknown, options?: Omit<InmaRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, options?: Omit<InmaRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'PUT', body }),
  delete: <T>(path: string, options?: Omit<InmaRequestOptions, 'method' | 'body'>) =>
    request<T>(path, { ...options, method: 'DELETE' }),
  request,
};
