-- RI Faz 6: Outcome Feedback Table (Ground Truth Flywheel)
-- Run on PostgreSQL production database
-- Date: 2026-03-01

CREATE TABLE IF NOT EXISTS wa_outcome_feedback (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    conversation_id VARCHAR(100) NOT NULL,
    original_label  VARCHAR(50) NOT NULL,
    is_agree        BOOLEAN NOT NULL,
    corrected_label VARCHAR(50),
    user_id         INT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_wa_outcome_feedback_tenant
    ON wa_outcome_feedback(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_outcome_feedback_label
    ON wa_outcome_feedback(tenant_id, original_label);

-- Grants
GRANT ALL ON wa_outcome_feedback TO invekto;
GRANT ALL ON SEQUENCE wa_outcome_feedback_id_seq TO invekto;
