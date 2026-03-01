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

-- RI-3.4: Revenue Attribution (Paket 5)
-- PG-only compute. Fixed TL values per outcome, 4 dimensions + summary.
-- instance_id NOT NULL DEFAULT 0 (same pattern as demand_heatmap)
CREATE TABLE IF NOT EXISTS wa_revenue_attribution (
    id                  BIGSERIAL PRIMARY KEY,
    tenant_id           INT NOT NULL,
    instance_id         INT NOT NULL DEFAULT 0,  -- 0 = aggregate
    dimension           TEXT NOT NULL,            -- outcome, hour, agent, instance, summary
    dimension_key       TEXT NOT NULL,            -- outcome_label, hour number, agent_id, instance_id, 'total'
    dimension_label     TEXT,                     -- display name
    total_conversations INT NOT NULL DEFAULT 0,
    attributed_revenue  DECIMAL(12,2) NOT NULL DEFAULT 0,
    avg_revenue         DECIMAL(12,2) NOT NULL DEFAULT 0,
    breakdown_json      JSONB,
    computed_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, instance_id, dimension, dimension_key)
);

CREATE INDEX IF NOT EXISTS idx_wa_revenue_attribution_tenant
    ON wa_revenue_attribution(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_revenue_attribution_dimension
    ON wa_revenue_attribution(tenant_id, dimension);

-- RI-3.5: Objection Map (Paket 6)
-- Evidence text'ten keyword-based objection type extraction.
-- offer_lost + offer_no_reply konusmalarindan itiraz sebepleri.
CREATE TABLE IF NOT EXISTS wa_objection_map (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    instance_id     INT NOT NULL DEFAULT 0,
    conversation_id TEXT NOT NULL,
    objection_type  VARCHAR(50) NOT NULL,       -- price_high, chose_competitor, not_ready, wrong_service, no_trust, timing, medical_concern, out_of_stock, location, unknown
    detail          TEXT,                        -- extracted reason text from evidence
    outcome_label   VARCHAR(30),                -- from wa_conversation_outcomes
    computed_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, conversation_id, objection_type)
);

CREATE INDEX IF NOT EXISTS idx_wa_objection_map_tenant
    ON wa_objection_map(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_objection_map_type
    ON wa_objection_map(tenant_id, objection_type);

-- RI-3.7: Conversation Quality Score (Paket 7)
-- Per-conversation quality computed from existing metrics (no LLM).
-- response_speed (from wa_response_times) + engagement (msg ratio) +
-- resolution (outcome-based) + sentiment (from wa_sentiments) = overall
CREATE TABLE IF NOT EXISTS wa_quality_scores (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INT NOT NULL,
    instance_id             INT NOT NULL DEFAULT 0,
    conversation_id         TEXT NOT NULL,
    agent_id                INT,
    agent_name              TEXT,
    response_speed_score    REAL NOT NULL DEFAULT 0,    -- 0-100: faster=better (from wa_response_times bucket)
    engagement_score        REAL NOT NULL DEFAULT 0,    -- 0-100: agent_msg_count / total_msg_count ratio
    resolution_score        REAL NOT NULL DEFAULT 0,    -- 0-100: outcome-based (sale=100, appt=90, offered=60, etc.)
    sentiment_score         REAL NOT NULL DEFAULT 0,    -- 0-100: from wa_sentiments (mapped -1..1 → 0..100)
    overall_score           REAL NOT NULL DEFAULT 0,    -- weighted: speed(25%) + engagement(25%) + resolution(30%) + sentiment(20%)
    computed_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, conversation_id)
);

CREATE INDEX IF NOT EXISTS idx_wa_quality_scores_tenant
    ON wa_quality_scores(tenant_id);
CREATE INDEX IF NOT EXISTS idx_wa_quality_scores_agent
    ON wa_quality_scores(tenant_id, agent_id);
CREATE INDEX IF NOT EXISTS idx_wa_quality_scores_overall
    ON wa_quality_scores(tenant_id, overall_score DESC);

-- Grants
GRANT ALL ON wa_response_times TO invekto;
GRANT ALL ON wa_agent_metrics TO invekto;
GRANT ALL ON wa_rescue_candidates TO invekto;
GRANT ALL ON wa_demand_heatmap TO invekto;
GRANT ALL ON SEQUENCE wa_response_times_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_agent_metrics_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_rescue_candidates_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_demand_heatmap_id_seq TO invekto;
GRANT ALL ON wa_revenue_attribution TO invekto;
GRANT ALL ON SEQUENCE wa_revenue_attribution_id_seq TO invekto;
GRANT ALL ON wa_objection_map TO invekto;
GRANT ALL ON SEQUENCE wa_objection_map_id_seq TO invekto;
GRANT ALL ON wa_quality_scores TO invekto;
GRANT ALL ON SEQUENCE wa_quality_scores_id_seq TO invekto;
