using System.Net.Http.Headers;
using System.Net.Http.Json;
using Hangfire;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services.Jobs;

/// <summary>
/// Paket B-META: thin Hangfire wrapper over Backend's
/// <c>/api/internal/meta-leadgen/process-lead</c>. Enqueued by Backend's
/// <c>MetaLeadgenWebhookService</c> when a signed Meta webhook POST arrives
/// and is deduped into <c>meta_leadgen_events</c>. The job's sole job is to
/// HTTP-hop back to Backend where Graph API fetch + canonical mapping + LIW
/// intake + <c>meta_leadgen_events.lead_id</c> back-fill all happen.
///
/// Why split? Meta requires a 200 from the webhook within 3s; the Graph API
/// fetch + phone normalize + lead UPSERT + welcome enqueue chain takes longer
/// in the worst case. Hangfire gives us durable retry on Graph API transient
/// failures (INV-META-003) without exposing Meta to our internal latency.
///
/// Retry policy:
///   * <see cref="AutomaticRetryAttribute.Attempts"/> = 3 with
///     <c>DelaysInSeconds = {30, 120, 600}</c> — matches Meta Graph API's
///     typical recovery window for rate limit / transient 5xx. After the
///     final attempt fails Hangfire moves the job to <c>failed</c> state
///     and the event row's error_code carries the terminal INV-META-* code.
///   * Non-retriable errors (INV-META-004 parse / INV-META-006 token missing)
///     still consume retry attempts by design — re-running a terminal error
///     is cheap (one HTTP 422/500) and ensures config fixes are picked up on
///     the next attempt without operator re-enqueue.
/// </summary>
[Queue("automation")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 600 })]
public sealed class MetaLeadgenIntakeJob
{
    public const string HttpClientName = LifecycleBackendClientExtensions.BackendInternalHttpClient;

    private const string EndpointPath    = "/api/internal/meta-leadgen/process-lead";
    private const string TokenHeaderName = "X-Internal-Service-Token";

    private readonly IHttpClientFactory _httpFactory;
    private readonly string _sharedSecret;
    private readonly JwtGenerator _jwt;
    private readonly JsonLinesLogger _logger;

    public MetaLeadgenIntakeJob(
        IHttpClientFactory httpFactory,
        MetaLeadgenSharedSecretHolder secretHolder,
        JwtGenerator jwt,
        JsonLinesLogger logger)
    {
        _httpFactory = httpFactory;
        _sharedSecret = secretHolder.Value;
        _jwt = jwt;
        _logger = logger;
    }

    /// <summary>
    /// Hangfire entry point. <paramref name="tenantId"/>, <paramref name="leadgenId"/>,
    /// and <paramref name="eventId"/> come from <c>MetaLeadgenWebhookService</c>'s
    /// enqueue call.
    /// </summary>
    public async Task ExecuteAsync(int tenantId, string leadgenId, long eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_sharedSecret))
        {
            // Config gap — can't proceed without the shared secret. Throwing here
            // triggers Hangfire's [AutomaticRetry 3x {30,120,600}s] policy so the
            // moment an operator configures InternalServices:SharedSecret inside
            // the retry window the job recovers on its own. Returning silently
            // would be a DURABLE DROP (Hangfire marks the job Succeeded, no
            // subsequent signal re-enqueues the meta_leadgen_events row), so
            // this throw is the correctness anchor: ops alert on 3x retry
            // exhaustion rather than a lost lead.
            var msg = $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] MetaLeadgenIntakeJob: " +
                      $"InternalServices:SharedSecret not configured " +
                      $"(tenant={tenantId}, leadgen={leadgenId}, eventId={eventId}) — " +
                      $"retrying via Hangfire until secret is populated or retries exhaust";
            _logger.SystemError(msg);
            throw new InvalidOperationException(msg);
        }

        var client = _httpFactory.CreateClient(HttpClientName);
        using var req = new HttpRequestMessage(HttpMethod.Post, EndpointPath);
        var serviceJwt = _jwt.GenerateServiceToken(tenantId);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceJwt);
        req.Headers.TryAddWithoutValidation(TokenHeaderName, _sharedSecret);
        req.Content = JsonContent.Create(new ProcessLeadHopRequest
        {
            TenantId  = tenantId,
            LeadgenId = leadgenId,
            EventId   = eventId
        });

        try
        {
            using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.StepInfo(
                    $"MetaLeadgenIntakeJob: process-lead OK tenant={tenantId} leadgen={leadgenId} eventId={eventId}",
                    requestId: leadgenId);
                return;
            }

            // Non-2xx: 5xx => Hangfire retry via throw; 4xx => terminal, log + return
            // (retrying a 4xx won't fix the underlying config/payload issue and would
            // just burn Hangfire worker cycles).
            //
            // Error-code semantic: this is an Automation↔Backend INTERNAL hop failure,
            // NOT a Graph API failure. INV-AT-070 (AutomationBackendIntakeUnavailable)
            // is the canonical code for "Backend internal endpoint unreachable/broken"
            // per arch/errors.md. Previously mislabeled as INV-META-003
            // (MetaGraphApiFetchFailed) — Codex iter 0 CQ12 caught the drift.
            var status = (int)response.StatusCode;
            if (status >= 500)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] MetaLeadgenIntakeJob: " +
                    $"process-lead 5xx (status={status}, tenant={tenantId}, leadgen={leadgenId}, eventId={eventId}) — Hangfire will retry");
                throw new HttpRequestException(
                    $"process-lead returned HTTP {status}; transient.");
            }

            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] MetaLeadgenIntakeJob: " +
                $"process-lead {status} terminal (tenant={tenantId}, leadgen={leadgenId}, eventId={eventId}) — " +
                $"inspect Backend logs for rejected contract; audit row error_code is set by the Backend handler");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationBackendIntakeUnavailable}] MetaLeadgenIntakeJob: process-lead timeout " +
                $"(tenant={tenantId}, leadgen={leadgenId}, eventId={eventId}): {ex.Message}");
            throw; // retry
        }
    }

    /// <summary>Body shape for the process-lead hop. Mirrors Backend-side ProcessLeadRequest.</summary>
    private sealed class ProcessLeadHopRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("tenant_id")]  public int    TenantId  { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("leadgen_id")] public string LeadgenId { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("event_id")]   public long   EventId   { get; set; }
    }
}

/// <summary>
/// Program.cs uses this named-client constant so the HttpClient factory
/// registration + the job's own factory.CreateClient call stay in sync.
/// </summary>
internal static class LifecycleBackendClientExtensions
{
    public const string BackendInternalHttpClient = "backend-internal";
}

/// <summary>
/// Captures the shared secret at DI-registration time so the job (transient
/// lifecycle per Hangfire convention) doesn't need to re-read IConfiguration
/// on every invocation. A dedicated holder type keeps the DI signature
/// explicit and testable — passing a bare <c>string</c> into the constructor
/// trips the DI container since string isn't a registered service.
/// </summary>
public sealed class MetaLeadgenSharedSecretHolder
{
    public string Value { get; }
    public MetaLeadgenSharedSecretHolder(string value) => Value = value ?? string.Empty;
}
