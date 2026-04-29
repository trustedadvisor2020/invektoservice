using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.ClinicMetadata;

/// <summary>
/// FEAT-CLINIC-METADATA: single team member entry in <c>tenant_settings.team_members JSONB</c> array.
///
/// Multi-role support: <c>{{team.dentist.name}}</c> resolves to the FIRST array element with
/// <see cref="Role"/>="dentist" (LINQ FirstOrDefault, JSONB array order preserved). Index syntax
/// like <c>{{team.dentist[0].name}}</c> is intentionally excluded for the pilot scope (see plan
/// JSON intentional_exclusions); operators sequence members via Dashboard drag-handle to designate
/// the "primary" entry per role.
///
/// All fields nullable so empty array element / partial JSON keep <see cref="ClinicTemplateApplier"/>
/// resilient — unknown role/key resolves to empty string.
/// </summary>
public sealed class TeamMemberDto
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("pronouns")]
    public string? Pronouns { get; set; }

    /// <summary>Resolve a snake_case key to its string value (case-insensitive).
    /// Returns empty string when key unknown or value null/empty.</summary>
    public string Resolve(string key) => (key ?? string.Empty).ToLowerInvariant() switch
    {
        "name"     => Name     ?? string.Empty,
        "title"    => Title    ?? string.Empty,
        "language" => Language ?? string.Empty,
        "pronouns" => Pronouns ?? string.Empty,
        "role"     => Role     ?? string.Empty,
        _          => string.Empty
    };
}
