using Invekto.ChatAnalysis.Services;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.ChatAnalysis;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.ChatAnalysisPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var wapCrmSecretKey = builder.Configuration["WapCrm:SecretKey"] ?? "";
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? "";

// Validate required config
if (string.IsNullOrEmpty(wapCrmSecretKey))
{
    Console.Error.WriteLine("FATAL: WapCrm:SecretKey is not configured");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(claudeApiKey))
{
    Console.Error.WriteLine("FATAL: Claude:ApiKey is not configured");
    Environment.Exit(1);
}

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

// Register WapCRM client
builder.Services.AddSingleton(new WapCrmClient(wapCrmSecretKey));

// Register Claude analyzer
builder.Services.AddSingleton(new ClaudeAnalyzer(claudeApiKey));

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
app.MapPost("/api/v1/analyze", async (
    HttpContext ctx,
    JsonLinesLogger jsonLogger,
    WapCrmClient wapCrmClient,
    ClaudeAnalyzer claudeAnalyzer,
    ChatAnalysisRequest? request) =>
{
    // Pass-through X-Request-Id if provided
    var context = RequestContext.CreateWithPassThrough(
        ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault(),
        ctx.Request.Headers[HeaderNames.TenantId].FirstOrDefault() ?? "-",
        ctx.Request.Headers[HeaderNames.ChatId].FirstOrDefault() ?? "-");

    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Validate request
    if (request == null || string.IsNullOrWhiteSpace(request.PhoneNumber))
    {
        sw.Stop();
        jsonLogger.RequestError(
            "Invalid request: missing phoneNumber",
            context,
            "/api/v1/analyze",
            sw.ElapsedMilliseconds,
            ErrorCodes.ChatAnalysisInvalidPayload);

        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.ChatAnalysisInvalidPayload,
                "Geçersiz istek: phoneNumber zorunlu",
                context.RequestId),
            statusCode: 400);
    }

    try
    {
        // Step 1: Fetch messages from WapCRM
        var messagesResult = await wapCrmClient.GetMessagesForPhoneAsync(
            request.PhoneNumber,
            request.InstanceId);

        if (!messagesResult.IsSuccess)
        {
            sw.Stop();
            jsonLogger.RequestError(
                $"WapCRM error: {messagesResult.ErrorMessage}",
                context,
                "/api/v1/analyze",
                sw.ElapsedMilliseconds,
                messagesResult.ErrorCode!);

            var statusCode = messagesResult.ErrorCode == ErrorCodes.ChatAnalysisNoMessages ? 404 : 502;

            return Results.Json(
                ErrorResponse.Create(
                    messagesResult.ErrorCode!,
                    GetUserMessage(messagesResult.ErrorCode!),
                    context.RequestId,
                    messagesResult.ErrorMessage),
                statusCode: statusCode);
        }

        var messages = messagesResult.Data!;

        // Step 2: Analyze with Claude
        var analysisResult = await claudeAnalyzer.AnalyzeAsync(messages);

        if (!analysisResult.IsSuccess)
        {
            sw.Stop();
            jsonLogger.RequestError(
                $"Claude error: {analysisResult.ErrorMessage}",
                context,
                "/api/v1/analyze",
                sw.ElapsedMilliseconds,
                analysisResult.ErrorCode!);

            return Results.Json(
                ErrorResponse.Create(
                    analysisResult.ErrorCode!,
                    GetUserMessage(analysisResult.ErrorCode!),
                    context.RequestId,
                    analysisResult.ErrorMessage),
                statusCode: 502);
        }

        var analysis = analysisResult.Data!;

        // Log warning if parse had issues (NOT silent - logged and confidence=0)
        if (!string.IsNullOrEmpty(analysisResult.Warning))
        {
            jsonLogger.SystemWarn($"Claude parse warning: {analysisResult.Warning}");
        }

        // Step 3: Build response
        var response = new ChatAnalysisResponse
        {
            RequestId = context.RequestId,
            PhoneNumber = request.PhoneNumber,
            MessageCount = messages.Count,
            Analysis = analysis,
            AnalyzedAt = DateTime.UtcNow
        };

        sw.Stop();
        jsonLogger.RequestInfo(
            $"Chat analysis completed: sentiment={analysis.Sentiment}, category={analysis.Category}",
            context,
            "/api/v1/analyze",
            sw.ElapsedMilliseconds);

        return Results.Ok(response);
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
                "Analiz işlemi başarısız oldu",
                context.RequestId,
                ex.Message),
            statusCode: 500);
    }
});

logger.SystemInfo($"ChatAnalysis service starting on port {listenPort}");
app.Run();

// Helper function for user-friendly error messages
static string GetUserMessage(string errorCode) => errorCode switch
{
    ErrorCodes.ChatAnalysisInvalidPayload => "Geçersiz istek formatı",
    ErrorCodes.ChatAnalysisProcessingFailed => "Analiz işlemi başarısız oldu",
    ErrorCodes.ChatAnalysisWapCrmError => "CRM servisine bağlanılamadı",
    ErrorCodes.ChatAnalysisWapCrmTimeout => "CRM servisi yanıt vermedi",
    ErrorCodes.ChatAnalysisClaudeError => "Analiz servisi hatası",
    ErrorCodes.ChatAnalysisClaudeTimeout => "Analiz servisi yanıt vermedi",
    ErrorCodes.ChatAnalysisNoMessages => "Bu numara için mesaj bulunamadı",
    _ => "Beklenmeyen bir hata oluştu"
};
