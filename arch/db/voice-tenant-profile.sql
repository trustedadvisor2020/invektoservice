-- FEAT-VFB F-VR-B (spec voice-multitenant-runtime.md §3): voice_tenant_profile
-- Per-tenant voice agent profile. Owns the tenant-level BEHAVIOR layer of the
-- multi-tenant voice runtime: business_type classification + capability flags +
-- goal/brand_tone. The LANGUAGE layer (sector_adapter: vocabulary/intent/banned_words/
-- required_fields) is code-versioned in VoiceRuntime, NOT here.
--
-- Ownership: Backend (sibling of tenant_registry, same Postgres). VoiceRuntime has NO
-- DB access — it reads this profile over HTTP via Backend GET /api/ops/tenants/{id}/voice-profile.
--
-- Override semantics: business_type + default_language are authoritative (NOT NULL).
-- Capability flags + goals + voice_brand_tone are NULLABLE = "inherit business_type default".
-- A NULL flag means VoiceRuntime applies the business_type default (sector_adapter layer);
-- a non-NULL flag is an explicit per-tenant ADMIN OVERRIDE (admin UI = §17, later phase).
-- Backfill (migration 050) sets only business_type (derived from tenant_registry.sector);
-- all overrides start NULL so existing behavior = pure business_type default.

CREATE TABLE IF NOT EXISTS voice_tenant_profile (
    tenant_id                INTEGER PRIMARY KEY
                                 REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    business_type            VARCHAR(30)  NOT NULL DEFAULT 'generic',
    default_language         VARCHAR(10)  NOT NULL DEFAULT 'tr-TR',

    -- Override layer (NULL = inherit business_type default; non-NULL = admin override)
    voice_brand_tone         VARCHAR(40),                 -- e.g. 'warm_professional' | 'corporate' | 'friendly'
    primary_goal             VARCHAR(40),                 -- e.g. 'appointment' | 'lead_capture' | 'order_support' | 'info'
    secondary_goal           VARCHAR(40),
    pricing_enabled          BOOLEAN,
    appointment_enabled      BOOLEAN,
    order_lookup_enabled     BOOLEAN,
    lead_capture_enabled     BOOLEAN,
    human_handoff_enabled    BOOLEAN,
    sensitive_topics_enabled BOOLEAN,

    created_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    -- Controlled vocabulary aligned to spec §3 enum + hair_transplant (Q 2026-06-01).
    -- MUST stay in sync with VoiceRuntime BusinessType enum + sector_adapter registry (B2).
    CONSTRAINT ck_voice_tenant_profile_business_type CHECK (business_type IN (
        'clinic', 'dental_clinic', 'aesthetic_clinic', 'hair_transplant',
        'ecommerce', 'service_business', 'education', 'real_estate', 'generic'
    ))
);

COMMENT ON TABLE voice_tenant_profile IS
    'FEAT-VFB F-VR-B: per-tenant voice behavior profile (business_type + capability overrides). Language layer (sector_adapter) is code-versioned in VoiceRuntime.';

-- updated_at trigger (reuses the shared update_updated_at_column() defined in tenant-registry.sql)
CREATE OR REPLACE TRIGGER trigger_voice_tenant_profile_updated_at
    BEFORE UPDATE ON voice_tenant_profile
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- App role grant (project convention: every new table needs GRANT ALL; 'invekto' = app/owner role).
GRANT ALL ON voice_tenant_profile TO invekto;
