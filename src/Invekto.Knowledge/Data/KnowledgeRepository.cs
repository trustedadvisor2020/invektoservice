using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Invekto.Shared.Logging;

namespace Invekto.Knowledge.Data;

/// <summary>
/// PostgreSQL data access for Knowledge Service.
/// FAQ CRUD, batch import, semantic search (pgvector), keyword search (FTS).
/// </summary>
public sealed class KnowledgeRepository
{
    private readonly KnowledgeConnectionFactory _db;
    private readonly JsonLinesLogger _logger;

    public KnowledgeRepository(KnowledgeConnectionFactory db, JsonLinesLogger logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // Health check
    // ============================================================

    public async Task<bool> CheckPgvectorAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM pg_extension WHERE extname = 'vector'";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null;
    }

    // ============================================================
    // FAQ CRUD
    // ============================================================

    public async Task<FaqDto?> GetFaqAsync(int tenantId, int faqId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, question, answer, category, lang, source,
                   keywords, is_active, created_at, updated_at
            FROM faqs
            WHERE tenant_id = @tid AND id = @fid";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", faqId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return ReadFaqDto(reader);
    }

    public async Task<(List<FaqDto> Faqs, int Total)> ListFaqsAsync(
        int tenantId, string? lang, string? category, int page, int limit, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        // Count
        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = @"
            SELECT COUNT(*) FROM faqs
            WHERE tenant_id = @tid AND is_active = true
              AND (@lang IS NULL OR lang = @lang)
              AND (@category IS NULL OR category = @category)";
        countCmd.Parameters.AddWithValue("tid", tenantId);
        countCmd.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Varchar) { Value = lang ?? (object)DBNull.Value });
        countCmd.Parameters.Add(new NpgsqlParameter("category", NpgsqlDbType.Varchar) { Value = category ?? (object)DBNull.Value });

        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        // List
        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = @"
            SELECT id, tenant_id, question, answer, category, lang, source,
                   keywords, is_active, created_at, updated_at
            FROM faqs
            WHERE tenant_id = @tid AND is_active = true
              AND (@lang IS NULL OR lang = @lang)
              AND (@category IS NULL OR category = @category)
            ORDER BY id DESC
            LIMIT @lim OFFSET @off";
        listCmd.Parameters.AddWithValue("tid", tenantId);
        listCmd.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Varchar) { Value = lang ?? (object)DBNull.Value });
        listCmd.Parameters.Add(new NpgsqlParameter("category", NpgsqlDbType.Varchar) { Value = category ?? (object)DBNull.Value });
        listCmd.Parameters.AddWithValue("lim", limit);
        listCmd.Parameters.AddWithValue("off", (page - 1) * limit);

        var faqs = new List<FaqDto>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            faqs.Add(ReadFaqDto(reader));

        return (faqs, total);
    }

    /// <summary>
    /// Insert a new FAQ or reactivate a soft-deleted one with the same question.
    /// Returns null if an active FAQ with the same (tenant_id, question) already exists (duplicate).
    /// </summary>
    public async Task<FaqDto?> InsertFaqAsync(int tenantId, string question, string answer,
        string? category, string lang, string[] keywords, string source, string? sourceMetadata,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO faqs (tenant_id, question, answer, category, lang, keywords, source, source_metadata)
            VALUES (@tid, @q, @a, @cat, @lang, @kw, @src, @meta::jsonb)
            ON CONFLICT (tenant_id, question) DO UPDATE SET
                answer = EXCLUDED.answer,
                category = EXCLUDED.category,
                lang = EXCLUDED.lang,
                keywords = EXCLUDED.keywords,
                source = EXCLUDED.source,
                source_metadata = EXCLUDED.source_metadata,
                is_active = true,
                embedding = NULL
            WHERE faqs.is_active = false
            RETURNING id, created_at, updated_at";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("q", question);
        cmd.Parameters.AddWithValue("a", answer);
        cmd.Parameters.Add(new NpgsqlParameter("cat", NpgsqlDbType.Varchar) { Value = category ?? (object)DBNull.Value });
        cmd.Parameters.AddWithValue("lang", lang);
        cmd.Parameters.Add(new NpgsqlParameter("kw", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keywords });
        cmd.Parameters.AddWithValue("src", source);
        cmd.Parameters.Add(new NpgsqlParameter("meta", NpgsqlDbType.Text) { Value = sourceMetadata ?? (object)DBNull.Value });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null; // Active duplicate exists

        return new FaqDto
        {
            Id = reader.GetInt32(0),
            TenantId = tenantId,
            Question = question,
            Answer = answer,
            Category = category,
            Lang = lang,
            Source = source,
            Keywords = keywords,
            IsActive = true,
            CreatedAt = reader.GetDateTime(1),
            UpdatedAt = reader.GetDateTime(2)
        };
    }

    public async Task<FaqDto?> UpdateFaqAsync(int tenantId, int faqId, string? question, string? answer,
        string? category, string? lang, string[]? keywords, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        var setClauses = new List<string>();
        if (question != null) { setClauses.Add("question = @q"); cmd.Parameters.AddWithValue("q", question); }
        if (answer != null) { setClauses.Add("answer = @a"); cmd.Parameters.AddWithValue("a", answer); }
        if (category != null) { setClauses.Add("category = @cat"); cmd.Parameters.AddWithValue("cat", category); }
        if (lang != null) { setClauses.Add("lang = @lang"); cmd.Parameters.AddWithValue("lang", lang); }
        if (keywords != null) { setClauses.Add("keywords = @kw"); cmd.Parameters.Add(new NpgsqlParameter("kw", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keywords }); }

        if (setClauses.Count == 0)
            return await GetFaqAsync(tenantId, faqId, ct);

        // Clear embedding when question/answer changes (needs re-generation)
        if (question != null || answer != null)
            setClauses.Add("embedding = NULL");

        cmd.CommandText = $@"
            UPDATE faqs SET {string.Join(", ", setClauses)}
            WHERE tenant_id = @tid AND id = @fid AND is_active = true
            RETURNING id, tenant_id, question, answer, category, lang, source,
                      keywords, is_active, created_at, updated_at";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", faqId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return ReadFaqDto(reader);
    }

    public async Task<bool> DeleteFaqAsync(int tenantId, int faqId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE faqs SET is_active = false
            WHERE tenant_id = @tid AND id = @fid AND is_active = true";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("fid", faqId);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    // ============================================================
    // Search
    // ============================================================

    public async Task<List<SearchResultDto>> SemanticSearchAsync(
        int tenantId, Vector queryEmbedding, int topK, string? lang, string? category,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, question, answer, category, lang,
                   1 - (embedding <=> @emb) AS score
            FROM faqs
            WHERE tenant_id = @tid AND is_active = true
              AND embedding IS NOT NULL
              AND (@lang IS NULL OR lang = @lang)
              AND (@category IS NULL OR category = @category)
            ORDER BY embedding <=> @emb
            LIMIT @topk";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("emb", queryEmbedding);
        cmd.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Varchar) { Value = lang ?? (object)DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("category", NpgsqlDbType.Varchar) { Value = category ?? (object)DBNull.Value });
        cmd.Parameters.AddWithValue("topk", topK);

        var results = new List<SearchResultDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SearchResultDto
            {
                SourceType = "faq",
                FaqId = reader.GetInt32(0),
                Question = reader.GetString(1),
                Answer = reader.GetString(2),
                Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                Lang = reader.GetString(4),
                Score = reader.GetDouble(5),
                Method = "semantic"
            });
        }

        return results;
    }

    public async Task<List<SearchResultDto>> KeywordSearchAsync(
        int tenantId, string query, int topK, string? lang, string? category,
        CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, question, answer, category, lang,
                   ts_rank(
                       to_tsvector('simple', question || ' ' || answer),
                       plainto_tsquery('simple', @query)
                   ) AS score
            FROM faqs
            WHERE tenant_id = @tid AND is_active = true
              AND to_tsvector('simple', question || ' ' || answer) @@ plainto_tsquery('simple', @query)
              AND (@lang IS NULL OR lang = @lang)
              AND (@category IS NULL OR category = @category)
            ORDER BY score DESC
            LIMIT @topk";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("query", query);
        cmd.Parameters.Add(new NpgsqlParameter("lang", NpgsqlDbType.Varchar) { Value = lang ?? (object)DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter("category", NpgsqlDbType.Varchar) { Value = category ?? (object)DBNull.Value });
        cmd.Parameters.AddWithValue("topk", topK);

        var results = new List<SearchResultDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SearchResultDto
            {
                SourceType = "faq",
                FaqId = reader.GetInt32(0),
                Question = reader.GetString(1),
                Answer = reader.GetString(2),
                Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                Lang = reader.GetString(4),
                Score = reader.GetDouble(5),
                Method = "keyword"
            });
        }

        return results;
    }

    // ============================================================
    // Batch import (WA-3)
    // ============================================================

    public async Task<int> BatchInsertFaqsAsync(int tenantId, List<FaqImportRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var inserted = 0;
            foreach (var row in rows)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO faqs (tenant_id, question, answer, category, lang, source, source_metadata, keywords)
                    VALUES (@tid, @q, @a, @cat, @lang, 'wa_import', @meta::jsonb, @kw)
                    ON CONFLICT (tenant_id, question) DO NOTHING";
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("q", row.Question);
                cmd.Parameters.AddWithValue("a", row.Answer);
                cmd.Parameters.Add(new NpgsqlParameter("cat", NpgsqlDbType.Varchar) { Value = row.Category ?? (object)DBNull.Value });
                cmd.Parameters.AddWithValue("lang", "tr");
                cmd.Parameters.AddWithValue("meta", row.SourceMetadata ?? "{}");
                cmd.Parameters.Add(new NpgsqlParameter("kw", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = row.Keywords ?? Array.Empty<string>() });

                inserted += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return inserted;
        }
        catch (NpgsqlException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> BatchInsertIntentPatternsAsync(int tenantId, List<IntentPatternRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var inserted = 0;
            foreach (var row in rows)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO intent_patterns (tenant_id, intent_name, keywords, confidence_avg, sample_count, sample_messages)
                    VALUES (@tid, @name, @kw, @conf, @cnt, @msgs::jsonb)
                    ON CONFLICT (tenant_id, intent_name) DO UPDATE SET
                        keywords = EXCLUDED.keywords,
                        confidence_avg = EXCLUDED.confidence_avg,
                        sample_count = EXCLUDED.sample_count,
                        sample_messages = EXCLUDED.sample_messages";
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("name", row.IntentName);
                cmd.Parameters.Add(new NpgsqlParameter("kw", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = row.Keywords ?? Array.Empty<string>() });
                cmd.Parameters.Add(new NpgsqlParameter("conf", NpgsqlDbType.Numeric) { Value = row.ConfidenceAvg ?? (object)DBNull.Value });
                cmd.Parameters.AddWithValue("cnt", row.SampleCount);
                cmd.Parameters.AddWithValue("msgs", row.SampleMessagesJson ?? "[]");

                inserted += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return inserted;
        }
        catch (NpgsqlException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> BatchInsertProductCatalogAsync(int tenantId, List<ProductCatalogRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            var inserted = 0;
            foreach (var row in rows)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO product_catalog (tenant_id, product_code, mention_count, sale_count, offered_count, avg_price, outcomes_json)
                    VALUES (@tid, @code, @mentions, @sales, @offered, @price, @outcomes::jsonb)
                    ON CONFLICT (tenant_id, product_code) DO UPDATE SET
                        mention_count = EXCLUDED.mention_count,
                        sale_count = EXCLUDED.sale_count,
                        offered_count = EXCLUDED.offered_count,
                        avg_price = EXCLUDED.avg_price,
                        outcomes_json = EXCLUDED.outcomes_json";
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.Parameters.AddWithValue("code", row.ProductCode);
                cmd.Parameters.AddWithValue("mentions", row.MentionCount);
                cmd.Parameters.AddWithValue("sales", row.SaleCount);
                cmd.Parameters.AddWithValue("offered", row.OfferedCount);
                cmd.Parameters.Add(new NpgsqlParameter("price", NpgsqlDbType.Numeric) { Value = row.AvgPrice ?? (object)DBNull.Value });
                cmd.Parameters.AddWithValue("outcomes", row.OutcomesJson ?? "{}");

                inserted += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return inserted;
        }
        catch (NpgsqlException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> BatchInsertSentimentsAsync(int tenantId, List<SentimentRow> rows, int batchSize = 500, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        var totalInserted = 0;

        // Process in batches to avoid huge transactions
        for (var i = 0; i < rows.Count; i += batchSize)
        {
            var batch = rows.Skip(i).Take(batchSize).ToList();
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                foreach (var row in batch)
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO conversation_sentiments (tenant_id, conversation_id, sentiment, score, method)
                        VALUES (@tid, @cid, @sent, @score, @method)
                        ON CONFLICT (tenant_id, conversation_id) DO UPDATE SET
                            sentiment = EXCLUDED.sentiment,
                            score = EXCLUDED.score,
                            method = EXCLUDED.method";
                    cmd.Parameters.AddWithValue("tid", tenantId);
                    cmd.Parameters.AddWithValue("cid", row.ConversationId);
                    cmd.Parameters.AddWithValue("sent", row.Sentiment);
                    cmd.Parameters.Add(new NpgsqlParameter("score", NpgsqlDbType.Numeric) { Value = row.Score ?? (object)DBNull.Value });
                    cmd.Parameters.AddWithValue("method", row.Method);

                    totalInserted += await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (NpgsqlException)
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        return totalInserted;
    }

    public async Task<int> InsertDocumentAsync(int tenantId, string title, string sourceType,
        string? filePath, string? metadataJson, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO documents (tenant_id, title, source_type, file_path, metadata_json, status)
            VALUES (@tid, @title, @type, @path, @meta::jsonb, 'processing')
            RETURNING id";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("type", sourceType);
        cmd.Parameters.Add(new NpgsqlParameter("path", NpgsqlDbType.Varchar) { Value = filePath ?? (object)DBNull.Value });
        cmd.Parameters.AddWithValue("meta", metadataJson ?? "{}");

        var id = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(id);
    }

    public async Task UpdateDocumentStatusAsync(int tenantId, int documentId, string status, int chunkCount = 0, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE documents SET status = @status, chunk_count = @cnt
            WHERE id = @id AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("id", documentId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("cnt", chunkCount);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ============================================================
    // Document CRUD (Phase B)
    // ============================================================

    public async Task<(List<DocumentDto> Documents, int Total)> ListDocumentsAsync(
        int tenantId, string? status, int page, int limit, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = @"
            SELECT COUNT(*) FROM documents
            WHERE tenant_id = @tid
              AND (@status IS NULL OR status = @status)";
        countCmd.Parameters.AddWithValue("tid", tenantId);
        countCmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = status ?? (object)DBNull.Value });

        var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(ct));

        await using var listCmd = conn.CreateCommand();
        listCmd.CommandText = @"
            SELECT id, tenant_id, title, source_type, status, file_path,
                   chunk_count, metadata_json, created_at, updated_at
            FROM documents
            WHERE tenant_id = @tid
              AND (@status IS NULL OR status = @status)
            ORDER BY id DESC
            LIMIT @lim OFFSET @off";
        listCmd.Parameters.AddWithValue("tid", tenantId);
        listCmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = status ?? (object)DBNull.Value });
        listCmd.Parameters.AddWithValue("lim", limit);
        listCmd.Parameters.AddWithValue("off", (page - 1) * limit);

        var docs = new List<DocumentDto>();
        await using var reader = await listCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            docs.Add(ReadDocumentDto(reader));

        return (docs, total);
    }

    public async Task<DocumentDto?> GetDocumentAsync(int tenantId, int documentId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, title, source_type, status, file_path,
                   chunk_count, metadata_json, created_at, updated_at
            FROM documents
            WHERE tenant_id = @tid AND id = @did";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("did", documentId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return ReadDocumentDto(reader);
    }

    public async Task<bool> DeleteDocumentAsync(int tenantId, int documentId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        // CASCADE: chunks deleted automatically via FK on documents(id)
        cmd.CommandText = "DELETE FROM documents WHERE tenant_id = @tid AND id = @did";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("did", documentId);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    /// <summary>
    /// Get documents stuck in pending/processing for restart recovery.
    /// Used by DocumentProcessingService.StartAsync to re-enqueue on startup.
    /// </summary>
    public async Task<List<DocumentDto>> GetStuckDocumentsAsync(CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, tenant_id, title, source_type, status, file_path,
                   chunk_count, metadata_json, created_at, updated_at
            FROM documents
            WHERE status IN ('pending', 'processing')
            ORDER BY id";

        var docs = new List<DocumentDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            docs.Add(ReadDocumentDto(reader));

        return docs;
    }

    // ============================================================
    // Chunk operations (Phase B)
    // ============================================================

    public async Task<int> BatchInsertChunksAsync(int tenantId, int documentId, List<ChunkInsertRow> rows, CancellationToken ct = default)
    {
        if (rows.Count == 0) return 0;

        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        try
        {
            // Verify document belongs to tenant before inserting chunks
            await using (var verifyCmd = conn.CreateCommand())
            {
                verifyCmd.Transaction = tx;
                verifyCmd.CommandText = "SELECT id FROM documents WHERE id = @did AND tenant_id = @tid";
                verifyCmd.Parameters.AddWithValue("did", documentId);
                verifyCmd.Parameters.AddWithValue("tid", tenantId);
                var exists = await verifyCmd.ExecuteScalarAsync(ct);
                if (exists == null)
                    throw new InvalidOperationException($"Document {documentId} not found for tenant {tenantId}");
            }

            // Build multi-value INSERT for batch efficiency
            var inserted = 0;
            const int batchSize = 50;
            for (int i = 0; i < rows.Count; i += batchSize)
            {
                var batch = rows.Skip(i).Take(batchSize).ToList();
                var values = new List<string>();
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                for (int j = 0; j < batch.Count; j++)
                {
                    values.Add($"(@did, @tid, @content{j}, @idx{j}, @meta{j}::jsonb)");
                    cmd.Parameters.AddWithValue($"content{j}", batch[j].Content);
                    cmd.Parameters.AddWithValue($"idx{j}", batch[j].ChunkIndex);
                    cmd.Parameters.AddWithValue($"meta{j}", batch[j].MetadataJson ?? "{}");
                }

                cmd.Parameters.AddWithValue("did", documentId);
                cmd.Parameters.AddWithValue("tid", tenantId);
                cmd.CommandText = $@"
                    INSERT INTO chunks (document_id, tenant_id, content, chunk_index, metadata_json)
                    VALUES {string.Join(",\n                           ", values)}";

                inserted += await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            return inserted;
        }
        catch (NpgsqlException)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> UpdateChunkEmbeddingAsync(int tenantId, long chunkId, Vector embedding, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE chunks SET embedding = @emb WHERE id = @id AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("emb", embedding);
        cmd.Parameters.AddWithValue("id", chunkId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<(long Id, string Text)>> GetChunksWithoutEmbeddingAsync(int tenantId, int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, content
            FROM chunks
            WHERE tenant_id = @tid AND embedding IS NULL
            ORDER BY id
            LIMIT @lim";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);

        var results = new List<(long, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetInt64(0), reader.GetString(1)));

        return results;
    }

    // ============================================================
    // Chunk search (Phase B)
    // ============================================================

    public async Task<List<ChunkSearchResultDto>> SemanticSearchChunksAsync(
        int tenantId, Vector queryEmbedding, int topK, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.document_id, d.title, c.content, c.chunk_index,
                   c.metadata_json, 1 - (c.embedding <=> @emb) AS score
            FROM chunks c
            INNER JOIN documents d ON d.id = c.document_id
            WHERE c.tenant_id = @tid
              AND c.embedding IS NOT NULL
              AND d.status = 'ready'
            ORDER BY c.embedding <=> @emb
            LIMIT @topk";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("emb", queryEmbedding);
        cmd.Parameters.AddWithValue("topk", topK);

        var results = new List<ChunkSearchResultDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var metadataJson = reader.IsDBNull(5) ? "{}" : reader.GetString(5);
            results.Add(new ChunkSearchResultDto
            {
                ChunkId = reader.GetInt64(0),
                DocumentId = reader.GetInt32(1),
                DocumentTitle = reader.GetString(2),
                Content = reader.GetString(3),
                ChunkIndex = reader.GetInt32(4),
                PageNumber = ExtractPageNumber(metadataJson),
                Score = reader.GetDouble(6),
                Method = "semantic"
            });
        }

        return results;
    }

    public async Task<List<ChunkSearchResultDto>> KeywordSearchChunksAsync(
        int tenantId, string query, int topK, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT c.id, c.document_id, d.title, c.content, c.chunk_index,
                   c.metadata_json,
                   ts_rank(
                       to_tsvector('simple', c.content),
                       plainto_tsquery('simple', @query)
                   ) AS score
            FROM chunks c
            INNER JOIN documents d ON d.id = c.document_id
            WHERE c.tenant_id = @tid
              AND d.status = 'ready'
              AND to_tsvector('simple', c.content) @@ plainto_tsquery('simple', @query)
            ORDER BY score DESC
            LIMIT @topk";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("query", query);
        cmd.Parameters.AddWithValue("topk", topK);

        var results = new List<ChunkSearchResultDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var metadataJson = reader.IsDBNull(5) ? "{}" : reader.GetString(5);
            results.Add(new ChunkSearchResultDto
            {
                ChunkId = reader.GetInt64(0),
                DocumentId = reader.GetInt32(1),
                DocumentTitle = reader.GetString(2),
                Content = reader.GetString(3),
                ChunkIndex = reader.GetInt32(4),
                PageNumber = ExtractPageNumber(metadataJson),
                Score = reader.GetDouble(6),
                Method = "keyword"
            });
        }

        return results;
    }

    // ============================================================
    // Embedding update
    // ============================================================

    public async Task<int> UpdateFaqEmbeddingAsync(int tenantId, int faqId, Vector embedding, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE faqs SET embedding = @emb WHERE id = @id AND tenant_id = @tid";
        cmd.Parameters.AddWithValue("emb", embedding);
        cmd.Parameters.AddWithValue("id", faqId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<(int Id, string Text)>> GetFaqsWithoutEmbeddingAsync(int tenantId, int limit = 100, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, question || ' ' || answer AS text
            FROM faqs
            WHERE tenant_id = @tid AND is_active = true AND embedding IS NULL
            ORDER BY id
            LIMIT @lim";
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("lim", limit);

        var results = new List<(int, string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add((reader.GetInt32(0), reader.GetString(1)));

        return results;
    }

    // ============================================================
    // Private helpers
    // ============================================================

    private static FaqDto ReadFaqDto(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        TenantId = reader.GetInt32(1),
        Question = reader.GetString(2),
        Answer = reader.GetString(3),
        Category = reader.IsDBNull(4) ? null : reader.GetString(4),
        Lang = reader.GetString(5),
        Source = reader.GetString(6),
        Keywords = reader.IsDBNull(7) ? Array.Empty<string>() : (string[])reader.GetValue(7),
        IsActive = reader.GetBoolean(8),
        CreatedAt = reader.GetDateTime(9),
        UpdatedAt = reader.GetDateTime(10)
    };

    private static DocumentDto ReadDocumentDto(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        TenantId = reader.GetInt32(1),
        Title = reader.GetString(2),
        SourceType = reader.GetString(3),
        Status = reader.GetString(4),
        FilePath = reader.IsDBNull(5) ? null : reader.GetString(5),
        ChunkCount = reader.GetInt32(6),
        MetadataJson = reader.IsDBNull(7) ? null : reader.GetString(7),
        CreatedAt = reader.GetDateTime(8),
        UpdatedAt = reader.GetDateTime(9)
    };

    private static int? ExtractPageNumber(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("page_number", out var pageEl))
                return pageEl.GetInt32();
        }
        catch (JsonException)
        {
            // Metadata JSON is written by our own BatchInsertChunks with a fixed template.
            // Malformed JSON here would indicate a code bug, not a runtime condition.
            // Returning null safely degrades: search result omits page number.
        }
        return null;
    }
}

// ============================================================
// DTOs
// ============================================================

public sealed class FaqDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public string? Category { get; init; }
    public required string Lang { get; init; }
    public required string Source { get; init; }
    public string[] Keywords { get; init; } = Array.Empty<string>();
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class SearchResultDto
{
    // Common
    public required string SourceType { get; init; } // "faq" or "chunk"
    public double Score { get; init; }
    public required string Method { get; init; }

    // FAQ fields (null for chunk results)
    public int? FaqId { get; init; }
    public string? Question { get; init; }
    public string? Answer { get; init; }
    public string? Category { get; init; }
    public string? Lang { get; init; }

    // Chunk fields (null for FAQ results)
    public long? ChunkId { get; init; }
    public string? Content { get; init; }
    public int? DocumentId { get; init; }
    public string? DocumentTitle { get; init; }
    public int? PageNumber { get; init; }
}

public sealed class FaqImportRow
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public string? Category { get; init; }
    public string? SourceMetadata { get; init; }
    public string[]? Keywords { get; init; }
}

public sealed class IntentPatternRow
{
    public required string IntentName { get; init; }
    public string[]? Keywords { get; init; }
    public decimal? ConfidenceAvg { get; init; }
    public int SampleCount { get; init; }
    public string? SampleMessagesJson { get; init; }
}

public sealed class ProductCatalogRow
{
    public required string ProductCode { get; init; }
    public int MentionCount { get; init; }
    public int SaleCount { get; init; }
    public int OfferedCount { get; init; }
    public decimal? AvgPrice { get; init; }
    public string? OutcomesJson { get; init; }
}

public sealed class SentimentRow
{
    public required string ConversationId { get; init; }
    public required string Sentiment { get; init; }
    public decimal? Score { get; init; }
    public required string Method { get; init; }
}

public sealed class DocumentDto
{
    public int Id { get; init; }
    public int TenantId { get; init; }
    public required string Title { get; init; }
    public required string SourceType { get; init; }
    public required string Status { get; init; }
    public string? FilePath { get; init; }
    public int ChunkCount { get; init; }
    public string? MetadataJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ChunkSearchResultDto
{
    public long ChunkId { get; init; }
    public int DocumentId { get; init; }
    public required string DocumentTitle { get; init; }
    public required string Content { get; init; }
    public int ChunkIndex { get; init; }
    public int? PageNumber { get; init; }
    public double Score { get; init; }
    public required string Method { get; init; }
}

public sealed class ChunkInsertRow
{
    public required string Content { get; init; }
    public int ChunkIndex { get; init; }
    public string? MetadataJson { get; init; }
}
