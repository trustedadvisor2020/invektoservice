-- Migration 020: leads.faq_rotation_state (FEAT-WTP)
-- Date: 2026-04-17
-- Purpose: Per-lead, per-group-tag variant counter for FAQ/message round-robin.
--          Shape: JSONB map { "<group_tag>": <next_variant_index:int> }.
--          Counter is ALWAYS the NEXT index to emit; Automation reads, picks,
--          and writes (idx+1) % variantCount atomically via jsonb_set.
-- Related: arch/features/welcome-template-pack.md AC-3
-- Canonical: arch/db/pkt6b1-niche-business.sql (this ALTER must remain in sync)

ALTER TABLE leads
    ADD COLUMN IF NOT EXISTS faq_rotation_state JSONB NOT NULL DEFAULT '{}'::jsonb;

-- No index: state is read/written only via lead PK lookup; JSONB size bounded
-- by count of distinct group_tags per tenant (~10s, not 1000s).
