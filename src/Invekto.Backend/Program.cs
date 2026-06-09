using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Middleware;
using Hangfire;
using Invekto.Backend.Data;
using Invekto.Backend.Endpoints;
using Invekto.Backend.Services;
using Invekto.Backend.Services.Jobs;
using Invekto.Shared.Auth;
using Invekto.Shared.Hosting;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Contracts.Inma.Webhooks;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.ChatAnalysis;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Integration;
using Invekto.Shared.DTOs.Analytics;
using Invekto.Shared.Contracts.Leads;
using Invekto.Shared.DTOs.Attribution;
using Invekto.Shared.DTOs.Leads;
using Invekto.Shared.DTOs.Onboarding;
using Invekto.Shared.DTOs.Payment;
using Invekto.Shared.DTOs.Translation;
using Invekto.Shared.Logging;
using Invekto.Shared.Logging.Reader;
using Invekto.Shared.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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
var webChatUrl = builder.Configuration["Microservice:WebChat:Url"]
    ?? $"http://localhost:{ServiceConstants.WebChatPort}";
var webChatTimeoutMs = builder.Configuration.GetValue<int>("Microservice:WebChat:TimeoutMs", 5000);
var voiceAIUrl = builder.Configuration["Microservice:VoiceAI:Url"]
    ?? $"http://localhost:{ServiceConstants.VoiceAIPort}";
var voiceAITimeoutMs = builder.Configuration.GetValue<int>("Microservice:VoiceAI:TimeoutMs", 35000);
var internalApiKey = builder.Configuration["Microservice:InternalApiKey"] ?? "";

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
    if (!string.IsNullOrEmpty(internalApiKey))
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
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

// FEAT-PROJELER PKT-14 S3: cxapi WhatsApp approved-template list client (READ-ONLY).
// Per-request X-CIB-SecretKey (never DefaultRequestHeaders); FIXED cxapi base URL from
// WapCrmTemplateOptions (never the tenant ApiUrl — SSRF mitigation); AllowAutoRedirect=false
// because cxapi 301/302 = rate-limit; per-attempt timeout enforced inside the client
// (HttpClient timeout Infinite). Mirrors the Outbound WapCrmSendClient registration.
var wapCrmTemplateOptions = new WapCrmTemplateOptions();
builder.Configuration.GetSection(WapCrmTemplateOptions.SectionName).Bind(wapCrmTemplateOptions);
builder.Services.AddSingleton(wapCrmTemplateOptions);
builder.Services.AddHttpClient<WapCrmTemplateClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    .ConfigureHttpClient((sp, client) =>
    {
        var opts = sp.GetRequiredService<WapCrmTemplateOptions>();
        client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
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

// FEAT-J2 + peer-service internal endpoints share a single secret rotation story.
// Used by manual opt-out (/api/v1/optout) and SuperAdmin outbox retry-skipped proxy
// (/api/ops/outbox/retry-skipped) for X-Internal-Service-Token header binding.
var internalSharedSecret = builder.Configuration["InternalServices:SharedSecret"] ?? string.Empty;

// Configure Marketing HTTP client (GR-3.21/3.22)
builder.Services.AddHttpClient<MarketingClient>(client =>
{
    client.BaseAddress = new Uri(marketingUrl);
    client.Timeout = TimeSpan.FromMilliseconds(marketingTimeoutMs);
});

// FEAT-EFS Drip Sequence: Backend → Marketing SPA-facing proxy. Reuses Marketing
// base URL + timeout already resolved above; mints per-call service JWT bound to the
// current request's tenant_id (TenantContext from JwtAuth middleware).
builder.Services.AddHttpClient<MarketingFollowupProxyClient>(client =>
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

// Translation service (Claude Haiku-powered, no base address — raw API call)
builder.Services.AddHttpClient<TranslationService>();

// Configure WebChat HTTP client (with internal API key for operator proxy)
builder.Services.AddHttpClient<WebChatClient>()
    .AddTypedClient((httpClient, sp) =>
    {
        httpClient.BaseAddress = new Uri(webChatUrl);
        httpClient.Timeout = TimeSpan.FromMilliseconds(webChatTimeoutMs);
        return new WebChatClient(httpClient, sp.GetRequiredService<ILogger<WebChatClient>>(), internalApiKey);
    });

// Configure VoiceAI HTTP client (PKT-11: Voice Message AI)
builder.Services.AddHttpClient<VoiceAIClient>(client =>
{
    client.BaseAddress = new Uri(voiceAIUrl);
    client.Timeout = TimeSpan.FromMilliseconds(voiceAITimeoutMs);
});

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

// UP0.6: server-side feature flag cache (5dk TTL, populated on inma token exchange)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IFeatureFlagService, InMemoryFeatureFlagService>();

// InmaJwtSettings (singleton — used by login/refresh/welcome proxy endpoints + introspector)
var inmaJwtSettings = new InmaJwtSettings
{
    LoginUrl = builder.Configuration["InmaAuth:LoginUrl"] ?? string.Empty,
    LoginTimeoutMs = builder.Configuration.GetValue<int>("InmaAuth:LoginTimeoutMs", 10000),
    RefreshUrl = builder.Configuration["InmaAuth:RefreshUrl"],
    ApiBaseUrl = builder.Configuration["InmaAuth:ApiBaseUrl"]
};
var inmaAuthMockEnabled = builder.Configuration.GetValue<bool>("InmaAuth:MockEnabled", false);

// inma login proxy HTTP client (no base address — LoginUrl is full URL from config)
builder.Services.AddHttpClient("inma_login", client =>
{
    var loginTimeoutMs = builder.Configuration.GetValue<int>("InmaAuth:LoginTimeoutMs", 10000);
    client.Timeout = TimeSpan.FromMilliseconds(loginTimeoutMs);
});

// inma token introspection: welcome-endpoint check (5s timeout, 5dk per-token cache).
// Replaces InmaJwtValidator HS256 signature verify (key never shared with downstream).
// DI-managed: typed HttpClient + IMemoryCache + ILogger<T> via standard ASP.NET Core pipeline.
// Singleton lifetime: container handles disposal; ApiBaseUrl boş = registered ama runtime null behavior
// (introspector ApiBaseUrl boş ise INV-AUTH-008 graceful döner, exception throw etmez).
builder.Services.AddHttpClient<InmaTokenIntrospector>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
// Override default constructor injection: IMemoryCache zaten singleton (UP0.6 L297), ILogger<T>
// ASP.NET Core'un kurduğu pipeline'dan; settings outer-scope variable capture.
builder.Services.AddSingleton<InmaTokenIntrospector>(sp => new InmaTokenIntrospector(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(InmaTokenIntrospector)),
    sp.GetRequiredService<IMemoryCache>(),
    inmaJwtSettings,
    sp.GetRequiredService<ILogger<InmaTokenIntrospector>>()));

// Helper: map InmaTokenIntrospector ErrorCode → correct HTTP status + ErrorResponse envelope.
// Structured ErrorCode (from IntrospectResult) — no substring matching:
//   INV-AUTH-008 = service unavailable (503)
//   INV-AUTH-001/002 (or null fallback) = unauthorized (401)
IResult MapInmaIntrospectError(string? errorCode, string? detail, string requestId)
{
    if (errorCode == ErrorCodes.AuthInmaIntrospectionUnavailable)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthInmaIntrospectionUnavailable,
                "INMA servisine ulaşılamadı, kısa süre sonra tekrar deneyin.", requestId),
            statusCode: 503);
    return Results.Json(
        ErrorResponse.Create(ErrorCodes.AuthUnauthorized, detail ?? "Invalid token", requestId),
        statusCode: 401);
}

// PostgreSQL connection factory (singleton, thread-safe pooling)
PostgresConnectionFactory? pgFactory = null;
var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSQL");
var inmaConnectionString = builder.Configuration.GetConnectionString("InmaManagementDb");
// HOTFIX 2026-05-26: registration is now unconditional. Previously this entire
// block was wrapped in `if (!string.IsNullOrEmpty(pgConnectionString))`. Under
// .NET 8.0.19 the stricter RequestDelegateFactory inspects every endpoint at
// startup and crashes the host (InvalidOperationException: "Body was inferred
// but the method does not allow inferred body parameters") whenever an endpoint
// parameter type was conditionally registered. Keeping registration
// unconditional means startup always succeeds; a missing connection string now
// fails at the first DB call site (anlamli runtime hata) instead of bringing
// the entire host down. The braces below are kept as a standalone block to
// preserve scope/indentation and minimise the diff.
{
    pgFactory = new PostgresConnectionFactory(pgConnectionString ?? string.Empty);
    builder.Services.AddSingleton(pgFactory);

    // PKT-3: AnalyticsRepository (singleton, thread-safe via connection pooling)
    builder.Services.AddSingleton<AnalyticsRepository>();
    // PKT-3 + G7 Faz 4: MetricsAggregationJob (Hangfire recurring, cron */5 min) — see below

    // GR-3.14: Attribution tracking (singleton, thread-safe via connection pooling)
    builder.Services.AddSingleton<AttributionRepository>();
    builder.Services.AddSingleton<AttributionService>();

    // PKT-6B1: Lead Management v2 (GR-3.13)
    builder.Services.AddSingleton<LeadRepository>();

    // FEAT-LIW Chunk A: Lead Intake Webhook services + tenant landing settings
    // All singletons — FieldMapResolver + PhoneE164Normalizer are stateless;
    // ApiKeyRateLimiter holds the in-memory sliding-window bucket state.
    builder.Services.AddSingleton<TenantLandingSettingsRepository>();
    builder.Services.AddSingleton<PhoneE164Normalizer>();
    builder.Services.AddSingleton<FieldMapResolver>();
    builder.Services.AddSingleton(new ApiKeyRateLimiter(
        limit: builder.Configuration.GetValue<int>("LeadIntake:RateLimitPerMinute", 100),
        windowSeconds: 60));
    builder.Services.AddSingleton<LeadIntakeService>();

    // FEAT-LIW Chunk C: Dashboard settings endpoints (tenant-scoped JWT-auth'd)
    builder.Services.AddSingleton<LiwAuditRepository>();
    builder.Services.AddSingleton<FieldMapValidator>();
    builder.Services.AddSingleton<ApiKeyGenerator>();
    // FEAT-LIW Chunk C: cross-service flow lookup via HTTP (microservice isolation —
    // direct DB access from Backend to Automation's chatbot_flows is forbidden).
    // Reuses the existing Automation BaseAddress + mints per-call service JWT through
    // the same JwtGenerator Chunk B's BackendIntakeClient pattern uses.
    builder.Services.AddHttpClient<FlowLookupClient>(client =>
    {
        client.BaseAddress = new Uri(automationUrl);
        client.Timeout = TimeSpan.FromSeconds(5);
    });
    builder.Services.AddSingleton<LiwSettingsService>();

    // SuperAdmin: Message log (fire-and-forget insert at webhook, paginated select for ops)
    builder.Services.AddSingleton<MessageLogRepository>();

    // SuperAdmin: Tenant registry (list + impersonate)
    builder.Services.AddSingleton<TenantRegistryRepository>();

    // FEAT-VFB F-VR-B: voice_tenant_profile read (VoiceRuntime reads it over HTTP via /api/ops/tenants/{id}/voice-profile).
    builder.Services.AddSingleton<VoiceTenantProfileRepository>();

    // FEAT-PILOT-KANBAN: SuperAdmin pilot tracking board (Migration 035).
    // Read-only Dashboard surface; mutation tek path /wrap workflow Step 3.5.
    builder.Services.AddSingleton<KanbanRepository>();

    // Instance management (tenant_instances table: filtering + flow routing)
    builder.Services.AddSingleton<InstanceRepository>();

    // Onboarding status aggregation (PKT-2: computed from Knowledge + Automation + tenant_registry)
    builder.Services.AddSingleton<OnboardingStatusService>();

    // Translation cache + G7 Faz 4 cleanup job (7-day TTL) — see Hangfire block below
    builder.Services.AddSingleton<TranslationCacheRepository>();

    // FEAT-PHOTO wire-up patch (2026-04-28): inbound media handler DI register.
    // Parent paket (commit 1da0da6) compiled-in code; runtime DI burada. Singleton
    // PostgresConnectionFactory + JsonLinesLogger consumer ortakligi (LeadRepository
    // pattern). Plan arch/plans/20260428-feat-photo-wireup-patch.json AC3.
    builder.Services.AddSingleton<Invekto.Backend.Services.Photos.IPhotoInboundHandler,
        Invekto.Backend.Services.Photos.PhotoInboundHandler>();

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

// FEAT-DMP: INMA dynamic-fields proxy + cache + NullResolver (default binding).
// IInmaDynamicFieldsClient uses a per-call secretKey so the typed-client model doesn't fit;
// we register a named HttpClient (5s timeout) and manually wire the client singleton.
// ApiBaseUrl falls back to the standard wapcrm.net production host when unset to match
// the FEAT-J2 convention used by HttpInmaContactOptOutClient consumers.
builder.Services.AddHttpClient("inma_dynamicfields", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddSingleton<Invekto.Shared.Contracts.Inma.IInmaDynamicFieldsClient>(sp =>
{
    var baseUrl = builder.Configuration["InmaAuth:ApiBaseUrl"]
                  ?? builder.Configuration["InmaAuth:OptOutSync:BaseUrl"]
                  ?? "https://cxapi.wapcrm.net";
    return new Invekto.Shared.Contracts.Inma.HttpInmaDynamicFieldsClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("inma_dynamicfields"),
        sp.GetRequiredService<JsonLinesLogger>(),
        baseUrl);
});
builder.Services.AddSingleton<Invekto.Shared.Services.InmaDynamicFieldsCache>();
// FEAT-TFM MVP: swap NullResolver → DbResolver (semantic→INMA key from tenant_settings.field_mapping).
// Mapping yoksa null döner → DMP DynamicMessageValidator raw INMA-key allowlist fallback intact.
builder.Services.AddSingleton<Invekto.Backend.Data.TenantSettingsRepository>();
builder.Services.AddSingleton<Invekto.Shared.Contracts.TenantFieldMapping.DbTenantFieldMappingResolver>();
builder.Services.AddSingleton<Invekto.Shared.Contracts.TenantFieldMapping.ITenantFieldMappingResolver>(sp =>
    sp.GetRequiredService<Invekto.Shared.Contracts.TenantFieldMapping.DbTenantFieldMappingResolver>());

// FEAT-MCC Multi-City Campaign: tenant_settings.campaign_config resolver. Backend uses
// it from PUT to invalidate the local-instance cache after a successful upsert. The
// same Shared resolver type is wired in Automation + Marketing (each with its own
// process-local IMemoryCache, eventual consistency MVP).
builder.Services.AddSingleton<Invekto.Shared.Contracts.Campaigns.DbTenantCampaignResolver>();
builder.Services.AddSingleton<Invekto.Shared.Contracts.Campaigns.ITenantCampaignResolver>(sp =>
    sp.GetRequiredService<Invekto.Shared.Contracts.Campaigns.DbTenantCampaignResolver>());

// FEAT-CLINIC-METADATA: clinic_contact + team_members resolver registration. Backend
// hosts the GET/PUT endpoints, calls Invalidate() on PUT for local cache freshness.
// Same Shared resolver wired in Automation (eventual consistency 5dk TTL).
builder.Services.AddSingleton<Invekto.Shared.Contracts.ClinicMetadata.DbTenantClinicMetadataResolver>();
builder.Services.AddSingleton<Invekto.Shared.Contracts.ClinicMetadata.ITenantClinicMetadataResolver>(sp =>
    sp.GetRequiredService<Invekto.Shared.Contracts.ClinicMetadata.DbTenantClinicMetadataResolver>());

// Paket B-META: Meta Leadgen webhook native integration
// Config + event repositories share the same PostgresConnectionFactory pattern as
// TenantSettingsRepository above. MetaGraphApiClient uses IHttpClientFactory +
// IMemoryCache (already registered at line ~309) so Singleton lifecycle is safe.
// MetaLeadgenWebhookService resolves IBackgroundJobClient from DI to enqueue the
// async MetaLeadgenIntakeJob onto the automation queue.
builder.Services.AddSingleton<Invekto.Backend.Services.MetaLeadgen.MetaLeadgenConfigRepository>();
builder.Services.AddSingleton<Invekto.Backend.Services.MetaLeadgen.MetaLeadgenEventRepository>();
builder.Services.AddSingleton<Invekto.Backend.Services.MetaLeadgen.MetaLeadgenWebhookService>();
builder.Services.AddSingleton<
    Invekto.Backend.Services.MetaLeadgen.IMetaGraphApiClient,
    Invekto.Backend.Services.MetaLeadgen.MetaGraphApiClient>();
builder.Services.AddHttpClient(
    Invekto.Backend.Services.MetaLeadgen.MetaGraphApiClient.HttpClientName,
    c =>
    {
        c.BaseAddress = new Uri("https://graph.facebook.com/v21.0/");
        c.Timeout = TimeSpan.FromSeconds(10);
    });

builder.Services.AddAuthorization();

// G7: Hangfire infrastructure — Backend hosts the dashboard and a placeholder server
// (queue=backend) ready for Faz 4 scheduler migrations (TranslationCleanup, MetricsAggregation).
// Enablement gate uses the SAME resolution as HangfireSetup (Hangfire-specific conn string
// first, PostgreSQL as fallback) so a deployment providing only ConnectionStrings:Hangfire
// still wires up the server and dashboard. When neither is present, Hangfire is skipped and
// a warn is logged just after app.Build() below.
var hangfireConnStr = HangfireSetup.ResolveConnectionString(builder.Configuration);
var hangfireEnabled = !string.IsNullOrEmpty(hangfireConnStr);
if (hangfireEnabled)
{
    builder.Services.AddInvektoHangfire("backend", hangfireConnStr);
    // G7 Faz 4: Backend recurring jobs (TranslationCleanup + MetricsAggregation)
    builder.Services.AddScoped<TranslationCleanupJob>();
    builder.Services.AddScoped<MetricsAggregationJob>();
    // FEAT-DBBK: Daily pg_dump custom-format backup with rolling retention.
    builder.Services.AddScoped<DbBackupJob>();
}

// QNB VPos — ödeme servisi
builder.Services.Configure<QnbVPosSettings>(builder.Configuration.GetSection("QnbVPos"));
builder.Services.AddSingleton<QnbVPosService>();

// Selective CORS for INMA Angular consumers of /api/v1/inma/nav (Seçenek C, 2026-04-16).
// Origins match the INMA bridge whitelist (parent INMA shell hosts).
// AllowCredentials NOT enabled: Angular HttpClient sends Bearer header explicitly,
// browser does NOT auto-propagate credentials cross-origin without it.
//
// VoicePocCors (FEAT-VFB F0.5 Chunk E, AD-35, 2026-05-26): named policy for the
// Voice Test page (voice.invekto.com:8443) — browser fetches /api/ops/tenants with
// the Dashboard JWT to populate the tenant dropdown. GET-only; AllowCredentials off
// (Bearer header passed explicitly). Default policy is NOT registered, so this only
// applies to endpoints with .RequireCors("VoicePocCors").
builder.Services.AddCors(options =>
{
    options.AddPolicy("InmaNavCors", policy => policy
        .WithOrigins("https://app.wapcrm.net", "https://developer.wapcrm.net", "http://localhost:4200")
        .WithMethods("GET")
        .AllowAnyHeader());

    options.AddPolicy("VoicePocCors", policy => policy
        .WithOrigins("https://voice.invekto.com:8443")
        .WithMethods("GET")
        .AllowAnyHeader());
});

var app = builder.Build();
if (hangfireEnabled)
{
    app.EnsureJobStorageInitialized();
    // Follow-up #3: Hangfire 1.8.14 + PostgreSql 1.20.10 quirk — newly-registered recurring
    // jobs land in hangfire.set with score=-1 and stay dormant until manually nudged. Sweep
    // once at Backend (leader) startup so new RecurringJob.AddOrUpdate calls schedule
    // immediately on next deploy.
    try
    {
        using var conn = new Npgsql.NpgsqlConnection(hangfireConnStr);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE hangfire.set SET score = EXTRACT(epoch FROM NOW())::bigint WHERE key = 'recurring-jobs' AND score = -1;";
        var updated = cmd.ExecuteNonQuery();
        if (updated > 0)
            app.Logger.LogInformation("Hangfire score=-1 nudge: {Count} recurring-jobs row(s) updated.", updated);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        app.Logger.LogWarning(ex, "Hangfire score=-1 nudge failed (non-fatal).");
    }

    // P10 orphan 'default' queue guard — microservice topology uses named queues
    // (backend, marketing-followup, automation, appointments, integrations,
    // waanalytics); no server listens to the 'default' queue. Any job scheduled
    // without an explicit queue (missing [Queue(...)] attribute on the class or
    // RecurringJobOptions.Queue on the registration) accumulates silently in
    // hangfire.jobqueue until an operator notices. This non-blocking startup
    // probe surfaces orphans early so deploys catch regressions before a latent
    // build-up (P9 S7b: 4-day, 1019-row accumulation).
    try
    {
        using var orphanConn = new Npgsql.NpgsqlConnection(hangfireConnStr);
        orphanConn.Open();
        using var orphanCmd = orphanConn.CreateCommand();
        orphanCmd.CommandText = "SELECT COUNT(*) FROM hangfire.jobqueue WHERE queue = 'default';";
        var orphanCount = Convert.ToInt64(orphanCmd.ExecuteScalar() ?? 0L);
        if (orphanCount > 0)
        {
            app.Logger.LogWarning(
                "[INV-JOB-006] {Count} job(s) stuck on 'default' queue — no server listens to it. " +
                "Missing [Queue(...)] attribute on job class/method or RecurringJobOptions.Queue on registration. " +
                "Drain via: DELETE FROM hangfire.jobqueue WHERE queue='default';",
                orphanCount);
        }
    }
    catch (Npgsql.NpgsqlException ex)
    {
        app.Logger.LogWarning(ex, "[INV-JOB-006] Guard probe failed (non-fatal).");
    }
}

// Enable traffic logging middleware (logs all HTTP request/response)
app.UseTrafficLogging(
    new[] { "/health", "/api/ops/", "/ops", "/assets/", "/favicon", "/logs", "/login" },
    new[] { ".js", ".css", ".svg", ".png", ".ico", ".woff", ".woff2", ".map" });

// GR-1.9: JWT auth middleware for protected API paths
// Webhook:AllowedIps — trusted IPs bypass JWT and use ?companyId= query param
var webhookIps = builder.Configuration.GetSection("Webhook:AllowedIps").Get<string[]>() ?? [];
// Normalize: store both raw and ::ffff: mapped forms so IPv6-mapped IPv4 matches
var webhookIpSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var ip in webhookIps)
{
    webhookIpSet.Add(ip);
    if (System.Net.IPAddress.TryParse(ip, out var parsed))
    {
        if (parsed.IsIPv4MappedToIPv6)
            webhookIpSet.Add(parsed.MapToIPv4().ToString());
        else
            webhookIpSet.Add($"::ffff:{ip}");
    }
}
// CORS for selective endpoints (INMA Angular consumers). Pipeline order: BEFORE JwtAuth
// so that preflight OPTIONS requests reach the CORS middleware before any auth gate.
app.UseCors();

if (jwtValidator != null)
{
    var jwtLogger = app.Services.GetRequiredService<JsonLinesLogger>();
    // /api/v1/inma/nav intentionally NOT whitelisted: endpoint accepts INMA JWT directly
    // via ExtractTenantFromBearer (welcome introspection) so INMA Angular shell can call
    // it with its own bearer. See plan 20260416-inma-nav-cors.
    // FEAT-LIW Chunk A: /api/v1/leads/intake/ opts out of JWT so the endpoint can
    // run its own per-tenant API key check (X-Invekto-Api-Key). Exclusions win over
    // the broader /api/v1/leads JWT inclusion; all other /api/v1/leads/* paths keep
    // Bearer JWT auth unchanged.
    var jwtRequiredPrefixes = new[]
    {
        "/api/v1/webhook/",
        "/api/v1/automation/",
        "/api/v1/outbound/",
        "/api/v1/flow-builder/flows/",
        "/api/v1/flow-builder/monitor/",
        "/api/v1/flow-builder/wizard/",
        "/api/v1/attribution/",
        "/api/v1/leads",
        "/api/v1/onboarding/",
        // FEAT-LIW Chunk C: tenant Dashboard LIW settings endpoints — tenant-scoped JWT
        // required; tenant_id is extracted exclusively from the JWT claim (never from
        // request body/query) to prevent IDOR across tenants.
        "/api/v1/tenant/landing/",
        // FEAT-TFM MVP + FEAT-DMP + FEAT-J2 + Payment: tenant-scoped JWT required. All
        // four consume tenant identity from the signed claim via TenantContext. Without
        // these prefixes the global JWT middleware skips the path entirely — handler
        // sees no TenantContext and returns 401 INV-AUTH-003 for every caller (valid
        // JWT or not). Reintroduced after latent-bug smoke revealed path-skip behavior.
        "/api/v1/tenant-settings/",
        "/api/v1/dynamic-fields",
        "/api/v1/optout",
        "/api/v1/payment/",
        // FEAT-EFS Drip Sequence: SPA-facing run history proxy. Sequence CRUD lives
        // under /api/v1/tenant-settings/followup-sequence (already covered above);
        // /api/v1/tenant/followup/ catches GET /runs and any future tenant-scoped
        // followup browse endpoints. Tenant-scoped JWT required (TenantContext
        // resolved from claim, never from path/body).
        "/api/v1/tenant/followup/",
        // FEAT-LIW Chunk B: service-to-service wa-direct internal endpoint REQUIRES JWT.
        // Automation generates a per-call service JWT (JwtGenerator.GenerateServiceToken
        // with tenant_id claim) so the existing JWT middleware enforces tenant binding;
        // the endpoint additionally checks the X-Internal-Service-Token header (defense
        // in depth — proves the caller is a peer Invekto service, not just any client
        // that happens to know how to mint a JWT) and validates that the JWT's tenant_id
        // claim matches the payload tenant_id. Iter 2 reinforcement of CQ9.
        "/api/internal/"
    };
    var jwtExcludedPrefixes = new[] { "/api/v1/leads/intake/" };
    app.UseJwtAuth(jwtValidator, jwtLogger, webhookIpSet, jwtRequiredPrefixes, jwtExcludedPrefixes);
}

// Enable static file serving for Dashboard UI (wwwroot/)
// Cache policy: HTML shell (index.html) MUST always revalidate so a fresh deploy reaches
// clients immediately; content-hashed assets keep their default caching. Without this the
// browser heuristically caches index.html and serves stale asset hashes -> old Dashboard nav
// keeps showing after deploy (INMA iframe especially). Q.
var spaStaticOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            ctx.Context.Response.Headers["Pragma"] = "no-cache";
            ctx.Context.Response.Headers["Expires"] = "0";
        }
    }
};
app.UseStaticFiles(spaStaticOptions);

// Start log cleanup service
_ = app.Services.GetRequiredService<LogCleanupService>();

var logger = app.Services.GetRequiredService<JsonLinesLogger>();

// Faz 1: Plan-based feature guard (after JwtAuth sets TenantContext)
TenantPlanCache? planCache = null;
if (!string.IsNullOrEmpty(pgConnectionString))
{
    planCache = new TenantPlanCache(pgConnectionString, logger);
    app.UseFeatureGuard(planCache, logger,
        ("/api/v1/flow-builder/", "FlowBuilder"),
        ("/api/v1/outbound/", "Outbound"));
}
app.UseAuthorization();

// G7: Hangfire dashboard — superadmin-only (tenant_id=0). Mounted after JWT middleware
// so HttpContext.User is populated before SuperAdminDashboardFilter runs.
if (hangfireEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new SuperAdminDashboardFilter() },
        DisplayStorageConnectionString = false
    });

    // G7: Cookie bridge for browser access to /hangfire.
    // HTML navigation cannot attach Authorization headers, so the dashboard sidebar link
    // hits /ops/hangfire-login?token=<localStorage.access_token> first. This endpoint
    // validates the JWT (tenant_id=0 required) and issues an HttpOnly+Secure cookie
    // scoped to /hangfire, then 302-redirects there. No duplicate credential surface.
    app.MapGet("/ops/hangfire-login", (HttpContext ctx, string? token, JwtValidator validator, JwtGenerator generator, JsonLinesLogger logger) =>
    {
        // Accepts two auth paths:
        //  (1) ?token=<jwt>  — tenant_id=0 JWT via localStorage (INMA-session flow).
        //  (2) Authorization: Basic <base64(user:pass)> — Ops mode (sessionStorage.ops_auth).
        // Valid either way, mint a short-lived superadmin JWT, drop it as an HttpOnly cookie
        // scoped to /hangfire, then redirect. Filter reuses the same cookie.
        string? issuedJwt = null;
        string? denyReason = null;

        if (!string.IsNullOrEmpty(token))
        {
            TenantContext? tctx;
            string? err;
            try { (tctx, err) = validator.ValidateToken(token); }
            catch (Exception ex) { tctx = null; err = ex.Message; }
            if (tctx == null) denyReason = $"token-invalid:{err}";
            else if (tctx.TenantId != 0) denyReason = $"token-non-superadmin:tenant={tctx.TenantId}";
            else issuedJwt = token;
        }
        else if (ValidateOpsAuth(ctx))
        {
            // Ops credentials verified; mint a 1-hour superadmin JWT (tenant_id=0).
            issuedJwt = generator.GenerateServiceToken(0, TimeSpan.FromHours(1));
        }
        else
        {
            denyReason = "no-token-and-basic-auth-invalid";
        }

        if (issuedJwt == null)
        {
            logger.SystemWarn($"[{ErrorCodes.JobDashboardUnauthorized}] hangfire-login rejected: {denyReason}");
            return Results.Json(new { error = denyReason ?? "unauthorized" }, statusCode: 401);
        }

        ctx.Response.Cookies.Append(SuperAdminDashboardFilter.CookieName, issuedJwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/hangfire",
            MaxAge = TimeSpan.FromHours(1)
        });
        return Results.Redirect("/hangfire");
    });

    // G7 Faz 4: Recurring job registration (idempotent via AddOrUpdate)
    var translationCleanupCron = builder.Configuration["TranslationCleanup:Cron"] ?? Cron.Hourly();
    // FEAT-LIW Chunk A: reclaim drained API-key rate-limit buckets every 5 minutes.
    // Pre-DB limiter accepts raw unknown keys (brute-force shield); Sweep bounds
    // dictionary growth so the MaxTrackedKeys cap doesn't stay saturated with
    // single-hit buckets from an attacker who has since stopped probing.
    // P10: Queue routing comes from the [Queue("backend")] attribute on
    // ApiKeyRateLimiter.SweepNow (same pattern as named job classes elsewhere);
    // without it, Hangfire defaults to "default" and no server listens to it —
    // the orphan-queue accumulation root cause of P9 S7b (1019 stuck rows
    // since 2026-04-18).
    RecurringJob.AddOrUpdate<ApiKeyRateLimiter>(
        "lead-intake:rate-limiter-sweep",
        limiter => limiter.SweepNow(),
        "*/5 * * * *");

    RecurringJob.AddOrUpdate<TranslationCleanupJob>(
        "backend:translation-cleanup",
        j => j.RunAsync(CancellationToken.None),
        translationCleanupCron);

    var metricsAggregationCron = builder.Configuration["MetricsAggregation:Cron"] ?? "*/5 * * * *";
    RecurringJob.AddOrUpdate<MetricsAggregationJob>(
        "backend:metrics-aggregation",
        j => j.RunAsync(CancellationToken.None),
        metricsAggregationCron);

    // FEAT-DBBK: Daily pg_dump (custom format) + 14-day rolling retention.
    var dbBackupCron = builder.Configuration["DbBackup:Cron"] ?? "0 3 * * *";
    RecurringJob.AddOrUpdate<DbBackupJob>(
        "backend:db-backup",
        j => j.RunAsync(CancellationToken.None),
        dbBackupCron);
}
else
{
    app.Services.GetRequiredService<JsonLinesLogger>().SystemWarn(
        $"[{ErrorCodes.JobStorageConnectionFailed}] Hangfire dashboard + server DISABLED: " +
        "ConnectionStrings:PostgreSQL is not configured. Recurring jobs will NOT run.");
}

// FEAT-VFB F0.5 Voice Test browser JWT bridge — symmetric to /ops/hangfire-login
// (line ~697) but JSON instead of cookie. The Voice Test page lives at
// https://voice.invekto.com:8443 (different origin from Dashboard), so the
// same-origin HttpOnly cookie bridge used for /hangfire is not applicable —
// the Dashboard caller mints a short-lived tenant=0 JWT here and passes it
// inline via the URL bridge (?token=<JWT>) consumed by voice-poc.html's
// inline script (history.replaceState scrubs the token from the address bar).
//
// Auth: Ops Basic Auth (sessionStorage.ops_auth from useAuth.ts loginWithOps).
// INMA-session callers don't need this endpoint — they already have a JWT in
// localStorage.access_token and the Dashboard Voice Test button uses that
// directly. This endpoint is the Ops-mode fallback (no JWT in localStorage).
//
// TTL: 30 minutes — Voice Test is a short-lived ops/demo tool; matches the
// expected single-session usage window without leaving long-lived tokens in
// browser memory.
app.MapGet("/ops/voice-jwt", (HttpContext ctx, JsonLinesLogger logger) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    if (!ValidateOpsAuth(ctx))
    {
        logger.SystemWarn($"[{ErrorCodes.AuthUnauthorized}] voice-jwt rejected: ops basic auth invalid (rid={requestId})");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized,
                "Voice Test JWT mint icin gecerli Ops Basic Auth gerekli. Dashboard'a tekrar login olun.", requestId),
            statusCode: 401);
    }
    if (jwtGenerator == null)
    {
        logger.SystemError($"[{ErrorCodes.AuthJwtGeneratorUnavailable}] voice-jwt: JwtGenerator not configured (server misconfig, rid={requestId})");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthJwtGeneratorUnavailable,
                "Voice Test JWT mint servisi su an kullanilamiyor (yapilandirma hatasi). Yoneticinize bildirin.", requestId),
            statusCode: 503);
    }
    // FEAT-VFB F0.5: mint a 30min ADMIN token (NOT service). The SAME token is URL-bridged to
    // voice.invekto.com and powers BOTH (a) the cross-origin dropdown fetches to
    // /api/ops/tenants + /api/v1/flow-builder/flows/{tid} — whose ValidateOpsAuth Bearer path
    // requires Role==admin AND TenantId==0 (D027 narrowing) — AND (b) the VoiceRuntime WS
    // handshake, which is role-agnostic (only checks TenantId==0, VoicePocEndpoints.cs).
    // A service-role token (the old value) is rejected by ValidateOpsAuth, so the dropdowns
    // stayed empty. tenant_id=0 keeps the superadmin/no-tenant scope unchanged.
    var jwt = jwtGenerator.GenerateToken(0, "admin", "voice_test", TimeSpan.FromMinutes(30));
    logger.StepInfo($"voice-jwt: 30min admin JWT issued (tenant=0, rid={requestId})", requestId);
    return Results.Ok(new { token = jwt, expires_in = 1800, token_type = "Bearer" });
});

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
        // Audit fix D027 (2026-04-29 Batch D — HIGH risk auth touchpoint, Q karari G1):
        // Onceki kod sadece Role=="admin" kontrol ediyordu — tenant-admin
        // (Role=admin, TenantId>0) tum /api/ops/* yuzeyine erisim aliyordu (30+ call
        // site). SuperAdmin canonical TenantId=0 (TenantContext.TenantId int non-null,
        // 0 = no-tenant scope). Tenant gate eklendi: Role=="admin" AND TenantId==0.
        if (jwtValidator != null)
        {
            var (context, _) = jwtValidator.ValidateToken(token);
            if (context?.Role == "admin" && context.TenantId == 0) return true;
        }

        // inma JWT fallback REJECTED for ops auth.
        // Audit fix D027 (Batch D 2026-04-29): InmaTokenContext yalnizca CompanyCode
        // (opaque string, ornek "5050" veya "dentadavista") tasiyor, TenantId YOK.
        // Tenant resolution async DB lookup gerekir (TenantRegistryRepository
        // .ResolveOrCreateByInmaCodeAsync). 30+ call site senkron, async refactor
        // scope creep — ek paket. Mevcut ValidateOpsAuth bool donerken
        // GetAwaiter().GetResult() kullanma cesareti riskli (deadlock + thread
        // pool starvation).
        //
        // Q karari G1 (2026-04-29 00:25 UTC): SuperAdmin paneline inse internal
        // JWT ile login (Q kendi credentials). inma SSO yolunu kullanmiyor.
        // Security-first: ops endpoint'leri SADECE inse internal JWT TenantId=0
        // kabul eder. inma JWT kullanicilari tenant endpoint'lerine kendi
        // ChatRole=admin scope'u icinde erisir, platform-level /api/ops/* yuzeyine
        // DEGIL.
        //
        // Future option (B0-AUTH backlog): CompanyCode -> tenant_id async resolution
        // + check == 0; 30+ call site async refactor + InmaTokenContext genisletme.
        var introspector = ctx.RequestServices.GetService<InmaTokenIntrospector>();
        if (introspector != null)
        {
            // Defense-in-depth: inma JWT validation hala calisir (sertifika spoof
            // tespiti, audit log) AMA admin gate'i gecmesin. Role=="admin" log
            // dahil — operator manuel decision (Q kendi inma SSO'ya gectiyse
            // gerekli refactor B0-AUTH).
            var introspectResult = introspector.ValidateAsync(token).GetAwaiter().GetResult();
            if (introspectResult.Context?.Role == "admin")
            {
                // Inma JWT admin tespit edildi ama TenantId=0 enforce edilemez.
                // Bilinçli reddet — security-first.
                return false;
            }
        }

        return false;
    }

    return false;
}

// A6 (Faz A, 2026-05-12): Tenant-scoped fallback for Ops Knowledge endpoints.
// Knowledge proxy endpoints under /api/ops/knowledge/{tenantId}/* were originally
// Ops-admin-only (SuperAdmin TenantId=0 via inse JWT, or Basic auth). A normal
// tenant user opening "Bilgi Bankasi" in Dashboard hit 401 because their INMA-exchanged
// inse JWT carries TenantId>0 — security-first by D027, but UX-blocking because
// no parallel /api/v1/knowledge set exists. Rather than fan out 9 new endpoint
// mappings (route duplication), the existing Ops gate falls through to a JWT
// tenant-bound check: the Bearer-token TenantContext.TenantId must equal the
// path tenantId. This preserves cross-tenant Ops auth (untouched true branch)
// while letting tenants manage their OWN Knowledge.
//
// Returns false when no JWT, malformed JWT, or tenant mismatch. The token parse
// mirrors JwtAuthMiddleware (BOM strip + trim) so the same A3 hardening applies.
// Templates endpoints (tenantId=0) remain Ops-only de-facto because no normal
// user JWT has TenantId=0.
bool ValidateJwtTenantAuth(HttpContext ctx, int tenantId)
{
    if (jwtValidator == null) return false;
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return false;
    var token = authHeader["Bearer ".Length..].Trim().TrimStart('﻿').Trim();
    var (tc, _) = jwtValidator.ValidateToken(token);
    return tc != null && tc.TenantId == tenantId;
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

// ---- WebChat operator proxy (superadmin chat window) ----

app.MapGet("/api/ops/webchat/conversations", async (HttpContext ctx, WebChatClient webChatClient) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var includeClosed = ctx.Request.Query["include_closed"].FirstOrDefault() == "true";
    var result = await webChatClient.GetConversationsAsync(includeClosed, ctx.RequestAborted);
    if (result == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendMicroserviceUnavailable, "WebChat service unavailable", "-"), statusCode: 502);

    return Results.Ok(result);
});

app.MapGet("/api/ops/webchat/conversations/{id}/messages", async (long id, HttpContext ctx, WebChatClient webChatClient) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var result = await webChatClient.GetMessagesAsync(id, ctx.RequestAborted);
    if (result == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendMicroserviceUnavailable, "WebChat service unavailable", "-"), statusCode: 502);

    return Results.Ok(result);
});

app.MapPost("/api/ops/webchat/conversations/{id}/messages", async (long id, HttpContext ctx, WebChatClient webChatClient) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
    var content = bodyDoc.RootElement.TryGetProperty("content", out var c) ? c.GetString() : null;
    if (string.IsNullOrEmpty(content))
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "content is required", "-"), statusCode: 400);

    var result = await webChatClient.SendOperatorMessageAsync(id, content, ctx.RequestAborted);
    if (result == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendMicroserviceError, "Failed to send message", "-"), statusCode: 502);

    return Results.Ok(result);
});

app.MapPut("/api/ops/webchat/conversations/{id}/close", async (long id, HttpContext ctx, WebChatClient webChatClient) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var closed = await webChatClient.CloseConversationAsync(id, ctx.RequestAborted);
    if (!closed)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendMicroserviceError, "Failed to close conversation", "-"), statusCode: 502);

    return Results.Ok(new { status = "closed" });
});

app.MapPut("/api/ops/webchat/conversations/{id}/route-ai", async (long id, HttpContext ctx, WebChatClient webChatClient) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var routed = await webChatClient.RouteToAiAsync(id, ctx.RequestAborted);
    if (!routed)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendMicroserviceError, "Failed to route to AI", "-"), statusCode: 502);

    return Results.Ok(new { status = "ai" });
});

app.MapGet("/api/ops/webchat/conversations/{id}/visitor", async (long id, HttpContext ctx, WebChatClient webChatClient) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var result = await webChatClient.GetVisitorAsync(id, ctx.RequestAborted);
    if (result == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendMicroserviceUnavailable, "WebChat service unavailable", "-"), statusCode: 502);

    return Results.Ok(result);
});

// ============================================
// PKT-11: VoiceAI proxy routes
// ============================================

app.MapPost("/api/v1/voice/transcribe", async (HttpContext ctx, VoiceAIClient voiceClient) =>
{
    if (!ctx.Request.HasFormContentType)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAINoAudioFile, "multipart/form-data bekleniyor", ctx.TraceIdentifier),
            statusCode: 400);

    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files.GetFile("audio");
    if (file == null || file.Length == 0)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.VoiceAINoAudioFile, "'audio' alanında ses dosyası bulunamadı", ctx.TraceIdentifier),
            statusCode: 400);

    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    using var stream = file.OpenReadStream();
    var (statusCode, body) = await voiceClient.ProxyTranscribeAsync(
        stream, file.FileName, authHeader, ctx.TraceIdentifier, ctx.RequestAborted);

    if (body == null)
        return Results.StatusCode(statusCode);

    return Results.Text(body, "application/json", statusCode: statusCode);
});

// Dashboard: Service health with response times
app.MapGet("/api/ops/health", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, KnowledgeClient knowledgeClient, AppointmentsClient appointmentsClient, IntegrationsClient integrationsClient, WhatsAppAnalyticsClient waAnalyticsClient, MarketingClient marketingClient, WebChatClient webChatClient, VoiceAIClient voiceAIClient) =>
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

    // WebChat - check health with timing
    var swWebChat = System.Diagnostics.Stopwatch.StartNew();
    var webChatHealthy = await webChatClient.CheckHealthAsync();
    swWebChat.Stop();

    services.Add(new
    {
        name = ServiceConstants.WebChatServiceName,
        status = webChatHealthy ? "ok" : "unavailable",
        responseTimeMs = webChatHealthy ? (int?)swWebChat.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = webChatHealthy ? null : "Service unreachable"
    });

    // VoiceAI - check health with timing (PKT-11)
    var swVoiceAI = System.Diagnostics.Stopwatch.StartNew();
    var voiceAIHealthy = await voiceAIClient.CheckHealthAsync();
    swVoiceAI.Stop();

    services.Add(new
    {
        name = ServiceConstants.VoiceAIServiceName,
        status = voiceAIHealthy ? "ok" : "unavailable",
        responseTimeMs = voiceAIHealthy ? (int?)swVoiceAI.ElapsedMilliseconds : null,
        uptimeSeconds = (long?)null,
        lastCheck = now,
        error = voiceAIHealthy ? null : "Service unreachable"
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

// Dashboard: Translation Stats (Ops auth)
// HOTFIX 2026-05-26: TranslationCacheRepository conditional-registered (line ~387, inside
// `if (!string.IsNullOrEmpty(pgConnectionString))`). .NET 8.0.19 RequestDelegateFactory
// stricter — without [FromServices] the parameter resolves as "Body (Inferred)" at startup
// and the host crashes with InvalidOperationException. Adding the attribute forces explicit
// DI lookup. (Other 244 endpoints in this file rely on the same implicit pattern but their
// services are unconditionally registered, so the fallback heuristic still works for them.)
app.MapGet("/api/ops/stats/translations", async (HttpContext ctx, [FromServices] TranslationCacheRepository cacheRepo) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    await using var conn = await cacheRepo.GetConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT
            COUNT(*) AS total_cached,
            COUNT(*) FILTER (WHERE expires_at > NOW()) AS active_cached,
            COUNT(DISTINCT tenant_id) AS tenant_count,
            COUNT(DISTINCT target_language) AS language_count,
            MIN(created_at) AS oldest_entry,
            MAX(created_at) AS newest_entry
        FROM message_translations";

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();

    var totalCached = reader.GetInt64(0);
    var activeCached = reader.GetInt64(1);
    var tenantCount = reader.GetInt64(2);
    var languageCount = reader.GetInt64(3);
    var oldest = reader.IsDBNull(4) ? (DateTime?)null : reader.GetDateTime(4);
    var newest = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5);

    // Top target languages
    await using var cmd2 = conn.CreateCommand();
    cmd2.CommandText = @"
        SELECT target_language, COUNT(*) AS cnt
        FROM message_translations
        WHERE expires_at > NOW()
        GROUP BY target_language
        ORDER BY cnt DESC
        LIMIT 10";

    var languages = new List<object>();
    await using var reader2 = await cmd2.ExecuteReaderAsync();
    while (await reader2.ReadAsync())
    {
        languages.Add(new { language = reader2.GetString(0), count = reader2.GetInt64(1) });
    }

    return Results.Ok(new
    {
        total_cached = totalCached,
        active_cached = activeCached,
        expired = totalCached - activeCached,
        tenant_count = tenantCount,
        language_count = languageCount,
        oldest_entry = oldest,
        newest_entry = newest,
        top_languages = languages
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
        "Invekto.WebChat" => "InvektoWebChat",
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
app.MapGet("/api/ops/test/{serviceName}/{*path}", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, KnowledgeClient knowledgeClient, AppointmentsClient appointmentsClient, IntegrationsClient integrationsClient, WhatsAppAnalyticsClient waAnalyticsClient, MarketingClient marketingClient, WebChatClient webChatClient, VoiceAIClient voiceAIClient, string serviceName, string? path) =>
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

        if (serviceName == "webchat")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await webChatClient.TestEndpointAsync(endpoint);
            sw.Stop();

            return Results.Ok(new
            {
                success = result.Success,
                statusCode = result.StatusCode,
                durationMs = sw.ElapsedMilliseconds,
                message = result.Message
            });
        }

        if (serviceName == "voiceai")
        {
            var endpoint = "/" + (path ?? "health");
            var result = await voiceAIClient.TestEndpointAsync(endpoint);
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
app.MapPost("/api/v1/webhook/event", async (HttpContext ctx, JsonLinesLogger jsonLogger) =>
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

    // PR-3b-2: WapCRM posts BOTH inbound messages AND cxapi delivery acks to this ONE webhook. Read the
    // body raw and shape-discriminate: a root non-empty messages[] => inbound (the unchanged path below);
    // a root Status/InstanceMessageID with NO messages[] => delivery ack. The typed framework binder is
    // dropped on purpose — the ack's InstanceID is an INT while the inbound envelope's is a STRING, so the
    // two shapes can never bind into one model; discrimination must happen BEFORE typed deserialization.
    IncomingWebhookEvent? webhookEvent;
    using (var bodyDoc = await TryParseWebhookBodyAsync())
    {
        if (bodyDoc == null)
        {
            sw.Stop();
            var reqCtx = RequestContext.Create(tenantContext.TenantId.ToString(), "-");
            jsonLogger.RequestError("Webhook: malformed or empty JSON body", reqCtx, "/api/v1/webhook/event", sw.ElapsedMilliseconds, ErrorCodes.IntegrationWebhookInvalidPayload);
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.IntegrationWebhookInvalidPayload, "request body must be valid JSON", requestId),
                statusCode: 400);
        }

        var root = bodyDoc.RootElement;
        // Discriminate strictly by the INMA shape contract (tested in WapCrmAckMappingTests): an inbound
        // message ALWAYS carries a root "messages" property; a delivery ack NEVER does. An ambiguous body
        // that has BOTH (e.g. {"messages":[],"Status":1}) is NOT a clean ack — it falls through to the
        // inbound path and is rejected by the existing messages-required 400 below, exactly as before.
        if (WapCrmAckMapping.IsDeliveryAckShape(root))
        {
            sw.Stop();
            return await HandleDeliveryAckAsync(root);
        }

        // Inbound message path: deserialize to the typed model with cached Web options (mirrors the
        // previous framework model-binding). The Automation forward below re-serializes this exact object.
        try
        {
            webhookEvent = root.Deserialize<IncomingWebhookEvent>(BackendWebhookJson.Options);
        }
        catch (JsonException)
        {
            webhookEvent = null;
        }
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

        // FEAT-PHOTO wire-up patch (2026-04-28, iter 1 fix CQ1+CQ2+CQ6+CQ12): media
        // event hop. Iter 0 implementation fire-and-forget Task.Run + bare catch idi;
        // Codex iter 0 FAIL: (a) lifecycle loss on shutdown, (b) unbounded ThreadPool
        // pressure, (c) typed catch ihlali, (d) silent skip paths. Iter 1: synchronous
        // await; INMA webhook latency'si idempotency INSERT + leads UPDATE icin 50-200ms
        // artar — INMA at-least-once timeout politikasi (>= 30s) icinde rahatlikla
        // tolere edilir. Tum skip path'lerinde INV-AT-086 audit log; failure path'inde
        // tipli catch (NpgsqlException + JsonException + OperationCanceledException +
        // InvalidOperationException + HttpRequestException). PhotoInboundHandler kendi
        // katmanindaki idempotency tablosu (lead_id, media_url_hash) UNIQUE INSERT
        // ON CONFLICT DO NOTHING ile at-least-once dedup'i hallediyor; bu hop sadece
        // ingress wiring. Plan arch/plans/20260428-feat-photo-wireup-patch.json AC4 + Q2.
        // CQ6 (Codex iter 2) fix: BATCHED lead lookup. Iter 1/2 implementation per-
        // message SELECT yapiyordu (N+1). Iter 3'de tum media events'in distinct
        // phone'lari toplandi -> tek SELECT id, phone FROM leads WHERE tenant_id=@tid
        // AND phone = ANY(@phones) -> in-memory dict ile match. PostgreSQL ANY array
        // operator native batched lookup; tek round-trip + tek connection. Pilot ve
        // post-pilot her olcekte uygun.
        //
        // CQ12/Q3 (Codex iter 2) fix: INV-AT-086 SADECE semantic 'lead not matched'
        // icin; DI-null/TenantContext-null/malformed paths INV-AT-087 PhotoRequestHook
        // Transient kullanir (canonical errors.md kontrati).
        var photoHandler = ctx.RequestServices.GetService<Invekto.Backend.Services.Photos.IPhotoInboundHandler>();
        if (photoHandler == null || pgFactory == null)
        {
            // Defensive: DI hic register edilmediyse (test ortami / partial config)
            // hop atla + audit log (INV-AT-087 transient infra mis-config; INV-AT-086
            // sadece semantic lead-not-matched icin).
            if (webhookEvent.Messages.Any(m => Invekto.Backend.Endpoints.InmaInboundMediaEndpoint.IsMediaMessage(m)))
            {
                jsonLogger.SystemWarn(
                    $"[{ErrorCodes.PhotoRequestHookTransient}] /webhook/event media hop: " +
                    $"PhotoInboundHandler DI not configured (photoHandler={photoHandler != null}, " +
                    $"pgFactory={pgFactory != null}) tenant={tenantContext.TenantId} — media events skipped");
            }
        }
        else
        {
            // §1. Collect valid media events; INV-AT-087 log for malformed (transient
            // payload bug, distinct from semantic skip).
            var mediaEvents = new List<(WebhookMessage Msg, string Phone)>();
            foreach (var msg in webhookEvent.Messages)
            {
                if (!Invekto.Backend.Endpoints.InmaInboundMediaEndpoint.IsMediaMessage(msg))
                    continue;

                var phone = Invekto.Backend.Endpoints.InmaInboundMediaEndpoint.ExtractPhoneFromChatId(msg.ChatId);
                if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(msg.ImageUrl))
                {
                    jsonLogger.SystemWarn(
                        $"[{ErrorCodes.PhotoRequestHookTransient}] /webhook/event media hop: " +
                        $"malformed media event tenant={tenantContext.TenantId} chat_id={msg.ChatId} " +
                        $"phone_present={!string.IsNullOrEmpty(phone)} url_present={!string.IsNullOrEmpty(msg.ImageUrl)} " +
                        $"(skipping — INMA payload bug)");
                    continue;
                }
                mediaEvents.Add((msg, phone));
            }

            if (mediaEvents.Count > 0)
            {
                try
                {
                    // §2. Single batched lookup — distinct phones -> id map.
                    var distinctPhones = mediaEvents.Select(e => e.Phone).Distinct().ToArray();
                    var phoneToLead = new Dictionary<string, int>(distinctPhones.Length);

                    await using (var conn = await pgFactory.OpenConnectionAsync(ctx.RequestAborted))
                    await using (var cmd = new Npgsql.NpgsqlCommand(
                        "SELECT id, phone FROM leads WHERE tenant_id = @tid AND phone = ANY(@phones)",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("tid", tenantContext.TenantId);
                        cmd.Parameters.AddWithValue("phones", distinctPhones);
                        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
                        while (await reader.ReadAsync(ctx.RequestAborted))
                        {
                            phoneToLead[reader.GetString(1)] = reader.GetInt32(0);
                        }
                    }

                    // §3. Loop media events; lookup from dict + handler call.
                    foreach (var (msg, phone) in mediaEvents)
                    {
                        // CQ5 (Codex iter 3) fix: explicit local null guard yerine
                        // null-forgiving operator project policy ihlali. msg.ImageUrl
                        // null/empty collection step'inde (line 1944-1951) zaten elendi;
                        // defensive local copy ile null-forgiving kaldiriliyor.
                        var imageUrl = msg.ImageUrl;
                        if (string.IsNullOrEmpty(imageUrl)) continue;

                        if (!phoneToLead.TryGetValue(phone, out var leadId))
                        {
                            jsonLogger.StepInfo(
                                $"[{ErrorCodes.PhotoRequestLeadResolveSkip}] /webhook/event media hop: " +
                                $"no lead matches phone tenant={tenantContext.TenantId} phone={phone} " +
                                $"(media event dropped — lead henuz olusmamis veya phone normalization mismatch)",
                                requestId);
                            continue;
                        }

                        await photoHandler.HandleInboundMediaAsync(
                            tenantContext.TenantId, leadId, imageUrl, ctx.RequestAborted);
                    }
                }
                catch (Npgsql.NpgsqlException ex)
                {
                    // DB transient — INMA at-least-once redelivery sonraki window'da
                    // retry yapacak (idempotency anchor henuz yazilmadi -> safe).
                    jsonLogger.SystemWarn(
                        $"[{ErrorCodes.PhotoInboundHandlerDbError}] /webhook/event media hop: " +
                        $"batched DB error tenant={tenantContext.TenantId} count={mediaEvents.Count}: {ex.Message}");
                }
                catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
                {
                    // Caller canceled — INMA at-least-once retry pattern'i kapsar.
                    jsonLogger.SystemWarn(
                        $"[{ErrorCodes.PhotoInboundCancelled}] /webhook/event media hop: " +
                        $"cancelled tenant={tenantContext.TenantId} processed_count={mediaEvents.Count}");
                }
                catch (InvalidOperationException ex)
                {
                    // PhotoInboundHandler DI mis-config veya pgFactory state hatasi —
                    // non-DB transient (INV-AT-087).
                    jsonLogger.SystemWarn(
                        $"[{ErrorCodes.PhotoRequestHookTransient}] /webhook/event media hop: " +
                        $"DI/state error tenant={tenantContext.TenantId}: {ex.Message}");
                }
            }
        }
    }

    // Instance filtering: check if this instance is enabled + assigned to a flow
    var instanceRepo = ctx.RequestServices.GetService<InstanceRepository>();
    if (instanceRepo != null && !string.IsNullOrEmpty(webhookEvent.InstanceId))
    {
        var hasRecords = await instanceRepo.HasInstanceRecordsAsync(tenantContext.TenantId);
        if (hasRecords)
        {
            var instStatus = await instanceRepo.GetInstanceStatusAsync(tenantContext.TenantId, webhookEvent.InstanceId);
            // instStatus == null: unknown instance (not yet synced) → pass through
            if (instStatus != null)
            {
                if (!instStatus.IsEnabled)
                {
                    jsonLogger.StepInfo(
                        $"{ErrorCodes.AutomationInstanceDisabled}: instance={webhookEvent.InstanceId} ({instStatus.InstanceName}) disabled, skipping Automation",
                        requestId);
                    return Results.Json(new { status = "filtered", reason = "instance_disabled", instance_id = webhookEvent.InstanceId }, statusCode: 202);
                }
                if (instStatus.FlowId == null)
                {
                    jsonLogger.StepInfo(
                        $"{ErrorCodes.AutomationInstanceUnassigned}: instance={webhookEvent.InstanceId} ({instStatus.InstanceName}) unassigned, skipping Automation",
                        requestId);
                    return Results.Json(new { status = "filtered", reason = "instance_unassigned", instance_id = webhookEvent.InstanceId }, statusCode: 202);
                }
            }
        }
        // hasRecords == false → old behavior (no instance config, pass through to Automation)
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

    // PR-3b-2 local helpers (hoisted) -------------------------------------------------------------
    // Parse the request body once; return null on malformed/empty JSON so the caller maps it to the
    // same 400 as before. OperationCanceledException (client abort) intentionally propagates.
    async Task<JsonDocument?> TryParseWebhookBodyAsync()
    {
        try
        {
            return await JsonDocument.ParseAsync(ctx.Request.Body, default, ctx.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // cxapi delivery ack: map the PascalCase WapCRM ack -> snake_case DeliveryStatusRequest and forward
    // to Outbound (the correlation owner). Tenant comes from companyId/TenantContext, never the payload.
    // ALWAYS returns 202 to WapCRM — a 404 (wamid not correlated: gate closed / bridge / non-cxapi send),
    // a transport failure, or an unsupported status are logged but never propagated (fire-and-forget ack;
    // WapCRM redelivery is safe because the Outbound apply is idempotent).
    async Task<IResult> HandleDeliveryAckAsync(JsonElement ackRoot)
    {
        WapCRMCloudAPIAckWebHookModel? ack;
        try
        {
            ack = ackRoot.Deserialize<WapCRMCloudAPIAckWebHookModel>(BackendWebhookJson.Options);
        }
        catch (JsonException)
        {
            ack = null;
        }

        var mappedStatus = ack == null ? null : WapCrmAckMapping.MapStatus(ack.Status);
        if (ack == null || string.IsNullOrWhiteSpace(ack.InstanceMessageID) || mappedStatus == null)
        {
            jsonLogger.StepInfo(
                $"[{ErrorCodes.CxapiDeliveryAckForwardFailed}] cxapi ack ignored (no-op): " +
                $"status_int={ack?.Status}, has_wamid={!string.IsNullOrWhiteSpace(ack?.InstanceMessageID)}, " +
                $"tenant={tenantContext.TenantId}", requestId);
            return Results.Json(new { status = "accepted", request_id = requestId, message = "ack ignored (unsupported)" }, statusCode: 202);
        }

        var deliveryReq = new DeliveryStatusRequest
        {
            ExternalMessageId = ack.InstanceMessageID,
            Status = mappedStatus,
            FailedReason = mappedStatus == "failed" ? WapCrmAckMapping.TruncateFailedReason(ack.ReasonDetailForNotSent) : null,
            InstanceId = ack.InstanceID
        };

        var outboundClient = ctx.RequestServices.GetService<OutboundClient>();
        if (outboundClient == null)
        {
            jsonLogger.SystemWarn(
                $"[{ErrorCodes.CxapiDeliveryAckForwardFailed}] OutboundClient DI not configured — delivery ack dropped " +
                $"tenant={tenantContext.TenantId} external_id={deliveryReq.ExternalMessageId}");
            return Results.Json(new { status = "accepted", request_id = requestId, message = "Delivery ack accepted" }, statusCode: 202);
        }

        // Tenant flows to Outbound via a short-lived service JWT (tenant=companyId) when the inbound was
        // IP-whitelisted (no bearer) — identical to the Automation forward pattern above. The whole forward
        // is wrapped so the public webhook ALWAYS returns 202: OutboundClient.ProxyPostAsync already maps
        // transport failures to 502/504 tuples (logged below), and this typed catch is the belt-and-
        // suspenders backstop for any other transport-layer throw (JWT mint / serialize / request abort) —
        // never a 5xx to WapCRM, whose at-least-once redelivery is safe against the idempotent apply.
        try
        {
            var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) && jwtGenerator != null)
            {
                var tempToken = jwtGenerator.GenerateToken(
                    tenantContext.TenantId, "system", "webhook_proxy",
                    TimeSpan.FromMinutes(5), tenantContext.UserId.ToString());
                authHeader = $"Bearer {tempToken}";
            }

            var (statusCode, _) = await outboundClient.ProxyPostAsync(
                "/api/v1/webhook/delivery-status",
                JsonSerializer.Serialize(deliveryReq),
                authHeader, requestId, ctx.RequestAborted);

            if (statusCode is < 200 or >= 300)
            {
                jsonLogger.StepInfo(
                    $"[{ErrorCodes.CxapiDeliveryAckForwardFailed}] delivery ack forward -> Outbound returned {statusCode}: " +
                    $"external_id={deliveryReq.ExternalMessageId}, status={deliveryReq.Status}, tenant={tenantContext.TenantId} " +
                    $"(swallowed; WapCRM redelivery is idempotent)", requestId);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            jsonLogger.SystemWarn(
                $"[{ErrorCodes.CxapiDeliveryAckForwardFailed}] delivery ack forward threw {ex.GetType().Name}: " +
                $"external_id={deliveryReq.ExternalMessageId} tenant={tenantContext.TenantId} " +
                $"(swallowed; WapCRM redelivery is idempotent)");
        }

        return Results.Json(new { status = "accepted", request_id = requestId, message = "Delivery ack accepted" }, statusCode: 202);
    }
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
// Auth: internal API key OR whitelisted IP (INMA calls this endpoint)
app.MapPost("/api/v1/chat/analyze", async (
    HttpContext ctx,
    ChatAnalysisClient chatClient,
    JsonLinesLogger jsonLogger,
    ChatAnalysisRequest? analysisRequest) =>
{
    // Auth gate: require internal API key or whitelisted IP
    var apiKeyHeader = ctx.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
    var hasValidApiKey = !string.IsNullOrEmpty(internalApiKey) && apiKeyHeader == internalApiKey;
    var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
    var isWhitelistedIp = webhookIpSet.Contains(remoteIp) || remoteIp == "127.0.0.1" || remoteIp == "::1";
    if (!hasValidApiKey && !isWhitelistedIp)
    {
        jsonLogger.SystemWarn($"Rejected /api/v1/chat/analyze: unauthorized (ip={remoteIp})");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Unauthorized", "-"),
            statusCode: 401);
    }

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

// ============================================================
// Onboarding status (PKT-2: aggregated from Knowledge + Automation + tenant_registry)
// ============================================================

app.MapGet("/api/v1/onboarding/status", async (HttpContext ctx, [FromServices] OnboardingStatusService onboardingService, JsonLinesLogger jsonLogger) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context missing", "-"),
            statusCode: 401);
    }

    try
    {
        var status = await onboardingService.GetStatusAsync(tenantContext.TenantId);
        return Results.Ok(status);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLogger.StepError($"[{ErrorCodes.BackendOnboardingStatusFailed}] Onboarding status DB error for tenant {tenantContext.TenantId}: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.BackendOnboardingStatusFailed, "Onboarding durumu hesaplanamadi: veritabani hatasi", requestId),
            statusCode: 500);
    }
    catch (HttpRequestException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLogger.StepError($"[{ErrorCodes.BackendOnboardingStatusFailed}] Onboarding status service call failed for tenant {tenantContext.TenantId}: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.BackendOnboardingStatusFailed, "Onboarding durumu hesaplanamadi: servis baglanti hatasi", requestId),
            statusCode: 500);
    }
});

// Endpoint discovery - returns all services' endpoints (aggregated)
app.MapGet("/api/ops/endpoints", async (HttpContext ctx, ChatAnalysisClient chatClient, AutomationClient automationClient, AgentAIClient agentAIClient, OutboundClient outboundClient, KnowledgeClient knowledgeClient, AppointmentsClient appointmentsClient, IntegrationsClient integrationsClient, WhatsAppAnalyticsClient waAnalyticsClient, MarketingClient marketingClient, WebChatClient webChatClient, VoiceAIClient voiceAIClient) =>
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

            // FEAT-PROJELER (PKT-14): cxapi WhatsApp approved-template list + Projeler project CRUD proxies
            new() { Method = "GET", Path = "/api/v1/settings/wa-templates", Description = "List tenant WhatsApp approved (HSM) templates from cxapi (read-only)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/outbound/projects", Description = "List Projeler projects proxy (Backend -> Outbound)", Auth = "Bearer", Category = "API" },
            new() { Method = "GET", Path = "/api/v1/outbound/projects/{id}", Description = "Get project detail (+targets) proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/projects", Description = "Create project (+targets) proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/outbound/projects/{id}", Description = "Update project (partial +targets replace) proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "DELETE", Path = "/api/v1/outbound/projects/{id}", Description = "Archive (soft-delete) project proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/projects/{id}/send/pause", Description = "Pause a project run (queued->paused) proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/projects/{id}/send/resume", Description = "Resume a paused project run (paused->queued) proxy", Auth = "Bearer", Category = "API" },
            new() { Method = "POST", Path = "/api/v1/outbound/projects/{id}/send/cancel", Description = "Cancel a project run (remaining->cancelled) proxy", Auth = "Bearer", Category = "API" },

            // FEAT-TFM MVP: tenant-scoped semantic field-mapping CRUD
            new() { Method = "GET", Path = "/api/v1/tenant-settings/field-mapping", Description = "Read tenant semantic→INMA field mapping", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/tenant-settings/field-mapping", Description = "Upsert tenant semantic→INMA field mapping (validate + cache invalidate)", Auth = "Bearer", Category = "API" },

            // FEAT-MCC: tenant-scoped multi-city campaign config CRUD
            new() { Method = "GET", Path = "/api/v1/tenant-settings/campaign-config", Description = "Read tenant multi-city campaign config (campaigns[], cities[], dates[])", Auth = "Bearer", Category = "API" },
            new() { Method = "PUT", Path = "/api/v1/tenant-settings/campaign-config", Description = "Upsert tenant multi-city campaign config (validate + cache invalidate)", Auth = "Bearer", Category = "API" },

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

            // Onboarding (PKT-2)
            new() { Method = "GET", Path = "/api/v1/onboarding/status", Description = "Onboarding status (aggregated)", Auth = "Bearer JWT", Category = "Onboarding" },

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

    // Fetch WebChat endpoints (internal call)
    var webChatEndpoints = await webChatClient.GetEndpointsAsync();

    // Fetch VoiceAI endpoints (internal call, PKT-11)
    var voiceAIEndpoints = await voiceAIClient.GetEndpointsAsync();

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
    if (webChatEndpoints != null)
    {
        services.Add(webChatEndpoints);
    }
    if (voiceAIEndpoints != null)
    {
        services.Add(voiceAIEndpoints);
    }

    return Results.Ok(new { services });
});

// Postman collection download - dynamically generated from endpoint discovery
// GET /api/ops/postman — Serve static Postman collection JSON (from postman/ folder, deployed with publish)
app.MapGet("/api/ops/postman", async (HttpContext ctx) =>
{
    if (!ValidateOpsAuth(ctx))
    {
        return OpsUnauthorized(ctx);
    }

    var postmanPath = Path.Combine(AppContext.BaseDirectory, "postman", "InvektoServis.postman_collection.json");
    if (!File.Exists(postmanPath))
    {
        return Results.Json(new { error = "Postman collection file not found." }, statusCode: 404);
    }

    var json = await File.ReadAllTextAsync(postmanPath);
    ctx.Response.Headers.ContentDisposition = "attachment; filename=\"InvektoServis.postman_collection.json\"";
    ctx.Response.ContentType = "application/json";
    return Results.Content(json, "application/json");
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
app.MapGet("/api/v1/dashboard/analytics/summary", async (HttpContext ctx, [FromServices] AnalyticsRepository analyticsRepo, JsonLinesLogger jsonLogger, string? from, string? to) =>
{
    // Single source of truth for INSE+INMA tenant extraction (CQ4 dedup, iter 2 fix).
    var (tenantContext, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
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
// INSTANCE MANAGEMENT ENDPOINTS (Settings)
// JWT auth (INSE + INMA fallback), tenant-scoped
// ============================================

// Helper: extract TenantContext from Bearer token (INSE first, INMA welcome introspection fallback).
// Returns (Tenant, Failure):
//   Failure != null  → caller should return failure verbatim (e.g. 503 INV-AUTH-008 transient outage)
//   Failure == null + Tenant == null → caller should return its own 401 with appropriate message
//   Failure == null + Tenant != null → success
// Shared helper: resolve INMA CompanyCode (opaque string) → INSE int tenant_id via lazy
// auto-provision. Maps repository exceptions to the standard INV-AUTH-009 envelope so
// every caller (ExtractTenantFromBearer + /api/v1/inma/auth/exchange + /api/v1/inma/auth/login)
// shares one error mapping point. Typed catches: NpgsqlException (transient, 503),
// ArgumentException + InvalidOperationException (programmer/data-shape errors, 500).
async Task<(int TenantId, IResult? Failure)> ResolveInmaTenantAsync(
    HttpContext ctx,
    TenantRegistryRepository tenantRepo,
    InmaTokenContext inmaCtx,
    string requestId)
{
    try
    {
        var tenantId = await tenantRepo.ResolveOrCreateByInmaCodeAsync(
            inmaCtx.CompanyCode, inmaCtx.FullName, ctx.RequestAborted);
        return (tenantId, null);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        var jsonLog = ctx.RequestServices.GetService<JsonLinesLogger>();
        jsonLog?.SystemWarn($"[{ErrorCodes.AuthTenantResolveFailed}] tenant resolve DB failure: company_code={inmaCtx.CompanyCode} error={ex.Message}");
        return (0, Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthTenantResolveFailed, "Tenant provision hatasi, kisa sure sonra tekrar deneyin.", requestId),
            statusCode: 503));
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        var jsonLog = ctx.RequestServices.GetService<JsonLinesLogger>();
        jsonLog?.SystemWarn($"[{ErrorCodes.AuthTenantResolveFailed}] tenant resolve logic failure: company_code={inmaCtx.CompanyCode} error={ex.Message}");
        return (0, Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthTenantResolveFailed, "Tenant provision dogrulamasi basarisiz, yetkili destek ile iletisime gecin.", requestId),
            statusCode: 500));
    }
}

async Task<(TenantContext? Tenant, IResult? Failure)> ExtractTenantFromBearer(HttpContext ctx)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        return (null, null);

    var token = authHeader["Bearer ".Length..].Trim();
    TenantContext? tenantContext = null;

    // Path A: INSE JWT (local signature verify, fast path)
    if (jwtValidator != null)
    {
        var (ctx1, _) = jwtValidator.ValidateToken(token);
        tenantContext = ctx1;
    }
    // Path B: INMA JWT (welcome-endpoint introspection — see InmaTokenIntrospector)
    if (tenantContext == null)
    {
        var introspector = ctx.RequestServices.GetService<InmaTokenIntrospector>();
        var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
        if (introspector == null)
        {
            // INMA introspector DI missing: production misconfiguration (expected registered
            // in Program.cs startup). Log + return 503 so the 401 fallthrough cannot mask a
            // broken auth layer. Dev-mode INSE-only deployments that intentionally skip
            // introspector should branch on this same signal before reaching Path B.
            var missingIntrospectorRequestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
            var jsonLog = ctx.RequestServices.GetService<JsonLinesLogger>();
            jsonLog?.SystemWarn($"[{ErrorCodes.AuthTenantResolveFailed}] InmaTokenIntrospector not registered - INSE JWT rejected but INMA path unavailable");
            return (null, Results.Json(
                ErrorResponse.Create(ErrorCodes.AuthTenantResolveFailed, "INMA dogrulama yapilandirilamamis, yetkili destek ile iletisime gecin.", missingIntrospectorRequestId),
                statusCode: 503));
        }
        if (tenantRepo == null)
        {
            // Misconfiguration: repository not registered. Fail explicitly rather than silently skip.
            var missingRepoRequestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
            var jsonLog = ctx.RequestServices.GetService<JsonLinesLogger>();
            jsonLog?.SystemWarn($"[{ErrorCodes.AuthTenantResolveFailed}] TenantRegistryRepository not registered");
            return (null, Results.Json(
                ErrorResponse.Create(ErrorCodes.AuthTenantResolveFailed, "Tenant provision hatasi, kisa sure sonra tekrar deneyin.", missingRepoRequestId),
                statusCode: 503));
        }

        var result = await introspector.ValidateAsync(token, ctx.RequestAborted);
        if (result.Context != null)
        {
            // CompanyCode is opaque string — resolve to INSE int tenant_id (lazy auto-provision).
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
            var (tenantId, resolveFailure) = await ResolveInmaTenantAsync(ctx, tenantRepo, result.Context, requestId);
            if (resolveFailure != null) return (null, resolveFailure);
            tenantContext = new TenantContext { TenantId = tenantId, UserId = result.Context.UserId, Role = result.Context.Role };
        }
        else if (result.ErrorCode == ErrorCodes.AuthInmaIntrospectionUnavailable)
        {
            // Transient INMA outage → propagate as 503 (uniform with /api/v1/inma/auth/* endpoints).
            var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
            return (null, MapInmaIntrospectError(result.ErrorCode, result.Detail, requestId));
        }
    }

    return (tenantContext, null);
}

// GET /api/v1/settings/instances — list instances (fetches from WapCRM on first call, then from DB cache)
app.MapGet("/api/v1/settings/instances", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var instanceRepo = ctx.RequestServices.GetService<InstanceRepository>();
    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (instanceRepo == null || tenantRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    var requestId = Guid.NewGuid().ToString("N")[..8];

    // Check if we have cached instances; if not, try fetching from WapCRM
    var hasRecords = await instanceRepo.HasInstanceRecordsAsync(tenant.TenantId);
    if (!hasRecords)
    {
        var wapcrm = await tenantRepo.GetWapCrmSettingsAsync(tenant.TenantId);
        if (wapcrm != null && !string.IsNullOrWhiteSpace(wapcrm.SecretKey))
        {
            try
            {
                var fetched = await FetchWapCrmInstances(wapcrm.SecretKey, jsonLog, requestId);
                if (fetched.Count > 0)
                    await instanceRepo.UpsertInstancesAsync(tenant.TenantId, fetched);
            }
            catch (Exception ex)
            {
                jsonLog.StepWarn($"Instance auto-fetch from WapCRM failed: {ex.Message}", requestId);
            }
        }
    }

    var instances = await instanceRepo.ListInstancesAsync(tenant.TenantId);
    return Results.Ok(new { instances });
});

// POST /api/v1/settings/instances/refresh — force re-fetch from WapCRM
app.MapPost("/api/v1/settings/instances/refresh", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var instanceRepo = ctx.RequestServices.GetService<InstanceRepository>();
    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (instanceRepo == null || tenantRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    var requestId = Guid.NewGuid().ToString("N")[..8];

    var wapcrm = await tenantRepo.GetWapCrmSettingsAsync(tenant.TenantId);
    if (wapcrm == null || string.IsNullOrWhiteSpace(wapcrm.SecretKey))
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = "WapCRM API anahtari yapilandirilmamis" }, statusCode: 422);

    try
    {
        var fetched = await FetchWapCrmInstances(wapcrm.SecretKey, jsonLog, requestId);
        if (fetched.Count > 0)
            await instanceRepo.UpsertInstancesAsync(tenant.TenantId, fetched);

        var instances = await instanceRepo.ListInstancesAsync(tenant.TenantId);
        return Results.Ok(new { instances, refreshed = true });
    }
    catch (Exception ex)
    {
        jsonLog.StepWarn($"WapCRM instance refresh failed: {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = $"WapCRM baglanti hatasi: {ex.Message}" }, statusCode: 502);
    }
});

// GET /api/v1/settings/wa-templates — list this tenant's WhatsApp approved (HSM) templates from cxapi (READ-ONLY).
// FEAT-PROJELER PKT-14 S3. Sibling of /api/v1/settings/instances: ExtractTenantFromBearer ->
// GetWapCrmSettingsAsync -> resolve+own instanceId -> WapCrmTemplateClient (per-request secret) -> cxapi POST /api/templates.
// Powers the S4 Projeler wizard template picker. NO send path; outside the P0-3 live-enablement gate.
// Services are resolved via RequestServices (sibling-endpoint style) — sidesteps the .NET 8 RDF body-inference crash.
app.MapGet("/api/v1/settings/wa-templates", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N")[..8];
    ctx.Response.Headers.CacheControl = "no-store"; // template names/content are tenant-sensitive

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    var instanceRepo = ctx.RequestServices.GetService<InstanceRepository>();
    var templateClient = ctx.RequestServices.GetService<WapCrmTemplateClient>();
    if (tenantRepo == null || instanceRepo == null || templateClient == null)
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    // Optional ?instanceId= (positive int). A present-but-non-positive/non-numeric value -> 400.
    int? requestedInstanceId = null;
    if (ctx.Request.Query.TryGetValue("instanceId", out var rawInstanceId) && !string.IsNullOrWhiteSpace(rawInstanceId))
    {
        if (!int.TryParse(rawInstanceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            return Results.Json(new { error = ErrorCodes.GeneralValidation, message = "instanceId pozitif bir tam sayı olmalı." }, statusCode: 400);
        requestedInstanceId = parsed;
    }

    var wapcrm = await tenantRepo.GetWapCrmSettingsAsync(tenant.TenantId);
    if (wapcrm == null || string.IsNullOrWhiteSpace(wapcrm.SecretKey))
        return Results.Json(new { error = ErrorCodes.WapCrmTemplatesNotConfigured, message = "WapCRM API anahtarı yapılandırılmamış. Ayarlar > Entegrasyon bölümünden bağlantınızı tamamlayın." }, statusCode: 422);

    // Resolve the effective instanceId: query value, else the tenant-default (must be > 0). Neither -> 422.
    int? defaultInstanceId = wapcrm.InstanceId is > 0 ? wapcrm.InstanceId : null;
    int effective = requestedInstanceId ?? defaultInstanceId ?? 0;
    if (effective <= 0)
        return Results.Json(new { error = ErrorCodes.WapCrmTemplatesInstanceUnresolved, message = "Geçerli bir WhatsApp instance seçilmedi. Bir instance seçip tekrar deneyin." }, statusCode: 422);

    // Ownership (Codex P0): the effective instance must belong to this tenant.
    var hasInstances = await instanceRepo.HasInstanceRecordsAsync(tenant.TenantId);
    if (hasInstances)
    {
        var status = await instanceRepo.GetInstanceStatusAsync(tenant.TenantId, effective.ToString(CultureInfo.InvariantCulture));
        if (status == null)
            return Results.Json(new { error = ErrorCodes.WapCrmTemplatesInstanceUnresolved, message = "Seçilen WhatsApp instance bu hesaba ait değil. Bir instance seçip tekrar deneyin." }, statusCode: 404);
    }
    else if (requestedInstanceId.HasValue && requestedInstanceId.Value != (defaultInstanceId ?? 0))
    {
        // Empty cache: trust cxapi secret-scoping ONLY for the tenant-default; refuse arbitrary instance probing.
        return Results.Json(new { error = ErrorCodes.WapCrmTemplatesInstanceUnresolved, message = "WhatsApp instance doğrulanamadı. Önce Ayarlar > Instance listesini yükleyip tekrar deneyin." }, statusCode: 404);
    }

    WapCrmTemplateListResult result;
    try
    {
        result = await templateClient.ListTemplatesAsync(wapcrm.SecretKey, effective, tenant.TenantId, ctx.RequestAborted);
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    {
        // Typed backstop (NOT a broad swallow — only contract-guard throws are caught here; a caller-cancel
        // OperationCanceledException is NOT one of these and propagates). The endpoint already pre-validates a
        // present secret + a positive instanceId, but the client also contract-guards a MALFORMED stored secret
        // (control chars) / an unattachable X-CIB-SecretKey header. Map any such config-invalid throw to a coded
        // 422 instead of letting it escape as an unhandled 500. The secret is never logged.
        jsonLog.StepWarn($"wa-templates invalid WapCRM config: tenant={tenant.TenantId}, instance={effective}, error={ex.GetType().Name}", requestId);
        return Results.Json(new { error = ErrorCodes.WapCrmTemplatesNotConfigured, message = "WapCRM API anahtarı geçersiz veya yapılandırılmamış. Ayarlar > Entegrasyon bölümünden bağlantınızı kontrol edin." }, statusCode: 422);
    }

    switch (result.Outcome)
    {
        case WapCrmTemplateListOutcome.Success:
            return Results.Ok(new { instanceId = effective, templates = result.Templates });

        case WapCrmTemplateListOutcome.RateLimited:
            if (result.RetryAfter is { } ra)
                ctx.Response.Headers.RetryAfter = ((int)Math.Ceiling(ra.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            jsonLog.StepWarn($"wa-templates rate-limited: tenant={tenant.TenantId}, instance={effective}, providerCode={result.ProviderStatusCode}", requestId);
            return Results.Json(new { error = ErrorCodes.WapCrmTemplatesUpstreamFailed, message = "WhatsApp şablonları şu anda alınamıyor (hız sınırı); birkaç saniye sonra tekrar deneyin." }, statusCode: 429);

        case WapCrmTemplateListOutcome.TimedOut:
            jsonLog.StepWarn($"wa-templates timeout: tenant={tenant.TenantId}, instance={effective}", requestId);
            return Results.Json(new { error = ErrorCodes.WapCrmTemplatesUpstreamFailed, message = "WhatsApp şablonları şu anda alınamıyor; birkaç saniye sonra tekrar deneyin." }, statusCode: 504);

        case WapCrmTemplateListOutcome.ProviderRejected:
            // Raw provider message is logged here (length-capped), NEVER returned to the SPA.
            jsonLog.StepWarn($"wa-templates provider rejected: tenant={tenant.TenantId}, instance={effective}, providerCode={result.ProviderStatusCode}, reqId={result.ProviderRequestId}, msg={CapProviderMessage(result.ProviderMessage)}", requestId);
            return Results.Json(new { error = ErrorCodes.WapCrmTemplatesProviderRejected, message = "WhatsApp şablonları alınamadı. Lütfen WapCRM bağlantınızı kontrol edin.", providerStatusCode = result.ProviderStatusCode, providerRequestId = result.ProviderRequestId }, statusCode: 502);

        default: // TransportError
            jsonLog.StepWarn($"wa-templates transport error: tenant={tenant.TenantId}, instance={effective}, http={result.HttpStatusCode}, msg={CapProviderMessage(result.ProviderMessage)}", requestId);
            return Results.Json(new { error = ErrorCodes.WapCrmTemplatesUpstreamFailed, message = "WhatsApp şablonları şu anda alınamıyor; birkaç saniye sonra tekrar deneyin." }, statusCode: 502);
    }

    // Local: cap an internal-only provider message for logging (the secret is a header, so it can never appear here).
    static string? CapProviderMessage(string? msg)
        => string.IsNullOrEmpty(msg) ? msg : (msg.Length > 200 ? msg[..200] : msg);
});

// PUT /api/v1/settings/instances/{instanceId}/toggle — enable/disable instance
app.MapPut("/api/v1/settings/instances/{instanceId}/toggle", async (HttpContext ctx, JsonLinesLogger jsonLog, string instanceId) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var instanceRepo = ctx.RequestServices.GetService<InstanceRepository>();
    if (instanceRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    // Parse body: { "is_enabled": true/false }
    JsonElement body;
    try
    {
        body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid JSON body" });
    }

    if (!body.TryGetProperty("is_enabled", out var enabledProp) || enabledProp.ValueKind != JsonValueKind.True && enabledProp.ValueKind != JsonValueKind.False)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "is_enabled (boolean) required" });

    var enabled = enabledProp.GetBoolean();
    var (success, error) = await instanceRepo.SetInstanceEnabledAsync(tenant.TenantId, instanceId, enabled);

    if (!success)
    {
        // Flow-assigned instance cannot be disabled
        var errorCode = error?.Contains("flow") == true ? ErrorCodes.BackendInstanceDisableBlocked : ErrorCodes.BackendInstanceFetchFailed;
        var statusCode = error?.Contains("flow") == true ? 409 : 404;
        return Results.Json(new { error = errorCode, message = error }, statusCode: statusCode);
    }

    return Results.Ok(new { instance_id = instanceId, is_enabled = enabled });
});

// GET /api/v1/settings/working-hours — read tenant's working hours configuration
app.MapGet("/api/v1/settings/working-hours", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (tenantRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendWorkingHoursFetchFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        var json = await tenantRepo.GetWorkingHoursJsonAsync(tenant.TenantId);
        if (json == null)
        {
            return Results.Ok(new
            {
                configured = false,
                start = "09:00",
                end = "18:00",
                timezone = "Europe/Istanbul",
                days_off = new[] { "Saturday", "Sunday" },
            });
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var start = root.TryGetProperty("start", out var s) ? s.GetString() : "09:00";
        var end = root.TryGetProperty("end", out var e) ? e.GetString() : "18:00";
        var timezone = root.TryGetProperty("timezone", out var tz) ? tz.GetString() : "Europe/Istanbul";
        var daysOff = new List<string>();
        if (root.TryGetProperty("days_off", out var days) && days.ValueKind == JsonValueKind.Array)
        {
            foreach (var day in days.EnumerateArray())
            {
                if (day.GetString() is string d) daysOff.Add(d);
            }
        }

        return Results.Ok(new
        {
            configured = true,
            start,
            end,
            timezone,
            days_off = daysOff,
        });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Working hours fetch failed for tenant {tenant.TenantId}: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendWorkingHoursFetchFailed, message = "Calisma saatleri yuklenemedi" }, statusCode: 500);
    }
});

// PUT /api/v1/settings/working-hours — update tenant's working hours configuration
app.MapPut("/api/v1/settings/working-hours", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (tenantRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendWorkingHoursUpdateFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    JsonElement body;
    try
    {
        body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Invalid JSON body" });
    }

    // Validate start/end time format (HH:mm)
    if (!body.TryGetProperty("start", out var startProp) || !TimeOnly.TryParse(startProp.GetString(), out _))
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "start (HH:mm) required" });

    if (!body.TryGetProperty("end", out var endProp) || !TimeOnly.TryParse(endProp.GetString(), out _))
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "end (HH:mm) required" });

    // Validate timezone
    var timezoneStr = body.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() : "Europe/Istanbul";
    try
    {
        TimeZoneInfo.FindSystemTimeZoneById(timezoneStr ?? "Europe/Istanbul");
    }
    catch (TimeZoneNotFoundException)
    {
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = $"Invalid timezone: {timezoneStr}" });
    }

    // Validate days_off (must be valid DayOfWeek strings)
    var daysOff = new List<string>();
    if (body.TryGetProperty("days_off", out var daysProp) && daysProp.ValueKind == JsonValueKind.Array)
    {
        foreach (var day in daysProp.EnumerateArray())
        {
            var dayStr = day.GetString();
            if (dayStr == null || !Enum.TryParse<DayOfWeek>(dayStr, ignoreCase: true, out _))
                return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = $"Invalid day_off value: {dayStr}" });
            daysOff.Add(dayStr);
        }
    }

    // Build the working_hours JSON object
    var workingHours = new
    {
        start = startProp.GetString(),
        end = endProp.GetString(),
        timezone = timezoneStr ?? "Europe/Istanbul",
        days_off = daysOff,
    };
    var whJson = JsonSerializer.Serialize(workingHours);

    try
    {
        var updated = await tenantRepo.UpdateWorkingHoursJsonAsync(tenant.TenantId, whJson);
        if (!updated)
            return Results.Json(new { error = ErrorCodes.BackendWorkingHoursUpdateFailed, message = "Tenant not found or inactive" }, statusCode: 404);

        return Results.Ok(new { success = true, working_hours = workingHours });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Working hours update failed for tenant {tenant.TenantId}: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendWorkingHoursUpdateFailed, message = "Calisma saatleri guncellenemedi" }, statusCode: 500);
    }
});

// ============================================================
// Sector settings (tenant self-service)
// ============================================================

var allowedSectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "eticaret", "dis_klinik", "estetik", "saglik", "otel", "guzellik", "egitim", "mobil", "genel"
};

// GET /api/v1/settings/sector — read tenant's sector
app.MapGet("/api/v1/settings/sector", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (tenantRepo == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendSectorUpdateFailed, "PostgreSQL not configured", "-"), statusCode: 503);

    try
    {
        var sector = await tenantRepo.GetSectorAsync(tenant.TenantId);
        return Results.Ok(new { sector });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.BackendSectorUpdateFailed}] Sector read failed for tenant {tenant.TenantId}: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendSectorUpdateFailed, "Sektor bilgisi alinamadi", "-"), statusCode: 500);
    }
});

// PUT /api/v1/settings/sector — update tenant's sector
app.MapPut("/api/v1/settings/sector", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var tenantRepo = ctx.RequestServices.GetService<TenantRegistryRepository>();
    if (tenantRepo == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendSectorUpdateFailed, "PostgreSQL not configured", "-"), statusCode: 503);

    JsonElement body;
    try { body = await ctx.Request.ReadFromJsonAsync<JsonElement>(); }
    catch (JsonException) { return Results.BadRequest(ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", "-")); }

    if (!body.TryGetProperty("sector", out var sectorProp) || string.IsNullOrWhiteSpace(sectorProp.GetString()))
        return Results.BadRequest(ErrorResponse.Create(ErrorCodes.BackendSectorInvalidValue, "sector field required", "-"));

    var sectorRaw = sectorProp.GetString();
    if (sectorRaw == null)
        return Results.BadRequest(ErrorResponse.Create(ErrorCodes.BackendSectorInvalidValue, "sector must be a string", "-"));

    var sector = sectorRaw.Trim().ToLowerInvariant();
    if (!allowedSectors.Contains(sector))
        return Results.BadRequest(ErrorResponse.Create(ErrorCodes.BackendSectorInvalidValue, $"Gecersiz sektor: {sector}", "-"));

    try
    {
        var updated = await tenantRepo.UpdateSectorAsync(tenant.TenantId, sector);
        if (!updated)
            return Results.Json(ErrorResponse.Create(ErrorCodes.BackendSectorUpdateFailed, "Tenant not found or inactive", "-"), statusCode: 404);

        return Results.Ok(new { success = true, sector });
    }
    catch (NpgsqlException ex)
    {
        jsonLog.SystemWarn($"[{ErrorCodes.BackendSectorUpdateFailed}] Sector update failed for tenant {tenant.TenantId}: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendSectorUpdateFailed, "Sektor guncellenemedi", "-"), statusCode: 500);
    }
});

// ============================================================
// Template self-service (tenant-scoped proxy to Knowledge)
// ============================================================

// GET /api/v1/templates/available — tenant's available templates (filtered by sector)
app.MapGet("/api/v1/templates/available", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", "-"), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenant.TenantId);
    var qs = ctx.Request.QueryString.Value ?? "";
    var (statusCode, body) = await knClient.ProxyGetAsync($"/api/v1/templates/{tenant.TenantId}/available{qs}", $"Bearer {serviceToken}", "-");

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

// POST /api/v1/templates/adopt/{templateId} — tenant adopts a template
app.MapPost("/api/v1/templates/adopt/{templateId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int templateId) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", "-"), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenant.TenantId);
    var (statusCode, body) = await knClient.ProxyPostAsync($"/api/v1/templates/{tenant.TenantId}/adopt/{templateId}", "{}", $"Bearer {serviceToken}", "-");

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

// GET /api/v1/templates/adoptions — tenant's adoption history
app.MapGet("/api/v1/templates/adoptions", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", "-"), statusCode: 500);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenant.TenantId);
    var qs = ctx.Request.QueryString.Value ?? "";
    var (statusCode, body) = await knClient.ProxyGetAsync($"/api/v1/templates/{tenant.TenantId}/adoptions{qs}", $"Bearer {serviceToken}", "-");

    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

// GET /api/v1/flow-builder/instances/available — available instances for flow assignment
app.MapGet("/api/v1/flow-builder/instances/available", async (HttpContext ctx, JsonLinesLogger jsonLog, int? flow_id) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    var instanceRepo = ctx.RequestServices.GetService<InstanceRepository>();
    if (instanceRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendInstanceFetchFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    var instances = await instanceRepo.GetAvailableInstancesAsync(tenant.TenantId, flow_id);
    return Results.Ok(new { instances });
});

// GET /api/v1/flow-builder/tenant/working-hours — tenant working hours for FlowSettings tooltip
app.MapGet("/api/v1/flow-builder/tenant/working-hours", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"), statusCode: 401);

    if (pgFactory == null)
        return Results.Ok(new { configured = false });

    await using var conn = await pgFactory.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT settings_json->'working_hours' FROM tenant_registry WHERE tenant_id = @tid AND is_active = true";
    cmd.Parameters.AddWithValue("tid", tenant.TenantId);
    var result = await cmd.ExecuteScalarAsync();

    if (result is null or DBNull)
        return Results.Ok(new { configured = false });

    var json = result.ToString();
    if (string.IsNullOrWhiteSpace(json))
        return Results.Ok(new { configured = false });

    try
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var start = root.TryGetProperty("start", out var s) ? s.GetString() : null;
        var end = root.TryGetProperty("end", out var e) ? e.GetString() : null;
        var timezone = root.TryGetProperty("timezone", out var tz) ? tz.GetString() : null;
        var daysOff = new List<string>();
        if (root.TryGetProperty("days_off", out var days) && days.ValueKind == JsonValueKind.Array)
        {
            foreach (var day in days.EnumerateArray())
            {
                if (day.GetString() is string d) daysOff.Add(d);
            }
        }

        return Results.Ok(new
        {
            configured = true,
            start,
            end,
            timezone,
            days_off = daysOff,
        });
    }
    catch (Exception ex)
    {
        jsonLog.StepWarn($"Working hours JSON parse failed for tenant {tenant.TenantId}: {ex.Message}", "-");
        return Results.Ok(new { configured = false });
    }
});

// WapCRM instance fetch helper (shared by GET + POST refresh)
async Task<List<WapCrmInstanceDto>> FetchWapCrmInstances(string secretKey, JsonLinesLogger jsonLog, string requestId)
{
    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    httpClient.DefaultRequestHeaders.Add("X-CIB-SecretKey", secretKey);

    var response = await httpClient.GetAsync("https://cxapi.wapcrm.net/api/Instances");
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var apiResp = JsonSerializer.Deserialize<WapCrmApiEnvelope>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (apiResp?.Data == null)
    {
        jsonLog.StepWarn($"WapCRM /api/Instances returned null data", requestId);
        return [];
    }

    return apiResp.Data.Select(i => new WapCrmInstanceDto
    {
        InstanceId = i.InstanceId.ToString(),
        InstanceName = i.InstanceName ?? $"Instance {i.InstanceId}",
        Account = i.Account,
        InstanceType = i.InstanceType,
    }).ToList();
}

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

// Streaming proxy for file downloads (FEAT-OBI Plan B export): forwards the upstream body
// straight to the client without buffering, preserving status + Content-Type/Content-Disposition
// + the no-store/nosniff security headers. Used only for the /exports/ routes.
async Task<IResult> OutboundProxyStreamGet(HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string targetPath)
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        using var upstream = await obClient.ProxyStreamGetAsync(targetPath, authHeader, requestId, ctx.RequestAborted);
        sw.Stop();
        jsonLog.StepInfo($"Outbound stream GET {targetPath}: status={(int)upstream.StatusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

        ctx.Response.StatusCode = (int)upstream.StatusCode;

        // Pass through the headers a download needs (content + security). Content-Length is
        // deliberately dropped so the body can stream chunked.
        if (upstream.Content.Headers.ContentType != null)
            ctx.Response.ContentType = upstream.Content.Headers.ContentType.ToString();
        if (upstream.Content.Headers.ContentDisposition != null)
            ctx.Response.Headers["Content-Disposition"] = upstream.Content.Headers.ContentDisposition.ToString();
        if (upstream.Headers.TryGetValues("Cache-Control", out var cc))
            ctx.Response.Headers["Cache-Control"] = string.Join(", ", cc);
        if (upstream.Headers.TryGetValues("X-Content-Type-Options", out var nosniff))
            ctx.Response.Headers["X-Content-Type-Options"] = string.Join(", ", nosniff);

        await using var bodyStream = await upstream.Content.ReadAsStreamAsync(ctx.RequestAborted);
        await bodyStream.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
        return Results.Empty;
    }
    catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
    {
        // Client disconnected mid-download (RequestAborted); nothing more to send.
        return Results.Empty;
    }
    catch (OperationCanceledException)
    {
        // HttpClient.Timeout elapsed before Outbound returned headers.
        sw.Stop();
        jsonLog.StepError($"Outbound stream GET {targetPath} timed out", requestId);
        if (!ctx.Response.HasStarted)
            return Results.Json(new { error_code = "INV-BE-002", message = "Outbound service timeout" }, statusCode: 504);
        return Results.Empty;
    }
    catch (HttpRequestException ex)
    {
        // Transport failure reaching Outbound (connection refused, reset, etc.).
        sw.Stop();
        jsonLog.StepError($"Outbound stream GET {targetPath} failed: {ex.Message}", requestId);
        if (!ctx.Response.HasStarted)
            return Results.Json(new { error_code = "INV-BE-001", message = "Outbound service unavailable" }, statusCode: 502);
        return Results.Empty;
    }
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

// Contact lists (FEAT-OBI Phase 1A — DataImportPage SPA surface).
// Tenant JWT + "Outbound" FeatureGuard already enforced by the /api/v1/outbound/ prefix
// (see jwtRequiredPrefixes + UseFeatureGuard above); tenant_id is resolved by the Outbound
// service from the forwarded Bearer token, never from the path/body.
app.MapGet("/api/v1/outbound/data-lists", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, "/api/v1/data-lists"));

app.MapPost("/api/v1/outbound/data-lists", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/data-lists"));

// records/exists + import are string segments, so they never collide with the {id:long} routes below.
app.MapPost("/api/v1/outbound/data-lists/records/exists", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/data-lists/records/exists"));

app.MapPost("/api/v1/outbound/data-lists/import", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/data-lists/import"));

app.MapPut("/api/v1/outbound/data-lists/{id:long}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPut(ctx, obClient, jsonLog, $"/api/v1/data-lists/{id}"));

app.MapDelete("/api/v1/outbound/data-lists/{id:long}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyDelete(ctx, obClient, jsonLog, $"/api/v1/data-lists/{id}"));

// FEAT-PROJELER PKT-14 — read-only list insights (query string forwarded verbatim).
// preview-sample = Projeler modal sample/reach/column-stats; {id}/records = viewer popup page.
app.MapGet("/api/v1/outbound/data-lists/preview-sample", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/data-lists/preview-sample{ctx.Request.QueryString.Value}"));

app.MapGet("/api/v1/outbound/data-lists/{id:long}/records", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/data-lists/{id}/records{ctx.Request.QueryString.Value}"));

// Export Manager (FEAT-OBI Phase 1A Plan B). File downloads are STREAMED through
// (ResponseHeadersRead -> CopyToAsync) so a 50k-row XLSX/CSV never buffers in Backend
// memory, preserving Content-Type + Content-Disposition + the no-store/nosniff headers.
// The query string (?format=, ?for=) is forwarded verbatim. JWT + "Outbound" FeatureGuard
// are already enforced by the /api/v1/outbound/ prefix; Outbound resolves tenant from the token.
app.MapGet("/api/v1/outbound/exports/send-jobs", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, "/api/v1/exports/send-jobs"));

app.MapGet("/api/v1/outbound/exports/contact-list/{listId:long}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long listId) =>
    await OutboundProxyStreamGet(ctx, obClient, jsonLog, $"/api/v1/exports/contact-list/{listId}{ctx.Request.QueryString.Value}"));

app.MapGet("/api/v1/outbound/exports/send-job/{jobId:long}/recipients", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long jobId) =>
    await OutboundProxyStreamGet(ctx, obClient, jsonLog, $"/api/v1/exports/send-job/{jobId}/recipients{ctx.Request.QueryString.Value}"));

app.MapGet("/api/v1/outbound/exports/send-job/{jobId:long}/report-data", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long jobId) =>
    await OutboundProxyStreamGet(ctx, obClient, jsonLog, $"/api/v1/exports/send-job/{jobId}/report-data{ctx.Request.QueryString.Value}"));

// Export Manager v2 (FEAT-OBI Phase 1B) — filter-driven recipients surface. JSON routes use
// the buffered proxy; the recipients file download streams through (no buffering). Query string
// (filters: ?templateId=&jobId=&listId=&status=&from=&to=&format=) is forwarded verbatim.
app.MapGet("/api/v1/outbound/exports/filter-options", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, "/api/v1/exports/filter-options"));

app.MapGet("/api/v1/outbound/exports/recipients/count", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/exports/recipients/count{ctx.Request.QueryString.Value}"));

app.MapGet("/api/v1/outbound/exports/recipients", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyStreamGet(ctx, obClient, jsonLog, $"/api/v1/exports/recipients{ctx.Request.QueryString.Value}"));

app.MapPost("/api/v1/outbound/exports/recipients/create-list", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/exports/recipients/create-list"));

app.MapGet("/api/v1/outbound/exports/history", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, "/api/v1/exports/history"));

// List -> bulk send wiring (preview-from-list snapshot, then the shared confirm/status path).
app.MapPost("/api/v1/outbound/bulk-send/preview-from-list", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/bulk-send/preview-from-list"));

app.MapPost("/api/v1/outbound/bulk-send/confirm", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/bulk-send/confirm"));

app.MapGet("/api/v1/outbound/bulk-send/{campaignId}/status", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, string campaignId) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/bulk-send/{campaignId}/status"));

// FEAT-PROJELER PKT-14 S4 — Projeler CRUD proxy (thin pass-through over the Outbound /api/v1/projects
// endpoints from S2; mirrors the data-lists GR4 block). The bearer is forwarded straight through;
// Outbound does ExtractTenant + the ProjectsOptions gate. DELETE is soft-delete-as-archive in Outbound.
app.MapGet("/api/v1/outbound/projects", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, "/api/v1/projects"));

app.MapGet("/api/v1/outbound/projects/{id:long}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/projects/{id}"));

app.MapPost("/api/v1/outbound/projects", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, "/api/v1/projects"));

app.MapPut("/api/v1/outbound/projects/{id:long}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPut(ctx, obClient, jsonLog, $"/api/v1/projects/{id}"));

app.MapDelete("/api/v1/outbound/projects/{id:long}", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyDelete(ctx, obClient, jsonLog, $"/api/v1/projects/{id}"));

// FEAT-PROJELER send-exec SS-C — project RUN dispatch proxy (preview -> confirm -> status).
// Thin pass-through over the Outbound /api/v1/projects/{id}/send/* endpoints; Outbound enforces the
// Projects + BulkSend gates + eligibility. The run is a bulk_send_job(project_id) under the hood.
app.MapPost("/api/v1/outbound/projects/{id:long}/send/preview", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, $"/api/v1/projects/{id}/send/preview"));

app.MapPost("/api/v1/outbound/projects/{id:long}/send/confirm", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, $"/api/v1/projects/{id}/send/confirm"));

app.MapGet("/api/v1/outbound/projects/{id:long}/send/status", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyGet(ctx, obClient, jsonLog, $"/api/v1/projects/{id}/send/status"));

// FEAT-PROJELER send-exec SS-D — project run pause / resume / cancel proxy (no body; project id in route).
app.MapPost("/api/v1/outbound/projects/{id:long}/send/pause", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, $"/api/v1/projects/{id}/send/pause"));

app.MapPost("/api/v1/outbound/projects/{id:long}/send/resume", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, $"/api/v1/projects/{id}/send/resume"));

app.MapPost("/api/v1/outbound/projects/{id:long}/send/cancel", async (HttpContext ctx, OutboundClient obClient, JsonLinesLogger jsonLog, long id) =>
    await OutboundProxyPost(ctx, obClient, jsonLog, $"/api/v1/projects/{id}/send/cancel"));

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

// Monitor proxy: Backend /api/v1/flow-builder/monitor/{tid}/executions -> Automation /api/v1/flows/{tid}/executions
app.MapGet("/api/v1/flow-builder/monitor/{tenantId:int}/executions", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/executions{ctx.Request.QueryString}"));

// Execution log proxy: Backend /api/v1/flow-builder/flows/{tid}/{fid}/executions -> Automation
app.MapGet("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/executions", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/executions{ctx.Request.QueryString}"));

app.MapGet("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/executions/{logId:long}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId, long logId) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/executions/{logId}"));

// Versioning proxy: Backend /api/v1/flow-builder/flows/{tid}/{fid}/versions -> Automation
app.MapGet("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/versions", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/versions"));

app.MapGet("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/versions/{versionNumber:int}", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId, int versionNumber) =>
    await FbProxyGet(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}"));

app.MapPost("/api/v1/flow-builder/flows/{tenantId:int}/{flowId:int}/versions/{versionNumber:int}/rollback", async (HttpContext ctx, FlowBuilderClient fbClient, JsonLinesLogger jsonLog, int tenantId, int flowId, int versionNumber) =>
    await FbProxyPost(ctx, fbClient, jsonLog, $"/api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}/rollback"));

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
        var currentFlowConfig = root.TryGetProperty("flow_config", out var fcProp) && fcProp.ValueKind == System.Text.Json.JsonValueKind.Object
            ? fcProp.GetRawText()
            : null;

        // Monitor mode: execution_detail is a JSON string containing execution trace for AI analysis
        var executionContext = root.TryGetProperty("execution_detail", out var edProp) && edProp.ValueKind == System.Text.Json.JsonValueKind.Object
            ? edProp.GetRawText()
            : null;

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
                    // Support both PascalCase (legacy) and camelCase property names
                    var role = (item.TryGetProperty("role", out var r) ? r.GetString()
                             : item.TryGetProperty("Role", out r) ? r.GetString() : null) ?? "user";
                    var content = (item.TryGetProperty("content", out var c) ? c.GetString()
                                : item.TryGetProperty("Content", out c) ? c.GetString() : null) ?? "";
                    var timestamp = item.TryGetProperty("timestamp", out var ts) ? ts.GetString()
                                 : item.TryGetProperty("Timestamp", out ts) ? ts.GetString() : null;

                    history.Add(new WizardMessage
                    {
                        Role = role,
                        Content = content,
                        Timestamp = timestamp
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
        List<WizardOption>? capturedOptions = null;
        bool hadError = false;

        // Retry once on transient errors (overloaded, rate limit) if no SSE data sent yet
        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            fullAssistantText.Clear();
            extractedFlowConfig = null;
            prerequisites = null;
            capturedOptions = null;
            hadError = false;
            bool anySseSent = false;

            await foreach (var chunk in wizardService.StreamChatAsync(userMessage, history, existingFlows, currentFlowConfig, executionContext, ctx.RequestAborted))
            {
                if (chunk.Type == "done")
                {
                    fullAssistantText.Clear();
                    fullAssistantText.Append(chunk.Content);
                    extractedFlowConfig = chunk.FlowConfig;
                    prerequisites = chunk.Prerequisites;
                    capturedOptions = chunk.Options;

                    var doneEvent = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "done",
                        content = chunk.Content,
                        flow_config = extractedFlowConfig != null
                            ? System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(extractedFlowConfig)
                            : (System.Text.Json.JsonElement?)null,
                        prerequisites,
                        options = chunk.Options
                    });
                    await ctx.Response.WriteAsync($"data: {doneEvent}\n\n");
                    await ctx.Response.Body.FlushAsync();
                    anySseSent = true;
                }
                else
                {
                    if (chunk.Type == "error")
                    {
                        hadError = true;
                        // If no data sent yet and we can retry, skip sending error to client
                        if (!anySseSent && attempt < maxAttempts)
                            break;
                    }
                    if (chunk.Type == "text") fullAssistantText.Append(chunk.Content);
                    var eventData = System.Text.Json.JsonSerializer.Serialize(new { type = chunk.Type, content = chunk.Content });
                    await ctx.Response.WriteAsync($"data: {eventData}\n\n");
                    await ctx.Response.Body.FlushAsync();
                    anySseSent = true;
                }
            }

            // Retry if error occurred before any SSE was sent
            if (hadError && !anySseSent && attempt < maxAttempts)
            {
                jsonLog.StepInfo($"Wizard AI transient error on attempt {attempt}, retrying in 2s...", requestId);
                await Task.Delay(2000, ctx.RequestAborted);
                continue;
            }
            break;
        }

        // Only save history when AI responded successfully
        if (!hadError && fullAssistantText.Length > 0)
        {
            // Always persist BOTH user and assistant turns so Claude conversation history stays intact
            // for subsequent API calls (Claude expects alternating user/assistant pairs starting with user).
            // Hidden TEMPLATE_SEED user messages are filtered out at the FE display layer only —
            // see ai-chat-store.ts open() history-load filter. This preserves Claude context across reloads.
            history.Add(new WizardMessage { Role = "user", Content = userMessage, Timestamp = DateTime.UtcNow.ToString("o") });
            history.Add(new WizardMessage
            {
                Role = "assistant",
                Content = fullAssistantText.ToString(),
                Timestamp = DateTime.UtcNow.ToString("o"),
                FlowConfigSnapshot = extractedFlowConfig,
                Options = capturedOptions
            });

            var historyJson = System.Text.Json.JsonSerializer.Serialize(history);
            var patchBody = System.Text.Json.JsonSerializer.Serialize(new { wizard_history = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(historyJson) });
            await fbClient.ProxyPatchAsync($"/api/v1/flows/{tenantId}/{flowId}/wizard-history", patchBody, authHeader, requestId);
        }
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
    // A6 dual-auth: Ops admin OR normal tenant user managing their own knowledge.
    if (!ValidateOpsAuth(ctx) && !ValidateJwtTenantAuth(ctx, tenantId))
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
    // A6 dual-auth: Ops admin OR normal tenant user managing their own knowledge.
    if (!ValidateOpsAuth(ctx) && !ValidateJwtTenantAuth(ctx, tenantId))
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
    // A6 dual-auth: Ops admin OR normal tenant user managing their own knowledge.
    if (!ValidateOpsAuth(ctx) && !ValidateJwtTenantAuth(ctx, tenantId))
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
    // A6 dual-auth: Ops admin OR normal tenant user managing their own knowledge.
    if (!ValidateOpsAuth(ctx) && !ValidateJwtTenantAuth(ctx, tenantId))
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
    // A6 dual-auth: Ops admin OR normal tenant user uploading to their own knowledge.
    if (!ValidateOpsAuth(ctx) && !ValidateJwtTenantAuth(ctx, tenantId))
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

// Website indexing proxy
app.MapPost("/api/ops/knowledge/{tenantId:int}/documents/website", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/documents/website"));

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

// Intent CRUD
app.MapGet("/api/ops/knowledge/{tenantId:int}/intents", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyGet(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/intents/full"));

app.MapPost("/api/ops/knowledge/{tenantId:int}/intents", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/intents"));

app.MapPut("/api/ops/knowledge/{tenantId:int}/intents/{intentId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, int intentId) =>
    await KnProxyPut(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/intents/{intentId}"));

app.MapDelete("/api/ops/knowledge/{tenantId:int}/intents/{intentId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, int intentId) =>
    await KnProxyDelete(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/intents/{intentId}"));

// Search + Embeddings
app.MapPost("/api/ops/knowledge/{tenantId:int}/search", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/search"));

app.MapPost("/api/ops/knowledge/{tenantId:int}/generate-embeddings", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, tenantId, $"/api/v1/knowledge/{tenantId}/generate-embeddings"));

// ============================================
// TEMPLATE SYSTEM PROXY ENDPOINTS (superadmin)
// ============================================

// Catalog CRUD
app.MapGet("/api/ops/templates/catalog", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
    await KnProxyGet(ctx, knClient, jsonLog, 0, "/api/v1/templates/catalog"));

app.MapGet("/api/ops/templates/catalog/{id:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyGet(ctx, knClient, jsonLog, 0, $"/api/v1/templates/catalog/{id}"));

app.MapPost("/api/ops/templates/catalog", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, "/api/v1/templates/catalog"));

// 2026-04-18: Dashboard bulk import proxy (body verbatim to Knowledge service).
app.MapPost("/api/ops/templates/catalog/bulk", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, "/api/v1/templates/catalog/bulk"));

app.MapPut("/api/ops/templates/catalog/{id:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyPut(ctx, knClient, jsonLog, 0, $"/api/v1/templates/catalog/{id}"));

app.MapDelete("/api/ops/templates/catalog/{id:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyDelete(ctx, knClient, jsonLog, 0, $"/api/v1/templates/catalog/{id}"));

app.MapPost("/api/ops/templates/catalog/{id:int}/publish", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, $"/api/v1/templates/catalog/{id}/publish"));

app.MapGet("/api/ops/templates/catalog/{id:int}/versions", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyGet(ctx, knClient, jsonLog, 0, $"/api/v1/templates/catalog/{id}/versions"));

// Suggestions
app.MapGet("/api/ops/templates/suggestions", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
    await KnProxyGet(ctx, knClient, jsonLog, 0, "/api/v1/templates/suggestions"));

app.MapGet("/api/ops/templates/suggestions/{id:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyGet(ctx, knClient, jsonLog, 0, $"/api/v1/templates/suggestions/{id}"));

app.MapPost("/api/ops/templates/suggestions/{id:int}/review", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int id) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, $"/api/v1/templates/suggestions/{id}/review"));

app.MapPost("/api/ops/templates/suggestions/bulk-review", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, "/api/v1/templates/suggestions/bulk-review"));

// Extraction
app.MapPost("/api/ops/templates/extract-from-analysis/{analysisId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int analysisId) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, $"/api/v1/templates/extract-from-analysis/{analysisId}"));

// Adoption (superadmin on behalf of tenant)
app.MapPost("/api/ops/templates/{tenantId:int}/adopt/{templateId:int}", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId, int templateId) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, $"/api/v1/templates/{tenantId}/adopt/{templateId}"));

app.MapPost("/api/ops/templates/{tenantId:int}/onboard", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyPost(ctx, knClient, jsonLog, 0, $"/api/v1/templates/{tenantId}/onboard"));

app.MapGet("/api/ops/templates/{tenantId:int}/adoptions", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog, int tenantId) =>
    await KnProxyGet(ctx, knClient, jsonLog, 0, $"/api/v1/templates/{tenantId}/adoptions"));

app.MapPost("/api/ops/templates/seed-from-se", async (HttpContext ctx, KnowledgeClient knClient, JsonLinesLogger jsonLog) =>
{
    var dryRun = ctx.Request.Query.ContainsKey("dry_run") ? $"?dry_run={ctx.Request.Query["dry_run"]}" : "";
    return await KnProxyPost(ctx, knClient, jsonLog, 0, $"/api/v1/templates/seed-from-se{dryRun}");
});

// ============================================
// RI CROSS-SERVICE SYNC ENDPOINTS (RI-8.x)
// ============================================

// POST /api/ops/ri/sync-knowledge/{tenantId} — Sync RI sector templates → Knowledge FAQs + intents (RI-8.11)
app.MapPost("/api/ops/ri/sync-knowledge/{tenantId:int}", async (
    HttpContext ctx,
    WhatsAppAnalyticsClient waClient,
    KnowledgeClient knClient,
    JsonLinesLogger jsonLog,
    int tenantId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    // Read sector from body
    string sector;
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, string>>();
        sector = body?.GetValueOrDefault("sector") ?? "";
    }
    catch (JsonException ex)
    {
        jsonLog.SystemWarn($"[RI-Sync] Invalid request body: {ex.Message}");
        sector = "";
    }

    if (string.IsNullOrWhiteSpace(sector))
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector is required in request body", requestId), statusCode: 400);

    // Step 1: Fetch sector templates from WA via tenant-facing endpoint (JWT auth)
    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var waAuth = $"Bearer {serviceToken}";

    var (waStatus, waBody) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenantId}/ri/templates?sector={Uri.EscapeDataString(sector)}",
        waAuth, requestId);

    if (waStatus != 200 || string.IsNullOrEmpty(waBody))
    {
        jsonLog.SystemError($"[RI-Sync] {ErrorCodes.BackendRiKnowledgeSyncFailed} WA templates fetch failed: status={waStatus}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendRiKnowledgeSyncFailed,
            $"Failed to fetch RI templates from WA (status={waStatus})", requestId), statusCode: 502);
    }

    // Parse WA response: { "requestId": "...", "data": { "sector": "...", "intents": [...], "faqs": [...] } }
    JsonElement dataElement;
    try
    {
        var waJson = JsonDocument.Parse(waBody);
        dataElement = waJson.RootElement.GetProperty("data");
    }
    catch (JsonException ex)
    {
        jsonLog.SystemError($"[RI-Sync] {ErrorCodes.BackendRiKnowledgeSyncFailed} Failed to parse WA response: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendRiKnowledgeSyncFailed,
            "Invalid response from WA templates endpoint", requestId), statusCode: 502);
    }
    catch (KeyNotFoundException ex)
    {
        jsonLog.SystemError($"[RI-Sync] {ErrorCodes.BackendRiKnowledgeSyncFailed} Missing 'data' in WA response: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendRiKnowledgeSyncFailed,
            "WA response missing data field", requestId), statusCode: 502);
    }

    var knAuth = $"Bearer {serviceToken}";
    int intentsSynced = 0, faqsSynced = 0, skipped = 0, errors = 0;

    // Step 2: Sync intents → Knowledge
    if (dataElement.TryGetProperty("intents", out var intentsArray))
    {
        foreach (var intent in intentsArray.EnumerateArray())
        {
            var isActive = intent.TryGetProperty("isActive", out var activeVal) && activeVal.GetBoolean();
            if (!isActive) { skipped++; continue; }

            var intentName = intent.TryGetProperty("intentName", out var nameVal) ? nameVal.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(intentName)) { skipped++; continue; }

            // Parse keywords from JSON array string
            string[]? keywords = null;
            if (intent.TryGetProperty("keywordsJson", out var kwVal) && kwVal.ValueKind == JsonValueKind.String)
            {
                try
                {
                    var kwStr = kwVal.GetString();
                    if (kwStr != null) keywords = JsonSerializer.Deserialize<string[]>(kwStr);
                }
                catch (JsonException ex) { jsonLog.SystemWarn($"[RI-Sync] Malformed keywords JSON: {ex.Message}"); }
            }

            var intentBody = JsonSerializer.Serialize(new { intent_name = intentName, keywords });
            var (knStatus, _) = await knClient.ProxyPostAsync(
                $"/api/v1/knowledge/{tenantId}/intents",
                intentBody, knAuth, requestId);

            if (knStatus is 200 or 201) intentsSynced++;
            else if (knStatus == 409) skipped++; // already exists
            else { errors++; jsonLog.SystemWarn($"[RI-Sync] {ErrorCodes.BackendRiKnowledgeSyncFailed} Intent sync failed for '{intentName}': Knowledge returned {knStatus}"); }
        }
    }

    // Step 3: Sync FAQs → Knowledge
    if (dataElement.TryGetProperty("faqs", out var faqsArray))
    {
        foreach (var faq in faqsArray.EnumerateArray())
        {
            var isActive = faq.TryGetProperty("isActive", out var activeVal) && activeVal.GetBoolean();
            if (!isActive) { skipped++; continue; }

            var question = faq.TryGetProperty("question", out var qVal) ? qVal.GetString() ?? "" : "";
            var answer = faq.TryGetProperty("answer", out var aVal) ? aVal.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer)) { skipped++; continue; }

            var category = faq.TryGetProperty("category", out var catVal) ? catVal.GetString() : null;

            string[]? keywords = null;
            if (faq.TryGetProperty("keywordsJson", out var kwVal) && kwVal.ValueKind == JsonValueKind.String)
            {
                try
                {
                    var kwStr = kwVal.GetString();
                    if (kwStr != null) keywords = JsonSerializer.Deserialize<string[]>(kwStr);
                }
                catch (JsonException ex) { jsonLog.SystemWarn($"[RI-Sync] Malformed keywords JSON: {ex.Message}"); }
            }

            var faqBody = JsonSerializer.Serialize(new { question, answer, category, keywords });
            var (knStatus, _) = await knClient.ProxyPostAsync(
                $"/api/v1/knowledge/{tenantId}/faqs",
                faqBody, knAuth, requestId);

            if (knStatus is 200 or 201) faqsSynced++;
            else if (knStatus == 409) skipped++; // already exists
            else { errors++; jsonLog.SystemWarn($"[RI-Sync] {ErrorCodes.BackendRiKnowledgeSyncFailed} FAQ sync failed for '{question}': Knowledge returned {knStatus}"); }
        }
    }

    jsonLog.SystemInfo($"[RI-Sync] Knowledge sync for tenant={tenantId} sector={sector}: " +
        $"intents={intentsSynced}, faqs={faqsSynced}, skipped={skipped}, errors={errors}");

    return Results.Ok(new
    {
        requestId,
        tenantId,
        sector,
        intents_synced = intentsSynced,
        faqs_synced = faqsSynced,
        skipped,
        errors
    });
});

// POST /api/ops/ri/rescue-trigger/{tenantId} — Trigger outbound messages for rescue candidates (RI-8.10)
app.MapPost("/api/ops/ri/rescue-trigger/{tenantId:int}", async (
    HttpContext ctx,
    WhatsAppAnalyticsClient waClient,
    OutboundClient obClient,
    JsonLinesLogger jsonLog,
    int tenantId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    // Parse body — use defaults on malformed input
    double minPriorityScore = 50.0;
    int maxCandidates = 20;
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<JsonElement>();
        if (body.TryGetProperty("min_priority_score", out var minVal))
            minPriorityScore = minVal.GetDouble();
        if (body.TryGetProperty("max_candidates", out var maxVal))
            maxCandidates = maxVal.GetInt32();
    }
    catch (JsonException ex)
    {
        jsonLog.SystemWarn($"[RI-Rescue] Invalid request body, using defaults: {ex.Message}");
    }

    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";

    // Step 1: Fetch rescue candidates from WA
    var (waStatus, waBody) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenantId}/ri/rescue", authHeader, requestId);

    if (waStatus != 200 || string.IsNullOrEmpty(waBody))
    {
        jsonLog.SystemError($"[RI-Rescue] {ErrorCodes.BackendRiRescueTriggerFailed} WA rescue fetch failed: status={waStatus}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendRiRescueTriggerFailed,
            $"Failed to fetch rescue candidates (status={waStatus})", requestId), statusCode: 502);
    }

    // Parse WA response: { "requestId": "...", "data": { "candidates": [...] } }
    JsonElement candidatesArray;
    try
    {
        var waJson = JsonDocument.Parse(waBody);
        candidatesArray = waJson.RootElement.GetProperty("data").GetProperty("candidates");
    }
    catch (JsonException ex)
    {
        jsonLog.SystemError($"[RI-Rescue] {ErrorCodes.BackendRiRescueTriggerFailed} Failed to parse WA response: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendRiRescueTriggerFailed,
            "Invalid response from WA rescue endpoint", requestId), statusCode: 502);
    }
    catch (KeyNotFoundException ex)
    {
        jsonLog.SystemError($"[RI-Rescue] {ErrorCodes.BackendRiRescueTriggerFailed} Missing data.candidates in WA response: {ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.BackendRiRescueTriggerFailed,
            "WA response missing candidates", requestId), statusCode: 502);
    }

    int triggered = 0, skipped = 0, noPhone = 0, errors = 0;

    // Step 2: For each eligible candidate, trigger outbound
    foreach (var candidate in candidatesArray.EnumerateArray())
    {
        if (triggered >= maxCandidates) break;

        var conversationId = candidate.TryGetProperty("conversationId", out var cidVal) ? cidVal.GetString() ?? "" : "";
        var priorityScore = candidate.TryGetProperty("rescuePriorityScore", out var psVal) ? psVal.GetDouble() : 0;
        var rescueStatus = candidate.TryGetProperty("rescueStatus", out var rsVal) ? rsVal.GetString() ?? "" : "";
        var outcomeLabel = candidate.TryGetProperty("outcomeLabel", out var olVal) ? olVal.GetString() ?? "" : "";

        // Filter: only pending candidates above threshold with a valid phone (conversationId = phone number)
        if (rescueStatus != "pending") { skipped++; continue; }
        if (priorityScore < minPriorityScore) { skipped++; continue; }
        if (string.IsNullOrWhiteSpace(conversationId) || conversationId.Length < 10) { noPhone++; continue; }

        // Call Outbound trigger
        var triggerBody = JsonSerializer.Serialize(new
        {
            @event = "lead_followup",
            phone = conversationId,
            variables = new Dictionary<string, string>
            {
                ["conversation_id"] = conversationId,
                ["priority_score"] = priorityScore.ToString("F1"),
                ["outcome"] = outcomeLabel
            }
        });

        var (obStatus, _) = await obClient.ProxyPostAsync(
            "/api/v1/webhook/trigger", triggerBody, authHeader, requestId);

        if (obStatus is >= 200 and < 300)
        {
            triggered++;
            // Update status in WA — log failure but don't halt the batch
            var statusBody = JsonSerializer.Serialize(new { status = "triggered" });
            var (putStatus, _) = await waClient.ProxyPutAsync(
                $"/api/v1/wa/{tenantId}/ri/rescue/{Uri.EscapeDataString(conversationId)}/status",
                statusBody, authHeader, requestId);
            if (putStatus is not (>= 200 and < 300))
                jsonLog.SystemWarn($"[RI-Rescue] {ErrorCodes.WARescueStatusUpdateFailed} Status update failed for {conversationId}: status={putStatus}");
        }
        else
        {
            errors++;
            jsonLog.SystemWarn($"[RI-Rescue] {ErrorCodes.BackendRiRescueTriggerFailed} Outbound trigger failed for {conversationId}: status={obStatus}");
        }
    }

    jsonLog.SystemInfo($"[RI-Rescue] Trigger complete for tenant={tenantId}: triggered={triggered}, skipped={skipped}, noPhone={noPhone}, errors={errors}");

    return Results.Ok(new
    {
        requestId,
        tenantId,
        triggered,
        skipped,
        no_phone = noPhone,
        errors
    });
});

// POST /api/ops/ri/sync-marketing/{tenantId} — Sync RI objections + templates → Marketing risks + rescue templates (RI-8.9)
app.MapPost("/api/ops/ri/sync-marketing/{tenantId:int}", async (
    HttpContext ctx,
    WhatsAppAnalyticsClient waClient,
    MarketingClient mktClient,
    JsonLinesLogger jsonLog,
    int tenantId) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT not configured", requestId), statusCode: 500);

    // Read sector from body
    string sector;
    try
    {
        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, string>>();
        sector = body?.GetValueOrDefault("sector") ?? "";
    }
    catch (JsonException ex)
    {
        jsonLog.SystemWarn($"[RI-MktSync] Invalid request body: {ex.Message}");
        sector = "";
    }

    if (string.IsNullOrWhiteSpace(sector))
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "sector is required in request body", requestId), statusCode: 400);

    var serviceToken = jwtGenerator.GenerateServiceToken(tenantId);
    var authHeader = $"Bearer {serviceToken}";
    int risksSynced = 0, templatesSynced = 0, skipped = 0, errors = 0;

    // Step 1: Fetch objection map from WA → create Marketing risks
    var (objStatus, objBody) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenantId}/ri/objections", authHeader, requestId);

    if (objStatus == 200 && !string.IsNullOrEmpty(objBody))
    {
        try
        {
            var objJson = JsonDocument.Parse(objBody);
            var dataEl = objJson.RootElement.GetProperty("data");
            if (dataEl.TryGetProperty("objectionTypes", out var typesArray))
            {
                foreach (var ot in typesArray.EnumerateArray())
                {
                    var objType = ot.TryGetProperty("objectionType", out var otVal) ? otVal.GetString() ?? "" : "";
                    var objLabel = ot.TryGetProperty("objectionLabel", out var olVal) ? olVal.GetString() ?? "" : "";
                    var count = ot.TryGetProperty("count", out var cVal) ? cVal.GetInt32() : 0;
                    var pct = ot.TryGetProperty("percentage", out var pVal) ? pVal.GetDouble() : 0;

                    if (string.IsNullOrWhiteSpace(objType)) { skipped++; continue; }

                    // Map objection percentage to risk level
                    var riskLevel = pct >= 30 ? "critical" : pct >= 20 ? "high" : pct >= 10 ? "medium" : "low";
                    var riskScore = (short)Math.Clamp((int)(pct * 2), 1, 100);

                    var riskBody = JsonSerializer.Serialize(new
                    {
                        customer_phone = $"ri_objection_{objType}",
                        conversation_id = $"sector:{sector}",
                        risk_score = riskScore,
                        risk_level = riskLevel,
                        trigger_reason = $"RI objection: {objLabel} ({count} occurrences, {pct:F1}%)"
                    });

                    var (mktStatus, _) = await mktClient.ProxyPostAsync(
                        "/api/v1/rescue/risks", riskBody, authHeader, requestId);

                    if (mktStatus is >= 200 and < 300) risksSynced++;
                    else if (mktStatus == 409) skipped++;
                    else { errors++; jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Risk sync failed for '{objType}': Marketing returned {mktStatus}"); }
                }
            }
        }
        catch (JsonException ex)
        {
            jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Objection parse failed: {ex.Message}");
            errors++;
        }
        catch (KeyNotFoundException ex)
        {
            jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Missing data in WA objection response: {ex.Message}");
            errors++;
        }
    }
    else if (objStatus != 404) // 404 = no data yet, not an error
    {
        jsonLog.SystemWarn($"[RI-MktSync] WA objections fetch: status={objStatus}");
    }

    // Step 2: Fetch sector templates (objection_handlers + followup_templates) → Marketing rescue templates
    var (tplStatus, tplBody) = await waClient.ProxyGetAsync(
        $"/api/v1/wa/{tenantId}/ri/templates?sector={Uri.EscapeDataString(sector)}",
        authHeader, requestId);

    if (tplStatus == 200 && !string.IsNullOrEmpty(tplBody))
    {
        try
        {
            var tplJson = JsonDocument.Parse(tplBody);
            var dataEl = tplJson.RootElement.GetProperty("data");

            // Map objection handlers → rescue templates
            if (dataEl.TryGetProperty("objectionHandlers", out var ohArray))
            {
                foreach (var oh in ohArray.EnumerateArray())
                {
                    var isActive = oh.TryGetProperty("isActive", out var aVal) && aVal.GetBoolean();
                    if (!isActive) { skipped++; continue; }

                    var objType = oh.TryGetProperty("objectionType", out var otVal) ? otVal.GetString() ?? "" : "";
                    var objLabel = oh.TryGetProperty("objectionLabel", out var olVal) ? olVal.GetString() ?? "" : "";
                    var responseTemplate = oh.TryGetProperty("responseTemplate", out var rtVal) ? rtVal.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(objType) || string.IsNullOrWhiteSpace(responseTemplate)) { skipped++; continue; }

                    // Map objection type to strategy
                    var strategy = objType switch
                    {
                        "price_high" => "discount",
                        "chose_competitor" => "discount",
                        "no_trust" => "apology",
                        "out_of_stock" => "exchange",
                        _ => "apology"
                    };

                    var templateBody = JsonSerializer.Serialize(new
                    {
                        template_name = $"RI_{sector}_{objType}",
                        risk_level = "medium",
                        strategy,
                        message_template = responseTemplate,
                        max_discount_pct = strategy == "discount" ? (short?)10 : null
                    });

                    var (mktStatus, _) = await mktClient.ProxyPostAsync(
                        "/api/v1/rescue/templates", templateBody, authHeader, requestId);

                    if (mktStatus is >= 200 and < 300) templatesSynced++;
                    else if (mktStatus == 409) skipped++;
                    else { errors++; jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Template sync failed for '{objType}': Marketing returned {mktStatus}"); }
                }
            }

            // Map followup templates → rescue templates
            if (dataEl.TryGetProperty("followupTemplates", out var ftArray))
            {
                foreach (var ft in ftArray.EnumerateArray())
                {
                    var isActive = ft.TryGetProperty("isActive", out var aVal) && aVal.GetBoolean();
                    if (!isActive) { skipped++; continue; }

                    var templateType = ft.TryGetProperty("templateType", out var ttVal) ? ttVal.GetString() ?? "" : "";
                    var messageTemplate = ft.TryGetProperty("messageTemplate", out var mtVal) ? mtVal.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(templateType) || string.IsNullOrWhiteSpace(messageTemplate)) { skipped++; continue; }

                    var templateBody = JsonSerializer.Serialize(new
                    {
                        template_name = $"RI_{sector}_followup_{templateType}",
                        risk_level = "low",
                        strategy = "apology",
                        message_template = messageTemplate
                    });

                    var (mktStatus, _) = await mktClient.ProxyPostAsync(
                        "/api/v1/rescue/templates", templateBody, authHeader, requestId);

                    if (mktStatus is >= 200 and < 300) templatesSynced++;
                    else if (mktStatus == 409) skipped++;
                    else { errors++; jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Followup template sync failed for '{templateType}': Marketing returned {mktStatus}"); }
                }
            }
        }
        catch (JsonException ex)
        {
            jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Template parse failed: {ex.Message}");
            errors++;
        }
        catch (KeyNotFoundException ex)
        {
            jsonLog.SystemWarn($"[RI-MktSync] {ErrorCodes.BackendRiMarketingSyncFailed} Missing data in WA template response: {ex.Message}");
            errors++;
        }
    }
    else if (tplStatus != 404)
    {
        jsonLog.SystemWarn($"[RI-MktSync] WA templates fetch: status={tplStatus}");
    }

    jsonLog.SystemInfo($"[RI-MktSync] Marketing sync for tenant={tenantId} sector={sector}: " +
        $"risks={risksSynced}, templates={templatesSynced}, skipped={skipped}, errors={errors}");

    return Results.Ok(new
    {
        requestId,
        tenantId,
        sector,
        risks_synced = risksSynced,
        templates_synced = templatesSynced,
        skipped,
        errors
    });
});

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
// FEAT-PHOTO wire-up patch (2026-04-28): /api/v1/appointments/book proxy expanded
// to schedule PhotoRequestDispatchJob 30sn after a successful booking.
//
// G7 SCHEDULER HOST EXCEPTION (Backend.csproj line 5-13, see also EFS lesson
// 2026-04-22): Backend is the SOLE Hangfire scheduler in this monorepo and holds
// PrivateAssets="all" ProjectReference to Invekto.Automation, Invekto.Appointments,
// Invekto.Marketing, Invekto.Integrations, Invekto.WhatsAppAnalytics. Hangfire
// DelayedJobScheduler reflects on job [Queue] attributes when promoting Scheduled
// -> Enqueued; without these compile-time refs Backend throws FileNotFoundException
// "Could not resolve assembly 'Invekto.<service>'" at promotion-time. Worker
// servers (Automation, Appointments, etc.) load only their own queue (queue-per-
// service topology, AddInvektoHangfire). The G7 exception is the SINGLE documented
// place where direct cross-service compile-references are allowed; CLAUDE.md's
// "no using Invekto.<other>" rule has G7 as its named carve-out.
//
// Therefore: trigger lives here (Backend), NOT in Invekto.Appointments. Appointments
// service stays Invekto.Shared-only (csproj has only Invekto.Shared ref). The same
// rule applies to webhook media hop above (PhotoInboundHandler is Backend-internal
// so no cross-service issue there).
//
// Plan arch/plans/20260428-feat-photo-wireup-patch.json AC5.
//
// Lead lookup: SELECT id FROM leads WHERE tenant_id=@tid AND phone=@phone LIMIT 1.
// No match -> graceful skip + INV-AT-086 log (manuel koordinator booking veya
// FlowBuilder disi kanaldan gelen rezervasyonlar foto akisini atlar). Transient
// failures (NpgsqlException / JsonException / InvalidOperationException) typed
// catches with distinct INV codes (INV-AT-078 / INV-AT-087 / INV-AT-087).
//
// Lead lookup: SELECT id FROM leads WHERE tenant_id=@tid AND phone=@phone LIMIT 1.
// No match -> graceful skip + INV log (manuel koordinator booking veya FlowBuilder
// disi kanaldan gelen rezervasyonlar foto akisini atlar).
app.MapPost("/api/v1/appointments/book", async (HttpContext ctx, AppointmentsClient apClient, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();

    string requestBody;
    using (var reader = new StreamReader(ctx.Request.Body))
        requestBody = await reader.ReadToEndAsync();

    // CQ1 (Codex iter 1) fix: typed catch around proxy call — Appointments servisi
    // unreachable / 5xx / timeout durumunda actionable INV-coded response don.
    // Mevcut generic AppointmentsProxyPost helper'i da try/catch yapmiyor; bu
    // expanded handler defense-in-depth ekliyor. Response shape (statusCode + body)
    // generic helper ile birebir ayni — sadece headers ekleme YOK (helper de yok),
    // CQ8 parity korunuyor.
    int statusCode;
    string? body;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        (statusCode, body) = await apClient.ProxyPostAsync("/api/v1/appointments/book", requestBody, authHeader, requestId);
    }
    catch (HttpRequestException ex)
    {
        sw.Stop();
        jsonLog.SystemWarn(
            $"[{ErrorCodes.AppointmentsProxyTransient}] Appointments proxy /book transport error: {ex.Message} time={sw.ElapsedMilliseconds}ms");
        // CQ12 (Codex iter 3) fix: requestId client-controlled (X-Request-Id header
        // line 4849); manuel string interpolation yerine JsonSerializer.Serialize
        // ile guvenli JSON construction (response-field injection riskini elimine
        // eder, ozel karakterler escape edilir).
        ctx.Response.StatusCode = 503;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            error_code = ErrorCodes.AppointmentsProxyTransient,
            message = "Appointments servisi gecici olarak kullanilamiyor; birkac saniye sonra tekrar deneyin",
            request_id = requestId
        }));
        return Results.Empty;
    }
    catch (TaskCanceledException ex)
    {
        sw.Stop();
        jsonLog.SystemWarn(
            $"[{ErrorCodes.AppointmentsProxyTransient}] Appointments proxy /book timeout: {ex.Message} time={sw.ElapsedMilliseconds}ms");
        ctx.Response.StatusCode = 504;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            error_code = ErrorCodes.AppointmentsProxyTransient,
            message = "Appointments servisi yanit vermedi; birkac saniye sonra tekrar deneyin",
            request_id = requestId
        }));
        return Results.Empty;
    }
    sw.Stop();

    jsonLog.StepInfo($"Appointments proxy POST /api/v1/appointments/book: status={statusCode}, time={sw.ElapsedMilliseconds}ms", requestId);

    // Photo dispatch hook (post-201 only). CQ2 + CQ12 (Codex iter 1) fix: TUM skip
    // path'lerinde INV-AT-086 audit log; silent skip yok. Failure modes are logged
    // but never bubble to the caller — booking 201 stays the source of truth.
    //
    // CQ11 (Codex iter 1) DB-code sync: leads tablosu schema referansi:
    //   arch/db/pkt6b1-niche-business.sql — leads(id integer PK, tenant_id integer
    //   NOT NULL, phone text NOT NULL, ...) — pre-existing tablo, FEAT-PHOTO
    //   migration 034 sadece foto-state kolonlari ekledi (photo_status, photo_count,
    //   photo_received_at). Phone parametresi raw (E.164 normalization caller
    //   sorumlulugunda; AutomationRepository.cs:907 SELECT preferred_locale FROM
    //   leads WHERE phone=@phone precedent — pilot pattern).
    if (statusCode == 201)
    {
        try
        {
            // CQ12/Q3 (Codex iter 2) fix: INV-AT-086 SADECE semantic 'lead not matched';
            // DI-null / TenantContext-null / missing patient_phone (infra/auth/payload
            // misconfiguration paths) INV-AT-087 PhotoRequestHookTransient kullanir.
            var tenantContext = ctx.Items["TenantContext"] as TenantContext;
            if (tenantContext == null)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.PhotoRequestHookTransient}] PhotoRequestDispatchJob hook skipped: " +
                    $"missing TenantContext post-201 (auth middleware bypass scenario; booking " +
                    $"already accepted, photo flow skipped)");
            }
            else if (pgFactory == null)
            {
                jsonLog.SystemWarn(
                    $"[{ErrorCodes.PhotoRequestHookTransient}] PhotoRequestDispatchJob hook skipped: " +
                    $"pgFactory DI not configured tenant={tenantContext.TenantId} (booking " +
                    $"accepted, photo flow skipped — production'da bu path'e duslmemeli)");
            }
            else
            {
                // Parse patient_phone from request body for lead lookup.
                using var reqDoc = System.Text.Json.JsonDocument.Parse(requestBody);
                var patientPhone = reqDoc.RootElement.TryGetProperty("patient_phone", out var phoneEl)
                    ? phoneEl.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(patientPhone))
                {
                    jsonLog.SystemWarn(
                        $"[{ErrorCodes.PhotoRequestHookTransient}] PhotoRequestDispatchJob hook skipped: " +
                        $"missing patient_phone in booking request body tenant={tenantContext.TenantId} " +
                        $"(booking accepted, photo flow skipped — INMA payload bug)");
                }
                else
                {
                    int? leadId = null;
                    await using (var conn = await pgFactory.OpenConnectionAsync())
                    await using (var cmd = new Npgsql.NpgsqlCommand(
                        "SELECT id FROM leads WHERE tenant_id = @tid AND phone = @phone LIMIT 1",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("tid", tenantContext.TenantId);
                        cmd.Parameters.AddWithValue("phone", patientPhone);
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                            leadId = Convert.ToInt32(result);
                    }

                    if (leadId.HasValue)
                    {
                        BackgroundJob.Schedule<Invekto.Automation.Services.Jobs.PhotoRequestDispatchJob>(
                            j => j.ExecuteAsync(tenantContext.TenantId, leadId.Value, CancellationToken.None),
                            TimeSpan.FromSeconds(30));
                        jsonLog.StepInfo(
                            $"PhotoRequestDispatchJob scheduled tenant={tenantContext.TenantId} " +
                            $"lead={leadId.Value} delay=30s (post-booking)",
                            requestId);
                    }
                    else
                    {
                        jsonLog.StepInfo(
                            $"[{ErrorCodes.PhotoRequestLeadResolveSkip}] PhotoRequestDispatchJob skipped: " +
                            $"no lead matches patient_phone tenant={tenantContext.TenantId} (booking " +
                            $"accepted, photo flow skipped — manuel koordinator booking veya FlowBuilder " +
                            $"disi kanal)",
                            requestId);
                    }
                }
            }
        }
        catch (Npgsql.NpgsqlException ex)
        {
            // DB transient — distinct from semantic skip (INV-AT-086).
            jsonLog.SystemWarn(
                $"[{ErrorCodes.PhotoInboundHandlerDbError}] PhotoRequestDispatchJob hook: " +
                $"DB error: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Caller payload bug — non-DB transient.
            jsonLog.SystemWarn(
                $"[{ErrorCodes.PhotoRequestHookTransient}] PhotoRequestDispatchJob hook: " +
                $"request body parse fail: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // Hangfire JobStorage not initialised veya DI state hatasi — non-DB transient.
            jsonLog.SystemWarn(
                $"[{ErrorCodes.PhotoRequestHookTransient}] PhotoRequestDispatchJob hook: " +
                $"Hangfire JobStorage not initialised: {ex.Message}");
        }
    }

    // CQ8 (Codex iter 1) parity: response shape generic AppointmentsProxyPost helper
    // ile birebir aynidir — sadece statusCode + body + ContentType='application/json'
    // yazilir; helper de zaten upstream response headers'i preserve etmiyor (line
    // 4592-4611 reference). Hicbir behavior degisikligi yok; sadece photo hook
    // genisletildi.
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
});

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

// FEAT-LIW Chunk A: Lead Intake Webhook — public endpoint, per-tenant API key auth.
// JWT middleware is excluded for /api/v1/leads/intake/ (see UseJwtAuth configuration
// above); LeadIntakeService enforces API key + rate limit + field map + phone parse
// + consent before UPSERT, then enqueues the welcome flow for fresh leads only.
app.MapPost("/api/v1/leads/intake/{source_slug}", async (
    string source_slug,
    LeadIntakeRequest? request,
    HttpContext ctx,
    LeadIntakeService intakeService) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var apiKey = ctx.Request.Headers["X-Invekto-Api-Key"].FirstOrDefault();

    var outcome = await intakeService.IntakeAsync(source_slug, apiKey, request, rid, ctx.RequestAborted);

    if (outcome.StatusCode == 429 && outcome.RetryAfterSeconds is int retry)
        ctx.Response.Headers["Retry-After"] = retry.ToString();

    if (outcome.Success != null)
        return Results.Json(outcome.Success, statusCode: outcome.StatusCode);

    return Results.Json(outcome.Error, statusCode: outcome.StatusCode);
});

// FEAT-LIW Chunk B: WA-direct service-to-service internal intake. Called by
// Automation when an inbound WA message comes from a phone Backend doesn't yet
// know about. Auth is two-layered (iter 2 fix for CQ9 + CQ12): (1) the standard
// JWT middleware runs on /api/internal/ and sets TenantContext from the JWT
// tenant_id claim — Automation mints these per-call via JwtGenerator
// .GenerateServiceToken so each request is bound to the tenant on whose behalf
// it is acting; (2) IntakeInternalAuth.Validate cross-checks the
// X-Internal-Service-Token header against InternalServices:SharedSecret to
// prove the caller is a peer Invekto service (not an arbitrary holder of a
// valid tenant JWT). The endpoint then verifies that payload.tenant_id matches
// TenantContext.TenantId before write — closing the "caller-supplied tenant_id"
// gap Codex flagged. Response envelope mirrors the rest of /api/v1/leads/* —
// 4xx/5xx use ErrorResponse.Create with the standardized INV-BE-110 user
// message from arch/errors.md (raw auth.Reason stays in server logs only,
// never reaches the client).
app.MapPost("/api/internal/leads/intake/wa-direct", async (
    WaDirectIntakeRequest? request,
    HttpContext ctx,
    IConfiguration config,
    JsonLinesLogger jsonLog,
    LeadIntakeService intakeService) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    const string standardizedAuthMessage = "Servisler arasi yetki gecersiz veya yapilandirilmamis.";

    // Layer 2: peer-service identity proof. Layer 1 (JWT tenant binding) was
    // already enforced by UseJwtAuth on the /api/internal/ prefix above; if we
    // reach this handler, TenantContext is present and the tenant_id claim is
    // signature-validated. The shared-secret header makes it harder for a
    // tenant-scoped JWT (e.g. a leaked Dashboard token) to call this endpoint
    // by accident — only Invekto services hold the secret.
    var auth = Invekto.Backend.Services.Internal.IntakeInternalAuth.Validate(ctx, config);
    if (!auth.Ok)
    {
        jsonLog.SystemWarn(
            $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] /api/internal/leads/intake/wa-direct: " +
            $"internal-auth check failed (status={auth.StatusCode}, reason='{auth.Reason}', requestId={rid})");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, standardizedAuthMessage, "-"),
            statusCode: auth.StatusCode);
    }

    // JWT-bound tenant binding: payload tenant_id MUST match the signed JWT
    // claim. This is the iter 2 reinforcement for CQ9 — even with a valid
    // shared-secret + a valid tenant JWT, the caller cannot write to a
    // different tenant than the one they authenticated for.
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        // Belt-and-braces: JWT middleware should have set this; if it didn't,
        // refuse the write rather than fall through to caller-trust mode.
        jsonLog.SystemError(
            $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] /api/internal/leads/intake/wa-direct: " +
            $"TenantContext missing despite JWT-required prefix (middleware misconfiguration; requestId={rid})");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, standardizedAuthMessage, "-"),
            statusCode: 401);
    }

    if (request != null && request.TenantId != tenantContext.TenantId)
    {
        jsonLog.SystemWarn(
            $"[{ErrorCodes.LeadIntakeInternalAuthInvalid}] /api/internal/leads/intake/wa-direct: " +
            $"tenant_id mismatch: payload={request.TenantId} jwt_claim={tenantContext.TenantId} requestId={rid}");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.LeadIntakeInternalAuthInvalid, standardizedAuthMessage, "-"),
            statusCode: 403);
    }

    var outcome = await intakeService.IntakeWaDirectAsync(request, rid, ctx.RequestAborted);

    if (outcome.Success != null)
        return Results.Json(outcome.Success, statusCode: outcome.StatusCode);

    return Results.Json(outcome.Error, statusCode: outcome.StatusCode);
});

// ============================================
// FEAT-LIW Chunk C: TENANT LANDING SETTINGS ENDPOINTS (/api/v1/tenant/landing/*)
// Consumed by Dashboard LeadIntakeSettingsPage. Tenant-scoped JWT (tenant_id from
// claim only — never request parameter). Envelope conforms to /api/v1/leads/*
// (ErrorResponse.Create on 4xx/5xx, typed DTO on 2xx). Mutations use optimistic
// concurrency via tenant_landing_settings.updated_at — 0 rows on UPDATE -> 409
// INV-BE-113 and the Dashboard refetches + retries. Every rotate/revoke/fieldmap.save
// writes a liw_audit_log row INSIDE the same transaction as the settings UPDATE.
// ============================================

// FEAT-LIW Chunk C: standardized tenant-auth envelope. Middleware normally sets
// TenantContext; reaching a handler without it means a misconfiguration or a path
// excluded from JWT. Using INV-AUTH-003 with a user-facing TR message matches the
// arch/errors.md contract ("Oturum gecerli degil; tekrar giris yapin.").
const string tenantContextUnauthorizedMessage = "Oturum gecerli degil; tekrar giris yapin.";

app.MapGet("/api/v1/tenant/landing/settings", async (HttpContext ctx, [FromServices] LiwSettingsService svc) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, tenantContextUnauthorizedMessage, rid), statusCode: 401);

    var result = await svc.GetSettingsAsync(tenantContext.TenantId, ctx.RequestAborted);
    if (result.Status == MutationStatus.Success && result.Payload != null)
        return Results.Json(result.Payload, statusCode: 200);

    return Results.Json(
        ErrorResponse.Create(result.ErrorCode ?? ErrorCodes.GeneralUnknown, result.Message ?? "Ayarlar yuklenemedi.", rid),
        statusCode: result.HttpStatus);
});

app.MapPost("/api/v1/tenant/landing/apikey/rotate", async (HttpContext ctx, LiwSettingsService svc) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, tenantContextUnauthorizedMessage, rid), statusCode: 401);

    // If-Match header is optional on first-time rotate (no row exists yet); required when a row already exists.
    var ifMatch = ctx.Request.Headers["If-Match"].FirstOrDefault();
    DateTime? expected = null;
    if (!string.IsNullOrWhiteSpace(ifMatch))
    {
        if (!DateTime.TryParse(ifMatch, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            // Malformed If-Match supplied (non-empty but unparseable) is a client bug, not a concurrency conflict → 400.
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.GeneralValidation,
                    "If-Match header gecersiz (ISO 8601 datetime bekleniyor).", rid),
                statusCode: 400);
        }
        expected = parsed;
    }

    var result = await svc.RotateApiKeyAsync(tenantContext.TenantId, tenantContext.UserId, expected, ctx.RequestAborted);
    if (result.Status == MutationStatus.Success && result.Payload != null)
        return Results.Json(result.Payload, statusCode: 200);

    return Results.Json(
        ErrorResponse.Create(result.ErrorCode ?? ErrorCodes.GeneralUnknown, result.Message ?? "Anahtar yenileme basarisiz.", rid),
        statusCode: result.HttpStatus);
});

app.MapPost("/api/v1/tenant/landing/apikey/revoke", async (HttpContext ctx, LiwSettingsService svc) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, tenantContextUnauthorizedMessage, rid), statusCode: 401);

    // Missing If-Match on /revoke is a BAD REQUEST (client forgot to include the required
    // header) — distinct from a concurrency conflict (409 INV-BE-113 is reserved for cases
    // where the header IS supplied but does not match the server's current row_version).
    // Codex iter 0 CQ1+CQ12: conflating 400 with 409 is a contract bug.
    var ifMatch = ctx.Request.Headers["If-Match"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(ifMatch))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation,
                "If-Match header zorunlu (revoke oncesi GET /settings ile row_version alinmali).", rid),
            statusCode: 400);
    }
    if (!DateTime.TryParse(ifMatch, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out var expected))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation,
                "If-Match header gecersiz (ISO 8601 datetime bekleniyor).", rid),
            statusCode: 400);
    }

    var result = await svc.RevokeApiKeyAsync(tenantContext.TenantId, tenantContext.UserId, expected, ctx.RequestAborted);
    if (result.Status == MutationStatus.Success)
        return Results.StatusCode(204);

    return Results.Json(
        ErrorResponse.Create(result.ErrorCode ?? ErrorCodes.GeneralUnknown, result.Message ?? "Anahtar iptal basarisiz.", rid),
        statusCode: result.HttpStatus);
});

app.MapPut("/api/v1/tenant/landing/fieldmap", async (HttpContext ctx, UpdateFieldMapRequest? request, LiwSettingsService svc) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, tenantContextUnauthorizedMessage, rid), statusCode: 401);

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.LeadIntakeFieldMapRequiredMissing,
            "Alan eslemesinde zorunlu canonical alan eksik veya hatali.", rid), statusCode: 400);

    // If-Match header takes priority over body.expected_row_version when both are provided;
    // endpoint layer normalizes the two pathways before handing to the service.
    if (request.ExpectedRowVersion == null)
    {
        var ifMatch = ctx.Request.Headers["If-Match"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(ifMatch) &&
            DateTime.TryParse(ifMatch, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            request.ExpectedRowVersion = parsed;
        }
    }

    var result = await svc.UpdateFieldMapAsync(tenantContext.TenantId, tenantContext.UserId, request, ctx.RequestAborted);
    if (result.Status == MutationStatus.Success && result.Payload != null)
        return Results.Json(result.Payload, statusCode: 200);

    // Validation failures include the full errors[] array alongside the envelope primary code.
    if (result.Status == MutationStatus.ValidationFailed && result.ValidationErrors != null)
    {
        return Results.Json(new
        {
            error_code = result.ErrorCode,
            message = result.Message,
            request_id = rid,
            timestamp = DateTime.UtcNow,
            errors = result.ValidationErrors
        }, statusCode: result.HttpStatus);
    }

    return Results.Json(
        ErrorResponse.Create(result.ErrorCode ?? ErrorCodes.GeneralUnknown, result.Message ?? "Alan eslemesi kaydedilemedi.", rid),
        statusCode: result.HttpStatus);
});

app.MapPost("/api/v1/tenant/landing/dry-run", async (HttpContext ctx, DryRunRequest? request, LiwSettingsService svc) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, tenantContextUnauthorizedMessage, rid), statusCode: 401);

    var outcome = await svc.DryRunAsync(tenantContext.TenantId, request, ctx.RequestAborted);
    if (outcome.Status == MutationStatus.Success && outcome.Payload != null)
        return Results.Json(outcome.Payload, statusCode: 200);

    return Results.Json(
        ErrorResponse.Create(outcome.ErrorCode ?? ErrorCodes.GeneralUnknown, outcome.Message ?? "Dry-run basarisiz.", rid),
        statusCode: outcome.HttpStatus);
});

app.MapGet("/api/v1/tenant/landing/audit", async (HttpContext ctx, [FromServices] LiwSettingsService svc, int? limit) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, tenantContextUnauthorizedMessage, rid), statusCode: 401);

    var result = await svc.ListAuditAsync(tenantContext.TenantId, limit ?? 50, ctx.RequestAborted);
    if (result.Status == MutationStatus.Success && result.Payload != null)
        return Results.Json(new { entries = result.Payload }, statusCode: 200);

    return Results.Json(
        ErrorResponse.Create(result.ErrorCode ?? ErrorCodes.GeneralUnknown, result.Message ?? "Degisiklik gecmisi yuklenemedi.", rid),
        statusCode: result.HttpStatus);
});

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
    HttpContext ctx, [FromServices] LeadRepository leadRepo,
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

        // FEAT-INMA-PIPELINE-V2 C1 (2026-05-13): Zoho sync dispatch removed. customer_status
        // authority moves to INMA agent UI; INSE-side status updates emitted via flow action
        // node (C4 BLOCKED pending INMA contract). Lead pipeline_status remains the local
        // sales-funnel tracker but no longer triggers cross-system sync.

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
app.MapGet("/api/ops/analytics/tenants", async (HttpContext ctx, [FromServices] AnalyticsRepository analyticsRepo) =>
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
// RI-6: Revenue Intelligence proxy (tenant-facing, JWT auth via WA service)
// Generic catch-all proxy — forwards /api/v1/wa/{tenantId}/ri/* to WhatsAppAnalytics.
// WA service handles JWT validation + tenant matching via its own middleware.
// ============================================

app.Map("/api/v1/wa/{tenantId:int}/ri/{**path}", async (HttpContext ctx, WhatsAppAnalyticsClient waClient, int tenantId, string? path) =>
{
    var targetPath = $"/api/v1/wa/{tenantId}/ri/{path ?? ""}{ctx.Request.QueryString}";
    var auth = ctx.Request.Headers.Authorization.FirstOrDefault();
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    int statusCode;
    string? body;
    switch (ctx.Request.Method.ToUpperInvariant())
    {
        case "GET":
            (statusCode, body) = await waClient.ProxyGetAsync(targetPath, auth, requestId);
            break;
        case "POST":
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            (statusCode, body) = await waClient.ProxyPostAsync(targetPath, requestBody, auth, requestId);
            break;
        }
        case "PUT":
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            (statusCode, body) = await waClient.ProxyPutAsync(targetPath, requestBody, auth, requestId);
            break;
        }
        default:
            return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation,
                "Method not allowed for RI proxy", requestId), statusCode: 405);
    }

    return Results.Text(body ?? "{}", "application/json", statusCode: statusCode);
});

// ============================================
// GR-3.18: ATTRIBUTION + CAMPAIGN ANALYTICS (ops-level, Basic auth)
// ============================================

app.MapGet("/api/ops/analytics/attribution/summary", async (HttpContext ctx, [FromServices] AttributionRepository attrRepo, int? tenant_id, string? from, string? to) =>
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
    string? from, string? to, string? instance_id,
    int? limit, int? offset) =>
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
            instance_id, limit ?? 50, offset ?? 0);

        return Results.Ok(new { messages, total });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Message log query failed ({ErrorCodes.BackendMessageLogQueryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendMessageLogQueryFailed, message = "Mesaj kayitlari yuklenemedi." }, statusCode: 500);
    }
});

app.MapGet("/api/ops/channels", async (HttpContext ctx, int? tenant_id) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    var msgLogRepo = ctx.RequestServices.GetService<MessageLogRepository>();
    if (msgLogRepo == null)
        return Results.Json(new { error = ErrorCodes.BackendMessageLogQueryFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    var channels = await msgLogRepo.GetDistinctChannelsAsync(tenant_id);
    return Results.Ok(new { channels });
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
}).RequireCors("VoicePocCors"); // FEAT-VFB F0.5 Chunk E (AD-35): Voice Test browser dropdown fetch.

// GET /api/ops/tenants/{id}/flows — FEAT-VFB F0.5 (AD-42): ops-scoped flow list for the Voice Test
// flow dropdown. The browser holds only a tenant=0 admin voice-jwt; it CANNOT call Automation
// /api/v1/flows/{id} directly because (a) Automation:7108 is not externally routed and (b)
// Automation.GetValidatedTenant requires the JWT's tenant_id == route tenantId (no cross-tenant ops
// bypass) so a tenant=0 token is rejected 403. The generic /api/v1/flow-builder/flows proxy forwards
// the caller token verbatim (works only for the impersonation path the Dashboard uses), so it does
// not help here. This endpoint instead mints a per-tenant SERVICE token (the same pattern as the
// Knowledge ops proxies above) and forwards THAT to Automation, mirroring /api/ops/tenants/{id}/plan.
app.MapGet("/api/ops/tenants/{id:int}/flows", async (HttpContext ctx, int id, FlowBuilderClient fbClient, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (id <= 0)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Gecersiz tenant_id." });

    if (jwtGenerator == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthJwtGeneratorUnavailable, "JWT mint servisi su an kullanilamiyor (yapilandirma hatasi).", "-"), statusCode: 503);

    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    // Mint a service token bound to the TARGET tenant so Automation.GetValidatedTenant(tenant==route) passes.
    var serviceToken = jwtGenerator.GenerateServiceToken(id);
    var (statusCode, body) = await fbClient.ProxyGetAsync($"/api/v1/flows/{id}", $"Bearer {serviceToken}", requestId);
    ctx.Response.StatusCode = statusCode;
    ctx.Response.ContentType = "application/json";
    if (body != null) await ctx.Response.WriteAsync(body);
    return Results.Empty;
}).RequireCors("VoicePocCors"); // Voice Test browser flow dropdown fetch (cross-origin from voice.invekto.com:8443).

// GET /api/ops/tenants/{id}/voice-profile — FEAT-VFB F-VR-B: per-tenant voice behavior profile
// (business_type + capability overrides, spec §3). Server-to-server: VoiceRuntime's VoiceProfileClient
// fetches this with an admin Bearer (ValidateOpsAuth, same pattern as /api/ops/tenants) — VoiceRuntime
// has no DB. NOT a browser endpoint, so no CORS. Never 404s for an active tenant: a missing row
// (tenant provisioned after migration 050, no admin profile yet) yields a synthesized 'generic'
// default so voice never breaks; default RESOLUTION (flag inheritance) happens in VoiceRuntime.
app.MapGet("/api/ops/tenants/{id:int}/voice-profile", async (HttpContext ctx, int id, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (id <= 0)
        return Results.BadRequest(new { error = ErrorCodes.GeneralValidation, message = "Gecersiz tenant_id." });

    var profileRepo = ctx.RequestServices.GetService<VoiceTenantProfileRepository>();
    if (profileRepo == null)
        return Results.Json(
            new { error = ErrorCodes.VoiceTenantProfileQueryFailed, message = "Ses profili geçici olarak yüklenemiyor; birkaç saniye sonra tekrar deneyin."},
            statusCode: 503);

    try
    {
        var profile = await profileRepo.GetAsync(id, ctx.RequestAborted);
        if (profile == null)
        {
            // Not-yet-provisioned tenant: synthesize a generic default rather than 404.
            jsonLog.SystemWarn($"voice-profile synth-default ({ErrorCodes.VoiceTenantProfileQueryFailed}): tenant {id} has no voice_tenant_profile row, returning generic.");
            profile = new Invekto.Shared.DTOs.Voice.VoiceTenantProfileDto
            {
                TenantId = id,
                BusinessType = "generic",
                DefaultLanguage = "tr-TR",
            };
        }
        return Results.Ok(profile);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"voice-profile query failed ({ErrorCodes.VoiceTenantProfileQueryFailed}): {ex.Message}");
        return Results.Json(
            new { error = ErrorCodes.VoiceTenantProfileQueryFailed, message = "Ses profili geçici olarak yüklenemiyor; birkaç saniye sonra tekrar deneyin."},
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
// FEAT-PILOT-KANBAN: SuperAdmin pilot tracking board (Migration 035)
// ============================================
// Read-only Dashboard surface (PilotKanbanPage + KanbanDrawer); Q manuel
// kart edit yapmaz. Mutation tek path: /wrap workflow Step 3.5 hibrit
// (otomatik oner + Q onayla -> PATCH).
//
//   GET   /api/ops/kanban/{board_key}                      -> KanbanBoardDto
//   PATCH /api/ops/kanban/{board_key}/cards/{card_slug}     -> KanbanCardDto
//
// Auth: ValidateOpsAuth + OpsUnauthorized helper`lari Func parametresi olarak
// MapKanbanEndpoints`e gecirilir (lesson: pattern duplication`u onler).
app.MapKanbanEndpoints(ValidateOpsAuth, OpsUnauthorized);

// ============================================
// PLAN CRUD + TENANT PLAN MANAGEMENT (Faz 1 Paket 2)
// ============================================

// GET /api/ops/plans — List all plan definitions
app.MapGet("/api/ops/plans", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT tier_name, display_name, features_json::text, quotas_json::text, is_active, created_at, updated_at FROM plan_definitions ORDER BY tier_name", conn);

        var plans = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
        while (await reader.ReadAsync(ctx.RequestAborted))
        {
            using var featDoc = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
            using var quotaDoc = System.Text.Json.JsonDocument.Parse(reader.GetString(3));
            plans.Add(new
            {
                tier_name = reader.GetString(0),
                display_name = reader.GetString(1),
                features_json = featDoc.RootElement.Clone(),
                quotas_json = quotaDoc.RootElement.Clone(),
                is_active = reader.GetBoolean(4),
                created_at = reader.GetDateTime(5),
                updated_at = reader.GetDateTime(6),
            });
        }
        return Results.Ok(new { plans });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Plan list query failed ({ErrorCodes.BackendPlanNotFound}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "Plan listesi yüklenemedi." }, statusCode: 500);
    }
});

// GET /api/ops/plans/{tierName} — Single plan detail
app.MapGet("/api/ops/plans/{tierName}", async (HttpContext ctx, string tierName, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT tier_name, display_name, features_json::text, quotas_json::text, is_active, created_at, updated_at FROM plan_definitions WHERE tier_name = @tier", conn);
        cmd.Parameters.AddWithValue("tier", tierName);

        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
        if (!await reader.ReadAsync(ctx.RequestAborted))
            return Results.NotFound(ErrorResponse.Create(ErrorCodes.BackendPlanNotFound, $"Plan '{tierName}' bulunamadı.", ctx.TraceIdentifier));

        using var featDoc = System.Text.Json.JsonDocument.Parse(reader.GetString(2));
        using var quotaDoc = System.Text.Json.JsonDocument.Parse(reader.GetString(3));
        var plan = new
        {
            tier_name = reader.GetString(0),
            display_name = reader.GetString(1),
            features_json = featDoc.RootElement.Clone(),
            quotas_json = quotaDoc.RootElement.Clone(),
            is_active = reader.GetBoolean(4),
            created_at = reader.GetDateTime(5),
            updated_at = reader.GetDateTime(6),
        };
        return Results.Ok(plan);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Plan get failed ({ErrorCodes.BackendPlanNotFound}): tier={tierName}, {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "Plan bilgisi yüklenemedi." }, statusCode: 500);
    }
});

// POST /api/ops/plans — Create new tier
app.MapPost("/api/ops/plans", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
        var root = bodyDoc.RootElement;

        var tierName = root.TryGetProperty("tier_name", out var tn) ? tn.GetString() : null;
        var displayName = root.TryGetProperty("display_name", out var dn) ? dn.GetString() : null;

        if (string.IsNullOrWhiteSpace(tierName) || string.IsNullOrWhiteSpace(displayName))
            return Results.BadRequest(ErrorResponse.Create(ErrorCodes.GeneralValidation, "tier_name ve display_name zorunlu.", ctx.TraceIdentifier));

        if (tierName.Length > 20)
            return Results.BadRequest(ErrorResponse.Create(ErrorCodes.GeneralValidation, "tier_name max 20 karakter.", ctx.TraceIdentifier));

        var featuresJson = root.TryGetProperty("features_json", out var fj) ? fj.GetRawText() : "{}";
        var quotasJson = root.TryGetProperty("quotas_json", out var qj) ? qj.GetRawText() : "{}";

        // Validate JSON structure
        using var testFeat = System.Text.Json.JsonDocument.Parse(featuresJson);
        using var testQuota = System.Text.Json.JsonDocument.Parse(quotasJson);

        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);
        await using var cmd = new Npgsql.NpgsqlCommand(@"
            INSERT INTO plan_definitions (tier_name, display_name, features_json, quotas_json)
            VALUES (@tier, @display, @feat::jsonb, @quota::jsonb)
            ON CONFLICT (tier_name) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("tier", tierName);
        cmd.Parameters.AddWithValue("display", displayName);
        cmd.Parameters.AddWithValue("feat", featuresJson);
        cmd.Parameters.AddWithValue("quota", quotasJson);

        var rows = await cmd.ExecuteNonQueryAsync(ctx.RequestAborted);
        if (rows == 0)
            return Results.Json(ErrorResponse.Create(ErrorCodes.BackendPlanTierConflict, $"Tier '{tierName}' zaten mevcut.", ctx.TraceIdentifier), statusCode: 409);

        jsonLog.StepInfo($"Plan created: tier={tierName}, display={displayName}", Guid.NewGuid().ToString("N"));
        return Results.Ok(new { tier_name = tierName, display_name = displayName, created = true });
    }
    catch (System.Text.Json.JsonException ex)
    {
        return Results.BadRequest(ErrorResponse.Create(ErrorCodes.BackendPlanFeaturesInvalid, $"JSON format geçersiz: {ex.Message}", ctx.TraceIdentifier));
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Plan create failed ({ErrorCodes.BackendPlanTierConflict}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPlanTierConflict, message = "Plan oluşturulamadı." }, statusCode: 500);
    }
});

// PUT /api/ops/plans/{tierName} — Update features/quotas/display_name
app.MapPut("/api/ops/plans/{tierName}", async (HttpContext ctx, string tierName, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
        var root = bodyDoc.RootElement;

        var setClauses = new List<string>();
        var parameters = new List<Npgsql.NpgsqlParameter> { new("tier", tierName) };

        if (root.TryGetProperty("display_name", out var dn) && dn.GetString() is string displayName)
        {
            setClauses.Add("display_name = @display");
            parameters.Add(new("display", displayName));
        }
        if (root.TryGetProperty("features_json", out var fj))
        {
            var featJson = fj.GetRawText();
            using var test = System.Text.Json.JsonDocument.Parse(featJson);
            setClauses.Add("features_json = @feat::jsonb");
            parameters.Add(new("feat", featJson));
        }
        if (root.TryGetProperty("quotas_json", out var qj))
        {
            var quotaJson = qj.GetRawText();
            using var test = System.Text.Json.JsonDocument.Parse(quotaJson);
            setClauses.Add("quotas_json = @quota::jsonb");
            parameters.Add(new("quota", quotaJson));
        }
        if (root.TryGetProperty("is_active", out var ia))
        {
            setClauses.Add("is_active = @active");
            parameters.Add(new("active", ia.GetBoolean()));
        }

        if (setClauses.Count == 0)
            return Results.BadRequest(ErrorResponse.Create(ErrorCodes.GeneralValidation, "En az bir alan güncellenmelidir.", ctx.TraceIdentifier));

        setClauses.Add("updated_at = NOW()");

        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);
        await using var cmd = new Npgsql.NpgsqlCommand(
            $"UPDATE plan_definitions SET {string.Join(", ", setClauses)} WHERE tier_name = @tier", conn);
        foreach (var p in parameters) cmd.Parameters.Add(p);

        var rows = await cmd.ExecuteNonQueryAsync(ctx.RequestAborted);
        if (rows == 0)
            return Results.NotFound(ErrorResponse.Create(ErrorCodes.BackendPlanNotFound, $"Plan '{tierName}' bulunamadı.", ctx.TraceIdentifier));

        // Auto-invalidate all tenants on this tier
        if (planCache != null)
        {
            await using var tidCmd = new Npgsql.NpgsqlCommand(
                "SELECT tenant_id FROM tenant_registry WHERE plan_tier = @tier AND is_active = true", conn);
            tidCmd.Parameters.AddWithValue("tier", tierName);
            await using var tidReader = await tidCmd.ExecuteReaderAsync(ctx.RequestAborted);
            while (await tidReader.ReadAsync(ctx.RequestAborted))
                planCache.Invalidate(tidReader.GetInt32(0));
        }

        jsonLog.StepInfo($"Plan updated: tier={tierName}, fields={string.Join(",", setClauses)}", Guid.NewGuid().ToString("N"));
        return Results.Ok(new { tier_name = tierName, updated = true });
    }
    catch (System.Text.Json.JsonException ex)
    {
        return Results.BadRequest(ErrorResponse.Create(ErrorCodes.BackendPlanFeaturesInvalid, $"JSON format geçersiz: {ex.Message}", ctx.TraceIdentifier));
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Plan update failed ({ErrorCodes.BackendPlanNotFound}): tier={tierName}, {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "Plan güncellenemedi." }, statusCode: 500);
    }
});

// DELETE /api/ops/plans/{tierName} — Soft-delete (is_active=false)
app.MapDelete("/api/ops/plans/{tierName}", async (HttpContext ctx, string tierName, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);

        // Guard: check no active tenants on this tier
        await using var countCmd = new Npgsql.NpgsqlCommand(
            "SELECT COUNT(*) FROM tenant_registry WHERE plan_tier = @tier AND is_active = true", conn);
        countCmd.Parameters.AddWithValue("tier", tierName);
        var result = await countCmd.ExecuteScalarAsync(ctx.RequestAborted);
        var tenantCount = result is long c ? (int)c : Convert.ToInt32(result);

        if (tenantCount > 0)
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.BackendPlanHasActiveTenants,
                    $"Bu plana bağlı {tenantCount} aktif firma var, silinemez.", ctx.TraceIdentifier),
                statusCode: 409);

        await using var cmd = new Npgsql.NpgsqlCommand(
            "UPDATE plan_definitions SET is_active = false, updated_at = NOW() WHERE tier_name = @tier AND is_active = true", conn);
        cmd.Parameters.AddWithValue("tier", tierName);

        var rows = await cmd.ExecuteNonQueryAsync(ctx.RequestAborted);
        if (rows == 0)
            return Results.NotFound(ErrorResponse.Create(ErrorCodes.BackendPlanNotFound, $"Plan '{tierName}' bulunamadı veya zaten silinmiş.", ctx.TraceIdentifier));

        jsonLog.StepInfo($"Plan soft-deleted: tier={tierName}", Guid.NewGuid().ToString("N"));
        return Results.Ok(new { tier_name = tierName, deleted = true });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Plan delete failed ({ErrorCodes.BackendPlanNotFound}): tier={tierName}, {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "Plan silinemedi." }, statusCode: 500);
    }
});

// GET /api/ops/tenants/{id}/plan — Tenant plan info + effective features + usage
app.MapGet("/api/ops/tenants/{id}/plan", async (HttpContext ctx, int id, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendTenantPlanUpdateFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);
        await using var cmd = new Npgsql.NpgsqlCommand(@"
            SELECT tr.plan_tier, tr.features_json::text, pd.features_json::text AS tier_features,
                   pd.quotas_json::text, pd.display_name,
                   COALESCE((SELECT messages_sent FROM tenant_usage WHERE tenant_id = @tid AND period_month = date_trunc('month', NOW())::date), 0)
            FROM tenant_registry tr
            LEFT JOIN plan_definitions pd ON pd.tier_name = tr.plan_tier AND pd.is_active = true
            WHERE tr.tenant_id = @tid", conn);
        cmd.Parameters.AddWithValue("tid", id);

        await using var reader = await cmd.ExecuteReaderAsync(ctx.RequestAborted);
        if (!await reader.ReadAsync(ctx.RequestAborted))
            return Results.NotFound(ErrorResponse.Create(ErrorCodes.IntegrationTenantNotFound, $"Tenant {id} bulunamadı.", ctx.TraceIdentifier));

        var planTier = reader.GetString(0);
        var tenantFeatJson = reader.IsDBNull(1) ? null : reader.GetString(1);
        var tierFeatJson = reader.IsDBNull(2) ? null : reader.GetString(2);
        var quotasJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        var tierDisplayName = reader.IsDBNull(4) ? planTier : reader.GetString(4);
        var messagesSent = reader.GetInt32(5);

        // Parse effective features (tenant override > tier defaults)
        var effectiveJson = !string.IsNullOrWhiteSpace(tenantFeatJson) && tenantFeatJson != "{}" ? tenantFeatJson : tierFeatJson;
        using var effectiveDoc = System.Text.Json.JsonDocument.Parse(!string.IsNullOrWhiteSpace(effectiveJson) ? effectiveJson : "{}");
        var effectiveFeatures = effectiveDoc.RootElement.Clone();

        using var quotasDoc = System.Text.Json.JsonDocument.Parse(!string.IsNullOrWhiteSpace(quotasJson) ? quotasJson : "{}");
        var quotas = quotasDoc.RootElement.Clone();

        return Results.Ok(new
        {
            tenant_id = id,
            plan_tier = planTier,
            tier_display_name = tierDisplayName,
            has_override = !string.IsNullOrWhiteSpace(tenantFeatJson) && tenantFeatJson != "{}",
            effective_features = effectiveFeatures,
            quotas,
            usage = new { messages_sent = messagesSent }
        });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tenant plan get failed ({ErrorCodes.BackendTenantPlanUpdateFailed}): tenant={id}, {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendTenantPlanUpdateFailed, message = "Firma plan bilgisi yüklenemedi." }, statusCode: 500);
    }
});

// PUT /api/ops/tenants/{id}/plan — Update plan_tier and/or features_json override
app.MapPut("/api/ops/tenants/{id}/plan", async (HttpContext ctx, int id, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendTenantPlanUpdateFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    try
    {
        using var bodyDoc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
        var root = bodyDoc.RootElement;

        var setClauses = new List<string>();
        var parameters = new List<Npgsql.NpgsqlParameter> { new("tid", id) };

        if (root.TryGetProperty("plan_tier", out var pt) && pt.GetString() is string newTier)
        {
            setClauses.Add("plan_tier = @tier");
            parameters.Add(new("tier", newTier));
        }
        if (root.TryGetProperty("features_json", out var fj))
        {
            if (fj.ValueKind == System.Text.Json.JsonValueKind.Null)
            {
                setClauses.Add("features_json = NULL");
            }
            else
            {
                var featJson = fj.GetRawText();
                using var test = System.Text.Json.JsonDocument.Parse(featJson);
                setClauses.Add("features_json = @feat::jsonb");
                parameters.Add(new("feat", featJson));
            }
        }

        if (setClauses.Count == 0)
            return Results.BadRequest(ErrorResponse.Create(ErrorCodes.GeneralValidation, "plan_tier veya features_json gerekli.", ctx.TraceIdentifier));

        setClauses.Add("updated_at = NOW()");

        await using var conn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync(ctx.RequestAborted);

        // Validate new tier exists if provided
        if (root.TryGetProperty("plan_tier", out var tierCheck) && tierCheck.GetString() is string tierToCheck)
        {
            await using var checkCmd = new Npgsql.NpgsqlCommand(
                "SELECT 1 FROM plan_definitions WHERE tier_name = @t AND is_active = true", conn);
            checkCmd.Parameters.AddWithValue("t", tierToCheck);
            var exists = await checkCmd.ExecuteScalarAsync(ctx.RequestAborted);
            if (exists == null)
                return Results.NotFound(ErrorResponse.Create(ErrorCodes.BackendPlanNotFound, $"Plan '{tierToCheck}' bulunamadı veya aktif değil.", ctx.TraceIdentifier));
        }

        await using var cmd = new Npgsql.NpgsqlCommand(
            $"UPDATE tenant_registry SET {string.Join(", ", setClauses)} WHERE tenant_id = @tid AND is_active = true", conn);
        foreach (var p in parameters) cmd.Parameters.Add(p);

        var rows = await cmd.ExecuteNonQueryAsync(ctx.RequestAborted);
        if (rows == 0)
            return Results.NotFound(ErrorResponse.Create(ErrorCodes.IntegrationTenantNotFound, $"Tenant {id} bulunamadı veya aktif değil.", ctx.TraceIdentifier));

        // Auto-invalidate cache
        planCache?.Invalidate(id);

        jsonLog.StepInfo($"Tenant plan updated: tenant={id}, fields={string.Join(",", setClauses)}", Guid.NewGuid().ToString("N"));
        return Results.Ok(new { tenant_id = id, updated = true });
    }
    catch (System.Text.Json.JsonException ex)
    {
        return Results.BadRequest(ErrorResponse.Create(ErrorCodes.BackendPlanFeaturesInvalid, $"JSON format geçersiz: {ex.Message}", ctx.TraceIdentifier));
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"Tenant plan update failed ({ErrorCodes.BackendTenantPlanUpdateFailed}): tenant={id}, {ex.Message}");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.BackendTenantPlanUpdateFailed, "Firma plan güncellemesi başarısız oldu.", ctx.TraceIdentifier),
            statusCode: 500);
    }
});

// POST /api/ops/plans/invalidate/{tenantId} — Manual cache invalidation (0 = all)
app.MapPost("/api/ops/plans/invalidate/{tenantId}", async (HttpContext ctx, int tenantId, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (planCache == null)
        return Results.Json(new { error = ErrorCodes.BackendPlanNotFound, message = "Plan cache not initialized" }, statusCode: 503);

    if (tenantId == 0)
    {
        planCache.InvalidateAll();
        jsonLog.StepInfo("Plan cache invalidated: ALL tenants", Guid.NewGuid().ToString("N"));
        return Results.Ok(new { invalidated = true, tenant_id = 0, message = "Tüm cache temizlendi." });
    }

    planCache.Invalidate(tenantId);
    jsonLog.StepInfo($"Plan cache invalidated: tenant={tenantId}", Guid.NewGuid().ToString("N"));
    return Results.Ok(new { invalidated = true, tenant_id = tenantId });
});

// GET /api/ops/tenants/{id}/license — Birleşik lisans bilgisi (Invekto PG + INMA MSSQL readonly)
app.MapGet("/api/ops/tenants/{id}/license", async (HttpContext ctx, int id, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return OpsUnauthorized(ctx);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendTenantPlanUpdateFailed, message = "PostgreSQL not configured" }, statusCode: 503);

    // --- Invekto: PostgreSQL (plan + usage) ---
    object? invektoData = null;
    try
    {
        await using var pgConn = new Npgsql.NpgsqlConnection(pgConnectionString);
        await pgConn.OpenAsync(ctx.RequestAborted);
        await using var pgCmd = new Npgsql.NpgsqlCommand(@"
            SELECT tr.plan_tier, tr.features_json::text, pd.features_json::text AS tier_features,
                   pd.quotas_json::text, pd.display_name,
                   COALESCE((SELECT messages_sent FROM tenant_usage WHERE tenant_id = @tid AND period_month = date_trunc('month', NOW())::date), 0),
                   to_char(date_trunc('month', NOW()), 'YYYY-MM-DD')
            FROM tenant_registry tr
            LEFT JOIN plan_definitions pd ON pd.tier_name = tr.plan_tier AND pd.is_active = true
            WHERE tr.tenant_id = @tid", pgConn);
        pgCmd.Parameters.AddWithValue("tid", id);

        await using var pgReader = await pgCmd.ExecuteReaderAsync(ctx.RequestAborted);
        if (!await pgReader.ReadAsync(ctx.RequestAborted))
            return Results.NotFound(ErrorResponse.Create(ErrorCodes.IntegrationTenantNotFound, $"Tenant {id} bulunamadı.", ctx.TraceIdentifier));

        var planTier = pgReader.GetString(0);
        var tenantFeatJson = pgReader.IsDBNull(1) ? null : pgReader.GetString(1);
        var tierFeatJson = pgReader.IsDBNull(2) ? null : pgReader.GetString(2);
        var quotasJson = pgReader.IsDBNull(3) ? null : pgReader.GetString(3);
        var tierDisplayName = pgReader.IsDBNull(4) ? planTier : pgReader.GetString(4);
        var messagesSent = pgReader.GetInt32(5);
        var periodMonth = pgReader.GetString(6);

        var effectiveJson = !string.IsNullOrWhiteSpace(tenantFeatJson) && tenantFeatJson != "{}" ? tenantFeatJson : tierFeatJson;
        using var effectiveDoc = System.Text.Json.JsonDocument.Parse(!string.IsNullOrWhiteSpace(effectiveJson) ? effectiveJson : "{}");
        using var quotasDoc = System.Text.Json.JsonDocument.Parse(!string.IsNullOrWhiteSpace(quotasJson) ? quotasJson : "{}");

        invektoData = new
        {
            plan_tier = planTier,
            tier_display_name = tierDisplayName,
            has_override = !string.IsNullOrWhiteSpace(tenantFeatJson) && tenantFeatJson != "{}",
            effective_features = effectiveDoc.RootElement.Clone(),
            quotas = quotasDoc.RootElement.Clone(),
            usage = new { messages_sent = messagesSent, period_month = periodMonth }
        };
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLog.SystemWarn($"License PG fetch failed: tenant={id}, {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendTenantPlanUpdateFailed, message = "Firma plan bilgisi yüklenemedi." }, statusCode: 500);
    }

    // --- INMA: SQL Server readonly (fail-safe: null döner, hata değil) ---
    object? inmaData = null;
    if (!string.IsNullOrEmpty(inmaConnectionString))
    {
        try
        {
            await using var mssqlConn = new Microsoft.Data.SqlClient.SqlConnection(inmaConnectionString);
            await mssqlConn.OpenAsync(ctx.RequestAborted);

            // Companies tablosundan InvektoCompanyCode ile eşleştir
            int? inmaCompanyId = null;
            await using var compCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                SELECT TOP 1 c.ID, c.Name, c.LicenseExpireDate, c.UserLicenseCount, c.InstanceLicenseCount,
                       c.LicenseFeature, c.LicenseRenewalPeriod, c.MessageLimitPerDaily,
                       c.LicenseProgressType, lt.Name AS LicenseTypeName, lt.Price AS LicenseTypePrice,
                       c.DataStatus
                FROM Companies c
                LEFT JOIN LicenseTypes lt ON c.LicenseTypeID = lt.ID
                WHERE c.InvektoCompanyCode = @code AND c.DataStatus != 2", mssqlConn);
            compCmd.Parameters.AddWithValue("@code", id.ToString());

            await using var compReader = await compCmd.ExecuteReaderAsync(ctx.RequestAborted);
            if (await compReader.ReadAsync(ctx.RequestAborted))
            {
                inmaCompanyId = compReader.GetInt32(0);
                var licenseExpireDate = compReader.IsDBNull(2) ? (DateTime?)null : compReader.GetDateTime(2);
                var userCount = compReader.IsDBNull(3) ? (int?)null : compReader.GetInt32(3);
                var instanceCount = compReader.IsDBNull(4) ? (int?)null : compReader.GetInt32(4);
                var licenseFeature = compReader.IsDBNull(5) ? null : compReader.GetString(5);
                var renewalPeriod = compReader.IsDBNull(6) ? (int?)null : compReader.GetInt32(6);
                var msgLimitDaily = compReader.IsDBNull(7) ? (int?)null : compReader.GetInt32(7);
                var licenseProgressType = compReader.IsDBNull(8) ? (byte?)null : compReader.GetByte(8);
                var licenseTypeName = compReader.IsDBNull(9) ? null : compReader.GetString(9);
                var licenseTypePrice = compReader.IsDBNull(10) ? (decimal?)null : compReader.GetDecimal(10);

                // LicenseHistories — son 5 kayıt
                var histories = new List<object>();
                await compReader.CloseAsync();

                if (inmaCompanyId.HasValue)
                {
                    await using var histCmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                        SELECT TOP 5 LicenseStart, LicenseEnd, IsPaymentReceived, LicenseChangeType
                        FROM LicenseHistories
                        WHERE CompanyID = @cid
                        ORDER BY CreateDate DESC", mssqlConn);
                    histCmd.Parameters.AddWithValue("@cid", inmaCompanyId.Value);

                    await using var histReader = await histCmd.ExecuteReaderAsync(ctx.RequestAborted);
                    while (await histReader.ReadAsync(ctx.RequestAborted))
                    {
                        histories.Add(new
                        {
                            start = histReader.IsDBNull(0) ? null : histReader.GetDateTime(0).ToString("yyyy-MM-dd"),
                            end = histReader.IsDBNull(1) ? null : histReader.GetDateTime(1).ToString("yyyy-MM-dd"),
                            is_paid = histReader.GetBoolean(2),
                            change_type = histReader.GetInt32(3)
                        });
                    }
                }

                // Lisans durumu: geçmiş mi?
                var isExpired = licenseExpireDate.HasValue && licenseExpireDate.Value < DateTime.UtcNow;
                var daysUntilExpiry = licenseExpireDate.HasValue
                    ? (int)(licenseExpireDate.Value - DateTime.UtcNow).TotalDays
                    : (int?)null;

                inmaData = new
                {
                    company_id = inmaCompanyId,
                    license_type = licenseTypeName,
                    license_type_price = licenseTypePrice,
                    license_expire_date = licenseExpireDate?.ToString("yyyy-MM-dd"),
                    is_expired = isExpired,
                    days_until_expiry = daysUntilExpiry,
                    user_license_count = userCount,
                    instance_license_count = instanceCount,
                    license_feature = licenseFeature,
                    license_renewal_period = renewalPeriod,
                    message_limit_daily = msgLimitDaily,
                    license_progress_type = licenseProgressType,
                    histories
                };
            }
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            jsonLog.SystemWarn($"INMA license fetch failed (non-critical): tenant={id}, {ex.Message}");
            // fail-safe: inmaData kalır null
        }
        catch (InvalidOperationException ex)
        {
            jsonLog.SystemWarn($"INMA license connection failed (non-critical): tenant={id}, {ex.Message}");
            // fail-safe: inmaData kalır null
        }
    }

    return Results.Ok(new
    {
        tenant_id = id,
        invekto = invektoData,
        inma = inmaData
    });
});

// ============================================
// INMA SSO AUTH ENDPOINTS
// ============================================

// Akis 1: inma JWT -> inse JWT exchange (URL token flow)
// inma'dan gelen ?accesstoken= parametresi bu endpoint'e gonderilir.
// Validation: InmaTokenIntrospector welcome-endpoint check (signature verify YASAK — see class docs).
app.MapPost("/api/v1/inma/auth/exchange", async (HttpContext ctx, IHttpClientFactory httpClientFactory, JsonLinesLogger jsonLogger, IFeatureFlagService featureFlagService, TenantRegistryRepository tenantRepo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (jwtGenerator == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "JWT generator not configured", requestId),
            statusCode: 503);

    var introspectorEx = ctx.RequestServices.GetService<InmaTokenIntrospector>();
    if (introspectorEx == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralUnknown, "INMA introspection not configured", requestId),
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

        var introspectExResult = await introspectorEx.ValidateAsync(inmaToken, ctx.RequestAborted);
        var inmaCtx = introspectExResult.Context;
        if (inmaCtx == null)
        {
            jsonLogger.StepWarn($"inma token exchange failed: [{introspectExResult.ErrorCode}] {introspectExResult.Detail}", requestId);
            return MapInmaIntrospectError(introspectExResult.ErrorCode, introspectExResult.Detail, requestId);
        }

        // CompanyCode (opaque string) → INSE int tenant_id. Shared helper maps repository
        // exceptions to the standard INV-AUTH-009 envelope (503 for NpgsqlException,
        // 500 for Argument/InvalidOperation — see ResolveInmaTenantAsync).
        var (tenantId, resolveFailure) = await ResolveInmaTenantAsync(ctx, tenantRepo, inmaCtx, requestId);
        if (resolveFailure != null) return resolveFailure;

        var tokenExpiry = TimeSpan.FromHours(8);
        var inseToken = jwtGenerator.GenerateToken(
            tenantId, inmaCtx.Role, "inma_exchange", tokenExpiry, inmaCtx.UserId.ToString());

        // UP0.6: server-side feature flag cache (5dk TTL). Subsequent requests check flags without re-decoding the inma JWT.
        featureFlagService.SetFeatures(tenantId, inmaCtx.UserId, inmaCtx.InseFeatures);

        jsonLogger.StepInfo($"inma token exchange success: tenant={tenantId} company_code={inmaCtx.CompanyCode} user={inmaCtx.UserId}", requestId);
        return Results.Ok(new
        {
            token = inseToken,
            tenant_id = tenantId,
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
app.MapPost("/api/v1/inma/auth/login", async (HttpContext ctx, IHttpClientFactory httpClientFactory, JsonLinesLogger jsonLogger, IFeatureFlagService featureFlagService, TenantRegistryRepository tenantRepo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var introspectorLogin = ctx.RequestServices.GetService<InmaTokenIntrospector>();
    if (introspectorLogin == null || jwtGenerator == null)
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

        var introspectLoginResult = await introspectorLogin.ValidateAsync(inmaToken, ctx.RequestAborted);
        var inmaCtx = introspectLoginResult.Context;
        if (inmaCtx == null)
        {
            jsonLogger.StepWarn($"inma login token validation failed: [{introspectLoginResult.ErrorCode}] {introspectLoginResult.Detail}", requestId);
            return MapInmaIntrospectError(introspectLoginResult.ErrorCode, introspectLoginResult.Detail, requestId);
        }

        // CompanyCode (opaque string) → INSE int tenant_id via shared helper.
        var (tenantId, resolveFailure) = await ResolveInmaTenantAsync(ctx, tenantRepo, inmaCtx, requestId);
        if (resolveFailure != null) return resolveFailure;

        // UP0.6: server-side feature flag cache (5dk TTL) populated from the inma token claim.
        featureFlagService.SetFeatures(tenantId, inmaCtx.UserId, inmaCtx.InseFeatures);

        jsonLogger.StepInfo($"inma login success: tenant={tenantId} company_code={inmaCtx.CompanyCode} user={inmaCtx.UserId}", requestId);
        return Results.Ok(new
        {
            token = inmaToken,
            refresh_token = inmaRefreshToken ?? string.Empty,
            tenant_id = tenantId,
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

    // Optional ?tenant=<id> override — smoke/ops amaçlı tenant-spesifik JWT üretmek için.
    // Yalnizca inmaAuthMockEnabled=true iken erisilebilir; prod'da kapali olmali.
    var tenantId = 0;
    var tenantQuery = ctx.Request.Query["tenant"].ToString();
    if (!string.IsNullOrWhiteSpace(tenantQuery))
    {
        if (!int.TryParse(tenantQuery, out tenantId) || tenantId < 0)
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.OpsQuickLoginInvalidTenant, "tenant query param must be non-negative integer (e.g., ?tenant=5050)", requestId),
                statusCode: 400);
    }

    var tokenExpiry = TimeSpan.FromHours(8);
    var inseToken = jwtGenerator.GenerateToken(tenantId, "admin", "ops_quicklogin", tokenExpiry, "0");

    jsonLogger.StepInfo($"ops quicklogin: admin JWT issued (tenant={tenantId})", requestId);
    return Results.Ok(new
    {
        token = inseToken,
        tenant_id = tenantId,
        user_id = 0,
        role = "admin",
        full_name = tenantId == 0 ? "Super Admin" : $"Admin (tenant {tenantId})",
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
// ============================================
// INMA NAV METADATA (tenant-facing)
// ============================================
// Serves localized (TR) navigation metadata so the INMA/WapCRM parent shell
// can render an outer sidebar that mirrors the Invekto feature surface.
// Tenant-only: ops items are intentionally excluded at the source. Feature
// licensing filtering is deferred — this phase returns the full tenant set.
// Icon strings are lucide-react kebab-case identifiers; consumer maps them.
app.MapGet("/api/v1/inma/nav", async (HttpContext ctx) =>
{
    // Auth gate: accepts both INSE JWT (Dashboard React) and INMA JWT (Angular shell via
    // welcome introspection). License/feature filtering deferred — full tenant set returned.
    var (tenant, extractFailure) = await ExtractTenantFromBearer(ctx);
    if (extractFailure != null) return extractFailure;
    if (tenant == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Bearer token required", "-"),
            statusCode: 401);

    // NOTE: Bu liste Dashboard'un tenant-facing menüsünü (Layout.tsx ALL_NAV_ITEMS,
    // session + feature filtreli set) birebir aynalar. Layout.tsx'te tenant menüsü
    // değişirse buradaki liste de güncellenmeli — iki kaynak elle senkron tutulur.
    var navResponse = new InmaNavResponse
    {
        Sections =
        [
            new InmaNavSection
            {
                Id = "workspace",
                Label = "Çalışma Alanı",
                Items =
                [
                    new InmaNavItem { Id = "home",            Label = "Ana Sayfa",       Path = "/",                Icon = "layout-dashboard" },
                    new InmaNavItem { Id = "flow-builder",    Label = "Flow Builder",    Path = "/flow-builder",    Icon = "git-branch" },
                    new InmaNavItem { Id = "flow-templates",  Label = "Şablon Galerisi", Path = "/flow-templates",  Icon = "layout-template" },
                    new InmaNavItem { Id = "knowledge",       Label = "Bilgi Bankası",   Path = "/knowledge",       Icon = "book-open" },
                    new InmaNavItem { Id = "data-management", Label = "Veri Yönetimi",   Path = "/data-management", Icon = "database" },
                    new InmaNavItem { Id = "projects",        Label = "Projeler",        Path = "/projects",        Icon = "briefcase" },
                ]
            },
            new InmaNavSection
            {
                Id = "analytics",
                Label = "Analiz",
                Items =
                [
                    new InmaNavItem { Id = "flow-monitor",    Label = "Flow İzleme",     Path = "/flow-monitor",    Icon = "activity" },
                ]
            },
            new InmaNavSection
            {
                Id = "system",
                Label = "Sistem",
                Items =
                [
                    new InmaNavItem { Id = "onboarding",      Label = "Kurulum Sihirbazı", Path = "/onboarding",    Icon = "rocket" },
                    new InmaNavItem { Id = "settings",        Label = "Ayarlar",           Path = "/settings",      Icon = "settings" },
                ]
            }
        ]
    };

    return Results.Ok(navResponse);
}).RequireCors("InmaNavCors");

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
// Auth: localhost or internal API key (service-to-service only).
app.MapPost("/api/v1/callback/wapcrm", async (HttpContext ctx, JsonLinesLogger jsonLog, IHttpClientFactory httpClientFactory) =>
{
    // Auth gate: localhost or internal API key
    var wapRemoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
    var wapApiKey = ctx.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
    var wapIsLocal = wapRemoteIp is "127.0.0.1" or "::1";
    var wapHasKey = !string.IsNullOrEmpty(internalApiKey) && wapApiKey == internalApiKey;
    if (!wapIsLocal && !wapHasKey)
    {
        jsonLog.SystemWarn($"WapCRM bridge: rejected unauthorized request (ip={wapRemoteIp})");
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Unauthorized", "-"),
            statusCode: 401);
    }

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

    // FEAT-J2: forward MessageCategory (marketing|transactional) when upstream set it.
    // Null = legacy path, INMA skips opt-out check (backward-compat).
    var messageCategory = callback.Data?.MessageCategory;
    if (!string.IsNullOrEmpty(messageCategory))
    {
        wapPayload["messageCategory"] = messageCategory;
    }

    // FEAT-DMP: activate INMA DynamicMessage when upstream set DynamicFields.
    // Null/empty = legacy INSE substitution already applied in caller; raw text ships as-is.
    // Non-empty = MessageText contains {{placeholders}} left raw for INMA to resolve from
    // the tenant Customer DB (wapcrm-marketing-api.md §2). Allowlist validation happened
    // in DynamicMessageValidator before dispatch (INV-OB-033 is an upstream reject).
    var dynamicFields = callback.Data?.DynamicFields;
    if (dynamicFields is { Length: > 0 })
    {
        wapPayload["dynamicMessage"] = true;
        wapPayload["dynamicMessageFields"] = dynamicFields;
    }

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

        // FEAT-J2 (AC9): parse WapCRM body to detect application-level blocks that
        // arrive as HTTP 200 + Status:false + StatusCode:'906'/'907'. Previously
        // treated as 'sent' — compliance/audit bug. Parse fail is graceful:
        // falls back to HTTP-status-based outcome (Q4 decision).
        // FEAT-DMP extends the same parse path to cover INMA DynamicMessage error codes
        // 900/901/902/903/905 (wapcrm-marketing-api.md §6). Same HTTP-200+Status:false
        // envelope semantics; we map each code to its INV-OB-033..036 log + mark
        // outbound='failed' so the Dashboard badge surfaces the cause.
        var wapStatusCode = ExtractWapStatusCode(responseBody);
        var isMarketingBlock = wapStatusCode is "906" or "907";
        var isDynamicFailure = wapStatusCode is "900" or "901" or "902" or "903" or "905";
        var outboundStatus = response.IsSuccessStatusCode
            ? (isMarketingBlock ? "blocked" : (isDynamicFailure ? "failed" : "sent"))
            : "failed";

        if (isMarketingBlock)
        {
            var invCode = wapStatusCode == "906"
                ? ErrorCodes.ChatoperationBlockedMarketing
                : ErrorCodes.ChatoperationBlockedContact;
            jsonLog.SystemWarn(
                $"[{invCode}] WapCRM marketing block: tenant={callback.TenantId}, phone={phone}, wap={wapStatusCode}, outbound_msg_id={callback.Data?.OutboundMessageId}");
        }
        else if (isDynamicFailure)
        {
            var invCode = wapStatusCode switch
            {
                "901" => ErrorCodes.DynamicFieldUnsupported,
                "903" => ErrorCodes.DynamicCustomerNotFound,
                "905" => ErrorCodes.DynamicFieldValueNull,
                _ => ErrorCodes.DynamicFieldValidationFailed, // 900 + 902
            };
            jsonLog.SystemWarn(
                $"[{invCode}] INMA DynamicMessage rejection: tenant={callback.TenantId}, phone={phone}, wap={wapStatusCode}, fields=[{string.Join(",", dynamicFields ?? Array.Empty<string>())}], outbound_msg_id={callback.Data?.OutboundMessageId}");
        }

        jsonLog.StepInfo(
            $"WapCRM bridge: tenant={callback.TenantId}, phone={phone}, instanceId={instanceIdInt}, " +
            $"action={callback.Action}, status={response.StatusCode}, wap={wapStatusCode ?? "-"}, outbound={outboundStatus}, elapsed={sw.ElapsedMilliseconds}ms",
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
            status = outboundStatus,
            wapcrm_status = (int)response.StatusCode,
            wapcrm_app_code = wapStatusCode,
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

// FEAT-J2 helper: extract INMA application-level StatusCode from the WapCRM
// response body. Returns null when the body isn't JSON or doesn't carry the
// envelope — callers then fall back to HTTP-status-based outcomes (Q4).
static string? ExtractWapStatusCode(string? body)
{
    if (string.IsNullOrWhiteSpace(body)) return null;
    try
    {
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
        var root = doc.RootElement;
        var statusFalse = root.TryGetProperty("Status", out var s) && s.ValueKind == JsonValueKind.False;
        if (!statusFalse) return null;
        if (root.TryGetProperty("StatusCode", out var sc) && sc.ValueKind == JsonValueKind.String)
            return sc.GetString();
        return null;
    }
    catch (JsonException)
    {
        return null;
    }
}

// ============================================
// FEAT-DMP: INMA /api/dynamicfields PROXY (tenant-scoped, cached)
// ============================================
// Auth: **both routes below are protected by the global JWT middleware registered in
// `app.UseAuthentication() + app.UseAuthorization()` early in this file (search the
// string "UseAuthentication" above the route table). The middleware rejects missing/
// invalid tokens with 401 INV-AUTH-003 before the handler runs, populates
// `ctx.Items["TenantId"]` from the signed claim, and that is the ONLY source of tenant
// scope — no query/header/body override is accepted. Identical gate as FEAT-J2
// /api/v1/optout and FEAT-LIW /api/v1/tenant/landing/*.**
//
// Consumed by Dashboard PlaceholderPicker (FlowBuilder / TemplateCreate). Cache is
// shared across all editors (1h TTL per tenant) with single-flight stampede guard.
//
// Failure semantics (iter 1 fix for CQ1/CQ2/CQ12):
//   - no WapCRM secret → 422 + ErrorResponse(INV-AUTH-003-equivalent surface via INV-OB-037
//     variant "not configured"); UI distinguishes "not configured" from "fetch fail".
//   - InmaDynamicFieldsFetchException from the client → 503 + ErrorResponse(DynamicFieldsFetchFailed).
//   - ok → 200 + {data: [{fieldKey, fieldName}]} — empty list is a valid tenant state.
app.MapGet("/api/v1/dynamic-fields", async (
    HttpContext ctx,
    JsonLinesLogger jsonLog,
    [FromServices] Invekto.Shared.Services.InmaDynamicFieldsCache cache,
    [FromServices] TenantRegistryRepository tenantRepo) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (ctx.Items["TenantContext"] is not TenantContext dmpGetTenantCtx)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);
    var tenantId = dmpGetTenantCtx.TenantId;

    var wapcrm = await tenantRepo.GetWapCrmSettingsAsync(tenantId, ctx.RequestAborted);
    if (wapcrm == null || string.IsNullOrWhiteSpace(wapcrm.SecretKey))
    {
        jsonLog.StepWarn($"[{ErrorCodes.DynamicFieldsNotConfigured}] /api/v1/dynamic-fields: no WapCRM secret for tenant {tenantId}", requestId);
        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.DynamicFieldsNotConfigured,
                "Tenant INMA entegrasyonu yapilandirilmamis. Yonetici tenant ayarlarindan eklemelidir.",
                requestId),
            statusCode: 422);
    }

    try
    {
        var fields = await cache.GetOrFetchAsync(tenantId, wapcrm.SecretKey, ctx.RequestAborted);
        return Results.Json(new { data = fields });
    }
    catch (Invekto.Shared.Contracts.Inma.InmaDynamicFieldsFetchException ex)
    {
        jsonLog.StepWarn(
            $"[{ErrorCodes.DynamicFieldsFetchFailed}] /api/v1/dynamic-fields upstream fail " +
            $"(tenant={tenantId}, inmaCode={ex.InmaStatusCode ?? "-"}, http={ex.HttpStatusCode?.ToString() ?? "-"}): {ex.Message}",
            requestId);
        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.DynamicFieldsFetchFailed,
                "INMA dinamik alan listesine ulasilamadi, kisa sure sonra tekrar deneyin.",
                requestId),
            statusCode: 503);
    }
});

// Cache drop endpoint. Tenant-scoped by design — each tenant owns its own cache slot and
// there is no super-admin role check on this handler; a rogue actor invalidating their own
// tenant only causes a single extra INMA fetch on the next GET (bounded by the 5s client
// timeout + HttpClient pool). Abuse surface therefore stays below the upstream proxy's
// rate-limit floor; the handler only logs the invocation for flap-tracking.
app.MapPost("/api/v1/dynamic-fields/cache-invalidate", (
    HttpContext ctx,
    JsonLinesLogger jsonLog,
    Invekto.Shared.Services.InmaDynamicFieldsCache cache) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (ctx.Items["TenantContext"] is not TenantContext dmpInvTenantCtx)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);
    var tenantId = dmpInvTenantCtx.TenantId;

    cache.Invalidate(tenantId);
    jsonLog.StepInfo($"/api/v1/dynamic-fields cache invalidated (tenant={tenantId})", requestId);
    return Results.Ok(new { invalidated = true });
});

// ============================================
// FEAT-TFM MVP: tenant-scoped field-mapping CRUD (semantic→INMA cf1..cf10).
// ============================================
// Routes registered as an extension to keep Program.cs scannable. Implementation +
// validation + cache-invalidation lives in Endpoints/TenantFieldMappingEndpoints.cs.
app.MapTenantFieldMappingEndpoints();

// FEAT-EFS Drip Sequence: SPA-facing proxy endpoints in front of Marketing's
// /api/v1/followup/* routes. Implementation in
// Endpoints/TenantFollowupSequenceEndpoints.cs.
app.MapTenantFollowupSequenceEndpoints();

// FEAT-MCC Multi-City Campaign: tenant-scoped /api/v1/tenant-settings/campaign-config
// CRUD. JWT covered by the existing /api/v1/tenant-settings/ prefix in
// jwtRequiredPrefixes; handler-side TenantContext guard inside the endpoint.
app.MapTenantCampaignConfigEndpoints();

// FEAT-CLINIC-METADATA: tenant-scoped /api/v1/tenant-settings/clinic-metadata
// CRUD (clinic_contact + team_members JSONB pair). JWT covered by the existing
// /api/v1/tenant-settings/ prefix; handler-side TenantContext + 3-layer
// cross-tenant guard in Endpoints/ClinicMetadataEndpoints.cs.
app.MapClinicMetadataEndpoints();

// Paket B-META: Meta Leadgen webhook native integration — public inbound handshake +
// signed POST (under /api/inbound/meta/leadgen/, NOT JWT-covered), admin settings CRUD
// under /api/v1/tenant-settings/meta-leadgen/* (JWT via the /api/v1/tenant-settings/
// prefix), and the internal process-lead hop under /api/internal/meta-leadgen/*
// (JWT + IntakeInternalAuth shared-secret dual auth).
Invekto.Backend.Services.MetaLeadgen.MetaLeadgenEndpoints.MapMetaLeadgenEndpoints(app);

// Automation → Backend lifecycle notifications receiver
// (/api/internal/lifecycle/welcome-sent). FEAT-INMA-PIPELINE-V2 C1 (2026-05-13):
// Zoho Blueprint dispatch removed; handler now accepts + logs the welcome-sent
// notification without downstream sync side effects. INMA-authoritative customer_status
// flow (V2 C2-C4) will replace this hop when INMA contract ships.
Invekto.Backend.Services.Lifecycle.LifecycleInternalEndpoints.MapLifecycleInternalEndpoints(app);

// ============================================
// FEAT-J2: Manual opt-out/opt-in (JWT-scoped dashboard admin action)
// ============================================
// Backend resolves last-known WapCRM instance via MessageLogRepository, then
// forwards to Outbound /api/v1/internal/optout over the internal shared secret.
// No instance found (lead never messaged) → 400 + INV-OB-029.
app.MapPost("/api/v1/optout", async (HttpContext ctx, JsonLinesLogger jsonLog, IHttpClientFactory httpClientFactory) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (ctx.Items["TenantContext"] is not TenantContext optoutTenantCtx)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId), statusCode: 401);
    }
    var tenantId = optoutTenantCtx.TenantId;

    OptOutRequest? dto;
    try { dto = await ctx.Request.ReadFromJsonAsync<OptOutRequest>(); }
    catch (JsonException ex)
    {
        jsonLog.StepWarn($"Manual opt-out: invalid JSON body: {ex.Message}", requestId);
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.GeneralValidation, "Invalid JSON body", requestId),
            statusCode: 400);
    }
    if (dto == null || string.IsNullOrWhiteSpace(dto.Phone))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.OutboundInvalidBroadcastPayload, "phone is required", requestId),
            statusCode: 400);
    }

    var msgLogRepo = ctx.RequestServices.GetService<MessageLogRepository>();
    if (msgLogRepo == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.BackendMicroserviceUnavailable, "PostgreSQL not configured", requestId),
            statusCode: 503);
    }

    var resolvedInstance = await msgLogRepo.GetLastInstanceIdAsync(tenantId, dto.Phone);
    if (string.IsNullOrWhiteSpace(resolvedInstance) || !int.TryParse(resolvedInstance, out var instanceIdInt))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.NoInstanceForOptOut,
                "Bu numara için geçmiş konuşma yok — önce etkileşim gerekli.", requestId),
            statusCode: 400);
    }

    var payload = new
    {
        tenant_id = tenantId,
        phone = dto.Phone,
        instance_id = instanceIdInt,
        event_type = string.IsNullOrEmpty(dto.EventType) ? "opt_out" : dto.EventType,
        reason = dto.Reason,
    };

    var client = httpClientFactory.CreateClient();
    client.BaseAddress = new Uri(outboundUrl);
    client.DefaultRequestHeaders.Add("X-Internal-Service-Token", internalSharedSecret);
    var upstream = await client.PostAsJsonAsync("/api/v1/internal/optout", payload, ctx.RequestAborted);
    var upstreamBody = await upstream.Content.ReadAsStringAsync(ctx.RequestAborted);

    jsonLog.StepInfo(
        $"Manual opt-out forwarded: tenant={tenantId}, phone={dto.Phone}, event={payload.event_type}, instance={instanceIdInt}, upstream={(int)upstream.StatusCode}",
        requestId);

    return Results.Content(upstreamBody, "application/json", statusCode: (int)upstream.StatusCode);
});

// ============================================
// FEAT-J2: SuperAdmin outbox retry-skipped proxy (AC11)
// ============================================
// Flips 'skipped_noop' rows back to 'pending' after Mode is switched from NoOp
// to Http. Forwards to Outbound internal endpoint with the shared secret.
app.MapPost("/api/ops/outbox/retry-skipped", async (HttpContext ctx, JsonLinesLogger jsonLog, IHttpClientFactory httpClientFactory) =>
{
    if (!ValidateOpsAuth(ctx)) return OpsUnauthorized(ctx);
    var identity = ResolveOpsIdentity(ctx);

    string body;
    using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8))
        body = await reader.ReadToEndAsync(ctx.RequestAborted).ConfigureAwait(false);

    var client = httpClientFactory.CreateClient();
    client.BaseAddress = new Uri(outboundUrl);
    client.DefaultRequestHeaders.Add("X-Internal-Service-Token", internalSharedSecret);
    var content = new StringContent(string.IsNullOrWhiteSpace(body) ? "{}" : body, Encoding.UTF8, "application/json");
    var upstream = await client.PostAsync("/api/v1/internal/outbox/retry-skipped", content, ctx.RequestAborted);
    var upstreamBody = await upstream.Content.ReadAsStringAsync(ctx.RequestAborted);

    jsonLog.SystemInfo($"[OPS-OUTBOX] drain super_admin={identity} upstream_status={(int)upstream.StatusCode}");
    return Results.Content(upstreamBody, "application/json", statusCode: (int)upstream.StatusCode);
});


// ============================================
// PAYMENT — QNB Finansbank 3DPay
// Ref: arch/docs/qnb-vpos-integration.md
// ============================================

// POST /api/v1/payment/initiate — JWT auth, pending kayıt oluştur, 3D HTML form döner
app.MapPost("/api/v1/payment/initiate", async (HttpContext ctx, QnbVPosService vpos, JsonLinesLogger jsonLog) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (ctx.Items["TenantContext"] is not TenantContext paymentTenantCtx)
        return Results.Json(
            ErrorResponse.Create(
                ErrorCodes.AuthUnauthorized,
                "Tenant context yok. Authorization: Bearer <jwt> header'i ile giris yapip tekrar deneyin.",
                requestId),
            statusCode: 401);
    var tenantId = paymentTenantCtx.TenantId.ToString();

    PaymentInitRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<PaymentInitRequest>(); }
    catch (Exception ex)
    {
        jsonLog.SystemWarn($"Payment initiate body parse failed: {ex.Message}");
        return Results.Json(new { error = ErrorCodes.GeneralUnknown, message = "Geçersiz istek gövdesi." }, statusCode: 400);
    }

    if (request == null || request.Amount <= 0)
        return Results.Json(new { error = ErrorCodes.BackendPaymentAmountInvalid, message = "Geçersiz ödeme tutarı." }, statusCode: 400);

    if (string.IsNullOrWhiteSpace(request.CardNumber) || string.IsNullOrWhiteSpace(request.CardExpireMonth) ||
        string.IsNullOrWhiteSpace(request.CardExpireYear) || string.IsNullOrWhiteSpace(request.Cvv))
        return Results.Json(new { error = ErrorCodes.GeneralValidation, message = "Kart bilgileri eksik." }, statusCode: 400);

    var scheme = ctx.Request.Scheme;
    var host = ctx.Request.Host.Value;
    var callbackUrl = $"{scheme}://{host}/api/v1/payment/callback";

    try
    {
        var result = vpos.InitiatePayment(request, callbackUrl, tenantId);

        if (!string.IsNullOrEmpty(pgConnectionString))
        {
            await using var conn = new NpgsqlConnection(pgConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO tenant_payments (order_id, tenant_id, amount, status) VALUES (@oid, @tid, @amt, 'pending')";
            cmd.Parameters.AddWithValue("oid", result.OrderId ?? throw new InvalidOperationException("OrderId null"));
            cmd.Parameters.AddWithValue("tid", int.Parse(tenantId));
            cmd.Parameters.AddWithValue("amt", request.Amount);
            await cmd.ExecuteNonQueryAsync();
        }

        return Results.Ok(new { order_id = result.OrderId, redirect_html = result.RedirectHtml });
    }
    catch (Exception ex)
    {
        jsonLog.SystemWarn($"Payment initiate failed ({ErrorCodes.BackendPaymentInitFailed}): tenant={tenantId}, {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPaymentInitFailed, message = "Ödeme başlatılamadı." }, statusCode: 500);
    }
});

// POST /api/v1/payment/callback — banka callback (public, form-data), kayıt güncelle, redirect
app.MapPost("/api/v1/payment/callback", async (HttpContext ctx, QnbVPosService vpos, JsonLinesLogger jsonLog) =>
{
    IFormCollection form;
    try { form = await ctx.Request.ReadFormAsync(); }
    catch (Exception ex)
    {
        jsonLog.SystemWarn($"Payment callback ({ErrorCodes.BackendPaymentCallbackInvalid}): form parse failed — {ex.Message}");
        return Results.Redirect("/app/#/licenses?payment_result=failed");
    }

    var formData = form.ToDictionary(k => k.Key, v => v.Value.ToString());
    PaymentCallbackData callback;
    try { callback = vpos.ParseCallback(formData); }
    catch (Exception ex)
    {
        jsonLog.SystemWarn($"Payment callback parse failed ({ErrorCodes.BackendPaymentCallbackInvalid}): {ex.Message}");
        return Results.Redirect("/app/#/licenses?payment_result=failed");
    }

    if (!string.IsNullOrEmpty(pgConnectionString) && !string.IsNullOrEmpty(callback.OrderId))
    {
        try
        {
            await using var conn = new NpgsqlConnection(pgConnectionString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE tenant_payments
                SET status           = @status,
                    three_d_status   = @tds,
                    proc_return_code = @prc,
                    auth_code        = @ac,
                    host_ref_num     = @hrn,
                    trans_id         = @tid,
                    err_msg          = @err,
                    updated_at       = NOW()
                -- order_id is globally UNIQUE; bank callback has no JWT tenant context
                WHERE order_id = @oid AND status = 'pending'";
            cmd.Parameters.AddWithValue("status", callback.IsSuccessful ? "success" : "failed");
            cmd.Parameters.AddWithValue("tds",    (object?)callback.ThreeDStatus   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("prc",    (object?)callback.ProcReturnCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ac",     (object?)callback.AuthCode       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("hrn",    (object?)callback.HostRefNum     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tid",    (object?)callback.TransId        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("err",    (object?)callback.ErrMsg         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("oid",    callback.OrderId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            jsonLog.SystemWarn($"Payment callback DB update failed ({ErrorCodes.BackendPaymentCallbackInvalid}): {ex.Message}");
            // DB hatası: yine de kullanıcıyı yönlendir
        }
    }

    if (callback.IsSuccessful)
        return Results.Redirect($"/app/#/licenses?payment_result=success&order_id={Uri.EscapeDataString(callback.OrderId ?? "")}");

    var errMsg = Uri.EscapeDataString(callback.ErrMsg ?? callback.ProcReturnCode ?? "failed");
    return Results.Redirect($"/app/#/licenses?payment_result=failed&error={errMsg}");
});

// GET /api/v1/payment/history — ops auth, tenant_id filtresi (opsiyonel)
app.MapGet("/api/v1/payment/history", async (HttpContext ctx, JsonLinesLogger jsonLog) =>
{
    if (!ValidateOpsAuth(ctx))
        return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = "Yetkisiz." }, statusCode: 401);

    if (string.IsNullOrEmpty(pgConnectionString))
        return Results.Json(new { error = ErrorCodes.BackendPaymentHistoryFailed, message = "PostgreSQL konfigüre edilmedi." }, statusCode: 503);

    var tenantIdFilter = ctx.Request.Query.TryGetValue("tenant_id", out var tidVal) && int.TryParse(tidVal, out var tid) ? (int?)tid : null;

    try
    {
        await using var conn = new NpgsqlConnection(pgConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        if (tenantIdFilter.HasValue)
        {
            cmd.CommandText = "SELECT id, order_id, tenant_id, amount, status, three_d_status, proc_return_code, auth_code, host_ref_num, trans_id, err_msg, created_at, updated_at FROM tenant_payments WHERE tenant_id = @tid ORDER BY created_at DESC LIMIT 100";
            cmd.Parameters.AddWithValue("tid", tenantIdFilter.Value);
        }
        else
        {
            cmd.CommandText = "SELECT id, order_id, tenant_id, amount, status, three_d_status, proc_return_code, auth_code, host_ref_num, trans_id, err_msg, created_at, updated_at FROM tenant_payments ORDER BY created_at DESC LIMIT 200";
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        var payments = new List<object>();
        while (await reader.ReadAsync())
        {
            payments.Add(new
            {
                id             = reader.GetInt64(0),
                order_id       = reader.GetString(1),
                tenant_id      = reader.GetInt32(2),
                amount         = reader.GetDecimal(3),
                status         = reader.GetString(4),
                three_d_status = reader.IsDBNull(5) ? null : reader.GetString(5),
                proc_return    = reader.IsDBNull(6) ? null : reader.GetString(6),
                auth_code      = reader.IsDBNull(7) ? null : reader.GetString(7),
                host_ref_num   = reader.IsDBNull(8) ? null : reader.GetString(8),
                trans_id       = reader.IsDBNull(9) ? null : reader.GetString(9),
                err_msg        = reader.IsDBNull(10) ? null : reader.GetString(10),
                created_at     = reader.GetFieldValue<DateTimeOffset>(11),
                updated_at     = reader.GetFieldValue<DateTimeOffset>(12),
            });
        }

        return Results.Ok(new { payments, count = payments.Count });
    }
    catch (Exception ex)
    {
        jsonLog.SystemWarn($"Payment history failed ({ErrorCodes.BackendPaymentHistoryFailed}): {ex.Message}");
        return Results.Json(new { error = ErrorCodes.BackendPaymentHistoryFailed, message = "Ödeme geçmişi yüklenemedi." }, statusCode: 500);
    }
});

// ============================================
// TRANSLATION — Chat Message Translation API
// Ref: arch/db/translations.sql
// ============================================

// Resolve tenant_id from X-Tenant-Id header (numeric or Inma company code string)
// Flow: numeric → direct | PostgreSQL inma_code lookup | auto-provision
async Task<(int tenantId, string? error)> ResolveTranslateTenantAsync(string rawTenantId, JsonLinesLogger log, CancellationToken ct)
{
    // 1. Numeric → direct
    if (int.TryParse(rawTenantId, out var numericId) && numericId > 0)
        return (numericId, null);

    var code = rawTenantId.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(code))
        return (0, "X-Tenant-Id boş olamaz.");

    if (pgFactory == null)
        return (0, "PostgreSQL bağlantısı yok.");

    // 2. PostgreSQL: tenant_registry.inma_code lookup (cached mapping)
    try
    {
        await using var pg = await pgFactory.OpenConnectionAsync(ct);
        await using var cmd = pg.CreateCommand();
        cmd.CommandText = "SELECT tenant_id FROM tenant_registry WHERE inma_code = @code AND is_active = true";
        cmd.Parameters.AddWithValue("code", code);
        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is int tid)
            return (tid, null);
    }
    catch (Exception ex)
    {
        log.StepError($"[translate-resolve] PG inma_code lookup error: {ex.Message}", "-");
    }

    // 3. Auto-provision: yeni tenant_id üret, tenant_registry'ye ekle
    var newTenantId = new Random().Next(10000000, 99999999);
    try
    {
        await using var pg2 = await pgFactory.OpenConnectionAsync(ct);
        await using var insertCmd = pg2.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO tenant_registry (tenant_id, tenant_name, inma_code, is_active, sector, plan_tier)
            VALUES (@tid, @name, @code, true, 'genel', 'baslangic')
            ON CONFLICT (tenant_id) DO UPDATE SET inma_code = @code
            RETURNING tenant_id";
        insertCmd.Parameters.AddWithValue("tid", newTenantId);
        insertCmd.Parameters.AddWithValue("name", code);
        insertCmd.Parameters.AddWithValue("code", code);
        var created = await insertCmd.ExecuteScalarAsync(ct);
        if (created is int createdId)
        {
            log.SystemInfo($"[translate-resolve] Auto-provisioned tenant: code={code}, tenant_id={createdId}");
            return (createdId, null);
        }
    }
    catch (Exception ex)
    {
        log.StepError($"[translate-resolve] Auto-provision error: {ex.Message}", "-");
        return (0, "Tenant oluşturma başarısız.");
    }

    return (0, "Tenant oluşturulamadı.");
}

// POST /api/v1/translate — Tek mesaj çeviri (JWT auth VEYA X-Internal-Api-Key + X-Tenant-Id)
app.MapPost("/api/v1/translate", async (HttpContext ctx, TranslationService translationService, JsonLinesLogger jsonLog) =>
{
    // Auth: JWT TenantContext VEYA InternalApiKey + X-Tenant-Id header
    int tenantId;
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext != null)
    {
        tenantId = tenantContext.TenantId;
    }
    else
    {
        var apiKeyHeader = ctx.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(internalApiKey) || apiKeyHeader != internalApiKey)
            return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = "Yetkisiz. JWT veya X-Internal-Api-Key gerekli." }, statusCode: 401);

        var rawTenantId = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "";
        var (resolved, resolveError) = await ResolveTranslateTenantAsync(rawTenantId, jsonLog, ctx.RequestAborted);
        if (resolved <= 0)
            return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = resolveError ?? "X-Tenant-Id çözümlenemedi." }, statusCode: 401);
        tenantId = resolved;
    }

    TranslateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<TranslateRequest>(); }
    catch (JsonException) { return Results.Json(new { error = ErrorCodes.GeneralValidation, message = "Geçersiz istek gövdesi." }, statusCode: 400); }

    if (request == null || string.IsNullOrWhiteSpace(request.Message))
        return Results.Json(new { error = ErrorCodes.BackendTranslationInvalidText, message = "message boş olamaz." }, statusCode: 400);

    if (string.IsNullOrWhiteSpace(request.TargetLanguage))
        return Results.Json(new { error = ErrorCodes.BackendTranslationUnsupportedLang, message = "targetLanguage belirtilmeli." }, statusCode: 400);

    try
    {
        await translationService.TranslateAsync(tenantId, request);
        return Results.Ok(request);
    }
    catch (ArgumentException ex) when (ex.Message == ErrorCodes.BackendTranslationUnsupportedLang)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationUnsupportedLang, message = "Desteklenmeyen hedef dil." }, statusCode: 400);
    }
    catch (ArgumentException ex) when (ex.Message == ErrorCodes.BackendTranslationInvalidText)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationInvalidText, message = "Geçersiz kaynak metin." }, statusCode: 400);
    }
    catch (HttpRequestException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLog.StepError($"[{ErrorCodes.BackendTranslationFailed}] HTTP: {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendTranslationFailed, message = "Çeviri işlemi başarısız." }, statusCode: 500);
    }
    catch (OperationCanceledException)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationFailed, message = "Çeviri zaman aşımına uğradı." }, statusCode: 504);
    }
    catch (InvalidOperationException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLog.StepError($"[{ErrorCodes.BackendTranslationFailed}] {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendTranslationFailed, message = "Çeviri işlemi başarısız." }, statusCode: 500);
    }
});

// POST /api/v1/translate/batch — Toplu çeviri, max 50 mesaj (JWT auth)
app.MapPost("/api/v1/translate/batch", async (HttpContext ctx, TranslationService translationService, JsonLinesLogger jsonLog) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = "Tenant context missing." }, statusCode: 401);

    BatchTranslateRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<BatchTranslateRequest>(); }
    catch (JsonException) { return Results.Json(new { error = ErrorCodes.GeneralValidation, message = "Geçersiz istek gövdesi." }, statusCode: 400); }

    if (request == null || request.Messages.Count == 0)
        return Results.Json(new { error = ErrorCodes.BackendTranslationInvalidText, message = "Çevrilecek mesaj listesi boş." }, statusCode: 400);

    if (request.Messages.Count > 50)
        return Results.Json(new { error = ErrorCodes.BackendTranslationBatchTooLarge, message = "Toplu çeviri limiti 50 mesaj." }, statusCode: 400);

    if (string.IsNullOrWhiteSpace(request.TargetLanguage))
        return Results.Json(new { error = ErrorCodes.BackendTranslationUnsupportedLang, message = "Hedef dil belirtilmeli." }, statusCode: 400);

    try
    {
        var result = await translationService.TranslateBatchAsync(tenantContext.TenantId, request);
        return Results.Ok(result);
    }
    catch (ArgumentException ex) when (ex.Message == ErrorCodes.BackendTranslationUnsupportedLang)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationUnsupportedLang, message = "Desteklenmeyen hedef dil." }, statusCode: 400);
    }
    catch (ArgumentException ex) when (ex.Message == ErrorCodes.BackendTranslationBatchTooLarge)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationBatchTooLarge, message = "Toplu çeviri limiti 50 mesaj." }, statusCode: 400);
    }
    catch (HttpRequestException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLog.StepError($"[{ErrorCodes.BackendTranslationFailed}] Batch HTTP: {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendTranslationFailed, message = "Toplu çeviri işlemi başarısız." }, statusCode: 500);
    }
    catch (OperationCanceledException)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationFailed, message = "Toplu çeviri zaman aşımına uğradı." }, statusCode: 504);
    }
    catch (InvalidOperationException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLog.StepError($"[{ErrorCodes.BackendTranslationFailed}] Batch: {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendTranslationFailed, message = "Toplu çeviri işlemi başarısız." }, statusCode: 500);
    }
});

// POST /api/v1/translate/detect — Dil algılama (JWT auth VEYA X-Internal-Api-Key + X-Tenant-Id)
// Ayrıca /api/v1/detect-language olarak da erişilebilir (middleware bypass)
app.MapPost("/api/v1/detect-language", DetectLanguageHandler);
app.MapPost("/api/v1/translate/detect", DetectLanguageHandler);

async Task<IResult> DetectLanguageHandler(HttpContext ctx, TranslationService translationService, JsonLinesLogger jsonLog)
{
    // Auth: JWT TenantContext VEYA InternalApiKey + X-Tenant-Id header
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
    {
        var apiKeyHeader = ctx.Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(internalApiKey) || apiKeyHeader != internalApiKey)
            return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = "Yetkisiz. JWT veya X-Internal-Api-Key gerekli." }, statusCode: 401);

        var rawTid = ctx.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "";
        var (resolvedTid, tidError) = await ResolveTranslateTenantAsync(rawTid, jsonLog, ctx.RequestAborted);
        if (resolvedTid <= 0)
            return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = tidError ?? "X-Tenant-Id çözümlenemedi." }, statusCode: 401);
    }

    DetectLanguageRequest? request;
    try { request = await ctx.Request.ReadFromJsonAsync<DetectLanguageRequest>(); }
    catch (JsonException) { return Results.Json(new { error = ErrorCodes.GeneralValidation, message = "Geçersiz istek gövdesi." }, statusCode: 400); }

    if (request == null || string.IsNullOrWhiteSpace(request.ResolvedText))
        return Results.Json(new { error = ErrorCodes.BackendTranslationInvalidText, message = "Metin boş olamaz." }, statusCode: 400);

    try
    {
        var result = await translationService.DetectLanguageAsync(request.ResolvedText);
        return Results.Ok(result);
    }
    catch (HttpRequestException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLog.StepError($"[{ErrorCodes.BackendTranslationDetectFailed}] HTTP: {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendTranslationDetectFailed, message = "Dil algılama başarısız." }, statusCode: 500);
    }
    catch (OperationCanceledException)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationDetectFailed, message = "Dil algılama zaman aşımına uğradı." }, statusCode: 504);
    }
    catch (InvalidOperationException ex)
    {
        var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? "-";
        jsonLog.StepError($"[{ErrorCodes.BackendTranslationDetectFailed}] {ex.Message}", requestId);
        return Results.Json(new { error = ErrorCodes.BackendTranslationDetectFailed, message = "Dil algılama başarısız." }, statusCode: 500);
    }
}

// GET /api/v1/translate/languages — Desteklenen dil listesi (JWT auth)
app.MapGet("/api/v1/translate/languages", (HttpContext ctx) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = "Tenant context missing." }, statusCode: 401);

    return Results.Ok(TranslationService.SupportedLanguages);
});

// HFM-2: POST /ops/translation/warmup — pre-populate translation cache for a tenant
// before pilot go-live. Ops-level auth (X-Ops-Key / Basic) — intentional cross-tenant
// admin endpoint (see plan JSON intentional_exclusions). Body: tenantId + texts[] + locales[].
// Fans out TranslationService.TranslateBatchAsync per locale and aggregates counters.
// Per-locale failures use documented INV-BE-09x codes; the audit log captures the
// resolved ops actor identity (X-Ops-Key or Basic) so cross-tenant calls are traceable.
app.MapPost("/ops/translation/warmup", async (HttpContext ctx, TranslationService translationService, JsonLinesLogger log) =>
{
    if (!ValidateOpsAuth(ctx))
        return Results.Json(new { error = ErrorCodes.AuthUnauthorized, message = "Ops auth required." }, statusCode: 401);

    var actor = ResolveOpsIdentity(ctx);

    WarmupRequest? body;
    try
    {
        body = await ctx.Request.ReadFromJsonAsync<WarmupRequest>();
    }
    catch (JsonException ex)
    {
        return Results.Json(new { error = ErrorCodes.BackendTranslationWarmupInvalidPayload, message = $"Invalid JSON: {ex.Message}" }, statusCode: 400);
    }

    if (body == null || body.TenantId <= 0 || body.Texts == null || body.Texts.Count == 0 || body.Locales == null || body.Locales.Count == 0)
        return Results.Json(new { error = ErrorCodes.BackendTranslationWarmupInvalidPayload, message = "tenantId (>0), texts[] (non-empty), locales[] (non-empty) are all required." }, statusCode: 400);

    var sw = System.Diagnostics.Stopwatch.StartNew();
    var totalCacheHits = 0;
    var totalApiCalls = 0;
    var totalTranslated = 0;
    var failedCount = 0;
    var perLocale = new Dictionary<string, object>();

    foreach (var rawLocale in body.Locales)
    {
        var normalized = LanguageDetector.Normalize(rawLocale);
        if (string.IsNullOrEmpty(normalized))
        {
            failedCount++;
            log.SystemWarn($"[{ErrorCodes.BackendTranslationWarmupInvalidLocale}] invalid locale actor={actor} tenant={body.TenantId} raw={rawLocale}");
            perLocale[rawLocale] = new { error = ErrorCodes.BackendTranslationWarmupInvalidLocale };
            continue;
        }

        var batch = new BatchTranslateRequest
        {
            TargetLanguage = normalized,
            Messages = body.Texts.Select((t, i) => new BatchTranslateItem
            {
                Id = $"warmup-{i}",
                Text = t
            }).ToList()
        };

        try
        {
            var result = await translationService.TranslateBatchAsync(body.TenantId, batch, ctx.RequestAborted);
            totalCacheHits += result.CacheHits;
            totalApiCalls += result.ApiCalls;
            totalTranslated += result.Translations.Count;
            perLocale[normalized] = new { cacheHits = result.CacheHits, apiCalls = result.ApiCalls, count = result.Translations.Count };
        }
        catch (HttpRequestException ex)
        {
            failedCount++;
            log.SystemError($"[{ErrorCodes.BackendTranslationWarmupHttpFailure}] actor={actor} tenant={body.TenantId} locale={normalized} http error: {ex.Message}");
            perLocale[normalized] = new { error = ErrorCodes.BackendTranslationWarmupHttpFailure };
        }
        catch (InvalidOperationException ex)
        {
            failedCount++;
            log.SystemError($"[{ErrorCodes.BackendTranslationWarmupInvalidState}] actor={actor} tenant={body.TenantId} locale={normalized} invalid state: {ex.Message}");
            perLocale[normalized] = new { error = ErrorCodes.BackendTranslationWarmupInvalidState };
        }
        catch (OperationCanceledException)
        {
            failedCount++;
            log.SystemWarn($"[{ErrorCodes.BackendTranslationWarmupCancelled}] actor={actor} tenant={body.TenantId} cancelled mid-warmup locale={normalized}");
            perLocale[normalized] = new { error = ErrorCodes.BackendTranslationWarmupCancelled };
            break;
        }
    }

    sw.Stop();
    log.SystemInfo($"[translation-warmup] actor={actor} tenant={body.TenantId} translated={totalTranslated} cacheHits={totalCacheHits} apiCalls={totalApiCalls} failed={failedCount} locales={body.Locales.Count} durationMs={sw.ElapsedMilliseconds}");

    return Results.Ok(new
    {
        tenantId = body.TenantId,
        translatedCount = totalTranslated,
        cacheHits = totalCacheHits,
        apiCalls = totalApiCalls,
        failedCount,
        durationMs = sw.ElapsedMilliseconds,
        perLocale
    });
});

// SuperAdmin cross-tenant ops identity resolver — used by FEAT-J2 retry-skipped
// audit log + future cross-tenant ops endpoints. Returns "basic:{user}" /
// "jwt:user={id},role={r}" / "inma-jwt:user={id},role={r}" / "unknown".
string ResolveOpsIdentity(HttpContext ctx)
{
    var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
    if (string.IsNullOrEmpty(authHeader)) return "unknown";
    if (authHeader.StartsWith("Basic "))
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Basic ".Length..]));
            var parts = decoded.Split(':', 2);
            return parts.Length >= 1 ? $"basic:{parts[0]}" : "basic:?";
        }
        catch (FormatException ex)
        {
            logger.SystemWarn($"[OPS-ZOHO] ops identity Basic auth base64 decode failed: {ex.Message}");
            return "basic:?";
        }
        catch (DecoderFallbackException ex)
        {
            logger.SystemWarn($"[OPS-ZOHO] ops identity Basic auth utf8 decode failed: {ex.Message}");
            return "basic:?";
        }
    }
    if (authHeader.StartsWith("Bearer "))
    {
        var token = authHeader["Bearer ".Length..];
        if (jwtValidator != null)
        {
            var (context, _) = jwtValidator.ValidateToken(token);
            if (context != null) return $"jwt:user={context.UserId},role={context.Role}";
        }
        var introspectorOps = ctx.RequestServices.GetService<InmaTokenIntrospector>();
        if (introspectorOps != null)
        {
            // Sync-over-async: this helper is called from sync ops audit logger; ASP.NET Core
            // has no SynchronizationContext so no deadlock. Cache hit (~95%) is fully sync.
            var introspectOpsResult = introspectorOps.ValidateAsync(token).GetAwaiter().GetResult();
            if (introspectOpsResult.Context != null)
                return $"inma-jwt:user={introspectOpsResult.Context.UserId},role={introspectOpsResult.Context.Role}";
        }
        return "jwt:?";
    }
    return "unknown";
}

// ============================================
// FEAT-PHOTO wire-up patch (2026-04-28)
// ============================================
// Parent paket (commit 1da0da6) compiled-in route handlers; runtime Map zincirine
// burada baglaniyor. Plan arch/plans/20260428-feat-photo-wireup-patch.json AC3.
// (a) MapInmaInboundMediaEndpoint: POST /api/v1/inma/inbound/media — INMA dogrudan
//     forward variant (localhost or X-Internal-Api-Key); ana /webhook/event
//     handler'ina alternatif test layer'i.
// (b) MapPhotoEndpoints: GET/POST /api/v1/leads/{leadId:int}/photos — Dashboard
//     LeadDetailPage PhotoTab tuketicisi (JWT TenantContext required).
Invekto.Backend.Endpoints.InmaInboundMediaEndpoint.MapInmaInboundMediaEndpoint(app, internalApiKey);
Invekto.Backend.Endpoints.PhotoEndpoints.MapPhotoEndpoints(app);

// ============================================
// SPA FALLBACK ROUTES
// ============================================

// Unified SPA fallback: /app/* -> wwwroot/app/index.html
// Reuse spaStaticOptions so the fallback-served index.html also gets no-cache headers
// (a bare "/app/" directory request lands here, not on UseStaticFiles). Q.
app.MapFallbackToFile("app/{*path:nonfile}", "app/index.html", spaStaticOptions);

// Root redirect -> /app/ (preserve query params for SSO token flow)
app.MapGet("/", (HttpContext ctx) => Results.Redirect($"/app/{ctx.Request.QueryString}"));

logger.SystemInfo($"Backend starting on port {ServiceConstants.BackendPort}");
app.Run();

// HFM-2: payload for POST /ops/translation/warmup. Kept local because the endpoint is
// ops-only; callers either post JSON directly or rely on future Invekto.Shared DTO if
// this contract gets a second consumer.
public sealed class WarmupRequest
{
    public int TenantId { get; set; }
    public List<string> Texts { get; set; } = new();
    public List<string> Locales { get; set; } = new();
}

// Required for integration tests
namespace Invekto.Backend { public partial class Program { } }

// PR-3b-2: cached options for re-deserializing the inbound webhook body (and the cxapi ack) after
// raw-JSON shape discrimination. STJ caches metadata per options instance, so this MUST NOT be
// allocated per request. Web defaults mirror the previous framework model-binding (case-insensitive
// matching). Declared in the global namespace so the top-level statements reference it by simple name.
internal static class BackendWebhookJson
{
    internal static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);
}
