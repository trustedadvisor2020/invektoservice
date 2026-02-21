-- message-log.sql
-- SuperAdmin mesaj izleme tablosu
-- Tum tenant'lara gelen/giden WhatsApp mesajlarini loglar
-- Hook: Backend POST /api/v1/webhook/event (fire-and-forget insert)

CREATE TABLE IF NOT EXISTS message_log (
    id                  BIGSERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    direction           VARCHAR(3) NOT NULL,         -- 'in' = musteri->firma, 'out' = firma->musteri
    phone               VARCHAR(20) NOT NULL,
    sender_name         TEXT,
    message_text        TEXT,
    message_type        VARCHAR(20) DEFAULT 'text',  -- text, image, video, document, audio
    chat_id             VARCHAR(50),
    external_message_id VARCHAR(100),
    instance_id         VARCHAR(100),                     -- WapCRM instance ID (INMA InstanceID)
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Superadmin sayfa sorgulari icin (ORDER BY created_at DESC)
CREATE INDEX IF NOT EXISTS idx_message_log_created ON message_log (created_at DESC);

-- Tenant bazli filtreleme
CREATE INDEX IF NOT EXISTS idx_message_log_tenant ON message_log (tenant_id, created_at DESC);

-- Telefon bazli arama
CREATE INDEX IF NOT EXISTS idx_message_log_phone ON message_log (phone, created_at DESC);

GRANT ALL ON message_log TO invekto;
GRANT USAGE, SELECT ON SEQUENCE message_log_id_seq TO invekto;
