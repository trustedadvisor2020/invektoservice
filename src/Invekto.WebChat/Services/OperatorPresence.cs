using System.Collections.Concurrent;
using Invekto.Shared.Logging;

namespace Invekto.WebChat.Services;

/// <summary>
/// In-memory operator online/offline state.
/// Tracks SignalR connection IDs of connected operators.
/// </summary>
public sealed class OperatorPresence
{
    private readonly ConcurrentDictionary<string, DateTime> _connections = new();
    private readonly JsonLinesLogger _logger;

    public OperatorPresence(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    public bool IsOnline => !_connections.IsEmpty;

    public void AddConnection(string connectionId)
    {
        _connections[connectionId] = DateTime.UtcNow;
        _logger.SystemInfo($"Operator connected: {connectionId}, total={_connections.Count}");
    }

    public void RemoveConnection(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
        _logger.SystemInfo($"Operator disconnected: {connectionId}, total={_connections.Count}");
    }

    public int ConnectionCount => _connections.Count;
}
