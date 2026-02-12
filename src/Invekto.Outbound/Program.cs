using Invekto.Outbound.Data;
using Invekto.Outbound.Middleware;
using Invekto.Outbound.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.OutboundPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";
var defaultMsgPerMin = builder.Configuration.GetValue<int>("RateLimit:DefaultMessagesPerMinute", 30);
var senderIntervalMs = builder.Configuration.GetValue<int>("RateLimit:SenderIntervalMs", 1000);
var callbackUrl = builder.Configuration["Callback:DefaultCallbackUrl"] ?? "";
var callbackMaxRetries = builder.Configuration.GetValue<int>("Callback:MaxRetries", 3);
var callbackBaseDelayMs = builder.Configuration.GetValue<int>("Callback:BaseDelayMs", 500);
var callbackTimeoutMs = builder.Configuration.GetValue<int>("Callback:TimeoutMs", 5000);

// Validate required config
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
var logger = new JsonLinesLogger(ServiceConstants.OutboundServiceName, logPath);
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
builder.Services.AddSingleton<OutboundRepository>();

// Register services
builder.Services.AddSingleton<TemplateEngine>();
builder.Services.AddSingleton<OptOutManager>();
builder.Services.AddSingleton(new RateLimiter(defaultMsgPerMin, logger));
builder.Services.AddSingleton<BroadcastOrchestrator>();
builder.Services.AddSingleton<TriggerProcessor>();

// Register MainAppCallbackClient with HttpClient
var callbackSettings = new CallbackSettings
{
    DefaultCallbackUrl = callbackUrl,
    MaxRetries = callbackMaxRetries,
    BaseDelayMs = callbackBaseDelayMs,
    TimeoutMs = callbackTimeoutMs
};
builder.Services.AddSingleton(callbackSettings);
builder.Services.AddHttpClient<MainAppCallbackClient>()
    .AddTypedClient((httpClient, sp) =>
    {
        return new MainAppCallbackClient(
            httpClient,
            sp.GetRequiredService<CallbackSettings>(),
            sp.GetRequiredService<JsonLinesLogger>());
    });

// Register background message sender
builder.Services.AddSingleton<MessageSenderService>(sp =>
    new MessageSenderService(
        sp.GetRequiredService<OutboundRepository>(),
        sp.GetRequiredService<RateLimiter>(),
        sp.GetRequiredService<MainAppCallbackClient>(),
        sp.GetRequiredService<JsonLinesLogger>(),
        senderIntervalMs));
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageSenderService>());

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.OutboundServiceName)));
app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.OutboundServiceName));
});

// ============================================================
// Broadcast endpoints
// ============================================================

app.MapPost("/api/v1/broadcast/send", async (
    HttpContext ctx,
    BroadcastOrchestrator orchestrator,
    JsonLinesLogger jsonLogger,
    BroadcastSendRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    var (response, errorCode, errorMessage) = await orchestrator.CreateBroadcastAsync(
        tenantContext.TenantId, request, CancellationToken.None);

    if (response == null)
    {
        var statusCode = errorCode switch
        {
            ErrorCodes.OutboundTooManyRecipients => 400,
            ErrorCodes.OutboundTemplateNotFound => 404,
            _ => 400
        };
        return Results.Json(
            ErrorResponse.Create(errorCode!, errorMessage!, requestId),
            statusCode: statusCode);
    }

    jsonLogger.StepInfo(
        $"Broadcast submitted: id={response.BroadcastId}, queued={response.Queued}, skipped={response.SkippedOptout}",
        requestId);

    return Results.Json(response, statusCode: 202);
});

app.MapGet("/api/v1/broadcast/{broadcastId}/status", async (
    HttpContext ctx,
    OutboundRepository repository,
    string broadcastId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    if (!Guid.TryParse(broadcastId, out var bid))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundBroadcastNotFound, "Invalid broadcast ID format", requestId),
            statusCode: 400);
    }

    var status = await repository.GetBroadcastStatusAsync(tenantContext.TenantId, bid);
    if (status == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundBroadcastNotFound, $"Broadcast {broadcastId} not found", requestId),
            statusCode: 404);
    }

    return Results.Ok(status);
});

// ============================================================
// Webhook endpoints
// ============================================================

app.MapPost("/api/v1/webhook/trigger", async (
    HttpContext ctx,
    TriggerProcessor triggerProcessor,
    JsonLinesLogger jsonLogger,
    TriggerWebhookRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    var (response, errorCode, errorMessage, statusCode) = await triggerProcessor.ProcessTriggerAsync(
        tenantContext.TenantId, request, CancellationToken.None);

    if (response == null)
    {
        return Results.Json(
            ErrorResponse.Create(errorCode!, errorMessage!, requestId),
            statusCode: statusCode);
    }

    jsonLogger.StepInfo(
        $"Trigger processed: event={request.Event}, message_id={response.MessageId}, template={response.TemplateId}",
        requestId);

    return Results.Json(response, statusCode: 202);
});

app.MapPost("/api/v1/webhook/delivery-status", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    DeliveryStatusRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.ExternalMessageId))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDeliveryStatusFailed, "external_message_id is required", requestId),
            statusCode: 400);
    }

    if (request.Status is not ("sent" or "delivered" or "read" or "failed"))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDeliveryStatusFailed,
                "status must be one of: sent, delivered, read, failed", requestId),
            statusCode: 400);
    }

    var found = await repository.FindMessageByExternalIdAsync(request.ExternalMessageId);
    if (found == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDeliveryStatusFailed,
                $"Message not found for external_message_id: {request.ExternalMessageId}", requestId),
            statusCode: 404);
    }

    var (messageId, broadcastId, tenantId) = found.Value;

    await repository.UpdateMessageStatusAsync(
        messageId, request.Status, failedReason: request.FailedReason);

    // Update broadcast counters if applicable
    if (broadcastId.HasValue && request.Status is "delivered" or "read" or "failed")
    {
        await repository.IncrementBroadcastCounterAsync(broadcastId.Value, request.Status);
    }

    jsonLogger.StepInfo(
        $"Delivery status updated: external_id={request.ExternalMessageId}, status={request.Status}", requestId);

    return Results.Ok(new { updated = true });
});

app.MapPost("/api/v1/webhook/message", async (
    HttpContext ctx,
    OptOutManager optOutManager,
    JsonLinesLogger jsonLogger,
    IncomingMessageRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.MessageText))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload, "phone and message_text are required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    var (optedOut, keyword) = await optOutManager.ProcessIncomingMessageAsync(
        tenantContext.TenantId, request.Phone, request.MessageText);

    if (optedOut)
    {
        jsonLogger.StepInfo(
            $"Opt-out detected: tenant={tenantContext.TenantId}, phone={request.Phone}, keyword={keyword}", requestId);
    }

    return Results.Ok(new IncomingMessageResponse
    {
        OptedOut = optedOut,
        KeywordMatched = keyword
    });
});

// ============================================================
// Template CRUD endpoints
// ============================================================

app.MapGet("/api/v1/templates", async (
    HttpContext ctx,
    OutboundRepository repository) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var templates = await repository.GetActiveTemplatesAsync(tenantContext.TenantId);
    return Results.Ok(new { templates });
});

app.MapPost("/api/v1/templates", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    TemplateCreateRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.MessageTemplate))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidTemplatePayload, "name and message_template are required", requestId),
            statusCode: 400);
    }

    if (request.Name.Length > 200)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidTemplatePayload, "name must be 200 characters or less", requestId),
            statusCode: 400);
    }

    // Validate trigger_event
    var validEvents = new[] { "manual", "new_lead", "payment_received", "appointment_reminder" };
    if (!validEvents.Contains(request.TriggerEvent))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidTemplatePayload,
                $"trigger_event must be one of: {string.Join(", ", validEvents)}", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var id = await repository.CreateTemplateAsync(
        tenantContext.TenantId, request.Name, request.TriggerEvent,
        request.MessageTemplate, request.VariablesJson);

    jsonLogger.StepInfo($"Template created: id={id}, name={request.Name}, event={request.TriggerEvent}", requestId);

    return Results.Json(new { id, name = request.Name }, statusCode: 201);
});

app.MapPut("/api/v1/templates/{id:int}", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    int id,
    TemplateUpdateRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidTemplatePayload, "Request body is required", requestId),
            statusCode: 400);
    }

    // Validate trigger_event if provided
    if (request.TriggerEvent != null)
    {
        var validEvents = new[] { "manual", "new_lead", "payment_received", "appointment_reminder" };
        if (!validEvents.Contains(request.TriggerEvent))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.OutboundInvalidTemplatePayload,
                    $"trigger_event must be one of: {string.Join(", ", validEvents)}", requestId),
                statusCode: 400);
        }
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var updated = await repository.UpdateTemplateAsync(tenantContext.TenantId, id, request);
    if (!updated)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundTemplateNotFound, $"Template {id} not found or inactive", requestId),
            statusCode: 404);
    }

    jsonLogger.StepInfo($"Template updated: id={id}", requestId);
    return Results.Ok(new { id, updated = true });
});

app.MapDelete("/api/v1/templates/{id:int}", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var deactivated = await repository.DeactivateTemplateAsync(tenantContext.TenantId, id);
    if (!deactivated)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundTemplateNotFound, $"Template {id} not found or already inactive", requestId),
            statusCode: 404);
    }

    jsonLogger.StepInfo($"Template deactivated: id={id}", requestId);
    return Results.Ok(new { id, deactivated = true });
});

// ============================================================
// Opt-out endpoints
// ============================================================

app.MapPost("/api/v1/optout", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    OptOutRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload, "phone is required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    await repository.AddOptOutAsync(tenantContext.TenantId, request.Phone, request.Reason);
    jsonLogger.StepInfo($"Manual opt-out added: phone={request.Phone}", requestId);

    return Results.Ok(new { phone = request.Phone, opted_out = true });
});

app.MapDelete("/api/v1/optout/{phone}", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    string phone) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var removed = await repository.RemoveOptOutAsync(tenantContext.TenantId, phone);
    if (!removed)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundRecipientOptedOut, $"Opt-out record not found for {phone}", requestId),
            statusCode: 404);
    }

    jsonLogger.StepInfo($"Opt-out removed: phone={phone}", requestId);
    return Results.Ok(new { phone, removed = true });
});

app.MapGet("/api/v1/optout/check/{phone}", async (
    HttpContext ctx,
    OutboundRepository repository,
    string phone) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var optOutDate = await repository.GetOptOutDateAsync(tenantContext.TenantId, phone);
    return Results.Ok(new OptOutCheckResponse
    {
        Phone = phone,
        OptedOut = optOutDate.HasValue,
        OptedOutAt = optOutDate
    });
});

// ============================================================
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "POST", Path = "/api/v1/broadcast/send", Description = "Submit broadcast (async, 202)", Auth = "Bearer JWT", Category = "Broadcast" },
        new() { Method = "GET", Path = "/api/v1/broadcast/{broadcastId}/status", Description = "Get broadcast delivery status", Auth = "Bearer JWT", Category = "Broadcast" },
        new() { Method = "POST", Path = "/api/v1/webhook/trigger", Description = "Receive trigger event from Main App", Auth = "Bearer JWT", Category = "Webhook" },
        new() { Method = "POST", Path = "/api/v1/webhook/delivery-status", Description = "Receive delivery status update", Auth = "Bearer JWT", Category = "Webhook" },
        new() { Method = "POST", Path = "/api/v1/webhook/message", Description = "Receive incoming message for opt-out detection", Auth = "Bearer JWT", Category = "Webhook" },
        new() { Method = "GET", Path = "/api/v1/templates", Description = "List active templates", Auth = "Bearer JWT", Category = "Templates" },
        new() { Method = "POST", Path = "/api/v1/templates", Description = "Create template", Auth = "Bearer JWT", Category = "Templates" },
        new() { Method = "PUT", Path = "/api/v1/templates/{id}", Description = "Update template", Auth = "Bearer JWT", Category = "Templates" },
        new() { Method = "DELETE", Path = "/api/v1/templates/{id}", Description = "Deactivate template", Auth = "Bearer JWT", Category = "Templates" },
        new() { Method = "POST", Path = "/api/v1/optout", Description = "Manual opt-out add", Auth = "Bearer JWT", Category = "OptOut" },
        new() { Method = "DELETE", Path = "/api/v1/optout/{phone}", Description = "Remove opt-out", Auth = "Bearer JWT", Category = "OptOut" },
        new() { Method = "GET", Path = "/api/v1/optout/check/{phone}", Description = "Check if phone opted out", Auth = "Bearer JWT", Category = "OptOut" },
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe (DB check)", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "none", Category = "Ops" },
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.OutboundServiceName,
        Port = ServiceConstants.OutboundPort,
        Endpoints = endpoints
    });
});

logger.SystemInfo($"Outbound service starting on port {listenPort}");
app.Run();

// Required for integration tests
public partial class Program { }
