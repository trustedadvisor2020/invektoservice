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
            SELECT id, board_key, card_slug, ref_code, depends_on, tenant_id,
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
        // /wrap workflow Step 3.5 commit message'da slug VEYA ref_code match
        // destekler (Q karari 2026-04-28 FEAT-ROADMAP-V2-REFCODE). Regex '^[A-Z][0-9]{3}$'
        // ref_code; her sey lowercase slug. WHERE clause ikisini de check eder
        // (card_slug = @slug OR ref_code = @slug) — caller param "slugOrRef"
        // semantik tek alanla iletilir.
        const string sql = @"
            UPDATE kanban_cards
               SET status       = @status,
                   updated_at   = @now,
                   completed_at = @completed
             WHERE board_key = @board
               AND (card_slug = @slug OR ref_code = @slug)
               AND tenant_id IS NOT DISTINCT FROM @tenant
            RETURNING id, board_key, card_slug, ref_code, depends_on, tenant_id,
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

    // Audit fix D037 (2026-04-29 Batch C): GetBoardUpdatedAtAsync dead code silindi.
    // Tek caller (KanbanEndpoints GET handler) artik cards.Max(c => c.UpdatedAt)
    // ile in-memory hesaplar — 1 DB call yeterli (onceki: 2 ayri call). Bos board
    // durumda DateTime.MinValue eski helper davranisi korunur.

    private static KanbanCardDto ReadCard(NpgsqlDataReader r) => new KanbanCardDto
    {
        Id            = r.GetInt64(0),
        BoardKey      = r.GetString(1),
        CardSlug      = r.GetString(2),
        RefCode       = r.GetString(3),
        DependsOn     = r.IsDBNull(4)  ? null : r.GetString(4),
        TenantId      = r.IsDBNull(5)  ? null : r.GetInt64(5),
        Status        = r.GetString(6),
        Category      = r.GetString(7),
        Priority      = r.GetString(8),
        Position      = r.GetInt32(9),
        Title         = r.GetString(10),
        Summary       = r.IsDBNull(11) ? null : r.GetString(11),
        BodyMarkdown  = r.IsDBNull(12) ? null : r.GetString(12),
        Owner         = r.IsDBNull(13) ? null : r.GetString(13),
        Eta           = r.IsDBNull(14) ? null : r.GetString(14),
        SourceFile    = r.IsDBNull(15) ? null : r.GetString(15),
        SourceAnchor  = r.IsDBNull(16) ? null : r.GetString(16),
        CreatedAt     = r.GetDateTime(17),
        UpdatedAt     = r.GetDateTime(18),
        CompletedAt   = r.IsDBNull(19) ? null : r.GetDateTime(19)
    };
}
