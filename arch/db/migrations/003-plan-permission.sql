-- Migration 003: Plan Permission System — Faz 1
-- Date: 2026-03-02
-- Description: Rename plan_tier values to Turkish + create plan_definitions table
-- Safe: UPDATE with WHERE (no schema change), CREATE TABLE IF NOT EXISTS
-- Precondition: update_updated_at_column() trigger function exists (from setup-postgresql.sql)

-- ============================================================
-- Part A: Rename plan_tier values (basic → baslangic, etc.)
-- ============================================================

UPDATE tenant_registry SET plan_tier = 'baslangic'   WHERE plan_tier = 'basic';
UPDATE tenant_registry SET plan_tier = 'profesyonel' WHERE plan_tier = 'pro';
UPDATE tenant_registry SET plan_tier = 'kurumsal'    WHERE plan_tier = 'enterprise';

-- Update default value
ALTER TABLE tenant_registry ALTER COLUMN plan_tier SET DEFAULT 'baslangic';

-- Verify: SELECT plan_tier, count(*) FROM tenant_registry GROUP BY plan_tier;

-- ============================================================
-- Part B: Create plan_definitions table
-- ============================================================

CREATE TABLE IF NOT EXISTS plan_definitions (
    tier_name       VARCHAR(20) PRIMARY KEY,    -- 'baslangic', 'profesyonel', 'kurumsal'
    display_name    VARCHAR(100) NOT NULL,       -- UI display: 'Başlangıç', 'Profesyonel', 'Kurumsal'
    features_json   JSONB NOT NULL,              -- {"Feature": ["mode1","mode2"]} — available features per tier
    quotas_json     JSONB NOT NULL,              -- {"messages_per_month": 1000, ...} — -1 = unlimited
    is_active       BOOLEAN NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE TRIGGER trigger_plan_definitions_updated_at
    BEFORE UPDATE ON plan_definitions
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================
-- Part C: Seed plan definitions (idempotent — ON CONFLICT DO UPDATE)
-- ============================================================

INSERT INTO plan_definitions (tier_name, display_name, features_json, quotas_json)
VALUES
(
    'baslangic',
    'Başlangıç',
    '{"WebChat": ["on"], "FlowBuilder": ["basic"], "Analytics": ["basic"], "Integrations": ["basic"]}',
    '{"messages_per_month": 1000, "max_users": 5, "max_flows": 3}'
),
(
    'profesyonel',
    'Profesyonel',
    '{"WebChat": ["on"], "FlowBuilder": ["basic", "premium"], "Knowledge": ["basic"], "Outbound": ["basic"], "Appointments": ["on"], "Analytics": ["basic", "premium"], "Integrations": ["basic"], "AgentAI": ["on"]}',
    '{"messages_per_month": 10000, "max_users": 20, "max_flows": -1}'
),
(
    'kurumsal',
    'Kurumsal',
    '{"WebChat": ["on"], "FlowBuilder": ["basic", "premium"], "Knowledge": ["basic", "premium"], "Outbound": ["basic", "premium"], "Appointments": ["on"], "Analytics": ["basic", "premium"], "Integrations": ["basic", "premium"], "Marketing": ["on"], "AgentAI": ["on", "premium"]}',
    '{"messages_per_month": -1, "max_users": -1, "max_flows": -1}'
)
ON CONFLICT (tier_name) DO UPDATE
    SET features_json = EXCLUDED.features_json,
        quotas_json   = EXCLUDED.quotas_json,
        display_name  = EXCLUDED.display_name,
        updated_at    = NOW();

-- ============================================================
-- Part D: Grants
-- ============================================================

GRANT ALL ON plan_definitions TO invekto;

-- Verify: SELECT tier_name, display_name, features_json, quotas_json FROM plan_definitions;
