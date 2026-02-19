using Invekto.Appointments.Data;
using Invekto.Appointments.Services;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.Data;
using Invekto.Shared.DTOs;
using Invekto.Shared.DTOs.Appointments;
using Invekto.Shared.Logging;
using Invekto.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Windows Service support
builder.Host.UseWindowsService();

// Read configuration
var listenPort = builder.Configuration.GetValue<int>("Service:ListenPort", ServiceConstants.AppointmentsPort);
var logPath = builder.Configuration["Logging:FilePath"] ?? "logs";
var pgConnStr = builder.Configuration.GetConnectionString("PostgreSQL") ?? "";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "";
var outboundUrl = builder.Configuration["Outbound:Url"] ?? "";
var outboundTimeoutMs = builder.Configuration.GetValue<int>("Outbound:TimeoutMs", 10000);
var reminderIntervalMs = builder.Configuration.GetValue<int>("Reminder:IntervalMs", 300_000);
var reminderBatchSize = builder.Configuration.GetValue<int>("Reminder:BatchSize", 50);
var lifecycleIntervalMs = builder.Configuration.GetValue<int>("Lifecycle:IntervalMs", 300_000);
var lifecycleBatchSize = builder.Configuration.GetValue<int>("Lifecycle:BatchSize", 50);

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
var logger = new JsonLinesLogger(ServiceConstants.AppointmentsServiceName, logPath);
builder.Services.AddSingleton(logger);

// Register log cleanup
builder.Services.AddSingleton<LogCleanupService>(sp =>
    new LogCleanupService(logPath, ServiceConstants.LogRetentionDays));

// Register JWT validator + generator
var jwtSettings = new JwtSettings
{
    SecretKey = jwtSecretKey,
    Issuer = builder.Configuration["Jwt:Issuer"],
    Audience = builder.Configuration["Jwt:Audience"],
    ClockSkewSeconds = builder.Configuration.GetValue<int>("Jwt:ClockSkewSeconds", 60)
};
var jwtValidator = new JwtValidator(jwtSettings);
builder.Services.AddSingleton(jwtValidator);
var jwtGenerator = new JwtGenerator(jwtSettings);
builder.Services.AddSingleton(jwtGenerator);

// Register PostgreSQL connection factory
var pgFactory = new PostgresConnectionFactory(pgConnStr);
builder.Services.AddSingleton(pgFactory);

// Register repository
builder.Services.AddSingleton<AppointmentsRepository>();

// Register Outbound HttpClient for reminder scheduler
builder.Services.AddHttpClient("Outbound", client =>
{
    client.BaseAddress = new Uri(outboundUrl);
    client.Timeout = TimeSpan.FromMilliseconds(outboundTimeoutMs);
});

// Register reminder scheduler (IHostedService)
builder.Services.AddSingleton<ReminderSchedulerService>(sp =>
    new ReminderSchedulerService(
        sp.GetRequiredService<AppointmentsRepository>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<JwtGenerator>(),
        sp.GetRequiredService<JsonLinesLogger>(),
        reminderIntervalMs,
        reminderBatchSize));
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReminderSchedulerService>());

// GR-3.19: Register WaitlistService (IHostedService - expiration timer + cancel-flow hook)
builder.Services.AddSingleton<WaitlistService>(sp =>
    new WaitlistService(
        sp.GetRequiredService<AppointmentsRepository>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<JwtGenerator>(),
        sp.GetRequiredService<JsonLinesLogger>()));
builder.Services.AddHostedService(sp => sp.GetRequiredService<WaitlistService>());

// GR-3.20/3.41/3.43: Register TreatmentLifecycleService (IHostedService)
builder.Services.AddSingleton<TreatmentLifecycleService>(sp =>
    new TreatmentLifecycleService(
        sp.GetRequiredService<AppointmentsRepository>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<JwtGenerator>(),
        sp.GetRequiredService<JsonLinesLogger>(),
        lifecycleIntervalMs,
        lifecycleBatchSize));
builder.Services.AddHostedService(sp => sp.GetRequiredService<TreatmentLifecycleService>());

// GR-3.19: Calendar sync (mock for now, interface ready for Google Calendar)
builder.Services.AddSingleton<ICalendarSyncService, MockCalendarSyncService>();

var app = builder.Build();

// Enable traffic logging middleware
app.UseTrafficLogging();

// Enable JWT auth for /api/v1/ prefixed paths
app.UseJwtAuth(jwtValidator, logger, "/api/v1/");

// Start log cleanup
_ = app.Services.GetRequiredService<LogCleanupService>();

// ============================================================
// Health endpoints
// ============================================================

app.MapGet("/health", () => Results.Ok(HealthResponse.Ok(ServiceConstants.AppointmentsServiceName)));
app.MapGet("/ready", async (PostgresConnectionFactory db) =>
{
    var (ok, error) = await db.TestConnectionAsync();
    if (!ok)
        return Results.Json(new { status = "unhealthy", error }, statusCode: 503);
    return Results.Ok(HealthResponse.Ok(ServiceConstants.AppointmentsServiceName));
});

// ============================================================
// Slot endpoints
// ============================================================

app.MapGet("/api/v1/slots", async (
    HttpContext ctx,
    AppointmentsRepository repository) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"),
            statusCode: 401);

    var slots = await repository.GetSlotsAsync(tenantContext.TenantId);
    return Results.Ok(new { slots });
});

app.MapPost("/api/v1/slots", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    JsonLinesLogger jsonLogger,
    SlotCreateRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null
        || string.IsNullOrWhiteSpace(request.StartTime)
        || string.IsNullOrWhiteSpace(request.EndTime))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "start_time and end_time are required", requestId),
            statusCode: 400);
    }

    if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "day_of_week must be 0 (Sunday) to 6 (Saturday)", requestId),
            statusCode: 400);
    }

    if (!TimeOnly.TryParseExact(request.StartTime, "HH:mm", null,
            System.Globalization.DateTimeStyles.None, out var startTime)
        || !TimeOnly.TryParseExact(request.EndTime, "HH:mm", null,
            System.Globalization.DateTimeStyles.None, out var endTime))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "start_time and end_time must be in HH:mm format", requestId),
            statusCode: 400);
    }

    if (startTime >= endTime)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "start_time must be before end_time", requestId),
            statusCode: 400);
    }

    if (request.MaxBookings <= 0)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "max_bookings must be greater than 0", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);

    var id = await repository.CreateSlotAsync(tenantContext.TenantId, request);

    jsonLogger.StepInfo(
        $"Slot created: id={id}, day={request.DayOfWeek}, time={request.StartTime}-{request.EndTime}",
        requestId);

    return Results.Json(new { id }, statusCode: 201);
});

app.MapPut("/api/v1/slots/{id:int}", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    JsonLinesLogger jsonLogger,
    int id,
    SlotUpdateRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    if (request == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload, "Request body is required", requestId),
            statusCode: 400);
    }

    if (request.DayOfWeek.HasValue && (request.DayOfWeek.Value < 0 || request.DayOfWeek.Value > 6))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "day_of_week must be 0 (Sunday) to 6 (Saturday)", requestId),
            statusCode: 400);
    }

    if (request.StartTime != null
        && !TimeOnly.TryParseExact(request.StartTime, "HH:mm", null,
            System.Globalization.DateTimeStyles.None, out _))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "start_time must be in HH:mm format", requestId),
            statusCode: 400);
    }

    if (request.EndTime != null
        && !TimeOnly.TryParseExact(request.EndTime, "HH:mm", null,
            System.Globalization.DateTimeStyles.None, out _))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "end_time must be in HH:mm format", requestId),
            statusCode: 400);
    }

    if (request.MaxBookings.HasValue && request.MaxBookings.Value <= 0)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidSlotPayload,
                "max_bookings must be greater than 0", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);

    var updated = await repository.UpdateSlotAsync(tenantContext.TenantId, id, request);
    if (!updated)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentSlotNotFound, $"Slot {id} not found", requestId),
            statusCode: 404);
    }

    jsonLogger.StepInfo($"Slot updated: id={id}", requestId);
    return Results.Ok(new { id, updated = true });
});

app.MapDelete("/api/v1/slots/{id:int}", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    JsonLinesLogger jsonLogger,
    int id) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);

    var deactivated = await repository.DeactivateSlotAsync(tenantContext.TenantId, id);
    if (!deactivated)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentSlotNotFound,
                $"Slot {id} not found or already inactive", requestId),
            statusCode: 404);
    }

    jsonLogger.StepInfo($"Slot deactivated: id={id}", requestId);
    return Results.Ok(new { id, deactivated = true });
});

// ============================================================
// Appointment endpoints
// ============================================================

app.MapPost("/api/v1/appointments/book", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    JsonLinesLogger jsonLogger,
    AppointmentBookRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    ctx.Request.Headers["X-Request-Id"] = requestId;

    if (request == null
        || string.IsNullOrWhiteSpace(request.PatientName)
        || string.IsNullOrWhiteSpace(request.PatientPhone)
        || string.IsNullOrWhiteSpace(request.AppointmentDate))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidBookingPayload,
                "patient_name, patient_phone, and appointment_date are required", requestId),
            statusCode: 400);
    }

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);

    // Validate slot exists and is active
    var slot = await repository.GetSlotByIdAsync(tenantContext.TenantId, request.SlotId);
    if (slot == null || !slot.IsActive)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentSlotNotFound,
                $"Slot {request.SlotId} not found or inactive", requestId),
            statusCode: 404);
    }

    // Validate appointment_date format
    if (!DateOnly.TryParseExact(request.AppointmentDate, "yyyy-MM-dd", out var appointmentDate))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidDateTime,
                "appointment_date must be in yyyy-MM-dd format", requestId),
            statusCode: 400);
    }

    // Validate not in the past
    if (appointmentDate < DateOnly.FromDateTime(DateTime.Today))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentBookingInPast,
                "Cannot book appointments in the past", requestId),
            statusCode: 400);
    }

    // Validate day_of_week matches slot definition
    if ((int)appointmentDate.DayOfWeek != slot.DayOfWeek)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidDateTime,
                $"Appointment date falls on {appointmentDate.DayOfWeek} but slot is for day_of_week={slot.DayOfWeek}",
                requestId),
            statusCode: 400);
    }

    // Check slot capacity
    var confirmedCount = await repository.CountConfirmedForSlotAsync(tenantContext.TenantId, request.SlotId, appointmentDate);
    if (confirmedCount >= slot.MaxBookings)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentSlotFullyBooked,
                $"Slot {request.SlotId} is fully booked for {request.AppointmentDate} ({confirmedCount}/{slot.MaxBookings})",
                requestId),
            statusCode: 409);
    }

    // Parse slot times for the appointment record
    var startTime = TimeOnly.Parse(slot.StartTime);
    var endTime = TimeOnly.Parse(slot.EndTime);

    var id = await repository.BookAppointmentAsync(
        tenantContext.TenantId, request.SlotId, slot.DoctorId,
        request.PatientName, request.PatientPhone,
        appointmentDate, startTime, endTime, request.Notes);

    jsonLogger.StepInfo(
        $"Appointment booked: id={id}, slot={request.SlotId}, date={request.AppointmentDate}, " +
        $"patient={request.PatientName}",
        requestId);

    return Results.Json(new { id, status = "confirmed" }, statusCode: 201);
});

app.MapGet("/api/v1/appointments", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    string? date,
    string? status) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"),
            statusCode: 401);

    DateOnly? dateFilter = null;
    if (!string.IsNullOrEmpty(date))
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed))
            return Results.Json(
                ErrorResponse.Create(ErrorCodes.AppointmentInvalidDateTime,
                    "date must be in yyyy-MM-dd format", "-"),
                statusCode: 400);
        dateFilter = parsed;
    }

    // Validate status if provided
    if (!string.IsNullOrEmpty(status)
        && status is not ("confirmed" or "cancelled" or "completed" or "no_show"))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidBookingPayload,
                "status must be one of: confirmed, cancelled, completed, no_show", "-"),
            statusCode: 400);
    }

    var appointments = await repository.GetAppointmentsAsync(
        tenantContext.TenantId, dateFilter, status);
    return Results.Ok(new { appointments });
});

app.MapGet("/api/v1/appointments/{id:long}", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    long id) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"),
            statusCode: 401);

    var appointment = await repository.GetAppointmentByIdAsync(tenantContext.TenantId, id);
    if (appointment == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentNotFound, $"Appointment {id} not found", "-"),
            statusCode: 404);
    }

    return Results.Ok(appointment);
});

app.MapPost("/api/v1/appointments/{id:long}/cancel", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    WaitlistService waitlistService,
    JsonLinesLogger jsonLogger,
    long id,
    AppointmentCancelRequest? request) =>
{
    var requestId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", requestId),
            statusCode: 401);

    // Check appointment exists
    var appointment = await repository.GetAppointmentByIdAsync(tenantContext.TenantId, id);
    if (appointment == null)
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentNotFound, $"Appointment {id} not found", requestId),
            statusCode: 404);
    }

    if (appointment.Status == "cancelled")
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentAlreadyCancelled,
                $"Appointment {id} is already cancelled", requestId),
            statusCode: 409);
    }

    var cancelled = await repository.CancelAppointmentAsync(
        tenantContext.TenantId, id, request?.Reason);
    if (!cancelled)
    {
        // Status was not 'confirmed' (completed or no_show)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentAlreadyCancelled,
                $"Appointment {id} cannot be cancelled (current status: {appointment.Status})", requestId),
            statusCode: 409);
    }

    // GR-3.19: Trigger waitlist matching (fire-and-forget, must not block cancel response)
    var apptDate = DateOnly.ParseExact(appointment.AppointmentDate, "yyyy-MM-dd");
    _ = waitlistService.ProcessCancelledAppointmentAsync(
        tenantContext.TenantId, apptDate, appointment.DoctorId, ctx.RequestAborted);

    jsonLogger.StepInfo($"Appointment cancelled: id={id}, reason={request?.Reason ?? "none"}", requestId);
    return Results.Ok(new { id, status = "cancelled" });
});

// ============================================================
// Available slots endpoint
// ============================================================

app.MapGet("/api/v1/appointments/available-slots", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    string? date,
    int? doctor_id) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"),
            statusCode: 401);

    if (string.IsNullOrEmpty(date))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidDateTime,
                "date query parameter is required (yyyy-MM-dd)", "-"),
            statusCode: 400);
    }

    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var dateValue))
    {
        return Results.Json(
            ErrorResponse.Create(ErrorCodes.AppointmentInvalidDateTime,
                "date must be in yyyy-MM-dd format", "-"),
            statusCode: 400);
    }

    // GR-3.19: Optional doctor_id filter
    var slots = await repository.GetAvailableSlotsAsync(tenantContext.TenantId, dateValue, doctor_id);
    return Results.Ok(new { date = dateValue.ToString("yyyy-MM-dd"), doctor_id, slots });
});

// ============================================================
// GR-3.19: Waitlist endpoints
// ============================================================

app.MapGet("/api/v1/waitlist", async (
    HttpContext ctx, AppointmentsRepository repository, string? status) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var entries = await repository.GetWaitlistAsync(tenantContext.TenantId, status);
    return Results.Ok(new { waitlist = entries });
});

app.MapPost("/api/v1/waitlist", async (
    HttpContext ctx, AppointmentsRepository repository, JsonLinesLogger jsonLogger,
    WaitlistCreateRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null || string.IsNullOrEmpty(request.PatientPhone) || string.IsNullOrEmpty(request.PatientName))
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentInvalidWaitlistPayload,
            "patient_phone and patient_name are required", rid), statusCode: 400);

    var id = await repository.InsertWaitlistAsync(tenantContext.TenantId, request);
    jsonLogger.StepInfo($"Waitlist entry created: id={id}, phone={request.PatientPhone}", rid);
    return Results.Json(new { message = "Waitlist entry created", id }, statusCode: 201);
});

app.MapPut("/api/v1/waitlist/{id:int}/status", async (
    HttpContext ctx, AppointmentsRepository repository, int id, string? status) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (string.IsNullOrEmpty(status) || status is not ("waiting" or "notified" or "booked" or "expired" or "cancelled"))
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentInvalidWaitlistPayload,
            "Valid status: waiting, notified, booked, expired, cancelled", rid), statusCode: 400);

    var updated = await repository.UpdateWaitlistStatusAsync(tenantContext.TenantId, id, status);
    if (!updated)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentWaitlistNotFound, "Waitlist entry not found", rid), statusCode: 404);

    return Results.Ok(new { id, status });
});

// ============================================================
// GR-3.19: Service pricing endpoints
// ============================================================

app.MapGet("/api/v1/pricing", async (
    HttpContext ctx, AppointmentsRepository repository, bool? all) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var pricing = await repository.GetPricingAsync(tenantContext.TenantId, activeOnly: all != true);
    return Results.Ok(new { pricing });
});

app.MapPost("/api/v1/pricing", async (
    HttpContext ctx, AppointmentsRepository repository, JsonLinesLogger jsonLogger,
    PricingCreateRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null || string.IsNullOrEmpty(request.ServiceName) || request.PriceMin < 0 || request.PriceMax < request.PriceMin)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentInvalidPricingPayload,
            "service_name required, price_min >= 0, price_max >= price_min", rid), statusCode: 400);

    try
    {
        var id = await repository.CreatePricingAsync(tenantContext.TenantId, request);
        jsonLogger.StepInfo($"Pricing created: id={id}, service={request.ServiceName}", rid);
        return Results.Json(new { message = "Pricing created", id }, statusCode: 201);
    }
    catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505") // unique violation
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentInvalidPricingPayload,
            $"Service '{request.ServiceName}' already has active pricing", rid), statusCode: 409);
    }
});

app.MapPut("/api/v1/pricing/{id:int}", async (
    HttpContext ctx, AppointmentsRepository repository, int id,
    PricingUpdateRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentInvalidPricingPayload, "Request body required", rid), statusCode: 400);

    var updated = await repository.UpdatePricingAsync(tenantContext.TenantId, id, request);
    if (!updated)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AppointmentPricingNotFound, "Pricing not found", rid), statusCode: 404);

    return Results.Ok(new { message = "Pricing updated", id });
});

// ============================================================
// GR-3.19: No-show stats endpoint
// ============================================================

app.MapGet("/api/v1/appointments/no-show-stats", async (
    HttpContext ctx, AppointmentsRepository repository, JsonLinesLogger jsonLogger, string? phone) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    if (string.IsNullOrEmpty(phone))
        return Results.Json(ErrorResponse.Create(ErrorCodes.GeneralValidation, "phone query parameter required", "-"), statusCode: 400);

    // Get no-show threshold from tenant settings (default 2)
    var (settingsJson, _) = await repository.GetTenantHealthInfoAsync(tenantContext.TenantId);
    var threshold = 2;
    if (!string.IsNullOrEmpty(settingsJson))
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(settingsJson);
            if (doc.RootElement.TryGetProperty("no_show_threshold", out var thresholdEl))
                threshold = thresholdEl.GetInt32();
        }
        catch (System.Text.Json.JsonException ex)
        {
            jsonLogger.SystemWarn($"Malformed settings_json for tenant {tenantContext.TenantId}: {ex.Message}");
        }
    }

    var stats = await repository.GetNoShowStatsAsync(tenantContext.TenantId, phone, threshold);
    return Results.Ok(stats);
});

// ============================================================
// GR-3.20/3.41/3.43: Treatment Lifecycle endpoints
// ============================================================

app.MapPost("/api/v1/lifecycle/start", async (
    HttpContext ctx, TreatmentLifecycleService lifecycleService,
    JsonLinesLogger jsonLogger, LifecycleStartRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null
        || string.IsNullOrWhiteSpace(request.LifecycleType)
        || string.IsNullOrWhiteSpace(request.PatientPhone)
        || string.IsNullOrWhiteSpace(request.PatientName)
        || string.IsNullOrWhiteSpace(request.ReferenceDate))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleInvalidPayload,
            "lifecycle_type, patient_phone, patient_name, reference_date are required", rid), statusCode: 400);
    }

    var validTypes = new[] { "post_treatment", "plan_approval", "pre_op" };
    if (!validTypes.Contains(request.LifecycleType))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleInvalidType,
            $"lifecycle_type must be one of: {string.Join(", ", validTypes)}", rid), statusCode: 400);
    }

    if (!DateTimeOffset.TryParse(request.ReferenceDate, out _))
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleInvalidPayload,
            "reference_date must be a valid ISO 8601 datetime", rid), statusCode: 400);
    }

    try
    {
        var followupId = await lifecycleService.StartLifecycleAsync(tenantContext.TenantId, request);
        jsonLogger.SystemInfo($"Lifecycle started via API: id={followupId}, type={request.LifecycleType}, tenant={tenantContext.TenantId}");
        return Results.Ok(new { id = followupId, lifecycle_type = request.LifecycleType, status = "active" });
    }
    catch (ArgumentException ex)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleInvalidType, ex.Message, rid), statusCode: 400);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.DatabaseConnectionFailed}] Lifecycle start failed: tenant={tenantContext.TenantId}, error={ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Lifecycle start failed due to a database error", rid), statusCode: 500);
    }
});

app.MapGet("/api/v1/lifecycle", async (
    HttpContext ctx, AppointmentsRepository repository, JsonLinesLogger jsonLogger,
    string? type, string? status) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var followups = await repository.GetFollowupsAsync(tenantContext.TenantId, type, status);
        return Results.Ok(new { followups });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.DatabaseConnectionFailed}] Lifecycle list failed: tenant={tenantContext.TenantId}, error={ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Lifecycle list failed due to a database error", rid), statusCode: 500);
    }
});

app.MapGet("/api/v1/lifecycle/{id:int}", async (
    HttpContext ctx, AppointmentsRepository repository, JsonLinesLogger jsonLogger, int id) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var followup = await repository.GetFollowupByIdAsync(tenantContext.TenantId, id);
        if (followup == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleNotFound, "Lifecycle not found", rid), statusCode: 404);
        return Results.Ok(followup);
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.DatabaseConnectionFailed}] Lifecycle get failed: id={id}, tenant={tenantContext.TenantId}, error={ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Lifecycle retrieval failed due to a database error", rid), statusCode: 500);
    }
});

app.MapPost("/api/v1/lifecycle/{id:int}/cancel", async (
    HttpContext ctx, AppointmentsRepository repository, JsonLinesLogger jsonLogger, int id) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    try
    {
        var cancelled = await repository.CancelFollowupAsync(tenantContext.TenantId, id);
        if (!cancelled)
        {
            var existing = await repository.GetFollowupByIdAsync(tenantContext.TenantId, id);
            if (existing == null)
                return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleNotFound, "Lifecycle not found", rid), statusCode: 404);
            return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleAlreadyFinished,
                $"Lifecycle already {existing.Status}", rid), statusCode: 409);
        }

        jsonLogger.SystemInfo($"Lifecycle cancelled via API: id={id}, tenant={tenantContext.TenantId}");
        return Results.Ok(new { id, status = "cancelled" });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.DatabaseConnectionFailed}] Lifecycle cancel failed: id={id}, tenant={tenantContext.TenantId}, error={ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Lifecycle cancellation failed due to a database error", rid), statusCode: 500);
    }
});

app.MapPost("/api/v1/lifecycle/{id:int}/response", async (
    HttpContext ctx, AppointmentsRepository repository,
    TreatmentLifecycleService lifecycleService,
    JsonLinesLogger jsonLogger, int id, LifecycleResponseRequest? request) =>
{
    var rid = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", rid), statusCode: 401);

    if (request == null || request.StepId <= 0)
    {
        return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleInvalidPayload,
            "step_id is required and must be > 0", rid), statusCode: 400);
    }

    try
    {
        // Verify followup exists and is active
        var followup = await repository.GetFollowupByIdAsync(tenantContext.TenantId, id);
        if (followup == null)
            return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleNotFound, "Lifecycle not found", rid), statusCode: 404);

        if (followup.Status != "active")
            return Results.Json(ErrorResponse.Create(ErrorCodes.LifecycleAlreadyFinished,
                $"Lifecycle already {followup.Status}", rid), statusCode: 409);

        await repository.RecordPatientResponseAsync(
            tenantContext.TenantId, id, request.StepId,
            request.ResponseText, request.ComplaintDetected);

        // If complaint detected, escalate to doctor
        if (request.ComplaintDetected)
        {
            var stepContext = new DueStepCandidate
            {
                StepId = request.StepId,
                FollowupId = id,
                TenantId = tenantContext.TenantId,
                PatientPhone = followup.PatientPhone,
                PatientName = followup.PatientName,
                LifecycleType = followup.LifecycleType,
                TreatmentType = followup.TreatmentType
            };
            await lifecycleService.HandleComplaintEscalationAsync(tenantContext.TenantId, id, stepContext);
            jsonLogger.SystemInfo($"Patient response with complaint: lifecycle={id}, step={request.StepId}, tenant={tenantContext.TenantId}");
        }

        return Results.Ok(new { message = "Response recorded", step_id = request.StepId, complaint_escalated = request.ComplaintDetected });
    }
    catch (Npgsql.NpgsqlException ex)
    {
        jsonLogger.SystemError($"[{ErrorCodes.DatabaseConnectionFailed}] Lifecycle response failed: id={id}, step={request.StepId}, tenant={tenantContext.TenantId}, error={ex.Message}");
        return Results.Json(ErrorResponse.Create(ErrorCodes.DatabaseConnectionFailed, "Lifecycle response recording failed due to a database error", rid), statusCode: 500);
    }
});

// ============================================================
// GR-3.19: Calendar sync status
// ============================================================

app.MapGet("/api/v1/calendar/status", async (
    HttpContext ctx, ICalendarSyncService calendarSync) =>
{
    var tenantContext = ctx.Items["TenantContext"] as TenantContext;
    if (tenantContext == null)
        return Results.Json(ErrorResponse.Create(ErrorCodes.AuthUnauthorized, "Tenant context not available", "-"), statusCode: 401);

    var available = await calendarSync.IsAvailableAsync(tenantContext.TenantId);
    return Results.Ok(new { calendar_sync_available = available, provider = "mock" });
});

// ============================================================
// Endpoint discovery
// ============================================================

app.MapGet("/api/ops/endpoints", () =>
{
    var endpoints = new List<EndpointInfo>
    {
        new() { Method = "GET", Path = "/api/v1/slots", Description = "List active slots", Auth = "Bearer JWT", Category = "Slots" },
        new() { Method = "POST", Path = "/api/v1/slots", Description = "Create slot", Auth = "Bearer JWT", Category = "Slots" },
        new() { Method = "PUT", Path = "/api/v1/slots/{id}", Description = "Update slot", Auth = "Bearer JWT", Category = "Slots" },
        new() { Method = "DELETE", Path = "/api/v1/slots/{id}", Description = "Deactivate slot (soft delete)", Auth = "Bearer JWT", Category = "Slots" },
        new() { Method = "POST", Path = "/api/v1/appointments/book", Description = "Book appointment", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "GET", Path = "/api/v1/appointments", Description = "List appointments (optional ?date=&status= filter)", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "GET", Path = "/api/v1/appointments/{id}", Description = "Get appointment by ID", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "POST", Path = "/api/v1/appointments/{id}/cancel", Description = "Cancel appointment + waitlist trigger", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "GET", Path = "/api/v1/appointments/available-slots", Description = "Available slots (?date=&doctor_id=)", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "GET", Path = "/api/v1/appointments/no-show-stats", Description = "No-show stats for patient (?phone=)", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "GET", Path = "/api/v1/waitlist", Description = "List waitlist entries (?status=)", Auth = "Bearer JWT", Category = "Waitlist" },
        new() { Method = "POST", Path = "/api/v1/waitlist", Description = "Add to waitlist", Auth = "Bearer JWT", Category = "Waitlist" },
        new() { Method = "PUT", Path = "/api/v1/waitlist/{id}/status", Description = "Update waitlist status (?status=)", Auth = "Bearer JWT", Category = "Waitlist" },
        new() { Method = "GET", Path = "/api/v1/pricing", Description = "List service pricing (?all=true)", Auth = "Bearer JWT", Category = "Pricing" },
        new() { Method = "POST", Path = "/api/v1/pricing", Description = "Create service pricing", Auth = "Bearer JWT", Category = "Pricing" },
        new() { Method = "PUT", Path = "/api/v1/pricing/{id}", Description = "Update service pricing", Auth = "Bearer JWT", Category = "Pricing" },
        new() { Method = "POST", Path = "/api/v1/lifecycle/start", Description = "Start treatment lifecycle", Auth = "Bearer JWT", Category = "Lifecycle" },
        new() { Method = "GET", Path = "/api/v1/lifecycle", Description = "List lifecycles (?type=&status=)", Auth = "Bearer JWT", Category = "Lifecycle" },
        new() { Method = "GET", Path = "/api/v1/lifecycle/{id}", Description = "Get lifecycle with steps", Auth = "Bearer JWT", Category = "Lifecycle" },
        new() { Method = "POST", Path = "/api/v1/lifecycle/{id}/cancel", Description = "Cancel lifecycle", Auth = "Bearer JWT", Category = "Lifecycle" },
        new() { Method = "POST", Path = "/api/v1/lifecycle/{id}/response", Description = "Record patient response", Auth = "Bearer JWT", Category = "Lifecycle" },
        new() { Method = "GET", Path = "/api/v1/calendar/status", Description = "Calendar sync status", Auth = "Bearer JWT", Category = "Calendar" },
        new() { Method = "GET", Path = "/health", Description = "Health check", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/ready", Description = "Readiness probe (DB check)", Auth = "none", Category = "Health" },
        new() { Method = "GET", Path = "/api/ops/endpoints", Description = "Endpoint discovery (this)", Auth = "none", Category = "Ops" },
    };

    return Results.Ok(new EndpointDiscoveryResponse
    {
        Service = ServiceConstants.AppointmentsServiceName,
        Port = ServiceConstants.AppointmentsPort,
        Endpoints = endpoints
    });
});

logger.SystemInfo($"Appointments service starting on port {listenPort}");
app.Run();

// Required for integration tests
public partial class Program { }
