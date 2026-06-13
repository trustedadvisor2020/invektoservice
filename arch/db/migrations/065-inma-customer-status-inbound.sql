-- Migration 065 — FEAT-INMA-PIPELINE-V2 C2: INMA customer.selection_changed inbound consumer
-- ----------------------------------------------------------------------------
-- INMA fires a SIGNED system event (type='customer.selection_changed') to our existing
-- /api/v1/webhook/event?companyId=X webhook when an agent changes a customer's feature-group
-- selection. C2 consumes it: HMAC verify -> dedupe + durable raw-event audit -> derive an
-- OPAQUE leads.customer_status (INMA-authoritative). This migration adds:
--   (a) leads.customer_status TEXT           — opaque INMA status (DISTINCT from pipeline_status)
--   (b) leads.customer_status_occurred_at    — out-of-order guard (occurredAt monotonic gate)
--   (c) functional index on (tenant_id, digit-normalized phone) — index-friendly phone match
--   (d) inma_webhook_events                  — dedupe (UNIQUE tenant_id,event_id) + raw audit
--
-- Additive + idempotent (IF NOT EXISTS). leads.pipeline_status is NOT touched.
-- ----------------------------------------------------------------------------

BEGIN;

-- (a) Opaque INMA-authoritative status. NULL = unknown / cleared. Separate column from
--     pipeline_status (INSE funnel enum) by design — different authority + semantics.
ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS customer_status TEXT;

-- (b) Last-applied event time for the customer_status. Gates the UPDATE so a late-delivered
--     OLDER event (at-least-once is not in-order) cannot overwrite newer state.
ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS customer_status_occurred_at TIMESTAMPTZ;

-- (c) Functional index matching the EXACT normalization expression used by the consumer's
--     phone lookup (digit-only). Keeps the per-event UPDATE off a tenant-wide table scan.
CREATE INDEX IF NOT EXISTS idx_leads_tenant_normalized_phone
    ON leads (tenant_id, (regexp_replace(phone, '\D', '', 'g')));

-- (d) Inbound system-event log: idempotency anchor + durable raw audit (for replay + C3).
--     event_id NOT NULL is REQUIRED — a nullable column would let Postgres store multiple
--     NULLs under the UNIQUE constraint and destroy idempotency.
CREATE TABLE IF NOT EXISTS inma_webhook_events (
    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id          INTEGER     NOT NULL,
    event_id           TEXT        NOT NULL,
    event_type         TEXT        NOT NULL,
    occurred_at        TIMESTAMPTZ,
    actor_type         TEXT,
    origin_request_id  TEXT,
    body_company_id    INTEGER,
    customer_id        INTEGER,
    feature_group_id   INTEGER,
    selection_mode     TEXT,
    derived_status     TEXT,
    matched_lead_count INTEGER     NOT NULL DEFAULT 0,
    raw_body           TEXT        NOT NULL,
    raw_body_sha256    TEXT        NOT NULL,
    received_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_inma_webhook_events_tenant_event UNIQUE (tenant_id, event_id)
);

-- received_at index enables a future 30-day retention sweep (deferred hardening).
CREATE INDEX IF NOT EXISTS idx_inma_webhook_events_received_at
    ON inma_webhook_events (received_at);

-- Mandatory grant (new table) — the app role is 'invekto'.
GRANT ALL ON inma_webhook_events TO invekto;

-- (e) Fail loud if anything did not materialise.
DO $verify$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'leads' AND column_name = 'customer_status'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-065] leads.customer_status column missing after migration 065';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'leads' AND column_name = 'customer_status_occurred_at'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-065] leads.customer_status_occurred_at column missing after migration 065';
    END IF;

    IF to_regclass('public.inma_webhook_events') IS NULL THEN
        RAISE EXCEPTION '[INV-SEED-065] inma_webhook_events table missing after migration 065';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE indexname = 'idx_leads_tenant_normalized_phone'
    ) THEN
        RAISE EXCEPTION '[INV-SEED-065] idx_leads_tenant_normalized_phone missing after migration 065';
    END IF;
END
$verify$;

COMMIT;
