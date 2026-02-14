namespace Invekto.Shared.Constants;

/// <summary>
/// Error codes following INV-{SERVICE}-{NUMBER} pattern from arch/errors.md
/// </summary>
public static class ErrorCodes
{
    // General errors (INV-GEN-xxx)
    public const string GeneralUnknown = "INV-GEN-001";
    public const string GeneralTimeout = "INV-GEN-002";
    public const string GeneralValidation = "INV-GEN-003";

    // Backend errors (INV-BE-xxx)
    public const string BackendMicroserviceUnavailable = "INV-BE-001";
    public const string BackendMicroserviceTimeout = "INV-BE-002";
    public const string BackendMicroserviceError = "INV-BE-003";           // 5xx from microservice
    public const string BackendMicroserviceInvalidResponse = "INV-BE-004"; // Invalid/null JSON body
    public const string BackendMicroserviceClientError = "INV-BE-005";     // 4xx from microservice (client error)

    // ChatAnalysis errors (INV-CA-xxx)
    public const string ChatAnalysisInvalidPayload = "INV-CA-001";
    public const string ChatAnalysisProcessingFailed = "INV-CA-002";
    public const string ChatAnalysisWapCrmError = "INV-CA-003";
    public const string ChatAnalysisWapCrmTimeout = "INV-CA-004";
    public const string ChatAnalysisClaudeError = "INV-CA-005";
    public const string ChatAnalysisClaudeTimeout = "INV-CA-006";
    public const string ChatAnalysisNoMessages = "INV-CA-007";

    // Auth errors (INV-AUTH-xxx)
    public const string AuthTokenExpired = "INV-AUTH-001";
    public const string AuthTokenInvalid = "INV-AUTH-002";
    public const string AuthUnauthorized = "INV-AUTH-003";

    // Backward-compat aliases (pre-GR-1.9 names, do not use in new code)
    public const string AuthForbidden = AuthTokenInvalid;

    // Integration errors (INV-INT-xxx) -- GR-1.9
    public const string IntegrationWebhookInvalidPayload = "INV-INT-001";
    public const string IntegrationCallbackFailed = "INV-INT-002";
    public const string IntegrationUnknownEventType = "INV-INT-003";
    public const string IntegrationTenantNotFound = "INV-INT-004";

    // Automation errors (INV-AT-xxx) -- GR-1.1
    public const string AutomationInvalidFlowConfig = "INV-AT-001";
    public const string AutomationFlowNotFound = "INV-AT-002";
    public const string AutomationFaqNotFound = "INV-AT-003";
    public const string AutomationIntentDetectionFailed = "INV-AT-004";
    public const string AutomationSessionExpired = "INV-AT-005";
    public const string AutomationFlowValidationFailed = "INV-AT-006";
    public const string AutomationFlowNotFoundById = "INV-AT-007";
    public const string AutomationFlowActivationConflict = "INV-AT-008";
    public const string AutomationInvalidFlowVersion = "INV-AT-009";
    public const string AutomationInvalidApiKey = "INV-AT-010";
    public const string AutomationMaxLoopExceeded = "INV-AT-011";
    public const string AutomationUnknownNodeType = "INV-AT-012";
    public const string AutomationNoPendingInput = "INV-AT-013";
    public const string AutomationUnknownInputType = "INV-AT-014";
    public const string AutomationGraphValidationFailed = "INV-AT-015";
    public const string AutomationRequiredFieldMissing = "INV-AT-016";
    public const string AutomationExpressionFailed = "INV-AT-017";
    public const string AutomationSimulationSessionNotFound = "INV-AT-018";
    public const string AutomationSimulationSessionExpired = "INV-AT-019";
    public const string AutomationSimulationFlowNotFound = "INV-AT-020";
    public const string AutomationNodeExecutionFailed = "INV-AT-021";
    public const string AutomationApiCallSsrfBlocked = "INV-AT-022";
    public const string AutomationApiCallTimeout = "INV-AT-023";
    public const string AutomationApiCallHttpError = "INV-AT-024";

    // AgentAI errors (INV-AA-xxx)
    public const string AgentAIInvalidPayload = "INV-AA-001";
    public const string AgentAIReplyGenerationFailed = "INV-AA-002";
    public const string AgentAIIntentDetectionFailed = "INV-AA-003";
    public const string AgentAINoConversationContext = "INV-AA-004";
    public const string AgentAIClaudeTimeout = "INV-AA-005";
    public const string AgentAIInvalidFeedback = "INV-AA-006";

    // Outbound errors (INV-OB-xxx) -- GR-1.3
    public const string OutboundInvalidBroadcastPayload = "INV-OB-001";
    public const string OutboundTemplateNotFound = "INV-OB-002";
    public const string OutboundRateLimitExceeded = "INV-OB-003";
    public const string OutboundRecipientOptedOut = "INV-OB-004";
    public const string OutboundBroadcastNotFound = "INV-OB-005";
    public const string OutboundDeliveryStatusFailed = "INV-OB-006";
    public const string OutboundInvalidTemplatePayload = "INV-OB-007";
    public const string OutboundNoMatchingTriggerTemplate = "INV-OB-008";
    public const string OutboundMessageSendCallbackFailed = "INV-OB-009";
    public const string OutboundTooManyRecipients = "INV-OB-010";

    // Knowledge errors (INV-KN-xxx) -- GR-2.1
    public const string KnowledgeImportPathNotFound = "INV-KN-001";
    public const string KnowledgeImportParseError = "INV-KN-002";
    public const string KnowledgeImportDbError = "INV-KN-003";
    public const string KnowledgeSearchFailed = "INV-KN-004";
    public const string KnowledgeOpenAITimeout = "INV-KN-005";
    public const string KnowledgeOpenAIRateLimit = "INV-KN-006";
    public const string KnowledgeOpenAIError = "INV-KN-007";
    public const string KnowledgeFaqNotFound = "INV-KN-008";
    public const string KnowledgeInvalidRequest = "INV-KN-009";
    public const string KnowledgePgvectorMissing = "INV-KN-010";
    public const string KnowledgeFileTooLarge = "INV-KN-011";
    public const string KnowledgeInvalidFileType = "INV-KN-012";
    public const string KnowledgePdfExtractionFailed = "INV-KN-013";
    public const string KnowledgeDocumentNotFound = "INV-KN-014";
    public const string KnowledgeUploadFailed = "INV-KN-015";

    // WhatsApp Analytics errors (INV-WA-xxx) -- WA-5/6
    public const string WAAnalysisNotFound = "INV-WA-001";
    public const string WAAnalysisInProgress = "INV-WA-002";
    public const string WACsvParseError = "INV-WA-003";
    public const string WACsvTooLarge = "INV-WA-004";
    public const string WACsvInvalidHeader = "INV-WA-005";
    public const string WAPipelineStageError = "INV-WA-006";
    public const string WAClaudeApiError = "INV-WA-007";
    public const string WAClaudeParseError = "INV-WA-008";
    public const string WAEmbeddingError = "INV-WA-009";
    public const string WAInvalidDateRange = "INV-WA-010";
    public const string WAInvalidFilter = "INV-WA-011";
    public const string WAAnalysisDeleteFailed = "INV-WA-012";
    public const string WATenantMismatch = "INV-WA-013";
    public const string WAStorageError = "INV-WA-014";
    public const string WADatabaseError = "INV-WA-015";

    // Database errors (INV-DB-xxx)
    public const string DatabaseConnectionFailed = "INV-DB-001";
    public const string DatabaseQueryTimeout = "INV-DB-002";
    public const string DatabaseDuplicateEntry = "INV-DB-003";
}
