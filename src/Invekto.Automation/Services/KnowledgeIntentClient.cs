using System.Net.Http.Headers;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Typed HTTP client for fetching tenant intent patterns from Knowledge service.
/// Called by AutomationOrchestrator at message start, not by handlers (handler purity preserved).
/// Returns null on any error (graceful degradation to default intents).
/// PKT-6A: Niche Foundation — DB-driven intent bridge.
/// </summary>
public sealed class KnowledgeIntentClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public KnowledgeIntentClient(HttpClient httpClient, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetch tenant-specific intent names from Knowledge service.
    /// Returns null on any failure (timeout, network, parse) — caller uses default intents.
    /// </summary>
    public async Task<string[]?> GetTenantIntentsAsync(int tenantId, string jwtToken, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/v1/knowledge/{tenantId}/intents");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Knowledge returned {(int)response.StatusCode} for tenant {tenantId}, falling back to defaults");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("intents", out var intentsEl))
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Knowledge response missing 'intents' field for tenant {tenantId}");
                return null;
            }

            var intents = intentsEl.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToArray();

            return intents.Length > 0 ? intents : null;
        }
        catch (TaskCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Knowledge intent fetch timed out for tenant {tenantId}, falling back to defaults");
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Knowledge intent fetch network error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeIntentFetchFailed}] Knowledge intent response parse error for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }
}
