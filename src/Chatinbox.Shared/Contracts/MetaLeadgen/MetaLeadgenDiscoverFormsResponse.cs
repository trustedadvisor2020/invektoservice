using System.Text.Json.Serialization;

namespace Chatinbox.Shared.Contracts.MetaLeadgen;

/// <summary>
/// Paket B-META: Dashboard /settings/meta-leadgen [Keşfet: Form Fields] response.
/// Server-side Graph API hop (GET /v21.0/{pageId}/leadgen_forms?fields=id,name,
/// questions{id,label,type}) serialized into a Dashboard-friendly tree so the
/// SPA can populate the FieldIdMap editor's left-column dropdown without
/// handling raw Meta pagination or surfacing the page_access_token to the
/// browser (G4 2026-04-24 Q decision — dedicated backend endpoint over direct
/// frontend Graph calls).
/// </summary>
public sealed class MetaLeadgenDiscoverFormsResponse
{
    [JsonPropertyName("forms")]
    public List<MetaLeadgenForm> Forms { get; set; } = new();
}

public sealed class MetaLeadgenForm
{
    [JsonPropertyName("id")]        public string Id        { get; set; } = string.Empty;
    [JsonPropertyName("name")]      public string Name      { get; set; } = string.Empty;

    [JsonPropertyName("questions")]
    public List<MetaLeadgenFormQuestion> Questions { get; set; } = new();
}

public sealed class MetaLeadgenFormQuestion
{
    /// <summary>Meta question_id (stable per form). Key for FieldIdMap lookup.</summary>
    [JsonPropertyName("id")]     public string Id     { get; set; } = string.Empty;

    /// <summary>
    /// Operator-friendly question label (e.g. "Full Name", "Phone Number").
    /// Used as the dropdown option text so operators can map without memorizing
    /// Meta question_ids.
    /// </summary>
    [JsonPropertyName("label")]  public string Label  { get; set; } = string.Empty;

    /// <summary>
    /// Meta question type (e.g. "FULL_NAME", "PHONE", "EMAIL", "CUSTOM"). Used
    /// by the SPA to suggest a default canonical mapping (type=PHONE → phone,
    /// type=FULL_NAME → name, etc.) while still letting operators override.
    /// </summary>
    [JsonPropertyName("type")]   public string Type   { get; set; } = string.Empty;
}
