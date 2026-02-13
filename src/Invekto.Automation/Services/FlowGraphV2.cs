using System.Text.Json;

namespace Invekto.Automation.Services;

/// <summary>
/// Immutable pre-computed graph built once from flow_config v2 JSON.
/// O(1) node lookup, O(1) outgoing edge lookup by (nodeId, handle).
/// Thread-safe after Build(). IMP-2.
/// </summary>
public sealed class FlowGraphV2
{
    public IReadOnlyDictionary<string, FlowNodeV2> NodesById { get; }
    public IReadOnlyDictionary<string, List<FlowEdgeV2>> OutgoingByNode { get; }
    public IReadOnlySet<string> NodesWithIncoming { get; }
    public FlowNodeV2? TriggerStart { get; }
    public FlowSettingsV2 Settings { get; }
    public IReadOnlyList<FlowNodeV2> AllNodes { get; }
    public IReadOnlyList<FlowEdgeV2> AllEdges { get; }

    private FlowGraphV2(
        Dictionary<string, FlowNodeV2> nodesById,
        Dictionary<string, List<FlowEdgeV2>> outgoing,
        FlowNodeV2? triggerStart,
        FlowSettingsV2 settings,
        List<FlowNodeV2> allNodes,
        List<FlowEdgeV2> allEdges)
    {
        NodesById = nodesById;
        OutgoingByNode = outgoing;
        TriggerStart = triggerStart;
        Settings = settings;
        AllNodes = allNodes;
        AllEdges = allEdges;

        // Pre-compute incoming edge set for O(1) HasIncomingEdges lookup
        var incoming = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in allEdges)
            incoming.Add(edge.Target);
        NodesWithIncoming = incoming;
    }

    /// <summary>
    /// Build an immutable graph from raw v2 flow_config JSON.
    /// Returns null if JSON is invalid or missing required fields.
    /// </summary>
    public static FlowGraphV2? Build(string flowConfigJson)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(flowConfigJson); }
        catch (JsonException)
        {
            // Invalid JSON — caller logs context (tenant/flow ID)
            return null;
        }

        using (doc)
        {
            return Build(doc);
        }
    }

    /// <summary>
    /// Build from parsed JsonDocument.
    /// </summary>
    public static FlowGraphV2? Build(JsonDocument doc)
    {
        var root = doc.RootElement;

        // Verify version = 2
        if (!root.TryGetProperty("version", out var vProp) || vProp.GetInt32() != 2)
            return null;

        // Parse settings
        var settings = ParseSettings(root);

        // Parse nodes
        var allNodes = new List<FlowNodeV2>();
        var nodesById = new Dictionary<string, FlowNodeV2>(StringComparer.Ordinal);
        FlowNodeV2? triggerStart = null;

        if (root.TryGetProperty("nodes", out var nodesProp) && nodesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var n in nodesProp.EnumerateArray())
            {
                var node = ParseNode(n);
                if (node == null) continue;

                allNodes.Add(node);
                nodesById[node.Id] = node;

                if (node.Type == "trigger_start")
                    triggerStart = node;
            }
        }

        // Parse edges
        var allEdges = new List<FlowEdgeV2>();
        var outgoing = new Dictionary<string, List<FlowEdgeV2>>(StringComparer.Ordinal);

        if (root.TryGetProperty("edges", out var edgesProp) && edgesProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in edgesProp.EnumerateArray())
            {
                var edge = ParseEdge(e);
                if (edge == null) continue;

                allEdges.Add(edge);

                if (!outgoing.TryGetValue(edge.Source, out var list))
                {
                    list = new List<FlowEdgeV2>();
                    outgoing[edge.Source] = list;
                }
                list.Add(edge);
            }
        }

        return new FlowGraphV2(nodesById, outgoing, triggerStart, settings, allNodes, allEdges);
    }

    /// <summary>
    /// Get outgoing edges for a node, optionally filtered by source handle.
    /// </summary>
    public List<FlowEdgeV2> GetOutgoingEdges(string nodeId, string? sourceHandle = null)
    {
        if (!OutgoingByNode.TryGetValue(nodeId, out var edges))
            return new List<FlowEdgeV2>();

        if (sourceHandle == null)
            return edges;

        return edges.Where(e => e.SourceHandle == sourceHandle).ToList();
    }

    /// <summary>
    /// Get the single default outgoing edge (no handle or null handle) for a node.
    /// Returns null if none found.
    /// </summary>
    public FlowEdgeV2? GetDefaultOutgoingEdge(string nodeId)
    {
        if (!OutgoingByNode.TryGetValue(nodeId, out var edges))
            return null;

        return edges.FirstOrDefault(e => string.IsNullOrEmpty(e.SourceHandle));
    }

    /// <summary>
    /// Get the target node for an edge. Returns null if target not in graph.
    /// </summary>
    public FlowNodeV2? GetTargetNode(FlowEdgeV2 edge)
    {
        NodesById.TryGetValue(edge.Target, out var node);
        return node;
    }

    /// <summary>
    /// Check if a node has any incoming edges. O(1) via pre-computed set.
    /// </summary>
    public bool HasIncomingEdges(string nodeId)
    {
        return NodesWithIncoming.Contains(nodeId);
    }

    private static FlowSettingsV2 ParseSettings(JsonElement root)
    {
        var settings = new FlowSettingsV2();
        if (!root.TryGetProperty("settings", out var s))
            return settings;

        if (s.TryGetProperty("off_hours_message", out var ohm))
            settings.OffHoursMessage = ohm.GetString();
        if (s.TryGetProperty("unknown_input_message", out var uim))
            settings.UnknownInputMessage = uim.GetString();
        if (s.TryGetProperty("handoff_confidence_threshold", out var hct))
            settings.HandoffConfidenceThreshold = hct.GetDouble();
        if (s.TryGetProperty("session_timeout_minutes", out var stm))
            settings.SessionTimeoutMinutes = stm.GetInt32();
        if (s.TryGetProperty("max_loop_count", out var mlc))
            settings.MaxLoopCount = mlc.GetInt32();

        return settings;
    }

    private static FlowNodeV2? ParseNode(JsonElement n)
    {
        if (!n.TryGetProperty("id", out var idProp) || !n.TryGetProperty("type", out var typeProp))
            return null;

        var id = idProp.GetString();
        var type = typeProp.GetString();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
            return null;

        // Store raw data as Dictionary for handler access
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        JsonElement? rawDataElement = null;

        if (n.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
        {
            rawDataElement = dataProp;
            foreach (var prop in dataProp.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    data[prop.Name] = prop.Value.GetString() ?? "";
                else
                    data[prop.Name] = prop.Value.GetRawText();
            }
        }

        return new FlowNodeV2
        {
            Id = id,
            Type = type,
            Data = data,
            RawDataJson = rawDataElement?.GetRawText()
        };
    }

    private static FlowEdgeV2? ParseEdge(JsonElement e)
    {
        if (!e.TryGetProperty("id", out var idProp) ||
            !e.TryGetProperty("source", out var srcProp) ||
            !e.TryGetProperty("target", out var tgtProp))
            return null;

        var id = idProp.GetString();
        var source = srcProp.GetString();
        var target = tgtProp.GetString();
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return null;

        return new FlowEdgeV2
        {
            Id = id,
            Source = source,
            Target = target,
            SourceHandle = e.TryGetProperty("sourceHandle", out var sh) ? sh.GetString() : null,
            TargetHandle = e.TryGetProperty("targetHandle", out var th) ? th.GetString() : null
        };
    }
}

// ============================================================
// Graph DTOs (immutable after construction)
// ============================================================

public sealed class FlowNodeV2
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required IReadOnlyDictionary<string, string> Data { get; init; }
    public string? RawDataJson { get; init; }

    public string GetData(string key, string fallback = "")
    {
        return Data.TryGetValue(key, out var val) ? val : fallback;
    }
}

public sealed class FlowEdgeV2
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }
    public string? SourceHandle { get; init; }
    public string? TargetHandle { get; init; }
}

public sealed class FlowSettingsV2
{
    public string? OffHoursMessage { get; set; }
    public string? UnknownInputMessage { get; set; }
    public double HandoffConfidenceThreshold { get; set; } = 0.5;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int MaxLoopCount { get; set; } = 10;
}

/// <summary>
/// v2 session state stored in chat_sessions.session_data JSONB.
/// Mutable during engine execution, serialized after each step.
/// </summary>
public sealed class SessionStateV2
{
    public string CurrentNodeId { get; set; } = "";
    public Dictionary<string, string> Variables { get; set; } = new();
    public List<string> ExecutionPath { get; set; } = new();
    public Dictionary<string, int> LoopCounters { get; set; } = new();
    public PendingInputState? PendingInput { get; set; }
    public string Status { get; set; } = "active"; // active, completed, error, handed_off
}

public sealed class PendingInputState
{
    public required string Type { get; init; } // "menu" or "text"
    public List<string>? Options { get; init; }
    public string? NodeId { get; init; }
}
