using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Logging;

namespace Invekto.Shared.Contracts.Inma;

/// <summary>
/// FEAT-PROJELER / cxapi — PKT-14 S3.
/// READ-ONLY typed client that lists a tenant's WhatsApp approved (HSM) templates
/// via cxapi <c>POST /api/templates</c> (body <c>{ instanceID }</c>), parses the
/// WapCRM envelope, and returns a typed <see cref="WapCrmTemplateListResult"/>.
/// There is NO send path — this powers the Projeler wizard template picker only.
///
/// Auth (codex P0 #1): the secret rides PER REQUEST via
/// <c>HttpRequestMessage.Headers.TryAddWithoutValidation("X-CIB-SecretKey", ...)</c>
/// — NEVER on <c>DefaultRequestHeaders</c> — so one pooled HttpClient serves many
/// tenants with no cross-tenant secret leak. The secret is never stored on the
/// instance and never logged.
///
/// SSRF mitigation: the egress host is the FIXED server config
/// <see cref="WapCrmTemplateOptions.BaseUrl"/> (set on the pooled HttpClient at DI
/// time) and the path is the compile-time constant <c>api/templates</c>; the
/// tenant-controlled <c>WapCrmSettings.ApiUrl</c> is never used as a target.
/// Register the handler with <c>AllowAutoRedirect=false</c> (cxapi 301/302 is a
/// rate-limit signal, not a redirect) and <c>UseCookies=false</c>.
///
/// Idempotent-read doctrine: a timeout is <see cref="WapCrmTemplateListOutcome.TimedOut"/>
/// (a GET has no duplicate-send risk → the caller maps it to 504, NOT the send
/// client's "ambiguous"); a 301/302 is <see cref="WapCrmTemplateListOutcome.RateLimited"/>
/// with no internal retry loop (the interactive caller retries).
/// </summary>
public sealed class WapCrmTemplateClient
{
    private readonly HttpClient _httpClient;
    private readonly WapCrmTemplateOptions _options;
    private readonly JsonLinesLogger _logger;

    private const string SecretKeyHeader = "X-CIB-SecretKey";
    private const string TemplatesPath = "api/templates";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WapCrmTemplateClient(HttpClient httpClient, WapCrmTemplateOptions options, JsonLinesLogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists the approved templates for one cxapi instance. Returns a typed outcome —
    /// it does not throw on provider/transport failure (only on a caller-contract
    /// violation or caller cancellation).
    /// </summary>
    public async Task<WapCrmTemplateListResult> ListTemplatesAsync(
        string secretKey, int instanceId, int tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("SecretKey is required.", nameof(secretKey));
        if (instanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(instanceId), "InstanceId must be a positive integer.");
        // TryAddWithoutValidation skips header validation, so refuse CR/LF/control
        // chars in the secret to close any header-injection vector before attaching.
        if (ContainsControlChars(secretKey))
            throw new ArgumentException("SecretKey contains invalid control characters.", nameof(secretKey));

        var payloadJson = JsonSerializer.Serialize(new Payload { InstanceId = instanceId });

        // A fresh request + content with the secret on THIS request only — never on
        // DefaultRequestHeaders (a pooled client serves many tenants).
        using var req = new HttpRequestMessage(HttpMethod.Post, TemplatesPath)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        if (!req.Headers.TryAddWithoutValidation(SecretKeyHeader, secretKey))
            throw new InvalidOperationException("Failed to attach the WapCRM secret header.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.TimeoutMs);

        try
        {
            using var response = await _httpClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
            var http = (int)response.StatusCode;

            // cxapi uses a redirect status as a rate-limit signal (AllowAutoRedirect=false).
            if (IsRedirectRateLimit(http))
                return RateLimited(tenantId, instanceId, http, http.ToString(), ParseRetryAfter(response));

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            WapCrmApiResponse<List<WapCrmTemplateDto>>? env;
            try
            {
                env = JsonSerializer.Deserialize<WapCrmApiResponse<List<WapCrmTemplateDto>>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                env = null;
            }

            if (env == null)
                return Transport(tenantId, instanceId, http, "Unparseable cxapi response.");

            // status==true on a 2xx is the only success; an empty data[] is a valid empty list.
            if (response.IsSuccessStatusCode && env.Status)
            {
                return new WapCrmTemplateListResult
                {
                    Outcome = WapCrmTemplateListOutcome.Success,
                    Templates = env.Data ?? [],
                    HttpStatusCode = http,
                    ProviderStatusCode = env.StatusCode,
                    ProviderRequestId = env.RequestId,
                    TenantId = tenantId,
                    InstanceId = instanceId
                };
            }

            // An envelope rate-limit code while status!=true is a genuine rate-limit.
            if (IsEnvelopeRateLimit(env.StatusCode))
                return RateLimited(tenantId, instanceId, http, env.StatusCode, ParseRetryAfter(response));

            // Provider spoke and rejected (2xx-with-status=false, or a parseable non-2xx envelope).
            return new WapCrmTemplateListResult
            {
                Outcome = WapCrmTemplateListOutcome.ProviderRejected,
                HttpStatusCode = http,
                ProviderStatusCode = env.StatusCode,
                ProviderRequestId = env.RequestId,
                ProviderMessage = env.Message,
                TenantId = tenantId,
                InstanceId = instanceId
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // caller cancellation / shutdown — propagate, do not classify or log as a failure
        }
        catch (OperationCanceledException)
        {
            // Our per-attempt timeout fired. A read has no duplicate side-effect → distinct TimedOut (caller → 504).
            _logger.SystemWarn(
                $"[cxapi-templates] timeout after {_options.TimeoutMs}ms: tenant={tenantId}, instance={instanceId}");
            return new WapCrmTemplateListResult
            {
                Outcome = WapCrmTemplateListOutcome.TimedOut,
                HttpStatusCode = 0,
                TenantId = tenantId,
                InstanceId = instanceId
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[cxapi-templates] transport error: tenant={tenantId}, instance={instanceId}, error={ex.Message}");
            return Transport(tenantId, instanceId, 0, $"Transport error: {ex.Message}");
        }
    }

    private static bool IsRedirectRateLimit(int httpStatus) => httpStatus is 301 or 302;

    private static bool IsEnvelopeRateLimit(string? statusCode) => statusCode is "301" or "302";

    private static bool ContainsControlChars(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
                return true;
        }
        return false;
    }

    private static TimeSpan? ParseRetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? ra = response.Headers.RetryAfter;
        if (ra == null)
            return null;
        if (ra.Delta.HasValue)
            return ra.Delta;
        if (ra.Date.HasValue)
        {
            var delta = ra.Date.Value - DateTimeOffset.UtcNow;
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }
        return null;
    }

    private static WapCrmTemplateListResult RateLimited(
        int tenantId, int instanceId, int http, string? providerCode, TimeSpan? retryAfter) => new()
    {
        Outcome = WapCrmTemplateListOutcome.RateLimited,
        HttpStatusCode = http,
        ProviderStatusCode = providerCode,
        RetryAfter = retryAfter,
        TenantId = tenantId,
        InstanceId = instanceId
    };

    private static WapCrmTemplateListResult Transport(int tenantId, int instanceId, int http, string message) => new()
    {
        Outcome = WapCrmTemplateListOutcome.TransportError,
        HttpStatusCode = http,
        ProviderMessage = message,
        TenantId = tenantId,
        InstanceId = instanceId
    };

    /// <summary>cxapi /api/templates wire body. Explicit name so instanceID casing is never mangled by a naming policy.</summary>
    private sealed class Payload
    {
        [JsonPropertyName("instanceID")] public int InstanceId { get; init; }
    }
}
