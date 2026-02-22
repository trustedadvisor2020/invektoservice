using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Templates;

// ── Suggestion DTO ──────────────────────────────────────────

public sealed class TemplateSuggestionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("analysis_id")]
    public int AnalysisId { get; set; }

    [JsonPropertyName("suggestion_type")]
    public string SuggestionType { get; set; } = "";

    [JsonPropertyName("existing_template_id")]
    public int? ExistingTemplateId { get; set; }

    [JsonPropertyName("existing_template_name")]
    public string? ExistingTemplateName { get; set; }

    [JsonPropertyName("similarity_score")]
    public decimal? SimilarityScore { get; set; }

    [JsonPropertyName("suggested_content_json")]
    public object? SuggestedContentJson { get; set; }

    [JsonPropertyName("suggested_slug")]
    public string SuggestedSlug { get; set; } = "";

    [JsonPropertyName("suggested_name")]
    public string SuggestedName { get; set; } = "";

    [JsonPropertyName("suggested_type")]
    public string SuggestedType { get; set; } = "";

    [JsonPropertyName("source_data_json")]
    public object? SourceDataJson { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("reviewed_by")]
    public string? ReviewedBy { get; set; }

    [JsonPropertyName("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [JsonPropertyName("result_template_id")]
    public int? ResultTemplateId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

// ── Review Request ──────────────────────────────────────────

public sealed class TemplateSuggestionReviewRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "approved";

    [JsonPropertyName("edited_content_json")]
    public object? EditedContentJson { get; set; }

    [JsonPropertyName("reviewed_by")]
    public string ReviewedBy { get; set; } = "superadmin";
}

// ── Bulk Review Request ─────────────────────────────────────

public sealed class TemplateBulkReviewRequest
{
    [JsonPropertyName("ids")]
    public int[] Ids { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "approved";

    [JsonPropertyName("reviewed_by")]
    public string ReviewedBy { get; set; } = "superadmin";
}

// ── Extraction Request ──────────────────────────────────────

public sealed class TemplateExtractionRequest
{
    [JsonPropertyName("tenant_name")]
    public string TenantName { get; set; } = "";

    [JsonPropertyName("sector")]
    public string? Sector { get; set; }

    [JsonPropertyName("auto_confirm_threshold")]
    public decimal AutoConfirmThreshold { get; set; } = 0.85m;
}

// ── Comparison Result ───────────────────────────────────────

public sealed class TemplateCompareResult
{
    [JsonPropertyName("analysis_id")]
    public int AnalysisId { get; set; }

    [JsonPropertyName("tenant_name")]
    public string TenantName { get; set; } = "";

    [JsonPropertyName("new_count")]
    public int NewCount { get; set; }

    [JsonPropertyName("update_count")]
    public int UpdateCount { get; set; }

    [JsonPropertyName("confirm_count")]
    public int ConfirmCount { get; set; }

    [JsonPropertyName("total_clusters_processed")]
    public int TotalClustersProcessed { get; set; }

    [JsonPropertyName("total_intents_processed")]
    public int TotalIntentsProcessed { get; set; }

    [JsonPropertyName("suggestions")]
    public List<TemplateSuggestionDto> Suggestions { get; set; } = [];

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}
