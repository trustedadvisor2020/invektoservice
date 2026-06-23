using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Data;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;
using Chatinbox.Shared.Middleware;
using Chatinbox.Shared.Services;
using Chatinbox.VoiceAI.Services;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.VoiceAIPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var whisperApiKey = builder.Configuration["Whisper:ApiKey"] ?? "";
var whisperModel = builder.Configuration["Whisper:Model"] ?? "whisper-1";
var whisperTimeoutSec = builder.Configuration.GetValue<int>("Whisper:TimeoutSeconds", 30);
var maxFileSizeMb = builder.Configuration.GetValue<int>("Whisper:MaxFileSizeMb", 25);
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";
var chatAnalysisBaseUrl = builder.Configuration["ChatAnalysis:BaseUrl"] ?? "http://localhost:7101";
var chatAnalysisTimeoutMs = builder.Configuration.GetValue<int>("ChatAnalysis:TimeoutMs", 15000);
var chatAnalysisInternalApiKey = builder.Configuration["ChatAnalysis:InternalApiKey"] ?? "";

// Validate required config
if (string.IsNullOrEmpty(whisperApiKey))
    throw new InvalidOperationException("FATAL: Whisper:ApiKey is not configured");
if (string.IsNullOrEmpty(pgConnStr))
    throw new InvalidOperationException("FATAL: ConnectionStrings:PostgreSQL is not configured");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey is not configured");

// Configure Kestrel
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
var logger = new JsonLinesLogger(ServiceConstants.VoiceAIServiceName, logPath);
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

// Register PostgreSQL
var pgFactory = new PostgresConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register WhisperApiService (typed HttpClient)
builder.Services.AddHttpClient<WhisperApiService>()
    .AddTypedClient((httpClient, sp) =>
    {
        httpClient.Timeout = TimeSpan.FromSeconds(whisperTimeoutSec);
        return new WhisperApiService(httpClient, whisperApiKey, whisperModel,
            sp.GetRequiredService<JsonLinesLogger>());
    });

// Register ChatAnalysis HttpClient (service-to-service)
builder.Services.AddHttpClient("ChatAnalysis", client =>
{
    client.BaseAddress = new Uri(chatAnalysisBaseUrl);
    client.Timeout = TimeSpan.FromMilliseconds(chatAnalysisTimeoutMs);
});

// Register VoiceTranscriptionService
builder.Services.AddSingleton(sp =>
{
    var whisper = sp.GetRequiredService<WhisperApiService>();
    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var caClient = httpFactory.CreateClient("ChatAnalysis");
    return new VoiceTranscriptionService(
        whisper, caClient, chatAnalysisInternalApiKey, pgConnStr,
        sp.GetRequiredService<JsonLinesLogger>());
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware
app.UseTrafficLogging();
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Faz 1: Plan-based feature guard
var planCache = new TenantPlanCache(pgConnStr, logger);
app.UseFeatureGuard(planCache, logger,
    ("/api/v1/", "VoiceAI"));
app.UseAuthorization();

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

var maxFileSizeBytes = maxFileSizeMb * 1024L * 1024L;

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.VoiceAIServiceName)));

app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.VoiceAIServiceName));
});

// ============================================================
// Transcription endpoint
// ============================================================

app.MapPost("/api/v1/voice/transcribe", async (HttpContext ctx, VoiceTranscriptionService svc, JsonLinesLogger log) =>
{
    var tenantId = ctx.Items.TryGetValue("TenantId", out var tid) && tid is int t ? t : 0;
    var requestId = ctx.TraceIdentifier;

    // Read multipart form
    if (!ctx.Request.HasFormContentType)
    {
        log.StepError($"[{ErrorCodes.VoiceAINoAudioFile}] Request is not multipart/form-data", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAINoAudioFile, "multipart/form-data bekleniyor", requestId),
            statusCode: 400);
    }

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("audio");

    if (file == null || file.Length == 0)
    {
        log.StepError($"[{ErrorCodes.VoiceAINoAudioFile}] No audio file in 'audio' field", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAINoAudioFile, "'audio' alanında ses dosyası bulunamadı", requestId),
            statusCode: 400);
    }

    if (file.Length > maxFileSizeBytes)
    {
        log.StepError($"[{ErrorCodes.VoiceAIAudioTooLarge}] Audio file too large: {file.Length / 1024 / 1024}MB > {maxFileSizeMb}MB", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAIAudioTooLarge,
                $"Ses dosyası çok büyük (max {maxFileSizeMb}MB)", requestId),
            statusCode: 413);
    }

    if (!WhisperApiService.IsSupportedFormat(file.FileName))
    {
        log.StepError($"[{ErrorCodes.VoiceAIUnsupportedFormat}] Unsupported format: {Path.GetExtension(file.FileName)}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAIUnsupportedFormat,
                "Desteklenmeyen ses formatı. Desteklenen: mp3, mp4, wav, webm, ogg, flac, m4a", requestId),
            statusCode: 415);
    }

    try
    {
        using var stream = file.OpenReadStream();
        var result = await svc.ProcessAsync(stream, file.FileName, tenantId, requestId, ctx.RequestAborted);

        return Results.Ok(result);
    }
    catch (HttpRequestException ex)
    {
        log.StepError($"[{ErrorCodes.VoiceAITranscriptionFailed}] Whisper API failed: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAITranscriptionFailed,
                "Ses tanıma servisi şu anda kullanılamıyor", requestId),
            statusCode: 502);
    }
    catch (TaskCanceledException ex) when (!ctx.RequestAborted.IsCancellationRequested)
    {
        log.StepError($"[{ErrorCodes.VoiceAITranscriptionFailed}] Whisper API timed out: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAITranscriptionFailed,
                "Ses tanıma zaman aşımına uğradı", requestId),
            statusCode: 504);
    }
});

// ============================================================
// Ops endpoints
// ============================================================

var endpoints = new List<EndpointInfo>
{
    new() { Method = "GET", Path = "/health", Description = "Health check", Category = "Health" },
    new() { Method = "GET", Path = "/ready", Description = "Readiness check (DB)", Category = "Health" },
    new() { Method = "POST", Path = "/api/v1/voice/transcribe", Description = "Transcribe audio → text + intent", Category = "API" },
    new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery", Category = "Ops" }
};

app.MapGet("/api/ops/endpoints", () => new EndpointDiscoveryResponse
{
    Service = ServiceConstants.VoiceAIServiceName,
    Port = ServiceConstants.VoiceAIPort,
    Endpoints = endpoints
});

app.Run();
