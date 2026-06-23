using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.Inma.Dtos;

// ============================================================================
// FEAT-PROJELER / cxapi — PKT-14 S3 (WapCrmTemplateClient)
// READ-ONLY: typed request/result/options + wire DTOs for listing a tenant's
// WhatsApp approved (HSM) templates from cxapi POST /api/templates.
// Mirrors the PR-2 WapCrmSendClient shape. There is NO send path here.
// New error codes (INV-BE-127..130) live with the Backend consumer, not here —
// this client returns a typed outcome enum.
// ============================================================================

/// <summary>
/// A single cxapi approved (HSM) template (one element of the
/// <c>POST /api/templates</c> <c>data[]</c>). The JSON names mirror cxapi's
/// camelCase wire shape and double as the SPA-facing contract (Backend serializes
/// camelCase), so the same property serves deserialize-from-cxapi and
/// serialize-to-SPA.
///
/// IMPORTANT (2026-06-09): the live cxapi wire shape was verified against
/// instance 6570 — it differs from the integration guide. <c>preview</c> is a
/// STRUCTURED OBJECT (header/body/footer/buttons), NOT a string; modelling it as a
/// string threw a JsonException → the whole list parse failed → the endpoint
/// returned 502. cxapi also returns <c>name</c>/<c>language</c>/<c>category</c>/
/// <c>paramFormat</c> per template (each language is a separate template slug).
/// </summary>
public sealed class WapCrmTemplateDto
{
    /// <summary>cxapi template identifier (used later as the send <c>template.templateId</c>).</summary>
    [JsonPropertyName("templateId")] public string? TemplateId { get; init; }

    /// <summary>Template name slug (e.g. <c>siparis_bilgi</c>). One slug per language.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }

    /// <summary>Template language tag (e.g. <c>tr</c>, <c>en</c>, <c>en_US</c>). A separate template exists per language.</summary>
    [JsonPropertyName("language")] public string? Language { get; init; }

    /// <summary>Meta category (e.g. <c>MARKETING</c> | <c>UTILITY</c> | <c>AUTHENTICATION</c>).</summary>
    [JsonPropertyName("category")] public string? Category { get; init; }

    /// <summary>Parameter style of the template body (<c>named</c> | <c>positional</c>).</summary>
    [JsonPropertyName("paramFormat")] public string? ParamFormat { get; init; }

    /// <summary>Structured preview of the rendered template (header/body/footer/buttons). Object, not text.</summary>
    [JsonPropertyName("preview")] public WapCrmTemplatePreviewDto? Preview { get; init; }

    /// <summary>A fixed note cxapi attaches automatically (e.g. for static media), if any.</summary>
    [JsonPropertyName("fixedNote")] public string? FixedNote { get; init; }

    /// <summary>The inputs the operator must fill at send time (text params + dynamic media).</summary>
    [JsonPropertyName("requiredInputs")] public List<WapCrmRequiredInputDto>? RequiredInputs { get; init; }
}

/// <summary>Structured rendered-template preview (cxapi <c>preview</c> object). Any sub-field may be null/empty.</summary>
public sealed class WapCrmTemplatePreviewDto
{
    /// <summary>Header preview, or null when the template has no header.</summary>
    [JsonPropertyName("header")] public WapCrmTemplatePreviewHeaderDto? Header { get; init; }

    /// <summary>Rendered body text (placeholders shown as <c>{{key}}</c>).</summary>
    [JsonPropertyName("body")] public string? Body { get; init; }

    /// <summary>Footer text, if any.</summary>
    [JsonPropertyName("footer")] public string? Footer { get; init; }

    /// <summary>Buttons attached to the template (possibly empty).</summary>
    [JsonPropertyName("buttons")] public List<WapCrmTemplateButtonDto>? Buttons { get; init; }
}

/// <summary>Header sub-object of a template preview.</summary>
public sealed class WapCrmTemplatePreviewHeaderDto
{
    /// <summary><c>TEXT</c> | <c>IMAGE</c> | <c>VIDEO</c> | <c>DOCUMENT</c>.</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    /// <summary>Header text for a <c>TEXT</c> header; null for a media header.</summary>
    [JsonPropertyName("text")] public string? Text { get; init; }
}

/// <summary>One template button (cxapi <c>preview.buttons[]</c>).</summary>
public sealed class WapCrmTemplateButtonDto
{
    /// <summary><c>QUICK_REPLY</c> | <c>URL</c> | <c>PHONE_NUMBER</c>.</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    /// <summary>Button label shown to the recipient.</summary>
    [JsonPropertyName("text")] public string? Text { get; init; }
}

/// <summary>One operator-fillable input of a template (cxapi <c>requiredInputs[]</c>).</summary>
public sealed class WapCrmRequiredInputDto
{
    /// <summary><c>text</c> | <c>media</c>.</summary>
    [JsonPropertyName("kind")] public string? Kind { get; init; }

    /// <summary><c>BODY</c> | <c>HEADER</c> | <c>BUTTON</c>.</summary>
    [JsonPropertyName("location")] public string? Location { get; init; }

    /// <summary>For a text input: the key matched at send time by <c>parameters[].paramKey</c>.</summary>
    [JsonPropertyName("paramKey")] public string? ParamKey { get; init; }

    /// <summary>For a media input: <c>image</c> | <c>video</c> | <c>document</c>.</summary>
    [JsonPropertyName("mediaType")] public string? MediaType { get; init; }

    /// <summary>Free-text hint describing the input.</summary>
    [JsonPropertyName("note")] public string? Note { get; init; }
}

/// <summary>
/// Terminal classification of a single <see cref="WapCrmTemplateClient"/> list call.
/// Unlike the send client there is NO <c>Ambiguous</c>: a GET-style read has no
/// duplicate side-effect, so a timeout is a plain <see cref="TimedOut"/> the caller
/// maps to 504.
/// </summary>
public enum WapCrmTemplateListOutcome
{
    /// <summary>HTTP 2xx + envelope status=true. The list (possibly empty) is in <see cref="WapCrmTemplateListResult.Templates"/>.</summary>
    Success,

    /// <summary>The provider responded but rejected the request (envelope status!=true with a non-rate-limit code, e.g. 509/621/400).</summary>
    ProviderRejected,

    /// <summary>Rate limited (HTTP 301/302, or envelope statusCode '301'/'302' while status!=true). Provider-documented; the interactive caller retries (no internal retry loop).</summary>
    RateLimited,

    /// <summary>The per-attempt linked-CTS timeout fired. Read-only, so safe — surfaced distinctly so it maps to 504, not a generic transport 502.</summary>
    TimedOut,

    /// <summary>A connection/transport error, an unexpected non-2xx, or an unparseable body.</summary>
    TransportError
}

/// <summary>
/// The typed outcome of a template list. The client never throws on a
/// provider/transport failure (only on a caller-contract violation or caller
/// cancellation), so the Backend endpoint maps each outcome 1:1 to an HTTP status
/// + INV-BE code. The per-tenant secret is never carried here.
/// </summary>
public sealed class WapCrmTemplateListResult
{
    public required WapCrmTemplateListOutcome Outcome { get; init; }

    public bool IsSuccess => Outcome == WapCrmTemplateListOutcome.Success;

    /// <summary>The approved templates — populated only on <see cref="WapCrmTemplateListOutcome.Success"/> (empty otherwise).</summary>
    public IReadOnlyList<WapCrmTemplateDto> Templates { get; init; } = [];

    /// <summary>Raw HTTP status of the call (0 when no response was received — transport error/timeout).</summary>
    public int HttpStatusCode { get; init; }

    /// <summary>Envelope <c>statusCode</c> (provider application code, e.g. 509/621). Safe to surface to the SPA.</summary>
    public string? ProviderStatusCode { get; init; }

    /// <summary>Envelope <c>requestID</c> — provider correlation id, safe to surface for support.</summary>
    public string? ProviderRequestId { get; init; }

    /// <summary>
    /// Envelope <c>message</c> or a short transport diagnostic. INTERNAL diagnostics
    /// only — the Backend logs it (length-capped) and does NOT echo it to the SPA.
    /// Never contains the secret (the secret is a header, never put in this field).
    /// </summary>
    public string? ProviderMessage { get; init; }

    /// <summary>For <see cref="WapCrmTemplateListOutcome.RateLimited"/>: the suggested cooldown, if the provider supplied a Retry-After.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>Echoes the request tenant (for logging only — never sent on the wire).</summary>
    public int TenantId { get; init; }

    /// <summary>Echoes the request instance.</summary>
    public int InstanceId { get; init; }
}

/// <summary>
/// Tuning for <see cref="WapCrmTemplateClient"/>. Bound from the optional
/// "WapCrmTemplates" config section; safe defaults apply when absent. Carries NO
/// secrets. <see cref="BaseUrl"/> is a FIXED server-side value — the tenant's
/// own <c>WapCrmSettings.ApiUrl</c> is intentionally NEVER used as the egress
/// target (SSRF / secret-exfiltration mitigation).
/// </summary>
public sealed class WapCrmTemplateOptions
{
    public const string SectionName = "WapCrmTemplates";

    /// <summary>cxapi base URL. The client posts to <c>api/templates</c> relative to this.</summary>
    public string BaseUrl { get; set; } = "https://cxapi.wapcrm.net/";

    /// <summary>Per-attempt timeout (ms). PRIMARY control: a per-attempt linked CTS. HttpClient.Timeout is set to a
    /// FINITE hard backstop (<see cref="HttpClientBackstopMs"/>) — never Infinite — so a stuck socket the linked CTS
    /// fails to cancel cannot hang the request thread (same class of bug as WapCrmSendClient, incident 2026-06-12).</summary>
    public int TimeoutMs { get; set; } = 10_000;

    /// <summary>Buffer (ms) added to <see cref="TimeoutMs"/> to derive the FINITE HttpClient.Timeout backstop.
    /// Clamped to a 1s floor so the backstop is ALWAYS &gt; the per-attempt CTS (which therefore fires first).</summary>
    public int HttpClientBackstopBufferMs { get; set; } = 5_000;

    /// <summary>FINITE HttpClient.Timeout hard backstop (ms) = TimeoutMs + buffer (buffer floored at 1s). Used at
    /// DI registration instead of Timeout.InfiniteTimeSpan so no template fetch can hang past this ceiling.</summary>
    public int HttpClientBackstopMs => TimeoutMs + Math.Max(HttpClientBackstopBufferMs, 1_000);
}
