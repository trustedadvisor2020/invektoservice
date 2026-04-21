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
-- FEAT-EFS Drip Sequence (Migration 029 — canonical schema mirror)
-- =============================================================
-- This section is the authoritative schema source-of-truth; migration 029 is the
-- cumulative delta for databases that predate FEAT-EFS. New fresh-create installs
-- run this full marketing.sql and skip migration 029 entirely.

CREATE TABLE IF NOT EXISTS event_followup_sequences (
    id                BIGSERIAL PRIMARY KEY,
    tenant_id         INT NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    slug              VARCHAR(64) NOT NULL,
    stages            JSONB NOT NULL,
    ab_split_percent  SMALLINT NOT NULL DEFAULT 50,
    enabled           BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT efs_sequences_slug_unique_per_tenant UNIQUE (tenant_id, slug),
    CONSTRAINT efs_sequences_ab_split_range CHECK (ab_split_percent BETWEEN 0 AND 100)
);

CREATE TABLE IF NOT EXISTS event_followup_runs (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    sequence_id     BIGINT NOT NULL REFERENCES event_followup_sequences(id) ON DELETE CASCADE,
    lead_id         BIGINT NOT NULL REFERENCES leads(id),
    stage_index     SMALLINT NOT NULL,
    ab_group        VARCHAR(10) NOT NULL,
    scheduled_at    TIMESTAMPTZ NOT NULL,
    executed_at     TIMESTAMPTZ NULL,
    status          VARCHAR(32) NOT NULL DEFAULT 'scheduled',
    hangfire_job_id VARCHAR(64) NULL,
    error_code      VARCHAR(32) NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT efs_runs_status_enum CHECK (status IN ('scheduled','sent','skipped_optout','skipped_disabled','failed')),
    CONSTRAINT efs_runs_ab_group_enum CHECK (ab_group IN ('drip','control')),
    CONSTRAINT efs_runs_stage_index_nonneg CHECK (stage_index >= 0)
);

CREATE INDEX IF NOT EXISTS idx_efs_runs_tenant_lead_status
    ON event_followup_runs (tenant_id, lead_id, status);
CREATE INDEX IF NOT EXISTS idx_efs_runs_scheduled_at_pending
    ON event_followup_runs (scheduled_at)
    WHERE status = 'scheduled';
CREATE INDEX IF NOT EXISTS idx_efs_runs_sequence_stage
    ON event_followup_runs (sequence_id, stage_index);
CREATE UNIQUE INDEX IF NOT EXISTS uq_efs_runs_lead_stage_scheduled
    ON event_followup_runs (tenant_id, lead_id, stage_index)
    WHERE status = 'scheduled';

GRANT SELECT, INSERT, UPDATE, DELETE ON event_followup_sequences TO invekto;
GRANT SELECT, INSERT, UPDATE, DELETE ON event_followup_runs TO invekto;
GRANT USAGE, SELECT ON SEQUENCE event_followup_sequences_id_seq TO invekto;
GRANT USAGE, SELECT ON SEQUENCE event_followup_runs_id_seq TO invekto;

-- tenant_settings FEAT-EFS flags (efs_test_mode, efs_no_reply_threshold_days) are
-- canonical in arch/db/tenant-settings.sql + migration 029.
-- leads FEAT-EFS columns (followup_state JSONB, followup_ab_group VARCHAR(10)) are
-- canonical in arch/db/pkt6b1-niche-business.sql + migration 029.

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
-- 4. event_followup_sequences / event_followup_runs: FEAT-EFS drip nurture.
--    Sequences are per-tenant + slug; runs are per-lead-per-stage with Hangfire
--    BackgroundJob.Schedule. Execution-time opt-out guard reads
--    inma_optout_outbox (arch/db/outbound.sql) latest event by (tenant_id, phone).
--    Partial unique index closes the concurrent-trigger race at the DB layer.
-- =============================================================
