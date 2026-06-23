using System.Text;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C4 — WRITE client for the cxapi customer-feature-groups UPDATE
/// (<c>POST api/customer-feature-groups/update</c>, WapCRM PDF §6.3). Applies a lead's new
/// COMPLETE feature-group selection (FULL-LIST semantics — the array replaces the whole group;
/// [] clears it; the vendor computes the diff atomically + audits <c>Source='api'</c>). It is the
/// counterpart to the READ <see cref="WapCrmFeatureGroupCatalogClient"/>; powers the 'Set Customer
/// Status' flow action, invoked only via the Backend internal proxy (Automation never holds the secret).
///
/// Security (mirrors <see cref="WapCrmFeatureGroupCatalogClient"/> / <see cref="WapCrmTemplateClient"/>):
/// the per-tenant secret rides PER REQUEST via
/// <c>HttpRequestMessage.Headers.TryAddWithoutValidation("X-CIB-SecretKey", ...)</c> — NEVER on
/// <c>DefaultRequestHeaders</c> — so one pooled HttpClient serves many tenants with no cross-tenant
/// leak. The secret is never stored on the instance and never logged. SSRF: the egress host is the
/// FIXED <see cref="WapCrmFeatureGroupUpdateOptions.BaseUrl"/> (set on the pooled client at DI time)
/// and the path is the compile-time constant <c>api/customer-feature-groups/update</c>; the
/// tenant-controlled <c>WapCrmSettings.ApiUrl</c> is never the target. Handler registered with
/// <c>AllowAutoRedirect=false</c> (cxapi 301/302 = rate-limit) and <c>UseCookies=false</c>.
///
/// Outcome model (keys on the PROVIDER payload statusCode, NOT HTTP): the client RETURNS a
/// <see cref="WapCrmFeatureGroupUpdateResult"/> for the three "vendor answered" cases — Success,
/// BusinessError (a deterministic provider statusCode such as 920/921/922/923/903), and AuthRejected
/// (HTTP 401/403 = invalid/expired secret) — and THROWS
/// <see cref="WapCrmFeatureGroupUpdateException"/> for transport / per-attempt timeout / HTTP 5xx /
/// malformed envelope / 301-302 rate-limit (retryable-but-unknown). This lets the Backend proxy map
/// deterministic rejections to a coded 422 (no retry) and transport faults to 503, never collapsing
/// the two. Caller cancellation (<paramref name="ct"/>) propagates as
/// <see cref="OperationCanceledException"/>; the per-attempt timeout is a distinct fetch failure.
/// </summary>
public sealed class WapCrmFeatureGroupUpdateClient
{
    private readonly HttpClient _httpClient;
    private readonly WapCrmFeatureGroupUpdateOptions _options;
    private readonly JsonLinesLogger _logger;

    private const string SecretKeyHeader = "X-CIB-SecretKey";
    private const string UpdatePath = "api/customer-feature-groups/update";
    private const int ProviderMessageCap = 300;

    /// <summary>Deterministic vendor business rejection codes (PDF §6.3 + §lookup): NOT retry-safe -> INV-BE-142.</summary>
    private static readonly HashSet<string> BusinessCodes = new(StringComparer.Ordinal)
    {
        "920", // FeatureGroup not found / passive
        "921", // Feature not found / passive
        "922", // single-selection group given multiple
        "923", // feature not in the group
        "903"  // customer not found
    };

    /// <summary>Provider PAYLOAD auth-rejection codes (distinct from a deterministic business rejection) -> INV-BE-143.</summary>
    private static readonly HashSet<string> AuthCodes = new(StringComparer.Ordinal)
    {
        "401", // both ids empty / unauthorized
        "403"  // forbidden (invalid/expired secret)
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WapCrmFeatureGroupUpdateClient(HttpClient httpClient, WapCrmFeatureGroupUpdateOptions options, JsonLinesLogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Apply the FULL new selection for <paramref name="request"/>'s group on the target customer.
    /// Throws <see cref="ArgumentException"/> on a caller-contract violation (bad secret / no identity),
    /// returns a classified <see cref="WapCrmFeatureGroupUpdateResult"/> when the vendor answered, and
    /// throws <see cref="WapCrmFeatureGroupUpdateException"/> on any transport/unknown failure.
    /// </summary>
    public async Task<WapCrmFeatureGroupUpdateResult> UpdateAsync(
        int tenantId, string secretKey, WapCrmFeatureGroupUpdateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("SecretKey is required.", nameof(secretKey));
        // TryAddWithoutValidation skips header validation, so refuse CR/LF/control chars in the
        // secret to close any header-injection vector before attaching.
        if (ContainsControlChars(secretKey))
            throw new ArgumentException("SecretKey contains invalid control characters.", nameof(secretKey));
        if (request.CustomerId is null or <= 0 && string.IsNullOrWhiteSpace(request.Phone))
            throw new ArgumentException("Either CustomerId or Phone is required.", nameof(request));

        var payload = JsonSerializer.Serialize(request);

        using var req = new HttpRequestMessage(HttpMethod.Post, UpdatePath)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
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
            if (http is 301 or 302)
            {
                _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] rate-limited (tenant={tenantId}, http={http})");
                throw new WapCrmFeatureGroupUpdateException(tenantId, "cxapi feature-group update rate-limited", httpStatusCode: http);
            }

            // HTTP-level auth rejection (invalid/expired secret) — distinct from a business rejection (-> INV-BE-143).
            if (http is 401 or 403)
            {
                _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateAuthRejected}] [cxapi-feature-group-update] auth rejected (tenant={tenantId}, http={http})");
                return new WapCrmFeatureGroupUpdateResult
                {
                    Outcome = WapCrmFeatureGroupUpdateOutcome.AuthRejected,
                    HttpStatusCode = http
                };
            }

            // HTTP 5xx — upstream/transport class, retryable-but-unknown (-> INV-BE-141).
            if (http >= 500)
            {
                _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] upstream {http} (tenant={tenantId})");
                throw new WapCrmFeatureGroupUpdateException(tenantId, $"cxapi feature-group update upstream {http}", httpStatusCode: http);
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            WapCrmApiResponse<JsonElement?>? env;
            try
            {
                env = JsonSerializer.Deserialize<WapCrmApiResponse<JsonElement?>>(body, ResponseJsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] malformed JSON (tenant={tenantId}, http={http}): {ex.Message}");
                throw new WapCrmFeatureGroupUpdateException(tenantId, "cxapi feature-group update malformed JSON", ex, httpStatusCode: http);
            }

            if (env is null)
            {
                _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] empty envelope (tenant={tenantId}, http={http})");
                throw new WapCrmFeatureGroupUpdateException(tenantId, "cxapi feature-group update empty envelope", httpStatusCode: http);
            }

            var providerCode = string.IsNullOrWhiteSpace(env.StatusCode) ? null : env.StatusCode.Trim();
            var providerMessage = Cap(env.Message);

            // Success: provider says it applied the selection.
            if (response.IsSuccessStatusCode && env.Status)
            {
                return new WapCrmFeatureGroupUpdateResult
                {
                    Outcome = WapCrmFeatureGroupUpdateOutcome.Success,
                    ProviderStatusCode = providerCode,
                    ProviderMessage = providerMessage,
                    HttpStatusCode = http
                };
            }

            // Provider PAYLOAD auth code (e.g. statusCode "401"/"403") — config class, kept distinct from a
            // business rejection so the Backend maps it to INV-BE-143 (fix the WapCRM key) not INV-BE-142.
            if (providerCode is not null && AuthCodes.Contains(providerCode))
            {
                _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateAuthRejected}] [cxapi-feature-group-update] auth rejected via provider code (tenant={tenantId}, providerCode={providerCode})");
                return new WapCrmFeatureGroupUpdateResult
                {
                    Outcome = WapCrmFeatureGroupUpdateOutcome.AuthRejected,
                    ProviderStatusCode = providerCode,
                    ProviderMessage = providerMessage,
                    HttpStatusCode = http
                };
            }

            // Deterministic, DOCUMENTED business rejection (920/921/922/923/903) — NOT retry-safe (-> INV-BE-142).
            if (providerCode is not null && BusinessCodes.Contains(providerCode))
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.CustomerStatusUpdateVendorRejected}] [cxapi-feature-group-update] vendor rejected (tenant={tenantId}, http={http}, providerCode={providerCode})");
                return new WapCrmFeatureGroupUpdateResult
                {
                    Outcome = WapCrmFeatureGroupUpdateOutcome.BusinessError,
                    ProviderStatusCode = providerCode,
                    ProviderMessage = providerMessage,
                    HttpStatusCode = http
                };
            }

            // status=false with NO usable code, OR a provider code we do not recognize (undocumented) =
            // unknown upstream state — treat as retryable-but-unknown rather than guessing a class (-> INV-BE-141).
            _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] unsuccessful/unrecognized (tenant={tenantId}, http={http}, providerCode={providerCode ?? "-"})");
            throw new WapCrmFeatureGroupUpdateException(tenantId, $"cxapi feature-group update unsuccessful (providerCode={providerCode ?? "-"})", httpStatusCode: http);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // caller cancellation / shutdown — propagate, do not classify as a failure
        }
        catch (OperationCanceledException ex)
        {
            // Our per-attempt timeout fired — unknown outcome (the request may or may not have reached the vendor).
            _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] timeout after {_options.TimeoutMs}ms (tenant={tenantId})");
            throw new WapCrmFeatureGroupUpdateException(tenantId, "cxapi feature-group update timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.CustomerStatusUpdateUpstreamFailed}] [cxapi-feature-group-update] transport error (tenant={tenantId}): {ex.Message}");
            throw new WapCrmFeatureGroupUpdateException(tenantId, "cxapi feature-group update transport error", ex);
        }
    }

    private static string? Cap(string? value)
        => string.IsNullOrEmpty(value) ? value
            : (value.Length <= ProviderMessageCap ? value : value[..ProviderMessageCap]);

    private static bool ContainsControlChars(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Thrown by <see cref="WapCrmFeatureGroupUpdateClient"/> when the cxapi update round-trip cannot
/// return an authoritative answer (timeout / network / HTTP 5xx / malformed envelope / 301-302
/// rate-limit / no provider code). The Backend proxy maps it to a coded 503 (INV-BE-141): the
/// outcome is UNKNOWN (the write may or may not have applied), so the flow author retries via the
/// node 'error' handle if appropriate — we never auto-retry. Never carries the secret.
/// </summary>
public sealed class WapCrmFeatureGroupUpdateException : Exception
{
    public int TenantId { get; }
    public int? HttpStatusCode { get; }

    public WapCrmFeatureGroupUpdateException(
        int tenantId,
        string message,
        Exception? innerException = null,
        int? httpStatusCode = null)
        : base(message, innerException)
    {
        TenantId = tenantId;
        HttpStatusCode = httpStatusCode;
    }
}
