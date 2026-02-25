-- Migration: Copy faq_entries (Automation) -> faqs (Knowledge)
-- Purpose: ai_faq node now uses Knowledge service semantic search instead of keyword match.
-- Run ONCE per environment. Safe to re-run (ON CONFLICT DO NOTHING).
-- After migration, call POST /api/v1/knowledge/{tenantId}/generate-embeddings per tenant
-- to generate pgvector embeddings for the migrated FAQs.

-- Step 1: Copy active FAQ entries from Automation's faq_entries to Knowledge's faqs table
-- Maps: question -> question, answer -> answer, keywords -> keywords (both text[])
-- Note: faqs table has UNIQUE(tenant_id, question) constraint — duplicates are skipped.
-- Note: faqs.source has CHECK constraint: only 'manual' or 'wa_import' allowed.
INSERT INTO faqs (tenant_id, question, answer, category, lang, keywords, source, is_active, created_at, updated_at)
SELECT
    fe.tenant_id,
    fe.question,
    fe.answer,
    'genel' AS category,
    'tr' AS lang,
    fe.keywords,
    'manual' AS source,
    true AS is_active,
    COALESCE(fe.created_at, NOW()) AS created_at,
    NOW() AS updated_at
FROM faq_entries fe
WHERE fe.is_active = true
ON CONFLICT (tenant_id, question) DO NOTHING;

-- Step 2: Verify migration counts per tenant
-- SELECT tenant_id, COUNT(*) as migrated_count
-- FROM faqs WHERE source = 'migration'
-- GROUP BY tenant_id ORDER BY tenant_id;

-- Step 3: Generate embeddings (run via API after SQL migration)
-- For each tenant with migrated FAQs:
--   POST /api/v1/knowledge/{tenantId}/generate-embeddings
-- This triggers OpenAI embedding generation for FAQs missing vectors.
