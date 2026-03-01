using System.Data.Common;
using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Invekto.WhatsAppAnalytics.Services;
using Invekto.WhatsAppAnalytics.Services.Benchmark;
using Invekto.WhatsAppAnalytics.Services.Insights;
using Invekto.WhatsAppAnalytics.Services.Pipeline;

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
            sp,
            sp.GetRequiredService<JsonLinesLogger>(),
            benchmarkDelay,
            benchmarkMaxTextLen,
            benchmarkMinMsgs,
            benchmarkMaxMsgs));
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BatchClassificationService>());

    // Nightly batch job
    var nightlyConfig = new NightlyBatchConfig();
    builder.Configuration.GetSection("NightlyBatch").Bind(nightlyConfig);
    builder.Services.AddSingleton(nightlyConfig);
    builder.Services.AddHostedService<NightlyBatchJob>();

    // Insight engines (RI Faz 3)
    builder.Services.AddSingleton<InsightRepository>();
    builder.Services.AddSingleton<InsightResponseTimeService>();
    builder.Services.AddSingleton<InsightAgentLeaderboardService>();
    builder.Services.AddSingleton<InsightRescueService>();
}

var app = builder.Build();

// Ensure upload directory exists
Directory.CreateDirectory(uploadPath);

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/wa/");

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

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

app.MapGet("/ready", async (AnalyticsRepository repo) =>
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
    AnalyticsRepository repo,
    CsvStreamReader csvReader,
    AnalysisProcessingService processingService,
    JsonLinesLogger jsonLogger) =>
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
    catch (Exception ex)
    {
        // Cleanup orphaned file on failure
        if (savedPath != null && File.Exists(savedPath))
        {
            try { File.Delete(savedPath); }
            catch (Exception cleanupEx) { jsonLogger.SystemWarn($"[Upload] Failed to cleanup orphaned file {savedPath}: {cleanupEx.Message}"); }
        }
        jsonLogger.StepError($"[{ErrorCodes.WAStorageError}] Upload failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAStorageError, "File upload failed", requestId), statusCode: 500);
    }
}).DisableAntiforgery();

// ============================================================
// MSSQL Import endpoint: POST /api/v1/wa/{tenantId}/import-mssql
// ============================================================

app.MapPost("/api/v1/wa/{tenantId:int}/import-mssql", async (
    int tenantId,
    HttpContext ctx,
    AnalyticsRepository repo,
    AnalysisProcessingService processingService,
    JsonLinesLogger jsonLogger) =>
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WACsvParseError, $"Invalid request body: {ex.Message}", requestId), statusCode: 400);
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
    AnalyticsRepository repo,
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"List failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Get analysis status
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    AnalyticsRepository repo) =>
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"Get failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Delete analysis
app.MapDelete("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    AnalyticsRepository repo,
    JsonLinesLogger jsonLogger) =>
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
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.WAAnalysisDeleteFailed}] Delete failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAAnalysisDeleteFailed, "Analysis delete failed", requestId), statusCode: 500);
    }
});

// ============================================================
// Metadata endpoint
// ============================================================

app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/metadata", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    AnalyticsRepository repo) =>
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WADatabaseError, $"Metadata query failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// ============================================================
// NLP Query endpoints (Phase B: PKT-4)
// ============================================================

// Intent distribution
app.MapGet("/api/v1/wa/{tenantId:int}/analyses/{analysisId:int}/intents", async (
    int tenantId, int analysisId,
    HttpContext ctx,
    AnalyticsRepository repo) =>
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
    AnalyticsRepository repo) =>
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
    AnalyticsRepository repo,
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
    AnalyticsRepository repo,
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
    AnalyticsRepository repo,
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
    AnalyticsRepository repo) =>
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
    if (string.IsNullOrEmpty(benchmarkOpsKey)) return true; // No key = open (dev mode)
    var key = ctx.Request.Headers["X-Ops-Key"].FirstOrDefault();
    return key == benchmarkOpsKey;
}

// POST /api/ops/benchmark/start
app.MapPost("/api/ops/benchmark/start", async (
    HttpContext ctx,
    BenchmarkRepository benchRepo,
    BenchmarkProcessingService? benchProcessor,
    JsonLinesLogger jsonLogger) =>
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
    JsonLinesLogger jsonLogger) =>
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
    ConversationOutcomeRepository outcomeRepo,
    BatchClassificationService? batchService,
    JsonLinesLogger jsonLogger) =>
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
    ConversationOutcomeRepository outcomeRepo) =>
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
    ConversationOutcomeRepository outcomeRepo) =>
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
    InsightResponseTimeService rtService,
    JsonLinesLogger jsonLogger) =>
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
    catch (Exception ex)
    {
        jsonLogger.SystemError($"[Insight] Response time compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Compute failed", requestId, ex.Message), statusCode: 500);
    }
});

// GET /api/ops/insights/response-time/{tenantId} — Get response time correlation data
app.MapGet("/api/ops/insights/response-time/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    InsightRepository insightRepo) =>
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Failed to read insight data", requestId, ex.Message), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/agent-leaderboard — Compute agent leaderboard metrics
app.MapPost("/api/ops/insights/compute/agent-leaderboard", async (
    HttpContext ctx,
    InsightAgentLeaderboardService alService,
    JsonLinesLogger jsonLogger) =>
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
    catch (Exception ex)
    {
        jsonLogger.SystemError($"[Insight] Agent leaderboard compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Compute failed", requestId, ex.Message), statusCode: 500);
    }
});

// GET /api/ops/insights/agent-leaderboard/{tenantId} — Get agent leaderboard data
app.MapGet("/api/ops/insights/agent-leaderboard/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    InsightRepository insightRepo) =>
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Failed to read agent leaderboard data", requestId, ex.Message), statusCode: 500);
    }
});

// POST /api/ops/insights/compute/rescue — Compute follow-up rescue candidates
app.MapPost("/api/ops/insights/compute/rescue", async (
    HttpContext ctx,
    InsightRescueService rescueService,
    JsonLinesLogger jsonLogger) =>
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
    catch (Exception ex)
    {
        jsonLogger.SystemError($"[Insight] Rescue compute failed: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed, "Compute failed", requestId, ex.Message), statusCode: 500);
    }
});

// GET /api/ops/insights/rescue/{tenantId} — Get rescue candidates
app.MapGet("/api/ops/insights/rescue/{tenantId:int}", async (
    int tenantId,
    HttpContext ctx,
    InsightRepository insightRepo) =>
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
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.WAInsightComputeFailed,
            "Failed to read rescue candidate data", requestId, ex.Message), statusCode: 500);
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
