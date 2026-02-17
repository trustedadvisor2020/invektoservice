using Invekto.Shared.Data;
using Invekto.Shared.DTOs.Appointments;
using Invekto.Shared.Logging;
using Npgsql;

namespace Invekto.Appointments.Data;

public sealed class AppointmentsRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public AppointmentsRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ================================================================
    // Slots
    // ================================================================

    public async Task<List<SlotDto>> GetSlotsAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, doctor_id, day_of_week, start_time, end_time,
                   max_bookings, is_active, created_at, updated_at
            FROM appointment_slots
            WHERE tenant_id = @tid AND is_active = TRUE
            ORDER BY day_of_week, start_time";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        var slots = new List<SlotDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            slots.Add(ReadSlotDto(reader));
        }
        return slots;
    }

    public async Task<SlotDto?> GetSlotByIdAsync(
        int tenantId, int slotId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, doctor_id, day_of_week, start_time, end_time,
                   max_bookings, is_active, created_at, updated_at
            FROM appointment_slots
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", slotId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadSlotDto(reader);
        return null;
    }

    public async Task<int> CreateSlotAsync(
        int tenantId, SlotCreateRequest req, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO appointment_slots (tenant_id, doctor_id, day_of_week, start_time, end_time, max_bookings)
            VALUES (@tid, @doctorId, @dow, @startTime, @endTime, @maxBookings)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("doctorId", req.DoctorId.HasValue ? req.DoctorId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("dow", (short)req.DayOfWeek);
        cmd.Parameters.AddWithValue("startTime", TimeOnly.Parse(req.StartTime));
        cmd.Parameters.AddWithValue("endTime", TimeOnly.Parse(req.EndTime));
        cmd.Parameters.AddWithValue("maxBookings", req.MaxBookings);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<bool> UpdateSlotAsync(
        int tenantId, int slotId, SlotUpdateRequest req, CancellationToken ct = default)
    {
        var setClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>
        {
            new("tid", tenantId),
            new("id", slotId)
        };

        if (req.DayOfWeek.HasValue)
        {
            setClauses.Add("day_of_week = @dow");
            parameters.Add(new NpgsqlParameter("dow", (short)req.DayOfWeek.Value));
        }
        if (req.StartTime != null)
        {
            setClauses.Add("start_time = @startTime");
            parameters.Add(new NpgsqlParameter("startTime", TimeOnly.Parse(req.StartTime)));
        }
        if (req.EndTime != null)
        {
            setClauses.Add("end_time = @endTime");
            parameters.Add(new NpgsqlParameter("endTime", TimeOnly.Parse(req.EndTime)));
        }
        if (req.MaxBookings.HasValue)
        {
            setClauses.Add("max_bookings = @maxBookings");
            parameters.Add(new NpgsqlParameter("maxBookings", req.MaxBookings.Value));
        }
        if (req.IsActive.HasValue)
        {
            setClauses.Add("is_active = @isActive");
            parameters.Add(new NpgsqlParameter("isActive", req.IsActive.Value));
        }
        if (req.DoctorId.HasValue)
        {
            setClauses.Add("doctor_id = @doctorId");
            parameters.Add(new NpgsqlParameter("doctorId", req.DoctorId.Value));
        }

        if (setClauses.Count == 0) return false;

        var sql = $@"
            UPDATE appointment_slots
            SET {string.Join(", ", setClauses)}
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var p in parameters)
            cmd.Parameters.Add(p);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> DeactivateSlotAsync(
        int tenantId, int slotId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE appointment_slots
            SET is_active = FALSE
            WHERE tenant_id = @tid AND id = @id AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", slotId);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ================================================================
    // Appointments
    // ================================================================

    public async Task<long> BookAppointmentAsync(
        int tenantId, int slotId, int? doctorId,
        string patientName, string patientPhone,
        DateOnly appointmentDate, TimeOnly startTime, TimeOnly endTime,
        string? notes, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO appointments
                (tenant_id, slot_id, doctor_id, patient_name, patient_phone,
                 appointment_date, start_time, end_time, notes)
            VALUES
                (@tid, @slotId, @doctorId, @name, @phone, @date, @startTime, @endTime, @notes)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("slotId", slotId);
        cmd.Parameters.AddWithValue("doctorId", doctorId.HasValue ? doctorId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("name", patientName);
        cmd.Parameters.AddWithValue("phone", patientPhone);
        cmd.Parameters.AddWithValue("date", appointmentDate);
        cmd.Parameters.AddWithValue("startTime", startTime);
        cmd.Parameters.AddWithValue("endTime", endTime);
        cmd.Parameters.AddWithValue("notes", notes ?? (object)DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return (long)result!;
    }

    public async Task<List<AppointmentDto>> GetAppointmentsAsync(
        int tenantId, DateOnly? date = null, string? status = null,
        int limit = 100, CancellationToken ct = default)
    {
        var sql = @"
            SELECT id, tenant_id, slot_id, doctor_id, patient_name, patient_phone,
                   appointment_date, start_time, end_time, status,
                   reminder_48h_sent, reminder_2h_sent, cancel_reason, notes,
                   created_at, updated_at
            FROM appointments
            WHERE tenant_id = @tid"
            + (date.HasValue ? " AND appointment_date = @date" : "")
            + (!string.IsNullOrEmpty(status) ? " AND status = @status" : "")
            + " ORDER BY appointment_date DESC, start_time DESC LIMIT @lim";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);
        if (date.HasValue)
            cmd.Parameters.AddWithValue("date", date.Value);
        if (!string.IsNullOrEmpty(status))
            cmd.Parameters.AddWithValue("status", status);

        var appointments = new List<AppointmentDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            appointments.Add(ReadAppointmentDto(reader));
        }
        return appointments;
    }

    public async Task<AppointmentDto?> GetAppointmentByIdAsync(
        int tenantId, long appointmentId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, slot_id, doctor_id, patient_name, patient_phone,
                   appointment_date, start_time, end_time, status,
                   reminder_48h_sent, reminder_2h_sent, cancel_reason, notes,
                   created_at, updated_at
            FROM appointments
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", appointmentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return ReadAppointmentDto(reader);
        return null;
    }

    public async Task<bool> CancelAppointmentAsync(
        int tenantId, long appointmentId, string? reason, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE appointments
            SET status = 'cancelled', cancel_reason = @reason
            WHERE tenant_id = @tid AND id = @id AND status = 'confirmed'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", appointmentId);
        cmd.Parameters.AddWithValue("reason", reason ?? (object)DBNull.Value);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    // ================================================================
    // Availability
    // ================================================================

    public async Task<int> CountConfirmedForSlotAsync(
        int tenantId, int slotId, DateOnly date, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM appointments
            WHERE tenant_id = @tid AND slot_id = @slotId AND appointment_date = @date AND status = 'confirmed'";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("slotId", slotId);
        cmd.Parameters.AddWithValue("date", date);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    // ================================================================
    // Reminders (scheduler queries)
    // ================================================================

    /// <summary>
    /// Get appointments that need T-48h reminder.
    /// Criteria: confirmed, date = today+2, reminder_48h_sent = false.
    /// </summary>
    public async Task<List<ReminderCandidate>> GetPending48hRemindersAsync(
        int batchSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT a.id, a.tenant_id, a.patient_name, a.patient_phone,
                   a.appointment_date, a.start_time, a.end_time,
                   tr.callback_url
            FROM appointments a
            INNER JOIN tenant_registry tr ON tr.tenant_id = a.tenant_id AND tr.is_active = TRUE
            WHERE a.status = 'confirmed'
              AND a.reminder_48h_sent = FALSE
              AND a.appointment_date = CURRENT_DATE + INTERVAL '2 days'
            ORDER BY a.appointment_date, a.start_time
            LIMIT @lim";

        return await ExecuteReminderQueryAsync(sql, batchSize, ct);
    }

    /// <summary>
    /// Get appointments that need T-2h reminder.
    /// Criteria: confirmed, date = today, start_time within 2 hours from now, reminder_2h_sent = false.
    /// </summary>
    public async Task<List<ReminderCandidate>> GetPending2hRemindersAsync(
        int batchSize = 50, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT a.id, a.tenant_id, a.patient_name, a.patient_phone,
                   a.appointment_date, a.start_time, a.end_time,
                   tr.callback_url
            FROM appointments a
            INNER JOIN tenant_registry tr ON tr.tenant_id = a.tenant_id AND tr.is_active = TRUE
            WHERE a.status = 'confirmed'
              AND a.reminder_2h_sent = FALSE
              AND a.appointment_date = CURRENT_DATE
              AND a.start_time > LOCALTIME
              AND a.start_time <= LOCALTIME + INTERVAL '2 hours'
            ORDER BY a.start_time
            LIMIT @lim";

        return await ExecuteReminderQueryAsync(sql, batchSize, ct);
    }

    public async Task MarkReminderSentAsync(
        int tenantId, long appointmentId, string reminderType, CancellationToken ct = default)
    {
        var column = reminderType switch
        {
            "48h" => "reminder_48h_sent",
            "2h" => "reminder_2h_sent",
            _ => throw new ArgumentException($"Unknown reminder type: {reminderType}", nameof(reminderType))
        };

        var sql = $"UPDATE appointments SET {column} = TRUE WHERE id = @id AND tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", appointmentId);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ================================================================
    // Tenant settings (for KVKK health check)
    // ================================================================

    public async Task<(string? settingsJson, string? sector)> GetTenantHealthInfoAsync(
        int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT settings_json::text, sector
            FROM tenant_registry
            WHERE tenant_id = @tid AND is_active = TRUE";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var settingsJson = reader.IsDBNull(0) ? null : reader.GetString(0);
            var sector = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (settingsJson, sector);
        }
        return (null, null);
    }

    // ================================================================
    // GR-3.19: Waitlist
    // ================================================================

    public async Task<int> InsertWaitlistAsync(
        int tenantId, WaitlistCreateRequest req, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO waitlist (tenant_id, patient_phone, patient_name,
                preferred_date, preferred_time, service_type, doctor_id,
                expires_at)
            VALUES (@tid, @phone, @name,
                @pdate, @ptime, @stype, @docId,
                NOW() + INTERVAL '14 days')
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", req.PatientPhone);
        cmd.Parameters.AddWithValue("name", req.PatientName);
        cmd.Parameters.AddWithValue("pdate",
            !string.IsNullOrEmpty(req.PreferredDate) ? DateOnly.Parse(req.PreferredDate) : DBNull.Value);
        cmd.Parameters.AddWithValue("ptime",
            !string.IsNullOrEmpty(req.PreferredTime) ? TimeOnly.Parse(req.PreferredTime) : DBNull.Value);
        cmd.Parameters.AddWithValue("stype", (object?)req.ServiceType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("docId", req.DoctorId.HasValue ? req.DoctorId.Value : DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<List<WaitlistDto>> GetWaitlistAsync(
        int tenantId, string? status = null, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, patient_phone, patient_name,
                   preferred_date::text, preferred_time::text, service_type, doctor_id,
                   status, notified_at, expires_at, created_at
            FROM waitlist
            WHERE tenant_id = @tid
              AND (@status IS NULL OR status = @status)
            ORDER BY created_at DESC LIMIT 200";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", (object?)status ?? DBNull.Value);

        var result = new List<WaitlistDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(ReadWaitlistDto(reader));
        }
        return result;
    }

    public async Task<bool> UpdateWaitlistStatusAsync(
        int tenantId, int id, string newStatus, CancellationToken ct = default)
    {
        var sql = @"
            UPDATE waitlist
            SET status = @status,
                notified_at = CASE WHEN @status = 'notified' THEN NOW() ELSE notified_at END
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", newStatus);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    /// <summary>
    /// Find waiting entries that match a cancelled appointment (date + doctor).
    /// Used by cancel endpoint to trigger waitlist notifications.
    /// </summary>
    public async Task<List<WaitlistDto>> FindMatchingWaitlistAsync(
        int tenantId, DateOnly appointmentDate, int? doctorId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, patient_phone, patient_name,
                   preferred_date::text, preferred_time::text, service_type, doctor_id,
                   status, notified_at, expires_at, created_at
            FROM waitlist
            WHERE tenant_id = @tid
              AND status = 'waiting'
              AND (preferred_date IS NULL OR preferred_date = @apptDate)
              AND (doctor_id IS NULL OR (@docId IS NOT NULL AND doctor_id = @docId))
            ORDER BY created_at ASC
            LIMIT 5";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("apptDate", appointmentDate);
        cmd.Parameters.AddWithValue("docId", doctorId.HasValue ? doctorId.Value : DBNull.Value);

        var result = new List<WaitlistDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(ReadWaitlistDto(reader));
        }
        return result;
    }

    /// <summary>
    /// Mark expired waitlist entries (status=waiting, expires_at past).
    /// Background timer job: processes all valid tenants via tenant_registry join.
    /// Returns count of expired entries.
    /// </summary>
    public async Task<int> ExpireWaitlistEntriesAsync(CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE waitlist
            SET status = 'expired'
            WHERE status = 'waiting'
              AND expires_at IS NOT NULL
              AND expires_at < NOW()
              AND tenant_id IN (SELECT tenant_id FROM tenant_registry)";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // ================================================================
    // GR-3.19: Service Pricing
    // ================================================================

    public async Task<List<ServicePricingDto>> GetPricingAsync(
        int tenantId, bool activeOnly = true, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, service_name, price_min, price_max, currency,
                   duration_minutes, description, is_active, created_at, updated_at
            FROM service_pricing
            WHERE tenant_id = @tid
              AND (@activeOnly = FALSE OR is_active = TRUE)
            ORDER BY service_name";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("activeOnly", activeOnly);

        var result = new List<ServicePricingDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ServicePricingDto
            {
                Id = reader.GetInt32(0),
                TenantId = reader.GetInt32(1),
                ServiceName = reader.GetString(2),
                PriceMin = reader.GetDecimal(3),
                PriceMax = reader.GetDecimal(4),
                Currency = reader.GetString(5),
                DurationMinutes = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsActive = reader.GetBoolean(8),
                CreatedAt = reader.GetDateTime(9),
                UpdatedAt = reader.GetDateTime(10)
            });
        }
        return result;
    }

    public async Task<int> CreatePricingAsync(
        int tenantId, PricingCreateRequest req, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO service_pricing (tenant_id, service_name, price_min, price_max, currency, duration_minutes, description)
            VALUES (@tid, @name, @pmin, @pmax, @currency, @duration, @desc)
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("name", req.ServiceName);
        cmd.Parameters.AddWithValue("pmin", req.PriceMin);
        cmd.Parameters.AddWithValue("pmax", req.PriceMax);
        cmd.Parameters.AddWithValue("currency", req.Currency);
        cmd.Parameters.AddWithValue("duration", req.DurationMinutes.HasValue ? req.DurationMinutes.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)req.Description ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is int id ? id : Convert.ToInt32(result);
    }

    public async Task<bool> UpdatePricingAsync(
        int tenantId, int id, PricingUpdateRequest req, CancellationToken ct = default)
    {
        if (req.ServiceName == null && !req.PriceMin.HasValue && !req.PriceMax.HasValue
            && req.Currency == null && !req.DurationMinutes.HasValue && req.Description == null
            && !req.IsActive.HasValue)
            return false;

        const string sql = @"
            UPDATE service_pricing
            SET service_name = COALESCE(@name, service_name),
                price_min = COALESCE(@pmin, price_min),
                price_max = COALESCE(@pmax, price_max),
                currency = COALESCE(@currency, currency),
                duration_minutes = COALESCE(@duration, duration_minutes),
                description = COALESCE(@desc, description),
                is_active = COALESCE(@active, is_active),
                updated_at = NOW()
            WHERE tenant_id = @tid AND id = @id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", (object?)req.ServiceName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pmin", req.PriceMin.HasValue ? req.PriceMin.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("pmax", req.PriceMax.HasValue ? req.PriceMax.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("currency", (object?)req.Currency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("duration", req.DurationMinutes.HasValue ? req.DurationMinutes.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("desc", (object?)req.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("active", req.IsActive.HasValue ? req.IsActive.Value : DBNull.Value);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ================================================================
    // GR-3.19: No-show stats
    // ================================================================

    public async Task<NoShowStatsDto> GetNoShowStatsAsync(
        int tenantId, string patientPhone, int threshold = 2, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT
                COUNT(*) FILTER (WHERE status = 'no_show') AS no_show_count,
                COUNT(*) AS total_appointments
            FROM appointments
            WHERE tenant_id = @tid AND patient_phone = @phone";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("phone", patientPhone);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var noShows = (int)reader.GetInt64(0);
            var total = (int)reader.GetInt64(1);
            return new NoShowStatsDto
            {
                PatientPhone = patientPhone,
                NoShowCount = noShows,
                TotalAppointments = total,
                NoShowRate = total > 0 ? Math.Round((double)noShows / total * 100, 1) : 0,
                ExceedsThreshold = noShows >= threshold,
                Threshold = threshold
            };
        }

        return new NoShowStatsDto
        {
            PatientPhone = patientPhone,
            Threshold = threshold
        };
    }

    // ================================================================
    // GR-3.19: Available slots with doctor filter
    // ================================================================

    public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(
        int tenantId, DateOnly date, int? doctorId, CancellationToken ct = default)
    {
        var dayOfWeek = (int)date.DayOfWeek;

        const string sql = @"
            SELECT s.id, s.start_time, s.end_time, s.max_bookings, s.doctor_id,
                   COALESCE(
                       (SELECT COUNT(*) FROM appointments a
                        WHERE a.slot_id = s.id AND a.appointment_date = @date AND a.status = 'confirmed'),
                       0
                   ) AS current_bookings
            FROM appointment_slots s
            WHERE s.tenant_id = @tid AND s.day_of_week = @dow AND s.is_active = TRUE
              AND (@docId IS NULL OR s.doctor_id = @docId)
            ORDER BY s.start_time";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("dow", (short)dayOfWeek);
        cmd.Parameters.AddWithValue("date", date);
        cmd.Parameters.AddWithValue("docId", doctorId.HasValue ? doctorId.Value : DBNull.Value);

        var slots = new List<AvailableSlotDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            slots.Add(new AvailableSlotDto
            {
                SlotId = reader.GetInt32(0),
                StartTime = reader.GetFieldValue<TimeOnly>(1).ToString("HH:mm"),
                EndTime = reader.GetFieldValue<TimeOnly>(2).ToString("HH:mm"),
                MaxBookings = reader.GetInt32(3),
                DoctorId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                CurrentBookings = reader.GetInt32(5)
            });
        }
        return slots;
    }

    // ================================================================
    // Private helpers
    // ================================================================

    private static WaitlistDto ReadWaitlistDto(NpgsqlDataReader reader)
    {
        return new WaitlistDto
        {
            Id = reader.GetInt32(0),
            TenantId = reader.GetInt32(1),
            PatientPhone = reader.GetString(2),
            PatientName = reader.GetString(3),
            PreferredDate = reader.IsDBNull(4) ? null : reader.GetString(4),
            PreferredTime = reader.IsDBNull(5) ? null : reader.GetString(5),
            ServiceType = reader.IsDBNull(6) ? null : reader.GetString(6),
            DoctorId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            Status = reader.GetString(8),
            NotifiedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            ExpiresAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            CreatedAt = reader.GetDateTime(11)
        };
    }

    private async Task<List<ReminderCandidate>> ExecuteReminderQueryAsync(
        string sql, int batchSize, CancellationToken ct)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("lim", batchSize);

        var candidates = new List<ReminderCandidate>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            candidates.Add(new ReminderCandidate
            {
                AppointmentId = reader.GetInt64(0),
                TenantId = reader.GetInt32(1),
                PatientName = reader.GetString(2),
                PatientPhone = reader.GetString(3),
                AppointmentDate = reader.GetFieldValue<DateOnly>(4),
                StartTime = reader.GetFieldValue<TimeOnly>(5),
                EndTime = reader.GetFieldValue<TimeOnly>(6),
                CallbackUrl = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return candidates;
    }

    private static SlotDto ReadSlotDto(NpgsqlDataReader reader)
    {
        return new SlotDto
        {
            Id = reader.GetInt32(0),
            TenantId = reader.GetInt32(1),
            DoctorId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            DayOfWeek = reader.GetInt16(3),
            StartTime = reader.GetFieldValue<TimeOnly>(4).ToString("HH:mm"),
            EndTime = reader.GetFieldValue<TimeOnly>(5).ToString("HH:mm"),
            MaxBookings = reader.GetInt32(6),
            IsActive = reader.GetBoolean(7),
            CreatedAt = reader.GetDateTime(8),
            UpdatedAt = reader.GetDateTime(9)
        };
    }

    private static AppointmentDto ReadAppointmentDto(NpgsqlDataReader reader)
    {
        return new AppointmentDto
        {
            Id = reader.GetInt64(0),
            TenantId = reader.GetInt32(1),
            SlotId = reader.GetInt32(2),
            DoctorId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            PatientName = reader.GetString(4),
            PatientPhone = reader.GetString(5),
            AppointmentDate = reader.GetFieldValue<DateOnly>(6).ToString("yyyy-MM-dd"),
            StartTime = reader.GetFieldValue<TimeOnly>(7).ToString("HH:mm"),
            EndTime = reader.GetFieldValue<TimeOnly>(8).ToString("HH:mm"),
            Status = reader.GetString(9),
            Reminder48hSent = reader.GetBoolean(10),
            Reminder2hSent = reader.GetBoolean(11),
            CancelReason = reader.IsDBNull(12) ? null : reader.GetString(12),
            Notes = reader.IsDBNull(13) ? null : reader.GetString(13),
            CreatedAt = reader.GetDateTime(14),
            UpdatedAt = reader.GetDateTime(15)
        };
    }
}
