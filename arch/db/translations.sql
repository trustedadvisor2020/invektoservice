-- translations.sql
-- Chat mesaj çeviri cache tablosu
-- Tüm kanallar (WhatsApp, WebChat, gelecek kanallar) için generic
-- TTL: 7 gün (Q kararı)
-- Hook: Backend POST /api/v1/translate, /api/v1/translate/batch

CREATE TABLE IF NOT EXISTS message_translations (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    source_hash     VARCHAR(64) NOT NULL,          -- SHA256(source_text) for fast lookup
    source_language VARCHAR(10),                    -- ISO 639-1 detected source lang
    target_language VARCHAR(10) NOT NULL,           -- ISO 639-1 requested target lang
    source_text     TEXT NOT NULL,
    translated_text TEXT NOT NULL,
    provider        VARCHAR(20) DEFAULT 'claude',   -- translation provider
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL            -- created_at + 7 days
);

-- Primary lookup: tenant + hash + target lang (UNIQUE = upsert desteği)
CREATE UNIQUE INDEX IF NOT EXISTS idx_msg_trans_lookup
    ON message_translations(tenant_id, source_hash, target_language);

-- Cleanup job: expired rows
CREATE INDEX IF NOT EXISTS idx_msg_trans_expiry
    ON message_translations(expires_at)
    WHERE expires_at < NOW();

GRANT ALL ON message_translations TO invekto;
GRANT USAGE, SELECT ON SEQUENCE message_translations_id_seq TO invekto;
