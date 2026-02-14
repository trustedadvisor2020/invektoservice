namespace Invekto.Shared.Constants;

/// <summary>
/// Service constants for InvektoServis platform.
/// Stage-0 + Phase 1 (GR-1.9 integration bridge)
/// </summary>
public static class ServiceConstants
{
    // Timeouts (Stage-0)
    public const int BackendToMicroserviceTimeoutMs = 600;
    public const int RetryCount = 0; // YASAK - Stage-0'da retry yok

    // GR-1.9: Integration callback retry settings
    public const int CallbackMaxRetries = 3;
    public const int CallbackBaseDelayMs = 500; // Exponential backoff: 500ms, 1s, 2s
    public const int CallbackTimeoutMs = 5000;  // 5s per callback attempt

    // Ports
    public const int ChatAnalysisPort = 7101;
    public const int BackendPort = 5000;
    // Phase 1 service ports (reserved)
    public const int AgentAIPort = 7105;
    public const int IntegrationsPort = 7106;
    public const int OutboundPort = 7107;
    public const int AutomationPort = 7108;
    // Phase 2 service ports
    public const int KnowledgePort = 7104;
    public const int WhatsAppAnalyticsPort = 7109;

    // Service names
    public const string BackendServiceName = "Invekto.Backend";
    public const string ChatAnalysisServiceName = "Invekto.ChatAnalysis";
    // Phase 1 service names (reserved)
    public const string AgentAIServiceName = "Invekto.AgentAI";
    public const string OutboundServiceName = "Invekto.Outbound";
    public const string AutomationServiceName = "Invekto.Automation";
    // Phase 2 service names
    public const string KnowledgeServiceName = "Invekto.Knowledge";
    public const string WhatsAppAnalyticsServiceName = "Invekto.WhatsAppAnalytics";

    // Log retention
    public const int LogRetentionDays = 30;

    // GR-1.9: Latency monitoring threshold
    public const int IntegrationLatencyThresholdMs = 200;
}
