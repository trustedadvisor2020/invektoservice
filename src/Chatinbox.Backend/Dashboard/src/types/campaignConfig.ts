// FEAT-MCC Multi-City Campaign — Dashboard wire types.
// Mirror Invekto.Shared/Contracts/Campaigns/Dtos/* with snake_case JsonPropertyName,
// because Backend's TenantCampaignConfigEndpoints round-trip the snake-cased shape
// unchanged through the SPA-facing { data: ... } envelope.

export interface CampaignCityDto {
  /** Lowercase slug, [a-z0-9_-]{1,40}; cross-referenced from CampaignDateDto.city. */
  slug: string;
  name: string;
  /** ISO-3166-1 alpha-2 (e.g. "IE"). Optional. */
  country?: string | null;
  /** IANA timezone (e.g. "Europe/Dublin"). Optional. */
  timezone?: string | null;
}

export interface CampaignDateDto {
  /** Must reference an existing CampaignCityDto.slug in the same campaign. */
  city: string;
  /** ISO-8601 calendar date (YYYY-MM-DD). */
  date: string;
  /** Display hours (e.g. "09:00-18:00"). Free text, max 64 chars. Optional. */
  hours?: string | null;
}

export interface CampaignEntryDto {
  /** Lowercase slug, [a-z][a-z0-9_-]{1,63}; tenant-scope unique. */
  slug: string;
  name: string;
  /** Operator kill-switch. False blocks the window guard even when start/end cover NOW. */
  active: boolean;
  /** Inclusive start (YYYY-MM-DD, tenant timezone). */
  start_date: string;
  /** Inclusive end (YYYY-MM-DD). Must be >= start_date. */
  end_date: string;
  cities: CampaignCityDto[];
  dates: CampaignDateDto[];
}

export interface CampaignConfigDto {
  campaigns: CampaignEntryDto[];
}

/** GET /api/v1/tenant-settings/campaign-config — Backend response envelope. */
export interface CampaignConfigGetResponse {
  data: {
    tenant_id: number;
    campaign_config: CampaignConfigDto;
    updated_at: string | null;
  };
}

/** PUT /api/v1/tenant-settings/campaign-config — request shape (tenant_id optional defensive field). */
export interface CampaignConfigPutRequest {
  tenant_id?: number;
  campaign_config: CampaignConfigDto;
}

/** PUT response — same envelope as GET. */
export type CampaignConfigPutResponse = CampaignConfigGetResponse;

/**
 * UI-only draft state for a single campaign row inside the editor. Mirrors the
 * FollowupStageDraft pattern: client-stable draftId for keying, bounded inputs as
 * strings (so YYYY-MM-DD partial typing renders cleanly), per-section error slots.
 */
export interface CampaignCityDraft {
  draftId: string;
  slug: string;
  name: string;
  country: string;
  timezone: string;
  rowError: string | null;
}

export interface CampaignDateDraft {
  draftId: string;
  city: string;
  date: string;
  hours: string;
  rowError: string | null;
}

export interface CampaignEntryDraft {
  draftId: string;
  slug: string;
  name: string;
  active: boolean;
  startDate: string;
  endDate: string;
  cities: CampaignCityDraft[];
  dates: CampaignDateDraft[];
  rowError: string | null;
}

export interface CampaignConfigDraft {
  campaigns: CampaignEntryDraft[];
  formError: string | null;
}
