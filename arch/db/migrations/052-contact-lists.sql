-- =============================================================
-- Migration 052: FEAT-OBI Phase 1A — Contact Lists (data layer)
-- =============================================================
-- Reusable, tenant-scoped contact lists that feed the Phase 0 bulk
-- send engine. An operator uploads CSV/Excel (parsed client-side),
-- the rows land here deduped + normalized, and a list can later be
-- snapshotted into an immutable bulk_send_recipients campaign via the
-- preview-from-list endpoint.
--
-- Design (Codex plan review 2026-06-03):
--   * NO auto-partition — a single per-list cap + clear rejection
--     (a bulk WhatsApp send has its own small cap, so splitting a
--      100K list into _partN lists is the wrong model).
--   * Composite FK list_records(tenant_id,list_id) -> data_lists(tenant_id,id)
--     makes a cross-tenant child row impossible even with an app bug.
--   * Normalized partial-unique active name per tenant.
--   * Nullable normalized_phone + CHECK(sendable OR phone present) +
--     invalid_reason — invalid rows are kept (sendable=false), never
--     silently dropped.
--   * Import-status lifecycle (importing/ready/failed) so a failed
--     import never leaves a half-built 'ready' list.
--   * Counters maintained by repo recompute-after-batch (no per-row
--     trigger). contacted_count is intentionally NOT here (needs a
--     delivery-status join — deferred to Plan B / Export Manager).
--
-- snake_case, tenant_id scoped (single shared Postgres, rollout skill).
-- Idempotent: IF NOT EXISTS guards, safe to re-run. GRANT ALL is the
-- project canon (codex-context.md L20/L69); do NOT downgrade.
-- =============================================================

-- -------------------------------------------------------------
-- data_lists: one row per reusable contact list
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS data_lists (
    id                      BIGSERIAL PRIMARY KEY,
    tenant_id               INTEGER NOT NULL REFERENCES tenant_registry(tenant_id),
    name                    VARCHAR(255) NOT NULL,
    -- 'upload' (CSV/Excel) | 'export' (created from an Export Manager filter, Plan B)
    source                  VARCHAR(16) NOT NULL DEFAULT 'upload',
    -- visibility flag (archive without delete); does NOT control name uniqueness
    active                  BOOLEAN NOT NULL DEFAULT TRUE,

    -- import lifecycle:
    --   importing -> batch insert in progress (not yet sendable-from)
    --   ready     -> import committed, list usable for preview-from-list/send
    --   failed    -> an import failed mid-way; list is recoverable (retry/delete)
    status                  VARCHAR(16) NOT NULL DEFAULT 'ready',

    -- denormalized counters; recomputed authoritatively after each batch
    -- (single UPDATE = SELECT COUNT) — never incrementally.
    total_records           INTEGER NOT NULL DEFAULT 0,
    sendable_count          INTEGER NOT NULL DEFAULT 0,

    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    -- soft delete: preserves the list for reporting; frees the name for reuse
    deleted_at              TIMESTAMPTZ,

    CONSTRAINT chk_data_list_status CHECK (status IN ('importing','ready','failed')),
    CONSTRAINT chk_data_list_source CHECK (source IN ('upload','export')),
    -- Composite unique so list_records can FK on (tenant_id, id) and a
    -- cross-tenant child row becomes structurally impossible.
    CONSTRAINT uq_data_lists_tenant_id UNIQUE (tenant_id, id)
);

-- Normalized active-name uniqueness per tenant (case/space-insensitive),
-- scoped to non-deleted rows so a deleted list's name can be reused.
CREATE UNIQUE INDEX IF NOT EXISTS uq_data_lists_tenant_name_active
    ON data_lists (tenant_id, lower(btrim(name)))
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_data_lists_tenant_active
    ON data_lists (tenant_id, active, created_at DESC)
    WHERE deleted_at IS NULL;

-- -------------------------------------------------------------
-- list_records: contacts within a list
-- -------------------------------------------------------------
CREATE TABLE IF NOT EXISTS list_records (
    id                      BIGSERIAL PRIMARY KEY,
    list_id                 BIGINT NOT NULL,
    tenant_id               INTEGER NOT NULL,

    -- intl-safe E.164 (set by the shared PhoneNormalizer, server-authoritative).
    -- NULLABLE: invalid/unparseable rows are still stored (sendable=false) so
    -- the operator sees them in the list, but they can never be messaged.
    normalized_phone        VARCHAR(20),
    invalid_reason          VARCHAR(64),

    name                    VARCHAR(255),
    surname                 VARCHAR(255),
    email                   VARCHAR(320),
    tags                    TEXT,
    note                    TEXT,
    field1                  VARCHAR(255),
    field2                  VARCHAR(255),
    field3                  VARCHAR(255),
    field4                  VARCHAR(255),
    field5                  VARCHAR(255),
    custom_fields           JSONB,

    -- WhatsApp adaptation of TONIVA 'callable': gates inclusion when the list
    -- is snapshotted into a send. Invalid-phone rows are forced sendable=false.
    sendable                BOOLEAN NOT NULL DEFAULT FALSE,
    first_contact_at        TIMESTAMPTZ,

    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Cross-tenant integrity: child tenant_id MUST match its parent list's tenant.
    CONSTRAINT fk_list_records_list
        FOREIGN KEY (tenant_id, list_id)
        REFERENCES data_lists (tenant_id, id) ON DELETE CASCADE,
    -- A sendable record must carry a phone; invalid rows stay sendable=false.
    CONSTRAINT chk_list_record_sendable_phone
        CHECK (sendable = FALSE OR normalized_phone IS NOT NULL),
    -- Dedup guarantee within a list. NULL phones (invalid rows) are exempt
    -- (Postgres treats NULLs as distinct), which is intended.
    CONSTRAINT uq_list_record_list_phone UNIQUE (list_id, normalized_phone)
);

CREATE INDEX IF NOT EXISTS idx_list_records_list
    ON list_records (list_id);

-- Sendable-only snapshot scan for preview-from-list.
CREATE INDEX IF NOT EXISTS idx_list_records_list_sendable
    ON list_records (list_id) WHERE sendable = TRUE;

-- records/exists batch check ("is this phone already on file for this tenant").
CREATE INDEX IF NOT EXISTS idx_list_records_tenant_phone
    ON list_records (tenant_id, normalized_phone);

-- -------------------------------------------------------------
-- Grants (production runs as role 'invekto')
-- -------------------------------------------------------------
GRANT ALL ON data_lists TO invekto;
GRANT ALL ON list_records TO invekto;
GRANT ALL ON SEQUENCE data_lists_id_seq TO invekto;
GRANT ALL ON SEQUENCE list_records_id_seq TO invekto;

-- -------------------------------------------------------------
-- Postcondition verifier (fail-loud, INV-SEED-052)
-- -------------------------------------------------------------
DO $verify$
DECLARE
    v_has_data_lists      BOOLEAN;
    v_has_list_records    BOOLEAN;
    v_has_composite_uq    BOOLEAN;
    v_has_composite_fk    BOOLEAN;
    v_has_sendable_check   BOOLEAN;
    v_has_name_index      BOOLEAN;
BEGIN
    v_has_data_lists   := to_regclass('public.data_lists')   IS NOT NULL;
    v_has_list_records := to_regclass('public.list_records') IS NOT NULL;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'uq_data_lists_tenant_id'
    ) INTO v_has_composite_uq;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_list_records_list' AND contype = 'f'
    ) INTO v_has_composite_fk;

    SELECT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_list_record_sendable_phone' AND contype = 'c'
    ) INTO v_has_sendable_check;

    SELECT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE indexname = 'uq_data_lists_tenant_name_active'
    ) INTO v_has_name_index;

    IF v_has_data_lists IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-052: data_lists table missing after migration';
    END IF;
    IF v_has_list_records IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-052: list_records table missing after migration';
    END IF;
    IF v_has_composite_uq IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-052: data_lists composite unique (tenant_id,id) missing';
    END IF;
    IF v_has_composite_fk IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-052: list_records composite tenant FK missing';
    END IF;
    IF v_has_sendable_check IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-052: list_records sendable/phone CHECK missing';
    END IF;
    IF v_has_name_index IS DISTINCT FROM TRUE THEN
        RAISE EXCEPTION 'INV-SEED-052: data_lists normalized active-name unique index missing';
    END IF;

    RAISE NOTICE 'INV-SEED-052 OK: contact-list tables verified';
END
$verify$;
