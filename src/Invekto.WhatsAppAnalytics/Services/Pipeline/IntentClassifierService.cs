using System.Text;
using System.Text.RegularExpressions;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services.Pipeline;

/// <summary>
/// Stage 4: Intent classification for customer messages.
/// Keyword-first + Claude Haiku hybrid. 12 intents, Turkish fashion e-commerce context.
/// Keyword match = confidence 1.0. Claude fallback for unmatched (batch 50).
/// </summary>
public sealed class IntentClassifierService
{
    private readonly AnalyticsRepository _repo;
    private readonly AnalyticsConnectionFactory _db;
    private readonly ClaudeClient _claude;
    private readonly TextNormalizer _normalizer;
    private readonly JsonLinesLogger _logger;

    private const int ClaudeBatchSize = 50;
    private const float MinClaudeConfidence = 0.7f;
    private const int TotalStages = 7;
    private const int StageNumber = 4;

    // 12 intent keyword patterns (transliterated Turkish for ASCII-safe regex)
    private static readonly Dictionary<string, Regex> IntentPatterns = new()
    {
        ["greeting"] = Compile(@"\b(merhaba|selam|iyi gunler|merhabalar|gunaydin)\b"),
        ["price_inquiry"] = Compile(@"\b(fiyat|kac tl|ne kadar|ucret|para)\b|₺"),
        ["size_inquiry"] = Compile(@"\b(beden|numara|kac beden|boy kilo|manken)\b|\b[sS]\s*[mM]\s*[lL]\s*[xX][lL]\b"),
        ["stock_inquiry"] = Compile(@"\b(stok|var mi|kaldi mi|tukendi|bitti mi)\b"),
        ["shipping_inquiry"] = Compile(@"\b(kargo|teslimat|ne zaman gelir|gonderim|aras|yurtici|mng)\b"),
        ["return_request"] = Compile(@"\b(iade|geri gonder|degisim|uymadi|buyuk geldi|kucuk geldi|dar geldi|genis geldi)\b"),
        ["complaint"] = Compile(@"\b(kotu|bozuk|yanlis geldi|memnun degilim|berbat|rezalet|saygisizlik)\b"),
        ["order_confirmation"] = Compile(@"\b(siparis|aliyorum|gonder|onayliyorum|siparisimi olustur)\b"),
        ["thank_you"] = Compile(@"\b(tesekkur|sagol|sagolun|cok iyi|harika|super|mukemmel)\b"),
        ["product_inquiry"] = new Regex(@"\b\d{4}\b", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100)),
        ["discount_inquiry"] = Compile(@"\b(indirim|kampanya|taksit|promosyon|kupon)\b"),
        ["address_info"] = Compile(@"\b(adres|mahalle|sokak|cadde|il\s|ilce|posta kodu)\b"),
    };

    private static readonly string[] IntentNames =
    [
        "greeting", "price_inquiry", "size_inquiry", "stock_inquiry",
        "shipping_inquiry", "return_request", "complaint", "order_confirmation",
        "thank_you", "product_inquiry", "discount_inquiry", "address_info"
    ];

    private static readonly string ClaudeSystemPrompt = @"Sen bir WhatsApp mesaj intent siniflandiricisin. Turkce giyim e-ticaret (moda) sektoru.

Mevcut intent'ler: greeting, price_inquiry, size_inquiry, stock_inquiry, shipping_inquiry, return_request, complaint, order_confirmation, thank_you, product_inquiry, discount_inquiry, address_info

Her mesaj icin EN UYGUN intent'i belirle. Emin degilsen ""unknown"" yaz.

Kurallar:
- Sadece MUSTERI mesajlarini siniflandir
- Kisa selamlasma (merhaba, selam) = greeting
- Tesekkur/memnuniyet = thank_you
- Urun kodu sorma = product_inquiry
- Fiyat sorma = price_inquiry
- Beden/numara sorma = size_inquiry
- Stok sorma = stock_inquiry
- Kargo/teslimat sorma = shipping_inquiry
- Iade/degisim = return_request
- Sikayet/memnuniyetsizlik = complaint
- Siparis onayi/aliyorum = order_confirmation
- Indirim/kampanya = discount_inquiry
- Adres bilgisi = address_info

JSON formatinda cevap ver:
[{""i"": 0, ""intent"": ""greeting"", ""conf"": 0.95}, ...]

Sadece JSON array dondur, baska metin yazma.";

    public IntentClassifierService(
        AnalyticsRepository repo,
        AnalyticsConnectionFactory db,
        ClaudeClient claude,
        TextNormalizer normalizer,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _db = db;
        _claude = claude;
        _normalizer = normalizer;
        _logger = logger;
    }

    /// <summary>
    /// Run intent classification on all CUSTOMER messages for the given analysis.
    /// Reads from wa_messages, writes to wa_intents.
    /// Returns total classified count.
    /// </summary>
    public async Task<int> RunAsync(
        int analysisId, int tenantId,
        Func<StageProgress, Task> onProgress,
        CancellationToken ct)
    {
        _logger.SystemInfo($"[IntentClassifier] Starting stage 4 for analysis {analysisId}");

        // Read customer messages from wa_messages
        var messages = await _repo.GetCustomerMessagesAsync(analysisId, tenantId, ct);
        _logger.SystemInfo($"[IntentClassifier] {messages.Count:N0} customer messages to classify");

        if (messages.Count == 0) return 0;

        var results = new List<IntentResult>();
        var unmatchedForClaude = new List<(int Index, string ConversationId, string Text)>();

        // Phase 1: Keyword matching
        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            var transliterated = _normalizer.TransliterateTurkish(msg.MessageText);

            var matched = false;
            foreach (var (intent, regex) in IntentPatterns)
            {
                try
                {
                    if (regex.IsMatch(transliterated))
                    {
                        results.Add(new IntentResult
                        {
                            ConversationId = msg.ConversationId,
                            MessageText = msg.MessageText,
                            Intent = intent,
                            Confidence = 1.0f,
                            Method = "keyword"
                        });
                        matched = true;
                        break;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    _logger.SystemWarn($"[IntentClassifier] Regex timeout for intent '{intent}' on message {i}");
                }
            }

            if (!matched)
            {
                unmatchedForClaude.Add((results.Count, msg.ConversationId, msg.MessageText));
                // Add placeholder for now
                results.Add(new IntentResult
                {
                    ConversationId = msg.ConversationId,
                    MessageText = msg.MessageText,
                    Intent = "unknown",
                    Confidence = 0.0f,
                    Method = _claude.IsAvailable ? "pending_claude" : "skipped"
                });
            }

            if (i % 10000 == 0 && i > 0)
            {
                var pct = (int)((double)i / messages.Count * 50); // Keyword phase = 0-50%
                await onProgress(new StageProgress("intents", pct, $"Keyword matching: {i:N0}/{messages.Count:N0}", StageNumber, TotalStages));
            }
        }

        var keywordCount = results.Count(r => r.Method == "keyword");
        _logger.SystemInfo($"[IntentClassifier] Keyword matched: {keywordCount:N0}/{messages.Count:N0} ({100.0 * keywordCount / messages.Count:F1}%)");

        // Phase 2: Claude Haiku for unmatched messages
        if (_claude.IsAvailable && unmatchedForClaude.Count > 0)
        {
            _logger.SystemInfo($"[IntentClassifier] Sending {unmatchedForClaude.Count:N0} messages to Claude Haiku");

            for (var batchStart = 0; batchStart < unmatchedForClaude.Count; batchStart += ClaudeBatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batchEnd = Math.Min(batchStart + ClaudeBatchSize, unmatchedForClaude.Count);
                var batch = unmatchedForClaude.GetRange(batchStart, batchEnd - batchStart);

                try
                {
                    var userContent = BuildClaudeBatchContent(batch);
                    var responseText = await _claude.SendBatchAsync(ClaudeSystemPrompt, userContent, ct);
                    var parsed = ClaudeClient.ParseJsonArray<ClaudeIntentItem>(responseText, _logger);

                    foreach (var item in parsed)
                    {
                        if (item.Index < 0 || item.Index >= batch.Count) continue;
                        var (resultIdx, convId, text) = batch[item.Index];

                        var isValid = IntentNames.Contains(item.Intent) || item.Intent == "unknown";
                        var intent = isValid ? item.Intent : "unknown";
                        var method = item.Confidence >= MinClaudeConfidence ? "claude" : "claude_low_conf";

                        results[resultIdx] = new IntentResult
                        {
                            ConversationId = convId,
                            MessageText = text,
                            Intent = intent,
                            Confidence = item.Confidence,
                            Method = method
                        };
                    }
                }
                catch (TaskCanceledException) { throw; }
                catch (ArgumentOutOfRangeException ex)
                {
                    _logger.SystemWarn($"[IntentClassifier] Claude batch {batchStart / ClaudeBatchSize} result mapping error: {ex.Message}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.SystemWarn($"[IntentClassifier] Claude batch {batchStart / ClaudeBatchSize} processing error: {ex.Message}");
                }

                if (batchStart % (ClaudeBatchSize * 10) == 0)
                {
                    var pct = 50 + (int)((double)batchStart / unmatchedForClaude.Count * 40); // Claude phase = 50-90%
                    await onProgress(new StageProgress("intents", pct, $"Claude classification: batch {batchStart / ClaudeBatchSize}", StageNumber, TotalStages));
                }
            }
        }

        // Fix remaining "pending_claude" entries that weren't resolved
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i].Method == "pending_claude")
            {
                results[i] = results[i] with { Method = "skipped", Intent = "unknown" };
            }
        }

        // Phase 3: Batch insert to wa_intents
        await onProgress(new StageProgress("intents", 90, $"Writing {results.Count:N0} intents to DB", StageNumber, TotalStages));
        await _repo.BatchInsertIntentsAsync(analysisId, tenantId, results, ct);

        await onProgress(new StageProgress("intents", 100, $"Complete: {results.Count:N0} intents classified", StageNumber, TotalStages));

        var claudeCount = results.Count(r => r.Method is "claude" or "claude_low_conf");
        var unknownCount = results.Count(r => r.Intent == "unknown");
        _logger.SystemInfo(
            $"[IntentClassifier] Stage 4 complete: keyword={keywordCount}, claude={claudeCount}, unknown={unknownCount}, total={results.Count}");

        return results.Count;
    }

    private static string BuildClaudeBatchContent(List<(int Index, string ConversationId, string Text)> batch)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < batch.Count; i++)
        {
            var text = batch[i].Text.Length > 200 ? batch[i].Text[..200] : batch[i].Text;
            sb.AppendLine($"[{i}] {text}");
        }
        return sb.ToString();
    }

    private static Regex Compile(string pattern) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
}
