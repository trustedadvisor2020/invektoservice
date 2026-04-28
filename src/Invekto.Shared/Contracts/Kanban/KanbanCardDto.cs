using System.Text.Json.Serialization;

namespace Invekto.Shared.Contracts.Kanban;

/// <summary>
/// FEAT-PILOT-KANBAN tek kart payload`i. SuperAdmin Dashboard
/// PilotKanbanPage.tsx render eder; KanbanDrawer.tsx tum alanlari inline
/// gosterir (body_markdown minimal subset render).
///
/// PostgreSQL kanban_cards tablosu (Migration 035) ile birebir mapping.
/// </summary>
public sealed class KanbanCardDto
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("board_key")]
    public string BoardKey { get; init; } = string.Empty;

    [JsonPropertyName("card_slug")]
    public string CardSlug { get; init; } = string.Empty;

    /// <summary>
    /// Reference code (Migration 036) — kart kimligi 4-karakter mnemonic
    /// (kategori prefix + 3 rakam, ornek "C001", "K005", "D021"). '----'
    /// placeholder = henuz atanmamis. /wrap workflow Step 3.5 commit message'da
    /// slug VEYA ref_code match destekli; ref_code regex '^[A-Z][0-9]{3}$'.
    /// Per-board UNIQUE (atanmislar partial index ile).
    /// </summary>
    [JsonPropertyName("ref_code")]
    public string RefCode { get; init; } = "----";

    /// <summary>
    /// Bagimlilik referans kodu listesi (CSV format, ornek "C001,C003"). Migration 037
    /// CHECK constraint regex `^[A-Z][0-9]{3}(,[A-Z][0-9]{3})*$` enforce. NULL = bagimsiz
    /// kart (paralel calisilabilir). UI kart altinda kucuk indicator + drawer'da
    /// tiklanabilir liste olarak render edilir.
    /// </summary>
    [JsonPropertyName("depends_on")]
    public string? DependsOn { get; init; }

    /// <summary>
    /// Platform-level board (board_key='dent-pilot') icin null. Gelecek tenant-scoped
    /// board'lar icin tenant_id dolu olur, repository runtime tarafinda WHERE clause
    /// ile scope'lanir.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public long? TenantId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = KanbanStatusExtensions.DbValueTodo;

    [JsonPropertyName("category")]
    public string Category { get; init; } = KanbanCategoryExtensions.DbValueOps;

    [JsonPropertyName("priority")]
    public string Priority { get; init; } = "P2";

    [JsonPropertyName("position")]
    public int Position { get; init; } = 100;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("body_markdown")]
    public string? BodyMarkdown { get; init; }

    [JsonPropertyName("owner")]
    public string? Owner { get; init; }

    [JsonPropertyName("eta")]
    public string? Eta { get; init; }

    [JsonPropertyName("source_file")]
    public string? SourceFile { get; init; }

    [JsonPropertyName("source_anchor")]
    public string? SourceAnchor { get; init; }

    [JsonPropertyName("created_at")]
    public System.DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public System.DateTime UpdatedAt { get; init; }

    [JsonPropertyName("completed_at")]
    public System.DateTime? CompletedAt { get; init; }
}
