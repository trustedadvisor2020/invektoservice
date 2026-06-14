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
        var rtTask = InsightSafeGet.RunAsync(() => _insightRepo.GetResponseTimeInsightAsync(tenantId, instanceId, ct), _logger, "responseTime");
        var alTask = InsightSafeGet.RunAsync(() => _insightRepo.GetAgentLeaderboardAsync(tenantId, instanceId, ct), _logger, "agentLeaderboard");
        var omTask = InsightSafeGet.RunAsync(() => _insightRepo.GetObjectionMapAsync(tenantId, instanceId, ct), _logger, "objectionMap");
        var rcTask = InsightSafeGet.RunAsync(() => _insightRepo.GetRescueCandidatesAsync(tenantId, instanceId, ct), _logger, "rescueCandidates");
        var qsTask = InsightSafeGet.RunAsync(() => _insightRepo.GetQualityInsightAsync(tenantId, instanceId, false, ct), _logger, "qualityScore");
        var dhTask = InsightSafeGet.RunAsync(() => _insightRepo.GetDemandHeatmapAsync(tenantId, instanceId, ct), _logger, "demandHeatmap");
        var rvTask = InsightSafeGet.RunAsync(() => _insightRepo.GetRevenueAttributionAsync(tenantId, instanceId, null, ct), _logger, "revenue");

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
            var templatesTask = InsightSafeGet.RunAsync(() => _templateRepo.GetAllTemplatesBySectorAsync(sector, ct), _logger, "templates");
            var benchTask = InsightSafeGet.RunAsync(() => GetBenchmarksAsync(sector, ct), _logger, "benchmarks");
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
}
