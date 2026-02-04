using Invekto.ChatAnalysis.Middleware;
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
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? "";
var callbackToken = builder.Configuration["Callback:Token"] ?? "";

// Validate required config
if (string.IsNullOrEmpty(claudeApiKey))
{
    Console.Error.WriteLine("FATAL: Claude:ApiKey is not configured");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(callbackToken))
{
    Console.Error.WriteLine("FATAL: Callback:Token is not configured");
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

// Register Claude analyzer
builder.Services.AddSingleton(new ClaudeAnalyzer(claudeApiKey));

// Register Callback service
builder.Services.AddSingleton(sp =>
    new CallbackService(callbackToken, sp.GetRequiredService<JsonLinesLogger>()));

var app = builder.Build();

// Enable traffic logging middleware (logs all HTTP request/response)
app.UseTrafficLogging();

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

// Chat analysis endpoint (V2 - async processing)
app.MapPost("/api/v1/analyze", (
    HttpContext ctx,
    JsonLinesLogger jsonLogger,
    ClaudeAnalyzer claudeAnalyzer,
    CallbackService callbackService,
    ChatAnalysisRequest? request) =>
{
    // Validate request
    if (request == null)
    {
        jsonLogger.SystemWarn("Invalid request: null body");
        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.ChatAnalysisInvalidPayload,
                "Geçersiz istek: boş body",
                "-"),
            statusCode: 400);
    }

    if (string.IsNullOrWhiteSpace(request.RequestID))
    {
        jsonLogger.SystemWarn("Invalid request: missing RequestID");
        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.ChatAnalysisInvalidPayload,
                "Geçersiz istek: RequestID zorunlu",
                "-"),
            statusCode: 400);
    }

    if (string.IsNullOrWhiteSpace(request.ChatServerURL))
    {
        jsonLogger.SystemWarn($"Invalid request: missing ChatServerURL, RequestID={request.RequestID}");
        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.ChatAnalysisInvalidPayload,
                "Geçersiz istek: ChatServerURL zorunlu",
                request.RequestID),
            statusCode: 400);
    }

    // Check if messages exist
    var hasMessages = request.MessageListObject != null && request.MessageListObject.Count > 0;

    if (!hasMessages)
    {
        // Send error callback immediately for empty messages
        jsonLogger.SystemInfo($"No messages to analyze, sending error callback, RequestID={request.RequestID}");

        _ = Task.Run(async () =>
        {
            var errorResponse = new ChatAnalysisErrorResponse
            {
                ChatID = request.ChatID,
                InstanceID = request.InstanceID,
                UserID = request.UserID,
                RequestID = request.RequestID,
                Error = "Analiz edilecek mesaj yok",
                AnalyzedAt = DateTime.UtcNow
            };

            await callbackService.SendCallbackAsync(
                request.ChatServerURL,
                errorResponse,
                request.RequestID);
        });

        return Results.Ok(ChatAnalysisAcceptedResponse.Processing(request.RequestID));
    }

    jsonLogger.SystemInfo($"Analysis request accepted, RequestID={request.RequestID}, MessageCount={request.MessageListObject!.Count}");

    // Start background processing
    _ = Task.Run(async () =>
    {
        await ProcessAnalysisAsync(
            request,
            claudeAnalyzer,
            callbackService,
            jsonLogger);
    });

    // Return immediately
    return Results.Ok(ChatAnalysisAcceptedResponse.Processing(request.RequestID));
});

logger.SystemInfo($"ChatAnalysis service starting on port {listenPort}");
app.Run();

// Background processing function
static async Task ProcessAnalysisAsync(
    ChatAnalysisRequest request,
    ClaudeAnalyzer claudeAnalyzer,
    CallbackService callbackService,
    JsonLinesLogger logger)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        // Analyze with Claude
        var analysisResult = await claudeAnalyzer.AnalyzeAsync(
            request.MessageListObject!,
            request.LabelSearchText);

        sw.Stop();

        if (!analysisResult.IsSuccess)
        {
            // Claude error - log only, don't send callback (per Q's requirement)
            logger.SystemError($"Claude analysis failed, RequestID={request.RequestID}, Error={analysisResult.ErrorMessage}, Duration={sw.ElapsedMilliseconds}ms");
            return;
        }

        var analysis = analysisResult.Data!;

        // Build callback response
        var callbackResponse = new ChatAnalysisCallbackResponse
        {
            // Echo fields
            ChatID = request.ChatID,
            InstanceID = request.InstanceID,
            UserID = request.UserID,
            RequestID = request.RequestID,

            // Labels
            SelectedLabels = analysis.SelectedLabels,
            SuggestedLabels = analysis.SuggestedLabels,

            // 15 Criteria
            Content = analysis.Content,
            Attitude = analysis.Attitude,
            ApproachRecommendation = analysis.ApproachRecommendation,
            PurchaseProbability = analysis.PurchaseProbability,
            Needs = analysis.Needs,
            DecisionProcess = analysis.DecisionProcess,
            SalesBarriers = analysis.SalesBarriers,
            CommunicationStyle = analysis.CommunicationStyle,
            CustomerProfile = analysis.CustomerProfile,
            SatisfactionAndFeedback = analysis.SatisfactionAndFeedback,
            OfferAndConversionRate = analysis.OfferAndConversionRate,
            SupportStrategy = analysis.SupportStrategy,
            CompetitorAnalysis = analysis.CompetitorAnalysis,
            BehaviorPatterns = analysis.BehaviorPatterns,
            RepresentativeResponseSuggestion = analysis.RepresentativeResponseSuggestion,

            // Metadata
            AnalyzedAt = DateTime.UtcNow
        };

        logger.SystemInfo($"Analysis completed, sending callback, RequestID={request.RequestID}, Duration={sw.ElapsedMilliseconds}ms");

        // Send callback
        var callbackSuccess = await callbackService.SendCallbackAsync(
            request.ChatServerURL,
            callbackResponse,
            request.RequestID);

        if (callbackSuccess)
        {
            logger.SystemInfo($"Callback sent successfully, RequestID={request.RequestID}");
        }
        else
        {
            logger.SystemError($"Callback failed after retries, RequestID={request.RequestID}");
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        logger.SystemError($"Analysis processing failed, RequestID={request.RequestID}, Error={ex.Message}, Duration={sw.ElapsedMilliseconds}ms");
    }
}

// Required for integration tests
public partial class Program { }
