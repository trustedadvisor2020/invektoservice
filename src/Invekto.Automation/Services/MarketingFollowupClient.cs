using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Followup;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// FEAT-EFS Automation → Marketing trigger hop. Wraps a typed
/// <see cref="HttpClient"/> that POSTs to the Marketing service's
/// <c>/api/internal/followup/trigger</c> endpoint.
///
/// Mirrors the FEAT-LIW Chunk B <see cref="BackendIntakeClient"/> pattern (X-Internal-
/// Service-Token + per-call service JWT, inline single-retry on 5xx/transient). One
/// retry max so a persistently-down Marketing never blocks NoReplyCheckJob's queue
/// drain for more than (HttpClient timeout + 500 ms).
///
/// Failures are surfaced via the typed <see cref="FollowupTriggerOutcome"/> so callers
/// can decide whether to log + drop or re-queue. Rule-of-thumb: log + drop. The trigger
/// is best-effort; missed triggers do not lose customer data (only delay the drip).
/// </summary>
public sealed class MarketingFollowupClient
{
    private const string EndpointPath = "/api/internal/followup/trigger";
    private const string TokenHeaderName = "X-Internal-Service-Token";
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly HttpClient _http;
    private readonly string _sharedSecret;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _log;

    public MarketingFollowupClient(
        HttpClient http,
        string sharedSecret,
        JwtGenerator jwt,
        JsonLinesLogger log)
    {
        _http = http;
        _sharedSecret = sharedSecret;
        _jwt = jwt;
        _log = log;
    }

    /// <summary>
    /// POST a trigger to Marketing. Returns the parsed outcome on a 2xx body, or a
    /// fail-shaped outcome on any error path. Caller uses
    /// <see cref="FollowupTriggerOutcome.Accepted"/> to gate downstream work.
    /// </summary>
    public async Task<FollowupTriggerOutcome> TriggerAsync(
        int tenantId,
        long leadId,
        FollowupTriggerReason reason,
        string? sequenceSlug,
        string requestId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_sharedSecret))
        {
            _log.SystemWarn(
                $"[{ErrorCodes.AuthUnauthorized}] MarketingFollowupClient: " +
                $"InternalServices:SharedSecret not configured, skipping trigger " +
                $"(tenant={tenantId}, lead={leadId}, reason={reason}, request={requestId}).");
            return FollowupTriggerOutcome.Fail(ErrorCodes.AuthUnauthorized, "Shared secret missing");
        }

        var body = new FollowupTriggerRequest
        {
            TenantId = tenantId,
            LeadId = leadId,
            Reason = reason,
            SequenceSlug = sequenceSlug,
            RequestId = requestId
        };

        var first = await TrySendAsync(body, requestId, attempt: 1, ct).ConfigureAwait(false);
        if (first.Final) return first.Outcome;

        try
        {
            await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return first.Outcome;
        }

        var retry = await TrySendAsync(body, requestId, attempt: 2, ct).ConfigureAwait(false);
        return retry.Outcome;
    }

    private async Task<TrySendResult> TrySendAsync(
        FollowupTriggerRequest body, string requestId, int attempt, CancellationToken ct)
    {
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, EndpointPath);
            // Layer 1: tenant-bound JWT (Marketing handler enforces tenant binding by
            // matching the JWT tenant_id claim against body.tenant_id).
            var serviceJwt = _jwt.GenerateServiceToken(body.TenantId);
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
            // Layer 2: peer-service identity proof, separate from JWT (defense in depth —
            // a leaked tenant JWT alone cannot reach this internal endpoint).
            msg.Headers.TryAddWithoutValidation(TokenHeaderName, _sharedSecret);
            msg.Headers.TryAddWithoutValidation("X-Request-Id", requestId);
            msg.Content = JsonContent.Create(body, options: JsonOpts);

            using var response = await _http.SendAsync(
                msg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            // Error code classification (lessons 2026-04-21 Codex iter 2 CQ1/CQ5/CQ12):
            //   - Transport/timeout/parse failures on OUR side → INV-MK-057
            //     (FollowupUpstreamUnavailable) — transient retry class.
            //   - Marketing's structured body error codes (INV-MK-050/053/054/055) are
            //     preserved verbatim when present — they describe VALIDATION / business
            //     rule rejections from the orchestrator and must not be relabeled.
            //   - Generic HTTP fail with no parseable body → INV-MK-057 transient.
            if (!response.IsSuccessStatusCode)
            {
                var retriable = (int)response.StatusCode >= 500;
                _log.SystemWarn(
                    $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupClient: " +
                    $"Marketing returned HTTP {(int)response.StatusCode} " +
                    $"(attempt={attempt}, tenant={body.TenantId}, lead={body.LeadId}, request={requestId}).");

                // Even on non-2xx Marketing returns a structured FollowupTriggerResponse
                // body (single-envelope rule). Preserve its INV-MK-* code verbatim if
                // present; otherwise classify as transient upstream failure.
                var parsed = await TryParseAsync(response, requestId, ct).ConfigureAwait(false);
                var fail = FollowupTriggerOutcome.Fail(
                    parsed?.ErrorCode ?? ErrorCodes.FollowupUpstreamUnavailable,
                    parsed?.ErrorMessage ?? $"Marketing HTTP {(int)response.StatusCode}");
                return new TrySendResult(fail, Final: !retriable);
            }

            var ok = await TryParseAsync(response, requestId, ct).ConfigureAwait(false);
            if (ok is null)
                return new TrySendResult(
                    FollowupTriggerOutcome.Fail(ErrorCodes.FollowupUpstreamUnavailable, "Empty response body"),
                    Final: true);

            return new TrySendResult(
                ok.Accepted
                    ? FollowupTriggerOutcome.Success(ok.AbGroup, ok.ScheduledRuns, ok.SequenceId)
                    // Marketing's structured error (orchestrator reject) — preserve code.
                    : FollowupTriggerOutcome.Fail(
                        ok.ErrorCode ?? ErrorCodes.FollowupUpstreamUnavailable,
                        ok.ErrorMessage ?? "Marketing rejected trigger"),
                Final: true);
        }
        catch (HttpRequestException ex)
        {
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupClient: HTTP transport error " +
                $"(attempt={attempt}, tenant={body.TenantId}, lead={body.LeadId}, request={requestId}): {ex.Message}");
            return new TrySendResult(
                FollowupTriggerOutcome.Fail(ErrorCodes.FollowupUpstreamUnavailable, ex.Message),
                Final: false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // TaskCanceledException without ct cancellation = HttpClient timeout.
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupClient: timeout " +
                $"(attempt={attempt}, tenant={body.TenantId}, lead={body.LeadId}, request={requestId}).");
            return new TrySendResult(
                FollowupTriggerOutcome.Fail(ErrorCodes.FollowupUpstreamUnavailable, "HTTP timeout"),
                Final: false);
        }
    }

    private async Task<FollowupTriggerResponse?> TryParseAsync(
        HttpResponseMessage response, string requestId, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<FollowupTriggerResponse>(JsonOpts, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            // Parse failure on OUR side = transient upstream fail class, not a
            // config-invalid (which would misdirect operator to check sequence config).
            _log.SystemWarn(
                $"[{ErrorCodes.FollowupUpstreamUnavailable}] MarketingFollowupClient: " +
                $"unparseable response body (request={requestId}): {ex.Message}");
            return null;
        }
    }

    private readonly record struct TrySendResult(FollowupTriggerOutcome Outcome, bool Final);
}

/// <summary>
/// Caller-friendly result type — wraps either the Marketing accept payload or a coded
/// failure. Mirrors the FollowupTriggerResponse shape but stripped of wire-only fields.
/// </summary>
public sealed class FollowupTriggerOutcome
{
    public bool Accepted { get; private init; }
    public string? AbGroup { get; private init; }
    public int ScheduledRuns { get; private init; }
    public long? SequenceId { get; private init; }
    public string? ErrorCode { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static FollowupTriggerOutcome Success(string? abGroup, int scheduledRuns, long? sequenceId) => new()
    {
        Accepted = true,
        AbGroup = abGroup,
        ScheduledRuns = scheduledRuns,
        SequenceId = sequenceId
    };

    public static FollowupTriggerOutcome Fail(string errorCode, string errorMessage) => new()
    {
        Accepted = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
