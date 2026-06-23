using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW Chunk C: single row from GET /api/v1/tenant/landing/audit.
/// Sourced from liw_audit_log via LiwAuditRepository.ListAsync. Newest-first.
/// <see cref="BeforeJson"/> and <see cref="AfterJson"/> are raw JSON payloads
/// (serialized as JsonElement to preserve exact shape for the UI's expandable
/// diff view); either may be null (e.g. 'apikey.revoke' has no meaningful after;
/// first-time 'apikey.rotate' has no before). <see cref="UserDisplay"/> is a
/// pre-computed 'User#{user_id}' or 'Sistem' string so the UI doesn't need a
/// second roundtrip to resolve actor names in v1 (full name resolution is Chunk D).
/// </summary>
public sealed class LiwAuditEntryDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("before_json")]
    public JsonElement? BeforeJson { get; set; }

    [JsonPropertyName("after_json")]
    public JsonElement? AfterJson { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("user_display")]
    public string UserDisplay { get; set; } = "Sistem";
}

/// <summary>Known audit action values (enforced at application layer in LiwSettingsService).</summary>
public static class LiwAuditActions
{
    public const string ApiKeyRotate = "apikey.rotate";
    public const string ApiKeyRevoke = "apikey.revoke";
    public const string FieldMapSave = "fieldmap.save";
    public const string WelcomeSlugChange = "welcome_slug.change";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        ApiKeyRotate, ApiKeyRevoke, FieldMapSave, WelcomeSlugChange
    };
}
