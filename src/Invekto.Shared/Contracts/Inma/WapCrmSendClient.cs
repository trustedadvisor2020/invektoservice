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
    // async kept deliberately (Codex chunk-1 iter1 CQ8): pre-await caller-contract violations must
    // keep surfacing as a FAULTED TASK (original PR-2 semantics), never as a synchronous throw.
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

        return await SendCoreAsync(payloadJson, request.SecretKey, request.TenantId, request.InstanceId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// PR-4: sends a single approved-template (HSM) message. The wire body carries a
    /// <c>template</c> object (templateId slug + named parameters + optional headerMedia)
    /// instead of messageType/messageText; there is NO <c>language</c> field (language is
    /// embedded in the slug — INMA 2026-06-08). Envelope parse, wamid capture, the retry
    /// doctrine (rate-limit-only retry, timeout → Ambiguous) and the typed result are
    /// IDENTICAL to the plain-text path. Throws only on a caller-contract violation or
    /// caller cancellation — async kept so those surface as a FAULTED TASK (plain-text parity).
    /// </summary>
    public async Task<WapCrmSendResult> SendTemplateAsync(WapCrmTemplateSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SecretKey))
            throw new ArgumentException("SecretKey is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ChatPhoneNumber))
            throw new ArgumentException("ChatPhoneNumber is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TemplateId))
            throw new ArgumentException("TemplateId is required.", nameof(request));
        if (request.InstanceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "InstanceId must be a positive integer.");
        if (request.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "UserId must be a positive integer.");

        // Named parameters: a present entry must be well-formed ({paramKey, value} both
        // non-empty key). Empty/null list => the wire object omits `parameters` entirely.
        List<TemplateParamWire>? wireParams = null;
        if (request.Parameters is { Count: > 0 })
        {
            wireParams = new List<TemplateParamWire>(request.Parameters.Count);
            foreach (var p in request.Parameters)
            {
                if (p is null || string.IsNullOrWhiteSpace(p.ParamKey))
                    throw new ArgumentException("Every template parameter requires a non-empty ParamKey.", nameof(request));
                wireParams.Add(new TemplateParamWire { ParamKey = p.ParamKey, Value = p.Value ?? string.Empty });
            }
        }

        // Dynamic header media: validate the URL hard (https-only, bounded, no
        // whitespace/control chars) and derive a sanitized fileName when absent —
        // the operator-supplied value is a literal URL from the dashboard mapping.
        TemplateHeaderMediaWire? wireMedia = null;
        if (request.HeaderMedia is { } media)
        {
            var url = ValidateHeaderMediaUrl(media.Url);
            wireMedia = new TemplateHeaderMediaWire
            {
                Url = url,
                FileName = SanitizeFileName(media.FileName, url)
            };
        }

        var payloadJson = JsonSerializer.Serialize(new TemplatePayload
        {
            InstanceId = request.InstanceId,
            UserId = request.UserId,
            ChatPhoneNumber = request.ChatPhoneNumber,
            Template = new TemplateBodyWire
            {
                TemplateId = request.TemplateId,
                Parameters = wireParams,
                HeaderMedia = wireMedia
            }
        }, PayloadJsonOptions);

        return await SendCoreAsync(payloadJson, request.SecretKey, request.TenantId, request.InstanceId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Shared send core for both wire shapes: POST + envelope parse + wamid capture +
    /// rate-limit-only retry + typed terminal classification. Behaviour is byte-identical
    /// to the original PR-2 plain-text loop.
    /// </summary>
    private async Task<WapCrmSendResult> SendCoreAsync(
        string payloadJson, string secretKey, int tenantId, int instanceId, CancellationToken ct)
    {
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
            if (!req.Headers.TryAddWithoutValidation(SecretKeyHeader, secretKey))
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
                        return Transport(tenantId, instanceId, attempt, http, $"Unparseable cxapi response (HTTP {http}).");

                    // status==true is AUTHORITATIVE: an accepted message is never retried,
                    // even if statusCode coincidentally resembles a rate-limit code.
                    if (response.IsSuccessStatusCode && env.Status)
                    {
                        var wamid = ExtractWamid(env.Data);
                        // PR-4: a Submitted send without a wamid is still SENT (provider said
                        // status=true) — only the delivery-ack correlation id is missing. Surface
                        // it loudly so an ack-less batch is diagnosable; never reclassify.
                        if (wamid == null)
                            _logger.SystemWarn(
                                $"[cxapi-send] submitted WITHOUT wamid (env.data empty/non-string) — delivery ack will not correlate: tenant={tenantId}, instance={instanceId}, attempt={attempt}");
                        return new WapCrmSendResult
                        {
                            Outcome = WapCrmSendOutcome.Submitted,
                            HttpStatusCode = http,
                            ProviderStatus = true,
                            ProviderStatusCode = env.StatusCode,
                            ProviderRequestId = env.RequestId,
                            ProviderMessageId = wamid,
                            ProviderErrorMessage = env.Message,
                            AttemptCount = attempt,
                            TenantId = tenantId,
                            InstanceId = instanceId
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
                            TenantId = tenantId,
                            InstanceId = instanceId
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
                    $"[cxapi-send] timeout after {_options.TimeoutMs}ms -> ambiguous: tenant={tenantId}, instance={instanceId}, attempt={attempt}");
                return Ambiguous(tenantId, instanceId, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn(
                    $"[cxapi-send] transport error: tenant={tenantId}, instance={instanceId}, attempt={attempt}, error={ex.Message}");
                return Transport(tenantId, instanceId, attempt, 0, $"Transport error: {ex.Message}");
            }

            // ── rate-limit path (the only retried outcome) ──
            if (rateLimited)
            {
                if (attempt <= _options.MaxRateLimitRetries)
                {
                    var delay = ComputeBackoff(attempt - 1, retryAfter);
                    _logger.SystemInfo(
                        $"[cxapi-send] rate-limited ({lastProviderStatusCode}) -> backoff {(int)delay.TotalMilliseconds}ms: tenant={tenantId}, instance={instanceId}, attempt={attempt}");
                    await _delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                _logger.SystemWarn(
                    $"[cxapi-send] rate-limit retries exhausted: tenant={tenantId}, instance={instanceId}, attempts={attempt}");
                return new WapCrmSendResult
                {
                    Outcome = WapCrmSendOutcome.RateLimited,
                    HttpStatusCode = lastHttp,
                    ProviderStatusCode = lastProviderStatusCode,
                    RetryAfter = retryAfter,
                    AttemptCount = attempt,
                    TenantId = tenantId,
                    InstanceId = instanceId
                };
            }
        }
    }

    private static bool IsRedirectRateLimit(int httpStatus) => httpStatus is 301 or 302;

    private static bool IsEnvelopeRateLimit(string? statusCode) => statusCode is "301" or "302";

    /// <summary>
    /// PR-3b-1: extract the sent message's wamid from the cxapi envelope <c>data</c> on a Submitted send.
    /// Per INMA C1 (2026-06-08) <c>data</c> is the WhatsApp message id (a plain JSON string) and equals
    /// the later ack's <c>InstanceMessageID</c>, so PR-3 persists it as <c>external_message_id</c> for
    /// tenant-scoped delivery correlation. Defensive: a null/empty/non-string <c>data</c> yields null —
    /// the send still succeeded; only the correlation id is absent (an ack for it just won't match).
    /// </summary>
    private static string? ExtractWamid(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.String } el)
            return null;
        var wamid = el.GetString();
        return string.IsNullOrWhiteSpace(wamid) ? null : wamid;
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

    private TimeSpan ComputeBackoff(int retryIndex, TimeSpan? retryAfter)
    {
        if (retryAfter.HasValue)
            return retryAfter.Value;

        var baseMs = Math.Min(_options.BaseBackoffMs * Math.Pow(2, retryIndex), _options.MaxBackoffMs);
        var jitterMs = _options.MaxJitterMs > 0 ? _jitter(_options.MaxJitterMs) : 0;
        return TimeSpan.FromMilliseconds(baseMs + jitterMs);
    }

    private static WapCrmSendResult Ambiguous(int tenantId, int instanceId, int attempt) => new()
    {
        Outcome = WapCrmSendOutcome.Ambiguous,
        HttpStatusCode = 0,
        ProviderErrorMessage = "Request timed out; delivery is unknown (not retried).",
        AttemptCount = attempt,
        TenantId = tenantId,
        InstanceId = instanceId
    };

    private static WapCrmSendResult Transport(int tenantId, int instanceId, int attempt, int http, string message) => new()
    {
        Outcome = WapCrmSendOutcome.TransportError,
        HttpStatusCode = http,
        ProviderErrorMessage = message,
        AttemptCount = attempt,
        TenantId = tenantId,
        InstanceId = instanceId
    };

    /// <summary>
    /// PR-4: hard validation for a dynamic header-media URL (operator-supplied literal).
    /// HTTPS-only absolute URL, bounded length, no whitespace/control characters.
    /// Throws <see cref="ArgumentException"/> — a malformed media URL is a caller-contract
    /// violation (the orchestrator validates upstream; this is the wire-side belt).
    /// </summary>
    private static string ValidateHeaderMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("HeaderMedia.Url is required when HeaderMedia is supplied.");
        if (url.Length > MaxMediaUrlLength)
            throw new ArgumentException($"HeaderMedia.Url exceeds {MaxMediaUrlLength} characters.");
        // Scan the RAW value — NO pre-trim (Codex chunk-1 iter0). Leading/trailing whitespace is
        // rejected exactly like embedded whitespace: upstream layers normalize operator input, so
        // by the time a URL reaches the wire client ANY whitespace means a corrupt value, and the
        // fail-loud contract is "reject before POST", never "silently repair".
        foreach (var c in url)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
                throw new ArgumentException("HeaderMedia.Url must not contain whitespace or control characters.");
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("HeaderMedia.Url must be an absolute https:// URL.");
        return url;
    }

    /// <summary>
    /// PR-4: derive/sanitize the header-media fileName. An explicit caller value is
    /// sanitized; otherwise the URL's last path segment is used; fallback "media".
    /// Only [A-Za-z0-9._-] survive; bounded to <see cref="MaxMediaFileNameLength"/>.
    /// </summary>
    private static string SanitizeFileName(string? fileName, string url)
    {
        var candidate = fileName;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            // Uri parse is guaranteed to succeed here (ValidateHeaderMediaUrl ran first).
            var uri = new Uri(url, UriKind.Absolute);
            var lastSegment = uri.Segments.Length > 0 ? uri.Segments[^1].Trim('/') : string.Empty;
            candidate = Uri.UnescapeDataString(lastSegment);
        }

        var sb = new StringBuilder(candidate.Length);
        foreach (var c in candidate)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
                sb.Append(c);
        }
        var clean = sb.ToString().Trim('.');
        if (clean.Length == 0)
            return "media";
        return clean.Length <= MaxMediaFileNameLength ? clean : clean[^MaxMediaFileNameLength..];
    }

    private const int MaxMediaUrlLength = 2048;
    private const int MaxMediaFileNameLength = 128;

    /// <summary>Serializer for wire payloads: omit null optionals (parameters/headerMedia) entirely.</summary>
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

    /// <summary>cxapi /chatoperation approved-template wire body (PR-4). NO language field — language is embedded in the templateId slug (INMA 2026-06-08).</summary>
    private sealed class TemplatePayload
    {
        [JsonPropertyName("instanceID")] public int InstanceId { get; init; }
        [JsonPropertyName("userID")] public int UserId { get; init; }
        [JsonPropertyName("chatPhoneNumber")] public required string ChatPhoneNumber { get; init; }
        [JsonPropertyName("template")] public required TemplateBodyWire Template { get; init; }
    }

    private sealed class TemplateBodyWire
    {
        [JsonPropertyName("templateId")] public required string TemplateId { get; init; }
        [JsonPropertyName("parameters")] public List<TemplateParamWire>? Parameters { get; init; }
        [JsonPropertyName("headerMedia")] public TemplateHeaderMediaWire? HeaderMedia { get; init; }
    }

    private sealed class TemplateParamWire
    {
        [JsonPropertyName("paramKey")] public required string ParamKey { get; init; }
        [JsonPropertyName("value")] public required string Value { get; init; }
    }

    private sealed class TemplateHeaderMediaWire
    {
        [JsonPropertyName("url")] public required string Url { get; init; }
        [JsonPropertyName("fileName")] public required string FileName { get; init; }
    }
}
