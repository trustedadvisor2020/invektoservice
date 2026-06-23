using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Followup;

/// <summary>
/// Cross-service request body for POST /api/internal/followup/trigger (Marketing).
///
/// Auth (defense in depth, lessons 2026-04-21):
///   - Header <c>X-Internal-Service-Token</c> matches <c>InternalServices:SharedSecret</c>
///     constant-time compare (LIW Chunk B precedent).
///   - Header <c>Authorization: Bearer &lt;service-jwt&gt;</c> with the same shared
///     <c>Jwt:SecretKey</c>; the JWT carries a tenant_id claim that MUST match the body
///     <see cref="TenantId"/> below. Marketing's UseJwtAuth("/api/v1/") prefix DOES NOT
///     cover /api/internal/, so the handler enforces both header checks explicitly.
///
/// Idempotency: Marketing's FollowupOrchestrator detects a pre-existing scheduled run for
/// the same (tenant_id, lead_id) tuple via <c>event_followup_runs</c> and rejects with
/// INV-MK-055 (followup_run_collision) — caller is responsible for de-duplication on
/// retry storms.
/// </summary>
public sealed class FollowupTriggerRequest
{
    /// <summary>Authoritative tenant scope; MUST match the JWT tenant_id claim.</summary>
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    /// <summary>Target lead row in <c>leads</c>. Resolved during EnqueueAsync.</summary>
    [JsonPropertyName("lead_id")]
    public long LeadId { get; set; }

    /// <summary>
    /// The originating domain event. Determines which sequence slug Marketing picks
    /// (see <see cref="SequenceSlug"/>) when not explicitly supplied.
    /// </summary>
    [JsonPropertyName("reason")]
    public FollowupTriggerReason Reason { get; set; }

    /// <summary>
    /// Optional explicit sequence slug override. NULL → orchestrator uses the
    /// tenant's default sequence for the given <see cref="Reason"/>. The pilot
    /// uses NULL (single configured sequence per reason).
    /// </summary>
    [JsonPropertyName("sequence_slug")]
    public string? SequenceSlug { get; set; }

    /// <summary>
    /// Caller-supplied correlation id (typically a request id from upstream). Surfaced
    /// in the response and structured logs for end-to-end trace.
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}

/// <summary>
/// Response envelope for POST /api/internal/followup/trigger. Mirrors the
/// VideoMeetingHopResponse "single envelope, distinct status fields" pattern
/// (lessons 2026-04-20 Chunk B: error and success share one shape so callers
/// have one deserialize path).
/// </summary>
public sealed class FollowupTriggerResponse
{
    /// <summary>
    /// True when the orchestrator created run rows OR routed to control group (both are
    /// successful outcomes from the caller's perspective). False when an error occurred
    /// (see <see cref="ErrorCode"/>).
    /// </summary>
    [JsonPropertyName("accepted")]
    public bool Accepted { get; set; }

    /// <summary>
    /// 'drip' | 'control' | NULL when not accepted.
    /// </summary>
    [JsonPropertyName("ab_group")]
    public string? AbGroup { get; set; }

    /// <summary>
    /// Number of <c>event_followup_runs</c> rows scheduled. Zero for control group;
    /// equal to <c>sequence.stages.Count</c> for drip group.
    /// </summary>
    [JsonPropertyName("scheduled_runs")]
    public int ScheduledRuns { get; set; }

    /// <summary>Server-assigned sequence id used for this trigger.</summary>
    [JsonPropertyName("sequence_id")]
    public long? SequenceId { get; set; }

    /// <summary>INV-MK-* code on failure; NULL on success.</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    /// <summary>Human-readable Turkish message for operator surfaces.</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>Echo of the request id for trace correlation.</summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}
