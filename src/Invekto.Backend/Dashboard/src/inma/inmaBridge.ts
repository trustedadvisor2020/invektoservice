import { useInmaSession } from './inmaSession';
import { INMA_ERRORS, inmaErrorMessage, type InmaErrorCode } from './inmaErrors';

// Parent INMA portal'inin origin'i. 'app.invekto.com' BIZIM kendi iframe domain'imiz olup
// whitelist'te olmamalidir; parent INMA prod'da 'app.wapcrm.net' uzerinden gonderir.
// localhost:4200 prod'da da whitelisted - INMA takimi gelistirme sirasinda embed testi icin.
const INMA_ALLOWED_ORIGINS: ReadonlyArray<string> = import.meta.env.PROD
  ? ['https://app.wapcrm.net', 'https://developer.wapcrm.net', 'http://localhost:4200']
  : ['https://developer.wapcrm.net', 'http://localhost:4200'];

const INMA_API_BASE_URL_REGEX = /^https:\/\/[a-z0-9-]+\.wapcrm\.net\/?$/;
const INMA_REFRESH_TIMEOUT_MS = 15_000;

type InmaInboundMessage =
  | { type: 'inma:auth'; accessToken: string; apiBaseUrl: string }
  | { type: 'inma:refresh-response'; accessToken: string }
  | { type: 'inma:refresh-error'; reason?: string }
  | { type: 'inma:logout' }
  | { type: 'inma:navigate'; path: string };

export interface InmaBridgeCallbacks {
  onReady?: () => void;
  onLogout?: () => void;
  onAuthError?: (reason: string) => void;
  onNavigate?: (path: string) => void;
}

interface PendingRefresh {
  promise: Promise<string>;
  resolve: (token: string) => void;
  reject: (err: Error) => void;
  timer: ReturnType<typeof setTimeout>;
}

class InmaBridgeImpl {
  private callbacks: InmaBridgeCallbacks = {};
  private pendingRefresh: PendingRefresh | null = null;
  private listenerAttached = false;
  private readonly handleMessage = (event: MessageEvent) => this.onMessage(event);

  init(callbacks: InmaBridgeCallbacks = {}): () => void {
    console.log('[inma-debug] bridge.init called', {
      allowedOrigins: INMA_ALLOWED_ORIGINS,
      isProd: import.meta.env.PROD,
      selfOrigin: window.location.origin,
      hasParent: window.parent !== window,
      callbackKeys: Object.keys(callbacks),
    });
    this.callbacks = callbacks;
    if (!this.listenerAttached) {
      window.addEventListener('message', this.handleMessage);
      this.listenerAttached = true;
      console.log('[inma-debug] message listener attached');
    }
    // Parent origin not yet known; 'inma:ready' is the only outbound using '*'.
    window.parent?.postMessage({ type: 'inma:ready' }, '*');
    console.log('[inma-debug] inma:ready sent to parent with targetOrigin=*');
    return () => this.dispose();
  }

  dispose(): void {
    if (this.listenerAttached) {
      window.removeEventListener('message', this.handleMessage);
      this.listenerAttached = false;
    }
    if (this.pendingRefresh) {
      clearTimeout(this.pendingRefresh.timer);
      this.pendingRefresh.reject(new Error(inmaErrorMessage(INMA_ERRORS.BRIDGE_DISPOSED)));
      this.pendingRefresh = null;
    }
    this.callbacks = {};
  }

  requestRefresh(): Promise<string> {
    if (this.pendingRefresh) return this.pendingRefresh.promise;

    const { trustedParentOrigin } = useInmaSession.getState();
    if (!trustedParentOrigin) {
      return Promise.reject(new Error(inmaErrorMessage(INMA_ERRORS.BRIDGE_NOT_READY)));
    }

    let resolve!: (token: string) => void;
    let reject!: (err: Error) => void;
    const promise = new Promise<string>((res, rej) => {
      resolve = res;
      reject = rej;
    });

    const timer = setTimeout(() => {
      if (this.pendingRefresh && this.pendingRefresh.promise === promise) {
        this.pendingRefresh = null;
        reject(new Error(inmaErrorMessage(INMA_ERRORS.REFRESH_TIMEOUT)));
      }
    }, INMA_REFRESH_TIMEOUT_MS);

    this.pendingRefresh = { promise, resolve, reject, timer };

    window.parent?.postMessage({ type: 'inma:refresh-request' }, trustedParentOrigin);
    return promise;
  }

  private onMessage(event: MessageEvent): void {
    const rawType = typeof (event.data as { type?: unknown } | undefined)?.type === 'string'
      ? (event.data as { type: string }).type
      : '<non-string>';
    console.log('[inma-debug] postMessage received', {
      origin: event.origin,
      type: rawType,
      allowedOrigins: INMA_ALLOWED_ORIGINS,
    });
    if (!INMA_ALLOWED_ORIGINS.includes(event.origin)) {
      console.warn('[inma-debug] ORIGIN REJECTED - message discarded silently pre-debug', {
        receivedOrigin: event.origin,
        allowedOrigins: INMA_ALLOWED_ORIGINS,
        hint: 'add parent origin to INMA_ALLOWED_ORIGINS (inmaBridge.ts) OR parent must postMessage from one of allowed origins',
      });
      return;
    }
    const data = event.data as InmaInboundMessage | undefined;
    if (!data || typeof data !== 'object' || typeof (data as { type?: unknown }).type !== 'string') {
      console.warn('[inma-debug] invalid payload shape (non-object or missing type)', { data });
      return;
    }

    console.log('[inma-debug] routing to case', { type: data.type });
    const session = useInmaSession.getState();

    switch (data.type) {
      case 'inma:auth': {
        if (typeof data.accessToken !== 'string' || !data.accessToken) {
          console.warn('[inma-debug] inma:auth rejected: invalid accessToken', { hasToken: !!data.accessToken });
          session.setError(INMA_ERRORS.INVALID_ACCESS_TOKEN);
          this.callbacks.onAuthError?.(INMA_ERRORS.INVALID_ACCESS_TOKEN);
          return;
        }
        if (typeof data.apiBaseUrl !== 'string' || !INMA_API_BASE_URL_REGEX.test(data.apiBaseUrl)) {
          console.warn('[inma-debug] inma:auth rejected: invalid apiBaseUrl', {
            apiBaseUrl: data.apiBaseUrl,
            regex: INMA_API_BASE_URL_REGEX.source,
          });
          session.setError(INMA_ERRORS.INVALID_API_BASE_URL);
          this.callbacks.onAuthError?.(INMA_ERRORS.INVALID_API_BASE_URL);
          return;
        }
        console.log('[inma-debug] inma:auth validated, setting session + firing onReady', {
          accessTokenLength: data.accessToken.length,
          apiBaseUrl: data.apiBaseUrl,
          parentOrigin: event.origin,
          hasOnReady: !!this.callbacks.onReady,
        });
        session.setAuth(data.accessToken, data.apiBaseUrl, event.origin);
        this.callbacks.onReady?.();
        console.log('[inma-debug] onReady callback returned');
        return;
      }
      case 'inma:refresh-response': {
        if (typeof data.accessToken !== 'string' || !data.accessToken) {
          this.failPendingRefresh(INMA_ERRORS.INVALID_ACCESS_TOKEN);
          return;
        }
        session.setAccessToken(data.accessToken);
        if (this.pendingRefresh) {
          clearTimeout(this.pendingRefresh.timer);
          const { resolve } = this.pendingRefresh;
          this.pendingRefresh = null;
          resolve(data.accessToken);
        }
        return;
      }
      case 'inma:refresh-error': {
        // Parent'in gonderdigi data.reason diagnostic detail olarak saklanir;
        // ancak error code HER ZAMAN INV-INT-103'e normalize edilir (CQ9: parent-controlled string app'e sizdirmaz).
        const parentDetail = typeof data.reason === 'string' ? data.reason : undefined;
        this.failPendingRefresh(INMA_ERRORS.REFRESH_FAILED, parentDetail);
        this.callbacks.onAuthError?.(INMA_ERRORS.REFRESH_FAILED);
        return;
      }
      case 'inma:logout': {
        console.log('[inma-debug] inma:logout received, clearing session + firing onLogout');
        session.clear();
        this.failPendingRefresh(INMA_ERRORS.BRIDGE_DISPOSED);
        this.callbacks.onLogout?.();
        return;
      }
      case 'inma:navigate': {
        // Input validation: path must be absolute local ('/foo') but not protocol-relative ('//evil.com').
        // Auth check is delegated to the onNavigate callback (App.tsx) where auth context lives.
        if (typeof data.path !== 'string' || !data.path.startsWith('/') || data.path.startsWith('//')) {
          console.warn('[inma-debug] inma:navigate rejected: invalid path', { path: data.path });
          console.warn(inmaErrorMessage(INMA_ERRORS.NAVIGATE_REJECTED, 'invalid_path'));
          return;
        }
        console.log('[inma-debug] inma:navigate firing onNavigate', { path: data.path });
        this.callbacks.onNavigate?.(data.path);
        return;
      }
    }
  }

  private failPendingRefresh(code: InmaErrorCode, detail?: string): void {
    if (!this.pendingRefresh) return;
    clearTimeout(this.pendingRefresh.timer);
    const { reject } = this.pendingRefresh;
    this.pendingRefresh = null;
    reject(new Error(inmaErrorMessage(code, detail)));
  }
}

export const inmaBridge = new InmaBridgeImpl();
