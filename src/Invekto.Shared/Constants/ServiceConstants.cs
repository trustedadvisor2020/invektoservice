namespace Invekto.Shared.Constants;

/// <summary>
/// Stage-0 service constants as defined in invekto_stage0_kurulum_adimlari.txt
/// </summary>
public static class ServiceConstants
{
    // Timeouts (Stage-0)
    public const int BackendToMicroserviceTimeoutMs = 600;
    public const int RetryCount = 0; // YASAK - Stage-0'da retry yok

    // Ports
    public const int ChatAnalysisPort = 7101;
    public const int BackendPort = 5000;

    // Service names
    public const string BackendServiceName = "Invekto.Backend";
    public const string ChatAnalysisServiceName = "Invekto.ChatAnalysis";

    // Log retention
    public const int LogRetentionDays = 30;
}
