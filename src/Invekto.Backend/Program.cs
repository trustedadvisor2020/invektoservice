using System.Net.Http.Headers;
using System.Text;
using Invekto.Backend.Services;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Logging.Reader;

var builder = WebApplication.CreateBuilder(args);

// Read configuration
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var opsUsername = builder.Configuration["Ops:Username"] ?? "admin";
var opsPassword = builder.Configuration["Ops:Password"] ?? "admin123"; // Stage-0 default
var slowThresholdMs = builder.Configuration.GetValue<int>("Ops:SlowThresholdMs", 500);
var microserviceUrl = builder.Configuration["Microservice:ChatAnalysis:Url"]
    ?? $"http://localhost:{ServiceConstants.ChatAnalysisPort}";
var microserviceLogPath = builder.Configuration["Microservice:ChatAnalysis:LogPath"];

// Register JSON Lines logger
builder.Services.AddSingleton(new JsonLinesLogger(ServiceConstants.BackendServiceName, logPath));

// Register log reader for /ops (aggregate backend + microservice logs)
var logPaths = new List<string> { logPath };
if (!string.IsNullOrEmpty(microserviceLogPath))
{
    logPaths.Add(microserviceLogPath);
}
builder.Services.AddSingleton(new LogReader(logPaths.ToArray(), slowThresholdMs));

// Register log cleanup service (30 day retention)
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// Configure ChatAnalysis HTTP client with 600ms timeout (Stage-0 rule)
builder.Services.AddHttpClient<ChatAnalysisClient>(client =>
{
    client.BaseAddress = new Uri(microserviceUrl);
    client.Timeout = TimeSpan.FromMilliseconds(ServiceConstants.BackendToMicroserviceTimeoutMs);
});

var app = builder.Build();

// Start log cleanup service
_ = app.Services.GetRequiredService<LogCleanupService>();

var logger = app.Services.GetRequiredService<JsonLinesLogger>();

// Health endpoint (no auth, no logging)
app.MapGet("/health", () =>
{
    return Results.Ok(HealthResponse.Ok(ServiceConstants.BackendServiceName));
});

// Basic Auth check for /ops endpoints
bool ValidateOpsAuth(HttpContext ctx)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic "))
        return false;

    try
    {
        var encoded = authHeader["Basic ".Length..];
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var parts = decoded.Split(':', 2);
        return parts.Length == 2 && parts[0] == opsUsername && parts[1] == opsPassword;
    }
    catch
    {
        return false;
    }
}

// OPS endpoint - Stage-0 troubleshooting dashboard
app.MapGet("/ops", async (HttpContext ctx, ChatAnalysisClient chatClient, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var chatHealthy = await chatClient.CheckHealthAsync();

    var ops = new
    {
        status = "ok",
        timestamp = DateTime.UtcNow,
        services = new
        {
            backend = new { status = "ok" },
            chatAnalysis = new { status = chatHealthy ? "ok" : "unavailable" }
        },
        info = new
        {
            stage = "Stage-0",
            timeout_ms = ServiceConstants.BackendToMicroserviceTimeoutMs,
            retry_count = ServiceConstants.RetryCount,
            slow_threshold_ms = slowThresholdMs
        }
    };

    return Results.Ok(ops);
});

// OPS: Last 100 errors
app.MapGet("/ops/errors", async (HttpContext ctx, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var errors = await logReader.GetLastErrorsAsync(100);
    return Results.Ok(new { count = errors.Count, errors });
});

// OPS: Last 100 slow requests
app.MapGet("/ops/slow", async (HttpContext ctx, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var slow = await logReader.GetLastSlowRequestsAsync(100);
    return Results.Ok(new { count = slow.Count, threshold_ms = slowThresholdMs, requests = slow });
});

// OPS: Search by requestId
app.MapGet("/ops/search", async (HttpContext ctx, LogReader logReader, string? requestId) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(requestId))
    {
        return Results.BadRequest(new { error = "requestId query parameter required" });
    }

    var entries = await logReader.SearchByRequestIdAsync(requestId);
    return Results.Ok(new { requestId, count = entries.Count, entries });
});

// Chat analysis proxy endpoint
app.MapPost("/api/v1/chat/analyze", async (
    HttpContext ctx,
    ChatAnalysisClient chatClient,
    JsonLinesLogger jsonLogger) =>
{
    // Pass-through X-Request-Id if provided, otherwise generate new
    var context = RequestContext.CreateWithPassThrough(
        ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault(),
        ctx.Request.Headers[HeaderNames.TenantId].FirstOrDefault() ?? "default",
        ctx.Request.Headers[HeaderNames.ChatId].FirstOrDefault() ?? "default");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    var result = await chatClient.AnalyzeAsync(context);
    sw.Stop();

    if (result.IsSuccess)
    {
        jsonLogger.RequestInfo("Chat analysis completed", context, "/api/v1/chat/analyze", sw.ElapsedMilliseconds);
        return Results.Ok(result.Data);
    }

    // Stage-0: Return partial instead of fail, use correct error code
    jsonLogger.RequestWarn(
        $"Chat analysis partial: {result.Warning}",
        context,
        "/api/v1/chat/analyze",
        sw.ElapsedMilliseconds,
        result.ErrorCode ?? ErrorCodes.BackendMicroserviceError);

    return Results.Ok(new
    {
        requestId = context.RequestId,
        status = "partial",
        warning = result.Warning,
        errorCode = result.ErrorCode,
        timestamp = DateTime.UtcNow
    });
});

logger.SystemInfo($"Backend starting on port {ServiceConstants.BackendPort}");
app.Run();
