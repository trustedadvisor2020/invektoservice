using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-META-FULL-INTAKE: response envelope for the in-process call to
/// <see cref="LeadIntakeService.IntakeMetaLeadgenAsync"/>. Shape parity with
/// <see cref="WaDirectIntakeResponse"/> so MetaLeadgenEndpoints handler can
/// keep its existing JSON response contract verbatim — only the producer
/// changed, not the public schema.
/// </summary>
public sealed class MetaLeadgenIntakeResponse
{
    /// <summary>Persisted lead row id (post-UPSERT).</summary>
    [JsonPropertyName("lead_id")]
    public int LeadId { get; set; }

    /// <summary>True for fresh INSERTs / re-engagement outside the dup window; false on dup-window short-circuit.</summary>
    [JsonPropertyName("is_new")]
    public bool IsNew { get; set; }

    /// <summary>True when TriggerWelcomeFlowJob was successfully enqueued (Hangfire).</summary>
    [JsonPropertyName("welcome_flow_enqueued")]
    public bool WelcomeFlowEnqueued { get; set; }

    /// <summary>
    /// Best-effort warnings — present when lead row was committed but a downstream
    /// non-critical step (welcome enqueue) failed. Each entry is an INV-* code so
    /// the caller can map without parsing prose.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}
