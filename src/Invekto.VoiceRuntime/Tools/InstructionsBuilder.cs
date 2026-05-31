using Invekto.Shared.Contracts.Voice;

namespace Invekto.VoiceRuntime.Tools;

/// <summary>
/// FEAT-VFB F0.5 Chunk B (AD-24): builds the Realtime `session.update.instructions` system prompt
/// from a server-side authoritative VoiceTestContext. Used in Chunk D when VoicePocEndpoints
/// sends the initial session.update — replaces the legacy F0 appsettings-driven instructions
/// for F0.5 mode only (F0 backward-compat path keeps the appsettings template untouched).
///
/// Generic template (per Q AskUserQuestion 2026-05-25): tenant_name + sector + flow_name
/// inject only. Per-flow voice_persona kolonu (F2 Migration 050) is a non-goal here.
///
/// Defensive null/empty handling (verification_question Q5): the template MUST never render
/// with `{placeholder}` leftovers. Each field has a fallback string so a missing sector or
/// blank flow_name still produces a complete Turkish prompt.
/// </summary>
public static class InstructionsBuilder
{
    private const string FallbackTenantName = "bu firma";
    private const string FallbackFlowName = "varsayilan akis";

    /// <summary>
    /// Renders the Turkish system prompt for the impersonated tenant + flow.
    /// Output is deterministic: same context → same string (caller can log + diff).
    /// </summary>
    public static string Build(VoiceTestContext ctx)
    {
        var tenantName = Sanitize(ctx.TenantName, FallbackTenantName);
        var flowName = Sanitize(ctx.FlowName, FallbackFlowName);
        var sectorClause = BuildSectorClause(ctx.Sector);

        // RAG grounding is MANDATORY, not advisory. The earlier soft wording ("bilgi gerektiginde
        // kullan") let the model answer factual questions from its own training instead of calling
        // search_knowledge_base — it confidently hallucinated (e.g. heard the brand name wrong via
        // STT and improvised a generic answer). The prompt below forces a tool call before ANY
        // informational answer, forbids guessing, and accounts for STT mishearing the brand/product.
        return
            $"Sen {tenantName} firmasinin{sectorClause} musteri hizmetleri sesli asistanisin. " +
            $"Aktif konusma akisi: {flowName}. " +
            "EN ONEMLI KURAL — BILGI BANKASI ZORUNLULUGU: Firma, urun, hizmet, ozellik, fiyat, " +
            "adres, calisma saati, kampanya, surec ya da herhangi bir bilgi iceren HER soruda, " +
            "cevap vermeden ONCE MUTLAKA search_knowledge_base aracini cagir ve SADECE donen " +
            "sonuclara dayanarak cevapla. Konuyu biliyor olsan bile once bilgi bankasini sorgula; " +
            "kendi genel bilgini KULLANMA, tahmin etme, uydurma. " +
            "Kullanici marka, urun ya da terimi yanlis telaffuz etmis olabilir (sesli giris hatasi) " +
            "— yine de soruyu search_knowledge_base ile aratip dogru bilgiyi bul, pesin hukum verme. " +
            "search_knowledge_base sonuc dondurmezse veya konu bilgi bankasinda yoksa BILGI UYDURMA; " +
            "'bu konuda bilgi bankamda kayit yok, dilerseniz ilgili birime aktarabilirim' de. " +
            "Yalnizca selamlama ve nezaket sozleri (merhaba, tesekkurler, hosca kal) icin arac " +
            "cagirmana gerek yok. Turkce, kisa, dogal ve samimi konus.";
    }

    private static string BuildSectorClause(string? sector)
    {
        var trimmed = sector?.Trim();
        return string.IsNullOrEmpty(trimmed) ? string.Empty : $" ({trimmed})";
    }

    private static string Sanitize(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? fallback : trimmed;
    }
}
