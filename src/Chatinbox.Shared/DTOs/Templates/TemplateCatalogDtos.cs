using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Templates;

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

    // FEAT-WTP (migration 019): operational grouping label; NULL for legacy rows.
    [JsonPropertyName("group_tag")]
    public string? GroupTag { get; set; }

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

    // FEAT-WTP: tenant-supplied grouping label for variant rotation (optional).
    [JsonPropertyName("group_tag")]
    public string? GroupTag { get; set; }

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

    // FEAT-WTP: update-only mutations send a non-null value to overwrite, null to leave
    // the existing group_tag untouched (sticky to prior value).
    [JsonPropertyName("group_tag")]
    public string? GroupTag { get; set; }

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

// ── Bulk Create (2026-04-18: Dashboard bulk import + pilot seed support) ────────

public sealed class TemplateBulkCreateRequest
{
    [JsonPropertyName("templates")]
    public List<TemplateCreateRequest> Templates { get; set; } = new();
}

public sealed class TemplateBulkCreateItemFailure
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = "";

    [JsonPropertyName("error_message")]
    public string ErrorMessage { get; set; } = "";
}

public sealed class TemplateBulkCreateResult
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("succeeded_count")]
    public int SucceededCount { get; set; }

    [JsonPropertyName("failed_count")]
    public int FailedCount { get; set; }

    [JsonPropertyName("succeeded")]
    public List<TemplateCatalogDto> Succeeded { get; set; } = new();

    [JsonPropertyName("failed")]
    public List<TemplateBulkCreateItemFailure> Failed { get; set; } = new();
}

// ────────────────────────────────────────────────────────────────────────────────

public sealed class TemplateCatalogFilter
{
    public string? Scope { get; set; }
    public string? TemplateType { get; set; }
    public string? Sector { get; set; }
    public string? Lang { get; set; }
    public string? Search { get; set; }
    public string[]? Tags { get; set; }
    // FEAT-WTP: tenant-scoped rotation pool lookup (variant pool by operational group).
    public string? GroupTag { get; set; }
    // FEAT-WTP: per-tenant lookup for Automation variant fetch (not used by Dashboard listings).
    public int? TenantId { get; set; }
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 20;
}
