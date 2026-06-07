using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Inma.Dtos;

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
/// </summary>
public sealed class WapCrmTemplateDto
{
    /// <summary>cxapi template identifier (used later as the send <c>template.templateId</c>).</summary>
    [JsonPropertyName("templateId")] public string? TemplateId { get; init; }

    /// <summary>Human-readable preview text of the rendered template.</summary>
    [JsonPropertyName("preview")] public string? Preview { get; init; }

    /// <summary>A fixed note cxapi attaches automatically (e.g. for static media), if any.</summary>
    [JsonPropertyName("fixedNote")] public string? FixedNote { get; init; }

    /// <summary>The inputs the operator must fill at send time (text params + dynamic media).</summary>
    [JsonPropertyName("requiredInputs")] public List<WapCrmRequiredInputDto>? RequiredInputs { get; init; }
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

    /// <summary>Per-attempt timeout (ms). Enforced by a linked CTS — the underlying HttpClient timeout is Infinite.</summary>
    public int TimeoutMs { get; set; } = 10_000;
}
