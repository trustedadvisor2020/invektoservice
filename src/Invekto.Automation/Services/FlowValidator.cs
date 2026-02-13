using System.Text.Json;

namespace Invekto.Automation.Services;

/// <summary>
/// Backend graph validation for v2 flows.
/// Checks: orphan nodes, dead-ends, required fields, loop detection, edge consistency.
/// Endpoint: POST /api/v1/flows/validate
/// </summary>
public sealed class FlowValidator
{
    private static readonly HashSet<string> TerminalTypes = new(StringComparer.Ordinal)
    {
        "action_handoff"
    };

    private static readonly HashSet<string> NoOutputTypes = new(StringComparer.Ordinal)
    {
        "action_handoff", "utility_note"
    };

    private static readonly HashSet<string> WaitTypes = new(StringComparer.Ordinal)
    {
        "message_menu", "ai_intent", "ai_faq"
    };

    private static readonly Dictionary<string, string[]> RequiredFields = new(StringComparer.Ordinal)
    {
        ["trigger_start"] = new[] { "label" },
        ["message_text"] = new[] { "label", "text" },
        ["message_menu"] = new[] { "label", "text", "options" },
        ["action_handoff"] = new[] { "label" },
        ["utility_note"] = new[] { "label", "text" },
        ["logic_condition"] = new[] { "label", "variable", "operator", "value" },
        ["logic_switch"] = new[] { "label", "variable", "cases", "default_handle_id" },
        ["ai_intent"] = new[] { "label" },
        ["ai_faq"] = new[] { "label" },
        ["action_api_call"] = new[] { "label", "method", "url" },
        ["action_delay"] = new[] { "label", "seconds" },
        ["utility_set_variable"] = new[] { "label", "variable_name", "value_expression" }
    };

    /// <summary>
    /// Validate a v2 flow config. Returns validation result with errors and warnings.
    /// </summary>
    public FlowValidationResult Validate(string flowConfigJson)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Parse graph
        var graph = FlowGraphV2.Build(flowConfigJson);
        if (graph == null)
        {
            errors.Add("Gecersiz v2 flow config: JSON parse veya version hatasi");
            return new FlowValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
        }

        // 1. Must have exactly one trigger_start
        var triggerStarts = graph.AllNodes.Where(n => n.Type == "trigger_start").ToList();
        if (triggerStarts.Count == 0)
            errors.Add("trigger_start node'u bulunamadi — her akis bir baslangic noktasi olmali");
        else if (triggerStarts.Count > 1)
            errors.Add($"Birden fazla trigger_start node'u var ({triggerStarts.Count}) — sadece 1 olmali");

        // 2. Orphan detection (no incoming edges, not trigger_start)
        foreach (var node in graph.AllNodes)
        {
            if (node.Type == "trigger_start") continue;

            if (!graph.HasIncomingEdges(node.Id))
                warnings.Add($"Orphan node: '{node.GetData("label", node.Id)}' ({node.Id}) — bu adima ulasilamiyor");
        }

        // 3. Dead-end detection (no outgoing edges, not terminal/note)
        foreach (var node in graph.AllNodes)
        {
            if (NoOutputTypes.Contains(node.Type)) continue;

            var outgoing = graph.GetOutgoingEdges(node.Id);
            if (outgoing.Count == 0)
                warnings.Add($"Dead-end node: '{node.GetData("label", node.Id)}' ({node.Id}) — bu adimdan sonra akis duruyor");
        }

        // 4. Required field check
        foreach (var node in graph.AllNodes)
        {
            if (!RequiredFields.TryGetValue(node.Type, out var fields))
                continue;

            foreach (var field in fields)
            {
                var value = node.GetData(field);
                if (string.IsNullOrWhiteSpace(value) || value == "[]" || value == "{}")
                    errors.Add($"Zorunlu alan eksik, node '{node.GetData("label", node.Id)}' ({node.Id}): {field}");
            }
        }

        // 5. Edge consistency: source and target nodes must exist
        foreach (var edge in graph.AllEdges)
        {
            if (!graph.NodesById.ContainsKey(edge.Source))
                errors.Add($"Edge '{edge.Id}' kaynak node'u bulunamadi: {edge.Source}");
            if (!graph.NodesById.ContainsKey(edge.Target))
                errors.Add($"Edge '{edge.Id}' hedef node'u bulunamadi: {edge.Target}");
        }

        // 6. Menu option handle consistency
        foreach (var node in graph.AllNodes.Where(n => n.Type == "message_menu"))
        {
            var optionsJson = node.GetData("options");
            if (string.IsNullOrEmpty(optionsJson)) continue;

            try
            {
                using var doc = JsonDocument.Parse(optionsJson);
                foreach (var opt in doc.RootElement.EnumerateArray())
                {
                    var handleId = opt.TryGetProperty("handle_id", out var h) ? h.GetString() : null;
                    if (string.IsNullOrEmpty(handleId)) continue;

                    // Check if there's an edge from this menu with this handle
                    var edges = graph.GetOutgoingEdges(node.Id, handleId);
                    if (edges.Count == 0)
                    {
                        var optLabel = opt.TryGetProperty("label", out var l) ? l.GetString() : handleId;
                        warnings.Add($"Menu secenegi '{optLabel}' (handle: {handleId}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                    }
                }
            }
            catch (JsonException) { /* Invalid options JSON — already caught by required field check (rule 4) */ }
        }

        // 7. Simple loop detection (DFS cycle check from trigger_start)
        if (graph.TriggerStart != null)
        {
            var cycleNodes = DetectCycles(graph);
            foreach (var nodeId in cycleNodes)
            {
                var node = graph.NodesById[nodeId];
                warnings.Add($"Potansiyel sonsuz dongu: '{node.GetData("label", nodeId)}' ({nodeId}) — max_loop_count ({graph.Settings.MaxLoopCount}) ile korunuyor");
            }
        }

        return new FlowValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Detect nodes that are part of cycles using DFS.
    /// Returns set of node IDs that participate in cycles.
    /// </summary>
    private static HashSet<string> DetectCycles(FlowGraphV2 graph)
    {
        var cycleNodes = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var inStack = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in graph.AllNodes)
        {
            if (!visited.Contains(node.Id))
                DfsCycleCheck(graph, node.Id, visited, inStack, cycleNodes);
        }

        return cycleNodes;
    }

    private static void DfsCycleCheck(
        FlowGraphV2 graph, string nodeId,
        HashSet<string> visited, HashSet<string> inStack, HashSet<string> cycleNodes)
    {
        visited.Add(nodeId);
        inStack.Add(nodeId);

        var edges = graph.GetOutgoingEdges(nodeId);
        foreach (var edge in edges)
        {
            if (!visited.Contains(edge.Target))
            {
                DfsCycleCheck(graph, edge.Target, visited, inStack, cycleNodes);
            }
            else if (inStack.Contains(edge.Target))
            {
                cycleNodes.Add(edge.Target);
                cycleNodes.Add(nodeId);
            }
        }

        inStack.Remove(nodeId);
    }
}

public sealed class FlowValidationResult
{
    public bool IsValid { get; init; }
    public required List<string> Errors { get; init; }
    public required List<string> Warnings { get; init; }
}
