using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-PROJELER / Feature A — cxapi message-webhook reconciliation.
/// Typed client for the cxapi webhook-settings surface used to point a tenant's
/// MESSAGE/ACK webhook at our delivery-ack ingress so delivery/read acks flow:
///   <c>GET  /api/webhook-settings</c>          → read current messages.{webhookUrl,isActive}
///   <c>POST /api/webhook-settings/messages</c> → set messages webhookUrl + isActive=true
///
/// It NEVER touches the separate HMAC-signed <c>/api/webhook-settings/events</c> channel.
///
/// Auth (mirrors <see cref="WapCrmSendClient"/>): the secret is attached PER REQUEST via
/// <c>HttpRequestMessage.Headers.TryAddWithoutValidation("X-CIB-SecretKey", ...)</c> — NEVER
/// on <c>DefaultRequestHeaders</c> — so one pooled HttpClient serves many tenants with no
/// cross-tenant secret leak. The secret is never logged or persisted.
///
/// Doctrine: register with <c>AllowAutoRedirect=false</c>/<c>UseCookies=false</c> (cxapi uses
/// HTTP 301/302 as rate-limit signals, not real redirects). Each call is bounded by a per-request
/// linked CTS (TimeoutMs) plus a finite <c>HttpClient.Timeout</c> backstop. Methods return a typed
/// outcome on EVERY operational path — including a header-attach failure (→ TransportError) — and throw
/// ONLY on a null/blank caller argument (secret/url) or caller cancellation. So an awaiting reconcile loop
/// never faces an unexpected throw (nothing escapes its typed catches into the async-void timer callback).
/// HTTP 509 = caller IP not whitelisted at cxapi (a distinct, ops-actionable outcome, never a transport error).
/// </summary>
public sealed class WapCrmWebhookSettingsClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly int _timeoutMs;

    private const string SecretKeyHeader = "X-CIB-SecretKey";
    private const string ReadPath = "api/webhook-settings";
    private const string SetMessagesPath = "api/webhook-settings/messages";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WapCrmWebhookSettingsClient(HttpClient httpClient, JsonLinesLogger logger, int timeoutMs)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutMs = timeoutMs > 0 ? timeoutMs : 5_000;
    }

    /// <summary>
    /// Reads the tenant's current MESSAGES webhook (url + isActive). Returns a typed outcome;
    /// only the messages section is parsed (events is deliberately ignored).
    /// </summary>
    public async Task<WapCrmWebhookReadResult> GetMessageWebhookAsync(string secretKey, int tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("SecretKey is required.", nameof(secretKey));

        using var req = new HttpRequestMessage(HttpMethod.Get, ReadPath);
        if (!req.Headers.TryAddWithoutValidation(SecretKeyHeader, secretKey))
            return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.TransportError, 0, message: "Failed to attach the WapCRM secret header.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeoutMs);

        try
        {
            using var response = await _httpClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
            var http = (int)response.StatusCode;

            if (http == 509)
                return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.IpDenied, http);
            if (IsRedirectRateLimit(http))
                return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.RateLimited, http);

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            WapCrmApiResponse<WebhookSettingsData>? env;
            try
            {
                env = JsonSerializer.Deserialize<WapCrmApiResponse<WebhookSettingsData>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                env = null;
            }

            if (env == null)
                return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.Unparseable, http);

            if (!response.IsSuccessStatusCode || !env.Status)
                return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.ProviderFailed, http, env.StatusCode, env.RequestId, env.Message);

            var messages = env.Data?.Messages;
            return new WapCrmWebhookReadResult
            {
                Outcome = WapCrmWebhookOutcome.Ok,
                HttpStatusCode = http,
                ProviderStatusCode = env.StatusCode,
                ProviderRequestId = env.RequestId,
                Message = env.Message,
                CurrentWebhookUrl = messages?.WebhookUrl,
                IsActive = messages?.IsActive ?? false
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // caller cancellation / shutdown — propagate
        }
        catch (OperationCanceledException)
        {
            return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.Timeout, 0);
        }
        catch (HttpRequestException ex)
        {
            return WapCrmWebhookReadResult.From(WapCrmWebhookOutcome.TransportError, 0, message: ex.Message);
        }
    }

    /// <summary>
    /// Sets the tenant's MESSAGES webhook url + isActive=true and CLEARS customHeaders
    /// (<c>customHeaders: []</c> — the partner contract preserves on null, so an empty array is
    /// required to guarantee a clean, Invekto-owned webhook with no stale third-party headers).
    /// </summary>
    public async Task<WapCrmWebhookWriteResult> SetMessageWebhookAsync(string secretKey, string webhookUrl, int tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("SecretKey is required.", nameof(secretKey));
        if (string.IsNullOrWhiteSpace(webhookUrl))
            throw new ArgumentException("webhookUrl is required.", nameof(webhookUrl));

        var payloadJson = JsonSerializer.Serialize(new SetMessagesBody
        {
            IsActive = true,
            WebhookUrl = webhookUrl,
            CustomHeaders = Array.Empty<object>()
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, SetMessagesPath)
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
        };
        if (!req.Headers.TryAddWithoutValidation(SecretKeyHeader, secretKey))
            return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.TransportError, 0, message: "Failed to attach the WapCRM secret header.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeoutMs);

        try
        {
            using var response = await _httpClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
            var http = (int)response.StatusCode;

            if (http == 509)
                return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.IpDenied, http);
            if (IsRedirectRateLimit(http))
                return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.RateLimited, http);

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
                return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.Unparseable, http);

            if (response.IsSuccessStatusCode && env.Status)
                return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.Ok, http, env.StatusCode, env.RequestId, env.Message);

            return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.ProviderFailed, http, env.StatusCode, env.RequestId, env.Message);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.Timeout, 0);
        }
        catch (HttpRequestException ex)
        {
            return WapCrmWebhookWriteResult.From(WapCrmWebhookOutcome.TransportError, 0, message: ex.Message);
        }
    }

    private static bool IsRedirectRateLimit(int httpStatus) => httpStatus is 301 or 302;

    // ── wire shapes ──

    /// <summary>Read body: only the messages section is consumed (events ignored — separate channel).</summary>
    private sealed class WebhookSettingsData
    {
        [JsonPropertyName("messages")] public MessagesSection? Messages { get; init; }
    }

    private sealed class MessagesSection
    {
        [JsonPropertyName("isActive")] public bool IsActive { get; init; }
        [JsonPropertyName("webhookUrl")] public string? WebhookUrl { get; init; }
    }

    /// <summary>POST body for /messages. customHeaders=[] CLEARS stale headers (null would preserve them).</summary>
    private sealed class SetMessagesBody
    {
        [JsonPropertyName("isActive")] public bool IsActive { get; init; }
        [JsonPropertyName("webhookUrl")] public required string WebhookUrl { get; init; }
        [JsonPropertyName("customHeaders")] public required object[] CustomHeaders { get; init; }
    }
}

/// <summary>Terminal classification for a webhook-settings call.</summary>
public enum WapCrmWebhookOutcome
{
    Ok,
    IpDenied,        // HTTP 509 — caller IP not whitelisted at cxapi (ops-actionable)
    RateLimited,     // HTTP 301/302
    ProviderFailed,  // 2xx parse but status!=true, or non-2xx with a parseable envelope
    TransportError,  // network fault
    Timeout,         // per-request timeout
    Unparseable      // response body was not a parseable envelope
}

/// <summary>Typed result of a GET /api/webhook-settings (messages section).</summary>
public sealed class WapCrmWebhookReadResult
{
    public required WapCrmWebhookOutcome Outcome { get; init; }
    public int HttpStatusCode { get; init; }
    public string? ProviderStatusCode { get; init; }
    public string? ProviderRequestId { get; init; }
    public string? Message { get; init; }
    public string? CurrentWebhookUrl { get; init; }
    public bool IsActive { get; init; }

    public static WapCrmWebhookReadResult From(
        WapCrmWebhookOutcome outcome, int http, string? statusCode = null, string? requestId = null, string? message = null) => new()
    {
        Outcome = outcome,
        HttpStatusCode = http,
        ProviderStatusCode = statusCode,
        ProviderRequestId = requestId,
        Message = message
    };
}

/// <summary>Typed result of a POST /api/webhook-settings/messages.</summary>
public sealed class WapCrmWebhookWriteResult
{
    public required WapCrmWebhookOutcome Outcome { get; init; }
    public int HttpStatusCode { get; init; }
    public string? ProviderStatusCode { get; init; }
    public string? ProviderRequestId { get; init; }
    public string? Message { get; init; }

    public static WapCrmWebhookWriteResult From(
        WapCrmWebhookOutcome outcome, int http, string? statusCode = null, string? requestId = null, string? message = null) => new()
    {
        Outcome = outcome,
        HttpStatusCode = http,
        ProviderStatusCode = statusCode,
        ProviderRequestId = requestId,
        Message = message
    };
}
