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

        return
            $"Sen {tenantName} firmasinin{sectorClause} sesli AI asistanisin. " +
            $"Aktif konusma akisi: {flowName}. " +
            "Musteri sorularina Turkce, kisa ve dogal cevap ver. " +
            "Bilgi gerektiginde search_knowledge_base aracini kullan; arac sonucu donmezse " +
            "musteriye 'su an bilgi bankama ulasamiyorum, kisaca bilgi alip donebilir miyim' " +
            "diyerek nazikce yonlendir. " +
            "Telefon, fiyat, randevu gibi spesifik bilgileri bilgi bankasi sonucu olmadan " +
            "uydurma — bilmiyorsan oldugunu acikca soyle.";
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
