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

// Analytics types (PKT-3)
export interface TenantMetricsInfo {
  tenant_id: number;
  tenant_name: string;
  has_automation_data: boolean;
  has_wa_data: boolean;
  latest_metric_date: string | null;
}

export interface AutomationSummary {
  tenant_id: number;
  from: string;
  to: string;
  total_replies: number;
  deflected_count: number;
  handoff_count: number;
  deflection_rate: number;
  handoff_rate: number;
  avg_processing_time_ms: number;
  avg_confidence: number;
  reply_type_breakdown: Record<string, number>;
  session_status_breakdown: Record<string, number>;
}

export interface DailyMetric {
  date: string;
  total_replies: number;
  deflected_count: number;
  handoff_count: number;
  deflection_rate: number;
  avg_processing_time_ms: number;
}

export interface IntentMetric {
  intent: string;
  total_count: number;
  handoff_count: number;
  handoff_rate: number;
  avg_confidence: number;
  avg_processing_time_ms: number;
}

export interface WaAnalysisInfo {
  analysis_id: number;
  source_file_name: string | null;
  status: string;
  total_messages: number;
  total_conversations: number;
  completed_at: string | null;
}

export interface WaSummary {
  analysis_id: number;
  total_messages: number;
  total_conversations: number;
  outcome_breakdown: Record<string, number>;
  avg_first_response_minutes: number;
  avg_duration_minutes: number;
}

export interface WaAgentMetric {
  agent_name: string;
  total_conversations: number;
  sale_count: number;
  offered_count: number;
  no_sale_count: number;
  conversion_rate: number;
  avg_first_response_minutes: number;
}

export interface WaTrend {
  date: string;
  message_count: number;
  conversation_count: number;
  sale_count: number;
  offered_count: number;
}

// GR-3.18: Attribution types
export interface AttributionSummary {
  tenant_id: number;
  from: string;
  to: string;
  total_leads: number;
  converted_leads: number;
  conversion_rate: number;
  total_revenue: number;
  by_source: SourceBreakdown[];
  by_campaign: CampaignBreakdown[];
}

export interface SourceBreakdown {
  lead_source: string;
  lead_count: number;
  converted_count: number;
  conversion_rate: number;
  total_revenue: number;
}

export interface CampaignBreakdown {
  utm_campaign: string;
  lead_source: string;
  lead_count: number;
  converted_count: number;
  conversion_rate: number;
  total_revenue: number;
}

export interface CostPerLead {
  platform: string;
  total_cost: number;
  lead_count: number;
  cost_per_lead: number;
  converted_count: number;
  cost_per_conversion: number;
}

// GR-3.18: Campaign types
export interface CampaignStat {
  id: number;
  name: string;
  trigger_type: string;
  status: string;
  stats_json: string;
  template_name: string | null;
  created_at: string;
}

// inma SSO session info stored after successful auth
export interface InseSession {
  token: string;
  tenantId: number;
  userId: number;
  role: string;
  fullName: string;
  lang: string;
  inseFeatures: string[];
  expiresAt: number; // Unix timestamp ms
}

// inma auth response from backend
export interface InmaAuthResponse {
  token: string;
  tenant_id: number;
  user_id: number;
  role: string;
  full_name: string;
  lang: string;
  inse_features: string[];
  expires_in: number;
  token_type: string;
}

// API Client
class OpsApiClient {
  private credentials: string | null = null;
  private session: InseSession | null = null;
  public readonly baseUrl: string = '';

  constructor() {
    // Restore Basic Auth credentials (ops admin)
    this.credentials = sessionStorage.getItem('ops_auth');
    // Restore inma SSO session
    const raw = sessionStorage.getItem('inse_session');
    if (raw) {
      try {
        const parsed: InseSession = JSON.parse(raw);
        if (parsed.expiresAt > Date.now()) {
          this.session = parsed;
        } else {
          sessionStorage.removeItem('inse_session');
        }
      } catch (_e: unknown) {
        // Corrupted JSON in sessionStorage — clear silently, user will re-login
        console.error('[api] Failed to parse inse_session, clearing.', _e);
        sessionStorage.removeItem('inse_session');
      }
    }
  }

  // --- Basic Auth (ops admin) ---

  setCredentials(username: string, password: string): void {
    this.credentials = btoa(`${username}:${password}`);
    sessionStorage.setItem('ops_auth', this.credentials);
  }

  clearCredentials(): void {
    this.credentials = null;
    sessionStorage.removeItem('ops_auth');
  }

  // --- inma SSO session ---

  setSession(resp: InmaAuthResponse): void {
    this.session = {
      token: resp.token,
      tenantId: resp.tenant_id,
      userId: resp.user_id,
      role: resp.role,
      fullName: resp.full_name,
      lang: resp.lang,
      inseFeatures: resp.inse_features ?? [],
      expiresAt: Date.now() + resp.expires_in * 1000,
    };
    sessionStorage.setItem('inse_session', JSON.stringify(this.session));
  }

  clearSession(): void {
    this.session = null;
    sessionStorage.removeItem('inse_session');
  }

  getSession(): InseSession | null {
    return this.session;
  }

  hasFeature(feature: string): boolean {
    return this.session?.inseFeatures?.includes(feature) ?? false;
  }

  isAuthenticated(): boolean {
    if (this.session && this.session.expiresAt > Date.now()) return true;
    return this.credentials !== null;
  }

  isInmaSession(): boolean {
    return this.session !== null && this.session.expiresAt > Date.now();
  }

  getAuthHeaders(): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (this.session) {
      headers['Authorization'] = `Bearer ${this.session.token}`;
    } else if (this.credentials) {
      headers['Authorization'] = `Basic ${this.credentials}`;
    }
    return headers;
  }

  // --- inma auth calls ---

  async exchangeInmaToken(inmaToken: string): Promise<InmaAuthResponse> {
    const response = await fetch('/api/v1/inma/auth/exchange', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token: inmaToken }),
    });
    if (!response.ok) {
      const err = await response.text();
      throw new Error(err || `HTTP ${response.status}`);
    }
    return response.json();
  }

  async loginWithInmaCredentials(
    companyName: string,
    username: string,
    password: string
  ): Promise<InmaAuthResponse> {
    const response = await fetch('/api/v1/inma/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ company_name: companyName, username, password }),
    });
    if (!response.ok) {
      const err = await response.text();
      throw new Error(err || `HTTP ${response.status}`);
    }
    return response.json();
  }

  // --- internal request helper ---

  private async request<T>(endpoint: string, options?: RequestInit): Promise<T> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (this.session && this.session.expiresAt > Date.now()) {
      headers['Authorization'] = `Bearer ${this.session.token}`;
    } else if (this.credentials) {
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
      this.clearSession();
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

  // Analytics endpoints (PKT-3)
  async getAnalyticsTenants(): Promise<{ tenants: TenantMetricsInfo[] }> {
    return this.request<{ tenants: TenantMetricsInfo[] }>('/api/ops/analytics/tenants');
  }

  async getAutomationSummary(tenantId: number, from?: string, to?: string): Promise<AutomationSummary> {
    const sp = new URLSearchParams();
    sp.set('tenant_id', tenantId.toString());
    if (from) sp.set('from', from);
    if (to) sp.set('to', to);
    return this.request<AutomationSummary>(`/api/ops/analytics/automation/summary?${sp}`);
  }

  async getAutomationTrends(tenantId: number, from?: string, to?: string): Promise<{ tenant_id: number; trends: DailyMetric[] }> {
    const sp = new URLSearchParams();
    sp.set('tenant_id', tenantId.toString());
    if (from) sp.set('from', from);
    if (to) sp.set('to', to);
    return this.request<{ tenant_id: number; trends: DailyMetric[] }>(`/api/ops/analytics/automation/trends?${sp}`);
  }

  async getAutomationIntents(tenantId: number, from?: string, to?: string): Promise<{ tenant_id: number; intents: IntentMetric[] }> {
    const sp = new URLSearchParams();
    sp.set('tenant_id', tenantId.toString());
    if (from) sp.set('from', from);
    if (to) sp.set('to', to);
    return this.request<{ tenant_id: number; intents: IntentMetric[] }>(`/api/ops/analytics/automation/intents?${sp}`);
  }

  async getWaAnalyses(tenantId: number): Promise<{ analyses: WaAnalysisInfo[] }> {
    return this.request<{ analyses: WaAnalysisInfo[] }>(`/api/ops/analytics/wa/analyses?tenant_id=${tenantId}`);
  }

  async getWaSummary(tenantId: number, analysisId: number): Promise<WaSummary> {
    return this.request<WaSummary>(`/api/ops/analytics/wa/summary?tenant_id=${tenantId}&analysis_id=${analysisId}`);
  }

  async getWaAgents(tenantId: number, analysisId: number): Promise<{ agents: WaAgentMetric[] }> {
    return this.request<{ agents: WaAgentMetric[] }>(`/api/ops/analytics/wa/agents?tenant_id=${tenantId}&analysis_id=${analysisId}`);
  }

  async getWaTrends(tenantId: number, analysisId: number): Promise<{ trends: WaTrend[] }> {
    return this.request<{ trends: WaTrend[] }>(`/api/ops/analytics/wa/trends?tenant_id=${tenantId}&analysis_id=${analysisId}`);
  }

  // GR-3.18: Attribution + Campaign analytics
  async getAttributionSummary(tenantId: number, from?: string, to?: string): Promise<AttributionSummary> {
    const sp = new URLSearchParams();
    sp.set('tenant_id', tenantId.toString());
    if (from) sp.set('from', from);
    if (to) sp.set('to', to);
    return this.request<AttributionSummary>(`/api/ops/analytics/attribution/summary?${sp}`);
  }

  async getCostPerLead(tenantId: number, from?: string, to?: string): Promise<{ cost_per_lead: CostPerLead[] }> {
    const sp = new URLSearchParams();
    sp.set('tenant_id', tenantId.toString());
    if (from) sp.set('from', from);
    if (to) sp.set('to', to);
    return this.request<{ cost_per_lead: CostPerLead[] }>(`/api/ops/analytics/attribution/cost-per-lead?${sp}`);
  }

  async getCampaignStats(tenantId: number): Promise<{ campaigns: CampaignStat[] }> {
    return this.request<{ campaigns: CampaignStat[] }>(`/api/ops/analytics/campaigns?tenant_id=${tenantId}`);
  }

  private async requestUpload<T>(endpoint: string, file: File, title?: string): Promise<T> {
    const formData = new FormData();
    formData.append('file', file);
    if (title) formData.append('title', title);

    const headers: Record<string, string> = {};
    if (this.session && this.session.expiresAt > Date.now()) {
      headers['Authorization'] = `Bearer ${this.session.token}`;
    } else if (this.credentials) {
      headers['Authorization'] = `Basic ${this.credentials}`;
    }

    const response = await fetch(endpoint, { method: 'POST', headers, body: formData });
    if (response.status === 401) {
      this.clearCredentials();
      this.clearSession();
      throw new Error('Unauthorized');
    }
    if (!response.ok) { const error = await response.text(); throw new Error(error || `HTTP ${response.status}`); }
    return response.json();
  }
}

export const api = new OpsApiClient();
