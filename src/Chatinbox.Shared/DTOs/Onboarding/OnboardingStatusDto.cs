using System.Text.Json.Serialization;

namespace Chatinbox.Shared.DTOs.Onboarding;

/// <summary>
/// Computed onboarding progress for a tenant.
/// Read-only, aggregated from Knowledge + Automation + tenant_registry.
/// </summary>
public sealed class OnboardingStatusDto
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; init; }

    [JsonPropertyName("sector")]
    public string? Sector { get; init; }

    [JsonPropertyName("progress_pct")]
    public int ProgressPct { get; init; }

    [JsonPropertyName("steps")]
    public required List<OnboardingStepDto> Steps { get; init; }

    [JsonPropertyName("next_step")]
    public OnboardingNextStepDto? NextStep { get; init; }
}

public sealed class OnboardingStepDto
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

public sealed class OnboardingNextStepDto
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("action_url")]
    public required string ActionUrl { get; init; }

    [JsonPropertyName("hint")]
    public required string Hint { get; init; }
}

/// <summary>
/// Lightweight knowledge stats for onboarding computation.
/// Returned by Knowledge: GET /api/v1/knowledge/{tenantId}/onboarding-stats
/// </summary>
public sealed class KnowledgeOnboardingStatsDto
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; init; }

    [JsonPropertyName("template_adoption_count")]
    public int TemplateAdoptionCount { get; init; }

    [JsonPropertyName("active_faq_count")]
    public int ActiveFaqCount { get; init; }

    [JsonPropertyName("intent_pattern_count")]
    public int IntentPatternCount { get; init; }
}

/// <summary>
/// Lightweight automation stats for onboarding computation.
/// Returned by Automation: GET /api/v1/flows/{tenantId}/onboarding-stats
/// </summary>
public sealed class AutomationOnboardingStatsDto
{
    [JsonPropertyName("tenant_id")]
    public int TenantId { get; init; }

    [JsonPropertyName("total_flow_count")]
    public int TotalFlowCount { get; init; }

    [JsonPropertyName("active_flow_count")]
    public int ActiveFlowCount { get; init; }
}
