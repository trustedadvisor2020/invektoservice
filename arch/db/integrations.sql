-- ============================================================
-- Invekto.Integrations DB Schema (Port 7106)
-- PKT-5A: GR-3.4 + GR-3.6
-- ============================================================

-- Integration accounts: tenant's 3rd party API credentials
CREATE TABLE integration_accounts (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    provider        VARCHAR(50) NOT NULL,  -- 'hepsiburada', 'trendyol', 'aras_kargo', 'yurtici_kargo'
    api_key_enc     TEXT,                  -- encrypted API key (AES or app-level)
    api_secret_enc  TEXT,                  -- encrypted API secret
    seller_id       VARCHAR(100),          -- marketplace seller ID
    settings_json   JSONB DEFAULT '{}',    -- provider-specific settings
    status          VARCHAR(20) NOT NULL DEFAULT 'active',  -- active, paused, error
    last_sync_at    TIMESTAMPTZ,
    error_message   TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_integration_tenant_provider UNIQUE (tenant_id, provider)
);

CREATE INDEX idx_integration_accounts_tenant ON integration_accounts(tenant_id);

-- Orders cache: synced from marketplace APIs
CREATE TABLE orders_cache (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    provider            VARCHAR(50) NOT NULL,       -- 'hepsiburada', 'trendyol'
    external_order_id   VARCHAR(100) NOT NULL,
    customer_phone      VARCHAR(20),
    customer_name       VARCHAR(200),
    tracking_code       VARCHAR(100),
    cargo_provider      VARCHAR(50),                -- 'aras', 'yurtici', 'mng', etc.
    order_status        VARCHAR(50) NOT NULL,        -- pending, processing, shipped, delivered, cancelled, returned
    order_data_json     JSONB DEFAULT '{}',          -- full order detail from provider
    total_amount        DECIMAL(12,2),
    currency            VARCHAR(3) DEFAULT 'TRY',
    synced_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_order_tenant_provider_ext UNIQUE (tenant_id, provider, external_order_id)
);

CREATE INDEX idx_orders_cache_tenant_phone ON orders_cache(tenant_id, customer_phone);
CREATE INDEX idx_orders_cache_tenant_status ON orders_cache(tenant_id, order_status);
CREATE INDEX idx_orders_cache_tracking ON orders_cache(tenant_id, tracking_code) WHERE tracking_code IS NOT NULL;

-- Cargo tracking events: status history per order
CREATE TABLE cargo_tracking_events (
    id              SERIAL PRIMARY KEY,
    tenant_id       INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    order_id        INTEGER NOT NULL REFERENCES orders_cache(id),
    cargo_provider  VARCHAR(50) NOT NULL,
    tracking_code   VARCHAR(100) NOT NULL,
    status          VARCHAR(50) NOT NULL,   -- picked_up, in_transit, out_for_delivery, delivered, returned
    location        VARCHAR(200),
    event_time      TIMESTAMPTZ NOT NULL,
    raw_data_json   JSONB,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_cargo_events_order ON cargo_tracking_events(order_id);
CREATE INDEX idx_cargo_events_tenant_tracking ON cargo_tracking_events(tenant_id, tracking_code);

-- Grants
GRANT ALL ON integration_accounts TO invekto;
GRANT ALL ON orders_cache TO invekto;
GRANT ALL ON cargo_tracking_events TO invekto;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO invekto;
