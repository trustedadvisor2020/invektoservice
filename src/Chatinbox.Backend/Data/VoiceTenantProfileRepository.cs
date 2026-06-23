using Chatinbox.Shared.Data;
using Chatinbox.Shared.DTOs.Voice;
using Chatinbox.Shared.Logging;
using Npgsql;

namespace Chatinbox.Backend.Data;

/// <summary>
/// FEAT-VFB F-VR-B: PostgreSQL repository for voice_tenant_profile (arch/db/voice-tenant-profile.sql).
/// Read-only here — writes are admin-controls scope (§17, later phase). Thread-safe, singleton.
///
/// NOT tenant-scoped (ops endpoint reads any tenant by id) — same posture as
/// <see cref="TenantRegistryRepository"/>.
/// </summary>
public class VoiceTenantProfileRepository
{
    private readonly PostgresConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public VoiceTenantProfileRepository(PostgresConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns the stored voice profile for a tenant, or null if no row exists. The caller
    /// (endpoint) decides the not-found policy — F-VR-B synthesizes a 'generic' default rather
    /// than 404 so voice never breaks for a not-yet-provisioned tenant.
    /// </summary>
    public virtual async Task<VoiceTenantProfileDto?> GetAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT tenant_id, business_type, default_language, voice_brand_tone,
                   primary_goal, secondary_goal, pricing_enabled, appointment_enabled,
                   order_lookup_enabled, lead_capture_enabled, human_handoff_enabled,
                   sensitive_topics_enabled
            FROM voice_tenant_profile
            WHERE tenant_id = @tid";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new VoiceTenantProfileDto
        {
            TenantId = reader.GetInt32(0),
            BusinessType = reader.GetString(1),
            DefaultLanguage = reader.GetString(2),
            VoiceBrandTone = reader.IsDBNull(3) ? null : reader.GetString(3),
            PrimaryGoal = reader.IsDBNull(4) ? null : reader.GetString(4),
            SecondaryGoal = reader.IsDBNull(5) ? null : reader.GetString(5),
            PricingEnabled = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
            AppointmentEnabled = reader.IsDBNull(7) ? null : reader.GetBoolean(7),
            OrderLookupEnabled = reader.IsDBNull(8) ? null : reader.GetBoolean(8),
            LeadCaptureEnabled = reader.IsDBNull(9) ? null : reader.GetBoolean(9),
            HumanHandoffEnabled = reader.IsDBNull(10) ? null : reader.GetBoolean(10),
            SensitiveTopicsEnabled = reader.IsDBNull(11) ? null : reader.GetBoolean(11),
        };
    }
}
