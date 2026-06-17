import { jwtDecode } from 'jwt-decode';
import type { FlowExecutionSummary, FlowExecutionDetail } from '../types/flow';
import type {
  TenantLandingSettingsDto as LiwSettingsDto,
  RotateApiKeyResponse as LiwRotateResponse,
  UpdateFieldMapRequest as LiwFieldMapRequest,
  UpdateFieldMapResponse as LiwFieldMapResponse,
  DryRunRequest as LiwDryRunRequest,
  DryRunResponse as LiwDryRunResponse,
  AuditListResponse as LiwAuditListResponse,
} from '../types/leadIntake';
import type {
  TenantFieldMappingGetResponse,
  TenantFieldMappingPutRequest,
  TenantFieldMappingPutResponse,
} from '../types/tenantFieldMapping';
import type {
  FollowupSequenceConfig,
  FollowupSequenceListResponse,
  FollowupSequencePutResponse,
  FollowupRunsResponse,
} from '../types/followupSequence';
import type {
  CampaignConfigGetResponse,
  CampaignConfigPutRequest,
  CampaignConfigPutResponse,
} from '../types/campaignConfig';
import type {
  ClinicMetadataGetResponse,
  ClinicMetadataPutRequest,
  ClinicMetadataPutResponse,
} from '../types/clinicMetadata';

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

// FEAT-PILOT-KANBAN: SuperAdmin pilot tracking board (Migration 035).
// Read-only Dashboard surface; mutation tek path /wrap workflow Step 3.5.
export type KanbanStatusValue = 'BLOCKED' | 'TODO' | 'IN_PROGRESS' | 'BACKLOG' | 'DONE';
export type KanbanCategoryValue = 'CUSTOMER' | 'OPS' | 'DEV' | 'DECISION' | 'UI' | 'DOC';
export type KanbanPriorityValue = 'P0' | 'P1' | 'P2' | 'P3';

export interface KanbanCard {
  id: number;
  board_key: string;
  card_slug: string;
  ref_code: string;               // 4-karakter mnemonic (C001/K005/D021) veya '----' placeholder
  depends_on: string | null;      // CSV ref_code listesi (örn "C001,C003") veya null (bağımsız)
  tenant_id: number | null;       // null = platform-level board (e.g. 'dent-pilot' SuperAdmin only)
  status: KanbanStatusValue;
  category: KanbanCategoryValue;
  priority: KanbanPriorityValue;
  position: number;
  title: string;
  summary: string | null;
  body_markdown: string | null;
  owner: string | null;
  eta: string | null;
  source_file: string | null;
  source_anchor: string | null;
  created_at: string;
  updated_at: string;
  completed_at: string | null;
}

export interface KanbanBoard {
  board_key: string;
  cards: KanbanCard[];
  updated_at: string;
}

export interface KanbanPatchRequest {
  status: KanbanStatusValue;
  completed_at?: string | null;
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
  // WapCRM connectionType ('WABA' | 'QR Code' | 'SMS' | ...). null on rows cached before migration 063.
  connectionType: string | null;
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
  // FEAT-WTP: operational rotation group label (free-text VARCHAR(50), nullable).
  group_tag?: string | null;
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

// 2026-04-18: Bulk create types (Dashboard bulk import + pilot seed support)
export interface TemplateCreateInput {
  template_type: string;
  scope: string;
  sector?: string | null;
  tenant_id?: number | null;
  parent_template_id?: number | null;
  slug: string;
  name: string;
  description?: string | null;
  lang: string;
  tags?: string[];
  group_tag?: string | null;
  content_json: Record<string, unknown>;
  created_by?: string;
}

export interface TemplateBulkCreateRequest {
  templates: TemplateCreateInput[];
}

export interface TemplateBulkCreateItemFailure {
  index: number;
  slug: string | null;
  error_code: string;
  error_message: string;
}

export interface TemplateBulkCreateResult {
  total: number;
  succeeded_count: number;
  failed_count: number;
  succeeded: TemplateCatalogDetail[];
  failed: TemplateBulkCreateItemFailure[];
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
  // INSE JWT isim claim'i tasimaz — login/exchange yanitindaki full_name burada
  // kimlige bagli ({name, tid, uid}) saklanir; token degisiminde bayat isim sizamaz.
  FULL_NAME: 'inse_full_name',
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

// FEAT-INMA-PIPELINE-V2 C3b: cxapi customer-feature-groups catalog (trimmed Backend projection).
// Powers the customer_status_changed trigger's feature_group_id picker.
export interface CustomerFeatureOption {
  id: number;
  name: string;
}

export interface CustomerFeatureGroup {
  id: number;
  name: string;
  selectionMode: number; // 1 = multi (çoklu), 2 = single (tek), 3 = text (metin)
  features: CustomerFeatureOption[];
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
  // Dedupes concurrent INMA→INSE exchange attempts so parallel 401 retries
  // fire only one /api/v1/inma/auth/exchange round-trip.
  private inmaExchangePromise: Promise<void> | null = null;
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
    localStorage.removeItem(TOKEN_KEYS.FULL_NAME);
    sessionStorage.removeItem('inse_session');
  }

  // --- Full name persistence (INSE JWT isim claim'i tasimaz) ---

  private storeFullName(fullName: string, tenantId: number, userId: number): void {
    if (!fullName) {
      localStorage.removeItem(TOKEN_KEYS.FULL_NAME);
      return;
    }
    localStorage.setItem(
      TOKEN_KEYS.FULL_NAME,
      JSON.stringify({ name: fullName, tid: tenantId, uid: userId })
    );
  }

  private getStoredFullName(tenantId: number, userId: number): string {
    const raw = localStorage.getItem(TOKEN_KEYS.FULL_NAME);
    if (!raw) return '';
    try {
      const parsed = JSON.parse(raw) as { name?: unknown; tid?: unknown; uid?: unknown };
      // Kimlik eslesmiyorsa isim kullanilmaz — farkli token'a bayat isim gosterilmez.
      return parsed.tid === tenantId && parsed.uid === userId && typeof parsed.name === 'string'
        ? parsed.name
        : '';
    } catch (err) {
      // Bozuk kayit: logla + temizle ki her getSession cagrisinda parse denenmesin.
      console.warn('[api] inse_full_name parse failed, clearing stored name:', err);
      localStorage.removeItem(TOKEN_KEYS.FULL_NAME);
      return '';
    }
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
    // Isim token'dan ONCE yazilir — token swap aninda getSession dogru ismi bulur.
    this.storeFullName(resp.full_name ?? '', resp.tenant_id, resp.user_id);
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

    // INSE JWT (exchange/mock/quicklogin): use inse claim names
    const inseTenantId = parseInt(decoded.tenant_id ?? '0') || 0;
    const inseUserId = parseInt(decoded.user_id ?? '0') || 0;
    // Saklanan isim yalniz INMA kaynakli tokenlarda ve ayni kimlikte kullanilir;
    // ops source etiketleri her zaman oncelikli (mevcut davranis korunur).
    const storedName = decoded.source === 'inma_exchange' || decoded.source === 'inma_mock'
      ? this.getStoredFullName(inseTenantId, inseUserId)
      : '';
    return {
      token,
      tenantId: inseTenantId,
      userId: inseUserId,
      role: decoded.role ?? 'agent',
      fullName: decoded.source === 'ops_quicklogin' ? 'Super Admin'
        : decoded.source === 'ops_impersonate' ? 'SuperAdmin'
        : storedName,
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
   * Concurrent callers share a single in-flight request via inmaExchangePromise.
   */
  async exchangeInmaToken(): Promise<void> {
    const token = this.getAccessToken();
    if (!token) return;

    // Only exchange INMA tokens (have CompanyCode claim, not INSE tokens)
    const decoded = this.getDecodedToken();
    if (!decoded?.CompanyCode) return;

    if (this.inmaExchangePromise) return this.inmaExchangePromise;

    this.inmaExchangePromise = this.runInmaExchange(token);
    try {
      await this.inmaExchangePromise;
    } finally {
      this.inmaExchangePromise = null;
    }
  }

  private async runInmaExchange(token: string): Promise<void> {
    // Throws on HTTP error OR network failure. Callers (inmaBootstrap, useAuth,
    // doRefresh, executeWithRefresh) each handle rejection explicitly — silent
    // swallow previously masked 401s as "SUCCESS" and left a stale raw INMA JWT
    // in localStorage, producing a bogus authenticated session downstream.
    const response = await fetch('/api/v1/inma/auth/exchange', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token }),
    });

    if (!response.ok) {
      console.warn('[api] INMA token exchange failed:', response.status);
      throw new Error(`INMA token exchange failed: HTTP ${response.status}`);
    }

    const data: InmaAuthResponse = await response.json();

    // Replace primary token with INSE JWT so all API calls (including
    // FlowBuilder endpoints protected by JwtAuthMiddleware) work correctly.
    // Isim once: INSE JWT isim claim'i tasimadigindan exchange yanitindaki
    // full_name kimlige bagli saklanir (refresh dongusu de buradan gecer).
    this.storeFullName(data.full_name ?? '', data.tenant_id, data.user_id);
    this.storeTokens(data.token, data.refresh_token ?? '');
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
    if (response.status !== 401) return response;

    // Attempt 1: refresh_token rotation (ops sessions + INMA sessions with refresh).
    if (this.getRefreshToken() && await this.handleRefresh()) {
      response = await doFetch();
      if (response.status !== 401) return response;
    }

    // Attempt 2: raw INMA JWT → INSE JWT exchange. URL SSO path stores the raw
    // INMA token synchronously then fires exchangeInmaToken() fire-and-forget,
    // so a request issued before the exchange resolves hits backend JwtValidator
    // with the wrong issuer → 401. After a successful exchange the CompanyCode
    // claim is gone, so this branch is a single-shot retry.
    if (this.isInmaSession() && this.getDecodedToken()?.CompanyCode) {
      try {
        await this.exchangeInmaToken();
      } catch (err) {
        // Preserve fall-through to INV-AU-001/002 classification below.
        // Bootstrap/useAuth handle their own exchange rejection paths;
        // this interceptor only mediates the retry window.
        console.warn('[api] executeWithRefresh: exchange retry failed, falling through', err);
      }
      if (!this.getDecodedToken()?.CompanyCode) {
        response = await doFetch();
        if (response.status !== 401) return response;
      }
    }

    // Still 401: classify as ops-only endpoint vs truly invalid session.
    if (!this.isInmaSession()) {
      this.removeTokens();
      this.clearCredentials();
      throw new Error('INV-AU-001: Session expired, refresh failed');
    }
    // INMA session is valid but endpoint demands ops auth (or refused post-exchange).
    throw new Error('INV-AU-002: Endpoint requires ops auth');
  }

  // --- internal request helpers ---

  private async request<T>(endpoint: string, options?: RequestInit): Promise<T> {
    const response = await this.executeWithRefresh(() =>
      fetch(endpoint, { ...options, headers: this.buildHeaders(options?.headers) })
    );

    if (!response.ok) {
      // Services serialize the envelope camelCase (ErrorResponse.ErrorCode -> errorCode); some
      // middleware (FeatureGuard) may use snake_case. Accept both so the code isn't shown as UNKNOWN.
      let errBody: { error_code?: string; errorCode?: string; message?: string; request_id?: string; requestId?: string } | null = null;
      try { errBody = await response.json(); } catch (_e) { /* non-JSON error body */ }
      throw new ApiClientError(
        response.status,
        errBody?.error_code ?? errBody?.errorCode ?? 'UNKNOWN',
        errBody?.message ?? `HTTP ${response.status}`,
        errBody?.request_id ?? errBody?.requestId,
      );
    }

    // 204 No Content (e.g. DELETE operations)
    if (response.status === 204) return undefined as T;

    return response.json();
  }

  // Blob download (FEAT-OBI Plan B export). Streams the file response; on a JSON error
  // envelope (gate/not-found/too-large) throws ApiClientError like request<T>. Returns the
  // blob + the server's filename (parsed from Content-Disposition).
  private async requestBlob(endpoint: string): Promise<{ blob: Blob; filename: string }> {
    const response = await this.executeWithRefresh(() =>
      fetch(endpoint, { headers: this.buildHeaders() })
    );

    if (!response.ok) {
      let errBody: { error_code?: string; errorCode?: string; message?: string; requestId?: string; request_id?: string } | null = null;
      try { errBody = await response.json(); } catch (_e) { /* non-JSON */ }
      throw new ApiClientError(
        response.status,
        errBody?.error_code ?? errBody?.errorCode ?? 'UNKNOWN',
        errBody?.message ?? `HTTP ${response.status}`,
        errBody?.request_id ?? errBody?.requestId,
      );
    }

    const cd = response.headers.get('Content-Disposition') ?? '';
    const match = /filename="?([^";]+)"?/i.exec(cd);
    const filename = match ? match[1] : 'export';
    const blob = await response.blob();
    return { blob, filename };
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

  // FEAT-PILOT-KANBAN: SuperAdmin pilot tracking board.
  // GET /api/ops/kanban/{board_key} — cards status+position'a gore sirali.
  async getKanbanBoard(boardKey: string): Promise<KanbanBoard> {
    return this.request<KanbanBoard>(`/api/ops/kanban/${encodeURIComponent(boardKey)}`);
  }

  // PATCH /api/ops/kanban/{board_key}/cards/{card_slug} — status update.
  // /wrap workflow Step 3.5 hibrit kanban sync bu metodu cagirir.
  async patchKanbanCard(
    boardKey: string,
    cardSlug: string,
    body: KanbanPatchRequest,
  ): Promise<KanbanCard> {
    return this.request<KanbanCard>(
      `/api/ops/kanban/${encodeURIComponent(boardKey)}/cards/${encodeURIComponent(cardSlug)}`,
      {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      },
    );
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

  // List a tenant's WhatsApp approved (HSM) templates for a specific channel (cxapi instanceID).
  // Read-only; powers the Projeler project template picker. 422 if WapCRM/instance not configured.
  async getWaTemplates(instanceId: number): Promise<{ instanceId: number; templates: WaTemplate[] }> {
    return this.request<{ instanceId: number; templates: WaTemplate[] }>(
      `/api/v1/settings/wa-templates?instanceId=${encodeURIComponent(instanceId)}`);
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

  // FEAT-INMA-PIPELINE-V2 C3b: cxapi customer-feature-groups catalog (24h cached Backend proxy).
  async getCustomerFeatureGroups(): Promise<{ data: CustomerFeatureGroup[] }> {
    return this.request<{ data: CustomerFeatureGroup[] }>('/api/v1/customer-feature-groups');
  }

  async invalidateCustomerFeatureGroupsCache(): Promise<{ invalidated: boolean }> {
    return this.request<{ invalidated: boolean }>('/api/v1/customer-feature-groups/cache-invalidate', { method: 'POST' });
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

  /** 2026-04-18: create a single template (superadmin). */
  async createTemplate(input: TemplateCreateInput): Promise<TemplateCatalogDetail> {
    return this.request<TemplateCatalogDetail>('/api/ops/templates/catalog', {
      method: 'POST',
      body: JSON.stringify(input),
    });
  }

  /**
   * 2026-04-18: bulk create templates (superadmin). Per-item partial success —
   * response.failed[] contains per-row error_code + slug for retry display.
   * Server enforces max 100 items / request.
   */
  async bulkImportTemplates(templates: TemplateCreateInput[]): Promise<TemplateBulkCreateResult> {
    return this.request<TemplateBulkCreateResult>('/api/ops/templates/catalog/bulk', {
      method: 'POST',
      body: JSON.stringify({ templates } satisfies TemplateBulkCreateRequest),
    });
  }

  /**
   * FEAT-WTP: update the operational rotation group_tag on an existing template.
   * Semantics mirror backend TemplateRepository.UpdateAsync:
   *   - non-empty string → overwrite
   *   - empty string ''  → clear (sets column to NULL)
   *   - null             → leave untouched (no-op on the column)
   * Callers that want to clear MUST send '' (not null).
   */
  async updateTemplateGroupTag(id: number, groupTag: string | null): Promise<TemplateCatalogDetail> {
    return this.request<TemplateCatalogDetail>(`/api/ops/templates/catalog/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ group_tag: groupTag }),
    });
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

  // ---- FEAT-LIW Chunk C: Tenant Landing Settings (/api/v1/tenant/landing/*) ----
  async getLeadIntakeSettings(): Promise<LiwSettingsDto> {
    return this.request<LiwSettingsDto>('/api/v1/tenant/landing/settings');
  }

  async rotateLeadIntakeApiKey(rowVersion: string | null): Promise<LiwRotateResponse> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (rowVersion) headers['If-Match'] = rowVersion;
    return this.request<LiwRotateResponse>('/api/v1/tenant/landing/apikey/rotate', {
      method: 'POST',
      headers,
    });
  }

  async revokeLeadIntakeApiKey(rowVersion: string): Promise<void> {
    return this.request<void>('/api/v1/tenant/landing/apikey/revoke', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'If-Match': rowVersion },
    });
  }

  async updateLeadIntakeFieldMap(req: LiwFieldMapRequest): Promise<LiwFieldMapResponse> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (req.expected_row_version) headers['If-Match'] = req.expected_row_version;
    return this.request<LiwFieldMapResponse>('/api/v1/tenant/landing/fieldmap', {
      method: 'PUT',
      headers,
      body: JSON.stringify(req),
    });
  }

  async dryRunLeadIntake(req: LiwDryRunRequest): Promise<LiwDryRunResponse> {
    return this.request<LiwDryRunResponse>('/api/v1/tenant/landing/dry-run', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  async listLeadIntakeAudit(limit: number = 50): Promise<LiwAuditListResponse> {
    const qs = limit ? `?limit=${limit}` : '';
    return this.request<LiwAuditListResponse>(`/api/v1/tenant/landing/audit${qs}`);
  }

  // FEAT-DMP: INMA dynamic-fields proxy (tenant-scoped, 1h cached server-side).
  // 200 + {data:[...]} = success (empty list = tenant has no active placeholders).
  // 422 INV-OB-038 DynamicFieldsNotConfigured = tenant has no WapCRM secret configured yet.
  // 503 INV-OB-037 DynamicFieldsFetchFailed   = upstream INMA unreachable; transient retry-able.
  // These error codes are distinct in arch/errors.md so dashboards can grep each state separately.
  async getInmaDynamicFields(): Promise<{ data: InmaDynamicFieldDto[] }> {
    return this.request<{ data: InmaDynamicFieldDto[] }>('/api/v1/dynamic-fields');
  }

  async invalidateInmaDynamicFieldsCache(): Promise<{ invalidated: boolean }> {
    return this.request<{ invalidated: boolean }>('/api/v1/dynamic-fields/cache-invalidate', {
      method: 'POST',
    });
  }

  // ---- FEAT-TFM-UI: Tenant field mapping (/api/v1/tenant-settings/field-mapping) ----
  // Backend endpoint DEPLOYED 2026-04-21 (FEAT-TFM MVP + auth hotfix). Envelope shape:
  //   GET  -> { data: { tenant_id, field_mapping: Record<semantic, entry>, updated_at } }
  //   PUT  -> same shape; backend upserts the full map atomically.
  // Validation errors surface via ApiClientError:
  //   400 INV-BE-096 DUP_SLOT | INV-BE-097 RESERVED_NAME | INV-BE-098 ENUM_EMPTY | INV-BE-099 SRC_RANGE
  //   403 INV-AUTH-010 cross-tenant
  async getTenantFieldMapping(): Promise<TenantFieldMappingGetResponse> {
    return this.request<TenantFieldMappingGetResponse>('/api/v1/tenant-settings/field-mapping');
  }

  async putTenantFieldMapping(req: TenantFieldMappingPutRequest): Promise<TenantFieldMappingPutResponse> {
    return this.request<TenantFieldMappingPutResponse>('/api/v1/tenant-settings/field-mapping', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  // FEAT-EFS Drip Sequence — proxy to Marketing /api/v1/followup/* via Backend.
  // Envelope: { data: <T> } on success; ApiClientError on 4xx/5xx with bracket-format
  // INV-MK-* code surfaced through err.errorCode (matches wrapError convention used
  // by useFieldMapping / useDynamicFields).
  async listFollowupSequences(): Promise<FollowupSequenceListResponse> {
    return this.request<FollowupSequenceListResponse>('/api/v1/tenant-settings/followup-sequence');
  }

  async upsertFollowupSequence(body: FollowupSequenceConfig): Promise<FollowupSequencePutResponse> {
    return this.request<FollowupSequencePutResponse>('/api/v1/tenant-settings/followup-sequence', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
  }

  async listFollowupRuns(): Promise<FollowupRunsResponse> {
    return this.request<FollowupRunsResponse>('/api/v1/tenant/followup/runs');
  }

  // ---- FEAT-MCC: Multi-City Campaign config (/api/v1/tenant-settings/campaign-config) ----
  // Envelope shape (matches FieldMapping/Followup convention):
  //   GET -> { data: { tenant_id, campaign_config: { campaigns: [...] }, updated_at } }
  //   PUT -> same shape; backend upserts the full config atomically.
  // Validation errors surface via ApiClientError:
  //   400 INV-BE-118 invalid (slug/cap/structure) | INV-BE-120 reserved slug
  //   403 INV-AUTH-010 cross-tenant
  //   500 INV-BE-121 DB transient
  async getCampaignConfig(): Promise<CampaignConfigGetResponse> {
    return this.request<CampaignConfigGetResponse>('/api/v1/tenant-settings/campaign-config');
  }

  async putCampaignConfig(req: CampaignConfigPutRequest): Promise<CampaignConfigPutResponse> {
    return this.request<CampaignConfigPutResponse>('/api/v1/tenant-settings/campaign-config', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  // ---- FEAT-CLINIC-METADATA: clinic_contact + team_members editor ----
  // Envelope shape (matching FEAT-MCC pattern):
  //   GET -> { data: { tenant_id, clinic_contact: {...}, team_members: [...], updated_at } }
  //   PUT -> same shape; backend upserts both JSONB columns atomically.
  // Validation errors surface via ApiClientError:
  //   400 INV-BE-122 invalid JSON | INV-BE-123 phone format | INV-BE-124 URL format
  //   403 INV-AUTH-010 cross-tenant
  //   503 INV-BE-125 DB transient
  async getClinicMetadata(): Promise<ClinicMetadataGetResponse> {
    return this.request<ClinicMetadataGetResponse>('/api/v1/tenant-settings/clinic-metadata');
  }

  async putClinicMetadata(req: ClinicMetadataPutRequest): Promise<ClinicMetadataPutResponse> {
    return this.request<ClinicMetadataPutResponse>('/api/v1/tenant-settings/clinic-metadata', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  // ---- Paket B-META: Meta Leadgen webhook settings ----
  // Envelope shape:
  //   GET  -> { data: { tenant_id, webhook_url, config: { ...last4 secrets, field_id_map }, updated_at } }
  //   PUT  -> { data: { tenant_id, updated_at } } — null-means-keep on secret fields
  //   POST rotate-verify-token -> { data: { tenant_id, verify_token, updated_at } }  (fresh token returned once)
  //   GET  discover-forms      -> { data: { forms: [...] } }                          (Graph API hop)
  //   GET  events?limit=N      -> { data: MetaLeadgenEventDto[] }                     (last-N audit rows)
  // Errors surface via ApiClientError with INV-META-* bracket codes.
  async getMetaLeadgenConfig(): Promise<MetaLeadgenConfigGetResponse> {
    return this.request<MetaLeadgenConfigGetResponse>('/api/v1/tenant-settings/meta-leadgen');
  }

  async putMetaLeadgenConfig(req: MetaLeadgenConfigPutRequest): Promise<MetaLeadgenConfigPutResponse> {
    return this.request<MetaLeadgenConfigPutResponse>('/api/v1/tenant-settings/meta-leadgen', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  async rotateMetaVerifyToken(): Promise<MetaLeadgenRotateTokenResponse> {
    return this.request<MetaLeadgenRotateTokenResponse>('/api/v1/tenant-settings/meta-leadgen/rotate-verify-token', {
      method: 'POST',
    });
  }

  async discoverMetaForms(): Promise<MetaLeadgenDiscoverFormsResponse> {
    return this.request<MetaLeadgenDiscoverFormsResponse>('/api/v1/tenant-settings/meta-leadgen/discover-forms');
  }

  async listMetaLeadgenEvents(limit: number = 5): Promise<MetaLeadgenEventsResponse> {
    return this.request<MetaLeadgenEventsResponse>(`/api/v1/tenant-settings/meta-leadgen/events?limit=${limit}`);
  }

  // ---- FEAT-OBI Phase 1A: Contact lists + list->bulk-send ----
  // Routes proxy /api/v1/outbound/* (tenant JWT + "Outbound" plan feature enforced by the
  // gateway; tenant_id is resolved from the signed claim by the Outbound service, never
  // sent from the client). All errors surface as ApiClientError with INV-OB-* codes.
  async listOutboundTemplates(lang?: string): Promise<{ templates: OutboundTemplateDto[] }> {
    const q = lang ? `?lang=${encodeURIComponent(lang)}` : '';
    return this.request<{ templates: OutboundTemplateDto[] }>(`/api/v1/outbound/templates${q}`);
  }

  async listDataLists(): Promise<DataListSummary[]> {
    return this.request<DataListSummary[]>('/api/v1/outbound/data-lists');
  }

  async createDataList(name: string): Promise<DataListSummary> {
    return this.request<DataListSummary>('/api/v1/outbound/data-lists', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name }),
    });
  }

  async updateDataList(id: number, data: { name?: string; active?: boolean }): Promise<DataListSummary> {
    return this.request<DataListSummary>(`/api/v1/outbound/data-lists/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
  }

  async deleteDataList(id: number): Promise<{ id: number; deleted: boolean }> {
    return this.request<{ id: number; deleted: boolean }>(`/api/v1/outbound/data-lists/${id}`, {
      method: 'DELETE',
    });
  }

  async dataListRecordsExist(phones: string[], listId?: number): Promise<RecordsExistsResponse> {
    return this.request<RecordsExistsResponse>('/api/v1/outbound/data-lists/records/exists', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phones, list_id: listId ?? null }),
    });
  }

  async importDataList(req: ImportBatchRequest): Promise<ImportBatchResponse> {
    return this.request<ImportBatchResponse>('/api/v1/outbound/data-lists/import', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  // FEAT-PROJELER PKT-14 — read-only list insights.
  // Projeler modal: sample recipient + deduplicated reach + per-column fill stats in ONE call.
  async getDataListPreviewSample(listIds: number[]): Promise<DataListPreviewSample> {
    return this.request<DataListPreviewSample>(
      `/api/v1/outbound/data-lists/preview-sample?listIds=${encodeURIComponent(listIds.join(','))}`);
  }

  // Veri Yönetimi viewer popup: one server-paged, searchable records page of a list.
  async getDataListRecords(
    listId: number, opts: { search?: string; page?: number; pageSize?: number } = {},
  ): Promise<ListRecordsPage> {
    const p = new URLSearchParams();
    if (opts.search) p.set('search', opts.search);
    p.set('page', String(opts.page ?? 1));
    p.set('pageSize', String(opts.pageSize ?? 50));
    return this.request<ListRecordsPage>(`/api/v1/outbound/data-lists/${listId}/records?${p.toString()}`);
  }

  // Viewer popup single-record mutations: manual add (invalid phone + duplicate rejected
  // server-side) and permanent delete (2-stage confirm lives in the UI).
  async addDataListRecord(listId: number, payload: AddListRecordPayload): Promise<AddListRecordResponse> {
    return this.request<AddListRecordResponse>(`/api/v1/outbound/data-lists/${listId}/records`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
  }

  async deleteDataListRecord(listId: number, recordId: number): Promise<DeleteListRecordResponse> {
    return this.request<DeleteListRecordResponse>(
      `/api/v1/outbound/data-lists/${listId}/records/${recordId}`, { method: 'DELETE' });
  }

  async previewFromList(req: PreviewFromListRequest): Promise<BulkSendPreviewResponse> {
    return this.request<BulkSendPreviewResponse>('/api/v1/outbound/bulk-send/preview-from-list', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  async confirmBulkSend(campaignId: string): Promise<BulkSendStatusResponse> {
    return this.request<BulkSendStatusResponse>('/api/v1/outbound/bulk-send/confirm', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ campaign_id: campaignId }),
    });
  }

  async getBulkSendStatus(campaignId: string): Promise<BulkSendStatusResponse> {
    return this.request<BulkSendStatusResponse>(`/api/v1/outbound/bulk-send/${encodeURIComponent(campaignId)}/status`);
  }

  // --- FEAT-PROJELER PKT-14 S4: Projeler (projects) CRUD ---
  // Metadata only (name + description + target data-lists). Template/instance/send land in PR-4.
  // includeArchived=true also returns archived (soft-deleted) projects; default mirrors the old behavior.
  async listProjects(includeArchived = false): Promise<ProjectSummary[]> {
    return this.request<ProjectSummary[]>(
      `/api/v1/outbound/projects${includeArchived ? '?includeArchived=true' : ''}`);
  }

  async getProject(id: number): Promise<ProjectDetail> {
    return this.request<ProjectDetail>(`/api/v1/outbound/projects/${id}`);
  }

  async createProject(req: CreateProjectRequest): Promise<ProjectSummary> {
    return this.request<ProjectSummary>('/api/v1/outbound/projects', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  async updateProject(id: number, req: UpdateProjectRequest): Promise<ProjectSummary> {
    return this.request<ProjectSummary>(`/api/v1/outbound/projects/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  // Soft-delete-as-archive (Outbound returns { id, archived:true }).
  async archiveProject(id: number): Promise<{ id: number; archived: boolean }> {
    return this.request<{ id: number; archived: boolean }>(`/api/v1/outbound/projects/${id}`, {
      method: 'DELETE',
    });
  }

  // --- FEAT-PROJELER PKT-14 SS-C: project RUN dispatch (Gönder) ---
  // A run reuses the bulk preview->confirm->status machinery. The caller generates ONE campaign_id
  // per Gönder flow and passes it to both preview and confirm (idempotency key).
  async projectSendPreview(projectId: number, campaignId: string): Promise<BulkSendPreviewResponse> {
    return this.request<BulkSendPreviewResponse>(`/api/v1/outbound/projects/${projectId}/send/preview`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ campaign_id: campaignId }),
    });
  }

  async projectSendConfirm(projectId: number, campaignId: string): Promise<BulkSendStatusResponse> {
    return this.request<BulkSendStatusResponse>(`/api/v1/outbound/projects/${projectId}/send/confirm`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ campaign_id: campaignId }),
    });
  }

  async projectSendStatus(projectId: number): Promise<ProjectDetail> {
    return this.request<ProjectDetail>(`/api/v1/outbound/projects/${projectId}/send/status`);
  }

  // SS-D run lifecycle: pause / resume / cancel. No body (project id in route); each returns the fresh detail.
  async projectSendPause(projectId: number): Promise<ProjectDetail> {
    return this.request<ProjectDetail>(`/api/v1/outbound/projects/${projectId}/send/pause`, { method: 'POST' });
  }

  async projectSendResume(projectId: number): Promise<ProjectDetail> {
    return this.request<ProjectDetail>(`/api/v1/outbound/projects/${projectId}/send/resume`, { method: 'POST' });
  }

  async projectSendCancel(projectId: number): Promise<ProjectDetail> {
    return this.request<ProjectDetail>(`/api/v1/outbound/projects/${projectId}/send/cancel`, { method: 'POST' });
  }

  // FEAT-PROJELER Rapor (delivery report) drawer.
  async getProjectReportRuns(projectId: number): Promise<ProjectRun[]> {
    return this.request<ProjectRun[]>(`/api/v1/outbound/projects/${projectId}/report/runs`);
  }

  async getProjectReportRecipients(
    projectId: number,
    opts: { campaignId?: string; search?: string; page?: number; pageSize?: number } = {},
  ): Promise<ProjectRecipientsPage> {
    const qs = new URLSearchParams();
    if (opts.campaignId) qs.set('campaignId', opts.campaignId);
    if (opts.search) qs.set('search', opts.search);
    qs.set('page', String(opts.page ?? 1));
    qs.set('pageSize', String(opts.pageSize ?? 50));
    return this.request<ProjectRecipientsPage>(`/api/v1/outbound/projects/${projectId}/report/recipients?${qs.toString()}`);
  }

  // Resend ONE undelivered (failed/ambiguous) recipient; returns the fresh project detail.
  async projectReportResend(projectId: number, messageId: number): Promise<ProjectDetail> {
    return this.request<ProjectDetail>(`/api/v1/outbound/projects/${projectId}/report/resend`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message_id: messageId }),
    });
  }

  // Resend EVERY undelivered (failed/ambiguous) recipient of the project at once; returns {requeued}.
  async projectReportResendBulk(projectId: number): Promise<ProjectResendBulkResult> {
    return this.request<ProjectResendBulkResult>(`/api/v1/outbound/projects/${projectId}/report/resend-bulk`, {
      method: 'POST',
    });
  }

  // FEATURE B: pull live cxapi message-status for the run's pending recipients (optional run scope).
  // Returns {checked, updated}. Gated server-side by the CxapiSend allowlist (403 INV-OB-094 when off).
  async projectRefreshReportStatus(projectId: number, campaignId?: string): Promise<ProjectStatusPullResult> {
    return this.request<ProjectStatusPullResult>(`/api/v1/outbound/projects/${projectId}/report/refresh-status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ campaign_id: campaignId ?? null }),
    });
  }

  // GR-8: one plain-text test message to one number, from the project modal. Dual-gated
  // server-side (Projects + BulkSend) + per-tenant throttle; not tied to a saved project.
  async projectTestSend(req: ProjectTestSendRequest): Promise<ProjectTestSendResponse> {
    return this.request<ProjectTestSendResponse>('/api/v1/outbound/projects/send/test', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    });
  }

  // Broadcast outcome poll (test-send result): the 202 only means "queued" — the worker delivers
  // seconds later; this surfaces the FINAL outcome incl. failed_reason_sample for the operator.
  async getBroadcastStatus(broadcastId: string): Promise<BroadcastStatusLite> {
    return this.request<BroadcastStatusLite>(
      `/api/v1/outbound/broadcast/${encodeURIComponent(broadcastId)}/status`);
  }

  // --- FEAT-OBI Phase 1A Plan B: Export Manager ---
  async listSendJobs(): Promise<SendJobSummary[]> {
    return this.request<SendJobSummary[]>('/api/v1/outbound/exports/send-jobs');
  }

  async downloadContactListExport(listId: number, format: 'csv' | 'xlsx'): Promise<{ blob: Blob; filename: string }> {
    return this.requestBlob(`/api/v1/outbound/exports/contact-list/${listId}?format=${format}`);
  }

  async downloadSendRecipientsExport(jobId: number, format: 'csv' | 'xlsx'): Promise<{ blob: Blob; filename: string }> {
    return this.requestBlob(`/api/v1/outbound/exports/send-job/${jobId}/recipients?format=${format}`);
  }

  async getSendReportData(jobId: number): Promise<SendReportData> {
    return this.request<SendReportData>(`/api/v1/outbound/exports/send-job/${jobId}/report-data?for=pdf`);
  }

  // --- FEAT-OBI Phase 1B: Export Manager v2 (filter-driven recipients surface) ---
  // Build the recipients filter querystring (camelCase keys; ASP.NET binds case-insensitively).
  private exportFilterQuery(f: ExportFilter): string {
    const p = new URLSearchParams();
    if (f.templateId != null) p.set('templateId', String(f.templateId));
    if (f.jobId != null) p.set('jobId', String(f.jobId));
    if (f.listId != null) p.set('listId', String(f.listId));
    if (f.status) p.set('status', f.status);
    if (f.from) p.set('from', f.from);
    if (f.to) p.set('to', f.to);
    const s = p.toString();
    return s ? `?${s}` : '';
  }

  async getExportFilterOptions(): Promise<ExportFilterOptions> {
    return this.request<ExportFilterOptions>('/api/v1/outbound/exports/filter-options');
  }

  async getExportRecipientCount(filter: ExportFilter): Promise<FilteredCount> {
    return this.request<FilteredCount>(`/api/v1/outbound/exports/recipients/count${this.exportFilterQuery(filter)}`);
  }

  async downloadFilteredExport(filter: ExportFilter, format: 'csv' | 'xlsx'): Promise<{ blob: Blob; filename: string }> {
    const q = this.exportFilterQuery(filter);
    const sep = q ? '&' : '?';
    return this.requestBlob(`/api/v1/outbound/exports/recipients${q}${sep}format=${format}`);
  }

  async createListFromExport(name: string, filter: ExportFilter): Promise<CreateListFromExportResult> {
    // POST body uses the server DTO's snake_case ExportFilter shape.
    return this.request<CreateListFromExportResult>('/api/v1/outbound/exports/recipients/create-list', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name,
        filter: {
          template_id: filter.templateId ?? null,
          job_id: filter.jobId ?? null,
          list_id: filter.listId ?? null,
          status: filter.status ?? null,
          from: filter.from ?? null,
          to: filter.to ?? null,
        },
      }),
    });
  }

  async listExportHistory(): Promise<ExportLogEntry[]> {
    return this.request<ExportLogEntry[]>('/api/v1/outbound/exports/history');
  }
}

// FEAT-OBI Phase 1A: contact-list + list->bulk-send DTOs (mirror Invekto.Shared
// DTOs/Outbound/DataListDtos.cs + BulkSendDtos.cs). Inline — the surface is one SPA page.
export type ImportScenario = 'new_only' | 'recall_all' | 'update_info' | 'custom';

export interface OutboundTemplateDto {
  id: number;
  name: string;
  trigger_event: string;
  message_template: string;
  variables_json: Record<string, string> | null;
  is_active: boolean;
  lang: string;
  created_at: string;
  updated_at: string;
}

// FEAT-PROJELER PKT-14 — read-only list insights (preview-sample + viewer records).
export interface ListRecord {
  id: number;               // list_records PK (single-record delete target)
  phone: string | null;     // normalized E.164; null = invalid row
  name: string | null;
  surname: string | null;
  email: string | null;
  field1: string | null;
  field2: string | null;
  field3: string | null;
  field4: string | null;
  field5: string | null;
  tags: string | null;
  note: string | null;
  sendable: boolean;
}
export interface ListColumnStat { filled: number; total: number; }
export interface DataListPreviewSample {
  sample: ListRecord | null;                       // first sendable record of the FIRST selected list
  reach: number;                                   // COUNT(DISTINCT phone) across selected lists (sendable)
  column_stats: Record<string, ListColumnStat>;    // keyed by column (name/surname/email/field1..5/tags/note)
}
export interface ListRecordsPage {
  records: ListRecord[];
  total: number;
  page: number;       // 1-based
  page_size: number;
}

// Single-record mutations from the list-viewer popup (manual add + permanent delete).
// Both responses carry fresh counters so the UI syncs popup header + main table
// without an extra round-trip.
export interface AddListRecordPayload {
  phone: string;
  name?: string;
  surname?: string;
  email?: string;
}
export interface AddListRecordResponse {
  record: ListRecord;
  total_records: number;
  sendable_count: number;
}
export interface DeleteListRecordResponse {
  deleted: boolean;
  total_records: number;
  sendable_count: number;
}

export interface DataListSummary {
  id: number;
  name: string;
  source: string;
  active: boolean;
  status: 'importing' | 'ready' | 'failed';
  total_records: number;
  sendable_count: number;
  created_at: string;
  updated_at: string;
}

// --- FEAT-PROJELER PKT-14 S4: Projeler (projects) ---
// Mirrors Invekto.Shared/DTOs/Outbound/ProjectDtos.cs (snake_case JSON). The counters +
// lifecycle timestamps are read-only (run-driven, populated in PR-4); S4 writes only
// name/description/target_list_ids.
export type ProjectStatus = 'draft' | 'running' | 'paused' | 'completed' | 'cancelled' | 'archived';

// GR-8 test send (mirrors ProjectTestSendRequest in ProjectDtos.cs; response = BroadcastSendResponse).
// EXACTLY ONE content source: message_text (plain) OR wa_template_id (real HSM template wire —
// wapcrm-api-integration-guide §3.2; template_params = resolved {paramKey: value} map).
export interface ProjectTestSendRequest {
  phone: string;
  message_text?: string;
  instance_id?: number;
  wa_template_id?: string;
  template_params?: Record<string, string>;
  template_header_media_url?: string;
}
export interface ProjectTestSendResponse {
  broadcast_id: string;
  total_recipients: number;
  queued: number;
  skipped_optout: number;
  skipped_consent?: number;
}
// Mirrors BroadcastStatusResponse (subset the test-send poller consumes).
export interface BroadcastStatusLite {
  broadcast_id: string;
  status: string;
  queued: number;
  sent: number;
  delivered: number;
  read: number;
  failed: number;
  failed_reason_sample?: string;
}

// Project send message kind (mirrors ProjectTemplateKinds in ProjectDtos.cs).
export type ProjectTemplateKind = 'plain_text' | 'wapcrm_template';

// plain_text content source (mirrors ProjectContentModes in ProjectDtos.cs). Applies only to
// template_kind === 'plain_text': the content is chosen in project settings (Q decision 2026-06-09).
export type ProjectContentMode = 'gallery_template' | 'free_text';

// One configured template parameter, stored in projects.param_mapping (JSONB array; opaque to the
// server — it only validates object/array + size). The send slice (PR-4) consumes it.
//   - text placeholders ({{key}} in header/body): source='column' → `column` is a list column key
//     (name/surname/email/field1..5/tags/note); PR-4 substitutes each recipient's column value.
//   - media (cxapi requiredInput kind='media'): source='literal' → `value` is a public media URL.
// `value` is optional (legacy literal-text rows + media URLs still use it).
export interface ProjectTemplateParam {
  kind: string | null;       // 'text' | 'media'
  location: string | null;   // 'BODY' | 'HEADER' | 'BUTTON'
  paramKey: string | null;
  mediaType: string | null;  // 'image' | 'video' | 'document'
  source?: 'column' | 'literal';
  column?: string | null;    // list column key when source='column'
  value?: string;            // literal value (media URL, or legacy literal text)
}

export interface ProjectSummary {
  id: number;
  name: string;
  description: string | null;
  status: ProjectStatus;
  target_count: number;
  // Total SENDABLE phone numbers across the project's target lists (SUM of data_lists.sendable_count).
  // Shown in the list view in place of the raw list count. Not cross-list deduplicated.
  recipients_total: number;
  run_count: number;
  total_targets: number;
  sent_count: number;
  delivered_count: number;
  read_count: number;
  failed_count: number;
  ambiguous_count: number;
  cancelled_count: number;
  created_at: string;
  updated_at: string;
  started_at: string | null;
  completed_at: string | null;
  // Send config (channel + template). Persisted now so edit re-populates; send execution is a later slice.
  instance_id: number | null;
  template_kind: ProjectTemplateKind | null;
  wa_template_id: string | null;
  template_language: string | null;
  param_mapping: ProjectTemplateParam[] | null;
  // plain_text content config (migration 059); re-populated in the edit modal.
  content_mode: ProjectContentMode | null;
  outbound_template_id: number | null;
  plain_text_body: string | null;
}

export interface ProjectTargetDto {
  data_list_id: number;
  list_name: string;
  total_records: number;
  sendable_count: number;
  sort_order: number;
}

export interface ProjectDetail {
  project: ProjectSummary;
  targets: ProjectTargetDto[];
}

// FEAT-PROJELER Rapor (delivery report) drawer.
export interface ProjectRun {
  campaign_id: string;
  status: string;
  created_at: string;
  total: number;
  queued: number;
  sent: number;
  delivered: number;
  read: number;
  failed: number;
  ambiguous: number;
  cancelled: number;
}

export interface ProjectRecipient {
  message_id: number;
  campaign_id: string;
  phone: string;
  status: string;
  error: string | null;
  sent_at: string | null;
  delivered_at: string | null;
  read_at: string | null;
  last_attempt_at: string | null;
  can_resend: boolean;
}

export interface ProjectRecipientsPage {
  items: ProjectRecipient[];
  total: number;
  page: number;
  page_size: number;
}

// FEATURE B (status-pull): result of a "Durumu Yenile" run — pending recipients queried vs. actually advanced.
export interface ProjectStatusPullResult {
  checked: number;
  updated: number;
}

// Bulk resend result: how many failed/ambiguous recipients were re-queued (0 = nothing was eligible).
export interface ProjectResendBulkResult {
  requeued: number;
}

// Send-config fields are OPTIONAL. template_kind is the DRIVER: when set, the whole config block
// is validated + persisted (create) / replaced (update); when omitted, config is left untouched.
export interface CreateProjectRequest {
  name: string;
  description?: string | null;
  target_list_ids: number[];
  instance_id?: number | null;
  template_kind?: ProjectTemplateKind | null;
  wa_template_id?: string | null;
  template_language?: string | null;
  param_mapping?: ProjectTemplateParam[] | null;
  content_mode?: ProjectContentMode | null;
  outbound_template_id?: number | null;
  plain_text_body?: string | null;
}

export interface UpdateProjectRequest {
  name?: string | null;
  description?: string | null;
  target_list_ids?: number[] | null;
  instance_id?: number | null;
  template_kind?: ProjectTemplateKind | null;
  wa_template_id?: string | null;
  template_language?: string | null;
  param_mapping?: ProjectTemplateParam[] | null;
  content_mode?: ProjectContentMode | null;
  outbound_template_id?: number | null;
  plain_text_body?: string | null;
}

// cxapi approved (HSM) template, as returned by GET /api/v1/settings/wa-templates (camelCase wire shape).
export interface WaTemplateRequiredInput {
  kind: string | null;       // 'text' | 'media'
  location: string | null;   // 'BODY' | 'HEADER' | 'BUTTON'
  paramKey: string | null;
  mediaType: string | null;  // 'image' | 'video' | 'document'
  note: string | null;
}
// cxapi `preview` is a STRUCTURED OBJECT (verified against the live wire 2026-06-09),
// not a string — header/body/footer/buttons. Each sub-field may be null.
export interface WaTemplateButton {
  type: string | null;   // 'QUICK_REPLY' | 'URL' | 'PHONE_NUMBER'
  text: string | null;
}
export interface WaTemplatePreviewHeader {
  type: string | null;   // 'TEXT' | 'IMAGE' | 'VIDEO' | 'DOCUMENT'
  text: string | null;   // null for a media header
}
export interface WaTemplatePreview {
  header: WaTemplatePreviewHeader | null;
  body: string | null;
  footer: string | null;
  buttons: WaTemplateButton[] | null;
}
export interface WaTemplate {
  templateId: string | null;
  name: string | null;       // template slug (one per language)
  language: string | null;   // 'tr' | 'en' | 'en_US' ...
  category: string | null;   // 'MARKETING' | 'UTILITY' | 'AUTHENTICATION'
  paramFormat: string | null;// 'named' | 'positional'
  preview: WaTemplatePreview | null;
  fixedNote: string | null;
  requiredInputs: WaTemplateRequiredInput[] | null;
}

export interface ImportCustomFlags {
  set_duplicates_sendable: boolean;
  update_duplicate_fields: boolean;
}

// FEAT-OBI Phase 1A Plan B: Export Manager DTOs (mirror Invekto.Shared ExportDtos.cs).
export interface SendJobSummary {
  id: number;
  campaign_id: string;
  status: string;
  // null for HSM (wa_template_id) and inline-text jobs.
  template_id: number | null;
  total_recipients: number;
  created_at: string;
  completed_at: string | null;
}

export interface SendRecipientRow {
  phone: string;
  status: string;
  status_label: string;
  sent_at: string | null;
  delivered_at: string | null;
  read_at: string | null;
  failed_reason: string | null;
}

export interface SendReportSummary {
  // null for HSM (wa_template_id) and inline-text jobs; template_name is then null too.
  template_id: number | null;
  template_name: string | null;
  lang: string | null;
  status: string;
  total_recipients: number;
  sent: number;
  delivered: number;
  read: number;
  failed: number;
  blocked: number;
  not_sent: number;
  skipped_optout: number;
  skipped_consent: number;
  created_at: string;
  confirmed_at: string | null;
  completed_at: string | null;
}

export interface SendReportData {
  job_id: number;
  campaign_id: string;
  generated_at: string;
  summary: SendReportSummary;
  total_recipient_count: number;
  recipient_table_truncated: boolean;
  recipient_table_limit: number;
  recipients: SendRecipientRow[];
}

// FEAT-OBI Phase 1B: Export Manager v2 (filter-driven recipients surface).
// ExportFilter uses camelCase here (querystring + SPA state); the create-list POST
// maps it to the server's snake_case ExportFilter in createListFromExport().
export interface ExportFilter {
  templateId?: number | null;
  jobId?: number | null;
  listId?: number | null;
  status?: string | null;
  from?: string | null; // YYYY-MM-DD (campaign date lower bound)
  to?: string | null;   // YYYY-MM-DD (campaign date upper bound, inclusive)
}

export interface FilteredCount {
  unique_count: number;
  total_count: number;
}

export interface FilterOptionItem {
  id: number;
  label: string;
}

export interface ExportFilterOptions {
  templates: FilterOptionItem[];
  campaigns: FilterOptionItem[];
  lists: FilterOptionItem[];
}

export interface CreateListFromExportResult {
  list_id: number;
  name: string;
  record_count: number;
}

export interface ExportLogEntry {
  id: number;
  export_type: string;
  source_name: string | null;
  format: string;
  delivery_mode: string;
  row_count: number;
  status: string;
  requested_by: string | null;
  error_code: string | null;
  created_at: string;
}

export interface ImportRowDto {
  phone?: string;
  name?: string;
  surname?: string;
  email?: string;
  tags?: string;
  note?: string;
  field1?: string;
  field2?: string;
  field3?: string;
  field4?: string;
  field5?: string;
  custom_fields?: Record<string, string>;
}

export interface ImportBatchRequest {
  list_id?: number | null;
  new_list_name?: string | null;
  scenario: ImportScenario;
  custom?: ImportCustomFlags;
  rows: ImportRowDto[];
}

export interface ImportBatchResponse {
  list_id: number;
  list_name: string;
  status: string;
  total_input: number;
  valid: number;
  file_duplicate: number;
  invalid: number;
  inserted: number;
  updated: number;
  skipped_existing: number;
  total_records: number;
  sendable_count: number;
  invalid_samples: string[];
}

export interface RecordsExistsResponse {
  exists: string[];
}

export interface PreviewFromListRequest {
  campaign_id: string;
  list_id: number;
  template_id: number;
  lang?: string | null;
}

export interface BulkSendPreviewResponse {
  campaign_id: string;
  status: string;
  hard_cap: number;
  total_input: number;
  total_valid: number;
  total_duplicate: number;
  total_invalid: number;
  sample: string[];
  invalid_samples: string[];
  // PR-4 (HSM project runs only): recipients excluded at preview because a required template
  // param resolved empty from its mapped list column. Absent/null for plain runs / zero skips.
  skipped_params?: BulkSkippedParamsInfo | null;
}

// PR-4: preview-time skip summary for HSM runs ("N alıcı atlanacak" + per-param breakdown).
export interface BulkSkippedParamsInfo {
  count: number;
  by_param?: Record<string, number> | null;
}

export interface BulkSendStatusResponse {
  campaign_id: string;
  status: string;
  total_valid: number;
  total_queued: number;
  total_skipped_optout: number;
  total_skipped_consent: number;
  sent: number;
  delivered: number;
  read: number;
  failed: number;
  broadcast_count: number;
  created_at: string;
  confirmed_at: string | null;
  completed_at: string | null;
}

// FEAT-TFM-UI type re-exports (pages + components consume from './api' to match the
// existing LiwSettingsDto / InmaDynamicFieldDto co-location convention).
export type {
  TenantFieldMappingEntryDto,
  TenantFieldMappingGetResponse,
  TenantFieldMappingPutRequest,
  TenantFieldMappingPutResponse,
} from '../types/tenantFieldMapping';

// FEAT-EFS Drip Sequence type re-exports.
export type {
  FollowupStageConfig,
  FollowupSequenceConfig,
  FollowupSequenceListResponse,
  FollowupSequencePutResponse,
  FollowupRunSummary,
  FollowupRunsResponse,
  FollowupStageDraft,
  FollowupSequenceDraft,
} from '../types/followupSequence';

// FEAT-MCC Multi-City Campaign type re-exports.
export type {
  CampaignCityDto,
  CampaignDateDto,
  CampaignEntryDto,
  CampaignConfigDto,
  CampaignConfigGetResponse,
  CampaignConfigPutRequest,
  CampaignConfigPutResponse,
  CampaignCityDraft,
  CampaignDateDraft,
  CampaignEntryDraft,
  CampaignConfigDraft,
} from '../types/campaignConfig';

// FEAT-CLINIC-METADATA type re-exports.
export type {
  ClinicContactDto,
  TeamMemberDto,
  ClinicMetadataGetResponse,
  ClinicMetadataPutRequest,
  ClinicMetadataPutResponse,
  TeamMemberDraft,
} from '../types/clinicMetadata';

// Paket B-META: Meta Leadgen webhook DTOs. Inline (no separate types file)
// because the full surface is 6 interfaces — tightly scoped to one SPA page
// so the types/ split-out overhead isn't justified.
export interface MetaLeadgenConfigMasked {
  verify_token_last4: string | null;
  app_secret_last4: string | null;
  page_access_token_last4: string | null;
  page_id: string | null;
  field_id_map: Record<string, string>;
}
export interface MetaLeadgenConfigGetResponse {
  data: {
    tenant_id: number;
    webhook_url: string;
    config: MetaLeadgenConfigMasked;
    updated_at: string | null;
  };
}
export interface MetaLeadgenConfigPutRequest {
  // null = keep server value (secrets); field_id_map is always the full map.
  verify_token: string | null;
  app_secret: string | null;
  page_access_token: string | null;
  page_id: string | null;
  field_id_map: Record<string, string>;
}
export interface MetaLeadgenConfigPutResponse {
  data: {
    tenant_id: number;
    updated_at: string;
  };
}
export interface MetaLeadgenRotateTokenResponse {
  data: {
    tenant_id: number;
    verify_token: string;
    updated_at: string;
  };
}
export interface MetaLeadgenFormQuestion {
  id: string;
  label: string;
  type: string;
}
export interface MetaLeadgenForm {
  id: string;
  name: string;
  questions: MetaLeadgenFormQuestion[];
}
export interface MetaLeadgenDiscoverFormsResponse {
  data: {
    forms: MetaLeadgenForm[];
  };
}
export interface MetaLeadgenEventDto {
  id: number;
  leadgen_id: string;
  form_id: string | null;
  page_id: string | null;
  received_at: string;
  http_status: number | null;
  error_code: string | null;
  lead_id: number | null;
  payload_keys: string[];
}
export interface MetaLeadgenEventsResponse {
  data: MetaLeadgenEventDto[];
}

// FEAT-DMP: INMA placeholder descriptor — matches the bridge proxy shape.
export interface InmaDynamicFieldDto {
  FieldKey: string;  // 'name' | 'email' | 'cf1' ... — the token inside {{...}}.
  FieldName: string; // Tenant-authored label shown in the picker (e.g. 'Şehir').
}

/** FEAT-DMP: distinct error kinds surfaced by useDynamicFields to PlaceholderPicker. */
export type DynamicFieldsErrorKind =
  | 'not_configured'      // 422 — tenant has no WapCRM secret; admin must configure INMA first.
  | 'upstream_fail'       // 503 — INMA unreachable; transient, refresh may help.
  | 'invalidate_partial'  // Server-side cache drop failed; fresh fetch may return stale (1h TTL) data.
  | 'unknown';            // Other (401, 5xx without code) — show generic retry.

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
