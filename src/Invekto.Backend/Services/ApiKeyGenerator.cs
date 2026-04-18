using System.Security.Cryptography;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW Chunk C: cryptographically secure generator for tenant landing API
/// keys. Output format: 'liw_live_' prefix + URL-safe base64 entropy, truncated
/// to a total length of <see cref="TotalLength"/> chars (64). The prefix is a
/// human hint — at-a-glance grep-ability in logs/configs, and reserves
/// 'liw_test_' for a future sandbox mode without format churn.
///
/// URL-safe transform: standard base64 '+' -> '-', '/' -> '_', '=' padding
/// stripped. 48 random bytes -> 64 base64 chars; after prefix (9) + truncation,
/// entropy is still ~55 base64 chars = ~330 bits, well above collision-
/// meaningful thresholds for a per-tenant registry of max tens of thousands.
/// </summary>
public class ApiKeyGenerator
{
    public const string Prefix = "liw_live_";
    public const int TotalLength = 64;

    public virtual string Generate()
    {
        // 48 bytes -> 64 base64 chars. After URL-safe transform + prefix we
        // truncate to TotalLength so the length invariant holds regardless of
        // how many '=' pad chars base64 produced (always 0 for 48 bytes, but
        // encoding this way keeps the method robust if the byte count changes).
        var bytes = RandomNumberGenerator.GetBytes(48);
        var b64 = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", string.Empty);

        var candidate = Prefix + b64;
        if (candidate.Length >= TotalLength)
            return candidate.Substring(0, TotalLength);
        // Shouldn't happen with 48 bytes, but pad defensively so callers never
        // see a short key.
        return candidate.PadRight(TotalLength, '0');
    }

    /// <summary>
    /// Mask a full API key for display — '{Prefix}****{last4}'. Returns null
    /// when the input is null/empty/too short (Dashboard renders 'not initialized').
    /// </summary>
    public static string? Mask(string? key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 4) return null;
        var last4 = key.Substring(key.Length - 4);
        return $"{Prefix}****{last4}";
    }
}
