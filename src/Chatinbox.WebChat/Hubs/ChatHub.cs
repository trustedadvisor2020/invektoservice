using Chatinbox.Shared.Logging;
using Chatinbox.WebChat.Services;
using Microsoft.AspNetCore.SignalR;

namespace Chatinbox.WebChat.Hubs;

/// <summary>
/// SignalR hub for real-time webchat communication.
/// Visitors and operators join conversation groups to exchange messages.
/// </summary>
public sealed class ChatHub : Hub
{
    private readonly OperatorPresence _presence;
    private readonly JsonLinesLogger _logger;

    public ChatHub(OperatorPresence presence, JsonLinesLogger logger)
    {
        _presence = presence;
        _logger = logger;
    }

    /// <summary>
    /// Join a conversation group to receive real-time messages.
    /// </summary>
    public async Task JoinConversation(long conversationId)
    {
        var groupName = $"conv_{conversationId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.SystemInfo($"Connection {Context.ConnectionId} joined {groupName}");
    }

    /// <summary>
    /// Leave a conversation group.
    /// </summary>
    public async Task LeaveConversation(long conversationId)
    {
        var groupName = $"conv_{conversationId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Broadcast typing indicator to conversation group.
    /// </summary>
    public async Task Typing(long conversationId, string senderType)
    {
        var groupName = $"conv_{conversationId}";
        await Clients.OthersInGroup(groupName).SendAsync("UserTyping", new
        {
            conversationId,
            senderType
        });
    }

    /// <summary>
    /// Operator sets online/offline status.
    /// Broadcasts to all connected clients.
    /// </summary>
    public async Task SetOperatorOnline(bool isOnline)
    {
        if (isOnline)
        {
            _presence.AddConnection(Context.ConnectionId);
        }
        else
        {
            _presence.RemoveConnection(Context.ConnectionId);
        }

        await Clients.All.SendAsync("OperatorStatusChanged", _presence.IsOnline);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.SystemInfo($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Remove operator presence if this was an operator connection
        _presence.RemoveConnection(Context.ConnectionId);

        // Broadcast updated operator status
        await Clients.All.SendAsync("OperatorStatusChanged", _presence.IsOnline);

        _logger.SystemInfo($"Client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}
