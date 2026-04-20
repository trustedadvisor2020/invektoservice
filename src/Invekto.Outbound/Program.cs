using Invekto.Outbound.Data;
using Invekto.Shared.Middleware;
using Invekto.Shared.Services;
using Invekto.Outbound.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;
using Npgsql;

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
builder.Services.AddSingleton<ConsentManager>();
builder.Services.AddSingleton(new RateLimiter(defaultMsgPerMin, logger));

// FEAT-DMP: NullResolver is the default binding; FEAT-TFM replaces it when it ships.
// DynamicMessageValidator depends on it for placeholder → INMA key resolution.
builder.Services.AddSingleton<Invekto.Shared.Contracts.TenantFieldMapping.ITenantFieldMappingResolver,
    Invekto.Shared.Contracts.TenantFieldMapping.NullTenantFieldMappingResolver>();
builder.Services.AddSingleton<Invekto.Shared.Services.DynamicMessageValidator>();

builder.Services.AddSingleton<BroadcastOrchestrator>();
builder.Services.AddSingleton<TriggerProcessor>();
builder.Services.AddSingleton<CampaignOrchestrator>();

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

// ─── FEAT-J2: INMA opt-out sync (IInmaContactOptOutClient + InmaOptOutSyncJob) ───
var optOutSyncOptions = new InmaOptOutSyncOptions();
builder.Configuration.GetSection("InmaAuth:OptOutSync").Bind(optOutSyncOptions);
builder.Services.AddSingleton(optOutSyncOptions);

if (string.Equals(optOutSyncOptions.Mode, "Http", StringComparison.OrdinalIgnoreCase))
{
    // Production path: push to cxapi.wapcrm.net
    builder.Services.AddHttpClient<IInmaContactOptOutClient, HttpInmaContactOptOutClient>()
        .AddTypedClient((httpClient, sp) =>
        {
            var opts = sp.GetRequiredService<InmaOptOutSyncOptions>();
            return new HttpInmaContactOptOutClient(
                httpClient,
                opts.BaseUrl,
                opts.SecretKey,
                opts.TimeoutSeconds);
        });
}
else
{
    // Emergency kill-switch: outbox rows park as 'skipped_noop'
    builder.Services.AddSingleton<IInmaContactOptOutClient, NoOpInmaContactOptOutClient>();
}

builder.Services.AddSingleton<InmaOptOutSyncJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<InmaOptOutSyncJob>());

builder.Services.AddAuthorization();

var app = builder.Build();

// GR-3.26: Wire OptOutManager → ConsentManager for STOP keyword sync
app.Services.GetRequiredService<OptOutManager>()
    .SetConsentManager(app.Services.GetRequiredService<ConsentManager>());

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Faz 1: Plan-based feature guard (after JwtAuth sets TenantContext)
var planCache = new TenantPlanCache(pgConnStr, logger);
app.UseFeatureGuard(planCache, logger,
    ("/api/v1/", "Outbound"));
app.UseAuthorization();

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
            ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Islem basarisiz oldu. Lutfen tekrar deneyin.", requestId),
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
            ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Islem basarisiz oldu. Lutfen tekrar deneyin.", requestId),
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
        tenantContext.TenantId, request.Phone, request.MessageText, request.InstanceId);

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
// FEAT-J2: Internal manuel opt-out / opt-in (Backend → Outbound)
// ============================================================
// Called by Backend Dashboard admin action after it resolves the last-known
// instance via MessageLogRepository.GetLastInstanceIdAsync. Authenticated via
// InternalServices:SharedSecret header (service-to-service).
var internalSharedSecret = builder.Configuration["InternalServices:SharedSecret"] ?? "";

app.MapPost("/api/v1/internal/optout", async (
    HttpContext ctx,
    OptOutManager optOutManager,
    JsonLinesLogger jsonLogger,
    InternalOptOutRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var providedSecret = ctx.Request.Headers["X-Internal-Service-Token"].FirstOrDefault() ?? "";
    if (string.IsNullOrEmpty(internalSharedSecret) || providedSecret != internalSharedSecret)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Invalid internal service token", requestId),
            statusCode: 401);
    }
    if (request == null || request.TenantId <= 0 || string.IsNullOrWhiteSpace(request.Phone))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload,
                "tenant_id and phone required", requestId),
            statusCode: 400);
    }

    // Trust boundary note (CQ9): the tenant_id in the payload is authoritative
    // because the caller is already authenticated via the internal shared
    // secret, and the Backend proxy (/api/v1/optout) validates the JWT-bound
    // tenant context before forwarding. Outbound does not expose this path
    // externally — only service-to-service callers with the shared secret can
    // reach it. Same trust model as the Zoho internal endpoints.
    if (request.EventType is not null and not ("opt_out" or "opt_in"))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload,
                "event_type must be 'opt_out' or 'opt_in'", requestId),
            statusCode: 400);
    }

    var added = request.EventType == "opt_in"
        ? await optOutManager.RegisterAdminOptInAsync(request.TenantId, request.Phone, request.InstanceId, ctx.RequestAborted)
        : await optOutManager.RegisterAdminOptOutAsync(request.TenantId, request.Phone, request.InstanceId, request.Reason, ctx.RequestAborted);

    jsonLogger.StepInfo(
        $"Internal opt-{(request.EventType == "opt_in" ? "in" : "out")}: tenant={request.TenantId}, phone={request.Phone}, instance={request.InstanceId}, updated={added}",
        requestId);
    return Results.Ok(new { updated = added });
});

// ============================================================
// FEAT-J2: Admin ops — retry 'skipped_noop' outbox rows (AC11)
// ============================================================
// SuperAdmin-triggered drain used after flipping Mode from NoOp to Http.
// Authenticated via the same InternalServices token (Backend proxies here).
app.MapPost("/api/v1/internal/outbox/retry-skipped", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    OutboxRetryRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var providedSecret = ctx.Request.Headers["X-Internal-Service-Token"].FirstOrDefault() ?? "";
    if (string.IsNullOrEmpty(internalSharedSecret) || providedSecret != internalSharedSecret)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Invalid internal service token", requestId),
            statusCode: 401);
    }

    var affected = await repository.RetrySkippedNoOpAsync(
        request?.TenantId, request?.SinceUtc, ctx.RequestAborted);
    jsonLogger.SystemInfo(
        $"[{ErrorCodes.OutboxDrainTriggered}] Outbox drain: tenantId={request?.TenantId?.ToString() ?? "*"}, since={request?.SinceUtc?.ToString("o") ?? "*"}, affected={affected}");
    return Results.Ok(new { affected_rows = affected });
});

// ============================================================
// Template CRUD endpoints
// ============================================================

app.MapGet("/api/v1/templates", async (
    HttpContext ctx,
    OutboundRepository repository,
    string? lang) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    // GR-2.3: Optional lang filter via query parameter (e.g., /api/v1/templates?lang=en)
    var templates = await repository.GetActiveTemplatesAsync(tenantContext.TenantId, lang);
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
    var validEvents = new[] { "manual", "new_lead", "payment_received", "appointment_reminder",
        "return_exchange", "return_coupon", "review_recovery", "lead_followup",
        "clinic_reminder", "post_treatment" };
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

    // GR-2.3: Pass language to template creation
    var id = await repository.CreateTemplateAsync(
        tenantContext.TenantId, request.Name, request.TriggerEvent,
        request.MessageTemplate, request.VariablesJson, request.Lang);

    jsonLogger.StepInfo($"Template created: id={id}, name={request.Name}, event={request.TriggerEvent}, lang={request.Lang}", requestId);

    return Results.Json(new { id, name = request.Name, lang = request.Lang }, statusCode: 201);
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
        var validEvents = new[] { "manual", "new_lead", "payment_received", "appointment_reminder",
        "return_exchange", "return_coupon", "review_recovery", "lead_followup",
        "clinic_reminder", "post_treatment" };
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
// Campaign endpoints (GR-3.15)
// ============================================================

app.MapPost("/api/v1/campaigns", async (
    HttpContext ctx,
    CampaignOrchestrator campaignOrchestrator,
    JsonLinesLogger jsonLogger,
    CampaignCreateRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidCampaignPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, errorMessage) = await campaignOrchestrator.CreateCampaignAsync(
        tenantContext.TenantId, request, CancellationToken.None);

    if (response == null)
    {
        var statusCode = errorCode == ErrorCodes.OutboundTemplateNotFound ? 404 : 400;
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Islem basarisiz oldu. Lutfen tekrar deneyin.", requestId), statusCode: statusCode);
    }

    jsonLogger.StepInfo($"Campaign created: id={response.Id}, name={response.Name}", requestId);
    return Results.Json(response, statusCode: 201);
});

app.MapGet("/api/v1/campaigns", async (
    HttpContext ctx,
    OutboundRepository repository,
    string? status) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var campaigns = await repository.ListCampaignsAsync(tenantContext.TenantId, status);
    return Results.Ok(new { campaigns });
});

app.MapGet("/api/v1/campaigns/{id:int}", async (
    HttpContext ctx,
    OutboundRepository repository,
    int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var campaign = await repository.GetCampaignAsync(tenantContext.TenantId, id);
    if (campaign == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.OutboundCampaignNotFound, $"Campaign {id} not found", requestId), statusCode: 404);

    return Results.Ok(campaign);
});

app.MapPost("/api/v1/campaigns/{id:int}/activate", async (
    HttpContext ctx,
    CampaignOrchestrator campaignOrchestrator,
    JsonLinesLogger jsonLogger,
    int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (success, errorCode, errorMessage) = await campaignOrchestrator.ActivateCampaignAsync(
        tenantContext.TenantId, id, CancellationToken.None);

    if (!success)
    {
        var statusCode = errorCode == ErrorCodes.OutboundCampaignNotFound ? 404 : 409;
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Islem basarisiz oldu. Lutfen tekrar deneyin.", requestId), statusCode: statusCode);
    }

    jsonLogger.StepInfo($"Campaign activated: id={id}", requestId);
    return Results.Ok(new { id, activated = true });
});

app.MapPost("/api/v1/campaigns/{id:int}/pause", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var updated = await repository.UpdateCampaignStatusAsync(tenantContext.TenantId, id, "paused");
    if (!updated)
        return Results.Json(ErrorResponse.Create(ErrorCodes.OutboundCampaignNotFound, $"Campaign {id} not found", requestId), statusCode: 404);

    jsonLogger.StepInfo($"Campaign paused: id={id}", requestId);
    return Results.Ok(new { id, paused = true });
});

app.MapGet("/api/v1/campaigns/{id:int}/roi", async (
    HttpContext ctx,
    OutboundRepository repository,
    int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var roi = await repository.GetCampaignRoiAsync(tenantContext.TenantId, id);
    if (roi == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.OutboundCampaignNotFound, $"Campaign {id} not found", requestId), statusCode: 404);

    return Results.Ok(roi);
});

// ============================================================
// Conversion endpoints (GR-3.15)
// ============================================================

app.MapPost("/api/v1/conversions", async (
    HttpContext ctx,
    CampaignOrchestrator campaignOrchestrator,
    JsonLinesLogger jsonLogger,
    ConversionRecordRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundConversionRecordFailed, "Request body is required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (conversionId, errorCode, errorMessage) = await campaignOrchestrator.RecordConversionAsync(
        tenantContext.TenantId, request, CancellationToken.None);

    if (conversionId == null)
    {
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Islem basarisiz oldu. Lutfen tekrar deneyin.", requestId), statusCode: 400);
    }

    jsonLogger.StepInfo(
        $"Conversion recorded: id={conversionId}, type={request.ConversionType}", requestId);

    return Results.Json(new { id = conversionId, conversion_type = request.ConversionType }, statusCode: 201);
});

// ============================================================
// Consent endpoints (GR-3.26)
// ============================================================

app.MapPost("/api/v1/consent", async (
    HttpContext ctx,
    ConsentManager consentManager,
    JsonLinesLogger jsonLogger,
    ConsentRecordRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.CustomerPhone))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidConsentPayload, "customer_phone is required", requestId),
            statusCode: 400);
    }

    if (string.IsNullOrWhiteSpace(request.Channel))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidConsentPayload, "channel is required", requestId),
            statusCode: 400);
    }

    var validTypes = new HashSet<string> { "marketing", "utility", "all" };
    if (!validTypes.Contains(request.ConsentType))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidConsentPayload,
                "consent_type must be one of: marketing, utility, all", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var response = await consentManager.UpsertConsentAsync(
        tenantContext.TenantId, request, CancellationToken.None);

    jsonLogger.StepInfo(
        $"Consent upserted: phone={request.CustomerPhone}, type={request.ConsentType}, opted_in={request.OptedIn}",
        requestId);

    return Results.Json(response, statusCode: 200);
});

app.MapGet("/api/v1/consent/check/{phone}", async (
    HttpContext ctx,
    ConsentManager consentManager,
    string phone) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var response = await consentManager.CheckConsentAsync(
        tenantContext.TenantId, phone);
    return Results.Ok(response);
});

// ============================================================
// Data deletion endpoints (GR-3.29)
// ============================================================

app.MapPost("/api/v1/data-deletion", async (
    HttpContext ctx,
    OutboundRepository repository,
    JsonLinesLogger jsonLogger,
    DataDeletionRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.CustomerPhone))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDataDeletionFailed, "customer_phone is required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        var deletionId = await repository.CreateDeletionRequestAsync(
            tenantContext.TenantId, request.CustomerPhone, request.RequestedBy, CancellationToken.None);

        var servicesCleaned = await repository.ExecuteDataDeletionAsync(
            tenantContext.TenantId, request.CustomerPhone, CancellationToken.None);

        await repository.UpdateDeletionRequestAsync(
            tenantContext.TenantId, deletionId, "completed", servicesCleaned, null, CancellationToken.None);

        jsonLogger.StepInfo(
            $"Data deletion completed: phone={request.CustomerPhone}, services={string.Join(",", servicesCleaned)}",
            requestId);

        return Results.Ok(new DataDeletionResponse
        {
            Id = deletionId,
            CustomerPhone = request.CustomerPhone,
            Status = "completed",
            ServicesCleaned = servicesCleaned,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"Data deletion DB error: phone={request.CustomerPhone}, error={ex.Message}");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDataDeletionFailed, "Data deletion failed", requestId),
            statusCode: 500);
    }
    catch (InvalidOperationException ex)
    {
        jsonLogger.SystemError($"Data deletion logic error: phone={request.CustomerPhone}, error={ex.Message}");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDataDeletionFailed, "Data deletion failed", requestId),
            statusCode: 500);
    }
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
        new() { Method = "POST", Path = "/api/v1/campaigns", Description = "Create campaign (GR-3.15)", Auth = "Bearer JWT", Category = "Campaign" },
        new() { Method = "GET", Path = "/api/v1/campaigns", Description = "List campaigns", Auth = "Bearer JWT", Category = "Campaign" },
        new() { Method = "GET", Path = "/api/v1/campaigns/{id}", Description = "Get campaign details", Auth = "Bearer JWT", Category = "Campaign" },
        new() { Method = "POST", Path = "/api/v1/campaigns/{id}/activate", Description = "Activate campaign", Auth = "Bearer JWT", Category = "Campaign" },
        new() { Method = "POST", Path = "/api/v1/campaigns/{id}/pause", Description = "Pause campaign", Auth = "Bearer JWT", Category = "Campaign" },
        new() { Method = "GET", Path = "/api/v1/campaigns/{id}/roi", Description = "Campaign ROI stats", Auth = "Bearer JWT", Category = "Campaign" },
        new() { Method = "POST", Path = "/api/v1/conversions", Description = "Record conversion event (GR-3.15)", Auth = "Bearer JWT", Category = "Conversion" },
        new() { Method = "POST", Path = "/api/v1/consent", Description = "Upsert consent record (GR-3.26)", Auth = "Bearer JWT", Category = "Consent" },
        new() { Method = "GET", Path = "/api/v1/consent/check/{phone}", Description = "Check consent status", Auth = "Bearer JWT", Category = "Consent" },
        new() { Method = "POST", Path = "/api/v1/data-deletion", Description = "KVKK data deletion request (GR-3.29)", Auth = "Bearer JWT", Category = "Compliance" },
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
namespace Invekto.Outbound { public partial class Program { } }
