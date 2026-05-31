using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Logging;
using Invekto.VoiceRuntime.Audio;
using Invekto.VoiceRuntime.Endpoints;
using Invekto.VoiceRuntime.Metrics;
using Invekto.VoiceRuntime.Providers;
using Invekto.VoiceRuntime.Realtime;

var builder = WebApplication.CreateBuilder(args);

// Windows Service host (NSSM in F2 prod; F0 = dotnet run local)
builder.Host.UseWindowsService();

// ── Configuration ─────────────────────────────────────────────────
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.VoiceRuntimePort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";

// F0 PoC: Jwt:SecretKey may be empty in Development (dev=1 query bypass allowed in VoicePocEndpoints).
// F2 prod: secret required. We do not fail-fast here so dev runs without explicit secret.
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey must be configured in production");

// ── Kestrel ───────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);

    var certPath = builder.Configuration["Kestrel:Certificate:Path"];
    var certPassword = builder.Configuration["Kestrel:Certificate:Password"];
    var httpsPort = builder.Configuration.GetValue("Kestrel:HttpsPort", 0);
    if (!string.IsNullOrEmpty(certPath) && File.Exists(certPath) && httpsPort > 0)
    {
        options.ListenAnyIP(httpsPort, listenOptions => listenOptions.UseHttps(certPath, certPassword));
    }
});

// ── Logging ───────────────────────────────────────────────────────
var logger = new JsonLinesLogger("Invekto.VoiceRuntime", logPath);
builder.Services.AddSingleton(logger);
builder.Services.AddSingleton(sp => new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// ── Auth ──────────────────────────────────────────────────────────
// Dev mode: provide a placeholder secret if empty so JwtValidator constructor doesn't throw.
var effectiveJwtSecret = string.IsNullOrWhiteSpace(jwtSecretKey)
    ? "dev-only-placeholder-secret-min-32-bytes-not-for-production!!"
    : jwtSecretKey;
var jwtSettings = new JwtSettings
{
    SecretKey = effectiveJwtSecret,
    Issuer = builder.Configuration["Jwt:Issuer"],
    Audience = builder.Configuration["Jwt:Audience"],
    ClockSkewSeconds = builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds", 60)
};
builder.Services.AddSingleton(new JwtValidator(jwtSettings));

// AD-22: VoiceRuntime cross-service auth via JwtGenerator.GenerateServiceToken(targetTenantId).
// Used by Tenant/Flow/Knowledge HTTP clients to mint a short-lived (5min) per-tenant service JWT
// — pattern cloned from Automation.AiFaqHandler. JwtSettings reused (same SecretKey/Issuer/Audience
// as inbound validation), so minted tokens are accepted by sibling services.
builder.Services.AddSingleton(new JwtGenerator(jwtSettings));

// ── HTTP clients (AD-22) ──────────────────────────────────────────
// Three named clients targeting sibling microservices. BaseAddress + Timeout per service;
// the actual ServiceJwt header is attached per-call by each client (target tenant_id varies).
// AD-25: Voice Test browser fetches /api/ops/tenants and /api/v1/automation/flows directly with
// the Dashboard JWT (UI display); these named clients are the SERVER-SIDE authoritative re-fetch
// used by VoiceRuntime to build instructions (defense-in-depth).
// Default base URLs derive from ServiceConstants (canonical port map). Override via
// InternalServices:{Service}BaseUrl when running against non-localhost or non-default ports.
builder.Services.AddHttpClient("Backend", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["InternalServices:BackendBaseUrl"]
        ?? $"http://localhost:{ServiceConstants.BackendPort}");
    c.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient("Automation", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["InternalServices:AutomationBaseUrl"]
        ?? $"http://localhost:{ServiceConstants.AutomationPort}");
    c.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient("Knowledge", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["InternalServices:KnowledgeBaseUrl"]
        ?? $"http://localhost:{ServiceConstants.KnowledgePort}");
    c.Timeout = TimeSpan.FromSeconds(5);
});

// ── Cross-service clients (Chunk B AD-22 + AD-25) ─────────────────
// TenantInfoClient uses admin-scope JWT (Backend ValidateOpsAuth Bearer path requires Role==admin AND
// TenantId==0). FlowInfoClient + KnowledgeSearchClient use per-tenant service JWTs (AiFaqHandler pattern).
// All three are scoped to a single WS session lifetime — registered as transient.
builder.Services.AddTransient<Invekto.VoiceRuntime.Clients.TenantInfoClient>();
builder.Services.AddTransient<Invekto.VoiceRuntime.Clients.FlowInfoClient>();
builder.Services.AddTransient<Invekto.VoiceRuntime.Clients.KnowledgeSearchClient>();

// ── Function tools (Chunk C AD-23/29/30/31) ───────────────────────
// SearchKnowledgeBaseTool wraps KnowledgeSearchClient; transient so a tool execution gets a fresh
// HttpClient per call (the IHttpClientFactory itself is singleton; the typed client wrapping it
// stays lightweight). ToolExecutor is instantiated PER WS session inside VoicePocEndpoints (one
// per-call StringBuilder buffer dictionary), so it is NOT registered in DI.
// F-VR-E (spec §5): KB confidence thresholds, config-driven (Knowledge:Confidence*) so they can be
// tuned on prod without a rebuild. Built once at startup (logs a WARN now if misconfigured).
builder.Services.AddSingleton(
    Invekto.VoiceRuntime.Tools.KbConfidenceOptions.FromConfig(builder.Configuration, logger));
builder.Services.AddTransient<Invekto.VoiceRuntime.Tools.SearchKnowledgeBaseTool>();

// ── Audio + Realtime + Metrics ────────────────────────────────────
builder.Services.AddSingleton<OpusCodec>();
builder.Services.AddSingleton<SileroVad>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var modelPath = cfg["Audio:VadModelPath"] ?? "Models/silero_vad.onnx";
    return new SileroVad(modelPath, sp.GetRequiredService<JsonLinesLogger>());
});
builder.Services.AddSingleton<RealtimeSessionFactory>();
builder.Services.AddSingleton<LatencyTracker>();
builder.Services.AddSingleton<MicrophoneCallProvider>();

// ── CORS for browser dev (voice-poc.html served from same origin in prod) ─
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? new[] { "http://localhost:7115" };
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

// ── Boot-time sanity: AD-19 lifecycle invariant requires fail-fast on missing model ──
var bootCfg = app.Services.GetRequiredService<IConfiguration>();
var vadModelPath = bootCfg["Audio:VadModelPath"] ?? "Models/silero_vad.onnx";
if (!File.Exists(vadModelPath))
{
    var msg = $"[{ErrorCodes.VoiceRuntimeVadModelMissing}] [Boot] Silero VAD model missing at '{vadModelPath}'. " +
              $"Run download script from src/Invekto.VoiceRuntime/Models/README.md.";
    logger.SystemError(msg);
    throw new FileNotFoundException($"{ErrorCodes.VoiceRuntimeVadModelMissing}: Silero VAD model missing at {vadModelPath}", vadModelPath);
}
logger.SystemInfo($"[Boot] Silero VAD model found: {vadModelPath}");

// AD-20: env-var-ONLY policy. No appsettings fallback.
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeRealtimeAuthFailed}] [Boot] OPENAI_API_KEY env variable not set. " +
                       $"First WS session will fail with {ErrorCodes.VoiceRuntimeRealtimeAuthFailed}.");
}
else
{
    var masked = apiKey.Length > 8 ? $"{apiKey[..4]}...{apiKey[^4..]}" : "***";
    logger.SystemInfo($"[Boot] OPENAI_API_KEY configured (masked: {masked})");
}

// ── Middleware ────────────────────────────────────────────────────
app.UseCors();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// Static files for browser PoC test page (wwwroot/voice-poc.html)
app.UseDefaultFiles();
app.UseStaticFiles();

// ── Endpoints ─────────────────────────────────────────────────────
app.MapHealthEndpoints();
app.MapMetricsEndpoints();
app.MapVoicePocEndpoints();

logger.SystemInfo($"[Boot] Invekto.VoiceRuntime starting on port {listenPort} (env={app.Environment.EnvironmentName})");

app.Run();
