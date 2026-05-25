using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Invekto.VoiceRuntime.Clients;

/// <summary>
/// FEAT-VFB F0.5 Chunk B (AD-22 + AD-23): Knowledge search wrapper invoked from the
/// Realtime function tool `search_knowledge_base` (wired up in Chunk C).
///
/// Knowledge endpoint: POST /api/v1/knowledge/{tenantId}/search (Bearer per-tenant service JWT).
/// Tool param top_k clamped to [1,10] BEFORE forwarding to Knowledge — even though Knowledge
/// itself accepts [1,50], the clamp protects the OpenAI Realtime cost/latency envelope and
/// keeps the model honest (AD-23 INV-VR-019 silent clamp + WARN log).
///
/// Returns a compact list of (source_type, question, answer, score) suitable for
/// function_call_output rendering. Raw Knowledge response shape (chunks vs FAQs, DurationMs,
/// Method) is intentionally NOT surfaced — model only needs the answer text.
///
/// Chunk B exposes the typed client; Chunk C wires it into ToolExecutor.
/// </summary>
public sealed class KnowledgeSearchClient
{
    private const string HttpClientName = "Knowledge";
    public const int TopKMin = 1;
    public const int TopKMax = 10;
    public const int TopKDefault = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpFactory;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;

    public KnowledgeSearchClient(IHttpClientFactory httpFactory, JwtGenerator jwtGenerator, JsonLinesLogger logger)
    {
        _httpFactory = httpFactory;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Search the tenant's knowledge base. top_k null/out-of-range silently clamped to [1,10]
    /// (default 3) with INV-VR-019 WARN log — never throws on input validation.
    /// Throws KnowledgeSearchException on HTTP/JSON/auth failure (caller maps to INV-VR-017
    /// and returns a structured error JSON to the model so it can verbalize "bilgi bankasina
    /// ulasamadim").
    /// </summary>
    public async Task<KnowledgeSearchResult> SearchAsync(int tenantId, string query, int? topK, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeKnowledgeToolFailed, "query bos olamaz.");

        var clampedTopK = ClampTopK(topK);

        var http = _httpFactory.CreateClient(HttpClientName);

        string token;
        try
        {
            token = _jwtGenerator.GenerateServiceToken(tenantId);
        }
        catch (ArgumentException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeServiceJwtMintFailed}] [KnowledgeSearchClient] service token mint argument invalid tenant={tenantId}: {ex.Message}");
            throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeServiceJwtMintFailed, "Yetki anahtari uretilemedi (gecersiz claim).", ex);
        }
        catch (SecurityTokenException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeServiceJwtMintFailed}] [KnowledgeSearchClient] service token mint signing failed tenant={tenantId}: {ex.Message}");
            throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeServiceJwtMintFailed, "Yetki anahtari uretilemedi (imzalama hatasi).", ex);
        }

        var requestBody = new SearchRequestBody
        {
            Query = query,
            TopK = clampedTopK,
            Lang = "tr"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/knowledge/{tenantId}/search")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeKnowledgeToolFailed}] [KnowledgeSearchClient] HTTP error tenant={tenantId}: {ex.Message}");
            throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeKnowledgeToolFailed, "Knowledge servis cagrisi basarisiz.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeKnowledgeToolFailed}] [KnowledgeSearchClient] HTTP timeout tenant={tenantId}: {ex.Message}");
            throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeKnowledgeToolFailed, "Knowledge servis zaman asimi.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeKnowledgeToolFailed}] [KnowledgeSearchClient] Knowledge rejected service token tenant={tenantId}: {(int)response.StatusCode}");
                throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeKnowledgeToolFailed, "Knowledge servis yetki reddi.");
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeKnowledgeToolFailed}] [KnowledgeSearchClient] Knowledge {(int)response.StatusCode} tenant={tenantId}");
                throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeKnowledgeToolFailed, "Knowledge servis hata dondu.");
            }

            KnowledgeWireResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<KnowledgeWireResponse>(JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.SystemError($"[{ErrorCodes.VoiceRuntimeKnowledgeToolFailed}] [KnowledgeSearchClient] JSON parse failed: {ex.Message}");
                throw new KnowledgeSearchException(ErrorCodes.VoiceRuntimeKnowledgeToolFailed, "Knowledge servis cevap formati gecersiz.", ex);
            }

            var compactResults = new List<KnowledgeSearchHit>();
            if (body?.Results != null)
            {
                foreach (var r in body.Results)
                {
                    compactResults.Add(new KnowledgeSearchHit(
                        SourceType: r.SourceType ?? "unknown",
                        Question: r.Question,
                        Answer: r.SourceType == "chunk" ? r.Content : r.Answer,
                        Score: r.Score
                    ));
                }
            }

            return new KnowledgeSearchResult(
                Results: compactResults,
                Method: body?.Method ?? "unknown",
                DurationMs: body?.DurationMs ?? 0,
                ClampedTopK: clampedTopK
            );
        }
    }

    private int ClampTopK(int? topK)
    {
        if (topK == null)
            return TopKDefault;

        var v = topK.Value;
        if (v < TopKMin)
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeToolTopKClamped}] [KnowledgeSearchClient] top_k={v} below {TopKMin}, clamped to default {TopKDefault}");
            return TopKDefault;
        }
        if (v > TopKMax)
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeToolTopKClamped}] [KnowledgeSearchClient] top_k={v} above {TopKMax}, clamped to {TopKMax}");
            return TopKMax;
        }
        return v;
    }

    private sealed class SearchRequestBody
    {
        [JsonPropertyName("query")] public string? Query { get; init; }
        [JsonPropertyName("topK")] public int? TopK { get; init; }
        [JsonPropertyName("lang")] public string? Lang { get; init; }
    }

    // Explicit JsonPropertyName attributes pin the wire contract — Knowledge serializes camelCase
    // (ASP.NET Core defaults: SearchResponse / SearchResultDto in src/Invekto.Knowledge). The
    // attributes remove any ambiguity if Knowledge ever flips its naming policy and document the
    // exact field names for code review.
    private sealed class KnowledgeWireResponse
    {
        [JsonPropertyName("results")] public List<KnowledgeWireResult>? Results { get; init; }
        [JsonPropertyName("method")] public string? Method { get; init; }
        [JsonPropertyName("reason")] public string? Reason { get; init; }
        [JsonPropertyName("durationMs")] public int DurationMs { get; init; }
    }

    private sealed class KnowledgeWireResult
    {
        [JsonPropertyName("sourceType")] public string? SourceType { get; init; }
        [JsonPropertyName("score")] public double Score { get; init; }
        [JsonPropertyName("question")] public string? Question { get; init; }
        [JsonPropertyName("answer")] public string? Answer { get; init; }
        [JsonPropertyName("content")] public string? Content { get; init; }
    }
}

/// <summary>Compact result row exposed to the model (function_call_output payload).</summary>
public sealed record KnowledgeSearchHit(
    string SourceType,    // "faq" | "chunk" | "unknown"
    string? Question,     // null for chunk hits
    string? Answer,       // FAQ.answer OR chunk.content (unified field for the model)
    double Score
);

/// <summary>Aggregate KnowledgeSearchClient response.</summary>
public sealed record KnowledgeSearchResult(
    IReadOnlyList<KnowledgeSearchHit> Results,
    string Method,        // "semantic" | "keyword"
    int DurationMs,
    int ClampedTopK
);

/// <summary>
/// Thrown on Knowledge HTTP/JSON/auth failure. Caller (ToolExecutor in Chunk C) catches and
/// returns a structured error JSON via function_call_output so the model can speak a Turkish
/// fallback ("bilgi bankasina ulasamadim") without terminating the WS session.
/// </summary>
public sealed class KnowledgeSearchException : Exception
{
    public string ErrorCode { get; }

    public KnowledgeSearchException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
