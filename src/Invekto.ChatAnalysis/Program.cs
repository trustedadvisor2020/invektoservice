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
    Console.WriteLine("WARN: Callback:Token is empty - callback auth header will be omitted");
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

    // Set X-Request-Id so TrafficLogging middleware groups this with background steps
    ctx.Request.Headers["X-Request-Id"] = request.RequestID;

    // Check if messages exist
    var hasMessages = request.MessageListObject != null && request.MessageListObject.Count > 0;

    if (!hasMessages)
    {
        // Send error callback immediately for empty messages
        jsonLogger.StepInfo("Mesaj yok, hata callback gönderiliyor", request.RequestID);

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

    jsonLogger.StepInfo($"Analiz isteği alındı ({request.MessageListObject!.Count} mesaj)", request.RequestID);

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

// Endpoint discovery - returns all registered endpoints for this service (no auth, internal only)
app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        // Internal API (called by Backend)
        new() { Method = "POST", Path = "/api/v1/analyze", Description = "Chat analysis (async processing)", Auth = "none", Category = "API" },

        // Health
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe", Auth = "none", Category = "Health" },

        // Discovery
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "none", Category = "Ops" },
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.ChatAnalysisServiceName,
        Port = ServiceConstants.ChatAnalysisPort,
        Endpoints = endpoints
    });
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
    var rid = request.RequestID;
    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        // Validate language parameter - warn if unsupported
        var requestedLang = request.Lang ?? "tr";
        if (!ClaudeAnalyzer.SupportedLanguages.Contains(requestedLang))
        {
            logger.StepWarn($"Desteklenmeyen dil '{requestedLang}', 'tr' kullanılıyor", rid);
        }

        // Analyze with Claude (lang parameter controls output language)
        var analysisResult = await claudeAnalyzer.AnalyzeParallelAsync(
            request.MessageListObject!,
            request.LabelSearchText,
            request.Lang ?? "tr");

        sw.Stop();

        if (!analysisResult.IsSuccess)
        {
            // Claude error - log only, don't send callback (per Q's requirement)
            logger.StepError($"Claude analiz hatası: {analysisResult.ErrorMessage}", rid, sw.ElapsedMilliseconds);
            return;
        }

        logger.StepInfo("Claude analizi tamamlandı", rid, sw.ElapsedMilliseconds);

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

        // Extract callback host for readable log
        var callbackHost = new Uri(request.ChatServerURL).Host;
        var cbSw = System.Diagnostics.Stopwatch.StartNew();

        // Send callback
        var callbackSuccess = await callbackService.SendCallbackAsync(
            request.ChatServerURL,
            callbackResponse,
            request.RequestID);

        cbSw.Stop();

        if (callbackSuccess)
        {
            logger.StepInfo($"Callback gönderildi ({callbackHost})", rid, cbSw.ElapsedMilliseconds);
        }
        else
        {
            logger.StepError($"Callback başarısız ({callbackHost})", rid, cbSw.ElapsedMilliseconds);
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        logger.StepError($"İşlem hatası: {ex.Message}", rid, sw.ElapsedMilliseconds);
    }
}

// Required for integration tests
public partial class Program { }
