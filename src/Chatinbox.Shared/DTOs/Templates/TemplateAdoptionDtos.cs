using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Templates;

// ── Adoption DTO ────────────────────────────────────────────

public sealed class TemplateAdoptionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("template_name")]
    public string? TemplateName { get; set; }

    [JsonPropertyName("template_type")]
    public string? TemplateType { get; set; }

    [JsonPropertyName("adopted_version")]
    public int AdoptedVersion { get; set; }

    [JsonPropertyName("target_type")]
    public string TargetType { get; set; } = "";

    [JsonPropertyName("target_id")]
    public int? TargetId { get; set; }

    [JsonPropertyName("customized")]
    public bool Customized { get; set; }

    [JsonPropertyName("adopted_at")]
    public DateTime AdoptedAt { get; set; }
}

// ── Onboard Request ─────────────────────────────────────────

public sealed class TemplateOnboardRequest
{
    [JsonPropertyName("sector")]
    public string Sector { get; set; } = "eticaret";

    [JsonPropertyName("template_types")]
    public string[]? TemplateTypes { get; set; }
}

// ── Onboard Result ──────────────────────────────────────────

public sealed class TemplateOnboardResult
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("sector")]
    public string Sector { get; set; } = "";

    [JsonPropertyName("adopted_count")]
    public int AdoptedCount { get; set; }

    [JsonPropertyName("skipped_count")]
    public int SkippedCount { get; set; }

    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    [JsonPropertyName("adoptions")]
    public List<TemplateAdoptionDto> Adoptions { get; set; } = [];

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}

// ── Performance DTO ─────────────────────────────────────────

public sealed class TemplatePerformanceDto
{
    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("variant")]
    public string Variant { get; set; } = "A";

    [JsonPropertyName("metric_type")]
    public string MetricType { get; set; } = "";

    [JsonPropertyName("metric_value")]
    public decimal MetricValue { get; set; }

    [JsonPropertyName("period_start")]
    public DateTime PeriodStart { get; set; }
}

// ── AB Test DTO ─────────────────────────────────────────────

public sealed class TemplateAbTestDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tenant_id")]
    public int TenantId { get; set; }

    [JsonPropertyName("template_a_id")]
    public int TemplateAId { get; set; }

    [JsonPropertyName("template_b_id")]
    public int TemplateBId { get; set; }

    [JsonPropertyName("split_pct")]
    public int SplitPct { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "draft";

    [JsonPropertyName("winner_template_id")]
    public int? WinnerTemplateId { get; set; }

    [JsonPropertyName("min_sample_size")]
    public int MinSampleSize { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
