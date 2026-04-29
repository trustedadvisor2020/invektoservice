-- Migration 043: FEAT-PILOT-FLAGS-UI + FEAT-VCP-PHASE2-UI kanban kart kayitlari
--
-- UI Gap Audit (2026-04-29 conversation): tenant_settings'de 5 alan backend'de
-- hazir ama Dashboard UI'si YOK. 2 yeni paket kayit:
--   D032 = FEAT-PILOT-FLAGS-UI (4 toggle: enforce_message_category +
--          enable_dynamic_message + efs_test_mode + efs_no_reply_threshold_days)
--   D033 = FEAT-VCP-PHASE2-UI (video_provider Mock/GoogleMeet picker + config)
--
-- ref_code D032/D033 (Migration 036 namespace D = DEV).
-- depends_on: D033 -> B0 backlog (FEAT-VCP Chunk C OAuth) — kanban'da B0
--             ayri kart degil (BACKLOG b0-google-meet-oauth referans), regex
--             '^[A-Z][0-9]{3}$' uyumlu degil. NULL kalir, body'de aciklama.
--
-- Idempotency: ON CONFLICT (board_key, card_slug) DO NOTHING.

INSERT INTO kanban_cards (
    board_key, card_slug, status, category, priority, position,
    title, summary, body_markdown,
    owner, eta, source_file, source_anchor,
    ref_code, depends_on
) VALUES
(
    'inse', 'feat-pilot-flags-ui', 'BACKLOG', 'DEV', 'P2', 240,
    'FEAT-PILOT-FLAGS-UI: Advanced Settings Dashboard',
    'UI Gap Audit 2026-04-29: 4 tenant_settings flag (enforce_message_category + enable_dynamic_message + efs_test_mode + efs_no_reply_threshold_days) backend hazir ama Dashboard UI YOK. Tek "Advanced Settings" tab + GET/PUT endpoint.',
    E'**UI Gap Audit bulgusu (2026-04-29):** UI coverage 9/14 = 64%. Bu paket 4/5 eksik UI alanini kapsar (kalan 1: video provider = D033).\n\n**Backend hazir, UI eksik:**\n- `enforce_message_category` (BOOL) - FEAT-J2 marketing/transactional category guard\n- `enable_dynamic_message` (BOOL) - FEAT-DMP INMA dynamicMessage on/off\n- `efs_test_mode` (BOOL) - FEAT-EFS drip delay_days→minutes test mode\n- `efs_no_reply_threshold_days` (INT 1-90) - FEAT-EFS no-reply trigger\n\n**Su anki yonetim:** Manuel SQL UPDATE.\n\n**Scope:**\n- Backend: GET/PUT /api/v1/tenant-settings/advanced + INV-BE-122/123 + cache invalidate push\n- Dashboard: AdvancedSettingsPage.tsx + /settings/advanced route + 4 input + 403 typed catch\n- Migration N: kolon validation guard + default values\n\n**Aktivasyon gate:** Pilot Stage 3 sonrasi (P2 oncelik, blocker degil), 2. musteri onboarding pre-req.\n\n**Tracking:** tracking/feat-pilot-flags-ui.md',
    'Dev', '~2 gun',
    'tracking/feat-pilot-flags-ui.md', NULL,
    'D032', NULL
),
(
    'inse', 'feat-vcp-phase2-ui', 'BACKLOG', 'DEV', 'P2', 250,
    'FEAT-VCP-PHASE2-UI: Video Provider Settings Dashboard',
    'UI Gap Audit 2026-04-29: tenant_settings.video_provider + video_provider_config backend hazir (FEAT-VCP-A/B DEPLOYED) ama Dashboard UI YOK. Provider picker (Mock/GoogleMeet) + config form.',
    E'**UI Gap Audit bulgusu (2026-04-29):** Video provider tenant_settings.video_provider (VARCHAR) + video_provider_config (JSONB) **backend hazir, FEAT-VCP Chunk A/B DEPLOYED ama Dashboard UI YOK.**\n\n**Provider secenekleri:**\n- mock (default — deterministic SHA256 link + ICS RFC 5545)\n- googlemeet (Chunk C OAuth provision sonrasi)\n\n**video_provider_config JSONB:**\n- google_oauth_client_id (googlemeet only)\n- google_oauth_client_secret_ref (KMS ref, plain TEXT YASAK)\n- google_workspace_user_email\n- default_meeting_duration_minutes (default 30)\n- calendar_event_organizer\n\n**Scope:**\n- Backend: GET/PUT /api/v1/tenant-settings/video-provider + provider whitelist + JSONB schema validation + secret_ref KMS path validation + INV-BE-124 + INV-INT-148/149\n- Dashboard: VideoProviderSettingsPage.tsx + /settings/video-provider route + radio picker + dinamik config form + Test connection (GoogleMeet ICS round-trip mock)\n- Migration N: tenant default mock set\n\n**Bagimlilik:** B0 backlog FEAT-VCP Chunk C (Q Google Workspace OAuth provision). UI Chunk C oncesi build edilebilir; GoogleMeet secimi "OAuth provision pending" disabled state.\n\n**Aktivasyon gate:** Pilot smoke S6 Mock provider yeterli ✅; production GoogleMeet icin Chunk C ile birlikte deploy.\n\n**Tracking:** tracking/feat-vcp-phase2-ui.md',
    'Dev', '~3 gun',
    'tracking/feat-vcp-phase2-ui.md', NULL,
    'D033', NULL
)
ON CONFLICT (board_key, card_slug) DO NOTHING;

-- §2. Postcondition guard
DO $verify$
DECLARE
    new_cards integer;
BEGIN
    SELECT COUNT(*) INTO new_cards
      FROM kanban_cards
     WHERE board_key='inse'
       AND card_slug IN ('feat-pilot-flags-ui', 'feat-vcp-phase2-ui');

    IF new_cards <> 2 THEN
        RAISE EXCEPTION '[INV-SEED-037] Migration 043 eksik: 2 kart bekleniyordu, % bulundu.', new_cards;
    END IF;

    RAISE NOTICE 'Migration 043 OK: % yeni UI gap kart insert/idempotent (D032 + D033, board=inse).', new_cards;
END$verify$;
