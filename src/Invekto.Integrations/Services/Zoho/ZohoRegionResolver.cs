// Adim 2 Paket A: Resolves Zoho data-center config from accounts-server callback param OR config key.
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Invekto.Integrations.Services.Zoho;

/// <summary>
/// Loads <see cref="ZohoRegionConfig"/> per region from <c>appsettings:Zoho.&lt;Region&gt;</c>
/// and resolves the region key from the OAuth callback's <c>accounts-server</c> URL.
/// </summary>
public sealed class ZohoRegionResolver
{
    // Authoritative map of accounts hostname -> region key (lowercase).
    // Region keys mirror Zoho DC suffixes used in URLs.
    private static readonly IReadOnlyDictionary<string, string> AccountsHostToRegion =
        new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["accounts.zoho.eu"]      = "eu",
            ["accounts.zoho.com"]     = "com",
            ["accounts.zoho.in"]      = "in",
            ["accounts.zoho.com.au"]  = "com.au",
            ["accounts.zoho.jp"]      = "jp",
            ["accounts.zoho.uk"]      = "uk",
            ["accounts.zohocloud.ca"] = "ca",
            ["accounts.zoho.sa"]      = "sa",
        };

    private readonly IConfiguration _configuration;

    public ZohoRegionResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Resolves a region key from Zoho callback's <c>accounts-server</c> URL.
    /// Returns null when the URL is malformed or host is unknown (caller maps to INV-INT-110).
    /// </summary>
    public string? RegionFromAccountsServer(string? accountsServerUrl)
    {
        if (string.IsNullOrWhiteSpace(accountsServerUrl))
            return null;

        if (!System.Uri.TryCreate(accountsServerUrl, System.UriKind.Absolute, out var uri))
            return null;

        return AccountsHostToRegion.TryGetValue(uri.Host, out var region) ? region : null;
    }

    /// <summary>
    /// Loads region-specific OAuth config block. The config key uses the upper-snake form of the region
    /// (e.g. <c>eu</c> -&gt; <c>Zoho:EU</c>, <c>com.au</c> -&gt; <c>Zoho:COM_AU</c>).
    /// Returns null when the config block is absent (caller maps to INV-INT-116).
    /// </summary>
    public ZohoRegionConfig? GetConfig(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return null;

        var configKey = "Zoho:" + region.ToUpperInvariant().Replace('.', '_');
        var section = _configuration.GetSection(configKey);
        if (!section.Exists())
            return null;

        var clientId     = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        var accountsUrl  = section["AccountsUrl"];
        var apiBaseUrl   = section["ApiBaseUrl"];
        var redirectUri  = section["RedirectUri"];
        var scopes       = section["Scopes"];

        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(accountsUrl)
            || string.IsNullOrWhiteSpace(apiBaseUrl)
            || string.IsNullOrWhiteSpace(redirectUri)
            || string.IsNullOrWhiteSpace(scopes))
        {
            return null;
        }

        return new ZohoRegionConfig(region, clientId, clientSecret, accountsUrl, apiBaseUrl, redirectUri, scopes);
    }
}
