using Chatinbox.Shared.Contracts.Photos;

namespace Chatinbox.Backend.Services.Photos;

/// <summary>
/// FEAT-PHOTO inbound media event entry point. Backend webhook layer
/// (InmaInboundMediaEndpoint) bu handler'a delege eder; tek sorumluluk
/// noktasi: idempotency anchor + atomic UPDATE leads + structured event
/// emission.
///
/// Interface uzerinden tanimlanir cunku:
///   * DI container'da concrete <see cref="PhotoInboundHandler"/> register
///     edilir (Backend Program.cs).
///   * Test layer (Chatinbox.Backend.Tests) NSubstitute ile mock'layabilir;
///     concrete repository new'leme yerine interface uzerinden assertion.
///   * Cross-service hop ileri paketlerde gerekirse (ChatAnalysis foto
///     intent prompt'u tetikleme), interface bagi sayesinde Backend
///     tarafini sealed tutmadan extension yapilabilir.
/// </summary>
public interface IPhotoInboundHandler
{
    /// <summary>
    /// INMA WhatsApp inbound media event'i icin atomic UPDATE + idempotency
    /// guard. Caller (InmaInboundMediaEndpoint) raw IncomingWebhookEvent.imageUrl
    /// + chatId -> phone resolve -> tenant_id + lead_id lookup zincirinden gecip
    /// bu handler'a uygun input'la cagirir.
    ///
    /// Returning <see cref="PhotoInboundOutcome"/> caller'a uc bilgi verir:
    ///   1. <see cref="PhotoInboundOutcome.FirstReceive"/> — ilk foto mu?
    ///   2. <see cref="PhotoInboundOutcome.PhotoCount"/> — UPDATE post-state.
    ///   3. <see cref="PhotoInboundOutcome.IsDuplicate"/> — at-least-once
    ///      redelivery dedup yakaladi mi (INV-AT-073 path).
    /// </summary>
    /// <param name="tenantId">JWT veya webhook auth layer'in resolve ettigi tenant.</param>
    /// <param name="leadId">phone -> leads.id lookup sonucu (caller resolve eder).</param>
    /// <param name="mediaUrl">INMA tarafinda host edilen INBOUND media URL'i.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PhotoInboundOutcome> HandleInboundMediaAsync(
        int tenantId,
        int leadId,
        string mediaUrl,
        CancellationToken ct);
}

/// <summary>
/// Handler outcome carrier — caller (InmaInboundMediaEndpoint) HTTP shape'i
/// karar verir. <see cref="ResultKind"/> uc terminal vaka ayirir:
///   * Success: ilk foto kaydi VEYA mevcut foto count incremented.
///   * Duplicate: idempotency UNIQUE violation, photo_count degismedi.
///   * TenantMismatch: leads.tenant_id != caller tenant; idempotency INSERT
///     yapilmadi (cross-tenant pollution guard).
///   * UpdateMiss: idempotency PASS oldu ama leads UPDATE 0 row matched
///     (lead silinmis VEYA photo_status='rejected'). Caller 422 doner.
/// FirstReceive = Success + count==1.
/// PhotoRequestEvent emit etmek caller'a kalir; handler kendi audit log
/// line'ini her zaman atar (INV-AT-073 swallow / INV-AT-078 DB error /
/// INV-AT-083 tenant mismatch / INV-AT-084 update miss).
/// </summary>
public enum PhotoInboundResultKind
{
    Success = 0,
    Duplicate = 1,
    TenantMismatch = 2,
    UpdateMiss = 3
}

public readonly record struct PhotoInboundOutcome(
    PhotoInboundResultKind ResultKind,
    bool FirstReceive,
    int PhotoCount,
    PhotoStatus Status)
{
    public bool IsDuplicate => ResultKind == PhotoInboundResultKind.Duplicate;
    public bool IsTenantMismatch => ResultKind == PhotoInboundResultKind.TenantMismatch;
    public bool IsUpdateMiss => ResultKind == PhotoInboundResultKind.UpdateMiss;
    public bool IsSuccess => ResultKind == PhotoInboundResultKind.Success;
}
