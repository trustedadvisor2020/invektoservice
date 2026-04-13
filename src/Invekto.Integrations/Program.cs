using System.Text.Json;
using Hangfire;
using Invekto.Integrations.Data;
using Invekto.Integrations.Services;
using Invekto.Integrations.Services.Jobs;
using Invekto.Shared.Hosting;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.Integrations;
using Invekto.Shared.DTOs.Reviews;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;
using Invekto.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.IntegrationsPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";

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
var logger = new JsonLinesLogger(ServiceConstants.IntegrationsServiceName, logPath);
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
builder.Services.AddSingleton<IntegrationsRepository>();

// Register marketplace providers (mock implementations)
builder.Services.AddSingleton<IMarketplaceProvider, HepsiburadaMockProvider>();

// Register ikas e-commerce provider (real OAuth2 + GraphQL)
builder.Services.AddHttpClient<Invekto.Integrations.Services.Ikas.IkasTokenManager>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<Invekto.Integrations.Services.Ikas.IkasTokenManager>();
builder.Services.AddHttpClient<Invekto.Integrations.Services.Ikas.IkasGraphQlClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<Invekto.Integrations.Services.Ikas.IkasGraphQlClient>();
builder.Services.AddSingleton<Invekto.Integrations.Services.Ikas.IkasProvider>();
builder.Services.AddSingleton<IEcommerceProvider>(sp =>
    sp.GetRequiredService<Invekto.Integrations.Services.Ikas.IkasProvider>());
builder.Services.AddSingleton<IMarketplaceProvider>(sp =>
    sp.GetRequiredService<Invekto.Integrations.Services.Ikas.IkasProvider>());

// Register cargo providers (mock implementations)
builder.Services.AddSingleton<ICargoProvider, ArasCargoMockProvider>();
builder.Services.AddSingleton<ICargoProvider, YurticiCargoMockProvider>();

// G7 Faz 5: Hangfire (replaces OrderSyncService IHostedService)
var hangfireConnStr = HangfireSetup.ResolveConnectionString(builder.Configuration);
builder.Services.AddInvektoHangfire("integrations", hangfireConnStr);
builder.Services.AddScoped<OrderSyncJob>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Faz 1: Plan-based feature guard (after JwtAuth sets TenantContext)
var planCache = new TenantPlanCache(pgConnStr, logger);
app.UseFeatureGuard(planCache, logger,
    ("/api/v1/", "Integrations"));
app.UseAuthorization();

// G7 Faz 5: OrderSync recurring job (idempotent via AddOrUpdate)
var orderSyncCron = builder.Configuration["OrderSync:Cron"] ?? "*/5 * * * *";
RecurringJob.AddOrUpdate<OrderSyncJob>(
    "integrations:order-sync",
    "integrations",
    j => j.RunAsync(CancellationToken.None),
    orderSyncCron);

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.IntegrationsServiceName)));
app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.IntegrationsServiceName));
});

// ============================================================
// Integration Account endpoints
// ============================================================

app.MapGet("/api/v1/accounts", async (
    HttpContext ctx,
    IntegrationsRepository repo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var accounts = await repo.ListAccountsAsync(tenantContext.TenantId);
    return Results.Ok(accounts);
});

app.MapGet("/api/v1/accounts/{provider}", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    string provider) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var account = await repo.GetAccountAsync(tenantContext.TenantId, provider);
    if (account == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsAccountNotFound, $"No integration account for provider '{provider}'", requestId), statusCode: 404);

    return Results.Ok(account);
});

app.MapPost("/api/v1/accounts", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    JsonLinesLogger jsonLogger,
    IntegrationAccountRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    if (request == null || string.IsNullOrWhiteSpace(request.Provider))
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidAccountPayload, "Provider is required", requestId), statusCode: 400);

    var validProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "hepsiburada", "trendyol", "aras_kargo", "yurtici_kargo", "ikas" };

    if (!validProviders.Contains(request.Provider))
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidAccountPayload, $"Invalid provider: {request.Provider}", requestId), statusCode: 400);

    var settingsJson = request.Settings != null
        ? System.Text.Json.JsonSerializer.Serialize(request.Settings)
        : null;

    var id = await repo.UpsertAccountAsync(
        tenantContext.TenantId, request.Provider,
        request.ApiKey, request.ApiSecret, request.SellerId,
        settingsJson);

    jsonLogger.StepInfo($"Integration account upserted: id={id}, provider={request.Provider}", requestId);
    return Results.Json(new { id, provider = request.Provider, status = "active" }, statusCode: 201);
});

app.MapPost("/api/v1/accounts/{provider}/test", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    IEnumerable<IMarketplaceProvider> providers,
    string provider) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var creds = await repo.GetAccountCredentialsAsync(tenantContext.TenantId, provider);
    if (creds == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsAccountNotFound, $"No active account for provider '{provider}'", requestId), statusCode: 404);

    var marketplaceProvider = providers.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));

    if (marketplaceProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsProviderConnectionFailed, $"Provider '{provider}' not supported", requestId), statusCode: 400);

    var (apiKeyEnc, apiSecretEnc, sellerId) = creds.Value;
    var (success, errorMessage) = await marketplaceProvider.TestConnectionAsync(
        apiKeyEnc ?? "", apiSecretEnc, sellerId, CancellationToken.None);

    if (!success)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsProviderConnectionFailed, errorMessage ?? "Connection test failed", requestId), statusCode: 502);

    return Results.Ok(new { provider, status = "connected" });
});

// ============================================================
// Orders endpoints
// ============================================================

app.MapGet("/api/v1/orders", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    string? provider, string? status, string? phone,
    DateTime? from, DateTime? to,
    int? limit, int? offset) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var orders = await repo.QueryOrdersAsync(
        tenantContext.TenantId, provider, status, phone,
        from, to, limit ?? 50, offset ?? 0);

    return Results.Ok(orders);
});

app.MapGet("/api/v1/orders/{provider}/{externalOrderId}", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    string provider, string externalOrderId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var order = await repo.GetOrderByExternalIdAsync(tenantContext.TenantId, provider, externalOrderId);
    if (order == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsOrderNotFound, $"Order '{externalOrderId}' not found", requestId), statusCode: 404);

    return Results.Ok(order);
});

// ============================================================
// Cargo tracking endpoints (GR-3.6)
// ============================================================

app.MapGet("/api/v1/cargo/{trackingCode}", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    IEnumerable<ICargoProvider> cargoProviders,
    string trackingCode,
    string? cargoProvider) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    // First check DB cache
    var events = await repo.GetCargoEventsAsync(tenantContext.TenantId, trackingCode);
    if (events.Count > 0)
    {
        var cached = new Invekto.Shared.DTOs.Integrations.CargoTrackingResponse
        {
            TrackingCode = trackingCode,
            CargoProvider = cargoProvider ?? "unknown",
            CurrentStatus = events[^1].Status,
            Events = events.Select(e => new Invekto.Shared.DTOs.Integrations.CargoTrackingEvent
            {
                Status = e.Status,
                Location = e.Location,
                EventTime = e.EventTime
            }).ToList()
        };
        return Results.Ok(cached);
    }

    // If no cached events and a provider is specified, query the mock
    if (!string.IsNullOrEmpty(cargoProvider))
    {
        var provider = cargoProviders.FirstOrDefault(p =>
            string.Equals(p.ProviderName, cargoProvider, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsCargoTrackingUnavailable, $"Cargo provider '{cargoProvider}' not supported", requestId), statusCode: 400);

        var result = await provider.TrackShipmentAsync(trackingCode, CancellationToken.None);
        if (result == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsCargoTrackingUnavailable, "Tracking info not available", requestId), statusCode: 404);

        var response = new Invekto.Shared.DTOs.Integrations.CargoTrackingResponse
        {
            TrackingCode = result.TrackingCode,
            CargoProvider = result.CargoProvider,
            CurrentStatus = result.CurrentStatus,
            Events = result.Events.Select(e => new Invekto.Shared.DTOs.Integrations.CargoTrackingEvent
            {
                Status = e.Status,
                Location = e.Location,
                EventTime = e.EventTime
            }).ToList()
        };
        return Results.Ok(response);
    }

    return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsCargoTrackingUnavailable, "No tracking data found. Specify cargo_provider parameter to query live.", requestId), statusCode: 404);
});

// ============================================================
// E-Commerce endpoints (ikas, Shopify, etc.)
// ============================================================

app.MapGet("/api/v1/ecommerce/{provider}/products", async (
    HttpContext ctx,
    IEnumerable<IEcommerceProvider> ecomProviders,
    string provider,
    string? search, string? status, int? limit, string? cursor) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var ecomProvider = ecomProviders.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));
    if (ecomProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProviderNotFound,
            $"E-commerce provider '{provider}' not supported", requestId), statusCode: 400);

    var result = await ecomProvider.ListProductsAsync(tenantContext.TenantId,
        new EcommerceProductFilter
        {
            SearchTerm = search, Status = status,
            Limit = limit ?? 20, Cursor = cursor
        }, ctx.RequestAborted);

    return Results.Ok(new EcommerceProductListResponse
    {
        Products = result.Products.Select(p => new EcommerceProductResponse
        {
            Id = p.Id, Name = p.Name, Price = p.Price,
            Currency = p.Currency, StockCount = p.StockCount,
            ImageUrl = p.ImageUrl, Status = p.Status
        }).ToList(),
        TotalCount = result.TotalCount,
        HasNextPage = result.HasNextPage,
        Cursor = result.Cursor
    });
});

app.MapGet("/api/v1/ecommerce/{provider}/products/{productId}", async (
    HttpContext ctx,
    IEnumerable<IEcommerceProvider> ecomProviders,
    string provider, string productId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var ecomProvider = ecomProviders.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));
    if (ecomProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProviderNotFound,
            $"E-commerce provider '{provider}' not supported", requestId), statusCode: 400);

    var product = await ecomProvider.GetProductAsync(tenantContext.TenantId, productId, ctx.RequestAborted);
    if (product == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProductQueryFailed,
            $"Product '{productId}' not found", requestId), statusCode: 404);

    return Results.Ok(new EcommerceProductResponse
    {
        Id = product.Id, Name = product.Name, Price = product.Price,
        Currency = product.Currency, StockCount = product.StockCount,
        ImageUrl = product.ImageUrl, Status = product.Status
    });
});

app.MapGet("/api/v1/ecommerce/{provider}/customers", async (
    HttpContext ctx,
    IEnumerable<IEcommerceProvider> ecomProviders,
    string provider,
    string? phone, string? email, string? search, int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var ecomProvider = ecomProviders.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));
    if (ecomProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProviderNotFound,
            $"E-commerce provider '{provider}' not supported", requestId), statusCode: 400);

    var result = await ecomProvider.ListCustomersAsync(tenantContext.TenantId,
        new EcommerceCustomerFilter
        {
            Phone = phone, Email = email,
            SearchTerm = search, Limit = limit ?? 20
        }, ctx.RequestAborted);

    return Results.Ok(new EcommerceCustomerListResponse
    {
        Customers = result.Customers.Select(c => new EcommerceCustomerResponse
        {
            Id = c.Id, FullName = c.FullName, Email = c.Email,
            Phone = c.Phone, OrderCount = c.OrderCount, TotalSpent = c.TotalSpent
        }).ToList(),
        TotalCount = result.TotalCount
    });
});

app.MapPost("/api/v1/ecommerce/{provider}/orders/{orderId}/fulfill", async (
    HttpContext ctx,
    IEnumerable<IEcommerceProvider> ecomProviders,
    string provider, string orderId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var ecomProvider = ecomProviders.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));
    if (ecomProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProviderNotFound,
            $"E-commerce provider '{provider}' not supported", requestId), statusCode: 400);

    var fulfillRequest = new FulfillOrderRequest();
    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        fulfillRequest = new FulfillOrderRequest
        {
            TrackingCode = root.TryGetProperty("tracking_code", out var tc) ? tc.GetString() : null,
            CargoProvider = root.TryGetProperty("cargo_provider", out var cp) ? cp.GetString() : null
        };
    }
    catch (JsonException ex)
    {
        logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOrderMutationFailed}] fulfill body parse error: {ex.Message}");
    }

    var result = await ecomProvider.FulfillOrderAsync(tenantContext.TenantId, orderId, fulfillRequest, ctx.RequestAborted);
    if (!result.Success)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomOrderMutationFailed,
            result.ErrorMessage ?? "Fulfill failed", requestId), statusCode: 502);

    return Results.Ok(new EcommerceOperationResponse { Success = true, Result = result.ResultJson });
});

app.MapPost("/api/v1/ecommerce/{provider}/orders/{orderId}/status", async (
    HttpContext ctx,
    IEnumerable<IEcommerceProvider> ecomProviders,
    string provider, string orderId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var ecomProvider = ecomProviders.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));
    if (ecomProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProviderNotFound,
            $"E-commerce provider '{provider}' not supported", requestId), statusCode: 400);

    string? newStatus = null;
    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        newStatus = bodyDoc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
    }
    catch (JsonException ex)
    {
        logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOrderMutationFailed}] status body parse error: {ex.Message}");
    }

    if (string.IsNullOrWhiteSpace(newStatus))
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomOrderMutationFailed,
            "status is required", requestId), statusCode: 400);

    var result = await ecomProvider.UpdateOrderStatusAsync(tenantContext.TenantId, orderId, newStatus, ctx.RequestAborted);
    if (!result.Success)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomOrderMutationFailed,
            result.ErrorMessage ?? "Status update failed", requestId), statusCode: 502);

    return Results.Ok(new EcommerceOperationResponse { Success = true, Result = result.ResultJson });
});

app.MapPost("/api/v1/ecommerce/{provider}/orders/{orderId}/refund-line", async (
    HttpContext ctx,
    IEnumerable<IEcommerceProvider> ecomProviders,
    string provider, string orderId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var ecomProvider = ecomProviders.FirstOrDefault(p =>
        string.Equals(p.ProviderName, provider, StringComparison.OrdinalIgnoreCase));
    if (ecomProvider == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomProviderNotFound,
            $"E-commerce provider '{provider}' not supported", requestId), statusCode: 400);

    var refundRequest = new RefundLineRequest();
    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;
        refundRequest = new RefundLineRequest
        {
            LineItemId = root.TryGetProperty("line_item_id", out var li) ? li.GetString() ?? "" : "",
            Quantity = root.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1,
            Reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null
        };
    }
    catch (JsonException ex)
    {
        logger.SystemWarn($"[{ErrorCodes.IntegrationsEcomOrderMutationFailed}] refund body parse error: {ex.Message}");
    }

    var result = await ecomProvider.RefundOrderLineAsync(tenantContext.TenantId, orderId, refundRequest, ctx.RequestAborted);
    if (!result.Success)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsEcomOrderMutationFailed,
            result.ErrorMessage ?? "Refund failed", requestId), statusCode: 502);

    return Results.Ok(new EcommerceOperationResponse { Success = true, Result = result.ResultJson });
});

// ============================================================
// Review Alerts endpoints (PKT-6B1: GR-3.16)
// ============================================================

app.MapPost("/api/v1/reviews/webhook", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    JsonLinesLogger jsonLogger,
    ReviewAlertWebhookRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null || string.IsNullOrWhiteSpace(request.Provider))
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidReviewWebhook, "provider is required", requestId), statusCode: 400);

    if (request.Rating < 1 || request.Rating > 5)
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidReviewWebhook, "rating must be 1-5", requestId), statusCode: 400);

    var validProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "trendyol", "hepsiburada", "google", "manual" };

    if (!validProviders.Contains(request.Provider))
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidReviewWebhook, $"Invalid provider: {request.Provider}", requestId), statusCode: 400);

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        var alertId = await repo.UpsertReviewAlertAsync(
            tenantContext.TenantId, request.Provider, request.ExternalReviewId,
            request.Rating, request.ReviewText, request.CustomerPhone, request.OrderId,
            ctx.RequestAborted);

        jsonLogger.StepInfo(
            $"Review alert created: id={alertId}, provider={request.Provider}, rating={request.Rating}", requestId);

        return Results.Json(new ReviewAlertResponse
        {
            Id = alertId,
            Provider = request.Provider,
            Rating = request.Rating,
            RecoveryStatus = "pending",
            CreatedAt = DateTime.UtcNow
        }, statusCode: 201);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.StepError($"Review alert DB error: {ex.Message}", requestId);
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsReviewAlertCreateFailed, "Review alert creation failed", requestId), statusCode: 500);
    }
});

app.MapGet("/api/v1/reviews/alerts", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    string? status, int? limit) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var alerts = await repo.GetReviewAlertsAsync(tenantContext.TenantId, status, limit ?? 50, ctx.RequestAborted);

    var response = alerts.Select(a => new ReviewAlertResponse
    {
        Id = a.Id,
        Provider = a.Provider,
        ExternalReviewId = a.ExternalReviewId,
        Rating = a.Rating,
        ReviewText = a.ReviewText,
        CustomerPhone = a.CustomerPhone,
        RecoveryStatus = a.RecoveryStatus,
        RecoveryAttempt = a.RecoveryAttempt,
        CreatedAt = a.CreatedAt
    }).ToList();

    return Results.Ok(new { alerts = response });
});

app.MapPut("/api/v1/reviews/alerts/{alertId:int}/status", async (
    HttpContext ctx,
    IntegrationsRepository repo,
    JsonLinesLogger jsonLogger,
    int alertId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    try
    {
        using var bodyDoc = await JsonDocument.ParseAsync(ctx.Request.Body);
        var root = bodyDoc.RootElement;

        var newStatus = root.TryGetProperty("recovery_status", out var s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(newStatus))
            return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidReviewWebhook, "recovery_status is required", requestId), statusCode: 400);

        var validStatuses = new HashSet<string> { "pending", "contacted", "resolved", "unresolved", "expired" };
        if (!validStatuses.Contains(newStatus))
            return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidReviewWebhook,
                $"recovery_status must be one of: {string.Join(", ", validStatuses)}", requestId), statusCode: 400);

        var message = root.TryGetProperty("recovery_message", out var m) ? m.GetString() : null;

        var updated = await repo.UpdateRecoveryStatusAsync(tenantContext.TenantId, alertId, newStatus, message, ctx.RequestAborted);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsReviewAlertCreateFailed, $"Alert {alertId} not found", requestId), statusCode: 404);

        jsonLogger.StepInfo($"Review alert status updated: id={alertId}, status={newStatus}", requestId);
        return Results.Ok(new { id = alertId, recovery_status = newStatus });
    }
    catch (JsonException)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.IntegrationsInvalidReviewWebhook, "Invalid JSON body", requestId), statusCode: 400);
    }
});

app.MapGet("/api/v1/reviews/stats", async (
    HttpContext ctx,
    IntegrationsRepository repo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);

    var (total, byStatus, byProvider) = await repo.GetReviewRecoveryStatsAsync(tenantContext.TenantId, ctx.RequestAborted);

    return Results.Ok(new ReviewRecoveryStatsResponse
    {
        TotalAlerts = total,
        ByStatus = byStatus,
        ByProvider = byProvider
    });
});

// ============================================================
// Ops endpoints
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/api/v1/accounts", Description = "List integration accounts", Auth = "Bearer", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/accounts/{provider}", Description = "Get integration account by provider", Auth = "Bearer", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/accounts", Description = "Create/update integration account", Auth = "Bearer", Category = "API" },
        new() { Method = "POST", Path = "/api/v1/accounts/{provider}/test", Description = "Test provider connection", Auth = "Bearer", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/orders", Description = "Query cached orders", Auth = "Bearer", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/orders/{provider}/{externalOrderId}", Description = "Get single order", Auth = "Bearer", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/cargo/{trackingCode}", Description = "Track cargo shipment", Auth = "Bearer", Category = "API" },
        new() { Method = "GET", Path = "/api/v1/ecommerce/{provider}/products", Description = "List e-commerce products", Auth = "Bearer", Category = "E-Commerce" },
        new() { Method = "GET", Path = "/api/v1/ecommerce/{provider}/products/{productId}", Description = "Get single product", Auth = "Bearer", Category = "E-Commerce" },
        new() { Method = "GET", Path = "/api/v1/ecommerce/{provider}/customers", Description = "List e-commerce customers", Auth = "Bearer", Category = "E-Commerce" },
        new() { Method = "POST", Path = "/api/v1/ecommerce/{provider}/orders/{orderId}/fulfill", Description = "Fulfill order", Auth = "Bearer", Category = "E-Commerce" },
        new() { Method = "POST", Path = "/api/v1/ecommerce/{provider}/orders/{orderId}/status", Description = "Update order status", Auth = "Bearer", Category = "E-Commerce" },
        new() { Method = "POST", Path = "/api/v1/ecommerce/{provider}/orders/{orderId}/refund-line", Description = "Refund order line", Auth = "Bearer", Category = "E-Commerce" },
        new() { Method = "POST", Path = "/api/v1/reviews/webhook", Description = "Receive review alert webhook (PKT-6B1)", Auth = "Bearer", Category = "Reviews" },
        new() { Method = "GET", Path = "/api/v1/reviews/alerts", Description = "List review alerts (PKT-6B1)", Auth = "Bearer", Category = "Reviews" },
        new() { Method = "PUT", Path = "/api/v1/reviews/alerts/{alertId}/status", Description = "Update review recovery status (PKT-6B1)", Auth = "Bearer", Category = "Reviews" },
        new() { Method = "GET", Path = "/api/v1/reviews/stats", Description = "Review recovery stats (PKT-6B1)", Auth = "Bearer", Category = "Reviews" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery", Auth = "none", Category = "Ops" }
    };
    return Results.Ok(endpoints);
});

logger.SystemInfo($"Invekto.Integrations starting on port {listenPort}");
app.Run();
