-- =============================================================
-- Invekto.Knowledge v2 Migration (PKT-1: GR-2.3 Multi-lang)
-- Run AFTER knowledge.sql base schema
-- =============================================================

-- GR-2.3: Add language column to chunks for multi-lang search filtering
ALTER TABLE chunks ADD COLUMN IF NOT EXISTS lang VARCHAR(10) NOT NULL DEFAULT 'tr';

-- Index for language-filtered chunk searches
CREATE INDEX IF NOT EXISTS idx_chunks_tenant_lang
    ON chunks (tenant_id, lang);

-- Grant permissions (idempotent)
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO invekto;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
