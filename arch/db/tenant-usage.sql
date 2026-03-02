-- tenant_usage: Monthly usage counters for quota enforcement.
-- One row per (tenant_id, period_month). period_month = first day of month.
-- messages_sent: incremented by Automation + WebChat per billable message.
-- Created by migration 004-tenant-usage.sql.
--
-- Reset semantics: new month = new row (old rows kept for historical reference).
-- Quota check: compare messages_sent vs plan_definitions.quotas_json->>'messages_per_month'.
-- -1 in quotas_json = unlimited (no row needed).

CREATE TABLE IF NOT EXISTS tenant_usage (
    tenant_id       INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    period_month    DATE NOT NULL,              -- e.g., '2026-03-01' (always first of month)
    messages_sent   INTEGER NOT NULL DEFAULT 0,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (tenant_id, period_month)
);

CREATE INDEX IF NOT EXISTS idx_tenant_usage_period
    ON tenant_usage (period_month, tenant_id);

CREATE OR REPLACE TRIGGER trigger_tenant_usage_updated_at
    BEFORE UPDATE ON tenant_usage
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

GRANT ALL ON tenant_usage TO invekto;
