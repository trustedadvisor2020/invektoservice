using System.Net.Http.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// HTTP client for Marketing service rescue endpoints.
/// PKT-12: Creates review_risk records via Marketing API.
/// </summary>
public sealed class MarketingRescueClient
{
    private readonly HttpClient _httpClient;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public MarketingRescueClient(HttpClient httpClient, JwtGenerator jwtGenerator, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Create a review risk record in Marketing service.
    /// Returns the created risk ID, or null on failure (graceful degradation).
    /// </summary>
    public async Task<int?> CreateRiskAsync(
        int tenantId, string phone, string? conversationId,
        short riskScore, string riskLevel, string triggerReason,
        CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/rescue/risks");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                customerPhone = phone,
                conversationId,
                riskScore,
                riskLevel,
                triggerReason
            });

            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateRiskResponse>(ct);
                return result?.Id;
            }

            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing risk create returned {(int)response.StatusCode} for tenant {tenantId}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing service unreachable for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Get count of prior risks for a phone in the last N days (for history scoring).
    /// Returns 0 on failure (graceful degradation).
    /// </summary>
    public async Task<int> GetPriorRiskCountAsync(int tenantId, string phone, CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v1/rescue/risks?status=rescued&status=failed&status=expired");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return 0;

            var result = await response.Content.ReadFromJsonAsync<ListRisksResponse>(ct);
            if (result?.Risks == null) return 0;

            // Count risks for this phone in last 90 days
            var cutoff = DateTime.UtcNow.AddDays(-90);
            return result.Risks.Count(r =>
                r.CustomerPhone == phone && r.CreatedAt > cutoff);
        }
        catch
        {
            return 0;
        }
    }

    private sealed class CreateRiskResponse
    {
        public int Id { get; set; }
    }

    private sealed class ListRisksResponse
    {
        public List<RiskItem>? Risks { get; set; }
    }

    private sealed class RiskItem
    {
        public string CustomerPhone { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
