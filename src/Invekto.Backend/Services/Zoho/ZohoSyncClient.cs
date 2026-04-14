using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Shared.Contracts.Zoho;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services.Zoho;

/// <summary>
/// Typed HttpClient that POSTs ZohoSyncRequest to Invekto.Integrations /api/internal/zoho/sync.
/// Short timeout (configured on HttpClient). Transport failures are logged as INV-INT-127 and
/// surface as a null return so callers (ZohoLifecycleDispatcher) can swallow without throwing.
/// </summary>
public sealed class ZohoSyncClient : IZohoSyncClient
{
    private const string InternalTokenHeader = "X-Internal-Service-Token";
    private const string TransportErrorCode = "INV-INT-127";
    private const string ConfigMissingErrorCode = "INV-INT-126";

    private readonly HttpClient _http;
    private readonly string _sharedSecret;
    private readonly JsonLinesLogger _logger;

    public ZohoSyncClient(HttpClient http, string sharedSecret, JsonLinesLogger logger)
    {
        _http = http;
        _sharedSecret = sharedSecret;
        _logger = logger;
    }

    public async Task<ZohoSyncResponse?> SyncAsync(ZohoSyncRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sharedSecret))
        {
            // Configuration missing is a distinct class from transport failure (see arch/errors.md).
            _logger.SystemWarn($"[{ConfigMissingErrorCode}] Zoho sync skipped: InternalServices:SharedSecret not configured on Backend.");
            return null;
        }

        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/internal/zoho/sync")
        {
            Content = JsonContent.Create(request)
        };
        msg.Headers.Add(InternalTokenHeader, _sharedSecret);

        try
        {
            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{TransportErrorCode}] Zoho sync non-2xx from Integrations: status={(int)response.StatusCode} tenant={request.TenantId} event={request.GunesEvent} lead={request.GunesLeadId}");
                return null;
            }

            var body = await response.Content
                .ReadFromJsonAsync<ZohoSyncResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            return body;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{TransportErrorCode}] Zoho sync HTTP transport failure: tenant={request.TenantId} event={request.GunesEvent} lead={request.GunesLeadId} err={ex.Message}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{TransportErrorCode}] Zoho sync timeout: tenant={request.TenantId} event={request.GunesEvent} lead={request.GunesLeadId} err={ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{TransportErrorCode}] Zoho sync response parse error: tenant={request.TenantId} event={request.GunesEvent} err={ex.Message}");
            return null;
        }
    }
}
