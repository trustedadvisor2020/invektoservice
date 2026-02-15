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

// Validate required config
if (string.IsNullOrEmpty(pgConnStr))
    throw new InvalidOperationException("FATAL: ConnectionStrings:PostgreSQL is not configured");
if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("FATAL: Jwt:SecretKey is not configured");

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(listenPort);
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

    jsonLogger.StepInfo($"Appointment cancelled: id={id}, reason={request?.Reason ?? "none"}", requestId);
    return Results.Ok(new { id, status = "cancelled" });
});

// ============================================================
// Available slots endpoint
// ============================================================

app.MapGet("/api/v1/appointments/available-slots", async (
    HttpContext ctx,
    AppointmentsRepository repository,
    string? date) =>
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

    var slots = await repository.GetAvailableSlotsAsync(tenantContext.TenantId, dateValue);
    return Results.Ok(new { date = dateValue.ToString("yyyy-MM-dd"), slots });
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
        new() { Method = "POST", Path = "/api/v1/appointments/{id}/cancel", Description = "Cancel appointment", Auth = "Bearer JWT", Category = "Appointments" },
        new() { Method = "GET", Path = "/api/v1/appointments/available-slots", Description = "Get available slots for a date (?date=yyyy-MM-dd)", Auth = "Bearer JWT", Category = "Appointments" },
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
