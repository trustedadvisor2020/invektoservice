import { loadSession, clearSession } from './auth';

const API_BASE = '/api/v1/flow-builder';

/** Standard error response from backend */
export interface ApiError {
  error_code: string;
  message: string;
  request_id?: string;
}

export class ApiClientError extends Error {
  constructor(
    public status: number,
    public errorCode: string,
    message: string,
    public requestId?: string,
  ) {
    super(message);
    this.name = 'ApiClientError';
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const session = loadSession();
  if (!session) {
    clearSession();
    window.location.replace('/flow-builder/login');
    throw new ApiClientError(401, 'NO_SESSION', 'Oturum bulunamadi');
  }

  const headers: Record<string, string> = {
    Authorization: `Bearer ${session.token}`,
  };

  let init: RequestInit = { method, headers };

  if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    init = { ...init, body: JSON.stringify(body) };
  }

  let res: Response;
  try {
    res = await fetch(`${API_BASE}${path}`, init);
  } catch {
    throw new ApiClientError(0, 'NETWORK_ERROR', 'Sunucuya baglanilamadi. Internet baglantinizi kontrol edin.');
  }

  if (res.status === 401) {
    clearSession();
    window.location.replace('/flow-builder/login');
    throw new ApiClientError(401, 'UNAUTHORIZED', 'Oturum suresi doldu');
  }

  if (!res.ok) {
    let errBody: ApiError | null = null;
    try {
      errBody = await res.json();
    } catch { /* non-JSON error response body — fall through to default message */ }
    throw new ApiClientError(
      res.status,
      errBody?.error_code ?? 'UNKNOWN',
      errBody?.message ?? `HTTP ${res.status}`,
      errBody?.request_id,
    );
  }

  // 204 No Content
  if (res.status === 204) return undefined as T;

  return res.json();
}

// -- Auth (no token needed) --

export interface LoginResponse {
  token: string;
  tenant_id: number;
  expires_in: number;
  token_type: string;
}

export async function login(tenantId: number, apiKey: string): Promise<LoginResponse> {
  let res: Response;
  try {
    res = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tenant_id: tenantId, api_key: apiKey }),
    });
  } catch {
    throw new ApiClientError(0, 'NETWORK_ERROR', 'Sunucuya baglanilamadi. Internet baglantinizi kontrol edin.');
  }

  if (!res.ok) {
    let errBody: ApiError | null = null;
    try {
      errBody = await res.json();
    } catch { /* non-JSON error response body — fall through to default message */ }
    throw new ApiClientError(
      res.status,
      errBody?.error_code ?? 'UNKNOWN',
      errBody?.message ?? `Giris basarisiz (HTTP ${res.status})`,
      errBody?.request_id,
    );
  }

  return res.json();
}

// -- Flows CRUD --

export interface FlowSummary {
  flow_id: number;
  flow_name: string;
  is_active: boolean;
  is_default: boolean;
  config_version: number;
  node_count: number;
  edge_count: number;
  created_at: string;
  updated_at: string;
  health_score: number | null;
  health_issues: string[] | null;
}

export interface FlowDetail {
  flow_id: number;
  tenant_id: number;
  flow_name: string;
  flow_config: unknown; // FlowConfigV2 JSON
  is_active: boolean;
  is_default: boolean;
  created_at: string;
  updated_at: string;
}

export function listFlows(tenantId: number): Promise<FlowSummary[]> {
  return request<FlowSummary[]>('GET', `/flows/${tenantId}`);
}

export function getFlow(tenantId: number, flowId: number): Promise<FlowDetail> {
  return request<FlowDetail>('GET', `/flows/${tenantId}/${flowId}`);
}

export function createFlow(tenantId: number, body: { flow_name: string; flow_config: unknown }): Promise<FlowDetail> {
  return request<FlowDetail>('POST', `/flows/${tenantId}`, body);
}

export function updateFlow(tenantId: number, flowId: number, body: { flow_name?: string; flow_config?: unknown }): Promise<FlowDetail> {
  return request<FlowDetail>('PUT', `/flows/${tenantId}/${flowId}`, body);
}

export function deleteFlow(tenantId: number, flowId: number): Promise<void> {
  return request<void>('DELETE', `/flows/${tenantId}/${flowId}`);
}

export function activateFlow(tenantId: number, flowId: number): Promise<void> {
  return request<void>('POST', `/flows/${tenantId}/${flowId}/activate`);
}

export function deactivateFlow(tenantId: number, flowId: number): Promise<void> {
  return request<void>('POST', `/flows/${tenantId}/${flowId}/deactivate`);
}

// -- Validation --

export interface ValidationResult {
  is_valid: boolean;
  errors: string[];
  warnings: string[];
}

export function validateFlow(flowConfig: unknown): Promise<ValidationResult> {
  return request<ValidationResult>('POST', '/flows/validate', { flow_config: flowConfig });
}

// -- Simulation --

export interface SimulationMessage {
  role: 'bot' | 'user' | 'system';
  text: string;
}

export interface SimulationPendingInput {
  type: 'menu' | 'text';
  options?: string[];
}

export interface SimulationStartResponse {
  session_id: string;
  messages: SimulationMessage[];
  current_node_id: string;
  variables: Record<string, string>;
  execution_path: string[];
  status: string;
  pending_input: SimulationPendingInput | null;
}

export interface SimulationStepResponse {
  messages: SimulationMessage[];
  current_node_id: string;
  variables: Record<string, string>;
  execution_path: string[];
  status: string;
  is_terminal: boolean;
  pending_input: SimulationPendingInput | null;
}

export function simulationStart(tenantId: number, flowId: number): Promise<SimulationStartResponse> {
  return request<SimulationStartResponse>('POST', '/simulation/start', { tenant_id: tenantId, flow_id: flowId });
}

export function simulationStep(sessionId: string, message: string): Promise<SimulationStepResponse> {
  return request<SimulationStepResponse>('POST', '/simulation/step', { session_id: sessionId, message });
}

export function simulationCleanup(sessionId: string): Promise<void> {
  return request<void>('DELETE', `/simulation/${sessionId}`);
}
