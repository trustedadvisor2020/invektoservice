using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Inma.Dtos;

/// <summary>
/// FEAT-J2: Request payload for INMA POST /api/optout and /api/optin endpoints.
/// Wire property names are PascalCase to match INMA contract
/// (wapcrm-marketing-api.md section 5.1 and 5.2).
/// </summary>
public sealed class InmaOptOutRequest
{
    /// <summary>"phone" (WhatsApp/Voip/SMS) or "social" (Instagram/Telegram/Facebook)</summary>
    [JsonPropertyName("IdentifierType")]
    public required string IdentifierType { get; init; }

    /// <summary>Phone number (E.164 without +) or social account ID</summary>
    [JsonPropertyName("Identifier")]
    public required string Identifier { get; init; }

    /// <summary>WapCRM instance ID — required; InstanceType is derived from this</summary>
    [JsonPropertyName("InstanceID")]
    public required int InstanceID { get; init; }

    /// <summary>"all" (global across channels) or "channel" (this instance only)</summary>
    [JsonPropertyName("Scope")]
    public required string Scope { get; init; }

    /// <summary>Free-text audit reason (kept immutable on idempotent retries)</summary>
    [JsonPropertyName("Reason")]
    public string? Reason { get; init; }

    /// <summary>Source label: "customer_request" | "manual_admin" | "api" | "webhook" | "whatsapp"</summary>
    [JsonPropertyName("Source")]
    public string? Source { get; init; }
}
