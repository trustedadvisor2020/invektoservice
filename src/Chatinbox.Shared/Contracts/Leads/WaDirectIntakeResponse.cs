using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk B: 201 Created response for POST /api/internal/leads/intake/wa-direct.
/// Returned to the Automation service so the wa-direct hook can correlate
/// subsequent flow execution with the lead row.
/// </summary>
public sealed class WaDirectIntakeResponse
{
    [JsonPropertyName("lead_id")]
    public int LeadId { get; set; }

    /// <summary>
    /// True when the lead row was newly inserted OR an existing row outside the
    /// tenant's duplicate window was re-engaged. False when an existing row was
    /// found within the window (no UPSERT, no metadata change, no welcome).
    /// Caller uses this to decide whether to expect downstream welcome activity.
    /// </summary>
    [JsonPropertyName("is_new")]
    public bool IsNew { get; set; }

    /// <summary>
    /// True when a welcome-flow job was handed to Hangfire (IsNew=true AND
    /// enqueue succeeded). Reflects scheduling, not delivery — same semantic as
    /// the landing endpoint's flag. False on duplicate-within-window OR enqueue
    /// failure (in which case Warnings carries the matching INV-JOB-* code).
    /// </summary>
    [JsonPropertyName("welcome_flow_enqueued")]
    public bool WelcomeFlowEnqueued { get; set; }

    /// <summary>
    /// Optional, additive signal channel mirrored from the landing endpoint
    /// pattern: populated with INV-JOB-* codes when Hangfire enqueue failed but
    /// the lead row still committed. Null in the common path.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}
