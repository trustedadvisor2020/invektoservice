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
    public const string BackendMessageLogQueryFailed = "INV-BE-010";      // SuperAdmin message log query failed
    public const string BackendTenantListQueryFailed = "INV-BE-011";     // Tenant list query failed
    public const string BackendTenantImpersonateFailed = "INV-BE-012";   // Tenant impersonate failed

    // Backend Wizard errors (INV-BE-020+)
    public const string BackendWizardSessionFailed = "INV-BE-020";     // Wizard session creation failed
    public const string BackendWizardAiUnavailable = "INV-BE-021";     // Wizard AI service unavailable
    public const string BackendWizardAiCommFailed = "INV-BE-022";      // Wizard AI communication failed
    public const string BackendWizardConfirmFailed = "INV-BE-023";     // Wizard confirm failed
    public const string BackendWizardInvalidPayload = "INV-BE-024";    // Wizard invalid payload

    // Backend Instance Management (INV-BE-030+)
    public const string BackendInstanceFetchFailed = "INV-BE-030";     // WapCRM instance fetch failed
    public const string BackendInstanceDisableBlocked = "INV-BE-031";  // Instance in use by flow, cannot disable

    // Backend Working Hours Settings (INV-BE-040+)
    public const string BackendWorkingHoursFetchFailed = "INV-BE-040";   // Working hours fetch failed
    public const string BackendWorkingHoursUpdateFailed = "INV-BE-041";  // Working hours update failed

    // Backend Onboarding Status (INV-BE-050+)
    public const string BackendOnboardingStatusFailed = "INV-BE-050";    // Onboarding status aggregation failed

    // Backend Sector (INV-BE-060+)
    public const string BackendSectorUpdateFailed = "INV-BE-060";
    public const string BackendSectorInvalidValue = "INV-BE-061";

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

    // Automation PKT-6A: Niche Foundation (INV-AT-025+)
    public const string AutomationKnowledgeIntentFetchFailed = "INV-AT-025";
    public const string AutomationVipDetectionFailed = "INV-AT-026";

    // Automation PKT-6B1: Return Deflection (INV-AT-027+)
    public const string AutomationReturnDeflectionFailed = "INV-AT-027";
    public const string AutomationReturnReasonClassifyFailed = "INV-AT-028";
    public const string AutomationCouponAssignFailed = "INV-AT-029";

    // Automation Webhook + Schedule Triggers (INV-AT-030+)
    public const string AutomationWebhookFlowNotFound = "INV-AT-030";
    public const string AutomationWebhookNotTriggerType = "INV-AT-031";
    public const string AutomationWebhookExecutionFailed = "INV-AT-032";
    public const string AutomationCronExpressionInvalid = "INV-AT-033";
    public const string AutomationScheduleExecutionFailed = "INV-AT-034";

    // Automation Instance Filtering (INV-AT-035+)
    public const string AutomationInstanceNotFound = "INV-AT-035";
    public const string AutomationInstanceDisabled = "INV-AT-036";
    public const string AutomationInstanceUnassigned = "INV-AT-037";

    // Automation Onboarding Stats (INV-AT-038)
    public const string AutomationOnboardingStatsFailed = "INV-AT-038"; // Onboarding stats query failed

    // Automation Execution Log (INV-AT-039+)
    public const string AutomationExecLogInsertFailed = "INV-AT-039";
    public const string AutomationExecLogUpdateFailed = "INV-AT-040";
    public const string AutomationExecLogQueryFailed = "INV-AT-041";

    // Automation Knowledge Search (INV-AT-042+)
    public const string AutomationKnowledgeSearchFailed = "INV-AT-042";
    public const string AutomationChunkSummarizationFailed = "INV-AT-043";

    // AgentAI errors (INV-AA-xxx)
    public const string AgentAIInvalidPayload = "INV-AA-001";
    public const string AgentAIReplyGenerationFailed = "INV-AA-002";
    public const string AgentAIIntentDetectionFailed = "INV-AA-003";
    public const string AgentAINoConversationContext = "INV-AA-004";
    public const string AgentAIClaudeTimeout = "INV-AA-005";
    public const string AgentAIInvalidFeedback = "INV-AA-006";
    public const string AgentAIKnowledgeUnavailable = "INV-AA-007";
    public const string AgentAILanguageDetectionFailed = "INV-AA-008";
    public const string AgentAIConversationSummaryFailed = "INV-AA-009";

    // AgentAI PKT-6B1: E-commerce Agent Assist (INV-AA-010+)
    public const string AgentAIOrderCardFetchFailed = "INV-AA-010";
    public const string AgentAIEscalationNoteFailed = "INV-AA-011";
    public const string AgentAIEcomReplyEnrichFailed = "INV-AA-012";

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
    public const string OutboundTemplateLangNotAvailable = "INV-OB-011";
    public const string OutboundNoTemplateForLanguage = "INV-OB-012";

    // Appointments errors (INV-AP-xxx) -- GR-2.4
    public const string AppointmentInvalidSlotPayload = "INV-AP-001";
    public const string AppointmentSlotNotFound = "INV-AP-002";
    public const string AppointmentInvalidBookingPayload = "INV-AP-003";
    public const string AppointmentSlotFullyBooked = "INV-AP-004";
    public const string AppointmentNotFound = "INV-AP-005";
    public const string AppointmentAlreadyCancelled = "INV-AP-006";
    public const string AppointmentInvalidDateTime = "INV-AP-007";
    public const string AppointmentBookingInPast = "INV-AP-008";
    public const string AppointmentReminderSendFailed = "INV-AP-009";
    public const string AppointmentOutboundUnavailable = "INV-AP-010";

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
    public const string KnowledgePhotoBlockedHealthTenant = "INV-KN-016";

    // Knowledge PKT-6A: Niche Foundation (INV-KN-017+)
    public const string KnowledgeIntentPatternsNotFound = "INV-KN-017";
    public const string KnowledgeIntentReadFailed = "INV-KN-018";

    // Knowledge Template System (INV-KN-019+)
    public const string TemplateNotFound = "INV-KN-019";
    public const string TemplateInvalidType = "INV-KN-020";
    public const string TemplateSlugConflict = "INV-KN-021";
    public const string TemplateScopeMismatch = "INV-KN-022";
    public const string TemplateNotPublished = "INV-KN-023";
    public const string TemplateAdoptionExists = "INV-KN-024";
    public const string TemplateAbTestInvalid = "INV-KN-025";
    public const string TemplateVersionNotFound = "INV-KN-026";
    public const string TemplateCannotDeleteAdopted = "INV-KN-027";
    public const string TemplateOnboardingFailed = "INV-KN-028";
    public const string TemplateSeedFailed = "INV-KN-029";
    public const string TemplateSuggestionNotFound = "INV-KN-030";
    public const string TemplateComparisonFailed = "INV-KN-031";
    public const string TemplateSuggestionInvalidStatus = "INV-KN-032";

    // Knowledge Intent CRUD (INV-KN-033+)
    public const string KnowledgeIntentCreateFailed = "INV-KN-033";
    public const string KnowledgeIntentUpdateFailed = "INV-KN-034";
    public const string KnowledgeIntentDeleteFailed = "INV-KN-035";

    // Knowledge Onboarding Stats (INV-KN-036)
    public const string KnowledgeOnboardingStatsFailed = "INV-KN-036"; // Onboarding stats query failed

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
    public const string WABenchmarkNotFound = "INV-WA-016";
    public const string WABenchmarkMssqlError = "INV-WA-017";
    public const string WABenchmarkInvalidConfig = "INV-WA-018";
    public const string WABenchmarkUnauthorized = "INV-WA-019";

    // Integrations errors (INV-IG-xxx) -- GR-3.4/3.6
    public const string IntegrationsInvalidAccountPayload = "INV-IG-001";
    public const string IntegrationsAccountNotFound = "INV-IG-002";
    public const string IntegrationsProviderSyncFailed = "INV-IG-003";
    public const string IntegrationsOrderNotFound = "INV-IG-004";
    public const string IntegrationsProviderConnectionFailed = "INV-IG-005";
    public const string IntegrationsInvalidOrderQuery = "INV-IG-006";
    public const string IntegrationsCargoTrackingUnavailable = "INV-IG-007";

    // Integrations PKT-6B1: Review Alerts + Stock Query (INV-IG-008+)
    public const string IntegrationsInvalidReviewWebhook = "INV-IG-008";
    public const string IntegrationsReviewAlertCreateFailed = "INV-IG-009";
    public const string IntegrationsStockQueryFailed = "INV-IG-010";

    // Outbound v2 errors (INV-OB-013+) -- GR-3.15/3.26/3.29
    public const string OutboundInvalidCampaignPayload = "INV-OB-013";
    public const string OutboundCampaignNotFound = "INV-OB-014";
    public const string OutboundCampaignAlreadyActive = "INV-OB-015";
    public const string OutboundConversionRecordFailed = "INV-OB-016";
    public const string OutboundAiPersonalizationUnavailable = "INV-OB-017";
    public const string OutboundConsentNotGiven = "INV-OB-018";
    public const string OutboundInvalidConsentPayload = "INV-OB-019";
    public const string OutboundDataDeletionFailed = "INV-OB-020";

    // Outbound PKT-6B1: E-com/Clinic triggers + Lead follow-up (INV-OB-021+)
    public const string OutboundEcomTriggerTemplateMissing = "INV-OB-021";
    public const string OutboundClinicTriggerTemplateMissing = "INV-OB-022";
    public const string OutboundLeadFollowUpFailed = "INV-OB-023";

    // Lead Management errors (INV-LD-xxx) -- GR-3.13
    public const string LeadInvalidPayload = "INV-LD-001";
    public const string LeadNotFound = "INV-LD-002";
    public const string LeadInvalidPipelineStatus = "INV-LD-003";
    public const string LeadScoringFailed = "INV-LD-004";
    public const string LeadFollowUpScheduleFailed = "INV-LD-005";
    public const string LeadInvalidActivityPayload = "INV-LD-006";
    public const string LeadFunnelQueryFailed = "INV-LD-007";
    public const string LeadHotAlertFailed = "INV-LD-008";

    // Attribution errors (INV-AD-xxx) -- GR-3.14
    public const string AttributionInvalidPayload = "INV-AD-001";
    public const string AttributionNotFound = "INV-AD-002";
    public const string AttributionInvalidCostEntry = "INV-AD-003";
    public const string AttributionCostNotFound = "INV-AD-004";
    public const string AttributionInvalidLeadStatus = "INV-AD-005";

    // Appointments Advanced errors (INV-AP-011+) -- GR-3.19
    public const string AppointmentInvalidWaitlistPayload = "INV-AP-011";
    public const string AppointmentWaitlistNotFound = "INV-AP-012";
    public const string AppointmentInvalidPricingPayload = "INV-AP-013";
    public const string AppointmentPricingNotFound = "INV-AP-014";
    public const string AppointmentCalendarSyncFailed = "INV-AP-015";

    // Treatment Lifecycle errors (INV-AP-016+) -- GR-3.20/3.41/3.43
    public const string LifecycleInvalidPayload = "INV-AP-016";
    public const string LifecycleNotFound = "INV-AP-017";
    public const string LifecycleAlreadyFinished = "INV-AP-018";
    public const string LifecycleInvalidType = "INV-AP-019";
    public const string LifecycleStepSendFailed = "INV-AP-020";

    // Metrics/Analytics errors (INV-MT-xxx) -- PKT-3
    public const string MetricsAggregationFailed = "INV-MT-001";
    public const string MetricsQueryFailed = "INV-MT-002";
    public const string MetricsInvalidDateRange = "INV-MT-003";

    // Marketing errors (INV-MK-xxx) -- GR-3.21/3.22
    public const string MarketingInvalidReviewPayload = "INV-MK-001";
    public const string MarketingReviewNotFound = "INV-MK-002";
    public const string MarketingInvalidReferralPayload = "INV-MK-003";
    public const string MarketingReferralNotFound = "INV-MK-004";
    public const string MarketingReferralCodeExists = "INV-MK-005";
    public const string MarketingInvalidTourismPayload = "INV-MK-006";
    public const string MarketingTourismLeadNotFound = "INV-MK-007";
    public const string MarketingInvalidTourismStatus = "INV-MK-008";
    public const string MarketingReviewStatsFailed = "INV-MK-009";
    public const string MarketingTourismStatsFailed = "INV-MK-010";

    // Marketing errors (INV-MK-xxx) -- GR-3.24 Review Rescue
    public const string MarketingInvalidRiskPayload = "INV-MK-011";
    public const string MarketingRiskNotFound = "INV-MK-012";
    public const string MarketingInvalidRiskStatus = "INV-MK-013";
    public const string MarketingRescueStatsFailed = "INV-MK-014";
    public const string MarketingInvalidTemplatePayload = "INV-MK-015";
    public const string MarketingTemplateNotFound = "INV-MK-016";

    // Marketing errors (INV-MK-xxx) -- GR-3.25 Multilingual Tourism
    public const string MarketingInvalidCatalogPayload = "INV-MK-017";
    public const string MarketingCatalogItemNotFound = "INV-MK-018";
    public const string MarketingInvalidConversationPayload = "INV-MK-019";
    public const string MarketingConversationNotFound = "INV-MK-020";
    public const string MarketingConversationStatsFailed = "INV-MK-021";
    public const string MarketingResponseGenerationFailed = "INV-MK-022";
    public const string MarketingClaudeUnavailable = "INV-MK-023";

    // Database errors (INV-DB-xxx)
    public const string DatabaseConnectionFailed = "INV-DB-001";
    public const string DatabaseQueryTimeout = "INV-DB-002";
    public const string DatabaseDuplicateEntry = "INV-DB-003";
}
