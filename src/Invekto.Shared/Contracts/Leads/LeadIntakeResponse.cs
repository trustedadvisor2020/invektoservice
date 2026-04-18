using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW: 201 Created response for POST /api/v1/leads/intake/{source_slug}.
/// </summary>
public sealed class LeadIntakeResponse
{
    [JsonPropertyName("lead_id")]
    public int LeadId { get; set; }

    /// <summary>
    /// True when the phone already existed within the tenant's duplicate window
    /// (default 30 days; tenant override via <c>tenant_landing_settings.intake_dup_window_days</c>).
    /// On duplicate the lead row is merged (intake_metadata appended, source_slug
    /// overwritten) but no fresh welcome flow is enqueued.
    /// </summary>
    [JsonPropertyName("duplicate")]
    public bool Duplicate { get; set; }

    /// <summary>
    /// True iff a welcome-flow job was handed to Hangfire for asynchronous
    /// dispatch (duplicate=false AND a welcome slug was resolved AND enqueue
    /// succeeded). This flag reflects scheduling, NOT delivery: the worker
    /// dispatch happens on the <c>automation</c> queue and its own retry
    /// policy. Duplicate-within-window =&gt; false (spam suppression).
    /// Field name is deliberately "enqueued" not "triggered" so callers don't
    /// conflate Hangfire acceptance with WhatsApp message send.
    /// </summary>
    [JsonPropertyName("welcome_flow_enqueued")]
    public bool WelcomeFlowEnqueued { get; set; }

    /// <summary>
    /// Optional, additive signal channel for non-fatal operational issues the
    /// caller can observe without reading server logs. Populated with Invekto
    /// error codes when a side effect failed but the primary outcome (lead
    /// create/merge) still succeeded — e.g. INV-JOB-001 when Hangfire rejected
    /// the welcome-flow enqueue. Null/empty in the common path. Not a
    /// substitute for ErrorResponse, which is only returned on non-2xx status.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}
