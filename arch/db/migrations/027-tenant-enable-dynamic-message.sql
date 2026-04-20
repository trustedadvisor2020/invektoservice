-- Migration 027: FEAT-DMP — INMA DynamicMessage integration (two additive columns).
--
-- 1) tenant_settings.enable_dynamic_message BOOLEAN DEFAULT TRUE
--    Interview Q5 (2026-04-20): default TRUE because DynamicFields is null-by-default
--    on CallbackData; activation is per-message (editor must insert a placeholder).
--    FALSE = tenant escape hatch — forces INSE legacy substitution regardless of
--    whether a template contains INMA placeholders.
--
-- 2) outbound_messages.dynamic_fields TEXT[] NULL
--    Stores the resolved INMA key list at broadcast-create time so MessageSenderService
--    can forward it to the callback bridge without re-parsing MessageText on dequeue.
--    NULL keeps backward-compat (pre-DMP rows + legacy-path inserts).
--
-- Canonical mirrors: arch/db/tenant-settings.sql, arch/db/outbound.sql.
-- Related plan: arch/plans/20260420-feat-dmp-inma-dynamic-message.json (AC3, AC10).

ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS enable_dynamic_message BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE outbound_messages
    ADD COLUMN IF NOT EXISTS dynamic_fields TEXT[] NULL;

-- Re-assert table-level grants so column additions above remain aligned with the
-- production role convention (lessons/2026-04-18 — prod role is `invekto`, not
-- `invekto_app`). Idempotent — re-granting ALL on an already-owned table is a no-op.
GRANT ALL ON tenant_settings TO invekto;
GRANT ALL ON outbound_messages TO invekto;
