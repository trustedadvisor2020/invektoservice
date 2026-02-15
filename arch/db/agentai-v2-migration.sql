-- =============================================================
-- Invekto.AgentAI v2 Migration (PKT-1: GR-2.2 + GR-2.3)
-- Run AFTER agentai.sql base schema
-- =============================================================

-- GR-2.2: Knowledge integration columns
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS tone VARCHAR(20);
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS knowledge_used BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS knowledge_sources JSONB;
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS knowledge_query TEXT;
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS suggested_followup TEXT;
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS conversation_summary TEXT;

-- GR-2.3: Language detection column
ALTER TABLE suggest_reply_log ADD COLUMN IF NOT EXISTS detected_language VARCHAR(5);

-- Index for Knowledge usage analytics
CREATE INDEX IF NOT EXISTS idx_suggest_log_knowledge
    ON suggest_reply_log (tenant_id, knowledge_used, created_at DESC)
    WHERE knowledge_used = TRUE;

-- Index for language analytics
CREATE INDEX IF NOT EXISTS idx_suggest_log_language
    ON suggest_reply_log (tenant_id, detected_language, created_at DESC);

-- Grant permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON suggest_reply_log TO invekto;
