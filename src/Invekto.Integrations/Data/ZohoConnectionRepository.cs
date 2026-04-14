// Adim 2 Paket A: persistence for Zoho OAuth connections (one row per tenant).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Invekto.Integrations.Services.Zoho;
using Invekto.Shared.Data;
using Npgsql;

namespace Invekto.Integrations.Data;

public sealed class ZohoConnectionRepository
{
    private readonly PostgresConnectionFactory _db;

    public ZohoConnectionRepository(PostgresConnectionFactory db)
    {
        _db = db;
    }

    /// <summary>Insert or update the tenant's Zoho connection (one row per tenant).</summary>
    public async Task<long> UpsertAsync(
        int tenantId,
        string region,
        string apiDomain,
        string accountsDomain,
        string encryptedRefreshToken,
        string grantedScopes,
        string? zohoUserEmail,
        CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO zoho_connections
                (tenant_id, region, api_domain, accounts_domain, refresh_token_enc, granted_scopes, zoho_user_email)
            VALUES
                (@tid, @region, @api, @accounts, @rtok, @scopes, @email)
            ON CONFLICT (tenant_id) DO UPDATE SET
                region            = EXCLUDED.region,
                api_domain        = EXCLUDED.api_domain,
                accounts_domain   = EXCLUDED.accounts_domain,
                refresh_token_enc = EXCLUDED.refresh_token_enc,
                granted_scopes    = EXCLUDED.granted_scopes,
                zoho_user_email   = EXCLUDED.zoho_user_email,
                updated_at        = NOW(),
                disconnected_at   = NULL
            RETURNING id";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("region", region);
        cmd.Parameters.AddWithValue("api", apiDomain);
        cmd.Parameters.AddWithValue("accounts", accountsDomain);
        cmd.Parameters.AddWithValue("rtok", encryptedRefreshToken);
        cmd.Parameters.AddWithValue("scopes", grantedScopes);
        cmd.Parameters.AddWithValue("email", (object?)zohoUserEmail ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Fetches the active connection for a tenant (returns null if absent or disconnected).</summary>
    public async Task<ZohoConnection?> GetActiveAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, tenant_id, region, api_domain, accounts_domain, refresh_token_enc,
                   granted_scopes, zoho_user_email, connected_at, updated_at, last_refreshed_at, disconnected_at
            FROM zoho_connections
            WHERE tenant_id = @tid AND disconnected_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return Read(reader);
    }

    /// <summary>Updates last_refreshed_at after a successful refresh_token grant.</summary>
    public async Task TouchRefreshedAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE zoho_connections
            SET last_refreshed_at = NOW(),
                updated_at        = NOW()
            WHERE tenant_id = @tid AND disconnected_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Replaces the encrypted refresh token (Zoho may rotate it on refresh response).</summary>
    public async Task UpdateRefreshTokenAsync(int tenantId, string newEncryptedRefreshToken, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE zoho_connections
            SET refresh_token_enc = @rtok,
                last_refreshed_at = NOW(),
                updated_at        = NOW()
            WHERE tenant_id = @tid AND disconnected_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("rtok", newEncryptedRefreshToken);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Soft-disconnects the tenant's Zoho connection (no row deletion).</summary>
    public async Task DisconnectAsync(int tenantId, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE zoho_connections
            SET disconnected_at = NOW(),
                updated_at      = NOW()
            WHERE tenant_id = @tid AND disconnected_at IS NULL";

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd  = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ZohoConnection Read(NpgsqlDataReader reader)
    {
        return new ZohoConnection(
            Id:                    reader.GetInt64(0),
            TenantId:              reader.GetInt32(1),
            Region:                reader.GetString(2),
            ApiDomain:             reader.GetString(3),
            AccountsDomain:        reader.GetString(4),
            EncryptedRefreshToken: reader.GetString(5),
            GrantedScopes:         reader.GetString(6),
            ZohoUserEmail:         reader.IsDBNull(7) ? null : reader.GetString(7),
            ConnectedAt:           reader.GetFieldValue<DateTime>(8),
            UpdatedAt:             reader.GetFieldValue<DateTime>(9),
            LastRefreshedAt:       reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTime>(10),
            DisconnectedAt:        reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTime>(11));
    }
}
