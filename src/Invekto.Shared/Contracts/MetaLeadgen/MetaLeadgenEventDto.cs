using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.MetaLeadgen;

/// <summary>
/// Paket B-META: Dashboard [Test Events] panel row. Read-only projection of
/// <c>meta_leadgen_events</c> that DELIBERATELY omits <c>raw_payload</c> — Meta
/// Lead Form submissions contain user PII (phone, email, free-text answers) and
/// the Dashboard surface is admin-facing but not a "view PII" tool.
///
/// <see cref="PayloadKeys"/> exposes the top-level JSON keys (e.g. "leadgen_id",
/// "form_id", "page_id", "created_time") so operators can confirm Meta is sending
/// the expected envelope shape without leaking customer contact data.
///
/// Ops teams needing raw payload for deep debug go through the DB directly
/// (invekto role access governed separately, same as leads / chat_messages).
/// </summary>
public sealed class MetaLeadgenEventDto
{
    [JsonPropertyName("id")]                public long       Id          { get; set; }
    [JsonPropertyName("leadgen_id")]        public string     LeadgenId   { get; set; } = string.Empty;
    [JsonPropertyName("form_id")]           public string?    FormId      { get; set; }
    [JsonPropertyName("page_id")]           public string?    PageId      { get; set; }
    [JsonPropertyName("received_at")]       public DateTime   ReceivedAt  { get; set; }
    [JsonPropertyName("http_status")]       public short?     HttpStatus  { get; set; }
    [JsonPropertyName("error_code")]        public string?    ErrorCode   { get; set; }
    [JsonPropertyName("lead_id")]           public int?       LeadId      { get; set; }

    /// <summary>
    /// Top-level JSON keys of <c>raw_payload</c> (no values). Empty when the
    /// INSERT captured signature-fail / tenant-unknown rows with a null payload.
    /// </summary>
    [JsonPropertyName("payload_keys")]
    public List<string> PayloadKeys { get; set; } = new();
}
