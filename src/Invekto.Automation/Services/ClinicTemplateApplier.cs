using System.Text.RegularExpressions;
using Invekto.Shared.Contracts.ClinicMetadata;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// FEAT-CLINIC-METADATA: applies <c>{{clinic.X}}</c> + <c>{{team.role.field}}</c>
/// substitution to a message about to be dispatched. Single helper so
/// AutomationOrchestrator's <c>SendCallbackAsync</c> stays lean and the substitution
/// is unit-testable in isolation.
///
/// <para>
/// Detection: two regexes match <c>{{clinic.[a-z_]+}}</c> and
/// <c>{{team.[a-z_]+\.[a-z_]+}}</c> tokens independently. Messages without any such
/// token go through unchanged (fast no-op early-exit). Existing
/// <see cref="CampaignTemplateApplier"/> behaviour is UNTOUCHED — this applier is
/// additive in the AutomationOrchestrator pipeline (see plan
/// <c>spec_architectural_decisions.AD1</c>).
/// </para>
///
/// <para>
/// Substitution: <c>{{clinic.X}}</c> resolves via <see cref="ClinicContactDto.Resolve"/>;
/// <c>{{team.role.field}}</c> resolves via <see cref="ClinicMetadata.ResolveTeamMemberField"/>
/// (case-insensitive role match, JSONB array first-match — multi-role index syntax
/// {{team.dentist[0].name}} is intentionally excluded for the pilot, see plan
/// <c>intentional_exclusions</c>). Unknown keys / missing roles render to empty
/// string so the message text never carries an unresolved literal token downstream.
/// </para>
///
/// <para>
/// Failure semantics: resolver fail (DB transient, malformed JSONB) returns
/// <see cref="ClinicMetadata.Empty"/> + WARN log under INV-BE-125. Empty metadata
/// turns substitution into a graceful empty-string fill — outbound flow continues
/// for clinic-agnostic messages, operator sees an empty Dashboard which signals
/// the issue.
/// </para>
/// </summary>
public sealed class ClinicTemplateApplier
{
    private static readonly Regex ClinicPlaceholderPattern = new(
        @"\{\{clinic\.([a-z_]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private static readonly Regex TeamPlaceholderPattern = new(
        @"\{\{team\.([a-z_]+)\.([a-z_]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private readonly ITenantClinicMetadataResolver _resolver;
    private readonly JsonLinesLogger _log;

    public ClinicTemplateApplier(
        ITenantClinicMetadataResolver resolver,
        JsonLinesLogger log)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Apply clinic + team substitution to <paramref name="text"/>. Returns the rendered
    /// text (unchanged when no <c>{{clinic.*}}</c> or <c>{{team.*}}</c> token is present).
    ///
    /// Never throws — resolver fail surfaces as empty metadata + WARN log; substitution
    /// renders empty strings for affected tokens, preserving outbound flow.
    /// </summary>
    public async Task<string> ApplyAsync(
        int tenantId,
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var clinicMatches = ClinicPlaceholderPattern.Matches(text);
        var teamMatches   = TeamPlaceholderPattern.Matches(text);
        if (clinicMatches.Count == 0 && teamMatches.Count == 0)
            return text;

        var metadata = await _resolver.GetAsync(tenantId, ct).ConfigureAwait(false);

        // Substitute clinic.X tokens first. Regex.Replace doesn't have an async overload
        // but the resolver call is single (covers both namespaces) so a synchronous
        // replace pass after the await is correct + lock-free.
        var rendered = ClinicPlaceholderPattern.Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            return metadata.Contact.Resolve(key);
        });

        rendered = TeamPlaceholderPattern.Replace(rendered, m =>
        {
            var role  = m.Groups[1].Value;
            var field = m.Groups[2].Value;
            return metadata.ResolveTeamMemberField(role, field);
        });

        return rendered;
    }

    /// <summary>Quick predicate for callers that want to early-exit without an async call
    /// (e.g. logging conditions). Mirrors the regexes used in <see cref="ApplyAsync"/>.</summary>
    public static bool ContainsClinicOrTeamPlaceholder(string? text)
        => !string.IsNullOrEmpty(text)
           && (ClinicPlaceholderPattern.IsMatch(text) || TeamPlaceholderPattern.IsMatch(text));
}
