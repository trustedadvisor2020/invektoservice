namespace Chatinbox.VoiceRuntime.Profile;

/// <summary>
/// FEAT-VFB F-VR-B Chunk B2 (spec voice-multitenant-runtime.md §3): the 9 voice business types.
/// Mirrors the voice_tenant_profile.business_type CHECK constraint (migration 050) and the Shared
/// <see cref="Chatinbox.Shared.DTOs.Voice.VoiceTenantProfileDto.BusinessType"/> string. VoiceRuntime-only:
/// the language adapter (<see cref="SectorAdapter"/>) and capability defaults
/// (<see cref="CapabilityDefaults"/>) are keyed on this enum. It deliberately stays OUT of Shared
/// (only the wire DTO is shared) so behavior/registry types do not leak across the service boundary.
/// </summary>
public enum BusinessType
{
    Clinic,
    DentalClinic,
    AestheticClinic,
    HairTransplant,
    Ecommerce,
    ServiceBusiness,
    Education,
    RealEstate,
    Generic
}

/// <summary>
/// Maps the DB/DTO snake_case business_type string to <see cref="BusinessType"/>.
/// </summary>
public static class BusinessTypeParser
{
    /// <summary>
    /// Unknown / null / blank / unexpected value → <see cref="BusinessType.Generic"/> (defensive:
    /// the DB CHECK constraint prevents out-of-set values, but VoiceRuntime never trusts upstream —
    /// a renamed/added type must degrade to Generic rather than throw and break the voice session).
    /// </summary>
    public static BusinessType Parse(string? businessType) => businessType?.Trim().ToLowerInvariant() switch
    {
        "clinic" => BusinessType.Clinic,
        "dental_clinic" => BusinessType.DentalClinic,
        "aesthetic_clinic" => BusinessType.AestheticClinic,
        "hair_transplant" => BusinessType.HairTransplant,
        "ecommerce" => BusinessType.Ecommerce,
        "service_business" => BusinessType.ServiceBusiness,
        "education" => BusinessType.Education,
        "real_estate" => BusinessType.RealEstate,
        _ => BusinessType.Generic
    };
}
