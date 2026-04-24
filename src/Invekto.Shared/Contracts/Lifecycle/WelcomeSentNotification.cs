using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Lifecycle;

/// <summary>
/// Paket B-META: Automation → Backend internal HTTP hop body shape for
/// <c>POST /api/internal/lifecycle/welcome-sent</c>. Triggered by
/// <c>TriggerWelcomeFlowJob</c> when <c>result.Messages.Count &gt; 0</c>
/// (G2 2026-04-24 Q decision — literal "welcome delivered" semantic over the
/// conservative <c>finalStatus==completed</c> or aggressive <c>IsTerminal</c>
/// alternatives). Backend-side handler forwards to
/// <c>ZohoLifecycleDispatcher.DispatchEvent(tenantId, leadId, "welcome_sent")</c>
/// which drives the Zoho "1. Mesaj Atildi" Blueprint transition.
///
/// Cross-service hop pattern mirrors LIW Chunk B's <c>WaDirectIntakeRequest</c>
/// (shared-secret header auth via <c>InternalServices:SharedSecret</c> +
/// <c>X-Internal-Service-Token</c>). Fire-and-forget: non-2xx / transport
/// failures log INV-AT-072 warn and return without retry because the welcome
/// message already reached the user — a later lifecycle transition miss is
/// recoverable via the next inbound engagement, whereas double-sending the
/// Zoho transition would dirty the CRM state.
/// </summary>
public sealed class WelcomeSentNotification
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("lead_id")]
    public int LeadId { get; set; }

    /// <summary>
    /// UTC instant at which TriggerWelcomeFlowJob observed <c>Messages.Count &gt; 0</c>.
    /// Backend uses it as the <c>occurred_at</c> source for zoho_sync_log
    /// (so Automation-side clock, not Backend receive-time, reflects the actual
    /// welcome-dispatch moment).
    /// </summary>
    [JsonPropertyName("triggered_at")]
    public DateTime TriggeredAt { get; set; }
}
