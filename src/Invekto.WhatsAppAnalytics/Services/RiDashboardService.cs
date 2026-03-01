using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// RI Faz 6: Revenue Intelligence Dashboard aggregation service.
/// Pulls data from all insight repositories in parallel and returns
/// a single aggregated response for the dashboard widget render.
/// </summary>
public sealed class RiDashboardService
{
    private readonly InsightRepository _insightRepo;
    private readonly TemplateRepository _templateRepo;
    private readonly SectorConfigRepository _sectorConfigRepo;
    private readonly JsonLinesLogger _logger;

    public RiDashboardService(
        InsightRepository insightRepo,
        TemplateRepository templateRepo,
        SectorConfigRepository sectorConfigRepo,
        JsonLinesLogger logger)
    {
        _insightRepo = insightRepo;
        _templateRepo = templateRepo;
        _sectorConfigRepo = sectorConfigRepo;
        _logger = logger;
    }

    /// <summary>
    /// Aggregate all dashboard widget data for a tenant in parallel.
    /// Returns null fields for insights that haven't been computed yet.
    /// </summary>
    public async Task<RiDashboardResponse> GetDashboardAsync(
        int tenantId, string? sector = null, int? instanceId = null, CancellationToken ct = default)
    {
        var response = new RiDashboardResponse { TenantId = tenantId, Sector = sector };

        // Fire all insight reads in parallel
        var rtTask = SafeGet(() => _insightRepo.GetResponseTimeInsightAsync(tenantId, instanceId, ct));
        var alTask = SafeGet(() => _insightRepo.GetAgentLeaderboardAsync(tenantId, instanceId, ct));
        var omTask = SafeGet(() => _insightRepo.GetObjectionMapAsync(tenantId, instanceId, ct));
        var rcTask = SafeGet(() => _insightRepo.GetRescueCandidatesAsync(tenantId, instanceId, ct));
        var qsTask = SafeGet(() => _insightRepo.GetQualityInsightAsync(tenantId, instanceId, false, ct));
        var dhTask = SafeGet(() => _insightRepo.GetDemandHeatmapAsync(tenantId, instanceId, ct));
        var rvTask = SafeGet(() => _insightRepo.GetRevenueAttributionAsync(tenantId, instanceId, null, ct));

        await Task.WhenAll(rtTask, alTask, omTask, rcTask, qsTask, dhTask, rvTask);

        response.ResponseTime = rtTask.Result;
        response.AgentLeaderboard = alTask.Result;
        response.ObjectionMap = omTask.Result;
        response.RescueAlerts = rcTask.Result;
        response.QualityScore = qsTask.Result;
        response.DemandHeatmap = dhTask.Result;
        response.Revenue = rvTask.Result;

        // Include sector templates and benchmarks if sector is provided
        if (!string.IsNullOrWhiteSpace(sector))
        {
            var templatesTask = SafeGet(() => _templateRepo.GetAllTemplatesBySectorAsync(sector, ct));
            var benchTask = SafeGet(() => GetBenchmarksAsync(sector, ct));
            await Task.WhenAll(templatesTask, benchTask);
            response.Templates = templatesTask.Result;
            response.Benchmarks = benchTask.Result;
        }

        return response;
    }

    /// <summary>
    /// Get sector benchmarks (config + template counts).
    /// </summary>
    public async Task<SectorBenchmarks> GetBenchmarksAsync(string sector, CancellationToken ct = default)
    {
        var config = await _sectorConfigRepo.GetByKeyAsync(sector, ct);
        var templates = await _templateRepo.GetAllTemplatesBySectorAsync(sector, ct);

        return new SectorBenchmarks
        {
            Sector = sector,
            DisplayName = config?.DisplayName ?? sector,
            BenchmarkF1 = config?.BenchmarkF1,
            TotalTemplates = templates.Intents.Count + templates.Faqs.Count + templates.Flows.Count,
            IntentCount = templates.Intents.Count,
            FaqCount = templates.Faqs.Count,
            FlowCount = templates.Flows.Count
        };
    }

    /// <summary>
    /// Safe wrapper that returns null instead of throwing for missing data.
    /// Insight data may not exist if compute hasn't been run for a tenant.
    /// </summary>
    private static async Task<T?> SafeGet<T>(Func<Task<T>> factory) where T : class
    {
        try
        {
            return await factory();
        }
        catch
        {
            return null;
        }
    }
}
