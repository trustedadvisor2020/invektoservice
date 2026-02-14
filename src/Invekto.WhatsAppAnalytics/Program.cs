using System.Text.Json;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;
using Invekto.WhatsAppAnalytics.Services;
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

// Validate required config
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
    options.Limits.MaxRequestBodySize = maxFileSizeMb * 1024L * 1024L;
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

// Register pipeline services
builder.Services.AddSingleton<CsvStreamReader>();
builder.Services.AddSingleton<TextNormalizer>();
builder.Services.AddSingleton<CleanerService>();
builder.Services.AddSingleton<ThreaderService>(sp =>
    new ThreaderService(
        sp.GetRequiredService<AnalyticsRepository>(),
        sp.GetRequiredService<AnalyticsConnectionFactory>(),
        sp.GetRequiredService<TextNormalizer>(),
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddSingleton<StatsService>();
builder.Services.AddSingleton<PipelineOrchestrator>();

// Register background processing service
builder.Services.AddSingleton<AnalysisProcessingService>(sp =>
    new AnalysisProcessingService(
        sp.GetRequiredService<AnalyticsRepository>(),
        sp.GetRequiredService<PipelineOrchestrator>(),
        sp.GetRequiredService<JsonLinesLogger>(),
        uploadPath));
builder.Services.AddHostedService(sp => sp.GetRequiredService<AnalysisProcessingService>());

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
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Ready check (DB connection)", Auth = "none", Category = "Health" },
        new() { Method = "POST", Path = "/api/v1/wa/{tenantId}/upload", Description = "Upload CSV and start analysis", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses", Description = "List analyses (paginated)", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}", Description = "Get analysis status", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "DELETE", Path = "/api/v1/wa/{tenantId}/analyses/{id}", Description = "Delete analysis + cascade data", Auth = "Bearer JWT", Category = "Pipeline" },
        new() { Method = "GET", Path = "/api/v1/wa/{tenantId}/analyses/{id}/metadata", Description = "Get full metadata JSON", Auth = "Bearer JWT", Category = "Query" },
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
