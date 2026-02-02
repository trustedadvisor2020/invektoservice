using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.ChatAnalysisPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";

// Configure Kestrel to listen on configured port
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(listenPort);
});

// Register JSON Lines logger
builder.Services.AddSingleton(new JsonLinesLogger(ServiceConstants.ChatAnalysisServiceName, logPath));

// Register log cleanup service (30 day retention)
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

var app = builder.Build();

// Start log cleanup service
_ = app.Services.GetRequiredService<LogCleanupService>();

var logger = app.Services.GetRequiredService<JsonLinesLogger>();

// Health endpoint - Stage-0 requirement (no logging for health checks)
app.MapGet("/health", () =>
{
    return Results.Ok(HealthResponse.Ok(ServiceConstants.ChatAnalysisServiceName));
});

// Ready endpoint (same as health for Stage-0)
app.MapGet("/ready", () =>
{
    return Results.Ok(HealthResponse.Ok(ServiceConstants.ChatAnalysisServiceName));
});

// Chat analysis endpoint
app.MapPost("/api/v1/analyze", (HttpContext ctx, JsonLinesLogger jsonLogger) =>
{
    // Pass-through X-Request-Id if provided
    var context = RequestContext.CreateWithPassThrough(
        ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault(),
        ctx.Request.Headers[HeaderNames.TenantId].FirstOrDefault() ?? "-",
        ctx.Request.Headers[HeaderNames.ChatId].FirstOrDefault() ?? "-");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        // TODO: Implement actual chat analysis logic
        // For now, return a placeholder response
        var result = new
        {
            requestId = context.RequestId,
            status = "ok",
            message = "Chat analysis placeholder - Stage-0",
            timestamp = DateTime.UtcNow
        };

        sw.Stop();
        jsonLogger.RequestInfo("Chat analysis completed", context, "/api/v1/analyze", sw.ElapsedMilliseconds);

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        sw.Stop();
        jsonLogger.RequestError(
            $"Chat analysis failed: {ex.Message}",
            context,
            "/api/v1/analyze",
            sw.ElapsedMilliseconds,
            ErrorCodes.ChatAnalysisProcessingFailed);

        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.ChatAnalysisProcessingFailed,
                "Chat analysis processing failed",
                context.RequestId,
                ex.Message),
            statusCode: 500);
    }
});

logger.SystemInfo($"ChatAnalysis service starting on port {listenPort}");
app.Run();
