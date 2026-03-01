-- RI Faz 4: Sector Template Mining Tables
-- Run on PostgreSQL production database
-- Date: 2026-03-01

-- RI-4.1: Intent Templates (per sector)
CREATE TABLE IF NOT EXISTS wa_sector_intents (
    id              BIGSERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,
    intent_name     VARCHAR(200) NOT NULL,
    description     TEXT,
    category        VARCHAR(100),
    examples        JSONB,                          -- ["Sac ekimi fiyat?", "Burun estetigi bilgi?"]
    keywords        JSONB,                          -- ["sac ekimi", "hair transplant", "fue"]
    priority        INT NOT NULL DEFAULT 0,
    frequency       INT NOT NULL DEFAULT 0,         -- how many times seen in data
    conversion_rate REAL,
    source_count    INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(sector, intent_name)
);

CREATE INDEX IF NOT EXISTS idx_wa_sector_intents_sector
    ON wa_sector_intents(sector);
CREATE INDEX IF NOT EXISTS idx_wa_sector_intents_priority
    ON wa_sector_intents(sector, priority DESC);

-- RI-4.2: FAQ Templates (per sector)
CREATE TABLE IF NOT EXISTS wa_sector_faqs (
    id              BIGSERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,
    question        TEXT NOT NULL,
    answer          TEXT NOT NULL,
    category        VARCHAR(100),
    keywords        JSONB,
    effectiveness_score REAL NOT NULL DEFAULT 0,    -- 0-100
    source_count    INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_wa_sector_faqs_sector
    ON wa_sector_faqs(sector);

-- RI-4.3: Flow Templates (per sector)
CREATE TABLE IF NOT EXISTS wa_sector_flows (
    id              BIGSERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,
    flow_name       VARCHAR(200) NOT NULL,
    flow_type       VARCHAR(50) NOT NULL,           -- ilk_temas, follow_up, rescue
    description     TEXT,
    stages          JSONB NOT NULL,                 -- [{name, description, avg_duration_hours, drop_off_rate}]
    drop_off_analysis JSONB,
    conversion_rate REAL,
    source_count    INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(sector, flow_name)
);

CREATE INDEX IF NOT EXISTS idx_wa_sector_flows_sector
    ON wa_sector_flows(sector);

-- RI-4.4: Objection Handling Templates (per sector)
CREATE TABLE IF NOT EXISTS wa_sector_objection_handlers (
    id              BIGSERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,
    objection_type  VARCHAR(50) NOT NULL,
    objection_label TEXT NOT NULL,
    response_templates JSONB NOT NULL,              -- [{text, effectiveness_score, rescue_rate}]
    total_occurrences INT NOT NULL DEFAULT 0,
    rescue_rate     REAL NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(sector, objection_type)
);

CREATE INDEX IF NOT EXISTS idx_wa_sector_objection_handlers_sector
    ON wa_sector_objection_handlers(sector);

-- RI-4.5: Follow-up Templates (per sector)
CREATE TABLE IF NOT EXISTS wa_sector_followup_templates (
    id              BIGSERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,
    template_type   VARCHAR(50) NOT NULL,           -- ilk_hatirlatma, ikinci_hatirlatma, son_sans, ozel_teklif
    template_label  VARCHAR(200) NOT NULL,
    message_template TEXT NOT NULL,
    optimal_delay_hours INT NOT NULL DEFAULT 48,
    rescue_rate     REAL NOT NULL DEFAULT 0,
    source_count    INT NOT NULL DEFAULT 0,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(sector, template_type)
);

CREATE INDEX IF NOT EXISTS idx_wa_sector_followup_templates_sector
    ON wa_sector_followup_templates(sector);

-- RI-4.6: Onboarding Checklists (per sector)
CREATE TABLE IF NOT EXISTS wa_sector_onboarding_steps (
    id              BIGSERIAL PRIMARY KEY,
    sector          VARCHAR(50) NOT NULL,
    step_number     INT NOT NULL,
    action          TEXT NOT NULL,
    description     TEXT,
    expected_impact TEXT,
    day_range       VARCHAR(20),                    -- "1-7", "8-14", "15-30"
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(sector, step_number)
);

CREATE INDEX IF NOT EXISTS idx_wa_sector_onboarding_steps_sector
    ON wa_sector_onboarding_steps(sector);

-- Grants
GRANT ALL ON wa_sector_intents TO invekto;
GRANT ALL ON wa_sector_faqs TO invekto;
GRANT ALL ON wa_sector_flows TO invekto;
GRANT ALL ON wa_sector_objection_handlers TO invekto;
GRANT ALL ON wa_sector_followup_templates TO invekto;
GRANT ALL ON wa_sector_onboarding_steps TO invekto;
GRANT ALL ON SEQUENCE wa_sector_intents_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_sector_faqs_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_sector_flows_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_sector_objection_handlers_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_sector_followup_templates_id_seq TO invekto;
GRANT ALL ON SEQUENCE wa_sector_onboarding_steps_id_seq TO invekto;
