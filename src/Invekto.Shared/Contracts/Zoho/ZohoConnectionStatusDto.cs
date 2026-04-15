namespace Invekto.Shared.Contracts.Zoho;

/// <summary>
/// Adim 3 Paket 3-B1: tenant'in Zoho baglanti durumu (Dashboard UI icin).
/// Connected=false ise diger alanlar null. GET /api/v1/zoho/connection response body.
/// </summary>
public sealed class ZohoConnectionStatusDto
{
    public required bool Connected { get; init; }

    /// <summary>Zoho DC region (zoho.eu, zoho.com, vb). Connected=true iken doludur.</summary>
    public string? Region { get; init; }

    /// <summary>Baglama sirasinda Zoho'dan gelen kullanici e-postasi (opsiyonel).</summary>
    public string? ZohoUserEmail { get; init; }

    public DateTimeOffset? ConnectedAt { get; init; }

    public DateTimeOffset? LastRefreshedAt { get; init; }
}
