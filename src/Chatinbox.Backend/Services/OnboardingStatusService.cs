using System.Text.Json;
using Chatinbox.Backend.Data;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs.Onboarding;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Backend.Services;

/// <summary>
/// Aggregates onboarding status from 3 sources:
/// 1. tenant_registry (direct DB) → sector, WhatsApp connection
/// 2. Knowledge service (HTTP) → template adoption, FAQ, intent counts
/// 3. Automation service (HTTP) → flow counts
/// Graceful degradation: if a downstream service is unavailable,
/// those steps show detail="bilinmiyor" and the endpoint still returns 200.
/// </summary>
public sealed class OnboardingStatusService
{
    private readonly TenantRegistryRepository _tenantRepo;
    private readonly KnowledgeClient _knClient;
    private readonly AutomationClient _atClient;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    // Step completion thresholds (const — easy to promote to config later)
    private const int MinTemplateAdoptions = 1;
    private const int MinActiveFaqs = 5;
    private const int MinIntentPatterns = 3;
    private const int MinFlowsCreated = 1;
    private const int MinActiveFlows = 1;

    private const int TotalStepCount = 7;

    public OnboardingStatusService(
        TenantRegistryRepository tenantRepo,
        KnowledgeClient knClient,
        AutomationClient atClient,
        JwtGenerator jwtGenerator,
        JsonLinesLogger logger)
    {
        _tenantRepo = tenantRepo;
        _knClient = knClient;
        _atClient = atClient;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(int tenantId, CancellationToken ct = default)
    {
        // 1. Direct DB: tenant info + WhatsApp connection
        var tenant = await _tenantRepo.GetTenantAsync(tenantId, ct);
        var wapCrm = await _tenantRepo.GetWapCrmSettingsAsync(tenantId, ct);

        var sector = tenant?.Sector;
        var whatsAppConnected = !string.IsNullOrEmpty(wapCrm?.SecretKey);

        // 2. Knowledge stats (graceful degradation)
        KnowledgeOnboardingStatsDto? knStats = null;
        try
        {
            var serviceToken = _jwtGenerator.GenerateServiceToken(tenantId);
            var authHeader = $"Bearer {serviceToken}";
            var (statusCode, body) = await _knClient.ProxyGetAsync(
                $"/api/v1/knowledge/{tenantId}/onboarding-stats", authHeader, null, ct);

            if (statusCode == 200 && body != null)
            {
                knStats = JsonSerializer.Deserialize<KnowledgeOnboardingStatsDto>(body);
            }
            else
            {
                _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Knowledge onboarding-stats returned {statusCode} for tenant {tenantId}", "-");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Knowledge onboarding-stats network error for tenant {tenantId}: {ex.Message}", "-");
        }
        catch (TaskCanceledException ex)
        {
            _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Knowledge onboarding-stats timeout for tenant {tenantId}: {ex.Message}", "-");
        }
        catch (JsonException ex)
        {
            _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Knowledge onboarding-stats deserialization failed for tenant {tenantId}: {ex.Message}", "-");
        }

        // 3. Automation stats (graceful degradation)
        AutomationOnboardingStatsDto? atStats = null;
        try
        {
            var serviceToken = _jwtGenerator.GenerateServiceToken(tenantId);
            var authHeader = $"Bearer {serviceToken}";
            var (statusCode, body) = await _atClient.ProxyGetAsync(
                $"/api/v1/flows/{tenantId}/onboarding-stats", authHeader, null, ct);

            if (statusCode == 200 && body != null)
            {
                atStats = JsonSerializer.Deserialize<AutomationOnboardingStatsDto>(body);
            }
            else
            {
                _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Automation onboarding-stats returned {statusCode} for tenant {tenantId}", "-");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Automation onboarding-stats network error for tenant {tenantId}: {ex.Message}", "-");
        }
        catch (TaskCanceledException ex)
        {
            _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Automation onboarding-stats timeout for tenant {tenantId}: {ex.Message}", "-");
        }
        catch (JsonException ex)
        {
            _logger.StepWarn($"[{ErrorCodes.BackendOnboardingStatusFailed}] Automation onboarding-stats deserialization failed for tenant {tenantId}: {ex.Message}", "-");
        }

        // 4. Compute steps
        var steps = ComputeSteps(sector, whatsAppConnected, knStats, atStats);
        var completedCount = steps.Count(s => s.Completed);
        var progressPct = (int)Math.Round(100.0 * completedCount / TotalStepCount);
        var nextStep = ResolveNextStep(steps);

        return new OnboardingStatusDto
        {
            TenantId = tenantId,
            Sector = sector,
            ProgressPct = progressPct,
            Steps = steps,
            NextStep = nextStep
        };
    }

    private static List<OnboardingStepDto> ComputeSteps(
        string? sector, bool whatsAppConnected,
        KnowledgeOnboardingStatsDto? knStats, AutomationOnboardingStatsDto? atStats)
    {
        return new List<OnboardingStepDto>
        {
            new()
            {
                Key = "sector_selected",
                Completed = !string.IsNullOrEmpty(sector),
                Detail = !string.IsNullOrEmpty(sector) ? sector : null
            },
            new()
            {
                Key = "templates_adopted",
                Completed = (knStats?.TemplateAdoptionCount ?? 0) >= MinTemplateAdoptions,
                Detail = knStats != null ? $"{knStats.TemplateAdoptionCount} şablon" : "bilinmiyor"
            },
            new()
            {
                Key = "knowledge_ready",
                Completed = (knStats?.ActiveFaqCount ?? 0) >= MinActiveFaqs,
                Detail = knStats != null ? $"{knStats.ActiveFaqCount}/{MinActiveFaqs} SSS" : "bilinmiyor"
            },
            new()
            {
                Key = "intents_configured",
                Completed = (knStats?.IntentPatternCount ?? 0) >= MinIntentPatterns,
                Detail = knStats != null ? $"{knStats.IntentPatternCount}/{MinIntentPatterns} niyet" : "bilinmiyor"
            },
            new()
            {
                Key = "first_flow_created",
                Completed = (atStats?.TotalFlowCount ?? 0) >= MinFlowsCreated,
                Detail = atStats != null ? (atStats.TotalFlowCount >= MinFlowsCreated ? $"{atStats.TotalFlowCount} akış" : null) : "bilinmiyor"
            },
            new()
            {
                Key = "flow_activated",
                Completed = (atStats?.ActiveFlowCount ?? 0) >= MinActiveFlows,
                Detail = atStats != null ? (atStats.ActiveFlowCount >= MinActiveFlows ? $"{atStats.ActiveFlowCount} aktif" : null) : "bilinmiyor"
            },
            new()
            {
                Key = "whatsapp_connected",
                Completed = whatsAppConnected,
                Detail = whatsAppConnected ? "bağlı" : null
            }
        };
    }

    private static OnboardingNextStepDto? ResolveNextStep(List<OnboardingStepDto> steps)
    {
        var firstIncomplete = steps.FirstOrDefault(s => !s.Completed);
        if (firstIncomplete == null)
            return null;

        return firstIncomplete.Key switch
        {
            "sector_selected" => new OnboardingNextStepDto
            {
                Key = "sector_selected",
                ActionUrl = "/app/settings",
                Hint = "İşletmenizin sektörünü seçin"
            },
            "templates_adopted" => new OnboardingNextStepDto
            {
                Key = "templates_adopted",
                ActionUrl = "/app/templates",
                Hint = "Sektörünüze uygun şablonları kütüphanenize ekleyin"
            },
            "knowledge_ready" => new OnboardingNextStepDto
            {
                Key = "knowledge_ready",
                ActionUrl = "/app/knowledge",
                Hint = $"En az {MinActiveFaqs} sık sorulan soru ekleyin"
            },
            "intents_configured" => new OnboardingNextStepDto
            {
                Key = "intents_configured",
                ActionUrl = "/app/intents",
                Hint = $"En az {MinIntentPatterns} müşteri niyeti tanımlayın"
            },
            "first_flow_created" => new OnboardingNextStepDto
            {
                Key = "first_flow_created",
                ActionUrl = "/app/flow-builder",
                Hint = "İlk chatbot akışınızı oluşturun"
            },
            "flow_activated" => new OnboardingNextStepDto
            {
                Key = "flow_activated",
                ActionUrl = "/app/flow-builder",
                Hint = "Bir chatbot akışını aktif hale getirin"
            },
            "whatsapp_connected" => new OnboardingNextStepDto
            {
                Key = "whatsapp_connected",
                ActionUrl = "/app/settings",
                Hint = "WhatsApp hesabınızı bağlayın"
            },
            _ => null
        };
    }
}
