using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C4: thin typed-HttpClient wrapper the 'Set Customer Status' flow action uses to
/// write a lead's INMA feature-group selection back via Backend (which holds the tenant WapCRM secret + the
/// whitelisted cxapi egress). Target: <c>POST /api/internal/customer-feature-groups/update</c>. Auth mirrors
/// <see cref="BackendIntakeClient"/> exactly — Layer 1: a per-call tenant-bound service JWT
/// (<see cref="JwtGenerator.GenerateServiceToken"/>) so Backend's standard JWT middleware binds the call to
/// the tenant; Layer 2: the shared <c>X-Internal-Service-Token</c> header so a leaked tenant JWT alone
/// cannot reach the write path. The WapCRM secret never travels on this hop.
///
/// BOUNDED TRANSIENT RETRY (C3 HARDENING): unlike a blind retry, this is SAFE here because the write is
/// value-idempotent (full-list replace) and the ClientRequestID is STABLE — re-applying the same selection
/// (even after an unknown-outcome timeout) yields the same end state, and cxapi dedupes on the
/// ClientRequestID. Retries are TRANSIENT-ONLY (timeout / transport / HTTP 5xx) with a short awaited backoff
/// (500ms, then 1500ms; max 2 retries); business/auth outcomes (4xx, vendor 903/920-923 parsed from the body)
/// go STRAIGHT to the 'error' output handle with no retry. The method still never throws (except caller
/// cancellation), so a single status node can never fault the whole flow run.
/// </summary>
public sealed class BackendCustomerStatusClient
{
    private const string EndpointPath = "/api/internal/customer-feature-groups/update";
    private const string TokenHeaderName = "X-Internal-Service-Token";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // C3 HARDENING: awaited backoff schedule for TRANSIENT failures (timeout / transport / HTTP 5xx).
    // Length == max retries (2). Stable ClientRequestID + full-list-replace make each retry value-idempotent.
    private static readonly int[] TransientBackoffMs = { 500, 1500 };

    private readonly HttpClient _http;
    private readonly string _sharedSecret;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _logger;

    public BackendCustomerStatusClient(HttpClient http, string sharedSecret, JwtGenerator jwt, JsonLinesLogger logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _sharedSecret = sharedSecret ?? string.Empty;
        _jwt = jwt ?? throw new ArgumentNullException(nameof(jwt));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Apply the FULL new selection for <paramref name="request"/>. Returns an outcome the handler maps to
    /// the success/error output handle. Caller cancellation propagates as <see cref="OperationCanceledException"/>;
    /// every other failure resolves to <c>Success=false</c> (never throws), so a single status node can never
    /// fault the whole flow run.
    /// </summary>
    public async Task<BackendCustomerStatusOutcome> UpdateAsync(
        SetCustomerStatusProxyRequest request, string requestId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(_sharedSecret))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: " +
                $"InternalServices:SharedSecret not configured, cannot apply status (tenant={request.TenantId}, requestId={requestId})");
            return BackendCustomerStatusOutcome.Fail(ErrorCodes.SetCustomerStatusActionFailed,
                "Servisler arası yetki yapılandırılmamış (InternalServices:SharedSecret).");
        }

        // C3 HARDENING: bounded transient retry. Each attempt rebuilds the request (a sent HttpRequestMessage
        // cannot be reused); the ClientRequestID inside `request` is unchanged across attempts (stable +
        // value-idempotent). The loop returns/throws on every path — max TransientBackoffMs.Length + 1 attempts.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var msg = new HttpRequestMessage(HttpMethod.Post, EndpointPath);
                var serviceJwt = _jwt.GenerateServiceToken(request.TenantId);
                msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
                msg.Headers.TryAddWithoutValidation(TokenHeaderName, _sharedSecret);
                msg.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
                msg.Content = JsonContent.Create(request);

                using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.IsSuccessStatusCode)
                    return BackendCustomerStatusOutcome.Ok();

                // HTTP 5xx = transient server error → retry (value-idempotent). 4xx = business/auth → no retry.
                if ((int)response.StatusCode >= 500 && attempt < TransientBackoffMs.Length)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: Backend HTTP {(int)response.StatusCode} " +
                        $"transient, retry {attempt + 1}/{TransientBackoffMs.Length} (tenant={request.TenantId}, requestId={requestId})");
                    await Task.Delay(TransientBackoffMs[attempt], ct);
                    continue;
                }

                // Non-2xx terminal (4xx, or 5xx with retries exhausted): parse the coded body so the node error
                // handle surfaces an actionable reason.
                var (code, message) = await TryReadErrorAsync(response, ct);
                _logger.SystemWarn(
                    $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: Backend HTTP {(int)response.StatusCode} " +
                    $"(tenant={request.TenantId}, code={code ?? "-"}, requestId={requestId})");
                // Pattern-bind the message (avoid the null-forgiving operator).
                var failMessage = message is { } m && !string.IsNullOrWhiteSpace(m) ? m : "Durum güncellenemedi.";
                return BackendCustomerStatusOutcome.Fail(code ?? ErrorCodes.SetCustomerStatusActionFailed, failMessage);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // caller cancellation / shutdown — propagate
            }
            catch (OperationCanceledException)
            {
                // HttpClient timeout — UNKNOWN outcome, but value-idempotent (stable ClientRequestID), so a
                // bounded retry is safe. Exhausted → route to the error handle.
                if (attempt < TransientBackoffMs.Length)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: timeout, retry " +
                        $"{attempt + 1}/{TransientBackoffMs.Length} (tenant={request.TenantId}, requestId={requestId})");
                    await Task.Delay(TransientBackoffMs[attempt], ct);
                    continue;
                }
                _logger.SystemWarn(
                    $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: timeout exhausted (unknown outcome) " +
                    $"(tenant={request.TenantId}, requestId={requestId})");
                return BackendCustomerStatusOutcome.Fail(ErrorCodes.CustomerStatusUpdateUpstreamFailed,
                    "Durum servisi zaman aşımına uğradı (sonuç belirsiz).");
            }
            catch (HttpRequestException ex)
            {
                if (attempt < TransientBackoffMs.Length)
                {
                    _logger.SystemWarn(
                        $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: transport error, retry " +
                        $"{attempt + 1}/{TransientBackoffMs.Length} (tenant={request.TenantId}, requestId={requestId}): {ex.Message}");
                    await Task.Delay(TransientBackoffMs[attempt], ct);
                    continue;
                }
                _logger.SystemWarn(
                    $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: transport error exhausted " +
                    $"(tenant={request.TenantId}, requestId={requestId}): {ex.Message}");
                return BackendCustomerStatusOutcome.Fail(ErrorCodes.CustomerStatusUpdateUpstreamFailed,
                    "Durum servisine ulaşılamadı.");
            }
        }
    }

    private static async Task<(string? code, string? message)> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, null);
            string? code = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            string? message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            return (code, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}

/// <summary>Outcome of a Backend customer-status write hop (never carries a secret).</summary>
public sealed class BackendCustomerStatusOutcome
{
    public bool Success { get; private init; }
    /// <summary>Backend/handler error code (INV-BE-140/141/142/143 or INV-AT-089) on failure; null on success.</summary>
    public string? Code { get; private init; }
    /// <summary>Actionable message for the node 'error' output handle; null on success.</summary>
    public string? Message { get; private init; }

    public static BackendCustomerStatusOutcome Ok() => new() { Success = true };
    public static BackendCustomerStatusOutcome Fail(string code, string message) =>
        new() { Success = false, Code = code, Message = message };
}
