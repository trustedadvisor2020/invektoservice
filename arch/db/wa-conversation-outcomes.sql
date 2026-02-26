-- wa_conversation_outcomes: Production outcome classification results
-- RI-2.7: Batch pipeline writes here, nightly job updates incrementally.
-- Source: MSSQL CustomerPhoneNumber -> tiered classification -> PG storage.

CREATE TABLE IF NOT EXISTS wa_conversation_outcomes (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    database_name   VARCHAR(100) NOT NULL,
    instance_id     INTEGER,
    conversation_id VARCHAR(50) NOT NULL,
    sector          VARCHAR(50),
    outcome_label   VARCHAR(30) NOT NULL,
    confidence      REAL NOT NULL DEFAULT 0,
    has_offer       BOOLEAN NOT NULL DEFAULT FALSE,
    evidence        TEXT,
    model_version   VARCHAR(50) NOT NULL DEFAULT 'tiered-v0.6',
    classified_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Upsert key: one outcome per tenant + conversation
CREATE UNIQUE INDEX IF NOT EXISTS uix_wa_conv_outcomes_tenant_conv
    ON wa_conversation_outcomes(tenant_id, conversation_id);

-- Label distribution queries
CREATE INDEX IF NOT EXISTS ix_wa_conv_outcomes_label
    ON wa_conversation_outcomes(tenant_id, outcome_label);

-- Sector-filtered queries
CREATE INDEX IF NOT EXISTS ix_wa_conv_outcomes_sector
    ON wa_conversation_outcomes(sector) WHERE sector IS NOT NULL;

-- Temporal queries (recent classifications)
CREATE INDEX IF NOT EXISTS ix_wa_conv_outcomes_classified_at
    ON wa_conversation_outcomes(tenant_id, classified_at DESC);

-- Batch job tracking
CREATE TABLE IF NOT EXISTS wa_batch_jobs (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    database_name   VARCHAR(100) NOT NULL,
    instance_id     INTEGER,
    sector          VARCHAR(50),
    job_type        VARCHAR(20) NOT NULL DEFAULT 'manual',
    status          VARCHAR(20) NOT NULL DEFAULT 'pending',
    lookback_days   INTEGER NOT NULL DEFAULT 7,
    total_candidates INTEGER,
    already_classified INTEGER DEFAULT 0,
    classified_count INTEGER DEFAULT 0,
    error_count     INTEGER DEFAULT 0,
    stage_progress  TEXT,
    error_message   TEXT,
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_conversation_outcomes TO invekto;
GRANT USAGE, SELECT ON SEQUENCE wa_conversation_outcomes_id_seq TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_batch_jobs TO invekto;
GRANT USAGE, SELECT ON SEQUENCE wa_batch_jobs_id_seq TO invekto;
