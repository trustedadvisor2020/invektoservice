using System.Text.Json;

namespace Chatinbox.Automation.Services;

/// <summary>
/// Backend graph validation for v2 flows.
/// Checks: orphan nodes, dead-ends, required fields, loop detection, edge consistency.
/// Endpoint: POST /api/v1/flows/validate
/// </summary>
public sealed class FlowValidator
{
    private static readonly HashSet<string> TerminalTypes = new(StringComparer.Ordinal)
    {
        "action_handoff", "action_assign_group"
    };

    private static readonly HashSet<string> NoOutputTypes = new(StringComparer.Ordinal)
    {
        "action_handoff", "action_assign_group", "utility_note"
    };

    private static readonly HashSet<string> WaitTypes = new(StringComparer.Ordinal)
    {
        "message_menu", "ai_intent", "ai_faq", "ai_sentiment"
    };

    private static readonly Dictionary<string, string[]> RequiredFields = new(StringComparer.Ordinal)
    {
        ["trigger_start"] = new[] { "label" },
        ["message_text"] = new[] { "label", "text" },
        ["message_menu"] = new[] { "label", "text", "options" },
        ["action_handoff"] = new[] { "label" },
        ["utility_note"] = new[] { "label", "text" },
        ["logic_condition"] = new[] { "label", "variable", "operator" },
        ["logic_switch"] = new[] { "label", "variable", "cases", "default_handle_id" },
        ["ai_intent"] = new[] { "label" },
        ["ai_faq"] = new[] { "label" },
        ["action_api_call"] = new[] { "label", "method", "url" },
        ["action_delay"] = new[] { "label", "seconds" },
        ["action_wait_until"] = new[] { "label" }, // duration field validated at runtime (one of wait_until_iso/wait_days/wait_hours/wait_minutes/wait_seconds)
        ["utility_set_variable"] = new[] { "label", "variable_name", "value_expression" },
        ["ai_sentiment"] = new[] { "label" },
        ["webhook_trigger"] = new[] { "label" },
        ["outbound_trigger"] = new[] { "label" },
        ["schedule_trigger"] = new[] { "label", "cron_expression" },
        ["customer_status_changed"] = new[] { "label" }, // FEAT-INMA-PIPELINE-V2 C3a — feature_group_id optional (empty = match any group)
        ["action_call_flow"] = new[] { "label", "flow_id" },
        ["action_set_customer_status"] = new[] { "label", "feature_group_id" } // FEAT-INMA-PIPELINE-V2 C4 — feature_ids optional (empty = clear group); not terminal, has success/error handles
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

        // 1. Must have exactly one trigger node (any type in TriggerTypes)
        var triggerNodes = graph.AllNodes.Where(n => FlowGraphV2.TriggerTypes.Contains(n.Type)).ToList();
        if (triggerNodes.Count == 0)
            errors.Add("Trigger node bulunamadi — her akis bir baslangic noktasi olmali");
        else if (triggerNodes.Count > 1)
            errors.Add($"Birden fazla trigger node var ({triggerNodes.Count}) — sadece 1 olmali");

        // 2. Orphan detection (no incoming edges, not a trigger type)
        foreach (var node in graph.AllNodes)
        {
            if (FlowGraphV2.TriggerTypes.Contains(node.Type)) continue;

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

        // 4b. message_menu: options must not be empty array
        foreach (var node in graph.AllNodes.Where(n => n.Type == "message_menu"))
        {
            var optionsJson = node.GetData("options");
            if (string.IsNullOrEmpty(optionsJson) || optionsJson == "[]")
            {
                errors.Add($"Menu secenekleri bos — node '{node.GetData("label", node.Id)}' ({node.Id}): en az 1 secenek ekleyin veya node tipini 'message_text' olarak degistirin");
            }
        }

        // 4c. logic_condition: value is required unless operator is "is_empty"
        foreach (var node in graph.AllNodes.Where(n => n.Type == "logic_condition"))
        {
            var op = node.GetData("operator");
            if (!string.Equals(op, "is_empty", StringComparison.OrdinalIgnoreCase))
            {
                var val = node.GetData("value");
                if (string.IsNullOrWhiteSpace(val))
                    errors.Add($"Zorunlu alan eksik, node '{node.GetData("label", node.Id)}' ({node.Id}): value");
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
                    if (string.IsNullOrEmpty(handleId))
                    {
                        var optLabel = opt.TryGetProperty("label", out var lbl) ? lbl.GetString() : "?";
                        warnings.Add($"Menu secenegi '{optLabel}' handle_id eksik — node '{node.GetData("label", node.Id)}' ({node.Id})");
                        continue;
                    }

                    // Check if there's an edge from this menu with this handle
                    var edges = graph.GetOutgoingEdges(node.Id, handleId);
                    if (edges.Count == 0)
                    {
                        var optLabel = opt.TryGetProperty("label", out var l) ? l.GetString() : handleId;
                        warnings.Add($"Menu secenegi '{optLabel}' (handle: {handleId}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                    }
                }
            }
            catch (JsonException)
            {
                warnings.Add($"Menu secenekleri JSON parse hatasi — node '{node.GetData("label", node.Id)}' ({node.Id})");
            }
        }

        // 7. Logic condition handle consistency (true_handle / false_handle)
        foreach (var node in graph.AllNodes.Where(n => n.Type == "logic_condition"))
        {
            foreach (var handle in new[] { "true_handle", "false_handle" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "true_handle" ? "DOGRU" : "YANLIS";
                    warnings.Add($"Kosul dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 8. Logic switch case handle consistency
        foreach (var node in graph.AllNodes.Where(n => n.Type == "logic_switch"))
        {
            var casesJson = node.GetData("cases");
            if (string.IsNullOrEmpty(casesJson)) continue;

            try
            {
                using var doc = JsonDocument.Parse(casesJson);
                foreach (var c in doc.RootElement.EnumerateArray())
                {
                    var handleId = c.TryGetProperty("handle_id", out var h) ? h.GetString() : null;
                    if (string.IsNullOrEmpty(handleId))
                    {
                        var caseVal = c.TryGetProperty("value", out var cv2) ? cv2.GetString() : "?";
                        warnings.Add($"Switch case '{caseVal}' handle_id eksik — node '{node.GetData("label", node.Id)}' ({node.Id})");
                        continue;
                    }

                    var edges = graph.GetOutgoingEdges(node.Id, handleId);
                    if (edges.Count == 0)
                    {
                        var caseLabel = c.TryGetProperty("value", out var cv) ? cv.GetString() : handleId;
                        warnings.Add($"Switch case '{caseLabel}' (handle: {handleId}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                    }
                }

                // Check default handle
                var defaultHandle = node.GetData("default_handle_id", "default");
                var defaultEdges = graph.GetOutgoingEdges(node.Id, defaultHandle);
                if (defaultEdges.Count == 0)
                    warnings.Add($"Switch varsayilan dal (handle: {defaultHandle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
            }
            catch (JsonException)
            {
                warnings.Add($"Switch cases JSON parse hatasi — node '{node.GetData("label", node.Id)}' ({node.Id})");
            }
        }

        // 9. ai_intent handle consistency (high_confidence / low_confidence)
        foreach (var node in graph.AllNodes.Where(n => n.Type == "ai_intent"))
        {
            foreach (var handle in new[] { "high_confidence", "low_confidence" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "high_confidence" ? "Yuksek Guven" : "Dusuk Guven";
                    warnings.Add($"Intent dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 10. ai_faq handle consistency (matched / no_match)
        foreach (var node in graph.AllNodes.Where(n => n.Type == "ai_faq"))
        {
            foreach (var handle in new[] { "matched", "no_match" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "matched" ? "Eslesti" : "Eslesmedi";
                    warnings.Add($"FAQ dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 10b. ai_sentiment handle consistency (positive / negative)
        foreach (var node in graph.AllNodes.Where(n => n.Type == "ai_sentiment"))
        {
            foreach (var handle in new[] { "positive", "negative" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "positive" ? "Pozitif" : "Negatif";
                    warnings.Add($"Sentiment dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 11. action_api_call handle consistency (success / error)
        foreach (var node in graph.AllNodes.Where(n => n.Type == "action_api_call"))
        {
            foreach (var handle in new[] { "success", "error" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "success" ? "Basarili" : "Hata";
                    warnings.Add($"API dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 11c. action_set_customer_status handle consistency (success / error) — FEAT-INMA-PIPELINE-V2 C4
        foreach (var node in graph.AllNodes.Where(n => n.Type == "action_set_customer_status"))
        {
            foreach (var handle in new[] { "success", "error" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "success" ? "Basarili" : "Hata";
                    warnings.Add($"Durum atama dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 11b. action_call_flow handle consistency (completed / error)
        foreach (var node in graph.AllNodes.Where(n => n.Type == "action_call_flow"))
        {
            foreach (var handle in new[] { "completed", "error" })
            {
                var edges = graph.GetOutgoingEdges(node.Id, handle);
                if (edges.Count == 0)
                {
                    var handleLabel = handle == "completed" ? "Tamamlandi" : "Hata";
                    warnings.Add($"Alt flow dali '{handleLabel}' ({handle}) baglantisiz — node '{node.GetData("label", node.Id)}' ({node.Id})");
                }
            }
        }

        // 12. Simple loop detection (DFS cycle check from trigger_start)
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
    /// Calculate a 0-100 health score for a flow config.
    /// Scoring: 100 base, -15/error (max -60), -5/warning (max -30), floor 0.
    /// Returns null issues list if config is unparseable.
    /// </summary>
    public FlowHealthScore CalculateHealthScore(string flowConfigJson)
    {
        var result = Validate(flowConfigJson);

        var errorPenalty = Math.Min(result.Errors.Count * 15, 60);
        var warningPenalty = Math.Min(result.Warnings.Count * 5, 30);
        var score = Math.Max(100 - errorPenalty - warningPenalty, 0);

        // Combine errors + warnings for issues list (errors first)
        var issues = new List<string>(result.Errors.Count + result.Warnings.Count);
        issues.AddRange(result.Errors);
        issues.AddRange(result.Warnings);

        return new FlowHealthScore
        {
            Score = score,
            Issues = issues,
            ErrorCount = result.Errors.Count,
            WarningCount = result.Warnings.Count
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

public sealed class FlowHealthScore
{
    public int Score { get; init; }
    public required List<string> Issues { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
}
