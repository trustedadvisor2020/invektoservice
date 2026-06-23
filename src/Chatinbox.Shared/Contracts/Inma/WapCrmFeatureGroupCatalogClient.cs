using System.Text;
using System.Text.Json;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C3b — READ-ONLY client for the cxapi customer-feature-groups
/// CATALOG (<c>POST api/customer-feature-groups/catalog</c>, WapCRM PDF §6.1). Returns the
/// tenant's feature groups (+ their options) for the Flow Builder feature_group_id picker.
/// There is NO write path (C4 = .../update).
///
/// Security (mirrors <see cref="WapCrmTemplateClient"/>): the per-tenant secret rides PER
/// REQUEST via <c>HttpRequestMessage.Headers.TryAddWithoutValidation("X-CIB-SecretKey", ...)</c>
/// — NEVER on <c>DefaultRequestHeaders</c> — so one pooled HttpClient serves many tenants with
/// no cross-tenant secret leak. The secret is never stored on the instance and never logged.
/// SSRF: the egress host is the FIXED <see cref="WapCrmFeatureGroupCatalogOptions.BaseUrl"/>
/// (set on the pooled HttpClient at DI time) and the path is the compile-time constant
/// <c>api/customer-feature-groups/catalog</c>; the tenant-controlled <c>WapCrmSettings.ApiUrl</c>
/// is never used as a target. Register the handler with <c>AllowAutoRedirect=false</c>
/// (cxapi 301/302 is a rate-limit signal) and <c>UseCookies=false</c>.
///
/// Failure semantics (mirrors <see cref="HttpInmaDynamicFieldsClient"/>): every non-success
/// path THROWS <see cref="WapCrmFeatureGroupCatalogFetchException"/> so the cache does not
/// swallow it and the Backend proxy maps it to a coded 503 — the SPA can tell "catalog
/// unreachable" apart from "tenant has no WapCRM secret" (422). Caller cancellation
/// (<paramref name="ct"/> cancelled) propagates as <see cref="OperationCanceledException"/>;
/// the per-attempt timeout is a distinct fetch failure.
/// </summary>
public sealed class WapCrmFeatureGroupCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly WapCrmFeatureGroupCatalogOptions _options;
    private readonly JsonLinesLogger _logger;

    private const string SecretKeyHeader = "X-CIB-SecretKey";
    private const string CatalogPath = "api/customer-feature-groups/catalog";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public WapCrmFeatureGroupCatalogClient(HttpClient httpClient, WapCrmFeatureGroupCatalogOptions options, JsonLinesLogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Fetch the tenant's feature-group catalog, trimmed to the SPA-facing
    /// <see cref="CustomerFeatureGroupView"/> shape. An empty list is a valid tenant state.
    /// Throws <see cref="WapCrmFeatureGroupCatalogFetchException"/> on any provider/transport
    /// failure, or <see cref="ArgumentException"/> on a caller-contract violation (bad secret).
    /// </summary>
    public async Task<IReadOnlyList<CustomerFeatureGroupView>> GetCatalogAsync(
        int tenantId, string secretKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("SecretKey is required.", nameof(secretKey));
        // TryAddWithoutValidation skips header validation, so refuse CR/LF/control chars in the
        // secret to close any header-injection vector before attaching.
        if (ContainsControlChars(secretKey))
            throw new ArgumentException("SecretKey contains invalid control characters.", nameof(secretKey));

        // Body is optional (channel filter). We send {} to fetch ALL groups (no InstanceID filter).
        using var req = new HttpRequestMessage(HttpMethod.Post, CatalogPath)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
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
                _logger.SystemWarn($"[cxapi-feature-groups] rate-limited (tenant={tenantId}, http={http})");
                throw new WapCrmFeatureGroupCatalogFetchException(tenantId, "cxapi feature-groups rate-limited", httpStatusCode: http);
            }

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

            WapCrmApiResponse<List<WapCrmFeatureGroupCatalogDto>>? env;
            try
            {
                env = JsonSerializer.Deserialize<WapCrmApiResponse<List<WapCrmFeatureGroupCatalogDto>>>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[cxapi-feature-groups] malformed JSON (tenant={tenantId}, http={http}): {ex.Message}");
                throw new WapCrmFeatureGroupCatalogFetchException(tenantId, "cxapi feature-groups malformed JSON", ex, httpStatusCode: http);
            }

            if (env == null || !response.IsSuccessStatusCode || !env.Status)
            {
                _logger.SystemWarn(
                    $"[cxapi-feature-groups] unsuccessful (tenant={tenantId}, http={http}, providerCode={env?.StatusCode ?? "-"})");
                throw new WapCrmFeatureGroupCatalogFetchException(
                    tenantId,
                    $"cxapi feature-groups unsuccessful (providerCode={env?.StatusCode ?? "-"})",
                    providerStatusCode: env?.StatusCode,
                    httpStatusCode: http);
            }

            // Trim to the SPA-facing projection (drop systemKey/rgbCode/sortOrder/instanceIDs).
            return (env.Data ?? new List<WapCrmFeatureGroupCatalogDto>())
                .Select(g => new CustomerFeatureGroupView
                {
                    Id = g.Id,
                    Name = g.Name ?? string.Empty,
                    SelectionMode = g.SelectionMode,
                    Features = (g.Features ?? new List<WapCrmFeatureDto>())
                        .Select(f => new CustomerFeatureView { Id = f.Id, Name = f.Name ?? string.Empty })
                        .ToList()
                })
                .ToList();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // caller cancellation / shutdown — propagate, do not classify as a failure
        }
        catch (OperationCanceledException ex)
        {
            // Our per-attempt timeout fired.
            _logger.SystemWarn($"[cxapi-feature-groups] timeout after {_options.TimeoutMs}ms (tenant={tenantId})");
            throw new WapCrmFeatureGroupCatalogFetchException(tenantId, "cxapi feature-groups timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[cxapi-feature-groups] transport error (tenant={tenantId}): {ex.Message}");
            throw new WapCrmFeatureGroupCatalogFetchException(tenantId, "cxapi feature-groups transport error", ex);
        }
    }

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
/// Thrown by <see cref="WapCrmFeatureGroupCatalogClient"/> when the cxapi catalog round-trip
/// cannot return an authoritative list (timeout / network / malformed envelope / non-success).
/// The cache deliberately does NOT swallow it; the Backend proxy maps it to a coded 503 so the
/// SPA distinguishes "catalog unreachable" from "tenant not configured" (422). Never carries the secret.
/// </summary>
public sealed class WapCrmFeatureGroupCatalogFetchException : Exception
{
    public int TenantId { get; }
    public string? ProviderStatusCode { get; }
    public int? HttpStatusCode { get; }

    public WapCrmFeatureGroupCatalogFetchException(
        int tenantId,
        string message,
        Exception? innerException = null,
        string? providerStatusCode = null,
        int? httpStatusCode = null)
        : base(message, innerException)
    {
        TenantId = tenantId;
        ProviderStatusCode = providerStatusCode;
        HttpStatusCode = httpStatusCode;
    }
}
