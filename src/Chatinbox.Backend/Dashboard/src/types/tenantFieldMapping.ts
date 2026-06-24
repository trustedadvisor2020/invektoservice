// FEAT-TFM-UI: DTO types for Dashboard field-mapping editor.
// Wire format mirrors backend /api/v1/tenant-settings/field-mapping envelope
// (Chatinbox.Shared/Contracts/TenantFieldMapping/Dtos/TenantFieldMappingEntry.cs)
// with snake_case JsonPropertyName. Backend uses PropertyNamingPolicy.SnakeCaseLower.

import type { TenantFieldType } from '../constants/tenantFieldMappingReserved';

/** Single mapping entry (semantic -> INMA cf1..cf10) in wire format. */
export interface TenantFieldMappingEntryDto {
  /** INMA source slot (cf1..cf10). */
  source: string;
  /** Value type for runtime validation. */
  type: TenantFieldType;
  /** Allowed values when type='enum'. Required if type='enum', else may be omitted. */
  enum_values?: string[];
  /** Tenant flag: is this field required on lead write? */
  required?: boolean;
}

/** GET /api/v1/tenant-settings/field-mapping response envelope. */
export interface TenantFieldMappingGetResponse {
  data: {
    tenant_id: number;
    field_mapping: Record<string, TenantFieldMappingEntryDto>;
    updated_at: string | null;
  };
}

/** PUT /api/v1/tenant-settings/field-mapping request body. */
export interface TenantFieldMappingPutRequest {
  tenant_id: number;
  field_mapping: Record<string, TenantFieldMappingEntryDto>;
}

/** PUT response envelope (mirrors GET shape). */
export type TenantFieldMappingPutResponse = TenantFieldMappingGetResponse;

/**
 * UI-only draft state per INMA slot. Keeps the editor form shape stable even
 * when the semantic name or type is being edited. Not sent to the server.
 */
export interface FieldMappingRowDraft {
  /** INMA slot (cf1..cf10) — row identity. */
  source: string;
  /** Current semantic name input value (empty string = not mapped). */
  semanticName: string;
  /** Type select value. */
  type: TenantFieldType;
  /** Comma-separated enum values input. Parsed to enum_values[] on save. */
  enumValuesInput: string;
  /** Required checkbox state. */
  required: boolean;
  /** INMA FieldName label (read-only from useDynamicFields), null if INMA hasn't enabled the slot. */
  inmaLabel: string | null;
}
