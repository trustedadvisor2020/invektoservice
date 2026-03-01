-- RI Faz 3: Insight Engine Tables
-- Run on PostgreSQL production database
-- Date: 2026-03-01

-- RI-3.1: Response Time Correlation
CREATE TABLE IF NOT EXISTS wa_response_times (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    conversation_id TEXT NOT NULL,
    instance_id     INT,
    first_customer_msg_at  TIMESTAMPTZ,
    first_agent_response_at TIMESTAMPTZ,
    response_time_ms BIGINT,
    bucket          TEXT NOT NULL,
    outcome_label   TEXT,
    computed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_wa_response_times_tenant
    ON wa_response_times(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_response_times_bucket
    ON wa_response_times(tenant_id, bucket);

-- RI-3.3: Agent Leaderboard (Paket 2'de kullanilacak)
CREATE TABLE IF NOT EXISTS wa_agent_metrics (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    instance_id     INT,
    agent_id        INT NOT NULL,
    agent_name      TEXT NOT NULL,
    total_conversations INT NOT NULL DEFAULT 0,
    sale_count      INT NOT NULL DEFAULT 0,
    offered_count   INT NOT NULL DEFAULT 0,
    no_response_count INT NOT NULL DEFAULT 0,
    offer_lost_count INT NOT NULL DEFAULT 0,
    other_count     INT NOT NULL DEFAULT 0,
    conversion_rate REAL NOT NULL DEFAULT 0,
    avg_response_time_ms BIGINT,
    ghost_rate      REAL NOT NULL DEFAULT 0,
    weighted_score  REAL NOT NULL DEFAULT 0,
    metrics_json    JSONB,
    computed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, instance_id, agent_id)
);

CREATE INDEX IF NOT EXISTS idx_wa_agent_metrics_tenant
    ON wa_agent_metrics(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_agent_metrics_score
    ON wa_agent_metrics(tenant_id, weighted_score DESC);

-- RI-3.6: Follow-up Rescue Candidates (Paket 3'te kullanilacak)
CREATE TABLE IF NOT EXISTS wa_rescue_candidates (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    conversation_id TEXT NOT NULL,
    instance_id     INT,
    outcome_label   TEXT NOT NULL,
    last_message_at TIMESTAMPTZ,
    last_message_from TEXT,
    days_since      INT NOT NULL DEFAULT 0,
    estimated_value DECIMAL(12,2),
    rescue_status   TEXT NOT NULL DEFAULT 'pending',
    computed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_wa_rescue_candidates_tenant
    ON wa_rescue_candidates(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_rescue_candidates_status
    ON wa_rescue_candidates(tenant_id, rescue_status);

-- RI-3.2: Demand Heatmap (Paket 4)
-- instance_id NOT NULL DEFAULT 0 to ensure UNIQUE constraint works (PG treats NULL as distinct)
CREATE TABLE IF NOT EXISTS wa_demand_heatmap (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    instance_id     INT NOT NULL DEFAULT 0,  -- 0 = unknown/aggregate
    day_of_week     INT NOT NULL,  -- 0=Monday, 6=Sunday
    hour_of_day     INT NOT NULL,  -- 0-23
    total_conversations INT NOT NULL DEFAULT 0,
    sale_count      INT NOT NULL DEFAULT 0,
    conversion_rate REAL NOT NULL DEFAULT 0,
    avg_response_time_ms BIGINT,
    computed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, instance_id, day_of_week, hour_of_day)
);

CREATE INDEX IF NOT EXISTS idx_wa_demand_heatmap_tenant
    ON wa_demand_heatmap(tenant_id);

-- Grants
GRANT ALL ON wa_response_times TO invekto;
GRANT ALL ON wa_agent_metrics TO invekto;
GRANT ALL ON wa_rescue_candidates TO invekto;
GRANT ALL ON wa_demand_heatmap TO invekto;
GRANT ALL ON SEQUENCE wa_response_times_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_agent_metrics_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_rescue_candidates_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_demand_heatmap_id_seq TO invekto;
