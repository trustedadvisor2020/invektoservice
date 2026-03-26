using System.Text.Json;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

namespace Invekto.VoiceAI.Services;

/// <summary>
/// OpenAI Whisper API client for speech-to-text transcription.
/// Platform-level API key (not per-tenant).
/// </summary>
public sealed class WhisperApiService
{
    private readonly HttpClient _httpClient;
    private readonly JsonLinesLogger _logger;
    private readonly string _apiKey;
    private readonly string _model;

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".wav", ".webm", ".ogg", ".flac"
    };

    public WhisperApiService(HttpClient httpClient, string apiKey, string model, JsonLinesLogger logger)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public static bool IsSupportedFormat(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && SupportedFormats.Contains(ext);
    }

    /// <summary>
    /// Transcribe audio file via Whisper API. Returns transcript text, language, and duration.
    /// </summary>
    public async Task<WhisperApiResponse> TranscribeAsync(Stream audioStream, string fileName, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(audioStream);

        content.Add(streamContent, "file", fileName);
        content.Add(new StringContent(_model), "model");
        content.Add(new StringContent("verbose_json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
        {
            Content = content
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.SystemError($"[{ErrorCodes.VoiceAITranscriptionFailed}] Whisper API returned {(int)response.StatusCode}: {errorBody}");
            throw new HttpRequestException($"Whisper API error: {(int)response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new WhisperApiResponse
        {
            Text = root.GetProperty("text").GetString() ?? string.Empty,
            Language = root.TryGetProperty("language", out var lang) ? lang.GetString() ?? "unknown" : "unknown",
            Duration = root.TryGetProperty("duration", out var dur) ? dur.GetDouble() : 0
        };
    }
}
