using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Kanban;

/// <summary>
/// FEAT-PILOT-KANBAN board payload`i. GET /api/ops/kanban/{board_key}
/// response`u. Cards status + position`a gore siralidir; Frontend
/// gruplamayi kolon bazinda yapar.
/// </summary>
public sealed class KanbanBoardDto
{
    [JsonPropertyName("board_key")]
    public string BoardKey { get; init; } = string.Empty;

    [JsonPropertyName("cards")]
    public System.Collections.Generic.IReadOnlyList<KanbanCardDto> Cards { get; init; } =
        System.Array.Empty<KanbanCardDto>();

    [JsonPropertyName("updated_at")]
    public System.DateTime UpdatedAt { get; init; }
}
