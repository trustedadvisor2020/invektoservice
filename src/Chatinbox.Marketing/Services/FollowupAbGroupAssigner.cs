using System.Security.Cryptography;
using System.Text;

namespace Chatinbox.Marketing.Services;

/// <summary>
/// Deterministic A/B group assignment for FEAT-EFS. The same (tenant, lead, sequence)
/// triple ALWAYS yields the same group, so re-running the orchestrator after Hangfire
/// restarts or duplicate trigger calls does not contaminate the experiment cohort.
///
/// Algorithm:
///   1. Build the seed string "<tenant_id>|<lead_id>|<sequence_id>".
///   2. Compute SHA-256 of the UTF-8 encoded seed.
///   3. Take the first 8 bytes, interpret as big-endian uint64.
///   4. Modulo 100 → bucket [0, 99]. bucket &lt; ab_split_percent → "drip", else "control".
///
/// Ties are deterministic (always go to the same group for the same input). Picking
/// SHA-256 over MD5/CRC32 is for cryptographic uniformity in the bucket distribution —
/// not for security; the seed is non-secret tenant/lead identity.
/// </summary>
public static class FollowupAbGroupAssigner
{
    /// <summary>Drip group label (matches DB CHECK constraint).</summary>
    public const string Drip = "drip";

    /// <summary>Control group label (matches DB CHECK constraint).</summary>
    public const string Control = "control";

    /// <summary>
    /// Returns "drip" if the deterministic bucket falls below <paramref name="abSplitPercent"/>,
    /// otherwise "control". <paramref name="abSplitPercent"/> is clamped to [0, 100] for safety.
    /// </summary>
    public static string Assign(int tenantId, long leadId, long sequenceId, int abSplitPercent)
    {
        var clamped = Math.Clamp(abSplitPercent, 0, 100);
        var bucket = ComputeBucket(tenantId, leadId, sequenceId);
        return bucket < clamped ? Drip : Control;
    }

    /// <summary>
    /// Exposed for unit tests + Codex CoVe Q3 reproducibility verification. Returns the
    /// bucket index in [0, 99].
    /// </summary>
    public static int ComputeBucket(int tenantId, long leadId, long sequenceId)
    {
        var seed = $"{tenantId}|{leadId}|{sequenceId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));

        // First 8 bytes as big-endian uint64 — independent of platform endianness.
        ulong value = 0;
        for (var i = 0; i < 8; i++)
            value = (value << 8) | hash[i];

        return (int)(value % 100UL);
    }
}
