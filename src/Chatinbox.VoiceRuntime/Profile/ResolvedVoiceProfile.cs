using System.Linq;
using Chatinbox.Shared.DTOs.Voice;

namespace Chatinbox.VoiceRuntime.Profile;

/// <summary>
/// FEAT-VFB F-VR-B Chunk B2: the fully RESOLVED voice profile fed to InstructionsBuilder. Merges the
/// Backend-stored <see cref="VoiceTenantProfileDto"/> (business_type + nullable capability/goal/tone
/// overrides) with the code-versioned defaults:
///   - business_type string → <see cref="BusinessType"/> enum (unknown → Generic)
///   - <see cref="SectorAdapter"/> = language layer (vocabulary/banned/lead fields)
///   - <see cref="CapabilityFlags"/> = per-flag (dto.Flag ?? CapabilityDefaults.For(type).Flag)
///   - brand tone + goals = sanitized non-null DTO values (admin-controlled VARCHAR(40))
///
/// Graceful: <see cref="Resolve"/>(null) — when the profile fetch degraded (INV-VR-024) — produces a
/// Generic profile with generic defaults and no tone/goals, so the voice session never breaks.
/// </summary>
public sealed record ResolvedVoiceProfile(
    BusinessType BusinessType,
    SectorAdapter Adapter,
    CapabilityFlags Capabilities,
    string? BrandTone,
    string? PrimaryGoal,
    string? SecondaryGoal)
{
    private const int MaxOverrideLength = 60;

    public static ResolvedVoiceProfile Resolve(VoiceTenantProfileDto? dto)
    {
        var businessType = BusinessTypeParser.Parse(dto?.BusinessType);
        var adapter = SectorAdapter.For(businessType);
        var defaults = CapabilityDefaults.For(businessType);

        // Per-flag resolution: a non-null admin override replaces the default; NULL inherits.
        var capabilities = new CapabilityFlags(
            PricingEnabled: dto?.PricingEnabled ?? defaults.PricingEnabled,
            AppointmentEnabled: dto?.AppointmentEnabled ?? defaults.AppointmentEnabled,
            OrderLookupEnabled: dto?.OrderLookupEnabled ?? defaults.OrderLookupEnabled,
            LeadCaptureEnabled: dto?.LeadCaptureEnabled ?? defaults.LeadCaptureEnabled,
            HumanHandoffEnabled: dto?.HumanHandoffEnabled ?? defaults.HumanHandoffEnabled,
            SensitiveTopicsEnabled: dto?.SensitiveTopicsEnabled ?? defaults.SensitiveTopicsEnabled);

        return new ResolvedVoiceProfile(
            businessType,
            adapter,
            capabilities,
            Sanitize(dto?.VoiceBrandTone),
            Sanitize(dto?.PrimaryGoal),
            Sanitize(dto?.SecondaryGoal));
    }

    /// <summary>
    /// Defensive sanitize for admin-controlled override strings before they enter the LLM prompt:
    /// strip control chars / newlines (prompt-injection guard), trim, length-cap. Blank → null
    /// (= "do not inject this line"). Low injection risk (ops-admin trusted VARCHAR(40)) but defensive.
    /// </summary>
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0) return null;
        return cleaned.Length > MaxOverrideLength ? cleaned[..MaxOverrideLength] : cleaned;
    }
}
