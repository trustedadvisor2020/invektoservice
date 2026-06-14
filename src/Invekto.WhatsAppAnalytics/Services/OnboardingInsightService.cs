using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Npgsql;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// RI Faz 7: Tenant onboarding insight service.
/// Provides sector overview, onboarding checklist, quick start recommendations,
/// and tenant-vs-sector benchmark comparison.
/// </summary>
public sealed class OnboardingInsightService
{
    private readonly TemplateRepository _templateRepo;
    private readonly SectorConfigRepository _sectorConfigRepo;
    private readonly InsightRepository _insightRepo;
    private readonly JsonLinesLogger _logger;

    public OnboardingInsightService(
        TemplateRepository templateRepo,
        SectorConfigRepository sectorConfigRepo,
        InsightRepository insightRepo,
        JsonLinesLogger logger)
    {
        _templateRepo = templateRepo;
        _sectorConfigRepo = sectorConfigRepo;
        _insightRepo = insightRepo;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive onboarding data for a tenant including checklist,
    /// quick start recommendations, and benchmark comparison.
    /// </summary>
    public async Task<RiOnboardingResponse> GetOnboardingAsync(
        int tenantId, string? sector, int? instanceId = null, CancellationToken ct = default)
    {
        var response = new RiOnboardingResponse { TenantId = tenantId, Sector = sector };

        if (string.IsNullOrWhiteSpace(sector))
            return response;

        // Load sector config (lookup-by-key returns null when absent; a DB failure degrades to the
        // sector key as display name rather than failing the whole onboarding response).
        SectorConfig? config = null;
        try
        {
            config = await _sectorConfigRepo.GetByKeyAsync(sector, ct);
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemWarn($"[InsightSafeGet] 'sectorConfig' panel unavailable: {ex.Message}");
        }
        response.SectorDisplayName = config?.DisplayName ?? sector;

        // Parallel: templates + onboarding steps + tenant insights
        var templatesTask = InsightSafeGet.RunAsync(() => _templateRepo.GetAllTemplatesBySectorAsync(sector, ct), _logger, "templates");
        var rtTask = InsightSafeGet.RunAsync(() => _insightRepo.GetResponseTimeInsightAsync(tenantId, instanceId, ct), _logger, "responseTime");
        var alTask = InsightSafeGet.RunAsync(() => _insightRepo.GetAgentLeaderboardAsync(tenantId, instanceId, ct), _logger, "agentLeaderboard");
        var qsTask = InsightSafeGet.RunAsync(() => _insightRepo.GetQualityInsightAsync(tenantId, instanceId, false, ct), _logger, "qualityScore");
        var rvTask = InsightSafeGet.RunAsync(() => _insightRepo.GetRevenueAttributionAsync(tenantId, instanceId, null, ct), _logger, "revenue");

        await Task.WhenAll(templatesTask, rtTask, alTask, qsTask, rvTask);

        var templates = templatesTask.Result;

        // Build sector overview
        if (templates != null)
        {
            response.Overview = new SectorOverview
            {
                IntentCount = templates.Intents.Count,
                FaqCount = templates.Faqs.Count,
                FlowCount = templates.Flows.Count,
                ObjectionCount = templates.ObjectionHandlers.Count,
                FollowupCount = templates.FollowupTemplates.Count,
                TotalTemplates = templates.Intents.Count + templates.Faqs.Count + templates.Flows.Count
                                + templates.ObjectionHandlers.Count + templates.FollowupTemplates.Count,
                BenchmarkF1 = config?.BenchmarkF1
            };

            // Build checklist from sector onboarding steps
            response.Checklist = BuildChecklist(templates.OnboardingSteps, tenantId);

            // Build quick start recommendations from top templates
            response.QuickStart = BuildQuickStart(templates, tenantId);
        }

        // Build tenant vs sector benchmark comparison
        response.Comparison = BuildComparison(
            rtTask.Result, alTask.Result, qsTask.Result, rvTask.Result);

        return response;
    }

    private static List<OnboardingChecklistItem> BuildChecklist(
        List<SectorOnboardingStepRecord> steps, int tenantId)
    {
        // Return sector-specific onboarding steps
        // IsCompleted is always false here — actual completion tracking
        // requires cross-service calls (deferred to full onboarding integration)
        return steps
            .Where(s => s.IsActive)
            .OrderBy(s => s.StepNumber)
            .Select(s => new OnboardingChecklistItem
            {
                StepNumber = s.StepNumber,
                Action = s.Action,
                Description = s.Description,
                ExpectedImpact = s.ExpectedImpact,
                DayRange = s.DayRange,
                IsCompleted = false
            })
            .ToList();
    }

    private static List<QuickStartItem> BuildQuickStart(
        SectorTemplatesResponse templates, int tenantId)
    {
        var items = new List<QuickStartItem>();

        // Top 2 flows by conversion rate (if any)
        foreach (var flow in templates.Flows
            .Where(f => f.IsActive)
            .OrderByDescending(f => f.ConversionRate ?? 0)
            .Take(2))
        {
            items.Add(new QuickStartItem
            {
                Type = "flow",
                Title = flow.FlowName,
                Description = flow.Description,
                ActionUrl = "/app/flow-builder",
                ImpactLabel = flow.ConversionRate.HasValue
                    ? $"Conversion +{(flow.ConversionRate.Value * 100):F0}%"
                    : null
            });
        }

        // Top intent by frequency
        var topIntent = templates.Intents
            .Where(i => i.IsActive)
            .OrderByDescending(i => i.Frequency)
            .FirstOrDefault();
        if (topIntent != null)
        {
            items.Add(new QuickStartItem
            {
                Type = "intent",
                Title = topIntent.IntentName,
                Description = topIntent.Description,
                ActionUrl = "/app/intents",
                ImpactLabel = topIntent.Frequency > 0 ? $"{topIntent.Frequency} konusmada goruldu" : null
            });
        }

        return items;
    }

    private static TenantBenchmarkComparison? BuildComparison(
        ResponseTimeInsight? rt, AgentLeaderboardInsight? al,
        QualityInsight? qs, RevenueAttributionInsight? rv)
    {
        // No data at all → no comparison
        if (rt == null && al == null && qs == null && rv == null)
            return null;

        var comparison = new TenantBenchmarkComparison();

        if (rt != null && rt.AvgResponseTimeMs > 0)
        {
            comparison.TenantAvgResponseMin = (float)(rt.AvgResponseTimeMs / 60_000.0);
        }

        if (al != null && al.Agents.Count > 0)
        {
            comparison.TenantActiveAgents = al.Agents.Count;
            var avgConv = al.Agents.Average(a => a.ConversionRate);
            comparison.TenantConversionRate = (float)avgConv;
        }

        if (qs != null && qs.TotalScored > 0)
        {
            comparison.TenantAvgQualityScore = (float)qs.AvgOverallScore;
        }

        // Generate recommendation based on available data
        comparison.Recommendation = GenerateRecommendation(comparison);

        return comparison;
    }

    private static string? GenerateRecommendation(TenantBenchmarkComparison c)
    {
        if (c.TenantAvgResponseMin.HasValue && c.TenantAvgResponseMin > 30)
            return "Cevap sureniz 30 dakikanin uzerinde. Hizli yanit veren agentlar %40 daha yuksek donusum orani sagliyor.";

        if (c.TenantConversionRate.HasValue && c.TenantConversionRate < 0.10f)
            return "Donusum oraniniz %10'un altinda. Follow-up sablonlarini aktif edin ve rescue adaylarini takip edin.";

        if (c.TenantAvgQualityScore.HasValue && c.TenantAvgQualityScore < 50)
            return "Agent kalite puaniniz ortalamanin altinda. Egitim sablonlarini inceleyerek ilk cevap kalitesini artirabilirsiniz.";

        if (c.TenantActiveAgents.HasValue && c.TenantActiveAgents == 0)
            return "Henuz aktif agent verisi yok. Ilk konusmalar islendikten sonra agent performans karsilastirmasi burada gorunecek.";

        return null;
    }
}
