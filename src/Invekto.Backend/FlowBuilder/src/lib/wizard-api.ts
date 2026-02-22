import type { WizardStreamEvent } from '../types/wizard';

const API_BASE = '/api/v1/flow-builder';

function getToken(): string | null {
  try {
    const raw = localStorage.getItem('fb_session') || sessionStorage.getItem('fb_session');
    if (!raw) return null;
    const session = JSON.parse(raw);
    return session.token || null;
  } catch (_e) {
    // localStorage/sessionStorage may throw in private browsing or cross-origin iframes
    return null;
  }
}

function authHeaders(): Record<string, string> {
  const token = getToken();
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

export async function startWizard(_tenantId?: number): Promise<{ flow_id: number }> {
  const res = await fetch(`${API_BASE}/wizard/start`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({}),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(err.error || err.message || `HTTP ${res.status}`);
  }
  return res.json();
}

export async function* streamMessage(
  flowId: number,
  _tenantId: number,
  message: string,
  signal?: AbortSignal,
  flowConfig?: object
): AsyncGenerator<WizardStreamEvent> {
  const body: Record<string, unknown> = { message };
  if (flowConfig) body.flow_config = flowConfig;

  const res = await fetch(`${API_BASE}/wizard/${flowId}/message`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify(body),
    signal,
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: 'Unknown error' }));
    yield { type: 'error', content: err.error || `HTTP ${res.status}` };
    return;
  }

  if (!res.body) {
    yield { type: 'error', content: 'SSE stream not available' };
    return;
  }
  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });
    const lines = buffer.split('\n');
    buffer = lines.pop() || '';

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue;
      const data = line.slice(6).trim();
      if (!data || data === '[DONE]') continue;

      try {
        const event: WizardStreamEvent = JSON.parse(data);
        yield event;
      } catch (_e) {
        // Malformed SSE JSON line — skip and continue (standard SSE practice)
        console.warn('Wizard SSE: skipping malformed JSON line', data);
      }
    }
  }

  // Process remaining buffer
  if (buffer.startsWith('data: ')) {
    const data = buffer.slice(6).trim();
    if (data && data !== '[DONE]') {
      try {
        yield JSON.parse(data);
      } catch (_e) {
        // Malformed trailing SSE data — skip
      }
    }
  }
}

export async function getWizardState(tenantId: number, flowId: number) {
  const res = await fetch(`${API_BASE}/flows/${tenantId}/${flowId}`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

export async function confirmWizard(
  flowId: number,
  _tenantId: number,
  flowName: string,
  flowConfig: object
): Promise<{ flow_id: number }> {
  const res = await fetch(`${API_BASE}/wizard/${flowId}/confirm`, {
    method: 'POST',
    headers: authHeaders(),
    body: JSON.stringify({ flow_name: flowName, flow_config: flowConfig }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: 'Unknown error' }));
    throw new Error(err.error || `HTTP ${res.status}`);
  }
  return res.json();
}

export async function deleteWizard(tenantId: number, flowId: number): Promise<void> {
  await fetch(`${API_BASE}/flows/${tenantId}/${flowId}`, {
    method: 'DELETE',
    headers: authHeaders(),
  });
}
