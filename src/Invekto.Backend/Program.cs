using System.Net.Http.Headers;
using System.Text;
using Invekto.Backend.Middleware;
using Invekto.Backend.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.ChatAnalysis;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Integration;
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
var automationUrl = builder.Configuration["Microservice:Automation:Url"]
    ?? $"http://localhost:{ServiceConstants.AutomationPort}";
var automationLogPath = builder.Configuration["Microservice:Automation:LogPath"];
var agentAIUrl = builder.Configuration["Microservice:AgentAI:Url"]
    ?? $"http://localhost:{ServiceConstants.AgentAIPort}";
var agentAILogPath = builder.Configuration["Microservice:AgentAI:LogPath"];
var agentAISuggestTimeoutMs = builder.Configuration.GetValue<int>("Microservice:AgentAI:SuggestTimeoutMs", 15000);
var outboundUrl = builder.Configuration["Microservice:Outbound:Url"]
    ?? $"http://localhost:{ServiceConstants.OutboundPort}";
var outboundLogPath = builder.Configuration["Microservice:Outbound:LogPath"];
var outboundTimeoutMs = builder.Configuration.GetValue<int>("Microservice:Outbound:TimeoutMs", 10000);

// Register JSON Lines logger
builder.Services.AddSingleton(new JsonLinesLogger(ServiceConstants.BackendServiceName, logPath));

// Register log reader for /ops (aggregate backend + microservice logs)
var logPaths = new List<string> { logPath };
if (!string.IsNullOrEmpty(microserviceLogPath))
{
    logPaths.Add(microserviceLogPath);
}
if (!string.IsNullOrEmpty(automationLogPath))
{
    logPaths.Add(automationLogPath);
}
if (!string.IsNullOrEmpty(agentAILogPath))
{
    logPaths.Add(agentAILogPath);
}
if (!string.IsNullOrEmpty(outboundLogPath))
{
    logPaths.Add(outboundLogPath);
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

// Configure Automation HTTP client
builder.Services.AddHttpClient<AutomationClient>(client =>
{
    client.BaseAddress = new Uri(automationUrl);
    client.Timeout = TimeSpan.FromMilliseconds(ServiceConstants.BackendToMicroserviceTimeoutMs);
});

// Configure AgentAI HTTP client (longer timeout for Claude API latency)
builder.Services.AddHttpClient<AgentAIClient>(client =>
{
    client.BaseAddress = new Uri(agentAIUrl);
    client.Timeout = TimeSpan.FromMilliseconds(agentAISuggestTimeoutMs);
});

// Configure Outbound HTTP client (GR-1.3)
builder.Services.AddHttpClient<OutboundClient>(client =>
{
    client.BaseAddress = new Uri(outboundUrl);
    client.Timeout = TimeSpan.FromMilliseconds(outboundTimeoutMs);
});

// ============================================
// GR-1.9: INTEGRATION BRIDGE SETUP
// ============================================

// JWT Validator (singleton, thread-safe)
JwtValidator? jwtValidator = null;
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
if (!string.IsNullOrEmpty(jwtSecretKey))
{
    var jwtSettings = new JwtSettings
    {
        SecretKey = jwtSecretKey,
        Issuer = builder.Configuration["Jwt:Issuer"],
        Audience = builder.Configuration["Jwt:Audience"],
        ClockSkewSeconds = builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds", 60)
    };
    jwtValidator = new JwtValidator(jwtSettings);
    builder.Services.AddSingleton(jwtValidator);
}

// PostgreSQL connection factory (singleton, thread-safe pooling)
PostgresConnectionFactory? pgFactory = null;
var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSQL");
if (!string.IsNullOrEmpty(pgConnectionString))
{
    pgFactory = new PostgresConnectionFactory(pgConnectionString);
    builder.Services.AddSingleton(pgFactory);
}

// Callback client for async results to Main App
var callbackUrl = builder.Configuration["Integration:Callback:DefaultCallbackUrl"];
if (!string.IsNullOrEmpty(callbackUrl))
{
    var callbackSettings = new CallbackSettings
    {
        DefaultCallbackUrl = callbackUrl,
        MaxRetries = builder.Configuration.GetValue<int>("Integration:Callback:MaxRetries", ServiceConstants.CallbackMaxRetries),
        BaseDelayMs = builder.Configuration.GetValue<int>("Integration:Callback:BaseDelayMs", ServiceConstants.CallbackBaseDelayMs),
        TimeoutMs = builder.Configuration.GetValue<int>("Integration:Callback:TimeoutMs", ServiceConstants.CallbackTimeoutMs)
    };
    builder.Services.AddSingleton(callbackSettings);
    builder.Services.AddHttpClient<MainAppCallbackClient>();
}

var app = builder.Build();

// Enable traffic logging middleware (logs all HTTP request/response)
app.UseTrafficLogging();

// GR-1.9: JWT auth middleware for protected API paths
if (jwtValidator != null)
{
    var jwtLogger = app.Services.GetRequiredService<JsonLinesLogger>();
    app.UseJwtAuth(jwtValidator, jwtLogger, "/api/v1/webhook/", "/api/v1/automation/", "/api/v1/outbound/");
}

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
app.MapGet("/ops", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var chatHealthy = await chatClient.CheckHealthAsync();
    var autoHealthy = await automationClient.CheckHealthAsync();
    var agentAIHealthy = await agentAIClient.CheckHealthAsync();
    var outboundHealthy = await outboundClient.CheckHealthAsync();

    var ops = new
    {
        status = "ok",
        timestamp = DateTime.UtcNow,
        services = new
        {
            backend = new { status = "ok" },
            chatAnalysis = new { status = chatHealthy ? "ok" : "unavailable" },
            automation = new { status = autoHealthy ? "ok" : "unavailable" },
            agentAI = new { status = agentAIHealthy ? "ok" : "unavailable" },
            outbound = new { status = outboundHealthy ? "ok" : "unavailable" }
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

// OPS: Debug - show configured paths and file counts
app.MapGet("/ops/debug", (HttpContext ctx) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var backendLogExists = Directory.Exists(logPath);
    var microserviceLogExists = !string.IsNullOrEmpty(microserviceLogPath) && Directory.Exists(microserviceLogPath);

    var backendFiles = backendLogExists ? Directory.GetFiles(logPath, "*.jsonl") : Array.Empty<string>();
    var microserviceFiles = microserviceLogExists ? Directory.GetFiles(microserviceLogPath!, "*.jsonl") : Array.Empty<string>();

    return Results.Ok(new
    {
        config = new
        {
            logPath,
            microserviceLogPath,
            workingDirectory = Directory.GetCurrentDirectory()
        },
        backend = new
        {
            exists = backendLogExists,
            files = backendFiles.Select(f => new { path = f, size = new FileInfo(f).Length })
        },
        microservice = new
        {
            exists = microserviceLogExists,
            files = microserviceFiles.Select(f => new { path = f, size = new FileInfo(f).Length })
        }
    });
});

// OPS: Debug2 - test LogReader directly
app.MapGet("/ops/debug2", async (HttpContext ctx, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    // Read first file manually with FileShare.ReadWrite
    var testFile = Path.Combine(logPath, $"{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
    var fileExists = File.Exists(testFile);
    string[] lines = Array.Empty<string>();
    if (fileExists)
    {
        var lineList = new List<string>();
        using var stream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
            lineList.Add(line);
        lines = lineList.ToArray();
    }
    var firstLine = lines.Length > 0 ? lines[0] : null;

    // Try to parse first line
    object? parsedEntry = null;
    string? parseError = null;
    if (firstLine != null)
    {
        try
        {
            parsedEntry = System.Text.Json.JsonSerializer.Deserialize<object>(firstLine);
        }
        catch (Exception ex)
        {
            parseError = ex.Message;
        }
    }

    // Try LogReader query
    var queryResult = await logReader.QueryLogsAsync(new Invekto.Shared.Logging.Reader.LogQueryOptions
    {
        Levels = new[] { "INFO", "WARN", "ERROR" },
        Limit = 5
    });

    return Results.Ok(new
    {
        testFile,
        fileExists,
        lineCount = lines.Length,
        firstLine,
        parsedEntry,
        parseError,
        logReaderResult = new
        {
            count = queryResult.Entries.Count,
            hasMore = queryResult.HasMore,
            entries = queryResult.Entries
        }
    });
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
app.MapGet("/api/ops/health", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient) =>
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
        uptimeSeconds = (long?)null,
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
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = chatHealthy ? null : "Service unreachable"
    });

    // Automation - check health with timing
    var swAuto = System.Diagnostics.Stopwatch.StartNew();
    var autoHealthy = await automationClient.CheckHealthAsync();
    swAuto.Stop();

    services.Add(new
    {
        name = ServiceConstants.AutomationServiceName,
        status = autoHealthy ? "ok" : "unavailable",
        responseTimeMs = autoHealthy ? (int?)swAuto.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = autoHealthy ? null : "Service unreachable"
    });

    // AgentAI - check health with timing
    var swAgent = System.Diagnostics.Stopwatch.StartNew();
    var agentAIHealthy = await agentAIClient.CheckHealthAsync();
    swAgent.Stop();

    services.Add(new
    {
        name = ServiceConstants.AgentAIServiceName,
        status = agentAIHealthy ? "ok" : "unavailable",
        responseTimeMs = agentAIHealthy ? (int?)swAgent.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = agentAIHealthy ? null : "Service unreachable"
    });

    // Outbound - check health with timing (GR-1.3)
    var swOutbound = System.Diagnostics.Stopwatch.StartNew();
    var outboundHealthy = await outboundClient.CheckHealthAsync();
    swOutbound.Stop();

    services.Add(new
    {
        name = ServiceConstants.OutboundServiceName,
        status = outboundHealthy ? "ok" : "unavailable",
        responseTimeMs = outboundHealthy ? (int?)swOutbound.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = outboundHealthy ? null : "Service unreachable"
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

// Dashboard: Grouped log stream (operations view)
app.MapGet("/api/ops/logs/grouped", async (
    HttpContext ctx,
    LogReader logReader,
    string? level,
    string? service,
    string? search,
    string? after,
    int? limit,
    string? category) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    // Category filter: default = no filter (backward compatible), explicit values filter
    string[]? categories = null;
    if (!string.IsNullOrEmpty(category) && !category.Equals("all", StringComparison.OrdinalIgnoreCase))
        categories = category.Split(',');

    var options = new LogQueryOptions
    {
        Levels = string.IsNullOrEmpty(level) ? null : level.Split(','),
        Service = service,
        Search = search,
        After = string.IsNullOrEmpty(after) ? null : DateTime.Parse(after),
        Limit = limit ?? 50,
        Categories = categories
    };

    var result = await logReader.QueryLogsGroupedAsync(options);
    return Results.Ok(new
    {
        groups = result.Groups,
        hasMore = result.HasMore
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
        "Invekto.Backend" => "Invekto.Backend",
        "Invekto.ChatAnalysis" => "Invekto.Microservice.Chat",
        "Invekto.Automation" => "InvektoAutomation",
        "Invekto.AgentAI" => "InvektoAgentAI",
        "Invekto.Outbound" => "InvektoOutbound",
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
app.MapGet("/api/ops/test/{serviceName}/{*path}", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, string serviceName, string? path) =>
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

        if (serviceName == "automation")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await automationClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "agentai")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await agentAIClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "outbound")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await outboundClient.TestEndpointAsync(endpoint);
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

// ============================================
// GR-1.9: INTEGRATION WEBHOOK ENDPOINTS
// ============================================

// Webhook event receiver (Main App -> InvektoServis)
// JWT auth enforced by middleware for /api/v1/webhook/ prefix
app.MapPost("/api/v1/webhook/event", (HttpContext ctx, JsonLinesLogger jsonLogger, IncomingWebhookEvent? webhookEvent) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var requestId = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    // Extract TenantContext (set by JWT middleware)
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        sw.Stop();
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", requestId),
            statusCode: 401);
    }

    // Validate payload
    if (webhookEvent == null)
    {
        sw.Stop();
        var reqCtx = RequestContext.Create(tenantContext.TenantId.ToString(), "-");
        jsonLogger.RequestError("Webhook: null payload", reqCtx, "/api/v1/webhook/event", sw.ElapsedMilliseconds, ErrorCodes.IntegrationWebhookInvalidPayload);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.IntegrationWebhookInvalidPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    if (!WebhookEventTypes.IsValid(webhookEvent.EventType))
    {
        sw.Stop();
        var reqCtx = RequestContext.Create(tenantContext.TenantId.ToString(), webhookEvent.ChatId.ToString());
        jsonLogger.RequestError(
            $"Webhook: unknown event_type={webhookEvent.EventType}", reqCtx,
            "/api/v1/webhook/event", sw.ElapsedMilliseconds, ErrorCodes.IntegrationUnknownEventType);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.IntegrationUnknownEventType,
                $"Unknown event_type: {webhookEvent.EventType}. Valid: new_message, conversation_closed, tag_changed, conversation_started, agent_assigned",
                requestId),
            statusCode: 400);
    }

    sw.Stop();
    var context = RequestContext.CreateWithPassThrough(
        requestId,
        tenantContext.TenantId.ToString(),
        webhookEvent.ChatId.ToString());

    // Log the accepted event
    jsonLogger.RequestInfo(
        $"Webhook accepted: event={webhookEvent.EventType}, chat_id={webhookEvent.ChatId}, seq={webhookEvent.SequenceId}",
        context, "/api/v1/webhook/event", sw.ElapsedMilliseconds);

    // Latency monitoring
    if (sw.ElapsedMilliseconds > ServiceConstants.IntegrationLatencyThresholdMs)
    {
        jsonLogger.SystemWarn(
            $"Webhook acceptance exceeded {ServiceConstants.IntegrationLatencyThresholdMs}ms threshold: {sw.ElapsedMilliseconds}ms, event={webhookEvent.EventType}");
    }

    // Add processing time header
    ctx.Response.Headers[HeaderNames.ProcessingTimeMs] = sw.ElapsedMilliseconds.ToString();

    // Return 202 Accepted -- async processing will happen in future GR-1.1/1.2/1.3 services
    return Results.Json(new
    {
        status = "accepted",
        request_id = context.RequestId,
        event_type = webhookEvent.EventType,
        sequence_id = webhookEvent.SequenceId,
        message = "Event accepted for processing"
    }, statusCode: 202);
});

// Tenant verify endpoint (quick integration health check)
app.MapGet("/api/v1/tenant/verify", (HttpContext ctx, JsonLinesLogger jsonLogger) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;

    // This endpoint is under /api/v1/webhook/ prefix? No, it's under /api/v1/
    // We need to manually check JWT here since it's not under the protected prefix
    if (jwtValidator == null)
    {
        return Results.Ok(new
        {
            status = "warning",
            message = "JWT validation not configured. Set Jwt:SecretKey in appsettings.",
            jwt_configured = false,
            postgres_configured = pgFactory != null
        });
    }

    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"),
            statusCode: 401);
    }

    var token = authHeader["Bearer ".Length..].Trim();
    var (tc, error) = jwtValidator.ValidateToken(token);
    if (tc == null)
    {
        // Use appropriate error code based on error message (matches middleware behavior)
        var errorCode = error != null && error.Contains("expired")
            ? ErrorCodes.AuthTokenExpired
            : ErrorCodes.AuthTokenInvalid;
        return Results.Json(
            ErrorResponse.Create(errorCode, error ?? "Token validation failed", "-"),
            statusCode: 401);
    }

    return Results.Ok(new
    {
        status = "ok",
        tenant_id = tc.TenantId,
        user_id = tc.UserId,
        role = tc.Role,
        jwt_configured = true,
        postgres_configured = pgFactory != null,
        message = "Integration bridge ready"
    });
});

// ============================================
// EXISTING API ENDPOINTS
// ============================================

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

// Endpoint discovery - returns all services' endpoints (aggregated)
app.MapGet("/api/ops/endpoints", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    var backendEndpoints = new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.BackendServiceName,
        Port = ServiceConstants.BackendPort,
        Endpoints = new List<EndpointInfo>
        {
            // Public API
            new() { Method = "POST", Path = "/api/v1/chat/analyze", Description = "Chat analysis (async, callback)", Auth = "none", Category = "API" },
            // GR-1.9: Integration endpoints
            new() { Method = "POST", Path = "/api/v1/webhook/event", Description = "Webhook receiver (Main App -> InvektoServis)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/tenant/verify", Description = "Tenant integration health check", Auth = "Bearer", Category = "API" },
            // Agent Assist proxy endpoints
            new() { Method = "POST", Path = "/api/v1/agent-assist/suggest", Description = "AI reply suggestion proxy (Backend -> AgentAI)", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/agent-assist/feedback", Description = "Agent feedback proxy (Backend -> AgentAI)", Auth = "Bearer", Category = "API" },
            // Automation proxy endpoint
            new() { Method = "POST", Path = "/api/v1/automation/webhook", Description = "Webhook event proxy (Backend -> Automation)", Auth = "Bearer", Category = "API" },
            // Outbound proxy endpoints (GR-1.3)
            new() { Method = "POST", Path = "/api/v1/outbound/broadcast/send", Description = "Broadcast send proxy (Backend -> Outbound)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/outbound/broadcast/{broadcastId}/status", Description = "Broadcast status proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/webhook/trigger", Description = "Trigger event proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/webhook/delivery-status", Description = "Delivery status proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/webhook/message", Description = "Incoming message proxy (opt-out)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/outbound/templates", Description = "List templates proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/templates", Description = "Create template proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/outbound/templates/{id}", Description = "Update template proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "DELETE", Path = "/api/v1/outbound/templates/{id}", Description = "Deactivate template proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/optout", Description = "Add opt-out proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "DELETE", Path = "/api/v1/outbound/optout/{phone}", Description = "Remove opt-out proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/outbound/optout/check/{phone}", Description = "Check opt-out proxy", Auth = "Bearer", Category = "API" },

            // Health
            new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },

            // Ops Dashboard API
            new() { Method = "GET", Path = "/api/ops/health", Description = "All services health", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/stream", Description = "Log stream with filters", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/grouped", Description = "Grouped log stream (operations view)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/context", Description = "Log context (\u00b110 lines)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/stats/errors", Description = "Error statistics (24h)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/postman", Description = "Postman collection download", Auth = "Basic", Category = "Ops" },
            new() { Method = "POST", Path = "/api/ops/services/{name}/restart", Description = "Restart Windows Service", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/test/{service}/{path}", Description = "Test proxy for microservices", Auth = "Basic", Category = "Ops" },

            // Legacy Ops (plain JSON)
            new() { Method = "GET", Path = "/ops", Description = "Operations dashboard (legacy)", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/debug", Description = "Debug: log paths and file counts", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/debug2", Description = "Debug: LogReader test", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/errors", Description = "Last 100 errors", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/slow", Description = "Last 100 slow requests", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/search", Description = "Search by requestId", Auth = "Basic", Category = "Legacy" },
        }
    };

    // Fetch ChatAnalysis endpoints (internal call)
    var chatEndpoints = await chatClient.GetEndpointsAsync();

    // Fetch Automation endpoints (internal call)
    var autoEndpoints = await automationClient.GetEndpointsAsync();

    // Fetch AgentAI endpoints (internal call)
    var agentAIEndpoints = await agentAIClient.GetEndpointsAsync();

    // Fetch Outbound endpoints (internal call, GR-1.3)
    var outboundEndpoints = await outboundClient.GetEndpointsAsync();

    var services = new List<EndpointDiscoveryResponse> { backendEndpoints };
    if (chatEndpoints != null)
    {
        services.Add(chatEndpoints);
    }
    if (autoEndpoints != null)
    {
        services.Add(autoEndpoints);
    }
    if (agentAIEndpoints != null)
    {
        services.Add(agentAIEndpoints);
    }
    if (outboundEndpoints != null)
    {
        services.Add(outboundEndpoints);
    }

    return Results.Ok(new { services });
});

// Postman collection download - dynamically generated from endpoint discovery
app.MapGet("/api/ops/postman", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
        return Results.Unauthorized();
    }

    // Fetch all service endpoints
    var chatEndpoints = await chatClient.GetEndpointsAsync();
    var autoEndpoints = await automationClient.GetEndpointsAsync();
    var agentAIEndpoints = await agentAIClient.GetEndpointsAsync();

    var allServices = new List<(string service, int port, List<EndpointInfo> endpoints)>
    {
        (ServiceConstants.BackendServiceName, ServiceConstants.BackendPort, new List<EndpointInfo>
        {
            new() { Method = "POST", Path = "/api/v1/chat/analyze", Description = "Chat analysis (async, callback)", Auth = "none", Category = "API" },
            new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
            new() { Method = "GET", Path = "/api/ops/health", Description = "All services health", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/postman", Description = "Postman collection download", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/stream", Description = "Log stream with filters", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/grouped", Description = "Grouped log stream", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/context", Description = "Log context (\u00b110 lines)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/stats/errors", Description = "Error statistics (24h)", Auth = "Basic", Category = "Ops" },
            new() { Method = "POST", Path = "/api/ops/services/{name}/restart", Description = "Restart Windows Service", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/test/{service}/{path}", Description = "Test proxy for microservices", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/ops", Description = "Operations dashboard (legacy)", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/errors", Description = "Last 100 errors", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/slow", Description = "Last 100 slow requests", Auth = "Basic", Category = "Legacy" },
            new() { Method = "GET", Path = "/ops/search", Description = "Search by requestId", Auth = "Basic", Category = "Legacy" },
        })
    };

    if (chatEndpoints != null)
    {
        allServices.Add((chatEndpoints.Service, chatEndpoints.Port, chatEndpoints.Endpoints));
    }
    if (autoEndpoints != null)
    {
        allServices.Add((autoEndpoints.Service, autoEndpoints.Port, autoEndpoints.Endpoints));
    }
    if (agentAIEndpoints != null)
    {
        allServices.Add((agentAIEndpoints.Service, agentAIEndpoints.Port, agentAIEndpoints.Endpoints));
    }

    // Sample request bodies for known endpoints
    var sampleBodies = new Dictionary<string, string>
    {
        ["/api/v1/chat/analyze"] = """
{
  "ChatID": 12345,
  "InstanceID": 1,
  "UserID": 100,
  "RequestID": "test-req-001",
  "ChatServerURL": "https://your-callback-url.com/api/callback",
  "Lang": "tr",
  "LabelSearchText": "Satis,Destek,Sikayet",
  "MessageListObject": [
    { "Source": "CUSTOMER", "Message": "Merhaba, bilgi almak istiyorum" },
    { "Source": "AGENT", "Message": "Merhaba, nasil yardimci olabilirim?" }
  ]
}
""",
        ["/api/v1/analyze"] = """
{
  "ChatID": 12345,
  "InstanceID": 1,
  "UserID": 100,
  "RequestID": "direct-test-001",
  "ChatServerURL": "https://your-callback-url.com/api/callback",
  "Lang": "tr",
  "MessageListObject": [
    { "Source": "CUSTOMER", "Message": "Merhaba, bilgi almak istiyorum" },
    { "Source": "AGENT", "Message": "Merhaba, nasil yardimci olabilirim?" }
  ]
}
""",
        ["/api/v1/suggest"] = """
{
  "chat_id": 12345,
  "message_text": "Merhaba, siparis durumumu ogrenmek istiyorum",
  "customer_name": "Ali Yilmaz",
  "channel": "whatsapp",
  "language": "tr",
  "conversation_history": [
    { "source": "CUSTOMER", "text": "Merhaba", "timestamp": "2026-02-11T10:00:00Z" },
    { "source": "AGENT", "text": "Merhaba, nasil yardimci olabilirim?", "timestamp": "2026-02-11T10:00:05Z" }
  ],
  "templates": [],
  "template_variables": { "agent_name": "Ayse" }
}
""",
        ["/api/v1/feedback"] = """
{
  "suggestion_id": "00000000-0000-0000-0000-000000000000",
  "agent_action": "accepted",
  "final_reply_text": null
}
"""
    };

    // Build Postman collection
    var folders = new List<object>();
    foreach (var (service, port, endpoints) in allServices)
    {
        var shortName = service.Replace("Invekto.", "");
        var baseUrl = $"http://localhost:{port}";

        // Group by category
        var grouped = endpoints
            .GroupBy(e => e.Category ?? "Other")
            .OrderBy(g => g.Key == "API" ? 0 : g.Key == "Health" ? 1 : g.Key == "Ops" ? 2 : 3);

        foreach (var group in grouped)
        {
            var items = new List<object>();
            foreach (var ep in group)
            {
                var urlParts = ep.Path.TrimStart('/').Split('/');
                var request = new Dictionary<string, object>
                {
                    ["method"] = ep.Method,
                    ["url"] = new
                    {
                        raw = $"{baseUrl}{ep.Path}",
                        host = new[] { baseUrl },
                        path = urlParts
                    },
                    ["description"] = ep.Description
                };

                // Add auth header for Basic auth endpoints
                if (ep.Auth == "Basic")
                {
                    request["auth"] = new
                    {
                        type = "basic",
                        basic = new[]
                        {
                            new { key = "username", value = "{{ops_username}}" },
                            new { key = "password", value = "{{ops_password}}" }
                        }
                    };
                }

                // Add sample body for POST endpoints
                if (ep.Method == "POST" && sampleBodies.TryGetValue(ep.Path, out var body))
                {
                    request["header"] = new[] { new { key = "Content-Type", value = "application/json" } };
                    request["body"] = new
                    {
                        mode = "raw",
                        raw = body.Trim(),
                        options = new { raw = new { language = "json" } }
                    };
                }

                items.Add(new
                {
                    name = $"{ep.Method} {ep.Path}",
                    request
                });
            }

            folders.Add(new
            {
                name = $"{shortName} - {group.Key}",
                item = items
            });
        }
    }

    var collection = new
    {
        info = new
        {
            name = "InvektoServis API",
            description = $"Auto-generated from endpoint discovery at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            schema = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
        },
        variable = new[]
        {
            new { key = "ops_username", value = "admin" },
            new { key = "ops_password", value = "admin123" }
        },
        item = folders
    };

    ctx.Response.Headers.ContentDisposition = "attachment; filename=\"InvektoServis.postman_collection.json\"";
    return Results.Json(collection);
});

// ============================================
// AGENT ASSIST PROXY ENDPOINTS
// ============================================

// Proxy: Suggest reply (Main App -> Backend -> AgentAI)
app.MapPost("/api/v1/agent-assist/suggest", async (HttpContext ctx, AgentAIClient agentAIClient, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    // Read request body
    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
    {
        requestBody = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(requestBody))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIInvalidPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var (statusCode, body) = await agentAIClient.ProxySuggestAsync(requestBody, authHeader, requestId);
    sw.Stop();

    jsonLogger.StepInfo($"AgentAI suggest proxy: status={statusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null)
        await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

// Proxy: Feedback (Main App -> Backend -> AgentAI, fire-and-forget)
app.MapPost("/api/v1/agent-assist/feedback", async (HttpContext ctx, AgentAIClient agentAIClient, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
    {
        requestBody = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(requestBody))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AgentAIInvalidFeedback, "Request body is required", requestId),
            statusCode: 400);
    }

    var (statusCode, body) = await agentAIClient.ProxyFeedbackAsync(requestBody, authHeader, requestId);

    jsonLogger.StepInfo($"AgentAI feedback proxy: status={statusCode}", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null)
        await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

// Proxy: Webhook event (Main App -> Backend -> Automation, Automation stays localhost-only)
app.MapPost("/api/v1/automation/webhook", async (HttpContext ctx, AutomationClient automationClient, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
    {
        requestBody = await reader.ReadToEndAsync();
    }

    if (string.IsNullOrWhiteSpace(requestBody))
    {
        return Results.Json(
            ErrorResponse.Create("INV-BE-003", "Request body is required", requestId),
            statusCode: 400);
    }

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var (statusCode, body) = await automationClient.ProxyWebhookEventAsync(requestBody, authHeader, requestId);
    sw.Stop();

    jsonLogger.StepInfo($"Automation webhook proxy: status={statusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null)
        await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

// ============================================
// OUTBOUND PROXY ENDPOINTS (GR-1.3)
// ============================================

// Generic outbound proxy helper
async Task<IResult> OutboundProxyPost(HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var (statusCode, body) = await obClient.ProxyPostAsync(targetPath, requestBody, authHeader, requestId);
    sw.Stop();

    jsonLog.StepInfo($"Outbound proxy POST {targetPath}: status={statusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> OutboundProxyGet(HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var (statusCode, body) = await obClient.ProxyGetAsync(targetPath, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> OutboundProxyPut(HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var (statusCode, body) = await obClient.ProxyPutAsync(targetPath, requestBody, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> OutboundProxyDelete(HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var (statusCode, body) = await obClient.ProxyDeleteAsync(targetPath, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

// Broadcast
app.MapPost("/api/v1/outbound/broadcast/send", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/broadcast/send"));

app.MapGet("/api/v1/outbound/broadcast/{broadcastId}/status", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string broadcastId) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/broadcast/{broadcastId}/status"));

// Webhooks
app.MapPost("/api/v1/outbound/webhook/trigger", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/webhook/trigger"));

app.MapPost("/api/v1/outbound/webhook/delivery-status", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/webhook/delivery-status"));

app.MapPost("/api/v1/outbound/webhook/message", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/webhook/message"));

// Templates
app.MapGet("/api/v1/outbound/templates", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, "/api/v1/templates"));

app.MapPost("/api/v1/outbound/templates", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/templates"));

app.MapPut("/api/v1/outbound/templates/{id:int}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, int id) =>
    await OutboundProxyPut(ctx, obClient, jsonLog, $"/api/v1/templates/{id}"));

app.MapDelete("/api/v1/outbound/templates/{id:int}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, int id) =>
    await OutboundProxyDelete(ctx, obClient, jsonLog, $"/api/v1/templates/{id}"));

// Opt-out
app.MapPost("/api/v1/outbound/optout", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/optout"));

app.MapDelete("/api/v1/outbound/optout/{phone}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string phone) =>
    await OutboundProxyDelete(ctx, obClient, jsonLog, $"/api/v1/optout/{phone}"));

app.MapGet("/api/v1/outbound/optout/check/{phone}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string phone) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/optout/check/{phone}"));

// SPA fallback - serve index.html for non-API routes (Dashboard routing)
app.MapFallbackToFile("index.html");

logger.SystemInfo($"Backend starting on port {ServiceConstants.BackendPort}");
app.Run();

// Required for integration tests
public partial class Program { }
