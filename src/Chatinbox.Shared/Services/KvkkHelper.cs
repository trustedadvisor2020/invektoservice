using System.Text.Json;

namespace Chatinbox.Shared.Services;

/// <summary>
/// GR-2.6: KVKK compliance helpers for health tenants.
/// Centralized disclaimer text and health tenant detection.
/// Used by: Automation, AgentAI, Outbound, Knowledge, Backend.
/// </summary>
public static class KvkkHelper
{
    /// <summary>
    /// Disclaimer appended to automated messages for health tenants.
    /// Used by: Automation (chatbot), Outbound (broadcast/trigger).
    /// </summary>
    public const string HealthDisclaimer =
        "\n\n---\nBu mesaj bilgilendirme amaclidir, tibbi teshis veya tedavi onerisi degildir. " +
        "Saglik sorunlariniz icin mutlaka doktorunuza basvurun.";

    /// <summary>
    /// Warning added to AgentAI suggestion responses for health tenants.
    /// Goes to agent UI, not to customer.
    /// </summary>
    public const string AgentAIWarning =
        "KVKK Uyarisi: Bu oneri bilgilendirme amaclidir, tibbi teshis/tedavi yerine gecmez.";

    /// <summary>
    /// Tag value for medical documents uploaded by health tenants.
    /// </summary>
    public const string MedicalDocumentTag = "kvkk_medical_content";

    /// <summary>
    /// Warning shown when a medical document is tagged.
    /// </summary>
    public const string MedicalDocumentWarning =
        "Bu dokuman tibbi icerik icermektedir. KVKK kapsaminda ozel nitelikli kisisel veri olarak degerlendirilir.";

    /// <summary>
    /// Health-sector values in tenant_registry.sector column.
    /// </summary>
    private static readonly HashSet<string> HealthSectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "dis_klinik",
        "estetik",
        "saglik",
        "health"
    };

    /// <summary>
    /// Check if a tenant is a health tenant.
    /// First checks settings_json for explicit "is_health_tenant": true.
    /// Falls back to sector column match (dis_klinik, estetik, saglik, health).
    /// </summary>
    /// <param name="settingsJson">tenant_registry.settings_json value (nullable)</param>
    /// <param name="sector">tenant_registry.sector value (nullable)</param>
    /// <returns>True if tenant is classified as health sector</returns>
    public static bool IsHealthTenant(string? settingsJson, string? sector)
    {
        // 1. Explicit flag in settings_json takes priority
        if (!string.IsNullOrEmpty(settingsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(settingsJson);
                if (doc.RootElement.TryGetProperty("is_health_tenant", out var prop)
                    && prop.ValueKind == JsonValueKind.True)
                {
                    return true;
                }

                // Also check explicit false to allow override
                if (doc.RootElement.TryGetProperty("is_health_tenant", out var propFalse)
                    && propFalse.ValueKind == JsonValueKind.False)
                {
                    return false;
                }
            }
            catch (JsonException)
            {
                // Malformed settings_json: fall through to sector check
            }
        }

        // 2. Fallback: check sector column
        if (!string.IsNullOrEmpty(sector))
        {
            return HealthSectors.Contains(sector);
        }

        return false;
    }

    /// <summary>
    /// Append health disclaimer to a message if the tenant is a health tenant.
    /// No-op for non-health tenants.
    /// </summary>
    public static string AppendDisclaimerIfHealth(string message, bool isHealthTenant)
    {
        if (!isHealthTenant)
            return message;

        return message + HealthDisclaimer;
    }
}
