-- ============================================================
-- wa_benchmark_jobs + wa_benchmark_results
-- LLM Benchmark: Multi-model outcome classification comparison
-- Phase RI-1A | Q | 2026-02-24
-- ============================================================

-- Job tracking (mirrors wa_analyses pattern)
CREATE TABLE IF NOT EXISTS wa_benchmark_jobs (
    id              SERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL DEFAULT 0,  -- 0 = ops-level (no FK, benchmark is cross-tenant)
    status          VARCHAR(20) NOT NULL DEFAULT 'pending',
    -- pending / sampling / classifying / completed / error
    database_name   TEXT NOT NULL,
    instance_id     INT,
    sample_size     INT NOT NULL DEFAULT 200,
    actual_sample   INT,
    config_json     JSONB,
    stage_progress  JSONB,
    results_summary JSONB,
    error_message   TEXT,
    started_at      TIMESTAMPTZ,
    completed_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_wa_benchmark_jobs_tenant
    ON wa_benchmark_jobs (tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_benchmark_jobs_status
    ON wa_benchmark_jobs (status);

-- Per-thread, per-model results (denormalized for trivial metric queries)
CREATE TABLE IF NOT EXISTS wa_benchmark_results (
    id                       BIGSERIAL PRIMARY KEY,
    benchmark_id             INT NOT NULL REFERENCES wa_benchmark_jobs(id) ON DELETE CASCADE,
    tenant_id                INT NOT NULL,
    conversation_id          TEXT NOT NULL,
    message_count            INT NOT NULL,
    thread_text_masked       TEXT,
    keyword_label            VARCHAR(30),
    claude_haiku_label       VARCHAR(30),
    claude_haiku_confidence  REAL,
    claude_haiku_evidence    TEXT,
    claude_sonnet_label      VARCHAR(30),
    claude_sonnet_confidence REAL,
    claude_sonnet_evidence   TEXT,
    gemini_flash_label       VARCHAR(30),
    gemini_flash_confidence  REAL,
    gemini_flash_evidence    TEXT,
    gemini_pro_label         VARCHAR(30),
    gemini_pro_confidence    REAL,
    gemini_pro_evidence      TEXT,
    gemini_3_flash_label     VARCHAR(30),
    gemini_3_flash_confidence REAL,
    gemini_3_flash_evidence  TEXT,
    tiered_label             VARCHAR(30),
    tiered_confidence        REAL,
    tiered_evidence          TEXT,
    has_offer                BOOLEAN,        -- Tiered model: was a concrete price/offer made? (taxonomy v0.4)
    ground_truth_label       VARCHAR(30),
    created_at               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_wa_benchmark_results_job
    ON wa_benchmark_results (benchmark_id);

-- Auto-update timestamp trigger
CREATE OR REPLACE FUNCTION update_wa_benchmark_jobs_updated_at()
RETURNS TRIGGER AS $$
BEGIN NEW.updated_at = NOW(); RETURN NEW; END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_wa_benchmark_jobs_updated_at ON wa_benchmark_jobs;
CREATE TRIGGER trg_wa_benchmark_jobs_updated_at
    BEFORE UPDATE ON wa_benchmark_jobs
    FOR EACH ROW EXECUTE FUNCTION update_wa_benchmark_jobs_updated_at();

-- Grants
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_benchmark_jobs TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_benchmark_results TO invekto;
GRANT USAGE, SELECT ON SEQUENCE wa_benchmark_jobs_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE wa_benchmark_results_id_seq TO invekto;
