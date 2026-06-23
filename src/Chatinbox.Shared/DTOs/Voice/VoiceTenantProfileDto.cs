namespace Chatinbox.Shared.DTOs.Voice;

/// <summary>
/// FEAT-VFB F-VR-B (spec voice-multitenant-runtime.md §3): wire contract for a tenant's voice
/// behavior profile. Returned by Backend GET /api/ops/tenants/{id}/voice-profile and consumed by
/// VoiceRuntime (which has no DB) to drive InstructionsBuilder + sector_adapter selection.
///
/// Override semantics: <see cref="BusinessType"/> and <see cref="DefaultLanguage"/> are
/// authoritative. The capability flags + goals + brand tone are NULLABLE = "inherit business_type
/// default". A null flag means VoiceRuntime applies the business_type default (sector_adapter
/// layer); a non-null flag is an explicit per-tenant admin override. This keeps the default logic
/// in ONE place (VoiceRuntime) — Backend is a thin storage/return layer and does NOT resolve
/// defaults.
///
/// Synthesized fallback: if no voice_tenant_profile row exists for an active tenant, Backend
/// returns a row with BusinessType='generic' + all overrides null rather than 404, so voice never
/// breaks for a not-yet-provisioned tenant.
/// </summary>
public sealed record VoiceTenantProfileDto
{
    public required int TenantId { get; init; }
    public required string BusinessType { get; init; }   // clinic|dental_clinic|aesthetic_clinic|hair_transplant|ecommerce|service_business|education|real_estate|generic
    public required string DefaultLanguage { get; init; } // e.g. "tr-TR"

    // Override layer — null = inherit business_type default (resolved in VoiceRuntime).
    public string? VoiceBrandTone { get; init; }
    public string? PrimaryGoal { get; init; }
    public string? SecondaryGoal { get; init; }
    public bool? PricingEnabled { get; init; }
    public bool? AppointmentEnabled { get; init; }
    public bool? OrderLookupEnabled { get; init; }
    public bool? LeadCaptureEnabled { get; init; }
    public bool? HumanHandoffEnabled { get; init; }
    public bool? SensitiveTopicsEnabled { get; init; }
}
