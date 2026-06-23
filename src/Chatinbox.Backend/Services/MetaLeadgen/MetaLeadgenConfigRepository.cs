using System.Security.Cryptography;
using System.Text.Json;
using Chatinbox.Shared.Contracts.MetaLeadgen;
using Chatinbox.Shared.Data;

namespace Chatinbox.Backend.Services.MetaLeadgen;

/// <summary>
/// Paket B-META: repository over <c>tenant_settings.meta_leadgen_config JSONB</c>.
/// GET surfaces the full DTO (Dashboard editor round-trips secrets as masked
/// last-4-style placeholders server-side — operators type the fresh secret when
/// they want to change it). PUT accepts a DTO with null-means-keep semantic: the
/// Dashboard submits null for secret fields the operator did NOT retype, and we
/// merge the existing stored value back in before writing the JSONB.
///
/// Microservice isolation: Backend owns this CRUD; Automation NEVER reads this
/// table directly. Automation goes through the <c>/api/internal/meta-leadgen/*</c>
/// internal HTTP endpoints so tenant secrets stay in Backend-process memory only.
/// </summary>
public class MetaLeadgenConfigRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly PostgresConnectionFactory _db;

    public MetaLeadgenConfigRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns the parsed config + <c>updated_at</c>. Missing row (tenant never
    /// configured) or NULL/{} JSONB yields <c>(Empty, null)</c>. Malformed stored
    /// JSON throws <see cref="JsonException"/> — caller maps to a
    /// state-corruption ErrorResponse so an operator is alerted to reseed.
    /// </summary>
    public virtual async Task<(MetaLeadgenConfigDto Config, DateTime? UpdatedAt)> GetConfigAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT meta_leadgen_config::text, updated_at
            FROM tenant_settings
            WHERE tenant_id = @tid
            LIMIT 1";
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return (MetaLeadgenConfigDto.Empty(), null);

        var json = reader.IsDBNull(0) ? "{}" : reader.GetString(0);
        var updatedAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);

        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return (MetaLeadgenConfigDto.Empty(), updatedAt);

        var parsed = JsonSerializer.Deserialize<MetaLeadgenConfigDto>(json, JsonOpts)
                     ?? MetaLeadgenConfigDto.Empty();
        parsed.FieldIdMap ??= new Dictionary<string, string>();
        return (parsed, updatedAt);
    }

    /// <summary>
    /// UPSERTs the full config. Caller is responsible for merging
    /// null-means-keep secret fields upstream (see
    /// <c>MetaLeadgenEndpoints</c> PUT handler). Returns post-write
    /// <c>updated_at</c>. Serialization uses System.Text.Json defaults so the
    /// snake_case property names from the DTO land verbatim in JSONB.
    /// </summary>
    public virtual async Task<DateTime> UpsertConfigAsync(
        int tenantId, MetaLeadgenConfigDto config, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(config);

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO tenant_settings (tenant_id, meta_leadgen_config, updated_at)
            VALUES (@tid, @json::jsonb, NOW())
            ON CONFLICT (tenant_id) DO UPDATE
                SET meta_leadgen_config = EXCLUDED.meta_leadgen_config,
                    updated_at = NOW()
            RETURNING updated_at";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("json", json);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is DateTime dt ? dt : DateTime.UtcNow;
    }

    /// <summary>
    /// Rotates ONLY the <c>verify_token</c> field, preserving all other config
    /// keys. Used by the Dashboard [Rotate Verify Token] button. Returns the
    /// freshly generated 32-char token + post-write <c>updated_at</c>.
    ///
    /// Server-generated (not client-generated) so operators cannot accidentally
    /// supply a weak token. Base64url of 24 random bytes = 32 URL-safe chars
    /// without '=' padding.
    /// </summary>
    public virtual async Task<(string NewToken, DateTime UpdatedAt)> RotateVerifyTokenAsync(
        int tenantId, CancellationToken ct = default)
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var newToken = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        await using var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        // jsonb_set with create_missing=true works on both fresh rows (empty {})
        // and existing rows. INSERT-first ensures a tenant that has never hit
        // the PUT endpoint can still rotate from a fresh state.
        cmd.CommandText = @"
            INSERT INTO tenant_settings (tenant_id, meta_leadgen_config, updated_at)
            VALUES (@tid, jsonb_build_object('verify_token', @tok::text), NOW())
            ON CONFLICT (tenant_id) DO UPDATE
                SET meta_leadgen_config =
                        jsonb_set(
                            COALESCE(tenant_settings.meta_leadgen_config, '{}'::jsonb),
                            '{verify_token}',
                            to_jsonb(@tok::text),
                            true),
                    updated_at = NOW()
            RETURNING updated_at";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tok", newToken);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        var updatedAt = result is DateTime dt ? dt : DateTime.UtcNow;
        return (newToken, updatedAt);
    }
}
