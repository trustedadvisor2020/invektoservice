-- GR-1.1: Invekto.Automation Service DDL for PostgreSQL
-- Database: invekto (same as tenant_registry)
-- Tables: chatbot_flows, faq_entries, chat_sessions, auto_reply_log

-- ============================================================
-- 1. chatbot_flows: Tenant bazli chatbot flow konfigurasyonu
--    Multi-flow: Tenant basina birden fazla flow (lisansa bagli)
--    is_active: Tenant basina max 1 flow aktif olabilir (partial unique index)
-- ============================================================
CREATE TABLE IF NOT EXISTS chatbot_flows (
    flow_id             SERIAL PRIMARY KEY,                     -- Unique flow ID (auto-increment)
    tenant_id           INTEGER NOT NULL,                       -- Tenant (multi-flow: N flows per tenant)
    flow_name           VARCHAR(200) NOT NULL DEFAULT 'Ana Flow', -- Flow adi (tenant icinde unique)
    flow_config         JSONB NOT NULL DEFAULT '{}'::jsonb,     -- Flow JSON (v1 menu OR v2 node/edge graph)
    is_active           BOOLEAN NOT NULL DEFAULT false,         -- Bu flow mesaj isleme icin aktif mi? (max 1/tenant)
    is_default          BOOLEAN NOT NULL DEFAULT false,         -- Tenant'in varsayilan flow'u (backward compat)
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_chatbot_flows_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_chatbot_flows_tenant
    ON chatbot_flows (tenant_id);

-- Tenant basina en fazla 1 aktif flow (partial unique index)
CREATE UNIQUE INDEX IF NOT EXISTS uq_chatbot_flows_active
    ON chatbot_flows (tenant_id) WHERE is_active = true;

-- Tenant icinde flow adi unique
CREATE UNIQUE INDEX IF NOT EXISTS uq_chatbot_flows_name
    ON chatbot_flows (tenant_id, flow_name);

-- ============================================================
-- 1b. chatbot_flows MIGRATION (v1 -> multi-flow)
--     Mevcut DB'de tenant_id PK ise:
--     → Executable migration: arch/db/migrations/001-chatbot-flows-multi-flow.sql
-- ============================================================

-- ============================================================
-- 2. faq_entries: Tenant bazli FAQ kayitlari
-- ============================================================
CREATE TABLE IF NOT EXISTS faq_entries (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    question            VARCHAR(500) NOT NULL,                  -- Soru metni
    answer              VARCHAR(2000) NOT NULL,                 -- Cevap metni
    keywords            TEXT[] NOT NULL DEFAULT '{}',            -- Arama anahtar kelimeleri
    is_active           BOOLEAN NOT NULL DEFAULT true,
    sort_order          INTEGER NOT NULL DEFAULT 0,             -- Siralama
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_faq_entries_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_faq_entries_tenant_active
    ON faq_entries (tenant_id, is_active)
    WHERE is_active = true;

-- ============================================================
-- 3. chat_sessions: Aktif chatbot sohbet durumlari (state tracking)
-- ============================================================
CREATE TABLE IF NOT EXISTS chat_sessions (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    chat_id             INTEGER NOT NULL,                       -- Main App chat ID
    phone               VARCHAR(20),                            -- Musteri telefon
    current_node        VARCHAR(50) NOT NULL DEFAULT 'welcome', -- Aktif flow adimi (welcome, menu, faq, etc.)
    session_data        JSONB NOT NULL DEFAULT '{}'::jsonb,     -- Ek state verisi
    status              VARCHAR(20) NOT NULL DEFAULT 'active',  -- active, completed, handed_off, expired
    started_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_activity_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '30 minutes'),
    CONSTRAINT fk_chat_sessions_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

-- Unique: bir tenant icin bir chat_id'de sadece 1 aktif session
CREATE UNIQUE INDEX IF NOT EXISTS idx_chat_sessions_active
    ON chat_sessions (tenant_id, chat_id)
    WHERE status = 'active';

CREATE INDEX IF NOT EXISTS idx_chat_sessions_expires
    ON chat_sessions (expires_at)
    WHERE status = 'active';

-- ============================================================
-- 4. auto_reply_log: Tum otomatik cevap loglari
-- ============================================================
CREATE TABLE IF NOT EXISTS auto_reply_log (
    id                  BIGSERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    chat_id             INTEGER NOT NULL,
    phone               VARCHAR(20),
    message_text        VARCHAR(2000),                          -- Gelen mesaj
    reply_text          VARCHAR(2000),                          -- Gonderilen cevap
    reply_type          VARCHAR(30) NOT NULL,                   -- menu, faq, intent, off_hours, handoff, welcome
    intent              VARCHAR(50),                            -- Algilanan intent (nullable)
    confidence          NUMERIC(4,3),                           -- AI confidence score 0.000-1.000
    processing_time_ms  INTEGER,                                -- Islem suresi ms
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_auto_reply_log_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_auto_reply_log_tenant_date
    ON auto_reply_log (tenant_id, created_at DESC);

-- ============================================================
-- Triggers: updated_at otomatik guncelleme
-- ============================================================
-- update_updated_at_column() fonksiyonu tenant-registry.sql'de zaten var

CREATE OR REPLACE TRIGGER trigger_chatbot_flows_updated_at
    BEFORE UPDATE ON chatbot_flows
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_faq_entries_updated_at
    BEFORE UPDATE ON faq_entries
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
