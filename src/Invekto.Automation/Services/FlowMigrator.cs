using System.Text.Json;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Converts v1 menu-based flow config to v2 node/edge graph format.
/// Auto-layout: trigger(250,50) → welcome(250,200) → menu(250,350) → options(spread, y=550).
/// IMP-6: Auto-Layout.
/// </summary>
public sealed class FlowMigrator
{
    private readonly JsonLinesLogger _logger;

    private const int CenterX = 250;
    private const int OptionStartY = 550;
    private const int OptionGapX = 200;

    public FlowMigrator(JsonLinesLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Migrate v1 flow_config JSON to v2 format.
    /// Returns migration result with v2 JSON + warnings. Returns null if input is invalid.
    /// </summary>
    public MigrationResult? MigrateToV2(string v1ConfigJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(v1ConfigJson);
            var root = doc.RootElement;

            // Check if already v2
            if (root.TryGetProperty("version", out var vProp) && vProp.ValueKind == JsonValueKind.Number && vProp.GetInt32() == 2)
            {
                _logger.SystemWarn("Flow is already v2, skipping migration");
                return null;
            }

            // Parse v1 fields
            var welcomeMessage = root.TryGetProperty("welcome_message", out var wm)
                ? wm.GetString() ?? "Hosgeldiniz!"
                : "Hosgeldiniz!";
            var menuText = "";
            var menuOptions = new List<V1MenuOption>();

            if (root.TryGetProperty("menu", out var menu))
            {
                if (menu.TryGetProperty("text", out var mt))
                    menuText = mt.GetString() ?? "";

                if (menu.TryGetProperty("options", out var opts))
                {
                    foreach (var opt in opts.EnumerateArray())
                    {
                        menuOptions.Add(new V1MenuOption
                        {
                            Key = opt.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "",
                            Label = opt.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "",
                            Action = opt.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "",
                            ReplyText = opt.TryGetProperty("reply_text", out var rt) ? rt.GetString() : null
                        });
                    }
                }
            }

            var offHoursMsg = root.TryGetProperty("off_hours_message", out var oh) ? oh.GetString() : null;
            var unknownMsg = root.TryGetProperty("unknown_input_message", out var ui) ? ui.GetString() : null;
            var threshold = root.TryGetProperty("handoff_confidence_threshold", out var ht) ? ht.GetDouble() : 0.5;

            // Build v2 structure
            var nodes = new List<object>();
            var edges = new List<object>();
            var warnings = new List<string>();
            var edgeId = 1;

            // 1. trigger_start
            nodes.Add(new
            {
                id = "trigger_start_1",
                type = "trigger_start",
                position = new { x = CenterX, y = 50 },
                data = new { label = "Baslangic" }
            });

            // 2. welcome message_text
            nodes.Add(new
            {
                id = "msg_text_welcome",
                type = "message_text",
                position = new { x = CenterX, y = 200 },
                data = new { label = "Karsilama", text = welcomeMessage }
            });
            edges.Add(new
            {
                id = $"e{edgeId++}",
                source = "trigger_start_1",
                target = "msg_text_welcome"
            });

            // 3. menu
            var menuOptionDefs = menuOptions.Select((opt, i) => new
            {
                key = opt.Key,
                label = opt.Label,
                handle_id = $"opt_{i + 1}"
            }).ToArray();

            nodes.Add(new
            {
                id = "msg_menu_main",
                type = "message_menu",
                position = new { x = CenterX, y = 350 },
                data = new
                {
                    label = "Ana Menu",
                    text = menuText,
                    options = menuOptionDefs
                }
            });
            edges.Add(new
            {
                id = $"e{edgeId++}",
                source = "msg_text_welcome",
                target = "msg_menu_main"
            });

            // 4. Option target nodes (auto-layout horizontally)
            var optionCount = menuOptions.Count;
            var totalWidth = (optionCount - 1) * OptionGapX;
            var startX = CenterX - totalWidth / 2;

            for (var i = 0; i < menuOptions.Count; i++)
            {
                var opt = menuOptions[i];
                var nodeX = startX + i * OptionGapX;
                var handleId = $"opt_{i + 1}";

                switch (opt.Action)
                {
                    case "reply":
                        var replyNodeId = $"msg_text_opt{i + 1}";
                        nodes.Add(new
                        {
                            id = replyNodeId,
                            type = "message_text",
                            position = new { x = nodeX, y = OptionStartY },
                            data = new { label = opt.Label, text = opt.ReplyText ?? "..." }
                        });
                        edges.Add(new
                        {
                            id = $"e{edgeId++}",
                            source = "msg_menu_main",
                            target = replyNodeId,
                            sourceHandle = handleId
                        });
                        break;

                    case "handoff":
                        var handoffNodeId = $"action_handoff_opt{i + 1}";
                        nodes.Add(new
                        {
                            id = handoffNodeId,
                            type = "action_handoff",
                            position = new { x = nodeX, y = OptionStartY },
                            data = new { label = opt.Label }
                        });
                        edges.Add(new
                        {
                            id = $"e{edgeId++}",
                            source = "msg_menu_main",
                            target = handoffNodeId,
                            sourceHandle = handleId
                        });
                        break;

                    case "faq":
                    case "intent":
                        // Phase 4 handlers — create a utility_note placeholder.
                        // Warn caller that these options become no-op until Phase 4.
                        warnings.Add($"Menu secenegi '{opt.Label}' (action: {opt.Action}) → utility_note placeholder'a donusturuldu. Phase 4'te gercek {opt.Action} handler eklenecek.");
                        var noteNodeId = $"note_{opt.Action}_opt{i + 1}";
                        nodes.Add(new
                        {
                            id = noteNodeId,
                            type = "utility_note",
                            position = new { x = nodeX, y = OptionStartY },
                            data = new
                            {
                                label = $"{opt.Label} ({opt.Action})",
                                text = $"v1 migration: {opt.Action} node — Phase 4'te gercek handler eklenecek"
                            }
                        });
                        edges.Add(new
                        {
                            id = $"e{edgeId++}",
                            source = "msg_menu_main",
                            target = noteNodeId,
                            sourceHandle = handleId
                        });
                        break;
                }
            }

            // Build final v2 config
            var v2Config = new
            {
                version = 2,
                metadata = new
                {
                    name = "Migrated from v1",
                    description = "Otomatik v1 → v2 donusumu",
                    canvas_viewport = new { x = 0, y = 0, zoom = 1.0 }
                },
                nodes,
                edges,
                settings = new
                {
                    off_hours_message = offHoursMsg ?? "Su anda mesai saatleri disindayiz.",
                    unknown_input_message = unknownMsg ?? "Anlayamadim. Lutfen gecerli bir secenek girin.",
                    handoff_confidence_threshold = threshold,
                    session_timeout_minutes = 30,
                    max_loop_count = 10
                }
            };

            var v2Json = JsonSerializer.Serialize(v2Config, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return new MigrationResult { V2ConfigJson = v2Json, Warnings = warnings };
        }
        catch (Exception ex)
        {
            _logger.SystemWarn($"v1 → v2 migration failed: {ex.Message}");
            return null;
        }
    }

    private sealed class V1MenuOption
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required string Action { get; init; }
        public string? ReplyText { get; init; }
    }
}

public sealed class MigrationResult
{
    public required string V2ConfigJson { get; init; }
    public required List<string> Warnings { get; init; }
}
