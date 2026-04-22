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

    // Ops/quicklogin errors (INV-OPS-xxx)
    public const string OpsQuickLoginInvalidTenant = "INV-OPS-001"; // ?tenant query non-numeric or negative

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

    // Backend RI Cross-Service Sync (INV-BE-070+)
    public const string BackendRiKnowledgeSyncFailed = "INV-BE-070";
    public const string BackendRiRescueTriggerFailed = "INV-BE-071";
    public const string BackendRiMarketingSyncFailed = "INV-BE-072";

    // Backend Plan CRUD (INV-BE-080+) — Faz 1 Paket 2
    public const string BackendPlanNotFound = "INV-BE-080";            // Plan tier not found
    public const string BackendPlanTierConflict = "INV-BE-081";        // Tier name already exists (409)
    public const string BackendPlanHasActiveTenants = "INV-BE-082";    // Cannot delete: active tenants on tier
    public const string BackendPlanFeaturesInvalid = "INV-BE-083";     // features_json invalid format
    public const string BackendTenantPlanUpdateFailed = "INV-BE-084";  // Tenant plan update failed
    // Backend Payment (INV-BE-085+) — QNB VPos
    public const string BackendPaymentInitFailed = "INV-BE-085";       // Payment initiation failed
    public const string BackendPaymentCallbackInvalid = "INV-BE-086";  // Payment callback invalid
    public const string BackendPaymentNotFound = "INV-BE-087";         // Payment record not found
    public const string BackendPaymentHistoryFailed = "INV-BE-088";    // Payment history query failed
    public const string BackendPaymentAmountInvalid = "INV-BE-089";    // Amount <= 0

    // Backend Translation Warmup (INV-BE-090+) — HFM-2
    public const string BackendTranslationWarmupInvalidPayload = "INV-BE-090";   // tenantId/texts/locales missing
    public const string BackendTranslationWarmupInvalidLocale = "INV-BE-091";    // Unsupported/unknown locale in body
    public const string BackendTranslationWarmupHttpFailure = "INV-BE-092";      // Gemma/Claude HTTP failure mid-batch
    public const string BackendTranslationWarmupInvalidState = "INV-BE-093";     // Translation service returned empty/invalid
    public const string BackendTranslationWarmupCancelled = "INV-BE-094";        // Client cancelled the warmup

    // FEAT-LIW: Lead Intake Webhook (INV-BE-100+) — Chunk A
    public const string LeadIntakeApiKeyInvalid = "INV-BE-100";         // X-Invekto-Api-Key missing or not recognized
    public const string LeadIntakeSourceSlugInvalid = "INV-BE-101";     // source_slug regex fail
    public const string LeadIntakeConsentMissing = "INV-BE-102";        // consent canonical not in field map or payload
    public const string LeadIntakeFieldMappingMissing = "INV-BE-103";   // required canonical field unresolved
    public const string LeadIntakePhoneInvalid = "INV-BE-104";          // libphonenumber parse/validate fail
    public const string LeadIntakeConsentNotTrue = "INV-BE-105";        // consent value resolved to non-true
    public const string LeadIntakeRateLimitExceeded = "INV-BE-106";     // sliding window 100/min per key
    public const string LeadIntakeTenantNotConfigured = "INV-BE-107";   // tenant_landing_settings row missing
    public const string LeadIntakeFieldMapMalformed = "INV-BE-108";     // tenant_landing_settings.landing_field_map JSONB unparseable (operator fix)
    public const string LeadIntakePayloadEmpty = "INV-BE-109";          // request body missing or fields map empty

    // FEAT-LIW Chunk B: WA-direct internal endpoint (Automation -> Backend service-to-service)
    public const string LeadIntakeInternalAuthInvalid = "INV-BE-110";   // X-Internal-Service-Token missing/mismatch (or InternalServices:SharedSecret unconfigured)
    public const string LeadIntakeWaDirectPhoneInvalid = "INV-BE-111";  // wa-direct payload phone missing/empty/E.164 normalize fail
    public const string LeadIntakeWaDirectUnknownTenant = "INV-BE-112"; // wa-direct payload tenant_id has no row in tenant_registry (defense-in-depth against caller bug)

    // FEAT-LIW Chunk C: Dashboard settings endpoints (INV-BE-113+)
    public const string LeadIntakeSettingsConcurrentModification = "INV-BE-113"; // updated_at row_version mismatch on rotate/revoke/fieldmap.save (another tab/operator updated)
    public const string LeadIntakeFieldMapRequiredMissing = "INV-BE-114";        // Field map save: phone or consent canonical not mapped, empty source, or duplicate canonical target
    public const string LeadIntakeFieldMapUnknownCanonical = "INV-BE-115";       // Field map save: canonical value outside allowlist (name/phone/email/consent/utm_*/referer/metadata)
    public const string LeadIntakeDryRunPayloadInvalid = "INV-BE-116";           // Dry-run payload JSON parse failure
    public const string LeadIntakeSettingsUnknownTenant = "INV-BE-117";          // LIW settings mutation (rotate/revoke/fieldmap): JWT-bound tenant_id has no row in tenant_registry (auth drift / stale test JWT); pre-check via TenantExistsAsync before opening settings tx

    // FEAT-MCC: Multi-City Campaign config (INV-BE-118..121)
    // Allocated AFTER INV-BE-117 (LIW Chunk C). Earlier feature spec drafted INV-BE-090..091
    // which collided with Translation (INV-BE-090..095) — see arch/errors.md and roadmap §2 audit.
    public const string CampaignConfigInvalid = "INV-BE-118";          // Validation failure: slug regex/uniqueness, max-campaigns cap, max cities/dates per campaign cap, date ordering, dates[].city not referencing a known city slug. Also surfaced from Automation when {{campaign.X}} placeholder cannot resolve post-edit.
    public const string CampaignWindowClosed = "INV-BE-119";           // Outbound dispatch bound to a campaign whose active window is in the future, in the past, or active=false. Fires only for campaign-bound dispatch (template contains {{campaign.*}} or caller passed an explicit slug).
    public const string CampaignSlugReserved = "INV-BE-120";           // Slug uses a reserved token ('primary'/'system'/'default'/'all').
    public const string CampaignConfigDbUnavailable = "INV-BE-121";    // tenant_settings.campaign_config DB read/write transient (Npgsql). Distinct from generic INV-BE-001 so dashboards isolate campaign-config storage outages.

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

    // Auth Plan Enforcement (INV-AUTH-005+) — Faz 1 Permission System
    public const string AuthFeatureDisabled = "INV-AUTH-005";     // Feature not in tenant's plan
    public const string AuthTierInsufficient = "INV-AUTH-006";    // Feature requires higher tier
    public const string AuthQuotaExceeded = "INV-AUTH-007";       // Quota limit reached

    // INMA token introspection (INV-AUTH-008+) — welcome-endpoint check
    public const string AuthInmaIntrospectionUnavailable = "INV-AUTH-008"; // Welcome endpoint network/transport fail
    public const string AuthTenantResolveFailed = "INV-AUTH-009";          // Lazy auto-provision tenant lookup/insert failure (CompanyCode → tenant_id)

    // FEAT-TFM MVP (INV-AUTH-010+) — explicit cross-tenant write defense
    public const string AuthCrossTenantBlocked = "INV-AUTH-010";           // 403: body-supplied tenant_id mismatches JWT claim (defensive guard)

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

    // Automation E-Commerce (INV-AT-044+)
    public const string AutomationEcommerceCallFailed = "INV-AT-044";
    public const string AutomationEcommerceTimeout = "INV-AT-045";

    // Automation Flow Versioning (INV-AT-046+)
    public const string AutomationVersionNotFound = "INV-AT-046";
    public const string AutomationVersionCreateFailed = "INV-AT-047";
    public const string AutomationRollbackFailed = "INV-AT-048";

    // Automation Flow Monitor (INV-AT-049+)
    public const string AutomationMonitorQueryFailed = "INV-AT-049";

    // Automation PKT-12: Review Rescue (INV-AT-050+)
    public const string AutomationRiskScoringFailed = "INV-AT-050";
    public const string AutomationRescueMarketingFailed = "INV-AT-051";
    public const string AutomationRescueAlertFailed = "INV-AT-052";
    public const string AutomationRescueDispatchFailed = "INV-AT-053";       // Template fetch or Outbound send failed
    public const string AutomationRescueMessageFailed = "INV-AT-054";       // Rescue message delivery failed
    public const string AutomationFollowUpQueryFailed = "INV-AT-055";      // Follow-up due query failed
    public const string AutomationFollowUpSendFailed = "INV-AT-056";       // Follow-up message send failed

    // G3: Template A/B Rotation
    public const string AutomationTemplateVariantInvalid = "INV-AT-057";   // text_variants JSON parse failed, fallback to text

    // G6: Flow Wait Persistence
    public const string AutomationFlowWaitPersistFailed = "INV-AT-058";    // flow_execution_state insert/update failed
    public const string AutomationFlowWaitResumeFailed = "INV-AT-059";     // Resume from persisted wait state failed
    public const string AutomationFlowWaitConfigInvalid = "INV-AT-060";    // action_wait_until node config invalid (no duration / out of bounds)

    // FEAT-WTP: Welcome Template Pack (faq rotation state upsert)
    public const string AutomationFaqRotationStateUpdateFailed = "INV-AT-061"; // leads.faq_rotation_state upsert failed, fallback to variant 0

    // HFM-1/HFM-2: Human-feel + Multi-language (INV-AT-062+)
    public const string AutomationChunkScheduleInvalid = "INV-AT-062";     // text_chunks JSON parse failed, fallback to legacy text
    public const string AutomationLocaleDetectFailed = "INV-AT-063";       // preferred_locale upsert failed or detect returned empty
    public const string AutomationFaqTranslationFailed = "INV-AT-064";     // AiFaq post-match translation hop failed, fallback to raw
    public const string AutomationIntentPromptResourceMissing = "INV-AT-065"; // IntentPrompts.{locale}.json resource not found

    // FEAT-WTP continued: variant pool fetch + JSONB parse
    public const string AutomationTemplateGroupFetchFailed = "INV-AT-066"; // Knowledge group_tag template fetch failed/timeout, fallback to inline text
    public const string AutomationFaqRotationStateMalformed = "INV-AT-067"; // leads.faq_rotation_state JSONB invalid shape, reset to empty

    // FEAT-LIW: Welcome flow enqueue (INV-AT-068) — triggered from Backend intake endpoint
    public const string AutomationWelcomeFlowSlugMissing = "INV-AT-068"; // welcome_flow_slug + 'welcome_default' both unavailable; lead created, enqueue skipped

    // FEAT-LIW Chunk B: Welcome flow real dispatch + WA direct intake hook
    public const string AutomationWelcomeFlowDefinitionMissing = "INV-AT-069"; // chatbot_flows row not found for tenant+slug (or inactive); lead created, dispatch skipped
    public const string AutomationBackendIntakeUnavailable = "INV-AT-070";     // AutomationOrchestrator wa-direct hook: Backend /api/internal/leads/intake/wa-direct unreachable; flow continues with leadId=null
    public const string AutomationWelcomeFlowDispatchFailed = "INV-AT-071";    // TriggerWelcomeFlowJob execution-time infra failure (DB error during lookup, parse/build error on flow_config, cancellation, InvalidOperationException during ExecuteAsync). Distinct from -069 (which is strictly "row not found").

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

    // Knowledge Website Indexing (INV-KN-037+)
    public const string KnowledgeWebsiteInvalidUrl = "INV-KN-037";
    public const string KnowledgeWebsiteSitemapFailed = "INV-KN-038";
    public const string KnowledgeWebsiteCrawlFailed = "INV-KN-039";

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

    // Insight Engine errors (INV-WA-020+) -- RI Faz 3
    public const string WAInsightMssqlError = "INV-WA-020";
    public const string WAInsightNoOutcomes = "INV-WA-021";
    public const string WAInsightNotFound = "INV-WA-022";
    public const string WAInsightComputeFailed = "INV-WA-023";

    // Nightly Batch errors (INV-WA-024+)
    public const string WADiscoveryFailed = "INV-WA-024";

    // RI Cross-Service errors (INV-WA-025+)
    public const string WARescueStatusUpdateFailed = "INV-WA-025";
    public const string WATemplateCreateFailed = "INV-WA-026";
    public const string WATemplateUpdateFailed = "INV-WA-027";

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

    // Integrations E-Commerce (INV-IG-011+)
    public const string IntegrationsEcomProviderNotFound = "INV-IG-011";
    public const string IntegrationsEcomProductQueryFailed = "INV-IG-012";
    public const string IntegrationsEcomCustomerQueryFailed = "INV-IG-013";
    public const string IntegrationsEcomOrderMutationFailed = "INV-IG-014";
    public const string IntegrationsEcomOAuthFailed = "INV-IG-015";
    public const string IntegrationsEcomGraphQlFailed = "INV-IG-016";

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

    // FEAT-J2: INMA opt-out outbox sync + MessageCategory (INV-OB-024+)
    public const string InmaOptOutSyncPending = "INV-OB-024";              // Outbox row enqueued, not yet pushed
    public const string InmaOptOutSyncFailed = "INV-OB-025";               // Max attempts exhausted or INMA 908 not-found
    public const string InmaOptOutSyncSkippedNoOp = "INV-OB-026";          // NoOp mode active, push deferred
    public const string ChatoperationBlockedMarketing = "INV-OB-027";      // INMA 906 chat-level marketing block
    public const string ChatoperationBlockedContact = "INV-OB-028";        // INMA 907 contact-level global block
    public const string NoInstanceForOptOut = "INV-OB-029";                // Manuel opt-out: last-known instance lookup returned null
    public const string TransactionalBypassAudit = "INV-OB-030";           // Informational: opt-out check bypassed for transactional event
    public const string MessageCategoryEnforcementFailed = "INV-OB-031";   // enforce_message_category=TRUE and event_name null/outside allow-list
    public const string OutboxDrainTriggered = "INV-OB-032";               // Audit: admin ops /outbox/retry-skipped endpoint invoked

    // FEAT-DMP: INMA DynamicMessage placeholder integration (INV-OB-033+)
    public const string DynamicFieldValidationFailed = "INV-OB-033";       // Pre-send validation: MessageText placeholder outside INMA allowlist OR INMA 900/902 response (empty fields / placeholder-text mismatch)
    public const string DynamicFieldUnsupported = "INV-OB-034";            // INMA 901: placeholder not supported by tenant (cf column not active)
    public const string DynamicCustomerNotFound = "INV-OB-035";            // INMA 903: phone/account not matched to any Customer row
    public const string DynamicFieldValueNull = "INV-OB-036";              // INMA 905: placeholder field exists but Customer row has NULL value
    public const string DynamicFieldsFetchFailed = "INV-OB-037";           // /api/dynamicfields INMA call failed (HTTP/timeout/malformed/Status:false) — upstream unreachable, transient
    public const string DynamicFieldsNotConfigured = "INV-OB-038";         // Tenant has no WapCRM secret key; Dashboard picker shows "Admin tenant ayarlarindan eklemelidir"

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
    public const string MarketingFollowUpQueryFailed = "INV-MK-024";       // Follow-up due query failed

    // Marketing errors (INV-MK-050..055) -- FEAT-EFS Drip Sequence (gap from 024 → 050 intentional;
    // reserved 025..049 for future PKT-12/13 follow-up rescue extensions per arch/quality-grades.md)
    public const string FollowupSequenceConfigInvalid = "INV-MK-050";    // Validation failure (slug/stages/cap/template_slug shape)
    public const string FollowupStageNotFound = "INV-MK-051";            // Scheduled stage missing during execution (sequence edited mid-flight)
    public const string FollowupOptOut = "INV-MK-052";                   // Opted-out lead skipped at execution-time guard
    public const string FollowupSequenceTooLong = "INV-MK-053";          // Cap exceeded (max 5 stages OR cumulative window > 30 days/minutes)
    public const string FollowupSequenceDisabled = "INV-MK-054";         // Sequence enabled=false at trigger time
    public const string FollowupRunCollision = "INV-MK-055";             // Lead already has an active scheduled sequence (idempotency guard)
    public const string FollowupStorageUnavailable = "INV-MK-056";       // Transient DB failure (Npgsql) — distinct from config-invalid; retry-safe class
    public const string FollowupUpstreamUnavailable = "INV-MK-057";      // Backend↔Marketing or Automation↔Marketing HTTP transient failure
    public const string FollowupReplyCheckFailed = "INV-MK-058";         // Reserved for deferred follow-up paket: inbound-reply pre-check DB failure inside the scheduling-site hook (NoReplyCheckJob will adopt this code when the auto-emit paket wires the concrete inbound source). Retained for forward-compat; currently unreferenced in MVP runtime code.

    // Backend Translation (INV-BE-090+)
    public const string BackendTranslationFailed = "INV-BE-090";            // Claude translation API call failed
    public const string BackendTranslationDetectFailed = "INV-BE-091";      // Language detection failed
    public const string BackendTranslationCacheError = "INV-BE-092";        // Translation cache read/write error
    public const string BackendTranslationUnsupportedLang = "INV-BE-093";   // Unsupported target language
    public const string BackendTranslationBatchTooLarge = "INV-BE-094";     // Batch size exceeds limit (max 50)
    public const string BackendTranslationInvalidText = "INV-BE-095";       // Empty or invalid source text

    // FEAT-TFM MVP: Tenant Field Mapping (INV-BE-096..099 + INV-BE-110)
    public const string FieldMappingInvalid = "INV-BE-096";              // Type/regex/source format invalid
    public const string FieldMappingReservedSemanticName = "INV-BE-097"; // Semantic name conflicts with InmaDynamicFieldKeys.Allowlist or leads core columns
    public const string FieldMappingEnumValueMissing = "INV-BE-098";     // Type=enum but enum_values null/empty
    public const string FieldMappingSourceOutOfRange = "INV-BE-099";     // Source not in cf1..cf10
    public const string FieldMappingDbUnavailable = "INV-BE-110";        // tenant_settings.field_mapping read/write DB fail (TFM-specific, distinguish from generic INV-BE-001)

    // WebChat errors (INV-WC-xxx)
    public const string WebChatInvalidVisitor = "INV-WC-001";
    public const string WebChatConversationNotFound = "INV-WC-002";
    public const string WebChatMessageSendFailed = "INV-WC-003";
    public const string WebChatAIReplyFailed = "INV-WC-004";
    public const string WebChatAIReplyTimeout = "INV-WC-005";
    public const string WebChatInvalidPayload = "INV-WC-006";
    public const string WebChatAuthLoginFailed = "INV-WC-007";
    public const string WebChatOperatorNotFound = "INV-WC-008";
    public const string WebChatConversationClosed = "INV-WC-009";
    public const string WebChatHubConnectionFailed = "INV-WC-010";

    // WebChat Automation Webhook (INV-WC-011+)
    public const string WebChatWebhookFailed = "INV-WC-011";
    public const string WebChatWebhookTimeout = "INV-WC-012";

    // VoiceAI errors (INV-VA-xxx) -- PKT-11
    public const string VoiceAINoAudioFile = "INV-VA-001";               // No audio file in request
    public const string VoiceAIAudioTooLarge = "INV-VA-002";             // Audio file exceeds size limit
    public const string VoiceAITranscriptionFailed = "INV-VA-003";       // Whisper API transcription failed
    public const string VoiceAIIntentForwardFailed = "INV-VA-004";       // ChatAnalysis intent forwarding failed
    public const string VoiceAITranscriptionLogFailed = "INV-VA-005";    // Transcription log DB insert failed
    public const string VoiceAIUnsupportedFormat = "INV-VA-006";         // Unsupported audio format

    // Hangfire Job Infrastructure (INV-JOB-xxx) -- G7
    public const string JobStorageConnectionFailed = "INV-JOB-001";   // Hangfire PG storage connection failed
    public const string JobHandlerUnresolved = "INV-JOB-002";         // DI could not resolve job handler
    public const string JobRegistrationConflict = "INV-JOB-003";      // Duplicate recurring job registration
    public const string JobDashboardUnauthorized = "INV-JOB-004";     // Dashboard access denied (non-superadmin)
    public const string JobExecutionFailed = "INV-JOB-005";           // Recurring job final failure (retries exhausted)
    public const string DbBackupPgDumpFailed = "INV-JOB-010";         // pg_dump exit code != 0 or stderr signals failure
    public const string DbBackupDiskSpaceInsufficient = "INV-JOB-011"; // Free disk below MinFreeDiskGb threshold
    public const string DbBackupBinaryNotFound = "INV-JOB-012";       // Configured PgDumpPath does not exist on disk
    public const string DbBackupConfigMissing = "INV-JOB-013";        // Required DbBackup configuration is missing

    // Database errors (INV-DB-xxx)
    public const string DatabaseConnectionFailed = "INV-DB-001";
    public const string DatabaseQueryTimeout = "INV-DB-002";
    public const string DatabaseDuplicateEntry = "INV-DB-003";
}
