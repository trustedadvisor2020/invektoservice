-- Migration 028: FEAT-TFM MVP — Tenant Field Mapping (semantic overlay on INMA cf1..cf10).
--
-- 1) tenant_settings.field_mapping JSONB NOT NULL DEFAULT '{}'::jsonb
--    Per-tenant semantic→INMA key map. Shape:
--      { "<semantic_name>": { "source": "cf1..cf10", "type": "enum|string|date|bool|int",
--                              "enum_values": ["..."], "required": false } }
--    Empty object = no mapping (DbTenantFieldMappingResolver returns null → DMP raw INMA key
--    fallback intact, sıfır breaking change). Reserved name guard enforced app-side
--    (TenantFieldMappingValidator: InmaDynamicFieldKeys.Allowlist ∪ leads core columns).
--
-- 2) leads.custom_1 .. custom_10 TEXT NULL
--    Forward-compat mirror columns for FEAT-TFM-SYNC (next packet). MVP'de boş kalır;
--    INMA → INSE sync worker (webhook/polling) bu kolonları dolduracak.
--    NULL keeps backward-compat (existing rows untouched).
--
-- Canonical mirrors: arch/db/tenant-settings.sql, arch/db/pkt6b1-niche-business.sql.
-- Related plan: arch/plans/20260421-feat-tfm-resolver-mvp.json (AC1).
-- Related spec: arch/features/tenant-field-mapping.md.

ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS field_mapping JSONB NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS custom_1  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_2  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_3  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_4  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_5  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_6  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_7  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_8  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_9  TEXT NULL,
    ADD COLUMN IF NOT EXISTS custom_10 TEXT NULL;

-- Re-assert table-level grants (lessons/2026-04-18 — prod role is `invekto`, not
-- `invekto_app`). Idempotent — re-granting ALL on an already-owned table is a no-op.
GRANT ALL ON tenant_settings TO invekto;
GRANT ALL ON leads TO invekto;
