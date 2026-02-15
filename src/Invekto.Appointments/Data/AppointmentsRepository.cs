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
        return (int)result!;
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

    public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(
        int tenantId, DateOnly date, CancellationToken ct = default)
    {
        var dayOfWeek = (int)date.DayOfWeek; // 0=Sunday matches PostgreSQL convention

        const string sql = @"
            SELECT s.id, s.start_time, s.end_time, s.max_bookings, s.doctor_id,
                   COALESCE(
                       (SELECT COUNT(*) FROM appointments a
                        WHERE a.slot_id = s.id AND a.appointment_date = @date AND a.status = 'confirmed'),
                       0
                   ) AS current_bookings
            FROM appointment_slots s
            WHERE s.tenant_id = @tid AND s.day_of_week = @dow AND s.is_active = TRUE
            ORDER BY s.start_time";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("dow", (short)dayOfWeek);
        cmd.Parameters.AddWithValue("date", date);

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
    // Private helpers
    // ================================================================

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
