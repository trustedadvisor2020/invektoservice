using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;
using Invekto.Shared.Services;
using Invekto.WebChat.Data;
using Invekto.WebChat.Hubs;
using Invekto.WebChat.Services;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.WebChatPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var claudeApiKey = builder.Configuration["Claude:ApiKey"] ?? "";
var claudeModel = builder.Configuration["Claude:Model"] ?? "claude-haiku-4-5-20251001";
var claudeTimeoutSec = builder.Configuration.GetValue<int>("Claude:TimeoutSeconds", 15);
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";
var operatorEmail = builder.Configuration["Operator:Email"] ?? "";
var operatorPasswordHash = builder.Configuration["Operator:PasswordHash"] ?? "";
var aiDelaySeconds = builder.Configuration.GetValue<int>("AIReply:DelaySeconds", 30);

// Validate required config
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
var logger = new JsonLinesLogger(ServiceConstants.WebChatServiceName, logPath);
builder.Services.AddSingleton(logger);

// Register log cleanup
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// Register PostgreSQL connection factory
var pgFactory = new PostgresConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register repository
builder.Services.AddSingleton<WebChatRepository>();

// Register OperatorPresence
builder.Services.AddSingleton<OperatorPresence>();

// Register AIReplyService with HttpClient
var claudeTimeoutMs = claudeTimeoutSec * 1000;
builder.Services.AddHttpClient<AIReplyService>()
    .AddTypedClient((httpClient, sp) =>
    {
        return new AIReplyService(
            httpClient, claudeApiKey, claudeModel, claudeTimeoutMs,
            sp.GetRequiredService<JsonLinesLogger>());
    });

// Register SignalR
builder.Services.AddSignalR();

// Register ConversationService (needs IHubContext, registered after SignalR)
builder.Services.AddSingleton<ConversationService>(sp =>
    new ConversationService(
        sp.GetRequiredService<WebChatRepository>(),
        sp.GetRequiredService<AIReplyService>(),
        sp.GetRequiredService<OperatorPresence>(),
        sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ChatHub>>(),
        sp.GetRequiredService<JsonLinesLogger>(),
        aiDelaySeconds));

// Add CORS for website widget
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Allow all origins for widget
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// Enable CORS
app.UseCors();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Map SignalR hub
app.MapHub<ChatHub>("/ws/chat");

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// Helper: extract or generate request ID
string GetRequestId(HttpContext ctx)
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault();
    if (string.IsNullOrEmpty(rid))
    {
        rid = Guid.NewGuid().ToString("N");
        ctx.Request.Headers["X-Request-Id"] = rid;
    }
    return rid;
}

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.WebChatServiceName)));
app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.WebChatServiceName));
});

// ============================================================
// Auth endpoint (operator login)
// ============================================================

app.MapPost("/api/v1/auth/login", (HttpContext ctx) =>
{
    var requestId = GetRequestId(ctx);
    try
    {
        using var bodyDoc = JsonDocument.Parse(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        var password = root.TryGetProperty("password", out var p) ? p.GetString() : null;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "email and password required", requestId),
                statusCode: 400);
        }

        // Verify credentials
        var inputHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        if (email != operatorEmail || inputHash != operatorPasswordHash)
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.WebChatAuthLoginFailed, "Invalid credentials", requestId),
                statusCode: 401);
        }

        // Generate JWT
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("email", email),
            new Claim("role", "operator"),
            new Claim("source", "webchat_login")
        };
        var token = new JwtSecurityToken(
            issuer: "InvektoServis",
            audience: "InvektoServis",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        logger.StepInfo($"Operator login: {email}", requestId);
        return Results.Ok(new { token = tokenString, email, expiresIn = 86400 });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "Invalid JSON", requestId),
            statusCode: 400);
    }
});

// ============================================================
// Visitor endpoints (no auth required)
// ============================================================

// Start new conversation
app.MapPost("/api/v1/conversations", async (HttpContext ctx, ConversationService convService) =>
{
    var requestId = GetRequestId(ctx);
    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var visitorId = root.TryGetProperty("visitor_id", out var vid) ? vid.GetString() : null;
        if (string.IsNullOrEmpty(visitorId))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.WebChatInvalidVisitor, "visitor_id is required", requestId),
                statusCode: 400);
        }

        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        var email = root.TryGetProperty("email", out var em) ? em.GetString() : null;
        var pageUrl = root.TryGetProperty("page_url", out var pu) ? pu.GetString() : null;
        var userAgent = ctx.Request.Headers.UserAgent.FirstOrDefault();

        var conversationId = await convService.StartConversationAsync(
            visitorId, name, email, pageUrl, userAgent, ctx.RequestAborted);

        return Results.Ok(new { conversation_id = conversationId });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "Invalid JSON", requestId),
            statusCode: 400);
    }
});

// Get conversation messages
app.MapGet("/api/v1/conversations/{id}/messages", async (long id, HttpContext ctx, WebChatRepository repo) =>
{
    var requestId = GetRequestId(ctx);
    var visitorId = ctx.Request.Query["visitor_id"].FirstOrDefault();
    long? afterId = long.TryParse(ctx.Request.Query["after_id"].FirstOrDefault(), out var aid) ? aid : null;

    var conversation = await repo.GetConversationAsync(id, ctx.RequestAborted);
    if (conversation == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatConversationNotFound, "Conversation not found", requestId),
            statusCode: 404);
    }

    // Visitors can only read their own conversations
    if (!string.IsNullOrEmpty(visitorId) && conversation.VisitorId != visitorId)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidVisitor, "Visitor mismatch", requestId),
            statusCode: 403);
    }

    var messages = await repo.GetMessagesAsync(id, 100, afterId, ctx.RequestAborted);
    return Results.Ok(new { messages });
});

// Send visitor message
app.MapPost("/api/v1/conversations/{id}/messages", async (long id, HttpContext ctx, ConversationService convService) =>
{
    var requestId = GetRequestId(ctx);
    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var visitorId = root.TryGetProperty("visitor_id", out var vid) ? vid.GetString() : null;
        var content = root.TryGetProperty("content", out var c) ? c.GetString() : null;

        if (string.IsNullOrEmpty(visitorId) || string.IsNullOrEmpty(content))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "visitor_id and content required", requestId),
                statusCode: 400);
        }

        var message = await convService.SendVisitorMessageAsync(id, visitorId, content, ctx.RequestAborted);
        return Results.Ok(new { message });
    }
    catch (InvalidOperationException ex)
    {
        var code = ex.Message.Contains("closed") ? ErrorCodes.WebChatConversationClosed : ErrorCodes.WebChatConversationNotFound;
        return Results.Json(ErrorResponse.Create(code, ex.Message, requestId), statusCode: 400);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidVisitor, "Visitor mismatch", requestId),
            statusCode: 403);
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "Invalid JSON", requestId),
            statusCode: 400);
    }
});

// Get operator online status (public)
app.MapGet("/api/v1/status", (OperatorPresence presence) =>
{
    return Results.Ok(new { operator_online = presence.IsOnline });
});

// ============================================================
// Operator endpoints (JWT auth required)
// ============================================================

// Simple JWT validation for operator endpoints
IResult? ValidateOperatorJwt(HttpContext ctx, string requestId)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Authorization header required", requestId),
            statusCode: 401);
    }

    try
    {
        var token = authHeader["Bearer ".Length..];
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey));

        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "InvektoServis",
            ValidateAudience = true,
            ValidAudience = "InvektoServis",
            ValidateLifetime = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(60)
        }, out _);

        return null; // Valid
    }
    catch
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthTokenInvalid, "Invalid token", requestId),
            statusCode: 401);
    }
}

// List active conversations (operator)
app.MapGet("/api/v1/operator/conversations", async (HttpContext ctx, WebChatRepository repo) =>
{
    var requestId = GetRequestId(ctx);
    var authError = ValidateOperatorJwt(ctx, requestId);
    if (authError != null) return authError;

    var conversations = await repo.GetActiveConversationsAsync(ctx.RequestAborted);

    // Get last message for each conversation
    var result = new List<object>();
    foreach (var conv in conversations)
    {
        var lastMsg = await repo.GetLastMessageAsync(conv.Id, ctx.RequestAborted);
        result.Add(new
        {
            id = conv.Id,
            visitor_id = conv.VisitorId,
            visitor_name = conv.VisitorName,
            visitor_email = conv.VisitorEmail,
            status = conv.Status,
            started_at = conv.StartedAt,
            last_message_at = conv.LastMessageAt,
            last_message = lastMsg != null ? new
            {
                sender_type = lastMsg.SenderType,
                content = lastMsg.Content,
                created_at = lastMsg.CreatedAt
            } : null
        });
    }

    return Results.Ok(new { conversations = result });
});

// Send operator message
app.MapPost("/api/v1/operator/conversations/{id}/messages", async (long id, HttpContext ctx, ConversationService convService) =>
{
    var requestId = GetRequestId(ctx);
    var authError = ValidateOperatorJwt(ctx, requestId);
    if (authError != null) return authError;

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        var content = root.TryGetProperty("content", out var c) ? c.GetString() : null;

        if (string.IsNullOrEmpty(content))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "content is required", requestId),
                statusCode: 400);
        }

        var message = await convService.SendOperatorMessageAsync(id, content, ctx.RequestAborted);
        return Results.Ok(new { message });
    }
    catch (InvalidOperationException ex)
    {
        var code = ex.Message.Contains("closed") ? ErrorCodes.WebChatConversationClosed : ErrorCodes.WebChatConversationNotFound;
        return Results.Json(ErrorResponse.Create(code, ex.Message, requestId), statusCode: 400);
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "Invalid JSON", requestId),
            statusCode: 400);
    }
});

// Close conversation (operator)
app.MapPut("/api/v1/operator/conversations/{id}/close", async (long id, HttpContext ctx, ConversationService convService) =>
{
    var requestId = GetRequestId(ctx);
    var authError = ValidateOperatorJwt(ctx, requestId);
    if (authError != null) return authError;

    var closed = await convService.CloseConversationAsync(id, ctx.RequestAborted);
    if (!closed)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatConversationNotFound, "Conversation not found or already closed", requestId),
            statusCode: 404);
    }

    return Results.Ok(new { status = "closed" });
});

// Register push token (operator)
app.MapPost("/api/v1/operator/push-token", async (HttpContext ctx, WebChatRepository repo) =>
{
    var requestId = GetRequestId(ctx);
    var authError = ValidateOperatorJwt(ctx, requestId);
    if (authError != null) return authError;

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        var token = root.TryGetProperty("token", out var t) ? t.GetString() : null;

        if (string.IsNullOrEmpty(token))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "token is required", requestId),
                statusCode: 400);
        }

        await repo.UpsertPushTokenAsync(token, ctx.RequestAborted);
        return Results.Ok(new { status = "registered" });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.WebChatInvalidPayload, "Invalid JSON", requestId),
            statusCode: 400);
    }
});

// ============================================================
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "POST", Path = "/api/v1/auth/login", Description = "Operator login", Auth = "none", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/conversations", Description = "Start new conversation (visitor)", Auth = "none", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/conversations/{id}/messages", Description = "Get conversation messages", Auth = "none", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/conversations/{id}/messages", Description = "Send visitor message", Auth = "none", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/status", Description = "Get operator online status", Auth = "none", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/operator/conversations", Description = "List active conversations (operator)", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/operator/conversations/{id}/messages", Description = "Send operator message", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "PUT", Path = "/api/v1/operator/conversations/{id}/close", Description = "Close conversation", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/operator/push-token", Description = "Register push notification token", Auth = "Bearer JWT", Category = "API" },
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe (DB check)", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "none", Category = "Ops" },
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.WebChatServiceName,
        Port = ServiceConstants.WebChatPort,
        Endpoints = endpoints
    });
});

logger.SystemInfo($"WebChat service starting on port {listenPort}");
app.Run();

// Required for integration tests
namespace Invekto.WebChat { public partial class Program { } }
