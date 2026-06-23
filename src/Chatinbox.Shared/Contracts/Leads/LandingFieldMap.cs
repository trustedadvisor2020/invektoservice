namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-LIW: typed view over tenant_landing_settings.landing_field_map JSONB.
/// The JSONB shape is <c>{ "&lt;canonical&gt;": "&lt;source_field&gt;" }</c>
/// plus a reserved optional entry <c>"phone.country_hint"</c> (ISO 3166-1 alpha-2).
/// Canonical field names are the constants on <see cref="LeadIntakeCanonical"/>.
/// </summary>
public sealed class LandingFieldMap
{
    /// <summary>Raw canonical -> source field map (loaded from JSONB verbatim).</summary>
    public Dictionary<string, string> Map { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional ISO 3166-1 alpha-2 country hint for phone parsing (e.g. "IE", "TR").</summary>
    public string? PhoneCountryHint { get; set; }

    /// <summary>Returns the source field for a canonical name, or null when unmapped.</summary>
    public string? GetSourceField(string canonical) =>
        Map.TryGetValue(canonical, out var src) ? src : null;
}

/// <summary>
/// Canonical field name registry. Tenant maps arbitrary source keys to these
/// constants; only <see cref="Name"/>, <see cref="Phone"/>, and <see cref="Consent"/>
/// are required — the rest are optional. <see cref="PhoneCountryHint"/> is a reserved
/// map key (not a canonical field) used to hint libphonenumber during parsing.
/// </summary>
public static class LeadIntakeCanonical
{
    public const string Name    = "name";
    public const string Phone   = "phone";
    public const string Consent = "consent";
    public const string Email   = "email";

    public const string Custom1  = "custom_1";
    public const string Custom2  = "custom_2";
    public const string Custom3  = "custom_3";
    public const string Custom4  = "custom_4";
    public const string Custom5  = "custom_5";
    public const string Custom6  = "custom_6";
    public const string Custom7  = "custom_7";
    public const string Custom8  = "custom_8";
    public const string Custom9  = "custom_9";
    public const string Custom10 = "custom_10";

    /// <summary>Reserved map key for phone parse country hint (not a canonical field).</summary>
    public const string PhoneCountryHintKey = "phone.country_hint";

    /// <summary>Canonical fields a tenant MUST map for intake to succeed.</summary>
    public static readonly IReadOnlyCollection<string> Required = new[] { Name, Phone, Consent };

    /// <summary>Optional canonical fields. Unmapped => null stored on lead row.</summary>
    public static readonly IReadOnlyCollection<string> Optional = new[]
    {
        Email, Custom1, Custom2, Custom3, Custom4, Custom5,
        Custom6, Custom7, Custom8, Custom9, Custom10
    };
}
