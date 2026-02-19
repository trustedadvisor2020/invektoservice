namespace Invekto.Shared.Auth;

/// <summary>
/// Configuration for validating inma (Invekto Main App) JWT tokens.
/// Maps to "InmaAuth" section in appsettings.json.
/// </summary>
public sealed class InmaJwtSettings
{
    /// <summary>Shared HMAC-SHA256 secret key (same key used by inma to sign tokens)</summary>
    public required string SecretKey { get; init; }

    /// <summary>inma login API URL. e.g. https://api.invekto.com/api/auth/login</summary>
    public required string LoginUrl { get; init; }

    /// <summary>Clock skew tolerance in seconds (default: 60s)</summary>
    public int ClockSkewSeconds { get; init; } = 60;

    /// <summary>HTTP timeout for inma login proxy requests in milliseconds (default: 10000)</summary>
    public int LoginTimeoutMs { get; init; } = 10000;

    /// <summary>inma token refresh API URL. e.g. https://testapi.wapcrm.net/api/token/refresh</summary>
    public string? RefreshUrl { get; init; }

    /// <summary>inma API base URL for proxy requests. e.g. https://testapi.wapcrm.net</summary>
    public string? ApiBaseUrl { get; init; }
}
