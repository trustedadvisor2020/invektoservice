// Adim 2 Paket A: Zoho OAuth domain models.
namespace Invekto.Integrations.Services.Zoho;

/// <summary>Per-region Zoho OAuth client configuration (loaded from appsettings Zoho.&lt;Region&gt; blocks).</summary>
public sealed record ZohoRegionConfig(
    string Region,
    string ClientId,
    string ClientSecret,
    string AccountsUrl,
    string ApiBaseUrl,
    string RedirectUri,
    string Scopes);

/// <summary>Persisted Zoho connection row (decrypted refresh token NOT exposed; service layer handles).</summary>
public sealed record ZohoConnection(
    long Id,
    int TenantId,
    string Region,
    string ApiDomain,
    string AccountsDomain,
    string EncryptedRefreshToken,
    string GrantedScopes,
    string? ZohoUserEmail,
    DateTime ConnectedAt,
    DateTime UpdatedAt,
    DateTime? LastRefreshedAt,
    DateTime? DisconnectedAt);

/// <summary>Result returned by Zoho /oauth/v2/token (authorization_code or refresh_token grant).</summary>
public sealed record ZohoTokenResponse(
    string AccessToken,
    string? RefreshToken,
    string ApiDomain,
    string TokenType,
    int ExpiresInSeconds,
    string? Scope);

/// <summary>OAuth state payload signed as JWT (HMAC-SHA256) and round-tripped via Zoho callback.</summary>
public sealed record ZohoOAuthState(int TenantId, string Nonce, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
