using System.Security.Cryptography;
using System.Text;

namespace Invekto.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: X-Hub-Signature-256 HMAC-SHA256 verifier for Meta Lead Ads
/// webhook POST bodies. Meta's spec (developers.facebook.com/docs/graph-api/
/// webhooks/getting-started#validating-payloads) emits a header of the form
/// <c>X-Hub-Signature-256: sha256=&lt;hex-digest&gt;</c> where the digest is
/// computed over the raw request body using the App Secret as HMAC key.
///
/// This class verifies the signature in constant time via
/// <see cref="CryptographicOperations.FixedTimeEquals"/> to neutralize the
/// timing side-channel an attacker could otherwise use to forge signatures
/// byte-by-byte. Parser is defensive: malformed header / wrong prefix / odd
/// hex length / non-hex chars all return false without throwing.
///
/// Raw body MUST be captured verbatim BEFORE any JSON binding (ASP.NET Core
/// body re-parse would lose byte-level fidelity for re-HMAC). The endpoint
/// layer does this via <c>EnableBuffering</c> + manual ReadAsByteArrayAsync
/// on the request stream.
/// </summary>
public static class MetaLeadgenSignatureValidator
{
    private const string SignaturePrefix = "sha256=";
    private const int Sha256HexLength = 64; // 32 bytes * 2

    /// <summary>
    /// Returns true iff <paramref name="signatureHeader"/> matches the HMAC-SHA256
    /// digest of <paramref name="rawBody"/> under <paramref name="appSecret"/>.
    /// All false returns are uniform from the caller's perspective — no
    /// differentiated error codes here so a timing attacker cannot learn which
    /// branch rejected them (header-missing vs hex-malformed vs digest-mismatch).
    /// </summary>
    public static bool Verify(string? appSecret, byte[] rawBody, string? signatureHeader)
    {
        // Zero-length or missing app_secret => configuration gap, reject all posts.
        // (The endpoint layer surfaces a distinct diagnostic so ops can differentiate
        // "tenant never onboarded" from "attacker with wrong key" via the event audit.)
        if (string.IsNullOrEmpty(appSecret)) return false;
        if (rawBody is null) return false;
        if (string.IsNullOrEmpty(signatureHeader)) return false;
        if (!signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal)) return false;

        var hex = signatureHeader.AsSpan(SignaturePrefix.Length);
        if (hex.Length != Sha256HexLength) return false;

        Span<byte> providedBytes = stackalloc byte[32];
        if (!TryParseHex(hex, providedBytes)) return false;

        Span<byte> computedBytes = stackalloc byte[32];
        var keyBytes = Encoding.UTF8.GetBytes(appSecret);
        using (var hmac = new HMACSHA256(keyBytes))
        {
            if (!hmac.TryComputeHash(rawBody, computedBytes, out var written) || written != 32)
                return false;
        }

        return CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes);
    }

    /// <summary>
    /// Branchless (per-char) hex parser. Reads into <paramref name="destination"/>
    /// if and only if the entire input is valid [0-9a-fA-F]; otherwise false.
    /// Deliberately does not throw — keeps timing uniform and caller contract clean.
    /// </summary>
    private static bool TryParseHex(ReadOnlySpan<char> hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2) return false;
        for (int i = 0; i < destination.Length; i++)
        {
            var hi = HexNibble(hex[i * 2]);
            var lo = HexNibble(hex[i * 2 + 1]);
            if (hi < 0 || lo < 0) return false;
            destination[i] = (byte)((hi << 4) | lo);
        }
        return true;
    }

    private static int HexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };
}
