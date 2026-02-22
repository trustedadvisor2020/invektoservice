using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Invekto.Shared.Middleware;
using Invekto.Backend.Data;
using Invekto.Backend.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.ChatAnalysis;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Integration;
using Invekto.Shared.DTOs.Analytics;
using Invekto.Shared.DTOs.Attribution;
using Invekto.Shared.DTOs.Leads;
using Invekto.Shared.Logging;
using Invekto.Shared.Logging.Reader;
using Invekto.Shared.Services;

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
var automationTimeoutMs = builder.Configuration.GetValue<int>("Microservice:Automation:TimeoutMs", 5000);
var knowledgeUrl = builder.Configuration["Microservice:Knowledge:Url"]
    ?? $"http://localhost:{ServiceConstants.KnowledgePort}";
var knowledgeLogPath = builder.Configuration["Microservice:Knowledge:LogPath"];
var appointmentsUrl = builder.Configuration["Microservice:Appointments:Url"]
    ?? $"http://localhost:{ServiceConstants.AppointmentsPort}";
var appointmentsLogPath = builder.Configuration["Microservice:Appointments:LogPath"];
var appointmentsTimeoutMs = builder.Configuration.GetValue<int>("Microservice:Appointments:TimeoutMs", 10000);
var waAnalyticsUrl = builder.Configuration["Microservice:WhatsAppAnalytics:Url"]
    ?? $"http://localhost:{ServiceConstants.WhatsAppAnalyticsPort}";
var waAnalyticsTimeoutMs = builder.Configuration.GetValue<int>("Microservice:WhatsAppAnalytics:TimeoutMs", 30000);
var integrationsUrl = builder.Configuration["Microservice:Integrations:Url"]
    ?? $"http://localhost:{ServiceConstants.IntegrationsPort}";
var integrationsTimeoutMs = builder.Configuration.GetValue<int>("Microservice:Integrations:TimeoutMs", 10000);
var marketingUrl = builder.Configuration["Microservice:Marketing:Url"]
    ?? $"http://localhost:{ServiceConstants.MarketingPort}";
var marketingTimeoutMs = builder.Configuration.GetValue<int>("Microservice:Marketing:TimeoutMs", 10000);

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
if (!string.IsNullOrEmpty(knowledgeLogPath))
{
    logPaths.Add(knowledgeLogPath);
}
if (!string.IsNullOrEmpty(appointmentsLogPath))
{
    logPaths.Add(appointmentsLogPath);
}
builder.Services.AddSingleton(new LogReader(logPaths.ToArray(), slowThresholdMs));

// Register log cleanup service (30 day retention)
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// PKT-3: Register AnalyticsRepository + MetricsAggregationService (requires PostgreSQL)
// Deferred registration: actual singleton created after pgFactory is available (see below)

// Configure Kestrel to listen on configured port + HTTPS if certificate is configured
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(ServiceConstants.BackendPort);

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
    client.Timeout = TimeSpan.FromMilliseconds(automationTimeoutMs);
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

// Configure Knowledge HTTP client (30s timeout for PDF uploads)
builder.Services.AddHttpClient<KnowledgeClient>(client =>
{
    client.BaseAddress = new Uri(knowledgeUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure Appointments HTTP client (GR-2.4)
builder.Services.AddHttpClient<AppointmentsClient>(client =>
{
    client.BaseAddress = new Uri(appointmentsUrl);
    client.Timeout = TimeSpan.FromMilliseconds(appointmentsTimeoutMs);
});

// Configure Integrations HTTP client
builder.Services.AddHttpClient<IntegrationsClient>(client =>
{
    client.BaseAddress = new Uri(integrationsUrl);
    client.Timeout = TimeSpan.FromMilliseconds(integrationsTimeoutMs);
});

// Configure Marketing HTTP client (GR-3.21/3.22)
builder.Services.AddHttpClient<MarketingClient>(client =>
{
    client.BaseAddress = new Uri(marketingUrl);
    client.Timeout = TimeSpan.FromMilliseconds(marketingTimeoutMs);
});

// Configure FlowBuilder proxy HTTP client (reuses Automation URL for flow management)
builder.Services.AddHttpClient<FlowBuilderClient>(client =>
{
    client.BaseAddress = new Uri(automationUrl);
    client.Timeout = TimeSpan.FromMilliseconds(automationTimeoutMs);
});

// Register AI Wizard service for flow builder
builder.Services.AddHttpClient<ClaudeWizardService>();

// Configure WhatsApp Analytics HTTP client (PKT-4: NLP query + upload proxy)
builder.Services.AddHttpClient<WhatsAppAnalyticsClient>(client =>
{
    client.BaseAddress = new Uri(waAnalyticsUrl);
    client.Timeout = TimeSpan.FromMilliseconds(waAnalyticsTimeoutMs);
});

// ============================================
// GR-1.9: INTEGRATION BRIDGE SETUP
// ============================================

// JWT Validator + Generator (singleton, thread-safe)
JwtValidator? jwtValidator = null;
JwtGenerator? jwtGenerator = null;
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
    jwtGenerator = new JwtGenerator(jwtSettings);
    builder.Services.AddSingleton(jwtValidator);
    builder.Services.AddSingleton(jwtGenerator);
}

// InmaJwtValidator + settings (singleton, thread-safe)
InmaJwtValidator? inmaJwtValidator = null;
// Always create settings if any InmaAuth config exists (proxy endpoints need URLs even without SecretKey)
var inmaJwtSettings = new InmaJwtSettings
{
    SecretKey = builder.Configuration["InmaAuth:SecretKey"] ?? string.Empty,
    LoginUrl = builder.Configuration["InmaAuth:LoginUrl"] ?? string.Empty,
    ClockSkewSeconds = builder.Configuration.GetValue<int>("InmaAuth:ClockSkewSeconds", 60),
    LoginTimeoutMs = builder.Configuration.GetValue<int>("InmaAuth:LoginTimeoutMs", 10000),
    RefreshUrl = builder.Configuration["InmaAuth:RefreshUrl"],
    ApiBaseUrl = builder.Configuration["InmaAuth:ApiBaseUrl"]
};
if (!string.IsNullOrEmpty(inmaJwtSettings.SecretKey))
{
    inmaJwtValidator = new InmaJwtValidator(inmaJwtSettings);
}
var inmaAuthMockEnabled = builder.Configuration.GetValue<bool>("InmaAuth:MockEnabled", false);

// inma login proxy HTTP client (no base address — LoginUrl is full URL from config)
builder.Services.AddHttpClient("inma_login", client =>
{
    var loginTimeoutMs = builder.Configuration.GetValue<int>("InmaAuth:LoginTimeoutMs", 10000);
    client.Timeout = TimeSpan.FromMilliseconds(loginTimeoutMs);
});

// PostgreSQL connection factory (singleton, thread-safe pooling)
PostgresConnectionFactory? pgFactory = null;
var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSQL");
if (!string.IsNullOrEmpty(pgConnectionString))
{
    pgFactory = new PostgresConnectionFactory(pgConnectionString);
    builder.Services.AddSingleton(pgFactory);

    // PKT-3: AnalyticsRepository (singleton, thread-safe via connection pooling)
    builder.Services.AddSingleton<AnalyticsRepository>();
    // PKT-3: MetricsAggregationService (IHostedService, 5min aggregation timer)
    builder.Services.AddHostedService<MetricsAggregationService>();

    // GR-3.14: Attribution tracking (singleton, thread-safe via connection pooling)
    builder.Services.AddSingleton<AttributionRepository>();
    builder.Services.AddSingleton<AttributionService>();

    // PKT-6B1: Lead Management v2 (GR-3.13)
    builder.Services.AddSingleton<LeadRepository>();

    // SuperAdmin: Message log (fire-and-forget insert at webhook, paginated select for ops)
    builder.Services.AddSingleton<MessageLogRepository>();

    // SuperAdmin: Tenant registry (list + impersonate)
    builder.Services.AddSingleton<TenantRegistryRepository>();
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
app.UseTrafficLogging(
    new[] { "/health", "/api/ops/", "/ops", "/assets/", "/favicon", "/logs", "/login" },
    new[] { ".js", ".css", ".svg", ".png", ".ico", ".woff", ".woff2", ".map" });

// GR-1.9: JWT auth middleware for protected API paths
// Webhook:AllowedIps — trusted IPs bypass JWT and use ?companyId= query param
if (jwtValidator != null)
{
    var jwtLogger = app.Services.GetRequiredService<JsonLinesLogger>();
    var webhookIps = builder.Configuration.GetSection("Webhook:AllowedIps").Get<string[]>() ?? [];
    var webhookIpSet = new HashSet<string>(webhookIps, StringComparer.OrdinalIgnoreCase);
    app.UseJwtAuth(jwtValidator, jwtLogger, webhookIpSet, "/api/v1/webhook/", "/api/v1/automation/", "/api/v1/outbound/", "/api/v1/flow-builder/flows/", "/api/v1/flow-builder/wizard/", "/api/v1/attribution/", "/api/v1/leads/");
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

// Ops auth: Basic Auth (admin) veya Bearer inse JWT (role=admin)
bool ValidateOpsAuth(HttpContext ctx)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader)) return false;

    // Basic Auth
    if (authHeader.StartsWith("Basic "))
    {
        try
        {
            var encoded = authHeader["Basic ".Length..];
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var parts = decoded.Split(':', 2);
            return parts.Length == 2 && parts[0] == opsUsername && parts[1] == opsPassword;
        }
        catch { return false; }
    }

    // Bearer JWT — try inse first, then inma
    if (authHeader.StartsWith("Bearer "))
    {
        var token = authHeader["Bearer ".Length..];

        // inse internal JWT
        if (jwtValidator != null)
        {
            var (context, _) = jwtValidator.ValidateToken(token);
            if (context?.Role == "admin") return true;
        }

        // inma JWT fallback (direct token from main app)
        if (inmaJwtValidator != null)
        {
            var (inmaCtx, _) = inmaJwtValidator.ValidateToken(token);
            if (inmaCtx?.Role == "admin") return true;
        }

        // Path C: decode-only fallback (INMA token without SecretKey configured)
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwt = handler.ReadJwtToken(token);
                if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow.AddSeconds(-60))
                    return false; // expired
                var chatRole = jwt.Claims.FirstOrDefault(c => c.Type == "ChatRole")?.Value;
                if (chatRole == "2") return true; // ChatRole 2 = admin
            }
        }
        catch { /* decode failed */ }

        return false;
    }

    return false;
}

// Ops 401 response: only trigger browser Basic popup for direct navigation (not SPA fetch)
IResult OpsUnauthorized(HttpContext ctx)
{
    if (!ctx.Request.Headers.ContainsKey("X-Requested-With"))
    {
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"Ops\"";
    }
    return Results.Unauthorized();
}

string Truncate(string? s, int maxLen) => s == null ? "" : s.Length <= maxLen ? s : s[..maxLen] + "...";

// OPS endpoint - Stage-0 troubleshooting dashboard
app.MapGet("/ops", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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
        return OpsUnauthorized(ctx);
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
        return OpsUnauthorized(ctx);
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
        return OpsUnauthorized(ctx);
    }

    var errors = await logReader.GetLastErrorsAsync(100);
    return Results.Ok(new { count = errors.Count, errors });
});

// OPS: Last 100 slow requests
app.MapGet("/ops/slow", async (HttpContext ctx, LogReader logReader) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    var slow = await logReader.GetLastSlowRequestsAsync(100);
    return Results.Ok(new { count = slow.Count, threshold_ms = slowThresholdMs, requests = slow });
});

// OPS: Search by requestId
app.MapGet("/ops/search", async (HttpContext ctx, LogReader logReader, string? requestId) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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
app.MapGet("/api/ops/health", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, KnowledgeClient knowledgeClient, AppointmentsClient appointmentsClient, IntegrationsClient integrationsClient, WhatsAppAnalyticsClient waAnalyticsClient, MarketingClient marketingClient) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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

    // Knowledge - check health with timing
    var swKnowledge = System.Diagnostics.Stopwatch.StartNew();
    var knowledgeHealthy = await knowledgeClient.CheckHealthAsync();
    swKnowledge.Stop();

    services.Add(new
    {
        name = ServiceConstants.KnowledgeServiceName,
        status = knowledgeHealthy ? "ok" : "unavailable",
        responseTimeMs = knowledgeHealthy ? (int?)swKnowledge.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = knowledgeHealthy ? null : "Service unreachable"
    });

    // Appointments - check health with timing (GR-2.4)
    var swAppointments = System.Diagnostics.Stopwatch.StartNew();
    var appointmentsHealthy = await appointmentsClient.CheckHealthAsync();
    swAppointments.Stop();

    services.Add(new
    {
        name = ServiceConstants.AppointmentsServiceName,
        status = appointmentsHealthy ? "ok" : "unavailable",
        responseTimeMs = appointmentsHealthy ? (int?)swAppointments.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = appointmentsHealthy ? null : "Service unreachable"
    });

    // Integrations - check health with timing
    var swIntegrations = System.Diagnostics.Stopwatch.StartNew();
    var integrationsHealthy = await integrationsClient.CheckHealthAsync();
    swIntegrations.Stop();

    services.Add(new
    {
        name = ServiceConstants.IntegrationsServiceName,
        status = integrationsHealthy ? "ok" : "unavailable",
        responseTimeMs = integrationsHealthy ? (int?)swIntegrations.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = integrationsHealthy ? null : "Service unreachable"
    });

    // WhatsApp Analytics - check health with timing (PKT-4)
    var swWaAnalytics = System.Diagnostics.Stopwatch.StartNew();
    var waAnalyticsHealthy = await waAnalyticsClient.CheckHealthAsync();
    swWaAnalytics.Stop();

    services.Add(new
    {
        name = ServiceConstants.WhatsAppAnalyticsServiceName,
        status = waAnalyticsHealthy ? "ok" : "unavailable",
        responseTimeMs = waAnalyticsHealthy ? (int?)swWaAnalytics.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = waAnalyticsHealthy ? null : "Service unreachable"
    });

    // Marketing - check health with timing (GR-3.21/3.22)
    var swMarketing = System.Diagnostics.Stopwatch.StartNew();
    var marketingHealthy = await marketingClient.CheckHealthAsync();
    swMarketing.Stop();

    services.Add(new
    {
        name = ServiceConstants.MarketingServiceName,
        status = marketingHealthy ? "ok" : "unavailable",
        responseTimeMs = marketingHealthy ? (int?)swMarketing.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = marketingHealthy ? null : "Service unreachable"
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
        return OpsUnauthorized(ctx);
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
        return OpsUnauthorized(ctx);
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
        return OpsUnauthorized(ctx);
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

// Dashboard: Clear log files
app.MapDelete("/api/ops/logs/clear", (HttpContext ctx, LogReader logReader, string? service) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    int deleted;
    if (!string.IsNullOrEmpty(service))
    {
        deleted = logReader.ClearServiceLogs(service);
    }
    else
    {
        deleted = logReader.ClearAllLogs();
    }

    return Results.Ok(new { deleted, service = service ?? "all" });
});

// Dashboard: Error stats by hour
app.MapGet("/api/ops/stats/errors", async (HttpContext ctx, LogReader logReader, int? hours) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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
        return OpsUnauthorized(ctx);
    }

    // Map service name to Windows Service name
    var windowsServiceName = serviceName switch
    {
        "Invekto.Backend" => "InvektoBackend",
        "Invekto.ChatAnalysis" => "InvektoChatAnalysis",
        "Invekto.Automation" => "InvektoAutomation",
        "Invekto.AgentAI" => "InvektoAgentAI",
        "Invekto.Outbound" => "InvektoOutbound",
        "Invekto.Knowledge" => "InvektoKnowledge",
        "Invekto.Appointments" => "InvektoAppointments",
        "Invekto.Integrations" => "InvektoIntegrations",
        "Invekto.WhatsAppAnalytics" => "InvektoWhatsAppAnalytics",
        "Invekto.Marketing" => "InvektoMarketing",
        _ => null
    };

    if (windowsServiceName == null)
    {
        return Results.Ok(new
        {
            success = false,
            service = serviceName,
            message = "Bilinmeyen servis veya yeniden baslatma desteklenmiyor"
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
            message = "Servis basariyla yeniden baslatildi"
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Ok(new
        {
            success = false,
            service = serviceName,
            message = $"Servis bulunamadi veya kurulu degil: {ex.Message}"
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            success = false,
            service = serviceName,
            message = $"Yeniden baslatma hatasi: {ex.Message}"
        });
    }
});

// Dashboard: Test proxy for external services (avoids CORS issues)
app.MapGet("/api/ops/test/{serviceName}/{*path}", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, KnowledgeClient knowledgeClient, AppointmentsClient appointmentsClient, IntegrationsClient integrationsClient, WhatsAppAnalyticsClient waAnalyticsClient, MarketingClient marketingClient, string serviceName, string? path) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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

        if (serviceName == "knowledge")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await knowledgeClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "appointments")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await appointmentsClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "integrations")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await integrationsClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "whatsappanalytics" || serviceName == "wa-analytics")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await waAnalyticsClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "marketing")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await marketingClient.TestEndpointAsync(endpoint);
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
    catch (HttpRequestException ex)
    {
        return Results.Ok(new
        {
            success = false,
            statusCode = 0,
            durationMs = 0,
            message = ex.Message
        });
    }
    catch (TaskCanceledException ex)
    {
        return Results.Ok(new
        {
            success = false,
            statusCode = 0,
            durationMs = 0,
            message = $"Timeout: {ex.Message}"
        });
    }
});

// ============================================
// GR-1.9: INTEGRATION WEBHOOK ENDPOINTS
// ============================================

// Webhook event receiver (INMA -> InvektoServis)
// Auth: JWT or IP whitelist with ?companyId= query param
app.MapPost("/api/v1/webhook/event", async (HttpContext ctx, JsonLinesLogger jsonLogger, IncomingWebhookEvent? webhookEvent) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var requestId = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    // Extract TenantContext (set by JWT middleware or IP whitelist)
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        sw.Stop();
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", requestId),
            statusCode: 401);
    }

    // Validate payload
    if (webhookEvent?.Messages == null || webhookEvent.Messages.Count == 0)
    {
        sw.Stop();
        var reqCtx = RequestContext.Create(tenantContext.TenantId.ToString(), "-");
        jsonLogger.RequestError("Webhook: empty or null payload", reqCtx, "/api/v1/webhook/event", sw.ElapsedMilliseconds, ErrorCodes.IntegrationWebhookInvalidPayload);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.IntegrationWebhookInvalidPayload, "messages array is required and must not be empty", requestId),
            statusCode: 400);
    }

    sw.Stop();
    var msgCount = webhookEvent.Messages.Count;
    var firstChatId = webhookEvent.Messages[0].ChatId ?? "-";
    var context = RequestContext.CreateWithPassThrough(requestId, tenantContext.TenantId.ToString(), firstChatId);

    // Log the accepted event
    jsonLogger.RequestInfo(
        $"Webhook accepted: msg_count={msgCount}, instance={webhookEvent.InstanceId}, chat_id={firstChatId}",
        context, "/api/v1/webhook/event", sw.ElapsedMilliseconds);

    // Latency monitoring
    if (sw.ElapsedMilliseconds > ServiceConstants.IntegrationLatencyThresholdMs)
    {
        jsonLogger.SystemWarn(
            $"Webhook acceptance exceeded {ServiceConstants.IntegrationLatencyThresholdMs}ms threshold: {sw.ElapsedMilliseconds}ms");
    }

    // Add processing time header
    ctx.Response.Headers[HeaderNames.ProcessingTimeMs] = sw.ElapsedMilliseconds.ToString();

    // SuperAdmin: fire-and-forget message logging
    var msgLogRepo = ctx.RequestServices.GetService<MessageLogRepository>();
    if (msgLogRepo != null)
    {
        foreach (var msg in webhookEvent.Messages)
        {
            var phone = (msg.ChatId ?? "").Replace("@c.us", "").Replace("@g.us", "");
            _ = msgLogRepo.InsertAsync(
                tenantContext.TenantId,
                msg.FromMe ? "out" : "in",
                phone,
                msg.SenderName,
                msg.Body,
                msg.Type ?? "text",
                msg.ChatId,
                msg.Id,
                webhookEvent.InstanceId
            ).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    jsonLogger.SystemWarn($"MessageLog insert failed: {t.Exception?.InnerException?.Message}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    // Forward to Automation for processing (fire-and-forget)
    var automationClient = ctx.RequestServices.GetService<AutomationClient>();
    if (automationClient != null)
    {
        var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
        // IP whitelist case: no auth header, generate temp JWT for Automation
        if (string.IsNullOrEmpty(authHeader) && jwtGenerator != null)
        {
            var tempToken = jwtGenerator.GenerateToken(
                tenantContext.TenantId, "system", "webhook_proxy",
                TimeSpan.FromMinutes(5), tenantContext.UserId.ToString());
            authHeader = $"Bearer {tempToken}";
        }
        var eventJson = JsonSerializer.Serialize(webhookEvent);
        _ = automationClient.ProxyWebhookEventAsync(eventJson, authHeader, requestId)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    jsonLogger.SystemWarn($"Automation proxy failed: {t.Exception?.GetBaseException().Message}");
                else if (t.Result.StatusCode >= 400)
                    jsonLogger.StepWarn($"Automation proxy returned {t.Result.StatusCode}: {t.Result.Body}", requestId);
            });
    }

    // Return 202 Accepted
    return Results.Json(new
    {
        status = "accepted",
        request_id = context.RequestId,
        message_count = msgCount,
        instance_id = webhookEvent.InstanceId,
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
app.MapGet("/api/ops/endpoints", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, KnowledgeClient knowledgeClient, AppointmentsClient appointmentsClient, IntegrationsClient integrationsClient, WhatsAppAnalyticsClient waAnalyticsClient, MarketingClient marketingClient) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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
            new() { Method = "GET", Path = "/api/v1/automation/flows/{tenantId}", Description = "Flow list for tenant (Main App routing config)", Auth = "Bearer", Category = "API" },
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

            // Appointments proxy endpoints (GR-2.4)
            new() { Method = "GET", Path = "/api/v1/appointments/slots", Description = "List slots proxy (Backend -> Appointments)", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/slots", Description = "Create slot proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/appointments/slots/{id}", Description = "Update slot proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "DELETE", Path = "/api/v1/appointments/slots/{id}", Description = "Deactivate slot proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/book", Description = "Book appointment proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/list", Description = "List appointments proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/{id}", Description = "Get appointment proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/{id}/cancel", Description = "Cancel appointment proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/available-slots", Description = "Available slots proxy (?doctor_id=)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/no-show-stats", Description = "No-show stats proxy (?phone=)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/waitlist", Description = "List waitlist proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/waitlist", Description = "Add to waitlist proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/appointments/waitlist/{id}/status", Description = "Update waitlist status proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/pricing", Description = "List pricing proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/pricing", Description = "Create pricing proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/appointments/pricing/{id}", Description = "Update pricing proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/calendar/status", Description = "Calendar sync status proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/lifecycle/start", Description = "Start treatment lifecycle proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/lifecycle", Description = "List lifecycles proxy (?type=&status=)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/appointments/lifecycle/{id}", Description = "Get lifecycle proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/lifecycle/{id}/cancel", Description = "Cancel lifecycle proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/appointments/lifecycle/{id}/response", Description = "Record patient response proxy", Auth = "Bearer", Category = "API" },

            // GR-3.14: Attribution
            new() { Method = "GET", Path = "/api/v1/attribution/leads", Description = "List lead attributions", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/attribution/leads/{id}/status", Description = "Update lead status", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/attribution/summary", Description = "Attribution summary", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/attribution/cost-per-lead", Description = "Cost per lead by platform", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/attribution/costs", Description = "List ad costs", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/attribution/costs", Description = "Create ad cost entry", Auth = "Bearer", Category = "API" },
            new() { Method = "DELETE", Path = "/api/v1/attribution/costs/{id}", Description = "Delete ad cost entry", Auth = "Bearer", Category = "API" },

            // Marketing proxy endpoints (GR-3.21/3.22)
            new() { Method = "POST", Path = "/api/v1/reviews/request", Description = "Create review request proxy (Backend -> Marketing)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/reviews", Description = "List review requests proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/reviews/{id}/sent", Description = "Mark review sent proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/reviews/{id}/posted", Description = "Mark review posted proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/reviews/stats", Description = "Review stats proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/referrals", Description = "Create referral proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/referrals", Description = "List referrals proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/referrals/lookup/{code}", Description = "Lookup referral by code proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/referrals/{id}/redeem", Description = "Redeem referral proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/tourism/leads", Description = "Create tourism lead proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/tourism/leads", Description = "List tourism leads proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/tourism/leads/{id}", Description = "Get tourism lead proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/tourism/leads/{id}", Description = "Update tourism lead proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/tourism/stats", Description = "Tourism stats proxy", Auth = "Bearer", Category = "API" },

            // Health
            new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },

            // Ops Dashboard API
            new() { Method = "GET", Path = "/api/ops/health", Description = "All services health", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/stream", Description = "Log stream with filters", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/grouped", Description = "Grouped log stream (operations view)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/logs/context", Description = "Log context (\u00b110 lines)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/stats/errors", Description = "Error statistics (24h)", Auth = "Basic", Category = "Ops" },
            new() { Method = "DELETE", Path = "/api/ops/logs/clear", Description = "Clear log files (all or by service)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/postman", Description = "Postman collection download", Auth = "Basic", Category = "Ops" },
            new() { Method = "POST", Path = "/api/ops/services/{name}/restart", Description = "Restart Windows Service", Auth = "Basic", Category = "Ops" },
            new() { Method = "GET", Path = "/api/ops/test/{service}/{path}", Description = "Test proxy for microservices", Auth = "Basic", Category = "Ops" },

            // Lead Management (PKT-6B1: GR-3.13)
            new() { Method = "POST", Path = "/api/v1/leads", Description = "Create/upsert lead", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "GET", Path = "/api/v1/leads", Description = "List leads", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "GET", Path = "/api/v1/leads/{id}", Description = "Get lead", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "PUT", Path = "/api/v1/leads/{id}/status", Description = "Update lead pipeline status", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "PUT", Path = "/api/v1/leads/{id}/score", Description = "Update lead score", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "POST", Path = "/api/v1/leads/{id}/activities", Description = "Add lead activity", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "GET", Path = "/api/v1/leads/{id}/activities", Description = "Get lead activities", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "GET", Path = "/api/v1/leads/funnel", Description = "Lead funnel stats", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "GET", Path = "/api/v1/leads/hot", Description = "Hot leads list", Auth = "Bearer JWT", Category = "Leads" },
            new() { Method = "POST", Path = "/api/v1/leads/{id}/followup", Description = "Schedule lead follow-up", Auth = "Bearer JWT", Category = "Leads" },

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

    // Fetch Knowledge endpoints (internal call)
    var knowledgeEndpoints = await knowledgeClient.GetEndpointsAsync();

    // Fetch Appointments endpoints (internal call, GR-2.4)
    var appointmentsEndpoints = await appointmentsClient.GetEndpointsAsync();

    // Fetch WhatsApp Analytics endpoints (internal call, PKT-4)
    var waAnalyticsEndpoints = await waAnalyticsClient.GetEndpointsAsync();

    // Fetch Integrations endpoints (internal call)
    var integrationsEndpoints = await integrationsClient.GetEndpointsAsync();

    // Fetch Marketing endpoints (internal call, GR-3.21/3.22)
    var marketingEndpoints = await marketingClient.GetEndpointsAsync();

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
    if (knowledgeEndpoints != null)
    {
        services.Add(knowledgeEndpoints);
    }
    if (appointmentsEndpoints != null)
    {
        services.Add(appointmentsEndpoints);
    }
    if (integrationsEndpoints != null)
    {
        services.Add(integrationsEndpoints);
    }
    if (waAnalyticsEndpoints != null)
    {
        services.Add(waAnalyticsEndpoints);
    }
    if (marketingEndpoints != null)
    {
        services.Add(marketingEndpoints);
    }

    return Results.Ok(new { services });
});

// Postman collection download - dynamically generated from endpoint discovery
app.MapGet("/api/ops/postman", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
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
            new() { Method = "DELETE", Path = "/api/ops/logs/clear", Description = "Clear log files", Auth = "Basic", Category = "Ops" },
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
            ErrorResponse.Create(ErrorCodes.BackendMicroserviceError, "Request body is required", requestId),
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

// Flow list for Main App routing config (same Automation backend, different path for Main App consumers)
app.MapGet("/api/v1/automation/flows/{tenantId:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLogger, int tenantId) =>
    await FbProxyGet(ctx, fbClient, jsonLogger, $"/api/v1/flows/{tenantId}"));

// Tenant-level automation analytics summary (manual JWT validation: INSE + INMA fallback)
app.MapGet("/api/v1/dashboard/analytics/summary", async (HttpContext ctx, AnalyticsRepository analyticsRepo, JsonLinesLogger jsonLogger, string? from, string? to) =>
{
    // Manual JWT validation (not under middleware-protected prefix)
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"),
            statusCode: 401);
    }

    var token = authHeader["Bearer ".Length..].Trim();
    TenantContext? tenantContext = null;

    // Try INSE JwtValidator first, then INMA fallback
    if (jwtValidator != null)
    {
        var (ctx1, _) = jwtValidator.ValidateToken(token);
        tenantContext = ctx1;
    }
    if (tenantContext == null && inmaJwtValidator != null)
    {
        var (inmaCtx, _) = inmaJwtValidator.ValidateToken(token);
        if (inmaCtx != null)
        {
            tenantContext = new TenantContext
            {
                TenantId = inmaCtx.TenantId,
                UserId = inmaCtx.UserId,
                Role = inmaCtx.Role
            };
        }
    }

    // Path C: decode-only fallback (INMA token without SecretKey configured)
    if (tenantContext == null)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                var jwt = handler.ReadJwtToken(token);

                // Manual expiry check
                if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow.AddSeconds(-60))
                {
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthTokenExpired, "Token expired", "-"),
                        statusCode: 401);
                }

                // CompanyCode = our tenant_id (e.g. "5050"), CompanyId = INMA's internal ID
                var companyCodeStr = jwt.Claims.FirstOrDefault(c => c.Type == "CompanyCode")?.Value;
                var userIdStr = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                    || c.Type == ClaimTypes.NameIdentifier)?.Value;
                var chatRole = jwt.Claims.FirstOrDefault(c => c.Type == "ChatRole")?.Value;

                if (int.TryParse(companyCodeStr, out var tenantId) && tenantId > 0
                    && int.TryParse(userIdStr, out var userId))
                {
                    tenantContext = new TenantContext
                    {
                        TenantId = tenantId,
                        UserId = userId,
                        Role = chatRole switch { "2" => "admin", _ => "agent" }
                    };
                    jsonLogger.SystemInfo($"Dashboard analytics (decode-only): tenant={tenantId} user={userId}");
                }
            }
        }
        catch { /* decode failed — fall through to 401 */ }
    }

    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Invalid or expired token", "-"),
            statusCode: 401);
    }

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-7) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid date format (expected yyyy-MM-dd)" });
    }

    if (fromDate > toDate)
    {
        return Results.BadRequest(new { error = ErrorCodes.MetricsInvalidDateRange, message = "Gecersiz tarih araligi (baslangic > bitis)." });
    }

    try
    {
        var summary = await analyticsRepo.GetAutomationSummaryAsync(tenantContext.TenantId, fromDate, toDate);
        return Results.Ok(summary);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.SystemWarn($"Tenant analytics summary failed for tenant {tenantContext.TenantId} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
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

// ============================================
// FLOW BUILDER PROXY ENDPOINTS
// ============================================

// Generic flow builder proxy helpers (same pattern as Outbound proxy)
async Task<IResult> FbProxyGet(HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var (statusCode, body) = await fbClient.ProxyGetAsync(targetPath, authHeader, requestId);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> FbProxyPost(HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();
    var (statusCode, body) = await fbClient.ProxyPostAsync(targetPath, requestBody, authHeader, requestId);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> FbProxyPut(HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();
    var (statusCode, body) = await fbClient.ProxyPutAsync(targetPath, requestBody, authHeader, requestId);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> FbProxyDelete(HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var (statusCode, body) = await fbClient.ProxyDeleteAsync(targetPath, authHeader, requestId);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

// Flow CRUD proxy: Backend /api/v1/flow-builder/flows/* -> Automation /api/v1/flows/*
app.MapGet("/api/v1/flow-builder/flows/{tenantId:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}"));

app.MapGet("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}"));

app.MapPost("/api/v1/flow-builder/flows/{tenantId:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId) =>
    await FbProxyPost(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}"));

app.MapPut("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyPut(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}"));

app.MapDelete("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyDelete(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}"));

app.MapPost("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/activate", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyPost(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/activate"));

app.MapPost("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/deactivate", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyPost(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/deactivate"));

// Validation proxy: Backend /api/v1/flow-builder/flows/validate -> Automation /api/v1/flows/validate
app.MapPost("/api/v1/flow-builder/flows/validate", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
    await FbProxyPost(ctx, fbClient, jsonLog, "/api/v1/flows/validate"));

// Simulation proxy: Backend /api/v1/flow-builder/simulation/* -> Automation /api/v1/simulation/*
app.MapPost("/api/v1/flow-builder/simulation/start", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
    await FbProxyPost(ctx, fbClient, jsonLog, "/api/v1/simulation/start"));

app.MapPost("/api/v1/flow-builder/simulation/step", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
    await FbProxyPost(ctx, fbClient, jsonLog, "/api/v1/simulation/step"));

app.MapDelete("/api/v1/flow-builder/simulation/{sessionId}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, string sessionId) =>
    await FbProxyDelete(ctx, fbClient, jsonLog, $"/api/v1/simulation/{sessionId}"));

// ============================================
// FLOW BUILDER WIZARD (AI-powered flow creation)
// ============================================

async Task<IResult> FbProxyPatch(HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    using var bodyReader = new StreamReader(ctx.Request.Body);
    var body = await bodyReader.ReadToEndAsync();
    var (statusCode, respBody) = await fbClient.ProxyPatchAsync(targetPath, body, authHeader, requestId);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (respBody != null) await ctx.Response.WriteAsync(respBody);
    return Results.Empty;
}

// POST /api/v1/flow-builder/wizard/start — Create draft flow and start wizard session
app.MapPost("/api/v1/flow-builder/wizard/start", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", requestId), statusCode: 401);

    var tenantId = tenantContext.TenantId;

    try
    {
        var draftName = $"AI Taslak - {DateTime.UtcNow:dd MMM HH:mm}";
        var emptyConfig = System.Text.Json.JsonSerializer.Serialize(new
        {
            version = 2,
            metadata = new { name = draftName },
            nodes = Array.Empty<object>(),
            edges = Array.Empty<object>(),
            settings = new
            {
                off_hours_message = "Su anda mesai saatleri disindayiz.",
                unknown_input_message = "Anlayamadim. Lutfen gecerli bir secenek girin.",
                handoff_confidence_threshold = 0.5,
                session_timeout_minutes = 30,
                max_loop_count = 10
            }
        });

        var createBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            flow_name = draftName,
            flow_config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(emptyConfig),
            wizard_status = "drafting"
        });

        var (statusCode, respBody) = await fbClient.ProxyPostAsync($"/api/v1/flows/{tenantId}", createBody, authHeader, requestId);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        if (respBody != null) await ctx.Response.WriteAsync(respBody);
        return Results.Empty;
    }
    catch (HttpRequestException ex)
    {
        jsonLog.StepError($"[{ErrorCodes.BackendWizardSessionFailed}] Wizard start proxy failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendWizardSessionFailed, "Wizard session olusturulamadi", requestId), statusCode: 502);
    }
    catch (System.Text.Json.JsonException ex)
    {
        jsonLog.StepError($"[{ErrorCodes.BackendWizardInvalidPayload}] Wizard start JSON error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendWizardInvalidPayload, "Gecersiz istek formati", requestId), statusCode: 400);
    }
});

// POST /api/v1/flow-builder/wizard/{flowId}/message — Send message to wizard, return SSE stream
app.MapPost("/api/v1/flow-builder/wizard/{flowId:int}/message", async (int flowId, HttpContext ctx,
    FlowBuilderClient fbClient, ClaudeWizardService wizardService, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsJsonAsync(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", requestId));
        return;
    }

    var tenantId = tenantContext.TenantId;

    if (!wizardService.IsAvailable)
    {
        ctx.Response.StatusCode = 503;
        await ctx.Response.WriteAsJsonAsync(ErrorResponse.Create(ErrorCodes.BackendWizardAiUnavailable, "AI servisi yapilandirilmamis. Claude API anahtari gerekli.", requestId));
        return;
    }

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        var userMessage = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(ErrorResponse.Create(ErrorCodes.BackendWizardInvalidPayload, "message is required", requestId));
            return;
        }

        // Load existing wizard history
        var (flowStatus, flowBody) = await fbClient.ProxyGetAsync($"/api/v1/flows/{tenantId}/{flowId}", authHeader, requestId);
        var history = new List<WizardMessage>();
        if (flowStatus == 200 && flowBody != null)
        {
            using var flowDoc = System.Text.Json.JsonDocument.Parse(flowBody);
            var flowRoot = flowDoc.RootElement;
            if (flowRoot.TryGetProperty("wizard_history", out var whProp) && whProp.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in whProp.EnumerateArray())
                {
                    history.Add(new WizardMessage
                    {
                        Role = item.GetProperty("role").GetString() ?? "user",
                        Content = item.GetProperty("content").GetString() ?? "",
                        Timestamp = item.TryGetProperty("timestamp", out var ts) ? ts.GetString() : null
                    });
                }
            }
        }

        // Load existing flows for context
        List<FlowSummaryContext>? existingFlows = null;
        var (listStatus, listBody) = await fbClient.ProxyGetAsync($"/api/v1/flows/{tenantId}", authHeader, requestId);
        if (listStatus == 200 && listBody != null)
        {
            existingFlows = ParseFlowSummaries(listBody, flowId, app.Logger);
        }

        // Set up SSE response
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.Append("Cache-Control", "no-cache");
        ctx.Response.Headers.Append("Connection", "keep-alive");
        ctx.Response.Headers.Append("X-Accel-Buffering", "no");

        var fullAssistantText = new System.Text.StringBuilder();
        string? extractedFlowConfig = null;
        List<FlowPrerequisite>? prerequisites = null;

        await foreach (var chunk in wizardService.StreamChatAsync(userMessage, history, existingFlows, ctx.RequestAborted))
        {
            if (chunk.Type == "done")
            {
                fullAssistantText.Clear();
                fullAssistantText.Append(chunk.Content);
                extractedFlowConfig = chunk.FlowConfig;
                prerequisites = chunk.Prerequisites;

                var doneEvent = System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "done",
                    flow_config = extractedFlowConfig != null
                        ? System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(extractedFlowConfig)
                        : (System.Text.Json.JsonElement?)null,
                    prerequisites
                });
                await ctx.Response.WriteAsync($"data: {doneEvent}\n\n");
                await ctx.Response.Body.FlushAsync();
            }
            else
            {
                if (chunk.Type == "text") fullAssistantText.Append(chunk.Content);
                var eventData = System.Text.Json.JsonSerializer.Serialize(new { type = chunk.Type, content = chunk.Content });
                await ctx.Response.WriteAsync($"data: {eventData}\n\n");
                await ctx.Response.Body.FlushAsync();
            }
        }

        // Save updated history
        history.Add(new WizardMessage { Role = "user", Content = userMessage, Timestamp = DateTime.UtcNow.ToString("o") });
        history.Add(new WizardMessage
        {
            Role = "assistant",
            Content = fullAssistantText.ToString(),
            Timestamp = DateTime.UtcNow.ToString("o"),
            FlowConfigSnapshot = extractedFlowConfig
        });

        var historyJson = System.Text.Json.JsonSerializer.Serialize(history);
        var patchBody = System.Text.Json.JsonSerializer.Serialize(new { wizard_history = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(historyJson) });
        await fbClient.ProxyPatchAsync($"/api/v1/flows/{tenantId}/{flowId}/wizard-history", patchBody, authHeader, requestId);
    }
    catch (OperationCanceledException)
    {
        jsonLog.StepInfo("Wizard SSE client disconnected (normal)", requestId);
    }
    catch (System.Text.Json.JsonException ex)
    {
        jsonLog.StepError($"[{ErrorCodes.BackendWizardInvalidPayload}] Wizard message JSON error: {ex.Message}", requestId);
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(ErrorResponse.Create(ErrorCodes.BackendWizardInvalidPayload, "Gecersiz istek formati", requestId));
        }
    }
    catch (HttpRequestException ex)
    {
        jsonLog.StepError($"[{ErrorCodes.BackendWizardAiCommFailed}] Wizard message proxy failed: {ex.Message}", requestId);
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = 502;
            await ctx.Response.WriteAsJsonAsync(ErrorResponse.Create(ErrorCodes.BackendWizardAiCommFailed, "AI iletisim hatasi", requestId));
        }
    }
});

// GET /api/v1/flow-builder/wizard/{flowId} — Load wizard state (history + current flow)
app.MapGet("/api/v1/flow-builder/wizard/{flowId:int}", async (int flowId, HttpContext ctx,
    FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", "-"), statusCode: 401);

    return await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantContext.TenantId}/{flowId}");
});

// POST /api/v1/flow-builder/wizard/{flowId}/confirm — Finalize wizard: update flow_config + wizard_status
app.MapPost("/api/v1/flow-builder/wizard/{flowId:int}/confirm", async (int flowId, HttpContext ctx,
    FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", requestId), statusCode: 401);

    var tenantId = tenantContext.TenantId;

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        var flowName = root.TryGetProperty("flow_name", out var fnProp) ? fnProp.GetString() : null;
        var flowConfig = root.TryGetProperty("flow_config", out var fcProp) ? fcProp.GetRawText() : null;

        if (string.IsNullOrEmpty(flowConfig))
            return Results.Json(ErrorResponse.Create(ErrorCodes.BackendWizardInvalidPayload, "flow_config is required", requestId), statusCode: 400);

        var updateBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            flow_name = flowName,
            flow_config = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(flowConfig),
            wizard_status = "completed"
        });

        var (statusCode, respBody) = await fbClient.ProxyPutAsync($"/api/v1/flows/{tenantId}/{flowId}", updateBody, authHeader, requestId);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        if (respBody != null) await ctx.Response.WriteAsync(respBody);
        return Results.Empty;
    }
    catch (System.Text.Json.JsonException ex)
    {
        jsonLog.StepError($"[{ErrorCodes.BackendWizardInvalidPayload}] Wizard confirm JSON error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendWizardInvalidPayload, "Gecersiz istek formati", requestId), statusCode: 400);
    }
    catch (HttpRequestException ex)
    {
        jsonLog.StepError($"[{ErrorCodes.BackendWizardConfirmFailed}] Wizard confirm proxy failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendWizardConfirmFailed, "Akis olusturulamadi", requestId), statusCode: 502);
    }
});

// Helper: parse flow list response into FlowSummaryContext (excluding current draft)
static List<FlowSummaryContext> ParseFlowSummaries(string json, int excludeFlowId, ILogger? logger = null)
{
    var result = new List<FlowSummaryContext>();
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        var flows = root.ValueKind == System.Text.Json.JsonValueKind.Array ? root : root;
        foreach (var flow in flows.EnumerateArray())
        {
            if (!flow.TryGetProperty("flow_id", out var fidProp)) continue;
            var fid = fidProp.GetInt32();
            if (fid == excludeFlowId) continue;

            var ctx = new FlowSummaryContext
            {
                FlowId = fid,
                FlowName = flow.TryGetProperty("flow_name", out var fnProp) ? fnProp.GetString() ?? "" : "",
                IsActive = flow.TryGetProperty("is_active", out var ia) && ia.GetBoolean(),
                NodeCount = flow.TryGetProperty("node_count", out var nc) ? nc.GetInt32() : 0
            };

            // Extract node types from flow_config_raw if available
            if (flow.TryGetProperty("flow_config_raw", out var rawProp) && rawProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var rawJson = rawProp.GetString();
                if (rawJson != null)
                {
                    try
                    {
                        using var cfgDoc = System.Text.Json.JsonDocument.Parse(rawJson);
                        if (cfgDoc.RootElement.TryGetProperty("nodes", out var nodesProp))
                        {
                            var types = new HashSet<string>();
                            foreach (var node in nodesProp.EnumerateArray())
                            {
                                if (node.TryGetProperty("type", out var typeProp))
                                    types.Add(typeProp.GetString() ?? "");
                            }
                            ctx.NodeTypes = types.ToList();
                        }
                    }
                    catch (System.Text.Json.JsonException ex) { logger?.LogDebug("ParseFlowSummaries: malformed flow_config for flow {FlowId}: {Error}", fid, ex.Message); }
                }
            }

            result.Add(ctx);
        }
    }
    catch (System.Text.Json.JsonException ex) { logger?.LogDebug("ParseFlowSummaries: malformed flow list JSON: {Error}", ex.Message); }
    return result;
}

// ============================================
// FLOW BUILDER AUTH (API Key -> JWT)
// ============================================

app.MapPost("/api/v1/flow-builder/auth/login", async (HttpContext ctx, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var tenantId = root.TryGetProperty("tenant_id", out var tid) ? tid.GetInt32() : 0;
        var apiKey = root.TryGetProperty("api_key", out var ak) ? ak.GetString() : null;

        if (tenantId <= 0 || string.IsNullOrEmpty(apiKey))
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralValidation, "tenant_id (int) and api_key (string) are required", requestId),
                statusCode: 400);
        }

        // Validate API key from tenant_registry.settings_json
        if (pgFactory == null)
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Database not configured", requestId),
                statusCode: 500);
        }

        await using var conn = await pgFactory.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT settings_json FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";
        cmd.Parameters.AddWithValue("tid", tenantId);
        var settingsResult = await cmd.ExecuteScalarAsync();

        if (settingsResult == null || settingsResult is DBNull)
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AutomationInvalidApiKey, "Tenant bulunamadi veya aktif degil", requestId),
                statusCode: 401);
        }

        var settingsJson = settingsResult.ToString();
        string? storedApiKey = null;
        if (!string.IsNullOrEmpty(settingsJson))
        {
            using var settingsDoc = System.Text.Json.JsonDocument.Parse(settingsJson);
            if (settingsDoc.RootElement.TryGetProperty("flow_builder_api_key", out var keyProp))
                storedApiKey = keyProp.GetString();
        }

        if (string.IsNullOrEmpty(storedApiKey) || storedApiKey != apiKey)
        {
            jsonLogger.StepWarn($"FlowBuilder login failed: invalid API key for tenant {tenantId}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AutomationInvalidApiKey, "Gecersiz API anahtari", requestId),
                statusCode: 401);
        }

        // Generate JWT token via shared JwtGenerator
        if (jwtGenerator == null)
        {
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId),
                statusCode: 500);
        }

        var tokenExpiry = TimeSpan.FromHours(8);
        var tokenString = jwtGenerator.GenerateToken(tenantId, "flow_builder", "flow_builder_api_key", tokenExpiry);

        jsonLogger.StepInfo($"FlowBuilder login success: tenant={tenantId}", requestId);
        return Results.Ok(new
        {
            token = tokenString,
            tenant_id = tenantId,
            expires_in = (int)tokenExpiry.TotalSeconds,
            token_type = "Bearer"
        });
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId),
            statusCode: 400);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"FlowBuilder login error: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Login failed", requestId),
            statusCode: 500);
    }
});

// ============================================
// KNOWLEDGE PROXY ENDPOINTS (Phase B)
// ============================================

// Knowledge proxy helpers (Basic Auth -> JWT bridge)
async Task<IResult> KnProxyGet(HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, string targetPath)
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";
    var queryString = ctx.Request.QueryString.Value ?? "";
    var (statusCode, body) = await knClient.ProxyGetAsync(targetPath + queryString, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> KnProxyPost(HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, string targetPath)
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";
    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();
    var (statusCode, body) = await knClient.ProxyPostAsync(targetPath, requestBody, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> KnProxyPut(HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, string targetPath)
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";
    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();
    var (statusCode, body) = await knClient.ProxyPutAsync(targetPath, requestBody, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> KnProxyDelete(HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, string targetPath)
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";
    var (statusCode, body) = await knClient.ProxyDeleteAsync(targetPath, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

// Upload proxy (multipart form-data)
app.MapPost("/api/ops/knowledge/{tenantId:int}/documents/upload", async (
    HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    if (!ctx.Request.HasFormContentType)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidFileType, "Multipart form data required", requestId), statusCode: 400);

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "file is required", requestId), statusCode: 400);

    // GR-2.6.5: Block image uploads for health tenants (KVKK photo policy)
    if (file.ContentType != null && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
    {
        if (pgFactory != null)
        {
            await using var hConn = await pgFactory.OpenConnectionAsync();
            await using var hCmd = hConn.CreateCommand();
            hCmd.CommandText = "SELECT settings_json::text, sector FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";
            hCmd.Parameters.AddWithValue("tid", tenantId);
            await using var hReader = await hCmd.ExecuteReaderAsync();
            if (await hReader.ReadAsync())
            {
                var hSettings = hReader.IsDBNull(0) ? null : hReader.GetString(0);
                var hSector = hReader.IsDBNull(1) ? null : hReader.GetString(1);
                if (KvkkHelper.IsHealthTenant(hSettings, hSector))
                {
                    jsonLog.StepWarn($"[KVKK] Health tenant {tenantId}: image upload blocked (INV-KN-016)", requestId);
                    return Results.Json(ErrorResponse.Create(
                        ErrorCodes.KnowledgePhotoBlockedHealthTenant,
                        "Saglik sektorundeki isletmeler icin gorsel yukleme KVKK kapsaminda engellenmistir",
                        requestId), statusCode: 403);
                }
            }
        }
    }

    var title = form["title"].FirstOrDefault();
    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";

    var stream = file.OpenReadStream();
    var (statusCode, body) = await knClient.ProxyUploadAsync(
        $"/api/v1/knowledge/{tenantId}/documents/upload",
        stream, file.FileName, title, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}).DisableAntiforgery();

// Document CRUD
app.MapGet("/api/ops/knowledge/{tenantId:int}/documents", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyGet(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/documents"));

app.MapDelete("/api/ops/knowledge/{tenantId:int}/documents/{docId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, int docId) =>
    await KnProxyDelete(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/documents/{docId}"));

// FAQ CRUD
app.MapGet("/api/ops/knowledge/{tenantId:int}/faqs", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyGet(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/faqs"));

app.MapPost("/api/ops/knowledge/{tenantId:int}/faqs", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/faqs"));

app.MapPut("/api/ops/knowledge/{tenantId:int}/faqs/{faqId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, int faqId) =>
    await KnProxyPut(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/faqs/{faqId}"));

app.MapDelete("/api/ops/knowledge/{tenantId:int}/faqs/{faqId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, int faqId) =>
    await KnProxyDelete(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/faqs/{faqId}"));

// Search + Embeddings
app.MapPost("/api/ops/knowledge/{tenantId:int}/search", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/search"));

app.MapPost("/api/ops/knowledge/{tenantId:int}/generate-embeddings", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/generate-embeddings"));

// ============================================
// APPOINTMENTS PROXY ENDPOINTS (GR-2.4)
// ============================================

// Appointments proxy helpers
async Task<IResult> AppointmentsProxyPost(HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var (statusCode, body) = await apClient.ProxyPostAsync(targetPath, requestBody, authHeader, requestId);
    sw.Stop();

    jsonLog.StepInfo($"Appointments proxy POST {targetPath}: status={statusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> AppointmentsProxyGet(HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    // Forward query string to downstream service
    var queryString = ctx.Request.QueryString.Value ?? "";

    var (statusCode, body) = await apClient.ProxyGetAsync(targetPath + queryString, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> AppointmentsProxyPut(HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var (statusCode, body) = await apClient.ProxyPutAsync(targetPath, requestBody, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> AppointmentsProxyDelete(HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var (statusCode, body) = await apClient.ProxyDeleteAsync(targetPath, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

// Slots
app.MapGet("/api/v1/appointments/slots", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/slots"));

app.MapPost("/api/v1/appointments/slots", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, "/api/v1/slots"));

app.MapPut("/api/v1/appointments/slots/{id:int}", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyPut(ctx, apClient, jsonLog, $"/api/v1/slots/{id}"));

app.MapDelete("/api/v1/appointments/slots/{id:int}", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyDelete(ctx, apClient, jsonLog, $"/api/v1/slots/{id}"));

// Appointments
app.MapPost("/api/v1/appointments/book", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, "/api/v1/appointments/book"));

app.MapGet("/api/v1/appointments/list", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/appointments"));

app.MapGet("/api/v1/appointments/{id:long}", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, long id) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, $"/api/v1/appointments/{id}"));

app.MapPost("/api/v1/appointments/{id:long}/cancel", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, long id) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, $"/api/v1/appointments/{id}/cancel"));

// Available slots (GR-3.19: now supports ?doctor_id= filter)
app.MapGet("/api/v1/appointments/available-slots", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/appointments/available-slots"));

// GR-3.19: No-show stats proxy
app.MapGet("/api/v1/appointments/no-show-stats", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/appointments/no-show-stats"));

// GR-3.19: Waitlist proxy
app.MapGet("/api/v1/appointments/waitlist", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/waitlist"));

app.MapPost("/api/v1/appointments/waitlist", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, "/api/v1/waitlist"));

app.MapPut("/api/v1/appointments/waitlist/{id:int}/status", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyPut(ctx, apClient, jsonLog, $"/api/v1/waitlist/{id}/status"));

// GR-3.19: Pricing proxy
app.MapGet("/api/v1/appointments/pricing", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/pricing"));

app.MapPost("/api/v1/appointments/pricing", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, "/api/v1/pricing"));

app.MapPut("/api/v1/appointments/pricing/{id:int}", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyPut(ctx, apClient, jsonLog, $"/api/v1/pricing/{id}"));

// GR-3.19: Calendar sync status proxy
app.MapGet("/api/v1/appointments/calendar/status", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/calendar/status"));

// GR-3.20/3.41/3.43: Treatment Lifecycle proxy endpoints
app.MapPost("/api/v1/appointments/lifecycle/start", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, "/api/v1/lifecycle/start"));

app.MapGet("/api/v1/appointments/lifecycle", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, "/api/v1/lifecycle"));

app.MapGet("/api/v1/appointments/lifecycle/{id:int}", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyGet(ctx, apClient, jsonLog, $"/api/v1/lifecycle/{id}"));

app.MapPost("/api/v1/appointments/lifecycle/{id:int}/cancel", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, $"/api/v1/lifecycle/{id}/cancel"));

app.MapPost("/api/v1/appointments/lifecycle/{id:int}/response", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog, int id) =>
    await AppointmentsProxyPost(ctx, apClient, jsonLog, $"/api/v1/lifecycle/{id}/response"));

// ============================================
// MARKETING PROXY ENDPOINTS (GR-3.21/3.22)
// ============================================

// Marketing proxy helpers
async Task<IResult> MarketingProxyPost(HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var (statusCode, body) = await mkClient.ProxyPostAsync(targetPath, requestBody, authHeader, requestId);
    sw.Stop();

    jsonLog.StepInfo($"Marketing proxy POST {targetPath}: status={statusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> MarketingProxyGet(HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var queryString = ctx.Request.QueryString.Value ?? "";

    var (statusCode, body) = await mkClient.ProxyGetAsync(targetPath + queryString, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> MarketingProxyPut(HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    var (statusCode, body) = await mkClient.ProxyPutAsync(targetPath, requestBody, authHeader, requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

async Task<IResult> MarketingProxyDelete(HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    var (statusCode, body) = await mkClient.ProxyDeleteAsync(targetPath, authHeader, requestId);

    jsonLog.StepInfo($"Marketing proxy DELETE {targetPath}: status={statusCode}", requestId);

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}

// Reviews (GR-3.21)
app.MapPost("/api/v1/reviews/request", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/reviews/request"));

app.MapGet("/api/v1/reviews", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/reviews"));

app.MapPost("/api/v1/reviews/{id:int}/sent", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, $"/api/v1/reviews/{id}/sent"));

app.MapPost("/api/v1/reviews/{id:int}/posted", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, $"/api/v1/reviews/{id}/posted"));

app.MapGet("/api/v1/reviews/stats", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/reviews/stats"));

// Referrals (GR-3.21)
app.MapPost("/api/v1/referrals", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/referrals"));

app.MapGet("/api/v1/referrals", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/referrals"));

app.MapGet("/api/v1/referrals/lookup/{code}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, string code) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, $"/api/v1/referrals/lookup/{code}"));

app.MapPut("/api/v1/referrals/{id:int}/redeem", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPut(ctx, mkClient, jsonLog, $"/api/v1/referrals/{id}/redeem"));

// Tourism Leads (GR-3.22)
app.MapPost("/api/v1/tourism/leads", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/tourism/leads"));

app.MapGet("/api/v1/tourism/leads", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/tourism/leads"));

app.MapGet("/api/v1/tourism/leads/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, $"/api/v1/tourism/leads/{id}"));

app.MapPut("/api/v1/tourism/leads/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPut(ctx, mkClient, jsonLog, $"/api/v1/tourism/leads/{id}"));

app.MapGet("/api/v1/tourism/stats", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/tourism/stats"));

// Review Rescue (GR-3.24)
app.MapPost("/api/v1/rescue/risks", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/rescue/risks"));

app.MapGet("/api/v1/rescue/risks", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/rescue/risks"));

app.MapPut("/api/v1/rescue/risks/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPut(ctx, mkClient, jsonLog, $"/api/v1/rescue/risks/{id}"));

app.MapGet("/api/v1/rescue/stats", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/rescue/stats"));

app.MapPost("/api/v1/rescue/templates", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/rescue/templates"));

app.MapGet("/api/v1/rescue/templates", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/rescue/templates"));

app.MapPut("/api/v1/rescue/templates/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPut(ctx, mkClient, jsonLog, $"/api/v1/rescue/templates/{id}"));

app.MapDelete("/api/v1/rescue/templates/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyDelete(ctx, mkClient, jsonLog, $"/api/v1/rescue/templates/{id}"));

// Tourism Catalog + Conversations (GR-3.25)
app.MapPost("/api/v1/tourism/catalog", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/tourism/catalog"));

app.MapGet("/api/v1/tourism/catalog", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/tourism/catalog"));

app.MapPut("/api/v1/tourism/catalog/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyPut(ctx, mkClient, jsonLog, $"/api/v1/tourism/catalog/{id}"));

app.MapDelete("/api/v1/tourism/catalog/{id:int}", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog, int id) =>
    await MarketingProxyDelete(ctx, mkClient, jsonLog, $"/api/v1/tourism/catalog/{id}"));

app.MapPost("/api/v1/tourism/conversations", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/tourism/conversations"));

app.MapGet("/api/v1/tourism/conversations", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/tourism/conversations"));

app.MapPost("/api/v1/tourism/respond", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyPost(ctx, mkClient, jsonLog, "/api/v1/tourism/respond"));

app.MapGet("/api/v1/tourism/conversations/stats", async (HttpContext ctx, MarketingClient mkClient, JsonLinesLogger jsonLog) =>
    await MarketingProxyGet(ctx, mkClient, jsonLog, "/api/v1/tourism/conversations/stats"));

// ============================================
// GR-3.14: ATTRIBUTION ENDPOINTS (/api/v1/attribution/*)
// JWT auth enforced by middleware for /api/v1/attribution/ prefix
// ============================================

// List leads with optional date range
app.MapGet("/api/v1/attribution/leads", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog, string? from, string? to) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    DateOnly? toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? null : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? null : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid date format (expected yyyy-MM-dd)", rid), statusCode: 400);
    }

    try
    {
        var leads = await attrRepo.GetLeadAttributionsAsync(tenantContext.TenantId, fromDate, toDate);
        return Results.Ok(new { leads });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution leads query failed ({ErrorCodes.AttributionInvalidPayload}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionInvalidPayload, message = "Attribution sorgusu basarisiz." }, statusCode: 500);
    }
});

// Update lead conversion status
app.MapPut("/api/v1/attribution/leads/{id:int}/status", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog, int id) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    LeadStatusUpdateRequest? req;
    try { req = await ctx.Request.ReadFromJsonAsync<LeadStatusUpdateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"Malformed JSON in lead status update: {ex.Message}"); req = null; }

    if (req == null || string.IsNullOrEmpty(req.ConversionStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.AttributionInvalidPayload, "conversion_status required", rid), statusCode: 400);

    if (!AttributionService.IsValidConversionStatus(req.ConversionStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.AttributionInvalidLeadStatus,
            "Valid: new, contacted, qualified, converted, lost", rid), statusCode: 400);

    try
    {
        var updated = await attrRepo.UpdateLeadStatusAsync(tenantContext.TenantId, id, req);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AttributionNotFound, "Lead not found", rid), statusCode: 404);

        return Results.Ok(new { message = "Lead status updated", id, status = req.ConversionStatus });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution lead status update failed ({ErrorCodes.AttributionNotFound}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionNotFound, message = "Guncelleme basarisiz." }, statusCode: 500);
    }
});

// Attribution summary (by source + by campaign breakdowns)
app.MapGet("/api/v1/attribution/summary", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog, string? from, string? to) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-30) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid date format (expected yyyy-MM-dd)", rid), statusCode: 400);
    }

    try
    {
        var summary = await attrRepo.GetAttributionSummaryAsync(tenantContext.TenantId, fromDate, toDate);
        return Results.Ok(summary);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution summary failed for tenant ({ErrorCodes.AttributionInvalidPayload}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionInvalidPayload, message = "Attribution ozet sorgusu basarisiz." }, statusCode: 500);
    }
});

// Cost-per-lead by platform
app.MapGet("/api/v1/attribution/cost-per-lead", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog, string? from, string? to) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-30) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid date format (expected yyyy-MM-dd)", rid), statusCode: 400);
    }

    try
    {
        var result = await attrRepo.GetCostPerLeadAsync(tenantContext.TenantId, fromDate, toDate);
        return Results.Ok(new { cost_per_lead = result });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution cost-per-lead failed ({ErrorCodes.AttributionInvalidCostEntry}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionInvalidCostEntry, message = "Maliyet sorgusu basarisiz." }, statusCode: 500);
    }
});

// Ad costs CRUD
app.MapGet("/api/v1/attribution/costs", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    try
    {
        var costs = await attrRepo.GetAdCostsAsync(tenantContext.TenantId);
        return Results.Ok(new { costs });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution costs query failed: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionInvalidCostEntry, message = "Maliyet listesi basarisiz." }, statusCode: 500);
    }
});

app.MapPost("/api/v1/attribution/costs", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    AdCostCreateRequest? req;
    try { req = await ctx.Request.ReadFromJsonAsync<AdCostCreateRequest>(); }
    catch (System.Text.Json.JsonException ex) { jsonLog.SystemWarn($"Malformed JSON in ad cost create: {ex.Message}"); req = null; }

    if (req == null || string.IsNullOrEmpty(req.Platform) || req.CostAmount <= 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AttributionInvalidCostEntry, "platform and cost_amount > 0 required", rid), statusCode: 400);

    if (!AttributionService.IsValidPlatform(req.Platform))
        return Results.Json(ErrorResponse.Create(ErrorCodes.AttributionInvalidCostEntry,
            "Valid platforms: meta, google, tiktok, linkedin, other", rid), statusCode: 400);

    try
    {
        var id = await attrRepo.InsertAdCostAsync(tenantContext.TenantId, req);
        return Results.Json(new { message = "Ad cost created", id }, statusCode: 201);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution cost insert failed: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionInvalidCostEntry, message = "Maliyet kaydi basarisiz." }, statusCode: 500);
    }
});

app.MapDelete("/api/v1/attribution/costs/{id:int}", async (HttpContext ctx, AttributionRepository attrRepo, JsonLinesLogger jsonLog, int id) =>
{
    var rid = ctx.Request.Headers[HeaderNames.RequestId].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", rid), statusCode: 401);

    try
    {
        var deleted = await attrRepo.DeleteAdCostAsync(tenantContext.TenantId, id);
        if (!deleted)
            return Results.Json(ErrorResponse.Create(ErrorCodes.AttributionCostNotFound, "Ad cost not found", rid), statusCode: 404);

        return Results.Ok(new { message = "Ad cost deleted", id });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Attribution cost delete failed: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.AttributionCostNotFound, message = "Maliyet silme basarisiz." }, statusCode: 500);
    }
});

// ============================================
// PKT-6B1: LEAD MANAGEMENT ENDPOINTS (/api/v1/leads/*)
// GR-3.13: Lead Management v2 - Backend API only (no UI)
// ============================================

app.MapPost("/api/v1/leads", async (HttpContext ctx, LeadRepository leadRepo, JsonLinesLogger jsonLog, LeadRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null || string.IsNullOrWhiteSpace(request.Phone))
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPayload, "phone is required", rid), statusCode: 400);

    try
    {
        var leadId = await leadRepo.UpsertLeadAsync(tenantContext.TenantId, request, ctx.RequestAborted);

        // Insert creation activity
        await leadRepo.InsertActivityAsync(tenantContext.TenantId, leadId, "created", null, "new", null, ctx.RequestAborted);

        jsonLog.StepInfo($"Lead upserted: id={leadId}, phone={request.Phone}, source={request.Source}", rid);
        return Results.Json(new { id = leadId, phone = request.Phone, pipeline_status = "new" }, statusCode: 201);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.LeadInvalidPayload}] Lead upsert DB error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPayload, "Lead kaydi olusturulamadi", rid), statusCode: 500);
    }
});

app.MapGet("/api/v1/leads", async (
    HttpContext ctx, LeadRepository leadRepo,
    string? status, bool? is_hot, string? search, int? limit, int? offset) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (status != null && !LeadPipelineStatuses.IsValid(status))
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPipelineStatus,
            $"Invalid status. Valid: {string.Join(", ", LeadPipelineStatuses.All)}", rid), statusCode: 400);

    try
    {
        var leads = await leadRepo.ListLeadsAsync(
            tenantContext.TenantId, status, is_hot, search, limit ?? 50, offset ?? 0, ctx.RequestAborted);
        return Results.Ok(new { leads, count = leads.Count });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        var jsonLog = ctx.RequestServices.GetRequiredService<JsonLinesLogger>();
        jsonLog.SystemWarn($"[{ErrorCodes.LeadInvalidPayload}] Lead list query error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPayload, "Lead listesi alinamadi", rid), statusCode: 500);
    }
});

app.MapGet("/api/v1/leads/{id:int}", async (HttpContext ctx, LeadRepository leadRepo, int id) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var lead = await leadRepo.GetLeadAsync(tenantContext.TenantId, id, ctx.RequestAborted);
        if (lead == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadNotFound, $"Lead {id} not found", rid), statusCode: 404);

        return Results.Ok(lead);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        var jsonLog = ctx.RequestServices.GetRequiredService<JsonLinesLogger>();
        jsonLog.SystemWarn($"[{ErrorCodes.LeadNotFound}] Lead get DB error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadNotFound, "Lead sorgusu basarisiz", rid), statusCode: 500);
    }
});

app.MapPut("/api/v1/leads/{id:int}/status", async (
    HttpContext ctx, LeadRepository leadRepo, JsonLinesLogger jsonLog,
    int id, LeadPipelineUpdateRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null || string.IsNullOrWhiteSpace(request.PipelineStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPipelineStatus, "pipeline_status is required", rid), statusCode: 400);

    if (!LeadPipelineStatuses.IsValid(request.PipelineStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPipelineStatus,
            $"Invalid status. Valid: {string.Join(", ", LeadPipelineStatuses.All)}", rid), statusCode: 400);

    try
    {
        var updated = await leadRepo.UpdatePipelineStatusAsync(
            tenantContext.TenantId, id, request.PipelineStatus, request.AssignedTo, ctx.RequestAborted);

        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadNotFound, $"Lead {id} not found", rid), statusCode: 404);

        jsonLog.StepInfo($"Lead status updated: id={id}, status={request.PipelineStatus}", rid);
        return Results.Ok(new { id, pipeline_status = request.PipelineStatus });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.LeadInvalidPipelineStatus}] Lead status update error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidPipelineStatus, "Status guncellenemedi", rid), statusCode: 500);
    }
});

app.MapPut("/api/v1/leads/{id:int}/score", async (
    HttpContext ctx, LeadRepository leadRepo, JsonLinesLogger jsonLog, int id) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        if (!root.TryGetProperty("score", out var scoreEl) || scoreEl.ValueKind != System.Text.Json.JsonValueKind.Number)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadScoringFailed, "score (0-100) is required", rid), statusCode: 400);

        var score = scoreEl.GetInt32();
        if (score < 0 || score > 100)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadScoringFailed, "score must be 0-100", rid), statusCode: 400);

        var updated = await leadRepo.UpdateScoreAsync(tenantContext.TenantId, id, score, ctx.RequestAborted);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadNotFound, $"Lead {id} not found", rid), statusCode: 404);

        jsonLog.StepInfo($"Lead score updated: id={id}, score={score}", rid);
        return Results.Ok(new { id, score, is_hot = score >= 80 });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.LeadScoringFailed}] Lead score update DB error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadScoringFailed, "Score guncellenemedi", rid), statusCode: 500);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadScoringFailed, "Invalid JSON body", rid), statusCode: 400);
    }
});

app.MapPost("/api/v1/leads/{id:int}/activities", async (
    HttpContext ctx, LeadRepository leadRepo, JsonLinesLogger jsonLog, int id) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var activityType = root.TryGetProperty("activity_type", out var at) ? at.GetString() : null;
        if (string.IsNullOrWhiteSpace(activityType))
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidActivityPayload, "activity_type is required", rid), statusCode: 400);

        var note = root.TryGetProperty("note", out var n) ? n.GetString() : null;
        var oldValue = root.TryGetProperty("old_value", out var ov) ? ov.GetString() : null;
        var newValue = root.TryGetProperty("new_value", out var nv) ? nv.GetString() : null;

        var activityId = await leadRepo.InsertActivityAsync(
            tenantContext.TenantId, id, activityType, oldValue, newValue, note, ctx.RequestAborted);

        jsonLog.StepInfo($"Lead activity added: lead={id}, type={activityType}", rid);
        return Results.Json(new { id = activityId, lead_id = id, activity_type = activityType }, statusCode: 201);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.LeadInvalidActivityPayload}] Lead activity insert error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidActivityPayload, "Aktivite kaydedilemedi", rid), statusCode: 500);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidActivityPayload, "Invalid JSON body", rid), statusCode: 400);
    }
});

app.MapGet("/api/v1/leads/{id:int}/activities", async (
    HttpContext ctx, LeadRepository leadRepo, int id, int? limit) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var activities = await leadRepo.GetActivitiesAsync(tenantContext.TenantId, id, limit ?? 50, ctx.RequestAborted);
        return Results.Ok(new { activities });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        var jsonLog = ctx.RequestServices.GetRequiredService<JsonLinesLogger>();
        jsonLog.SystemWarn($"[{ErrorCodes.LeadInvalidActivityPayload}] Lead activities query error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadInvalidActivityPayload, "Aktiviteler alinamadi", rid), statusCode: 500);
    }
});

app.MapGet("/api/v1/leads/funnel", async (HttpContext ctx, LeadRepository leadRepo, JsonLinesLogger jsonLog) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var stats = await leadRepo.GetFunnelStatsAsync(tenantContext.TenantId, ctx.RequestAborted);
        return Results.Ok(stats);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.LeadFunnelQueryFailed}] Funnel stats error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadFunnelQueryFailed, "Funnel sorgusu basarisiz", rid), statusCode: 500);
    }
});

app.MapGet("/api/v1/leads/hot", async (HttpContext ctx, LeadRepository leadRepo, int? limit) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var leads = await leadRepo.GetHotLeadsAsync(tenantContext.TenantId, limit ?? 20, ctx.RequestAborted);
        return Results.Ok(new { leads, count = leads.Count });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        var jsonLog = ctx.RequestServices.GetRequiredService<JsonLinesLogger>();
        jsonLog.SystemWarn($"[{ErrorCodes.LeadHotAlertFailed}] Hot leads query error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadHotAlertFailed, "Hot lead listesi alinamadi", rid), statusCode: 500);
    }
});

app.MapPost("/api/v1/leads/{id:int}/followup", async (
    HttpContext ctx, LeadRepository leadRepo, JsonLinesLogger jsonLog, int id) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var followUpStr = root.TryGetProperty("follow_up_at", out var fu) ? fu.GetString() : null;
        if (string.IsNullOrWhiteSpace(followUpStr) || !DateTime.TryParse(followUpStr, out var followUpAt))
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadFollowUpScheduleFailed, "follow_up_at (ISO date) is required", rid), statusCode: 400);

        var scheduled = await leadRepo.ScheduleFollowUpAsync(
            tenantContext.TenantId, id, followUpAt, ctx.RequestAborted);

        if (!scheduled)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LeadNotFound, $"Lead {id} not found or in terminal status", rid), statusCode: 404);

        await leadRepo.InsertActivityAsync(
            tenantContext.TenantId, id, "followup_scheduled", null, followUpAt.ToString("o"), null, ctx.RequestAborted);

        jsonLog.StepInfo($"Lead follow-up scheduled: id={id}, at={followUpAt:o}", rid);
        return Results.Ok(new { id, next_followup_at = followUpAt });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.LeadFollowUpScheduleFailed}] Lead follow-up DB error: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadFollowUpScheduleFailed, "Follow-up zamanlanamadi", rid), statusCode: 500);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadFollowUpScheduleFailed, "Invalid JSON body", rid), statusCode: 400);
    }
});

// ============================================
// PKT-3: ANALYTICS DASHBOARD ENDPOINTS (/api/ops/analytics/*)
// ============================================

// Analytics: List tenants with metrics availability
app.MapGet("/api/ops/analytics/tenants", async (HttpContext ctx, AnalyticsRepository analyticsRepo) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    try
    {
        var tenants = await analyticsRepo.GetTenantsWithMetricsAsync();
        return Results.Ok(new { tenants });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics tenants query failed ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: Automation summary for tenant in date range
app.MapGet("/api/ops/analytics/automation/summary", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id, string? from, string? to) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });
    }

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-7) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid date format (expected yyyy-MM-dd)" });
    }

    if (fromDate > toDate)
    {
        return Results.BadRequest(new { error = ErrorCodes.MetricsInvalidDateRange, message = "Gecersiz tarih araligi (baslangic > bitis)." });
    }

    try
    {
        var summary = await analyticsRepo.GetAutomationSummaryAsync(tenant_id.Value, fromDate, toDate);
        return Results.Ok(summary);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics automation summary failed for tenant {tenant_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: Automation daily trends for charting
app.MapGet("/api/ops/analytics/automation/trends", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id, string? from, string? to) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });
    }

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-7) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid date format (expected yyyy-MM-dd)" });
    }

    if (fromDate > toDate)
    {
        return Results.BadRequest(new { error = ErrorCodes.MetricsInvalidDateRange, message = "Gecersiz tarih araligi (baslangic > bitis)." });
    }

    try
    {
        var trends = await analyticsRepo.GetAutomationTrendsAsync(tenant_id.Value, fromDate, toDate);
        return Results.Ok(new { tenant_id = tenant_id.Value, trends });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics automation trends failed for tenant {tenant_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: Intent performance breakdown
app.MapGet("/api/ops/analytics/automation/intents", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id, string? from, string? to) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });
    }

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-7) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid date format (expected yyyy-MM-dd)" });
    }

    if (fromDate > toDate)
    {
        return Results.BadRequest(new { error = ErrorCodes.MetricsInvalidDateRange, message = "Gecersiz tarih araligi (baslangic > bitis)." });
    }

    try
    {
        var intents = await analyticsRepo.GetIntentMetricsAsync(tenant_id.Value, fromDate, toDate);
        return Results.Ok(new { tenant_id = tenant_id.Value, intents });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics intents failed for tenant {tenant_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: List WA analyses for tenant
app.MapGet("/api/ops/analytics/wa/analyses", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });
    }

    try
    {
        var analyses = await analyticsRepo.GetWaAnalysesAsync(tenant_id.Value);
        return Results.Ok(new { analyses });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics WA analyses failed for tenant {tenant_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: WA analysis summary
app.MapGet("/api/ops/analytics/wa/summary", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id, int? analysis_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue || !analysis_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });
    }

    try
    {
        var summary = await analyticsRepo.GetWaSummaryAsync(tenant_id.Value, analysis_id.Value);
        return Results.Ok(summary);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics WA summary failed for tenant {tenant_id}, analysis {analysis_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: WA agent performance comparison
app.MapGet("/api/ops/analytics/wa/agents", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id, int? analysis_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue || !analysis_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });
    }

    try
    {
        var agents = await analyticsRepo.GetWaAgentMetricsAsync(tenant_id.Value, analysis_id.Value);
        return Results.Ok(new { agents });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics WA agents failed for tenant {tenant_id}, analysis {analysis_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// Analytics: WA daily conversation trends
app.MapGet("/api/ops/analytics/wa/trends", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id, int? analysis_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    if (!tenant_id.HasValue || !analysis_id.HasValue)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });
    }

    try
    {
        var trends = await analyticsRepo.GetWaTrendsAsync(tenant_id.Value, analysis_id.Value);
        return Results.Ok(new { trends });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Analytics WA trends failed for tenant {tenant_id}, analysis {analysis_id} ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Analitik sorgusu basarisiz oldu." }, statusCode: 500);
    }
});

// ============================================
// WA Analytics NLP Proxy (PKT-4: Dashboard -> Backend -> WA Analytics)
// ============================================

// WA NLP: Intent distribution
app.MapGet("/api/ops/analytics/wa/intents-nlp", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int? tenant_id, int? analysis_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue || !analysis_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });

    var (statusCode, body) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenant_id}/analyses/{analysis_id}/intents",
        ctx.Request.Headers.Authorization.FirstOrDefault(),
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault());

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// WA NLP: Sentiment summary
app.MapGet("/api/ops/analytics/wa/sentiments", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int? tenant_id, int? analysis_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue || !analysis_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });

    var (statusCode, body) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenant_id}/analyses/{analysis_id}/sentiments",
        ctx.Request.Headers.Authorization.FirstOrDefault(),
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault());

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// WA NLP: Top products
app.MapGet("/api/ops/analytics/wa/products", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int? tenant_id, int? analysis_id, int? limit) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue || !analysis_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });

    var lim = Math.Clamp(limit ?? 50, 1, 200);
    var (statusCode, body) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenant_id}/analyses/{analysis_id}/products?limit={lim}",
        ctx.Request.Headers.Authorization.FirstOrDefault(),
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault());

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// WA NLP: Top prices
app.MapGet("/api/ops/analytics/wa/prices", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int? tenant_id, int? analysis_id, int? limit) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue || !analysis_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });

    var lim = Math.Clamp(limit ?? 30, 1, 100);
    var (statusCode, body) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenant_id}/analyses/{analysis_id}/prices?limit={lim}",
        ctx.Request.Headers.Authorization.FirstOrDefault(),
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault());

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// WA NLP: FAQ clusters
app.MapGet("/api/ops/analytics/wa/faq-clusters", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int? tenant_id, int? analysis_id, int? limit) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue || !analysis_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });

    var lim = Math.Clamp(limit ?? 50, 1, 200);
    var (statusCode, body) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenant_id}/analyses/{analysis_id}/faq-clusters?limit={lim}",
        ctx.Request.Headers.Authorization.FirstOrDefault(),
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault());

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// WA NLP: Aggregate NLP summary
app.MapGet("/api/ops/analytics/wa/nlp-summary", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int? tenant_id, int? analysis_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue || !analysis_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id and analysis_id query parameters required" });

    var (statusCode, body) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenant_id}/analyses/{analysis_id}/nlp-summary",
        ctx.Request.Headers.Authorization.FirstOrDefault(),
        ctx.Request.Headers["X-Request-Id"].FirstOrDefault());

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// ============================================
// GR-3.18: ATTRIBUTION + CAMPAIGN ANALYTICS (ops-level, Basic auth)
// ============================================

app.MapGet("/api/ops/analytics/attribution/summary", async (HttpContext ctx, AttributionRepository attrRepo, int? tenant_id, string? from, string? to) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-30) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid date format (expected yyyy-MM-dd)" });
    }

    try
    {
        var summary = await attrRepo.GetAttributionSummaryAsync(tenant_id.Value, fromDate, toDate);
        return Results.Ok(summary);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Attribution summary failed ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Attribution sorgusu basarisiz." }, statusCode: 500);
    }
});

app.MapGet("/api/ops/analytics/attribution/cost-per-lead", async (HttpContext ctx, AttributionRepository attrRepo, int? tenant_id, string? from, string? to) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });

    DateOnly toDate, fromDate;
    try
    {
        toDate = string.IsNullOrEmpty(to) ? DateOnly.FromDateTime(DateTime.UtcNow) : DateOnly.Parse(to);
        fromDate = string.IsNullOrEmpty(from) ? toDate.AddDays(-30) : DateOnly.Parse(from);
    }
    catch (FormatException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid date format (expected yyyy-MM-dd)" });
    }

    try
    {
        var result = await attrRepo.GetCostPerLeadAsync(tenant_id.Value, fromDate, toDate);
        return Results.Ok(new { cost_per_lead = result });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Attribution cost-per-lead failed ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Maliyet sorgusu basarisiz." }, statusCode: 500);
    }
});

app.MapGet("/api/ops/analytics/campaigns", async (HttpContext ctx, AnalyticsRepository analyticsRepo, int? tenant_id) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }
    if (!tenant_id.HasValue)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "tenant_id query parameter required" });

    try
    {
        var campaigns = await analyticsRepo.GetCampaignStatsAsync(tenant_id.Value);
        return Results.Ok(new { campaigns });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        logger.SystemWarn($"Campaign stats failed ({ErrorCodes.MetricsQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.MetricsQueryFailed, message = "Kampanya sorgusu basarisiz." }, statusCode: 500);
    }
});

// ============================================
// SUPERADMIN: MESSAGE LOG
// ============================================

app.MapGet("/api/ops/messages", async (HttpContext ctx, JsonLinesLogger jsonLog,
    int? tenant_id, string? phone, string? direction,
    string? from, string? to, int? limit, int? offset) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var msgLogRepo = ctx.RequestServices.GetService<MessageLogRepository>();
    if (msgLogRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendMessageLogQueryFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    DateTime? fromDt = null;
    DateTime? toDt = null;

    if (!string.IsNullOrEmpty(from))
    {
        if (!DateTime.TryParse(from, out var parsedFrom))
            return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid 'from' date format" });
        fromDt = DateTime.SpecifyKind(parsedFrom, DateTimeKind.Utc);
    }
    if (!string.IsNullOrEmpty(to))
    {
        if (!DateTime.TryParse(to, out var parsedTo))
            return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid 'to' date format" });
        toDt = DateTime.SpecifyKind(parsedTo, DateTimeKind.Utc);
    }

    if (!string.IsNullOrEmpty(direction) && direction != "in" && direction != "out")
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "direction must be 'in' or 'out'" });

    try
    {
        var (messages, total) = await msgLogRepo.GetMessagesAsync(
            tenant_id, phone, direction, fromDt, toDt,
            limit ?? 50, offset ?? 0);

        return Results.Ok(new { messages, total });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Message log query failed ({ErrorCodes.BackendMessageLogQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendMessageLogQueryFailed, message = "Mesaj kayitlari yuklenemedi." }, statusCode: 500);
    }
});

// ============================================
// SUPERADMIN: MESSAGE STORY
// ============================================

app.MapGet("/api/ops/messages/{id}/story", async (HttpContext ctx, long id, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var msgLogRepo = ctx.RequestServices.GetService<MessageLogRepository>();
    if (msgLogRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendMessageLogQueryFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        var story = await msgLogRepo.GetMessageStoryAsync(id);
        if (story == null)
            return Results.NotFound(new { error = "NOT_FOUND", message = $"Message {id} not found" });

        // Build timeline
        var timeline = new List<object>();

        // 1. Incoming message
        timeline.Add(new
        {
            time = story.CreatedAt.ToString("HH:mm:ss"),
            icon = "incoming",
            title = "Müşteri Mesajı",
            detail = $"{story.Phone}: '{Truncate(story.MessageText, 120)}'"
        });

        // 2. Flow triggered (if active flow exists)
        if (story.FlowId.HasValue)
        {
            timeline.Add(new
            {
                time = story.CreatedAt.ToString("HH:mm:ss"),
                icon = "flow",
                title = "Flow Tetiklendi",
                detail = $"{story.FlowName ?? "Adsız Flow"} (flow #{story.FlowId})"
            });
        }

        // 3. Auto-reply entries (intent + FAQ)
        foreach (var ar in story.AutoReplies)
        {
            timeline.Add(new
            {
                time = ar.CreatedAt.ToString("HH:mm:ss"),
                icon = "ai",
                title = "AI İşleme",
                detail = $"Intent: {ar.Intent} (confidence: {ar.Confidence:F2}), Tip: {ar.ReplyType ?? "auto"}"
            });
            timeline.Add(new
            {
                time = ar.CreatedAt.ToString("HH:mm:ss"),
                icon = "reply",
                title = "Otomatik Yanıt",
                detail = Truncate(ar.ReplyText, 200)
            });
        }

        // 4. Outgoing messages (WapCRM callback results)
        foreach (var om in story.OutgoingMessages)
        {
            timeline.Add(new
            {
                time = om.CreatedAt.ToString("HH:mm:ss"),
                icon = "callback",
                title = om.SenderName == "bot" ? "WapCRM Callback" : "Giden Mesaj",
                detail = Truncate(om.MessageText, 200)
            });
        }

        // Summary
        var firstReply = story.AutoReplies.FirstOrDefault();
        var summary = new
        {
            flow_name = story.FlowName,
            flow_id = story.FlowId,
            intent = firstReply?.Intent,
            confidence = firstReply?.Confidence,
            reply_type = firstReply?.ReplyType,
            processing_time_ms = firstReply?.ProcessingTimeMs,
            auto_reply_count = story.AutoReplies.Count,
            outgoing_count = story.OutgoingMessages.Count
        };

        return Results.Ok(new { timeline, summary });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Message story query failed: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendMessageLogQueryFailed, message = "Mesaj hikayesi yuklenemedi." }, statusCode: 500);
    }
});

// ============================================
// SUPERADMIN: TENANT LIST + IMPERSONATE
// ============================================

app.MapGet("/api/ops/tenants", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (tenantRepo == null)
        return Results.Json(
            new { error = ErrorCodes.BackendTenantListQueryFailed, message = "PostgreSQL not configured" },
            statusCode: 503);

    try
    {
        var tenants = await tenantRepo.ListTenantsAsync();
        return Results.Ok(new { tenants });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tenant list query failed ({ErrorCodes.BackendTenantListQueryFailed}): {ex.Message}");
        return Results.Json(
            new { error = ErrorCodes.BackendTenantListQueryFailed, message = "Firma listesi yuklenemedi." },
            statusCode: 500);
    }
});

app.MapPost("/api/ops/tenants/{id}/impersonate", async (HttpContext ctx, int id, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (jwtGenerator == null)
        return Results.Json(
            new { error = ErrorCodes.BackendTenantImpersonateFailed, message = "JWT not configured" },
            statusCode: 503);

    if (id <= 0)
        return Results.BadRequest(
            new { error = ErrorCodes.GeneralValidation, message = "Gecersiz tenant_id." });

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (tenantRepo == null)
        return Results.Json(
            new { error = ErrorCodes.BackendTenantImpersonateFailed, message = "PostgreSQL not configured" },
            statusCode: 503);

    try
    {
        var tenant = await tenantRepo.GetTenantAsync(id);
        if (tenant == null)
            return Results.NotFound(
                new { error = ErrorCodes.IntegrationTenantNotFound, message = $"Tenant {id} bulunamadi." });

        if (!tenant.IsActive)
            return Results.Json(
                new { error = ErrorCodes.BackendTenantImpersonateFailed, message = $"Tenant {id} aktif degil." },
                statusCode: 403);

        var tokenExpiry = TimeSpan.FromHours(8);
        var token = jwtGenerator.GenerateToken(id, "admin", "ops_impersonate", tokenExpiry, "0");

        jsonLog.StepInfo($"ops impersonate: superadmin entered tenant {id} ({tenant.TenantName})", Guid.NewGuid().ToString("N"));

        return Results.Ok(new
        {
            token,
            tenant_id = id,
            user_id = 0,
            role = "admin",
            full_name = $"SuperAdmin @ {tenant.TenantName}",
            lang = "tr",
            inse_features = new[] { "FlowBuilder", "Knowledge", "Outbound", "Appointments", "Analytics", "Integrations", "Marketing" },
            expires_in = (int)tokenExpiry.TotalSeconds,
            token_type = "Bearer",
        });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tenant impersonate failed ({ErrorCodes.BackendTenantImpersonateFailed}): tenantId={id}, {ex.Message}");
        return Results.Json(
            new { error = ErrorCodes.BackendTenantImpersonateFailed, message = "Firma girisi basarisiz oldu." },
            statusCode: 500);
    }
});

// ============================================
// INMA SSO AUTH ENDPOINTS
// ============================================

// Akis 1: inma JWT -> inse JWT exchange (URL token flow)
// inma'dan gelen ?accesstoken= parametresi bu endpoint'e gonderilir.
// InmaAuth:SecretKey yoksa signature validation atlanir (decode-only fallback).
app.MapPost("/api/v1/inma/auth/exchange", async (HttpContext ctx, IHttpClientFactory httpClientFactory, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (jwtGenerator == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT generator not configured", requestId),
            statusCode: 503);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var inmaToken = root.TryGetProperty("token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(inmaToken))
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralValidation, "token field required", requestId),
                statusCode: 400);

        InmaTokenContext? inmaCtx = null;

        // Path A: inmaJwtValidator configured → full signature validation
        if (inmaJwtValidator != null)
        {
            var (ctx2, error) = inmaJwtValidator.ValidateToken(inmaToken);
            if (ctx2 == null)
            {
                jsonLogger.StepWarn($"inma token exchange failed: {error}", requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthUnauthorized, error ?? "Invalid token", requestId),
                    statusCode: 401);
            }
            inmaCtx = ctx2;
        }
        else
        {
            // Path B: InmaAuth:SecretKey not configured → decode-only (no signature verification)
            // INMA JWT claim'lerini okuyup InmaTokenContext olustur
            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (!handler.CanReadToken(inmaToken))
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthTokenInvalid, "Not a valid JWT format", requestId),
                        statusCode: 401);

                var jwt = handler.ReadJwtToken(inmaToken);

                // Expiry check (manual — no signature validation means no automatic lifetime check)
                if (jwt.ValidTo != DateTime.MinValue && jwt.ValidTo < DateTime.UtcNow.AddSeconds(-60))
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthTokenExpired, "Token expired", requestId),
                        statusCode: 401);

                // CompanyCode = our tenant_id (e.g. "5050"), CompanyId = INMA's internal ID
                var companyCodeStr = jwt.Claims.FirstOrDefault(c => c.Type == "CompanyCode")?.Value;
                var userIdStr = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
                    || c.Type == ClaimTypes.NameIdentifier)?.Value;
                var chatRole = jwt.Claims.FirstOrDefault(c => c.Type == "ChatRole")?.Value;
                var fullName = jwt.Claims.FirstOrDefault(c => c.Type == "FullName")?.Value ?? "";
                var lang = jwt.Claims.FirstOrDefault(c => c.Type == "Lang")?.Value ?? "tr";

                if (!int.TryParse(companyCodeStr, out var tenantId) || tenantId <= 0)
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthTokenInvalid, "Missing or invalid CompanyCode claim", requestId),
                        statusCode: 401);
                if (!int.TryParse(userIdStr, out var userId))
                    return Results.Json(
                        ErrorResponse.Create(ErrorCodes.AuthTokenInvalid, "Missing or invalid nameidentifier claim", requestId),
                        statusCode: 401);

                var role = chatRole switch { "2" => "admin", _ => "agent" };

                string[] inseFeatures = [];
                var featuresRaw = jwt.Claims.FirstOrDefault(c => c.Type == "InseFeatures")?.Value;
                if (!string.IsNullOrWhiteSpace(featuresRaw))
                    try { inseFeatures = System.Text.Json.JsonSerializer.Deserialize<string[]>(featuresRaw) ?? []; } catch { }

                inmaCtx = new InmaTokenContext
                {
                    TenantId = tenantId,
                    UserId = userId,
                    Role = role,
                    FullName = fullName,
                    Lang = lang,
                    InseFeatures = inseFeatures
                };

                jsonLogger.StepInfo($"inma token exchange (decode-only): tenant={tenantId} user={userId}", requestId);
            }
            catch (Exception ex)
            {
                jsonLogger.StepWarn($"inma token decode failed: {ex.Message}", requestId);
                return Results.Json(
                    ErrorResponse.Create(ErrorCodes.AuthTokenInvalid, "Failed to decode INMA token", requestId),
                    statusCode: 401);
            }
        }

        var tokenExpiry = TimeSpan.FromHours(8);
        var inseToken = jwtGenerator.GenerateToken(
            inmaCtx.TenantId, inmaCtx.Role, "inma_exchange", tokenExpiry, inmaCtx.UserId.ToString());

        jsonLogger.StepInfo($"inma token exchange success: tenant={inmaCtx.TenantId} user={inmaCtx.UserId}", requestId);
        return Results.Ok(new
        {
            token = inseToken,
            tenant_id = inmaCtx.TenantId,
            user_id = inmaCtx.UserId,
            role = inmaCtx.Role,
            full_name = inmaCtx.FullName,
            lang = inmaCtx.Lang,
            inse_features = inmaCtx.InseFeatures,
            expires_in = (int)tokenExpiry.TotalSeconds,
            token_type = "Bearer"
        });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId),
            statusCode: 400);
    }
});

// Akis 2: firma + kullanici + parola -> inma login -> inse JWT
// inse login ekranindan kullanici inma credentials ile giris yapar.
app.MapPost("/api/v1/inma/auth/login", async (HttpContext ctx, IHttpClientFactory httpClientFactory, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (inmaJwtValidator == null || jwtGenerator == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma auth not configured", requestId),
            statusCode: 503);

    if (string.IsNullOrEmpty(inmaJwtSettings.LoginUrl))
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma login URL not configured", requestId),
            statusCode: 503);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var companyName = root.TryGetProperty("company_name", out var c) ? c.GetString() : null;
        var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
        var password = root.TryGetProperty("password", out var p) ? p.GetString() : null;

        if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralValidation, "company_name, username and password are required", requestId),
                statusCode: 400);

        // Proxy credentials to inma login endpoint
        var inmaClient = httpClientFactory.CreateClient("inma_login");
        var loginPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            companyName,
            username,
            password
        });

        using var inmaRequest = new HttpRequestMessage(HttpMethod.Post, inmaJwtSettings.LoginUrl)
        {
            Content = new StringContent(loginPayload, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage inmaResponse;
        try
        {
            inmaResponse = await inmaClient.SendAsync(inmaRequest);
        }
        catch (HttpRequestException ex)
        {
            jsonLogger.StepWarn($"inma login proxy failed (network): {ex.Message}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma servisine erisim saglanamadi", requestId),
                statusCode: 503);
        }
        catch (TaskCanceledException)
        {
            jsonLogger.StepWarn("inma login proxy timed out", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma servisine erisim zaman asimina ugradi", requestId),
                statusCode: 503);
        }

        if (!inmaResponse.IsSuccessStatusCode)
        {
            jsonLogger.StepWarn($"inma login rejected: HTTP {(int)inmaResponse.StatusCode}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Gecersiz firma adi, kullanici adi veya parola", requestId),
                statusCode: 401);
        }

        var inmaBody = await inmaResponse.Content.ReadAsStringAsync();
        // inma returns accesstoken + refreshtoken in response body
        string? inmaToken = null;
        string? inmaRefreshToken = null;
        try
        {
            using var respDoc = JsonDocument.Parse(inmaBody);
            inmaToken = respDoc.RootElement.TryGetProperty("accesstoken", out var at) ? at.GetString()
                      : respDoc.RootElement.TryGetProperty("accessToken", out var at2) ? at2.GetString()
                      : respDoc.RootElement.TryGetProperty("token", out var tk) ? tk.GetString()
                      : null;
            inmaRefreshToken = respDoc.RootElement.TryGetProperty("refreshtoken", out var rt) ? rt.GetString()
                             : respDoc.RootElement.TryGetProperty("refreshToken", out var rt2) ? rt2.GetString()
                             : null;
        }
        catch (JsonException)
        {
            // inma may return plain token string
            inmaToken = inmaBody.Trim('"');
        }

        if (string.IsNullOrWhiteSpace(inmaToken))
        {
            jsonLogger.StepWarn("inma login succeeded but token missing in response", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma token alinamadi", requestId),
                statusCode: 500);
        }

        var (inmaCtx, error) = inmaJwtValidator.ValidateToken(inmaToken);
        if (inmaCtx == null)
        {
            jsonLogger.StepWarn($"inma login token validation failed: {error}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AuthUnauthorized, error ?? "Token dogrulanamadi", requestId),
                statusCode: 401);
        }

        jsonLogger.StepInfo($"inma login success: tenant={inmaCtx.TenantId} user={inmaCtx.UserId}", requestId);
        return Results.Ok(new
        {
            token = inmaToken,
            refresh_token = inmaRefreshToken ?? string.Empty,
            tenant_id = inmaCtx.TenantId,
            user_id = inmaCtx.UserId,
            role = inmaCtx.Role,
            full_name = inmaCtx.FullName,
            lang = inmaCtx.Lang,
            inse_features = inmaCtx.InseFeatures,
            expires_in = 28800,
            token_type = "Bearer"
        });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId),
            statusCode: 400);
    }
});

// Akis 3: Mock login (DEV/TEST only — InmaAuth:MockEnabled=true zorunlu)
// inma hazir olmadan UI akisini test etmek icin. Hardcoded scenariolar, inse JWT uretir.
app.MapPost("/api/v1/inma/auth/mock-login", async (HttpContext ctx, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (!inmaAuthMockEnabled)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Mock login disabled", requestId),
            statusCode: 503);

    if (jwtGenerator == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId),
            statusCode: 503);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var scenario = bodyDoc.RootElement.TryGetProperty("scenario", out var s) ? s.GetString() : null;

        var (tenantId, userId, role, fullName, features) = scenario switch
        {
            "klinik" => (1, 9002, "admin", "Demo Klinik",
                new[] { "Appointments", "Knowledge", "Analytics" }),
            "otel"   => (1, 9003, "admin", "Demo Otel",
                new[] { "FlowBuilder", "Knowledge", "Outbound", "Marketing" }),
            _        => (1, 9001, "admin", "Demo Admin (Tam Yetkili)",
                new[] { "FlowBuilder", "Knowledge", "Outbound", "Appointments", "Analytics", "Integrations", "Marketing" })
        };

        var tokenExpiry = TimeSpan.FromHours(8);
        var inseToken = jwtGenerator.GenerateToken(tenantId, role, "inma_mock", tokenExpiry, userId.ToString());

        jsonLogger.StepInfo($"inma mock login: scenario={scenario ?? "full"} tenant={tenantId} user={userId}", requestId);
        return Results.Ok(new
        {
            token = inseToken,
            tenant_id = tenantId,
            user_id = userId,
            role,
            full_name = fullName,
            lang = "tr",
            inse_features = features,
            expires_in = (int)tokenExpiry.TotalSeconds,
            token_type = "Bearer"
        });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId),
            statusCode: 400);
    }
});

// Ops superadmin quick login (MockEnabled gate) — sifre gerektirmez, inse JWT ile ops yetkisi
app.MapPost("/api/v1/ops/auth/quicklogin", (HttpContext ctx, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (!inmaAuthMockEnabled)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "Quick login disabled", requestId),
            statusCode: 503);

    if (jwtGenerator == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId),
            statusCode: 503);

    var tokenExpiry = TimeSpan.FromHours(8);
    var inseToken = jwtGenerator.GenerateToken(0, "admin", "ops_quicklogin", tokenExpiry, "0");

    jsonLogger.StepInfo("ops quicklogin: superadmin JWT issued", requestId);
    return Results.Ok(new
    {
        token = inseToken,
        tenant_id = 0,
        user_id = 0,
        role = "admin",
        full_name = "Super Admin",
        lang = "tr",
        inse_features = new[] { "FlowBuilder", "Knowledge", "Outbound", "Appointments", "Analytics", "Integrations", "Marketing" },
        expires_in = (int)tokenExpiry.TotalSeconds,
        token_type = "Bearer"
    });
});

// Akis 5: Token refresh proxy — INMA refresh endpoint'ine proxy yapar
app.MapPost("/api/v1/inma/auth/refresh", async (HttpContext ctx, IHttpClientFactory httpClientFactory, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var refreshUrl = inmaJwtSettings.RefreshUrl;
    if (string.IsNullOrEmpty(refreshUrl))
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma refresh URL not configured", requestId),
            statusCode: 503);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var accessToken = root.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
        var refreshToken = root.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;

        if (string.IsNullOrWhiteSpace(refreshToken))
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralValidation, "refreshToken is required", requestId),
                statusCode: 400);

        var inmaClient = httpClientFactory.CreateClient("inma_login");
        var refreshPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            refreshToken,
            accessToken = accessToken ?? string.Empty
        });

        using var inmaRequest = new HttpRequestMessage(HttpMethod.Post, refreshUrl)
        {
            Content = new StringContent(refreshPayload, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage inmaResponse;
        try
        {
            inmaResponse = await inmaClient.SendAsync(inmaRequest);
        }
        catch (HttpRequestException ex)
        {
            jsonLogger.StepWarn($"inma refresh proxy failed (network): {ex.Message}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma servisine erisim saglanamadi", requestId),
                statusCode: 503);
        }
        catch (TaskCanceledException)
        {
            jsonLogger.StepWarn("inma refresh proxy timed out", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma servisine erisim zaman asimina ugradi", requestId),
                statusCode: 503);
        }

        if (!inmaResponse.IsSuccessStatusCode)
        {
            jsonLogger.StepWarn($"inma refresh rejected: HTTP {(int)inmaResponse.StatusCode}", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Refresh token gecersiz veya suresi dolmus", requestId),
                statusCode: 401);
        }

        var inmaBody = await inmaResponse.Content.ReadAsStringAsync();
        // Parse new tokens from inma response
        string? newAccessToken = null;
        string? newRefreshToken = null;
        try
        {
            using var respDoc = JsonDocument.Parse(inmaBody);
            newAccessToken = respDoc.RootElement.TryGetProperty("accesstoken", out var a1) ? a1.GetString()
                           : respDoc.RootElement.TryGetProperty("accessToken", out var a2) ? a2.GetString()
                           : respDoc.RootElement.TryGetProperty("token", out var tk) ? tk.GetString()
                           : null;
            newRefreshToken = respDoc.RootElement.TryGetProperty("refreshtoken", out var r1) ? r1.GetString()
                            : respDoc.RootElement.TryGetProperty("refreshToken", out var r2) ? r2.GetString()
                            : null;
        }
        catch (JsonException)
        {
            jsonLogger.StepWarn("inma refresh response parse failed", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma refresh cevabi okunamadi", requestId),
                statusCode: 500);
        }

        if (string.IsNullOrWhiteSpace(newAccessToken))
        {
            jsonLogger.StepWarn("inma refresh succeeded but new token missing", requestId);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma yeni token donmedi", requestId),
                statusCode: 500);
        }

        jsonLogger.StepInfo("inma token refresh success", requestId);
        return Results.Ok(new
        {
            accessToken = newAccessToken,
            refreshToken = newRefreshToken ?? string.Empty
        });
    }
    catch (JsonException)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId),
            statusCode: 400);
    }
});

// Akis 6: Welcome proxy — INMA welcome endpoint'ine proxy yapar
app.MapGet("/api/v1/inma/welcome", async (HttpContext ctx, IHttpClientFactory httpClientFactory, JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var apiBaseUrl = inmaJwtSettings.ApiBaseUrl;
    if (string.IsNullOrEmpty(apiBaseUrl))
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma API base URL not configured", requestId),
            statusCode: 503);

    // Forward the Bearer token from the incoming request to inma
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", requestId),
            statusCode: 401);

    var inmaClient = httpClientFactory.CreateClient("inma_login");
    var welcomeUrl = $"{apiBaseUrl.TrimEnd('/')}/api/invekto/welcome";

    using var inmaRequest = new HttpRequestMessage(HttpMethod.Get, welcomeUrl);
    inmaRequest.Headers.TryAddWithoutValidation("Authorization", authHeader);

    HttpResponseMessage inmaResponse;
    try
    {
        inmaResponse = await inmaClient.SendAsync(inmaRequest);
    }
    catch (HttpRequestException ex)
    {
        jsonLogger.StepWarn($"inma welcome proxy failed (network): {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma servisine erisim saglanamadi", requestId),
            statusCode: 503);
    }
    catch (TaskCanceledException)
    {
        jsonLogger.StepWarn("inma welcome proxy timed out", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "inma zaman asimi", requestId),
            statusCode: 503);
    }

    var body = await inmaResponse.Content.ReadAsStringAsync();
    var contentType = inmaResponse.Content.Headers.ContentType?.MediaType ?? "application/json";

    if (!inmaResponse.IsSuccessStatusCode)
    {
        jsonLogger.StepWarn($"inma welcome failed: HTTP {(int)inmaResponse.StatusCode}", requestId);
        return Results.Text(body, contentType, statusCode: (int)inmaResponse.StatusCode);
    }

    return Results.Text(body, contentType);
});

// ============================================
// WAPCRM CALLBACK BRIDGE
// ============================================
// Automation sends OutgoingCallback → this endpoint transforms to WapCRM chatoperation format.
// No auth — internal service-to-service (localhost only).

app.MapPost("/api/v1/callback/wapcrm", async (HttpContext ctx, JsonLinesLogger jsonLog, IHttpClientFactory httpClientFactory) =>
{
    var requestId = Guid.NewGuid().ToString("N");

    OutgoingCallback? callback;
    try
    {
        callback = await ctx.Request.ReadFromJsonAsync<OutgoingCallback>();
    }
    catch (JsonException ex)
    {
        jsonLog.StepWarn($"WapCRM bridge: invalid JSON body: {ex.Message}", requestId);
        return Results.BadRequest(new { error = "INVALID_JSON", message = "Invalid callback JSON" });
    }

    if (callback == null)
        return Results.BadRequest(new { error = "EMPTY_BODY", message = "Callback body is null" });

    requestId = callback.RequestId ?? requestId;

    // handoff_to_human: log only, do NOT send WhatsApp message — flow already sent handoff message
    if (callback.Action == CallbackActions.HandoffToHuman)
    {
        jsonLog.StepInfo($"WapCRM bridge: handoff logged for tenant {callback.TenantId}, chat={callback.ChatId}", requestId);
        return Results.Ok(new { status = "handoff_logged", action = callback.Action });
    }

    // Only handle send_message
    if (callback.Action != CallbackActions.SendMessage)
    {
        jsonLog.StepInfo($"WapCRM bridge: skipping action '{callback.Action}' for tenant {callback.TenantId}", requestId);
        return Results.Ok(new { status = "skipped", action = callback.Action });
    }

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    var msgLogRepo = ctx.RequestServices.GetService<MessageLogRepository>();
    if (tenantRepo == null || msgLogRepo == null)
        return Results.Json(new { error = "DB_NOT_CONFIGURED", message = "PostgreSQL not configured" }, statusCode: 503);

    // Get WapCRM settings for this tenant
    var wapcrm = await tenantRepo.GetWapCrmSettingsAsync(callback.TenantId);
    if (wapcrm == null || string.IsNullOrWhiteSpace(wapcrm.SecretKey) || string.IsNullOrWhiteSpace(wapcrm.ApiUrl))
    {
        jsonLog.StepWarn($"WapCRM bridge: no WapCRM settings for tenant {callback.TenantId}", requestId);
        return Results.Json(new { error = "WAPCRM_NOT_CONFIGURED", message = $"Tenant {callback.TenantId} has no WapCRM settings" }, statusCode: 422);
    }

    // Extract phone from chat_id ("905xxx@c.us" → "905xxx")
    var phoneMatch = Regex.Match(callback.ChatId ?? "", @"(\d+)@");
    var phone = phoneMatch.Success ? phoneMatch.Groups[1].Value : callback.Data?.Phone ?? "";
    if (string.IsNullOrWhiteSpace(phone))
    {
        jsonLog.StepWarn($"WapCRM bridge: cannot extract phone from chat_id '{callback.ChatId}'", requestId);
        return Results.BadRequest(new { error = "INVALID_PHONE", message = "Cannot extract phone from chat_id" });
    }

    // Get instanceId from last incoming message for this tenant+phone
    var instanceId = await msgLogRepo.GetLastInstanceIdAsync(callback.TenantId, phone);
    if (string.IsNullOrWhiteSpace(instanceId) || !int.TryParse(instanceId, out var instanceIdInt))
    {
        jsonLog.StepWarn($"WapCRM bridge: no instanceId found for tenant {callback.TenantId}, phone {phone}", requestId);
        return Results.Json(new { error = "NO_INSTANCE_ID", message = "No incoming message found for this phone" }, statusCode: 422);
    }

    // Determine message text (only send_message reaches here)
    var messageText = callback.Data?.MessageText ?? "";

    if (string.IsNullOrWhiteSpace(messageText))
    {
        jsonLog.StepWarn($"WapCRM bridge: empty message_text for tenant {callback.TenantId}", requestId);
        return Results.BadRequest(new { error = "EMPTY_MESSAGE", message = "message_text is empty" });
    }

    // Build WapCRM chatoperation payload (exact property names — no camelCase)
    var wapPayload = new Dictionary<string, object>
    {
        ["instanceID"] = instanceIdInt,
        ["userID"] = wapcrm.UserId,
        ["chatPhoneNumber"] = phone,
        ["messageType"] = 1,
        ["messageText"] = messageText
    };

    try
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-CIB-SecretKey", wapcrm.SecretKey);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var jsonPayload = JsonSerializer.Serialize(wapPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(wapcrm.ApiUrl, content);
        sw.Stop();

        var responseBody = await response.Content.ReadAsStringAsync();

        jsonLog.StepInfo(
            $"WapCRM bridge: tenant={callback.TenantId}, phone={phone}, instanceId={instanceIdInt}, " +
            $"action={callback.Action}, status={response.StatusCode}, elapsed={sw.ElapsedMilliseconds}ms",
            requestId);

        // Log outgoing message to message_log
        _ = msgLogRepo.InsertAsync(
            callback.TenantId, "out", phone,
            senderName: "bot",
            messageText: messageText,
            messageType: "text",
            chatId: callback.ChatId,
            externalMessageId: null,
            instanceId: instanceId
        ).ContinueWith(t =>
        {
            if (t.IsFaulted) jsonLog.SystemWarn($"WapCRM bridge: message_log insert failed: {t.Exception?.GetBaseException().Message}");
        });

        return Results.Ok(new
        {
            status = response.IsSuccessStatusCode ? "sent" : "failed",
            wapcrm_status = (int)response.StatusCode,
            elapsed_ms = sw.ElapsedMilliseconds,
            response = responseBody
        });
    }
    catch (HttpRequestException ex)
    {
        jsonLog.SystemWarn($"WapCRM bridge: HTTP error sending to {wapcrm.ApiUrl}: {ex.Message}");
        return Results.Json(new { error = "WAPCRM_HTTP_ERROR", message = ex.Message }, statusCode: 502);
    }
});

// ============================================
// SPA FALLBACK ROUTES
// ============================================

// Flow Builder SPA fallback: /flow-builder/* -> wwwroot/flow-builder/index.html
app.MapFallbackToFile("flow-builder/{*path:nonfile}", "flow-builder/index.html");

// Dashboard SPA fallback - serve index.html for non-API routes (Dashboard routing)
app.MapFallbackToFile("index.html");

logger.SystemInfo($"Backend starting on port {ServiceConstants.BackendPort}");
app.Run();

// Required for integration tests
public partial class Program { }
