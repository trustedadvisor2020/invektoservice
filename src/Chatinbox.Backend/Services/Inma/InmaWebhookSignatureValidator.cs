using System.Security.Cryptography;
using System.Text;

namespace Chatinbox.Backend.Services.Inma;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C2 — fail-closed HMAC-SHA256 verifier for INMA's signed
/// <c>customer.selection_changed</c> system events. The signature covers the exact string
/// <c>{X-Invekto-Timestamp}.{rawBody}</c> (timestamp ASCII + '.' + the VERBATIM body bytes —
/// never a reserialized JSON re-encoding) keyed by the per-tenant secret.
///
/// Modeled on <c>MetaLeadgenSignatureValidator</c>: constant-time compare via
/// <see cref="CryptographicOperations.FixedTimeEquals"/>, defensive parsing that never throws,
/// raw body captured BEFORE any JSON binding. Because the exact digest encoding is not yet
/// documented, it can try base64 then hex (verify-live) and reports which matched so ops can
/// lock the config once the first live event confirms it.
/// </summary>
public static class InmaWebhookSignatureValidator
{
    public const string SignatureHeader = "X-Invekto-Signature";
    public const string TimestampHeader = "X-Invekto-Timestamp";
    public const string EventIdHeader = "X-Invekto-Event-Id";
    public const string EventTypeHeader = "X-Invekto-Event-Type";

    private const int Sha256Bytes = 32;

    /// <summary>
    /// Returns true iff the signature header matches the HMAC-SHA256 of
    /// <c>{timestamp}.{rawBody}</c> under <paramref name="secret"/>. All false returns are
    /// uniform from the caller's perspective (no branch/timing leak). <paramref name="matchedEncoding"/>
    /// is set to "base64"/"hex" on success (for the verify-live log), null otherwise.
    /// </summary>
    public static bool Verify(
        string? secret,
        string? timestamp,
        byte[] rawBody,
        string? signatureHeader,
        InmaWebhookOptions options,
        out string? matchedEncoding)
    {
        matchedEncoding = null;

        if (string.IsNullOrEmpty(secret)) return false;          // INV-INM-002 surfaced by caller
        if (string.IsNullOrEmpty(timestamp)) return false;
        if (rawBody is null) return false;
        if (string.IsNullOrEmpty(signatureHeader)) return false;

        // Resolve the HMAC key bytes from the configured secret encoding.
        if (!TryGetKeyBytes(secret, options.KeyEncoding, out var key)) return false;

        // Strip an optional configured prefix (e.g. "sha256=") before decoding the digest.
        var rawSig = signatureHeader;
        var prefix = options.SignaturePrefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            if (!rawSig.StartsWith(prefix, StringComparison.Ordinal)) return false;
            rawSig = rawSig[prefix.Length..];
        }

        // Compute the expected digest over {timestamp}.{rawBody} with verbatim body bytes.
        Span<byte> computed = stackalloc byte[Sha256Bytes];
        if (!TryComputeHmac(key, timestamp, rawBody, computed)) return false;

        // Decode the provided digest. Try the preferred encoding first, then (verify-live) the other.
        var preferred = string.Equals(options.SignatureDigestEncoding, "hex", StringComparison.OrdinalIgnoreCase)
            ? "hex" : "base64";

        if (DigestMatches(rawSig, preferred, computed))
        {
            matchedEncoding = preferred;
            return true;
        }

        if (options.AcceptEitherEncoding)
        {
            var other = preferred == "hex" ? "base64" : "hex";
            if (DigestMatches(rawSig, other, computed))
            {
                matchedEncoding = other;
                return true;
            }
        }

        return false;
    }

    private static bool TryComputeHmac(byte[] key, string timestamp, byte[] rawBody, Span<byte> destination)
    {
        var tsBytes = Encoding.UTF8.GetBytes(timestamp + ".");
        var message = new byte[tsBytes.Length + rawBody.Length];
        Buffer.BlockCopy(tsBytes, 0, message, 0, tsBytes.Length);
        Buffer.BlockCopy(rawBody, 0, message, tsBytes.Length, rawBody.Length);

        using var hmac = new HMACSHA256(key);
        return hmac.TryComputeHash(message, destination, out var written) && written == Sha256Bytes;
    }

    private static bool DigestMatches(string providedDigest, string encoding, ReadOnlySpan<byte> computed)
    {
        Span<byte> provided = stackalloc byte[Sha256Bytes];
        if (encoding == "hex")
        {
            if (!TryParseHex(providedDigest, provided)) return false;
        }
        else
        {
            if (!TryParseBase64(providedDigest, provided)) return false;
        }
        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }

    private static bool TryGetKeyBytes(string secret, string keyEncoding, out byte[] key)
    {
        if (string.Equals(keyEncoding, "utf8", StringComparison.OrdinalIgnoreCase))
        {
            key = Encoding.UTF8.GetBytes(secret);
            return key.Length > 0;
        }
        // base64 (default): the INMA secret is a base64-encoded 32-byte key.
        try
        {
            key = Convert.FromBase64String(secret);
            return key.Length > 0;
        }
        catch (FormatException)
        {
            key = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>Parse a base64 digest into exactly 32 bytes; false on malformed / wrong length.</summary>
    private static bool TryParseBase64(string value, Span<byte> destination)
    {
        Span<byte> buffer = stackalloc byte[64]; // base64 of 32 bytes is 44 chars; ample headroom
        if (!Convert.TryFromBase64String(value, buffer, out var written)) return false;
        if (written != destination.Length) return false;
        buffer[..written].CopyTo(destination);
        return true;
    }

    /// <summary>Branchless hex parser; reads into destination iff the whole input is valid [0-9a-fA-F] of the right length.</summary>
    private static bool TryParseHex(string value, Span<byte> destination)
    {
        if (value.Length != destination.Length * 2) return false;
        for (int i = 0; i < destination.Length; i++)
        {
            var hi = HexNibble(value[i * 2]);
            var lo = HexNibble(value[i * 2 + 1]);
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
