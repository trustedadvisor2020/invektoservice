namespace Invekto.Shared.Services;

/// <summary>
/// Per-(tenant,user) feature flag service. Source of truth: INMA 'InseFeatures' JWT claim.
/// Cached server-side after token exchange/login (5 min TTL by default).
///
/// Usage:
///   - Auth pipeline: after validating an inma JWT, call SetFeatures(...) so subsequent
///     requests can check IsEnabled(...) without re-decoding the token.
///   - Feature gates: any service injects IFeatureFlagService and calls IsEnabled(tenantId, userId, flag).
///
/// If features were never set for a (tenant,user) — IsEnabled returns false (closed by default).
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>Check whether a feature flag is enabled for the given user.</summary>
    bool IsEnabled(int tenantId, int userId, string flag);

    /// <summary>Cache the licensed feature list for this (tenant,user). Overwrites prior entry.</summary>
    void SetFeatures(int tenantId, int userId, IReadOnlyCollection<string> features, TimeSpan? ttl = null);

    /// <summary>Drop the cached feature list (e.g. on logout / explicit invalidation).</summary>
    void Invalidate(int tenantId, int userId);
}
