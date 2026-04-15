using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 3-B1: Raw-forward proxy client for Dashboard UI Zoho endpoints.
/// Differs from ZohoSyncClient (P2) which uses shared-secret + body.TenantId for internal/zoho/sync;
/// here the caller is an authenticated UI user, so we forward the tenant JWT verbatim.
/// Typed exception handling follows project rule (no bare catch): transport/timeout/IO separately.
/// </summary>
public sealed class ZohoProxyClient : IZohoProxyClient
{
    private const string TransportErrorCode = "INV-INT-127";

    private readonly HttpClient _http;
    private readonly JsonLinesLogger _logger;

    public ZohoProxyClient(HttpClient http, JsonLinesLogger logger)
    {
        _http = http;
        _logger = logger;
    }

    public Task<ZohoProxyResult> ForwardAsync(
        HttpMethod method,
        string pathAndQuery,
        string? bearerToken,
        CancellationToken ct = default) =>
        ForwardAsync(method, pathAndQuery, bearerToken, jsonBody: null, ct);

    public async Task<ZohoProxyResult> ForwardAsync(
        HttpMethod method,
        string pathAndQuery,
        string? bearerToken,
        string? jsonBody,
        CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(method, pathAndQuery);
        if (!string.IsNullOrEmpty(bearerToken))
        {
            msg.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearerToken);
        }
        if (jsonBody is not null)
        {
            msg.Content = new System.Net.Http.StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return new ZohoProxyResult((int)response.StatusCode, body, contentType);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{TransportErrorCode}] Zoho proxy transport failure: method={method.Method} path={pathAndQuery} err={ex.Message}");
            return BuildTransportError(502, "Invekto.Integrations servisine ulasilamiyor; birkac saniye icinde tekrar deneyin. Surekli olursa yoneticinize bildirin.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.SystemWarn($"[{TransportErrorCode}] Zoho proxy timeout: method={method.Method} path={pathAndQuery} err={ex.Message}");
            return BuildTransportError(504, "Invekto.Integrations yanit verme suresini asti; birkac saniye icinde tekrar deneyin.");
        }
        catch (IOException ex)
        {
            _logger.SystemWarn($"[{TransportErrorCode}] Zoho proxy IO failure: method={method.Method} path={pathAndQuery} err={ex.Message}");
            return BuildTransportError(502, "Upstream yanit okumasi sirasinda IO hatasi olustu; tekrar deneyin.");
        }
    }

    // arch/errors.md INV-INT-127 envelope — Turkish user_message + INV-INT-127 code mirrors production convention.
    private static ZohoProxyResult BuildTransportError(int statusCode, string userMessage)
    {
        var escaped = userMessage.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var body = $"{{\"error\":{{\"code\":\"{TransportErrorCode}\",\"message\":\"{escaped}\"}}}}";
        return new ZohoProxyResult(statusCode, body, "application/json");
    }
}
