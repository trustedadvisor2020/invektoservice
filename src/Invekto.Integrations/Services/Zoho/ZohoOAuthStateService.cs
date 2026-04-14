// Adim 2 Paket A: Sign/verify HMAC-SHA256 OAuth state tokens (CSRF protection).
// Format (compact, no dependency on JWT lib):
//   "<payload>.<hmac>"     where payload = base64url("<tenantId>|<nonce>|<iatUnix>|<expUnix>")
//                          and   hmac    = base64url(HMAC-SHA256(secret, payload))
// State is short-lived (default 10 minutes). Secret comes from the existing Jwt:SecretKey.
using System;
using System.Security.Cryptography;
using System.Text;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoOAuthStateService
{
    private const int DefaultTtlSeconds = 600;
    private const int NonceLengthBytes  = 16;

    private readonly byte[] _secretKey;
    private readonly TimeSpan _ttl;

    public ZohoOAuthStateService(string secretKey, int ttlSeconds = DefaultTtlSeconds)
    {
        // Misconfiguration is a startup-fatal programming error. INV-INT-116 (Zoho bölgesi yapılandırılmamış)
        // covers the family of OAuth client misconfiguration; surface the code so ops/logs are searchable.
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException(
                "INV-INT-116: State signing secret cannot be empty. Set Jwt:SecretKey in appsettings.",
                nameof(secretKey));

        _secretKey = Encoding.UTF8.GetBytes(secretKey);
        if (_secretKey.Length < 32)
            throw new ArgumentException(
                "INV-INT-116: State signing secret must be at least 32 bytes (256 bits). Increase Jwt:SecretKey length.",
                nameof(secretKey));

        if (ttlSeconds <= 0 || ttlSeconds > 3600)
            throw new ArgumentOutOfRangeException(
                nameof(ttlSeconds),
                "INV-INT-116: ttlSeconds must be in (0, 3600]. Adjust ZohoOAuthStateService construction.");

        _ttl = TimeSpan.FromSeconds(ttlSeconds);
    }

    /// <summary>Signs a fresh OAuth state token for the given tenant. Returns the token string.</summary>
    public string Sign(int tenantId)
    {
        var nonce  = GenerateNonce();
        var issued = DateTimeOffset.UtcNow;
        var expiry = issued.Add(_ttl);

        var payloadStr   = tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                           + "|" + nonce
                           + "|" + issued.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)
                           + "|" + expiry.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadStr);
        var payloadB64   = Base64UrlEncode(payloadBytes);

        var hmacBytes    = ComputeHmac(payloadB64);
        var hmacB64      = Base64UrlEncode(hmacBytes);

        return payloadB64 + "." + hmacB64;
    }

    /// <summary>
    /// Verifies a state token. Returns the parsed payload on success, or <c>null</c> when the token is malformed,
    /// the HMAC mismatches, or the token has expired (caller maps to INV-INT-111).
    /// </summary>
    public ZohoOAuthState? Verify(string? stateToken)
    {
        if (string.IsNullOrWhiteSpace(stateToken))
            return null;

        var dotIndex = stateToken.IndexOf('.');
        if (dotIndex <= 0 || dotIndex == stateToken.Length - 1)
            return null;

        var payloadB64 = stateToken.Substring(0, dotIndex);
        var hmacB64    = stateToken.Substring(dotIndex + 1);

        byte[] presentedHmac;
        byte[] expectedHmac;
        try
        {
            presentedHmac = Base64UrlDecode(hmacB64);
            expectedHmac  = ComputeHmac(payloadB64);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(presentedHmac, expectedHmac))
            return null;

        byte[] payloadBytes;
        try
        {
            payloadBytes = Base64UrlDecode(payloadB64);
        }
        catch (FormatException)
        {
            return null;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var parts   = payload.Split('|');
        if (parts.Length != 4)
            return null;

        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var tenantId)
            || !long.TryParse(parts[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var iatUnix)
            || !long.TryParse(parts[3], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var expUnix))
        {
            return null;
        }

        var nonce  = parts[1];
        if (string.IsNullOrEmpty(nonce))
            return null;

        var issued = DateTimeOffset.FromUnixTimeSeconds(iatUnix);
        var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix);

        if (DateTimeOffset.UtcNow > expiry)
            return null;

        return new ZohoOAuthState(tenantId, nonce, issued, expiry);
    }

    private byte[] ComputeHmac(string payloadB64)
    {
        using var hmac = new HMACSHA256(_secretKey);
        return hmac.ComputeHash(Encoding.ASCII.GetBytes(payloadB64));
    }

    private static string GenerateNonce()
    {
        var buffer = new byte[NonceLengthBytes];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlEncode(buffer);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var b64 = text.Replace('-', '+').Replace('_', '/');
        var padding = (4 - b64.Length % 4) % 4;
        if (padding > 0)
            b64 += new string('=', padding);
        return Convert.FromBase64String(b64);
    }
}
