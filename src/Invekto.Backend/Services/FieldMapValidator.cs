using Invekto.Shared.Constants;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW Chunk C: pure validation helper for the Dashboard field_map editor.
/// Input shape: { source_field_name -> canonical_field_name } (direction mirrors
/// the UI's natural authoring order — tenants think "my form has 'ad_soyad',
/// map it to 'name'" — NOT the JSONB storage direction which is inverse).
/// Returns a <see cref="ValidationResult"/> (never throws) so the caller can
/// present every failing row in one 400 response.
///
/// Rules (all enforced at save time; runtime rejects at intake but that's too
/// late for UX): (1) phone canonical mapped, (2) consent canonical mapped,
/// (3) canonical value in allowlist, (4) non-empty source field string,
/// (5) unique canonical target except 'metadata'.
/// </summary>
public sealed class FieldMapValidator
{
    /// <summary>Canonical fields a tenant can map to via the Dashboard UI.</summary>
    public static readonly IReadOnlySet<string> AllowedCanonicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "name", "phone", "email", "consent",
        "utm_source", "utm_medium", "utm_campaign", "utm_content", "utm_term",
        "referer", "metadata"
    };

    /// <summary>Canonicals that must be mapped (UI blocks save otherwise).</summary>
    public static readonly IReadOnlyCollection<string> RequiredCanonicals = new[] { "phone", "consent" };

    /// <summary>Canonical targets that allow multiple source rows (others must be unique).</summary>
    public static readonly IReadOnlySet<string> DuplicateAllowedCanonicals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "metadata"
    };

    public ValidationResult Validate(IDictionary<string, string>? sourceToCanonical)
    {
        var result = new ValidationResult();
        if (sourceToCanonical == null || sourceToCanonical.Count == 0)
        {
            foreach (var req in RequiredCanonicals)
            {
                result.Errors.Add(new ValidationError(
                    SourceField: "",
                    CanonicalField: req,
                    ErrorCode: ErrorCodes.LeadIntakeFieldMapRequiredMissing,
                    Message: $"Zorunlu canonical alan '{req}' icin en az bir satir gerekli."));
            }
            return result;
        }

        var canonicalCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in sourceToCanonical)
        {
            var source = kvp.Key;
            var canonical = kvp.Value;

            if (string.IsNullOrWhiteSpace(source))
            {
                result.Errors.Add(new ValidationError(
                    SourceField: source ?? "",
                    CanonicalField: canonical,
                    ErrorCode: ErrorCodes.LeadIntakeFieldMapRequiredMissing,
                    Message: "Kaynak alan adi bos birakilamaz."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(canonical))
            {
                result.Errors.Add(new ValidationError(
                    SourceField: source,
                    CanonicalField: canonical ?? "",
                    ErrorCode: ErrorCodes.LeadIntakeFieldMapRequiredMissing,
                    Message: "Canonical alan secilmedi."));
                continue;
            }

            if (!AllowedCanonicals.Contains(canonical))
            {
                result.Errors.Add(new ValidationError(
                    SourceField: source,
                    CanonicalField: canonical,
                    ErrorCode: ErrorCodes.LeadIntakeFieldMapUnknownCanonical,
                    Message: $"Tanimsiz canonical alan '{canonical}'. Gecerli alanlar: {string.Join(", ", AllowedCanonicals)}."));
                continue;
            }

            if (canonicalCounts.TryGetValue(canonical, out var existing))
                canonicalCounts[canonical] = existing + 1;
            else
                canonicalCounts[canonical] = 1;
        }

        foreach (var (canonical, count) in canonicalCounts)
        {
            if (count > 1 && !DuplicateAllowedCanonicals.Contains(canonical))
            {
                result.Errors.Add(new ValidationError(
                    SourceField: "",
                    CanonicalField: canonical,
                    ErrorCode: ErrorCodes.LeadIntakeFieldMapRequiredMissing,
                    Message: $"Canonical alan '{canonical}' birden fazla kaynak alana atanamaz."));
            }
        }

        foreach (var required in RequiredCanonicals)
        {
            if (!canonicalCounts.ContainsKey(required))
            {
                result.Errors.Add(new ValidationError(
                    SourceField: "",
                    CanonicalField: required,
                    ErrorCode: ErrorCodes.LeadIntakeFieldMapRequiredMissing,
                    Message: $"Zorunlu canonical alan '{required}' icin en az bir satir gerekli."));
            }
        }

        return result;
    }
}

public sealed class ValidationResult
{
    public List<ValidationError> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public sealed record ValidationError(
    string SourceField,
    string CanonicalField,
    string ErrorCode,
    string Message);
