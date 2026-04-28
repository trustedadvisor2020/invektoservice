-- Migration 038: Yol Haritasi Audit Data Fix (Paket 1 of 5)
-- Risk: LOW (data-only, idempotent UPDATE WHERE clauses)
-- Q kararlari (2026-04-29): batch A = Paket 1 + Paket 5 tek commit
-- Audit kaynagi: c:/tmp/tmp-superadmin-roadmap-audit-20260428.md (Codex+Claude v2)
-- Handoff: c:/tmp/tmp-roadmap-audit-next-session-handoff.md
--
-- Hedef: Audit raporundan cikan veri tutarsizligi bulgularini idempotent
-- UPDATE statement'lar ile gider. Kart status sahte sinyali (D005-D009 IN_PROGRESS
-- frontend %0-10), summary eskik (D006/D009 endpoint hazir ama body "eksik" diyor),
-- title sayim hatasi (O001 "7 sayfa" gercek 8), roll-up duplikasyon clarification
-- (D020 pilot-15-packets D021-D026 toplami).
--
-- §1 Status downgrade D005-D009 IN_PROGRESS -> TODO (frontend %0-10 sahte sinyal)
UPDATE kanban_cards
SET status = 'TODO', updated_at = NOW()
WHERE board_key = 'dent-pilot'
  AND status = 'IN_PROGRESS'
  AND card_slug IN (
    'lead-list-page',                  -- D005
    'lead-detail-timeline',            -- D006
    'appointments-page-zoho-sync',     -- D007
    'doctors-management-page',         -- D008
    'conversion-funnel-chart'          -- D009
  );

-- §2 D006 (lead-detail-timeline) summary reality fix
-- Onceki: "GET /api/v1/leads/{id}/activities + add note UI."
-- Sonraki: "Endpoint hazir (Program.cs:5712, 5750); LeadDetailPage Timeline tab eksik."
UPDATE kanban_cards
SET summary = 'Endpoint hazir (Program.cs:5712, 5750); LeadDetailPage Timeline tab eksik.',
    updated_at = NOW()
WHERE board_key = 'dent-pilot'
  AND card_slug = 'lead-detail-timeline';

-- §3 D009 (conversion-funnel-chart) summary tightening
-- Onceki: "/api/v1/leads/funnel endpoint`i var, gorsel yok." (yeterli aciklayici degil)
-- Sonraki: "Endpoint hazir (Program.cs:5771); frontend chart komponenti eksik."
UPDATE kanban_cards
SET summary = 'Endpoint hazir (Program.cs:5771); frontend chart komponenti eksik.',
    updated_at = NOW()
WHERE board_key = 'dent-pilot'
  AND card_slug = 'conversion-funnel-chart';

-- §4 O001 (dashboard-config-7-pages) title sayim fix + summary reality match
-- Onceki: title "(7 sayfa)" + body 8 madde + reality 5/8 implementasyon
-- Sonraki: title "(8 ayar yuzeyi)" + summary 5/8 split (implement vs eksik)
UPDATE kanban_cards
SET title = 'E.1-E.8 Dashboard Konfigurasyon (8 ayar yuzeyi)',
    summary = 'Tenant ayarlari + LIW + VCP + MCC + EFS + TFM + Templates + Meta Leadgen. 5/8 implement (CampaignConfig + FieldMapping + FollowupSequence + LeadIntake + MetaLeadgen); 3 eksik (E.1 Tenant ayarlari, E.3 VCP, E.7 Templates ayri).',
    updated_at = NOW()
WHERE board_key = 'dent-pilot'
  AND card_slug = 'dashboard-config-7-pages';

-- §5 D020 (pilot-15-packets) roll-up clarification
-- Onceki summary: "Sistem yazilim tarafi tamamen hazir." (ne D020 olduğu belli değil)
-- Sonraki: explicit roll-up notu D021-D026 baglantili
UPDATE kanban_cards
SET summary = '15 paket roll-up (D021-D026 alt-paket toplami: Meta + Hangfire + Migration 031/033/034 + Plan tier upgrade); sistem yazilim tarafi tamamen hazir.',
    updated_at = NOW()
WHERE board_key = 'dent-pilot'
  AND card_slug = 'pilot-15-packets';

-- §6 Postcondition INV-SEED-028 — fail-loud verify
DO $verify$
DECLARE
  inprogress_count INT;
  o001_title TEXT;
  d006_summary TEXT;
BEGIN
  -- AC1: D005-D009 IN_PROGRESS purge
  SELECT COUNT(*) INTO inprogress_count
  FROM kanban_cards
  WHERE board_key = 'dent-pilot'
    AND status = 'IN_PROGRESS'
    AND card_slug IN (
      'lead-list-page',
      'lead-detail-timeline',
      'appointments-page-zoho-sync',
      'doctors-management-page',
      'conversion-funnel-chart'
    );
  IF inprogress_count > 0 THEN
    RAISE EXCEPTION '[INV-SEED-028] Migration 038 audit-fix postcondition: D005-D009 still IN_PROGRESS (count=%) — status downgrade UPDATE failed', inprogress_count;
  END IF;

  -- AC2: O001 title 8 ayar yuzeyi
  SELECT title INTO o001_title
  FROM kanban_cards
  WHERE board_key = 'dent-pilot' AND card_slug = 'dashboard-config-7-pages';
  IF o001_title IS NULL OR o001_title NOT LIKE '%8 ayar yuzeyi%' THEN
    RAISE EXCEPTION '[INV-SEED-028] Migration 038 audit-fix postcondition: O001 title NOT updated to "(8 ayar yuzeyi)" (got: %)', COALESCE(o001_title, '<NULL>');
  END IF;

  -- AC3: D006 summary endpoint-hazir reality match
  SELECT summary INTO d006_summary
  FROM kanban_cards
  WHERE board_key = 'dent-pilot' AND card_slug = 'lead-detail-timeline';
  IF d006_summary IS NULL OR d006_summary NOT LIKE '%Endpoint hazir%' THEN
    RAISE EXCEPTION '[INV-SEED-028] Migration 038 audit-fix postcondition: D006 summary NOT reality-aligned (got: %)', COALESCE(d006_summary, '<NULL>');
  END IF;

  RAISE NOTICE '[INV-SEED-028] Migration 038 audit-fix verify PASS — D005-D009 TODO + O001 title sync + D006 summary reality match';
END
$verify$;
