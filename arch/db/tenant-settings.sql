-- arch/db/tenant-settings.sql — CANONICAL schema for tenant_settings table.
-- FEAT-VCP Chunk A introduced this table for video_provider; future per-tenant toggles
-- land in this same table (single-row-per-tenant PK discipline).
--
-- Source migration: arch/db/migrations/023-tenant-settings-video-provider.sql
-- Any column additions land here first, then as an additive ALTER migration.

CREATE TABLE tenant_settings (
    tenant_id                 INTEGER     PRIMARY KEY
                                          REFERENCES tenant_registry(tenant_id) ON DELETE CASCADE,
    video_provider            VARCHAR(20) NULL
                                          CHECK (video_provider IS NULL
                                                 OR video_provider IN ('mock', 'googlemeet')),
    video_provider_config     JSONB       NULL,
    -- FEAT-VCP Chunk B (migration 024): per-tenant IANA timezone for ICS DTSTART TZID
    -- and WA meeting_start_local substitution. Default Europe/Istanbul for TR pilot.
    timezone                  VARCHAR(40) NOT NULL DEFAULT 'Europe/Istanbul',
    -- FEAT-J2 (migration 026): MessageCategory enforcement gate. FALSE keeps backward-
    -- compat (legacy flows with null event_name still send, INMA opt-out check skipped).
    -- TRUE rejects send_message without event_name (INV-OB-031). Pilot tenants only.
    enforce_message_category  BOOLEAN     NOT NULL DEFAULT FALSE,
    -- FEAT-DMP (migration 027): INMA DynamicMessage placeholder activation gate. TRUE (default)
    -- lets BroadcastOrchestrator/AutomationOrchestrator attach DynamicFields when a template
    -- contains INMA-reserved placeholders. FALSE forces legacy INSE substitution (admin escape
    -- hatch — if INMA DynamicMessage misbehaves tenant-side, disable without code change).
    enable_dynamic_message    BOOLEAN     NOT NULL DEFAULT TRUE,
    -- FEAT-TFM MVP (migration 028): per-tenant semantic→INMA key mapping for cf1..cf10. Shape:
    --   { "<semantic_name>": { "source": "cf1..cf10", "type": "enum|string|date|bool|int",
    --                           "enum_values": ["..."], "required": false } }
    -- Empty object = no mapping (DbTenantFieldMappingResolver returns null → DMP raw key fallback).
    -- Reserved name guard (InmaDynamicFieldKeys.Allowlist ∪ leads core columns) enforced app-side
    -- by TenantFieldMappingValidator (INV-BE-096..099).
    field_mapping             JSONB       NOT NULL DEFAULT '{}'::jsonb,
    -- FEAT-MCC (migration 030): per-tenant multi-city campaign config. Shape:
    --   { "campaigns": [
    --       { "slug": "...", "name": "...", "active": true,
    --         "start_date": "YYYY-MM-DD", "end_date": "YYYY-MM-DD",
    --         "cities": [{ "slug","name","country","timezone" }],
    --         "dates":  [{ "city","date","hours" }] }
    --     ] }
    -- Empty default = zero campaigns; window guard / {{campaign.*}} substitution become no-ops.
    -- App-side validation (TenantCampaignConfigValidator: INV-BE-118..120) enforces tenant-scope
    -- unique slug, lowercase regex, max 8 campaigns, max 20 cities/dates per campaign,
    -- start_date <= end_date, dates[].city referencing a campaigns[].cities[].slug.
    -- Window guard (INV-BE-119) lives in Automation outbound dispatch + Marketing EFS scheduler.
    campaign_config           JSONB       NOT NULL DEFAULT '{"campaigns":[]}'::jsonb,
    -- Paket B-META (migration 033): per-tenant Meta Leadgen webhook config. Shape:
    --   { "verify_token":     "<32-char random, Dashboard rotate>",
    --     "app_secret":       "<Meta App Secret, HMAC-SHA256 key for X-Hub-Signature-256>",
    --     "page_access_token":"<Page Access Token for Graph API /{leadgen_id}?fields=field_data>",
    --     "page_id":          "<Facebook Page id>",
    --     "field_id_map":     { "<meta_question_id>": "<canonical: name|phone|email|custom_1..5|consent_marketing>" } }
    -- Empty default ({}) = no Meta integration; webhook GET handshake returns 403
    -- (verify_token empty => INV-META-002) and POST returns 401 (signature mismatch
    -- => INV-META-001), both with meta_leadgen_events audit row INSERT.
    -- Secret storage is plaintext per LIW precedent (G3 2026-04-24); DB-level
    -- access-controlled (GRANT ALL invekto only). Application layer guards against
    -- jsonl logging (MetaLeadgenConfigDto never serialized verbatim to System* logs).
    meta_leadgen_config       JSONB       NOT NULL DEFAULT '{}'::jsonb,
    -- FEAT-CLINIC-METADATA (migration 040): per-tenant klinik iletisim bilgileri JSONB. Shape:
    --   { "name":      "...",                  -- klinik adi (e.g. "Dent Adavista Dental Clinic")
    --     "phone":     "+CC nnn nnn nn nn",    -- E.164 (validation INV-BE-123)
    --     "website":   "https://...",          -- HTTPS URL (validation INV-BE-124)
    --     "instagram": "https://...",          -- IG profile URL (optional)
    --     "facebook":  "https://...",          -- FB profile URL (optional)
    --     "address":   "...",                  -- full address line (e.g. "Kuşadası, Aydın, Türkiye")
    --     "city":      "..." }                 -- short city for inline mention (e.g. "Kuşadası")
    -- Empty default ({}) = klinik bilgisi yok; ClinicTemplateApplier {{clinic.X}} render bos string
    -- (graceful no-op). 5dk MemoryCache TTL (DbTenantClinicMetadataResolver) — Backend PUT
    -- cache.Invalidate, peer Automation/Marketing eventual consistency 5dk (FEAT-MCC pattern).
    -- App-side validation: phone E.164 (INV-BE-123), URL https?:// (INV-BE-124), JSON shape (INV-BE-122).
    clinic_contact            JSONB       NOT NULL DEFAULT '{}'::jsonb,
    -- FEAT-CLINIC-METADATA (migration 040): per-tenant ekip uyeleri JSONB array. Shape:
    --   [{ "role":     "dentist|receptionist|coordinator|...",
    --      "name":     "...",
    --      "title":    "...",
    --      "language": "en|tr|ar|de|fr|...",
    --      "pronouns": "she/her|he/him|they/them" }]
    -- Empty default ([]) = ekip yok; ClinicTemplateApplier {{team.role.field}} render bos string.
    -- Multi-role klinikler icin first-match rule (LINQ FirstOrDefault on role match — array order
    -- preserved). Index syntax {{team.dentist[0].name}} post-pilot Phase 2 backlog.
    -- Dashboard /settings/clinic-metadata CRUD ile yonetilir.
    team_members              JSONB       NOT NULL DEFAULT '[]'::jsonb,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Production role (see 2026-04-18 lesson: invekto, not invekto_app).
GRANT ALL ON tenant_settings TO invekto;
