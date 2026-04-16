namespace Invekto.Shared.Auth;

/// <summary>
/// Configuration for inma (Invekto Main App) integration.
/// Maps to "InmaAuth" section in appsettings.json.
///
/// Note: SecretKey was removed in 20260416-inma-token-introspection. inma JWT
/// signature is NEVER verified locally (key is WapCRM backend's private secret).
/// Validation goes through InmaTokenIntrospector → welcome-endpoint check.
/// </summary>
public sealed class InmaJwtSettings
{
    /// <summary>inma login API URL. e.g. https://api.invekto.com/api/auth/login</summary>
    public required string LoginUrl { get; init; }

    /// <summary>HTTP timeout for inma login proxy requests in milliseconds (default: 10000)</summary>
    public int LoginTimeoutMs { get; init; } = 10000;

    /// <summary>inma token refresh API URL. e.g. https://testapi.wapcrm.net/api/token/refresh</summary>
    public string? RefreshUrl { get; init; }

    /// <summary>inma API base URL for proxy + introspection. e.g. https://testapi.wapcrm.net</summary>
    public string? ApiBaseUrl { get; init; }
}
