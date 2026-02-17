-- =============================================================
-- Ads Attribution Database Schema
-- Service: Invekto.Backend (port 5000)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- GR-3.14: Ads Attribution (PKT-5B)
-- =============================================================

-- Depends on: tenant-registry.sql (tenant_registry table)

-- =============================================================
-- lead_attributions: UTM/Meta click attribution per lead
-- One row per conversation_started event with UTM data.
-- =============================================================

CREATE TABLE IF NOT EXISTS lead_attributions (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    customer_phone      VARCHAR(20) NOT NULL,
    chat_id             INTEGER,                            -- Main App chat ID
    utm_source          VARCHAR(100),                       -- google, meta, tiktok, etc.
    utm_medium          VARCHAR(100),                       -- cpc, cpm, social, email, etc.
    utm_campaign        VARCHAR(200),                       -- campaign name
    utm_content         VARCHAR(200),                       -- ad content identifier
    utm_term            VARCHAR(200),                       -- keyword (for search ads)
    meta_click_id       VARCHAR(200),                       -- fbclid / ctwa_clid
    lead_source         VARCHAR(50) NOT NULL DEFAULT 'direct', -- auto-detected: meta_ad, google_ad, organic, direct, referral
    lead_labels         JSONB DEFAULT '[]',                 -- auto-tagged labels ["summer_sale", "vip"]
    conversion_status   VARCHAR(20) NOT NULL DEFAULT 'new', -- new, contacted, qualified, converted, lost
    conversion_value    DECIMAL(12,2),
    converted_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_lead_conversion_status CHECK (conversion_status IN ('new', 'contacted', 'qualified', 'converted', 'lost'))
);

CREATE INDEX IF NOT EXISTS idx_lead_attr_tenant_source
    ON lead_attributions (tenant_id, lead_source);

CREATE INDEX IF NOT EXISTS idx_lead_attr_tenant_campaign
    ON lead_attributions (tenant_id, utm_campaign) WHERE utm_campaign IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_lead_attr_tenant_phone
    ON lead_attributions (tenant_id, customer_phone);

CREATE INDEX IF NOT EXISTS idx_lead_attr_tenant_created
    ON lead_attributions (tenant_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_lead_attr_tenant_status
    ON lead_attributions (tenant_id, conversion_status) WHERE conversion_status NOT IN ('converted', 'lost');

-- =============================================================
-- ad_costs: Manual ad cost entries for CPL calculation
-- Entered by tenant admin via Dashboard.
-- =============================================================

CREATE TABLE IF NOT EXISTS ad_costs (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    platform            VARCHAR(50) NOT NULL,               -- meta, google, tiktok, linkedin, other
    campaign_name       VARCHAR(200),                       -- optional: match with utm_campaign
    cost_amount         DECIMAL(12,2) NOT NULL,
    currency            VARCHAR(3) NOT NULL DEFAULT 'TRY',
    period_start        DATE NOT NULL,
    period_end          DATE NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_ad_cost_period CHECK (period_start <= period_end),
    CONSTRAINT chk_ad_cost_amount CHECK (cost_amount >= 0)
);

CREATE INDEX IF NOT EXISTS idx_ad_costs_tenant_platform
    ON ad_costs (tenant_id, platform);

CREATE INDEX IF NOT EXISTS idx_ad_costs_tenant_period
    ON ad_costs (tenant_id, period_start, period_end);

-- =============================================================
-- Triggers: auto-update updated_at on row change
-- update_updated_at_column() function already defined in tenant-registry.sql
-- =============================================================

CREATE OR REPLACE TRIGGER trigger_lead_attributions_updated_at
    BEFORE UPDATE ON lead_attributions
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_ad_costs_updated_at
    BEFORE UPDATE ON ad_costs
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON lead_attributions TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON ad_costs TO invekto;
GRANT USAGE, SELECT ON SEQUENCE lead_attributions_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE ad_costs_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. lead_attributions: One row per conversation_started webhook with UTM data.
--    lead_source auto-detected from utm_source + meta_click_id.
--    conversion_status updated via API when lead progresses.
-- 2. ad_costs: Manual entries from Dashboard. Matched to lead_attributions
--    via platform (lead_source prefix) and campaign_name (utm_campaign).
--    CPL = cost_amount / lead_count for matching period + platform.
-- 3. tenant_id WHERE clause REQUIRED on all queries (multi-tenant isolation).
-- =============================================================
