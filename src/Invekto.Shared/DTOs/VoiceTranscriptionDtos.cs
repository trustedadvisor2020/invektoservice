namespace Invekto.Shared.DTOs;

/// <summary>
/// Response from VoiceAI transcription endpoint.
/// Includes Whisper transcript + optional ChatAnalysis intent result.
/// </summary>
public sealed class VoiceTranscriptionResponse
{
    public string Transcript { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double DurationSeconds { get; set; }
    public int WordCount { get; set; }
    public string? IntentLabel { get; set; }
    public double? IntentConfidence { get; set; }
    public string? IntentSummary { get; set; }
}

/// <summary>
/// Whisper API raw response (subset of OpenAI response).
/// </summary>
public sealed class WhisperApiResponse
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double Duration { get; set; }
}
