-- PKT-11: VoiceAI — voice_transcriptions table
-- Transcription log for audit/analytics. Audio files NOT stored (transcribe & delete).

CREATE TABLE IF NOT EXISTS voice_transcriptions (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    request_id      VARCHAR(64) NOT NULL,
    file_name       VARCHAR(255) NOT NULL,
    language        VARCHAR(10) NOT NULL DEFAULT 'unknown',
    duration_seconds DOUBLE PRECISION NOT NULL DEFAULT 0,
    word_count      INT NOT NULL DEFAULT 0,
    transcript      TEXT NOT NULL,
    intent_label    VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_voice_transcriptions_tenant ON voice_transcriptions(tenant_id);
CREATE INDEX idx_voice_transcriptions_created ON voice_transcriptions(created_at);

COMMENT ON TABLE voice_transcriptions IS 'PKT-11: Voice message transcription log (audio deleted after processing)';

-- Permissions
GRANT SELECT, INSERT ON voice_transcriptions TO invekto_app;
GRANT USAGE, SELECT ON SEQUENCE voice_transcriptions_id_seq TO invekto_app;
