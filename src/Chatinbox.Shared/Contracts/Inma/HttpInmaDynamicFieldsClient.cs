using System.Net.Http.Json;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Inma.Dtos;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-DMP: production client for INMA <c>/api/dynamicfields</c>.
/// Per-call <c>X-CIB-SecretKey</c> is injected because every tenant has their own key
/// (read from <c>tenant_registry.wapcrm_secret_key</c>). Timeout 5s (short — editor roundtrip).
///
/// Failure semantics (iter 1 fix for CQ2 silent-degradation): every non-success path throws
/// <see cref="InmaDynamicFieldsFetchException"/> tagged with <see cref="ErrorCodes.DynamicFieldsFetchFailed"/>.
/// Callers (cache / bridge) turn this into a 503 for the UI, so tenants can tell
/// "INMA unreachable" apart from "tenant has no active placeholders" (empty success).
///
/// Wire contract: <c>wapcrm-marketing-api.md</c> §3.
/// </summary>
public sealed class HttpInmaDynamicFieldsClient : IInmaDynamicFieldsClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly string _baseUrl;

    private const string SecretKeyHeader = "X-CIB-SecretKey";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public HttpInmaDynamicFieldsClient(HttpClient httpClient, JsonLinesLogger logger, string baseUrl)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = (baseUrl ?? throw new ArgumentNullException(nameof(baseUrl))).TrimEnd('/');
    }

    public async Task<IReadOnlyList<InmaDynamicField>> GetFieldsAsync(
        int tenantId,
        string secretKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("secretKey is required", nameof(secretKey));

        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/api/dynamicfields")
        {
            Content = JsonContent.Create(new { }, options: JsonOptions),
        };
        req.Headers.Add(SecretKeyHeader, secretKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(req, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn($"[{ErrorCodes.DynamicFieldsFetchFailed}] INMA /api/dynamicfields timeout (tenant={tenantId}): {ex.Message}");
            throw new InmaDynamicFieldsFetchException(tenantId, "INMA dynamic-fields timeout", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.DynamicFieldsFetchFailed}] INMA /api/dynamicfields HTTP failure (tenant={tenantId}): {ex.Message}");
            throw new InmaDynamicFieldsFetchException(tenantId, "INMA dynamic-fields HTTP failure", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            InmaDynamicFieldsResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<InmaDynamicFieldsResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.DynamicFieldsFetchFailed}] INMA /api/dynamicfields malformed JSON (tenant={tenantId}, http={(int)response.StatusCode}): {ex.Message}");
                throw new InmaDynamicFieldsFetchException(
                    tenantId, "INMA dynamic-fields malformed JSON", ex,
                    httpStatusCode: (int)response.StatusCode);
            }

            if (parsed is null || !parsed.Status)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.DynamicFieldsFetchFailed}] INMA /api/dynamicfields unsuccessful " +
                    $"(tenant={tenantId}, http={(int)response.StatusCode}, inmaCode={parsed?.StatusCode}): {parsed?.Message}");
                throw new InmaDynamicFieldsFetchException(
                    tenantId,
                    $"INMA dynamic-fields unsuccessful (inmaCode={parsed?.StatusCode ?? "-"})",
                    inmaStatusCode: parsed?.StatusCode,
                    httpStatusCode: (int)response.StatusCode);
            }

            // Empty-but-success is a valid tenant state: no custom fields activated yet.
            return parsed.Data is { } list ? list : (IReadOnlyList<InmaDynamicField>)Array.Empty<InmaDynamicField>();
        }
    }
}
