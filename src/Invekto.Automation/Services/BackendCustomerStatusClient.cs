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
/// NO auto-retry (unlike <see cref="BackendIntakeClient"/>): this is a CRM mutation and a timeout is an
/// UNKNOWN outcome (the write may already have applied), so a blind retry risks a duplicate write. The flow
/// author branches on the node 'error' output handle instead. The write is value-idempotent (full-list
/// replace) and the ClientRequestID is stable, so a deliberate author-driven retry is safe.
/// </summary>
public sealed class BackendCustomerStatusClient
{
    private const string EndpointPath = "/api/internal/customer-feature-groups/update";
    private const string TokenHeaderName = "X-Internal-Service-Token";

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

            // Non-2xx: parse the coded body best-effort so the node error handle surfaces an actionable reason.
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
            // HttpClient timeout — UNKNOWN outcome; do NOT retry (route to error handle).
            _logger.SystemWarn(
                $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: timeout (unknown outcome) " +
                $"(tenant={request.TenantId}, requestId={requestId})");
            return BackendCustomerStatusOutcome.Fail(ErrorCodes.CustomerStatusUpdateUpstreamFailed,
                "Durum servisi zaman aşımına uğradı (sonuç belirsiz).");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.SetCustomerStatusActionFailed}] BackendCustomerStatusClient: transport error " +
                $"(tenant={request.TenantId}, requestId={requestId}): {ex.Message}");
            return BackendCustomerStatusOutcome.Fail(ErrorCodes.CustomerStatusUpdateUpstreamFailed,
                "Durum servisine ulaşılamadı.");
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
