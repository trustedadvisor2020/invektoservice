using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Handles return/refund intent detection, reason classification, and deflection actions.
/// Called post-flow by AutomationOrchestrator when 'return_request' intent is detected.
/// Records result to return_deflections table. Never throws — all exceptions caught and logged.
/// PKT-6B1: GR-3.8 Return Deflection v1 + GR-3.17 Return Deflection v2.
/// </summary>
public sealed class ReturnDeflectionService
{
    private readonly AutomationRepository _repo;
    private readonly JsonLinesLogger _logger;

    /// <summary>Return reason keywords (Turkish, lowercase for case-insensitive match).</summary>
    private static readonly (string Keyword, string Category)[] ReasonPatterns =
    {
        ("beden", "beden"),
        ("boyut", "beden"),
        ("küçük geldi", "beden"),
        ("büyük geldi", "beden"),
        ("dar geldi", "beden"),
        ("bol geldi", "beden"),
        ("renk", "renk"),
        ("rengi farklı", "renk"),
        ("rengi uymuyor", "renk"),
        ("hasarlı", "hasar"),
        ("kırık", "hasar"),
        ("bozuk", "hasar"),
        ("yırtık", "hasar"),
        ("defolu", "hasar"),
        ("kalite", "kalite"),
        ("kalitesiz", "kalite"),
        ("malzeme kötü", "kalite"),
        ("vazgeçtim", "fikir_degisiklik"),
        ("fikrimi değiştirdim", "fikir_degisiklik"),
        ("istemiyorum", "fikir_degisiklik"),
        ("almaktan vazgeçtim", "fikir_degisiklik"),
        ("yanlış ürün", "yanlis_urun"),
        ("başka ürün geldi", "yanlis_urun")
    };

    /// <summary>Action routing: reason → deflection action.</summary>
    private static readonly Dictionary<string, string> ReasonToAction = new(StringComparer.OrdinalIgnoreCase)
    {
        { "beden", "exchange_offered" },
        { "renk", "exchange_offered" },
        { "yanlis_urun", "exchange_offered" },
        { "fikir_degisiklik", "coupon_offered" },
        { "hasar", "return_started" },
        { "kalite", "return_started" },
        { "diger", "handoff" }
    };

    public ReturnDeflectionService(AutomationRepository repo, JsonLinesLogger logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Process a return intent: classify reason, determine action, record to DB.
    /// Returns null on failure. Never throws.
    /// </summary>
    public async Task<ReturnDeflectionResult?> ProcessReturnIntentAsync(
        int tenantId, string? phone, string messageText,
        string? conversationId, string? settingsJson,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            _logger.SystemWarn($"[{ErrorCodes.AutomationReturnDeflectionFailed}] Cannot process return without phone for tenant {tenantId}");
            return null;
        }

        try
        {
            var normalizedText = messageText.ToLowerInvariant();
            var reasonCategory = ClassifyReason(normalizedText);
            var actionTaken = ReasonToAction.GetValueOrDefault(reasonCategory, "handoff");

            // GR-3.17: Assign coupon from tenant settings if action is coupon_offered
            string? couponCode = null;
            decimal? couponValue = null;
            if (actionTaken == "coupon_offered")
            {
                (couponCode, couponValue) = ExtractCouponFromSettings(settingsJson, tenantId);
            }

            // Record deflection
            var deflectionId = await _repo.InsertReturnDeflectionAsync(
                tenantId, conversationId, phone,
                reasonCategory, normalizedText,
                actionTaken, couponCode, couponValue, ct);

            _logger.StepInfo(
                $"Return deflection: tenant={tenantId}, phone={phone}, reason={reasonCategory}, " +
                $"action={actionTaken}, coupon={couponCode ?? "none"}, id={deflectionId}",
                "return-deflection");

            return new ReturnDeflectionResult
            {
                DeflectionId = deflectionId,
                ReasonCategory = reasonCategory,
                ActionTaken = actionTaken,
                CouponCode = couponCode,
                CouponValue = couponValue,
                SuggestedResponse = BuildSuggestedResponse(reasonCategory, actionTaken, couponCode)
            };
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationReturnDeflectionFailed}] Return deflection DB error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCouponAssignFailed}] Return deflection JSON parse error for tenant {tenantId}: {ex.Message}");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationReturnDeflectionFailed}] Return deflection operation error for tenant {tenantId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Mark a deflection as successfully deflected (customer accepted alternative).
    /// Called when conversion is confirmed.
    /// </summary>
    public async Task<bool> MarkDeflectedAsync(
        int tenantId, int deflectionId, decimal? revenueSaved, CancellationToken ct = default)
    {
        try
        {
            return await _repo.UpdateReturnDeflectionResultAsync(
                tenantId, deflectionId, wasDeflected: true, revenueSaved, ct);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationReturnDeflectionFailed}] Mark deflected DB error: tenant={tenantId}, id={deflectionId}: {ex.Message}");
            return false;
        }
    }

    private string ClassifyReason(string normalizedText)
    {
        foreach (var (keyword, category) in ReasonPatterns)
        {
            if (normalizedText.Contains(keyword, StringComparison.Ordinal))
                return category;
        }
        return "diger";
    }

    private (string? Code, decimal? Value) ExtractCouponFromSettings(string? settingsJson, int tenantId)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCouponAssignFailed}] No settings_json for tenant {tenantId}, coupon assignment skipped");
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("return_coupons", out var couponsEl) &&
                couponsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var coupon in couponsEl.EnumerateArray())
                {
                    var code = coupon.TryGetProperty("code", out var c) ? c.GetString() : null;
                    var value = coupon.TryGetProperty("value", out var v) ? v.GetDecimal() : 0m;
                    var isActive = !coupon.TryGetProperty("is_active", out var a) || a.GetBoolean();

                    if (!string.IsNullOrWhiteSpace(code) && isActive)
                        return (code, value);
                }
            }

            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCouponAssignFailed}] No active coupons in settings for tenant {tenantId}");
            return (null, null);
        }
        catch (JsonException ex)
        {
            _logger.SystemWarn(
                $"[{ErrorCodes.AutomationCouponAssignFailed}] Settings parse error for tenant {tenantId}: {ex.Message}");
            return (null, null);
        }
    }

    private static string BuildSuggestedResponse(string reason, string action, string? couponCode)
    {
        return action switch
        {
            "exchange_offered" => reason switch
            {
                "beden" => "Beden değişimi yapabiliriz. Hangi bedeni tercih edersiniz?",
                "renk" => "Renk değişimi yapabiliriz. Hangi rengi tercih edersiniz?",
                "yanlis_urun" => "Doğru ürünü gönderebiliriz. Sipariş numaranızı paylaşır mısınız?",
                _ => "Değişim yapabiliriz. Size nasıl yardımcı olabiliriz?"
            },
            "coupon_offered" when couponCode != null =>
                $"Sizi memnun etmek isteriz. {couponCode} koduyla bir sonraki alışverişinizde indirim kazanabilirsiniz.",
            "coupon_offered" =>
                "Sizi memnun etmek isteriz. Size özel bir indirim kodu oluşturabiliriz.",
            "return_started" =>
                "İade talebinizi başlatıyoruz. Size iade süreciyle ilgili bilgi vereceğiz.",
            _ =>
                "İade talebinizi bir uzmanımıza yönlendiriyorum."
        };
    }
}

/// <summary>Result of return deflection processing.</summary>
public sealed class ReturnDeflectionResult
{
    public int DeflectionId { get; init; }
    public string ReasonCategory { get; init; } = "";
    public string ActionTaken { get; init; } = "";
    public string? CouponCode { get; init; }
    public decimal? CouponValue { get; init; }
    public string SuggestedResponse { get; init; } = "";
}
