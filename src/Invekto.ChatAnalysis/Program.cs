using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Configure Kestrel to listen on port 7101
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(ServiceConstants.ChatAnalysisPort);
});

// Register JSON Lines logger
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
builder.Services.AddSingleton(new JsonLinesLogger(
    ServiceConstants.ChatAnalysisServiceName,
    logPath));

var app = builder.Build();

var logger = app.Services.GetRequiredService<JsonLinesLogger>();

// Health endpoint - Stage-0 requirement
app.MapGet("/health", () =>
{
    logger.Info("Health check requested", route: "/health");
    return Results.Ok(HealthResponse.Ok(ServiceConstants.ChatAnalysisServiceName));
});

// Ready endpoint (same as health for Stage-0)
app.MapGet("/ready", () =>
{
    return Results.Ok(HealthResponse.Ok(ServiceConstants.ChatAnalysisServiceName));
});

// Placeholder for chat analysis endpoint
app.MapPost("/api/v1/analyze", (HttpContext ctx) =>
{
    var requestId = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantId = ctx.Request.Headers[HeaderNames.TenantId].FirstOrDefault() ?? "-";
    var chatId = ctx.Request.Headers[HeaderNames.ChatId].FirstOrDefault() ?? "-";

    var context = new RequestContext
    {
        RequestId = requestId,
        TenantId = tenantId,
        ChatId = chatId
    };

    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        // TODO: Implement actual chat analysis logic
        // For now, return a placeholder response
        var result = new
        {
            requestId,
            status = "ok",
            message = "Chat analysis placeholder - Stage-0",
            timestamp = DateTime.UtcNow
        };

        sw.Stop();
        logger.Info("Chat analysis completed", context, "/api/v1/analyze", sw.ElapsedMilliseconds);

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        sw.Stop();
        logger.Error($"Chat analysis failed: {ex.Message}", context, "/api/v1/analyze", ErrorCodes.ChatAnalysisProcessingFailed);

        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.ChatAnalysisProcessingFailed,
                "Chat analysis processing failed",
                requestId,
                ex.Message),
            statusCode: 500);
    }
});

logger.Info($"ChatAnalysis service starting on port {ServiceConstants.ChatAnalysisPort}");
app.Run();
