using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Followup;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Backend.Services;

/// <summary>
/// FEAT-EFS Backend → Marketing SPA-facing proxy. The Dashboard SPA hits Backend
/// (which already owns the user's tenant JWT validation chain); Backend in turn calls
/// Marketing with a freshly-minted service JWT bound to the SAME tenant_id. This keeps
/// the SPA from needing to know about Marketing's port + secret config.
///
/// Auth chain:
///   SPA ──Bearer&lt;user-jwt&gt;──► Backend (UseJwtAuth validates, sets TenantContext)
///   Backend ──Bearer&lt;service-jwt minted with TenantContext.TenantId&gt;──► Marketing
///   Marketing UseJwtAuth("/api/v1/") validates → sets TenantContext → handler reads
///
/// Note: this is a TENANT-SCOPED proxy (uses /api/v1/followup/...), NOT the internal
/// trigger endpoint (which is Automation → Marketing only with X-Internal-Service-Token).
/// </summary>
public sealed class MarketingFollowupProxyClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly HttpClient _http;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _log;

    public MarketingFollowupProxyClient(HttpClient http, JwtGenerator jwt, JsonLinesLogger log)
    {
        _http = http;
        _jwt = jwt;
        _log = log;
    }

    /// <summary>
    /// GET /api/v1/followup/sequences. Returns the parsed envelope (sequences + tenant
    /// test_mode + no_reply_threshold_days), or a fail-shaped result on transport failure.
    /// The test_mode flag is piped through to the Dashboard editor so unit labels and
    /// summary text reflect the actual tenant setting (Codex iter 0 CQ10 fix).
    /// </summary>
    public async Task<ProxyResult<FollowupSequenceListEnvelope>> ListSequencesAsync(
        int tenantId, string requestId, CancellationToken ct)
    {
        return await ForwardListAsync(
            HttpMethod.Get, "/api/v1/followup/sequences",
            tenantId, requestId, ct).ConfigureAwait(false);
    }

    /// <summary>PUT /api/v1/followup/sequences with the user's edited config.</summary>
    public async Task<ProxyResult<FollowupSequenceConfig>> UpsertSequenceAsync(
        int tenantId, FollowupSequenceConfig body, string requestId, CancellationToken ct)
    {
        return await ForwardJsonAsync<FollowupSequenceConfig>(
            HttpMethod.Put, "/api/v1/followup/sequences", body,
            tenantId, requestId, ct).ConfigureAwait(false);
    }

    /// <summary>GET /api/v1/followup/runs. Returns list of recent run rows for the tenant.</summary>
    public async Task<ProxyResult<List<FollowupRunSummary>>> ListRecentRunsAsync(
        int tenantId, string requestId, CancellationToken ct)
    {
        return await ForwardJsonAsync<List<FollowupRunSummary>>(
            HttpMethod.Get, "/api/v1/followup/runs", body: null,
            tenantId, requestId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Specialized forward for GET /api/v1/followup/sequences because that endpoint
    /// returns an extended envelope (<c>data</c> + <c>test_mode</c> + <c>no_reply_threshold_days</c>)
    /// rather than the generic <c>{ data: T }</c> shape used by other routes.
    /// </summary>
    private async Task<ProxyResult<FollowupSequenceListEnvelope>> ForwardListAsync(
        HttpMethod method, string path,
        int tenantId, string requestId, CancellationToken ct)
    {
        try
        {
            using var msg = new HttpRequestMessage(method, path);
            var serviceJwt = _jwt.GenerateServiceToken(tenantId);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
            msg.Headers.TryAddWithoutValidation("X-Request-Id", requestId);

            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                    $"upstream HTTP {(int)response.StatusCode} for {method} {path} (tenant={tenantId}, request={requestId}).");
                return ProxyResult<FollowupSequenceListEnvelope>.UpstreamError((int)response.StatusCode, rawBody);
            }

            var envelope = JsonSerializer.Deserialize<FollowupSequenceListEnvelope>(rawBody, JsonOpts);
            if (envelope is null)
            {
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                    $"empty/invalid envelope for {method} {path} (tenant={tenantId}, request={requestId}).");
                return ProxyResult<FollowupSequenceListEnvelope>.UpstreamError(502, "Invalid upstream envelope");
            }
            envelope.Data ??= new List<FollowupSequenceConfig>();
            return ProxyResult<FollowupSequenceListEnvelope>.Ok(envelope);
        }
        catch (HttpRequestException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                $"transport error (tenant={tenantId}, request={requestId}): {ex.Message}");
            return ProxyResult<FollowupSequenceListEnvelope>.UpstreamError(502, ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                $"timeout (tenant={tenantId}, request={requestId}).");
            return ProxyResult<FollowupSequenceListEnvelope>.UpstreamError(504, "Upstream timeout");
        }
        catch (JsonException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                $"unparseable upstream body (tenant={tenantId}, request={requestId}): {ex.Message}");
            return ProxyResult<FollowupSequenceListEnvelope>.UpstreamError(502, "Unparseable upstream body");
        }
    }

    private async Task<ProxyResult<T>> ForwardJsonAsync<T>(
        HttpMethod method, string path, object? body,
        int tenantId, string requestId, CancellationToken ct)
        where T : class
    {
        try
        {
            using var msg = new HttpRequestMessage(method, path);
            var serviceJwt = _jwt.GenerateServiceToken(tenantId);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
            msg.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
            if (body is not null)
                msg.Content = JsonContent.Create(body, options: JsonOpts);

            using var response = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            var rawBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                    $"upstream HTTP {(int)response.StatusCode} for {method} {path} (tenant={tenantId}, request={requestId}).");
                return ProxyResult<T>.UpstreamError((int)response.StatusCode, rawBody);
            }

            // Marketing returns { data: T } envelope per its endpoint convention.
            var envelope = JsonSerializer.Deserialize<DataEnvelope<T>>(rawBody, JsonOpts);
            if (envelope?.Data is null)
            {
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                    $"empty/invalid 'data' field in upstream body (tenant={tenantId}, request={requestId}).");
                return ProxyResult<T>.UpstreamError(502, "Invalid upstream envelope");
            }
            return ProxyResult<T>.Ok(envelope.Data);
        }
        catch (HttpRequestException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                $"transport error (tenant={tenantId}, request={requestId}): {ex.Message}");
            return ProxyResult<T>.UpstreamError(502, ex.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                $"timeout (tenant={tenantId}, request={requestId}).");
            return ProxyResult<T>.UpstreamError(504, "Upstream timeout");
        }
        catch (JsonException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupProxyClient: " +
                $"unparseable upstream body (tenant={tenantId}, request={requestId}): {ex.Message}");
            return ProxyResult<T>.UpstreamError(502, "Unparseable upstream body");
        }
    }

    private sealed class DataEnvelope<T>
    {
        public T? Data { get; set; }
    }
}

/// <summary>
/// Caller-friendly result. <see cref="Value"/> populated when <see cref="Ok"/>;
/// <see cref="StatusCode"/> + <see cref="ErrorBody"/> populated otherwise so the
/// endpoint handler can echo Marketing's exact response to the SPA.
/// </summary>
public sealed class ProxyResult<T> where T : class
{
    public bool IsOk { get; private init; }
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public string? ErrorBody { get; private init; }

    public static ProxyResult<T> Ok(T value) => new() { IsOk = true, Value = value, StatusCode = 200 };
    public static ProxyResult<T> UpstreamError(int statusCode, string? body) => new()
    {
        IsOk = false,
        StatusCode = statusCode,
        ErrorBody = body
    };
}

/// <summary>Lightweight projection of event_followup_runs row for the SPA Runs tab.</summary>
public sealed class FollowupRunSummary
{
    public long Id { get; set; }
    public long SequenceId { get; set; }
    public long LeadId { get; set; }
    public int StageIndex { get; set; }
    public string AbGroup { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Extended envelope returned by Marketing's GET /api/v1/followup/sequences route.
/// Carries tenant settings (test_mode, no_reply_threshold_days) alongside the sequence
/// list so the Dashboard editor can render unit labels consistent with backend
/// validation + scheduling behavior (Codex iter 0 CQ10 fix).
/// </summary>
public sealed class FollowupSequenceListEnvelope
{
    [System.Text.Json.Serialization.JsonPropertyName("data")]
    public List<FollowupSequenceConfig>? Data { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("test_mode")]
    public bool TestMode { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("no_reply_threshold_days")]
    public int NoReplyThresholdDays { get; set; }
}
