using System.Diagnostics;
using Chatinbox.Shared.Logging;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI Faz 5: Bulk orchestration for template mining across all sectors.
/// Mines all supported sectors sequentially and returns aggregate results.
/// Also provides sector profile aggregation (template counts + config).
/// </summary>
public sealed class BulkOrchestrationService
{
    private readonly TemplateMiningService _miningService;
    private readonly TemplateRepository _templateRepo;
    private readonly SectorConfigRepository _sectorConfigRepo;
    private readonly JsonLinesLogger _logger;

    public BulkOrchestrationService(
        TemplateMiningService miningService,
        TemplateRepository templateRepo,
        SectorConfigRepository sectorConfigRepo,
        JsonLinesLogger logger)
    {
        _miningService = miningService;
        _templateRepo = templateRepo;
        _sectorConfigRepo = sectorConfigRepo;
        _logger = logger;
    }

    /// <summary>
    /// Mine templates for all supported sectors (or a subset).
    /// Processes sectors sequentially to avoid PG connection pressure.
    /// </summary>
    public async Task<BulkMineResult> MineAllSectorsAsync(
        List<string>? sectorFilter = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"bulk-mine-{DateTime.UtcNow:HHmmss}";

        var sectors = sectorFilter?.Count > 0
            ? sectorFilter.Where(TemplateMiningService.IsSupportedSector).ToList()
            : TemplateMiningService.GetSupportedSectorList();

        _logger.StepInfo($"[BulkMine] Starting bulk mine for {sectors.Count} sectors", rid);

        var result = new BulkMineResult();

        foreach (var sector in sectors)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var mineResult = await _miningService.MineAsync(sector, ct);
                result.SectorResults.Add(mineResult);
                result.TotalSectorsMined++;
                _logger.StepInfo($"[BulkMine] {sector}: " +
                    $"{mineResult.IntentsCreated}i/{mineResult.FaqsCreated}f/{mineResult.FlowsCreated}fl/" +
                    $"{mineResult.ObjectionHandlersCreated}oh/{mineResult.FollowupTemplatesCreated}fu/" +
                    $"{mineResult.OnboardingStepsCreated}ob", rid);
            }
            catch (OperationCanceledException)
            {
                throw; // honour cancellation — do not record it as a per-sector error and keep looping
            }
            catch (Exception ex)
            {
                // Sanctioned per-sector degradation boundary (see arch/codex-context.md): one sector's
                // mining failure must not abort the bulk sweep; record it and continue with the rest.
                // Raw ex.Message goes to the server log only; the returned result carries a sanitized note.
                _logger.SystemError($"[BulkMine] Failed for sector '{sector}': {ex.Message}");
                result.Errors.Add($"{sector}: mining failed (see server logs)");
            }
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        _logger.StepInfo($"[BulkMine] Complete: {result.TotalSectorsMined}/{sectors.Count} sectors, " +
            $"{result.Errors.Count} errors, {result.DurationMs}ms", rid);

        return result;
    }

    /// <summary>
    /// Get sector profile: config + template counts for each sector.
    /// </summary>
    public async Task<List<SectorProfile>> GetSectorProfilesAsync(CancellationToken ct = default)
    {
        var configs = await _sectorConfigRepo.GetAllAsync(ct);
        var profiles = new List<SectorProfile>();

        foreach (var cfg in configs.Where(c => c.IsActive))
        {
            ct.ThrowIfCancellationRequested();
            var templates = await _templateRepo.GetAllTemplatesBySectorAsync(cfg.SectorKey, ct);

            profiles.Add(new SectorProfile
            {
                SectorKey = cfg.SectorKey,
                DisplayName = cfg.DisplayName,
                BenchmarkF1 = cfg.BenchmarkF1,
                IsActive = cfg.IsActive,
                IntentCount = templates.Intents.Count,
                FaqCount = templates.Faqs.Count,
                FlowCount = templates.Flows.Count,
                ObjectionHandlerCount = templates.ObjectionHandlers.Count,
                FollowupTemplateCount = templates.FollowupTemplates.Count,
                OnboardingStepCount = templates.OnboardingSteps.Count,
                TotalTemplates = templates.Intents.Count + templates.Faqs.Count + templates.Flows.Count
                    + templates.ObjectionHandlers.Count + templates.FollowupTemplates.Count
                    + templates.OnboardingSteps.Count
            });
        }

        return profiles;
    }
}
