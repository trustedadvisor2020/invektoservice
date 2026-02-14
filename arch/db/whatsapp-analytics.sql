-- ============================================================
-- WhatsApp Analytics Service - Database Schema
-- WA-5/6: Pipeline + Query Layer
-- ============================================================
-- Phase A: wa_analyses, wa_messages, wa_conversations, wa_metadata
-- Phase B: wa_intents, wa_sentiments, wa_products, wa_prices, wa_faq_pairs, wa_faq_clusters
-- ============================================================

-- 1. Analysis job tracking
CREATE TABLE IF NOT EXISTS wa_analyses (
    id SERIAL PRIMARY KEY,
    tenant_id INT NOT NULL REFERENCES tenant_registry(id),
    status VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending/recovering/cleaning/threading/stats/intents/faq/sentiment/products/completed/error
    source_file_name TEXT,
    config_json JSONB,                               -- {delimiter, encoding, tenant_name, sector}
    total_messages INT,
    total_conversations INT,
    stage_progress JSONB,                            -- {stage, percent, message, stageNumber, totalStages}
    error_message TEXT,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_wa_analyses_tenant ON wa_analyses (tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_analyses_status ON wa_analyses (status);

-- 2. Cleaned messages (LARGE: 2M+ per tenant)
CREATE TABLE IF NOT EXISTS wa_messages (
    id BIGSERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    conversation_id TEXT NOT NULL,
    business_phone TEXT,
    timestamp TIMESTAMPTZ NOT NULL,
    message_text TEXT,
    sender_type VARCHAR(10),                         -- ME, CUSTOMER
    agent_name TEXT,
    message_hash VARCHAR(16)                         -- SHA256[:16] for dedup
);

CREATE INDEX IF NOT EXISTS idx_wa_messages_tenant_analysis ON wa_messages (tenant_id, analysis_id);
CREATE INDEX IF NOT EXISTS idx_wa_messages_analysis_conv ON wa_messages (analysis_id, conversation_id);
CREATE INDEX IF NOT EXISTS idx_wa_messages_tenant_ts ON wa_messages (tenant_id, timestamp);

-- 3. Conversations
CREATE TABLE IF NOT EXISTS wa_conversations (
    id BIGSERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    conversation_id TEXT NOT NULL,
    business_phone TEXT,
    start_time TIMESTAMPTZ,
    end_time TIMESTAMPTZ,
    duration_minutes INT,
    message_count INT,
    customer_message_count INT,
    agent_message_count INT,
    primary_agent TEXT,
    first_response_minutes REAL,
    outcome VARCHAR(20),                             -- sale/offered/no_sale/abandoned/no_response/return/complaint
    product_codes TEXT,                              -- pipe-separated
    first_customer_msg TEXT,
    last_agent_msg TEXT
);

CREATE INDEX IF NOT EXISTS idx_wa_conversations_tenant_analysis ON wa_conversations (tenant_id, analysis_id);
CREATE INDEX IF NOT EXISTS idx_wa_conversations_analysis_outcome ON wa_conversations (analysis_id, outcome);
CREATE INDEX IF NOT EXISTS idx_wa_conversations_analysis_agent ON wa_conversations (analysis_id, primary_agent);

-- 4. Intent classifications (Phase B)
CREATE TABLE IF NOT EXISTS wa_intents (
    id BIGSERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    conversation_id TEXT NOT NULL,
    message_text TEXT,
    intent VARCHAR(30),                              -- 12 types: size/price/stock/shipping/return/complaint/order/greeting/thank/product/discount/address
    confidence REAL,
    method VARCHAR(20)                               -- keyword/regex/claude/claude_low_conf/skipped/unknown
);

CREATE INDEX IF NOT EXISTS idx_wa_intents_tenant_analysis ON wa_intents (tenant_id, analysis_id);
CREATE INDEX IF NOT EXISTS idx_wa_intents_analysis_intent ON wa_intents (analysis_id, intent);

-- 5. Sentiments (per conversation, Phase B)
CREATE TABLE IF NOT EXISTS wa_sentiments (
    id BIGSERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    conversation_id TEXT NOT NULL,
    sentiment VARCHAR(10),                           -- positive/neutral/negative
    score REAL,                                      -- -1.0 to 1.0
    method VARCHAR(20)                               -- keyword/claude/empty/skipped
);

CREATE INDEX IF NOT EXISTS idx_wa_sentiments_tenant_analysis ON wa_sentiments (tenant_id, analysis_id);
CREATE INDEX IF NOT EXISTS idx_wa_sentiments_analysis_sentiment ON wa_sentiments (analysis_id, sentiment);

-- 6. Product analysis (per conversation, Phase B)
CREATE TABLE IF NOT EXISTS wa_products (
    id BIGSERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    conversation_id TEXT NOT NULL,
    product_codes TEXT,                              -- pipe-separated
    product_count INT,
    prices_mentioned TEXT,                           -- pipe-separated
    price_count INT,
    outcome VARCHAR(20),
    primary_agent TEXT
);

CREATE INDEX IF NOT EXISTS idx_wa_products_tenant_analysis ON wa_products (tenant_id, analysis_id);

-- 7. Price analysis (unique prices per analysis, Phase B)
CREATE TABLE IF NOT EXISTS wa_prices (
    id SERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    price DECIMAL(10,2),
    mention_count INT,
    likely_tl TEXT
);

CREATE INDEX IF NOT EXISTS idx_wa_prices_analysis ON wa_prices (analysis_id);

-- 8. FAQ pairs (extracted Q&A, Phase B)
CREATE TABLE IF NOT EXISTS wa_faq_pairs (
    id BIGSERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    conversation_id TEXT NOT NULL,
    question TEXT,
    answer TEXT,
    question_len INT,
    answer_len INT,
    cluster_id INT                                   -- assigned during clustering
);

CREATE INDEX IF NOT EXISTS idx_wa_faq_pairs_tenant_analysis ON wa_faq_pairs (tenant_id, analysis_id);

-- 9. FAQ clusters (Phase B)
CREATE TABLE IF NOT EXISTS wa_faq_clusters (
    id SERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    cluster_label INT NOT NULL,
    representative_question TEXT,
    question_count INT,
    sample_questions JSONB,
    sample_answers JSONB
    -- embedding vector(3072) added when pgvector enabled
);

CREATE INDEX IF NOT EXISTS idx_wa_faq_clusters_analysis ON wa_faq_clusters (analysis_id);

-- 10. Pre-aggregated metadata
CREATE TABLE IF NOT EXISTS wa_metadata (
    id SERIAL PRIMARY KEY,
    analysis_id INT NOT NULL REFERENCES wa_analyses(id) ON DELETE CASCADE,
    tenant_id INT NOT NULL,
    metadata_json JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_wa_metadata_analysis ON wa_metadata (analysis_id);

-- ============================================================
-- Trigger: auto-update updated_at on wa_analyses
-- ============================================================

CREATE OR REPLACE FUNCTION update_wa_analyses_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_wa_analyses_updated_at ON wa_analyses;
CREATE TRIGGER trg_wa_analyses_updated_at
    BEFORE UPDATE ON wa_analyses
    FOR EACH ROW
    EXECUTE FUNCTION update_wa_analyses_updated_at();

-- ============================================================
-- Grants (match existing invekto user pattern)
-- ============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON wa_analyses TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_messages TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_conversations TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_intents TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_sentiments TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_products TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_prices TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_faq_pairs TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_faq_clusters TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_metadata TO invekto;

GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
