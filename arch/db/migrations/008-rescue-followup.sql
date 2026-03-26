-- Migration 008: PKT-12 Faz 3 — Rescue Follow-Up Scheduler
-- Adds follow-up tracking columns to review_risks table
-- Date: 2026-03-26
-- Author: Q (PKT-12)

-- Follow-up status tracking
ALTER TABLE review_risks
    ADD COLUMN IF NOT EXISTS followup_status VARCHAR(30) NOT NULL DEFAULT 'none',
    ADD COLUMN IF NOT EXISTS followup_sent_at TIMESTAMPTZ;

-- Constraint for followup_status values
ALTER TABLE review_risks
    ADD CONSTRAINT chk_followup_status
    CHECK (followup_status IN ('none', 'satisfaction_sent', 'review_redirect_sent', 'completed', 'closed'));

-- Index for follow-up due queries (rescued risks pending follow-up)
CREATE INDEX IF NOT EXISTS idx_review_risks_followup_due
    ON review_risks (rescue_status, followup_status, updated_at)
    WHERE rescue_status = 'rescued';

-- Verify
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_name = 'review_risks'
  AND column_name IN ('followup_status', 'followup_sent_at')
ORDER BY ordinal_position;
