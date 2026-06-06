using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Logging;

namespace Invekto.Shared.Contracts.Inma;

/// <summary>
/// FEAT-PROJELER / cxapi send engine — PR-2.
/// Typed client that POSTs a PLAIN-TEXT message to cxapi
/// <c>POST /api/chatoperation</c>, parses the WapCRM envelope, and maps it to a
/// typed <see cref="WapCrmSendResult"/>.
///
/// DEV-ONLY in PR-2: it is registered in Outbound DI but NOTHING in production
/// resolves it — the sender route, state machine and ext_id matching land in
/// PR-3. The bridge (<c>MainAppCallbackClient</c> / MessageSenderService) path is
/// untouched, so this PR is a behavioural no-op.
///
/// Auth (Codex P0 #1): the secret is attached PER REQUEST via
/// <c>HttpRequestMessage.Headers.TryAddWithoutValidation("X-CIB-SecretKey", ...)</c>
/// — NEVER on <c>DefaultRequestHeaders</c> — so a single pooled HttpClient can
/// serve many tenants with no cross-tenant secret leak. The secret is never
/// stored on the instance and never logged.
///
/// Retry doctrine (Codex P0 #2/#6): only a rate limit (HTTP 301/302, or an
/// envelope statusCode '301'/'302' while status != true) is delay-retried — the
/// message was provably not accepted. A timeout is <see cref="WapCrmSendOutcome.Ambiguous"/>
/// (may have reached the server → never auto-retried). Register with
/// <c>AllowAutoRedirect=false</c> and <c>UseCookies=false</c>.
/// </summary>
public sealed class WapCrmSendClient
{
    private readonly HttpClient _httpClient;
    private readonly WapCrmSendOptions _options;
    private readonly JsonLinesLogger _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<int, int> _jitter;

    private const string SecretKeyHeader = "X-CIB-SecretKey";
    private const string ChatOperationPath = "api/chatoperation";
    private const int TextMessageType = 1; // 1 = text (2 = image), per the cxapi guide

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <param name="delay">Backoff delay seam (defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>); injected as a no-op in tests for instant, deterministic runs.</param>
    /// <param name="jitter">Jitter seam mapping a max-ms bound to an actual ms value (defaults to <see cref="Random.Shared"/>); injected to return 0 in tests.</param>
    public WapCrmSendClient(
        HttpClient httpClient,
        WapCrmSendOptions options,
        JsonLinesLogger logger,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<int, int>? jitter = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _delay = delay ?? ((d, token) => Task.Delay(d, token));
        _jitter = jitter ?? (max => Random.Shared.Next(0, max + 1));
    }

    /// <summary>
    /// Sends a single plain-text message. Returns a typed outcome — it does not
    /// throw on provider/transport failures (only on a caller-contract violation
    /// or caller cancellation).
    /// </summary>
    public async Task<WapCrmSendResult> SendPlainTextAsync(WapCrmSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SecretKey))
            throw new ArgumentException("SecretKey is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ChatPhoneNumber))
            throw new ArgumentException("ChatPhoneNumber is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.MessageText))
            throw new ArgumentException("MessageText is required.", nameof(request));
        if (request.InstanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "InstanceId must be a positive integer.");
        if (request.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "UserId must be a positive integer.");

        var payloadJson = JsonSerializer.Serialize(new Payload
        {
            InstanceId = request.InstanceId,
            UserId = request.UserId,
            ChatPhoneNumber = request.ChatPhoneNumber,
            MessageType = TextMessageType,
            MessageText = request.MessageText
        });

        var attempt = 0;
        string? lastProviderStatusCode = null;
        var lastHttp = 0;

        while (true)
        {
            attempt++;
            var rateLimited = false;
            TimeSpan? retryAfter = null;

            // A FRESH request + content per attempt (an HttpRequestMessage cannot be resent),
            // with the secret on THIS request only — never on DefaultRequestHeaders.
            using var req = new HttpRequestMessage(HttpMethod.Post, ChatOperationPath)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            if (!req.Headers.TryAddWithoutValidation(SecretKeyHeader, request.SecretKey))
                throw new InvalidOperationException("Failed to attach the WapCRM secret header.");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_options.TimeoutMs);

            try
            {
                using var response = await _httpClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
                var http = (int)response.StatusCode;

                if (IsRedirectRateLimit(http))
                {
                    rateLimited = true;
                    retryAfter = ParseRetryAfter(response);
                    lastProviderStatusCode = http.ToString();
                    lastHttp = http;
                }
                else
                {
                    var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                    WapCrmApiResponse<JsonElement>? env;
                    try
                    {
                        env = JsonSerializer.Deserialize<WapCrmApiResponse<JsonElement>>(body, JsonOptions);
                    }
                    catch (JsonException)
                    {
                        env = null;
                    }

                    if (env == null)
                        return Transport(request, attempt, http, $"Unparseable cxapi response (HTTP {http}).");

                    // status==true is AUTHORITATIVE: an accepted message is never retried,
                    // even if statusCode coincidentally resembles a rate-limit code.
                    if (response.IsSuccessStatusCode && env.Status)
                    {
                        return new WapCrmSendResult
                        {
                            Outcome = WapCrmSendOutcome.Submitted,
                            HttpStatusCode = http,
                            ProviderStatus = true,
                            ProviderStatusCode = env.StatusCode,
                            ProviderRequestId = env.RequestId,
                            ProviderErrorMessage = env.Message,
                            AttemptCount = attempt,
                            TenantId = request.TenantId,
                            InstanceId = request.InstanceId
                        };
                    }

                    if (IsEnvelopeRateLimit(env.StatusCode))
                    {
                        // status != true here → a genuine rate-limit rejection (safe to retry).
                        rateLimited = true;
                        retryAfter = ParseRetryAfter(response);
                        lastProviderStatusCode = env.StatusCode;
                        lastHttp = http;
                    }
                    else
                    {
                        return new WapCrmSendResult
                        {
                            Outcome = WapCrmSendOutcome.ProviderFailed,
                            HttpStatusCode = http,
                            ProviderStatus = env.Status,
                            ProviderStatusCode = env.StatusCode,
                            ProviderRequestId = env.RequestId,
                            ProviderErrorMessage = env.Message,
                            AttemptCount = attempt,
                            TenantId = request.TenantId,
                            InstanceId = request.InstanceId
                        };
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // caller cancellation / shutdown — propagate, do not classify
            }
            catch (OperationCanceledException)
            {
                // Our per-attempt timeout fired — the request may already be on the server.
                _logger.SystemWarn(
                    $"[cxapi-send] timeout after {_options.TimeoutMs}ms -> ambiguous: tenant={request.TenantId}, instance={request.InstanceId}, attempt={attempt}");
                return Ambiguous(request, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn(
                    $"[cxapi-send] transport error: tenant={request.TenantId}, instance={request.InstanceId}, attempt={attempt}, error={ex.Message}");
                return Transport(request, attempt, 0, $"Transport error: {ex.Message}");
            }

            // ── rate-limit path (the only retried outcome) ──
            if (rateLimited)
            {
                if (attempt <= _options.MaxRateLimitRetries)
                {
                    var delay = ComputeBackoff(attempt - 1, retryAfter);
                    _logger.SystemInfo(
                        $"[cxapi-send] rate-limited ({lastProviderStatusCode}) -> backoff {(int)delay.TotalMilliseconds}ms: tenant={request.TenantId}, instance={request.InstanceId}, attempt={attempt}");
                    await _delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                _logger.SystemWarn(
                    $"[cxapi-send] rate-limit retries exhausted: tenant={request.TenantId}, instance={request.InstanceId}, attempts={attempt}");
                return new WapCrmSendResult
                {
                    Outcome = WapCrmSendOutcome.RateLimited,
                    HttpStatusCode = lastHttp,
                    ProviderStatusCode = lastProviderStatusCode,
                    RetryAfter = retryAfter,
                    AttemptCount = attempt,
                    TenantId = request.TenantId,
                    InstanceId = request.InstanceId
                };
            }
        }
    }

    private static bool IsRedirectRateLimit(int httpStatus) => httpStatus is 301 or 302;

    private static bool IsEnvelopeRateLimit(string? statusCode) => statusCode is "301" or "302";

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

    private TimeSpan ComputeBackoff(int retryIndex, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
            return retryAfter.Value;

        var baseMs = Math.Min(_options.BaseBackoffMs * Math.Pow(2, retryIndex), _options.MaxBackoffMs);
        var jitterMs = _options.MaxJitterMs > 0 ? _jitter(_options.MaxJitterMs) : 0;
        return TimeSpan.FromMilliseconds(baseMs + jitterMs);
    }

    private static WapCrmSendResult Ambiguous(WapCrmSendRequest request, int attempt) => new()
    {
        Outcome = WapCrmSendOutcome.Ambiguous,
        HttpStatusCode = 0,
        ProviderErrorMessage = "Request timed out; delivery is unknown (not retried).",
        AttemptCount = attempt,
        TenantId = request.TenantId,
        InstanceId = request.InstanceId
    };

    private static WapCrmSendResult Transport(WapCrmSendRequest request, int attempt, int http, string message) => new()
    {
        Outcome = WapCrmSendOutcome.TransportError,
        HttpStatusCode = http,
        ProviderErrorMessage = message,
        AttemptCount = attempt,
        TenantId = request.TenantId,
        InstanceId = request.InstanceId
    };

    /// <summary>cxapi /chatoperation plain-text wire body. Explicit names so acronym casing (instanceID/userID) is never mangled by a naming policy.</summary>
    private sealed class Payload
    {
        [JsonPropertyName("instanceID")] public int InstanceId { get; init; }
        [JsonPropertyName("userID")] public int UserId { get; init; }
        [JsonPropertyName("chatPhoneNumber")] public required string ChatPhoneNumber { get; init; }
        [JsonPropertyName("messageType")] public int MessageType { get; init; }
        [JsonPropertyName("messageText")] public required string MessageText { get; init; }
    }
}
