using System.Net.Http.Headers;
using System.Text;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// HTTP client for Integrations service e-commerce API.
/// Called by EcommerceHandler to execute e-commerce operations.
/// Pattern: same as KnowledgeSearchClient (typed HttpClient, graceful degradation).
/// Base URL: http://localhost:7106 (configurable via Integrations:BaseUrl).
/// </summary>
public sealed class IntegrationsClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public IntegrationsClient(HttpClient httpClient, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Execute an e-commerce operation against the Integrations service.
    /// Returns success/failure with raw JSON response body.
    /// </summary>
    public async Task<IntegrationsEcomResult> ExecuteAsync(
        HttpMethod method, string path, string? bodyJson,
        string jwtToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));

            if (bodyJson != null && method != HttpMethod.Get)
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return new IntegrationsEcomResult
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    ResponseJson = responseBody
                };
            }

            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationEcommerceCallFailed}] Integrations HTTP {(int)response.StatusCode}: {Truncate(responseBody, 200)}");

            return new IntegrationsEcomResult
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                ResponseJson = responseBody,
                ErrorReason = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationEcommerceTimeout}] Integrations ecommerce call timeout");
            return IntegrationsEcomResult.Timeout();
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationEcommerceCallFailed}] Integrations connection error: {ex.Message}");
            return IntegrationsEcomResult.ConnectionError(ex.Message);
        }
    }

    private static string Truncate(string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "...";
}

/// <summary>
/// Result of an Integrations service e-commerce API call.
/// </summary>
public sealed class IntegrationsEcomResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseJson { get; init; } = "";
    public string? ErrorReason { get; init; }

    public static IntegrationsEcomResult Timeout() => new()
    {
        Success = false,
        StatusCode = 0,
        ErrorReason = "Timeout"
    };

    public static IntegrationsEcomResult ConnectionError(string msg) => new()
    {
        Success = false,
        StatusCode = 0,
        ErrorReason = $"Connection error: {msg}"
    };
}
