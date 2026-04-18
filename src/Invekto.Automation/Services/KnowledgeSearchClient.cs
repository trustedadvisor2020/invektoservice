using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// HTTP client for Knowledge service semantic search API.
/// Called by AiFaqHandler to search FAQs + document chunks via pgvector.
/// Graceful degradation: returns unavailable result on any failure.
/// Pattern: same as KnowledgeIntentClient (typed HttpClient, 3s timeout).
/// </summary>
public sealed class KnowledgeSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;

    public KnowledgeSearchClient(HttpClient httpClient, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Search Knowledge service for relevant FAQs and document chunks.
    /// Returns KnowledgeFaqSearchResult with parsed results or unavailable fallback.
    /// </summary>
    public async Task<KnowledgeFaqSearchResult> SearchAsync(
        int tenantId, string query, int topK, string? searchSource,
        string jwtToken, CancellationToken ct = default)
    {
        try
        {
            var requestBody = new
            {
                query,
                topK,
                lang = (string?)null,
                category = (string?)null
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"/api/v1/knowledge/{tenantId}/search");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));
            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search HTTP {(int)response.StatusCode} for tenant {tenantId}: {errorBody}");
                return KnowledgeFaqSearchResult.Unavailable($"HTTP {(int)response.StatusCode}");
            }

            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (json == null)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search null response for tenant {tenantId}");
                return KnowledgeFaqSearchResult.Unavailable("Null response body");
            }

            return ParseSearchResponse(json, searchSource);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // App shutting down — propagate
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search timeout for tenant {tenantId}");
            return KnowledgeFaqSearchResult.Unavailable("Timeout");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search connection error for tenant {tenantId}: {ex.Message}");
            return KnowledgeFaqSearchResult.Unavailable($"Connection error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationKnowledgeSearchFailed}] Knowledge search parse error for tenant {tenantId}: {ex.Message}");
            return KnowledgeFaqSearchResult.Unavailable($"Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// FEAT-WTP: fetch the active+published variant pool for a tenant group_tag from Knowledge.
    /// Returns the raw template list (each row = one variant); caller extracts
    /// content_json.text via <see cref="ExtractVariantText"/> after <see cref="Invekto.Shared.Services.ITemplateRotationService"/> picks an index.
    /// On any failure returns an empty list and logs INV-AT-066 — caller falls back to
    /// inline text_variants (G3) or data.text (legacy). Never throws.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeTemplateVariant>> FetchVariantPoolAsync(
        int tenantId, string groupTag, string? lang, string jwtToken, CancellationToken ct = default)
    {
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(groupTag))
            return Array.Empty<KnowledgeTemplateVariant>();

        try
        {
            // Path-encode the group_tag (free-text VARCHAR(50) may contain '_' only in practice
            // but Uri.EscapeDataString is defensive against future tag formats).
            var encoded = Uri.EscapeDataString(groupTag);
            var url = $"/api/v1/templates/{tenantId}/group/{encoded}";
            if (!string.IsNullOrEmpty(lang))
                url += $"?lang={Uri.EscapeDataString(lang)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);
            request.Headers.Add("X-Request-Id", Guid.NewGuid().ToString("N"));

            using var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] FetchVariantPool HTTP {(int)response.StatusCode} tenant={tenantId} group_tag={groupTag}");
                return Array.Empty<KnowledgeTemplateVariant>();
            }

            using var json = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (json == null || !json.RootElement.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
            {
                // Malformed response (missing or non-array `items`) — INV-AT-066 observable
                // so ops can distinguish payload shape issues from transport/HTTP failures.
                _logger.SystemWarn(
                    $"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] FetchVariantPool malformed response (missing items array) tenant={tenantId} group_tag={groupTag}");
                return Array.Empty<KnowledgeTemplateVariant>();
            }

            var list = new List<KnowledgeTemplateVariant>(itemsEl.GetArrayLength());
            foreach (var row in itemsEl.EnumerateArray())
            {
                var id = row.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                var text = ExtractVariantText(row);
                if (id > 0 && !string.IsNullOrWhiteSpace(text))
                    list.Add(new KnowledgeTemplateVariant { Id = id, Text = text! });
            }
            return list;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] FetchVariantPool timeout tenant={tenantId} group_tag={groupTag}");
            return Array.Empty<KnowledgeTemplateVariant>();
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] FetchVariantPool transport tenant={tenantId} group_tag={groupTag}: {ex.Message}");
            return Array.Empty<KnowledgeTemplateVariant>();
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationTemplateGroupFetchFailed}] FetchVariantPool parse tenant={tenantId} group_tag={groupTag}: {ex.Message}");
            return Array.Empty<KnowledgeTemplateVariant>();
        }
    }

    /// <summary>
    /// Extract the variant text from a template row. Convention:
    ///   content_json = { "text": "..." }
    /// Falls through: "text" -> "answer" -> null. Returns null for whitespace-only values.
    /// </summary>
    private static string? ExtractVariantText(JsonElement row)
    {
        if (!row.TryGetProperty("content_json", out var content))
            return null;

        // The catalog stores content_json as a JSONB blob; the endpoint emits it either as
        // an object (most rows) or a JSON string (older rows escaped by the repo). Handle both.
        JsonElement body = content;
        if (body.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var doc = JsonDocument.Parse(body.GetString() ?? "{}");
                body = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (body.ValueKind != JsonValueKind.Object)
            return null;

        if (body.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            return textEl.GetString();
        if (body.TryGetProperty("answer", out var ansEl) && ansEl.ValueKind == JsonValueKind.String)
            return ansEl.GetString();
        return null;
    }

    private static KnowledgeFaqSearchResult ParseSearchResponse(JsonDocument json, string? searchSource)
    {
        var root = json.RootElement;
        var items = new List<KnowledgeFaqSearchItem>();

        if (!root.TryGetProperty("results", out var resultsArr))
            return new KnowledgeFaqSearchResult { Available = true, Items = items };

        foreach (var item in resultsArr.EnumerateArray())
        {
            var sourceType = item.TryGetProperty("sourceType", out var st) ? st.GetString() ?? "" : "";
            var score = item.TryGetProperty("score", out var sc) ? sc.GetDouble() : 0;

            // Filter by search_source: "faq_only" skips chunks
            if (searchSource == "faq_only" && sourceType != "faq")
                continue;

            if (sourceType == "faq")
            {
                var question = item.TryGetProperty("question", out var q) ? q.GetString() : null;
                var answer = item.TryGetProperty("answer", out var a) ? a.GetString() : null;

                if (!string.IsNullOrEmpty(answer))
                {
                    items.Add(new KnowledgeFaqSearchItem
                    {
                        SourceType = "faq",
                        Score = score,
                        Answer = answer,
                        Question = question ?? "",
                        ChunkContent = null,
                        DocumentTitle = null,
                        PageNumber = null
                    });
                }
            }
            else if (sourceType == "chunk")
            {
                var content = item.TryGetProperty("content", out var c) ? c.GetString() : null;
                var docTitle = item.TryGetProperty("documentTitle", out var dt) ? dt.GetString() : null;
                var pageNum = item.TryGetProperty("pageNumber", out var pn) && pn.ValueKind == JsonValueKind.Number
                    ? (int?)pn.GetInt32() : null;

                if (!string.IsNullOrEmpty(content))
                {
                    items.Add(new KnowledgeFaqSearchItem
                    {
                        SourceType = "chunk",
                        Score = score,
                        Answer = null,
                        Question = null,
                        ChunkContent = content,
                        DocumentTitle = docTitle ?? "Dokuman",
                        PageNumber = pageNum
                    });
                }
            }
        }

        var method = root.TryGetProperty("method", out var m) ? m.GetString() ?? "unknown" : "unknown";
        var durationMs = root.TryGetProperty("durationMs", out var d) ? d.GetInt32() : 0;

        return new KnowledgeFaqSearchResult
        {
            Available = true,
            Items = items,
            Method = method,
            DurationMs = durationMs
        };
    }
}

/// <summary>
/// Result from Knowledge service search, tailored for ai_faq node usage.
/// </summary>
public sealed class KnowledgeFaqSearchResult
{
    public bool Available { get; init; }
    public string? UnavailableReason { get; init; }
    public List<KnowledgeFaqSearchItem> Items { get; init; } = new();
    public string Method { get; init; } = "unknown";
    public int DurationMs { get; init; }

    public static KnowledgeFaqSearchResult Unavailable(string reason) => new()
    {
        Available = false,
        UnavailableReason = reason,
        Items = new List<KnowledgeFaqSearchItem>()
    };
}

/// <summary>
/// FEAT-WTP: one template row from a group_tag variant pool. Id identifies the underlying
/// template_catalog row for audit logging; Text is the extracted variant body.
/// </summary>
public sealed class KnowledgeTemplateVariant
{
    public required int Id { get; init; }
    public required string Text { get; init; }
}

/// <summary>
/// Single search result item — either FAQ (has Answer) or chunk (has ChunkContent).
/// </summary>
public sealed class KnowledgeFaqSearchItem
{
    public required string SourceType { get; init; }
    public double Score { get; init; }

    // FAQ fields
    public string? Answer { get; init; }
    public string? Question { get; init; }

    // Chunk fields
    public string? ChunkContent { get; init; }
    public string? DocumentTitle { get; init; }
    public int? PageNumber { get; init; }
}
