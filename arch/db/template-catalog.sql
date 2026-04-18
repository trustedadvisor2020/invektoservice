-- ============================================================
-- Template System DDL - Sablon Sistemi
-- Service: Invekto.Knowledge (port 7104)
-- Database: invekto (shared)
-- Tables: template_catalog, template_versions, template_sources,
--         template_suggestions, template_adoptions,
--         template_performance, template_ab_tests
-- ============================================================
-- Prerequisite: pgvector extension (already from knowledge.sql)
-- Prerequisite: update_updated_at_column() trigger (already exists)
-- ============================================================

-- ============================================================
-- 1. template_catalog: Central template registry
--    3-tier hierarchy: platform > sector > tenant
--    5 types: faq, message, intent, flow, scenario
-- ============================================================
CREATE TABLE IF NOT EXISTS template_catalog (
    id                  SERIAL PRIMARY KEY,
    template_type       VARCHAR(30) NOT NULL,
    scope               VARCHAR(20) NOT NULL,
    sector              VARCHAR(50),
    tenant_id           INTEGER,
    parent_template_id  INTEGER,
    slug                VARCHAR(200) NOT NULL,
    name                VARCHAR(300) NOT NULL,
    description         TEXT,
    lang                VARCHAR(10) NOT NULL DEFAULT 'tr',
    tags                TEXT[] NOT NULL DEFAULT '{}',
    -- FEAT-WTP (migration 019): operational grouping label for variant rotation.
    -- Free-text; recommended values documented in arch/features/welcome-template-pack.md.
    group_tag           VARCHAR(50),
    content_json        JSONB NOT NULL,
    version             INTEGER NOT NULL DEFAULT 1,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    is_published        BOOLEAN NOT NULL DEFAULT false,
    usage_count         INTEGER NOT NULL DEFAULT 0,
    confidence_score    NUMERIC(4,3) NOT NULL DEFAULT 0.000,
    source_count        INTEGER NOT NULL DEFAULT 1,
    created_by          VARCHAR(100) NOT NULL DEFAULT 'manual',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_tc_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT fk_tc_parent
        FOREIGN KEY (parent_template_id) REFERENCES template_catalog(id),
    CONSTRAINT chk_tc_type
        CHECK (template_type IN ('faq', 'message', 'intent', 'flow', 'scenario')),
    CONSTRAINT chk_tc_scope
        CHECK (scope IN ('platform', 'sector', 'tenant')),
    CONSTRAINT chk_tc_scope_fields
        CHECK (
            (scope = 'platform' AND sector IS NULL AND tenant_id IS NULL) OR
            (scope = 'sector'   AND sector IS NOT NULL AND tenant_id IS NULL) OR
            (scope = 'tenant'   AND tenant_id IS NOT NULL)
        ),
    CONSTRAINT chk_tc_created_by
        CHECK (created_by IN ('manual', 'wa_import', 'system', 'ai_generated', 'migration'))
);

-- Unique slug per scope/sector/tenant/lang combo
CREATE UNIQUE INDEX IF NOT EXISTS uq_tc_slug
    ON template_catalog (slug, scope, COALESCE(sector, ''), COALESCE(tenant_id, 0), lang);

CREATE INDEX IF NOT EXISTS idx_tc_type_scope
    ON template_catalog (template_type, scope);

CREATE INDEX IF NOT EXISTS idx_tc_sector
    ON template_catalog (sector) WHERE sector IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_tc_tenant
    ON template_catalog (tenant_id) WHERE tenant_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_tc_active_published
    ON template_catalog (is_active, is_published)
    WHERE is_active = true AND is_published = true;

CREATE INDEX IF NOT EXISTS idx_tc_tags
    ON template_catalog USING gin(tags);

CREATE INDEX IF NOT EXISTS idx_tc_slug_lookup
    ON template_catalog (slug, lang);

CREATE INDEX IF NOT EXISTS idx_tc_parent
    ON template_catalog (parent_template_id) WHERE parent_template_id IS NOT NULL;

-- FEAT-WTP (migration 019): tenant-scoped variant pool lookup.
CREATE INDEX IF NOT EXISTS idx_tc_group_tag
    ON template_catalog (tenant_id, group_tag, lang)
    WHERE group_tag IS NOT NULL;

CREATE TRIGGER trigger_tc_updated_at
    BEFORE UPDATE ON template_catalog
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();


-- ============================================================
-- 2. template_versions: Append-only version history
-- ============================================================
CREATE TABLE IF NOT EXISTS template_versions (
    id                  BIGSERIAL PRIMARY KEY,
    template_id         INTEGER NOT NULL,
    version             INTEGER NOT NULL,
    content_json        JSONB NOT NULL,
    change_summary      VARCHAR(500),
    changed_by          VARCHAR(100) NOT NULL DEFAULT 'manual',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_tv_template
        FOREIGN KEY (template_id) REFERENCES template_catalog(id) ON DELETE CASCADE,
    CONSTRAINT uq_tv_template_version
        UNIQUE (template_id, version)
);

CREATE INDEX IF NOT EXISTS idx_tv_template
    ON template_versions (template_id, version DESC);


-- ============================================================
-- 3. template_sources: Provenance — which analyses contributed
--    "Bu FAQ 3 firmadan dogrulandi: ebrumoda (3829), firma_b (2100)"
-- ============================================================
CREATE TABLE IF NOT EXISTS template_sources (
    id                  SERIAL PRIMARY KEY,
    template_id         INTEGER NOT NULL,
    analysis_id         INTEGER NOT NULL,
    tenant_name         VARCHAR(200) NOT NULL,
    contribution_type   VARCHAR(20) NOT NULL DEFAULT 'new',
    sample_count        INTEGER NOT NULL DEFAULT 0,
    contributed_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_ts_template
        FOREIGN KEY (template_id) REFERENCES template_catalog(id) ON DELETE CASCADE,
    CONSTRAINT fk_ts_analysis
        FOREIGN KEY (analysis_id) REFERENCES wa_analyses(id),
    CONSTRAINT uq_ts_template_analysis
        UNIQUE (template_id, analysis_id),
    CONSTRAINT chk_ts_contribution_type
        CHECK (contribution_type IN ('new', 'update', 'confirm'))
);

CREATE INDEX IF NOT EXISTS idx_ts_template
    ON template_sources (template_id);

CREATE INDEX IF NOT EXISTS idx_ts_analysis
    ON template_sources (analysis_id);


-- ============================================================
-- 4. template_suggestions: Review queue after analysis extraction
--    Superadmin reviews: approve / edit+approve / reject
-- ============================================================
CREATE TABLE IF NOT EXISTS template_suggestions (
    id                      SERIAL PRIMARY KEY,
    analysis_id             INTEGER NOT NULL,
    suggestion_type         VARCHAR(20) NOT NULL,
    existing_template_id    INTEGER,
    similarity_score        NUMERIC(4,3),
    suggested_content_json  JSONB NOT NULL,
    suggested_slug          VARCHAR(200) NOT NULL,
    suggested_name          VARCHAR(300) NOT NULL,
    suggested_type          VARCHAR(30) NOT NULL,
    source_data_json        JSONB NOT NULL DEFAULT '{}'::jsonb,
    status                  VARCHAR(20) NOT NULL DEFAULT 'pending',
    reviewed_by             VARCHAR(100),
    reviewed_at             TIMESTAMPTZ,
    result_template_id      INTEGER,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_tsg_analysis
        FOREIGN KEY (analysis_id) REFERENCES wa_analyses(id),
    CONSTRAINT fk_tsg_existing
        FOREIGN KEY (existing_template_id) REFERENCES template_catalog(id),
    CONSTRAINT fk_tsg_result
        FOREIGN KEY (result_template_id) REFERENCES template_catalog(id),
    CONSTRAINT chk_tsg_suggestion_type
        CHECK (suggestion_type IN ('new', 'update', 'merge')),
    CONSTRAINT chk_tsg_suggested_type
        CHECK (suggested_type IN ('faq', 'message', 'intent', 'flow', 'scenario')),
    CONSTRAINT chk_tsg_status
        CHECK (status IN ('pending', 'approved', 'rejected', 'merged'))
);

CREATE INDEX IF NOT EXISTS idx_tsg_analysis_status
    ON template_suggestions (analysis_id, status);

CREATE INDEX IF NOT EXISTS idx_tsg_status
    ON template_suggestions (status) WHERE status = 'pending';


-- ============================================================
-- 5. template_adoptions: Tracks tenant template adoption
-- ============================================================
CREATE TABLE IF NOT EXISTS template_adoptions (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    template_id         INTEGER NOT NULL,
    adopted_version     INTEGER NOT NULL,
    target_type         VARCHAR(30) NOT NULL,
    target_id           INTEGER,
    customized          BOOLEAN NOT NULL DEFAULT false,
    adopted_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_ta_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT fk_ta_template
        FOREIGN KEY (template_id) REFERENCES template_catalog(id),
    CONSTRAINT uq_ta_tenant_template
        UNIQUE (tenant_id, template_id),
    CONSTRAINT chk_ta_target_type
        CHECK (target_type IN ('faq', 'outbound_template', 'chatbot_flow', 'faq_entry', 'intent_pattern'))
);

CREATE INDEX IF NOT EXISTS idx_ta_tenant
    ON template_adoptions (tenant_id);

CREATE INDEX IF NOT EXISTS idx_ta_template
    ON template_adoptions (template_id);


-- ============================================================
-- 6. template_performance: Daily metric buckets
-- ============================================================
CREATE TABLE IF NOT EXISTS template_performance (
    id                  BIGSERIAL PRIMARY KEY,
    template_id         INTEGER NOT NULL,
    tenant_id           INTEGER NOT NULL,
    variant             VARCHAR(10) NOT NULL DEFAULT 'A',
    metric_type         VARCHAR(30) NOT NULL,
    metric_value        NUMERIC(12,2) NOT NULL DEFAULT 0,
    period_start        DATE NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_tp_template
        FOREIGN KEY (template_id) REFERENCES template_catalog(id),
    CONSTRAINT fk_tp_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT chk_tp_variant
        CHECK (variant IN ('A', 'B')),
    CONSTRAINT chk_tp_metric
        CHECK (metric_type IN ('usage', 'sent', 'delivered', 'replied', 'converted', 'resolved')),
    CONSTRAINT uq_tp_daily
        UNIQUE (template_id, tenant_id, variant, metric_type, period_start)
);

CREATE INDEX IF NOT EXISTS idx_tp_template_period
    ON template_performance (template_id, period_start);

CREATE INDEX IF NOT EXISTS idx_tp_tenant_period
    ON template_performance (tenant_id, period_start);


-- ============================================================
-- 7. template_ab_tests: A/B test configuration
-- ============================================================
CREATE TABLE IF NOT EXISTS template_ab_tests (
    id                  SERIAL PRIMARY KEY,
    tenant_id           INTEGER NOT NULL,
    template_a_id       INTEGER NOT NULL,
    template_b_id       INTEGER NOT NULL,
    split_pct           INTEGER NOT NULL DEFAULT 50,
    status              VARCHAR(20) NOT NULL DEFAULT 'draft',
    winner_template_id  INTEGER,
    min_sample_size     INTEGER NOT NULL DEFAULT 100,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_ab_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id),
    CONSTRAINT fk_ab_template_a
        FOREIGN KEY (template_a_id) REFERENCES template_catalog(id),
    CONSTRAINT fk_ab_template_b
        FOREIGN KEY (template_b_id) REFERENCES template_catalog(id),
    CONSTRAINT fk_ab_winner
        FOREIGN KEY (winner_template_id) REFERENCES template_catalog(id),
    CONSTRAINT chk_ab_split
        CHECK (split_pct BETWEEN 10 AND 90),
    CONSTRAINT chk_ab_status
        CHECK (status IN ('draft', 'running', 'completed', 'cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_ab_tenant_status
    ON template_ab_tests (tenant_id, status) WHERE status = 'running';

-- ============================================================
-- Grants
-- ============================================================
GRANT ALL ON template_catalog TO invekto;
GRANT ALL ON template_versions TO invekto;
GRANT ALL ON template_sources TO invekto;
GRANT ALL ON template_suggestions TO invekto;
GRANT ALL ON template_adoptions TO invekto;
GRANT ALL ON template_performance TO invekto;
GRANT ALL ON template_ab_tests TO invekto;
GRANT ALL ON SEQUENCE template_catalog_id_seq TO invekto;
GRANT ALL ON SEQUENCE template_versions_id_seq TO invekto;
GRANT ALL ON SEQUENCE template_sources_id_seq TO invekto;
GRANT ALL ON SEQUENCE template_suggestions_id_seq TO invekto;
GRANT ALL ON SEQUENCE template_adoptions_id_seq TO invekto;
GRANT ALL ON SEQUENCE template_performance_id_seq TO invekto;
GRANT ALL ON SEQUENCE template_ab_tests_id_seq TO invekto;
