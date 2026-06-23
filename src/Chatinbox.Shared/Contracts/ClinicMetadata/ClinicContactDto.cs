using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.ClinicMetadata;

/// <summary>
/// FEAT-CLINIC-METADATA: shape of <c>tenant_settings.clinic_contact JSONB</c>.
///
/// All fields nullable (string?) — empty default JSON {} maps to all-null DTO and
/// <see cref="ClinicTemplateApplier"/> renders empty string for any unfilled key
/// so template substitution remains graceful.
///
/// Validation lives at the Backend endpoint layer (ClinicMetadataEndpoints):
///   <list type="bullet">
///     <item>phone — E.164 regex (<c>^\+[0-9 ]{8,20}$</c>) → INV-BE-123 on PUT.</item>
///     <item>website / instagram / facebook — <c>^https?://</c> → INV-BE-124 on PUT.</item>
///     <item>JSON shape malformed → INV-BE-122 on PUT.</item>
///   </list>
///
/// Snake_case JSON contract matches DB JSONB shape and Migration 040 §3 seed.
/// </summary>
public sealed class ClinicContactDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("instagram")]
    public string? Instagram { get; set; }

    [JsonPropertyName("facebook")]
    public string? Facebook { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>Empty contact for missing tenant_settings row or empty JSONB column.</summary>
    public static ClinicContactDto Empty() => new();

    /// <summary>Resolve a snake_case key to its string value (case-insensitive).
    /// Returns empty string when the key is unknown or the value is null/empty —
    /// matches FEAT-MCC <c>RenderPlaceholderAsync</c> graceful-empty contract so
    /// {{clinic.X}} substitution never crashes downstream rendering.</summary>
    public string Resolve(string key) => (key ?? string.Empty).ToLowerInvariant() switch
    {
        "name"      => Name      ?? string.Empty,
        "phone"     => Phone     ?? string.Empty,
        "website"   => Website   ?? string.Empty,
        "instagram" => Instagram ?? string.Empty,
        "facebook"  => Facebook  ?? string.Empty,
        "address"   => Address   ?? string.Empty,
        "city"      => City      ?? string.Empty,
        _           => string.Empty
    };
}
