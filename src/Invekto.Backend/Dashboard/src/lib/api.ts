import { jwtDecode } from 'jwt-decode';

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

// Message log types (SuperAdmin)
export interface MessageLogEntry {
  id: number;
  tenantId: number;
  direction: string;
  phone: string;
  senderName: string | null;
  messageText: string | null;
  messageType: string | null;
  chatId: string | null;
  externalMessageId: string | null;
  instanceId: string | null;
  createdAt: string;
}

// Message story types (SuperAdmin)
export interface TimelineItem {
  time: string;
  icon: string;
  title: string;
  detail: string;
}

export interface MessageStorySummary {
  flow_name: string | null;
  flow_id: number | null;
  intent: string | null;
  confidence: number | null;
  reply_type: string | null;
  processing_time_ms: number | null;
  auto_reply_count: number;
  outgoing_count: number;
}

export interface MessageStoryResponse {
  timeline: TimelineItem[];
  summary: MessageStorySummary;
}

// Tenant registry types (SuperAdmin)
export interface TenantEntry {
  tenantId: number;
  tenantName: string;
  isActive: boolean;
  sector: string | null;
  planTier: string;
  createdAt: string;
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

// INMA JWT decoded claims
export interface InmaTokenClaims {
  exp: number;
  iat?: number;
  FullName?: string;
  CompanyCode?: string;
  CompanyId?: string;
  IconText?: string;
  ChatRole?: string;
  SignalRKey?: string;
  ApiServerUrl?: string;
  ChatServerUrl?: string;
  AuthorizedInstances?: string;
  AuthorizedAreas?: string;
  Permissions?: string;
  PhoneViewPermission?: string;
  ChannelManagementPermission?: string;
  CustomerMergePermission?: string;
  InseFeatures?: string;
  Lang?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'?: string;
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'?: string;
  // INSE JWT claims (mock/quicklogin tokens)
  tenant_id?: string;
  user_id?: string;
  role?: string;
  source?: string;
}

// Session info derived from decoded JWT (backward compatible with old InseSession)
export interface InseSession {
  token: string;
  tenantId: number;
  userId: number;
  role: string;
  fullName: string;
  lang: string;
  inseFeatures: string[];
  expiresAt: number; // Unix timestamp ms
  companyCode: string;
}

// inma auth response from backend
export interface InmaAuthResponse {
  token: string;
  refresh_token?: string;
  tenant_id: number;
  user_id: number;
  role: string;
  full_name: string;
  lang: string;
  inse_features: string[];
  expires_in: number;
  token_type: string;
}

// Refresh token response
interface RefreshResponse {
  accessToken: string;
  refreshToken: string;
}

// localStorage keys
const TOKEN_KEYS = {
  ACCESS_TOKEN: 'access_token',
  REFRESH_TOKEN: 'refresh_token',
} as const;

// API Client
class OpsApiClient {
  private credentials: string | null = null;
  private isRefreshing = false;
  private refreshPromise: Promise<boolean> | null = null;
  public readonly baseUrl: string = '';

  constructor() {
    // Restore Basic Auth credentials (ops admin)
    this.credentials = sessionStorage.getItem('ops_auth');
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

  // --- Token storage (localStorage) ---

  getAccessToken(): string | null {
    return localStorage.getItem(TOKEN_KEYS.ACCESS_TOKEN);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(TOKEN_KEYS.REFRESH_TOKEN);
  }

  storeTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem(TOKEN_KEYS.ACCESS_TOKEN, accessToken);
    if (refreshToken) {
      localStorage.setItem(TOKEN_KEYS.REFRESH_TOKEN, refreshToken);
    }
    // Sync FlowBuilder iframe session (same origin, shared localStorage)
    const session = this.getSession();
    if (session) {
      localStorage.setItem('fb_session', JSON.stringify({
        token: accessToken,
        tenant_id: session.tenantId,
        expires_at: session.expiresAt,
      }));
    }
  }

  removeTokens(): void {
    localStorage.removeItem(TOKEN_KEYS.ACCESS_TOKEN);
    localStorage.removeItem(TOKEN_KEYS.REFRESH_TOKEN);
    localStorage.removeItem('fb_session');
    // Clean up legacy sessionStorage keys
    sessionStorage.removeItem('inse_session');
    sessionStorage.removeItem('fb_session');
  }

  // --- JWT decode ---

  getDecodedToken(): InmaTokenClaims | null {
    const token = this.getAccessToken();
    if (!token) return null;
    try {
      const decoded = jwtDecode<InmaTokenClaims>(token);
      // Check expiry
      if (decoded.exp && decoded.exp * 1000 <= Date.now()) {
        return null;
      }
      return decoded;
    } catch (err) {
      console.warn('[api] JWT decode failed:', err);
      return null;
    }
  }

  // --- Session info (backward compatible) ---

  setSession(resp: InmaAuthResponse): void {
    this.storeTokens(resp.token, resp.refresh_token ?? '');
  }

  getSession(): InseSession | null {
    const token = this.getAccessToken();
    if (!token) return null;

    const decoded = this.getDecodedToken();
    if (!decoded) return null;

    // Handle both INMA JWT claims and INSE JWT claims (mock/quicklogin)
    // CompanyCode = our tenant_id (e.g. "5050"), CompanyId = INMA's internal ID
    const isInmaToken = !!decoded.CompanyCode || !!decoded.FullName;

    let features: string[] = [];
    if (decoded.InseFeatures) {
      try { features = JSON.parse(decoded.InseFeatures); } catch (err) { console.warn('[api] InseFeatures parse failed:', err); }
    }

    if (isInmaToken) {
      // INMA JWT: decode INMA-specific claims (CompanyCode = tenant_id)
      const nameId = decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'];
      return {
        token,
        tenantId: parseInt(decoded.CompanyCode ?? '0') || 0,
        userId: parseInt(nameId ?? '0') || 0,
        role: decoded.ChatRole === '2' ? 'admin' : 'agent',
        fullName: decoded.FullName ?? '',
        lang: decoded.Lang ?? 'tr',
        inseFeatures: features,
        expiresAt: decoded.exp * 1000,
        companyCode: decoded.CompanyCode ?? '',
      };
    }

    // INSE JWT (mock/quicklogin): use inse claim names
    return {
      token,
      tenantId: parseInt(decoded.tenant_id ?? '0') || 0,
      userId: parseInt(decoded.user_id ?? '0') || 0,
      role: decoded.role ?? 'agent',
      fullName: decoded.source === 'ops_quicklogin' ? 'Super Admin'
        : decoded.source === 'ops_impersonate' ? 'SuperAdmin'
        : 'Demo User',
      lang: 'tr',
      inseFeatures: features,
      expiresAt: decoded.exp * 1000,
      companyCode: '',
    };
  }

  clearSession(): void {
    this.removeTokens();
  }

  hasFeature(feature: string): boolean {
    const session = this.getSession();
    if (!session) return false;
    // INMA SSO users: all features enabled (no InseFeatures claim in INMA JWT)
    if (session.inseFeatures.length === 0 && this.isInmaSession()) return true;
    return session.inseFeatures.includes(feature);
  }

  isAuthenticated(): boolean {
    if (this.getAccessToken() && this.getDecodedToken()) return true;
    return this.credentials !== null;
  }

  isInmaSession(): boolean {
    return this.getAccessToken() !== null && this.getDecodedToken() !== null;
  }

  isImpersonating(): boolean {
    return this.getDecodedToken()?.source === 'ops_impersonate';
  }

  getAuthHeaders(): Record<string, string> {
    return this.buildHeaders();
  }

  // --- INMA token exchange for FlowBuilder ---

  /**
   * Exchanges raw INMA JWT for an INSE JWT and updates fb_session.
   * FlowBuilder backend validates with INSE JwtValidator, so INMA JWTs fail 401.
   * This method is fire-and-forget — called after URL token SSO flow.
   */
  async exchangeInmaToken(): Promise<void> {
    const token = this.getAccessToken();
    if (!token) return;

    // Only exchange INMA tokens (have CompanyCode claim, not INSE tokens)
    const decoded = this.getDecodedToken();
    if (!decoded?.CompanyCode) return;

    try {
      const response = await fetch('/api/v1/inma/auth/exchange', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token }),
      });

      if (!response.ok) {
        console.warn('[api] INMA token exchange failed:', response.status);
        return;
      }

      const data: InmaAuthResponse = await response.json();

      // Update fb_session with INSE JWT (FlowBuilder can validate this)
      localStorage.setItem('fb_session', JSON.stringify({
        token: data.token,
        tenant_id: data.tenant_id,
        expires_at: Date.now() + data.expires_in * 1000,
      }));
    } catch (err) {
      console.warn('[api] INMA token exchange for FlowBuilder failed:', err);
    }
  }

  // --- inma auth calls ---

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

  async quickAdminLogin(): Promise<InmaAuthResponse> {
    const response = await fetch('/api/v1/ops/auth/quicklogin', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
    });
    if (!response.ok) {
      const err = await response.text();
      throw new Error(err || `HTTP ${response.status}`);
    }
    return response.json();
  }

  async mockLogin(scenario: 'full' | 'klinik' | 'otel'): Promise<InmaAuthResponse> {
    const response = await fetch('/api/v1/inma/auth/mock-login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scenario }),
    });
    if (!response.ok) {
      const err = await response.text();
      throw new Error(err || `HTTP ${response.status}`);
    }
    return response.json();
  }

  // --- Welcome endpoint ---

  async getWelcome(): Promise<unknown> {
    return this.request<unknown>('/api/v1/inma/welcome');
  }

  // --- 401 refresh logic ---

  private async handleRefresh(): Promise<boolean> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken || refreshToken.length < 10) return false;

    // If already refreshing, wait for the existing promise
    if (this.isRefreshing && this.refreshPromise) {
      return this.refreshPromise;
    }

    this.isRefreshing = true;
    this.refreshPromise = this.doRefresh(refreshToken);

    try {
      return await this.refreshPromise;
    } finally {
      this.isRefreshing = false;
      this.refreshPromise = null;
    }
  }

  private async doRefresh(refreshToken: string): Promise<boolean> {
    try {
      const response = await fetch('/api/v1/inma/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          accessToken: this.getAccessToken() ?? '',
          refreshToken,
        }),
      });

      if (!response.ok) return false;

      const tokens: RefreshResponse = await response.json();
      if (!tokens.accessToken) return false;

      this.storeTokens(tokens.accessToken, tokens.refreshToken);
      return true;
    } catch (err) {
      console.warn('[api] token refresh failed:', err);
      return false;
    }
  }

  // --- Auth header builder ---

  private buildHeaders(extra?: HeadersInit): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Requested-With': 'fetch',
    };

    const accessToken = this.getAccessToken();
    if (accessToken) {
      headers['Authorization'] = `Bearer ${accessToken}`;
    } else if (this.credentials) {
      headers['Authorization'] = `Basic ${this.credentials}`;
    }

    // Merge extra headers
    if (extra) {
      const extraObj = extra instanceof Headers
        ? Object.fromEntries(extra.entries())
        : Array.isArray(extra) ? Object.fromEntries(extra) : extra;
      Object.assign(headers, extraObj);
    }

    return headers;
  }

  // --- 401 interceptor: shared retry-with-refresh wrapper ---

  private async executeWithRefresh(doFetch: () => Promise<Response>): Promise<Response> {
    let response = await doFetch();

    if (response.status === 401 && this.getRefreshToken()) {
      const refreshed = await this.handleRefresh();
      if (refreshed) {
        response = await doFetch();
      } else if (!this.isInmaSession()) {
        // Ops/Basic Auth: refresh failed → session truly invalid, wipe everything.
        this.removeTokens();
        this.clearCredentials();
        throw new Error('INV-AU-001: Session expired, refresh failed');
      } else {
        // INMA session: 401 from ops-only endpoint, NOT a session problem.
        // Token validity is enforced by getDecodedToken() expiry check —
        // once JWT expires, isInmaSession() returns false and this branch
        // is never reached, so stale tokens cannot persist.
        throw new Error('INV-AU-002: Endpoint requires ops auth');
      }
    }

    if (response.status === 401) {
      if (!this.isInmaSession()) {
        this.removeTokens();
        this.clearCredentials();
        throw new Error('INV-AU-001: Session expired, refresh failed');
      }
      // INMA session: endpoint rejected JWT but session is still valid
      throw new Error('INV-AU-002: Endpoint requires ops auth');
    }

    return response;
  }

  // --- internal request helpers ---

  private async request<T>(endpoint: string, options?: RequestInit): Promise<T> {
    const response = await this.executeWithRefresh(() =>
      fetch(endpoint, { ...options, headers: this.buildHeaders(options?.headers) })
    );

    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || `HTTP ${response.status}`);
    }

    return response.json();
  }

  private async requestUpload<T>(endpoint: string, file: File, title?: string): Promise<T> {
    const formData = new FormData();
    formData.append('file', file);
    if (title) formData.append('title', title);

    const buildUploadHeaders = (): Record<string, string> => {
      const headers: Record<string, string> = { 'X-Requested-With': 'fetch' };
      const accessToken = this.getAccessToken();
      if (accessToken) {
        headers['Authorization'] = `Bearer ${accessToken}`;
      } else if (this.credentials) {
        headers['Authorization'] = `Basic ${this.credentials}`;
      }
      return headers;
    };

    const response = await this.executeWithRefresh(() =>
      fetch(endpoint, { method: 'POST', headers: buildUploadHeaders(), body: formData })
    );

    if (!response.ok) { const error = await response.text(); throw new Error(error || `HTTP ${response.status}`); }
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
    if (from) sp.set('from', from);
    if (to) sp.set('to', to);

    // Use tenant-level endpoint for INMA sessions, ops endpoint for SuperAdmin
    if (this.isInmaSession() && !this.isImpersonating()) {
      return this.request<AutomationSummary>(`/api/v1/dashboard/analytics/summary?${sp}`);
    }
    sp.set('tenant_id', tenantId.toString());
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

  // SuperAdmin: Message log
  async getOpsMessages(params: {
    tenantId?: number;
    phone?: string;
    direction?: string;
    from?: string;
    to?: string;
    limit?: number;
    offset?: number;
  }): Promise<{ messages: MessageLogEntry[]; total: number }> {
    const sp = new URLSearchParams();
    if (params.tenantId) sp.set('tenant_id', params.tenantId.toString());
    if (params.phone) sp.set('phone', params.phone);
    if (params.direction) sp.set('direction', params.direction);
    if (params.from) sp.set('from', params.from);
    if (params.to) sp.set('to', params.to);
    if (params.limit) sp.set('limit', params.limit.toString());
    if (params.offset) sp.set('offset', params.offset.toString());
    return this.request<{ messages: MessageLogEntry[]; total: number }>(`/api/ops/messages?${sp}`);
  }

  // SuperAdmin: Message story
  async getMessageStory(messageId: number): Promise<MessageStoryResponse> {
    return this.request<MessageStoryResponse>(`/api/ops/messages/${messageId}/story`);
  }

  // SuperAdmin: Tenant list
  async getOpsTenants(): Promise<{ tenants: TenantEntry[] }> {
    return this.request<{ tenants: TenantEntry[] }>('/api/ops/tenants');
  }

  // SuperAdmin: Impersonate tenant
  async impersonateTenant(tenantId: number): Promise<InmaAuthResponse> {
    return this.request<InmaAuthResponse>(`/api/ops/tenants/${tenantId}/impersonate`, {
      method: 'POST',
    });
  }
}

export const api = new OpsApiClient();
