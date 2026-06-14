using Invekto.Outbound.Data;
using Invekto.Shared.Middleware;
using Invekto.Shared.Services;
using Invekto.Outbound.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;
using Microsoft.AspNetCore.Mvc;
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

// FEAT-TFM MVP: DbResolver swap (was NullResolver). Mapping yoksa null döner → DMP
// DynamicMessageValidator raw INMA-key allowlist fallback intact (sıfır breaking change).
// Multi-instance not: Outbound her instance kendi cache'ini tutar; Backend PUT'tan sonra
// 5dk TTL ile eventual consistent (FEAT-TFM-CACHE cross-invalidation v2).
builder.Services.AddMemoryCache(); // DbTenantFieldMappingResolver dependency
builder.Services.AddSingleton<Invekto.Shared.Contracts.TenantFieldMapping.ITenantFieldMappingResolver,
    Invekto.Shared.Contracts.TenantFieldMapping.DbTenantFieldMappingResolver>();
builder.Services.AddSingleton<Invekto.Shared.Services.DynamicMessageValidator>();

// ─── FEAT-PROJELER / cxapi PR-3a: plain-text bulk cutover (feature-flagged, allowlisted; default OFF) ───
// BroadcastOrchestrator (route decision at create) + MessageSenderService (cxapi send path) consume this.
// Default OFF + empty allowlist => no row is ever send_route='wapcrm_cxapi' (pure bridge no-op).
var cxapiSendOptions = new CxapiSendOptions();
builder.Configuration.GetSection(CxapiSendOptions.SectionName).Bind(cxapiSendOptions);
builder.Services.AddSingleton(cxapiSendOptions);

builder.Services.AddSingleton<BroadcastOrchestrator>();
builder.Services.AddSingleton<TriggerProcessor>();
builder.Services.AddSingleton<CampaignOrchestrator>();

// ─── FEAT-OBI Phase 0: CSV bulk send (feature-flagged, allowlisted, hard-capped) ───
var bulkSendOptions = new BulkSendOptions();
builder.Configuration.GetSection(BulkSendOptions.SectionName).Bind(bulkSendOptions);
builder.Services.AddSingleton(bulkSendOptions);
builder.Services.AddSingleton<PhoneNormalizer>();
builder.Services.AddSingleton<CsvRecipientParser>();
builder.Services.AddSingleton<BulkSendRepository>();
builder.Services.AddSingleton<BulkSendOrchestrator>();

// ─── FEAT-OBI Phase 1A: Contact lists (feature-flagged, allowlisted, capped) ───
var contactListOptions = new ContactListOptions();
builder.Configuration.GetSection(ContactListOptions.SectionName).Bind(contactListOptions);
builder.Services.AddSingleton(contactListOptions);
builder.Services.AddSingleton<DataListRepository>();
builder.Services.AddSingleton<ContactListImportService>();

// ─── FEAT-PROJELER (PKT-14) slice S2: Projects CRUD (feature-flagged, allowlisted) ───
// Unconditional registration (never inside an if-block) — .NET 8 RDF startup inspects every
// endpoint param via IServiceProviderIsService; an unregistered complex param on a GET/DELETE
// endpoint crashes startup ("Body was inferred ...") (2026-05-27 RDF incident). Endpoints also
// carry explicit [FromServices] as a belt-and-suspenders guard.
var projectsOptions = new ProjectsOptions();
builder.Configuration.GetSection(ProjectsOptions.SectionName).Bind(projectsOptions);
builder.Services.AddSingleton(projectsOptions);
builder.Services.AddSingleton<ProjectsRepository>();
builder.Services.AddSingleton<ProjectsService>();

// ─── FEAT-OBI Phase 1A Plan B: Export Manager (dedicated flag, default OFF) ───
var exportOptions = new ExportOptions();
builder.Configuration.GetSection(ExportOptions.SectionName).Bind(exportOptions);
builder.Services.AddSingleton(exportOptions);
builder.Services.AddSingleton<ExportRepository>();
builder.Services.AddSingleton<ExportService>();

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
        sp.GetRequiredService<WapCrmSendClient>(),
        sp.GetRequiredService<CxapiSendOptions>(),
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

// ─── FEAT-PROJELER / cxapi send engine — PR-2: WapCrmSendClient ───
// Typed cxapi /chatoperation plain-text client. Registered so it is wired and
// testable, but NOTHING resolves it in PR-2 — the sender route lands in PR-3, so
// this registration is a behavioural NO-OP. Per-request secret only (the secret
// is supplied per call, never here). The primary handler disables auto-redirect
// (cxapi uses HTTP 301/302 as RATE-LIMIT signals, not real redirects — auto-follow
// would swallow them) and cookies (no cross-tenant state on the pooled handler).
// The per-attempt timeout is enforced inside the client via a linked CTS, hence
// the HttpClient timeout is Infinite.
var wapCrmSendOptions = new WapCrmSendOptions();
builder.Configuration.GetSection(WapCrmSendOptions.SectionName).Bind(wapCrmSendOptions);
builder.Services.AddSingleton(wapCrmSendOptions);
builder.Services.AddHttpClient<WapCrmSendClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    .ConfigureHttpClient((sp, http) =>
    {
        var opts = sp.GetRequiredService<WapCrmSendOptions>();
        http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        // FINITE backstop (was Timeout.InfiniteTimeSpan): the per-attempt linked CTS in SendCoreAsync is the
        // primary timeout, but a stuck socket it fails to cancel must NEVER wedge the single-threaded
        // MessageSenderService (incident 2026-06-12: a hung POST froze the sender 4h). Backstop > TimeoutMs
        // so the CTS still fires first under normal load.
        http.Timeout = TimeSpan.FromMilliseconds(opts.HttpClientBackstopMs);
    })
    .AddTypedClient((http, sp) => new WapCrmSendClient(
        http,
        sp.GetRequiredService<WapCrmSendOptions>(),
        sp.GetRequiredService<JsonLinesLogger>()));

// ─── FEAT-PROJELER / cxapi PR-4: WapCrmTemplateClient (READ-ONLY template catalog) ───
// ProjectsService validates an HSM run against the LIVE cxapi template list at preview AND
// again before a fresh confirm (ownership + required-param coverage; hard-fail when the
// catalog is unreachable). Mirrors the Backend registration: per-request X-CIB-SecretKey,
// FIXED base URL (SSRF mitigation), AllowAutoRedirect=false (cxapi 301/302 = rate-limit),
// per-attempt timeout inside the client + a FINITE HttpClient.Timeout backstop. The CxapiSend
// allowlist is checked BEFORE any call — with the allowlist empty (P0-3) this client
// never fires in prod.
var wapCrmTemplateOptions = new WapCrmTemplateOptions();
builder.Configuration.GetSection(WapCrmTemplateOptions.SectionName).Bind(wapCrmTemplateOptions);
builder.Services.AddSingleton(wapCrmTemplateOptions);
builder.Services.AddHttpClient<WapCrmTemplateClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    .ConfigureHttpClient((sp, http) =>
    {
        var opts = sp.GetRequiredService<WapCrmTemplateOptions>();
        http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        // FINITE backstop (was Timeout.InfiniteTimeSpan): same hung-socket guard as WapCrmSendClient — a
        // stuck template fetch must not hang the request thread past this ceiling. Backstop > TimeoutMs.
        http.Timeout = TimeSpan.FromMilliseconds(opts.HttpClientBackstopMs);
    });

// ─── Feature A — cxapi message-webhook reconciliation (WapCrmWebhookSettingsClient + job) ───
// Background sweep that points each allowlisted, WapCRM-configured tenant's cxapi MESSAGES webhook
// at our delivery-ack ingress so acks flow. Production-safe default OFF (Enabled=false) — the hosted
// service does not even schedule a sweep until enabled via config. Same client doctrine as the send
// client: per-request X-CIB-SecretKey, fixed base URL, AllowAutoRedirect=false (301/302 = rate-limit),
// per-request CTS timeout + a FINITE HttpClient.Timeout backstop.
var cxapiWebhookReconcileOptions = new CxapiWebhookReconcileOptions();
builder.Configuration.GetSection(CxapiWebhookReconcileOptions.SectionName).Bind(cxapiWebhookReconcileOptions);
builder.Services.AddSingleton(cxapiWebhookReconcileOptions);
builder.Services.AddHttpClient<WapCrmWebhookSettingsClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    .ConfigureHttpClient((sp, http) =>
    {
        var opts = sp.GetRequiredService<CxapiWebhookReconcileOptions>();
        http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromMilliseconds(opts.HttpClientBackstopMs);
    })
    .AddTypedClient((http, sp) => new WapCrmWebhookSettingsClient(
        http,
        sp.GetRequiredService<JsonLinesLogger>(),
        sp.GetRequiredService<CxapiWebhookReconcileOptions>().TimeoutMs));
builder.Services.AddSingleton<CxapiWebhookReconcileJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CxapiWebhookReconcileJob>());

builder.Services.AddAuthorization();

var app = builder.Build();

// GR-3.26: Wire OptOutManager → ConsentManager for STOP keyword sync
app.Services.GetRequiredService<OptOutManager>()
    .SetConsentManager(app.Services.GetRequiredService<ConsentManager>());

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths.
// EXCLUSION: /api/v1/internal/ peer-service endpoints (optout, outbox/retry-skipped)
// authenticate via the X-Internal-Service-Token shared secret, NOT a tenant JWT — the
// Backend proxy forwards with only that header (no Bearer). They sit under /api/v1/, so
// without this exclusion the JWT middleware would 401 them before the handler's shared-secret
// check runs (the endpoints were unreachable). Mirrors Backend's /api/v1/leads/intake/
// FEAT-LIW exclusion; the handlers enforce the shared secret and fail closed when it is unset.
var jwtRequiredPrefixes = new[] { "/api/v1/" };
var jwtExcludedPrefixes = new[] { "/api/v1/internal/" };
app.UseJwtAuth(jwtValidator, logger, new HashSet<string>(), jwtRequiredPrefixes, jwtExcludedPrefixes);

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
// Bulk send endpoints (FEAT-OBI Phase 0 — CSV source, gated)
// ============================================================

app.MapPost("/api/v1/bulk-send/preview", async (
    HttpContext ctx,
    BulkSendOrchestrator orchestrator,
    BulkSendPreviewRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BulkSendInvalidPayload, "Request body is required", requestId), statusCode: 400);

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, errorMessage) = await orchestrator.PreviewAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (response == null)
    {
        var statusCode = errorCode switch
        {
            ErrorCodes.BulkSendDisabled => 403,
            ErrorCodes.OutboundTemplateNotFound => 404,
            _ => 400
        };
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Bulk preview failed", requestId), statusCode: statusCode);
    }
    return Results.Ok(response);
});

app.MapPost("/api/v1/bulk-send/confirm", async (
    HttpContext ctx,
    BulkSendOrchestrator orchestrator,
    BulkSendConfirmRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null || string.IsNullOrWhiteSpace(request.CampaignId))
        return Results.Json(ErrorResponse.Create(ErrorCodes.BulkSendInvalidPayload, "campaign_id is required", requestId), statusCode: 400);

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, errorMessage) = await orchestrator.ConfirmAsync(tenantContext.TenantId, request.CampaignId, ctx.RequestAborted);
    if (response == null)
    {
        var statusCode = errorCode switch
        {
            ErrorCodes.BulkSendDisabled => 403,
            ErrorCodes.BulkSendJobNotFound => 404,
            ErrorCodes.BulkSendDispatchFailed => 500, // operational dispatch/DB failure, not bad input
            _ => 400
        };
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Bulk confirm failed", requestId), statusCode: statusCode);
    }
    return Results.Json(response, statusCode: 202);
});

app.MapGet("/api/v1/bulk-send/{campaignId}/status", async (
    HttpContext ctx,
    BulkSendOrchestrator orchestrator,
    string campaignId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode) = await orchestrator.StatusAsync(tenantContext.TenantId, campaignId, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.BulkSendJobNotFound, $"Campaign '{campaignId}' not found", requestId), statusCode: 404);
    return Results.Ok(response);
});

app.MapPost("/api/v1/bulk-send/preview-from-list", async (
    HttpContext ctx,
    BulkSendOrchestrator orchestrator,
    PreviewFromListRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BulkSendInvalidPayload, "Request body is required", requestId), statusCode: 400);

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, errorMessage) = await orchestrator.PreviewFromListAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (response == null)
    {
        var statusCode = errorCode switch
        {
            ErrorCodes.BulkSendDisabled => 403,
            ErrorCodes.ContactListDisabled => 403,
            ErrorCodes.OutboundTemplateNotFound => 404,
            ErrorCodes.ContactListNotFound => 404,
            ErrorCodes.BulkSendAlreadyConfirmed => 409,
            ErrorCodes.ContactListNotReady => 409,
            ErrorCodes.ContactListNoSendable => 422,
            ErrorCodes.ContactListDbError => 503,
            _ => 400
        };
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, errorMessage ?? "Preview-from-list failed", requestId), statusCode: statusCode);
    }
    return Results.Ok(response);
});

// ============================================================
// FEAT-OBI Phase 1A — Contact Lists (data layer)
// ============================================================

// Map a contact-list error code to an HTTP status (shared by the endpoints below).
static int ContactListStatus(string? code) => code switch
{
    ErrorCodes.ContactListDisabled => 403,
    ErrorCodes.ContactListNotFound => 404,
    ErrorCodes.ContactListRecordNotFound => 404,
    ErrorCodes.ContactListNameConflict => 409,
    ErrorCodes.ContactListNotReady => 409,
    ErrorCodes.ContactListRecordDuplicate => 409,
    ErrorCodes.ContactListCapExceeded => 422,
    ErrorCodes.ContactListDbError => 503,   // transient DB failure — import rolled back, safe to retry
    _ => 400
};

app.MapGet("/api/v1/data-lists", async (HttpContext ctx, ContactListImportService svc) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (lists, errorCode) = await svc.ListAsync(tenantContext.TenantId, ctx.RequestAborted);
    if (lists == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, "Contact lists could not be loaded; please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(lists);
});

app.MapPost("/api/v1/data-lists", async (HttpContext ctx, ContactListImportService svc, CreateDataListRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ContactListInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (list, errorCode, message) = await svc.CreateListAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (list == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "List could not be created; check the name and retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Json(list, statusCode: 201);
});

app.MapPut("/api/v1/data-lists/{id:long}", async (HttpContext ctx, ContactListImportService svc, long id, UpdateDataListRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ContactListInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (list, errorCode, message) = await svc.UpdateListAsync(tenantContext.TenantId, id, request, ctx.RequestAborted);
    if (list == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "List could not be updated; verify it exists and retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(list);
});

app.MapDelete("/api/v1/data-lists/{id:long}", async (HttpContext ctx, ContactListImportService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (ok, errorCode) = await svc.DeleteListAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (!ok)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.ContactListNotFound, $"List {id} not found", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(new { id, deleted = true });
});

app.MapPost("/api/v1/data-lists/records/exists", async (HttpContext ctx, ContactListImportService svc, RecordsExistsRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ContactListInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, message) = await svc.RecordsExistsAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Existence check failed; please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(response);
});

app.MapPost("/api/v1/data-lists/import", async (HttpContext ctx, ContactListImportService svc, ImportBatchRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ContactListInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, message) = await svc.ImportAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Import could not complete; the list was not modified. Please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(response);
});

// FEAT-PROJELER PKT-14 — read-only list insights.
// GET /api/v1/data-lists/preview-sample?listIds=1,2 — sample recipient + deduplicated reach +
// per-column fill stats for the Projeler modal (one round-trip). Literal segment, so it never
// collides with the {id:long} routes.
app.MapGet("/api/v1/data-lists/preview-sample", async (HttpContext ctx, ContactListImportService svc, string? listIds) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, message) = await svc.PreviewSampleAsync(tenantContext.TenantId, listIds, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Preview sample could not be loaded; please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(response);
});

// GET /api/v1/data-lists/{id}/records?search=&page=&pageSize= — server-paged, searchable
// records of one list (Veri Yönetimi viewer popup). Read-only.
app.MapGet("/api/v1/data-lists/{id:long}/records", async (HttpContext ctx, ContactListImportService svc, long id, string? search, int page = 1, int pageSize = 50) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, message) = await svc.RecordsPageAsync(tenantContext.TenantId, id, search, page, pageSize, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Records could not be loaded; please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(response);
});

// Single-record mutations from the list-viewer popup (Veri Yönetimi).
// POST /api/v1/data-lists/{id}/records — manual add (invalid phone + duplicate REJECTED).
app.MapPost("/api/v1/data-lists/{id:long}/records", async (HttpContext ctx, ContactListImportService svc, long id, AddListRecordRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ContactListInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, message) = await svc.AddRecordAsync(tenantContext.TenantId, id, request, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Record could not be added; please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Json(response, statusCode: 201);
});

// DELETE /api/v1/data-lists/{id}/records/{recordId} — permanent single-record delete.
app.MapDelete("/api/v1/data-lists/{id:long}/records/{recordId:long}", async (HttpContext ctx, ContactListImportService svc, long id, long recordId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (response, errorCode, message) = await svc.DeleteRecordAsync(tenantContext.TenantId, id, recordId, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Record could not be deleted; please retry.", requestId), statusCode: ContactListStatus(errorCode));
    return Results.Ok(response);
});

// ============================================================
// FEAT-PROJELER (PKT-14) slice S2 — Projects CRUD (metadata only; send is PR-4)
// ============================================================

// Map a projects error code to an HTTP status (shared by the endpoints below).
static int ProjectStatus(string? code) => code switch
{
    ErrorCodes.ProjectDisabled => 403,
    ErrorCodes.ProjectNotFound => 404,
    ErrorCodes.ProjectNameConflict => 409,
    ErrorCodes.ProjectInvalidTarget => 422,
    ErrorCodes.ProjectDbError => 503,   // transient DB failure — change rolled back, safe to retry
    ErrorCodes.ProjectInvalidSendConfig => 400,
    _ => 400                            // ProjectInvalidPayload + fallback
};

// Code-specific operator-facing message so each projects INV-OB error states the real cause +
// next action (e.g. a disabled-feature 403 must NOT say "retry"). Used where no service message exists.
static string ProjectMessage(string? code) => code switch
{
    ErrorCodes.ProjectDisabled => "Projeler bu hesap için etkin değil.",
    ErrorCodes.ProjectInvalidPayload => "Proje bilgisi geçersiz. Ad ve seçilen listeleri kontrol edin.",
    ErrorCodes.ProjectNotFound => "Proje bulunamadı.",
    ErrorCodes.ProjectNameConflict => "Bu isimde bir proje zaten var. Farklı bir ad girin.",
    ErrorCodes.ProjectInvalidTarget => "Seçilen listelerden biri bulunamadı. Liste seçimini güncelleyin.",
    ErrorCodes.ProjectDbError => "Veritabanı hatası nedeniyle işlem tamamlanamadı. Lütfen tekrar deneyin.",
    ErrorCodes.ProjectInvalidSendConfig => "Kanal/şablon ayarları geçersiz. Seçimlerinizi kontrol edin.",
    _ => "İşlem tamamlanamadı."
};

// FEAT-PROJELER send-exec SS-C — HTTP status for the project RUN-DISPATCH endpoints. A run reuses
// the bulk machinery, so these surface Project*, BulkSend* AND ContactList* codes; map them all here.
static int ProjectSendStatus(string? code) => code switch
{
    ErrorCodes.ProjectDisabled => 403,
    ErrorCodes.BulkSendDisabled => 403,
    ErrorCodes.ProjectNotFound => 404,
    ErrorCodes.BulkSendJobNotFound => 404,
    ErrorCodes.ContactListNotFound => 404,
    ErrorCodes.BulkSendAlreadyConfirmed => 409,
    ErrorCodes.ContactListNotReady => 409,
    ErrorCodes.ProjectRunNotPausable => 409,    // SS-D lifecycle state conflicts
    ErrorCodes.ProjectRunNotResumable => 409,
    ErrorCodes.ProjectRunNotCancellable => 409,
    ErrorCodes.ProjectRunInProgress => 409,
    ErrorCodes.ProjectResendNotEligible => 409,   // Rapor resend: not failed/ambiguous or not this project's
    ErrorCodes.ProjectStatusPullNotEnabled => 403, // FEATURE B status-pull: not allowlisted / no creds
    ErrorCodes.ProjectStatusPullDbError => 503,    // FEATURE B status-pull: DB read/apply failure
    ErrorCodes.ProjectNoContent => 422,
    ErrorCodes.ProjectNoTargets => 422,
    ErrorCodes.ProjectHsmSendNotSupported => 422,
    ErrorCodes.ProjectTestSendInvalidPayload => 422,   // GR-8 test send: bad phone / empty / oversized text
    ErrorCodes.ProjectTestSendThrottled => 429,        // GR-8 test send: per-tenant per-minute cap
    ErrorCodes.ContactListNoSendable => 422,
    ErrorCodes.BulkSendCapExceeded => 422,
    ErrorCodes.BulkSendNoValidRecipients => 422,
    // PR-4 — HSM run validation (preview + pre-confirm revalidation)
    ErrorCodes.HsmSendNotAllowlisted => 403,
    ErrorCodes.HsmTemplateNotFound => 422,
    ErrorCodes.HsmRequiredParamUnmapped => 422,
    ErrorCodes.HsmDynamicButtonUnsupported => 422,
    ErrorCodes.HsmTemplateCatalogUnreachable => 503,
    ErrorCodes.CxapiRouteMisconfigured => 422,  // HSM preview: WapCRM creds missing/incomplete
    ErrorCodes.BulkSendDispatchFailed => 502,
    ErrorCodes.ContactListDbError => 503,
    ErrorCodes.ProjectDbError => 503,
    _ => 400   // ProjectInvalidPayload + ProjectInvalidSendConfig + fallback
};

app.MapGet("/api/v1/projects", async (HttpContext ctx, [FromServices] ProjectsService svc, bool includeArchived = false) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (projects, errorCode) = await svc.ListAsync(tenantContext.TenantId, ctx.RequestAborted, includeArchived);
    if (projects == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, ProjectMessage(errorCode), requestId), statusCode: ProjectStatus(errorCode));
    return Results.Ok(projects);
});

app.MapGet("/api/v1/projects/{id:long}", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (project, errorCode, message) = await svc.GetAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (project == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? $"Project {id} not found", requestId), statusCode: ProjectStatus(errorCode));
    return Results.Ok(project);
});

app.MapPost("/api/v1/projects", async (HttpContext ctx, [FromServices] ProjectsService svc, CreateProjectRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ProjectInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (project, errorCode, message) = await svc.CreateAsync(tenantContext.TenantId, tenantContext.UserId, request, ctx.RequestAborted);
    if (project == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Project could not be created; check the name and retry.", requestId), statusCode: ProjectStatus(errorCode));
    return Results.Json(project, statusCode: 201);
});

app.MapPut("/api/v1/projects/{id:long}", async (HttpContext ctx, [FromServices] ProjectsService svc, long id, UpdateProjectRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ProjectInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (project, errorCode, message) = await svc.UpdateAsync(tenantContext.TenantId, id, request, ctx.RequestAborted);
    if (project == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Project could not be updated; verify it exists and retry.", requestId), statusCode: ProjectStatus(errorCode));
    return Results.Ok(project);
});

app.MapDelete("/api/v1/projects/{id:long}", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    // Soft-delete = archive (ON DELETE RESTRICT on runs forbids hard delete; archiving frees the name).
    var (ok, errorCode) = await svc.ArchiveAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (!ok)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.ProjectNotFound, ProjectMessage(errorCode ?? ErrorCodes.ProjectNotFound), requestId), statusCode: ProjectStatus(errorCode));
    return Results.Ok(new { id, archived = true });
});

// ─── FEAT-PROJELER send-exec SS-C: project RUN dispatch (preview -> confirm -> status) ───
// A run is a bulk_send_job(project_id): the audience/content/instance are read from the project,
// dispatched via the shared bulk machinery. Dual-gated (Projects + BulkSend); prod-inert until a
// tenant is allowlisted for sending. The frontend supplies one campaign_id per Gönder flow.

app.MapPost("/api/v1/projects/{id:long}/send/preview", async (HttpContext ctx, [FromServices] ProjectsService svc, long id, ProjectSendRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ProjectInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (preview, errorCode, message) = await svc.PreviewSendAsync(tenantContext.TenantId, id, request.CampaignId, ctx.RequestAborted);
    if (preview == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Önizleme oluşturulamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(preview);
});

app.MapPost("/api/v1/projects/{id:long}/send/confirm", async (HttpContext ctx, [FromServices] ProjectsService svc, long id, ProjectSendRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ProjectInvalidPayload, "Request body is required", requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (status, errorCode, message) = await svc.ConfirmSendAsync(tenantContext.TenantId, id, request.CampaignId, ctx.RequestAborted);
    if (status == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Gönderim onaylanamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Json(status, statusCode: 202);
});

app.MapGet("/api/v1/projects/{id:long}/send/status", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (detail, errorCode, message) = await svc.GetSendStatusAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (detail == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Durum okunamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(detail);
});

// ─── FEAT-PROJELER send-exec SS-D: project run pause / resume / cancel ───
// Project-level lifecycle over the active run. No body (the project id is the route). Gated on Projects
// (stopping/resuming an already-authorized run); the lifecycle state guard is the real gate. Each returns
// the fresh ProjectDetail (recomputed rollup). Prod-inert until a tenant is allowlisted for sending.

app.MapPost("/api/v1/projects/{id:long}/send/pause", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (detail, errorCode, message) = await svc.PauseSendAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (detail == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Gönderim duraklatılamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(detail);
});

app.MapPost("/api/v1/projects/{id:long}/send/resume", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (detail, errorCode, message) = await svc.ResumeSendAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (detail == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Gönderim sürdürülemedi.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(detail);
});

app.MapPost("/api/v1/projects/{id:long}/send/cancel", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (detail, errorCode, message) = await svc.CancelSendAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (detail == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Gönderim iptal edilemedi.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(detail);
});

// GR-8 — single-recipient plain-text TEST send from the project modal. Not bound to a project id
// (create-mode content is testable before save). Dual-gated (Projects + BulkSend) + per-tenant
// throttle inside the service; with the prod allowlists disjoint (P0-3) this endpoint is inert.
app.MapPost("/api/v1/projects/send/test", async (HttpContext ctx, [FromServices] ProjectsService svc, ProjectTestSendRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ProjectTestSendInvalidPayload, "Request body is required", requestId), statusCode: 422);

    var (response, errorCode, message) = await svc.TestSendAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (response == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Test mesajı gönderilemedi.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Json(response, statusCode: 202);
});

// ─── FEAT-PROJELER Rapor (delivery report) drawer: runs dropdown + paged recipient table + resend ───
// Read paths are gated (Projects) + 404-ownership-probed in the service. Resend re-queues ONE
// undelivered (failed/ambiguous) recipient via its PRESERVED route (the existing worker re-sends).

app.MapGet("/api/v1/projects/{id:long}/report/runs", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (runs, errorCode, message) = await svc.GetReportRunsAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (runs == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Rapor okunamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(runs);
});

app.MapGet("/api/v1/projects/{id:long}/report/recipients", async (
    HttpContext ctx, [FromServices] ProjectsService svc, long id,
    string? campaignId, string? search, int page, int pageSize) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (pageData, errorCode, message) = await svc.GetReportRecipientsAsync(
        tenantContext.TenantId, id, campaignId, search, page < 1 ? 1 : page, pageSize < 1 ? 50 : pageSize, ctx.RequestAborted);
    if (pageData == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Rapor okunamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(pageData);
});

app.MapPost("/api/v1/projects/{id:long}/report/resend", async (HttpContext ctx, [FromServices] ProjectsService svc, long id, ProjectResendRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);
    if (request == null || request.MessageId <= 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ProjectInvalidPayload, "message_id gerekli (pozitif tamsayı).", requestId), statusCode: 400);

    var (detail, errorCode, message) = await svc.ResendAsync(tenantContext.TenantId, id, request.MessageId, ctx.RequestAborted);
    if (detail == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Yeniden gönderilemedi.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(detail);
});

// Bulk resend: re-queue EVERY undelivered (failed/ambiguous) recipient of the project at once (no payload).
// Same preserved-route re-send as the single resend; returns {requeued} (0 = nothing eligible, still 200 OK).
app.MapPost("/api/v1/projects/{id:long}/report/resend-bulk", async (HttpContext ctx, [FromServices] ProjectsService svc, long id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (result, errorCode, message) = await svc.ResendAllAsync(tenantContext.TenantId, id, ctx.RequestAborted);
    if (result == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Yeniden gönderilemedi.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(result);
});

// FEATURE B (status-pull): pull live cxapi message-status for the run's pending (wamid-bearing) recipients
// and apply it via the idempotent ApplyDeliveryStatusAsync. Gated by the CxapiSend allowlist (INV-OB-094 inert).
app.MapPost("/api/v1/projects/{id:long}/report/refresh-status", async (HttpContext ctx, [FromServices] ProjectsService svc, long id, ProjectStatusPullRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var campaignId = request?.CampaignId?.Trim();
    if (string.IsNullOrEmpty(campaignId)) campaignId = null;
    var (result, errorCode, message) = await svc.RefreshRunStatusAsync(tenantContext.TenantId, id, campaignId, ctx.RequestAborted);
    if (result == null)
        return Results.Json(ErrorResponse.Create(errorCode ?? ErrorCodes.GeneralUnknown, message ?? "Durum sorgulanamadı.", requestId), statusCode: ProjectSendStatus(errorCode));
    return Results.Ok(result);
});

// ============================================================
// FEAT-OBI Phase 1A Plan B — Export Manager
// ============================================================

// Map an export error code to an HTTP status (shared by the endpoints below).
static int ExportStatus(string? code) => code switch
{
    ErrorCodes.ExportFeatureDisabled => 403,
    ErrorCodes.ExportSourceNotFound => 404,
    ErrorCodes.ExportTooLarge => 422,
    ErrorCodes.ExportDbError => 503,
    ErrorCodes.ExportListNameConflict => 409,   // Phase 1B
    ErrorCodes.ExportListEmpty => 422,          // Phase 1B
    ErrorCodes.ExportFilterInvalid => 400,      // Phase 1B
    _ => 400 // ExportValidationError + fallback
};

// Code-specific operator-facing message so each INV-OB export error is actionable
// (not a single generic "export failed" for every distinct cause).
static string ExportMessage(string? code) => code switch
{
    ErrorCodes.ExportValidationError => "Dışa aktarma isteği geçersiz. Format ve seçimi kontrol edin.",
    ErrorCodes.ExportSourceNotFound => "Dışa aktarılacak liste ya da kampanya bulunamadı.",
    ErrorCodes.ExportFeatureDisabled => "Dışa aktarma bu hesap için aktif değil.",
    ErrorCodes.ExportDbError => "Veritabanı hatası nedeniyle dışa aktarma tamamlanamadı. Lütfen tekrar deneyin.",
    ErrorCodes.ExportTooLarge => "Excel için veri çok büyük. CSV formatını kullanın (CSV tüm satırları içerir).",
    ErrorCodes.ExportListNameConflict => "Bu isimde bir liste zaten var. Farklı bir ad girin.",
    ErrorCodes.ExportListEmpty => "Filtreye uyan gönderilebilir numara yok; liste oluşturulamadı.",
    ErrorCodes.ExportFilterInvalid => "Filtre geçersiz. Tarih aralığını ve seçimleri kontrol edin.",
    _ => "Dışa aktarma başarısız oldu."
};

// Apply the no-store + nosniff headers every export response must carry.
static void ExportFileHeaders(HttpContext ctx)
{
    ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
}

// Serve a built export file: clean JSON error (code-specific message) if the build failed,
// else a Results.File (filename is an ascii slug; Results.File encodes Content-Disposition safely).
static IResult ExportFileResult(HttpContext ctx, ExportService.ExportFile file, string requestId)
{
    if (file.ErrorCode != null)
        return Results.Json(ErrorResponse.Create(file.ErrorCode, ExportMessage(file.ErrorCode), requestId), statusCode: ExportStatus(file.ErrorCode));
    var bytes = file.Bytes;
    if (bytes is null) // defensive: a successful ExportFile always carries bytes; never serve an empty 200
        return Results.Json(ErrorResponse.Create(ErrorCodes.ExportDbError, ExportMessage(ErrorCodes.ExportDbError), requestId), statusCode: ExportStatus(ErrorCodes.ExportDbError));
    ExportFileHeaders(ctx);
    return Results.File(bytes, file.ContentType, file.FileName);
}

// GET /api/v1/exports/send-jobs — recent campaigns for the export picker (read-only metadata).
app.MapGet("/api/v1/exports/send-jobs", async (HttpContext ctx, ExportService svc) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (err, jobs) = await svc.ListSendJobsAsync(tenantContext.TenantId, ctx.RequestAborted);
    if (err != null)
        return Results.Json(ErrorResponse.Create(err, ExportMessage(err), requestId), statusCode: ExportStatus(err));
    return Results.Ok(jobs);
});

// GET /api/v1/exports/contact-list/{listId}?format=csv|xlsx
app.MapGet("/api/v1/exports/contact-list/{listId:long}", async (HttpContext ctx, ExportService svc, long listId, string? format) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var fmt = (format ?? ExportFormats.Csv).ToLowerInvariant();
    var file = await svc.ExportContactListAsync(tenantContext.TenantId, listId, fmt, tenantContext.UserId.ToString(), ctx.RequestAborted);
    return ExportFileResult(ctx, file, requestId);
});

// GET /api/v1/exports/send-job/{jobId}/recipients?format=csv|xlsx
app.MapGet("/api/v1/exports/send-job/{jobId:long}/recipients", async (HttpContext ctx, ExportService svc, long jobId, string? format) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var fmt = (format ?? ExportFormats.Csv).ToLowerInvariant();
    var file = await svc.ExportSendRecipientsAsync(tenantContext.TenantId, jobId, fmt, tenantContext.UserId.ToString(), ctx.RequestAborted);
    return ExportFileResult(ctx, file, requestId);
});

// GET /api/v1/exports/send-job/{jobId}/report-data?for=pdf  (browser renders the PDF)
app.MapGet("/api/v1/exports/send-job/{jobId:long}/report-data", async (HttpContext ctx, ExportService svc, long jobId, string? @for) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (err, data) = await svc.BuildSendReportDataAsync(tenantContext.TenantId, jobId, tenantContext.UserId.ToString(), ctx.RequestAborted);
    if (err != null)
        return Results.Json(ErrorResponse.Create(err, ExportMessage(err), requestId), statusCode: ExportStatus(err));

    ExportFileHeaders(ctx);
    return Results.Ok(data);
});

// ------------------------------------------------------------
// FEAT-OBI Phase 1B — filter-driven recipients surface
// ------------------------------------------------------------
// Assemble the recipients filter from the querystring (every field optional).
static ExportFilter BuildExportFilter(int? templateId, long? jobId, long? listId, string? status, DateTime? from, DateTime? to)
    => new()
    {
        TemplateId = templateId,
        JobId = jobId,
        ListId = listId,
        Status = status,
        From = from,
        To = to
    };

// GET /api/v1/exports/filter-options — templates + recent campaigns + active lists for the 5 dropdowns.
app.MapGet("/api/v1/exports/filter-options", async (HttpContext ctx, ExportService svc) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (err, options) = await svc.ListFilterOptionsAsync(tenantContext.TenantId, ctx.RequestAborted);
    if (err != null)
        return Results.Json(ErrorResponse.Create(err, ExportMessage(err), requestId), statusCode: ExportStatus(err));
    return Results.Ok(options);
});

// GET /api/v1/exports/recipients/count?templateId=&jobId=&listId=&status=&from=&to=  (count card)
app.MapGet("/api/v1/exports/recipients/count", async (HttpContext ctx, ExportService svc,
    int? templateId, long? jobId, long? listId, string? status, DateTime? from, DateTime? to) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var filter = BuildExportFilter(templateId, jobId, listId, status, from, to);
    var (err, count) = await svc.CountFilteredAsync(tenantContext.TenantId, filter, ctx.RequestAborted);
    if (err != null)
        return Results.Json(ErrorResponse.Create(err, ExportMessage(err), requestId), statusCode: ExportStatus(err));
    return Results.Ok(count);
});

// GET /api/v1/exports/recipients?format=csv|xlsx&...filters  (filtered CSV/XLSX)
app.MapGet("/api/v1/exports/recipients", async (HttpContext ctx, ExportService svc, string? format,
    int? templateId, long? jobId, long? listId, string? status, DateTime? from, DateTime? to) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var fmt = (format ?? ExportFormats.Csv).ToLowerInvariant();
    var filter = BuildExportFilter(templateId, jobId, listId, status, from, to);
    var file = await svc.ExportFilteredAsync(tenantContext.TenantId, filter, fmt, tenantContext.UserId.ToString(), ctx.RequestAborted);
    return ExportFileResult(ctx, file, requestId);
});

// POST /api/v1/exports/recipients/create-list  ("Liste Oluştur" — new data_list from the filter)
app.MapPost("/api/v1/exports/recipients/create-list", async (HttpContext ctx, ExportService svc, CreateListFromExportRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.ExportValidationError, ExportMessage(ErrorCodes.ExportValidationError), requestId), statusCode: 400);
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (err, result) = await svc.CreateListFromExportAsync(tenantContext.TenantId, request, tenantContext.UserId.ToString(), ctx.RequestAborted);
    if (err != null)
        return Results.Json(ErrorResponse.Create(err, ExportMessage(err), requestId), statusCode: ExportStatus(err));
    return Results.Ok(result);
});

// GET /api/v1/exports/history — recent export_logs rows for the history table.
app.MapGet("/api/v1/exports/history", async (HttpContext ctx, ExportService svc) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (err, entries) = await svc.ListHistoryAsync(tenantContext.TenantId, ctx.RequestAborted);
    if (err != null)
        return Results.Json(ErrorResponse.Create(err, ExportMessage(err), requestId), statusCode: ExportStatus(err));
    return Results.Ok(entries);
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

    // PR-3b-2: tenant identity comes from the JWT (Backend mints a service JWT with tenant=companyId
    // when relaying a cxapi ack; the legacy bridge presents its own per-tenant JWT). The lookup is now
    // tenant-scoped, so the handler MUST have a tenant context.
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", requestId),
            statusCode: 401);
    }

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

    // PR-3b-2: ONE atomic, tenant-scoped, idempotent, partition-correct apply (monotonic downgrade guard
    // + transition-gated broadcast counter). Replaces the old tenant-blind Find+Update+Increment trio so
    // a duplicate / out-of-order WapCRM ack is a safe no-op and a wamid can only touch its own tenant.
    var result = await repository.ApplyDeliveryStatusAsync(
        tenantContext.TenantId, request.ExternalMessageId, request.Status,
        failedReason: request.FailedReason, ct: ctx.RequestAborted);

    if (!result.Matched)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundDeliveryStatusFailed,
                $"Message not found for external_message_id: {request.ExternalMessageId}", requestId),
            statusCode: 404);
    }

    // SOFT defense-in-depth: correlation is already single-row-safe via (tenant_id, wamid). An InstanceID
    // mismatch is logged and the status applied anyway (Q decision 2026-06-08) — instance_id is nullable
    // at send time, so a strict reject would risk dropping valid acks.
    if (request.InstanceId.HasValue && result.RowInstanceId.HasValue
        && request.InstanceId.Value != result.RowInstanceId.Value)
    {
        jsonLogger.SystemWarn(
            $"[{ErrorCodes.CxapiDeliveryAckInstanceMismatch}] delivery ack InstanceID mismatch: " +
            $"ack={request.InstanceId} row={result.RowInstanceId} tenant={tenantContext.TenantId} " +
            $"external_id={request.ExternalMessageId} — applying anyway (soft cross-check)");
    }

    jsonLogger.StepInfo(
        $"Delivery status applied: external_id={request.ExternalMessageId}, status={request.Status}, " +
        $"prev={result.PreviousStatus ?? "-"}, applied={result.Applied}, tenant={tenantContext.TenantId}", requestId);

    return Results.Ok(new { updated = result.Applied, matched = true });
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
    // reach it. Canonical peer-service trust model.
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
        // FEAT-PROJELER (PKT-14 S2): Projeler project CRUD (gated by ProjectsOptions, default-OFF)
        new() { Method = "GET", Path = "/api/v1/projects", Description = "List projects (Projeler)", Auth = "Bearer JWT", Category = "Projects" },
        new() { Method = "GET", Path = "/api/v1/projects/{id}", Description = "Get project detail (+targets)", Auth = "Bearer JWT", Category = "Projects" },
        new() { Method = "POST", Path = "/api/v1/projects", Description = "Create project (+targets)", Auth = "Bearer JWT", Category = "Projects" },
        new() { Method = "PUT", Path = "/api/v1/projects/{id}", Description = "Update project (partial +targets replace)", Auth = "Bearer JWT", Category = "Projects" },
        new() { Method = "DELETE", Path = "/api/v1/projects/{id}", Description = "Archive (soft-delete) project", Auth = "Bearer JWT", Category = "Projects" },
        new() { Method = "POST", Path = "/api/v1/projects/send/test", Description = "Single-recipient plain-text TEST send (Projects+BulkSend gated, throttled)", Auth = "Bearer JWT", Category = "Projects" },
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
