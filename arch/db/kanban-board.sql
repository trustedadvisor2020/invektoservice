-- =============================================================
-- FEAT-PILOT-KANBAN: SuperAdmin Pilot Tracking Board (canonical schema)
-- Service: Invekto.Backend (port 5000)
-- Database: invekto (PostgreSQL, shared instance)
-- Convention: snake_case for all identifiers
-- Migration: 035-kanban-cards.sql (idempotent)
-- Plan: arch/plans/20260428-feat-pilot-kanban-superadmin.json
-- Tracking: tracking/20260428-feat-pilot-kanban-superadmin.md
-- =============================================================
--
-- Purpose:
--   Q's "tum gidisat" tracking aracı. Read-only SuperAdmin Dashboard
--   surface (PilotKanbanPage + KanbanDrawer). Mutation tek path:
--   /wrap workflow Step 3.5 hibrit (otomatik öner + Q onayla -> API PATCH).
--
-- Q Decisions (2026-04-28):
--   1a Platform-level board_key (FK YOK)
--   2a PostgreSQL (vs JSON file)
--   3c Hibrit /wrap (öner+onay+PATCH)
--   G1 Drawer slide-in (drag-drop YOK, manuel edit YOK)
--   G2 History/audit YOK (sadece updated_at + completed_at)
--   G3 Tüm detay drawer'da inline
--   G4 Migration içinde seed (idempotent ON CONFLICT DO NOTHING)
--
-- Depends on: NONE (FK yok — board_key string-keyed; gelecek
--             tenant-scoped board'lar tenant_id BIGINT NULL ile
--             izolasyon flag'i kullanir, FK reference'i yok).
--
-- =============================================================
-- kanban_cards: Pilot tracking kart'lari (board_key + card_slug UNIQUE)
-- =============================================================

CREATE TABLE IF NOT EXISTS kanban_cards (
    id              BIGSERIAL PRIMARY KEY,

    -- Board identity (composite UNIQUE = /wrap PATCH lookup anchor).
    board_key       VARCHAR(64)  NOT NULL,                  -- 'dent-pilot' veya 'tenant-X-pilot'
    card_slug       VARCHAR(128) NOT NULL,                  -- e.g. 'meta-hsm-template', 'b-vcp-doctors'

    -- Tenant scope (NULL = platform-level, e.g. 'dent-pilot' SuperAdmin only).
    -- Gelecek tenant-scoped board'lar BIGINT tenant_id ile scope'lanir; runtime
    -- query'ler WHERE tenant_id = $1 ile filtreler. FK YOK — board_key string
    -- olduğu için tenant_id sadece izolasyon flag'i (cross-tenant guard /api/ops
    -- + repository layer'inda enforce edilir).
    tenant_id       BIGINT       NULL,

    -- Status enum (5 deger) — chk_kanban_cards_status CHECK constraint
    -- birebir KanbanStatusExtensions.DbValue* sabitleri ile senkrondur.
    status          VARCHAR(20)  NOT NULL DEFAULT 'TODO',

    -- Category enum (6 deger) — chk_kanban_cards_category CHECK constraint
    -- birebir KanbanCategoryExtensions.DbValue* sabitleri ile senkrondur.
    category        VARCHAR(20)  NOT NULL DEFAULT 'OPS',

    -- Priority enum (4 deger): P0 pilot blocker, P1 stage 2-3, P2 standart, P3 nice-to-have.
    priority        VARCHAR(8)   NOT NULL DEFAULT 'P2',

    -- Display order within column (10/20/30... → yeni kart araya 15/25 ile eklenebilir).
    position        INTEGER      NOT NULL DEFAULT 100,

    -- Display content.
    title           VARCHAR(255) NOT NULL,
    summary         TEXT,                                    -- Kart üzerinde + drawer "Özet" bölümü
    body_markdown   TEXT,                                    -- Drawer'da MarkdownLite (XSS-safe) ile render — bold/italic/code/link/list
    owner           VARCHAR(64),                             -- "Müşteri", "Q + Müşteri", "Dev", "Invekto Ops" vb.
    eta             VARCHAR(32),                             -- "~3 gün", "24-48h", "Toplantı" gibi serbest format

    -- Source link (drawer'da tiklanabilir).
    source_file     VARCHAR(255),                            -- Repo-relative path (örn. 'DentAdavista/plan/dent-golive.html')
    source_anchor   VARCHAR(64),                             -- '#section-id' (opsiyonel)

    -- Lifecycle timestamps.
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),    -- /wrap PATCH her zaman tazeler
    completed_at    TIMESTAMPTZ,                             -- DONE'a geçince NOW() otomatik (re-open NULL'a çekilir)

    -- Reference code (Migration 036) — kart kimligi 4-karakter mnemonic
    -- (kategori prefix + 3 rakam, ornek C001 / K005 / D021 / U009 / X003).
    -- '----' placeholder = henuz atanmamis (yeni eklenen kart). UNIQUE (board_key,
    -- ref_code) WHERE ref_code <> '----' partial index ile atanmislar UNIQUE.
    -- /wrap workflow Step 3.5 commit message'da slug VEYA ref_code match destekler.
    ref_code        VARCHAR(4)   NOT NULL DEFAULT '----'
);

-- =============================================================
-- Constraints
-- =============================================================

-- Composite UNIQUE: /wrap workflow Step 3.5 PATCH lookup anchor + idempotent seed.
ALTER TABLE kanban_cards
    ADD CONSTRAINT uq_kanban_cards_board_slug UNIQUE (board_key, card_slug);

-- Status enum (KanbanStatusExtensions.DbValue* mirror).
ALTER TABLE kanban_cards
    ADD CONSTRAINT chk_kanban_cards_status
    CHECK (status IN ('BLOCKED','TODO','IN_PROGRESS','BACKLOG','DONE'));

-- Category enum (KanbanCategoryExtensions.DbValue* mirror).
ALTER TABLE kanban_cards
    ADD CONSTRAINT chk_kanban_cards_category
    CHECK (category IN ('CUSTOMER','OPS','DEV','DECISION','UI','DOC'));

-- Priority enum.
ALTER TABLE kanban_cards
    ADD CONSTRAINT chk_kanban_cards_priority
    CHECK (priority IN ('P0','P1','P2','P3'));

-- Ref code format (Migration 036): '----' placeholder VEYA '^[A-Z][0-9]{3}$'.
ALTER TABLE kanban_cards
    ADD CONSTRAINT chk_kanban_cards_ref_code
    CHECK (ref_code = '----' OR ref_code ~ '^[A-Z][0-9]{3}$');

-- =============================================================
-- Indexes
-- =============================================================

-- Kanban render hot path (status grouping + position sort within column).
CREATE INDEX idx_kanban_cards_board_status
    ON kanban_cards(board_key, status, position);

-- Recent activity sort (drawer "Son güncelleme" + Frontend stats).
CREATE INDEX idx_kanban_cards_board_updated
    ON kanban_cards(board_key, updated_at DESC);

-- Tenant-scoped board partial index — platform-level (tenant_id IS NULL)
-- kart'lari index'e girmez, depo boyutu küçük kalır.
CREATE INDEX idx_kanban_cards_tenant
    ON kanban_cards(tenant_id, board_key)
    WHERE tenant_id IS NOT NULL;

-- Ref code partial UNIQUE (Migration 036): atanmis ref_code'lar per-board UNIQUE.
-- Birden fazla kart '----' placeholder olabilir (yeni eklenen, atanmamis); regex
-- match olanlar (CXXX/KXXX/DXXX/UXXX/XXXX) board_key icinde UNIQUE.
CREATE UNIQUE INDEX uq_kanban_cards_board_ref
    ON kanban_cards(board_key, ref_code)
    WHERE ref_code <> '----';

-- =============================================================
-- Permissions
-- =============================================================
-- Lesson 2026-04-18: invekto role (NOT invekto_app).

GRANT ALL ON kanban_cards TO invekto;
GRANT USAGE, SELECT ON SEQUENCE kanban_cards_id_seq TO invekto;

-- =============================================================
-- API Surface (reference)
-- =============================================================
--   GET   /api/ops/kanban/{board_key}                       -> KanbanBoardDto
--   PATCH /api/ops/kanban/{board_key}/cards/{card_slug}      -> KanbanCardDto
--
-- Auth: Ops mode (Backend ValidateOpsAuth + OpsUnauthorized helper'lari
--       Func parametresi olarak KanbanEndpoints'e gecirilir; tenant
--       kullanicilari (session.tenantId != 0) reddedilir 401/403).
--
-- Error codes (arch/errors.md):
--   INV-SEED-024  : seed postcondition failure (kart sayisi < 40)
--   INV-SEED-025  : constraint postcondition failure (UNIQUE/CHECK eksik)
--   INV-KB-001    : KanbanStatus enum out-of-range (defensive guard)
--   INV-KB-002    : KanbanCategory enum out-of-range (defensive guard)
--   INV-KB-003    : PATCH body status invalid (400)
--   INV-KB-004    : PATCH card_slug bulunamadi (404)
--   INV-KB-005    : DB connection / query failure (503)

-- =============================================================
-- Seed (board_key='dent-pilot')
-- =============================================================
-- Migration 035 ~50 kart insert eder (BLOCKED/TODO/IN_PROGRESS/BACKLOG/DONE).
-- ON CONFLICT (board_key, card_slug) DO NOTHING → idempotent re-run safe.
-- Mevcut row'larin status/title kirilmaz; sadece yeni slug'lar insert edilir.
-- Detay: arch/db/migrations/035-kanban-cards.sql §6.1-§6.5.
