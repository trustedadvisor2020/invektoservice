-- Migration 012: Zoho OAuth connections per tenant (Adim 2 Paket A)
-- Purpose: Persist Zoho CRM OAuth tokens per tenant, region-aware (multi-DC).
--          refresh_token is encrypted by .NET Data Protection API at the application layer.
-- Reference: arch/plans/20260414-zoho-oauth-token-infra.json

CREATE TABLE IF NOT EXISTS zoho_connections (
    id                   BIGSERIAL PRIMARY KEY,
    tenant_id            INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    region               VARCHAR(8) NOT NULL
        CHECK (region IN ('eu','com','in','com.au','jp','uk','ca','sa')),  -- defense-in-depth: ZohoRegionResolver service-level whitelist + DB CHECK
    api_domain           VARCHAR(128) NOT NULL,         -- 'www.zohoapis.eu'
    accounts_domain      VARCHAR(128) NOT NULL,         -- 'accounts.zoho.eu'
    refresh_token_enc    TEXT NOT NULL,                 -- DataProtection-wrapped (purpose: Invekto.Integrations.Zoho.RefreshToken)
    granted_scopes       TEXT NOT NULL,                 -- comma-separated scope list returned by Zoho
    zoho_user_email      VARCHAR(256),                  -- best-effort identity (optional)
    connected_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_refreshed_at    TIMESTAMPTZ,
    disconnected_at      TIMESTAMPTZ,
    CONSTRAINT uq_zoho_connections_tenant UNIQUE (tenant_id)
);

CREATE INDEX IF NOT EXISTS idx_zoho_connections_tenant ON zoho_connections(tenant_id);

GRANT ALL ON TABLE zoho_connections TO invekto;
GRANT USAGE, SELECT ON SEQUENCE zoho_connections_id_seq TO invekto;

COMMENT ON TABLE  zoho_connections IS 'Adim 2: Per-tenant Zoho OAuth connection. refresh_token AES-encrypted via .NET Data Protection.';
COMMENT ON COLUMN zoho_connections.refresh_token_enc IS 'Protected by IDataProtector purpose=Invekto.Integrations.Zoho.RefreshToken';
COMMENT ON COLUMN zoho_connections.region IS 'Zoho data center suffix (eu/com/in/com.au/jp/uk/ca/sa)';
