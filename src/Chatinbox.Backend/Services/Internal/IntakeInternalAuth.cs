namespace Chatinbox.Backend.Services.Internal;

/// <summary>
/// FEAT-LIW Chunk B: peer-service identity gate for the wa-direct internal
/// endpoint (<c>POST /api/internal/leads/intake/wa-direct</c>). Uses the
/// canonical peer-service auth pattern (<c>X-Internal-Service-Token</c> header +
/// constant-time compare against <c>InternalServices:SharedSecret</c>) so
/// service-to-service calls share one rotation story.
/// Iter 2 reinforcement: <c>/api/internal/</c> is REQUIRED by
/// <c>UseJwtAuth</c> as well — this gate is the SECOND layer running
/// AFTER the JWT middleware sets the tenant context, not in place of it.
/// The two layers cover different risks: JWT proves the request is bound to
/// a specific tenant_id (signed claim); this gate proves the caller is a
/// peer Invekto service (only services hold the shared secret), so a leaked
/// tenant Dashboard JWT alone cannot reach this write path.
/// </summary>
public static class IntakeInternalAuth
{
    public const string HeaderName = "X-Internal-Service-Token";
    public const string ConfigKey = "InternalServices:SharedSecret";

    /// <summary>
    /// Validates the internal-service token. Returns (Ok, StatusCode, Reason).
    /// 500 when the operator forgot to configure the shared secret (refuse to
    /// accept traffic in an undefined-trust state); 401 when the header is
    /// missing/empty; 403 when the header value mismatches. All non-Ok paths
    /// share <c>INV-BE-110</c> at the call site.
    /// </summary>
    public static (bool Ok, int StatusCode, string Reason) Validate(
        HttpContext ctx, IConfiguration config)
    {
        var expected = config[ConfigKey];
        if (string.IsNullOrWhiteSpace(expected))
            return (false, 500, $"Internal service auth not configured (missing {ConfigKey}).");

        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var provided) || provided.Count == 0)
            return (false, 401, $"Missing {HeaderName} header.");

        var supplied = provided[0];
        if (string.IsNullOrEmpty(supplied) || !SlowEquals(supplied, expected))
            return (false, 403, $"Invalid {HeaderName}.");

        return (true, 200, string.Empty);
    }

    /// <summary>
    /// Length-leaking-but-content-blind comparison. Both sides are operator-set
    /// strings of identical length once a tenant has been onboarded; an attacker
    /// observing length information learns nothing they couldn't observe by
    /// reading the deployment manifest.
    /// </summary>
    private static bool SlowEquals(string? a, string b)
    {
        if (a is null) return false;
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
