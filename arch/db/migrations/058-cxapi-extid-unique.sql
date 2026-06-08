-- =============================================================
-- Migration 058: FEAT-PROJELER / cxapi Send Engine — PR-3b-1 (ext_id correlation foundation)
-- =============================================================
-- ADDITIVE, idempotent. Adds the per-tenant uniqueness guarantee that makes a
-- delivery-status ack resolvable to EXACTLY ONE tenant's message, unblocked by
-- INMA's G12 answer (2026-06-08): the cxapi /api/chatoperation send response
-- `data` field is the sent message's WhatsApp wamid, and it equals the later
-- ack's InstanceMessageID. PR-3b-1 starts persisting that wamid as
-- outbound_messages.external_message_id (WapCrmSendClient -> MarkCxapiOutcomeAsync),
-- so this composite UNIQUE makes the (tenant_id, external_message_id) lookup that
-- PR-3b-2's tenant-aware ack endpoint will use single-row-safe.
--
-- SAFETY: external_message_id is currently NULL on EVERY existing row — its sole
-- historical writer (UpdateMessageStatusAsync via Backend) only ever passes null —
-- so this PARTIAL index (WHERE external_message_id IS NOT NULL) builds against an
-- empty set and cannot conflict with existing data. If, contrary to that, any
-- duplicate (tenant_id, external_message_id) pair existed, CREATE UNIQUE INDEX
-- would FAIL LOUD here (correct: investigate before shipping), never silently.
--
-- The existing non-unique idx_outbound_messages_external_id (single-column) is
-- LEFT IN PLACE — it still serves the legacy tenant-blind FindMessageByExternalIdAsync
-- (bridge delivery-status path). Tenant-scoping of that lookup ships ATOMICALLY with
-- the new ack endpoint in PR-3b-2; this migration only adds the uniqueness invariant.
--
-- GATE P0-3 unaffected: no tenant is allowlisted, so no wapcrm_cxapi row is created
-- and external_message_id stays NULL in production until go-live — this is a
-- schema-only, runtime no-op change.
--
-- snake_case, single shared Postgres (rollout skill). Safe to re-run.
-- =============================================================

-- -------------------------------------------------------------
-- (A) outbound_messages: per-tenant uniqueness of the WhatsApp correlation id
-- -------------------------------------------------------------
CREATE UNIQUE INDEX IF NOT EXISTS uq_outbound_messages_tenant_external_id
    ON outbound_messages (tenant_id, external_message_id)
    WHERE external_message_id IS NOT NULL;

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-058)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_is_unique BOOLEAN;
BEGIN
    SELECT idx.indisunique INTO v_is_unique
    FROM pg_class c
    JOIN pg_index idx ON idx.indexrelid = c.oid
    WHERE c.relname = 'uq_outbound_messages_tenant_external_id';

    IF v_is_unique IS NULL THEN
        RAISE EXCEPTION 'INV-SEED-058: uq_outbound_messages_tenant_external_id missing';
    END IF;
    IF v_is_unique IS NOT TRUE THEN
        RAISE EXCEPTION 'INV-SEED-058: uq_outbound_messages_tenant_external_id is not UNIQUE';
    END IF;

    RAISE NOTICE 'INV-SEED-058 OK: partial UNIQUE (tenant_id, external_message_id) — cxapi ack correlation single-row-safe (PR-3b-2 wires the tenant-scoped lookup)';
END
$verify$;
