using Chatinbox.Shared.Contracts.MetaLeadgen;

namespace Chatinbox.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: Graph API wrapper surface. Exposed as an interface so the
/// Backend internal process-lead endpoint can unit-test the orchestration path
/// without issuing real HTTP calls to Meta.
///
/// Implementation lives in <see cref="MetaGraphApiClient"/>; registration wires
/// an IHttpClientFactory named client <c>"meta-graph"</c> with
/// <c>BaseAddress = https://graph.facebook.com/v21.0/</c> and a 10s timeout.
/// </summary>
public interface IMetaGraphApiClient
{
    /// <summary>
    /// GET /v21.0/{leadgenId}?access_token=...&amp;fields=field_data,form_id,created_time.
    /// Returns <see cref="MetaFetchLeadResult.Success"/> carrying the parsed
    /// field_data + form_id + created_time on 2xx; otherwise a typed failure
    /// with INV-META-003 (transient), INV-META-006 (auth), or INV-META-004
    /// (parse). Exceptions are caught by the implementation; this interface
    /// never throws.
    /// </summary>
    Task<MetaFetchLeadResult> FetchLeadAsync(
        string leadgenId, string? pageAccessToken, CancellationToken ct);

    /// <summary>
    /// GET /v21.0/{pageId}/leadgen_forms?access_token=...&amp;fields=id,name,questions{id,label,type}.
    /// Backs the Dashboard [Keşfet: Form Fields] button. Response cached in a
    /// process-local memory cache (10min TTL) keyed by <c>(pageId, accessToken)</c>
    /// so a Dashboard operator clicking the button repeatedly during edit does
    /// not fan out to Meta on every click.
    /// </summary>
    Task<MetaDiscoverFormsResult> DiscoverFormsAsync(
        string pageId, string? pageAccessToken, CancellationToken ct);
}

/// <summary>
/// FetchLead outcome — success carries field_data; failure carries an
/// INV-META-* code. Field_data keys are Meta question_ids; values are the raw
/// strings Meta submitted (un-normalized; caller maps to canonical via
/// <c>field_id_map</c>).
/// </summary>
public sealed class MetaFetchLeadResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public string? FormId { get; init; }
    public DateTime? CreatedTime { get; init; }

    /// <summary>question_id -> raw value. Empty when <see cref="IsSuccess"/> is false.</summary>
    public Dictionary<string, string> FieldData { get; init; } = new();

    public static MetaFetchLeadResult Success(
        Dictionary<string, string> fieldData, string? formId, DateTime? createdTime)
        => new()
        {
            IsSuccess   = true,
            FieldData   = fieldData,
            FormId      = formId,
            CreatedTime = createdTime
        };

    public static MetaFetchLeadResult Fail(string errorCode, string message)
        => new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = message };
}

/// <summary>DiscoverForms outcome — success carries the form tree; failure carries INV-META-*.</summary>
public sealed class MetaDiscoverFormsResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public MetaLeadgenDiscoverFormsResponse? Forms { get; init; }

    public static MetaDiscoverFormsResult Success(MetaLeadgenDiscoverFormsResponse forms)
        => new() { IsSuccess = true, Forms = forms };

    public static MetaDiscoverFormsResult Fail(string errorCode, string message)
        => new() { IsSuccess = false, ErrorCode = errorCode, ErrorMessage = message };
}
