-- Migration 011: Hangfire storage schema (G7 Faz 1)
-- Purpose: Provide dedicated `hangfire` schema for Hangfire.PostgreSql storage.
--          Hangfire creates its own tables via PrepareSchemaIfNecessary=true at first
--          server start (advisory-lock protected, concurrent-safe).
-- Reference: arch/specs/g7-hangfire-migration.md, arch/plans/20260413-g7-hangfire-faz1.json

CREATE SCHEMA IF NOT EXISTS hangfire;

GRANT USAGE ON SCHEMA hangfire TO invekto;
GRANT ALL ON SCHEMA hangfire TO invekto;

-- Future Hangfire auto-created tables/sequences inherit grants via default privileges.
ALTER DEFAULT PRIVILEGES IN SCHEMA hangfire GRANT ALL ON TABLES TO invekto;
ALTER DEFAULT PRIVILEGES IN SCHEMA hangfire GRANT ALL ON SEQUENCES TO invekto;

COMMENT ON SCHEMA hangfire IS 'G7: Hangfire recurring/enqueued job storage. Tables auto-created by Hangfire on first server start.';
