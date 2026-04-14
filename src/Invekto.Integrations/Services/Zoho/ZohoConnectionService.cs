// Adim 2 Paket B: orchestrates the Zoho OAuth handshake (state issue, callback handling, encryption).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Data;
using Microsoft.AspNetCore.DataProtection;

namespace Invekto.Integrations.Services.Zoho;

public sealed class ZohoConnectionService
{
    private readonly ZohoConnectionRepository _repository;
    private readonly ZohoRegionResolver _regionResolver;
    private readonly ZohoOAuthStateService _stateService;
    private readonly ZohoTokenProvider _tokenProvider;
    private readonly IDataProtector _protector;
    private readonly string _defaultRegionForAuthorize;

    public ZohoConnectionService(
        ZohoConnectionRepository repository,
        ZohoRegionResolver regionResolver,
        ZohoOAuthStateService stateService,
        ZohoTokenProvider tokenProvider,
        IDataProtectionProvider protectionProvider,
        string defaultRegionForAuthorize)
    {
        _repository                = repository;
        _regionResolver            = regionResolver;
        _stateService              = stateService;
        _tokenProvider             = tokenProvider;
        _protector                 = protectionProvider.CreateProtector(ZohoTokenProvider.DataProtectionPurpose);
        _defaultRegionForAuthorize = defaultRegionForAuthorize;
    }

    /// <summary>
    /// Builds the Zoho OAuth authorize URL for the tenant.
    /// Uses the configured default region as the starting point; user's actual DC will be reflected in the
    /// callback's <c>accounts-server</c> param (multi-DC same-credential model).
    /// Throws InvalidOperationException with INV-INT-116 when default region is not configured.
    /// </summary>
    public string BuildAuthorizeUrl(int tenantId)
    {
        var regionConfig = _regionResolver.GetConfig(_defaultRegionForAuthorize)
            ?? throw new InvalidOperationException(
                $"INV-INT-116: Default Zoho region '{_defaultRegionForAuthorize}' is not configured. " +
                $"Add a Zoho:{_defaultRegionForAuthorize.ToUpperInvariant()} block (ClientId/ClientSecret/AccountsUrl/ApiBaseUrl/RedirectUri/Scopes) to appsettings.");

        var state = _stateService.Sign(tenantId);
        var authorizeBase = TrimSlash(regionConfig.AccountsUrl) + "/oauth/v2/auth";

        var query = "?response_type=code"
                    + "&client_id="    + Uri.EscapeDataString(regionConfig.ClientId)
                    + "&scope="        + Uri.EscapeDataString(regionConfig.Scopes)
                    + "&redirect_uri=" + Uri.EscapeDataString(regionConfig.RedirectUri)
                    + "&access_type=offline"
                    + "&prompt=consent"
                    + "&state="        + Uri.EscapeDataString(state);

        return authorizeBase + query;
    }

    /// <summary>
    /// Handles the OAuth callback: validates state, resolves region from accounts-server, exchanges code for tokens,
    /// encrypts refresh_token via DataProtection, and persists the connection row.
    /// Returns the tenantId on success. Throws InvalidOperationException with INV-INT-* prefix on every failure path.
    /// </summary>
    public async Task<int> HandleCallbackAsync(
        string? code,
        string? stateToken,
        string? accountsServerUrl,
        string? zohoUserEmail,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("INV-INT-113: OAuth callback missing 'code' query parameter. Restart the OAuth flow from /api/v1/zoho/connect-url.");

        var state = _stateService.Verify(stateToken)
            ?? throw new InvalidOperationException("INV-INT-111: OAuth state token invalid or expired (10 min TTL). Restart the OAuth flow from /api/v1/zoho/connect-url.");

        var region = _regionResolver.RegionFromAccountsServer(accountsServerUrl)
            ?? throw new InvalidOperationException(
                $"INV-INT-110: Unknown Zoho region (accounts-server='{accountsServerUrl}'). " +
                "Verify Zoho data center is one of: zoho.eu, zoho.com, zoho.in, zoho.com.au, zoho.jp, zoho.uk, zohocloud.ca, zoho.sa.");

        var regionConfig = _regionResolver.GetConfig(region)
            ?? throw new InvalidOperationException(
                $"INV-INT-116: Zoho region '{region}' not configured. Add Zoho:{region.ToUpperInvariant().Replace('.', '_')} block to appsettings.");

        var tokenResponse = await _tokenProvider.ExchangeAuthorizationCodeAsync(regionConfig, code, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(tokenResponse.RefreshToken))
            throw new InvalidOperationException(
                "INV-INT-113: Zoho token exchange returned no refresh_token. " +
                "Restart OAuth flow; ensure authorize URL includes access_type=offline AND prompt=consent.");

        var encryptedRefreshToken = _protector.Protect(tokenResponse.RefreshToken);

        var apiDomain = string.IsNullOrEmpty(tokenResponse.ApiDomain)
            ? StripScheme(regionConfig.ApiBaseUrl)
            : StripScheme(tokenResponse.ApiDomain);
        var accountsDomain = StripScheme(regionConfig.AccountsUrl);

        await _repository.UpsertAsync(
            tenantId:              state.TenantId,
            region:                region,
            apiDomain:             apiDomain,
            accountsDomain:        accountsDomain,
            encryptedRefreshToken: encryptedRefreshToken,
            grantedScopes:         tokenResponse.Scope ?? regionConfig.Scopes,
            zohoUserEmail:         zohoUserEmail,
            ct:                    ct).ConfigureAwait(false);

        // Invalidate any stale cached access token (e.g. from a prior connection).
        _tokenProvider.InvalidateCache(state.TenantId);

        return state.TenantId;
    }

    private static string TrimSlash(string url) => url.EndsWith('/') ? url.TrimEnd('/') : url;

    private static string StripScheme(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return url.Substring(8).TrimEnd('/');
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))  return url.Substring(7).TrimEnd('/');
        return url.TrimEnd('/');
    }
}
