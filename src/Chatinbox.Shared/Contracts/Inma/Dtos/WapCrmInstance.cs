namespace Chatinbox.Shared.Contracts.Inma.Dtos;

/// <summary>
/// DTO for WapCRM API instance response, used in UpsertInstancesAsync.
/// </summary>
public sealed class WapCrmInstanceDto
{
    public required string InstanceId { get; init; }
    public required string InstanceName { get; init; }
    public string? Account { get; init; }
    public int InstanceType { get; init; }

    /// <summary>
    /// WapCRM connectionType ('WABA' | 'QR Code' | 'SMS' | 'Voip' | ...) — vendor-defined open set.
    /// instanceType=1 covers BOTH WABA and QR lines; this is the only reliable WABA discriminator.
    /// Trimmed, empty mapped to null by the fetch.
    /// </summary>
    public string? ConnectionType { get; init; }
}

/// <summary>
/// WapCRM GET /api/Instances response envelope.
/// </summary>
public sealed class WapCrmApiEnvelope
{
    public bool Status { get; set; }
    public string? Message { get; set; }
    public List<WapCrmRawInstance>? Data { get; set; }
}

/// <summary>
/// Single instance from WapCRM API response.
/// </summary>
public sealed class WapCrmRawInstance
{
    public int InstanceId { get; set; }
    public string? InstanceName { get; set; }
    public string? Account { get; set; }
    public int InstanceType { get; set; }
    public string? ConnectionType { get; set; }
}
