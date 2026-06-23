using Chatinbox.Backend.Data;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Contracts.Kanban;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Backend.Endpoints;

/// <summary>
/// FEAT-PILOT-KANBAN SuperAdmin Dashboard endpoint`leri.
///
///   GET   /api/ops/kanban/{board_key}                       -> KanbanBoardDto
///   PATCH /api/ops/kanban/{board_key}/cards/{card_slug}      -> KanbanCardDto
///
/// Auth: Ops mode (TenantsPage pattern) — Program.cs`teki ValidateOpsAuth +
/// OpsUnauthorized helper`larini Func parametresi olarak alir; bu sayede
/// endpoint mantigi izole + test edilebilir, ayni zamanda lesson 2026-04-22
/// pattern duplication`ini onler. Tenant kullanicilari (session.tenantId != 0)
/// bu endpoint`lere erisemez (Program.cs`teki ValidateOpsAuth zaten reddeder).
///
/// Read-only Dashboard semantigi:
///   * Q manuel kart edit yapmaz; Frontend (PilotKanbanPage + KanbanDrawer)
///     read-only goster surur.
///   * Mutation tek path: PATCH endpoint -> /wrap workflow Step 3.5 hibrit.
///   * Card create/delete YOK (Migration 035 seed + manual SQL).
/// </summary>
public static class KanbanEndpoints
{
    public static void MapKanbanEndpoints(
        this WebApplication app,
        Func<HttpContext, bool> validateOpsAuth,
        Func<HttpContext, IResult> opsUnauthorized)
    {
        // GET /api/ops/kanban/{board_key}
        app.MapGet("/api/ops/kanban/{boardKey}", async (
            string boardKey,
            HttpContext ctx,
            KanbanRepository repo,
            JsonLinesLogger jsonLog) =>
        {
            if (!validateOpsAuth(ctx))
                return opsUnauthorized(ctx);

            if (string.IsNullOrWhiteSpace(boardKey) || boardKey.Length > 64)
            {
                return Results.Json(
                    new { error = ErrorCodes.GeneralValidation,
                          message = "board_key parametresi gerekli (1-64 karakter)." },
                    statusCode: 400);
            }

            try
            {
                // Audit fix D037 (2026-04-29 Batch C): GET single-query optimize.
                // Onceki: GetCardsAsync + GetBoardUpdatedAtAsync 2 ayri DB call.
                // Yeni: GetCardsAsync tek call, updated_at = cards.Max(updated_at).
                // Bos board durumda DateTime.MinValue (eski helper davranisiyla ayni).
                var cards = await repo.GetCardsAsync(boardKey, ctx.RequestAborted);
                var updatedAt = cards.Count > 0
                    ? cards.Max(c => c.UpdatedAt)
                    : DateTime.MinValue;
                return Results.Ok(new KanbanBoardDto
                {
                    BoardKey  = boardKey,
                    Cards     = cards,
                    UpdatedAt = updatedAt
                });
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.KanbanDatabaseError}] KanbanEndpoints GET fail board={boardKey}: {ex.Message}");
                return Results.Json(
                    new { error = ErrorCodes.KanbanDatabaseError,
                          message = $"Kanban veritabanı geçici olarak kullanılamıyor (board='{boardKey}'). " +
                                    "Birkaç dakika sonra tekrar deneyin; sorun devam ederse Migration 035 uygulandı mı kontrol edin." },
                    statusCode: 503);
            }
        });

        // PATCH /api/ops/kanban/{board_key}/cards/{slugOrRef}
        // slugOrRef = card_slug (lowercase kebab) VEYA ref_code (^[A-Z][0-9]{3}$, ornek K005).
        // Repository WHERE clause '(card_slug = @slug OR ref_code = @slug)' ile ikisini de match'ler.
        // /wrap workflow Step 3.5 commit message'da kart referansi 4-karakter mnemonic
        // (Q karari 2026-04-28 FEAT-ROADMAP-V2-REFCODE) veya tam slug ile yapilabilir.
        app.MapPatch("/api/ops/kanban/{boardKey}/cards/{cardSlug}", async (
            string boardKey,
            string cardSlug,
            KanbanPatchRequest body,
            HttpContext ctx,
            KanbanRepository repo,
            JsonLinesLogger jsonLog) =>
        {
            if (!validateOpsAuth(ctx))
                return opsUnauthorized(ctx);

            if (string.IsNullOrWhiteSpace(boardKey) || boardKey.Length > 64
                || string.IsNullOrWhiteSpace(cardSlug) || cardSlug.Length > 128)
            {
                return Results.Json(
                    new { error = ErrorCodes.GeneralValidation,
                          message = "board_key (1-64) ve card_slug (1-128) parametreleri gerekli." },
                    statusCode: 400);
            }

            if (body == null || string.IsNullOrWhiteSpace(body.Status))
            {
                return Results.Json(
                    new { error = ErrorCodes.GeneralValidation,
                          message = "PATCH body 'status' alanı zorunlu (BLOCKED|TODO|IN_PROGRESS|BACKLOG|DONE)." },
                    statusCode: 400);
            }

            if (!KanbanStatusExtensions.TryParse(body.Status, out var newStatus))
            {
                return Results.Json(
                    new { error = ErrorCodes.KanbanInvalidStatus,
                          message = $"Geçersiz status değeri: '{body.Status}'. " +
                                    "Beklenen değerler: BLOCKED, TODO, IN_PROGRESS, BACKLOG, DONE." },
                    statusCode: 400);
            }

            try
            {
                var updated = await repo.UpdateCardStatusAsync(
                    boardKey, cardSlug, newStatus,
                    body.CompletedAt, DateTime.UtcNow, ctx.RequestAborted);

                if (updated == null)
                {
                    return Results.Json(
                        new { error = ErrorCodes.KanbanCardNotFound,
                              message = $"Kart bulunamadı: board='{boardKey}', slug/ref='{cardSlug}'. " +
                                        "GET /api/ops/kanban/{board_key} ile mevcut kart slug ve ref_code listesini doğrulayın." },
                        statusCode: 404);
                }

                jsonLog.SystemInfo(
                    $"KanbanEndpoints: card status update board={boardKey} slug={cardSlug} status={updated.Status}");

                return Results.Ok(updated);
            }
            catch (NpgsqlException ex)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.KanbanDatabaseError}] KanbanEndpoints PATCH fail board={boardKey} slug={cardSlug}: {ex.Message}");
                return Results.Json(
                    new { error = ErrorCodes.KanbanDatabaseError,
                          message = $"Kanban kart güncellenemedi (board='{boardKey}', slug='{cardSlug}'). " +
                                    "Veritabanı geçici olarak kullanılamıyor; birkaç dakika sonra tekrar deneyin." },
                    statusCode: 503);
            }
        });
    }
}
