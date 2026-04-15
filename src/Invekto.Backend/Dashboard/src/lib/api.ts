import { jwtDecode } from 'jwt-decode';
import type { FlowExecutionSummary, FlowExecutionDetail } from '../types/flow';

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

export interface TranslationStatsResponse {
  total_cached: number;
  active_cached: number;
  expired: number;
  tenant_count: number;
  language_count: number;
  oldest_entry: string | null;
  newest_entry: string | null;
  top_languages: { language: string; count: number }[];
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
  instanceName: string | null;
  createdAt: string;
}

export interface ChannelEntry {
  instanceId: string;
  instanceName: string;
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

export interface IntentPatternDto {
  id: number;
  tenant_id: number;
  intent_name: string;
  keywords: string[];
  confidence_avg: number | null;
  sample_count: number;
  sample_messages_json: string;
  created_at: string;
}

// Instance management types (Settings)
export interface InstanceDto {
  id: number;
  instanceId: string;
  instanceName: string;
  account: string | null;
  instanceType: number;
  isEnabled: boolean;
  flowId: number | null;
  flowName: string | null;
  fetchedAt: string;
}

// Working Hours types (Settings)
export interface WorkingHoursDto {
  configured: boolean;
  start: string;
  end: string;
  timezone: string;
  days_off: string[];
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

// Template System types (SuperAdmin)
export interface TemplateCatalogItem {
  id: number;
  template_type: string;
  scope: string;
  sector?: string;
  slug: string;
  name: string;
  description?: string;
  lang: string;
  tags: string[];
  version: number;
  is_published: boolean;
  usage_count: number;
  confidence_score: number;
  source_count: number;
  created_by: string;
  created_at: string;
}

export interface TemplateCatalogDetail extends TemplateCatalogItem {
  tenant_id?: number;
  parent_template_id?: number;
  content_json: Record<string, unknown>;
  is_active: boolean;
  updated_at: string;
  sources?: TemplateSourceItem[];
}

export interface TemplateSourceItem {
  id: number;
  template_id: number;
  analysis_id: number;
  tenant_name: string;
  contribution_type: string;
  sample_count: number;
  contributed_at: string;
}

export interface TemplateVersionItem {
  id: number;
  template_id: number;
  version: number;
  content_json: Record<string, unknown>;
  change_summary?: string;
  changed_by: string;
  created_at: string;
}

export interface TemplateSuggestionItem {
  id: number;
  analysis_id: number;
  suggestion_type: string;
  existing_template_id?: number;
  existing_template_name?: string;
  similarity_score?: number;
  suggested_content_json: Record<string, unknown>;
  suggested_slug: string;
  suggested_name: string;
  suggested_type: string;
  source_data_json?: Record<string, unknown>;
  status: string;
  created_at: string;
}

export interface TemplateCompareResult {
  analysis_id: number;
  tenant_name: string;
  new_count: number;
  update_count: number;
  confirm_count: number;
  total_clusters_processed: number;
  total_intents_processed: number;
  suggestions: TemplateSuggestionItem[];
  duration_ms: number;
}

export interface TemplateOnboardResult {
  tenant_id: number;
  sector: string;
  adopted_count: number;
  skipped_count: number;
  failed_count: number;
  duration_ms: number;
}

// Self-service template types (tenant-facing)
export interface AvailableTemplate {
  id: number;
  template_type: string;
  scope: string;
  sector?: string;
  name: string;
  description?: string;
  tags: string[];
  version: number;
  confidence_score: number;
  is_adopted: boolean;
}

export interface AdoptionRecord {
  id: number;
  template_id: number;
  template_name: string;
  template_type: string;
  adopted_version: number;
  target_type: string;
  target_id: number;
  adopted_at: string;
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

// FlowBuilder API types
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

export interface AssignedInstance {
  instanceId: string;
  instanceName: string;
  instanceType: number;
}

export interface FlowSummary {
  flow_id: number;
  flow_name: string;
  flow_description: string | null;
  is_active: boolean;
  is_default: boolean;
  config_version: number;
  node_count: number;
  edge_count: number;
  created_at: string;
  updated_at: string;
  health_score: number | null;
  health_issues: string[] | null;
  assigned_instances: AssignedInstance[];
}

export interface FlowDetail {
  flow_id: number;
  tenant_id: number;
  flow_name: string;
  flow_config: unknown;
  is_active: boolean;
  is_default: boolean;
  created_at: string;
  updated_at: string;
  wizard_history?: unknown[] | null;
  wizard_status?: 'drafting' | 'completed' | null;
  current_version?: number;
}

export interface FlowVersionSummary {
  id: number;
  flowId: number;
  versionNumber: number;
  createdAt: string;
  createdBy: string | null;
}

export interface FbAvailableInstance {
  instanceId: string;
  instanceName: string;
  instanceType: number;
  account: string | null;
  assignedFlowId?: number | null;
  assignedFlowName?: string | null;
}

export interface FbWorkingHoursInfo {
  configured: boolean;
  start?: string;
  end?: string;
  timezone?: string;
  days_off?: string[];
}

export interface FbValidationResult {
  is_valid: boolean;
  errors: string[];
  warnings: string[];
}

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

// Onboarding status (PKT-2)
export interface OnboardingStepResponse {
  key: string;
  completed: boolean;
  detail: string | null;
}

export interface OnboardingNextStepResponse {
  key: string;
  action_url: string;
  hint: string;
}

export interface OnboardingStatusResponse {
  tenant_id: number;
  sector: string | null;
  progress_pct: number;
  steps: OnboardingStepResponse[];
  next_step: OnboardingNextStepResponse | null;
}

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
  }

  removeTokens(): void {
    localStorage.removeItem(TOKEN_KEYS.ACCESS_TOKEN);
    localStorage.removeItem(TOKEN_KEYS.REFRESH_TOKEN);
    sessionStorage.removeItem('inse_session');
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
   * Exchanges raw INMA JWT for an INSE JWT and replaces the primary token.
   * FlowBuilder backend validates with INSE JwtValidator, so INMA JWTs fail 401.
   * This method is fire-and-forget ΓÇö called after URL token SSO flow.
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

      // Replace primary token with INSE JWT so all API calls (including
      // FlowBuilder endpoints protected by JwtAuthMiddleware) work correctly.
      this.storeTokens(data.token, data.refresh_token ?? '');
    } catch (err) {
      console.warn('[api] INMA token exchange failed:', err);
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
    // Welcome endpoint may return plain text ΓÇö bypass default JSON parse
    const response = await this.executeWithRefresh(() =>
      fetch('/api/v1/inma/welcome', { headers: this.buildHeaders() })
    );
    if (!response.ok) return null;
    const text = await response.text();
    try { return JSON.parse(text); } catch { return text; }
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

      // Refresh returns raw INMA JWT ΓÇö re-exchange to INSE JWT
      // so JwtAuthMiddleware-protected endpoints keep working.
      await this.exchangeInmaToken();

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
        // Ops/Basic Auth: refresh failed ΓåÆ session truly invalid, wipe everything.
        this.removeTokens();
        this.clearCredentials();
        throw new Error('INV-AU-001: Session expired, refresh failed');
      } else {
        // INMA session: 401 from ops-only endpoint, NOT a session problem.
        // Token validity is enforced by getDecodedToken() expiry check ΓÇö
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
      let errBody: { error_code?: string; message?: string; request_id?: string } | null = null;
      try { errBody = await response.json(); } catch (_e) { /* non-JSON error body */ }
      throw new ApiClientError(
        response.status,
        errBody?.error_code ?? 'UNKNOWN',
        errBody?.message ?? `HTTP ${response.status}`,
        errBody?.request_id,
      );
    }

    // 204 No Content (e.g. DELETE operations)
    if (response.status === 204) return undefined as T;

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

  async getTranslationStats(): Promise<TranslationStatsResponse> {
    return this.request<TranslationStatsResponse>('/api/ops/stats/translations');
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

  async indexWebsite(tenantId: number, url: string, title?: string) {
    return this.request<{ documentId: number; status: string; title: string }>(
      `/api/ops/knowledge/${tenantId}/documents/website`, {
        method: 'POST',
        body: JSON.stringify({ url, title: title || undefined }),
      });
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
    instanceId?: string;
    from?: string;
    to?: string;
    limit?: number;
    offset?: number;
  }): Promise<{ messages: MessageLogEntry[]; total: number }> {
    const sp = new URLSearchParams();
    if (params.tenantId) sp.set('tenant_id', params.tenantId.toString());
    if (params.phone) sp.set('phone', params.phone);
    if (params.direction) sp.set('direction', params.direction);
    if (params.instanceId) sp.set('instance_id', params.instanceId);
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

  // SuperAdmin: Channel list for filter dropdown
  async getOpsChannels(tenantId?: number): Promise<{ channels: ChannelEntry[] }> {
    const sp = new URLSearchParams();
    if (tenantId) sp.set('tenant_id', tenantId.toString());
    return this.request<{ channels: ChannelEntry[] }>(`/api/ops/channels?${sp}`);
  }

  // SuperAdmin: Impersonate tenant
  async impersonateTenant(tenantId: number): Promise<InmaAuthResponse> {
    return this.request<InmaAuthResponse>(`/api/ops/tenants/${tenantId}/impersonate`, {
      method: 'POST',
    });
  }

  // SuperAdmin: Tenant license info (Invekto PG + INMA MSSQL readonly)
  async getTenantLicense(tenantId: number): Promise<TenantLicenseInfo> {
    return this.request<TenantLicenseInfo>(`/api/ops/tenants/${tenantId}/license`);
  }

  // SuperAdmin: Update tenant plan tier / feature overrides
  async updateTenantPlan(
    tenantId: number,
    data: { plan_tier?: string; features_json?: Record<string, unknown> | null }
  ): Promise<{ tenant_id: number; updated: boolean }> {
    return this.request<{ tenant_id: number; updated: boolean }>(`/api/ops/tenants/${tenantId}/plan`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  // SuperAdmin: List all plan definitions
  async getPlans(): Promise<{ plans: PlanDefinition[] }> {
    return this.request<{ plans: PlanDefinition[] }>('/api/ops/plans');
  }

  async initiatePayment(request: PaymentInitRequest): Promise<PaymentInitResult> {
    return this.request<PaymentInitResult>('/api/v1/payment/initiate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    });
  }

  // Instance management (Settings page)
  async getInstances(): Promise<{ instances: InstanceDto[] }> {
    return this.request<{ instances: InstanceDto[] }>('/api/v1/settings/instances');
  }

  async refreshInstances(): Promise<{ instances: InstanceDto[]; refreshed: boolean }> {
    return this.request<{ instances: InstanceDto[]; refreshed: boolean }>('/api/v1/settings/instances/refresh', {
      method: 'POST',
    });
  }

  async toggleInstance(instanceId: string, enabled: boolean): Promise<{ instance_id: string; is_enabled: boolean }> {
    return this.request<{ instance_id: string; is_enabled: boolean }>(`/api/v1/settings/instances/${instanceId}/toggle`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ is_enabled: enabled }),
    });
  }

  // Working Hours settings
  async getWorkingHours(): Promise<WorkingHoursDto> {
    return this.request<WorkingHoursDto>('/api/v1/settings/working-hours');
  }

  async updateWorkingHours(data: { start: string; end: string; timezone: string; days_off: string[] }): Promise<{ success: boolean; working_hours: WorkingHoursDto }> {
    return this.request<{ success: boolean; working_hours: WorkingHoursDto }>('/api/v1/settings/working-hours', {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  // Sector settings
  async getSector(): Promise<{ sector: string | null }> {
    return this.request<{ sector: string | null }>('/api/v1/settings/sector');
  }

  async updateSector(sector: string): Promise<{ success: boolean; sector: string }> {
    return this.request<{ success: boolean; sector: string }>('/api/v1/settings/sector', {
      method: 'PUT',
      body: JSON.stringify({ sector }),
    });
  }

  // Self-service template adoption (tenant-facing)
  async getAvailableTemplates(): Promise<{ items: AvailableTemplate[] }> {
    return this.request<{ items: AvailableTemplate[] }>('/api/v1/templates/available');
  }

  async adoptTemplate(templateId: number): Promise<{ adopted: boolean; target_type: string; target_id: number }> {
    return this.request<{ adopted: boolean; target_type: string; target_id: number }>(
      `/api/v1/templates/adopt/${templateId}`, { method: 'POST' });
  }

  async getMyAdoptions(): Promise<{ items: AdoptionRecord[] }> {
    return this.request<{ items: AdoptionRecord[] }>('/api/v1/templates/adoptions');
  }

  // --- FlowBuilder API methods ---

  async listFlows(tenantId: number): Promise<FlowSummary[]> {
    return this.request<FlowSummary[]>(`/api/v1/flow-builder/flows/${tenantId}`);
  }

  async getFlow(tenantId: number, flowId: number): Promise<FlowDetail> {
    return this.request<FlowDetail>(`/api/v1/flow-builder/flows/${tenantId}/${flowId}`);
  }

  async createFlow(tenantId: number, body: { flow_name: string; flow_config: unknown }): Promise<FlowDetail> {
    return this.request<FlowDetail>(`/api/v1/flow-builder/flows/${tenantId}`, {
      method: 'POST', body: JSON.stringify(body),
    });
  }

  async updateFlow(tenantId: number, flowId: number, body: { flow_name?: string; flow_config?: unknown }): Promise<FlowDetail> {
    return this.request<FlowDetail>(`/api/v1/flow-builder/flows/${tenantId}/${flowId}`, {
      method: 'PUT', body: JSON.stringify(body),
    });
  }

  async deleteFlow(tenantId: number, flowId: number): Promise<void> {
    return this.request<void>(`/api/v1/flow-builder/flows/${tenantId}/${flowId}`, { method: 'DELETE' });
  }

  async activateFlow(tenantId: number, flowId: number): Promise<void> {
    return this.request<void>(`/api/v1/flow-builder/flows/${tenantId}/${flowId}/activate`, { method: 'POST' });
  }

  async deactivateFlow(tenantId: number, flowId: number): Promise<void> {
    return this.request<void>(`/api/v1/flow-builder/flows/${tenantId}/${flowId}/deactivate`, { method: 'POST' });
  }

  async getFlowVersions(tenantId: number, flowId: number): Promise<{ flow_id: number; versions: FlowVersionSummary[] }> {
    return this.request(`/api/v1/flow-builder/flows/${tenantId}/${flowId}/versions`);
  }

  async getFlowVersion(tenantId: number, flowId: number, versionNumber: number): Promise<{ flow_config: unknown; versionNumber: number; createdAt: string; createdBy: string | null }> {
    return this.request(`/api/v1/flow-builder/flows/${tenantId}/${flowId}/versions/${versionNumber}`);
  }

  async rollbackFlowVersion(tenantId: number, flowId: number, versionNumber: number): Promise<{ current_version: number; status: string }> {
    return this.request(`/api/v1/flow-builder/flows/${tenantId}/${flowId}/versions/${versionNumber}/rollback`, { method: 'POST' });
  }

  async validateFlow(flowConfig: unknown): Promise<FbValidationResult> {
    return this.request<FbValidationResult>('/api/v1/flow-builder/flows/validate', {
      method: 'POST', body: JSON.stringify({ flow_config: flowConfig }),
    });
  }

  async simulationStart(tenantId: number, flowId: number): Promise<SimulationStartResponse> {
    return this.request<SimulationStartResponse>('/api/v1/flow-builder/simulation/start', {
      method: 'POST', body: JSON.stringify({ tenant_id: tenantId, flow_id: flowId }),
    });
  }

  async simulationStep(sessionId: string, message: string): Promise<SimulationStepResponse> {
    return this.request<SimulationStepResponse>('/api/v1/flow-builder/simulation/step', {
      method: 'POST', body: JSON.stringify({ session_id: sessionId, message }),
    });
  }

  async simulationCleanup(sessionId: string): Promise<void> {
    return this.request<void>(`/api/v1/flow-builder/simulation/${sessionId}`, { method: 'DELETE' });
  }

  // --- Flow Execution Logs ---

  async getFlowExecutions(tenantId: number, flowId: number, params?: {
    limit?: number; offset?: number;
  }): Promise<{ items: FlowExecutionSummary[]; total: number }> {
    const sp = new URLSearchParams();
    if (params?.limit) sp.set('limit', params.limit.toString());
    if (params?.offset) sp.set('offset', params.offset.toString());
    return this.request<{ items: FlowExecutionSummary[]; total: number }>(
      `/api/v1/flow-builder/flows/${tenantId}/${flowId}/executions?${sp}`);
  }

  async getFlowExecution(tenantId: number, flowId: number, logId: number): Promise<FlowExecutionDetail> {
    return this.request<FlowExecutionDetail>(
      `/api/v1/flow-builder/flows/${tenantId}/${flowId}/executions/${logId}`);
  }

  // --- Flow Monitor ---

  async getMonitorExecutions(tenantId: number, params?: {
    limit?: number; offset?: number; flow_id?: number; status?: string;
    date_from?: string; date_to?: string; phone?: string;
  }): Promise<{ items: import('../types/flow').MonitorExecutionSummary[]; total: number }> {
    const sp = new URLSearchParams();
    if (params?.limit) sp.set('limit', params.limit.toString());
    if (params?.offset) sp.set('offset', params.offset.toString());
    if (params?.flow_id) sp.set('flow_id', params.flow_id.toString());
    if (params?.status) sp.set('status', params.status);
    if (params?.date_from) sp.set('date_from', params.date_from);
    if (params?.date_to) sp.set('date_to', params.date_to);
    if (params?.phone) sp.set('phone', params.phone);
    return this.request(`/api/v1/flow-builder/monitor/${tenantId}/executions?${sp}`);
  }

  async getFlowBuilderInstances(flowId?: number): Promise<{ instances: FbAvailableInstance[] }> {
    const params = flowId ? `?flow_id=${flowId}` : '';
    return this.request<{ instances: FbAvailableInstance[] }>(`/api/v1/flow-builder/instances/available${params}`);
  }

  async getFlowBuilderWorkingHours(): Promise<FbWorkingHoursInfo> {
    return this.request<FbWorkingHoursInfo>('/api/v1/flow-builder/tenant/working-hours');
  }

  // --- Template System (SuperAdmin) ---

  async getTemplateCatalog(params?: {
    type?: string; scope?: string; search?: string; page?: number; limit?: number;
  }): Promise<{ items: TemplateCatalogItem[]; total: number }> {
    const sp = new URLSearchParams();
    if (params?.type) sp.set('type', params.type);
    if (params?.scope) sp.set('scope', params.scope);
    if (params?.search) sp.set('search', params.search);
    if (params?.page) sp.set('page', params.page.toString());
    if (params?.limit) sp.set('limit', (params.limit ?? 20).toString());
    return this.request<{ items: TemplateCatalogItem[]; total: number }>(
      `/api/ops/templates/catalog?${sp}`);
  }

  async getTemplateCatalogItem(id: number): Promise<TemplateCatalogDetail> {
    return this.request<TemplateCatalogDetail>(`/api/ops/templates/catalog/${id}`);
  }

  async publishTemplate(id: number): Promise<void> {
    return this.request<void>(`/api/ops/templates/catalog/${id}/publish`, { method: 'POST' });
  }

  async deleteTemplate(id: number): Promise<void> {
    return this.request<void>(`/api/ops/templates/catalog/${id}`, { method: 'DELETE' });
  }

  async getTemplateVersions(id: number): Promise<{ versions: TemplateVersionItem[] }> {
    return this.request<{ versions: TemplateVersionItem[] }>(
      `/api/ops/templates/catalog/${id}/versions`);
  }

  async getTemplateSuggestions(params?: {
    status?: string; analysisId?: number; page?: number; limit?: number;
  }): Promise<{ items: TemplateSuggestionItem[]; total: number }> {
    const sp = new URLSearchParams();
    if (params?.status) sp.set('status', params.status);
    if (params?.analysisId) sp.set('analysis_id', params.analysisId.toString());
    if (params?.page) sp.set('page', params.page.toString());
    if (params?.limit) sp.set('limit', (params.limit ?? 20).toString());
    return this.request<{ items: TemplateSuggestionItem[]; total: number }>(
      `/api/ops/templates/suggestions?${sp}`);
  }

  async extractFromAnalysis(analysisId: number, body: {
    tenant_name: string; sector?: string; auto_confirm_threshold?: number;
  }): Promise<TemplateCompareResult> {
    return this.request<TemplateCompareResult>(
      `/api/ops/templates/extract-from-analysis/${analysisId}`, {
        method: 'POST', body: JSON.stringify(body),
      });
  }

  async reviewTemplateSuggestion(id: number, body: { status: string }): Promise<void> {
    return this.request<void>(`/api/ops/templates/suggestions/${id}/review`, {
      method: 'POST', body: JSON.stringify(body),
    });
  }

  async bulkReviewSuggestions(body: { ids: number[]; status: string }): Promise<void> {
    return this.request<void>('/api/ops/templates/suggestions/bulk-review', {
      method: 'POST', body: JSON.stringify(body),
    });
  }

  async onboardTemplates(tenantId: number, body: { sector: string }): Promise<TemplateOnboardResult> {
    return this.request<TemplateOnboardResult>(
      `/api/ops/templates/${tenantId}/onboard`, {
        method: 'POST', body: JSON.stringify(body),
      });
  }

  // Intent CRUD
  async getIntents(tenantId: number): Promise<{ intents: IntentPatternDto[]; count: number }> {
    return this.request(`/api/ops/knowledge/${tenantId}/intents`);
  }

  async createIntent(tenantId: number, data: { intent_name: string; keywords: string[] }): Promise<{ id: number }> {
    return this.request(`/api/ops/knowledge/${tenantId}/intents`, {
      method: 'POST', body: JSON.stringify(data),
    });
  }

  async updateIntent(tenantId: number, intentId: number, data: { intent_name: string; keywords: string[] }): Promise<{ updated: boolean }> {
    return this.request(`/api/ops/knowledge/${tenantId}/intents/${intentId}`, {
      method: 'PUT', body: JSON.stringify(data),
    });
  }

  async deleteIntent(tenantId: number, intentId: number): Promise<{ deleted: boolean }> {
    return this.request(`/api/ops/knowledge/${tenantId}/intents/${intentId}`, {
      method: 'DELETE',
    });
  }

  // Onboarding status (PKT-2 API)
  async getOnboardingStatus(): Promise<OnboardingStatusResponse> {
    return this.request<OnboardingStatusResponse>('/api/v1/onboarding/status');
  }

  // --- Revenue Intelligence (RI-6) ---

  async getRiDashboard(tenantId: number, sector?: string, instanceId?: number): Promise<RiDashboardResponse> {
    const sp = new URLSearchParams();
    if (sector) sp.set('sector', sector);
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request<RiDashboardResponse>(`/api/v1/wa/${tenantId}/ri/dashboard${qs}`);
  }

  async getRiRevenue(tenantId: number, instanceId?: number, dimension?: string): Promise<{ requestId: string; data: RiRevenueAttribution }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    if (dimension) sp.set('dimension', dimension);
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/revenue${qs}`);
  }

  async getRiAgents(tenantId: number, instanceId?: number): Promise<{ requestId: string; data: RiAgentLeaderboard }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/agents${qs}`);
  }

  async getRiObjections(tenantId: number, instanceId?: number): Promise<{ requestId: string; data: RiObjectionMap }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/objections${qs}`);
  }

  async getRiResponseTime(tenantId: number, instanceId?: number): Promise<{ requestId: string; data: RiResponseTime }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/response-time${qs}`);
  }

  async getRiRescue(tenantId: number, instanceId?: number): Promise<{ requestId: string; data: RiRescueInsight }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/rescue${qs}`);
  }

  async getRiQuality(tenantId: number, instanceId?: number): Promise<{ requestId: string; data: RiQualityInsight }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/quality${qs}`);
  }

  async getRiDemand(tenantId: number, instanceId?: number): Promise<{ requestId: string; data: RiDemandHeatmap }> {
    const sp = new URLSearchParams();
    if (instanceId) sp.set('instanceId', instanceId.toString());
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request(`/api/v1/wa/${tenantId}/ri/demand${qs}`);
  }

  async getRiTemplates(tenantId: number, sector: string): Promise<RiSectorTemplates> {
    return this.request<RiSectorTemplates>(`/api/v1/wa/${tenantId}/ri/templates?sector=${encodeURIComponent(sector)}`);
  }

  async toggleRiTemplate(tenantId: number, type: string, id: number, isActive: boolean): Promise<{ success: boolean }> {
    return this.request(`/api/v1/wa/${tenantId}/ri/templates/${type}/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ isActive }),
    });
  }

  async createRiTemplate(tenantId: number, type: string, data: Record<string, unknown>): Promise<{ id: number }> {
    return this.request(`/api/v1/wa/${tenantId}/ri/templates/${type}`, {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateRiTemplate(tenantId: number, type: string, id: number, data: Record<string, unknown>): Promise<{ success: boolean }> {
    return this.request(`/api/v1/wa/${tenantId}/ri/templates/${type}/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deleteRiTemplate(tenantId: number, type: string, id: number): Promise<{ deleted: boolean }> {
    return this.request(`/api/v1/wa/${tenantId}/ri/templates/${type}/${id}`, {
      method: 'DELETE',
    });
  }

  async submitRiFeedback(tenantId: number, feedback: RiFeedbackRequest): Promise<{ success: boolean; record: RiFeedbackRecord }> {
    return this.request(`/api/v1/wa/${tenantId}/ri/feedback`, {
      method: 'POST',
      body: JSON.stringify(feedback),
    });
  }

  async getRiBenchmarks(tenantId: number, sector: string): Promise<RiSectorBenchmarks> {
    return this.request<RiSectorBenchmarks>(`/api/v1/wa/${tenantId}/ri/benchmarks?sector=${encodeURIComponent(sector)}`);
  }

  async getRiOnboarding(tenantId: number, sector?: string): Promise<RiOnboardingResponse> {
    const params = sector ? `?sector=${encodeURIComponent(sector)}` : '';
    return this.request<RiOnboardingResponse>(`/api/v1/wa/${tenantId}/ri/onboarding${params}`);
  }

  // --- Review Rescue (PKT-12 Faz 4) ---

  async getRescueStats(): Promise<RescueStatsResponse> {
    return this.request<RescueStatsResponse>('/api/v1/rescue/stats');
  }

  async listRescueRisks(riskLevel?: string, rescueStatus?: string): Promise<ReviewRiskResponse[]> {
    const sp = new URLSearchParams();
    if (riskLevel) sp.set('riskLevel', riskLevel);
    if (rescueStatus) sp.set('rescueStatus', rescueStatus);
    const qs = sp.toString() ? `?${sp}` : '';
    return this.request<ReviewRiskResponse[]>(`/api/v1/rescue/risks${qs}`);
  }

  async updateRescueRisk(id: number, data: RescueRiskUpdateRequest): Promise<{ success: boolean }> {
    return this.request(`/api/v1/rescue/risks/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async listRescueTemplates(riskLevel?: string): Promise<RescueTemplateResponse[]> {
    const sp = new URLSearchParams();
    if (riskLevel) sp.set('riskLevel', riskLevel);
    sp.set('active_only', 'false');
    const qs = `?${sp}`;
    return this.request<RescueTemplateResponse[]>(`/api/v1/rescue/templates${qs}`);
  }

  async createRescueTemplate(data: RescueTemplateCreateRequest): Promise<{ id: number }> {
    return this.request('/api/v1/rescue/templates', {
      method: 'POST',
      body: JSON.stringify(data),
    });
  }

  async updateRescueTemplate(id: number, data: RescueTemplateUpdateRequest): Promise<{ success: boolean }> {
    return this.request(`/api/v1/rescue/templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    });
  }

  async deleteRescueTemplate(id: number): Promise<{ success: boolean }> {
    return this.request(`/api/v1/rescue/templates/${id}`, {
      method: 'DELETE',
    });
  }

  // --- WebChat operator proxy (superadmin chat window) ---

  async getWebChatConversations(includeClosed = false): Promise<WebChatConversationsResponse> {
    const qs = includeClosed ? '?include_closed=true' : '';
    return this.request<WebChatConversationsResponse>(`/api/ops/webchat/conversations${qs}`);
  }

  async getWebChatMessages(conversationId: number): Promise<WebChatMessagesResponse> {
    return this.request<WebChatMessagesResponse>(`/api/ops/webchat/conversations/${conversationId}/messages`);
  }

  async sendWebChatMessage(conversationId: number, content: string): Promise<WebChatSendResult> {
    return this.request<WebChatSendResult>(`/api/ops/webchat/conversations/${conversationId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    });
  }

  async closeWebChatConversation(conversationId: number): Promise<{ status: string }> {
    return this.request<{ status: string }>(`/api/ops/webchat/conversations/${conversationId}/close`, {
      method: 'PUT',
    });
  }

  async routeWebChatToAi(conversationId: number): Promise<{ status: string }> {
    return this.request<{ status: string }>(`/api/ops/webchat/conversations/${conversationId}/route-ai`, {
      method: 'PUT',
    });
  }

  async getWebChatVisitor(conversationId: number): Promise<WebChatVisitor> {
    return this.request<WebChatVisitor>(`/api/ops/webchat/conversations/${conversationId}/visitor`);
  }

  // --- Zoho (Adim 3 P3-B2): Dashboard -> Backend proxy /api/v1/zoho/* ---
  async getZohoConnection(): Promise<ZohoConnectionStatusDto> {
    return this.request<ZohoConnectionStatusDto>('/api/v1/zoho/connection');
  }

  async createZohoConnectUrl(): Promise<ZohoConnectUrlResponse> {
    return this.request<ZohoConnectUrlResponse>('/api/v1/zoho/connect-url');
  }

  async disconnectZoho(): Promise<ZohoDisconnectResponse> {
    return this.request<ZohoDisconnectResponse>('/api/v1/zoho/connection', { method: 'DELETE' });
  }

  async getZohoStageMappings(): Promise<ZohoStageMappingListResponse> {
    return this.request<ZohoStageMappingListResponse>('/api/v1/zoho/stage-mappings');
  }

  async getZohoSyncLog(params: ZohoSyncLogQuery): Promise<ZohoSyncLogPageResponse> {
    const qs = new URLSearchParams();
    if (params.page) qs.set('page', params.page.toString());
    if (params.pageSize) qs.set('pageSize', params.pageSize.toString());
    if (params.status) qs.set('status', params.status);
    if (params.event) qs.set('event', params.event);
    if (params.from) qs.set('from', params.from);
    if (params.to) qs.set('to', params.to);
    const query = qs.toString();
    return this.request<ZohoSyncLogPageResponse>(`/api/v1/zoho/sync-log${query ? `?${query}` : ''}`);
  }

  async retryZohoSyncLog(id: number): Promise<ZohoSyncLogRetryResponse> {
    return this.request<ZohoSyncLogRetryResponse>(`/api/v1/zoho/sync-log/${id}/retry`, { method: 'POST' });
  }
}

// --- Zoho DTOs (mirror src/Invekto.Shared/Contracts/Zoho, camelCase per API serializer) ---

export interface ZohoConnectionStatusDto {
  connected: boolean;
  region?: string | null;
  zohoUserEmail?: string | null;
  connectedAt?: string | null;
  lastRefreshedAt?: string | null;
}

export interface ZohoConnectUrlResponse {
  authorizeUrl: string;
}

export interface ZohoDisconnectResponse {
  disconnected: boolean;
  tokenRevoked: boolean;
}

export interface ZohoStageMappingDto {
  zohoEvent: string;
  zohoTransitionId: string;
  zohoTransitionName?: string | null;
  updatedAt?: string | null;
}

export interface ZohoStageMappingListResponse {
  mappings: ZohoStageMappingDto[];
}

export type ZohoSyncLogStatus = 'pending' | 'failed' | 'success';

export interface ZohoSyncLogQuery {
  page?: number;
  pageSize?: number;
  status?: ZohoSyncLogStatus | '';
  event?: string;
  from?: string;
  to?: string;
}

export interface ZohoSyncLogEntryDto {
  id: number;
  zohoEvent: string;
  sourceLeadId: string;
  zohoLeadId?: string | null;
  status: ZohoSyncLogStatus;
  attemptCount: number;
  lastErrorCode?: string | null;
  lastErrorMessage?: string | null;
  updatedAt: string;
  completedAt?: string | null;
}

export interface ZohoSyncLogPageResponse {
  items: ZohoSyncLogEntryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ZohoSyncLogRetryResponse {
  retriedId: number;
  newAttemptCount: number;
}

// --- RI Types (RI-6) ---

export interface RiDashboardResponse {
  tenantId: number;
  sector: string | null;
  responseTime: RiResponseTime | null;
  agentLeaderboard: RiAgentLeaderboard | null;
  objectionMap: RiObjectionMap | null;
  rescueAlerts: RiRescueInsight | null;
  qualityScore: RiQualityInsight | null;
  demandHeatmap: RiDemandHeatmap | null;
  revenue: RiRevenueAttribution | null;
  templates: RiSectorTemplates | null;
  benchmarks: RiSectorBenchmarks | null;
}

export interface RiResponseTime {
  tenantId: number;
  instanceId: number | null;
  totalConversations: number;
  avgResponseTimeMs: number | null;
  buckets: RiResponseTimeBucket[];
}

export interface RiResponseTimeBucket {
  bucket: string;
  bucketLabel: string;
  conversationCount: number;
  percentage: number;
  saleCount: number;
  conversionRate: number;
}

export interface RiAgentLeaderboard {
  tenantId: number;
  instanceId: number | null;
  totalAgents: number;
  agents: RiAgentEntry[];
}

export interface RiAgentEntry {
  agentId: number;
  agentName: string;
  instanceId: number | null;
  totalConversations: number;
  saleCount: number;
  offeredCount: number;
  noResponseCount: number;
  offerLostCount: number;
  otherCount: number;
  conversionRate: number;
  avgResponseTimeMs: number | null;
  ghostRate: number;
  weightedScore: number;
}

export interface RiObjectionMap {
  tenantId: number;
  instanceId: number | null;
  totalObjections: number;
  objectionTypes: RiObjectionEntry[];
}

export interface RiObjectionEntry {
  objectionType: string;
  objectionLabel: string;
  count: number;
  percentage: number;
}

export interface RiRescueInsight {
  tenantId: number;
  instanceId: number | null;
  totalCandidates: number;
  candidates: RiRescueCandidate[];
}

export interface RiRescueCandidate {
  conversationId: string;
  instanceId: number | null;
  outcomeLabel: string;
  lastMessageAt: string | null;
  lastMessageFrom: string | null;
  daysSince: number;
  rescuePriorityScore: number;
  rescueStatus: string;
}

export interface RiQualityInsight {
  tenantId: number;
  instanceId: number | null;
  totalScored: number;
  avgOverallScore: number;
  scores: RiQualityEntry[];
}

export interface RiQualityEntry {
  conversationId: string;
  agentId: number | null;
  agentName: string | null;
  responseSpeedScore: number;
  engagementScore: number;
  resolutionScore: number;
  sentimentScore: number;
  overallScore: number;
}

export interface RiDemandHeatmap {
  tenantId: number;
  instanceId: number | null;
  totalConversations: number;
  cells: RiDemandCell[];
}

export interface RiDemandCell {
  dayOfWeek: number;
  dayLabel: string;
  hourOfDay: number;
  totalConversations: number;
  saleCount: number;
  conversionRate: number;
  avgResponseTimeMs: number | null;
}

export interface RiRevenueAttribution {
  tenantId: number;
  instanceId: number | null;
  totalRevenue: number;
  totalConversations: number;
  entries: RiRevenueEntry[];
}

export interface RiRevenueEntry {
  dimension: string;
  dimensionKey: string;
  dimensionLabel: string | null;
  totalConversations: number;
  attributedRevenue: number;
  avgRevenue: number;
  breakdown: unknown;
}

export interface RiSectorTemplates {
  sector: string;
  intents: RiTemplateItem[];
  faqs: RiTemplateItem[];
  flows: RiTemplateItem[];
  objectionHandlers: RiTemplateItem[];
  followupTemplates: RiTemplateItem[];
  onboardingSteps: RiTemplateItem[];
}

export interface RiTemplateItem {
  id: number;
  sector: string;
  [key: string]: unknown;
}

export interface RiSectorBenchmarks {
  sector: string;
  displayName: string;
  benchmarkF1: number | null;
  totalTemplates: number;
  intentCount: number;
  faqCount: number;
  flowCount: number;
}

export interface RiFeedbackRequest {
  conversationId: string;
  originalLabel: string;
  isAgree: boolean;
  correctedLabel?: string;
}

export interface RiFeedbackRecord {
  id: number;
  tenantId: number;
  conversationId: string;
  originalLabel: string;
  isAgree: boolean;
  correctedLabel: string | null;
  userId: number | null;
  createdAt: string;
}

// --- RI Onboarding Types (RI-7) ---

export interface RiOnboardingResponse {
  tenantId: number;
  sector: string | null;
  sectorDisplayName: string | null;
  checklist: RiOnboardingChecklistItem[];
  quickStart: RiQuickStartItem[];
  overview: RiSectorOverview | null;
  comparison: RiTenantBenchmarkComparison | null;
}

export interface RiOnboardingChecklistItem {
  stepNumber: number;
  action: string;
  description: string | null;
  expectedImpact: string | null;
  dayRange: string | null;
  isCompleted: boolean;
}

export interface RiQuickStartItem {
  type: string;
  title: string;
  description: string | null;
  actionUrl: string | null;
  impactLabel: string | null;
}

export interface RiSectorOverview {
  intentCount: number;
  faqCount: number;
  flowCount: number;
  objectionCount: number;
  followupCount: number;
  totalTemplates: number;
  benchmarkF1: number | null;
}

export interface RiTenantBenchmarkComparison {
  tenantAvgResponseMin: number | null;
  sectorAvgResponseMin: number | null;
  tenantConversionRate: number | null;
  sectorConversionRate: number | null;
  tenantActiveAgents: number | null;
  tenantAvgQualityScore: number | null;
  recommendation: string | null;
}

// --- Review Rescue Types (PKT-12 Faz 4) ---

export interface RescueStatsResponse {
  total: number;
  pending: number;
  inProgress: number;
  rescued: number;
  failed: number;
  criticalCount: number;
  highCount: number;
  reviewsPosted: number;
  avgReviewRating: number;
  totalRescueCost: number;
  rescueRate: number;
}

export interface ReviewRiskResponse {
  id: number;
  tenantId: number;
  customerPhone: string;
  conversationId: string | null;
  riskScore: number;
  riskLevel: string;
  triggerReason: string | null;
  rescueStatus: string;
  rescueStrategy: string | null;
  rescueCost: number | null;
  customerResponse: string | null;
  reviewPosted: boolean;
  reviewRating: number | null;
  createdAt: string;
  resolvedAt: string | null;
  updatedAt: string;
}

export interface RescueRiskUpdateRequest {
  rescueStatus?: string;
  rescueStrategy?: string;
  rescueCost?: number;
  customerResponse?: string;
  reviewPosted?: boolean;
  reviewRating?: number;
}

export interface RescueTemplateResponse {
  id: number;
  tenantId: number;
  templateName: string;
  riskLevel: string;
  strategy: string;
  messageTemplate: string;
  maxDiscountPct: number | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface RescueTemplateCreateRequest {
  templateName: string;
  riskLevel: string;
  strategy: string;
  messageTemplate: string;
  maxDiscountPct?: number;
}

export interface RescueTemplateUpdateRequest {
  templateName?: string;
  messageTemplate?: string;
  maxDiscountPct?: number;
  isActive?: boolean;
}

// --- WebChat Types ---

export interface WebChatConversation {
  id: number;
  visitor_id: string;
  visitor_name: string | null;
  visitor_email: string | null;
  status: 'active' | 'ai' | 'closed';
  started_at: string;
  last_message_at: string | null;
  last_message: {
    sender_type: string;
    content: string;
    created_at: string;
  } | null;
}

export interface WebChatConversationsResponse {
  conversations: WebChatConversation[];
}

export interface WebChatMessage {
  id: number;
  conversation_id: number;
  sender_type: 'visitor' | 'operator' | 'ai';
  content: string;
  created_at: string;
}

export interface WebChatMessagesResponse {
  messages: WebChatMessage[];
}

export interface WebChatSendResult {
  message: WebChatMessage;
}

export interface WebChatVisitor {
  visitor_id: string;
  name: string | null;
  email: string | null;
  first_seen: string;
  last_seen: string;
  page_url: string | null;
  user_agent: string | null;
}

// ---- Licensing types ----

export interface PlanDefinition {
  tier_name: string;
  display_name: string;
  features_json: Record<string, string[]>;
  quotas_json: {
    messages_per_month: number;
    max_users: number;
    max_flows: number;
  };
  is_active: boolean;
}

export interface InmaLicenseHistory {
  start: string | null;
  end: string | null;
  is_paid: boolean;
  change_type: number;
}

export interface InmaLicenseInfo {
  company_id: number;
  license_type: string | null;
  license_type_price: number | null;
  license_expire_date: string | null;
  is_expired: boolean;
  days_until_expiry: number | null;
  user_license_count: number | null;
  instance_license_count: number | null;
  license_feature: string | null;
  license_renewal_period: number | null;
  message_limit_daily: number | null;
  license_progress_type: number | null;
  histories: InmaLicenseHistory[];
}

export interface InvektoLicenseInfo {
  plan_tier: string;
  tier_display_name: string;
  has_override: boolean;
  effective_features: Record<string, unknown>;
  quotas: {
    messages_per_month: number;
    max_users: number;
    max_flows: number;
  };
  usage: {
    messages_sent: number;
    period_month: string;
  };
}

export interface TenantLicenseInfo {
  tenant_id: number;
  invekto: InvektoLicenseInfo;
  inma: InmaLicenseInfo | null;
}

export interface PaymentInitRequest {
  amount: number;
  card_number: string;
  card_expire_month: string;
  card_expire_year: string;
  cvv: string;
  card_holder_name: string;
}

export interface PaymentInitResult {
  order_id: string;
  redirect_html: string;
}

export const api = new OpsApiClient();
