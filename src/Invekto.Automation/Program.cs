using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Automation.Services;
using Invekto.Shared.Middleware;
using Invekto.Shared.Services;
using Invekto.Automation.Services.NodeHandlers;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.Contracts.Inma.Webhooks;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.DTOs.Onboarding;
using Invekto.Shared.DTOs.Returns;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.AutomationPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? "";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";

// JWT settings
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";

// Validate required config
if (string.IsNullOrEmpty(claudeApiKey))
    throw new InvalidOperationException("FATAL: Claude:ApiKey is not configured");
if (string.IsNullOrEmpty(pgConnStr))
    throw new InvalidOperationException("FATAL: ConnectionStrings:PostgreSQL is not configured");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey is not configured");

// Configure Kestrel + HTTPS if certificate is configured
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);

    var certPath = builder.Configuration["Kestrel:Certificate:Path"];
    var certPassword = builder.Configuration["Kestrel:Certificate:Password"];
    var httpsPort = builder.Configuration.GetValue("Kestrel:HttpsPort", 0);

    if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath) && httpsPort > 0)
    {
        options.ListenAnyIP(httpsPort, listenOptions =>
        {
            listenOptions.UseHttps(certPath, certPassword);
        });
    }
});

// Register logger
var logger = new JsonLinesLogger(ServiceConstants.AutomationServiceName, logPath);
builder.Services.AddSingleton(logger);

// Register log cleanup
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// Register JWT validator
var jwtSettings = new JwtSettings
{
    SecretKey = jwtSecretKey,
    Issuer = builder.Configuration["Jwt:Issuer"],
    Audience = builder.Configuration["Jwt:Audience"],
    ClockSkewSeconds = builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds", 60)
};
var jwtValidator = new JwtValidator(jwtSettings);
builder.Services.AddSingleton(jwtValidator);

// Register PostgreSQL connection factory
var pgFactory = new PostgresConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register repository
builder.Services.AddSingleton<AutomationRepository>();

// Register callback client
var callbackSettings = builder.Configuration.GetSection("Integration:Callback").Get<CallbackSettings>() ?? new CallbackSettings();
builder.Services.AddSingleton(callbackSettings);
builder.Services.AddHttpClient<MainAppCallbackClient>();

// Register services (v1)
builder.Services.AddSingleton<WorkingHoursChecker>();
builder.Services.AddSingleton<FlowEngine>();

// G3: Template A/B rotation (deterministic hash-based)
builder.Services.AddSingleton<Invekto.Shared.Services.ITemplateRotationService,
    Invekto.Shared.Services.HashBasedTemplateRotationService>();

// Register v2 node handlers (IMP-1: Strategy Pattern)
builder.Services.AddSingleton<INodeHandler, TriggerStartHandler>();
builder.Services.AddSingleton<INodeHandler, MessageTextHandler>();
builder.Services.AddSingleton<INodeHandler, MessageMenuHandler>();
builder.Services.AddSingleton<INodeHandler, LogicConditionHandler>();
builder.Services.AddSingleton<INodeHandler, LogicSwitchHandler>();
builder.Services.AddSingleton<INodeHandler, ActionDelayHandler>();
builder.Services.AddSingleton<INodeHandler, SetVariableHandler>();
builder.Services.AddSingleton<INodeHandler, ActionHandoffHandler>();
builder.Services.AddSingleton<INodeHandler, UtilityNoteHandler>();
builder.Services.AddSingleton<INodeHandler, AiIntentHandler>();
builder.Services.AddSingleton<INodeHandler, AiFaqHandler>();
builder.Services.AddSingleton<INodeHandler, ApiCallHandler>();
builder.Services.AddSingleton<INodeHandler, AiSentimentHandler>();
builder.Services.AddSingleton<INodeHandler, WebhookTriggerHandler>();
builder.Services.AddSingleton<INodeHandler, OutboundTriggerHandler>();
builder.Services.AddSingleton<INodeHandler, ScheduleTriggerHandler>();
builder.Services.AddSingleton<INodeHandler, CallFlowHandler>();
builder.Services.AddSingleton<INodeHandler, LogicWorkingHoursHandler>();
builder.Services.AddSingleton<INodeHandler, ActionAssignGroupHandler>();
builder.Services.AddSingleton<INodeHandler, EcommerceHandler>();

// Register HttpClientFactory for ApiCallHandler
builder.Services.AddHttpClient("ApiCallHandler");

// Register v2 services
builder.Services.AddSingleton<ExpressionEvaluator>();
builder.Services.AddSingleton<FlowEngineV2>();
builder.Services.AddSingleton<FlowValidator>();
builder.Services.AddSingleton<FlowMigrator>();

// Register IntentDetector with HttpClient
builder.Services.AddHttpClient<IntentDetector>((sp, client) =>
{
    // HttpClient is configured, IntentDetector gets it via DI
}).AddTypedClient((httpClient, sp) =>
{
    return new IntentDetector(httpClient, claudeApiKey, sp.GetRequiredService<JsonLinesLogger>());
});

// Register SentimentAnalyzer with HttpClient (same pattern as IntentDetector)
builder.Services.AddHttpClient<SentimentAnalyzer>((sp, client) =>
{
}).AddTypedClient((httpClient, sp) =>
{
    return new SentimentAnalyzer(httpClient, claudeApiKey, sp.GetRequiredService<JsonLinesLogger>());
});

// Register simulation engine (Phase 3b) + mock services
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulationEngine>());
builder.Services.AddSingleton<MockFaqMatcher>();
builder.Services.AddSingleton<MockIntentDetector>();
builder.Services.AddSingleton<MockSentimentAnalyzer>();

// Register CronSchedulerService (fires schedule_trigger flows on cron schedule)
builder.Services.AddSingleton<CronSchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CronSchedulerService>());

// PKT-6A: Register JwtGenerator for service-to-service auth
var jwtGenerator = new JwtGenerator(jwtSettings);
builder.Services.AddSingleton(jwtGenerator);

// PKT-6A: Register KnowledgeIntentClient (typed HttpClient with 3s timeout)
var knowledgeBaseUrl = builder.Configuration["Knowledge:BaseUrl"] ?? "http://localhost:7104";
var knowledgeTimeoutMs = builder.Configuration.GetValue<int>("Knowledge:IntentFetchTimeoutMs", 3000);
builder.Services.AddHttpClient<KnowledgeIntentClient>((sp, client) =>
{
    client.BaseAddress = new Uri(knowledgeBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(knowledgeTimeoutMs);
});

// Register KnowledgeSearchClient (typed HttpClient for ai_faq semantic search)
var knowledgeSearchTimeoutMs = builder.Configuration.GetValue<int>("Knowledge:SearchTimeoutMs", 5000);
builder.Services.AddHttpClient<KnowledgeSearchClient>((sp, client) =>
{
    client.BaseAddress = new Uri(knowledgeBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(knowledgeSearchTimeoutMs);
});

// Register IntegrationsClient (typed HttpClient for e-commerce operations)
var integrationsBaseUrl = builder.Configuration["Integrations:BaseUrl"] ?? $"http://localhost:{ServiceConstants.IntegrationsPort}";
var integrationsTimeoutMs = builder.Configuration.GetValue<int>("Integrations:TimeoutMs", 10000);
builder.Services.AddHttpClient<IntegrationsClient>((sp, client) =>
{
    client.BaseAddress = new Uri(integrationsBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(integrationsTimeoutMs);
});

// Register ChunkSummarizer (Claude Haiku for PDF chunk -> customer answer)
builder.Services.AddHttpClient<ChunkSummarizer>((sp, client) =>
{
}).AddTypedClient((httpClient, sp) =>
{
    return new ChunkSummarizer(httpClient, claudeApiKey, sp.GetRequiredService<JsonLinesLogger>());
});

// PKT-6A: Register VipDetectionService (with HttpClient for sales webhook)
builder.Services.AddHttpClient<VipDetectionService>();

// PKT-6B1: Register ReturnDeflectionService (GR-3.8 + GR-3.17)
builder.Services.AddSingleton<ReturnDeflectionService>();

// PKT-12: Register MarketingRescueClient (typed HttpClient for Review Rescue)
var marketingBaseUrl = builder.Configuration["Marketing:BaseUrl"] ?? $"http://localhost:{ServiceConstants.MarketingPort}";
var marketingTimeoutMs = builder.Configuration.GetValue<int>("Marketing:TimeoutMs", 10000);
builder.Services.AddHttpClient<MarketingRescueClient>((sp, client) =>
{
    client.BaseAddress = new Uri(marketingBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(marketingTimeoutMs);
});

// PKT-12 Faz 2: Register OutboundRescueClient (typed HttpClient for rescue message dispatch)
var outboundBaseUrl = builder.Configuration["Outbound:BaseUrl"] ?? $"http://localhost:{ServiceConstants.OutboundPort}";
var outboundTimeoutMs = builder.Configuration.GetValue<int>("Outbound:TimeoutMs", 10000);
builder.Services.AddHttpClient<OutboundRescueClient>((sp, client) =>
{
    client.BaseAddress = new Uri(outboundBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(outboundTimeoutMs);
});

// PKT-12 Faz 2: Register RescueDispatcher (orchestrates template fetch + Outbound send)
builder.Services.AddSingleton<RescueDispatcher>();

// PKT-12: Register ReviewRescueService (with HttpClient for supervisor webhook)
builder.Services.AddHttpClient<ReviewRescueService>();

// PKT-12 Faz 3: Register RescueFollowUpService (background hosted service, 4h timer)
builder.Services.AddSingleton<RescueFollowUpService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RescueFollowUpService>());

// PKT-6A: Register OnboardingService (with HttpClient for Knowledge seed API)
builder.Services.AddHttpClient<OnboardingService>((sp, client) =>
{
    client.BaseAddress = new Uri(knowledgeBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Register orchestrator
builder.Services.AddSingleton<AutomationOrchestrator>();

// Satisfy .NET 8 EndpointMiddleware authorization metadata check (no policies configured)
builder.Services.AddAuthorization();

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/webhook/", "/api/v1/flows/", "/api/v1/faq/", "/api/v1/simulation/", "/api/v1/onboarding/", "/api/v1/returns/");

// Faz 1: Plan-based feature guard (after JwtAuth sets TenantContext)
var planCache = new TenantPlanCache(pgConnStr, logger);
var usageService = new TenantUsageService(pgConnStr, logger);
app.UseFeatureGuard(planCache, logger,
    ("/api/v1/flows/", "FlowBuilder"),
    ("/api/v1/faq/", "FlowBuilder"),
    ("/api/v1/simulation/", "FlowBuilder"));
app.UseAuthorization();

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.AutomationServiceName)));
app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.AutomationServiceName));
});

// ============================================================
// Webhook endpoint (Main App -> Automation)
// ============================================================

app.MapPost("/api/v1/webhook/event", async (
    HttpContext ctx,
    AutomationOrchestrator orchestrator,
    JsonLinesLogger jsonLogger,
    IncomingWebhookEvent? webhookEvent) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (webhookEvent?.Messages == null || webhookEvent.Messages.Count == 0)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.IntegrationWebhookInvalidPayload, "messages array is required", requestId),
            statusCode: 400);
    }

    // Extract tenant from JWT or IP whitelist (stored by middleware)
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    var accepted = 0;
    var ignored = 0;
    var callbackClient = app.Services.GetRequiredService<MainAppCallbackClient>();

    foreach (var msg in webhookEvent.Messages)
    {
        // Only process incoming customer messages (not fromMe), skip group messages
        if (msg.FromMe || msg.IsGroupMessage)
        {
            ignored++;
            continue;
        }

        // Only process text messages
        if (!WebhookEventTypes.IsTextMessage(msg.Type))
        {
            ignored++;
            continue;
        }

        if (string.IsNullOrWhiteSpace(msg.Body))
        {
            ignored++;
            continue;
        }

        accepted++;
        var chatId = msg.ChatId ?? "-";

        // Faz 1 Paket 2: Message quota check
        var tenantPlan = await planCache.GetAsync(tenantContext.TenantId);
        if (tenantPlan != null)
        {
            var (allowed, current, limit) = await usageService.IncrementMessageAndCheckAsync(
                tenantContext.TenantId, tenantPlan.Quotas, CancellationToken.None);
            if (!allowed)
            {
                jsonLogger.StepWarn(
                    $"[{ErrorCodes.AuthQuotaExceeded}] Message quota exceeded for tenant {tenantContext.TenantId}: {current}/{limit}, chat {chatId}", requestId);
                ignored++;
                accepted--;
                continue;
            }
        }

        jsonLogger.StepInfo($"Processing message for tenant {tenantContext.TenantId}, chat {chatId}", requestId);

        // Capture loop variable for async closure
        var currentMsg = msg;
        var eventInstanceId = webhookEvent.InstanceId;
        _ = Task.Run(async () =>
        {
            try
            {
                var success = await orchestrator.ProcessMessageAsync(tenantContext, currentMsg, requestId, null, eventInstanceId, CancellationToken.None);
                if (!success)
                    jsonLogger.StepError($"Message processing failed for tenant {tenantContext.TenantId}, chat {chatId}", requestId);
            }
            catch (Exception ex)
            {
                jsonLogger.StepError($"Background processing exception: {ex.Message}", requestId);
                await SendErrorCallbackAsync(callbackClient, requestId, tenantContext.TenantId, chatId, currentMsg.Time,
                    $"Background processing error: {ex.Message}", null, jsonLogger);
            }
        });
    }

    return Results.Json(new
    {
        status = "accepted",
        request_id = requestId,
        accepted_count = accepted,
        ignored_count = ignored,
        instance_id = webhookEvent.InstanceId,
        message = "Event accepted for processing"
    }, statusCode: 202);
});

// ============================================================
// Error callback helper (for background processing failures)
// ============================================================

static async Task SendErrorCallbackAsync(
    MainAppCallbackClient callbackClient,
    string requestId, int tenantId, string chatId, long sequenceId,
    string errorMessage, string? callbackUrl, JsonLinesLogger logger)
{
    try
    {
        var errorCallback = new OutgoingCallback
        {
            RequestId = requestId,
            Action = CallbackActions.Error,
            TenantId = tenantId,
            ChatId = chatId,
            SequenceId = sequenceId,
            Data = new CallbackData { ErrorMessage = errorMessage },
            ProcessingTimeMs = 0
        };
        await callbackClient.SendCallbackAsync(errorCallback, callbackUrl);
    }
    catch (Exception ex)
    {
        logger.SystemWarn($"Failed to send error callback: {ex.Message}");
    }
}

// ============================================================
// Tenant validation helper
// ============================================================

static TenantContext? GetValidatedTenant(HttpContext ctx, int routeTenantId)
{
    var tenant = ctx.Items["TenantContext"] as TenantContext;
    if (tenant == null || tenant.TenantId != routeTenantId) return null;
    return tenant;
}

// ============================================================
// Flow management endpoints
// ============================================================

// GET /api/v1/flows/{tenantId} — List all flows for tenant (multi-flow)
app.MapGet("/api/v1/flows/{tenantId:int}", async (int tenantId, HttpContext ctx, AutomationRepository repo, FlowValidator validator, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var flows = await repo.ListFlowsAsync(tenantId);
        return Results.Ok(flows.Select(f =>
        {
            // Compute health score if v2 config is available
            int? healthScore = null;
            string[]? healthIssues = null;
            if (!string.IsNullOrEmpty(f.FlowConfigJson) && f.ConfigVersion == "2")
            {
                try
                {
                    var hs = validator.CalculateHealthScore(f.FlowConfigJson);
                    healthScore = hs.Score;
                    healthIssues = hs.Issues.Take(3).ToArray();
                }
                catch (System.Text.Json.JsonException jsonEx)
                {
                    jsonLogger.StepWarn($"Health score skipped for flow {f.FlowId}: config parse error — {jsonEx.Message}", "-");
                    healthScore = 0;
                    healthIssues = new[] { "Config parse hatasi — saglik skoru hesaplanamadi" };
                }
                catch (Exception ex)
                {
                    jsonLogger.StepWarn($"Health score failed for flow {f.FlowId}: {ex.Message}", "-");
                    healthScore = 0;
                    healthIssues = new[] { "Saglik skoru hesaplanamadi" };
                }
            }

            // Extract description from flow_config metadata
            string? flowDescription = null;
            if (!string.IsNullOrEmpty(f.FlowConfigJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(f.FlowConfigJson);
                    if (doc.RootElement.TryGetProperty("metadata", out var meta) &&
                        meta.TryGetProperty("description", out var desc) &&
                        desc.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        flowDescription = desc.GetString();
                    }
                }
                catch { /* ignore parse errors for description extraction */ }
            }

            return new
            {
                flow_id = f.FlowId,
                flow_name = f.FlowName,
                flow_description = flowDescription,
                is_active = f.IsActive,
                is_default = f.IsDefault,
                config_version = f.ConfigVersion,
                node_count = f.NodeCount,
                edge_count = f.EdgeCount,
                created_at = f.CreatedAt,
                updated_at = f.UpdatedAt,
                health_score = healthScore,
                health_issues = healthIssues,
                assigned_instances = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(f.AssignedInstancesJson ?? "[]")
            };
        }));
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow list failed: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", "-"), statusCode: 500);
    }
});

// GET /api/v1/flows/{tenantId}/{flowId} — Get single flow by ID
app.MapGet("/api/v1/flows/{tenantId:int}/{flowId:int}", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var flow = await repo.GetFlowByIdAsync(tenantId, flowId);
        if (flow == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", "-"), statusCode: 404);

        var result = new Dictionary<string, object?>
        {
            ["flow_id"] = flow.FlowId,
            ["tenant_id"] = flow.TenantId,
            ["flow_name"] = flow.FlowName,
            ["is_active"] = flow.IsActive,
            ["is_default"] = flow.IsDefault,
            ["flow_config"] = JsonSerializer.Deserialize<JsonElement>(flow.FlowConfigJson),
            ["created_at"] = flow.CreatedAt,
            ["updated_at"] = flow.UpdatedAt,
            ["wizard_status"] = flow.WizardStatus,
            ["current_version"] = flow.CurrentVersion
        };
        if (flow.WizardHistoryJson != null)
            result["wizard_history"] = JsonSerializer.Deserialize<JsonElement>(flow.WizardHistoryJson);

        return Results.Ok(result);
    }
    catch (JsonException ex)
    {
        jsonLogger.StepError($"Flow GET by ID JSON error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Akis verisi okunamadi. Lutfen tekrar deneyin.", "-"), statusCode: 500);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"Flow GET by ID DB error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Veritabani hatasi. Lutfen tekrar deneyin.", "-"), statusCode: 500);
    }
});

// POST /api/v1/flows/{tenantId} — Create new flow
app.MapPost("/api/v1/flows/{tenantId:int}", async (int tenantId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var flowName = root.TryGetProperty("flow_name", out var fn) ? fn.GetString() : null;
        if (string.IsNullOrWhiteSpace(flowName))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "flow_name is required", requestId), statusCode: 400);

        var flowConfig = root.TryGetProperty("flow_config", out var fc) ? fc.GetRawText() : "{}";
        var wizardStatus = root.TryGetProperty("wizard_status", out var ws) ? ws.GetString() : null;

        // Validate flow_config is valid JSON
        try { using var _ = JsonDocument.Parse(flowConfig); }
        catch (JsonException ex) { return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, $"flow_config is not valid JSON: {ex.Message}", requestId), statusCode: 400); }

        // Faz 1 Paket 2: max_flows quota check
        var tenantPlan = await planCache.GetAsync(tenantId);
        if (tenantPlan != null && tenantPlan.Quotas.MaxFlows >= 0)
        {
            var currentFlowCount = await repo.CountActiveFlowsAsync(tenantId);
            if (currentFlowCount >= tenantPlan.Quotas.MaxFlows)
            {
                jsonLogger.StepWarn(
                    $"[{ErrorCodes.AuthQuotaExceeded}] Flow quota exceeded for tenant {tenantId}: {currentFlowCount}/{tenantPlan.Quotas.MaxFlows}", requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthQuotaExceeded,
                        $"Maksimum akış sayısına ulaştınız ({tenantPlan.Quotas.MaxFlows}).", requestId),
                    statusCode: 429);
            }
        }

        var flowId = await repo.CreateFlowAsync(tenantId, flowName, flowConfig, wizardStatus);
        jsonLogger.StepInfo($"Flow created for tenant {tenantId}: flow_id={flowId}, name={flowName}, wizard={wizardStatus ?? "none"}", requestId);

        return Results.Json(new { flow_id = flowId, tenant_id = tenantId, flow_name = flowName, status = "created" }, statusCode: 201);
    }
    catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "23505") // unique_violation
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseDuplicateEntry, "Bu isimde bir akis zaten mevcut", requestId), statusCode: 409);
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow POST failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// PUT /api/v1/flows/{tenantId}/{flowId} — Update flow config (with health score)
app.MapPut("/api/v1/flows/{tenantId:int}/{flowId:int}", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, FlowValidator validator, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var flowConfig = root.TryGetProperty("flow_config", out var fc) ? fc.GetRawText() : null;
        if (string.IsNullOrEmpty(flowConfig))
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "flow_config is required", requestId), statusCode: 400);

        // Validate flow_config is valid JSON
        try { using var _ = JsonDocument.Parse(flowConfig); }
        catch (JsonException ex) { return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, $"flow_config is not valid JSON: {ex.Message}", requestId), statusCode: 400); }

        var flowName = root.TryGetProperty("flow_name", out var fn) ? fn.GetString() : null;
        var wizardHistory = root.TryGetProperty("wizard_history", out var wh) ? wh.GetRawText() : null;
        var wizardStatus = root.TryGetProperty("wizard_status", out var ws) ? ws.GetString() : null;

        var updated = await repo.UpdateFlowByIdAsync(tenantId, flowId, flowName, flowConfig, wizardHistory, wizardStatus);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        // Auto-version: create a version snapshot on every save (non-fatal)
        int newVersion = 0;
        try
        {
            newVersion = await repo.CreateFlowVersionAsync(tenantId, flowId, flowConfig!, "user");
            jsonLogger.StepInfo($"Flow version created for flow {flowId}: v{newVersion}", requestId);
        }
        catch (Npgsql.NpgsqlException versionEx)
        {
            jsonLogger.StepError($"[{ErrorCodes.AutomationVersionCreateFailed}] Flow version creation DB error for flow {flowId}: {versionEx.Message}", requestId);
        }

        // Non-blocking validation: calculate health score for response
        int healthScore = 0;
        string[] healthIssues = Array.Empty<string>();
        int healthErrors = 0;
        int healthWarnings = 0;
        try
        {
            var health = validator.CalculateHealthScore(flowConfig!);
            healthScore = health.Score;
            healthIssues = health.Issues.ToArray();
            healthErrors = health.ErrorCount;
            healthWarnings = health.WarningCount;
            if (healthErrors > 0)
                jsonLogger.StepWarn($"Flow {flowId} saved with {healthErrors} error(s), {healthWarnings} warning(s), health={healthScore}", requestId);
        }
        catch (JsonException valEx)
        {
            jsonLogger.StepWarn($"Flow {flowId} health score calculation failed (JSON parse): {valEx.Message}", requestId);
        }
        catch (InvalidOperationException valEx)
        {
            jsonLogger.StepWarn($"Flow {flowId} health score calculation failed (graph build): {valEx.Message}", requestId);
        }
        catch (ArgumentException valEx)
        {
            jsonLogger.StepWarn($"Flow {flowId} health score calculation failed (argument): {valEx.Message}", requestId);
        }

        jsonLogger.StepInfo($"Flow updated for tenant {tenantId}: flow_id={flowId}, health={healthScore}", requestId);

        // Sync instance-flow mapping from trigger_start node (non-fatal)
        try
        {
            using var cfgDoc = JsonDocument.Parse(flowConfig!);
            if (cfgDoc.RootElement.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    var nodeType = node.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (nodeType == "trigger_start" && node.TryGetProperty("data", out var data))
                    {
                        var instanceIds = new List<string>();
                        if (data.TryGetProperty("allowed_instance_ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var id in ids.EnumerateArray())
                            {
                                var val = id.GetString();
                                if (!string.IsNullOrEmpty(val)) instanceIds.Add(val);
                            }
                        }
                        await repo.SyncFlowInstanceMappingAsync(tenantId, flowId, instanceIds);
                        jsonLogger.StepInfo($"Instance mapping synced for flow {flowId}: {instanceIds.Count} instance(s)", requestId);
                        break;
                    }
                }
            }
        }
        catch (Exception syncEx)
        {
            jsonLogger.StepError($"Instance mapping sync failed for flow {flowId}: {syncEx.Message}", requestId);
        }

        return Results.Ok(new
        {
            flow_id = flowId,
            tenant_id = tenantId,
            status = "updated",
            current_version = newVersion,
            health_score = healthScore,
            health_errors = healthErrors,
            health_warnings = healthWarnings,
            health_issues = healthIssues
        });
    }
    catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "23505")
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseDuplicateEntry, "Bu isimde bir akis zaten mevcut", requestId), statusCode: 409);
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow PUT failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// DELETE /api/v1/flows/{tenantId}/{flowId} — Delete flow
app.MapDelete("/api/v1/flows/{tenantId:int}/{flowId:int}", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var (deleted, wasActive) = await repo.DeleteFlowByIdAsync(tenantId, flowId);

        if (wasActive)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowActivationConflict, "Aktif akis silinemez. Once pasife alin.", requestId), statusCode: 409);

        if (!deleted)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Flow deleted for tenant {tenantId}: flow_id={flowId}", requestId);
        return Results.Ok(new { flow_id = flowId, tenant_id = tenantId, status = "deleted" });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow DELETE failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// POST /api/v1/flows/{tenantId}/{flowId}/activate — Activate flow (validation gate: errors block activation)
app.MapPost("/api/v1/flows/{tenantId:int}/{flowId:int}/activate", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, FlowValidator validator, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        // Fetch flow config to validate before activation
        var flow = await repo.GetFlowByIdAsync(tenantId, flowId);
        if (flow == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        // Validation gate: block activation if flow has errors
        var validation = validator.Validate(flow.FlowConfigJson);
        if (!validation.IsValid)
        {
            jsonLogger.StepWarn($"Flow {flowId} activation blocked: {validation.Errors.Count} error(s)", requestId);
            var healthScore = Math.Max(100 - Math.Min(validation.Errors.Count * 15, 60) - Math.Min(validation.Warnings.Count * 5, 30), 0);
            var details = $"errors={validation.Errors.Count}, warnings={validation.Warnings.Count}, health_score={healthScore}; {string.Join("; ", validation.Errors)}";
            return Results.Json(ErrorResponse.Create(
                ErrorCodes.AutomationFlowValidationFailed,
                $"Akis aktif edilemez — {validation.Errors.Count} hata bulundu. Once hatalari duzeltip tekrar deneyin.",
                requestId,
                details
            ), statusCode: 422);
        }

        var activated = await repo.ActivateFlowAsync(tenantId, flowId);
        if (!activated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Flow activated for tenant {tenantId}: flow_id={flowId}, warnings={validation.Warnings.Count}", requestId);
        return Results.Ok(new
        {
            flow_id = flowId,
            tenant_id = tenantId,
            status = "activated",
            warnings = validation.Warnings
        });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow activate failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// POST /api/v1/flows/{tenantId}/{flowId}/deactivate — Deactivate flow
app.MapPost("/api/v1/flows/{tenantId:int}/{flowId:int}/deactivate", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var deactivated = await repo.DeactivateFlowAsync(tenantId, flowId);
        if (!deactivated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Flow deactivated for tenant {tenantId}: flow_id={flowId}", requestId);
        return Results.Ok(new { flow_id = flowId, tenant_id = tenantId, status = "deactivated" });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow deactivate failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// PATCH /api/v1/flows/{tenantId}/{flowId}/wizard-history — Update wizard history only
app.MapMethods("/api/v1/flows/{tenantId:int}/{flowId:int}/wizard-history", new[] { "PATCH" },
    async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var wizardHistory = root.TryGetProperty("wizard_history", out var wh) ? wh.GetRawText() : null;
        if (string.IsNullOrEmpty(wizardHistory))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "wizard_history is required", requestId), statusCode: 400);

        var updated = await repo.UpdateWizardHistoryAsync(tenantId, flowId, wizardHistory);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        return Results.Ok(new { flow_id = flowId, status = "wizard_history_updated" });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] Wizard history PATCH DB error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Veritabani hatasi", requestId), statusCode: 500);
    }
});

// ============================================================
// Flow versioning endpoints (FM-1a)
// ============================================================

// GET /api/v1/flows/{tenantId}/{flowId}/versions — List all versions
app.MapGet("/api/v1/flows/{tenantId:int}/{flowId:int}/versions", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var versions = await repo.GetFlowVersionsAsync(tenantId, flowId);
        return Results.Ok(new { flow_id = flowId, versions });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] Flow versions list DB error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Veritabani hatasi. Lutfen tekrar deneyin.", "-"), statusCode: 500);
    }
});

// GET /api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber} — Get specific version detail
app.MapGet("/api/v1/flows/{tenantId:int}/{flowId:int}/versions/{versionNumber:int}", async (int tenantId, int flowId, int versionNumber, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var version = await repo.GetFlowVersionAsync(tenantId, flowId, versionNumber);
        if (version == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationVersionNotFound, "Belirtilen surum bulunamadi", "-"), statusCode: 404);

        return Results.Ok(new
        {
            version.Id,
            version.FlowId,
            version.VersionNumber,
            version.CreatedAt,
            version.CreatedBy,
            flow_config = JsonSerializer.Deserialize<JsonElement>(version.FlowConfigJson)
        });
    }
    catch (JsonException ex)
    {
        jsonLogger.StepError($"Flow version detail JSON error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationVersionNotFound, "Surum verisi okunamadi", "-"), statusCode: 500);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] Flow version detail DB error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Veritabani hatasi. Lutfen tekrar deneyin.", "-"), statusCode: 500);
    }
});

// POST /api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}/rollback — Rollback to version
app.MapPost("/api/v1/flows/{tenantId:int}/{flowId:int}/versions/{versionNumber:int}/rollback", async (int tenantId, int flowId, int versionNumber, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var newVersion = await repo.RollbackFlowVersionAsync(tenantId, flowId, versionNumber);
        if (newVersion == -1)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationVersionNotFound, "Belirtilen surum bulunamadi", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Flow {flowId} rolled back to v{versionNumber}, new version v{newVersion}", requestId);
        return Results.Ok(new { flow_id = flowId, rolled_back_to = versionNumber, current_version = newVersion, status = "rolled_back" });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.AutomationRollbackFailed}] Flow rollback DB error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationRollbackFailed, "Surum geri alma basarisiz. Veritabani hatasi.", requestId), statusCode: 500);
    }
});

// ============================================================
// Flow validation endpoint (Phase 3a)
// ============================================================

// POST /api/v1/flows/validate — Validate a v2 flow config (graph validation)
app.MapPost("/api/v1/flows/validate", async (HttpContext ctx, FlowValidator validator, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var flowConfig = root.TryGetProperty("flow_config", out var fc) ? fc.GetRawText() : null;
        if (string.IsNullOrEmpty(flowConfig))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "flow_config is required", requestId), statusCode: 400);

        var result = validator.Validate(flowConfig);

        return Results.Ok(new
        {
            is_valid = result.IsValid,
            errors = result.Errors,
            warnings = result.Warnings
        });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow validation failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// POST /api/v1/flows/{tenantId}/{flowId}/migrate-v1 — Convert v1 config to v2 graph + save
app.MapPost("/api/v1/flows/{tenantId:int}/{flowId:int}/migrate-v1", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, FlowMigrator migrator, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        // Get current flow
        var flow = await repo.GetFlowByIdAsync(tenantId, flowId);
        if (flow == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        // Log v1 config before overwrite (safety)
        jsonLogger.StepInfo($"migrate-v1: Backing up v1 config for flow {flowId}, tenant {tenantId}. v1_config={flow.FlowConfigJson}", requestId);

        // Migrate
        var migrationResult = migrator.MigrateToV2(flow.FlowConfigJson);
        if (migrationResult == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "v1 → v2 migrasyon basarisiz (zaten v2 veya gecersiz config)", requestId), statusCode: 400);

        // Save to DB
        var updated = await repo.UpdateFlowByIdAsync(tenantId, flowId, null, migrationResult.V2ConfigJson);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Flow bulunamadi (save sirasinda)", requestId), statusCode: 404);

        jsonLogger.StepInfo($"migrate-v1: Flow {flowId} migrated to v2 for tenant {tenantId}", requestId);

        return Results.Ok(new
        {
            flow_id = flowId,
            tenant_id = tenantId,
            status = "migrated",
            warnings = migrationResult.Warnings,
            flow_config_v2 = JsonSerializer.Deserialize<JsonElement>(migrationResult.V2ConfigJson)
        });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"migrate-v1 failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// ============================================================
// Flow Execution Log endpoints
// ============================================================

// GET /api/v1/flows/{tenantId}/{flowId}/executions — List execution logs (paginated)
app.MapGet("/api/v1/flows/{tenantId:int}/{flowId:int}/executions", async (
    int tenantId, int flowId, HttpContext ctx,
    AutomationRepository repo, JsonLinesLogger jsonLogger,
    int limit = 50, int offset = 0) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    limit = Math.Clamp(limit, 1, 100);
    offset = Math.Max(offset, 0);

    try
    {
        var (items, total) = await repo.ListExecutionLogsAsync(tenantId, flowId, limit, offset);
        return Results.Ok(new { items, total });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.AutomationExecLogQueryFailed}] Execution log list DB error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationExecLogQueryFailed, "Akis yurutme loglari alinamadi.", "-"), statusCode: 500);
    }
});

// GET /api/v1/flows/{tenantId}/{flowId}/executions/{logId} — Single execution detail
app.MapGet("/api/v1/flows/{tenantId:int}/{flowId:int}/executions/{logId:long}", async (
    int tenantId, int flowId, long logId, HttpContext ctx,
    AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var detail = await repo.GetExecutionLogAsync(tenantId, flowId, logId);
        if (detail == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationExecLogQueryFailed, "Akis yurutme logu bulunamadi.", "-"), statusCode: 404);

        return Results.Ok(new
        {
            detail.Id,
            detail.FlowId,
            detail.ChatId,
            detail.Phone,
            detail.InstanceId,
            detail.TriggerMessage,
            detail.StartedAt,
            detail.CompletedAt,
            detail.Status,
            detail.NodeCount,
            node_trace = string.IsNullOrEmpty(detail.NodeTraceJson) ? (object)"[]" : JsonSerializer.Deserialize<JsonElement>(detail.NodeTraceJson),
            variables_final = string.IsNullOrEmpty(detail.VariablesFinalJson) ? null : (object?)JsonSerializer.Deserialize<JsonElement>(detail.VariablesFinalJson),
            detail.ErrorDetail
        });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.AutomationExecLogQueryFailed}] Execution log detail DB error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationExecLogQueryFailed, "Akis yurutme logu alinamadi.", "-"), statusCode: 500);
    }
    catch (System.Text.Json.JsonException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.AutomationExecLogQueryFailed}] Execution log JSON parse error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationExecLogQueryFailed, "Akis yurutme logu okunamadi.", "-"), statusCode: 500);
    }
});

// ============================================================
// Flow Monitor endpoints (cross-flow execution listing)
// ============================================================

// GET /api/v1/flows/{tenantId}/executions — Cross-flow execution list with filters
app.MapGet("/api/v1/flows/{tenantId:int}/executions", async (
    int tenantId, HttpContext ctx,
    AutomationRepository repo, JsonLinesLogger jsonLogger,
    int limit = 50, int offset = 0,
    int? flow_id = null, string? status = null,
    string? date_from = null, string? date_to = null, string? phone = null) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    limit = Math.Clamp(limit, 1, 100);
    offset = Math.Max(offset, 0);

    DateTime? dateFrom = null;
    DateTime? dateTo = null;
    if (!string.IsNullOrEmpty(date_from) && DateTime.TryParse(date_from, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var df))
        dateFrom = df.ToUniversalTime();
    if (!string.IsNullOrEmpty(date_to) && DateTime.TryParse(date_to, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
        dateTo = dt.ToUniversalTime();

    // Default: last 7 days if no date filter provided
    if (!dateFrom.HasValue && !dateTo.HasValue)
        dateFrom = DateTime.UtcNow.AddDays(-7);

    try
    {
        var (items, total) = await repo.ListMonitorExecutionsAsync(
            tenantId, flow_id, status, dateFrom, dateTo, phone, limit, offset);

        return Results.Ok(new
        {
            items = items.Select(e => new
            {
                id = e.Id,
                flow_id = e.FlowId,
                flow_name = e.FlowName,
                chat_id = e.ChatId,
                phone = e.Phone,
                trigger_message = e.TriggerMessage,
                started_at = e.StartedAt,
                completed_at = e.CompletedAt,
                status = e.Status,
                node_count = e.NodeCount
            }),
            total
        });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.AutomationMonitorQueryFailed}] Monitor query DB error: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationMonitorQueryFailed, "Monitor verileri alinamadi.", "-"), statusCode: 500);
    }
});

// ============================================================
// Simulation endpoints (Phase 3b — in-memory, no DB writes, no side-effects)
// ============================================================

// POST /api/v1/simulation/start — Start new simulation session
app.MapPost("/api/v1/simulation/start", async (HttpContext ctx, SimulationEngine sim, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var tenantId = root.TryGetProperty("tenant_id", out var tid) ? tid.GetInt32() : 0;
        var flowId = root.TryGetProperty("flow_id", out var fid) ? fid.GetInt32() : 0;

        if (tenantId <= 0 || flowId <= 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "tenant_id and flow_id are required", requestId), statusCode: 400);

        // Verify JWT tenant matches request tenant
        var tenant = ctx.Items["TenantContext"] as TenantContext;
        if (tenant == null || tenant.TenantId != tenantId)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match request tenant", requestId), statusCode: 403);

        var result = await sim.StartAsync(tenantId, flowId, ctx.RequestAborted);

        if (!result.Success)
        {
            var httpStatus = result.ErrorCode switch
            {
                ErrorCodes.AutomationSimulationFlowNotFound => 404,
                ErrorCodes.AutomationInvalidFlowConfig => 400,
                ErrorCodes.AutomationGraphValidationFailed => 400,
                _ => 500
            };
            return Results.Json(ErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!, requestId), statusCode: httpStatus);
        }

        return Results.Ok(new
        {
            session_id = result.SessionId,
            messages = result.Messages.Select(m => new { role = m.Role, text = m.Text }),
            current_node_id = result.CurrentNodeId,
            variables = result.Variables,
            execution_path = result.ExecutionPath,
            status = result.Status,
            pending_input = result.PendingInput != null
                ? new { type = result.PendingInput.Type, options = result.PendingInput.Options }
                : null
        });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Simulation start failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// POST /api/v1/simulation/step — Send user message to simulation
app.MapPost("/api/v1/simulation/step", async (HttpContext ctx, SimulationEngine sim, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var sessionId = root.TryGetProperty("session_id", out var sid) ? sid.GetString() : null;
        var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : null;

        if (string.IsNullOrEmpty(sessionId))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "session_id is required", requestId), statusCode: 400);
        if (string.IsNullOrEmpty(message))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "message is required", requestId), statusCode: 400);

        // Verify JWT tenant owns this simulation session (only block if session exists but belongs to different tenant)
        var tenant = ctx.Items["TenantContext"] as TenantContext;
        var sessionTenant = sim.GetSessionTenantId(sessionId);
        if (sessionTenant != null && (tenant == null || tenant.TenantId != sessionTenant))
            return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match session tenant", requestId), statusCode: 403);
        // If sessionTenant == null, session not found/expired — let StepAsync return proper INV-AT-018/019

        var result = await sim.StepAsync(sessionId, message, ctx.RequestAborted);

        if (!result.Success)
        {
            var httpStatus = result.ErrorCode switch
            {
                ErrorCodes.AutomationSimulationSessionNotFound => 404,
                ErrorCodes.AutomationSimulationSessionExpired => 410,
                ErrorCodes.AutomationNoPendingInput => 400,
                _ => 500
            };
            return Results.Json(ErrorResponse.Create(result.ErrorCode!, result.ErrorMessage!, requestId), statusCode: httpStatus);
        }

        return Results.Ok(new
        {
            messages = result.Messages.Select(m => new { role = m.Role, text = m.Text }),
            current_node_id = result.CurrentNodeId,
            variables = result.Variables,
            execution_path = result.ExecutionPath,
            status = result.Status,
            is_terminal = result.IsTerminal,
            pending_input = result.PendingInput != null
                ? new { type = result.PendingInput.Type, options = result.PendingInput.Options }
                : null
        });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Simulation step failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// DELETE /api/v1/simulation/{sessionId} — Cleanup simulation session
app.MapDelete("/api/v1/simulation/{sessionId}", (string sessionId, HttpContext ctx, SimulationEngine sim) =>
{
    // Verify JWT tenant owns this simulation session (skip if session already expired/removed)
    var tenant = ctx.Items["TenantContext"] as TenantContext;
    var sessionTenant = sim.GetSessionTenantId(sessionId);
    if (sessionTenant != null && (tenant == null || tenant.TenantId != sessionTenant))
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match session tenant", "-"), statusCode: 403);

    sim.Remove(sessionId);
    return Results.StatusCode(204);
});

// ============================================================
// FAQ management endpoints
// ============================================================

app.MapGet("/api/v1/faq/{tenantId:int}", async (int tenantId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var faqs = await repo.GetActiveFaqsAsync(tenantId);
        return Results.Ok(new { tenant_id = tenantId, count = faqs.Count, items = faqs });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"FAQ GET failed: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", "-"), statusCode: 500);
    }
});

app.MapPost("/api/v1/faq/{tenantId:int}", async (int tenantId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var question = root.TryGetProperty("question", out var q) ? q.GetString() : null;
        var answer = root.TryGetProperty("answer", out var a) ? a.GetString() : null;

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(answer))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "question and answer are required", requestId), statusCode: 400);

        var keywords = Array.Empty<string>();
        if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
            keywords = kw.EnumerateArray().Select(k => k.GetString() ?? "").Where(k => !string.IsNullOrEmpty(k)).ToArray();

        var sortOrder = root.TryGetProperty("sort_order", out var so) ? so.GetInt32() : 0;

        var id = await repo.InsertFaqAsync(tenantId, question, answer, keywords, sortOrder);
        jsonLogger.StepInfo($"FAQ created for tenant {tenantId}: id={id}", requestId);

        return Results.Json(new { id, tenant_id = tenantId, status = "created" }, statusCode: 201);
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"FAQ POST failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

app.MapPut("/api/v1/faq/{tenantId:int}/{id:int}", async (int tenantId, int id, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var question = root.TryGetProperty("question", out var q) ? q.GetString() : null;
        var answer = root.TryGetProperty("answer", out var a) ? a.GetString() : null;

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(answer))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "question and answer are required", requestId), statusCode: 400);

        var keywords = Array.Empty<string>();
        if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
            keywords = kw.EnumerateArray().Select(k => k.GetString() ?? "").Where(k => !string.IsNullOrEmpty(k)).ToArray();

        var isActive = !root.TryGetProperty("is_active", out var ia) || ia.GetBoolean();

        var updated = await repo.UpdateFaqAsync(id, tenantId, question, answer, keywords, isActive);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFaqNotFound, "FAQ entry not found", requestId), statusCode: 404);

        jsonLogger.StepInfo($"FAQ updated for tenant {tenantId}: id={id}", requestId);
        return Results.Ok(new { id, tenant_id = tenantId, status = "updated" });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"FAQ PUT failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

app.MapDelete("/api/v1/faq/{tenantId:int}/{id:int}", async (int tenantId, int id, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var deleted = await repo.DeleteFaqAsync(id, tenantId);
        if (!deleted)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFaqNotFound, "FAQ entry not found", "-"), statusCode: 404);

        jsonLogger.StepInfo($"FAQ deleted for tenant {tenantId}: id={id}", "-");
        return Results.Ok(new { id, tenant_id = tenantId, status = "deleted" });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"FAQ DELETE failed: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", "-"), statusCode: 500);
    }
});

// ============================================================
// PKT-6A: Onboarding endpoint (seed tenant intents from sector templates)
// ============================================================

app.MapPost("/api/v1/onboarding/{tenantId:int}/seed-intents", async (int tenantId, HttpContext ctx, OnboardingService onboarding, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var sector = root.TryGetProperty("sector", out var s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(sector))
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector is required (eticaret, dis_klinik, estetik)", requestId), statusCode: 400);

        // Generate service JWT for Knowledge API call
        var jwtGen = app.Services.GetRequiredService<JwtGenerator>();
        var serviceJwt = jwtGen.GenerateServiceToken(tenantId);

        var count = await onboarding.SeedTenantIntentsAsync(tenantId, sector, serviceJwt, ctx.RequestAborted);

        if (count < 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationKnowledgeIntentFetchFailed, "Knowledge service seed call failed", requestId), statusCode: 502);

        jsonLogger.StepInfo($"Onboarding: seeded {count} intents for tenant {tenantId}, sector={sector}", requestId);
        return Results.Ok(new { tenant_id = tenantId, sector, intents_seeded = count, status = "seeded" });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId), statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Onboarding seed failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId), statusCode: 500);
    }
});

// ============================================================
// Return Deflection endpoints (PKT-6B1: GR-3.8 + GR-3.17)
// ============================================================

app.MapGet("/api/v1/returns/{tenantId:int}/stats", async (
    int tenantId, HttpContext ctx,
    ReturnDeflectionService deflectionService,
    AutomationRepository repo,
    JsonLinesLogger jsonLogger,
    string? from, string? to) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var fromDate = !string.IsNullOrEmpty(from) ? DateTime.Parse(from) : DateTime.UtcNow.AddDays(-30);
        var toDate = !string.IsNullOrEmpty(to) ? DateTime.Parse(to) : DateTime.UtcNow;

        var stats = await repo.GetReturnDeflectionStatsAsync(tenantId, fromDate, toDate, ctx.RequestAborted);
        return Results.Ok(stats);
    }
    catch (FormatException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid date format. Use yyyy-MM-dd", requestId), statusCode: 400);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"Return deflection stats DB error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationReturnDeflectionFailed, "Stats query failed", requestId), statusCode: 500);
    }
});

app.MapPost("/api/v1/returns/{tenantId:int}/{deflectionId:int}/deflected", async (
    int tenantId, int deflectionId, HttpContext ctx,
    ReturnDeflectionService deflectionService,
    JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        var revenueSaved = root.TryGetProperty("revenue_saved", out var r) && r.ValueKind == JsonValueKind.Number
            ? r.GetDecimal() : (decimal?)null;

        var updated = await deflectionService.MarkDeflectedAsync(tenantId, deflectionId, revenueSaved, ctx.RequestAborted);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationReturnDeflectionFailed, "Deflection not found", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Return deflection marked: tenant={tenantId}, id={deflectionId}, revenue={revenueSaved}", requestId);
        return Results.Ok(new { deflection_id = deflectionId, was_deflected = true, revenue_saved = revenueSaved });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId), statusCode: 400);
    }
});

// ============================================================
// Webhook trigger endpoint (external systems -> Automation)
// /api/v1/webhooks/ (plural) — intentionally outside JWT middleware prefix (/api/v1/webhook/ singular)
// No auth — security through URL obscurity (Q's decision)
// ============================================================

app.MapPost("/api/v1/webhooks/{tenantId:int}/{flowId:int}", async (
    int tenantId, int flowId,
    HttpContext ctx,
    AutomationRepository repo,
    FlowEngineV2 engineV2,
    JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    // Read raw JSON body
    string payloadJson;
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        payloadJson = await reader.ReadToEndAsync(ctx.RequestAborted);
    }
    catch (OperationCanceledException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralTimeout, "Request cancelled", requestId),
            statusCode: 408);
    }

    if (string.IsNullOrWhiteSpace(payloadJson))
    {
        jsonLogger.StepWarn($"Webhook: empty payload for tenant={tenantId}, flow={flowId}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Request body is required", requestId),
            statusCode: 400);
    }

    // Validate JSON syntax
    try { using var _ = JsonDocument.Parse(payloadJson); }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Request body must be valid JSON", requestId),
            statusCode: 400);
    }

    // Load flow (tenant-scoped)
    FlowDetail? flowDetail;
    try
    {
        flowDetail = await repo.GetFlowByIdAsync(tenantId, flowId, ctx.RequestAborted);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"Webhook: DB error loading flow {flowId}: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Database error", requestId),
            statusCode: 500);
    }

    if (flowDetail == null || !flowDetail.IsActive)
    {
        jsonLogger.StepWarn($"Webhook: flow not found or inactive tenant={tenantId}, flow={flowId}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AutomationWebhookFlowNotFound, "Webhook flow not found or inactive", requestId),
            statusCode: 404);
    }

    // Build immutable graph
    var graph = FlowGraphV2.Build(flowDetail.FlowConfigJson);
    if (graph == null)
    {
        jsonLogger.StepError($"Webhook: invalid flow config for flow={flowId}, tenant={tenantId}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "Flow config is invalid", requestId),
            statusCode: 400);
    }

    // Verify trigger type is webhook_trigger
    if (graph.TriggerStart == null || graph.TriggerStart.Type != "webhook_trigger")
    {
        jsonLogger.StepWarn(
            $"Webhook: flow {flowId} trigger is '{graph.TriggerStart?.Type ?? "null"}', not webhook_trigger", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AutomationWebhookNotTriggerType, "Flow trigger type is not webhook_trigger", requestId),
            statusCode: 400);
    }

    // Synthetic chat_id (never collides with real WhatsApp chat_ids)
    var syntheticChatId = $"webhook_{Interlocked.Decrement(ref WebhookState.ChatIdCounter)}";
    var sessionId = -1;

    try
    {
        sessionId = await repo.CreateSessionAsync(
            tenantId, syntheticChatId, phone: null, currentNode: "v2_active", ctx.RequestAborted);

        // Initial state with __webhook_payload injected
        var state = new SessionStateV2
        {
            CurrentNodeId = graph.TriggerStart.Id,
            Status = "active",
            Variables = { ["__webhook_payload"] = payloadJson }
        };

        var result = await engineV2.ExecuteAsync(graph, state, ctx.RequestAborted, tenantId: tenantId);

        var finalStatus = result.IsTerminal
            ? (result.NeedsHandoff ? "handed_off" : (result.ErrorCode != null ? "error" : "completed"))
            : "completed"; // Webhook flows don't wait for input

        await repo.EndSessionAsync(sessionId, finalStatus, ctx.RequestAborted);

        jsonLogger.StepInfo(
            $"Webhook executed: tenant={tenantId}, flow={flowId}, status={finalStatus}, messages={result.Messages.Count}",
            requestId);

        return Results.Ok(new
        {
            success = result.ErrorCode == null,
            messages = result.Messages,
            status = finalStatus,
            execution_path = state.ExecutionPath,
            error_code = result.ErrorCode
        });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError(
            $"[{ErrorCodes.AutomationWebhookExecutionFailed}] Webhook DB error: tenant={tenantId}, flow={flowId}: {ex.Message}",
            requestId);

        if (sessionId > 0)
        {
            try { await repo.EndSessionAsync(sessionId, "error", CancellationToken.None); }
            catch (Npgsql.NpgsqlException ex2) { jsonLogger.StepWarn($"Webhook: secondary EndSession failed for session {sessionId}: {ex2.Message}", requestId); }
        }

        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AutomationWebhookExecutionFailed, "Webhook execution failed", requestId),
            statusCode: 500);
    }
    catch (OperationCanceledException)
    {
        if (sessionId > 0)
        {
            try { await repo.EndSessionAsync(sessionId, "error", CancellationToken.None); }
            catch (Npgsql.NpgsqlException ex2) { jsonLogger.StepWarn($"Webhook: secondary EndSession failed for session {sessionId}: {ex2.Message}", requestId); }
        }
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralTimeout, "Request cancelled", requestId),
            statusCode: 408);
    }
    catch (InvalidOperationException ex)
    {
        jsonLogger.StepError(
            $"[{ErrorCodes.AutomationWebhookExecutionFailed}] Webhook execution error: tenant={tenantId}, flow={flowId}: {ex.Message}",
            requestId);

        if (sessionId > 0)
        {
            try { await repo.EndSessionAsync(sessionId, "error", CancellationToken.None); }
            catch (Npgsql.NpgsqlException ex2) { jsonLogger.StepWarn($"Webhook: secondary EndSession failed for session {sessionId}: {ex2.Message}", requestId); }
        }

        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AutomationWebhookExecutionFailed, "Webhook execution failed", requestId),
            statusCode: 500);
    }
});

// ============================================================
// Onboarding stats (lightweight, called by Backend aggregation)
// ============================================================

app.MapGet("/api/v1/flows/{tenantId:int}/onboarding-stats", async (int tenantId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var (totalFlowCount, activeFlowCount) = await repo.GetOnboardingStatsAsync(tenantId);
        return Results.Ok(new AutomationOnboardingStatsDto
        {
            TenantId = tenantId,
            TotalFlowCount = totalFlowCount,
            ActiveFlowCount = activeFlowCount
        });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"Onboarding stats failed for tenant {tenantId}: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationOnboardingStatsFailed, "Failed to retrieve onboarding stats", "-"), statusCode: 500);
    }
});

// ============================================================
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "POST", Path = "/api/v1/webhook/event", Description = "Process incoming message (async)", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/flows/{tenantId}", Description = "List all flows for tenant (multi-flow)", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "GET", Path = "/api/v1/flows/{tenantId}/{flowId}", Description = "Get single flow by ID", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "POST", Path = "/api/v1/flows/{tenantId}", Description = "Create new flow", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "PUT", Path = "/api/v1/flows/{tenantId}/{flowId}", Description = "Update flow config", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "DELETE", Path = "/api/v1/flows/{tenantId}/{flowId}", Description = "Delete flow", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "POST", Path = "/api/v1/flows/{tenantId}/{flowId}/activate", Description = "Activate flow (deactivate others)", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "POST", Path = "/api/v1/flows/{tenantId}/{flowId}/deactivate", Description = "Deactivate flow", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "POST", Path = "/api/v1/flows/validate", Description = "Validate v2 flow config (graph validation)", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "POST", Path = "/api/v1/flows/{tenantId}/{flowId}/migrate-v1", Description = "Convert v1 config to v2 graph + save", Auth = "Bearer JWT", Category = "Flow" },
        new() { Method = "POST", Path = "/api/v1/simulation/start", Description = "Start simulation session (in-memory)", Auth = "Bearer JWT", Category = "Simulation" },
        new() { Method = "POST", Path = "/api/v1/simulation/step", Description = "Send user message to simulation", Auth = "Bearer JWT", Category = "Simulation" },
        new() { Method = "DELETE", Path = "/api/v1/simulation/{sessionId}", Description = "Cleanup simulation session", Auth = "Bearer JWT", Category = "Simulation" },
        new() { Method = "GET", Path = "/api/v1/faq/{tenantId}", Description = "List FAQ entries", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/faq/{tenantId}", Description = "Create FAQ entry", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "PUT", Path = "/api/v1/faq/{tenantId}/{id}", Description = "Update FAQ entry", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "DELETE", Path = "/api/v1/faq/{tenantId}/{id}", Description = "Delete FAQ entry", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/onboarding/{tenantId}/seed-intents", Description = "Seed tenant intents from sector templates (PKT-6A)", Auth = "Bearer JWT", Category = "Onboarding" },
        new() { Method = "GET", Path = "/api/v1/returns/{tenantId}/stats", Description = "Return deflection stats (PKT-6B1)", Auth = "Bearer JWT", Category = "Returns" },
        new() { Method = "POST", Path = "/api/v1/returns/{tenantId}/{deflectionId}/deflected", Description = "Mark deflection as successful (PKT-6B1)", Auth = "Bearer JWT", Category = "Returns" },
        new() { Method = "POST", Path = "/api/v1/webhooks/{tenantId}/{flowId}", Description = "Fire webhook_trigger flow (no auth)", Auth = "none", Category = "Webhook" },
        new() { Method = "GET", Path = "/api/v1/flows/{tenantId}/onboarding-stats", Description = "Onboarding stats (flow counts)", Auth = "Bearer JWT", Category = "Onboarding" },
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe (DB check)", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "none", Category = "Ops" },
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.AutomationServiceName,
        Port = ServiceConstants.AutomationPort,
        Endpoints = endpoints
    });
});

logger.SystemInfo($"Automation service starting on port {listenPort}");
app.Run();

// Required for integration tests
public partial class Program { }

/// <summary>
/// Static state for webhook endpoint — atomic counter for synthetic chat_ids.
/// Separate from CronSchedulerService counter to avoid collision.
/// </summary>
static class WebhookState
{
    public static long ChatIdCounter = -1_000_000L;
}
