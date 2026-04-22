using System.Text.RegularExpressions;
using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Campaigns;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// FEAT-MCC: applies <c>{{campaign.X}}</c> substitution + the outbound active-window guard
/// to a message about to be dispatched. Single helper so AutomationOrchestrator's
/// SendCallbackAsync stays lean and the same logic is unit-testable in isolation.
///
/// <para>
/// Detection: the regex matches <c>{{campaign.[a-z_]+}}</c> tokens. Messages without any
/// such token go through unchanged + bypass the window guard (campaign-agnostic outbound
/// is unaffected by this paket per Q's interview Q6 answer).
/// </para>
///
/// <para>
/// Window guard: when at least one <c>{{campaign.X}}</c> token is present AND the resolver
/// reports no active campaign in window for the tenant, returns <see cref="Skip"/>=true.
/// AutomationOrchestrator logs INV-BE-119 + drops the dispatch (returns false from
/// SendCallbackAsync caller chain).
/// </para>
///
/// <para>
/// Substitution: each token is replaced via <see cref="ITenantCampaignResolver.RenderPlaceholderAsync"/>.
/// Locale is taken from <c>leads.preferred_locale</c> (HFM-2 sticky resolver) when available;
/// otherwise defaults to "en" so cities_human renders English connector ("and"). The lead.custom_1
/// city override is plumbed through but the chat-send hot path passes <c>null</c> for the
/// initial implementation — the resolver falls back to the campaign's first dates[] entry,
/// which matches the spec AC-2 / pilot smoke S7 PASS bar. A follow-up paket can enrich the
/// hot path with the lead row to enable strict lead.custom_1 → city date selection (interview
/// Q7 answer is implemented at the resolver layer; this caller just doesn't pass leadCity yet).
/// </para>
/// </summary>
public sealed class CampaignTemplateApplier
{
    private static readonly Regex CampaignPlaceholderPattern = new(
        @"\{\{campaign\.([a-z_]+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private readonly ITenantCampaignResolver _resolver;
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _log;

    public CampaignTemplateApplier(
        ITenantCampaignResolver resolver,
        AutomationRepository repo,
        JsonLinesLogger log)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Apply campaign substitution + window guard.
    ///
    /// Returns:
    /// <list type="bullet">
    ///   <item><c>NoOp</c> when text contains no <c>{{campaign.X}}</c> tokens — original text returned unchanged.</item>
    ///   <item><c>Skip</c> when text is campaign-bound but no in-window active campaign exists. Caller must NOT dispatch.</item>
    ///   <item><c>Substituted</c> when tokens were rendered successfully. Use <see cref="CampaignApplyResult.RenderedText"/>.</item>
    /// </list>
    /// </summary>
    public async Task<CampaignApplyResult> ApplyAsync(
        int tenantId,
        string? phone,
        string text,
        DateTimeOffset nowUtc,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return CampaignApplyResult.NoOp(text ?? string.Empty);

        var matches = CampaignPlaceholderPattern.Matches(text);
        if (matches.Count == 0)
            return CampaignApplyResult.NoOp(text);

        // Window guard fires once per message regardless of how many tokens are present.
        // We pass campaignSlug=null so the resolver evaluates "any active campaign covers NOW".
        var inWindow = await _resolver.IsWithinWindowAsync(tenantId, campaignSlug: null, nowUtc, ct)
            .ConfigureAwait(false);
        if (!inWindow)
        {
            _log.StepWarn(
                $"[{ErrorCodes.CampaignWindowClosed}] CampaignTemplateApplier blocked outbound: tenant={tenantId} phone={phone ?? "-"} placeholders={matches.Count} reason=no_active_campaign_in_window",
                $"mcc:{tenantId}:{phone ?? "-"}");
            return CampaignApplyResult.Skip(ErrorCodes.CampaignWindowClosed);
        }

        // Locale + leadCity for substitution context. Locale from HFM-2 sticky resolver,
        // leadCity null for the chat-send hot path (see XML doc).
        var locale = !string.IsNullOrWhiteSpace(phone)
            ? await _repo.GetLeadPreferredLocaleAsync(tenantId, phone!, ct).ConfigureAwait(false)
            : null;
        var resolvedLocale = string.IsNullOrEmpty(locale) ? "en" : locale!;

        // Substitute each match. Regex.Replace doesn't have an async overload; collect
        // resolved values up-front (deduplicated by token) and inline-replace afterwards.
        var distinctTails = matches.Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rendered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tail in distinctTails)
        {
            var value = await _resolver.RenderPlaceholderAsync(
                tenantId, tail, campaignSlug: null, leadCity: null, resolvedLocale, nowUtc, ct)
                .ConfigureAwait(false);
            rendered[tail] = value ?? string.Empty;
        }

        var substituted = CampaignPlaceholderPattern.Replace(text, m =>
        {
            var tail = m.Groups[1].Value;
            return rendered.TryGetValue(tail, out var v) ? v : string.Empty;
        });

        return CampaignApplyResult.Substituted(substituted);
    }

    /// <summary>Quick predicate for callers that want to early-exit without an async call
    /// (e.g. logging conditions). Mirrors the regex used in <see cref="ApplyAsync"/>.</summary>
    public static bool ContainsCampaignPlaceholder(string? text)
        => !string.IsNullOrEmpty(text) && CampaignPlaceholderPattern.IsMatch(text);
}

/// <summary>FEAT-MCC: outcome of a single <see cref="CampaignTemplateApplier.ApplyAsync"/> call.</summary>
public sealed record CampaignApplyResult(bool ShouldSkip, string RenderedText, string? ErrorCode)
{
    public static CampaignApplyResult NoOp(string text)
        => new(false, text, null);

    public static CampaignApplyResult Substituted(string text)
        => new(false, text, null);

    public static CampaignApplyResult Skip(string errorCode)
        => new(true, string.Empty, errorCode);
}
