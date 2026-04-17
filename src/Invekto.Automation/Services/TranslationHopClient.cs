using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// HFM-2: thin HTTP client for POST {backend}/api/v1/translate used by AiFaqHandler
/// to translate a matched FAQ answer into the lead's preferred locale.
///
/// Graceful degradation: ANY failure (network, timeout, 5xx, parse) returns null so the
/// caller falls back to the original answer text. All failures log INV-AT-064.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class TranslationHopClient
{
    private readonly HttpClient _httpClient;
    private readonly JwtGenerator _jwtGenerator;
    private readonly JsonLinesLogger _logger;
    private readonly string _backendBaseUrl;

    private const int TimeoutMs = 5000;

    public TranslationHopClient(HttpClient httpClient, JwtGenerator jwtGenerator, JsonLinesLogger logger, string backendBaseUrl)
    {
        _httpClient = httpClient;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
        _backendBaseUrl = backendBaseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Translate <paramref name="text"/> into <paramref name="targetLocale"/>.
    /// Returns the translated string on success, null on any failure.
    /// </summary>
    public async Task<string?> TranslateAsync(int tenantId, string text, string targetLocale, string requestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetLocale))
            return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeoutMs);

        try
        {
            var url = $"{_backendBaseUrl}/api/v1/translate";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new TranslateHopRequest
                {
                    Message = text,
                    TargetLanguage = targetLocale
                })
            };
            request.Headers.Add("Authorization", $"Bearer {_jwtGenerator.GenerateServiceToken(tenantId)}");

            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.StepInfo(
                    $"[{ErrorCodes.AutomationFaqTranslationFailed}] translate HTTP {(int)response.StatusCode} tenant={tenantId} target={targetLocale}",
                    requestId);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<TranslateHopResponse>(cancellationToken: cts.Token);
            var translated = body?.TranslatedMessage;
            return string.IsNullOrEmpty(translated) ? null : translated;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // caller-side cancel, propagate
        }
        catch (TaskCanceledException)
        {
            _logger.StepInfo(
                $"[{ErrorCodes.AutomationFaqTranslationFailed}] translate timeout tenant={tenantId} target={targetLocale}",
                requestId);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.StepInfo(
                $"[{ErrorCodes.AutomationFaqTranslationFailed}] translate http error tenant={tenantId} target={targetLocale}: {ex.Message}",
                requestId);
            return null;
        }
        catch (JsonException ex)
        {
            _logger.StepInfo(
                $"[{ErrorCodes.AutomationFaqTranslationFailed}] translate invalid json tenant={tenantId} target={targetLocale}: {ex.Message}",
                requestId);
            return null;
        }
    }

    private sealed class TranslateHopRequest
    {
        [JsonPropertyName("message")]
        public required string Message { get; init; }

        [JsonPropertyName("targetLanguage")]
        public required string TargetLanguage { get; init; }
    }

    private sealed class TranslateHopResponse
    {
        [JsonPropertyName("translatedMessage")]
        public string? TranslatedMessage { get; init; }
    }
}
