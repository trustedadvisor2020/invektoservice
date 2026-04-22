using Hangfire;
using Invekto.Marketing.Data;
using Invekto.Marketing.Endpoints;
using Invekto.Marketing.Services;
using Invekto.Marketing.Services.Jobs;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.Hosting;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;
using Invekto.Shared.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// ============================================
// CONFIG
// ============================================

var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.MarketingPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";

if (string.IsNullOrEmpty(pgConnStr))
    throw new InvalidOperationException("FATAL: ConnectionStrings:PostgreSQL is not configured");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey is not configured");

// ============================================
// KESTREL
// ============================================

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

// ============================================
// DI
// ============================================

var logger = new JsonLinesLogger(ServiceConstants.MarketingServiceName, logPath);
builder.Services.AddSingleton(logger);

builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

var jwtSettings = new JwtSettings
{
    SecretKey = jwtSecretKey,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "InvektoServis",
    Audience = builder.Configuration["Jwt:Audience"] ?? "InvektoServis",
    ClockSkewSeconds = builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds", 30)
};
var jwtValidator = new JwtValidator(jwtSettings);
builder.Services.AddSingleton(jwtValidator);

var pgFactory = new PostgresConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);
builder.Services.AddSingleton<MarketingRepository>();

// Claude Haiku — Multilingual Tourism Response (GR-3.25)
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? "";
var claudeModel = builder.Configuration["Claude:Model"] ?? "claude-haiku-4-5-20251001";
var claudeMaxTokens = builder.Configuration.GetValue<int>("Claude:MaxTokens", 1024);
var claudeTimeoutSecs = builder.Configuration.GetValue<int>("Claude:TimeoutSeconds", 15);

if (string.IsNullOrEmpty(claudeApiKey))
    throw new InvalidOperationException("FATAL: Claude:ApiKey is not configured");

builder.Services.AddHttpClient<TourismResponseGenerator>()
    .AddTypedClient((httpClient, sp) => new TourismResponseGenerator(
        httpClient, claudeApiKey, claudeModel, claudeMaxTokens, claudeTimeoutSecs,
        sp.GetRequiredService<JsonLinesLogger>()));

// ============================================
// FEAT-EFS Drip Sequence — DI + Hangfire
// ============================================
//
// Marketing's Hangfire setup mirrors the queue-per-service topology established by G7
// (see Invekto.Shared.Hosting.HangfireSetup). Queue name 'marketing-followup' isolates
// EFS scheduled jobs from any future Marketing recurring work; multiple Marketing
// instances coexist via Hangfire advisory-lock leader election.
//
// Connection string falls back to ConnectionStrings:PostgreSQL when ConnectionStrings:Hangfire
// is absent, matching the Backend/Appointments pattern.

builder.Services.AddSingleton<FollowupSequenceRepository>();
builder.Services.AddSingleton<FollowupSequenceCache>();
builder.Services.AddSingleton<FollowupOrchestrator>();

// FEAT-MCC: Campaign resolver — used by FollowupStageJob.ExecuteAsync to suppress drip
// stages whose campaign window has closed (interview Q6: window guard fires in EFS
// scheduler too, not just Automation dispatch). Same Shared resolver type as
// Backend/Automation, each instance carries its own process-local cache (5dk TTL).
// AddMemoryCache() is the first IMemoryCache registration in Marketing — Backend and
// Automation get it via their MVC stack defaults, Marketing's minimal-API setup needs
// the explicit add (DbTenantCampaignResolver depends on it).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Invekto.Shared.Contracts.Campaigns.ITenantCampaignResolver,
    Invekto.Shared.Contracts.Campaigns.DbTenantCampaignResolver>();

builder.Services.AddTransient<FollowupStageJob>();

var hangfireConn = HangfireSetup.ResolveConnectionString(builder.Configuration);
if (string.IsNullOrWhiteSpace(hangfireConn))
    throw new InvalidOperationException(
        "FATAL: Marketing service requires ConnectionStrings:Hangfire (or ConnectionStrings:PostgreSQL fallback) for FEAT-EFS scheduled jobs.");

builder.Services.AddInvektoHangfire(
    queueName: "marketing-followup",
    connectionString: hangfireConn,
    enableScheduler: false /* Backend remains the recurring-jobs leader; Marketing is a worker server only */);

// ============================================
// APP
// ============================================

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseTrafficLogging();
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Faz 1: Plan-based feature guard (after JwtAuth sets TenantContext)
var planCache = new TenantPlanCache(pgConnStr, logger);
app.UseFeatureGuard(planCache, logger,
    ("/api/v1/", "Marketing"));
app.UseAuthorization();

// FEAT-EFS: ensure Hangfire JobStorage.Current is initialized before the orchestrator
// invokes BackgroundJob.Schedule via DI'd IBackgroundJobClient (the static API + the DI
// API both rely on the same JobStorage; explicit init avoids "JobStorage instance has
// not been initialized yet" on first request after cold start).
app.EnsureJobStorageInitialized();

// FEAT-EFS endpoints (3 tenant-scoped + 1 internal trigger).
app.MapFollowupEndpoints();

_ = app.Services.GetRequiredService<LogCleanupService>();

// ============================================
// HEALTH ENDPOINTS
// ============================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.MarketingServiceName)));

app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.MarketingServiceName));
});

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "GET", Path = "/health", Description = "Health check", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness check (DB)", Category = "Health" },
        new() { Method = "POST", Path = "/api/v1/reviews/request", Description = "Create review request", Category = "Reviews" },
        new() { Method = "GET", Path = "/api/v1/reviews", Description = "List review requests", Category = "Reviews" },
        new() { Method = "POST", Path = "/api/v1/reviews/{id}/sent", Description = "Mark review link sent", Category = "Reviews" },
        new() { Method = "POST", Path = "/api/v1/reviews/{id}/posted", Description = "Mark review posted", Category = "Reviews" },
        new() { Method = "GET", Path = "/api/v1/reviews/stats", Description = "Review statistics", Category = "Reviews" },
        new() { Method = "POST", Path = "/api/v1/referrals", Description = "Create referral", Category = "Referrals" },
        new() { Method = "GET", Path = "/api/v1/referrals", Description = "List referrals", Category = "Referrals" },
        new() { Method = "GET", Path = "/api/v1/referrals/lookup/{code}", Description = "Lookup referral by code", Category = "Referrals" },
        new() { Method = "PUT", Path = "/api/v1/referrals/{id}/redeem", Description = "Redeem referral", Category = "Referrals" },
        new() { Method = "POST", Path = "/api/v1/tourism/leads", Description = "Create tourism lead", Category = "Tourism" },
        new() { Method = "GET", Path = "/api/v1/tourism/leads", Description = "List tourism leads", Category = "Tourism" },
        new() { Method = "GET", Path = "/api/v1/tourism/leads/{id}", Description = "Get tourism lead", Category = "Tourism" },
        new() { Method = "PUT", Path = "/api/v1/tourism/leads/{id}", Description = "Update tourism lead", Category = "Tourism" },
        new() { Method = "GET", Path = "/api/v1/tourism/stats", Description = "Tourism statistics", Category = "Tourism" },
        // GR-3.24: Rescue
        new() { Method = "POST", Path = "/api/v1/rescue/risks", Description = "Create review risk assessment", Category = "Rescue" },
        new() { Method = "GET", Path = "/api/v1/rescue/risks", Description = "List review risks", Category = "Rescue" },
        new() { Method = "PUT", Path = "/api/v1/rescue/risks/{id}", Description = "Update review risk", Category = "Rescue" },
        new() { Method = "GET", Path = "/api/v1/rescue/stats", Description = "Rescue statistics", Category = "Rescue" },
        new() { Method = "POST", Path = "/api/v1/rescue/templates", Description = "Create rescue template", Category = "Rescue" },
        new() { Method = "GET", Path = "/api/v1/rescue/templates", Description = "List rescue templates", Category = "Rescue" },
        new() { Method = "PUT", Path = "/api/v1/rescue/templates/{id}", Description = "Update rescue template", Category = "Rescue" },
        new() { Method = "DELETE", Path = "/api/v1/rescue/templates/{id}", Description = "Deactivate rescue template", Category = "Rescue" },
        // PKT-12 Faz 3: Follow-Up
        new() { Method = "GET", Path = "/api/v1/rescue/risks/followup-due", Description = "Get risks due for follow-up", Category = "Rescue" },
        new() { Method = "PUT", Path = "/api/v1/rescue/risks/{id}/followup", Description = "Update follow-up status", Category = "Rescue" },
        // GR-3.25: Tourism Catalog + Conversations
        new() { Method = "POST", Path = "/api/v1/tourism/catalog", Description = "Create treatment", Category = "Tourism Catalog" },
        new() { Method = "GET", Path = "/api/v1/tourism/catalog", Description = "List treatments", Category = "Tourism Catalog" },
        new() { Method = "PUT", Path = "/api/v1/tourism/catalog/{id}", Description = "Update treatment", Category = "Tourism Catalog" },
        new() { Method = "DELETE", Path = "/api/v1/tourism/catalog/{id}", Description = "Deactivate treatment", Category = "Tourism Catalog" },
        new() { Method = "POST", Path = "/api/v1/tourism/conversations", Description = "Record conversation", Category = "Tourism Conversations" },
        new() { Method = "GET", Path = "/api/v1/tourism/conversations", Description = "List conversations", Category = "Tourism Conversations" },
        new() { Method = "POST", Path = "/api/v1/tourism/respond", Description = "Generate multilingual response (Claude)", Category = "Tourism Conversations" },
        new() { Method = "GET", Path = "/api/v1/tourism/conversations/stats", Description = "Conversation statistics", Category = "Tourism Conversations" }
    };
    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.MarketingServiceName,
        Port = ServiceConstants.MarketingPort,
        Endpoints = endpoints
    });
});

// ============================================
// REVIEW ENDPOINTS (GR-3.21)
// ============================================

app.MapPost("/api/v1/reviews/request", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    ReviewCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<ReviewCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in review request: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.PatientPhone))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidReviewPayload, "patient_phone is required", requestId), statusCode: 400);

    try
    {
        var id = await repo.CreateReviewRequestAsync(
            tenantContext.TenantId, request.PatientPhone, request.PatientName,
            request.TreatmentType, request.SatisfactionScore, request.ReviewLinkUrl,
            request.Platform ?? "google");
        jsonLog.StepInfo($"Review request created: id={id}, phone={request.PatientPhone}", requestId);
        return Results.Json(new { id }, statusCode: 201);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Review request creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Review request creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/reviews", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var status = ctx.Request.Query["status"].FirstOrDefault();
    var platform = ctx.Request.Query["platform"].FirstOrDefault();

    try
    {
        var reviews = await repo.ListReviewRequestsAsync(tenantContext.TenantId, status, platform);
        return Results.Ok(new { reviews });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Review list query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Review list query failed", "-"), statusCode: 500);
    }
});

app.MapPost("/api/v1/reviews/{id:int}/sent", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        var updated = await repo.MarkReviewSentAsync(tenantContext.TenantId, id);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingReviewNotFound, "Review request not found or already sent", requestId), statusCode: 404);
        jsonLog.StepInfo($"Review marked sent: id={id}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Mark review sent failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Mark review sent failed", requestId), statusCode: 500);
    }
});

app.MapPost("/api/v1/reviews/{id:int}/posted", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    ReviewPostedRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<ReviewPostedRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in review posted: {ex.Message}"); request = null; }

    if (request == null || request.ReviewRating < 1 || request.ReviewRating > 5)
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidReviewPayload, "review_rating (1-5) is required", requestId), statusCode: 400);

    try
    {
        var updated = await repo.MarkReviewPostedAsync(tenantContext.TenantId, id, request.ReviewRating);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingReviewNotFound, "Review request not found", requestId), statusCode: 404);
        jsonLog.StepInfo($"Review marked posted: id={id}, rating={request.ReviewRating}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Mark review posted failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Mark review posted failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/reviews/stats", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    try
    {
        var stats = await repo.GetReviewStatsAsync(tenantContext.TenantId);
        return Results.Ok(stats);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Review stats query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingReviewStatsFailed, "Review stats query failed", "-"), statusCode: 500);
    }
});

// ============================================
// REFERRAL ENDPOINTS (GR-3.21)
// ============================================

app.MapPost("/api/v1/referrals", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    ReferralCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<ReferralCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in referral create: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.ReferrerPhone))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidReferralPayload, "referrer_phone is required", requestId), statusCode: 400);

    try
    {
        var (id, code) = await repo.CreateReferralAsync(
            tenantContext.TenantId, request.ReferrerPhone, request.ReferrerName,
            request.DiscountPct > 0 ? request.DiscountPct : (short)10,
            request.ReferrerReward);
        jsonLog.StepInfo($"Referral created: id={id}, code={code}, referrer={request.ReferrerPhone}", requestId);
        return Results.Json(new { id, referral_code = code }, statusCode: 201);
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        jsonLog.SystemWarn($"[{requestId}] Referral code collision, retrying: {ex.Message}");
        // Retry once with new code (collision extremely unlikely with crypto-random)
        try
        {
            var (id, code) = await repo.CreateReferralAsync(
                tenantContext.TenantId, request.ReferrerPhone, request.ReferrerName,
                request.DiscountPct > 0 ? request.DiscountPct : (short)10,
                request.ReferrerReward);
            return Results.Json(new { id, referral_code = code }, statusCode: 201);
        }
        catch (NpgsqlException retryEx)
        {
            jsonLog.SystemWarn($"[{requestId}] Referral creation retry failed: {retryEx.Message}");
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingReferralCodeExists, "Referral code generation failed", requestId), statusCode: 409);
        }
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Referral creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Referral creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/referrals", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var status = ctx.Request.Query["status"].FirstOrDefault();

    try
    {
        var referrals = await repo.ListReferralsAsync(tenantContext.TenantId, status);
        return Results.Ok(new { referrals });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Referral list query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Referral list query failed", "-"), statusCode: 500);
    }
});

app.MapGet("/api/v1/referrals/lookup/{code}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, string code) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    try
    {
        var referral = await repo.LookupReferralByCodeAsync(tenantContext.TenantId, code);
        if (referral == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingReferralNotFound, "Referral code not found", "-"), statusCode: 404);
        return Results.Ok(referral);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Referral lookup failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Referral lookup failed", "-"), statusCode: 500);
    }
});

app.MapPut("/api/v1/referrals/{id:int}/redeem", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    ReferralRedeemRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<ReferralRedeemRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in referral redeem: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.RefereePhone))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidReferralPayload, "referee_phone is required", requestId), statusCode: 400);

    try
    {
        var redeemed = await repo.RedeemReferralAsync(tenantContext.TenantId, id, request.RefereePhone, request.RefereeName);
        if (!redeemed)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingReferralNotFound, "Referral not found or already redeemed", requestId), statusCode: 404);
        jsonLog.StepInfo($"Referral redeemed: id={id}, referee={request.RefereePhone}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Referral redeem failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Referral redeem failed", requestId), statusCode: 500);
    }
});

// ============================================
// TOURISM LEAD ENDPOINTS (GR-3.22)
// ============================================

app.MapPost("/api/v1/tourism/leads", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    TourismLeadCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<TourismLeadCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in tourism lead create: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.PatientPhone))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTourismPayload, "patient_phone is required", requestId), statusCode: 400);

    try
    {
        var id = await repo.CreateTourismLeadAsync(
            tenantContext.TenantId, request.PatientPhone, request.PatientName,
            request.PatientCountry, request.PatientLang ?? "en", request.TreatmentInterest,
            request.AccommodationNeeded, request.TransferNeeded,
            request.BudgetCurrency, request.BudgetAmount, request.Source,
            request.Notes);
        jsonLog.StepInfo($"Tourism lead created: id={id}, phone={request.PatientPhone}, country={request.PatientCountry}", requestId);
        return Results.Json(new { id }, statusCode: 201);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Tourism lead creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism lead creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/tourism/leads", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var status = ctx.Request.Query["status"].FirstOrDefault();
    var country = ctx.Request.Query["country"].FirstOrDefault();

    try
    {
        var leads = await repo.ListTourismLeadsAsync(tenantContext.TenantId, status, country);
        return Results.Ok(new { leads });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tourism leads query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism leads query failed", "-"), statusCode: 500);
    }
});

app.MapGet("/api/v1/tourism/leads/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    try
    {
        var lead = await repo.GetTourismLeadAsync(tenantContext.TenantId, id);
        if (lead == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingTourismLeadNotFound, "Tourism lead not found", "-"), statusCode: 404);
        return Results.Ok(lead);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tourism lead query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism lead query failed", "-"), statusCode: 500);
    }
});

app.MapPut("/api/v1/tourism/leads/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    TourismLeadUpdateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<TourismLeadUpdateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in tourism lead update: {ex.Message}"); request = null; }

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTourismPayload, "Request body is required", requestId), statusCode: 400);

    // Validate status if provided
    var validStatuses = new[] { "new", "contacted", "consultation", "booked", "treated", "reviewed", "lost" };
    if (request.Status != null && !validStatuses.Contains(request.Status))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTourismStatus, $"Invalid status. Valid values: {string.Join(", ", validStatuses)}", requestId), statusCode: 400);

    try
    {
        var updated = await repo.UpdateTourismLeadAsync(
            tenantContext.TenantId, id, request.Status, request.Notes,
            request.PatientName, request.BudgetCurrency, request.BudgetAmount);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingTourismLeadNotFound, "Tourism lead not found", requestId), statusCode: 404);
        jsonLog.StepInfo($"Tourism lead updated: id={id}, status={request.Status}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Tourism lead update failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism lead update failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/tourism/stats", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    try
    {
        var stats = await repo.GetTourismStatsAsync(tenantContext.TenantId);
        return Results.Ok(stats);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tourism stats query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingTourismStatsFailed, "Tourism stats query failed", "-"), statusCode: 500);
    }
});

// ============================================
// RESCUE RISK ENDPOINTS (GR-3.24)
// ============================================

app.MapPost("/api/v1/rescue/risks", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    RiskCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<RiskCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in risk create: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.CustomerPhone) || request.RiskScore < 0 || request.RiskScore > 100)
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskPayload, "customer_phone and risk_score (0-100) are required", requestId), statusCode: 400);

    var validLevels = new[] { "low", "medium", "high", "critical" };
    if (string.IsNullOrWhiteSpace(request.RiskLevel) || !validLevels.Contains(request.RiskLevel))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskPayload, $"risk_level must be one of: {string.Join(", ", validLevels)}", requestId), statusCode: 400);

    try
    {
        var id = await repo.CreateReviewRiskAsync(
            tenantContext.TenantId, request.CustomerPhone, request.ConversationId,
            request.RiskScore, request.RiskLevel, request.TriggerReason);
        jsonLog.StepInfo($"Review risk created: id={id}, phone={request.CustomerPhone}, level={request.RiskLevel}", requestId);
        return Results.Json(new { id }, statusCode: 201);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Review risk creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Review risk creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/rescue/risks", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var level = ctx.Request.Query["level"].FirstOrDefault();
    var status = ctx.Request.Query["status"].FirstOrDefault();

    try
    {
        var risks = await repo.ListReviewRisksAsync(tenantContext.TenantId, level, status);
        return Results.Ok(new { risks });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Review risks query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Review risks query failed", "-"), statusCode: 500);
    }
});

app.MapPut("/api/v1/rescue/risks/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    RiskUpdateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<RiskUpdateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in risk update: {ex.Message}"); request = null; }

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskPayload, "Request body is required", requestId), statusCode: 400);

    // Validate rescue_status if provided
    if (request.RescueStatus != null)
    {
        var validStatuses = new[] { "pending", "in_progress", "rescued", "failed", "expired" };
        if (!validStatuses.Contains(request.RescueStatus))
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskStatus, $"Invalid rescue_status. Valid values: {string.Join(", ", validStatuses)}", requestId), statusCode: 400);
    }

    // Validate rescue_strategy if provided
    if (request.RescueStrategy != null)
    {
        var validStrategies = new[] { "apology", "discount", "free_return", "exchange", "full_refund" };
        if (!validStrategies.Contains(request.RescueStrategy))
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskPayload, $"Invalid rescue_strategy. Valid values: {string.Join(", ", validStrategies)}", requestId), statusCode: 400);
    }

    // Validate customer_response if provided
    if (request.CustomerResponse != null)
    {
        var validResponses = new[] { "satisfied", "unsatisfied", "no_response" };
        if (!validResponses.Contains(request.CustomerResponse))
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskPayload, $"Invalid customer_response. Valid values: {string.Join(", ", validResponses)}", requestId), statusCode: 400);
    }

    try
    {
        var updated = await repo.UpdateReviewRiskAsync(
            tenantContext.TenantId, id, request.RescueStatus, request.RescueStrategy,
            request.RescueCost, request.CustomerResponse, request.ReviewPosted, request.ReviewRating);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingRiskNotFound, "Review risk not found", requestId), statusCode: 404);
        jsonLog.StepInfo($"Review risk updated: id={id}, status={request.RescueStatus}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Review risk update failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Review risk update failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/rescue/stats", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    try
    {
        var stats = await repo.GetRescueStatsAsync(tenantContext.TenantId);
        return Results.Ok(stats);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Rescue stats query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingRescueStatsFailed, "Rescue stats query failed", "-"), statusCode: 500);
    }
});

// ============================================
// PKT-12 Faz 3: RESCUE FOLLOW-UP ENDPOINTS
// ============================================

// Intentionally cross-tenant: follow-up scheduler queries all tenants at once (ops-level batch job)
app.MapGet("/api/v1/rescue/risks/followup-due", async (MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    try
    {
        var risks = await repo.GetFollowUpDueRisksAsync();
        return Results.Ok(new { risks });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.MarketingFollowUpQueryFailed}] Follow-up due query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingFollowUpQueryFailed, "Follow-up due query failed", "-"), statusCode: 500);
    }
});

app.MapPut("/api/v1/rescue/risks/{id:int}/followup", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    FollowUpUpdateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<FollowUpUpdateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in followup update: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.FollowUpStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskPayload, "followup_status is required", requestId), statusCode: 400);

    var validStatuses = new[] { "none", "satisfaction_sent", "review_redirect_sent", "completed", "closed" };
    if (!validStatuses.Contains(request.FollowUpStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidRiskStatus, $"Invalid followup_status. Valid: {string.Join(", ", validStatuses)}", requestId), statusCode: 400);

    try
    {
        var updated = await repo.UpdateFollowUpStatusAsync(tenantContext.TenantId, id, request.FollowUpStatus);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingRiskNotFound, "Review risk not found", requestId), statusCode: 404);
        jsonLog.StepInfo($"Follow-up status updated: id={id}, status={request.FollowUpStatus}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Follow-up status update failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Follow-up status update failed", requestId), statusCode: 500);
    }
});

// ============================================
// RESCUE TEMPLATE ENDPOINTS (GR-3.24)
// ============================================

app.MapPost("/api/v1/rescue/templates", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    RescueTemplateCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<RescueTemplateCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in rescue template create: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.TemplateName) || string.IsNullOrWhiteSpace(request.MessageTemplate))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTemplatePayload, "template_name and message_template are required", requestId), statusCode: 400);

    var validLevels = new[] { "low", "medium", "high", "critical" };
    if (string.IsNullOrWhiteSpace(request.RiskLevel) || !validLevels.Contains(request.RiskLevel))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTemplatePayload, $"risk_level must be one of: {string.Join(", ", validLevels)}", requestId), statusCode: 400);

    var validStrategies = new[] { "apology", "discount", "free_return", "exchange", "full_refund" };
    if (string.IsNullOrWhiteSpace(request.Strategy) || !validStrategies.Contains(request.Strategy))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTemplatePayload, $"strategy must be one of: {string.Join(", ", validStrategies)}", requestId), statusCode: 400);

    try
    {
        var id = await repo.CreateRescueTemplateAsync(
            tenantContext.TenantId, request.TemplateName, request.RiskLevel,
            request.Strategy, request.MessageTemplate, request.MaxDiscountPct);
        jsonLog.StepInfo($"Rescue template created: id={id}, name={request.TemplateName}", requestId);
        return Results.Json(new { id }, statusCode: 201);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Rescue template creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Rescue template creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/rescue/templates", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var level = ctx.Request.Query["level"].FirstOrDefault();
    var activeStr = ctx.Request.Query["active"].FirstOrDefault();
    var activeOnly = activeStr != "false"; // default: only active

    try
    {
        var templates = await repo.ListRescueTemplatesAsync(tenantContext.TenantId, level, activeOnly);
        return Results.Ok(new { templates });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Rescue templates query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Rescue templates query failed", "-"), statusCode: 500);
    }
});

app.MapPut("/api/v1/rescue/templates/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    RescueTemplateUpdateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<RescueTemplateUpdateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in rescue template update: {ex.Message}"); request = null; }

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidTemplatePayload, "Request body is required", requestId), statusCode: 400);

    try
    {
        var updated = await repo.UpdateRescueTemplateAsync(
            tenantContext.TenantId, id, request.TemplateName, request.MessageTemplate,
            request.MaxDiscountPct, request.IsActive);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingTemplateNotFound, "Rescue template not found", requestId), statusCode: 404);
        jsonLog.StepInfo($"Rescue template updated: id={id}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Rescue template update failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Rescue template update failed", requestId), statusCode: 500);
    }
});

app.MapDelete("/api/v1/rescue/templates/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        var deactivated = await repo.DeactivateRescueTemplateAsync(tenantContext.TenantId, id);
        if (!deactivated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingTemplateNotFound, "Rescue template not found or already inactive", requestId), statusCode: 404);
        jsonLog.StepInfo($"Rescue template deactivated: id={id}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Rescue template deactivation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Rescue template deactivation failed", requestId), statusCode: 500);
    }
});

// ============================================
// TREATMENT CATALOG ENDPOINTS (GR-3.25)
// ============================================

app.MapPost("/api/v1/tourism/catalog", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    TreatmentCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<TreatmentCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in treatment create: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.TreatmentName))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidCatalogPayload, "treatment_name is required", requestId), statusCode: 400);

    try
    {
        var id = await repo.CreateTreatmentAsync(
            tenantContext.TenantId, request.TreatmentName, request.TreatmentNameEn,
            request.Category, request.PriceMin, request.PriceMax,
            request.PriceCurrency ?? "EUR", request.DurationDays, request.RecoveryDays,
            request.DescriptionTr, request.DescriptionEn, request.PackageIncludes);
        jsonLog.StepInfo($"Treatment created: id={id}, name={request.TreatmentName}", requestId);
        return Results.Json(new { id }, statusCode: 201);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Treatment creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Treatment creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/tourism/catalog", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var category = ctx.Request.Query["category"].FirstOrDefault();
    var activeStr = ctx.Request.Query["active"].FirstOrDefault();
    var activeOnly = activeStr != "false";

    try
    {
        var treatments = await repo.ListTreatmentsAsync(tenantContext.TenantId, category, activeOnly);
        return Results.Ok(new { treatments });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Treatment catalog query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Treatment catalog query failed", "-"), statusCode: 500);
    }
});

app.MapPut("/api/v1/tourism/catalog/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    TreatmentUpdateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<TreatmentUpdateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in treatment update: {ex.Message}"); request = null; }

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidCatalogPayload, "Request body is required", requestId), statusCode: 400);

    try
    {
        var updated = await repo.UpdateTreatmentAsync(
            tenantContext.TenantId, id, request.TreatmentName, request.TreatmentNameEn,
            request.Category, request.PriceMin, request.PriceMax, request.PriceCurrency,
            request.DurationDays, request.RecoveryDays, request.DescriptionTr,
            request.DescriptionEn, request.PackageIncludes, request.IsActive);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingCatalogItemNotFound, "Treatment not found", requestId), statusCode: 404);
        jsonLog.StepInfo($"Treatment updated: id={id}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Treatment update failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Treatment update failed", requestId), statusCode: 500);
    }
});

app.MapDelete("/api/v1/tourism/catalog/{id:int}", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog, int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        var deactivated = await repo.DeactivateTreatmentAsync(tenantContext.TenantId, id);
        if (!deactivated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingCatalogItemNotFound, "Treatment not found or already inactive", requestId), statusCode: 404);
        jsonLog.StepInfo($"Treatment deactivated: id={id}", requestId);
        return Results.Ok(new { success = true });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Treatment deactivation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Treatment deactivation failed", requestId), statusCode: 500);
    }
});

// ============================================
// TOURISM CONVERSATION ENDPOINTS (GR-3.25)
// ============================================

app.MapPost("/api/v1/tourism/conversations", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    ConversationCreateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<ConversationCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in conversation create: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.PatientPhone) || string.IsNullOrWhiteSpace(request.PatientLang) || string.IsNullOrWhiteSpace(request.PatientMessage))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidConversationPayload, "patient_phone, patient_lang and patient_message are required", requestId), statusCode: 400);

    try
    {
        var id = await repo.CreateTourismConversationAsync(
            tenantContext.TenantId, request.LeadId, request.PatientPhone, request.PatientLang,
            request.PatientCountry, request.PatientMessage, request.DetectedIntent,
            request.AiResponse, request.AiResponseLang, request.TrTranslation,
            request.TreatmentInterest, request.ResponseGenerated);
        jsonLog.StepInfo($"Tourism conversation recorded: id={id}, phone={request.PatientPhone}, lang={request.PatientLang}", requestId);
        return Results.Json(new { id }, statusCode: 201);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Tourism conversation creation failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism conversation creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/tourism/conversations", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var lang = ctx.Request.Query["lang"].FirstOrDefault();
    var country = ctx.Request.Query["country"].FirstOrDefault();

    try
    {
        var conversations = await repo.ListTourismConversationsAsync(tenantContext.TenantId, lang, country);
        return Results.Ok(new { conversations });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tourism conversations query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism conversations query failed", "-"), statusCode: 500);
    }
});

app.MapPost("/api/v1/tourism/respond", async (HttpContext ctx, MarketingRepository repo, TourismResponseGenerator generator, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    TourismRespondRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<TourismRespondRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"[{requestId}] Malformed JSON in tourism respond: {ex.Message}"); request = null; }

    if (request == null || string.IsNullOrWhiteSpace(request.PatientPhone) || string.IsNullOrWhiteSpace(request.PatientLang) || string.IsNullOrWhiteSpace(request.PatientMessage))
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingInvalidConversationPayload, "patient_phone, patient_lang and patient_message are required", requestId), statusCode: 400);

    try
    {
        // Fetch active treatments for Claude context
        var catalog = await repo.GetActiveTreatmentsForResponseAsync(tenantContext.TenantId);

        var result = await generator.GenerateResponseAsync(
            tenantContext.TenantId, request.PatientLang, request.PatientMessage,
            request.PatientCountry, request.TreatmentInterest, catalog);

        if (result == null)
        {
            jsonLog.SystemWarn($"[{requestId}] Claude response generation returned null (tenant={tenantContext.TenantId})");
            return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingClaudeUnavailable, "AI response service temporarily unavailable", requestId), statusCode: 503);
        }

        // Persist conversation record
        var convId = await repo.CreateTourismConversationAsync(
            tenantContext.TenantId, request.LeadId, request.PatientPhone, request.PatientLang,
            request.PatientCountry, request.PatientMessage, result.DetectedIntent,
            result.Response, result.ResponseLang, result.TrTranslation,
            request.TreatmentInterest, true);

        jsonLog.StepInfo($"Tourism response generated: conv_id={convId}, lang={request.PatientLang}, intent={result.DetectedIntent}, ms={result.ProcessingTimeMs}", requestId);
        return Results.Ok(new
        {
            conversation_id = convId,
            response = result.Response,
            response_lang = result.ResponseLang,
            tr_translation = result.TrTranslation,
            detected_intent = result.DetectedIntent,
            processing_time_ms = result.ProcessingTimeMs
        });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{requestId}] Tourism respond DB error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Tourism respond failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/tourism/conversations/stats", async (HttpContext ctx, MarketingRepository repo, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    try
    {
        var stats = await repo.GetTourismConversationStatsAsync(tenantContext.TenantId);
        return Results.Ok(stats);
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tourism conversation stats query failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.MarketingConversationStatsFailed, "Conversation stats query failed", "-"), statusCode: 500);
    }
});

// ============================================
// STARTUP
// ============================================

logger.SystemInfo($"Marketing service starting on port {listenPort}");
app.Run();

public partial class Program { }

// ============================================
// REQUEST DTOs
// ============================================

public sealed class ReviewCreateRequest
{
    public string PatientPhone { get; set; } = "";
    public string? PatientName { get; set; }
    public string? TreatmentType { get; set; }
    public short? SatisfactionScore { get; set; }
    public string? ReviewLinkUrl { get; set; }
    public string? Platform { get; set; }
}

public sealed class ReviewPostedRequest
{
    public short ReviewRating { get; set; }
}

public sealed class ReferralCreateRequest
{
    public string ReferrerPhone { get; set; } = "";
    public string? ReferrerName { get; set; }
    public short DiscountPct { get; set; }
    public string? ReferrerReward { get; set; }
}

public sealed class ReferralRedeemRequest
{
    public string RefereePhone { get; set; } = "";
    public string? RefereeName { get; set; }
}

public sealed class TourismLeadCreateRequest
{
    public string PatientPhone { get; set; } = "";
    public string? PatientName { get; set; }
    public string? PatientCountry { get; set; }
    public string? PatientLang { get; set; }
    public string? TreatmentInterest { get; set; }
    public bool AccommodationNeeded { get; set; }
    public bool TransferNeeded { get; set; }
    public string? BudgetCurrency { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public sealed class TourismLeadUpdateRequest
{
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? PatientName { get; set; }
    public string? BudgetCurrency { get; set; }
    public decimal? BudgetAmount { get; set; }
}

// GR-3.24: Review Rescue Request DTOs
public sealed class RiskCreateRequest
{
    public string CustomerPhone { get; set; } = "";
    public string? ConversationId { get; set; }
    public short RiskScore { get; set; }
    public string RiskLevel { get; set; } = "";
    public string? TriggerReason { get; set; }
}

public sealed class RiskUpdateRequest
{
    public string? RescueStatus { get; set; }
    public string? RescueStrategy { get; set; }
    public decimal? RescueCost { get; set; }
    public string? CustomerResponse { get; set; }
    public bool? ReviewPosted { get; set; }
    public short? ReviewRating { get; set; }
}

/// <summary>PKT-12 Faz 3: Follow-up status update request.</summary>
public sealed class FollowUpUpdateRequest
{
    public string FollowUpStatus { get; set; } = "";
}

public sealed class RescueTemplateCreateRequest
{
    public string TemplateName { get; set; } = "";
    public string RiskLevel { get; set; } = "";
    public string Strategy { get; set; } = "";
    public string MessageTemplate { get; set; } = "";
    public short? MaxDiscountPct { get; set; }
}

public sealed class RescueTemplateUpdateRequest
{
    public string? TemplateName { get; set; }
    public string? MessageTemplate { get; set; }
    public short? MaxDiscountPct { get; set; }
    public bool? IsActive { get; set; }
}

// GR-3.25: Tourism Catalog + Conversation Request DTOs
public sealed class TreatmentCreateRequest
{
    public string TreatmentName { get; set; } = "";
    public string? TreatmentNameEn { get; set; }
    public string? Category { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public string? PriceCurrency { get; set; }
    public short? DurationDays { get; set; }
    public short? RecoveryDays { get; set; }
    public string? DescriptionTr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? PackageIncludes { get; set; }
}

public sealed class TreatmentUpdateRequest
{
    public string? TreatmentName { get; set; }
    public string? TreatmentNameEn { get; set; }
    public string? Category { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public string? PriceCurrency { get; set; }
    public short? DurationDays { get; set; }
    public short? RecoveryDays { get; set; }
    public string? DescriptionTr { get; set; }
    public string? DescriptionEn { get; set; }
    public string? PackageIncludes { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class ConversationCreateRequest
{
    public int? LeadId { get; set; }
    public string PatientPhone { get; set; } = "";
    public string PatientLang { get; set; } = "";
    public string? PatientCountry { get; set; }
    public string PatientMessage { get; set; } = "";
    public string? DetectedIntent { get; set; }
    public string? AiResponse { get; set; }
    public string? AiResponseLang { get; set; }
    public string? TrTranslation { get; set; }
    public string? TreatmentInterest { get; set; }
    public bool ResponseGenerated { get; set; }
}

public sealed class TourismRespondRequest
{
    public int? LeadId { get; set; }
    public string PatientPhone { get; set; } = "";
    public string PatientLang { get; set; } = "";
    public string? PatientCountry { get; set; }
    public string PatientMessage { get; set; } = "";
    public string? TreatmentInterest { get; set; }
}
