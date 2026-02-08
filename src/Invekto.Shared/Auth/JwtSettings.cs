namespace Invekto.Shared.Auth;

/// <summary>
/// JWT validation configuration.
/// GR-1.9: Shared JWT key between Main App and InvektoServis.
/// Maps to "Jwt" section in appsettings.json.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>HMAC-SHA256 shared secret key (base64 encoded, minimum 32 bytes)</summary>
    public required string SecretKey { get; init; }

    /// <summary>Expected issuer claim. Null = skip issuer validation.</summary>
    public string? Issuer { get; init; }

    /// <summary>Expected audience claim. Null = skip audience validation.</summary>
    public string? Audience { get; init; }

    /// <summary>Clock skew tolerance in seconds (default: 60s)</summary>
    public int ClockSkewSeconds { get; init; } = 60;
}
