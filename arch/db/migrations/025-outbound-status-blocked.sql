-- Migration 025: FEAT-J2 — outbound_messages.status enum +'blocked'.
-- Bridge /api/v1/callback/wapcrm now parses WapCRM response body: when INMA
-- returns Status:false + StatusCode:'906'|'907' (marketing opt-out block),
-- the outbound row is marked 'blocked' instead of the previous silent 'sent'.
--
-- Compliance/audit fix (GDPR Article 21 evidence integrity). See
-- arch/plans/20260417-j2-opt-out-inse-sync.json AC9.
--
-- Idempotent: DROP + ADD constraint pattern (PG allows re-running).

ALTER TABLE outbound_messages DROP CONSTRAINT IF EXISTS chk_message_status;
ALTER TABLE outbound_messages ADD CONSTRAINT chk_message_status
    CHECK (status IN ('queued', 'sending', 'sent', 'delivered', 'read', 'failed', 'blocked'));
