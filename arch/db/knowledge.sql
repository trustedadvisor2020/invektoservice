-- GR-2.1: Invekto.Knowledge Service DDL for PostgreSQL
-- Service: Invekto.Knowledge (port 7104)
-- Database: invekto (same as tenant_registry)
-- Tables: documents, chunks, faqs, tags, document_tags, intent_patterns, product_catalog, conversation_sentiments
-- pgvector: Required for semantic search (embedding vector(3072))

-- ============================================================
-- PREREQUISITE: pgvector extension (Q must have superuser access)
-- ============================================================
CREATE EXTENSION IF NOT EXISTS vector;

-- ============================================================
-- Auto-update trigger function (idempotent, may already exist from automation.sql)
-- ============================================================
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- ============================================================
-- 1. documents: Uploaded documents (PDF, CSV, JSON)
--    Phase A: WA-3 import records. Phase B: PDF upload + chunking.
-- ============================================================
CREATE TABLE IF NOT EXISTS documents (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    title               VARCHAR(500) NOT NULL,
    source_type         VARCHAR(50) NOT NULL,           -- pdf, csv, json, manual, wa_import
    status              VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending, processing, ready, error
    file_path           VARCHAR(1000),
    chunk_count         INTEGER NOT NULL DEFAULT 0,
    metadata_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_documents_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_documents_source_type
        CHECK (source_type IN ('pdf', 'csv', 'json', 'manual', 'wa_import')),
    CONSTRAINT chk_documents_status
        CHECK (status IN ('pending', 'processing', 'ready', 'error'))
);

CREATE INDEX IF NOT EXISTS idx_documents_tenant_status
    ON documents (tenant_id, status);

CREATE TRIGGER trigger_documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- 2. chunks: Document content chunks with embeddings
--    Phase A: Empty (no PDF chunking). Phase B: PDF -> chunks.
-- ============================================================
CREATE TABLE IF NOT EXISTS chunks (
    id                  BIGSERIAL PRIMARY KEY,
    document_id         INTEGER NOT NULL,
    tenant_id           INTEGER NOT NULL,
    content             TEXT NOT NULL,
    chunk_index         INTEGER NOT NULL DEFAULT 0,
    metadata_json       JSONB NOT NULL DEFAULT '{}'::jsonb,
    embedding           vector(3072),                   -- OpenAI text-embedding-3-large
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_chunks_document
        FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_chunks_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_chunks_document
    ON chunks (document_id, chunk_index);

CREATE INDEX IF NOT EXISTS idx_chunks_tenant
    ON chunks (tenant_id);

-- ============================================================
-- 3. faqs: FAQ entries from WA-3 cluster import + manual CRUD
--    Core table for Knowledge Service retrieval API.
-- ============================================================
CREATE TABLE IF NOT EXISTS faqs (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    question            VARCHAR(1000) NOT NULL,
    answer              TEXT NOT NULL,
    category            VARCHAR(100),                   -- payment, shipping, returns, size, stock, order, general
    lang                VARCHAR(10) NOT NULL DEFAULT 'tr',
    source              VARCHAR(50) NOT NULL DEFAULT 'manual', -- manual, wa_import
    source_metadata     JSONB,                          -- {cluster_id, question_count, ...}
    keywords            TEXT[] NOT NULL DEFAULT '{}',
    embedding           vector(3072),                   -- OpenAI text-embedding-3-large (nullable until generated)
    is_active           BOOLEAN NOT NULL DEFAULT true,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_faqs_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_faqs_source
        CHECK (source IN ('manual', 'wa_import')),
    CONSTRAINT uq_faqs_tenant_question
        UNIQUE (tenant_id, question)
);

CREATE INDEX IF NOT EXISTS idx_faqs_tenant_active
    ON faqs (tenant_id, is_active)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_faqs_category
    ON faqs (tenant_id, category)
    WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_faqs_lang
    ON faqs (tenant_id, lang)
    WHERE is_active = true;

-- Full-text search index (keyword fallback when no embedding)
CREATE INDEX IF NOT EXISTS idx_faqs_fts
    ON faqs USING gin(to_tsvector('simple', question || ' ' || answer));

-- Keywords array GIN index
CREATE INDEX IF NOT EXISTS idx_faqs_keywords
    ON faqs USING gin(keywords);

CREATE TRIGGER trigger_faqs_updated_at
    BEFORE UPDATE ON faqs
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- 4. tags: Tag management for documents/FAQs (Phase B: UI CRUD)
-- ============================================================
CREATE TABLE IF NOT EXISTS tags (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    name                VARCHAR(100) NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_tags_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT uq_tags_tenant_name
        UNIQUE (tenant_id, name)
);

CREATE INDEX IF NOT EXISTS idx_tags_tenant
    ON tags (tenant_id);

-- ============================================================
-- 5. document_tags: Many-to-many document <-> tag (Phase B)
-- ============================================================
CREATE TABLE IF NOT EXISTS document_tags (
    document_id         INTEGER NOT NULL,
    tag_id              INTEGER NOT NULL,
    PRIMARY KEY (document_id, tag_id),
    CONSTRAINT fk_document_tags_document
        FOREIGN KEY (document_id) REFERENCES documents(id) ON DELETE CASCADE,
    CONSTRAINT fk_document_tags_tag
        FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
);

-- ============================================================
-- WA-3 IMPORT TABLES
-- ============================================================

-- 6. intent_patterns: Aggregated intent classifications from WA-2
--    12 intent types, each with keyword list + sample messages.
CREATE TABLE IF NOT EXISTS intent_patterns (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    intent_name         VARCHAR(100) NOT NULL,          -- order_confirmation, price_inquiry, etc.
    keywords            TEXT[] NOT NULL DEFAULT '{}',
    confidence_avg      NUMERIC(4,3),                   -- 0.000 - 1.000
    sample_count        INTEGER NOT NULL DEFAULT 0,
    sample_messages     JSONB,                          -- ["msg1", "msg2", ...] (max 10)
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_intent_patterns_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT uq_intent_patterns_tenant_name
        UNIQUE (tenant_id, intent_name),
    CONSTRAINT chk_intent_confidence
        CHECK (confidence_avg IS NULL OR (confidence_avg >= 0 AND confidence_avg <= 1))
);

CREATE INDEX IF NOT EXISTS idx_intent_patterns_tenant
    ON intent_patterns (tenant_id);

-- 7. product_catalog: Product mention analysis from WA-2
--    5141 unique products with mention/sale/offered counts.
CREATE TABLE IF NOT EXISTS product_catalog (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    product_code        VARCHAR(50) NOT NULL,
    mention_count       INTEGER NOT NULL DEFAULT 0,
    sale_count          INTEGER NOT NULL DEFAULT 0,
    offered_count       INTEGER NOT NULL DEFAULT 0,
    avg_price           NUMERIC(10,2),
    outcomes_json       JSONB,                          -- {sale: N, offered: M, no_sale: K, ...}
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_product_catalog_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT uq_product_catalog_tenant_code
        UNIQUE (tenant_id, product_code)
);

CREATE INDEX IF NOT EXISTS idx_product_catalog_tenant_mentions
    ON product_catalog (tenant_id, mention_count DESC);

CREATE TRIGGER trigger_product_catalog_updated_at
    BEFORE UPDATE ON product_catalog
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- 8. conversation_sentiments: Per-conversation sentiment from WA-2
--    164K conversations with sentiment score.
CREATE TABLE IF NOT EXISTS conversation_sentiments (
    id                  BIGSERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    conversation_id     VARCHAR(50) NOT NULL,
    sentiment           VARCHAR(20) NOT NULL,           -- positive, neutral, negative
    score               NUMERIC(4,2),                   -- -1.00 to 1.00
    method              VARCHAR(20) NOT NULL,           -- keyword, ai, empty
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_conversation_sentiments_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_sentiment_value
        CHECK (sentiment IN ('positive', 'neutral', 'negative')),
    CONSTRAINT chk_sentiment_score
        CHECK (score IS NULL OR (score >= -1.00 AND score <= 1.00)),
    CONSTRAINT uq_sentiments_tenant_conversation
        UNIQUE (tenant_id, conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_conversation_sentiments_tenant
    ON conversation_sentiments (tenant_id);

CREATE INDEX IF NOT EXISTS idx_conversation_sentiments_conversation
    ON conversation_sentiments (tenant_id, conversation_id);

-- ============================================================
-- Grant permissions to invekto user
-- ============================================================
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO invekto;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
