using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.MetaLeadgen;

/// <summary>
/// Paket B-META: tenant-level Meta Leadgen webhook config. Read from / written to
/// <c>tenant_settings.meta_leadgen_config JSONB</c>. Shape mirrors the Dashboard
/// /settings/meta-leadgen form so the SPA can round-trip GET/PUT without a
/// separate view-model.
///
/// Secret storage discipline (G3 2026-04-24 Q decision):
///   * VerifyToken / AppSecret / PageAccessToken are stored plaintext per LIW
///     precedent. Row-level access is governed by the `invekto` role (DB GRANT).
///     Application layer MUST NOT serialize this DTO into jsonl System* log
///     lines; pass individual redacted diagnostic fields (e.g. last-4-of-token)
///     if a log trace is needed. Unit tests pin this contract.
///
/// FieldIdMap shape:
///   key   = Meta question_id (stable per Lead Form; surfaced via /discover-forms)
///   value = canonical field name from the Invekto set:
///           { "name", "phone", "email", "custom_1".."custom_5", "consent_marketing" }
///   Operators edit this map in the Dashboard editor; MetaGraphApiClient.DiscoverFormsAsync
///   populates the left-column key picker so tenants don't need to copy IDs by hand.
/// </summary>
public sealed class MetaLeadgenConfigDto
{
    /// <summary>
    /// Meta webhook GET handshake token. 32-char random recommended. Rotated via
    /// Dashboard button; Meta Subscription setup must be updated on every rotation.
    /// </summary>
    [JsonPropertyName("verify_token")]
    public string? VerifyToken { get; set; }

    /// <summary>
    /// Meta App Secret — HMAC-SHA256 key for X-Hub-Signature-256 validation.
    /// Found in Meta App Dashboard → Settings → Basic → App Secret. Per-app, not
    /// per-page, so a multi-page integration shares one AppSecret across all
    /// page subscriptions on the same App.
    /// </summary>
    [JsonPropertyName("app_secret")]
    public string? AppSecret { get; set; }

    /// <summary>
    /// Page Access Token used for Graph API /{leadgen_id}?fields=field_data hops.
    /// Long-lived (~60 days) — Dashboard surfaces INV-META-006 when renewal is due;
    /// operators re-paste from Meta Business Manager.
    /// </summary>
    [JsonPropertyName("page_access_token")]
    public string? PageAccessToken { get; set; }

    /// <summary>Facebook Page id owning the Lead Form subscription.</summary>
    [JsonPropertyName("page_id")]
    public string? PageId { get; set; }

    /// <summary>
    /// Meta question_id → canonical field name map. Empty dictionary = lead rows
    /// will lack all non-synthetic canonical fields (phone / name / etc) so the
    /// intake layer rejects with INV-BE-104 on missing phone. Dashboard editor
    /// surfaces this expectation at save time.
    /// </summary>
    [JsonPropertyName("field_id_map")]
    public Dictionary<string, string> FieldIdMap { get; set; } = new();

    /// <summary>Convenience factory used by the repository when DB row is missing.</summary>
    public static MetaLeadgenConfigDto Empty() => new() { FieldIdMap = new Dictionary<string, string>() };
}
