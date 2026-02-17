-- =============================================================
-- Invekto.Marketing Database Schema
-- Service: Invekto.Marketing (port 7112)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- GR-3.21: Google Yorum + Referans Motoru
-- GR-3.22: Medikal Turizm Lead Capture
-- Depends on: tenant-registry.sql
-- =============================================================

-- =============================================================
-- review_requests: Google review request tracking
-- Each row = one review request sent to a patient after treatment.
-- Status lifecycle: pending -> sent -> posted | expired
-- =============================================================

CREATE TABLE IF NOT EXISTS review_requests (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    patient_phone       VARCHAR(20) NOT NULL,
    patient_name        VARCHAR(200),
    treatment_type      VARCHAR(100),
    satisfaction_score   SMALLINT,                       -- 1-5, null if unknown
    review_link_url     TEXT,                            -- Google Maps review link
    review_link_sent    BOOLEAN NOT NULL DEFAULT FALSE,
    review_posted       BOOLEAN NOT NULL DEFAULT FALSE,
    review_rating       SMALLINT,                        -- 1-5, null if not posted
    platform            VARCHAR(30) NOT NULL DEFAULT 'google', -- 'google', 'trustpilot', etc.
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_review_satisfaction CHECK (satisfaction_score IS NULL OR (satisfaction_score >= 1 AND satisfaction_score <= 5)),
    CONSTRAINT chk_review_rating CHECK (review_rating IS NULL OR (review_rating >= 1 AND review_rating <= 5)),
    CONSTRAINT chk_review_status CHECK (status IN ('pending', 'sent', 'posted', 'expired'))
);

CREATE INDEX IF NOT EXISTS idx_review_requests_tenant_status
    ON review_requests (tenant_id, status);

CREATE INDEX IF NOT EXISTS idx_review_requests_tenant_phone
    ON review_requests (tenant_id, patient_phone);

-- =============================================================
-- referrals: Patient referral tracking
-- Each row = one referral code issued to a patient.
-- Status lifecycle: active -> redeemed | expired
-- =============================================================

CREATE TABLE IF NOT EXISTS referrals (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    referrer_phone      VARCHAR(20) NOT NULL,
    referrer_name       VARCHAR(200),
    referee_phone       VARCHAR(20),                     -- null until redeemed
    referee_name        VARCHAR(200),
    referral_code       VARCHAR(12) NOT NULL,
    discount_pct        SMALLINT NOT NULL DEFAULT 10,    -- discount percentage for referee
    referrer_reward     VARCHAR(200),                    -- reward description for referrer
    status              VARCHAR(20) NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    redeemed_at         TIMESTAMPTZ,

    CONSTRAINT chk_referral_discount CHECK (discount_pct >= 0 AND discount_pct <= 100),
    CONSTRAINT chk_referral_status CHECK (status IN ('active', 'redeemed', 'expired'))
);

-- Unique referral code per tenant
CREATE UNIQUE INDEX IF NOT EXISTS idx_referrals_tenant_code
    ON referrals (tenant_id, referral_code);

CREATE INDEX IF NOT EXISTS idx_referrals_tenant_status
    ON referrals (tenant_id, status);

CREATE INDEX IF NOT EXISTS idx_referrals_tenant_referrer
    ON referrals (tenant_id, referrer_phone);

-- =============================================================
-- medical_tourism_leads: International patient lead tracking
-- Each row = one foreign patient inquiry.
-- Status lifecycle: new -> contacted -> consultation -> booked -> treated -> reviewed | lost
-- =============================================================

CREATE TABLE IF NOT EXISTS medical_tourism_leads (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    patient_phone       VARCHAR(20) NOT NULL,
    patient_name        VARCHAR(200),
    patient_country     VARCHAR(3),                      -- ISO 3166-1 alpha-3 (GBR, ARE, DEU)
    patient_lang        VARCHAR(5) NOT NULL DEFAULT 'en',-- ISO 639-1 (en, ar, ru, de)
    treatment_interest  VARCHAR(200),                    -- e.g. 'rhinoplasty', 'dental_veneers'
    accommodation_needed BOOLEAN NOT NULL DEFAULT FALSE,
    transfer_needed     BOOLEAN NOT NULL DEFAULT FALSE,
    budget_currency     VARCHAR(3) DEFAULT 'EUR',        -- EUR, USD, GBP
    budget_amount       DECIMAL(10,2),
    source              VARCHAR(50),                     -- 'instagram', 'google', 'referral', 'website'
    notes               TEXT,
    status              VARCHAR(20) NOT NULL DEFAULT 'new',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT chk_tourism_status CHECK (status IN ('new', 'contacted', 'consultation', 'booked', 'treated', 'reviewed', 'lost'))
);

CREATE INDEX IF NOT EXISTS idx_tourism_leads_tenant_status
    ON medical_tourism_leads (tenant_id, status);

CREATE INDEX IF NOT EXISTS idx_tourism_leads_tenant_country
    ON medical_tourism_leads (tenant_id, patient_country);

CREATE INDEX IF NOT EXISTS idx_tourism_leads_tenant_phone
    ON medical_tourism_leads (tenant_id, patient_phone);

-- =============================================================
-- Triggers: auto-update updated_at on row change
-- update_updated_at_column() function already defined in tenant-registry.sql
-- =============================================================

CREATE OR REPLACE TRIGGER trigger_review_requests_updated_at
    BEFORE UPDATE ON review_requests
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE TRIGGER trigger_medical_tourism_leads_updated_at
    BEFORE UPDATE ON medical_tourism_leads
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =============================================================
-- Grants (run after creating tables)
-- =============================================================

GRANT SELECT, INSERT, UPDATE, DELETE ON review_requests TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON referrals TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON medical_tourism_leads TO invekto;
GRANT USAGE, SELECT ON SEQUENCE review_requests_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE referrals_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE medical_tourism_leads_id_seq TO invekto;

-- =============================================================
-- Usage Notes
-- =============================================================
--
-- 1. review_requests: Created via POST /api/v1/reviews/request.
--    review_link_url = Google Maps review link (tenant provides).
--    After sending, set review_link_sent = TRUE, status = 'sent'.
--    If patient posts review, set review_posted = TRUE, review_rating, status = 'posted'.
-- 2. referrals: Created via POST /api/v1/referrals.
--    referral_code = crypto-random 8-char alphanumeric (unique per tenant).
--    When referee uses code: set referee_phone, status = 'redeemed', redeemed_at.
-- 3. medical_tourism_leads: Created via POST /api/v1/tourism/leads.
--    Pipeline: new -> contacted -> consultation -> booked -> treated -> reviewed.
--    patient_lang used for response language (EN only in MVP).
-- =============================================================
