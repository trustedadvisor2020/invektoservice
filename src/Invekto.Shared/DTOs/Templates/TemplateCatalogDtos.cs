using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Templates;

// ── Response ────────────────────────────────────────────────

public sealed class TemplateCatalogDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "";

    [JsonPropertyName("sector")]
    public string? Sector { get; set; }

    [JsonPropertyName("tenant_id")]
    public int? TenantId { get; set; }

    [JsonPropertyName("parent_template_id")]
    public int? ParentTemplateId { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "tr";

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = [];

    [JsonPropertyName("content_json")]
    public object? ContentJson { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; }

    [JsonPropertyName("usage_count")]
    public int UsageCount { get; set; }

    [JsonPropertyName("confidence_score")]
    public decimal ConfidenceScore { get; set; }

    [JsonPropertyName("source_count")]
    public int SourceCount { get; set; }

    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("sources")]
    public List<TemplateSourceDto>? Sources { get; set; }
}

// ── Create Request ──────────────────────────────────────────

public sealed class TemplateCreateRequest
{
    [JsonPropertyName("template_type")]
    public string TemplateType { get; set; } = "faq";

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = "sector";

    [JsonPropertyName("sector")]
    public string? Sector { get; set; }

    [JsonPropertyName("tenant_id")]
    public int? TenantId { get; set; }

    [JsonPropertyName("parent_template_id")]
    public int? ParentTemplateId { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "tr";

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("content_json")]
    public object ContentJson { get; set; } = new { };

    [JsonPropertyName("created_by")]
    public string CreatedBy { get; set; } = "manual";
}

// ── Update Request ──────────────────────────────────────────

public sealed class TemplateUpdateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public string[]? Tags { get; set; }

    [JsonPropertyName("content_json")]
    public object? ContentJson { get; set; }

    [JsonPropertyName("is_published")]
    public bool? IsPublished { get; set; }

    [JsonPropertyName("change_summary")]
    public string? ChangeSummary { get; set; }
}

// ── Resolution Result ───────────────────────────────────────

public sealed class TemplateResolutionResult
{
    [JsonPropertyName("template")]
    public TemplateCatalogDto? Template { get; set; }

    [JsonPropertyName("source_scope")]
    public string? SourceScope { get; set; }

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }

    [JsonPropertyName("resolved")]
    public bool Resolved { get; set; }
}

// ── Version ─────────────────────────────────────────────────

public sealed class TemplateVersionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("content_json")]
    public object? ContentJson { get; set; }

    [JsonPropertyName("change_summary")]
    public string? ChangeSummary { get; set; }

    [JsonPropertyName("changed_by")]
    public string ChangedBy { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

// ── Source (provenance) ─────────────────────────────────────

public sealed class TemplateSourceDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("template_id")]
    public int TemplateId { get; set; }

    [JsonPropertyName("analysis_id")]
    public int AnalysisId { get; set; }

    [JsonPropertyName("tenant_name")]
    public string TenantName { get; set; } = "";

    [JsonPropertyName("contribution_type")]
    public string ContributionType { get; set; } = "new";

    [JsonPropertyName("sample_count")]
    public int SampleCount { get; set; }

    [JsonPropertyName("contributed_at")]
    public DateTime ContributedAt { get; set; }
}

// ── Clone Request ───────────────────────────────────────────

public sealed class TemplateCloneRequest
{
    [JsonPropertyName("target_scope")]
    public string TargetScope { get; set; } = "tenant";

    [JsonPropertyName("target_sector")]
    public string? TargetSector { get; set; }

    [JsonPropertyName("target_tenant_id")]
    public int? TargetTenantId { get; set; }

    [JsonPropertyName("new_slug")]
    public string? NewSlug { get; set; }
}

// ── List Query Params ───────────────────────────────────────

public sealed class TemplateCatalogFilter
{
    public string? Scope { get; set; }
    public string? TemplateType { get; set; }
    public string? Sector { get; set; }
    public string? Lang { get; set; }
    public string? Search { get; set; }
    public string[]? Tags { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
