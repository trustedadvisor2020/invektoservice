// FEAT-CLINIC-METADATA — Dashboard wire types.
// Mirror Chatinbox.Shared/Contracts/ClinicMetadata/* with snake_case JsonPropertyName,
// because Backend's ClinicMetadataEndpoints round-trip the snake-cased shape unchanged
// through the SPA-facing { data: ... } envelope.

export interface ClinicContactDto {
  /** Klinik adı (e.g. "Dent Adavista Dental Clinic"). */
  name?: string | null;
  /** E.164-style phone (^\+[0-9 ]{8,20}$). Validation enforced by Backend INV-BE-123. */
  phone?: string | null;
  /** HTTPS URL. Validation enforced by Backend INV-BE-124. */
  website?: string | null;
  instagram?: string | null;
  facebook?: string | null;
  /** Free-text full address line. */
  address?: string | null;
  /** Short city name for inline mention (e.g. "Kuşadası"). */
  city?: string | null;
}

export interface TeamMemberDto {
  /** Free-text role identifier ("dentist", "receptionist", "coordinator", ...). */
  role?: string | null;
  name?: string | null;
  title?: string | null;
  /** ISO-639-1 code or free text ("en", "tr", "ar", "de", "fr", ...). */
  language?: string | null;
  /** "she/her", "he/him", "they/them", or free text. */
  pronouns?: string | null;
}

/** GET /api/v1/tenant-settings/clinic-metadata — Backend response envelope. */
export interface ClinicMetadataGetResponse {
  data: {
    tenant_id: number;
    clinic_contact: ClinicContactDto;
    team_members: TeamMemberDto[];
    updated_at: string | null;
  };
}

/** PUT /api/v1/tenant-settings/clinic-metadata — request shape (tenant_id optional defensive field). */
export interface ClinicMetadataPutRequest {
  tenant_id?: number;
  clinic_contact: ClinicContactDto;
  team_members: TeamMemberDto[];
}

/** PUT response — same envelope as GET. */
export type ClinicMetadataPutResponse = ClinicMetadataGetResponse;

/**
 * UI-only draft state for a single team row inside the editor. Mirrors the
 * CampaignEntryDraft pattern: client-stable draftId for keying, bounded inputs as
 * strings, per-row error slot.
 */
export interface TeamMemberDraft {
  draftId: string;
  role: string;
  name: string;
  title: string;
  language: string;
  pronouns: string;
  rowError: string | null;
}
