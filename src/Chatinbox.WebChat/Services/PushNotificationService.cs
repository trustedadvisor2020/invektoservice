using System.Text;
using System.Text.Json;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Chatinbox.WebChat.Data;
using Npgsql;

namespace Chatinbox.WebChat.Services;

/// <summary>
/// Sends push notifications to operator's Android app via Expo Push API.
/// </summary>
public sealed class PushNotificationService
{
    private readonly HttpClient _http;
    private readonly WebChatRepository _repo;
    private readonly JsonLinesLogger _logger;
    private const string ExpoPushUrl = "https://exp.host/--/api/v2/push/send";

    public PushNotificationService(HttpClient http, WebChatRepository repo, JsonLinesLogger logger)
    {
        _http = http;
        _repo = repo;
        _logger = logger;
    }

    public async Task NotifyNewMessageAsync(long conversationId, string visitorName, string messagePreview, CancellationToken ct = default)
    {
        var tokens = await _repo.GetPushTokensAsync(ct);
        if (tokens.Count == 0) return;

        var title = string.IsNullOrEmpty(visitorName) ? "Yeni mesaj" : $"{visitorName} yazdı";
        var body = messagePreview.Length > 100 ? messagePreview[..100] + "..." : messagePreview;

        var messages = tokens.Select(token => new
        {
            to = token,
            sound = "default",
            title,
            body,
            data = new { conversationId }
        }).ToArray();

        try
        {
            var json = JsonSerializer.Serialize(messages);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(ExpoPushUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.SystemError($"Expo Push API error ({response.StatusCode}): {responseBody}");
                return;
            }

            // Parse response to remove invalid tokens
            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
            {
                var idx = 0;
                foreach (var item in dataArr.EnumerateArray())
                {
                    if (idx < tokens.Count &&
                        item.TryGetProperty("status", out var status) &&
                        status.GetString() == "error" &&
                        item.TryGetProperty("details", out var details) &&
                        details.TryGetProperty("error", out var errorCode) &&
                        errorCode.GetString() == "DeviceNotRegistered")
                    {
                        await _repo.DeletePushTokenAsync(tokens[idx], ct);
                        _logger.SystemInfo($"Removed invalid push token: {tokens[idx][..20]}...");
                    }
                    idx++;
                }
            }

            _logger.SystemInfo($"Push notification sent to {tokens.Count} device(s) for conv {conversationId}");
        }
        catch (TaskCanceledException)
        {
            // No caller token is wired (invoked via Task.Run with ct=default), so the only
            // reachable cancellation is the HttpClient transport timeout — log it as such.
            _logger.SystemError($"[{ErrorCodes.WebChatPushNotificationFailed}] Expo push timed out for conv {conversationId}");
        }
        catch (HttpRequestException ex)
        {
            _logger.SystemError($"[{ErrorCodes.WebChatPushNotificationFailed}] Expo push HTTP failure: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.SystemError($"[{ErrorCodes.WebChatPushNotificationFailed}] Expo push response parse failure: {ex.Message}");
        }
        catch (NpgsqlException ex)
        {
            _logger.SystemError($"[{ErrorCodes.DatabaseConnectionFailed}] Invalid push token cleanup failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Sanctioned fire-and-forget boundary (see arch/codex-context.md): invoked via Task.Run,
            // no caller to surface to; log full type+detail so ops can triage.
            _logger.SystemError($"[{ErrorCodes.WebChatPushNotificationFailed}] Push notification unexpected ({ex.GetType().Name}): {ex}");
        }
    }
}