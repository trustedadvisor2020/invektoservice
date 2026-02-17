-- PKT-6B1: Niche Business Logic
-- GR-3.8 İade Çevirme v1+v2, GR-3.13 Lead Management v2, GR-3.16 Negatif Yorum Kurtarma
-- Run on: invekto database (PostgreSQL 16)
-- Depends on: tenant_registry, automation.sql, outbound.sql, integrations.sql

-- ============================================================
-- GR-3.8 + GR-3.17: İade Çevirme (return_deflections)
-- ============================================================

CREATE TABLE IF NOT EXISTS return_deflections (
    id              SERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    conversation_id VARCHAR(100),
    customer_phone  VARCHAR(20) NOT NULL,
    original_intent VARCHAR(50) NOT NULL DEFAULT 'return_request',
    reason_category VARCHAR(30) NOT NULL,
        -- beden, renk, hasar, kalite, fikir_degisiklik, diger
    reason_text     TEXT,
    action_taken    VARCHAR(30) NOT NULL,
        -- exchange_offered, coupon_offered, return_started, handoff
    coupon_code     VARCHAR(50),
    coupon_value    DECIMAL(10,2),
    exchange_product VARCHAR(200),
    was_deflected   BOOLEAN DEFAULT FALSE,
    deflection_revenue DECIMAL(10,2),
    follow_up_sent  BOOLEAN DEFAULT FALSE,
    follow_up_at    TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_return_deflections_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_return_deflections_tenant
    ON return_deflections(tenant_id);
CREATE INDEX IF NOT EXISTS idx_return_deflections_phone
    ON return_deflections(tenant_id, customer_phone);
CREATE INDEX IF NOT EXISTS idx_return_deflections_created
    ON return_deflections(tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_return_deflections_followup
    ON return_deflections(tenant_id, follow_up_at)
    WHERE follow_up_sent = FALSE AND follow_up_at IS NOT NULL;

GRANT ALL ON return_deflections TO invekto_app;
GRANT USAGE, SELECT ON SEQUENCE return_deflections_id_seq TO invekto_app;

-- ============================================================
-- GR-3.16: Negatif Yorum Kurtarma (review_alerts)
-- ============================================================

CREATE TABLE IF NOT EXISTS review_alerts (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INT NOT NULL,
    provider            VARCHAR(30) NOT NULL,
        -- trendyol, hepsiburada, google, manual
    external_review_id  VARCHAR(200),
    rating              INT NOT NULL,
    review_text         TEXT,
    customer_phone      VARCHAR(20),
    order_id            VARCHAR(100),
    recovery_status     VARCHAR(20) NOT NULL DEFAULT 'pending',
        -- pending, contacted, resolved, unresolved, expired
    recovery_message    TEXT,
    recovery_attempt    INT NOT NULL DEFAULT 0,
    last_attempt_at     TIMESTAMPTZ,
    resolved_at         TIMESTAMPTZ,
    customer_response   VARCHAR(20),
        -- satisfied, unsatisfied, no_response
    review_updated      BOOLEAN DEFAULT FALSE,
    new_rating          INT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_review_alerts_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_review_alerts_rating
        CHECK (rating BETWEEN 1 AND 5),
    CONSTRAINT chk_review_alerts_new_rating
        CHECK (new_rating IS NULL OR new_rating BETWEEN 1 AND 5)
);

CREATE INDEX IF NOT EXISTS idx_review_alerts_tenant
    ON review_alerts(tenant_id);
CREATE INDEX IF NOT EXISTS idx_review_alerts_status
    ON review_alerts(tenant_id, recovery_status)
    WHERE recovery_status IN ('pending', 'contacted');
CREATE INDEX IF NOT EXISTS idx_review_alerts_provider
    ON review_alerts(tenant_id, provider, external_review_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_review_alerts_external
    ON review_alerts(tenant_id, provider, external_review_id)
    WHERE external_review_id IS NOT NULL;

GRANT ALL ON review_alerts TO invekto_app;
GRANT USAGE, SELECT ON SEQUENCE review_alerts_id_seq TO invekto_app;

-- ============================================================
-- GR-3.13: Lead Management v2 (leads + lead_activities)
-- ============================================================

CREATE TABLE IF NOT EXISTS leads (
    id              SERIAL PRIMARY KEY,
    tenant_id       INT NOT NULL,
    phone           VARCHAR(20) NOT NULL,
    name            VARCHAR(200),
    email           VARCHAR(200),
    source          VARCHAR(30) NOT NULL DEFAULT 'organic',
        -- instagram, google, referral, organic, whatsapp, trendyol, hepsiburada
    utm_source      VARCHAR(100),
    utm_medium      VARCHAR(100),
    utm_campaign    VARCHAR(200),
    interest        VARCHAR(200),
    score           INT NOT NULL DEFAULT 0,
        -- 0-100, computed from interest + budget + timing signals
    pipeline_status VARCHAR(30) NOT NULL DEFAULT 'new',
        -- new, contacted, consultation, appointment, patient, lost
    assigned_to     VARCHAR(100),
    last_contact_at TIMESTAMPTZ,
    next_followup_at TIMESTAMPTZ,
    followup_count  INT NOT NULL DEFAULT 0,
    notes           TEXT,
    is_hot          BOOLEAN NOT NULL DEFAULT FALSE,
    hot_alert_sent  BOOLEAN NOT NULL DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_leads_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_leads_score
        CHECK (score BETWEEN 0 AND 100)
);

CREATE INDEX IF NOT EXISTS idx_leads_tenant
    ON leads(tenant_id);
CREATE INDEX IF NOT EXISTS idx_leads_phone
    ON leads(tenant_id, phone);
CREATE INDEX IF NOT EXISTS idx_leads_pipeline
    ON leads(tenant_id, pipeline_status);
CREATE INDEX IF NOT EXISTS idx_leads_followup
    ON leads(tenant_id, next_followup_at)
    WHERE pipeline_status NOT IN ('patient', 'lost');
CREATE INDEX IF NOT EXISTS idx_leads_hot
    ON leads(tenant_id)
    WHERE is_hot = TRUE AND hot_alert_sent = FALSE;
CREATE UNIQUE INDEX IF NOT EXISTS uq_leads_tenant_phone
    ON leads(tenant_id, phone);

GRANT ALL ON leads TO invekto_app;
GRANT USAGE, SELECT ON SEQUENCE leads_id_seq TO invekto_app;

CREATE TABLE IF NOT EXISTS lead_activities (
    id              SERIAL PRIMARY KEY,
    lead_id         INT NOT NULL,
    tenant_id       INT NOT NULL,
    activity_type   VARCHAR(30) NOT NULL,
        -- created, status_change, note, followup_sent, followup_response, score_change, hot_alert
    old_value       VARCHAR(100),
    new_value       VARCHAR(100),
    note            TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_lead_activities_lead
        FOREIGN KEY (lead_id) REFERENCES leads(id) ON DELETE CASCADE,
    CONSTRAINT fk_lead_activities_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_lead_activities_lead
    ON lead_activities(lead_id);
CREATE INDEX IF NOT EXISTS idx_lead_activities_tenant
    ON lead_activities(tenant_id, created_at DESC);

GRANT ALL ON lead_activities TO invekto_app;
GRANT USAGE, SELECT ON SEQUENCE lead_activities_id_seq TO invekto_app;

-- ============================================================
-- service_pricing genişletme (GR-3.13 service_catalog ihtiyacı)
-- Mevcut appointments-v2.sql'deki service_pricing tablosuna
-- lead management için gerekli alanları ekle
-- ============================================================

ALTER TABLE service_pricing
    ADD COLUMN IF NOT EXISTS category VARCHAR(100),
    ADD COLUMN IF NOT EXISTS duration_minutes INT,
    ADD COLUMN IF NOT EXISTS recovery_days INT,
    ADD COLUMN IF NOT EXISTS description_tr TEXT,
    ADD COLUMN IF NOT EXISTS description_en TEXT;
