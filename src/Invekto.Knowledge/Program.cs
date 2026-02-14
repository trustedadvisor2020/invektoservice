using System.Text.Json;
using Invekto.Knowledge.Data;
using Invekto.Knowledge.Services;
using Invekto.Shared.Middleware;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs;
using Invekto.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.KnowledgePort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";

// OpenAI settings
var openAiKey = builder.Configuration["OpenAI:ApiKey"] ?? "";
var openAiModel = builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-large";
var openAiDimensions = builder.Configuration.GetValue<int>("OpenAI:EmbeddingDimensions", 3072);
var openAiTimeoutMs = builder.Configuration.GetValue<int>("OpenAI:TimeoutMs", 10000);
var openAiMaxRetries = builder.Configuration.GetValue<int>("OpenAI:MaxRetries", 1);

// JWT settings
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";

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

// Warn (not fatal) if OpenAI key is missing -- keyword search still works
if (string.IsNullOrEmpty(openAiKey) || openAiKey.StartsWith("REPLACE_"))
{
    Console.WriteLine("WARN: OpenAI:ApiKey not configured -- semantic search disabled, keyword fallback active");
}

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);
});

// Register logger
var logger = new JsonLinesLogger(ServiceConstants.KnowledgeServiceName, logPath);
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

// Register PostgreSQL connection factory (pgvector-aware)
var pgFactory = new KnowledgeConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register repository
builder.Services.AddSingleton<KnowledgeRepository>();

// Register import service
builder.Services.AddSingleton<ImportService>();

// Register embedding service with HttpClient
builder.Services.AddSingleton<EmbeddingService>(sp =>
{
    var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(openAiTimeoutMs) };
    return new EmbeddingService(httpClient, logger, openAiKey, openAiModel, openAiDimensions, openAiMaxRetries);
});

// Register retrieval service
builder.Services.AddSingleton<RetrievalService>();

// Phase B: Processing config
var chunkSize = builder.Configuration.GetValue<int>("Processing:ChunkSize", 512);
var chunkOverlap = builder.Configuration.GetValue<int>("Processing:ChunkOverlap", 50);
var maxFileSizeMb = builder.Configuration.GetValue<int>("Processing:MaxFileSizeMb", 10);
var uploadPath = builder.Configuration["Storage:UploadPath"] ?? "uploads";

// Register PDF chunking service
builder.Services.AddSingleton(sp =>
    new PdfChunkingService(chunkSize, chunkOverlap, sp.GetRequiredService<JsonLinesLogger>()));

// Register document processing background service
builder.Services.AddSingleton<DocumentProcessingService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DocumentProcessingService>());

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/knowledge/");

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

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.KnowledgeServiceName)));
app.MapGet("/ready", async (KnowledgeRepository repo) =>
{
    try
    {
        var hasPgvector = await repo.CheckPgvectorAsync();
        if (!hasPgvector)
            return Results.Json(new { status = "unhealthy", error = "pgvector extension not installed" }, statusCode: 503);
        return Results.Ok(HealthResponse.Ok(ServiceConstants.KnowledgeServiceName));
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

// ============================================================
// Import endpoint (WA-3)
// ============================================================

app.MapPost("/api/v1/knowledge/{tenantId:int}/import", async (
    int tenantId,
    HttpContext ctx,
    ImportService importService,
    JsonLinesLogger jsonLogger,
    HttpRequest request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    ImportRequest? body;
    try
    {
        body = await request.ReadFromJsonAsync<ImportRequest>();
    }
    catch (JsonException ex)
    {
        jsonLogger.SystemWarn($"[{ErrorCodes.KnowledgeInvalidRequest}] Invalid import request JSON: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "Invalid JSON request body", requestId), statusCode: 400);
    }

    if (body == null || string.IsNullOrWhiteSpace(body.NlpOutputPath))
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "nlpOutputPath is required", requestId), statusCode: 400);

    if (!Directory.Exists(body.NlpOutputPath))
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportPathNotFound, $"Path not found: {body.NlpOutputPath}", requestId), statusCode: 400);

    var minQuestions = body.FaqFilter?.MinQuestions ?? 5;
    var minAnswerLength = body.FaqFilter?.MinAnswerLength ?? 20;

    try
    {
        var summary = await importService.ImportNlpDataAsync(tenantId, body.NlpOutputPath, minQuestions, minAnswerLength);
        return Results.Ok(summary);
    }
    catch (InvalidOperationException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportParseError}] Import parse error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportParseError, ex.Message, requestId), statusCode: 400);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportDbError}] Import DB error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, "Database import failed", requestId), statusCode: 500);
    }
});

// ============================================================
// Search endpoint (Retrieval API)
// ============================================================

app.MapPost("/api/v1/knowledge/{tenantId:int}/search", async (
    int tenantId,
    HttpContext ctx,
    RetrievalService retrievalService,
    JsonLinesLogger jsonLogger,
    HttpRequest request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    SearchRequest? body;
    try
    {
        body = await request.ReadFromJsonAsync<SearchRequest>();
    }
    catch (JsonException ex)
    {
        jsonLogger.SystemWarn($"[{ErrorCodes.KnowledgeInvalidRequest}] Invalid search request JSON: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "Invalid JSON request body", requestId), statusCode: 400);
    }

    if (body == null || string.IsNullOrWhiteSpace(body.Query))
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "query is required", requestId), statusCode: 400);

    var rawTopK = body.TopK ?? 5;
    if (rawTopK < 1 || rawTopK > 50)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "topK must be between 1 and 50", requestId), statusCode: 400);

    var topK = rawTopK;

    try
    {
        var response = await retrievalService.SearchAsync(tenantId, body.Query, topK, body.Lang, body.Category);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeSearchFailed}] Search failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeSearchFailed, "Search failed", requestId), statusCode: 500);
    }
});

// ============================================================
// FAQ CRUD endpoints
// ============================================================

// List FAQs
app.MapGet("/api/v1/knowledge/{tenantId:int}/faqs", async (
    int tenantId,
    HttpContext ctx,
    KnowledgeRepository repo,
    string? lang,
    string? category,
    int? page,
    int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    var p = Math.Max(page ?? 1, 1);
    var l = Math.Clamp(limit ?? 50, 1, 200);

    try
    {
        var (faqs, total) = await repo.ListFaqsAsync(tenantId, lang, category, p, l);
        return Results.Ok(new { faqs, total, page = p, limit = l });
    }
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeSearchFailed, $"List failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Create FAQ
app.MapPost("/api/v1/knowledge/{tenantId:int}/faqs", async (
    int tenantId,
    HttpContext ctx,
    KnowledgeRepository repo,
    JsonLinesLogger jsonLogger,
    HttpRequest request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    FaqCreateRequest? body;
    try
    {
        body = await request.ReadFromJsonAsync<FaqCreateRequest>();
    }
    catch (JsonException ex)
    {
        jsonLogger.SystemWarn($"[{ErrorCodes.KnowledgeInvalidRequest}] Invalid FAQ create JSON: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "Invalid JSON", requestId), statusCode: 400);
    }

    if (body == null || string.IsNullOrWhiteSpace(body.Question) || string.IsNullOrWhiteSpace(body.Answer))
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "question and answer are required", requestId), statusCode: 400);

    try
    {
        var faq = await repo.InsertFaqAsync(tenantId, body.Question, body.Answer,
            body.Category, body.Lang ?? "tr", body.Keywords ?? Array.Empty<string>(), "manual", null);

        if (faq == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, $"FAQ with this question already exists for tenant {tenantId}", requestId), statusCode: 409);

        return Results.Json(faq, statusCode: 201);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportDbError}] FAQ create failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, "FAQ create failed", requestId), statusCode: 500);
    }
});

// Update FAQ
app.MapPut("/api/v1/knowledge/{tenantId:int}/faqs/{faqId:int}", async (
    int tenantId, int faqId,
    HttpContext ctx,
    KnowledgeRepository repo,
    JsonLinesLogger jsonLogger,
    HttpRequest request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    FaqUpdateRequest? body;
    try
    {
        body = await request.ReadFromJsonAsync<FaqUpdateRequest>();
    }
    catch (JsonException ex)
    {
        jsonLogger.SystemWarn($"[{ErrorCodes.KnowledgeInvalidRequest}] Invalid FAQ update JSON: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "Invalid JSON", requestId), statusCode: 400);
    }

    if (body == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "Request body is required", requestId), statusCode: 400);

    try
    {
        var updated = await repo.UpdateFaqAsync(tenantId, faqId, body.Question, body.Answer, body.Category, body.Lang, body.Keywords);
        if (updated == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeFaqNotFound, $"FAQ {faqId} not found", requestId), statusCode: 404);
        return Results.Ok(updated);
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportDbError}] FAQ update failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, "FAQ update failed", requestId), statusCode: 500);
    }
});

// Delete FAQ (soft delete)
app.MapDelete("/api/v1/knowledge/{tenantId:int}/faqs/{faqId:int}", async (
    int tenantId, int faqId,
    HttpContext ctx,
    KnowledgeRepository repo,
    JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var deleted = await repo.DeleteFaqAsync(tenantId, faqId);
        if (!deleted)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeFaqNotFound, $"FAQ {faqId} not found or already deleted", requestId), statusCode: 404);

        return Results.Ok(new { message = "FAQ deactivated", faqId });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportDbError}] FAQ delete failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, "FAQ delete failed", requestId), statusCode: 500);
    }
});

// ============================================================
// Document endpoints (Phase B)
// ============================================================

// Upload document (PDF)
app.MapPost("/api/v1/knowledge/{tenantId:int}/documents/upload", async (
    int tenantId,
    HttpContext ctx,
    KnowledgeRepository repo,
    DocumentProcessingService processingService,
    JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    if (!ctx.Request.HasFormContentType)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidFileType, "Multipart form data required", requestId), statusCode: 400);

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidRequest, "file is required", requestId), statusCode: 400);

    // Validate PDF extension
    var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
    if (ext != ".pdf")
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidFileType, "Only PDF files are supported", requestId), statusCode: 400);

    // Validate content-type
    if (file.ContentType != "application/pdf")
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidFileType, "Invalid content type, expected application/pdf", requestId), statusCode: 400);

    // Validate size
    var maxBytes = maxFileSizeMb * 1024L * 1024L;
    if (file.Length > maxBytes)
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeFileTooLarge, $"File exceeds {maxFileSizeMb}MB limit", requestId), statusCode: 400);

    var title = form["title"].FirstOrDefault() ?? Path.GetFileNameWithoutExtension(file.FileName);

    string? savedPath = null;
    try
    {
        // Validate PDF magic bytes (%PDF) - inside try-catch for stream errors
        byte[] magic = new byte[4];
        using (var peek = file.OpenReadStream()) { await peek.ReadExactlyAsync(magic); }
        if (magic[0] != 0x25 || magic[1] != 0x50 || magic[2] != 0x44 || magic[3] != 0x46)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeInvalidFileType, "File content is not a valid PDF", requestId), statusCode: 400);

        // Save file to disk first
        var safeFileName = $"{Guid.NewGuid():N}.pdf";
        var docDir = Path.Combine(uploadPath, tenantId.ToString());
        Directory.CreateDirectory(docDir);
        savedPath = Path.Combine(docDir, safeFileName);

        await using (var fs = new FileStream(savedPath, FileMode.Create))
            await file.CopyToAsync(fs);

        // Insert document record
        var docId = await repo.InsertDocumentAsync(tenantId, title, "pdf", savedPath, null);

        // Enqueue for background processing
        processingService.EnqueueDocument(new DocumentProcessJob
        {
            TenantId = tenantId,
            DocumentId = docId,
            FilePath = savedPath,
            Title = title
        });

        jsonLogger.StepInfo($"Document uploaded: id={docId}, tenant={tenantId}, title={title}, size={file.Length}", requestId);
        return Results.Json(new { documentId = docId, status = "processing", title }, statusCode: 202);
    }
    catch (Exception ex)
    {
        // Cleanup orphaned file on failure
        if (savedPath != null && File.Exists(savedPath))
        {
            try { File.Delete(savedPath); }
            catch (Exception cleanupEx) { jsonLogger.SystemWarn($"[DocumentUpload] Failed to cleanup orphaned file {savedPath}: {cleanupEx.Message}"); }
        }
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeUploadFailed}] Upload failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeUploadFailed, "File upload failed", requestId), statusCode: 500);
    }
}).DisableAntiforgery();

// List documents
app.MapGet("/api/v1/knowledge/{tenantId:int}/documents", async (
    int tenantId,
    HttpContext ctx,
    KnowledgeRepository repo,
    string? status,
    int? page,
    int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    var p = Math.Max(page ?? 1, 1);
    var l = Math.Clamp(limit ?? 50, 1, 200);

    try
    {
        var (docs, total) = await repo.ListDocumentsAsync(tenantId, status, p, l);
        return Results.Ok(new { documents = docs, total, page = p, limit = l });
    }
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeSearchFailed, $"List failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Get document detail
app.MapGet("/api/v1/knowledge/{tenantId:int}/documents/{docId:int}", async (
    int tenantId, int docId,
    HttpContext ctx,
    KnowledgeRepository repo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        var doc = await repo.GetDocumentAsync(tenantId, docId);
        if (doc == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeDocumentNotFound, $"Document {docId} not found", requestId), statusCode: 404);
        return Results.Ok(doc);
    }
    catch (Exception ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeSearchFailed, $"Get failed: {ex.Message}", requestId), statusCode: 500);
    }
});

// Delete document (hard delete, CASCADE removes chunks)
app.MapDelete("/api/v1/knowledge/{tenantId:int}/documents/{docId:int}", async (
    int tenantId, int docId,
    HttpContext ctx,
    KnowledgeRepository repo,
    JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    try
    {
        // Get document first to delete file from disk
        var doc = await repo.GetDocumentAsync(tenantId, docId);
        if (doc == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeDocumentNotFound, $"Document {docId} not found", requestId), statusCode: 404);

        var deleted = await repo.DeleteDocumentAsync(tenantId, docId);
        if (!deleted)
            return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeDocumentNotFound, $"Document {docId} not found", requestId), statusCode: 404);

        // Clean up file from disk (best-effort)
        if (!string.IsNullOrEmpty(doc.FilePath) && File.Exists(doc.FilePath))
        {
            try { File.Delete(doc.FilePath); }
            catch (Exception ex)
            {
                jsonLogger.SystemWarn($"[DocumentDelete] Failed to delete file {doc.FilePath}: {ex.Message}");
            }
        }

        jsonLogger.StepInfo($"Document deleted: id={docId}, tenant={tenantId}", requestId);
        return Results.Ok(new { message = "Document deleted", documentId = docId });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeImportDbError}] Document delete failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeImportDbError, "Document delete failed", requestId), statusCode: 500);
    }
});

// ============================================================
// Generate embeddings endpoint (background trigger)
// ============================================================

app.MapPost("/api/v1/knowledge/{tenantId:int}/generate-embeddings", async (
    int tenantId,
    HttpContext ctx,
    KnowledgeRepository repo,
    EmbeddingService embeddingService,
    JsonLinesLogger jsonLogger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenant = GetValidatedTenant(ctx, tenantId);
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Token tenant does not match route tenant", requestId), statusCode: 403);

    if (!embeddingService.IsAvailable)
        return Results.Json(new { message = "OpenAI API key not configured. Cannot generate embeddings.", generated = 0 }, statusCode: 200);

    try
    {
        var pending = await repo.GetFaqsWithoutEmbeddingAsync(tenantId, limit: 100);
        if (pending.Count == 0)
            return Results.Ok(new { message = "All FAQs already have embeddings", generated = 0 });

        var generated = 0;
        var failed = 0;
        foreach (var (faqId, text) in pending)
        {
            var embedding = await embeddingService.GetEmbeddingAsync(text);
            if (embedding != null)
            {
                await repo.UpdateFaqEmbeddingAsync(tenantId, faqId, embedding);
                generated++;
            }
            else
            {
                failed++;
                jsonLogger.SystemWarn($"Embedding generation failed for FAQ {faqId}");
            }
        }

        jsonLogger.StepInfo($"Embedding generation: {generated} generated, {failed} failed out of {pending.Count}", requestId);
        return Results.Ok(new { message = $"Generated {generated} embeddings", generated, failed, total = pending.Count });
    }
    catch (Exception ex)
    {
        jsonLogger.StepError($"[{ErrorCodes.KnowledgeOpenAIError}] Embedding generation failed: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.KnowledgeOpenAIError, "Embedding generation failed", requestId), statusCode: 500);
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
        new() { Method = "GET", Path = "/ready", Description = "Ready check (DB + pgvector)", Auth = "none", Category = "Health" },
        new() { Method = "POST", Path = "/api/v1/knowledge/{tenantId}/import", Description = "Import WA-3 NLP data", Auth = "Bearer JWT", Category = "Import" },
        new() { Method = "POST", Path = "/api/v1/knowledge/{tenantId}/search", Description = "Search FAQs (semantic + keyword)", Auth = "Bearer JWT", Category = "Search" },
        new() { Method = "GET", Path = "/api/v1/knowledge/{tenantId}/faqs", Description = "List FAQs", Auth = "Bearer JWT", Category = "FAQ" },
        new() { Method = "POST", Path = "/api/v1/knowledge/{tenantId}/faqs", Description = "Create FAQ", Auth = "Bearer JWT", Category = "FAQ" },
        new() { Method = "PUT", Path = "/api/v1/knowledge/{tenantId}/faqs/{id}", Description = "Update FAQ", Auth = "Bearer JWT", Category = "FAQ" },
        new() { Method = "DELETE", Path = "/api/v1/knowledge/{tenantId}/faqs/{id}", Description = "Delete FAQ (soft)", Auth = "Bearer JWT", Category = "FAQ" },
        new() { Method = "POST", Path = "/api/v1/knowledge/{tenantId}/documents/upload", Description = "Upload PDF document", Auth = "Bearer JWT", Category = "Document" },
        new() { Method = "GET", Path = "/api/v1/knowledge/{tenantId}/documents", Description = "List documents", Auth = "Bearer JWT", Category = "Document" },
        new() { Method = "GET", Path = "/api/v1/knowledge/{tenantId}/documents/{id}", Description = "Get document detail", Auth = "Bearer JWT", Category = "Document" },
        new() { Method = "DELETE", Path = "/api/v1/knowledge/{tenantId}/documents/{id}", Description = "Delete document + chunks", Auth = "Bearer JWT", Category = "Document" },
        new() { Method = "POST", Path = "/api/v1/knowledge/{tenantId}/generate-embeddings", Description = "Generate embeddings for FAQs without one", Auth = "Bearer JWT", Category = "Embedding" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery", Auth = "none", Category = "Ops" }
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.KnowledgeServiceName,
        Port = ServiceConstants.KnowledgePort,
        Endpoints = endpoints
    });
});

// ============================================================
// Start
// ============================================================

logger.SystemInfo($"Knowledge Service starting on port {listenPort}");
app.Run();

// ============================================================
// Request DTOs (Program.cs local, not shared)
// ============================================================

internal sealed class ImportRequest
{
    public string? NlpOutputPath { get; set; }
    public FaqFilterOptions? FaqFilter { get; set; }
}

internal sealed class FaqFilterOptions
{
    public int MinQuestions { get; set; } = 5;
    public int MinAnswerLength { get; set; } = 20;
}

internal sealed class SearchRequest
{
    public string? Query { get; set; }
    public int? TopK { get; set; }
    public string? Lang { get; set; }
    public string? Category { get; set; }
}

internal sealed class FaqCreateRequest
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public string? Category { get; set; }
    public string? Lang { get; set; }
    public string[]? Keywords { get; set; }
}

internal sealed class FaqUpdateRequest
{
    public string? Question { get; set; }
    public string? Answer { get; set; }
    public string? Category { get; set; }
    public string? Lang { get; set; }
    public string[]? Keywords { get; set; }
}
