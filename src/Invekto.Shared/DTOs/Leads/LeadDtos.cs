namespace Invekto.Shared.DTOs.Leads;

/// <summary>PKT-6B1 GR-3.13: Create/update lead request.</summary>
public sealed class LeadRequest
{
    public string Phone { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? Interest { get; set; }
    public string? AssignedTo { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Lead record for API responses.</summary>
public sealed class LeadResponse
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string Phone { get; set; } = "";
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string Source { get; set; } = "organic";
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? Interest { get; set; }
    public int Score { get; set; }
    public string PipelineStatus { get; set; } = "new";
    public string? AssignedTo { get; set; }
    public DateTime? LastContactAt { get; set; }
    public DateTime? NextFollowupAt { get; set; }
    public int FollowupCount { get; set; }
    public string? Notes { get; set; }
    public bool IsHot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Pipeline status transition request.</summary>
public sealed class LeadPipelineUpdateRequest
{
    public string PipelineStatus { get; set; } = "";
    public string? AssignedTo { get; set; }
    public string? Note { get; set; }
}

/// <summary>Lead activity record.</summary>
public sealed class LeadActivityResponse
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string ActivityType { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Funnel stats for lead conversion dashboard (GR-3.13.6).</summary>
public sealed class LeadFunnelStatsResponse
{
    public int TotalLeads { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
}

/// <summary>Valid pipeline statuses for leads.</summary>
public static class LeadPipelineStatuses
{
    public const string New = "new";
    public const string Contacted = "contacted";
    public const string Consultation = "consultation";
    public const string Appointment = "appointment";
    public const string Patient = "patient";
    public const string Lost = "lost";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        New, Contacted, Consultation, Appointment, Patient, Lost
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status);
}
