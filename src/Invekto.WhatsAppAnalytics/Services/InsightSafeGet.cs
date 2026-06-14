using Invekto.Shared.Logging;

namespace Invekto.WhatsAppAnalytics.Services;

/// <summary>
/// Shared guard for dashboard/onboarding insight panels.
/// An insight panel may not exist if its compute stage has not run for the tenant,
/// so a single panel's failure degrades to an empty panel rather than failing the
/// whole aggregated response.
/// Sanctioned broad-catch degradation boundary — see arch/codex-context.md.
/// Cancellation is NOT masked: it propagates so the request aborts correctly.
/// </summary>
internal static class InsightSafeGet
{
    public static async Task<T?> RunAsync<T>(
        Func<Task<T>> factory, JsonLinesLogger logger, string panel) where T : class
    {
        try
        {
            return await factory();
        }
        catch (OperationCanceledException)
        {
            throw; // shutdown/caller cancellation must abort the request, not become empty data
        }
        catch (Exception ex)
        {
            logger.SystemWarn($"[InsightSafeGet] '{panel}' panel unavailable: {ex.Message}");
            return null;
        }
    }
}
