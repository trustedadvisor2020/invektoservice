// FEAT-EFS Drip Sequence — Dashboard wire types.
// Mirror Invekto.Shared/Contracts/Followup/* with snake_case JsonPropertyName,
// because Backend's MarketingFollowupProxyClient round-trips Marketing's
// snake-cased response unchanged through the SPA-facing { data: ... } envelope.

/**
 * Single stage within a sequence. The array order in `FollowupSequenceConfig.stages`
 * IS the stage order. Stage 0 fires `delay_days` after EnqueueAsync; stage N fires
 * `delay_days` after stage N-1. When the tenant flag `efs_test_mode=true`, the unit
 * is reinterpreted as MINUTES so the smoke validates message timing in ~24 minutes
 * instead of 24 days.
 */
export interface FollowupStageConfig {
  /** Days from previous stage (or from EnqueueAsync for index 0). 1..30 in normal mode. */
  delay_days: number;
  /** Template lookup slug (lowercase, [a-z0-9_-], max 64 chars). */
  template_slug: string;
  /** Optional rotation/group tag — multiple template variants may share a group. */
  template_group?: string | null;
}

/**
 * Tenant-scoped follow-up sequence. Maps 1:1 to a row in `event_followup_sequences`.
 * Server-managed fields (id, created_at, updated_at) are read-only.
 */
export interface FollowupSequenceConfig {
  /** Server-assigned id; undefined on a fresh-create PUT. */
  id?: number | null;
  /** Tenant-scoped slug, unique per tenant. e.g. "post-roadshow", "no-reply-welcome". */
  slug: string;
  stages: FollowupStageConfig[];
  /** % routed to drip group; remainder go to control (zero rows scheduled). 0..100. */
  ab_split_percent: number;
  /** Whether the sequence is live. Disabled sequences reject EnqueueAsync (INV-MK-054). */
  enabled: boolean;
  /** Server timestamps (read-only). */
  created_at?: string | null;
  updated_at?: string | null;
}

/** GET /api/v1/tenant-settings/followup-sequence — Backend proxy response envelope.
 *  Carries tenant settings (test_mode, no_reply_threshold_days) so the editor labels
 *  units (gun vs dk) consistent with backend validation + scheduling behavior. */
export interface FollowupSequenceListResponse {
  data: FollowupSequenceConfig[];
  test_mode: boolean;
  no_reply_threshold_days: number;
}

/** PUT /api/v1/tenant-settings/followup-sequence — Backend proxy response envelope. */
export interface FollowupSequencePutResponse {
  data: FollowupSequenceConfig;
}

/** Recent run row projection for the Runs tab. */
export interface FollowupRunSummary {
  id: number;
  sequence_id: number;
  lead_id: number;
  stage_index: number;
  ab_group: 'drip' | 'control';
  scheduled_at: string;
  status: 'scheduled' | 'sent' | 'skipped_optout' | 'skipped_disabled' | 'failed';
}

/** GET /api/v1/tenant/followup/runs — Backend proxy response envelope. */
export interface FollowupRunsResponse {
  data: FollowupRunSummary[];
}

/**
 * UI-only draft state for a stage row inside the editor. Tracks whether the row
 * carries a validation error so FollowupStageRow can render its sibling-tr error
 * banner (Fragment + sibling tr per lessons 2026-04-22 P3 CQ5+CQ10).
 */
export interface FollowupStageDraft {
  /** Stable id for React keying — assigned client-side, never sent to server. */
  draftId: string;
  delayDaysInput: string;   // string so the input field can be edited cleanly
  templateSlug: string;
  templateGroup: string;     // empty string = no rotation group
  rowError: string | null;   // populated when validation fails for THIS row
}

/** UI-only draft for the whole sequence editor. */
export interface FollowupSequenceDraft {
  slug: string;
  abSplitPercent: number;
  enabled: boolean;
  stages: FollowupStageDraft[];
  formError: string | null;  // top-of-form error (cap exceeded, slug invalid, etc.)
}
