namespace Invekto.Shared.Services;

/// <summary>
/// Deterministic template variant selector for A/B rotation.
///
/// Rotation strategy: stable hash of (contactKey + nodeId) modulo variant count.
/// Same contact sees the same variant for the same node on every visit — prevents
/// intra-session inconsistency ("am I talking to two different bots?").
///
/// Pure, stateless; no DB, no clock, no randomness. Safe as a singleton.
/// </summary>
public interface ITemplateRotationService
{
    /// <summary>
    /// Pick a variant index in [0, variantCount).
    /// Returns 0 when variantCount &lt;= 1.
    /// When <paramref name="contactKey"/> is null/empty, callers SHOULD pass a stable
    /// per-flow fallback (e.g. "flow:{flowId}") so unknown contacts vary by flow instead of
    /// collapsing to a single variant per node. Selection is deterministic regardless.
    /// </summary>
    int PickVariantIndex(string? contactKey, string nodeId, int variantCount);
}
