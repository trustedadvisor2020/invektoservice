using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Menu-based chatbot flow engine.
/// Manages conversation state: welcome -> menu -> action selection.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class FlowEngine
{
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;

    private const string DefaultWelcome = "Hosgeldiniz! Size nasil yardimci olabilirim?";
    private const string DefaultUnknownInput = "Anlayamadim. Lutfen menueden bir secenek belirleyin veya '0' yazarak ana menuye donun.";
    private const double DefaultHandoffThreshold = 0.5;

    public FlowEngine(AutomationRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Get the parsed flow config for a tenant. Returns null if not found or inactive.
    /// </summary>
    public async Task<FlowConfig?> GetActiveFlowAsync(int tenantId, CancellationToken ct = default)
    {
        var (flowDoc, isActive) = await _repo.GetFlowAsync(tenantId, ct);
        if (flowDoc == null || !isActive)
            return null;

        try
        {
            return ParseFlowConfig(flowDoc);
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"Failed to parse flow config for tenant {tenantId}: {ex.Message}");
            return null;
        }
        finally
        {
            flowDoc.Dispose();
        }
    }

    /// <summary>
    /// Process user input against the current flow state.
    /// Returns the flow action to take.
    /// </summary>
    public FlowAction ProcessInput(FlowConfig flow, ChatSession? session, string userInput)
    {
        // Reset command: '0' returns to menu
        if (userInput.Trim() == "0")
        {
            return new FlowAction
            {
                Type = FlowActionType.ShowMenu,
                ReplyText = FormatMenu(flow),
                NextNode = "menu"
            };
        }

        // No session or welcome state -> show welcome + menu
        if (session == null || session.CurrentNode == "welcome")
        {
            return new FlowAction
            {
                Type = FlowActionType.ShowWelcome,
                ReplyText = flow.WelcomeMessage + "\n\n" + FormatMenu(flow),
                NextNode = "menu"
            };
        }

        // In menu state -> process option selection
        if (session.CurrentNode == "menu")
        {
            var selectedOption = flow.MenuOptions.FirstOrDefault(
                o => o.Key.Equals(userInput.Trim(), StringComparison.OrdinalIgnoreCase));

            if (selectedOption == null)
            {
                return new FlowAction
                {
                    Type = FlowActionType.UnknownInput,
                    ReplyText = flow.UnknownInputMessage + "\n\n" + FormatMenu(flow),
                    NextNode = "menu"
                };
            }

            return selectedOption.Action switch
            {
                "reply" => new FlowAction
                {
                    Type = FlowActionType.StaticReply,
                    ReplyText = selectedOption.ReplyText ?? "...",
                    NextNode = "menu"
                },
                "faq" => new FlowAction
                {
                    Type = FlowActionType.FaqSearch,
                    NextNode = "faq"
                },
                "handoff" => new FlowAction
                {
                    Type = FlowActionType.Handoff,
                    NextNode = "handed_off"
                },
                "intent" => new FlowAction
                {
                    Type = FlowActionType.IntentDetection,
                    NextNode = "intent"
                },
                _ => new FlowAction
                {
                    Type = FlowActionType.UnknownInput,
                    ReplyText = flow.UnknownInputMessage,
                    NextNode = "menu"
                }
            };
        }

        // In faq node -> run FAQ search on free text
        if (session.CurrentNode == "faq")
        {
            return new FlowAction
            {
                Type = FlowActionType.FaqSearch,
                NextNode = "faq"
            };
        }

        // In intent node -> run intent detection on free text
        if (session.CurrentNode == "intent")
        {
            return new FlowAction
            {
                Type = FlowActionType.IntentDetection,
                NextNode = "intent"
            };
        }

        // Fallback: show menu
        return new FlowAction
        {
            Type = FlowActionType.ShowMenu,
            ReplyText = FormatMenu(flow),
            NextNode = "menu"
        };
    }

    private string FormatMenu(FlowConfig flow)
    {
        var lines = new List<string> { flow.MenuText };
        foreach (var opt in flow.MenuOptions)
        {
            lines.Add($"{opt.Key}. {opt.Label}");
        }
        return string.Join("\n", lines);
    }

    private static FlowConfig ParseFlowConfig(JsonDocument doc)
    {
        var root = doc.RootElement;

        var menuOptions = new List<MenuOption>();
        if (root.TryGetProperty("menu", out var menu) && menu.TryGetProperty("options", out var opts))
        {
            foreach (var opt in opts.EnumerateArray())
            {
                menuOptions.Add(new MenuOption
                {
                    Key = opt.GetProperty("key").GetString()!,
                    Label = opt.GetProperty("label").GetString()!,
                    Action = opt.GetProperty("action").GetString()!,
                    ReplyText = opt.TryGetProperty("reply_text", out var rt) ? rt.GetString() : null
                });
            }
        }

        var menuText = "";
        if (root.TryGetProperty("menu", out var menuElement) && menuElement.TryGetProperty("text", out var mt))
            menuText = mt.GetString() ?? "";

        return new FlowConfig
        {
            WelcomeMessage = root.TryGetProperty("welcome_message", out var wm) ? wm.GetString() ?? DefaultWelcome : DefaultWelcome,
            MenuText = menuText,
            MenuOptions = menuOptions,
            OffHoursMessage = root.TryGetProperty("off_hours_message", out var oh) ? oh.GetString() : null,
            UnknownInputMessage = root.TryGetProperty("unknown_input_message", out var ui) ? ui.GetString() ?? DefaultUnknownInput : DefaultUnknownInput,
            HandoffConfidenceThreshold = root.TryGetProperty("handoff_confidence_threshold", out var ht) ? ht.GetDouble() : DefaultHandoffThreshold
        };
    }
}

// ============================================================
// Flow DTOs
// ============================================================

public sealed class FlowConfig
{
    public required string WelcomeMessage { get; init; }
    public required string MenuText { get; init; }
    public required List<MenuOption> MenuOptions { get; init; }
    public string? OffHoursMessage { get; init; }
    public required string UnknownInputMessage { get; init; }
    public double HandoffConfidenceThreshold { get; init; } = 0.5;
}

public sealed class MenuOption
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Action { get; init; }
    public string? ReplyText { get; init; }
}

public sealed class FlowAction
{
    public required FlowActionType Type { get; init; }
    public string? ReplyText { get; init; }
    public required string NextNode { get; init; }
}

public enum FlowActionType
{
    ShowWelcome,
    ShowMenu,
    StaticReply,
    FaqSearch,
    IntentDetection,
    Handoff,
    UnknownInput
}
