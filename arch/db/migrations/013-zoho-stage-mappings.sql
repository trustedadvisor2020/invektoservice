-- Migration 013: Zoho stage mapping per tenant (Adim 3 Paket 1)
-- Purpose: Map Gunes lifecycle events to Zoho Blueprint transitions per tenant.
--          Each tenant configures which Zoho Blueprint transition to trigger for
--          each lifecycle event (welcome_sent, engaged, qualified, offer_sent,
--          closed_won, deposit_paid, closed_lost).
-- Policy:  Blueprint-only (field update fallback is explicitly forbidden in Adim 3).
-- Reference: arch/plans/20260415-zoho-step3-p1-api.json

CREATE TABLE IF NOT EXISTS zoho_stage_mappings (
    id                    BIGSERIAL PRIMARY KEY,
    tenant_id             INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    gunes_event           VARCHAR(64) NOT NULL
        CHECK (gunes_event IN ('welcome_sent','engaged','qualified','offer_sent','closed_won','deposit_paid','closed_lost')),
    zoho_transition_id    VARCHAR(64) NOT NULL,  -- Blueprint transition ID discovered from GET /settings/blueprint
    zoho_transition_name  VARCHAR(256),          -- human-readable label (UI convenience, not authoritative)
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_zoho_stage_mappings_tenant_event UNIQUE (tenant_id, gunes_event)
);

CREATE INDEX IF NOT EXISTS idx_zoho_stage_mappings_tenant ON zoho_stage_mappings(tenant_id);

GRANT ALL ON TABLE zoho_stage_mappings TO invekto;
GRANT USAGE, SELECT ON SEQUENCE zoho_stage_mappings_id_seq TO invekto;

COMMENT ON TABLE  zoho_stage_mappings IS 'Adim 3 P1: Gunes lifecycle event -> Zoho Blueprint transition per tenant. Blueprint-only policy.';
COMMENT ON COLUMN zoho_stage_mappings.zoho_transition_id IS 'Blueprint transition_id from GET /crm/v6/settings/blueprint?module=Leads';
COMMENT ON COLUMN zoho_stage_mappings.gunes_event IS 'Gunes lifecycle event (fired in Adim 3 P2 from AttributionService/FlowEngine)';
