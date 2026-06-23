using System.Data.Common;
using System.Text.Json;
using Hangfire;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Hosting;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.DTOs;
using Chatinbox.Shared.Logging;
using Chatinbox.Shared.Middleware;
using Chatinbox.WhatsAppAnalytics.Data;
using Chatinbox.WhatsAppAnalytics.Models;
using Chatinbox.WhatsAppAnalytics.Services;
using Chatinbox.WhatsAppAnalytics.Services.Benchmark;
using Chatinbox.WhatsAppAnalytics.Services.Insights;
using Microsoft.AspNetCore.Mvc;
using Chatinbox.WhatsAppAnalytics.Services.Pipeline;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.WhatsAppAnalyticsPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";
var uploadPath = builder.Configuration["Storage:UploadPath"] ?? "uploads";
var maxFileSizeMb = builder.Configuration.GetValue<int>("Storage:MaxFileSizeMb", 500);

// Claude API configuration (NLP stages 4 & 6)
var claudeApiKey = builder.Configuration["Claude:ApiKey"];
var claudeModel = builder.Configuration["Claude:Model"] ?? "claude-haiku-4-5-20251001";
var claudeMaxTokens = builder.Configuration.GetValue<int>("Claude:MaxTokens", 4096);
var claudeTimeoutSeconds = builder.Configuration.GetValue<int>("Claude:TimeoutSeconds", 30);

// Customer MSSQL configuration (optional — for import-mssql endpoint)
var mssqlServer = builder.Configuration["CustomerMssql:Server"] ?? "";
var mssqlPort = builder.Configuration.GetValue<int>("CustomerMssql:Port", 1433);
var mssqlUser = builder.Configuration["CustomerMssql:User"] ?? "";
var mssqlPassword = builder.Configuration["CustomerMssql:Password"] ?? "";
var mssqlConfigured = !string.IsNullOrEmpty(mssqlServer) && !string.IsNullOrEmpty(mssqlUser);

// Validate required config
if (string.IsNullOrEmpty(pgConnStr))
    throw new InvalidOperationException("FATAL: ConnectionStrings:PostgreSQL is not configured");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey is not configured");

// Configure form options to match max file size
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = maxFileSizeMb * 1024L * 1024L;
});

// Configure Kestrel + HTTPS if certificate is configured
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);
    options.Limits.MaxRequestBodySize = maxFileSizeMb * 1024L * 1024L;

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
var logger = new JsonLinesLogger(ServiceConstants.WhatsAppAnalyticsServiceName, logPath);
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
var pgFactory = new AnalyticsConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register repository
builder.Services.AddSingleton<AnalyticsRepository>();

// Register pipeline services (Phase A: stages 1-3)
builder.Services.AddSingleton<CsvStreamReader>();
builder.Services.AddSingleton<TextNormalizer>();

// Register MSSQL reader (optional — null if not configured)
if (mssqlConfigured)
{
    builder.Services.AddSingleton<MssqlReaderService>(sp =>
        new MssqlReaderService(mssqlServer, mssqlPort, mssqlUser, mssqlPassword,
            sp.GetRequiredService<JsonLinesLogger>()));
}

builder.Services.AddSingleton<CleanerService>(sp =>
    new CleanerService(
        sp.GetRequiredService<AnalyticsRepository>(),
        sp.GetRequiredService<CsvStreamReader>(),
        sp.GetService<MssqlReaderService>(), // nullable — null if not configured
        sp.GetRequiredService<TextNormalizer>(),
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddSingleton<ThreaderService>(sp =>
    new ThreaderService(
        sp.GetRequiredService<AnalyticsRepository>(),
        sp.GetRequiredService<AnalyticsConnectionFactory>(),
        sp.GetRequiredService<TextNormalizer>(),
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddSingleton<StatsService>();

// Register Claude API client (NLP stages 4 & 6)
builder.Services.AddSingleton<ClaudeClient>(sp =>
    new ClaudeClient(claudeApiKey, claudeModel, claudeMaxTokens, claudeTimeoutSeconds,
        sp.GetRequiredService<JsonLinesLogger>()));

// Register NLP pipeline services (Phase B: stages 4-7)
builder.Services.AddSingleton<IntentClassifierService>();
builder.Services.AddSingleton<FaqExtractorService>();
builder.Services.AddSingleton<SentimentAnalyzerService>();
builder.Services.AddSingleton<ProductAnalyzerService>();

// Register orchestrator (stages 1-7)
builder.Services.AddSingleton<PipelineOrchestrator>();

// Register background processing service
builder.Services.AddSingleton<AnalysisProcessingService>(sp =>
    new AnalysisProcessingService(
        sp.GetRequiredService<AnalyticsRepository>(),
        sp.GetRequiredService<PipelineOrchestrator>(),
        sp.GetRequiredService<JsonLinesLogger>(),
        uploadPath));
builder.Services.AddHostedService(sp => sp.GetRequiredService<AnalysisProcessingService>());

// ============================================================
// LLM Benchmark services (Phase RI)
// ============================================================

// Gemini API configuration
var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
var geminiFlashModel = builder.Configuration["Gemini:FlashModel"] ?? "gemini-2.0-flash";
var geminiProModel = builder.Configuration["Gemini:ProModel"] ?? "gemini-3.1-pro-preview";
var gemini3FlashModel = builder.Configuration["Gemini:Flash3Model"] ?? "gemini-3-flash-preview";
var geminiMaxTokens = builder.Configuration.GetValue<int>("Gemini:MaxTokens", 4096);
var geminiTimeoutSeconds = builder.Configuration.GetValue<int>("Gemini:TimeoutSeconds", 60);

// Claude Sonnet model (shares API key with Haiku)
var claudeSonnetModel = builder.Configuration["ClaudeSonnet:Model"] ?? "claude-sonnet-4-20250514";

// Benchmark configuration
var benchmarkOpsKey = builder.Configuration["Benchmark:OpsKey"] ?? "";
if (string.IsNullOrEmpty(benchmarkOpsKey))
    // Fail-closed (audit Batch 3): an empty key previously made ValidateOpsKey return true,
    // leaving every /api/ops/* benchmark endpoint (MSSQL reads, PG writes, label mutation) open.
    throw new InvalidOperationException("FATAL: Benchmark:OpsKey is not configured (/api/ops/* benchmark endpoints would be OPEN without auth)");
var benchmarkDelay = builder.Configuration.GetValue<int>("Benchmark:DelayBetweenCallsMs", 500);
var benchmarkMaxTextLen = builder.Configuration.GetValue<int>("Benchmark:MaxThreadTextLength", 4000);
var benchmarkDefaultSample = builder.Configuration.GetValue<int>("Benchmark:DefaultSampleSize", 200);
var benchmarkMinMsgs = builder.Configuration.GetValue<int>("Benchmark:MinMessages", 6);
var benchmarkMaxMsgs = builder.Configuration.GetValue<int>("Benchmark:MaxMessages", 200);

// Keyed ILlmClient registrations (.NET 8)
builder.Services.AddKeyedSingleton<ILlmClient>("claude_haiku", (sp, _) =>
    new ClaudeClient(claudeApiKey, claudeModel, claudeMaxTokens, claudeTimeoutSeconds,
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddKeyedSingleton<ILlmClient>("claude_sonnet", (sp, _) =>
    new ClaudeClient(claudeApiKey, claudeSonnetModel, claudeMaxTokens, claudeTimeoutSeconds,
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddKeyedSingleton<ILlmClient>("gemini_flash", (sp, _) =>
    new GeminiClient(geminiApiKey, geminiFlashModel, geminiMaxTokens, geminiTimeoutSeconds,
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddKeyedSingleton<ILlmClient>("gemini_pro", (sp, _) =>
    new GeminiClient(geminiApiKey, geminiProModel, geminiMaxTokens, geminiTimeoutSeconds,
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddKeyedSingleton<ILlmClient>("gemini_3_flash", (sp, _) =>
    new GeminiClient(geminiApiKey, gemini3FlashModel, geminiMaxTokens, geminiTimeoutSeconds,
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddKeyedSingleton<ILlmClient>("tiered", (sp, _) =>
    new TieredClassifierService(
        sp.GetRequiredKeyedService<ILlmClient>("gemini_flash"),
        sp.GetRequiredKeyedService<ILlmClient>("claude_haiku"),
        sp.GetRequiredService<JsonLinesLogger>()));

// Benchmark services
builder.Services.AddSingleton<PiiMasker>();
builder.Services.AddSingleton<BenchmarkRepository>();
builder.Services.AddSingleton<MetricsCalculator>();
builder.Services.AddSingleton<OutcomeClassifierService>();

if (mssqlConfigured)
{
    builder.Services.AddSingleton<BenchmarkOrchestrator>(sp =>
        new BenchmarkOrchestrator(
            sp.GetRequiredService<BenchmarkRepository>(),
            sp.GetRequiredService<MssqlReaderService>(),
            sp.GetRequiredService<PiiMasker>(),
            sp.GetRequiredService<OutcomeClassifierService>(),
            sp.GetRequiredService<ThreaderService>(),
            sp,
            sp.GetRequiredService<JsonLinesLogger>(),
            benchmarkDelay,
            benchmarkMaxTextLen));
    builder.Services.AddSingleton<BenchmarkProcessingService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BenchmarkProcessingService>());
}

// ============================================================
// Batch Classification services (RI-2.6-2.8)
// ============================================================

builder.Services.AddSingleton<ConversationOutcomeRepository>();
builder.Services.AddSingleton<SectorConfigRepository>();

if (mssqlConfigured)
{
    builder.Services.AddSingleton<BatchClassificationService>(sp =>
        new BatchClassificationService(
            sp.GetRequiredService<ConversationOutcomeRepository>(),
            sp.GetRequiredService<MssqlReaderService>(),
            sp.GetRequiredService<PiiMasker>(),
            sp.GetRequiredService<OutcomeClassifierService>(),
            sp.GetRequiredService<TextNormalizer>(),
            sp,
            sp.GetRequiredService<JsonLinesLogger>(),
            benchmarkDelay,
            benchmarkMaxTextLen,
            benchmarkMinMsgs,
            benchmarkMaxMsgs));
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BatchClassificationService>());

    // G7 Faz 5: NightlyBatch migrated to Hangfire recurring (queue=waanalytics)
    var nightlyConfig = new NightlyBatchConfig();
    builder.Configuration.GetSection("NightlyBatch").Bind(nightlyConfig);
    builder.Services.AddSingleton(nightlyConfig);
    var hangfireConnStr = Chatinbox.Shared.Hosting.HangfireSetup.ResolveConnectionString(builder.Configuration);
    builder.Services.AddInvektoHangfire("waanalytics", hangfireConnStr, enableScheduler: false);
    builder.Services.AddScoped<Chatinbox.WhatsAppAnalytics.Services.Jobs.NightlyBatchJob>();

    // Insight engines (RI Faz 3)
    builder.Services.AddSingleton<InsightRepository>();
    builder.Services.AddSingleton<InsightResponseTimeService>();
    builder.Services.AddSingleton<InsightAgentLeaderboardService>();
    builder.Services.AddSingleton<InsightRescueService>();
    builder.Services.AddSingleton<InsightDemandHeatmapService>();
    builder.Services.AddSingleton<InsightRevenueService>();
    builder.Services.AddSingleton<InsightObjectionMapService>();
    builder.Services.AddSingleton<InsightQualityService>();

    // Template mining (RI Faz 4)
    builder.Services.AddSingleton<TemplateRepository>();
    builder.Services.AddSingleton<TemplateMiningService>();

    // Bulk orchestration (RI Faz 5)
    builder.Services.AddSingleton<BulkOrchestrationService>();

    // RI Dashboard + Feedback (RI Faz 6) + Onboarding (RI Faz 7)
    builder.Services.AddSingleton<RiDashboardService>();
    builder.Services.AddSingleton<FeedbackRepository>();
    builder.Services.AddSingleton<OnboardingInsightService>();
}

builder.Services.AddAuthorization();

var app = builder.Build();
if (mssqlConfigured) app.EnsureJobStorageInitialized();

// Ensure upload directory exists
Directory.CreateDirectory(uploadPath);

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/wa/");
app.UseAuthorization();

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// G7 Faz 5: NightlyBatch recurring job (cron derived from config.RunHour).
// Gated on mssqlConfigured — matches AddInvektoHangfire registration above.
if (mssqlConfigured)
{
    var nightlyCfg = app.Services.GetRequiredService<NightlyBatchConfig>();
    var nightlyCron = builder.Configuration["NightlyBatch:Cron"] ?? $"0 {nightlyCfg.RunHour} * * *";
    RecurringJob.AddOrUpdate<Chatinbox.WhatsAppAnalytics.Services.Jobs.NightlyBatchJob>(
        "waanalytics:nightly-batch",
        j => j.RunAsync(CancellationToken.None),
        nightlyCron);
}

// ============================================================
// Helper: Get validated tenant from JWT context
// ============================================================

static TenantContext? GetValidatedTenant(HttpContext ctx, int routeTenantId)
{
    var tenant = ctx.Items["TenantContext"] as TenantContext;
    if (tenant == null || tenant.TenantId != routeTenantId) return null;
    return tenant;
}

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.WhatsAppAnalyticsServiceName)));

app.MapGet("/ready", async ([FromServices] AnalyticsRepository repo) =>
{
    try
    {
        var ok = await repo.CheckConnectionAsync();
        if (!ok)
            return Results.Json(new { status = "unhealthy", error = "Database connection failed" }, statusCode: 503);
        return Results.Ok(HealthResponse.Ok(ServiceConstants.WhatsAppAnalyticsServiceName));
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

// ============================================================
// Upload endpoint: POST /api/v1/wa/{tenantId}/upload
// ============================================================

app.MapPost("/api/v1/wa/{tenantId:int}/upload", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    CsvStreamReader csvReader,
    [FromServices] AnalysisProcessingService processingService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    if (!ctx.Request.HasFormContentType)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvParseError, "Multipart form data required", requestId), statusCode: 400);

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvParseError, "file is required", requestId), statusCode: 400);

    // Validate file size
    var maxBytes = maxFileSizeMb * 1024L * 1024L;
    if (file.Length > maxBytes)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvTooLarge, $"File exceeds {maxFileSizeMb}MB limit", requestId), statusCode: 400);

    // Parse optional delimiter from form
    var delimiterStr = form["delimiter"].FirstOrDefault();
    var delimiter = !string.IsNullOrEmpty(delimiterStr) && delimiterStr.Length == 1 ? delimiterStr[0] : ';';

    // Parse optional config JSON
    var configJsonStr = form["config"].FirstOrDefault();

    string? savedPath = null;
    try
    {
        // Save file to disk
        var safeFileName = $"{Guid.NewGuid():N}.csv";
        var tenantDir = Path.Combine(uploadPath, tenantId.ToString());
        Directory.CreateDirectory(tenantDir);
        savedPath = Path.Combine(tenantDir, safeFileName);

        await using (var fs = new FileStream(savedPath, FileMode.Create))
            await file.CopyToAsync(fs);

        // Validate CSV header
        try
        {
            csvReader.ValidateHeader(savedPath, delimiter);
        }
        catch (InvalidOperationException ex)
        {
            // Clean up file
            if (File.Exists(savedPath)) File.Delete(savedPath);
            return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvInvalidHeader, ex.Message, requestId), statusCode: 400);
        }

        // Build config JSON
        var config = new Dictionary<string, object?>
        {
            ["delimiter"] = delimiter.ToString(),
            ["original_filename"] = file.FileName,
            ["file_size_bytes"] = file.Length
        };
        if (!string.IsNullOrEmpty(configJsonStr))
        {
            try
            {
                var userConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(configJsonStr);
                if (userConfig != null)
                    foreach (var kv in userConfig)
                        config[kv.Key] = kv.Value;
            }
            catch (JsonException ex) { jsonLogger.SystemWarn($"[Upload] Invalid user config JSON, ignoring: {ex.Message}"); }
        }
        var configJson = JsonSerializer.Serialize(config);

        // Create analysis record (store safe filename for restart recovery path reconstruction)
        var analysisId = await repo.CreateAnalysisAsync(tenantId, safeFileName, configJson);

        // Enqueue for background processing
        processingService.EnqueueAnalysis(new AnalysisProcessJob
        {
            AnalysisId = analysisId,
            TenantId = tenantId,
            FilePath = savedPath,
            SourceFileName = safeFileName,
            Delimiter = delimiter
        });

        jsonLogger.StepInfo($"Analysis uploaded: id={analysisId}, tenant={tenantId}, file={file.FileName}, size={file.Length}", requestId);
        return Results.Json(new { analysisId, status = "pending", fileName = file.FileName }, statusCode: 202);
    }
    catch (OperationCanceledException)
    {
        CleanupOrphanedFile();
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        CleanupOrphanedFile();
        jsonLogger.StepError($"[{ErrorCodes.WADatabaseError}] Upload DB write failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, "Failed to save the upload due to a database error. Please retry.", requestId), statusCode: 500);
    }
    catch (Exception ex)
    {
        // Sanctioned storage boundary (arch/codex-context.md): the upload writes a file then a DB row;
        // genuine file/IO failures surface as a coded storage error (DB + cancellation handled above).
        CleanupOrphanedFile();
        jsonLogger.StepError($"[{ErrorCodes.WAStorageError}] Upload failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAStorageError, "File upload failed. Please retry.", requestId), statusCode: 500);
    }

    // Best-effort orphaned-file cleanup (sanctioned broad catch — arch/codex-context.md):
    // a cleanup failure must never mask the original error response.
    void CleanupOrphanedFile()
    {
        if (savedPath != null && File.Exists(savedPath))
        {
            try { File.Delete(savedPath); }
            catch (Exception cleanupEx) { jsonLogger.SystemWarn($"[Upload] Failed to cleanup orphaned file {savedPath}: {cleanupEx.Message}"); }
        }
    }
}).DisableAntiforgery();

// ============================================================
// MSSQL Import endpoint: POST /api/v1/wa/{tenantId}/import-mssql
// ============================================================

app.MapPost("/api/v1/wa/{tenantId:int}/import-mssql", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    [FromServices] AnalysisProcessingService processingService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    if (!mssqlConfigured)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAStorageError, "CustomerMssql is not configured on this server", requestId), statusCode: 503);

    // Parse request body
    string? database;
    int instanceId;
    try
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
        database = body.GetProperty("database").GetString();
        instanceId = body.GetProperty("instanceId").GetInt32();

        if (string.IsNullOrWhiteSpace(database))
            return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvParseError, "database is required", requestId), statusCode: 400);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception)
    {
        // Sanctioned request-body-parse boundary (arch/codex-context.md): a malformed or missing-field
        // body (JsonException, missing property, etc.) is a client error → 400, not a 500.
        return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvParseError, "Invalid request body. Provide a JSON object with 'database' and 'instanceId'.", requestId), statusCode: 400);
    }

    // Build config JSON
    var config = new Dictionary<string, object?>
    {
        ["source_type"] = "mssql",
        ["mssql_database"] = database,
        ["mssql_instance_id"] = instanceId
    };
    var configJson = JsonSerializer.Serialize(config);

    // Create analysis record (source_file_name stores database_instanceId for identification)
    var sourceLabel = $"{database}_{instanceId}";
    var analysisId = await repo.CreateAnalysisAsync(tenantId, sourceLabel, configJson);

    // Enqueue for background processing
    processingService.EnqueueAnalysis(new AnalysisProcessJob
    {
        AnalysisId = analysisId,
        TenantId = tenantId,
        SourceType = "mssql",
        SourceFileName = sourceLabel,
        MssqlDatabase = database,
        MssqlInstanceId = instanceId
    });

    jsonLogger.StepInfo($"MSSQL import started: id={analysisId}, tenant={tenantId}, db={database}, instance={instanceId}", requestId);
    return Results.Json(new { analysisId, status = "pending", database, instanceId }, statusCode: 202);
});

// ============================================================
// Analysis CRUD endpoints
// ============================================================

// List analyses
app.MapGet("/api/v1/wa/{tenantId:int}/analyses", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    [FromServices] JsonLinesLogger jsonLogger,
    int? page,
    int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    var p = Math.Max(page ?? 1, 1);
    var l = Math.Clamp(limit ?? 20, 1, 100);

    try
    {
        var (items, total) = await repo.ListAnalysesAsync(tenantId, p, l);
        return Results.Ok(new { analyses = items, total, page = p, limit = l });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] List analyses failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, "Failed to list analyses due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// Get analysis status
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var analysis = await repo.GetAnalysisAsync(tenantId, analysisId);
        if (analysis == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAAnalysisNotFound, $"Analysis {analysisId} not found", requestId), statusCode: 404);

        return Results.Ok(analysis);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Get analysis failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, "Failed to read the analysis due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// Delete analysis
app.MapDelete("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var deleted = await repo.DeleteAnalysisAsync(tenantId, analysisId);
        if (!deleted)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAAnalysisNotFound, $"Analysis {analysisId} not found", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Analysis deleted: id={analysisId}, tenant={tenantId}", requestId);
        return Results.Ok(new { message = "Analysis deleted", analysisId });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.WADatabaseError}] Delete failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, "Failed to delete the analysis due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// ============================================================
// Metadata endpoint
// ============================================================

app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/metadata", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var metadataJson = await repo.GetMetadataAsync(tenantId, analysisId);
        if (metadataJson == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAAnalysisNotFound, $"Metadata for analysis {analysisId} not found", requestId), statusCode: 404);

        // Return raw JSON
        return Results.Text(metadataJson, "application/json");
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Metadata read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, "Failed to read metadata due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// ============================================================
// NLP Query endpoints (Phase B: PKT-4)
// ============================================================

// Intent distribution
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/intents", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var json = await repo.GetIntentDistributionAsync(tenantId, analysisId);
        return Results.Text(json, "application/json");
    }
    catch (DbException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"Intent query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Sentiment summary
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/sentiments", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var json = await repo.GetSentimentSummaryAsync(tenantId, analysisId);
        return Results.Text(json, "application/json");
    }
    catch (DbException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"Sentiment query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Top products
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/products", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var lim = Math.Clamp(limit ?? 50, 1, 200);
        var json = await repo.GetTopProductsAsync(tenantId, analysisId, lim);
        return Results.Text(json, "application/json");
    }
    catch (DbException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"Products query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Top prices
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/prices", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var lim = Math.Clamp(limit ?? 30, 1, 100);
        var json = await repo.GetTopPricesAsync(tenantId, analysisId, lim);
        return Results.Text(json, "application/json");
    }
    catch (DbException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"Prices query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// FAQ clusters
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/faq-clusters", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo,
    int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var lim = Math.Clamp(limit ?? 50, 1, 200);
        var json = await repo.GetFaqClustersAsync(tenantId, analysisId, lim);
        return Results.Text(json, "application/json");
    }
    catch (DbException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"FAQ clusters query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// NLP summary (aggregate counts)
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/nlp-summary", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    [FromServices] AnalyticsRepository repo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var json = await repo.GetNlpSummaryAsync(tenantId, analysisId);
        return Results.Text(json, "application/json");
    }
    catch (DbException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"NLP summary query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// ============================================================
// LLM Benchmark endpoints (Phase RI) — /api/ops/benchmark/
// Auth: X-Ops-Key header (not JWT — internal ops tool)
// ============================================================

// Helper: validate ops key
bool ValidateOpsKey(HttpContext ctx)
{
    // benchmarkOpsKey is guaranteed non-empty (startup throws otherwise) — never open by default.
    var key = ctx.Request.Headers["X-Ops-Key"].FirstOrDefault();
    return key == benchmarkOpsKey;
}

// POST /api/ops/benchmark/start
app.MapPost("/api/ops/benchmark/start", async (
    HttpContext ctx,
    BenchmarkRepository benchRepo,
    BenchmarkProcessingService? benchProcessor,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    if (benchProcessor == null || !mssqlConfigured)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, "MSSQL not configured — benchmark unavailable", requestId), statusCode: 503);

    BenchmarkStartRequest? req;
    try
    {
        req = await JsonSerializer.DeserializeAsync<BenchmarkStartRequest>(ctx.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req == null || string.IsNullOrWhiteSpace(req.Database))
            return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, "database is required", requestId), statusCode: 400);
    }
    catch (JsonException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, $"Invalid request: {ex.Message}", requestId), statusCode: 400);
    }

    var sampleSize = req.ConversationIds is { Length: > 0 }
        ? req.ConversationIds.Length
        : Math.Clamp(req.SampleSize, 1, 5000);
    var config = JsonSerializer.Serialize(new
    {
        models = req.Models ?? new[] { "claude_haiku", "claude_sonnet", "gemini_flash", "gemini_pro", "gemini_3_flash", "tiered" },
        min_messages = req.MinMessages ?? benchmarkMinMsgs,
        max_messages = req.MaxMessages ?? benchmarkMaxMsgs,
        explicit_conversation_ids = req.ConversationIds?.Length ?? 0
    });

    // tenantId = 0 for ops benchmarks (not tenant-scoped)
    var benchmarkId = await benchRepo.CreateJobAsync(0, req.Database, req.InstanceId, sampleSize, config);

    benchProcessor.EnqueueBenchmark(new BenchmarkProcessJob
    {
        BenchmarkId = benchmarkId,
        TenantId = 0,
        DatabaseName = req.Database,
        InstanceId = req.InstanceId,
        SampleSize = sampleSize,
        MinMessages = req.MinMessages ?? benchmarkMinMsgs,
        MaxMessages = req.MaxMessages ?? benchmarkMaxMsgs,
        Models = req.Models ?? new[] { "claude_haiku", "claude_sonnet", "gemini_flash", "gemini_pro", "gemini_3_flash", "tiered" },
        ConversationIds = req.ConversationIds
    });

    jsonLogger.StepInfo($"Benchmark started: id={benchmarkId}, db={req.Database}, sample={sampleSize}", requestId);
    return Results.Json(new { benchmarkId, status = "pending", database = req.Database, sampleSize }, statusCode: 202);
});

// GET /api/ops/benchmark/{id}
app.MapGet("/api/ops/benchmark/{id:int}", async (
    int id,
    HttpContext ctx,
    BenchmarkRepository benchRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    var job = await benchRepo.GetJobAsync(id);
    if (job == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkNotFound, $"Benchmark {id} not found", requestId), statusCode: 404);

    return Results.Ok(job);
});

// GET /api/ops/benchmark/{id}/results
app.MapGet("/api/ops/benchmark/{id:int}/results", async (
    int id,
    HttpContext ctx,
    BenchmarkRepository benchRepo,
    string? model,
    string? label) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    var results = await benchRepo.GetResultsAsync(id, model, label);
    return Results.Ok(new { results, total = results.Count });
});

// GET /api/ops/benchmark/{id}/metrics
app.MapGet("/api/ops/benchmark/{id:int}/metrics", async (
    int id,
    HttpContext ctx,
    BenchmarkRepository benchRepo,
    MetricsCalculator metricsCalc) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    var results = await benchRepo.GetResultsAsync(id);
    if (results.Count == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkNotFound, $"No results for benchmark {id}", requestId), statusCode: 404);

    var groundTruth = results.Select(r => r.GroundTruthLabel).ToList();
    var hasGroundTruth = groundTruth.Any(g => g != null);

    var metrics = new Dictionary<string, object>();

    // Model metrics (accuracy only if ground truth exists)
    var modelPairs = new (string key, Func<BenchmarkResult, string?> labelGetter)[]
    {
        ("keyword", r => r.KeywordLabel),
        ("claude_haiku", r => r.ClaudeHaikuLabel),
        ("claude_sonnet", r => r.ClaudeSonnetLabel),
        ("gemini_flash", r => r.GeminiFlashLabel),
        ("gemini_pro", r => r.GeminiProLabel),
        ("gemini_3_flash", r => r.Gemini3FlashLabel),
        ("tiered", r => r.TieredLabel)
    };

    foreach (var (key, getter) in modelPairs)
    {
        var predictions = results.Select(getter).ToList();
        var distribution = MetricsCalculator.ComputeDistribution(predictions);
        var classified = predictions.Count(p => p != null);

        if (hasGroundTruth)
        {
            var modelMetrics = metricsCalc.Compute(key, predictions, groundTruth);
            modelMetrics.LabelDistribution = distribution;
            metrics[key] = modelMetrics;
        }
        else
        {
            metrics[key] = new { modelName = key, classified, total = results.Count, labelDistribution = distribution };
        }
    }

    return Results.Ok(new
    {
        benchmarkId = id,
        threadCount = results.Count,
        hasGroundTruth,
        groundTruthCount = groundTruth.Count(g => g != null),
        models = metrics
    });
});

// PUT /api/ops/benchmark/{id}/ground-truth
app.MapPut("/api/ops/benchmark/{id:int}/ground-truth", async (
    int id,
    HttpContext ctx,
    BenchmarkRepository benchRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    GroundTruthRequest? req;
    try
    {
        req = await JsonSerializer.DeserializeAsync<GroundTruthRequest>(ctx.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req == null || req.Labels.Count == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, "labels array is required", requestId), statusCode: 400);
    }
    catch (JsonException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, $"Invalid request: {ex.Message}", requestId), statusCode: 400);
    }

    var updated = await benchRepo.UpdateGroundTruthAsync(id, req.Labels);
    jsonLogger.StepInfo($"Ground truth updated: benchmark={id}, labels={req.Labels.Count}, updated={updated}", requestId);
    return Results.Ok(new { updated, requested = req.Labels.Count });
});

// ============================================================
// Batch Classification endpoints (RI-2.6)
// ============================================================

// POST /api/ops/classify/batch — Start async batch classification
app.MapPost("/api/ops/classify/batch", async (
    HttpContext ctx,
    [FromServices] ConversationOutcomeRepository outcomeRepo,
    BatchClassificationService? batchService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    if (batchService == null || !mssqlConfigured)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, "MSSQL not configured — batch unavailable", requestId), statusCode: 503);

    BatchStartRequest? req;
    try
    {
        req = await JsonSerializer.DeserializeAsync<BatchStartRequest>(ctx.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req == null || string.IsNullOrWhiteSpace(req.Database))
            return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, "database is required", requestId), statusCode: 400);
        if (req.TenantId <= 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, "tenant_id is required", requestId), statusCode: 400);
    }
    catch (JsonException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkInvalidConfig, $"Invalid request: {ex.Message}", requestId), statusCode: 400);
    }

    var jobId = await outcomeRepo.CreateBatchJobAsync(
        req.TenantId, req.Database, req.InstanceId, req.Sector, "manual", req.LookbackDays);

    batchService.Enqueue(new BatchProcessJob
    {
        BatchJobId = jobId,
        TenantId = req.TenantId,
        DatabaseName = req.Database,
        InstanceId = req.InstanceId,
        Sector = req.Sector,
        LookbackDays = req.LookbackDays,
        MaxThreads = req.MaxThreads
    });

    jsonLogger.StepInfo($"Batch job created: id={jobId}, db={req.Database}, tenant={req.TenantId}", requestId);
    return Results.Ok(new { batchJobId = jobId, status = "pending", database = req.Database, tenantId = req.TenantId });
});

// GET /api/ops/classify/batch/{id} — Batch job status
app.MapGet("/api/ops/classify/batch/{id:int}", async (
    int id,
    HttpContext ctx,
    [FromServices] ConversationOutcomeRepository outcomeRepo) =>
{
    if (!ValidateOpsKey(ctx)) return Results.StatusCode(401);
    var job = await outcomeRepo.GetBatchJobAsync(id);
    if (job == null) return Results.NotFound();
    return Results.Ok(job);
});

// GET /api/ops/outcomes/{tenantId}/distribution — Label distribution for a tenant
app.MapGet("/api/ops/outcomes/{tenantId:int}/distribution", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] ConversationOutcomeRepository outcomeRepo) =>
{
    if (!ValidateOpsKey(ctx)) return Results.StatusCode(401);
    var dist = await outcomeRepo.GetLabelDistributionAsync(tenantId);
    return Results.Ok(new { tenantId, distribution = dist, total = dist.Values.Sum() });
});

// GET /api/ops/sectors — List sector configs
app.MapGet("/api/ops/sectors", async (
    HttpContext ctx,
    SectorConfigRepository sectorRepo) =>
{
    if (!ValidateOpsKey(ctx)) return Results.StatusCode(401);
    var sectors = await sectorRepo.GetAllAsync();
    return Results.Ok(sectors);
});

// ============================================================
// Insight Engine endpoints (RI Faz 3)
// ============================================================

// POST /api/ops/insights/compute/response-time — Compute response time correlation
app.MapPost("/api/ops/insights/compute/response-time", async (
    HttpContext ctx,
    [FromServices] InsightResponseTimeService rtService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0 || string.IsNullOrEmpty(request.Database))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId and database are required", requestId), statusCode: 400);

    try
    {
        var result = await rtService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("No classified outcomes"))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNoOutcomes, ex.Message, requestId), statusCode: 404);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): MSSQL source read + PG
        // writes; any failure surfaces as a coded compute-failure rather than a bare 500.
        jsonLogger.SystemError($"[Insight] Response time compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Response time compute failed. Verify source data exists, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/response-time/{tenantId} — Get response time correlation data
app.MapGet("/api/ops/insights/response-time/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var insight = await insightRepo.GetResponseTimeInsightAsync(tenantId, instanceId);
        if (insight.TotalConversations == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No response time data found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Response time read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read response time insight due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/agent-leaderboard — Compute agent leaderboard metrics
app.MapPost("/api/ops/insights/compute/agent-leaderboard", async (
    HttpContext ctx,
    [FromServices] InsightAgentLeaderboardService alService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0 || string.IsNullOrEmpty(request.Database))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId and database are required", requestId), statusCode: 400);

    try
    {
        var result = await alService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): MSSQL source read + PG
        // writes; any failure surfaces as a coded compute-failure rather than a bare 500.
        jsonLogger.SystemError($"[Insight] Agent leaderboard compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Agent leaderboard compute failed. Verify source data exists, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/agent-leaderboard/{tenantId} — Get agent leaderboard data
app.MapGet("/api/ops/insights/agent-leaderboard/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var insight = await insightRepo.GetAgentLeaderboardAsync(tenantId, instanceId);
        if (insight.TotalAgents == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No agent leaderboard data found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Agent leaderboard read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read agent leaderboard due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/rescue — Compute follow-up rescue candidates
app.MapPost("/api/ops/insights/compute/rescue", async (
    HttpContext ctx,
    [FromServices] InsightRescueService rescueService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0 || string.IsNullOrEmpty(request.Database))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId and database are required", requestId), statusCode: 400);

    try
    {
        var result = await rescueService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): MSSQL source read + PG
        // writes; any failure surfaces as a coded compute-failure rather than a bare 500.
        jsonLogger.SystemError($"[Insight] Rescue compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Rescue compute failed. Verify source data exists, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/rescue/{tenantId} — Get rescue candidates
app.MapGet("/api/ops/insights/rescue/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var insight = await insightRepo.GetRescueCandidatesAsync(tenantId, instanceId);
        if (insight.TotalCandidates == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No rescue candidates found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Rescue candidates read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read rescue candidates due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/demand-heatmap — Compute demand heatmap
app.MapPost("/api/ops/insights/compute/demand-heatmap", async (
    HttpContext ctx,
    [FromServices] InsightDemandHeatmapService dhService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0 || string.IsNullOrEmpty(request.Database))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId and database are required", requestId), statusCode: 400);

    try
    {
        var result = await dhService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("No classified outcomes"))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNoOutcomes, ex.Message, requestId), statusCode: 404);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): MSSQL source read + PG
        // writes; any failure surfaces as a coded compute-failure rather than a bare 500.
        jsonLogger.SystemError($"[Insight] Demand heatmap compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Demand heatmap compute failed. Verify source data exists, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/demand-heatmap/{tenantId} — Get demand heatmap data
app.MapGet("/api/ops/insights/demand-heatmap/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var insight = await insightRepo.GetDemandHeatmapAsync(tenantId, instanceId);
        if (insight.TotalConversations == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No demand heatmap data found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Demand heatmap read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read demand heatmap due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/revenue-attribution — Compute revenue attribution (RI-3.4)
app.MapPost("/api/ops/insights/compute/revenue-attribution", async (
    HttpContext ctx,
    [FromServices] InsightRevenueService revService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId is required", requestId), statusCode: 400);

    try
    {
        var result = await revService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("No classified outcomes"))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNoOutcomes, ex.Message, requestId), statusCode: 404);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): MSSQL source read + PG
        // writes; any failure surfaces as a coded compute-failure rather than a bare 500.
        jsonLogger.SystemError($"[Insight] Revenue attribution compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Revenue attribution compute failed. Verify source data exists, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/revenue-attribution/{tenantId} — Get revenue attribution data (RI-3.4)
app.MapGet("/api/ops/insights/revenue-attribution/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    string? dimension = null;
    if (ctx.Request.Query.TryGetValue("dimension", out var dimStr) && !string.IsNullOrEmpty(dimStr))
        dimension = dimStr.ToString();

    try
    {
        var insight = await insightRepo.GetRevenueAttributionAsync(tenantId, instanceId, dimension);
        if (insight.Entries.Count == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No revenue attribution data found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Revenue attribution read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read revenue attribution due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/objection-map — Compute objection map (RI-3.5)
app.MapPost("/api/ops/insights/compute/objection-map", async (
    HttpContext ctx,
    [FromServices] InsightObjectionMapService objService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId is required", requestId), statusCode: 400);

    try
    {
        var result = await objService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): coded compute-failure, not bare 500.
        jsonLogger.SystemError($"[Insight] Objection map compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Objection map compute failed. Verify PG offer_lost outcomes exist, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/objection-map/{tenantId} — Get objection map data (RI-3.5)
app.MapGet("/api/ops/insights/objection-map/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var insight = await insightRepo.GetObjectionMapAsync(tenantId, instanceId);
        if (insight.TotalObjections == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No objection map data found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Objection map read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read objection map due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/quality-score — Compute conversation quality scores (RI-3.7)
app.MapPost("/api/ops/insights/compute/quality-score", async (
    HttpContext ctx,
    [FromServices] InsightQualityService qsService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    InsightComputeRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<InsightComputeRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || request.TenantId <= 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "tenantId is required", requestId), statusCode: 400);

    try
    {
        var result = await qsService.ComputeAsync(request);
        return Results.Ok(new { requestId, result });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("No classified outcomes"))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNoOutcomes, ex.Message, requestId), statusCode: 404);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned compute-orchestration boundary (arch/codex-context.md): coded compute-failure, not bare 500.
        jsonLogger.SystemError($"[Insight] Quality score compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Quality score compute failed. Verify PG outcomes + response times exist, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/insights/quality-score/{tenantId} — Get quality score data (RI-3.7)
app.MapGet("/api/ops/insights/quality-score/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var groupByAgent = ctx.Request.Query.ContainsKey("groupByAgent");

    try
    {
        var insight = await insightRepo.GetQualityInsightAsync(tenantId, instanceId, groupByAgent);
        if (insight.TotalScored == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "No quality score data found. Run compute first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, insight });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.WADatabaseError}] Quality scores read failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read quality scores due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// ============================================================
// Template Mining endpoints (RI Faz 4)
// ============================================================

// POST /api/ops/templates/mine — Mine templates for a sector (RI-4.1 through 4.6)
app.MapPost("/api/ops/templates/mine", async (
    HttpContext ctx,
    [FromServices] TemplateMiningService miningService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    TemplateMineRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<TemplateMineRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || string.IsNullOrWhiteSpace(request.Sector))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "sector is required", requestId), statusCode: 400);

    if (!TemplateMiningService.IsSupportedSector(request.Sector))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            $"Sector '{request.Sector}' not supported. Use one of: {string.Join(", ", TemplateMiningService.GetSupportedSectorList())}", requestId), statusCode: 400);

    try
    {
        var result = await miningService.MineAsync(request.Sector);
        return Results.Ok(new { requestId, result });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned mining-orchestration boundary (arch/codex-context.md): coded failure, not bare 500.
        jsonLogger.SystemError($"[TemplateMining] Mine failed for sector '{request.Sector}': {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            $"Template mining failed for sector '{request.Sector}'. Verify PG template tables exist, then retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/templates/{sector} — Get all templates for a sector
app.MapGet("/api/ops/templates/{sector}", async (
    string sector,
    HttpContext ctx,
    [FromServices] TemplateRepository templateRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    try
    {
        var templates = await templateRepo.GetAllTemplatesBySectorAsync(sector);
        var total = templates.Intents.Count + templates.Faqs.Count + templates.Flows.Count
                  + templates.ObjectionHandlers.Count + templates.FollowupTemplates.Count
                  + templates.OnboardingSteps.Count;

        if (total == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                $"No templates found for sector '{sector}'. Run mine first.", requestId), statusCode: 404);

        return Results.Ok(new { requestId, templates });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[TemplateMining] Get templates failed for sector '{sector}': {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            $"Failed to read templates for sector '{sector}' due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// ============================================================
// Bulk Orchestration endpoints (RI Faz 5)
// ============================================================

// POST /api/ops/templates/mine-all — Mine templates for all (or selected) sectors
app.MapPost("/api/ops/templates/mine-all", async (
    HttpContext ctx,
    [FromServices] BulkOrchestrationService bulkService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    BulkMineRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<BulkMineRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Invalid request body", requestId), statusCode: 400);
    }

    try
    {
        var result = await bulkService.MineAllSectorsAsync(request?.Sectors);
        return Results.Ok(new { requestId, result });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned mining-orchestration boundary (arch/codex-context.md): coded failure, not bare 500.
        jsonLogger.SystemError($"[BulkMine] Bulk mine failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Bulk template mining failed. Please retry.", requestId), statusCode: 500);
    }
});

// GET /api/ops/templates/profiles — Get sector profiles (config + template counts)
app.MapGet("/api/ops/templates/profiles", async (
    HttpContext ctx,
    [FromServices] BulkOrchestrationService bulkService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    if (!ValidateOpsKey(ctx))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WABenchmarkUnauthorized, "Invalid ops key", requestId), statusCode: 401);

    try
    {
        var profiles = await bulkService.GetSectorProfilesAsync();
        return Results.Ok(new { requestId, totalSectors = profiles.Count, profiles });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[BulkMine] Sector profiles failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to load sector profiles due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// ============================================================
// Revenue Intelligence tenant-facing endpoints (RI Faz 6)
// JWT-protected: /api/v1/wa/{tenantId}/ri/*
// ============================================================

// GET /api/v1/wa/{tenantId}/ri/dashboard — All widget data in one call (RI-6.17)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/dashboard", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] RiDashboardService dashboardService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    var sector = ctx.Request.Query["sector"].FirstOrDefault();
    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var dashboard = await dashboardService.GetDashboardAsync(tenantId, sector, instanceId);
        return Results.Ok(new { requestId, dashboard });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned dashboard-aggregation boundary (arch/codex-context.md): coded failure, not bare 500.
        jsonLogger.SystemError($"[RiDashboard] Dashboard failed for tenant {tenantId}: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Dashboard aggregation failed. Please retry.", requestId), statusCode: 500);
    }
});

// GET /api/v1/wa/{tenantId}/ri/revenue — Lost revenue detail (RI-6.18)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/revenue", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;
    var dimension = ctx.Request.Query["dimension"].FirstOrDefault();

    try
    {
        var data = await insightRepo.GetRevenueAttributionAsync(tenantId, instanceId, dimension);
        return Results.Ok(new { requestId, data });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[RiDashboard] Revenue failed for tenant {tenantId}: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to read revenue data due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// GET /api/v1/wa/{tenantId}/ri/agents — Agent leaderboard (RI-6.19)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/agents", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var data = await insightRepo.GetAgentLeaderboardAsync(tenantId, instanceId);
    if (data.TotalAgents == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
            "No agent data found. Run insight compute first.", requestId), statusCode: 404);
    return Results.Ok(new { requestId, data });
});

// GET /api/v1/wa/{tenantId}/ri/objections — Objection map (RI-6.20)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/objections", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var data = await insightRepo.GetObjectionMapAsync(tenantId, instanceId);
    if (data.TotalObjections == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
            "No objection data found. Run insight compute first.", requestId), statusCode: 404);
    return Results.Ok(new { requestId, data });
});

// GET /api/v1/wa/{tenantId}/ri/response-time — Response time correlation (RI-6.21)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/response-time", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var data = await insightRepo.GetResponseTimeInsightAsync(tenantId, instanceId);
    if (data.TotalConversations == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
            "No response time data found. Run insight compute first.", requestId), statusCode: 404);
    return Results.Ok(new { requestId, data });
});

// GET /api/v1/wa/{tenantId}/ri/rescue — Rescue candidates (RI-6.22)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/rescue", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var data = await insightRepo.GetRescueCandidatesAsync(tenantId, instanceId);
    return Results.Ok(new { requestId, data });
});

// GET /api/v1/wa/{tenantId}/ri/quality — Quality scores (RI-6.23)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/quality", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var data = await insightRepo.GetQualityInsightAsync(tenantId, instanceId);
    if (data.TotalScored == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
            "No quality data found. Run insight compute first.", requestId), statusCode: 404);
    return Results.Ok(new { requestId, data });
});

// GET /api/v1/wa/{tenantId}/ri/demand — Service demand heatmap (RI-6.24)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/demand", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    var data = await insightRepo.GetDemandHeatmapAsync(tenantId, instanceId);
    if (data.TotalConversations == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
            "No demand data found. Run insight compute first.", requestId), statusCode: 404);
    return Results.Ok(new { requestId, data });
});

// GET /api/v1/wa/{tenantId}/ri/templates — Sector templates (RI-6.25)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/templates", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] TemplateRepository templateRepo) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    var sector = ctx.Request.Query["sector"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(sector))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "sector query parameter required", requestId), statusCode: 400);

    var data = await templateRepo.GetAllTemplatesBySectorAsync(sector);
    return Results.Ok(new { requestId, data });
});

// POST /api/v1/wa/{tenantId}/ri/templates/{type} — Create a sector template (RI-6.10-14)
app.MapPost("/api/v1/wa/{tenantId:int}/ri/templates/{type}", async (
    int tenantId,
    string type,
    HttpContext ctx,
    [FromServices] TemplateRepository templateRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        long id;
        switch (type.ToLowerInvariant())
        {
            case "intent":
            {
                var r = await ctx.Request.ReadFromJsonAsync<SectorIntentRecord>();
                if (r == null || string.IsNullOrWhiteSpace(r.IntentName) || string.IsNullOrWhiteSpace(r.Sector))
                    return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector and intentName required", requestId), statusCode: 400);
                r.IsActive = true;
                id = await templateRepo.InsertIntentAsync(r, ctx.RequestAborted);
                break;
            }
            case "faq":
            {
                var r = await ctx.Request.ReadFromJsonAsync<SectorFaqRecord>();
                if (r == null || string.IsNullOrWhiteSpace(r.Question) || string.IsNullOrWhiteSpace(r.Sector))
                    return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector and question required", requestId), statusCode: 400);
                r.IsActive = true;
                id = await templateRepo.InsertFaqAsync(r, ctx.RequestAborted);
                break;
            }
            case "objection_handler":
            {
                var r = await ctx.Request.ReadFromJsonAsync<SectorObjectionHandlerRecord>();
                if (r == null || string.IsNullOrWhiteSpace(r.ObjectionType) || string.IsNullOrWhiteSpace(r.Sector))
                    return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector and objectionType required", requestId), statusCode: 400);
                r.IsActive = true;
                id = await templateRepo.InsertObjectionHandlerAsync(r, ctx.RequestAborted);
                break;
            }
            case "followup_template":
            {
                var r = await ctx.Request.ReadFromJsonAsync<SectorFollowupTemplateRecord>();
                if (r == null || string.IsNullOrWhiteSpace(r.TemplateType) || string.IsNullOrWhiteSpace(r.Sector))
                    return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector and templateType required", requestId), statusCode: 400);
                r.IsActive = true;
                id = await templateRepo.InsertFollowupTemplateAsync(r, ctx.RequestAborted);
                break;
            }
            default:
                return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation,
                    "Unknown type. Use: intent, faq, objection_handler, followup_template", requestId), statusCode: 400);
        }

        jsonLogger.StepInfo($"[RiTemplates] Created {type} id={id} by tenant {tenantId}", requestId);
        return Results.Json(new { requestId, type, id }, statusCode: 201);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned write-operation boundary (arch/codex-context.md): body parse + DB insert; coded failure, not bare 500.
        jsonLogger.SystemError($"[RiTemplates] {ErrorCodes.WATemplateCreateFailed} Create {type} failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WATemplateCreateFailed,
            "Template create failed. Verify the request body, then retry.", requestId), statusCode: 500);
    }
});

// PUT /api/v1/wa/{tenantId}/ri/templates/{type}/{id} (extended) — Update a sector template (RI-6.10-14)
// Note: This is the EXTENDED version that handles both toggle (isActive only) and full update.
// The original toggle endpoint remains compatible — if body has only isActive, it toggles.
// If body has more fields, it does a full update.
app.MapPut("/api/v1/wa/{tenantId:int}/ri/templates/{type}/{id:long}", async (
    int tenantId,
    string type,
    long id,
    HttpContext ctx,
    [FromServices] TemplateRepository templateRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        // Read body as raw JSON to detect if it's a simple toggle or full update
        string bodyText;
        using (var reader = new StreamReader(ctx.Request.Body))
            bodyText = await reader.ReadToEndAsync();
        using var bodyDoc = JsonDocument.Parse(bodyText);
        var bodyJson = bodyDoc.RootElement;

        // Simple toggle: body has only isActive
        if (bodyJson.TryGetProperty("isActive", out var activeVal) &&
            bodyJson.EnumerateObject().Count() <= 1)
        {
            await templateRepo.ToggleTemplateActiveAsync(type, id, activeVal.GetBoolean(), ctx.RequestAborted);
            jsonLogger.StepInfo($"[RiTemplates] Toggled {type}/{id} active={activeVal.GetBoolean()} by tenant {tenantId}", requestId);
            return Results.Ok(new { requestId, type, id, isActive = activeVal.GetBoolean() });
        }

        // Full update
        bool updated;
        switch (type.ToLowerInvariant())
        {
            case "intent":
            {
                var r = JsonSerializer.Deserialize<SectorIntentRecord>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (r == null) return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid body", requestId), statusCode: 400);
                updated = await templateRepo.UpdateIntentAsync(id, r, ctx.RequestAborted);
                break;
            }
            case "faq":
            {
                var r = JsonSerializer.Deserialize<SectorFaqRecord>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (r == null) return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid body", requestId), statusCode: 400);
                updated = await templateRepo.UpdateFaqAsync(id, r, ctx.RequestAborted);
                break;
            }
            case "objection_handler":
            {
                var r = JsonSerializer.Deserialize<SectorObjectionHandlerRecord>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (r == null) return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid body", requestId), statusCode: 400);
                updated = await templateRepo.UpdateObjectionHandlerAsync(id, r, ctx.RequestAborted);
                break;
            }
            case "followup_template":
            {
                var r = JsonSerializer.Deserialize<SectorFollowupTemplateRecord>(bodyText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (r == null) return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid body", requestId), statusCode: 400);
                updated = await templateRepo.UpdateFollowupTemplateAsync(id, r, ctx.RequestAborted);
                break;
            }
            default:
                return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation,
                    "Unknown type. Use: intent, faq, objection_handler, followup_template", requestId), statusCode: 400);
        }

        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                $"Template {type}/{id} not found", requestId), statusCode: 404);

        jsonLogger.StepInfo($"[RiTemplates] Updated {type}/{id} by tenant {tenantId}", requestId);
        return Results.Ok(new { requestId, type, id });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned write-operation boundary (arch/codex-context.md): body parse + DB update/toggle; coded failure, not bare 500.
        jsonLogger.SystemError($"[RiTemplates] {ErrorCodes.WATemplateUpdateFailed} Update {type}/{id} failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WATemplateUpdateFailed,
            "Template update failed. Verify the request body, then retry.", requestId), statusCode: 500);
    }
});

// DELETE /api/v1/wa/{tenantId}/ri/templates/{type}/{id} — Soft-delete a sector template (RI-6.10-14)
app.MapDelete("/api/v1/wa/{tenantId:int}/ri/templates/{type}/{id:long}", async (
    int tenantId,
    string type,
    long id,
    HttpContext ctx,
    [FromServices] TemplateRepository templateRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        await templateRepo.ToggleTemplateActiveAsync(type, id, false, ctx.RequestAborted);
        jsonLogger.StepInfo($"[RiTemplates] Soft-deleted {type}/{id} by tenant {tenantId}", requestId);
        return Results.Ok(new { requestId, type, id, deleted = true });
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
            ex.Message, requestId), statusCode: 404);
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[RiTemplates] {ErrorCodes.WADatabaseError} Delete {type}/{id} failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to delete the template due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// PUT /api/v1/wa/{tenantId}/ri/rescue/{conversationId}/status — Update rescue candidate status (RI-8.10)
app.MapPut("/api/v1/wa/{tenantId:int}/ri/rescue/{conversationId}/status", async (
    int tenantId,
    string conversationId,
    HttpContext ctx,
    [FromServices] InsightRepository insightRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    string status;
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, string>>();
        status = body?.GetValueOrDefault("status") ?? "";
    }
    catch (JsonException ex)
    {
        jsonLogger.SystemWarn($"[Rescue] Invalid status body: {ex.Message}");
        status = "";
    }

    var validStatuses = new HashSet<string> { "pending", "triggered", "contacted", "resolved", "expired" };
    if (!validStatuses.Contains(status))
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation,
            $"Invalid status. Valid: {string.Join(", ", validStatuses)}", requestId), statusCode: 400);

    try
    {
        var updated = await insightRepo.UpdateRescueStatusAsync(tenantId, conversationId, status, ctx.RequestAborted);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightNotFound,
                "Rescue candidate not found", requestId), statusCode: 404);

        jsonLogger.StepInfo($"[Rescue] Status updated: tenant={tenantId}, conv={conversationId}, status={status}", requestId);
        return Results.Ok(new { requestId, conversationId, status });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[Rescue] {ErrorCodes.WADatabaseError} Status update failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to update rescue status due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// POST /api/v1/wa/{tenantId}/ri/feedback — Submit agree/disagree (RI-6.27)
app.MapPost("/api/v1/wa/{tenantId:int}/ri/feedback", async (
    int tenantId,
    HttpContext ctx,
    FeedbackRepository feedbackRepo,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    FeedbackRequest? request;
    try
    {
        request = await ctx.Request.ReadFromJsonAsync<FeedbackRequest>();
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Invalid request body", requestId), statusCode: 400);
    }

    if (request is null || string.IsNullOrWhiteSpace(request.ConversationId) || string.IsNullOrWhiteSpace(request.OriginalLabel))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "conversationId and originalLabel are required", requestId), statusCode: 400);

    try
    {
        await feedbackRepo.UpsertFeedbackAsync(tenantId, tenant.UserId, request);
        jsonLogger.StepInfo($"[RiDashboard] Feedback: tenant={tenantId} conv={request.ConversationId} agree={request.IsAgree}", requestId);
        return Results.Ok(new { requestId, saved = true });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (NpgsqlException ex)
    {
        jsonLogger.SystemError($"[RiDashboard] Feedback save failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError,
            "Failed to save feedback due to a database error. Please retry.", requestId), statusCode: 500);
    }
});

// GET /api/v1/wa/{tenantId}/ri/benchmarks — Sector benchmarks (RI-6.28)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/benchmarks", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] RiDashboardService dashboardService) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    var sector = ctx.Request.Query["sector"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(sector))
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "sector query parameter required", requestId), statusCode: 400);

    var benchmarks = await dashboardService.GetBenchmarksAsync(sector);
    return Results.Ok(new { requestId, benchmarks });
});

// GET /api/v1/wa/{tenantId}/ri/onboarding — Onboarding insight (RI-7.2~7.6)
app.MapGet("/api/v1/wa/{tenantId:int}/ri/onboarding", async (
    int tenantId,
    HttpContext ctx,
    [FromServices] OnboardingInsightService onboardingService,
    [FromServices] JsonLinesLogger jsonLogger) =>
{
    var requestId = Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
            "Token tenant does not match route tenant", requestId), statusCode: 403);

    var sector = ctx.Request.Query["sector"].FirstOrDefault();
    int? instanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var iidStr) && int.TryParse(iidStr, out var iid))
        instanceId = iid;

    try
    {
        var data = await onboardingService.GetOnboardingAsync(tenantId, sector, instanceId);
        return Results.Ok(new { requestId, data });
    }
    catch (OperationCanceledException)
    {
        return Results.StatusCode(499);
    }
    catch (Exception ex)
    {
        // Sanctioned aggregation boundary (arch/codex-context.md): coded failure, not bare 500.
        jsonLogger.SystemError($"[RiOnboarding] Onboarding insight failed for tenant {tenantId}: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Onboarding insight failed. Please retry.", requestId), statusCode: 500);
    }
});

// ============================================================
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Ready check (DB connection)", Auth = "none", Category = "Health" },
        new() { Method = "POST", Path = "/api/v1/wa/{tenantId}/upload", Description = "Upload CSV and start analysis", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "POST", Path = "/api/v1/wa/{tenantId}/import-mssql", Description = "Import from customer MSSQL DB", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses", Description = "List analyses (paginated)", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}", Description = "Get analysis status", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "DELETE", Path = "/api/v1/wa/{tenantId}/analyses/{id}", Description = "Delete analysis + cascade data", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/metadata", Description = "Get full metadata JSON", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/intents", Description = "Intent distribution (NLP)", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/sentiments", Description = "Sentiment summary (NLP)", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/products", Description = "Top product codes", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/prices", Description = "Top price mentions", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/faq-clusters", Description = "FAQ question clusters", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/nlp-summary", Description = "NLP aggregate summary", Auth = "Bearer JWT", Category = "Query" },
        new() { Method = "POST", Path = "/api/ops/benchmark/start", Description = "Start LLM benchmark", Auth = "X-Ops-Key", Category = "Benchmark" },
        new() { Method = "GET", Path = "/api/ops/benchmark/{id}", Description = "Get benchmark status", Auth = "X-Ops-Key", Category = "Benchmark" },
        new() { Method = "GET", Path = "/api/ops/benchmark/{id}/results", Description = "Get benchmark results", Auth = "X-Ops-Key", Category = "Benchmark" },
        new() { Method = "GET", Path = "/api/ops/benchmark/{id}/metrics", Description = "Get comparison metrics", Auth = "X-Ops-Key", Category = "Benchmark" },
        new() { Method = "PUT", Path = "/api/ops/benchmark/{id}/ground-truth", Description = "Update ground truth labels", Auth = "X-Ops-Key", Category = "Benchmark" },
        new() { Method = "POST", Path = "/api/ops/classify/batch", Description = "Start async batch classification", Auth = "X-Ops-Key", Category = "Batch" },
        new() { Method = "GET", Path = "/api/ops/classify/batch/{id}", Description = "Batch job status", Auth = "X-Ops-Key", Category = "Batch" },
        new() { Method = "GET", Path = "/api/ops/outcomes/{tenantId}/distribution", Description = "Outcome label distribution", Auth = "X-Ops-Key", Category = "Batch" },
        new() { Method = "GET", Path = "/api/ops/sectors", Description = "List sector configs", Auth = "X-Ops-Key", Category = "Batch" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/response-time", Description = "Compute response time correlation", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/response-time/{tenantId}", Description = "Get response time insight", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/agent-leaderboard", Description = "Compute agent leaderboard metrics", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/agent-leaderboard/{tenantId}", Description = "Get agent leaderboard", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/rescue", Description = "Compute follow-up rescue candidates", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/rescue/{tenantId}", Description = "Get rescue candidates", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/demand-heatmap", Description = "Compute demand heatmap", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/demand-heatmap/{tenantId}", Description = "Get demand heatmap", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/revenue-attribution", Description = "Compute revenue attribution", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/revenue-attribution/{tenantId}", Description = "Get revenue attribution", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/objection-map", Description = "Compute objection map", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/objection-map/{tenantId}", Description = "Get objection map", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/insights/compute/quality-score", Description = "Compute quality scores", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "GET", Path = "/api/ops/insights/quality-score/{tenantId}", Description = "Get quality scores", Auth = "X-Ops-Key", Category = "Insight" },
        new() { Method = "POST", Path = "/api/ops/templates/mine", Description = "Mine sector templates", Auth = "X-Ops-Key", Category = "Template" },
        new() { Method = "GET", Path = "/api/ops/templates/{sector}", Description = "Get sector templates", Auth = "X-Ops-Key", Category = "Template" },
        new() { Method = "POST", Path = "/api/ops/templates/mine-all", Description = "Bulk mine all sectors", Auth = "X-Ops-Key", Category = "Bulk" },
        new() { Method = "GET", Path = "/api/ops/templates/profiles", Description = "Get sector profiles", Auth = "X-Ops-Key", Category = "Bulk" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/dashboard", Description = "RI dashboard aggregate", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/revenue", Description = "Lost revenue detail", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/agents", Description = "Agent leaderboard", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/objections", Description = "Objection map", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/response-time", Description = "Response time correlation", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/rescue", Description = "Rescue candidates", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/quality", Description = "Quality scores", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/demand", Description = "Service demand heatmap", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/templates", Description = "Sector templates", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "PUT", Path = "/api/v1/wa/{tenantId}/ri/templates/{type}/{id}", Description = "Toggle template active", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "POST", Path = "/api/v1/wa/{tenantId}/ri/feedback", Description = "Submit label feedback", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/benchmarks", Description = "Sector benchmarks", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/ri/onboarding", Description = "Onboarding insight + checklist", Auth = "Bearer JWT", Category = "RI" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery", Auth = "none", Category = "Ops" }
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.WhatsAppAnalyticsServiceName,
        Port = ServiceConstants.WhatsAppAnalyticsPort,
        Endpoints = endpoints
    });
});

// ============================================================
// Start
// ============================================================

logger.SystemInfo($"WhatsApp Analytics Service starting on port {listenPort}");
app.Run();
