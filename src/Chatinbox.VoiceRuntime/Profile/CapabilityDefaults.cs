namespace Chatinbox.VoiceRuntime.Profile;

/// <summary>
/// FEAT-VFB F-VR-B Chunk B2 (spec §3 capability flags): RESOLVED per-tenant voice capability gates.
/// All six are non-nullable here — they are the resolved values (admin override ?? business_type
/// default, computed in <see cref="ResolvedVoiceProfile.Resolve"/>). InstructionsBuilder turns these
/// into capability-driven sentences in the sector layer.
/// </summary>
public readonly record struct CapabilityFlags(
    bool PricingEnabled,
    bool AppointmentEnabled,
    bool OrderLookupEnabled,
    bool LeadCaptureEnabled,
    bool HumanHandoffEnabled,
    bool SensitiveTopicsEnabled);

/// <summary>
/// FEAT-VFB F-VR-B Chunk B2 (Q-approved matrix, 2026-06-02): the per-business_type DEFAULT capability
/// flags applied when voice_tenant_profile leaves a column NULL (= inherit). Kept SEPARATE from
/// <see cref="SectorAdapter"/> (AD-51) because service_business/education/real_estate share the generic
/// LANGUAGE adapter but have distinct capability defaults (education/real_estate appointment=true,
/// generic appointment=false). A non-null admin override in the DTO replaces the matching default.
/// </summary>
public static class CapabilityDefaults
{
    // Health verticals (clinic/dental/aesthetic/hair_transplant): appointment + pricing ON,
    // sensitive-topic escalation ON, order lookup OFF.
    private static readonly CapabilityFlags Health = new(
        PricingEnabled: true, AppointmentEnabled: true, OrderLookupEnabled: false,
        LeadCaptureEnabled: true, HumanHandoffEnabled: true, SensitiveTopicsEnabled: true);

    public static CapabilityFlags For(BusinessType type) => type switch
    {
        BusinessType.Clinic => Health,
        BusinessType.DentalClinic => Health,
        BusinessType.AestheticClinic => Health,
        BusinessType.HairTransplant => Health,

        // E-commerce: order lookup ON, appointment OFF, sensitive OFF.
        BusinessType.Ecommerce => new(
            PricingEnabled: true, AppointmentEnabled: false, OrderLookupEnabled: true,
            LeadCaptureEnabled: true, HumanHandoffEnabled: true, SensitiveTopicsEnabled: false),

        // Education / real-estate: appointment ON (tour/visit/enrolment meeting), order/sensitive OFF.
        BusinessType.Education => new(
            PricingEnabled: true, AppointmentEnabled: true, OrderLookupEnabled: false,
            LeadCaptureEnabled: true, HumanHandoffEnabled: true, SensitiveTopicsEnabled: false),
        BusinessType.RealEstate => new(
            PricingEnabled: true, AppointmentEnabled: true, OrderLookupEnabled: false,
            LeadCaptureEnabled: true, HumanHandoffEnabled: true, SensitiveTopicsEnabled: false),

        // Generic service business: lead-only (no appointment booking), order/sensitive OFF.
        BusinessType.ServiceBusiness => new(
            PricingEnabled: true, AppointmentEnabled: false, OrderLookupEnabled: false,
            LeadCaptureEnabled: true, HumanHandoffEnabled: true, SensitiveTopicsEnabled: false),

        // Generic / fallback: minimal — pricing + lead + handoff ON, everything else OFF.
        _ => new(
            PricingEnabled: true, AppointmentEnabled: false, OrderLookupEnabled: false,
            LeadCaptureEnabled: true, HumanHandoffEnabled: true, SensitiveTopicsEnabled: false)
    };
}
