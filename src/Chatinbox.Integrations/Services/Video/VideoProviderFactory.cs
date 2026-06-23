using Chatinbox.Integrations.Data;
using Chatinbox.Shared.Contracts.Video;
using Chatinbox.Shared.Logging;

namespace Chatinbox.Integrations.Services.Video;

/// <summary>
/// FEAT-VCP Chunk A: resolves the active <see cref="IVideoConsultProvider"/> for a tenant
/// by reading <c>tenant_settings.video_provider</c>. Chunk A wires only the mock
/// implementation; Chunk C extends this factory to return the production
/// <c>GoogleMeetProvider</c> when the value is <c>"googlemeet"</c>.
///
/// Result-semantics contract (distinguish failure modes — Codex iter 0 CQ2/CQ9/CQ12):
/// <list type="bullet">
/// <item><description><c>null</c> return = provider-not-configured business state: the tenant has no
/// <c>tenant_settings</c> row, <c>video_provider</c> column is null, or the value names an
/// implementation that is not yet wired (e.g. <c>googlemeet</c> before Chunk C ships).
/// Callers (Chunk B) surface this as INV-INT-142 and confirm the appointment
/// without a meeting link.</description></item>
/// <item><description><c>NpgsqlException</c> propagates = DB outage: the repository could not execute
/// the probe at all. Callers MUST translate this to INV-INT-143 and a 503 envelope so
/// tenants / operators know to check database health rather than tenant configuration.
/// Catching and returning null here would collapse two operationally distinct failure
/// modes into the same graceful-skip branch.</description></item>
/// </list>
/// </summary>
public class VideoProviderFactory
{
    private readonly TenantSettingsRepository _tenantSettings;
    private readonly GoogleMeetMockProvider _mock;
    private readonly JsonLinesLogger _logger;

    public VideoProviderFactory(
        TenantSettingsRepository tenantSettings,
        GoogleMeetMockProvider mock,
        JsonLinesLogger logger)
    {
        _tenantSettings = tenantSettings;
        _mock = mock;
        _logger = logger;
    }

    /// <summary>
    /// Return the provider the tenant has selected, or <c>null</c> when the tenant has not
    /// configured one or the configured value is not yet wired. Propagates
    /// <see cref="Npgsql.NpgsqlException"/> when the settings probe fails so callers can
    /// distinguish DB outages (INV-INT-143) from genuine configuration absence (INV-INT-142).
    /// </summary>
    public virtual async Task<IVideoConsultProvider?> ResolveAsync(int tenantId, CancellationToken ct)
    {
        // NpgsqlException is intentionally NOT caught here — see class-level XML doc for the
        // result-semantics split. Returning null on DB failure would conflate two distinct
        // states and mask outages as "tenant config gap" to support teams.
        var row = await _tenantSettings.FindByTenantIdAsync(tenantId, ct).ConfigureAwait(false);

        var name = row?.VideoProvider;
        if (string.IsNullOrEmpty(name))
        {
            _logger.SystemInfo(
                $"[{VideoErrorCodes.ProviderNotConfigured}] VideoProviderFactory.Resolve: " +
                $"no video_provider configured tenant={tenantId}");
            return null;
        }

        switch (name)
        {
            case "mock":
                return _mock;
            case "googlemeet":
                // Chunk C will add GoogleMeetProvider DI registration and extend this switch.
                // Until then, surface the same graceful-null semantic so tenants that set
                // the production value early get INV-INT-142 instead of a crash.
                _logger.SystemWarn(
                    $"[{VideoErrorCodes.ProviderNotConfigured}] VideoProviderFactory.Resolve: " +
                    $"provider 'googlemeet' requested but Chunk C implementation not yet wired tenant={tenantId}");
                return null;
            default:
                _logger.SystemWarn(
                    $"[{VideoErrorCodes.ProviderNotConfigured}] VideoProviderFactory.Resolve: " +
                    $"unknown video_provider value '{name}' tenant={tenantId}");
                return null;
        }
    }
}
