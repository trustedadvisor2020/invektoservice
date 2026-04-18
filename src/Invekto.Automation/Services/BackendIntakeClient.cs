using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Leads;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// FEAT-LIW Chunk B: thin typed-HttpClient wrapper Automation uses to register
/// a wa-direct lead with Backend before chat-flow execution. The endpoint
/// (<c>POST /api/internal/leads/intake/wa-direct</c>) is service-to-service:
/// auth is the shared <c>X-Internal-Service-Token</c> header (mirrors Zoho
/// pattern), not a tenant JWT. Failures are non-fatal — see
/// <see cref="IntakeAsync"/>'s contract: a null return tells the orchestrator
/// to log <c>INV-AT-070</c> and proceed with leadId=null. One inline retry
/// (500 ms) covers transient hiccups (TCP reset, brief Backend restart) without
/// pulling Polly into the dependency graph.
/// </summary>
public sealed class BackendIntakeClient
{
    private const string EndpointPath = "/api/internal/leads/intake/wa-direct";
    private const string TokenHeaderName = "X-Internal-Service-Token";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly string _sharedSecret;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _logger;

    public BackendIntakeClient(HttpClient http, string sharedSecret, JwtGenerator jwt, JsonLinesLogger logger)
    {
        _http = http;
        _sharedSecret = sharedSecret;
        _jwt = jwt;
        _logger = logger;
    }

    /// <summary>
    /// POST the wa-direct payload to Backend. Returns the parsed response on a
    /// 2xx, or null on any failure path (HTTP exception, timeout, non-2xx,
    /// unparseable body, missing shared secret). Caller should treat null as
    /// "best-effort done, log INV-AT-070, continue chat".
    /// </summary>
    public async Task<WaDirectIntakeResponse?> IntakeAsync(
        WaDirectIntakeRequest request, string requestId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_sharedSecret))
        {
            // Surfacing the misconfig at call time (not at startup) keeps the
            // service bootable even when InternalServices:SharedSecret is empty
            // — wa-direct silently degrades to "best-effort fail" and the
            // tenant chat path keeps working.
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] BackendIntakeClient: " +
                $"InternalServices:SharedSecret not configured, skipping wa-direct intake " +
                $"(tenant={request.TenantId}, requestId={requestId})");
            return null;
        }

        // First attempt; retry once on transient HTTP/timeout failure. Inline
        // retry deliberately doesn't recurse — at most 2 calls total — so a
        // persistently-down Backend never blocks chat reply for more than
        // one HTTP timeout + 500 ms.
        var first = await TrySendAsync(request, requestId, attempt: 1, ct);
        if (first.Outcome == SendOutcome.Success)
            return first.Body;

        if (first.Outcome == SendOutcome.NonRetriable)
            return null;

        try
        {
            await Task.Delay(RetryDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        var retry = await TrySendAsync(request, requestId, attempt: 2, ct);
        return retry.Outcome == SendOutcome.Success ? retry.Body : null;
    }

    private async Task<SendResult> TrySendAsync(
        WaDirectIntakeRequest request, string requestId, int attempt, CancellationToken ct)
    {
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, EndpointPath);
            // Layer 1: tenant-bound JWT so Backend's standard JWT middleware can
            // enforce tenant binding on /api/internal/ paths just like every other
            // /api/v1/* route. Short expiry (5 min default in JwtGenerator) makes
            // a leaked token cheap to revoke by simple rotation; per-call mint
            // avoids any cache-coherency hazard.
            var serviceJwt = _jwt.GenerateServiceToken(request.TenantId);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
            // Layer 2: peer-service identity proof, separate from JWT so a
            // leaked tenant JWT alone can't reach this endpoint.
            msg.Headers.TryAddWithoutValidation(TokenHeaderName, _sharedSecret);
            msg.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
            msg.Content = JsonContent.Create(request);

            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                // 4xx is a contract problem (e.g. bad payload, missing token);
                // retrying the same call won't fix it. 5xx might recover, so we
                // distinguish: 5xx + transient → retriable; everything else → not.
                var retriable = (int)response.StatusCode >= 500;
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] BackendIntakeClient: " +
                    $"Backend returned HTTP {(int)response.StatusCode} " +
                    $"(attempt={attempt}, tenant={request.TenantId}, requestId={requestId})");
                return new SendResult(retriable ? SendOutcome.Retriable : SendOutcome.NonRetriable, null);
            }

            try
            {
                var body = await response.Content.ReadFromJsonAsync<WaDirectIntakeResponse>(
                    ResponseJsonOptions, ct);
                if (body == null)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] BackendIntakeClient: " +
                        $"Backend response body null (tenant={request.TenantId}, requestId={requestId})");
                    return new SendResult(SendOutcome.NonRetriable, null);
                }
                return new SendResult(SendOutcome.Success, body);
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] BackendIntakeClient: " +
                    $"unparseable response body (tenant={request.TenantId}, requestId={requestId}): {ex.Message}");
                return new SendResult(SendOutcome.NonRetriable, null);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] BackendIntakeClient: " +
                $"HTTP transport error (attempt={attempt}, tenant={request.TenantId}, requestId={requestId}): {ex.Message}");
            return new SendResult(SendOutcome.Retriable, null);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // TaskCanceledException without ct cancellation = HttpClient timeout.
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] BackendIntakeClient: " +
                $"timeout (attempt={attempt}, tenant={request.TenantId}, requestId={requestId})");
            return new SendResult(SendOutcome.Retriable, null);
        }
        catch (OperationCanceledException)
        {
            // Caller cancellation: don't retry, just bail.
            return new SendResult(SendOutcome.NonRetriable, null);
        }
    }

    private enum SendOutcome { Success, Retriable, NonRetriable }
    private readonly record struct SendResult(SendOutcome Outcome, WaDirectIntakeResponse? Body);
}
