using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Invekto.Shared.DTOs.Templates;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Data;

public class TemplateRepository
{
    private readonly KnowledgeConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public TemplateRepository(KnowledgeConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // Catalog CRUD
    // ============================================================

    public virtual async Task<TemplateCatalogDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, template_type, scope, sector, tenant_id, parent_template_id,
                   slug, name, description, lang, tags, content_json, version,
                   is_active, is_published, usage_count, confidence_score, source_count,
                   created_by, created_at, updated_at
            FROM template_catalog WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return ReadCatalogDto(r);
    }

    public virtual async Task<(List<TemplateCatalogDto> Items, int Total)> ListAsync(
        TemplateCatalogFilter f, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        var where = "WHERE is_active = true";
        var parms = new List<NpgsqlParameter>();

        if (!string.IsNullOrEmpty(f.Scope))
        {
            where += " AND scope = @scope";
            parms.Add(new NpgsqlParameter("scope", f.Scope));
        }
        if (!string.IsNullOrEmpty(f.TemplateType))
        {
            where += " AND template_type = @ttype";
            parms.Add(new NpgsqlParameter("ttype", f.TemplateType));
        }
        if (!string.IsNullOrEmpty(f.Sector))
        {
            where += " AND sector = @sector";
            parms.Add(new NpgsqlParameter("sector", f.Sector));
        }
        if (!string.IsNullOrEmpty(f.Lang))
        {
            where += " AND lang = @lang";
            parms.Add(new NpgsqlParameter("lang", f.Lang));
        }
        if (!string.IsNullOrEmpty(f.Search))
        {
            where += " AND (slug ILIKE @search OR name ILIKE @search OR description ILIKE @search)";
            parms.Add(new NpgsqlParameter("search", $"%{f.Search}%"));
        }
        if (f.Tags is { Length: > 0 })
        {
            where += " AND tags && @tags";
            parms.Add(new NpgsqlParameter("tags", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = f.Tags });
        }

        // Count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM template_catalog {where}";
        foreach (var p in parms) countCmd.Parameters.Add(p.Clone());
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // List
        var offset = (f.Page - 1) * f.Limit;
        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $@"
            SELECT id, template_type, scope, sector, tenant_id, parent_template_id,
                   slug, name, description, lang, tags, content_json, version,
                   is_active, is_published, usage_count, confidence_score, source_count,
                   created_by, created_at, updated_at
            FROM template_catalog {where}
            ORDER BY usage_count DESC, created_at DESC
            LIMIT @limit OFFSET @offset";
        foreach (var p in parms) listCmd.Parameters.Add(p.Clone());
        listCmd.Parameters.AddWithValue("limit", f.Limit);
        listCmd.Parameters.AddWithValue("offset", offset);

        var items = new List<TemplateCatalogDto>();
        await using var r = await listCmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            items.Add(ReadCatalogDto(r));

        return (items, total);
    }

    public virtual async Task<int> InsertAsync(TemplateCreateRequest req, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_catalog
                (template_type, scope, sector, tenant_id, parent_template_id,
                 slug, name, description, lang, tags, content_json, created_by)
            VALUES
                (@type, @scope, @sector, @tid, @parent,
                 @slug, @name, @desc, @lang, @tags, @content::jsonb, @created_by)
            RETURNING id";

        cmd.Parameters.AddWithValue("type", req.TemplateType);
        cmd.Parameters.AddWithValue("scope", req.Scope);
        cmd.Parameters.Add(new NpgsqlParameter("sector", NpgsqlDbType.Varchar) { Value = req.Sector ?? (object)DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("tid", NpgsqlDbType.Integer) { Value = req.TenantId.HasValue ? req.TenantId.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("parent", NpgsqlDbType.Integer) { Value = req.ParentTemplateId.HasValue ? req.ParentTemplateId.Value : DBNull.Value });
        cmd.Parameters.AddWithValue("slug", req.Slug);
        cmd.Parameters.AddWithValue("name", req.Name);
        cmd.Parameters.Add(new NpgsqlParameter("desc", NpgsqlDbType.Text) { Value = req.Description ?? (object)DBNull.Value });
        cmd.Parameters.AddWithValue("lang", req.Lang);
        cmd.Parameters.Add(new NpgsqlParameter("tags", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = req.Tags ?? Array.Empty<string>() });
        cmd.Parameters.AddWithValue("content", JsonSerializer.Serialize(req.ContentJson));
        cmd.Parameters.AddWithValue("created_by", req.CreatedBy);

        var id = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));

        // Insert initial version
        await InsertVersionInternalAsync(conn, id, 1, req.ContentJson, "Initial version", req.CreatedBy, ct);

        return id;
    }

    public virtual async Task<bool> UpdateAsync(int id, TemplateUpdateRequest req, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Get current version
        await using var getCmd = conn.CreateCommand();
        getCmd.CommandText = "SELECT version FROM template_catalog WHERE id = @id AND is_active = true";
        getCmd.Parameters.AddWithValue("id", id);
        var currentVersion = await getCmd.ExecuteScalarAsync(ct);
        if (currentVersion == null) return false;

        var newVersion = Convert.ToInt32(currentVersion) + 1;

        var sets = new List<string> { "version = @ver" };
        var parms = new List<NpgsqlParameter>
        {
            new("ver", newVersion),
            new("id", id)
        };

        if (req.Name != null) { sets.Add("name = @name"); parms.Add(new("name", req.Name)); }
        if (req.Description != null) { sets.Add("description = @desc"); parms.Add(new("desc", req.Description)); }
        if (req.Tags != null) { sets.Add("tags = @tags"); parms.Add(new NpgsqlParameter("tags", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = req.Tags }); }
        if (req.ContentJson != null) { sets.Add("content_json = @content::jsonb"); parms.Add(new("content", JsonSerializer.Serialize(req.ContentJson))); }
        if (req.IsPublished.HasValue) { sets.Add("is_published = @pub"); parms.Add(new("pub", req.IsPublished.Value)); }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"UPDATE template_catalog SET {string.Join(", ", sets)} WHERE id = @id AND is_active = true";
        foreach (var p in parms) cmd.Parameters.Add(p);

        var rows = await cmd.ExecuteNonQueryAsync(ct);
        if (rows == 0) return false;

        // Insert version record
        var contentForVersion = req.ContentJson;
        if (contentForVersion == null)
        {
            await using var readCmd = conn.CreateCommand();
            readCmd.CommandText = "SELECT content_json FROM template_catalog WHERE id = @id";
            readCmd.Parameters.AddWithValue("id", id);
            contentForVersion = await readCmd.ExecuteScalarAsync(ct);
        }
        await InsertVersionInternalAsync(conn, id, newVersion, contentForVersion ?? "{}", req.ChangeSummary, "manual", ct);

        return true;
    }

    public virtual async Task<bool> SoftDeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE template_catalog SET is_active = false WHERE id = @id AND is_active = true";
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public virtual async Task<bool> PublishAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE template_catalog SET is_published = true WHERE id = @id AND is_active = true";
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // Resolution — 3-tier hierarchy: tenant > sector > platform
    // ============================================================

    public virtual async Task<TemplateCatalogDto?> ResolveAsync(
        int tenantId, string slug, string lang, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Step 1: Tenant scope
        var result = await ResolveAtScopeAsync(conn, slug, lang, "tenant", null, tenantId, ct);
        if (result != null) return result;

        // Get tenant sector
        string? sector = null;
        await using var sectorCmd = conn.CreateCommand();
        sectorCmd.CommandText = "SELECT sector FROM tenant_registry WHERE tenant_id = @tid";
        sectorCmd.Parameters.AddWithValue("tid", tenantId);
        var sectorObj = await sectorCmd.ExecuteScalarAsync(ct);
        if (sectorObj is string s) sector = s;

        // Step 2: Sector scope
        if (!string.IsNullOrEmpty(sector))
        {
            result = await ResolveAtScopeAsync(conn, slug, lang, "sector", sector, null, ct);
            if (result != null) return result;
        }

        // Step 3: Platform scope
        result = await ResolveAtScopeAsync(conn, slug, lang, "platform", null, null, ct);
        if (result != null) return result;

        // Step 4: Language fallback to 'tr'
        if (lang != "tr")
        {
            result = await ResolveAtScopeAsync(conn, slug, "tr", "tenant", null, tenantId, ct);
            if (result != null) return result;

            if (!string.IsNullOrEmpty(sector))
            {
                result = await ResolveAtScopeAsync(conn, slug, "tr", "sector", sector, null, ct);
                if (result != null) return result;
            }

            result = await ResolveAtScopeAsync(conn, slug, "tr", "platform", null, null, ct);
        }

        return result;
    }

    public virtual async Task<List<TemplateCatalogDto>> GetAvailableAsync(
        int tenantId, string? templateType, string? lang, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Get tenant sector
        string? sector = null;
        await using var sectorCmd = conn.CreateCommand();
        sectorCmd.CommandText = "SELECT sector FROM tenant_registry WHERE tenant_id = @tid";
        sectorCmd.Parameters.AddWithValue("tid", tenantId);
        var sectorObj = await sectorCmd.ExecuteScalarAsync(ct);
        if (sectorObj is string s) sector = s;

        var where = "WHERE is_active = true AND is_published = true";
        var scopeFilter = "(scope = 'platform'";
        if (!string.IsNullOrEmpty(sector))
            scopeFilter += " OR (scope = 'sector' AND sector = @sector)";
        scopeFilter += $" OR (scope = 'tenant' AND tenant_id = @tid))";
        where += $" AND ({scopeFilter})";

        if (!string.IsNullOrEmpty(templateType))
            where += " AND template_type = @ttype";
        if (!string.IsNullOrEmpty(lang))
            where += " AND lang = @lang";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT id, template_type, scope, sector, tenant_id, parent_template_id,
                   slug, name, description, lang, tags, content_json, version,
                   is_active, is_published, usage_count, confidence_score, source_count,
                   created_by, created_at, updated_at
            FROM template_catalog {where}
            ORDER BY scope DESC, usage_count DESC";
        cmd.Parameters.AddWithValue("tid", tenantId);
        if (!string.IsNullOrEmpty(sector))
            cmd.Parameters.AddWithValue("sector", sector);
        if (!string.IsNullOrEmpty(templateType))
            cmd.Parameters.AddWithValue("ttype", templateType);
        if (!string.IsNullOrEmpty(lang))
            cmd.Parameters.AddWithValue("lang", lang);

        var items = new List<TemplateCatalogDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            items.Add(ReadCatalogDto(r));
        return items;
    }

    // ============================================================
    // Suggestions
    // ============================================================

    public virtual async Task<int> InsertSuggestionAsync(
        int analysisId, string suggestionType, int? existingTemplateId,
        decimal? similarityScore, object suggestedContent, string suggestedSlug,
        string suggestedName, string suggestedType, object? sourceData,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_suggestions
                (analysis_id, suggestion_type, existing_template_id, similarity_score,
                 suggested_content_json, suggested_slug, suggested_name, suggested_type,
                 source_data_json)
            VALUES (@aid, @stype, @eid, @sim, @content::jsonb, @slug, @name, @ttype, @source::jsonb)
            RETURNING id";

        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("stype", suggestionType);
        cmd.Parameters.Add(new NpgsqlParameter("eid", NpgsqlDbType.Integer) { Value = existingTemplateId.HasValue ? existingTemplateId.Value : DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("sim", NpgsqlDbType.Numeric) { Value = similarityScore.HasValue ? similarityScore.Value : DBNull.Value });
        cmd.Parameters.AddWithValue("content", JsonSerializer.Serialize(suggestedContent));
        cmd.Parameters.AddWithValue("slug", suggestedSlug);
        cmd.Parameters.AddWithValue("name", suggestedName);
        cmd.Parameters.AddWithValue("ttype", suggestedType);
        cmd.Parameters.AddWithValue("source", JsonSerializer.Serialize(sourceData ?? new { }));

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    public virtual async Task<(List<TemplateSuggestionDto> Items, int Total)> ListSuggestionsAsync(
        int? analysisId, string? status, int page, int limit, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        var where = "WHERE 1=1";
        var parms = new List<NpgsqlParameter>();
        if (analysisId.HasValue) { where += " AND s.analysis_id = @aid"; parms.Add(new("aid", analysisId.Value)); }
        if (!string.IsNullOrEmpty(status)) { where += " AND s.status = @status"; parms.Add(new("status", status)); }

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM template_suggestions s {where}";
        foreach (var p in parms) countCmd.Parameters.Add(p.Clone());
        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        var offset = (page - 1) * limit;
        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = $@"
            SELECT s.id, s.analysis_id, s.suggestion_type, s.existing_template_id,
                   tc.name AS existing_name, s.similarity_score,
                   s.suggested_content_json, s.suggested_slug, s.suggested_name,
                   s.suggested_type, s.source_data_json, s.status,
                   s.reviewed_by, s.reviewed_at, s.result_template_id, s.created_at
            FROM template_suggestions s
            LEFT JOIN template_catalog tc ON tc.id = s.existing_template_id
            {where}
            ORDER BY s.created_at DESC
            LIMIT @limit OFFSET @offset";
        foreach (var p in parms) listCmd.Parameters.Add(p.Clone());
        listCmd.Parameters.AddWithValue("limit", limit);
        listCmd.Parameters.AddWithValue("offset", offset);

        var items = new List<TemplateSuggestionDto>();
        await using var r = await listCmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            items.Add(ReadSuggestionDto(r));

        return (items, total);
    }

    public virtual async Task<TemplateSuggestionDto?> GetSuggestionByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT s.id, s.analysis_id, s.suggestion_type, s.existing_template_id,
                   tc.name AS existing_name, s.similarity_score,
                   s.suggested_content_json, s.suggested_slug, s.suggested_name,
                   s.suggested_type, s.source_data_json, s.status,
                   s.reviewed_by, s.reviewed_at, s.result_template_id, s.created_at
            FROM template_suggestions s
            LEFT JOIN template_catalog tc ON tc.id = s.existing_template_id
            WHERE s.id = @id";
        cmd.Parameters.AddWithValue("id", id);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return ReadSuggestionDto(r);
    }

    public virtual async Task<bool> UpdateSuggestionStatusAsync(
        int id, string status, string? reviewedBy, int? resultTemplateId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE template_suggestions
            SET status = @status, reviewed_by = @rby, reviewed_at = NOW(), result_template_id = @rtid
            WHERE id = @id";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.Add(new NpgsqlParameter("rby", NpgsqlDbType.Varchar) { Value = reviewedBy ?? (object)DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("rtid", NpgsqlDbType.Integer) { Value = resultTemplateId.HasValue ? resultTemplateId.Value : DBNull.Value });
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ============================================================
    // Sources (provenance)
    // ============================================================

    public virtual async Task InsertSourceAsync(
        int templateId, int analysisId, string tenantName,
        string contributionType, int sampleCount, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_sources (template_id, analysis_id, tenant_name, contribution_type, sample_count)
            VALUES (@tid, @aid, @tname, @ctype, @cnt)
            ON CONFLICT (template_id, analysis_id) DO UPDATE
            SET sample_count = EXCLUDED.sample_count, contribution_type = EXCLUDED.contribution_type";
        cmd.Parameters.AddWithValue("tid", templateId);
        cmd.Parameters.AddWithValue("aid", analysisId);
        cmd.Parameters.AddWithValue("tname", tenantName);
        cmd.Parameters.AddWithValue("ctype", contributionType);
        cmd.Parameters.AddWithValue("cnt", sampleCount);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public virtual async Task<List<TemplateSourceDto>> GetSourcesAsync(int templateId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, template_id, analysis_id, tenant_name, contribution_type, sample_count, contributed_at
            FROM template_sources WHERE template_id = @tid ORDER BY sample_count DESC";
        cmd.Parameters.AddWithValue("tid", templateId);

        var items = new List<TemplateSourceDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            items.Add(new TemplateSourceDto
            {
                Id = r.GetInt32(0),
                TemplateId = r.GetInt32(1),
                AnalysisId = r.GetInt32(2),
                TenantName = r.GetString(3),
                ContributionType = r.GetString(4),
                SampleCount = r.GetInt32(5),
                ContributedAt = r.GetDateTime(6)
            });
        }
        return items;
    }

    public virtual async Task IncrementSourceCountAsync(int templateId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE template_catalog SET source_count = source_count + 1 WHERE id = @id";
        cmd.Parameters.AddWithValue("id", templateId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ============================================================
    // Adoptions
    // ============================================================

    public virtual async Task<int> InsertAdoptionAsync(
        int tenantId, int templateId, int adoptedVersion,
        string targetType, int? targetId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_adoptions (tenant_id, template_id, adopted_version, target_type, target_id)
            VALUES (@tid, @tmid, @ver, @ttype, @targid)
            ON CONFLICT (tenant_id, template_id) DO NOTHING
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("tmid", templateId);
        cmd.Parameters.AddWithValue("ver", adoptedVersion);
        cmd.Parameters.AddWithValue("ttype", targetType);
        cmd.Parameters.Add(new NpgsqlParameter("targid", NpgsqlDbType.Integer) { Value = targetId.HasValue ? targetId.Value : DBNull.Value });

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public virtual async Task<List<TemplateAdoptionDto>> ListAdoptionsAsync(
        int tenantId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.id, a.tenant_id, a.template_id, tc.name, tc.template_type,
                   a.adopted_version, a.target_type, a.target_id, a.customized, a.adopted_at
            FROM template_adoptions a
            JOIN template_catalog tc ON tc.id = a.template_id
            WHERE a.tenant_id = @tid
            ORDER BY a.adopted_at DESC";
        cmd.Parameters.AddWithValue("tid", tenantId);

        var items = new List<TemplateAdoptionDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            items.Add(new TemplateAdoptionDto
            {
                Id = r.GetInt32(0),
                TenantId = r.GetInt32(1),
                TemplateId = r.GetInt32(2),
                TemplateName = r.IsDBNull(3) ? null : r.GetString(3),
                TemplateType = r.IsDBNull(4) ? null : r.GetString(4),
                AdoptedVersion = r.GetInt32(5),
                TargetType = r.GetString(6),
                TargetId = r.IsDBNull(7) ? null : r.GetInt32(7),
                Customized = r.GetBoolean(8),
                AdoptedAt = r.GetDateTime(9)
            });
        }
        return items;
    }

    // ============================================================
    // Performance
    // ============================================================

    public virtual async Task UpsertPerformanceAsync(
        int templateId, int tenantId, string variant,
        string metricType, decimal metricValue, DateTime periodStart,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_performance (template_id, tenant_id, variant, metric_type, metric_value, period_start)
            VALUES (@tmid, @tid, @var, @mtype, @mval, @pstart)
            ON CONFLICT (template_id, tenant_id, variant, metric_type, period_start)
            DO UPDATE SET metric_value = template_performance.metric_value + EXCLUDED.metric_value";
        cmd.Parameters.AddWithValue("tmid", templateId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("var", variant);
        cmd.Parameters.AddWithValue("mtype", metricType);
        cmd.Parameters.AddWithValue("mval", metricValue);
        cmd.Parameters.AddWithValue("pstart", periodStart.Date);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ============================================================
    // Batch operations
    // ============================================================

    public virtual async Task<int> BatchInsertTemplatesAsync(
        List<TemplateCreateRequest> templates, CancellationToken ct = default)
    {
        if (templates.Count == 0) return 0;
        await using var conn = await _db.OpenConnectionAsync(ct);
        var count = 0;

        await using var batch = new NpgsqlBatch(conn);
        foreach (var req in templates)
        {
            var cmd = new NpgsqlBatchCommand(@"
                INSERT INTO template_catalog
                    (template_type, scope, sector, tenant_id, slug, name, description,
                     lang, tags, content_json, is_published, created_by)
                VALUES
                    ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10::jsonb, $11, $12)
                ON CONFLICT ON CONSTRAINT uq_tc_slug DO NOTHING");

            cmd.Parameters.AddWithValue(req.TemplateType);
            cmd.Parameters.AddWithValue(req.Scope);
            cmd.Parameters.Add(new NpgsqlParameter { Value = req.Sector ?? (object)DBNull.Value, NpgsqlDbType = NpgsqlDbType.Varchar });
            cmd.Parameters.Add(new NpgsqlParameter { Value = req.TenantId.HasValue ? req.TenantId.Value : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer });
            cmd.Parameters.AddWithValue(req.Slug);
            cmd.Parameters.AddWithValue(req.Name);
            cmd.Parameters.Add(new NpgsqlParameter { Value = req.Description ?? (object)DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });
            cmd.Parameters.AddWithValue(req.Lang);
            cmd.Parameters.Add(new NpgsqlParameter { Value = req.Tags ?? Array.Empty<string>(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
            cmd.Parameters.AddWithValue(JsonSerializer.Serialize(req.ContentJson));
            cmd.Parameters.AddWithValue(true); // is_published
            cmd.Parameters.AddWithValue(req.CreatedBy);

            batch.BatchCommands.Add(cmd);
        }

        count = await batch.ExecuteNonQueryAsync(ct);
        return count;
    }

    /// <summary>
    /// Gets all published templates matching scope + sector + type for comparison.
    /// Used by TemplateExtractorService to find existing templates for similarity matching.
    /// </summary>
    public virtual async Task<List<TemplateCatalogDto>> GetPublishedForComparisonAsync(
        string templateType, string? sector, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var where = "WHERE is_active = true AND is_published = true AND template_type = @ttype";
        if (!string.IsNullOrEmpty(sector))
            where += " AND (scope = 'platform' OR (scope = 'sector' AND sector = @sector))";
        else
            where += " AND scope = 'platform'";

        cmd.CommandText = $@"
            SELECT id, template_type, scope, sector, tenant_id, parent_template_id,
                   slug, name, description, lang, tags, content_json, version,
                   is_active, is_published, usage_count, confidence_score, source_count,
                   created_by, created_at, updated_at
            FROM template_catalog {where}
            ORDER BY usage_count DESC";
        cmd.Parameters.AddWithValue("ttype", templateType);
        if (!string.IsNullOrEmpty(sector))
            cmd.Parameters.AddWithValue("sector", sector);

        var items = new List<TemplateCatalogDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            items.Add(ReadCatalogDto(r));
        return items;
    }

    // ============================================================
    // Versions
    // ============================================================

    public virtual async Task<List<TemplateVersionDto>> GetVersionHistoryAsync(
        int templateId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, template_id, version, content_json, change_summary, changed_by, created_at
            FROM template_versions WHERE template_id = @tid ORDER BY version DESC";
        cmd.Parameters.AddWithValue("tid", templateId);

        var items = new List<TemplateVersionDto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            items.Add(new TemplateVersionDto
            {
                Id = r.GetInt64(0),
                TemplateId = r.GetInt32(1),
                Version = r.GetInt32(2),
                ContentJson = r.IsDBNull(3) ? null : JsonSerializer.Deserialize<object>(r.GetString(3)),
                ChangeSummary = r.IsDBNull(4) ? null : r.GetString(4),
                ChangedBy = r.GetString(5),
                CreatedAt = r.GetDateTime(6)
            });
        }
        return items;
    }

    // ============================================================
    // Private helpers
    // ============================================================

    private static async Task InsertVersionInternalAsync(
        NpgsqlConnection conn, int templateId, int version, object contentJson,
        string? changeSummary, string changedBy, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO template_versions (template_id, version, content_json, change_summary, changed_by)
            VALUES (@tid, @ver, @content::jsonb, @summary, @by)
            ON CONFLICT (template_id, version) DO NOTHING";
        cmd.Parameters.AddWithValue("tid", templateId);
        cmd.Parameters.AddWithValue("ver", version);
        cmd.Parameters.AddWithValue("content", contentJson is string s ? s : JsonSerializer.Serialize(contentJson));
        cmd.Parameters.Add(new NpgsqlParameter("summary", NpgsqlDbType.Varchar) { Value = changeSummary ?? (object)DBNull.Value });
        cmd.Parameters.AddWithValue("by", changedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<TemplateCatalogDto?> ResolveAtScopeAsync(
        NpgsqlConnection conn, string slug, string lang, string scope,
        string? sector, int? tenantId, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        var where = "WHERE slug = @slug AND lang = @lang AND scope = @scope AND is_active = true AND is_published = true";
        if (scope == "sector")
            where += " AND sector = @sector";
        else if (scope == "tenant")
            where += " AND tenant_id = @tid";

        cmd.CommandText = $@"
            SELECT id, template_type, scope, sector, tenant_id, parent_template_id,
                   slug, name, description, lang, tags, content_json, version,
                   is_active, is_published, usage_count, confidence_score, source_count,
                   created_by, created_at, updated_at
            FROM template_catalog {where}
            ORDER BY version DESC LIMIT 1";

        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("lang", lang);
        cmd.Parameters.AddWithValue("scope", scope);
        if (scope == "sector")
            cmd.Parameters.Add(new NpgsqlParameter("sector", NpgsqlDbType.Varchar) { Value = sector ?? (object)DBNull.Value });
        if (scope == "tenant")
            cmd.Parameters.Add(new NpgsqlParameter("tid", NpgsqlDbType.Integer) { Value = tenantId.HasValue ? tenantId.Value : DBNull.Value });

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return ReadCatalogDto(r);
    }

    private static TemplateCatalogDto ReadCatalogDto(NpgsqlDataReader r)
    {
        return new TemplateCatalogDto
        {
            Id = r.GetInt32(0),
            TemplateType = r.GetString(1),
            Scope = r.GetString(2),
            Sector = r.IsDBNull(3) ? null : r.GetString(3),
            TenantId = r.IsDBNull(4) ? null : r.GetInt32(4),
            ParentTemplateId = r.IsDBNull(5) ? null : r.GetInt32(5),
            Slug = r.GetString(6),
            Name = r.GetString(7),
            Description = r.IsDBNull(8) ? null : r.GetString(8),
            Lang = r.GetString(9),
            Tags = r.IsDBNull(10) ? [] : (string[])r.GetValue(10),
            ContentJson = r.IsDBNull(11) ? null : JsonSerializer.Deserialize<object>(r.GetString(11)),
            Version = r.GetInt32(12),
            IsActive = r.GetBoolean(13),
            IsPublished = r.GetBoolean(14),
            UsageCount = r.GetInt32(15),
            ConfidenceScore = r.GetDecimal(16),
            SourceCount = r.GetInt32(17),
            CreatedBy = r.GetString(18),
            CreatedAt = r.GetDateTime(19),
            UpdatedAt = r.GetDateTime(20)
        };
    }

    private static TemplateSuggestionDto ReadSuggestionDto(NpgsqlDataReader r)
    {
        return new TemplateSuggestionDto
        {
            Id = r.GetInt32(0),
            AnalysisId = r.GetInt32(1),
            SuggestionType = r.GetString(2),
            ExistingTemplateId = r.IsDBNull(3) ? null : r.GetInt32(3),
            ExistingTemplateName = r.IsDBNull(4) ? null : r.GetString(4),
            SimilarityScore = r.IsDBNull(5) ? null : r.GetDecimal(5),
            SuggestedContentJson = r.IsDBNull(6) ? null : JsonSerializer.Deserialize<object>(r.GetString(6)),
            SuggestedSlug = r.GetString(7),
            SuggestedName = r.GetString(8),
            SuggestedType = r.GetString(9),
            SourceDataJson = r.IsDBNull(10) ? null : JsonSerializer.Deserialize<object>(r.GetString(10)),
            Status = r.GetString(11),
            ReviewedBy = r.IsDBNull(12) ? null : r.GetString(12),
            ReviewedAt = r.IsDBNull(13) ? null : r.GetDateTime(13),
            ResultTemplateId = r.IsDBNull(14) ? null : r.GetInt32(14),
            CreatedAt = r.GetDateTime(15)
        };
    }
}
