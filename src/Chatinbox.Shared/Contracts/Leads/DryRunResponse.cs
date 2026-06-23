using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: POST /api/v1/tenant/landing/dry-run 200 response.
/// Structured preview of what the real intake endpoint WOULD do with the
/// supplied payload. <see cref="Warnings"/> carries human-readable diagnostic
/// lines (missing required canonical, phone parse failure, consent missing,
/// unknown canonical in override map, etc.) rather than error codes — this is
/// a diagnostic tool for tenant operators, not a strict error envelope.
/// <see cref="WouldUpsert"/> is the concrete "yes/no" signal for the tenant:
/// when true, the real intake would create or merge a lead; when false, the
/// warnings[] list explains why it would fail.
/// </summary>
public sealed class DryRunResponse
{
    /// <summary>Canonical -> resolved value (strings, numbers, bools, or null). Never includes 'phone.country_hint'.</summary>
    [JsonPropertyName("resolved_fields")]
    public Dictionary<string, object?> ResolvedFields { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("would_upsert")]
    public bool WouldUpsert { get; set; }

    [JsonPropertyName("would_trigger_welcome")]
    public bool WouldTriggerWelcome { get; set; }

    [JsonPropertyName("phone_e164")]
    public string? PhoneE164 { get; set; }

    [JsonPropertyName("consent_ok")]
    public bool ConsentOk { get; set; }
}
