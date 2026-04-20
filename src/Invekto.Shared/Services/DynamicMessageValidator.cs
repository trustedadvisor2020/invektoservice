using System.Text.RegularExpressions;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.TenantFieldMapping;

namespace Invekto.Shared.Services;

/// <summary>
/// FEAT-DMP send-time validator (AC-8). Scans MessageText for {{placeholder}} tokens,
/// optionally resolves each through <see cref="ITenantFieldMappingResolver"/>, and
/// checks the resolved set against <see cref="InmaDynamicFieldKeys.Allowlist"/>.
///
/// Return semantics:
/// - <c>HasPlaceholders=false</c> → MessageText is plain, caller sends without DynamicMessage.
/// - <c>HasPlaceholders=true &amp;&amp; IsValid=true</c> → caller sets DynamicFields to
///   <see cref="DynamicMessageValidationResult.InmaFieldKeys"/> and forwards to INMA.
/// - <c>HasPlaceholders=true &amp;&amp; IsValid=false</c> → caller rejects send (logs
///   INV-OB-033 with the <see cref="DynamicMessageValidationResult.UnknownPlaceholders"/>).
/// </summary>
public sealed class DynamicMessageValidator
{
    private readonly ITenantFieldMappingResolver _resolver;

    // {{placeholder}} — whitespace-tolerant, alphanumerics + underscore only (so false matches
    // on patterns like {{"not a placeholder"}} are naturally excluded).
    private static readonly Regex PlaceholderPattern = new(
        @"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public DynamicMessageValidator(ITenantFieldMappingResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<DynamicMessageValidationResult> ValidateAsync(
        int tenantId,
        string? messageText,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(messageText))
            return DynamicMessageValidationResult.NoPlaceholders;

        var matches = PlaceholderPattern.Matches(messageText);
        if (matches.Count == 0)
            return DynamicMessageValidationResult.NoPlaceholders;

        var inmaKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();

        foreach (Match m in matches)
        {
            var raw = m.Groups[1].Value;

            // 1) Try TFM semantic resolve (FEAT-TFM forward-compat; NullResolver returns null today).
            var resolved = await _resolver.ResolveToInmaKeyAsync(tenantId, raw, ct);
            var candidate = resolved ?? raw;

            // 2) Allowlist check against INMA contract.
            if (InmaDynamicFieldKeys.Allowlist.Contains(candidate))
                inmaKeys.Add(candidate.ToLowerInvariant());
            else
                unknown.Add(raw);
        }

        return new DynamicMessageValidationResult(
            hasPlaceholders: true,
            isValid: unknown.Count == 0,
            inmaFieldKeys: inmaKeys.ToArray(),
            unknownPlaceholders: unknown);
    }
}

/// <summary>
/// Outcome of <see cref="DynamicMessageValidator.ValidateAsync"/>. Immutable.
/// </summary>
public sealed class DynamicMessageValidationResult
{
    public bool HasPlaceholders { get; }
    public bool IsValid { get; }
    public IReadOnlyList<string> InmaFieldKeys { get; }
    public IReadOnlyList<string> UnknownPlaceholders { get; }

    internal DynamicMessageValidationResult(
        bool hasPlaceholders,
        bool isValid,
        IReadOnlyList<string> inmaFieldKeys,
        IReadOnlyList<string> unknownPlaceholders)
    {
        HasPlaceholders = hasPlaceholders;
        IsValid = isValid;
        InmaFieldKeys = inmaFieldKeys;
        UnknownPlaceholders = unknownPlaceholders;
    }

    public static readonly DynamicMessageValidationResult NoPlaceholders =
        new(hasPlaceholders: false, isValid: true, Array.Empty<string>(), Array.Empty<string>());
}
