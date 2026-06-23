namespace Chatinbox.WhatsAppAnalytics.Models;

// ── Sector Template Mining Request ──

public sealed class TemplateMineRequest
{
    public string Sector { get; set; } = "";
}

// ── Template Mining Result ──

public sealed class TemplateMineResult
{
    public string Sector { get; set; } = "";
    public int IntentsCreated { get; set; }
    public int FaqsCreated { get; set; }
    public int FlowsCreated { get; set; }
    public int ObjectionHandlersCreated { get; set; }
    public int FollowupTemplatesCreated { get; set; }
    public int OnboardingStepsCreated { get; set; }
    public long DurationMs { get; set; }
}

// ── Sector Templates Response (all 6 types) ──

public sealed class SectorTemplatesResponse
{
    public string Sector { get; set; } = "";
    public List<SectorIntentRecord> Intents { get; set; } = new();
    public List<SectorFaqRecord> Faqs { get; set; } = new();
    public List<SectorFlowRecord> Flows { get; set; } = new();
    public List<SectorObjectionHandlerRecord> ObjectionHandlers { get; set; } = new();
    public List<SectorFollowupTemplateRecord> FollowupTemplates { get; set; } = new();
    public List<SectorOnboardingStepRecord> OnboardingSteps { get; set; } = new();
}

// ── Intent Template (RI-4.1) ──

public sealed class SectorIntentRecord
{
    public long Id { get; set; }
    public string Sector { get; set; } = "";
    public string IntentName { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ExamplesJson { get; set; }
    public string? KeywordsJson { get; set; }
    public int Priority { get; set; }
    public int Frequency { get; set; }
    public float? ConversionRate { get; set; }
    public int SourceCount { get; set; }
    public bool IsActive { get; set; }
}

// ── FAQ Template (RI-4.2) ──

public sealed class SectorFaqRecord
{
    public long Id { get; set; }
    public string Sector { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string? Category { get; set; }
    public string? KeywordsJson { get; set; }
    public float EffectivenessScore { get; set; }
    public int SourceCount { get; set; }
    public bool IsActive { get; set; }
}

// ── Flow Template (RI-4.3) ──

public sealed class SectorFlowRecord
{
    public long Id { get; set; }
    public string Sector { get; set; } = "";
    public string FlowName { get; set; } = "";
    public string FlowType { get; set; } = "";
    public string? Description { get; set; }
    public string StagesJson { get; set; } = "[]";
    public string? DropOffAnalysisJson { get; set; }
    public float? ConversionRate { get; set; }
    public int SourceCount { get; set; }
    public bool IsActive { get; set; }
}

// ── Objection Handler Template (RI-4.4) ──

public sealed class SectorObjectionHandlerRecord
{
    public long Id { get; set; }
    public string Sector { get; set; } = "";
    public string ObjectionType { get; set; } = "";
    public string ObjectionLabel { get; set; } = "";
    public string ResponseTemplatesJson { get; set; } = "[]";
    public int TotalOccurrences { get; set; }
    public float RescueRate { get; set; }
    public bool IsActive { get; set; }
}

// ── Follow-up Template (RI-4.5) ──

public sealed class SectorFollowupTemplateRecord
{
    public long Id { get; set; }
    public string Sector { get; set; } = "";
    public string TemplateType { get; set; } = "";
    public string TemplateLabel { get; set; } = "";
    public string MessageTemplate { get; set; } = "";
    public int OptimalDelayHours { get; set; }
    public float RescueRate { get; set; }
    public int SourceCount { get; set; }
    public bool IsActive { get; set; }
}

// ── Onboarding Step (RI-4.6) ──

public sealed class SectorOnboardingStepRecord
{
    public long Id { get; set; }
    public string Sector { get; set; } = "";
    public int StepNumber { get; set; }
    public string Action { get; set; } = "";
    public string? Description { get; set; }
    public string? ExpectedImpact { get; set; }
    public string? DayRange { get; set; }
    public bool IsActive { get; set; }
}

// ── Bulk Mine (RI Faz 5) ──

public sealed class BulkMineRequest
{
    public List<string>? Sectors { get; set; }
}

public sealed class BulkMineResult
{
    public int TotalSectorsMined { get; set; }
    public List<TemplateMineResult> SectorResults { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public long DurationMs { get; set; }
}

public sealed class SectorProfile
{
    public string SectorKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float? BenchmarkF1 { get; set; }
    public bool IsActive { get; set; }
    public int IntentCount { get; set; }
    public int FaqCount { get; set; }
    public int FlowCount { get; set; }
    public int ObjectionHandlerCount { get; set; }
    public int FollowupTemplateCount { get; set; }
    public int OnboardingStepCount { get; set; }
    public int TotalTemplates { get; set; }
}
