using Chatinbox.Shared.Logging;

namespace Chatinbox.Integrations.Services;

/// <summary>
/// Mock implementation of Aras Kargo tracking API (GR-3.6).
/// </summary>
public sealed class ArasCargoMockProvider : ICargoProvider
{
    private readonly JsonLinesLogger _logger;

    public ArasCargoMockProvider(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    public string ProviderName => "aras";

    public Task<CargoTrackingResult?> TrackShipmentAsync(
        string trackingCode, CancellationToken ct)
    {
        _logger.SystemWarn($"[ARAS-MOCK] TrackShipment called (mock mode, tracking={trackingCode})");

        var result = new CargoTrackingResult
        {
            TrackingCode = trackingCode,
            CargoProvider = ProviderName,
            CurrentStatus = "in_transit",
            Events = new List<CargoEvent>
            {
                new()
                {
                    Status = "picked_up",
                    Location = "Istanbul - Merkez Dagitim",
                    EventTime = DateTime.UtcNow.AddDays(-2),
                    RawDataJson = "{\"mock\":true}"
                },
                new()
                {
                    Status = "in_transit",
                    Location = "Ankara - Transfer Merkezi",
                    EventTime = DateTime.UtcNow.AddDays(-1),
                    RawDataJson = "{\"mock\":true}"
                }
            }
        };

        return Task.FromResult<CargoTrackingResult?>(result);
    }
}
