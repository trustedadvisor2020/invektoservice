-- plan_definitions: Canonical feature set and quotas per plan tier.
-- Source of truth for what features are available per tier.
-- Managed by SuperAdmin. Created by migration 003-plan-permission.sql.
--
-- Tier values: 'baslangic', 'profesyonel', 'kurumsal'
-- features_json format: {"FeatureName": ["mode1", "mode2"]}
--   Key presence = feature available in plan. Modes: "on" (simple toggle), "basic", "premium".
-- quotas_json format: {"metric_name": limit} — -1 = unlimited

CREATE TABLE IF NOT EXISTS plan_definitions (
    tier_name       VARCHAR(20) PRIMARY KEY,
    display_name    VARCHAR(100) NOT NULL,
    features_json   JSONB NOT NULL,
    quotas_json     JSONB NOT NULL,
    is_active       BOOLEAN NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE TRIGGER trigger_plan_definitions_updated_at
    BEFORE UPDATE ON plan_definitions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

GRANT ALL ON plan_definitions TO invekto;
