using System.Net.Http.Headers;
using System.Net.Http.Json;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Lifecycle;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services.Lifecycle;

/// <summary>
/// Paket B-META: HTTP implementation of <see cref="ILifecycleBackendClient"/>.
/// Dual-auth pattern inherited verbatim from LIW Chunk B's BackendIntakeClient /
/// FEAT-EFS's MarketingFollowupClient:
///   * Layer 1 — tenant-bound short-lived JWT via <see cref="JwtGenerator.GenerateServiceToken"/>
///   * Layer 2 — peer-service proof via <c>X-Internal-Service-Token</c> header
///     (cross-checked against <c>InternalServices:SharedSecret</c>)
///
/// Fire-and-forget contract: all transport-class exceptions are swallowed
/// after structured INV-AT-072 warn log; the caller
/// (<c>TriggerWelcomeFlowJob</c>) continues cleanup regardless. Empty shared
/// secret degrades to "log + return false" so a dev box without
/// InternalServices:SharedSecret configured can still run the job without
/// blocking the welcome path.
/// </summary>
public sealed class LifecycleBackendClient : ILifecycleBackendClient
{
    public const string HttpClientName = "backend-internal";

    private const string EndpointPath    = "/api/internal/lifecycle/welcome-sent";
    private const string TokenHeaderName = "X-Internal-Service-Token";

    private readonly HttpClient _http;
    private readonly string _sharedSecret;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _logger;

    public LifecycleBackendClient(
        HttpClient http, string sharedSecret, JwtGenerator jwt, JsonLinesLogger logger)
    {
        _http = http;
        _sharedSecret = sharedSecret;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<bool> SendWelcomeSentAsync(int tenantId, int leadId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_sharedSecret))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationLifecycleHopFailed}] LifecycleBackendClient: " +
                $"InternalServices:SharedSecret not configured, skipping welcome-sent hop " +
                $"(tenant={tenantId}, lead={leadId})");
            return false;
        }

        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, EndpointPath);
            var serviceJwt = _jwt.GenerateServiceToken(tenantId);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
            msg.Headers.TryAddWithoutValidation(TokenHeaderName, _sharedSecret);
            msg.Content = JsonContent.Create(new WelcomeSentNotification
            {
                TenantId    = tenantId,
                LeadId      = leadId,
                TriggeredAt = DateTime.UtcNow
            });

            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationLifecycleHopFailed}] LifecycleBackendClient: " +
                    $"welcome-sent non-2xx (status={(int)response.StatusCode}, tenant={tenantId}, lead={leadId})");
                return false;
            }
            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationLifecycleHopFailed}] LifecycleBackendClient: " +
                $"welcome-sent transport error (tenant={tenantId}, lead={leadId}): {ex.Message}");
            return false;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationLifecycleHopFailed}] LifecycleBackendClient: " +
                $"welcome-sent timeout (tenant={tenantId}, lead={leadId}): {ex.Message}");
            return false;
        }
    }
}
