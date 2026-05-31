-- Migration 050: FEAT-VFB F-VR-B — voice_tenant_profile (tenant_profile §3).
--
-- Context:
-- Multi-tenant voice runtime (arch/specs/voice-multitenant-runtime.md). F-VR-A (prompt) +
-- F-VR-E (KB confidence banding) already live. This migration adds the BEHAVIOR layer:
-- per-tenant business_type + capability flag overrides + goal/brand_tone. VoiceRuntime reads
-- it over HTTP (Backend GET /api/ops/tenants/{id}/voice-profile) — VoiceRuntime has no DB.
--
-- Scope:
--   Step 1: CREATE TABLE voice_tenant_profile (canonical DDL = arch/db/voice-tenant-profile.sql).
--   Step 2: Backfill one row per existing tenant_registry tenant. business_type derived from
--           tenant_registry.sector slug; ALL override columns left NULL (= inherit business_type
--           default in VoiceRuntime). Existing voice behavior is therefore unchanged.
--   Step 3: DO $verify$ fail-loud postcondition — every active+inactive tenant has a profile row.
--
-- Idempotency: Step 1 CREATE TABLE IF NOT EXISTS. Step 2 INSERT ... ON CONFLICT (tenant_id)
--   DO NOTHING (re-run = 0 new rows, never overwrites an admin-edited profile). Step 3 read-only.
--
-- Reversibility: additive only (new table, new rows). No tenant_registry column touched. Rollback
--   = DROP TABLE voice_tenant_profile (CASCADE not needed; no dependents).

-- ── Step 1: table ──────────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS voice_tenant_profile (
    tenant_id                INTEGER PRIMARY KEY
                                 REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    business_type            VARCHAR(30)  NOT NULL DEFAULT 'generic',
    default_language         VARCHAR(10)  NOT NULL DEFAULT 'tr-TR',
    voice_brand_tone         VARCHAR(40),
    primary_goal             VARCHAR(40),
    secondary_goal           VARCHAR(40),
    pricing_enabled          BOOLEAN,
    appointment_enabled      BOOLEAN,
    order_lookup_enabled     BOOLEAN,
    lead_capture_enabled     BOOLEAN,
    human_handoff_enabled    BOOLEAN,
    sensitive_topics_enabled BOOLEAN,
    created_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at               TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_voice_tenant_profile_business_type CHECK (business_type IN (
        'clinic', 'dental_clinic', 'aesthetic_clinic', 'hair_transplant',
        'ecommerce', 'service_business', 'education', 'real_estate', 'generic'
    ))
);

COMMENT ON TABLE voice_tenant_profile IS
    'FEAT-VFB F-VR-B: per-tenant voice behavior profile (business_type + capability overrides). Language layer (sector_adapter) is code-versioned in VoiceRuntime.';

CREATE OR REPLACE TRIGGER trigger_voice_tenant_profile_updated_at
    BEFORE UPDATE ON voice_tenant_profile
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- App role grant (project convention: every new table needs GRANT ALL; 'invekto' = app/owner role).
GRANT ALL ON voice_tenant_profile TO invekto;

-- ── Step 2: backfill business_type from sector slug (overrides stay NULL) ───
-- Mapping mirrors VoiceRuntime SectorMapper (B2). Unknown/NULL sector → 'generic'.
INSERT INTO voice_tenant_profile (tenant_id, business_type)
SELECT
    tr.tenant_id,
    CASE LOWER(COALESCE(tr.sector, ''))
        WHEN 'dis_klinik'  THEN 'dental_clinic'
        WHEN 'eticaret'    THEN 'ecommerce'
        WHEN 'estetik'     THEN 'aesthetic_clinic'
        WHEN 'sac_ekimi'   THEN 'hair_transplant'
        WHEN 'sacekimi'    THEN 'hair_transplant'
        WHEN 'klinik'      THEN 'clinic'
        WHEN 'saglik'      THEN 'clinic'
        ELSE 'generic'
    END AS business_type
FROM tenant_registry tr
ON CONFLICT (tenant_id) DO NOTHING;

-- ── Step 3: fail-loud postcondition (INV-SEED-050) ─────────────────────────
DO $verify$
DECLARE
    v_tenants  INTEGER;
    v_profiles INTEGER;
    v_bad_type INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_tenants  FROM tenant_registry;
    SELECT COUNT(*) INTO v_profiles FROM voice_tenant_profile;
    SELECT COUNT(*) INTO v_bad_type FROM voice_tenant_profile vtp
        WHERE NOT EXISTS (SELECT 1 FROM tenant_registry tr WHERE tr.tenant_id = vtp.tenant_id);

    IF v_profiles < v_tenants THEN
        RAISE EXCEPTION 'INV-SEED-050: voice_tenant_profile backfill incomplete — % tenants, % profiles',
            v_tenants, v_profiles;
    END IF;
    IF v_bad_type > 0 THEN
        RAISE EXCEPTION 'INV-SEED-050: % orphan voice_tenant_profile rows (no tenant_registry parent)', v_bad_type;
    END IF;

    RAISE NOTICE 'INV-SEED-050 OK: % tenant(s), % voice profile(s) backfilled.', v_tenants, v_profiles;
END
$verify$;
