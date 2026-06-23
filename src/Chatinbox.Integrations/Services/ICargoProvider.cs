namespace Chatinbox.Integrations.Services;

/// <summary>
/// Interface for cargo/shipping provider integrations (GR-3.6).
/// Mock implementations until real API credentials are available.
/// </summary>
public interface ICargoProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Track a shipment by tracking code.
    /// </summary>
    Task<CargoTrackingResult?> TrackShipmentAsync(
        string trackingCode, CancellationToken ct);
}

/// <summary>
/// Cargo tracking result from provider.
/// </summary>
public sealed class CargoTrackingResult
{
    public string TrackingCode { get; set; } = "";
    public string CargoProvider { get; set; } = "";
    public string CurrentStatus { get; set; } = "";
    public List<CargoEvent> Events { get; set; } = new();
}

public sealed class CargoEvent
{
    public string Status { get; set; } = "";
    public string? Location { get; set; }
    public DateTime EventTime { get; set; }
    public string? RawDataJson { get; set; }
}
