// API types
export interface ServiceHealth {
  name: string;
  status: 'ok' | 'unavailable' | 'degraded';
  responseTimeMs: number | null;
  uptimeSeconds: number | null;
  lastCheck: string;
  error?: string;
}

export interface HealthResponse {
  timestamp: string;
  services: ServiceHealth[];
  info: {
    stage: string;
    timeout_ms: number;
    retry_count: number;
    slow_threshold_ms: number;
  };
}

export interface LogEntry {
  id?: string;
  timestamp: string;
  service: string;
  level: 'INFO' | 'WARN' | 'ERROR';
  requestId: string;
  tenantId?: string;
  chatId?: string;
  route?: string;
  durationMs?: number;
  status?: string;
  errorCode?: string;
  message: string;
  category?: string;
}

export interface LogStreamResponse {
  entries: LogEntry[];
  hasMore: boolean;
  nextCursor?: string;
}

export interface LogContextResponse {
  target: LogEntry;
  before: LogEntry[];
  after: LogEntry[];
}

export interface LogGroup {
  requestId: string;
  startTime: string;
  endTime: string;
  durationMs: number | null;
  service: string;
  level: 'INFO' | 'WARN' | 'ERROR';
  route?: string;
  status?: string;
  errorCode?: string;
  entryCount: number;
  category?: string;
  summary: string;
  entries: LogEntry[];
}

export interface LogGroupedResponse {
  groups: LogGroup[];
  hasMore: boolean;
}

export interface ErrorStatsBucket {
  hour: string;
  count: number;
}

export interface ErrorStatsResponse {
  buckets: ErrorStatsBucket[];
  total: number;
}

export interface ServiceRestartResponse {
  success: boolean;
  service: string;
  message: string;
}

export interface EndpointInfo {
  method: string;
  path: string;
  description: string;
  auth: string | null;
  category: string;
}

export interface EndpointDiscoveryResponse {
  service: string;
  port: number;
  endpoints: EndpointInfo[];
}

// Knowledge types
export interface DocumentDto {
  id: number;
  tenantId: number;
  title: string;
  sourceType: string;
  status: string;
  filePath: string | null;
  chunkCount: number;
  metadataJson: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface FaqDto {
  id: number;
  tenantId: number;
  question: string;
  answer: string;
  category: string | null;
  lang: string;
  source: string;
  keywords: string[];
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

// API Client
class OpsApiClient {
  private credentials: string | null = null;
  public readonly baseUrl: string = '';

  constructor() {
    // Restore credentials from sessionStorage
    this.credentials = sessionStorage.getItem('ops_auth');
  }

  setCredentials(username: string, password: string): void {
    this.credentials = btoa(`${username}:${password}`);
    sessionStorage.setItem('ops_auth', this.credentials);
  }

  clearCredentials(): void {
    this.credentials = null;
    sessionStorage.removeItem('ops_auth');
  }

  isAuthenticated(): boolean {
    return this.credentials !== null;
  }

  getAuthHeaders(): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (this.credentials) {
      headers['Authorization'] = `Basic ${this.credentials}`;
    }
    return headers;
  }

  private async request<T>(endpoint: string, options?: RequestInit): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this.credentials) {
      headers['Authorization'] = `Basic ${this.credentials}`;
    }

    const response = await fetch(endpoint, {
      ...options,
      headers: {
        ...headers,
        ...options?.headers,
      },
    });

    if (response.status === 401) {
      this.clearCredentials();
      throw new Error('Unauthorized');
    }

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP ${response.status}`);
    }

    return response.json();
  }

  // Health endpoints
  async getHealth(): Promise<HealthResponse> {
    return this.request<HealthResponse>('/api/ops/health');
  }

  // Log endpoints
  async getLogs(params: {
    level?: string[];
    service?: string;
    search?: string;
    after?: string;
    limit?: number;
    cursor?: string;
  }): Promise<LogStreamResponse> {
    const searchParams = new URLSearchParams();
    if (params.level?.length) searchParams.set('level', params.level.join(','));
    if (params.service) searchParams.set('service', params.service);
    if (params.search) searchParams.set('search', params.search);
    if (params.after) searchParams.set('after', params.after);
    if (params.limit) searchParams.set('limit', params.limit.toString());
    if (params.cursor) searchParams.set('cursor', params.cursor);

    return this.request<LogStreamResponse>(`/api/ops/logs/stream?${searchParams}`);
  }

  async getLogsGrouped(params: {
    level?: string[];
    service?: string;
    search?: string;
    after?: string;
    limit?: number;
    category?: string;
  }): Promise<LogGroupedResponse> {
    const searchParams = new URLSearchParams();
    if (params.level?.length) searchParams.set('level', params.level.join(','));
    if (params.service) searchParams.set('service', params.service);
    if (params.search) searchParams.set('search', params.search);
    if (params.after) searchParams.set('after', params.after);
    if (params.limit) searchParams.set('limit', params.limit.toString());
    if (params.category) searchParams.set('category', params.category);

    return this.request<LogGroupedResponse>(`/api/ops/logs/grouped?${searchParams}`);
  }

  async getLogContext(file: string, line: number, range: number = 10): Promise<LogContextResponse> {
    const searchParams = new URLSearchParams({
      file,
      line: line.toString(),
      range: range.toString(),
    });
    return this.request<LogContextResponse>(`/api/ops/logs/context?${searchParams}`);
  }

  // Log management
  async clearLogs(service?: string): Promise<{ deleted: number; service: string }> {
    const params = service ? `?service=${encodeURIComponent(service)}` : '';
    return this.request(`/api/ops/logs/clear${params}`, { method: 'DELETE' });
  }

  // Stats endpoints
  async getErrorStats(hours: number = 24): Promise<ErrorStatsResponse> {
    return this.request<ErrorStatsResponse>(`/api/ops/stats/errors?hours=${hours}`);
  }

  // Service management
  async restartService(serviceName: string): Promise<ServiceRestartResponse> {
    return this.request<ServiceRestartResponse>(`/api/ops/services/${serviceName}/restart`, {
      method: 'POST',
    });
  }

  // Endpoint discovery (aggregated from all services)
  async getAllEndpoints(): Promise<{ services: EndpointDiscoveryResponse[] }> {
    return this.request<{ services: EndpointDiscoveryResponse[] }>('/api/ops/endpoints');
  }

  // Legacy ops endpoints (for backward compatibility)
  async getOpsStatus(): Promise<unknown> {
    return this.request('/ops');
  }

  async getOpsErrors(): Promise<{ count: number; errors: LogEntry[] }> {
    return this.request('/ops/errors');
  }

  async getOpsSlow(): Promise<{ count: number; threshold_ms: number; requests: LogEntry[] }> {
    return this.request('/ops/slow');
  }

  async searchByRequestId(requestId: string): Promise<{ requestId: string; count: number; entries: LogEntry[] }> {
    return this.request(`/ops/search?requestId=${encodeURIComponent(requestId)}`);
  }

  // Knowledge endpoints
  async getDocuments(tenantId: number, params?: { status?: string; page?: number; limit?: number }) {
    const sp = new URLSearchParams();
    if (params?.status) sp.set('status', params.status);
    if (params?.page) sp.set('page', params.page.toString());
    if (params?.limit) sp.set('limit', params.limit.toString());
    return this.request<{ documents: DocumentDto[]; total: number; page: number; limit: number }>(
      `/api/ops/knowledge/${tenantId}/documents?${sp}`);
  }

  async uploadDocument(tenantId: number, file: File, title?: string) {
    return this.requestUpload<{ documentId: number; status: string; title: string }>(
      `/api/ops/knowledge/${tenantId}/documents/upload`, file, title);
  }

  async deleteDocument(tenantId: number, docId: number) {
    return this.request<{ message: string; documentId: number }>(
      `/api/ops/knowledge/${tenantId}/documents/${docId}`, { method: 'DELETE' });
  }

  async getFaqs(tenantId: number, params?: { lang?: string; category?: string; page?: number; limit?: number }) {
    const sp = new URLSearchParams();
    if (params?.lang) sp.set('lang', params.lang);
    if (params?.category) sp.set('category', params.category);
    if (params?.page) sp.set('page', params.page.toString());
    if (params?.limit) sp.set('limit', params.limit.toString());
    return this.request<{ faqs: FaqDto[]; total: number; page: number; limit: number }>(
      `/api/ops/knowledge/${tenantId}/faqs?${sp}`);
  }

  async createFaq(tenantId: number, data: { question: string; answer: string; category?: string; lang?: string; keywords?: string[] }) {
    return this.request<FaqDto>(`/api/ops/knowledge/${tenantId}/faqs`, {
      method: 'POST', body: JSON.stringify(data),
    });
  }

  async updateFaq(tenantId: number, faqId: number, data: { question?: string; answer?: string; category?: string; lang?: string; keywords?: string[] }) {
    return this.request<FaqDto>(`/api/ops/knowledge/${tenantId}/faqs/${faqId}`, {
      method: 'PUT', body: JSON.stringify(data),
    });
  }

  async deleteFaq(tenantId: number, faqId: number) {
    return this.request<{ message: string; faqId: number }>(
      `/api/ops/knowledge/${tenantId}/faqs/${faqId}`, { method: 'DELETE' });
  }

  async generateEmbeddings(tenantId: number) {
    return this.request<{ message: string; generated: number; failed?: number; total?: number }>(
      `/api/ops/knowledge/${tenantId}/generate-embeddings`, { method: 'POST' });
  }

  private async requestUpload<T>(endpoint: string, file: File, title?: string): Promise<T> {
    const formData = new FormData();
    formData.append('file', file);
    if (title) formData.append('title', title);

    const headers: Record<string, string> = {};
    if (this.credentials) {
      headers['Authorization'] = `Basic ${this.credentials}`;
    }

    const response = await fetch(endpoint, { method: 'POST', headers, body: formData });
    if (response.status === 401) { this.clearCredentials(); throw new Error('Unauthorized'); }
    if (!response.ok) { const error = await response.text(); throw new Error(error || `HTTP ${response.status}`); }
    return response.json();
  }
}

export const api = new OpsApiClient();
