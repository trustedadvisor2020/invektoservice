using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Logging;
using Microsoft.Extensions.Configuration;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Adim 3 Paket 3-C: Raw-forward proxy client for super-admin ops Zoho endpoints.
/// Authenticates to Integrations with shared-secret (X-Internal-Service-Token); NOT JWT forward.
/// Typed exception handling mirrors ZohoProxyClient (P3-B1).
/// Typed-client pattern: AddHttpClient&lt;ZohoOpsProxyClient&gt; injects HttpClient via DI;
/// shared secret is read from IConfiguration["InternalServices:SharedSecret"] at ctor time.
/// </summary>
public sealed class ZohoOpsProxyClient : IZohoOpsProxyClient
{
    private const string TransportErrorCode = "INV-INT-127";
    public  const string InternalTokenHeader = "X-Internal-Service-Token";
    private const string SharedSecretConfigKey = "InternalServices:SharedSecret";

    private readonly HttpClient _http;
    private readonly string _sharedSecret;
    private readonly JsonLinesLogger _logger;

    public ZohoOpsProxyClient(HttpClient http, IConfiguration config, JsonLinesLogger logger)
    {
        _http = http;
        _sharedSecret = config[SharedSecretConfigKey] ?? string.Empty;
        _logger = logger;
    }

    public async Task<ZohoProxyResult> ForwardAsync(
        HttpMethod method,
        string pathAndQuery,
        string? jsonBody,
        CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(method, pathAndQuery);
        if (!string.IsNullOrEmpty(_sharedSecret))
            msg.Headers.TryAddWithoutValidation(InternalTokenHeader, _sharedSecret);

        if (jsonBody is not null)
            msg.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            return new ZohoProxyResult((int)response.StatusCode, body, contentType);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{TransportErrorCode}] Zoho ops proxy transport failure: method={method.Method} path={pathAndQuery} err={ex.Message}");
            return BuildTransportError(502, "Invekto.Integrations servisine ulasilamiyor; birkac saniye icinde tekrar deneyin.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.SystemWarn($"[{TransportErrorCode}] Zoho ops proxy timeout: method={method.Method} path={pathAndQuery} err={ex.Message}");
            return BuildTransportError(504, "Invekto.Integrations yanit verme suresini asti; birkac saniye icinde tekrar deneyin.");
        }
        catch (IOException ex)
        {
            _logger.SystemWarn($"[{TransportErrorCode}] Zoho ops proxy IO failure: method={method.Method} path={pathAndQuery} err={ex.Message}");
            return BuildTransportError(502, "Upstream yanit okumasi sirasinda IO hatasi olustu; tekrar deneyin.");
        }
    }

    private static ZohoProxyResult BuildTransportError(int statusCode, string userMessage)
    {
        var escaped = userMessage.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var body = $"{{\"error\":{{\"code\":\"{TransportErrorCode}\",\"message\":\"{escaped}\"}}}}";
        return new ZohoProxyResult(statusCode, body, "application/json");
    }
}
