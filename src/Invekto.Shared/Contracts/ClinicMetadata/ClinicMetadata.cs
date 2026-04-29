using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.ClinicMetadata;

/// <summary>
/// FEAT-CLINIC-METADATA: composite root carrying both <c>clinic_contact</c> and
/// <c>team_members</c> JSONB columns from <c>tenant_settings</c>. Resolver returns this
/// shape so a single cache entry covers both namespaces ({{clinic.*}} + {{team.*}}).
///
/// Backend GET/PUT envelope serializes this directly; Dashboard receives <c>contact</c>
/// + <c>team</c> object pair. Empty default = empty contact + empty team list, matching
/// new tenant default '{}' / '[]' DB row.
/// </summary>
public sealed class ClinicMetadata
{
    [JsonPropertyName("contact")]
    public ClinicContactDto Contact { get; set; } = new();

    [JsonPropertyName("team")]
    public List<TeamMemberDto> Team { get; set; } = new();

    public static ClinicMetadata Empty() => new();

    /// <summary>Resolve <c>{{team.role.field}}</c> token: case-insensitive role match,
    /// first array element wins (FEAT-MCC FirstOrDefault precedent — JSONB array order
    /// preserved). Empty string when role missing or field unknown.</summary>
    public string ResolveTeamMemberField(string role, string field)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(field))
            return string.Empty;

        var member = Team.FirstOrDefault(m =>
            string.Equals(m.Role, role, StringComparison.OrdinalIgnoreCase));

        return member?.Resolve(field) ?? string.Empty;
    }
}
