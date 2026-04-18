-- Migration 019: template_catalog.group_tag (FEAT-WTP)
-- Date: 2026-04-17
-- Purpose: Operational grouping label for template variants.
--          Enables welcome rotation ("welcome_with_date" / "welcome_no_date")
--          and FAQ variant pools ("faq_pricing" / "faq_hours") without
--          breaking existing suggestion/pgvector flows.
-- Related: arch/features/welcome-template-pack.md
-- Canonical: arch/db/template-catalog.sql (this ALTER must remain in sync)

ALTER TABLE template_catalog ADD COLUMN IF NOT EXISTS group_tag VARCHAR(50);

-- Partial index: most queries filter on a specific tag; NULL majority
-- (platform/legacy templates) is skipped by the index.
CREATE INDEX IF NOT EXISTS idx_tc_group_tag
    ON template_catalog (tenant_id, group_tag, lang)
    WHERE group_tag IS NOT NULL;
