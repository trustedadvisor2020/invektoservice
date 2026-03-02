using System.Collections.Concurrent;
using Invekto.Shared.Logging;
using Invekto.WebChat.Data;
using Invekto.WebChat.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Invekto.WebChat.Services;

/// <summary>
/// Manages chat conversations: create, send message, close.
/// Handles AI auto-reply timer (fires after configurable delay when operator unavailable).
/// </summary>
public sealed class ConversationService
{
    private readonly WebChatRepository _repo;
    private readonly AIReplyService _aiReply;
    private readonly OperatorPresence _presence;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly PushNotificationService _pushService;
    private readonly AutomationWebhookClient _webhookClient;
    private readonly JsonLinesLogger _logger;
    private readonly int _aiDelaySeconds;

    // Timer tracking per conversation for AI auto-reply
    private readonly ConcurrentDictionary<long, Timer> _aiTimers = new();

    // Widget flow config cache (5 min TTL)
    private readonly ConcurrentDictionary<string, (WidgetFlowConfig Config, DateTime CachedAt)> _widgetCache = new();
    private const int WidgetCacheTtlSeconds = 300;

    public ConversationService(
        WebChatRepository repo,
        AIReplyService aiReply,
        OperatorPresence presence,
        IHubContext<ChatHub> hubContext,
        PushNotificationService pushService,
        AutomationWebhookClient webhookClient,
        JsonLinesLogger logger,
        int aiDelaySeconds)
    {
        _repo = repo;
        _aiReply = aiReply;
        _presence = presence;
        _hubContext = hubContext;
        _pushService = pushService;
        _webhookClient = webhookClient;
        _logger = logger;
        _aiDelaySeconds = aiDelaySeconds;
    }

    public async Task<long> StartConversationAsync(
        string visitorId, string? name, string? email,
        string? pageUrl, string? userAgent, string? widgetId = null, CancellationToken ct = default)
    {
        // Upsert visitor
        await _repo.UpsertVisitorAsync(visitorId, name, email, pageUrl, userAgent, ct);

        // Create conversation
        var conversationId = await _repo.CreateConversationAsync(visitorId, widgetId, ct);

        _logger.SystemInfo($"Conversation {conversationId} started for visitor {visitorId}");

        // Fire-and-forget: notify Automation webhook
        if (!string.IsNullOrEmpty(widgetId))
        {
            _ = FireWebhookForWidgetAsync(widgetId, wc =>
                _webhookClient.NotifyConversationCreatedAsync(
                    wc.TenantId, wc.FlowConversationCreated,
                    conversationId, visitorId, name, email, pageUrl, ct));
        }

        return conversationId;
    }

    public async Task<MessageRow> SendVisitorMessageAsync(
        long conversationId, string visitorId, string content, CancellationToken ct = default)
    {
        var conversation = await _repo.GetConversationAsync(conversationId, ct);
        if (conversation == null)
            throw new InvalidOperationException("Conversation not found");
        if (conversation.Status == "closed")
            throw new InvalidOperationException("Conversation is closed");
        if (conversation.VisitorId != visitorId)
            throw new UnauthorizedAccessException("Visitor mismatch");

        var msgId = await _repo.InsertMessageAsync(conversationId, "visitor", content, ct);
        await _repo.UpdateLastMessageAtAsync(conversationId, ct);

        var message = new MessageRow
        {
            Id = msgId,
            ConversationId = conversationId,
            SenderType = "visitor",
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        // Broadcast to conversation group
        await _hubContext.Clients.Group($"conv_{conversationId}")
            .SendAsync("ReceiveMessage", message, ct);

        // Send push notification to operator
        var visitorName = conversation.VisitorName ?? "Ziyaretçi";
        _ = _pushService.NotifyNewMessageAsync(conversationId, visitorName, content, ct);

        // Fire-and-forget: notify Automation webhook
        if (!string.IsNullOrEmpty(conversation.WidgetId))
        {
            _ = FireWebhookForWidgetAsync(conversation.WidgetId, wc =>
                _webhookClient.NotifyVisitorMessageAsync(
                    wc.TenantId, wc.FlowVisitorMessage,
                    conversationId, visitorId, content, ct));
        }

        // Start AI timer if operator not online
        if (!_presence.IsOnline)
        {
            StartAITimer(conversationId);
        }

        return message;
    }

    public async Task<MessageRow> SendOperatorMessageAsync(
        long conversationId, string content, CancellationToken ct = default)
    {
        var conversation = await _repo.GetConversationAsync(conversationId, ct);
        if (conversation == null)
            throw new InvalidOperationException("Conversation not found");
        if (conversation.Status == "closed")
            throw new InvalidOperationException("Conversation is closed");

        // Cancel AI timer since operator is responding
        CancelAITimer(conversationId);

        // Switch from AI to active if needed
        if (conversation.Status == "ai")
        {
            await _repo.UpdateConversationStatusAsync(conversationId, "active", ct);
        }

        var msgId = await _repo.InsertMessageAsync(conversationId, "operator", content, ct);
        await _repo.UpdateLastMessageAtAsync(conversationId, ct);

        var message = new MessageRow
        {
            Id = msgId,
            ConversationId = conversationId,
            SenderType = "operator",
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        // Broadcast to conversation group
        await _hubContext.Clients.Group($"conv_{conversationId}")
            .SendAsync("ReceiveMessage", message, ct);

        return message;
    }

    public async Task<bool> CloseConversationAsync(long conversationId, CancellationToken ct = default)
    {
        CancelAITimer(conversationId);
        var (closed, widgetId) = await _repo.CloseConversationAsync(conversationId, ct);
        if (closed)
        {
            await _hubContext.Clients.Group($"conv_{conversationId}")
                .SendAsync("ConversationClosed", conversationId, ct);
            _logger.SystemInfo($"Conversation {conversationId} closed");

            // Fire-and-forget: notify Automation webhook
            if (!string.IsNullOrEmpty(widgetId))
            {
                _ = FireWebhookForWidgetAsync(widgetId, wc =>
                    _webhookClient.NotifyConversationClosedAsync(
                        wc.TenantId, wc.FlowConversationClosed,
                        conversationId, ct));
            }
        }
        return closed;
    }

    // ── Widget Config Cache ──

    private async Task FireWebhookForWidgetAsync(string widgetId, Func<WidgetFlowConfig, Task> action)
    {
        try
        {
            var wc = await GetCachedWidgetConfigAsync(widgetId);
            if (wc != null)
                await action(wc);
        }
        catch (Exception ex)
        {
            _logger.SystemError($"Webhook fire failed for widget {widgetId}: {ex.Message}");
        }
    }

    private async Task<WidgetFlowConfig?> GetCachedWidgetConfigAsync(string widgetId, CancellationToken ct = default)
    {
        if (_widgetCache.TryGetValue(widgetId, out var cached) &&
            (DateTime.UtcNow - cached.CachedAt).TotalSeconds < WidgetCacheTtlSeconds)
        {
            return cached.Config;
        }

        var config = await _repo.GetWidgetConfigAsync(widgetId, ct);
        if (config != null)
            _widgetCache[widgetId] = (config, DateTime.UtcNow);
        else
            _widgetCache.TryRemove(widgetId, out _);

        return config;
    }

    // ── AI Auto-Reply Timer ──

    private void StartAITimer(long conversationId)
    {
        CancelAITimer(conversationId); // Cancel existing timer if any

        var timer = new Timer(
            async _ => await TriggerAIReplyAsync(conversationId),
            null,
            TimeSpan.FromSeconds(_aiDelaySeconds),
            Timeout.InfiniteTimeSpan);

        _aiTimers[conversationId] = timer;
        _logger.SystemInfo($"AI timer started for conv {conversationId} ({_aiDelaySeconds}s)");
    }

    private void CancelAITimer(long conversationId)
    {
        if (_aiTimers.TryRemove(conversationId, out var timer))
        {
            timer.Dispose();
            _logger.SystemInfo($"AI timer cancelled for conv {conversationId}");
        }
    }

    private async Task TriggerAIReplyAsync(long conversationId)
    {
        try
        {
            // Check if operator came online in the meantime
            if (_presence.IsOnline)
            {
                _logger.SystemInfo($"AI reply skipped for conv {conversationId} - operator online");
                return;
            }

            var conversation = await _repo.GetConversationAsync(conversationId);
            if (conversation == null || conversation.Status == "closed") return;

            // Get recent messages for context
            var messages = await _repo.GetMessagesAsync(conversationId, 20);
            var lastVisitorMsg = messages.LastOrDefault(m => m.SenderType == "visitor");
            if (lastVisitorMsg == null) return;

            // Build history for AI
            var history = messages
                .Select(m => (m.SenderType == "visitor" ? "user" : "assistant", m.Content))
                .ToList();

            // Remove the last user message from history (it's the prompt)
            if (history.Count > 0) history.RemoveAt(history.Count - 1);

            // Generate AI reply
            var aiReply = await _aiReply.GenerateReplyAsync(lastVisitorMsg.Content, history);
            if (string.IsNullOrEmpty(aiReply)) return;

            // Save and broadcast
            var msgId = await _repo.InsertMessageAsync(conversationId, "ai", aiReply);
            await _repo.UpdateLastMessageAtAsync(conversationId);
            await _repo.UpdateConversationStatusAsync(conversationId, "ai");

            var message = new MessageRow
            {
                Id = msgId,
                ConversationId = conversationId,
                SenderType = "ai",
                Content = aiReply,
                CreatedAt = DateTime.UtcNow
            };

            await _hubContext.Clients.Group($"conv_{conversationId}")
                .SendAsync("ReceiveMessage", message);

            _logger.SystemInfo($"AI reply sent for conv {conversationId}");
        }
        catch (Exception ex)
        {
            _logger.SystemError($"AI reply failed for conv {conversationId}: {ex.Message}");
        }
        finally
        {
            _aiTimers.TryRemove(conversationId, out _);
        }
    }
}
