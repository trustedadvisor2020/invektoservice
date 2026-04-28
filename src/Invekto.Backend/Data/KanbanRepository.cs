using Invekto.Shared.Contracts.Kanban;
using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Backend.Data;

/// <summary>
/// FEAT-PILOT-KANBAN: read+update repository over kanban_cards (Migration 035).
///
/// Read-only Dashboard semantics — Q manuel kart edit yapmaz. Tek mutation
/// path: PATCH /api/ops/kanban/{board_key}/cards/{card_slug} ile status
/// update. /wrap workflow Step 3.5 hibrit kanban sync bu endpoint`i hibrit
/// (otomatik oner + Q onayla) flow ile cagirir.
///
/// board_key plat-formum geneli (tenant_id NULL kavrami): board_key='dent-pilot'
/// SuperAdmin tek bakis. Gelecek pilotlar icin board_key='tenant-X-pilot' acilir.
/// </summary>
public class KanbanRepository
{
    private readonly PostgresConnectionFactory _db;

    public KanbanRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Belirtilen board`un tum kart`larini status + position`a gore sirali
    /// donerir. Bos liste = board mevcut degil (404 endpoint tarafinda mapleyebilir).
    ///
    /// Tenant isolation (lesson 2026-04-28 Codex CQ9): tum DB queries tenant_id
    /// filter ile scope'lanir. tenantId=null -> platform-level board (board_key
    /// 'dent-pilot' SuperAdmin), kart'lar tenant_id IS NULL kayitli. tenantId=X
    /// -> tenant-scoped board, sadece o tenant'in kart'lari donerir. Cross-tenant
    /// pollution riski yok: composite (board_key + tenant_id) kombinasyonu disinda
    /// row donulmez. IS NOT DISTINCT FROM null-safe equality (NULL = NULL match).
    /// </summary>
    public virtual async Task<IReadOnlyList<KanbanCardDto>> GetCardsAsync(
        string boardKey, CancellationToken ct = default, long? tenantId = null)
    {
        const string sql = @"
            SELECT id, board_key, card_slug, tenant_id,
                   status, category, priority, position,
                   title, summary, body_markdown, owner, eta,
                   source_file, source_anchor,
                   created_at, updated_at, completed_at
              FROM kanban_cards
             WHERE board_key = @board
               AND tenant_id IS NOT DISTINCT FROM @tenant
             ORDER BY
                CASE status
                    WHEN 'BLOCKED'     THEN 1
                    WHEN 'TODO'        THEN 2
                    WHEN 'IN_PROGRESS' THEN 3
                    WHEN 'BACKLOG'     THEN 4
                    WHEN 'DONE'        THEN 5
                    ELSE 99
                END,
                position ASC,
                id ASC";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("board", boardKey);
        cmd.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);

        var list = new List<KanbanCardDto>(64);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadCard(reader));
        }
        return list;
    }

    /// <summary>
    /// Bir kart`in status (+ optional completed_at) alanlarini gunceller.
    /// Donus: guncellenmis card; null = card slug bulunamadi.
    ///
    /// completedAtOverride:
    ///   - status=DONE ise null gelirse server NOW() basar (otomatik tamamlanma)
    ///   - status=DONE ise deger gelirse override edilir
    ///   - status<>DONE ise her durumda completed_at NULL`a cekilir
    ///     (DONE`dan geri cekilen kart re-open semantigi)
    /// </summary>
    public virtual async Task<KanbanCardDto?> UpdateCardStatusAsync(
        string boardKey,
        string cardSlug,
        KanbanStatus newStatus,
        DateTime? completedAtOverride,
        DateTime nowUtc,
        CancellationToken ct = default,
        long? tenantId = null)
    {
        var statusDb = newStatus.ToDbValue();
        DateTime? effectiveCompletedAt =
            newStatus == KanbanStatus.Done
                ? (completedAtOverride ?? nowUtc)
                : null;

        // Tenant isolation guard: tenant_id IS NOT DISTINCT FROM @tenant
        // null-safe equality. Caller tenant context'i WHERE clause'a yansir,
        // cross-tenant kart UPDATE imkansiz (composite board_key + card_slug
        // + tenant_id mismatch -> 0 row matched -> endpoint 404).
        const string sql = @"
            UPDATE kanban_cards
               SET status       = @status,
                   updated_at   = @now,
                   completed_at = @completed
             WHERE board_key = @board
               AND card_slug = @slug
               AND tenant_id IS NOT DISTINCT FROM @tenant
            RETURNING id, board_key, card_slug, tenant_id,
                      status, category, priority, position,
                      title, summary, body_markdown, owner, eta,
                      source_file, source_anchor,
                      created_at, updated_at, completed_at";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("status", statusDb);
        cmd.Parameters.AddWithValue("now", nowUtc);
        cmd.Parameters.AddWithValue("completed",
            (object?)effectiveCompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("board", boardKey);
        cmd.Parameters.AddWithValue("slug", cardSlug);
        cmd.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return ReadCard(reader);
    }

    /// <summary>
    /// Board son guncelleme zamani (header / cache check icin). Bos board ->
    /// DateTime.MinValue (epoch). Endpoint bunu KanbanBoardDto.UpdatedAt
    /// alanina yansitir.
    /// </summary>
    public virtual async Task<DateTime> GetBoardUpdatedAtAsync(
        string boardKey, CancellationToken ct = default, long? tenantId = null)
    {
        const string sql = @"
            SELECT COALESCE(MAX(updated_at), '0001-01-01T00:00:00Z'::timestamptz)
              FROM kanban_cards
             WHERE board_key = @board
               AND tenant_id IS NOT DISTINCT FROM @tenant";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("board", boardKey);
        cmd.Parameters.AddWithValue("tenant", (object?)tenantId ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DateTime dt ? dt : DateTime.MinValue;
    }

    private static KanbanCardDto ReadCard(NpgsqlDataReader r) => new KanbanCardDto
    {
        Id            = r.GetInt64(0),
        BoardKey      = r.GetString(1),
        CardSlug      = r.GetString(2),
        TenantId      = r.IsDBNull(3)  ? null : r.GetInt64(3),
        Status        = r.GetString(4),
        Category      = r.GetString(5),
        Priority      = r.GetString(6),
        Position      = r.GetInt32(7),
        Title         = r.GetString(8),
        Summary       = r.IsDBNull(9)  ? null : r.GetString(9),
        BodyMarkdown  = r.IsDBNull(10) ? null : r.GetString(10),
        Owner         = r.IsDBNull(11) ? null : r.GetString(11),
        Eta           = r.IsDBNull(12) ? null : r.GetString(12),
        SourceFile    = r.IsDBNull(13) ? null : r.GetString(13),
        SourceAnchor  = r.IsDBNull(14) ? null : r.GetString(14),
        CreatedAt     = r.GetDateTime(15),
        UpdatedAt     = r.GetDateTime(16),
        CompletedAt   = r.IsDBNull(17) ? null : r.GetDateTime(17)
    };
}
