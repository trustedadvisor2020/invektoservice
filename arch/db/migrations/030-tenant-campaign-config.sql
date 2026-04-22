-- Migration 030: FEAT-MCC Multi-City Campaign — tenant_settings.campaign_config JSONB
-- + idempotent Dent (tenant_id=18173130) pilot seed.
--
-- Scope (per arch/plans/20260425-feat-mcc-multi-city.json + arch/features/multi-city-campaign.md):
--
-- 1) tenant_settings.campaign_config JSONB NOT NULL DEFAULT '{"campaigns":[]}'::jsonb
--    Holds an array of campaigns. Shape (CampaignConfig DTO):
--      {
--        "campaigns": [{
--          "slug": "roadshow_ireland_2026",
--          "name": "Ireland Roadshow 2026",
--          "active": true,
--          "start_date": "2026-03-01",
--          "end_date":   "2026-03-20",
--          "cities": [
--            { "slug": "dublin", "name": "Dublin", "country": "IE", "timezone": "Europe/Dublin" },
--            { "slug": "cork",   "name": "Cork",   "country": "IE", "timezone": "Europe/Dublin" }
--          ],
--          "dates": [
--            { "city": "dublin", "date": "2026-03-14", "hours": "09:00-18:00" },
--            { "city": "cork",   "date": "2026-03-15", "hours": "09:00-18:00" }
--          ]
--        }]
--      }
--    Empty default keeps backward-compat: every existing tenant has zero campaigns →
--    resolver returns empty config → window guard is a no-op for that tenant.
--    App-layer validation (TenantCampaignConfigValidator, INV-BE-118):
--      - tenant-scope unique slug per campaigns[].slug (case-insensitive)
--      - lowercase [a-z][a-z0-9_-]{1,63} slug regex
--      - max 8 campaigns per tenant
--      - max 20 cities + max 20 dates per campaign
--      - start_date <= end_date (inclusive both sides, tenant_settings.timezone)
--      - dates[].city must reference a campaigns[].cities[].slug (no orphan)
--    Window guard (INV-BE-119) and reserved slug guard (INV-BE-120) live in app code.
--
-- 2) Dent Adavista (tenant_id = 18173130) pilot seed — idempotent JSONB merge.
--    Q's interview choice: pilot seed runs as part of migration so post-deploy
--    smoke (Pilot Smoke S7) finds the config already present; rollback drops it
--    via the inverse merge below if needed.
--    Idempotency: only writes when the campaign slug is not already present.
--    Re-running the migration is a no-op for tenants that already carry the slug.
--    Tenants other than 18173130 are NOT touched.
--
-- 3) GIN index for JSONB containment search (campaign_config @> '{...}') — keeps
--    ops dashboard / cross-tenant slug audit queries reasonable as the table grows.
--    Single-column expression index on campaign_config; tenant lookups are PK-driven
--    so per-row containment is the only non-PK search path we plan to support.
--
-- Idempotency: ALTER COLUMN uses IF NOT EXISTS; Dent seed uses an UPDATE guarded
-- by jsonb_path_exists check; CREATE INDEX uses IF NOT EXISTS.
-- GRANT ALL re-asserted at end (lessons 2026-04-18 — prod role is `invekto`, not `invekto_app`).

-- ============================================================
-- 1. tenant_settings.campaign_config column
-- ============================================================
ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS campaign_config JSONB NOT NULL DEFAULT '{"campaigns":[]}'::jsonb;

-- ============================================================
-- 2. JSONB GIN index (containment + path queries)
-- ============================================================
-- Use jsonb_path_ops for smaller, faster containment-only index. Ops dashboards
-- run "WHERE campaign_config @> '{\"campaigns\":[{\"slug\":\"X\"}]}'" patterns.
CREATE INDEX IF NOT EXISTS idx_tenant_settings_campaign_config_gin
    ON tenant_settings USING GIN (campaign_config jsonb_path_ops);

-- ============================================================
-- 3. Dent Adavista pilot seed (idempotent)
-- ============================================================
-- Pre-condition: tenant_settings row exists for 18173130 (auto-provisioned via
-- INMA welcome path in 2026-04 — verified by P5 EFS smoke). If the row does NOT
-- exist (e.g. local dev DB without Dent), this UPDATE is a no-op (0 rows).
-- The roadshow_ireland_2026 seed mirrors DentAdavista/plan/pilot-checklist.md.
DO $$
DECLARE
    dent_tenant_id INT := 18173130;
    seed_slug TEXT := 'roadshow_ireland_2026';
    seed_payload JSONB := jsonb_build_object(
        'slug',       seed_slug,
        'name',       'Ireland Roadshow 2026',
        'active',     true,
        'start_date', '2026-03-01',
        'end_date',   '2026-03-20',
        'cities', jsonb_build_array(
            jsonb_build_object('slug', 'dublin', 'name', 'Dublin', 'country', 'IE', 'timezone', 'Europe/Dublin'),
            jsonb_build_object('slug', 'cork',   'name', 'Cork',   'country', 'IE', 'timezone', 'Europe/Dublin')
        ),
        'dates', jsonb_build_array(
            jsonb_build_object('city', 'dublin', 'date', '2026-03-14', 'hours', '09:00-18:00'),
            jsonb_build_object('city', 'cork',   'date', '2026-03-15', 'hours', '09:00-18:00')
        )
    );
    existing JSONB;
    new_campaigns JSONB;
BEGIN
    -- Read current config; tolerate row-not-found by skipping (no INSERT — tenant_settings
    -- row creation is owned by the auto-provision path, not this migration).
    SELECT campaign_config INTO existing
        FROM tenant_settings
        WHERE tenant_id = dent_tenant_id;

    IF existing IS NULL THEN
        RAISE NOTICE 'Migration 030: tenant_settings row missing for tenant_id=%; skipping Dent seed (auto-provision path will create row, re-run seed manually if needed).', dent_tenant_id;
        RETURN;
    END IF;

    -- Idempotency: skip if slug already present in campaigns[].
    IF jsonb_path_exists(existing, ('$.campaigns[*] ? (@.slug == "' || seed_slug || '")')::jsonpath) THEN
        RAISE NOTICE 'Migration 030: Dent seed slug=% already present for tenant_id=%; no-op.', seed_slug, dent_tenant_id;
        RETURN;
    END IF;

    -- Merge: keep any pre-existing campaigns[], append the seed.
    new_campaigns := COALESCE(existing -> 'campaigns', '[]'::jsonb) || jsonb_build_array(seed_payload);

    UPDATE tenant_settings
        SET campaign_config = jsonb_set(existing, '{campaigns}', new_campaigns, true),
            updated_at = NOW()
        WHERE tenant_id = dent_tenant_id;

    RAISE NOTICE 'Migration 030: Dent seed slug=% inserted for tenant_id=%.', seed_slug, dent_tenant_id;
END $$;

-- ============================================================
-- 4. GRANT ALL re-assertion
-- ============================================================
-- Idempotent re-grant — required so the prod `invekto` role can read/write the new
-- column (lessons 2026-04-18: PostgreSQL role is `invekto`, not `invekto_app`).
GRANT ALL ON tenant_settings TO invekto;
