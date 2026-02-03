using System.Net.Http.Headers;
using System.Text;
using Invekto.Backend.Services;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.ChatAnalysis;
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

// Configure Kestrel to listen on configured port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(ServiceConstants.BackendPort);
});

// Configure ChatAnalysis HTTP client with 600ms timeout (Stage-0 rule)
builder.Services.AddHttpClient<ChatAnalysisClient>(client =>
{
    client.BaseAddress = new Uri(microserviceUrl);
    client.Timeout = TimeSpan.FromMilliseconds(ServiceConstants.BackendToMicroserviceTimeoutMs);
});

var app = builder.Build();

// Enable static file serving for Dashboard UI (wwwroot/)
app.UseStaticFiles();

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

// ============================================
// DASHBOARD API ENDPOINTS (/api/ops/*)
// ============================================

// Dashboard: Service health with response times
app.MapGet("/api/ops/health", async (HttpContext ctx, ChatAnalysisClient chatClient) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var services = new List<object>();
    var now = DateTime.UtcNow;

    // Backend (self) - always ok
    services.Add(new
    {
        name = ServiceConstants.BackendServiceName,
        status = "ok",
        responseTimeMs = 0,
        uptimeSeconds = (long?)null, // Not tracked
        lastCheck = now
    });

    // ChatAnalysis - check health with timing
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var chatHealthy = await chatClient.CheckHealthAsync();
    sw.Stop();

    services.Add(new
    {
        name = ServiceConstants.ChatAnalysisServiceName,
        status = chatHealthy ? "ok" : "unavailable",
        responseTimeMs = chatHealthy ? (int?)sw.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null, // Not tracked
        lastCheck = now,
        error = chatHealthy ? null : "Service unreachable"
    });

    return Results.Ok(new
    {
        timestamp = now,
        services,
        info = new
        {
            stage = "Stage-0",
            timeout_ms = ServiceConstants.BackendToMicroserviceTimeoutMs,
            retry_count = ServiceConstants.RetryCount,
            slow_threshold_ms = slowThresholdMs
        }
    });
});

// Dashboard: Log stream with filters
app.MapGet("/api/ops/logs/stream", async (
    HttpContext ctx,
    LogReader logReader,
    string? level,
    string? service,
    string? search,
    string? after,
    int? limit) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var options = new LogQueryOptions
    {
        Levels = string.IsNullOrEmpty(level) ? null : level.Split(','),
        Service = service,
        Search = search,
        After = string.IsNullOrEmpty(after) ? null : DateTime.Parse(after),
        Limit = limit ?? 100
    };

    var result = await logReader.QueryLogsAsync(options);
    return Results.Ok(new
    {
        entries = result.Entries,
        hasMore = result.HasMore,
        nextCursor = result.NextCursor
    });
});

// Dashboard: Log context (+-N lines around entry)
app.MapGet("/api/ops/logs/context", async (
    HttpContext ctx,
    LogReader logReader,
    string? file,
    int? line,
    int? range) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(file) || !line.HasValue)
    {
        return Results.BadRequest(new { error = "file and line parameters required" });
    }

    var result = await logReader.GetLogContextAsync(file, line.Value, range ?? 10);
    return Results.Ok(new
    {
        target = result.Target,
        before = result.Before,
        after = result.After
    });
});

// Dashboard: Error stats by hour
app.MapGet("/api/ops/stats/errors", async (HttpContext ctx, LogReader logReader, int? hours) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var result = await logReader.GetErrorStatsAsync(hours ?? 24);
    return Results.Ok(new
    {
        buckets = result.Buckets,
        total = result.Total
    });
});

// Dashboard: Service restart (Windows Service)
app.MapPost("/api/ops/services/{serviceName}/restart", async (HttpContext ctx, string serviceName) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    // Map service name to Windows Service name
    var windowsServiceName = serviceName switch
    {
        "Invekto.ChatAnalysis" => "Invekto.Microservice.Chat",
        _ => null
    };

    if (windowsServiceName == null)
    {
        return Results.BadRequest(new
        {
            success = false,
            service = serviceName,
            message = "Unknown service or restart not supported"
        });
    }

    try
    {
        // Try to restart Windows Service
        using var sc = new System.ServiceProcess.ServiceController(windowsServiceName);
        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Running)
        {
            sc.Stop();
            sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        }
        sc.Start();
        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

        return Results.Ok(new
        {
            success = true,
            service = serviceName,
            message = "Service restarted successfully"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Ok(new
        {
            success = false,
            service = serviceName,
            message = $"Service not found or not installed: {ex.Message}"
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            success = false,
            service = serviceName,
            message = $"Restart failed: {ex.Message}"
        });
    }
});

// Dashboard: Test proxy for external services (avoids CORS issues)
app.MapGet("/api/ops/test/{serviceName}/{*path}", async (HttpContext ctx, ChatAnalysisClient chatClient, string serviceName, string? path) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    try
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (serviceName == "chatanalysis")
        {
            // Forward to ChatAnalysis service
            var endpoint = "/" + (path ?? "health");
            var result = await chatClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        return Results.BadRequest(new { success = false, message = "Unknown service" });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            success = false,
            statusCode = 0,
            durationMs = 0,
            message = ex.Message
        });
    }
});

// Chat analysis proxy endpoint (V2 - async with callback)
app.MapPost("/api/v1/chat/analyze", async (
    HttpContext ctx,
    ChatAnalysisClient chatClient,
    JsonLinesLogger jsonLogger,
    ChatAnalysisRequest? analysisRequest) =>
{
    // Pass-through X-Request-Id if provided, otherwise generate new
    var context = RequestContext.CreateWithPassThrough(
        ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault(),
        ctx.Request.Headers[HeaderNames.TenantId].FirstOrDefault() ?? "default",
        ctx.Request.Headers[HeaderNames.ChatId].FirstOrDefault() ?? "default");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Validate request
    if (analysisRequest == null || string.IsNullOrWhiteSpace(analysisRequest.RequestID))
    {
        sw.Stop();
        jsonLogger.RequestError(
            "Invalid request: missing RequestID",
            context,
            "/api/v1/chat/analyze",
            sw.ElapsedMilliseconds,
            ErrorCodes.GeneralValidation);

        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.GeneralValidation,
                "Geçersiz istek: RequestID zorunlu",
                context.RequestId),
            statusCode: 400);
    }

    if (string.IsNullOrWhiteSpace(analysisRequest.ChatServerURL))
    {
        sw.Stop();
        jsonLogger.RequestError(
            "Invalid request: missing ChatServerURL",
            context,
            "/api/v1/chat/analyze",
            sw.ElapsedMilliseconds,
            ErrorCodes.GeneralValidation);

        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.GeneralValidation,
                "Geçersiz istek: ChatServerURL zorunlu",
                context.RequestId),
            statusCode: 400);
    }

    var result = await chatClient.SubmitAnalysisAsync(context, analysisRequest);
    sw.Stop();

    if (result.IsSuccess)
    {
        jsonLogger.RequestInfo("Chat analysis submitted", context, "/api/v1/chat/analyze", sw.ElapsedMilliseconds);
        return Results.Ok(result.Data);
    }

    // Submission failed
    jsonLogger.RequestWarn(
        $"Chat analysis submission failed: {result.ErrorMessage}",
        context,
        "/api/v1/chat/analyze",
        sw.ElapsedMilliseconds,
        result.ErrorCode ?? ErrorCodes.BackendMicroserviceError);

    return Results.Json(
        ErrorResponse.Create(
            result.ErrorCode ?? ErrorCodes.BackendMicroserviceError,
            result.ErrorMessage ?? "Analiz isteği gönderilemedi",
            context.RequestId),
        statusCode: 502);
});

// SPA fallback - serve index.html for non-API routes (Dashboard routing)
app.MapFallbackToFile("index.html");

logger.SystemInfo($"Backend starting on port {ServiceConstants.BackendPort}");
app.Run();

// Required for integration tests
public partial class Program { }
