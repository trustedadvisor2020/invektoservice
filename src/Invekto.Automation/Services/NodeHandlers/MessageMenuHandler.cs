using System.Text.Json;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// Show menu and wait for user selection.
/// On first visit: sends menu text, returns WaitForInput.
/// On user input: resolves selected option handle, returns Continue.
/// </summary>
public sealed class MessageMenuHandler : INodeHandler
{
    public string NodeType => "message_menu";

    public Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Check if we have pending input for this node (user already selected)
        if (ctx.State.PendingInput != null && ctx.State.PendingInput.NodeId == node.Id)
        {
            // User has responded — resolve the selected option
            var userInput = ctx.State.Variables.TryGetValue("__last_input", out var li) ? li : "";
            var options = ParseOptions(node);

            // Find matching option by key (case-insensitive)
            var selectedOption = options.FirstOrDefault(
                o => o.Key.Equals(userInput.Trim(), StringComparison.OrdinalIgnoreCase));

            if (selectedOption == null)
            {
                // Unknown input — re-show menu
                var unknownMsg = ctx.Graph.Settings.UnknownInputMessage
                    ?? "Anlayamadim. Lutfen gecerli bir secenek girin.";
                var menuText = FormatMenu(node, ctx, options);

                return Task.FromResult(new NodeResult
                {
                    MessageText = unknownMsg + "\n\n" + menuText,
                    Action = NodeAction.WaitForInput,
                    PendingInput = new PendingInput
                    {
                        Type = "menu",
                        Options = options.Select(o => o.Key).ToList()
                    }
                });
            }

            // Valid selection — set variable and continue via the selected handle
            var updates = new Dictionary<string, string>
            {
                ["selected_option"] = selectedOption.Key,
                ["selected_label"] = selectedOption.Label
            };

            return Task.FromResult(new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = selectedOption.HandleId,
                VariableUpdates = updates
            });
        }

        // First visit — show menu, wait for input
        {
            var options = ParseOptions(node);
            var menuText = FormatMenu(node, ctx, options);

            return Task.FromResult(new NodeResult
            {
                MessageText = menuText,
                Action = NodeAction.WaitForInput,
                PendingInput = new PendingInput
                {
                    Type = "menu",
                    Options = options.Select(o => o.Key).ToList()
                }
            });
        }
    }

    private string FormatMenu(FlowNodeV2 node, ExecutionContext ctx, List<MenuOptionV2> options)
    {
        var headerText = node.GetData("text");
        var header = ctx.Evaluator.Substitute(headerText, ctx.State.Variables);

        var lines = new List<string> { header };
        foreach (var opt in options)
        {
            lines.Add($"{opt.Key}. {opt.Label}");
        }
        return string.Join("\n", lines);
    }

    private static List<MenuOptionV2> ParseOptions(FlowNodeV2 node)
    {
        var result = new List<MenuOptionV2>();
        var rawJson = node.GetData("options");
        if (string.IsNullOrEmpty(rawJson))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            foreach (var opt in doc.RootElement.EnumerateArray())
            {
                var key = opt.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                var label = opt.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                var handleId = opt.TryGetProperty("handle_id", out var h) ? h.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(key))
                    result.Add(new MenuOptionV2 { Key = key, Label = label, HandleId = handleId });
            }
        }
        catch (JsonException)
        {
            // Invalid options JSON — return empty list.
            // FlowValidator catches this at design time (required field check on "options").
        }

        return result;
    }
}

internal sealed class MenuOptionV2
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string HandleId { get; init; }
}
