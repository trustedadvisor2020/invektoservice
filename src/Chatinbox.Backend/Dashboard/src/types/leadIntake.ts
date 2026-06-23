// FEAT-LIW Chunk C: TypeScript mirrors of Invekto.Shared.Contracts.Leads DTOs
// consumed by LeadIntakeSettingsPage + its child components. Keep in sync with
// the C# DTOs + the FieldMapValidator allowlist; the contract docs live at
// arch/contracts/lead-intake.json (dashboard_endpoints section).

export interface FlowStatusDto {
  resolved_slug: string;
  exists: boolean;
  flow_id?: number | null;
  flow_display_name?: string | null;
}

export interface TenantLandingSettingsDto {
  tenant_id: number;
  masked_active_key: string | null;
  masked_old_key: string | null;
  old_expires_at: string | null; // ISO datetime
  /** UI direction: source_field -> canonical_field */
  field_map: Record<string, string>;
  phone_country_hint: string | null;
  welcome_flow_slug: string | null;
  dup_window_days: number;
  flow_status: FlowStatusDto;
  updated_at: string | null; // row_version (ISO); null = first-time tenant
  created_at: string | null;
  has_active_key: boolean;
}

export interface RotateApiKeyResponse {
  active_plaintext: string;
  masked_old: string | null;
  old_expires_at: string | null;
  row_version: string;
}

export interface UpdateFieldMapRequest {
  field_map: Record<string, string>;
  phone_country_hint?: string | null;
  expected_row_version?: string | null;
}

export interface UpdateFieldMapResponse {
  field_map: Record<string, string>;
  phone_country_hint: string | null;
  row_version: string;
}

export interface DryRunRequest {
  source_slug?: string;
  payload?: Record<string, unknown>;
  field_map_override?: Record<string, string> | null;
  phone_country_hint_override?: string | null;
}

export interface DryRunResponse {
  resolved_fields: Record<string, unknown>;
  warnings: string[];
  would_upsert: boolean;
  would_trigger_welcome: boolean;
  phone_e164?: string | null;
  consent_ok: boolean;
}

export interface LiwAuditEntryDto {
  id: number;
  action: LiwAuditAction;
  before_json: Record<string, unknown> | null;
  after_json: Record<string, unknown> | null;
  created_at: string;
  user_display: string;
}

export type LiwAuditAction =
  | 'apikey.rotate'
  | 'apikey.revoke'
  | 'fieldmap.save'
  | 'welcome_slug.change';

export interface AuditListResponse {
  entries: LiwAuditEntryDto[];
}

/**
 * Canonical field allowlist (mirror of FieldMapValidator.AllowedCanonicals).
 * Keep in sync with Invekto.Backend.Services.FieldMapValidator.
 */
export const CANONICAL_FIELDS: readonly string[] = [
  'name',
  'phone',
  'email',
  'consent',
  'utm_source',
  'utm_medium',
  'utm_campaign',
  'utm_content',
  'utm_term',
  'referer',
  'metadata',
] as const;

export const REQUIRED_CANONICALS: readonly string[] = ['phone', 'consent'] as const;
