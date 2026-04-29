-- Migration 044: FEAT-TENANT-SETTINGS-JSON-UI kanban kart kayit
--
-- UI Gap Audit (2026-04-29 conversation): tenant_registry.settings_json JSONB
-- aktif kullanimda (2/7 tenant dolu, 4 key: flow_builder_api_key, wapcrm,
-- default_tone, confidence_threshold) ama Dashboard UI YOK. 20+ servis okuyor,
-- manuel SQL UPDATE ile yonetiliyor.
--
-- D034 = FEAT-TENANT-SETTINGS-JSON-UI BACKLOG (post-pilot, ~2-3 gun)
--
-- ref_code D034 (Migration 036 namespace D = DEV).
-- depends_on NULL.
--
-- Idempotency: ON CONFLICT (board_key, card_slug) DO NOTHING.

INSERT INTO kanban_cards (
    board_key, card_slug, status, category, priority, position,
    title, summary, body_markdown,
    owner, eta, source_file, source_anchor,
    ref_code, depends_on
) VALUES
(
    'inse', 'feat-tenant-settings-json-ui', 'BACKLOG', 'DEV', 'P2', 260,
    'FEAT-TENANT-SETTINGS-JSON-UI: Generic Settings JSONB Editor',
    'UI Gap Audit 2026-04-29: tenant_registry.settings_json aktif kullanimda (2/7 tenant dolu, 4 key) ama Dashboard UI YOK. 20+ servis okuyor, su an manuel SQL UPDATE.',
    E'**UI Gap Audit bulgusu (2026-04-29):** UI coverage tamamlama paket''i. `tenant_settings` 12/12 kolon UI ile kapsandi (D032+D033 ile). Bu paket `tenant_registry.settings_json` JSONB icin generic editor.\n\n**Mevcut kullanim:**\n- `flow_builder_api_key` (string secret) - 2 tenant (1, 5050) - Flow Builder publish API key\n- `wapcrm` (object) - tenant 5050 - WapCRM integration ayarlari\n- `default_tone` (string) - tenant 5050 - AI yanit tonu\n- `confidence_threshold` (number) - tenant 5050 - AI intent confidence esigi\n\n**Su anki yonetim:** Manuel SQL UPDATE.\n\n**Scope:**\n- Backend: GET/PUT /api/ops/tenants/{id}/settings-json + secret masking + size cap + INV-BE-125/126/127\n- Backend: POST /api/ops/tenants/{id}/settings-json/rotate-secret/{key} (LIW pattern)\n- Dashboard: TenantSettingsJsonPage.tsx (SuperAdmin /ops scope) + LicensesPage features_json pattern reuse\n- Migration N: whitelist key validation guard\n- arch/contracts/tenant-settings-json-schema.json typed schema\n\n**Acik sorular:**\n1. Schema-strict (4 whitelisted key) vs schema-less editor?\n2. Secret key masking last4 vs rotate-only (no display)?\n3. Tenant operatoru kendi tenant icin edit edebilir mi yoksa SuperAdmin only?\n4. wapcrm key shape arch/contracts''a eklensin mi?\n5. Audit log per PUT (LIW pattern) vs updated_at yeterli?\n\n**callback_url not:** tenant_registry.callback_url DEAD COLUMN (0/7 tenant dolu, appsettings global config kullaniliyor) — UI gereksiz, opsiyonel future Migration DROP cleanup.\n\n**Aktivasyon gate:** Pilot Stage 3 sonrasi (P2, blocker degil), 2. musteri onboarding pre-req.\n\n**Tracking:** tracking/feat-tenant-settings-json-ui.md',
    'Dev', '~2-3 gun',
    'tracking/feat-tenant-settings-json-ui.md', NULL,
    'D034', NULL
)
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §2. Postcondition guard
DO $verify$
DECLARE
    new_card integer;
BEGIN
    SELECT COUNT(*) INTO new_card
      FROM kanban_cards
     WHERE board_key='inse' AND card_slug = 'feat-tenant-settings-json-ui';

    IF new_card <> 1 THEN
        RAISE EXCEPTION '[INV-SEED-038] Migration 044 eksik: 1 kart bekleniyordu, % bulundu.', new_card;
    END IF;

    RAISE NOTICE 'Migration 044 OK: D034 FEAT-TENANT-SETTINGS-JSON-UI insert/idempotent.';
END$verify$;
