using System.Linq;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Chatinbox.VoiceRuntime.Clients;

namespace Chatinbox.VoiceRuntime.Tools;

/// <summary>
/// FEAT-VFB F0.5 Chunk C (AD-23, AD-30): the only function tool in F0.5 — `search_knowledge_base`.
/// Wraps KnowledgeSearchClient and produces a compact JSON payload for the model.
///
/// F-VR-E (spec §5): confidence banding. The top SEMANTIC score is mapped to a band
/// (high ≥ 0.80 / medium 0.55–0.79 / low &lt; 0.55, all config-driven via KbConfidenceOptions).
/// Hits below the medium floor are mechanically DROPPED from the payload so the model can never
/// read an off-topic chunk aloud (the prior "alakasız sonucu okuma" bug). A low band drops ALL
/// hits and ships a Türkçe steer to take contact / hand off instead of inventing; a medium band
/// keeps the surviving hits but adds a caution steer. Banding is skipped for keyword-fallback
/// results (different score scale — see KbConfidenceOptions).
///
/// Result shape (sent verbatim to the model via function_call_output):
///   {
///     "results": [ { "source_type": "faq"|"chunk", "question": "...", "answer": "...", "score": 0.83 }, ... ],
///     "method": "semantic"|"keyword",
///     "duration_ms": 420,
///     "clamped_top_k": 3,
///     "confidence": "high"|"medium"|"low",  // present only for semantic results (F-VR-E band)
///     "guidance": "..."  // present for medium/low band (Türkçe behavioural steer)
///     "message": "..."   // present only when the ORIGINAL result set is empty (AD-30 Türkçe hint)
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

    // F-VR-E: low band — best match too weak to be the answer (all hits dropped). Steer to take
    // contact / hand off rather than reading an off-topic chunk or inventing specifics.
    private const string LowConfidenceGuidance =
        "Elimdeki kayitlar bu soruyla yeterince ortusmuyor; dogru bilgi veremem. Spesifik bir sey " +
        "(fiyat, tarih, adres, prosedur) UYDURMA ve alakasiz bir konuyu anlatma. Musteriye 'bu konuda " +
        "size kesin bilgi vermem dogru olmaz, dogrusunu iletmem icin bilgilerinizi alip ekiple kontrol " +
        "edeyim' tarzinda yaklas; iletisim bilgisi al ya da ilgili ekibe yonlendir.";

    // F-VR-E: medium band — partial match. Help in general terms but do not commit to specifics.
    private const string MediumConfidenceGuidance =
        "Elimdeki bilgi bu konuya kismen uyuyor ama tam emin degilim. Genel cercevede yardimci ol; " +
        "ancak kesin rakam, kesin tarih veya taahhut verme. Emin olmadigin spesifik detayi 'bunu " +
        "netlestirip size donelim' diyerek dogrula, gerekirse iletisim bilgisi alip ekibe yonlendir. Uydurma.";

    private readonly KnowledgeSearchClient _client;
    private readonly KbConfidenceOptions _confidence;
    private readonly JsonLinesLogger _logger;

    public SearchKnowledgeBaseTool(KnowledgeSearchClient client, KbConfidenceOptions confidence, JsonLinesLogger logger)
    {
        _client = client;
        _confidence = confidence;
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

            // F-VR-E confidence banding (spec §5). Applies to SEMANTIC scores only — keyword-fallback
            // scores use a different scale, so they pass through untouched (see KbConfidenceOptions).
            var originalHits = result.Results;
            IReadOnlyList<KnowledgeSearchHit> effectiveHits = originalHits;
            string? confidenceBand = null;
            string? guidance = null;

            if (originalHits.Count > 0 &&
                string.Equals(result.Method, "semantic", StringComparison.OrdinalIgnoreCase))
            {
                var topScore = originalHits.Max(h => h.Score);
                confidenceBand = _confidence.BandFor(topScore);

                if (confidenceBand == "low")
                {
                    // Best match too weak — drop every hit so the model can never read an off-topic
                    // chunk aloud; the guidance steers it to take contact / hand off instead.
                    effectiveHits = Array.Empty<KnowledgeSearchHit>();
                    guidance = LowConfidenceGuidance;
                }
                else
                {
                    // Strong-enough top hit, but the result set may still mix in weak rows. Drop the
                    // sub-floor hits so only relevant rows reach the model; add a caution steer when medium.
                    effectiveHits = originalHits.Where(h => h.Score >= _confidence.MediumThreshold).ToList();
                    if (confidenceBand == "medium")
                        guidance = MediumConfidenceGuidance;
                }

                _logger.SystemInfo(
                    $"[SearchKnowledgeBaseTool] confidence tenant={tenantId} method={result.Method} " +
                    $"top_score={Math.Round(topScore, 4)} band={confidenceBand} kept={effectiveHits.Count}/{originalHits.Count}");
            }

            var resultsOut = new List<object>(effectiveHits.Count);
            foreach (var hit in effectiveHits)
            {
                resultsOut.Add(new
                {
                    source_type = hit.SourceType,
                    question = hit.Question,
                    answer = hit.Answer,
                    score = Math.Round(hit.Score, 4)
                });
            }

            // AD-30: ORIGINAL empty results → embed Türkçe fallback message so the model speaks the
            // policy text instead of hallucinating. (A low-band drop leaves resultsOut empty too, but
            // that path carries `guidance` instead — `message` stays reserved for a genuinely empty KB.)
            var payload = new
            {
                results = resultsOut,
                method = result.Method,
                duration_ms = result.DurationMs,
                clamped_top_k = result.ClampedTopK,
                confidence = confidenceBand,
                guidance,
                message = originalHits.Count == 0 ? EmptyResultsMessage : null
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
