using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk B: POST /api/internal/leads/intake/wa-direct payload.
/// Service-to-service contract used by Automation's WA inbound hook to register a
/// lead row in Backend before chat-flow execution. The caller is the Automation
/// service (authenticated via the X-Internal-Service-Token header — canonical
/// peer-service shared-secret pattern); end-users never reach this endpoint.
/// Consent is implied by user-initiated contact (GDPR Recital 32 affirmative
/// action), so no explicit consent field is required — distinct from landing
/// intake which validates a checkbox value.
/// </summary>
public sealed class WaDirectIntakeRequest
{
    /// <summary>Tenant id resolved by Automation (typically from TenantContext on the inbound webhook).</summary>
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    /// <summary>Sender phone in any format; Backend normalizes via libphonenumber-csharp.</summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>WhatsApp profile name when present; nullable for inbounds with no profile metadata.</summary>
    [JsonPropertyName("profile_name")]
    public string? ProfileName { get; set; }

    /// <summary>
    /// WhatsApp click-to-chat referrer (e.g. ctwa_clid for Meta Click-to-WA ads).
    /// Stored in intake_metadata.referer when present; absent fields are silently
    /// omitted (no synthetic placeholder so reports stay honest).
    /// </summary>
    [JsonPropertyName("referer")]
    public string? Referer { get; set; }

    /// <summary>Inbound message timestamp (defaults to server NOW() when null).</summary>
    [JsonPropertyName("received_at")]
    public DateTime? ReceivedAt { get; set; }
}
