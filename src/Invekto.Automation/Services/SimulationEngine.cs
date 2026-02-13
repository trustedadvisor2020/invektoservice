using System.Collections.Concurrent;
using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// In-memory simulation engine for testing flows without side-effects.
/// - ConcurrentDictionary for sessions (no DB writes)
/// - 30-minute TTL per session, timer-based cleanup every 5 minutes
/// - Reuses FlowEngineV2 (IMP-8 pure engine) — no callbacks, no message sending
/// - Thread-safe, register as singleton + IHostedService for cleanup timer
/// </summary>
public sealed class SimulationEngine : IHostedService, IDisposable
{
    private readonly ConcurrentDictionary<string, SimulationSession> _sessions = new(StringComparer.Ordinal);
    private readonly FlowEngineV2 _engine;
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;
    private Timer? _cleanupTimer;

    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    public SimulationEngine(FlowEngineV2 engine, AutomationRepository repo, JsonLinesLogger logger)
    {
        _engine = engine;
        _repo = repo;
        _logger = logger;
    }

    // ============================================================
    // IHostedService — cleanup timer lifecycle
    // ============================================================

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, CleanupInterval, CleanupInterval);
        _logger.SystemInfo("SimulationEngine cleanup timer started (interval: 5min, TTL: 30min)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer?.Change(Timeout.Infinite, 0);
        _logger.SystemInfo($"SimulationEngine stopping. Active sessions: {_sessions.Count}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    // ============================================================
    // Public API
    // ============================================================

    /// <summary>
    /// Start a new simulation session. Loads flow from DB, builds graph, executes from trigger_start.
    /// Returns initial messages and session state.
    /// </summary>
    public async Task<SimulationStartResult> StartAsync(int tenantId, int flowId, CancellationToken ct)
    {
        // Load flow from DB
        var flow = await _repo.GetFlowByIdAsync(tenantId, flowId, ct);
        if (flow == null)
        {
            return SimulationStartResult.Error(
                ErrorCodes.AutomationSimulationFlowNotFound,
                "Simulasyon icin akis bulunamadi");
        }

        // Build immutable graph
        var graph = FlowGraphV2.Build(flow.FlowConfigJson);
        if (graph == null)
        {
            return SimulationStartResult.Error(
                ErrorCodes.AutomationInvalidFlowConfig,
                "Akis konfigurasyonu gecersiz (v2 graph olusturulamadi)");
        }

        if (graph.TriggerStart == null)
        {
            return SimulationStartResult.Error(
                ErrorCodes.AutomationGraphValidationFailed,
                "Akis baslangic node'u (trigger_start) bulunamadi");
        }

        // Create session
        var sessionId = Guid.NewGuid().ToString("N");
        var state = new SessionStateV2
        {
            CurrentNodeId = graph.TriggerStart.Id,
            Status = "active"
        };

        var session = new SimulationSession
        {
            SessionId = sessionId,
            TenantId = tenantId,
            FlowId = flowId,
            Graph = graph,
            State = state,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(SessionTtl)
        };

        if (!_sessions.TryAdd(sessionId, session))
        {
            _logger.SystemWarn($"SimulationEngine: session ID collision {sessionId}");
            return SimulationStartResult.Error(
                ErrorCodes.GeneralUnknown,
                "Oturum olusturulamadi, tekrar deneyin");
        }

        // Execute from trigger_start (auto-chain until WaitForInput or terminal)
        var result = await _engine.ExecuteAsync(graph, state, ct);

        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.Add(SessionTtl);

        _logger.StepInfo($"Simulation started: session={sessionId}, tenant={tenantId}, flow={flowId}, " +
            $"messages={result.Messages.Count}, currentNode={state.CurrentNodeId}, status={state.Status}", sessionId);

        return new SimulationStartResult
        {
            Success = true,
            SessionId = sessionId,
            Messages = result.Messages.Select(m => new SimulationMessage("bot", m)).ToList(),
            CurrentNodeId = state.CurrentNodeId,
            Variables = new Dictionary<string, string>(state.Variables),
            ExecutionPath = new List<string>(state.ExecutionPath),
            Status = state.Status,
            PendingInput = state.PendingInput != null
                ? new SimulationPendingInput
                {
                    Type = state.PendingInput.Type,
                    Options = state.PendingInput.Options
                }
                : null
        };
    }

    /// <summary>
    /// Process user input in an existing simulation session.
    /// Sets __last_input variable, executes engine step.
    /// </summary>
    public async Task<SimulationStepResult> StepAsync(string sessionId, string userMessage, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return SimulationStepResult.Error(
                ErrorCodes.AutomationSimulationSessionNotFound,
                "Simulasyon oturumu bulunamadi");
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            return SimulationStepResult.Error(
                ErrorCodes.AutomationSimulationSessionExpired,
                "Simulasyon oturumunun suresi doldu");
        }

        // Check if session is still active
        if (session.State.Status != "active")
        {
            return SimulationStepResult.Error(
                ErrorCodes.AutomationNoPendingInput,
                $"Simulasyon oturumu aktif degil (status: {session.State.Status})");
        }

        // Set user input variable
        session.State.Variables["__last_input"] = userMessage;

        // Execute engine step
        var result = await _engine.ExecuteAsync(session.Graph, session.State, ct);

        session.LastActivityAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.Add(SessionTtl);

        // Build response messages: user message + bot messages
        var messages = new List<SimulationMessage>
        {
            new("user", userMessage)
        };
        messages.AddRange(result.Messages.Select(m => new SimulationMessage("bot", m)));

        // Add error message if engine produced an error
        if (result.ErrorMessage != null)
        {
            messages.Add(new SimulationMessage("system", $"[{result.ErrorCode}] {result.ErrorMessage}"));
        }

        _logger.StepInfo($"Simulation step: session={sessionId}, input='{Truncate(userMessage, 50)}', " +
            $"botMessages={result.Messages.Count}, currentNode={session.State.CurrentNodeId}, status={session.State.Status}", sessionId);

        return new SimulationStepResult
        {
            Success = true,
            Messages = messages,
            CurrentNodeId = session.State.CurrentNodeId,
            Variables = new Dictionary<string, string>(session.State.Variables),
            ExecutionPath = new List<string>(session.State.ExecutionPath),
            Status = session.State.Status,
            IsTerminal = result.IsTerminal,
            PendingInput = session.State.PendingInput != null
                ? new SimulationPendingInput
                {
                    Type = session.State.PendingInput.Type,
                    Options = session.State.PendingInput.Options
                }
                : null
        };
    }

    /// <summary>
    /// Get the tenant ID for a simulation session (for tenant isolation checks).
    /// Returns null if session not found.
    /// </summary>
    public int? GetSessionTenantId(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session.TenantId : null;
    }

    /// <summary>
    /// Remove a simulation session (manual cleanup).
    /// </summary>
    public bool Remove(string sessionId)
    {
        var removed = _sessions.TryRemove(sessionId, out _);
        if (removed)
            _logger.StepInfo($"Simulation session removed: {sessionId}", sessionId);
        return removed;
    }

    // ============================================================
    // Cleanup timer callback
    // ============================================================

    private void CleanupExpiredSessions(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredCount = 0;

        foreach (var kvp in _sessions)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                if (_sessions.TryRemove(kvp.Key, out _))
                    expiredCount++;
            }
        }

        if (expiredCount > 0)
            _logger.SystemInfo($"SimulationEngine cleanup: removed {expiredCount} expired sessions. Active: {_sessions.Count}");
    }

    private static string Truncate(string s, int maxLen)
    {
        return s.Length <= maxLen ? s : s[..maxLen] + "...";
    }
}

// ============================================================
// Session and result DTOs
// ============================================================

internal sealed class SimulationSession
{
    public required string SessionId { get; init; }
    public required int TenantId { get; init; }
    public required int FlowId { get; init; }
    public required FlowGraphV2 Graph { get; init; }
    public required SessionStateV2 State { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime LastActivityAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class SimulationMessage
{
    public string Role { get; init; } // "bot", "user", "system"
    public string Text { get; init; }

    public SimulationMessage(string role, string text)
    {
        Role = role;
        Text = text;
    }
}

public sealed class SimulationPendingInput
{
    public required string Type { get; init; }
    public List<string>? Options { get; init; }
}

public sealed class SimulationStartResult
{
    public bool Success { get; init; }
    public string? SessionId { get; init; }
    public List<SimulationMessage> Messages { get; init; } = new();
    public string? CurrentNodeId { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
    public List<string> ExecutionPath { get; init; } = new();
    public string? Status { get; init; }
    public SimulationPendingInput? PendingInput { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static SimulationStartResult Error(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}

public sealed class SimulationStepResult
{
    public bool Success { get; init; }
    public List<SimulationMessage> Messages { get; init; } = new();
    public string? CurrentNodeId { get; init; }
    public Dictionary<string, string> Variables { get; init; } = new();
    public List<string> ExecutionPath { get; init; } = new();
    public string? Status { get; init; }
    public bool IsTerminal { get; init; }
    public SimulationPendingInput? PendingInput { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static SimulationStepResult Error(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
