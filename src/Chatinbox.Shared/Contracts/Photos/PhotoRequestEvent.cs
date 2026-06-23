using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Photos;

/// <summary>
/// FEAT-PHOTO: PhotoInboundHandler atomic UPDATE'i basarili oldugunda log /
/// audit trail / downstream notification icin emit edilen event DTO.
/// Backend internal context'inde tasinir (henuz cross-service hop yok); ileri
/// paketlerde ChatAnalysis veya Marketing tarafinda subscriber eklenebilir
/// (ornegin "5 foto geldi -> doctor briefing" tetigi). Bu yuzden Shared.
///
/// Snake-case JSON property names HTTP wire convention'una uyar
/// (lessons-learned 2026-04-13: PostgreSQL JSON serialization snake_case ile
/// hizalanir, dual naming policy gereksiz).
/// </summary>
public sealed class PhotoRequestEvent
{
    [JsonPropertyName("lead_id")]
    public required int LeadId { get; init; }

    [JsonPropertyName("tenant_id")]
    public required int TenantId { get; init; }

    /// <summary>
    /// INMA inbound media event'inin orijinal media URL'i. Persistent storage
    /// YOK — INMA URL'i tutulur (storage drift onleme). PhotoEndpoints
    /// /api/v1/leads/{id}/photos GET endpoint'i INMA /api/chats/getimage
    /// proxy uzerinden bu URL'i thumbnail olarak servis eder.
    /// </summary>
    [JsonPropertyName("media_url")]
    public required string MediaUrl { get; init; }

    /// <summary>
    /// Idempotency anchor — SHA-256(media_url) hex lowercase. Migration 034
    /// photo_inbound_idempotency.media_url_hash kolonu (CHAR(64)) ile birebir.
    /// </summary>
    [JsonPropertyName("media_url_sha256")]
    public required string MediaUrlSha256 { get; init; }

    /// <summary>
    /// Handler-side now(). DB'deki photo_received_at degeri ile aynidir
    /// (atomic UPDATE'in tetikledigi degeri yansitir).
    /// </summary>
    [JsonPropertyName("received_at")]
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Atomic UPDATE'in WHERE photo_status&lt;&gt;'received' guard'i sonrasi
    /// rowsAffected=1 oldugu durumlarda true; INMA at-least-once redelivery
    /// duplicate'i icin false (handler yine de bilgi amacli event emit
    /// ederse). Audit / downstream logic FirstReceive=true icin "ilk foto"
    /// sinyalini kullanir, duplicate'lar icin no-op.
    /// </summary>
    [JsonPropertyName("first_receive")]
    public required bool FirstReceive { get; init; }

    /// <summary>
    /// Atomic UPDATE post-state photo_count. FirstReceive=true ise &gt;=1,
    /// duplicate path'inde mevcut count (UPDATE WHERE guard'i tetiklendi
    /// degilse mevcut deger).
    /// </summary>
    [JsonPropertyName("photo_count")]
    public required int PhotoCount { get; init; }
}
