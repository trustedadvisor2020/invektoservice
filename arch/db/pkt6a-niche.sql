-- PKT-6A: Niche Foundation DDL
-- Service: Invekto.Knowledge (sector_intent_templates) + Invekto.Automation (vip_flags)
-- Database: invekto (same as tenant_registry)

-- ============================================================
-- 1. sector_intent_templates: Global sector intent template definitions
--    No tenant FK - these are global seeds used to onboard new tenants.
--    Sectors: eticaret, dis_klinik, estetik
--    Seeded via pkt6a-niche-seeds.sql
-- ============================================================
CREATE TABLE IF NOT EXISTS sector_intent_templates (
    id              SERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,                  -- eticaret, dis_klinik, estetik
    intent_name     VARCHAR(100) NOT NULL,                 -- return_request, emergency, etc.
    keywords        TEXT[] NOT NULL DEFAULT '{}',           -- Turkish keyword arrays
    description     VARCHAR(500),                          -- Human-readable description
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_sector_intent_template
        UNIQUE (sector, intent_name)
);

CREATE INDEX IF NOT EXISTS idx_sector_intent_templates_sector
    ON sector_intent_templates (sector);

-- ============================================================
-- 2. vip_flags: B2B/VIP lead detection results (Automation scope)
--    Written by VipDetectionService after message processing.
--    Upsert pattern: ON CONFLICT (tenant_id, phone) DO UPDATE.
-- ============================================================
CREATE TABLE IF NOT EXISTS vip_flags (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL,
    phone           VARCHAR(20) NOT NULL,                  -- Customer phone (E.164)
    vip_type        VARCHAR(30) NOT NULL DEFAULT 'b2b',    -- b2b, vip, high_value
    detection_score NUMERIC(5,2) NOT NULL DEFAULT 0,       -- 0.00 - 100.00
    matched_signals TEXT[] NOT NULL DEFAULT '{}',           -- Keywords that triggered detection
    first_detected  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_seen       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_vip_flags_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT uq_vip_flags_tenant_phone
        UNIQUE (tenant_id, phone),
    CONSTRAINT chk_vip_type
        CHECK (vip_type IN ('b2b', 'vip', 'high_value')),
    CONSTRAINT chk_detection_score
        CHECK (detection_score >= 0 AND detection_score <= 100)
);

CREATE INDEX IF NOT EXISTS idx_vip_flags_tenant_type
    ON vip_flags (tenant_id, vip_type);

-- ============================================================
-- Grant permissions
-- ============================================================
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO invekto;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
