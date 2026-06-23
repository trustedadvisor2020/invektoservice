namespace Chatinbox.Shared.Contracts.Photos;

/// <summary>
/// FEAT-PHOTO: leads.photo_status DB enum mirror.
/// CHECK constraint <c>chk_leads_photo_status</c> on the leads table is the
/// authority — this enum must stay in lock-step with migration 034 §1.
///
/// Lifecycle:
///   * <see cref="None"/> — varsayilan; foto akisi henuz tetiklenmedi (lead
///     yeni olusturuldu veya P11 slot booking olmadi).
///   * <see cref="Requested"/> — PhotoRequestDispatchJob A varyantini
///     gonderdi; lead foto bekliyor.
///   * <see cref="Received"/> — PhotoInboundHandler ilk INMA inbound media
///     event'ini gordu; photo_received_at = now() ve photo_count >= 1.
///   * <see cref="Escalated"/> — 48 saat sessizlik; PhotoEscalationJob
///     koordinator devrini isaretledi (Dashboard rozet otomatik gosterir).
///   * <see cref="Rejected"/> — post-pilot icin reserve (hasta foto vermek
///     istemedigini belirtti). Pilotta job'lar bu status'u set etmez.
/// </summary>
public enum PhotoStatus
{
    None = 0,
    Requested = 1,
    Received = 2,
    Escalated = 3,
    Rejected = 4
}

/// <summary>
/// DB string deger ile enum cevirimi. Dispatch / Reminder / Escalation jobs ve
/// PhotoInboundHandler birden fazla yerde ayni serialization'i kullaniyor;
/// inline switch yerine merkezi extension method drift'i onler. Codex iter 0
/// CQ7 feedback (lessons-learned 2026-04-25): runtime-side enum-to-string
/// repetition magic-string lekesidir.
/// </summary>
public static class PhotoStatusExtensions
{
    public const string DbValueNone       = "none";
    public const string DbValueRequested  = "requested";
    public const string DbValueReceived   = "received";
    public const string DbValueEscalated  = "escalated";
    public const string DbValueRejected   = "rejected";

    public static string ToDbValue(this PhotoStatus status) => status switch
    {
        PhotoStatus.None       => DbValueNone,
        PhotoStatus.Requested  => DbValueRequested,
        PhotoStatus.Received   => DbValueReceived,
        PhotoStatus.Escalated  => DbValueEscalated,
        PhotoStatus.Rejected   => DbValueRejected,
        _ => DbValueNone
    };

    /// <summary>
    /// DB string deger -> enum. Tanimli set: none/requested/received/escalated/
    /// rejected (migration 034 chk_leads_photo_status CHECK constraint mirror).
    /// Bilinmeyen veya null gelirse, caller drift uyarisini almak isteyebilir
    /// (CHECK constraint silinmedikce DB'ye invalid string yazilamaz; bu yol
    /// ancak schema drift / pre-migration row'da tetiklenir). Caller
    /// <c>recognized=false</c> sinyalini log + INV-AT-083 path olarak
    /// kullanabilir; default <see cref="PhotoStatus.None"/> savunmaci fallback.
    /// </summary>
    public static PhotoStatus FromDbValue(string? value, out bool recognized)
    {
        switch (value)
        {
            case DbValueNone:       recognized = true; return PhotoStatus.None;
            case DbValueRequested:  recognized = true; return PhotoStatus.Requested;
            case DbValueReceived:   recognized = true; return PhotoStatus.Received;
            case DbValueEscalated:  recognized = true; return PhotoStatus.Escalated;
            case DbValueRejected:   recognized = true; return PhotoStatus.Rejected;
            default:                recognized = false; return PhotoStatus.None;
        }
    }

    /// <summary>
    /// Convenience overload — recognized sinyali gerekmedigi yerlerde caller
    /// kisa form kullanabilir; bilinmeyen yine None'a duser.
    /// </summary>
    public static PhotoStatus FromDbValue(string? value) => FromDbValue(value, out _);

    /// <summary>
    /// Terminal-state guard: PhotoRequestReminderJob / PhotoEscalationJob
    /// pre-check'inde "foto geldi mi yada zaten escalated mi" sorusunu tek
    /// yerde tutar. <see cref="Received"/> + <see cref="Escalated"/> +
    /// <see cref="Rejected"/> = double-send guard tetiklenir, early-return.
    /// FollowupStageJob terminal-skip pattern (line 35-62) precedent'ini
    /// foto akisi icin tekrar uygular.
    /// </summary>
    public static bool IsTerminalForPhotoRequest(this PhotoStatus status) =>
        status is PhotoStatus.Received or PhotoStatus.Escalated or PhotoStatus.Rejected;
}
