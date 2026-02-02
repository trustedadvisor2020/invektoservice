using Invekto.Backend.Services;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Register JSON Lines logger
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
builder.Services.AddSingleton(new JsonLinesLogger(
    ServiceConstants.BackendServiceName,
    logPath));

// Configure ChatAnalysis HTTP client with 600ms timeout (Stage-0 rule)
builder.Services.AddHttpClient<ChatAnalysisClient>(client =>
{
    client.BaseAddress = new Uri($"http://localhost:{ServiceConstants.ChatAnalysisPort}");
    client.Timeout = TimeSpan.FromMilliseconds(ServiceConstants.BackendToMicroserviceTimeoutMs);
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<JsonLinesLogger>();

// Health endpoint
app.MapGet("/health", () =>
{
    return Results.Ok(HealthResponse.Ok(ServiceConstants.BackendServiceName));
});

// OPS endpoint - Stage-0 minimum troubleshooting
app.MapGet("/ops", async (ChatAnalysisClient chatClient) =>
{
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
            retry_count = ServiceConstants.RetryCount
        }
    };

    return Results.Ok(ops);
});

// Chat analysis proxy endpoint
app.MapPost("/api/v1/chat/analyze", async (
    HttpContext ctx,
    ChatAnalysisClient chatClient,
    JsonLinesLogger jsonLogger) =>
{
    var context = RequestContext.Create(
        ctx.Request.Headers[HeaderNames.TenantId].FirstOrDefault() ?? "default",
        ctx.Request.Headers[HeaderNames.ChatId].FirstOrDefault() ?? "default");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    var result = await chatClient.AnalyzeAsync(context);
    sw.Stop();

    if (result.IsSuccess)
    {
        jsonLogger.Info("Chat analysis completed", context, "/api/v1/chat/analyze", sw.ElapsedMilliseconds);
        return Results.Ok(result.Data);
    }

    // Stage-0: Return partial instead of fail
    jsonLogger.Warn($"Chat analysis partial: {result.Warning}", context, "/api/v1/chat/analyze", ErrorCodes.BackendMicroserviceTimeout);

    return Results.Ok(new
    {
        requestId = context.RequestId,
        status = "partial",
        warning = result.Warning,
        timestamp = DateTime.UtcNow
    });
});

logger.Info($"Backend starting on port {ServiceConstants.BackendPort}");
app.Run();
