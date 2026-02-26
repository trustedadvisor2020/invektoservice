-- wa_sector_config: Sector metadata for classification pipeline
-- RI-2.9: Sector-specific config, label overrides, benchmark F1 tracking.

CREATE TABLE IF NOT EXISTS wa_sector_config (
    id              SERIAL PRIMARY KEY,
    sector_key      VARCHAR(50) NOT NULL UNIQUE,
    display_name    VARCHAR(100) NOT NULL,
    label_overrides JSONB,
    custom_prompt   TEXT,
    benchmark_f1    REAL,
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Seed initial sectors (Faz 2 pilot)
INSERT INTO wa_sector_config (sector_key, display_name) VALUES
    ('saglik', 'Sağlık / Estetik'),
    ('moda', 'Moda / Tekstil'),
    ('gayrimenkul', 'Gayrimenkul')
ON CONFLICT (sector_key) DO NOTHING;

-- Permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON wa_sector_config TO invekto;
GRANT USAGE, SELECT ON SEQUENCE wa_sector_config_id_seq TO invekto;
