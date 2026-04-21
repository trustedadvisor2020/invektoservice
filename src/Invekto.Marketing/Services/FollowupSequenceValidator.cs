using System.Text.RegularExpressions;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Followup;

namespace Invekto.Marketing.Services;

/// <summary>
/// Validates a <see cref="FollowupSequenceConfig"/> against the EFS contract caps. Test
/// mode (<paramref name="testMode"/>) flips the unit interpretation: stage delays are
/// minutes when TRUE, days when FALSE — but the cap (max 30) and stage count (max 5)
/// are identical because the smoke needs to exercise the same boundaries quickly.
///
/// Throws <see cref="FollowupSequenceValidationException"/> with a typed INV-MK code so
/// endpoint handlers map to the right HTTP status without string-matching.
/// </summary>
public static class FollowupSequenceValidator
{
    /// <summary>
    /// Slug shape — must start with a lowercase alphanumeric and contain only
    /// <c>[a-z0-9_-]</c>. Mirrors the Postgres unique constraint without imposing extra
    /// reserved words; tenants pick their own taxonomy.
    /// </summary>
    private static readonly Regex SlugPattern = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled);

    /// <summary>
    /// Template slug shape — same charset/length as sequence slug. Distinct constant for
    /// readability.
    /// </summary>
    private static readonly Regex TemplateSlugPattern = new("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.Compiled);

    /// <summary>Maximum stage count per sequence (spec §7 mitigation).</summary>
    public const int MaxStages = 5;

    /// <summary>Maximum cumulative window — days when test mode false, minutes when true.</summary>
    public const int MaxWindow = 30;

    /// <summary>
    /// Validates a sequence config. Caller has already authenticated tenant scope.
    /// </summary>
    public static void Validate(FollowupSequenceConfig config, bool testMode)
    {
        // Slug shape — required.
        if (string.IsNullOrWhiteSpace(config.Slug) || !SlugPattern.IsMatch(config.Slug))
            throw new FollowupSequenceValidationException(
                ErrorCodes.FollowupSequenceConfigInvalid,
                "Sequence slug gecersiz: lowercase harf/rakam ile baslamali, [a-z0-9_-] icerebilir, max 64 karakter (ornek: 'post-roadshow').");

        // AB split bounds.
        if (config.AbSplitPercent < 0 || config.AbSplitPercent > 100)
            throw new FollowupSequenceValidationException(
                ErrorCodes.FollowupSequenceConfigInvalid,
                $"ab_split_percent 0-100 araliginda olmali (gelen: {config.AbSplitPercent}).");

        // Stage count.
        if (config.Stages == null || config.Stages.Count == 0)
            throw new FollowupSequenceValidationException(
                ErrorCodes.FollowupSequenceConfigInvalid,
                "stages bos olamaz: en az 1 stage tanimlayin.");
        if (config.Stages.Count > MaxStages)
            throw new FollowupSequenceValidationException(
                ErrorCodes.FollowupSequenceTooLong,
                $"Followup sequence cap asimi: max {MaxStages} stage. Mevcut: {config.Stages.Count} stage. Stage sayisini dusurun.");

        // Per-stage validation + cumulative window.
        var unitLabel = testMode ? "dakika" : "gun";
        var cumulative = 0;
        for (var i = 0; i < config.Stages.Count; i++)
        {
            var stage = config.Stages[i];
            if (stage.DelayDays <= 0)
                throw new FollowupSequenceValidationException(
                    ErrorCodes.FollowupSequenceConfigInvalid,
                    $"Stage {i}: delay_days 0'dan buyuk olmali (gelen: {stage.DelayDays} {unitLabel}).");
            if (stage.DelayDays > MaxWindow)
                throw new FollowupSequenceValidationException(
                    ErrorCodes.FollowupSequenceTooLong,
                    $"Stage {i}: delay_days max {MaxWindow} {unitLabel} olabilir (gelen: {stage.DelayDays} {unitLabel}).");

            cumulative += stage.DelayDays;
            if (cumulative > MaxWindow)
                throw new FollowupSequenceValidationException(
                    ErrorCodes.FollowupSequenceTooLong,
                    $"Followup sequence cap asimi: max {MaxWindow} {unitLabel} toplam pencere. Mevcut: {cumulative} {unitLabel} (stage {i} sonunda). Delay degerlerini dusurun.");

            if (string.IsNullOrWhiteSpace(stage.TemplateSlug) || !TemplateSlugPattern.IsMatch(stage.TemplateSlug))
                throw new FollowupSequenceValidationException(
                    ErrorCodes.FollowupSequenceConfigInvalid,
                    $"Stage {i}: template_slug gecersiz. Lowercase harf/rakam ile baslamali, [a-z0-9_-] icerebilir, max 64 karakter.");
        }
    }
}

/// <summary>
/// Typed exception so endpoint handlers can <c>catch (FollowupSequenceValidationException ex)</c>
/// and map ex.ErrorCode → HTTP status without parsing message strings (lessons 2026-04-21
/// FEAT-TFM iter 1: avoid string-matching in catch blocks).
/// </summary>
public sealed class FollowupSequenceValidationException : Exception
{
    public string ErrorCode { get; }

    public FollowupSequenceValidationException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }
}
