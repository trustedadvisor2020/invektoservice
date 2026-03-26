using System.Text;
using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.VoiceAI.Services;

/// <summary>
/// Orchestrates: receive audio → Whisper STT → ChatAnalysis intent → DB log → cleanup.
/// </summary>
public sealed class VoiceTranscriptionService
{
    private readonly WhisperApiService _whisper;
    private readonly HttpClient _chatAnalysisClient;
    private readonly string _chatAnalysisInternalApiKey;
    private readonly string _pgConnStr;
    private readonly JsonLinesLogger _logger;

    public VoiceTranscriptionService(
        WhisperApiService whisper,
        HttpClient chatAnalysisClient,
        string chatAnalysisInternalApiKey,
        string pgConnStr,
        JsonLinesLogger logger)
    {
        _whisper = whisper;
        _chatAnalysisClient = chatAnalysisClient;
        _chatAnalysisInternalApiKey = chatAnalysisInternalApiKey;
        _pgConnStr = pgConnStr;
        _logger = logger;
    }

    /// <summary>
    /// Full pipeline: transcribe → intent → log → response.
    /// Temp file is cleaned up in finally block.
    /// </summary>
    public async Task<VoiceTranscriptionResponse> ProcessAsync(
        Stream audioStream, string fileName, int tenantId, string requestId, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"voiceai_{requestId}_{fileName}");
        try
        {
            // Save to temp file (Whisper needs seekable stream for multipart)
            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await audioStream.CopyToAsync(fs, ct);
            }

            // 1. Transcribe via Whisper
            WhisperApiResponse whisperResult;
            await using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
            {
                whisperResult = await _whisper.TranscribeAsync(fs, fileName, ct);
            }

            var wordCount = whisperResult.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            _logger.StepInfo(
                $"Transcribed: tenant={tenantId}, lang={whisperResult.Language}, duration={whisperResult.Duration:F1}s, words={wordCount}",
                requestId);

            // 2. Forward to ChatAnalysis for intent detection (non-blocking on failure)
            string? intentLabel = null;
            double? intentConfidence = null;
            string? intentSummary = null;

            try
            {
                var intentResult = await ForwardToChatAnalysisAsync(
                    whisperResult.Text, tenantId, requestId, ct);

                if (intentResult != null)
                {
                    intentLabel = intentResult.Value.label;
                    intentConfidence = intentResult.Value.confidence;
                    intentSummary = intentResult.Value.summary;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.VoiceAIIntentForwardFailed}] ChatAnalysis intent forward failed: {ex.Message}");
            }
            catch (TaskCanceledException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.VoiceAIIntentForwardFailed}] ChatAnalysis intent forward timed out: {ex.Message}");
            }

            // 3. Log transcription to DB (non-fatal)
            try
            {
                await LogTranscriptionAsync(tenantId, requestId, fileName,
                    whisperResult.Language, whisperResult.Duration, wordCount,
                    whisperResult.Text, intentLabel, ct);
            }
            catch (NpgsqlException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.VoiceAITranscriptionLogFailed}] Transcription log insert failed: {ex.Message}");
            }

            return new VoiceTranscriptionResponse
            {
                Transcript = whisperResult.Text,
                Language = whisperResult.Language,
                DurationSeconds = whisperResult.Duration,
                WordCount = wordCount,
                IntentLabel = intentLabel,
                IntentConfidence = intentConfidence,
                IntentSummary = intentSummary
            };
        }
        finally
        {
            // Always clean up temp audio file
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException ex)
            {
                _logger.SystemWarn($"[{ErrorCodes.VoiceAITranscriptionFailed}] Temp file cleanup failed: {ex.Message}");
            }
        }
    }

    private async Task<(string label, double confidence, string summary)?> ForwardToChatAnalysisAsync(
        string transcript, int tenantId, string requestId, CancellationToken ct)
    {
        var payload = new
        {
            RequestID = requestId,
            TenantId = tenantId,
            Messages = new[]
            {
                new { Role = "customer", Content = transcript, Timestamp = DateTime.UtcNow.ToString("o") }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/analyze")
        {
            Content = content
        };

        if (!string.IsNullOrEmpty(_chatAnalysisInternalApiKey))
            request.Headers.Add("X-Internal-Api-Key", _chatAnalysisInternalApiKey);

        using var response = await _chatAnalysisClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.SystemWarn($"[{ErrorCodes.VoiceAIIntentForwardFailed}] ChatAnalysis returned {(int)response.StatusCode}");
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        // ChatAnalysis returns async (accepted=202) — intent may not be immediately available
        // For MVP, return what we can parse from the response
        if (doc.RootElement.TryGetProperty("intent", out var intent))
        {
            var label = intent.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
            var confidence = intent.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0;
            var summary = intent.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            return (label, confidence, summary);
        }

        return null;
    }

    private async Task LogTranscriptionAsync(
        int tenantId, string requestId, string fileName,
        string language, double durationSeconds, int wordCount,
        string transcript, string? intentLabel, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_pgConnStr);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO voice_transcriptions
                (tenant_id, request_id, file_name, language, duration_seconds, word_count, transcript, intent_label, created_at)
            VALUES
                (@tenant_id, @request_id, @file_name, @language, @duration_seconds, @word_count, @transcript, @intent_label, NOW())
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("request_id", requestId);
        cmd.Parameters.AddWithValue("file_name", fileName);
        cmd.Parameters.AddWithValue("language", language);
        cmd.Parameters.AddWithValue("duration_seconds", durationSeconds);
        cmd.Parameters.AddWithValue("word_count", wordCount);
        cmd.Parameters.AddWithValue("transcript", transcript);
        cmd.Parameters.AddWithValue("intent_label", (object?)intentLabel ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
