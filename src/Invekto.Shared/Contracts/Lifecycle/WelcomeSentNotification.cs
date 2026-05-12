using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Lifecycle;

/// <summary>
/// Paket B-META: Automation → Backend internal HTTP hop body shape for
/// <c>POST /api/internal/lifecycle/welcome-sent</c>. Triggered by
/// <c>TriggerWelcomeFlowJob</c> when <c>result.Messages.Count &gt; 0</c>
/// (G2 2026-04-24 Q decision — literal "welcome delivered" semantic over the
/// conservative <c>finalStatus==completed</c> or aggressive <c>IsTerminal</c>
/// alternatives). FEAT-INMA-PIPELINE-V2 C1 (2026-05-13): Backend-side handler
/// now accepts + logs the notification without downstream sync dispatch
/// (previously forwarded to <c>ZohoLifecycleDispatcher.DispatchEvent</c>; Zoho
/// pipeline removed). INMA-authoritative <c>customer_status</c> trigger channel
/// (V2 C3) will consume welcome-sent events when INMA contract ships.
///
/// Cross-service hop pattern mirrors LIW Chunk B's <c>WaDirectIntakeRequest</c>
/// (shared-secret header auth via <c>InternalServices:SharedSecret</c> +
/// <c>X-Internal-Service-Token</c>). Fire-and-forget: non-2xx / transport
/// failures log INV-AT-072 warn and return without retry because the welcome
/// message already reached the user — a later lifecycle notification miss is
/// recoverable via the next inbound engagement, whereas double-sending the
/// downstream event would dirty consumer state once C3 activates.
/// </summary>
public sealed class WelcomeSentNotification
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("lead_id")]
    public int LeadId { get; set; }

    /// <summary>
    /// UTC instant at which TriggerWelcomeFlowJob observed <c>Messages.Count &gt; 0</c>.
    /// Backend logs this as the welcome-dispatch moment (Automation-side clock,
    /// not Backend receive-time). Future V2 C3 trigger consumer will use the
    /// same field as the <c>occurred_at</c> source for downstream emissions.
    /// </summary>
    [JsonPropertyName("triggered_at")]
    public DateTime TriggeredAt { get; set; }
}
