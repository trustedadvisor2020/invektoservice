-- Migration 002: AI Wizard history support for chatbot_flows
-- Adds wizard_history (JSONB) and wizard_status columns
-- wizard_status: 'drafting' (in progress), 'completed' (wizard done), NULL (not wizard-created)

ALTER TABLE chatbot_flows
  ADD COLUMN IF NOT EXISTS wizard_history JSONB DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS wizard_status VARCHAR(20) DEFAULT NULL;

-- Index for listing draft flows
CREATE INDEX IF NOT EXISTS idx_chatbot_flows_wizard_status
  ON chatbot_flows (tenant_id, wizard_status) WHERE wizard_status IS NOT NULL;

-- Grant
GRANT ALL ON chatbot_flows TO invekto_app;
