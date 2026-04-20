-- Migration 026: FEAT-J2 — tenant_settings.enforce_message_category BOOL flag.
-- Gradual-rollout feature flag for MessageCategory enforcement in
-- AutomationOrchestrator. FALSE (default) keeps backward-compat behavior:
-- event_name null -> MessageCategory null, INMA opt-out check skipped.
-- TRUE (pilot tenants) rejects send_message callbacks without event_name
-- or with event_name outside the transactional allow-list with INV-OB-031.
--
-- Canonical mirror: arch/db/tenant-settings.sql.
-- Related: arch/plans/20260417-j2-opt-out-inse-sync.json AC8, AC12.

ALTER TABLE tenant_settings
    ADD COLUMN IF NOT EXISTS enforce_message_category BOOLEAN NOT NULL DEFAULT FALSE;
