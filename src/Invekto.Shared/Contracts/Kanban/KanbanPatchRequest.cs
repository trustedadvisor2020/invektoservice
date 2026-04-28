using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Kanban;

/// <summary>
/// FEAT-PILOT-KANBAN PATCH /api/ops/kanban/{board_key}/cards/{card_slug}
/// body. /wrap workflow Step 3.5 hibrit kanban sync bu payload`i gonderir.
///
/// status zorunlu — KanbanStatus enum (BLOCKED|TODO|IN_PROGRESS|BACKLOG|DONE).
/// completed_at opsiyonel — DONE`a gecince server tarafinda otomatik NOW()
/// basar; istemci override icin override edebilir.
///
/// Diger alanlar (title, summary, body_markdown, owner, eta) PATCH disinda —
/// sadece status update icin endpoint. Icerik degisiklikleri icin Migration
/// re-seed veya manual SQL gerek (read-only Dashboard kuralı).
/// </summary>
public sealed class KanbanPatchRequest
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("completed_at")]
    public System.DateTime? CompletedAt { get; init; }
}
