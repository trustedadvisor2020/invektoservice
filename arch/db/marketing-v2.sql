-- =============================================================
-- Invekto.Marketing Database Schema v2
-- Service: Invekto.Marketing (port 7112)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- GR-3.24: Proactive Review Rescue
-- GR-3.25: Multilingual Medical Tourism
-- Depends on: tenant-registry.sql, marketing.sql
-- =============================================================

-- =============================================================
-- review_risks: Review risk assessment tracking (GR-3.24)
-- Each row = one detected risk of negative review.
-- Risk scoring done externally, Marketing stores + tracks rescue.
-- Status lifecycle: pending -> in_progress -> rescued | failed | expired
-- =============================================================

CREATE TABLE IF NOT EXISTS review_risks (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    customer_phone      VARCHAR(20) NOT NULL,
    conversation_id     VARCHAR(100),                       -- external reference
    risk_score          SMALLINT NOT NULL,                  -- 0-100
    risk_level          VARCHAR(20) NOT NULL,               -- 'low','medium','high','critical'
    trigger_reason      TEXT,                               -- "sentiment:-0.8, keyword:iade, timing:T+2h"
    rescue_status       VARCHAR(20) NOT NULL DEFAULT 'pending',
    rescue_strategy     VARCHAR(50),                        -- 'apology','discount','free_return','exchange','full_refund'
    rescue_cost         DECIMAL(10,2),                      -- cost of rescue action (discount amount, etc.)
    customer_response   VARCHAR(20),                        -- 'satisfied','unsatisfied','no_response'
    review_posted       BOOLEAN NOT NULL DEFAULT FALSE,     -- did the customer post a review anyway?
    review_rating       SMALLINT,                           -- 1-5 if posted
    followup_status     VARCHAR(30) NOT NULL DEFAULT 'none', -- PKT-12 Faz 3: follow-up tracking
    followup_sent_at    TIMESTAMPTZ,                         -- PKT-12 Faz 3: when last follow-up was sent
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at         TIMESTAMPTZ,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_risk_score CHECK (risk_score >= 0 AND risk_score <= 100),
    CONSTRAINT chk_risk_level CHECK (risk_level IN ('low', 'medium', 'high', 'critical')),
    CONSTRAINT chk_rescue_status CHECK (rescue_status IN ('pending', 'in_progress', 'rescued', 'failed', 'expired')),
    CONSTRAINT chk_rescue_strategy CHECK (rescue_strategy IS NULL OR rescue_strategy IN ('apology', 'discount', 'free_return', 'exchange', 'full_refund')),
    CONSTRAINT chk_risk_review_rating CHECK (review_rating IS NULL OR (review_rating >= 1 AND review_rating <= 5)),
    CONSTRAINT chk_customer_response CHECK (customer_response IS NULL OR customer_response IN ('satisfied', 'unsatisfied', 'no_response')),
    CONSTRAINT chk_followup_status CHECK (followup_status IN ('none', 'satisfaction_sent', 'review_redirect_sent', 'completed', 'closed'))
);

CREATE INDEX IF NOT EXISTS idx_review_risks_tenant_level
    ON review_risks (tenant_id, risk_level);

CREATE INDEX IF NOT EXISTS idx_review_risks_tenant_status
    ON review_risks (tenant_id, rescue_status);

CREATE INDEX IF NOT EXISTS idx_review_risks_tenant_phone
    ON review_risks (tenant_id, customer_phone);

-- PKT-12 Faz 3: Follow-up due query index
CREATE INDEX IF NOT EXISTS idx_review_risks_followup_due
    ON review_risks (rescue_status, followup_status, updated_at)
    WHERE rescue_status = 'rescued';

-- =============================================================
-- rescue_templates: Tenant-configurable rescue message templates (GR-3.24)
-- Each row = one rescue strategy template per risk level.
-- Tenant can have multiple templates per risk_level with different strategies.
-- =============================================================

CREATE TABLE IF NOT EXISTS rescue_templates (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    template_name       VARCHAR(200) NOT NULL,
    risk_level          VARCHAR(20) NOT NULL,               -- 'low','medium','high','critical'
    strategy            VARCHAR(50) NOT NULL,               -- 'apology','discount','free_return','exchange','full_refund'
    message_template    TEXT NOT NULL,                      -- message body with {{variable}} placeholders
    max_discount_pct    SMALLINT,                           -- max discount percentage for this strategy
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_template_risk_level CHECK (risk_level IN ('low', 'medium', 'high', 'critical')),
    CONSTRAINT chk_template_strategy CHECK (strategy IN ('apology', 'discount', 'free_return', 'exchange', 'full_refund')),
    CONSTRAINT chk_template_discount CHECK (max_discount_pct IS NULL OR (max_discount_pct >= 0 AND max_discount_pct <= 100))
);

CREATE INDEX IF NOT EXISTS idx_rescue_templates_tenant_level
    ON rescue_templates (tenant_id, risk_level, is_active);

-- =============================================================
-- treatment_catalog: Tenant treatment/service catalog (GR-3.25)
-- Each row = one treatment/service offered by the clinic.
-- Used by TourismResponseGenerator to build Claude context.
-- =============================================================

CREATE TABLE IF NOT EXISTS treatment_catalog (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    treatment_name      VARCHAR(200) NOT NULL,              -- Turkish name
    treatment_name_en   VARCHAR(200),                       -- English name (optional)
    category            VARCHAR(100),                       -- 'rhinoplasty','dental_veneers','hair_transplant'
    price_min           DECIMAL(10,2),
    price_max           DECIMAL(10,2),
    price_currency      VARCHAR(3) NOT NULL DEFAULT 'EUR',  -- EUR, USD, GBP
    duration_days       SMALLINT,                           -- treatment duration in days
    recovery_days       SMALLINT,                           -- recovery period in days
    description_tr      TEXT,                               -- Turkish description
    description_en      TEXT,                               -- English description
    package_includes    TEXT,                               -- plain text: hotel, transfer, etc.
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_treatment_catalog_tenant_category
    ON treatment_catalog (tenant_id, category, is_active);

-- =============================================================
-- tourism_conversations: Multilingual conversation tracking (GR-3.25)
-- Each row = one patient message + AI response pair.
-- Links to medical_tourism_leads via lead_id (optional).
-- =============================================================

CREATE TABLE IF NOT EXISTS tourism_conversations (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    lead_id             INTEGER REFERENCES medical_tourism_leads(id),
    patient_phone       VARCHAR(20) NOT NULL,
    patient_lang        VARCHAR(5) NOT NULL,                -- ISO 639-1: en, ar, ru, de, fr, etc.
    patient_country     VARCHAR(3),                         -- ISO 3166-1 alpha-3: GBR, ARE, DEU
    patient_message     TEXT NOT NULL,                      -- original patient message
    detected_intent     VARCHAR(100),                       -- 'treatment_inquiry','price_query','package_query','availability','photo_consultation','general'
    ai_response         TEXT,                               -- generated response in patient's language
    ai_response_lang    VARCHAR(5),                         -- language of the AI response
    tr_translation      TEXT,                               -- Turkish translation for clinic staff
    treatment_interest  VARCHAR(200),                       -- extracted treatment interest
    response_generated  BOOLEAN NOT NULL DEFAULT FALSE,     -- was AI response successfully generated?
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tourism_conversations_tenant_lang
    ON tourism_conversations (tenant_id, patient_lang);

CREATE INDEX IF NOT EXISTS idx_tourism_conversations_tenant_created
    ON tourism_conversations (tenant_id, created_at DESC);

-- =============================================================
-- Triggers: auto-update updated_at on row change
-- update_updated_at_column() function already defined in tenant-registry.sql
-- =============================================================

CREATE OR REPLACE TRIGGER trigger_review_risks_updated_at
    BEFORE UPDATE ON review_risks
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_rescue_templates_updated_at
    BEFORE UPDATE ON rescue_templates
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_treatment_catalog_updated_at
    BEFORE UPDATE ON treatment_catalog
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- tourism_conversations has no updated_at (immutable conversation records)

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON review_risks TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON rescue_templates TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON treatment_catalog TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON tourism_conversations TO invekto;
GRANT USAGE, SELECT ON SEQUENCE review_risks_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE rescue_templates_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE treatment_catalog_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE tourism_conversations_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. review_risks: Created when risk is detected externally (e.g. by ChatAnalysis sentiment).
--    Risk level thresholds: LOW 0-30, MEDIUM 30-60, HIGH 60-80, CRITICAL 80-100.
--    rescue_strategy set when rescue action is chosen.
--    customer_response tracked after rescue attempt.
-- 2. rescue_templates: Tenant-configurable rescue message templates.
--    Each template has a risk_level + strategy combo.
--    message_template supports {{customer_name}}, {{product}}, {{discount_pct}} placeholders.
--    max_discount_pct = maximum allowed discount for this strategy.
-- 3. treatment_catalog: Tenant treatment/service catalog for tourism responses.
--    Used by TourismResponseGenerator to build Claude AI context.
--    package_includes: plain text listing (hotel, transfer, pre-op tests, etc.)
-- 4. tourism_conversations: Immutable log of patient messages + AI responses.
--    lead_id links to medical_tourism_leads for pipeline tracking.
--    tr_translation = Turkish translation shown to clinic staff.
--    response_generated = FALSE if Claude was unavailable.
-- =============================================================
