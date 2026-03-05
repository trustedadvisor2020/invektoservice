import type { WizardStreamEvent } from '../types/wizard';
import { api, ApiClientError } from './api';

const API_BASE = '/api/v1/flow-builder';

/** Parse error body from a failed fetch response and throw ApiClientError */
async function throwApiError(res: Response): Promise<never> {
  let body: { error_code?: string; error?: string; message?: string; request_id?: string } | null = null;
  try { body = await res.json(); } catch (_e) { /* non-JSON error body */ }
  throw new ApiClientError(
    res.status,
    body?.error_code ?? 'UNKNOWN',
    body?.error ?? body?.message ?? `HTTP ${res.status}`,
    body?.request_id,
  );
}

export async function startWizard(_tenantId?: number): Promise<{ flow_id: number }> {
  const res = await fetch(`${API_BASE}/wizard/start`, {
    method: 'POST',
    headers: api.getAuthHeaders(),
    body: JSON.stringify({}),
  });
  if (!res.ok) await throwApiError(res);
  return res.json();
}

export async function* streamMessage(
  flowId: number,
  _tenantId: number,
  message: string,
  signal?: AbortSignal,
  flowConfig?: object,
  executionDetail?: object
): AsyncGenerator<WizardStreamEvent> {
  const body: Record<string, unknown> = { message };
  if (flowConfig) body.flow_config = flowConfig;
  if (executionDetail) body.execution_detail = executionDetail;

  const res = await fetch(`${API_BASE}/wizard/${flowId}/message`, {
    method: 'POST',
    headers: api.getAuthHeaders(),
    body: JSON.stringify(body),
    signal,
  });

  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
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
    headers: api.getAuthHeaders(),
  });
  if (!res.ok) await throwApiError(res);
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
    headers: api.getAuthHeaders(),
    body: JSON.stringify({ flow_name: flowName, flow_config: flowConfig }),
  });
  if (!res.ok) await throwApiError(res);
  return res.json();
}

export async function deleteWizard(tenantId: number, flowId: number): Promise<void> {
  const res = await fetch(`${API_BASE}/flows/${tenantId}/${flowId}`, {
    method: 'DELETE',
    headers: api.getAuthHeaders(),
  });
  if (!res.ok) await throwApiError(res);
}
