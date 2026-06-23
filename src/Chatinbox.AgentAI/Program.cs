using Npgsql;
using Chatinbox.AgentAI.Data;
using Chatinbox.Shared.Middleware;
using Chatinbox.AgentAI.Services;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.DTOs.AgentAI;
using Chatinbox.Shared.Logging;
using Chatinbox.Shared.Services;

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
var knowledgeBaseUrl = builder.Configuration["Knowledge:BaseUrl"] ?? "http://localhost:7104";
var knowledgeTimeoutMs = builder.Configuration.GetValue<int>("Knowledge:TimeoutMs", 5000);
var knowledgeTopK = builder.Configuration.GetValue<int>("Knowledge:TopK", 5);
var summaryThreshold = builder.Configuration.GetValue<int>("Summarizer:SummaryThreshold", 15);
var recentMessageCount = builder.Configuration.GetValue<int>("Summarizer:RecentMessageCount", 5);

// Validate required config
if (string.IsNullOrEmpty(claudeApiKey))
    throw new InvalidOperationException("FATAL: Claude:ApiKey is not configured");
if (string.IsNullOrEmpty(pgConnStr))
    throw new InvalidOperationException("FATAL: ConnectionStrings:PostgreSQL is not configured");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey is not configured");

// Configure Kestrel + HTTPS if certificate is configured
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);

    var certPath = builder.Configuration["Kestrel:Certificate:Path"];
    var certPassword = builder.Configuration["Kestrel:Certificate:Password"];
    var httpsPort = builder.Configuration.GetValue("Kestrel:HttpsPort", 0);

    if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath) && httpsPort > 0)
    {
        options.ListenAnyIP(httpsPort, listenOptions =>
        {
            listenOptions.UseHttps(certPath, certPassword);
        });
    }
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

// GR-2.2: Register KnowledgeHttpClient (direct service-to-service)
builder.Services.AddHttpClient<KnowledgeHttpClient>()
    .AddTypedClient((httpClient, sp) =>
    {
        return new KnowledgeHttpClient(
            httpClient, knowledgeBaseUrl, knowledgeTimeoutMs,
            sp.GetRequiredService<JsonLinesLogger>());
    });

// PKT-6B1: Register OrderCardService (GR-3.3, typed HttpClient → Integrations)
var integrationsBaseUrl = builder.Configuration["Integrations:BaseUrl"] ?? "http://localhost:7106";
var integrationsTimeoutMs = builder.Configuration.GetValue<int>("Integrations:TimeoutMs", 3000);
builder.Services.AddHttpClient<OrderCardService>((sp, client) =>
{
    client.BaseAddress = new Uri(integrationsBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(integrationsTimeoutMs);
});

// PKT-6B1: Register EscalationNoteService (GR-3.3, no HttpClient needed)
builder.Services.AddSingleton<EscalationNoteService>();

// GR-2.2: Register ConversationSummarizer
builder.Services.AddHttpClient<ConversationSummarizer>()
    .AddTypedClient((httpClient, sp) =>
    {
        return new ConversationSummarizer(
            httpClient, claudeApiKey, claudeModel,
            summaryThreshold, recentMessageCount,
            sp.GetRequiredService<JsonLinesLogger>());
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Faz 1: Plan-based feature guard (after JwtAuth sets TenantContext)
var planCache = new TenantPlanCache(pgConnStr, logger);
app.UseFeatureGuard(planCache, logger,
    ("/api/v1/", "AgentAI"));
app.UseAuthorization();

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
    KnowledgeHttpClient knowledgeClient,
    ConversationSummarizer conversationSummarizer,
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

    // GR-2.2: Tone from request (Main App reads tenant_registry.settings_json.default_tone,
    // sends via Backend proxy). AgentAI does not query tenant_registry directly (service isolation).
    var tone = request.Tone;

    // GR-2.2: Fetch Knowledge context (graceful degradation)
    var jwtToken = ctx.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
    var knowledgeResult = await knowledgeClient.SearchAsync(
        tenantContext.TenantId, request.MessageText!, request.Language,
        knowledgeTopK, jwtToken, CancellationToken.None);

    if (!knowledgeResult.Available)
    {
        jsonLogger.StepInfo($"Knowledge unavailable for tenant {tenantContext.TenantId}: {knowledgeResult.UnavailableReason}", requestId);
    }

    // GR-2.2: Summarize conversation if history is long
    string? conversationSummary = null;
    var recentHistory = request.ConversationHistory!;
    try
    {
        var (summary, recent) = await conversationSummarizer.SummarizeIfNeededAsync(
            request.ConversationHistory!, CancellationToken.None);
        conversationSummary = summary;
        recentHistory = recent;
    }
    catch (OperationCanceledException)
    {
        throw; // Propagate app-shutdown / caller cancellation -- never swallow it
    }
    catch (Exception ex)
    {
        // Summarization is a non-critical optimization: any unexpected failure degrades to
        // raw history rather than failing the /suggest endpoint. App-shutdown OCE is rethrown
        // above; this logs and continues. Sanctioned broad-catch boundary -> arch/codex-context.md
        // (optional-step degradation boundary).
        jsonLogger.StepWarn($"Conversation summarization failed, using raw history: {ex.Message}", requestId);
    }

    // Update request with trimmed history for ReplyGenerator
    request.ConversationHistory = recentHistory;

    // Generate AI reply with Knowledge context, tone, and summary
    var result = await replyGenerator.GenerateAsync(
        request, agentProfile, templateSuggestion,
        knowledgeResult.ContextText, tone, conversationSummary,
        CancellationToken.None);

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

    // GR-2.2: Serialize Knowledge sources for DB logging
    string? knowledgeSourcesJson = null;
    if (knowledgeResult.Available && knowledgeResult.Sources.Count > 0)
    {
        knowledgeSourcesJson = System.Text.Json.JsonSerializer.Serialize(knowledgeResult.Sources);
    }

    // Generate suggestion ID and log to DB
    var suggestionId = Guid.NewGuid();
    bool dbLogFailed = false;
    try
    {
        await repository.LogSuggestionAsync(
            suggestionId, tenantContext.TenantId, tenantContext.UserId,
            request.ChatId, request.Channel, request.Language,
            request.MessageText!, request.ConversationHistory?.Count ?? 0,
            result.SuggestedReply, result.Intent, result.Confidence,
            replyGenerator.ModelName, (int)result.ProcessingTimeMs,
            tone, knowledgeResult.Available, knowledgeSourcesJson,
            knowledgeResult.Available ? request.MessageText : null,
            result.SuggestedFollowup, conversationSummary,
            result.DetectedLanguage,
            CancellationToken.None);
    }
    catch (NpgsqlException ex)
    {
        // DB log failure is non-blocking -- suggestion still returned with warning.
        // CancellationToken.None is passed above, so no OperationCanceledException can arise here.
        dbLogFailed = true;
        jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] Suggestion DB log error: {ex.Message}", requestId);
    }

    jsonLogger.StepInfo(
        $"Suggest OK: tenant={tenantContext.TenantId}, chat={request.ChatId}, " +
        $"intent={result.Intent}, conf={result.Confidence:F2}, time={result.ProcessingTimeMs}ms, " +
        $"knowledge={knowledgeResult.Available}, lang={result.DetectedLanguage}",
        requestId);

    // GR-2.6: Add KVKK warning for health tenants
    string? warning = dbLogFailed ? "Oneri kaydedilemedi, feedback takibi kullanilamayacak" : null;
    var (healthSettingsJson, healthSector) = await repository.GetTenantHealthInfoAsync(tenantContext.TenantId);
    if (KvkkHelper.IsHealthTenant(healthSettingsJson, healthSector))
    {
        warning = warning != null
            ? $"{warning} | {KvkkHelper.AgentAIWarning}"
            : KvkkHelper.AgentAIWarning;
    }

    return Results.Ok(new SuggestReplyResponse
    {
        SuggestionId = suggestionId.ToString(),
        SuggestedReply = result.SuggestedReply,
        Intent = result.Intent,
        Confidence = result.Confidence,
        ProcessingTimeMs = result.ProcessingTimeMs,
        Model = replyGenerator.ModelName,
        Warning = warning,
        Sources = knowledgeResult.Available && knowledgeResult.Sources.Count > 0
            ? knowledgeResult.Sources : null,
        SuggestedFollowup = result.SuggestedFollowup,
        KnowledgeAvailable = knowledgeResult.Available,
        ConversationSummary = conversationSummary,
        DetectedLanguage = result.DetectedLanguage
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
    catch (NpgsqlException ex)
    {
        // DB error on feedback update -> coded 500 (was generic GeneralUnknown).
        // CancellationToken.None is passed above, so no OperationCanceledException can arise here.
        jsonLogger.StepError($"[{ErrorCodes.DatabaseConnectionFailed}] Feedback DB update error: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed,
                "Failed to save feedback due to a database error. Please retry.", requestId),
            statusCode: 500);
    }

    return Results.Json(new { status = "accepted", suggestion_id = feedback.SuggestionId }, statusCode: 202);
});

// ============================================================
// E-commerce Agent Assist endpoints (PKT-6B1: GR-3.3)
// ============================================================

app.MapGet("/api/v1/ecom/order-card/{phone}", async (
    HttpContext ctx,
    OrderCardService orderCardService,
    string phone) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var jwtToken = ctx.Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");

    var card = await orderCardService.GetOrderCardAsync(
        tenantContext.TenantId, phone, jwtToken, ctx.RequestAborted);

    if (card == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AgentAIOrderCardFetchFailed, "Order card unavailable", requestId), statusCode: 502);

    return Results.Ok(card);
});

app.MapPost("/api/v1/ecom/escalation-note", async (
    HttpContext ctx,
    EscalationNoteService escalationService) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var intent = root.TryGetProperty("intent", out var i) ? i.GetString() : null;
        var sentiment = root.TryGetProperty("sentiment", out var s) ? s.GetString() : null;

        List<ConversationEntry>? messages = null;
        if (root.TryGetProperty("messages", out var msgArr) && msgArr.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            messages = new List<ConversationEntry>();
            foreach (var msg in msgArr.EnumerateArray())
            {
                messages.Add(new ConversationEntry
                {
                    Role = msg.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "",
                    Text = msg.TryGetProperty("text", out var t) ? t.GetString() ?? "" : ""
                });
            }
        }

        var note = escalationService.GenerateNote(tenantContext.TenantId, intent, sentiment, messages);
        if (note == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AgentAIEscalationNoteFailed, "Escalation note generation failed", requestId), statusCode: 500);

        return Results.Ok(note);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AgentAIEscalationNoteFailed, "Invalid JSON body", requestId), statusCode: 400);
    }
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
        new() { Method = "GET", Path = "/api/v1/ecom/order-card/{phone}", Description = "Order card for customer (PKT-6B1)", Auth = "Bearer JWT", Category = "Ecom" },
        new() { Method = "POST", Path = "/api/v1/ecom/escalation-note", Description = "Generate escalation note (PKT-6B1)", Auth = "Bearer JWT", Category = "Ecom" },
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
namespace Chatinbox.AgentAI { public partial class Program { } }
