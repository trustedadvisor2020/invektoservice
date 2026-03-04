-- Migration 006: Chat Translation Cache
-- Date: 2026-03-04
-- Feature: INMA Chat Translation (all channels)
-- TTL: 7 days

CREATE TABLE IF NOT EXISTS message_translations (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    source_hash     VARCHAR(64) NOT NULL,
    source_language VARCHAR(10),
    target_language VARCHAR(10) NOT NULL,
    source_text     TEXT NOT NULL,
    translated_text TEXT NOT NULL,
    provider        VARCHAR(20) DEFAULT 'claude',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_msg_trans_lookup
    ON message_translations(tenant_id, source_hash, target_language);

CREATE INDEX IF NOT EXISTS idx_msg_trans_expiry
    ON message_translations(expires_at);

GRANT ALL ON message_translations TO invekto;
GRANT USAGE, SELECT ON SEQUENCE message_translations_id_seq TO invekto;
