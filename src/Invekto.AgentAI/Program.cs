using Invekto.AgentAI.Data;
using Invekto.AgentAI.Middleware;
using Invekto.AgentAI.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.AgentAI;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.AgentAIPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? "";
var claudeModel = builder.Configuration["Claude:Model"] ?? "claude-haiku-4-5-20251001";
var claudeTimeoutSec = builder.Configuration.GetValue<int>("Claude:TimeoutSeconds", 10);
var maxHistoryMessages = builder.Configuration.GetValue<int>("Claude:MaxHistoryMessages", 20);
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";
var maxFeedbackHistory = builder.Configuration.GetValue<int>("AgentProfile:MaxFeedbackHistory", 20);

// Validate required config
if (string.IsNullOrEmpty(claudeApiKey))
{
    Console.Error.WriteLine("FATAL: Claude:ApiKey is not configured");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(pgConnStr))
{
    Console.Error.WriteLine("FATAL: ConnectionStrings:PostgreSQL is not configured");
    Environment.Exit(1);
}
if (string.IsNullOrEmpty(jwtSecretKey))
{
    Console.Error.WriteLine("FATAL: Jwt:SecretKey is not configured");
    Environment.Exit(1);
}

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);
});

// Register logger
var logger = new JsonLinesLogger(ServiceConstants.AgentAIServiceName, logPath);
builder.Services.AddSingleton(logger);

// Register log cleanup
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// Register JWT validator
var jwtSettings = new JwtSettings
{
    SecretKey = jwtSecretKey,
    Issuer = builder.Configuration["Jwt:Issuer"],
    Audience = builder.Configuration["Jwt:Audience"],
    ClockSkewSeconds = builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds", 60)
};
var jwtValidator = new JwtValidator(jwtSettings);
builder.Services.AddSingleton(jwtValidator);

// Register PostgreSQL connection factory
var pgFactory = new PostgresConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register repository
builder.Services.AddSingleton<AgentAIRepository>();

// Register services
builder.Services.AddSingleton<TemplateEngine>();
builder.Services.AddSingleton(sp => new AgentProfileBuilder(
    sp.GetRequiredService<AgentAIRepository>(),
    sp.GetRequiredService<JsonLinesLogger>(),
    maxFeedbackHistory));

// Register ReplyGenerator with HttpClient
var claudeTimeoutMs = claudeTimeoutSec * 1000;
builder.Services.AddHttpClient<ReplyGenerator>()
    .AddTypedClient((httpClient, sp) =>
    {
        return new ReplyGenerator(
            httpClient, claudeApiKey, claudeModel, claudeTimeoutMs,
            sp.GetRequiredService<JsonLinesLogger>());
    });

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.AgentAIServiceName)));
app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.AgentAIServiceName));
});

// ============================================================
// Suggest Reply endpoint (Sync API -- agent waits for response)
// ============================================================

app.MapPost("/api/v1/suggest", async (
    HttpContext ctx,
    ReplyGenerator replyGenerator,
    TemplateEngine templateEngine,
    AgentProfileBuilder profileBuilder,
    AgentAIRepository repository,
    JsonLinesLogger jsonLogger,
    SuggestReplyRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    // Validate request
    if (request == null || string.IsNullOrWhiteSpace(request.MessageText))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIInvalidPayload, "message_text is required", requestId),
            statusCode: 400);
    }

    if (request.ChatId <= 0)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIInvalidPayload, "chat_id is required", requestId),
            statusCode: 400);
    }

    if (request.ConversationHistory == null || request.ConversationHistory.Count == 0)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAINoConversationContext,
                "conversation_history is required (en az 1 mesaj)", requestId),
            statusCode: 400);
    }

    // Extract tenant from JWT
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    jsonLogger.StepInfo($"Suggest request for tenant {tenantContext.TenantId}, chat {request.ChatId}", requestId);

    // Trim conversation history to max allowed
    if (request.ConversationHistory is { Count: > 0 })
    {
        if (request.ConversationHistory.Count > maxHistoryMessages)
            request.ConversationHistory = request.ConversationHistory
                .TakeLast(maxHistoryMessages).ToList();
    }

    // Build agent profile from feedback history
    var agentProfile = await profileBuilder.BuildProfileAsync(
        tenantContext.TenantId, tenantContext.UserId, CancellationToken.None);

    // Try template substitution
    string? templateSuggestion = null;
    if (request.Templates is { Count: > 0 })
    {
        templateSuggestion = templateEngine.FindBestTemplate(
            request.Templates, null, request.TemplateVariables);
    }

    // Generate AI reply
    var result = await replyGenerator.GenerateAsync(
        request, agentProfile, templateSuggestion, CancellationToken.None);

    if (result == null || !result.IsSuccess)
    {
        if (result?.ErrorCode == "timeout")
        {
            jsonLogger.StepError($"Claude timeout for tenant {tenantContext.TenantId}, chat {request.ChatId}, time={result.ProcessingTimeMs}ms", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AgentAIClaudeTimeout,
                    "AI servis zaman asimi. Lutfen tekrar deneyin veya manuel devam edin.", requestId),
                statusCode: 504);
        }

        jsonLogger.StepError($"Reply generation failed for tenant {tenantContext.TenantId}, chat {request.ChatId}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIReplyGenerationFailed,
                "AI cevap onerisi olusturulamadi. Lutfen manuel devam edin.", requestId),
            statusCode: 500);
    }

    // Apply template variables to AI reply if applicable
    if (request.TemplateVariables is { Count: > 0 })
    {
        result.SuggestedReply = templateEngine.Substitute(result.SuggestedReply, request.TemplateVariables);
    }

    // Generate suggestion ID and log to DB
    var suggestionId = Guid.NewGuid();
    bool dbLogFailed = false;
    try
    {
        await repository.LogSuggestionAsync(
            suggestionId, tenantContext.TenantId, tenantContext.UserId,
            request.ChatId, request.Channel, request.Language,
            request.MessageText, request.ConversationHistory?.Count ?? 0,
            result.SuggestedReply, result.Intent, result.Confidence,
            replyGenerator.ModelName, (int)result.ProcessingTimeMs,
            CancellationToken.None);
    }
    catch (Exception ex)
    {
        // DB log failure is non-blocking -- suggestion still returned with warning
        dbLogFailed = true;
        jsonLogger.StepError($"Failed to log suggestion to DB: {ex.Message}", requestId);
    }

    jsonLogger.StepInfo(
        $"Suggest OK: tenant={tenantContext.TenantId}, chat={request.ChatId}, " +
        $"intent={result.Intent}, conf={result.Confidence:F2}, time={result.ProcessingTimeMs}ms",
        requestId);

    return Results.Ok(new SuggestReplyResponse
    {
        SuggestionId = suggestionId.ToString(),
        SuggestedReply = result.SuggestedReply,
        Intent = result.Intent,
        Confidence = result.Confidence,
        ProcessingTimeMs = result.ProcessingTimeMs,
        Model = replyGenerator.ModelName,
        Warning = dbLogFailed ? "Oneri kaydedilemedi, feedback takibi kullanilamayacak" : null
    });
});

// ============================================================
// Feedback endpoint (Async -- fire-and-forget from Main App)
// ============================================================

app.MapPost("/api/v1/feedback", async (
    HttpContext ctx,
    AgentAIRepository repository,
    JsonLinesLogger jsonLogger,
    SuggestionFeedbackRequest? feedback) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (feedback == null || !feedback.IsValid())
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIInvalidFeedback,
                "suggestion_id and agent_action (accepted|edited|rejected) are required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);
    }

    if (!Guid.TryParse(feedback.SuggestionId, out var suggestionGuid))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIInvalidFeedback, "Invalid suggestion_id format", requestId),
            statusCode: 400);
    }

    // Update feedback in DB (fire-and-forget for the caller)
    try
    {
        var updated = await repository.UpdateFeedbackAsync(
            suggestionGuid, tenantContext.TenantId,
            feedback.AgentAction!, feedback.FinalReplyText,
            CancellationToken.None);

        if (!updated)
        {
            jsonLogger.StepWarn($"Feedback for unknown suggestion_id={feedback.SuggestionId}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AgentAIInvalidFeedback,
                    $"Suggestion not found: {feedback.SuggestionId}", requestId),
                statusCode: 404);
        }

        jsonLogger.StepInfo(
            $"Feedback received: suggestion={feedback.SuggestionId}, action={feedback.AgentAction}", requestId);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"Feedback DB update failed: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Internal server error", requestId),
            statusCode: 500);
    }

    return Results.Json(new { status = "accepted", suggestion_id = feedback.SuggestionId }, statusCode: 202);
});

// ============================================================
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "POST", Path = "/api/v1/suggest", Description = "Generate AI reply suggestion (sync)", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/feedback", Description = "Submit agent feedback on suggestion", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe (DB check)", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "none", Category = "Ops" },
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.AgentAIServiceName,
        Port = ServiceConstants.AgentAIPort,
        Endpoints = endpoints
    });
});

logger.SystemInfo($"AgentAI service starting on port {listenPort}");
app.Run();

// Required for integration tests
public partial class Program { }
