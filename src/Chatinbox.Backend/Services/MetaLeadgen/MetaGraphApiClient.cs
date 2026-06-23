using System.Net;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.MetaLeadgen;
using Chatinbox.Shared.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Chatinbox.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: concrete Graph API wrapper. Typed catches for the three
/// transport failure classes Meta exposes (HttpRequestException, TaskCanceled,
/// JsonException); each maps to an <c>INV-META-*</c> code so Dashboard
/// [Test Events] and ops jsonl logs surface the root cause without raw
/// exception strings leaking the access token.
///
/// HttpClientFactory named client: <c>"meta-graph"</c>.
///   BaseAddress: <c>https://graph.facebook.com/v21.0/</c>
///   Timeout:     10 seconds (Meta's 99p on lead fetch is ~1.5s; 10s covers
///                long-tail TCP/TLS re-handshake without exhausting the
///                Hangfire job's CancellationToken budget).
/// </summary>
public sealed class MetaGraphApiClient : IMetaGraphApiClient
{
    /// <summary>Named client key for IHttpClientFactory registration in Program.cs.</summary>
    public const string HttpClientName = "meta-graph";

    private static readonly TimeSpan DiscoverFormsCacheTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly JsonLinesLogger _logger;

    public MetaGraphApiClient(
        IHttpClientFactory httpFactory, IMemoryCache cache, JsonLinesLogger logger)
    {
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MetaFetchLeadResult> FetchLeadAsync(
        string leadgenId, string? pageAccessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pageAccessToken))
            return MetaFetchLeadResult.Fail(ErrorCodes.MetaAccessTokenMissing,
                "Page Access Token missing for tenant.");

        var escaped = Uri.EscapeDataString(leadgenId);
        var url = $"{escaped}?fields=field_data,form_id,created_time&access_token={Uri.EscapeDataString(pageAccessToken)}";

        var client = _httpFactory.CreateClient(HttpClientName);
        HttpResponseMessage? response = null;
        try
        {
            response = await client.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = MapHttpStatusToErrorCode(response.StatusCode);
                _logger.SystemWarn(
                    $"[{errorCode}] MetaGraph FetchLead non-2xx: leadgen={leadgenId} " +
                    $"status={(int)response.StatusCode} bodyLen={body?.Length ?? 0}");
                return MetaFetchLeadResult.Fail(errorCode,
                    $"Graph API returned status {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            string? formId = null;
            DateTime? createdTime = null;
            if (root.TryGetProperty("form_id", out var formEl) && formEl.ValueKind == JsonValueKind.String)
                formId = formEl.GetString();
            if (root.TryGetProperty("created_time", out var ctEl))
            {
                if (ctEl.ValueKind == JsonValueKind.String && DateTime.TryParse(ctEl.GetString(), out var parsed))
                    createdTime = parsed.ToUniversalTime();
                else if (ctEl.ValueKind == JsonValueKind.Number && ctEl.TryGetInt64(out var epoch))
                    createdTime = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            }

            var fieldData = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("field_data", out var fdEl) && fdEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in fdEl.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    if (!entry.TryGetProperty("name", out var nameEl)) continue;
                    if (!entry.TryGetProperty("values", out var valuesEl)) continue;
                    if (nameEl.ValueKind != JsonValueKind.String) continue;
                    if (valuesEl.ValueKind != JsonValueKind.Array) continue;

                    var key = nameEl.GetString();
                    if (string.IsNullOrEmpty(key)) continue;
                    var first = valuesEl.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind != JsonValueKind.String) continue;
                    var value = first.GetString();
                    if (value is null) continue;

                    fieldData[key] = value;
                }
            }

            return MetaFetchLeadResult.Success(fieldData, formId, createdTime);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaGraphApiFetchFailed}] MetaGraph FetchLead transport error: leadgen={leadgenId} msg={ex.Message}");
            return MetaFetchLeadResult.Fail(ErrorCodes.MetaGraphApiFetchFailed,
                "Graph API transport failure.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaGraphApiFetchFailed}] MetaGraph FetchLead timeout: leadgen={leadgenId} msg={ex.Message}");
            return MetaFetchLeadResult.Fail(ErrorCodes.MetaGraphApiFetchFailed,
                "Graph API request timed out.");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaFieldDataParseFailed}] MetaGraph FetchLead JSON parse: leadgen={leadgenId} msg={ex.Message}");
            return MetaFetchLeadResult.Fail(ErrorCodes.MetaFieldDataParseFailed,
                "Graph API response JSON malformed.");
        }
        finally
        {
            response?.Dispose();
        }
    }

    public async Task<MetaDiscoverFormsResult> DiscoverFormsAsync(
        string pageId, string? pageAccessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(pageAccessToken))
            return MetaDiscoverFormsResult.Fail(ErrorCodes.MetaAccessTokenMissing,
                "Page Access Token missing for tenant.");
        if (string.IsNullOrWhiteSpace(pageId))
            return MetaDiscoverFormsResult.Fail(ErrorCodes.MetaFieldDataParseFailed,
                "page_id required for form discovery.");

        var cacheKey = $"meta-leadgen-forms:{pageId}:{pageAccessToken}";
        if (_cache.TryGetValue<MetaLeadgenDiscoverFormsResponse>(cacheKey, out var cached) && cached != null)
            return MetaDiscoverFormsResult.Success(cached);

        var url = $"{Uri.EscapeDataString(pageId)}/leadgen_forms?fields=id,name,questions%7Bid,label,type%7D" +
                  $"&access_token={Uri.EscapeDataString(pageAccessToken)}";

        var client = _httpFactory.CreateClient(HttpClientName);
        HttpResponseMessage? response = null;
        try
        {
            response = await client.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorCode = MapHttpStatusToErrorCode(response.StatusCode);
                _logger.SystemWarn(
                    $"[{errorCode}] MetaGraph DiscoverForms non-2xx: page={pageId} " +
                    $"status={(int)response.StatusCode} bodyLen={body?.Length ?? 0}");
                return MetaDiscoverFormsResult.Fail(errorCode,
                    $"Graph API returned status {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(body);
            var forms = new MetaLeadgenDiscoverFormsResponse();
            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var formEl in dataEl.EnumerateArray())
                {
                    var form = new MetaLeadgenForm
                    {
                        Id   = formEl.TryGetProperty("id", out var idEl)   && idEl.ValueKind   == JsonValueKind.String ? idEl.GetString()   ?? "" : "",
                        Name = formEl.TryGetProperty("name", out var nmEl) && nmEl.ValueKind   == JsonValueKind.String ? nmEl.GetString()   ?? "" : ""
                    };
                    if (formEl.TryGetProperty("questions", out var qsEl))
                    {
                        // Meta returns questions as {"data":[...]} nested object OR as a
                        // flat array depending on the expansion syntax; support both.
                        var qsArrayEl = qsEl.ValueKind == JsonValueKind.Object &&
                                        qsEl.TryGetProperty("data", out var nestedData)
                            ? nestedData : qsEl;
                        if (qsArrayEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var q in qsArrayEl.EnumerateArray())
                            {
                                if (q.ValueKind != JsonValueKind.Object) continue;
                                form.Questions.Add(new MetaLeadgenFormQuestion
                                {
                                    Id    = q.TryGetProperty("id", out var qid)   && qid.ValueKind  == JsonValueKind.String ? qid.GetString()  ?? "" : "",
                                    Label = q.TryGetProperty("label", out var ql) && ql.ValueKind   == JsonValueKind.String ? ql.GetString()   ?? "" : "",
                                    Type  = q.TryGetProperty("type", out var qt)  && qt.ValueKind   == JsonValueKind.String ? qt.GetString()   ?? "" : ""
                                });
                            }
                        }
                    }
                    forms.Forms.Add(form);
                }
            }

            _cache.Set(cacheKey, forms, DiscoverFormsCacheTtl);
            return MetaDiscoverFormsResult.Success(forms);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaGraphApiFetchFailed}] MetaGraph DiscoverForms transport error: page={pageId} msg={ex.Message}");
            return MetaDiscoverFormsResult.Fail(ErrorCodes.MetaGraphApiFetchFailed,
                "Graph API transport failure.");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaGraphApiFetchFailed}] MetaGraph DiscoverForms timeout: page={pageId} msg={ex.Message}");
            return MetaDiscoverFormsResult.Fail(ErrorCodes.MetaGraphApiFetchFailed,
                "Graph API request timed out.");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.MetaFieldDataParseFailed}] MetaGraph DiscoverForms JSON parse: page={pageId} msg={ex.Message}");
            return MetaDiscoverFormsResult.Fail(ErrorCodes.MetaFieldDataParseFailed,
                "Graph API response JSON malformed.");
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static string MapHttpStatusToErrorCode(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized  => ErrorCodes.MetaAccessTokenMissing,  // 401 — token expired / wrong
        HttpStatusCode.Forbidden     => ErrorCodes.MetaAccessTokenMissing,  // 403 — token scope issue
        HttpStatusCode.NotFound      => ErrorCodes.MetaGraphApiFetchFailed, // 404 — form/lead deprecated
        HttpStatusCode.BadRequest    => ErrorCodes.MetaFieldDataParseFailed, // 400 — malformed request
        _ => ErrorCodes.MetaGraphApiFetchFailed
    };
}
