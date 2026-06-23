using System.Net.Http.Json;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

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
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Prior-risk-count fetch failed for tenant {tenantId}: {ex.Message}");
            return 0;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Prior-risk-count response parse failed for tenant {tenantId}: {ex.Message}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Fetch active rescue templates for a given risk level from Marketing service.
    /// Returns empty list on failure (graceful degradation).
    /// </summary>
    public async Task<List<RescueTemplateDto>> GetTemplatesByRiskLevelAsync(
        int tenantId, string riskLevel, CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v1/rescue/templates?level={Uri.EscapeDataString(riskLevel)}&active=true");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing template fetch returned {(int)response.StatusCode} for tenant {tenantId}");
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<TemplateListResponse>(ct);
            return result?.Templates ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing template fetch failed for tenant {tenantId}: {ex.Message}");
            return [];
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing template fetch cancelled for tenant {tenantId}: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Update risk status in Marketing (e.g., "rescued", "failed").
    /// Returns true on success.
    /// </summary>
    public async Task<bool> UpdateRiskStatusAsync(
        int tenantId, int riskId, string status, string? rescueMessageId = null,
        CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/rescue/risks/{riskId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                status,
                rescueMessageId
            });

            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing risk status update failed for tenant {tenantId}, riskId={riskId}: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing risk status update cancelled for tenant {tenantId}, riskId={riskId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// PKT-12 Faz 3: Fetch risks due for follow-up (cross-tenant batch query).
    /// Returns empty list on failure.
    /// </summary>
    public async Task<List<FollowUpDueItem>> GetFollowUpDueRisksAsync(CancellationToken ct = default)
    {
        try
        {
            // Intentionally cross-tenant: ops-level batch endpoint, no tenant JWT needed
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/rescue/risks/followup-due");
            request.Headers.Add("X-Internal-Service", "automation");

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationFollowUpQueryFailed}] Marketing followup-due returned {(int)response.StatusCode}");
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<FollowUpDueResponse>(ct);
            return result?.Risks ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpQueryFailed}] Marketing followup-due failed: {ex.Message}");
            return [];
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpQueryFailed}] Marketing followup-due cancelled: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// PKT-12 Faz 3: Update follow-up status for a risk.
    /// </summary>
    public async Task<bool> UpdateFollowUpStatusAsync(
        int tenantId, int riskId, string followUpStatus, CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/rescue/risks/{riskId}/followup");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { followUpStatus });

            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpSendFailed}] Marketing followup status update failed for tenant {tenantId}, riskId={riskId}: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationFollowUpSendFailed}] Marketing followup status update cancelled for tenant {tenantId}, riskId={riskId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// PKT-12 Faz 3: Update customer_response on a risk (e.g., after keyword detection).
    /// </summary>
    public async Task<bool> UpdateCustomerResponseAsync(
        int tenantId, int riskId, string customerResponse, CancellationToken ct = default)
    {
        try
        {
            var token = _jwtGenerator.GenerateServiceToken(tenantId);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/rescue/risks/{riskId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { customerResponse });

            using var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing customer response update failed for tenant {tenantId}, riskId={riskId}: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationRescueMarketingFailed}] Marketing customer response update cancelled for tenant {tenantId}, riskId={riskId}: {ex.Message}");
            return false;
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

    private sealed class TemplateListResponse
    {
        public List<RescueTemplateDto>? Templates { get; set; }
    }

    private sealed class FollowUpDueResponse
    {
        public List<FollowUpDueItem>? Risks { get; set; }
    }
}

/// <summary>DTO for rescue template from Marketing service.</summary>
public sealed class RescueTemplateDto
{
    public int Id { get; set; }
    public string TemplateName { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Strategy { get; set; } = "";
    public string MessageTemplate { get; set; } = "";
    public short? MaxDiscountPct { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>PKT-12 Faz 3: Follow-up due risk item from Marketing service.</summary>
public sealed class FollowUpDueItem
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string CustomerPhone { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string FollowUpStatus { get; set; } = "none";
    public string? CustomerResponse { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? FollowUpSentAt { get; set; }
}
