-- ============================================================
-- PKT-3: Backend Analytics - Aggregated Metrics Tables
-- GR-2.5: Automation Dashboard + WA-4: BI Dashboard
-- Database: invekto (same as all services)
-- ============================================================
-- MetricsAggregationService (Backend IHostedService, 5dk periyodik)
-- aggregates from: auto_reply_log, chat_sessions
-- WA-4 queries: direct from wa_* tables (no aggregation needed)
-- ============================================================

-- 1. daily_metrics: Tenant bazli gunluk otomasyon metrikleri (aggregated)
CREATE TABLE IF NOT EXISTS daily_metrics (
    id                      SERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL,
    metric_date             DATE NOT NULL,
    total_replies           INTEGER NOT NULL DEFAULT 0,
    deflected_count         INTEGER NOT NULL DEFAULT 0,      -- menu, faq, intent, off_hours, welcome
    handoff_count           INTEGER NOT NULL DEFAULT 0,      -- handoff reply_type
    faq_count               INTEGER NOT NULL DEFAULT 0,
    intent_count            INTEGER NOT NULL DEFAULT 0,
    menu_count              INTEGER NOT NULL DEFAULT 0,
    off_hours_count         INTEGER NOT NULL DEFAULT 0,
    welcome_count           INTEGER NOT NULL DEFAULT 0,
    avg_processing_time_ms  REAL,
    avg_confidence          REAL,
    active_sessions         INTEGER NOT NULL DEFAULT 0,
    completed_sessions      INTEGER NOT NULL DEFAULT 0,
    handed_off_sessions     INTEGER NOT NULL DEFAULT 0,
    expired_sessions        INTEGER NOT NULL DEFAULT 0,
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_daily_metrics_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

-- Unique: bir tenant icin bir gunde tek kayit (UPSERT icin)
CREATE UNIQUE INDEX IF NOT EXISTS uq_daily_metrics_tenant_date
    ON daily_metrics (tenant_id, metric_date);

CREATE INDEX IF NOT EXISTS idx_daily_metrics_date
    ON daily_metrics (metric_date DESC);

-- 2. daily_intent_metrics: Intent bazli gunluk performans
CREATE TABLE IF NOT EXISTS daily_intent_metrics (
    id                      SERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL,
    metric_date             DATE NOT NULL,
    intent                  VARCHAR(50) NOT NULL,
    total_count             INTEGER NOT NULL DEFAULT 0,
    handoff_count           INTEGER NOT NULL DEFAULT 0,
    avg_confidence          REAL,
    avg_processing_time_ms  REAL,
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_daily_intent_metrics_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

-- Unique: bir tenant icin bir gunde bir intent icin tek kayit
CREATE UNIQUE INDEX IF NOT EXISTS uq_daily_intent_metrics_tenant_date_intent
    ON daily_intent_metrics (tenant_id, metric_date, intent);

CREATE INDEX IF NOT EXISTS idx_daily_intent_metrics_date
    ON daily_intent_metrics (metric_date DESC);

-- ============================================================
-- Triggers: updated_at otomatik guncelleme
-- ============================================================

CREATE OR REPLACE TRIGGER trigger_daily_metrics_updated_at
    BEFORE UPDATE ON daily_metrics
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_daily_intent_metrics_updated_at
    BEFORE UPDATE ON daily_intent_metrics
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- Grants
-- ============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON daily_metrics TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON daily_intent_metrics TO invekto;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
