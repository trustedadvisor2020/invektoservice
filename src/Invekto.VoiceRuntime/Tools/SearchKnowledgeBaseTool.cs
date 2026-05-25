using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Invekto.VoiceRuntime.Clients;

namespace Invekto.VoiceRuntime.Tools;

/// <summary>
/// FEAT-VFB F0.5 Chunk C (AD-23, AD-30): the only function tool in F0.5 — `search_knowledge_base`.
/// Wraps KnowledgeSearchClient and produces a compact JSON payload for the model.
///
/// Result shape (sent verbatim to the model via function_call_output):
///   {
///     "results": [ { "source_type": "faq"|"chunk", "question": "...", "answer": "...", "score": 0.83 }, ... ],
///     "method": "semantic"|"keyword",
///     "duration_ms": 420,
///     "clamped_top_k": 3,
///     "message": "..."   // present only when results is empty (AD-30 Türkçe fallback hint)
///   }
///
/// Error shape (status=error, OutputJson):
///   { "error": "kb_unavailable"|"unknown_args"|"unknown_tool", "code": "INV-VR-xxx", "message": "..." }
/// Model uses the error message to deliver a natural Turkish fallback ("şu an bilgi bankama
/// ulaşamıyorum"). WS session is NOT terminated — a tool failure is conversation-recoverable.
/// </summary>
public sealed class SearchKnowledgeBaseTool : IVoiceTool
{
    public const string ToolName = "search_knowledge_base";

    // Description English (AD-23): non-translated to keep model intent alignment stable across locales.
    private const string ToolDescription =
        "Search the active tenant's knowledge base (FAQs + documents) for the user's question. " +
        "Use whenever the customer asks about specific information you do not already know with " +
        "certainty (e.g. pricing, working hours, procedure details, appointment policy, address). " +
        "Returns top results with scores; if results is empty, follow the included Turkish guidance.";

    private static readonly JsonElement ParametersSchema = BuildParametersSchema();

    private static readonly JsonSerializerOptions OutputJsonOpts = new(JsonSerializerDefaults.Web)
    {
        // Keep payload terse for the model — null fields suppressed so empty values do not
        // distract the LLM from the populated ones.
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // AD-30: when KB returns 0 hits, ship a Türkçe fallback hint inline with the empty results
    // so the model is steered toward "bu konuda bilgim yok" rather than hallucinating from training.
    private const string EmptyResultsMessage =
        "Bu konuda bilgi bankamda kayıt yok. Lütfen müşteriye 'bu konuda spesifik bilgim yok, " +
        "ilgili uzmanla görüştüreyim' deyin — telefon, fiyat veya randevu detayını uydurmayın.";

    private readonly KnowledgeSearchClient _client;
    private readonly JsonLinesLogger _logger;

    public SearchKnowledgeBaseTool(KnowledgeSearchClient client, JsonLinesLogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public string Name => ToolName;
    public string Description => ToolDescription;
    public JsonElement Parameters => ParametersSchema;

    public async Task<ToolExecutionResult> ExecuteAsync(int tenantId, string argumentsJson, CancellationToken ct)
    {
        // Defensive: model may emit empty string when it hits ambiguity; treat as bad input.
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return ErrorResult("unknown_args", ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed,
                "Arac argumanlari bos geldi; lutfen tekrar deneyin.");
        }

        string query;
        int? topK;
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ErrorResult("unknown_args", ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed,
                    "Arac argumanlari JSON nesnesi olmali.");
            }
            query = doc.RootElement.TryGetProperty("query", out var qEl) && qEl.ValueKind == JsonValueKind.String
                ? qEl.GetString() ?? string.Empty
                : string.Empty;

            // TryGetInt32 returns false for decimals (e.g. 3.5), out-of-Int32-range, and parser
            // edge cases — avoids the FormatException/OverflowException paths from GetInt32().
            // Non-integer / out-of-range JSON numbers are silently forwarded as null so ClampTopK
            // applies the default 3 (CQ1/CQ12: no uncaught parser exception escapes here).
            topK = doc.RootElement.TryGetProperty("top_k", out var tEl)
                && tEl.ValueKind == JsonValueKind.Number
                && tEl.TryGetInt32(out var topKValue)
                ? topKValue
                : null;
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed}] [SearchKnowledgeBaseTool] args parse failed tenant={tenantId}: {ex.Message}");
            return ErrorResult("unknown_args", ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed,
                "Arac argumanlari JSON formatinda degil.");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult("unknown_args", ErrorCodes.VoiceRuntimeFunctionCallDispatchFailed,
                "query alani zorunlu ve bos olamaz.");
        }

        try
        {
            var result = await _client.SearchAsync(tenantId, query, topK, ct);

            var resultsOut = new List<object>(result.Results.Count);
            foreach (var hit in result.Results)
            {
                resultsOut.Add(new
                {
                    source_type = hit.SourceType,
                    question = hit.Question,
                    answer = hit.Answer,
                    score = Math.Round(hit.Score, 4)
                });
            }

            // AD-30: empty results → embed Türkçe fallback message so the model speaks the policy
            // text instead of hallucinating from training data. Non-empty results omit the field
            // (null suppressed by serializer) to keep the payload lean.
            var payload = new
            {
                results = resultsOut,
                method = result.Method,
                duration_ms = result.DurationMs,
                clamped_top_k = result.ClampedTopK,
                message = resultsOut.Count == 0 ? EmptyResultsMessage : null
            };

            var json = JsonSerializer.Serialize(payload, OutputJsonOpts);
            return new ToolExecutionResult(
                OutputJson: json,
                ResultCount: resultsOut.Count,
                Status: "ok",
                ErrorCode: null);
        }
        catch (KnowledgeSearchException ex)
        {
            // KnowledgeSearchClient already logged via SystemError; map to structured error JSON so
            // the model speaks a fallback rather than going silent. WS session remains open.
            return ErrorResult("kb_unavailable", ex.ErrorCode,
                "Bilgi bankasina su an ulasilamiyor; musteriye 'kisaca bilgi alip donebilir miyim' diyerek nazikce yonlendir.");
        }
        catch (OperationCanceledException)
        {
            // Two flavors: outer session CT (browser disconnect) — propagate, executor exits cleanly.
            //              per-call 5sn timeout CT — caller (ToolExecutor) maps to INV-VR-017 timeout
            //              via the structured kb_unavailable JSON, same fallback path.
            throw;
        }
    }

    private static ToolExecutionResult ErrorResult(string error, string code, string message)
    {
        var payload = new { error, code, message };
        var json = JsonSerializer.Serialize(payload, OutputJsonOpts);
        return new ToolExecutionResult(json, 0, "error", code);
    }

    private static JsonElement BuildParametersSchema()
    {
        // OpenAI JSON Schema subset: object + properties + required. additionalProperties=false
        // tightens the contract so the model does not invent extra fields.
        const string schemaJson = """
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "The customer question in natural Turkish (e.g. 'saç ekimi sonrası ne kadar dinlenmek gerekir?'). Do NOT translate to English."
                },
                "top_k": {
                  "type": "integer",
                  "minimum": 1,
                  "maximum": 10,
                  "description": "How many results to return (default 3). Server-side clamped to [1,10]."
                }
              },
              "required": ["query"],
              "additionalProperties": false
            }
            """;
        using var doc = JsonDocument.Parse(schemaJson);
        return doc.RootElement.Clone();
    }
}
