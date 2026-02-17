using Invekto.Marketing.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;
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

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);
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

// ============================================
// APP
// ============================================

var app = builder.Build();
app.UseTrafficLogging();
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");
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
        new() { Method = "GET", Path = "/api/v1/tourism/stats", Description = "Tourism statistics", Category = "Tourism" }
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
