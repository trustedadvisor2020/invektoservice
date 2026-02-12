using System.Text.Json;
using Invekto.Automation.Data;
using Invekto.Automation.Middleware;
using Invekto.Automation.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.Integration;
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
{
    Console.Error.WriteLine("FATAL: Claude:ApiKey is not configured");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(pgConnStr))
{
    Console.Error.WriteLine("FATAL: ConnectionStrings:PostgreSQL is not configured");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(jwtSecretKey))
{
    Console.Error.WriteLine("FATAL: Jwt:SecretKey is not configured");
    Environment.Exit(1);
}

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);
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

// Register services
builder.Services.AddSingleton<WorkingHoursChecker>();
builder.Services.AddSingleton<FaqMatcher>();
builder.Services.AddSingleton<FlowEngine>();

// Register IntentDetector with HttpClient
builder.Services.AddHttpClient<IntentDetector>((sp, client) =>
{
    // HttpClient is configured, IntentDetector gets it via DI
}).AddTypedClient((httpClient, sp) =>
{
    return new IntentDetector(httpClient, claudeApiKey, sp.GetRequiredService<JsonLinesLogger>());
});

// Register orchestrator
builder.Services.AddSingleton<AutomationOrchestrator>();

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/webhook/", "/api/v1/flows/", "/api/v1/faq/");

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

app.MapPost("/api/v1/webhook/event", (
    HttpContext ctx,
    AutomationOrchestrator orchestrator,
    JsonLinesLogger jsonLogger,
    IncomingWebhookEvent? webhookEvent) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (webhookEvent == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.IntegrationWebhookInvalidPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    // Only process CUSTOMER messages
    if (webhookEvent.EventType != "new_message" || webhookEvent.Data?.MessageSource != "CUSTOMER")
    {
        return Results.Json(new { status = "ignored", request_id = requestId, reason = "Not a customer message" }, statusCode: 200);
    }

    if (string.IsNullOrWhiteSpace(webhookEvent.Data?.MessageText))
    {
        return Results.Json(new { status = "ignored", request_id = requestId, reason = "Empty message text" }, statusCode: 200);
    }

    // Extract tenant from JWT (stored by middleware)
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    jsonLogger.StepInfo($"Processing message for tenant {tenantContext.TenantId}, chat {webhookEvent.ChatId}", requestId);

    // Process async (return 202 immediately)
    var callbackUrl = webhookEvent.CallbackUrl;
    var callbackClient = app.Services.GetRequiredService<MainAppCallbackClient>();
    _ = Task.Run(async () =>
    {
        try
        {
            var success = await orchestrator.ProcessMessageAsync(tenantContext, webhookEvent, requestId, callbackUrl, CancellationToken.None);
            if (!success)
                jsonLogger.StepError($"Message processing completed with failure for tenant {tenantContext.TenantId}, chat {webhookEvent.ChatId}", requestId);
        }
        catch (Exception ex)
        {
            // Orchestrator's catch block already sends error callback with detailed message.
            // This catch handles only unexpected exceptions outside orchestrator scope.
            jsonLogger.StepError($"Background processing exception: {ex.Message}", requestId);
            await SendErrorCallbackAsync(callbackClient, requestId, tenantContext.TenantId, webhookEvent.ChatId, webhookEvent.SequenceId,
                $"Background processing error: {ex.Message}", callbackUrl, jsonLogger);
        }
    });

    return Results.Json(new
    {
        status = "accepted",
        request_id = requestId,
        event_type = webhookEvent.EventType,
        sequence_id = webhookEvent.SequenceId,
        message = "Event accepted for processing"
    }, statusCode: 202);
});

// ============================================================
// Error callback helper (for background processing failures)
// ============================================================

static async Task SendErrorCallbackAsync(
    MainAppCallbackClient callbackClient,
    string requestId, int tenantId, int chatId, long sequenceId,
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
app.MapGet("/api/v1/flows/{tenantId:int}", async (int tenantId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", "-"), statusCode: 403);

    try
    {
        var flows = await repo.ListFlowsAsync(tenantId);
        return Results.Ok(flows.Select(f => new
        {
            flow_id = f.FlowId,
            flow_name = f.FlowName,
            is_active = f.IsActive,
            is_default = f.IsDefault,
            config_version = f.ConfigVersion,
            node_count = f.NodeCount,
            edge_count = f.EdgeCount,
            created_at = f.CreatedAt,
            updated_at = f.UpdatedAt
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

        return Results.Ok(new
        {
            flow_id = flow.FlowId,
            tenant_id = flow.TenantId,
            flow_name = flow.FlowName,
            is_active = flow.IsActive,
            is_default = flow.IsDefault,
            flow_config = JsonSerializer.Deserialize<JsonElement>(flow.FlowConfigJson),
            created_at = flow.CreatedAt,
            updated_at = flow.UpdatedAt
        });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Flow GET by ID failed: {ex.Message}", "-");
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", "-"), statusCode: 500);
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

        // Validate flow_config is valid JSON
        try { using var _ = JsonDocument.Parse(flowConfig); }
        catch { return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "flow_config is not valid JSON", requestId), statusCode: 400); }

        var flowId = await repo.CreateFlowAsync(tenantId, flowName, flowConfig);
        jsonLogger.StepInfo($"Flow created for tenant {tenantId}: flow_id={flowId}, name={flowName}", requestId);

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

// PUT /api/v1/flows/{tenantId}/{flowId} — Update flow config
app.MapPut("/api/v1/flows/{tenantId:int}/{flowId:int}", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
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
        catch { return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationInvalidFlowConfig, "flow_config is not valid JSON", requestId), statusCode: 400); }

        var flowName = root.TryGetProperty("flow_name", out var fn) ? fn.GetString() : null;

        var updated = await repo.UpdateFlowByIdAsync(tenantId, flowId, flowName, flowConfig);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Flow updated for tenant {tenantId}: flow_id={flowId}", requestId);
        return Results.Ok(new { flow_id = flowId, tenant_id = tenantId, status = "updated" });
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

// POST /api/v1/flows/{tenantId}/{flowId}/activate — Activate flow (deactivate others)
app.MapPost("/api/v1/flows/{tenantId:int}/{flowId:int}/activate", async (int tenantId, int flowId, HttpContext ctx, AutomationRepository repo, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";

    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var activated = await repo.ActivateFlowAsync(tenantId, flowId);
        if (!activated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AutomationFlowNotFoundById, "Belirtilen chatbot akisi bulunamadi", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Flow activated for tenant {tenantId}: flow_id={flowId}", requestId);
        return Results.Ok(new { flow_id = flowId, tenant_id = tenantId, status = "activated" });
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
        new() { Method = "GET", Path = "/api/v1/faq/{tenantId}", Description = "List FAQ entries", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/faq/{tenantId}", Description = "Create FAQ entry", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "PUT", Path = "/api/v1/faq/{tenantId}/{id}", Description = "Update FAQ entry", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "DELETE", Path = "/api/v1/faq/{tenantId}/{id}", Description = "Delete FAQ entry", Auth = "Bearer JWT", Category = "API" },
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
