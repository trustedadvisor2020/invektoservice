using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Leads;

/// <summary>
/// FEAT-META-FULL-INTAKE: in-process payload from Backend's MetaLeadgenEndpoints
/// process-lead handler to <see cref="LeadIntakeService.IntakeMetaLeadgenAsync"/>.
/// Carries the full canonical set resolved from Meta Graph API field_data via
/// tenant_settings.meta_leadgen_config.field_id_map (phone + name + email +
/// custom_1..custom_5 + consent).
///
/// Versus <see cref="WaDirectIntakeRequest"/>: WaDirect is the Automation
/// real-time WA inbound path (phone+name only, consent implied by user-initiated
/// contact per GDPR Recital 32). MetaLeadgen is the form-submitted lead-ads path
/// where the user explicitly checks a privacy/marketing consent box; consent=true
/// is gated upstream and required.
///
/// Trust model: this DTO is constructed in-process inside Backend after the
/// handler has already validated the Meta webhook signature + tenant context.
/// No HTTP transport / external auth needed; the type exists only for keeping
/// the service method signature small and self-documenting.
/// </summary>
public sealed class MetaLeadgenIntakeRequest
{
    /// <summary>Tenant id resolved from the URL path of the inbound webhook.</summary>
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    /// <summary>Phone in any format; Backend normalizes via libphonenumber-csharp.</summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Lead's name from the Meta form (canonical "name").</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Lead's email from the Meta form (canonical "email"). Persisted into leads.email.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>
    /// Marketing/privacy consent (canonical "consent"). Required true for intake
    /// to proceed; false / null / unmapped → 400 INV-BE-105 reject before any
    /// row write. Stored in intake_metadata.resolved.consent for audit trail.
    /// </summary>
    [JsonPropertyName("consent")]
    public bool? Consent { get; set; }

    /// <summary>
    /// Optional custom slot values (canonical "custom_1".."custom_5"). Persisted
    /// into intake_metadata.resolved.<custom_N> JSONB; leads.custom_N columns
    /// stay NULL pending FEAT-TFM-SYNC semantic projection (forward-compat per
    /// arch/db/pkt6b1-niche-business.sql:135-149).
    /// </summary>
    [JsonPropertyName("custom_1")] public string? Custom1 { get; set; }
    [JsonPropertyName("custom_2")] public string? Custom2 { get; set; }
    [JsonPropertyName("custom_3")] public string? Custom3 { get; set; }
    [JsonPropertyName("custom_4")] public string? Custom4 { get; set; }
    [JsonPropertyName("custom_5")] public string? Custom5 { get; set; }

    /// <summary>
    /// Meta-leadgen referer string (e.g. <c>"meta-leadgen:{form_id}"</c>) for
    /// downstream attribution; stored in intake_metadata.resolved.referer.
    /// </summary>
    [JsonPropertyName("referer")]
    public string? Referer { get; set; }

    /// <summary>Meta form created_time (server NOW() if null).</summary>
    [JsonPropertyName("received_at")]
    public DateTime? ReceivedAt { get; set; }
}
