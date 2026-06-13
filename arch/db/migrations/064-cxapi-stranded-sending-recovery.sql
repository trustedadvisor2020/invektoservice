-- Migration 064 — FEATURE C: cxapi stranded-'sending' periodic recovery
-- ----------------------------------------------------------------------------
-- 2026-06-12 second incident: a mid-dispatch JWT-crash restart left 3 cxapi rows
-- stranded in 'sending' (attempt_count=0, claimed by DequeueMessagesAsync but never
-- POSTed). The 'sending'->'queued' reset was STARTUP-only, so those rows stayed
-- orphaned until the next service restart. This migration adds a claim-time anchor
-- (claimed_at) so a periodic, staleness-gated sweep can reset genuinely stranded
-- 'sending' rows WITHOUT a restart — complementing the e62ce0e8 'posting' sweep.
--
-- Additive + idempotent. No CHECK-constraint change ('sending'/'queued' already exist).
-- ----------------------------------------------------------------------------

BEGIN;

-- (a) Claim-time anchor. NULL for pre-existing rows; the periodic sweep guards
--     claimed_at IS NOT NULL, and the ungated startup reset clears any legacy
--     'sending' rows on first deploy, so no backfill is required.
ALTER TABLE outbound_messages
    ADD COLUMN IF NOT EXISTS claimed_at TIMESTAMPTZ;

-- (b) Partial index keeps the periodic sweep off a full-table scan (mirrors the
--     idx_outbound_messages_queued precedent — narrow, route+status scoped).
CREATE INDEX IF NOT EXISTS idx_outbound_messages_cxapi_sending
    ON outbound_messages (claimed_at)
    WHERE send_route = 'wapcrm_cxapi' AND status = 'sending';

-- (c) Fail loud if the column did not materialise.
DO $verify$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'outbound_messages' AND column_name = 'claimed_at'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-064] outbound_messages.claimed_at column missing after migration 064';
    END IF;
END
$verify$;

COMMIT;
