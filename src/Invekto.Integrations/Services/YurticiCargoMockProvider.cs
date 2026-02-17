using Invekto.Shared.Logging;

namespace Invekto.Integrations.Services;

/// <summary>
/// Mock implementation of Yurtici Kargo tracking API (GR-3.6).
/// </summary>
public sealed class YurticiCargoMockProvider : ICargoProvider
{
    private readonly JsonLinesLogger _logger;

    public YurticiCargoMockProvider(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    public string ProviderName => "yurtici";

    public Task<CargoTrackingResult?> TrackShipmentAsync(
        string trackingCode, CancellationToken ct)
    {
        _logger.SystemWarn($"[YURTICI-MOCK] TrackShipment called (mock mode, tracking={trackingCode})");

        var result = new CargoTrackingResult
        {
            TrackingCode = trackingCode,
            CargoProvider = ProviderName,
            CurrentStatus = "out_for_delivery",
            Events = new List<CargoEvent>
            {
                new()
                {
                    Status = "picked_up",
                    Location = "Izmir - Toplama Merkezi",
                    EventTime = DateTime.UtcNow.AddDays(-3),
                    RawDataJson = "{\"mock\":true}"
                },
                new()
                {
                    Status = "in_transit",
                    Location = "Istanbul - Dagitim",
                    EventTime = DateTime.UtcNow.AddDays(-1),
                    RawDataJson = "{\"mock\":true}"
                },
                new()
                {
                    Status = "out_for_delivery",
                    Location = "Istanbul - Kadikoy Subesi",
                    EventTime = DateTime.UtcNow.AddHours(-2),
                    RawDataJson = "{\"mock\":true}"
                }
            }
        };

        return Task.FromResult<CargoTrackingResult?>(result);
    }
}
