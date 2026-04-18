using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Leads;
using Invekto.Shared.Logging;

namespace Invekto.Backend.Services;

/// <summary>
/// FEAT-LIW Chunk C: typed HTTP client Backend uses to look up an active
/// chatbot_flows row metadata through Automation's public API instead of
/// doing a direct cross-service DB read. Mirrors Chunk B's
/// <c>Invekto.Automation.Services.BackendIntakeClient</c> — per-call service
/// JWT via <see cref="JwtGenerator.GenerateServiceToken"/> so Automation's
/// standard JWT middleware enforces tenant binding on /api/v1/automation/flows/
/// paths. Failure is non-fatal: null return tells
/// <c>LiwSettingsService.GetSettingsAsync</c> to leave FlowStatus.Exists=false
/// — the UI FlowWarningBanner still renders and the tenant sees the gap.
/// No retry: the settings page is a human-speed interaction, a single
/// transient failure is acceptable and the user can simply refresh.
/// </summary>
public class FlowLookupClient
{
    private const string EndpointPath = "/api/v1/automation/flows/lookup";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _logger;

    public FlowLookupClient(HttpClient http, JwtGenerator jwt, JsonLinesLogger logger)
    {
        _http = http;
        _jwt = jwt;
        _logger = logger;
    }

    /// <summary>
    /// Call Automation to resolve (tenant_id, flow_name) -> active chatbot_flows
    /// metadata. Returns null on any failure path (HTTP error, timeout,
    /// unparseable body, or the remote explicitly returning exists=false) so
    /// the caller uniformly treats "no active match" identically whether the
    /// miss is authoritative or degraded.
    /// </summary>
    public virtual async Task<FlowLookupResponse?> LookupAsync(
        int tenantId,
        string flowName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(flowName)) return null;

        try
        {
            using var msg = new HttpRequestMessage(
                HttpMethod.Get,
                $"{EndpointPath}?name={Uri.EscapeDataString(flowName)}");

            // Service-JWT: tenant_id claim is signed; Automation middleware
            // validates + sets TenantContext so downstream Authorization /
            // tenant filtering behaves identically to any /api/v1/ endpoint.
            var serviceJwt = _jwt.GenerateServiceToken(tenantId);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);

            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.BackendMicroserviceError}] FlowLookupClient: " +
                    $"Automation returned HTTP {(int)response.StatusCode} " +
                    $"(tenant={tenantId}, name='{flowName}')");
                return null;
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<FlowLookupResponse>(
                    ResponseJsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.BackendMicroserviceInvalidResponse}] FlowLookupClient: " +
                    $"unparseable response body (tenant={tenantId}, name='{flowName}'): {ex.Message}");
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.BackendMicroserviceUnavailable}] FlowLookupClient: " +
                $"HTTP transport error (tenant={tenantId}, name='{flowName}'): {ex.Message}");
            return null;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient-level timeout (not caller cancellation). Observable + returns null
            // so the caller treats the settings page as "flow_status unknown" rather than
            // blowing up the whole page for a degraded dependency.
            _logger.SystemWarn(
                $"[{ErrorCodes.BackendMicroserviceTimeout}] FlowLookupClient: " +
                $"timeout (tenant={tenantId}, name='{flowName}')");
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller asked to cancel — preserve cancellation semantics upstream; the
            // page handler surfaces this as request-aborted (no silent null swallow).
            throw;
        }
    }
}
