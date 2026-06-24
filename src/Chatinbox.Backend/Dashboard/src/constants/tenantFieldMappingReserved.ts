// FEAT-TFM-UI: reserved semantic name set mirrored from Chatinbox.Shared.Services.TenantFieldMappingValidator.
// Kept in sync MANUALLY with:
//   - Chatinbox.Shared/Constants/InmaDynamicFieldKeys.cs (Allowlist: 15 keys)
//   - Chatinbox.Shared/Services/TenantFieldMappingValidator.cs (LeadsCoreColumns: 8 keys)
// Drift check: if backend adds/removes reserved keys, bump this file + lessons-learned entry.
// Rationale: live FE validation (P3 AC4) avoids a round-trip to surface INV-BE-097 reserved
// name violations — backend still enforces as source of truth.

// INMA Allowlist (wapcrm-marketing-api.md §2 + InmaDynamicFieldKeys.cs):
//   name | email | note | pushname | datalistname | cf1..cf10 (lowercase, case-insensitive match)
const INMA_ALLOWLIST: ReadonlyArray<string> = [
  'name',
  'email',
  'note',
  'pushname',
  'datalistname',
  'cf1', 'cf2', 'cf3', 'cf4', 'cf5',
  'cf6', 'cf7', 'cf8', 'cf9', 'cf10',
];

// Leads core columns (TenantFieldMappingValidator.cs:LeadsCoreColumns):
//   id | tenant_id | full_name | phone | created_at | updated_at | pipeline_status | preferred_locale
const LEADS_CORE_COLUMNS: ReadonlyArray<string> = [
  'id',
  'tenant_id',
  'full_name',
  'phone',
  'created_at',
  'updated_at',
  'pipeline_status',
  'preferred_locale',
];

/**
 * Full reserved semantic name set (23 entries total). Tenant may NOT register any of
 * these as a semantic mapping key — ambiguous substitution vs INMA allowlist / leads
 * core column collision. Check is case-insensitive (Set.has after toLowerCase).
 */
export const TENANT_FIELD_MAPPING_RESERVED: ReadonlySet<string> = new Set(
  [...INMA_ALLOWLIST, ...LEADS_CORE_COLUMNS].map((s) => s.toLowerCase()),
);

/**
 * Semantic names matching `^cf\d+$` are reserved regardless of range (cf11, cf200, etc.).
 * Rationale: INMA custom field namespace `cf\d+` belongs to the source-slot layer, so a
 * semantic alias that looks like a raw slot key creates the same ambiguous substitution
 * as an allowlist collision. Backend TenantFieldMappingValidator already rejects semantic
 * names that collide with `cf1..cf10` via the Allowlist union; FE rejects the full `cfN`
 * shape so tenants cannot smuggle `cf11`-style names that Codex Q2 calls out.
 */
const CF_NAMESPACE_PATTERN = /^cf\d+$/i;

/** Lowercase check: is the candidate semantic name reserved? */
export function isReservedSemanticName(candidate: string): boolean {
  if (!candidate) return false;
  const lower = candidate.toLowerCase();
  if (TENANT_FIELD_MAPPING_RESERVED.has(lower)) return true;
  if (CF_NAMESPACE_PATTERN.test(lower)) return true;
  return false;
}

/** Allowed INMA source slot range cf1..cf10 (cf11+ rejected by backend INV-BE-099). */
export const ALLOWED_SOURCE_SLOTS: ReadonlyArray<string> = [
  'cf1', 'cf2', 'cf3', 'cf4', 'cf5',
  'cf6', 'cf7', 'cf8', 'cf9', 'cf10',
];

/** Mapping entry type whitelist (mirrors TenantFieldMappingValidator.AllowedTypes). */
export const ALLOWED_FIELD_TYPES = ['enum', 'string', 'date', 'bool', 'int'] as const;
export type TenantFieldType = typeof ALLOWED_FIELD_TYPES[number];

/** Semantic name regex (mirrors backend `^[a-z][a-z0-9_]{1,63}$`). */
export const SEMANTIC_NAME_PATTERN = /^[a-z][a-z0-9_]{1,63}$/;
