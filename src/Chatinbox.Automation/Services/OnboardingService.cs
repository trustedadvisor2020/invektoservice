using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Automation.Services;

/// <summary>
/// Seeds tenant-specific intents from sector templates via Knowledge service.
/// PKT-6A: GR-3.5/3.10 Onboarding Automation.
/// </summary>
public sealed class OnboardingService
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public OnboardingService(HttpClient httpClient, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Seed tenant intents from a sector template by calling Knowledge seed endpoint.
    /// Returns number of seeded intents, or -1 on failure.
    /// </summary>
    public async Task<int> SeedTenantIntentsAsync(int tenantId, string sector, string jwtToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/v1/knowledge/{tenantId}/intents/seed?sector={Uri.EscapeDataString(sector)}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwtToken);
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Intent seed failed: Knowledge returned {(int)response.StatusCode} for tenant {tenantId}, sector '{sector}'");
                return -1;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("seeded", out var seededEl))
            {
                var count = seededEl.GetInt32();
                _logger.StepInfo($"Seeded {count} intents for tenant {tenantId} from sector '{sector}'", "onboarding");
                return count;
            }

            return 0;
        }
        catch (TaskCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Intent seed timed out for tenant {tenantId}, sector '{sector}'");
            return -1;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Intent seed network error for tenant {tenantId}: {ex.Message}");
            return -1;
        }
    }
}
