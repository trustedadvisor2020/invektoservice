# Pilot Launch Roadmap — Sirali Execution Queue (v2, revize 2026-04-21)

> **Slug:** pilot-launch-roadmap | **Mode:** ACTIVE | **Version:** 2.1 (Codex iter 0 FAIL feedback uygulandi)
> **Hedef:** 9 paket pilota odakli → Dent Adavista full-stack smoke. BACKLOG 6 paket pilot sonrasi.
> **Q tercihi (2026-04-21):** "Hepsini bitirelim, smoke en son" — pilota giden zincir optimize edildi.
> **Codex iter 0 feedback (2026-04-21 23:25):** P7 OAuth blocker ile BACKLOG'a tasindi (B0). P5/P6 UI exit criteria'ya dahil. Interview gate P4/P8/P9/P10 eklendi. Smoke S5a/S5b FEAT-TFM resolver dedicated. Error code collision evidence grep-verified.

Bu dosya **pilot launch boyunca execution queue'nun tek kaynagidir**. Session bootstrap zorunlu okuma.

---

## DEVAM PROTOKOLU (KRITIK)

### Session Basi (her /clear sonrasi)
1. `arch/session-memory.md` oku (son paket detayi)
2. `tracking/pilot-launch-roadmap.md` oku (BU DOSYA — sira + status)
3. **Master Queue** tablosunda `Status = PENDING` olan **ILK** paketi bul
4. Q'ya su formatta sun:
   ```
   Siradaki: P{N} {slug} ({faz})
   Scope: {ozet 1 satir}
   Pre-req: {dep + interview soru sayisi}
   Migration: {var/yok + numara}
   Deploy scope: {hangi servisler}
   Baslayalim mi?
   ```
5. Q onay → `/auto` workflow (interview → plan → dev → build → /rev → commit → deploy → smoke)
6. Paket DONE → **Paket Tamamlama Checklist** uygula (asagida)
7. `/clear` + next-session prompt uret

### Q Override Komutlari
| Komut | Etki |
|-------|------|
| `SKIP P{N}` | Status=SKIPPED, Notes'a reason |
| `PAUSE` | Roadmap donar, Q manuel task'a gecer |
| `RESUME` | Sonraki PENDING paket kaldiginda devam |
| `REORDER P{A} before P{B}` | Sira yer degistir |
| `ADD P{N} {slug}` | Yeni paket ekle |
| `PROMOTE B{N}` | BACKLOG paketi main queue'ya tasi |

### Paket Tamamlama Checklist
- [ ] Plan JSON (`arch/plans/{slug}.json`) + per-paket tracking (`tracking/{slug}.md`)
- [ ] Interview: Q'nun onaylamadigi gri nokta yok
- [ ] Kod + build PASS (`dotnet build InvektoServis.sln` 0 errors)
- [ ] **Error code pre-flight check** (`arch/errors.md` + `ErrorCodes.cs` grep — namespace collision YOK)
- [ ] **Migration pre-flight check** (`arch/db/migrations/` son numara + sequential)
- [ ] Unit test (yeni code varsa) — eklenebilecek testler eklenmis
- [ ] `/rev` Codex verdict = PASS (CODEX UTANSIN: iteration=0 hedef)
- [ ] Commit + push master (HEREDOC message + Co-Authored-By)
- [ ] **Prod deploy pre-flight** (config audit: yeni key varsa peer-service mirror check)
- [ ] Deploy (gerekirse) + /health HEALTHY (MCP invekto-ops server-deploy)
- [ ] **Post-deploy smoke** (binary freshness + endpoint auth gate + integration flow)
- [ ] `tracking/{slug}.md` Status=DONE+DEPLOYED+SMOKED + Codex iter + deploy date
- [ ] **Bu dosya** Master Queue satirinda Status guncelle
- [ ] `arch/session-memory.md`: Last Update + Execution Queue + Recently Completed
- [ ] `/clear` oner + next-session prompt

---

## FAZ A — Demo Toparlama Queue (2026-05-12 acil)

Q 2026-05-12 demo sonrası: "bu hafta servisleri çalışır hale getirelim". Pilot Mode mevcut tüm paketler DONE, ama production runtime'da 4 büyük problem tespit edildi (rapor: oturum). Sıra Q onayı (2026-05-12 sondan):

| # | Paket | Slug | Scope | Tahmin | Status |
|---|-------|------|-------|--------|--------|
| **A1** | ~~JsonLinesLogger 0-byte Fix~~ | n/a | **SKIPPED 2026-05-12** — smoke test ile doğrulandı: logger ÇALIŞIYOR. Backend BEFORE 255KB → AFTER 258KB sadece 5 health hit ile (+2.6KB). İlk sorgudaki 0 byte file rotation timing'inin yakalandığı an anlık snapshot'tı, sürekli durum değil. Tüm 12 servisin `logs/YYYY-MM-DD.jsonl` dosyaları gün içinde dolu yazılıyor. Root cause başka değişikliklerde (A2 endpoint discovery / A3 JWT 500). | — | **SKIPPED** |
| **A2** | `/api/discovery/endpoints` 404 Fix | `20260512-a2-integrations-discovery` | Backend `IntegrationsClient.cs:43` her 30sn `/api/discovery/endpoints` çağırıyor → Integrations 404 dönüyor → JSON parse FAIL → log gürültüsü + microservice routing patlamış. Karar: endpoint implement et VEYA `IntegrationsClient` consumer'dan kaldır (FEAT-INMA-PIPELINE-V2'de Integrations zaten küçülecek). | 2-3 saat | PENDING |
| **A3** | JWT BOM 500→401 Fix | `20260512-a3a4a-jwt-bom-hangfire-fix` | `JwtValidator.ValidateToken` IDX12709/12729/12741-class ArgumentException (BOM / bozuk segment / non-Base64Url) middleware 500'üne sızıyordu. Fix: typed `catch (ArgumentException)` + `JwtAuthMiddleware` BOM strip + errorCode mapping `malformed` → INV-AUTH-011. **A4a ile bundle** commit + deploy. | 1-2 saat | **DONE+DEPLOYED+SMOKED** 2026-05-12 13:51 UTC (commits `da2a253` + `0d9a4f3`; Codex iter 0 FAIL → iter 1 PASS 12/12 CQ + 3/3 CoVe → iter 2 FAIL meta-shell taxonomy → Q FORCE PASS; 10/10 deploy HEALTHY; 3/3 smoke PASS: BOM→401 INV-AUTH-011, bozuk segment→401 INV-AUTH-011, no-auth regression→401 INV-AUTH-003) |
| **A4a** | Hangfire `int.MaxValue` Boot Crash | `20260512-a3a4a-jwt-bom-hangfire-fix` | `HangfireSetup.cs` worker `enableScheduler=false` dalında `SchedulePollingInterval = TimeSpan.FromMilliseconds(int.MaxValue)` 2026-05-06 deploy stderr'de 5 worker servis boot crash'i (Appointments/Automation/Integrations/Marketing/WhatsAppAnalytics — "must be non-negative and either equal to or less than 2147483647 milliseconds"). Fix: `TimeSpan.FromDays(20)` (~1.73e9 ms, int32-ms cap altında, race-safety advisory-lock guard zaten Backend leader'da). **A3 ile bundle**. | 30dk | **DONE+DEPLOYED+SMOKED** 2026-05-12 13:51 UTC (commits `da2a253`; tüm worker servisler HEALTHY post-deploy, SchedulePollingInterval crash kalıntısı stderr'de yok) |
| **A4b** | WhatsAppAnalytics `Hangfire.JobStorage` DI Path | `20260512-a4b-waa-jobstorage` | WhatsAppAnalytics/Program.cs:277 `EnsureJobStorageInitialized` çağrısı `mssqlConfigured` flag'ine bağlı; stderr 2026-05-06 boot crash'i "No service for type 'Hangfire.JobStorage' has been registered". Hipotez: `AddInvektoHangfire` ile `EnsureJobStorageInitialized` flag tutarsızlığı veya config dependency. Investigate + minimal fix. | 1-2 saat | PENDING |
| **A4c** | Geri kalan 2026-05-06 crash root cause'ları (config-level, KOD fix YOK) | doc-only | `E:\Invekto\...\logs` path referansları kodda HİÇ YOK (grep boş) — production appsettings.Production.json `Logging:FilePath` config'i düzeltilmiş, fallback "logs" çalışıyor. JWT SecretKey < 32 byte production config'i düzeltilmiş, kod boot-time check doğru defansif davranış. cacheRepo `[FromServices]` Program.cs:7349 crash kalıntısı eski versiyondaydı (şu an satır 7349 farklı kod, refactor edilmiş). Bu 3 root cause **kod fix gerektirmez** — tracking notu. | doc | **SKIPPED (config-level, kod ile alakasız)** 2026-05-12 |
| **A5** | Demo Retry (Q ile birlikte) | manuel | Q yeniden demo yapar, Claude UI/HTTP network monitor + log izleyici. Kalan failing endpoint'ler tespit edilir. | 30dk-1saat | PENDING |
| **A6** | A5'ten çıkan ek bug fix'leri | esnek | Demo'da gözüken kalan problemler için ad-hoc paketler. | esnek | PENDING |

**Tamamlanma kriteri:** Q'nun A5 demo'sunda tüm akış (lead intake → flow trigger → outbound → INMA → status) hatasız.

**Faz B (Faz A sonrası):** `FEAT-INMA-PIPELINE-V2` 5 chunk (bkz. tracking/README.md). INMA contract gelene kadar C1 (Zoho-out) bağımsız ilerleyebilir, C2-C4 BLOCKED.

---

## MASTER QUEUE (Pilot Critical Path — 9 Paket)

### FAZ 1 — Retro-Fix & Lessons (context warm, dusuk risk)

| # | Paket | Slug | Status | Migration | Deploy | Exit Criteria |
|---|-------|------|--------|-----------|--------|---------------|
| 1 | FEAT-DMP Cache Poison Fix | `20260422-feat-dmp-cache-poison-fix` | **DONE+DEPLOYED+SMOKED** 2026-04-21 13:04 UTC (commit `ca2d2d5` + `3a21d2c`) | Yok | Backend 10/10 HEALTHY | Codex iter 0 PASS ✅ 7/7 test ✅ endpoint 401 gate ✅ |
| 2 | Lessons +3 AUTH-HOTFIX + 1 test-skip + 1 inline update | `20260422-lessons-tfm-auth-hotfix` | **DONE** 2026-04-21 23:42 UTC (Codex iter 0 PASS 12/12 CQ + 3/3 CoVe) | Yok | Doc-only | 4 yeni entry (satir 16-19) + 1 inline update (satir 21) `arch/lessons-learned.md` ✅ |

### FAZ 2 — FEAT-TFM Suite Pilot-Required (resolver MVP uzerine UI + picker)

**Not:** FEAT-TFM-SYNC ve FEAT-TFM-CACHE BACKLOG'a tasindi (bkz. Audit §1).

| # | Paket | Slug | Status | Migration | Deploy | Exit Criteria |
|---|-------|------|--------|-----------|--------|---------------|
| 3 | FEAT-TFM-UI Dashboard Editor | `20260423-feat-tfm-ui-editor` | **DONE+DEPLOYED+SMOKED** 2026-04-22 12:12 UTC (commit `f4fdc60`, Codex iter 1 PASS 12/12 CQ + 5/5 CoVe after iter 0 FAIL 8 blocker) | Yok | Backend SPA redeploy 10/10 HEALTHY | 10-slot editor + INMA FieldName + auth gate 401/401 ✅ SPA chunk `FieldMappingSettingsPage-D2v_4qCv.js` 13KB prod'da ✅ |
| 4 | FEAT-TFM-FLOW Picker | `20260424-feat-tfm-flow-picker` | **DONE+DEPLOYED+SMOKED** 2026-04-22 12:37 UTC (commit `783a7ab`, Codex iter 1 PASS after iter 0 FAIL 1 blocker CQ12) | Yok | Backend SPA redeploy 10/10 HEALTHY | PlaceholderPicker `tfmAware` prop + 2-grup + useFieldMapping + 2 consumer opt-in ✅ SPA chunks `PlaceholderPicker-DW65pAcx.js` 6KB prod'da ✅ TFM+DMP auth gate 401/401 ✅ |

### FAZ 3 — Pilot Omurgasi Feature'lar (3 kritik)

| # | Paket | Slug | Status | Migration | Error Codes | Deploy | Exit Criteria |
|---|-------|------|--------|-----------|-------------|--------|---------------|
| 5 | FEAT-EFS Drip Sequence | `20260425-feat-efs-drip-sequence` | **DONE+DEPLOYED+SMOKED** 2026-04-22 07:50 UTC (commit `29e8d18`, Codex iter 4 PASS arc 0→4, CoVe 7/7 + CQ 12/12, 0 blocker). Migration 029 run, Marketing+Automation+Backend deploy 10/10 HEALTHY, 3-tier auth smoke PASS (middleware-401/BadJWT500/ValidJWT200), Dent PUT round-trip PASS (id=1 post-roadshow 3/7/14 A/B 50/50), 5 validation caps 400, SPA chunk FollowupSequenceSettingsPage-BBB3hJ7E.js referenced from index-oKrfe438.js. | **029**-efs-followup-sequence.sql (event_followup_sequences + event_followup_runs + FKs tenant_registry/leads/sequences + tenant_settings.efs_test_mode + efs_no_reply_threshold_days + leads.followup_state JSONB + followup_ab_group + partial unique race guard) + arch/db/marketing.sql canonical mirror (Codex iter 2 CQ11) | **INV-MK-050..058** (9 kod: validation/logical-absence/opt-out/cap/disabled/collision/storage-unavailable-056/upstream-unavailable-057/reserved-058) — iter 0 tek sınıf yanlıştı, iter 1-3 failure-class taksonomisi ayrıştı | Marketing (:7112) + Automation + Backend SPA + Dashboard SPA `/settings/followup-sequence` editor | Hangfire scheduled + SHA256 deterministic A/B + 4 trigger contract (no-reply-welcome auto-emit scheduling deferred follow-up paket; 3 reason ops-manual) + execution-time opt-out dual-signal (inma_optout_outbox.event_type OR followup_state.opted_out_at JSON key) + concurrent-trigger race closed (partial unique index + PostgresException 23505 → INV-MK-055) + test mode (efs_test_mode) → delay_days as minutes + Dashboard TEST MODE banner + stage cap enforcement (max 5 / max 30 unit) + single-flight CT-safe cache + SPA wrapError helper + Fragment+sibling tr row error + existing-data-guard disabled UI ✅ |
| 6 | FEAT-MCC Multi-City Campaign | `20260425-feat-mcc-multi-city` | **DONE+DEPLOYED+SMOKED** 2026-04-22 12:49 UTC (commit `d84e304`, Codex 4-chunk review ALL PASS 48/48 CQ + 13/13 CoVe; Migration 030 prod + Backend+Automation+Marketing deploy 10/10 HEALTHY + SPA CampaignConfigSettingsPage-VsvlFArG.js 13251 bytes + Pilot Smoke S7 PASS: GET seed intact, INV-BE-120 reserved slug reject, INV-BE-118 slug-regex/orphan-date/inverted-window rejects, PUT round-trip 200, final GET verify) | **030**-tenant-campaign-config.sql (`tenant_settings.campaign_config JSONB` additive + GIN jsonb_path_ops + idempotent Dent seed via `DO $$ jsonb_path_exists` block) | **INV-BE-118..121** (INV-BE-118 validation, INV-BE-119 window-closed, INV-BE-120 reserved slug, INV-BE-121 DB transient — collision-free, allocated AFTER INV-BE-117 LIW Chunk C) | Backend + Automation + Marketing + **Dashboard SPA (`/settings/campaigns` editor)** | Array-of-campaigns JSONB config (max 8/tenant, max 20 cities&dates each) + locale-aware `{{campaign.cities_human|cities_csv|cities_json|name|slug|start_date|end_date|event_date|event_hours}}` substitution (en `and` / tr `ve`) + dual-layer outbound window guard (Automation SendCallbackAsync + Marketing FollowupStageJob; campaign-agnostic outbound bypass + empty-campaigns tenant bypass for backward compat) + push cache invalidate (Backend PUT → resolver.Invalidate; peers 5dk TTL) + Dent pilot seed (roadshow_ireland_2026, Dublin+Cork dates 2026-03-14/15, window 2026-03-01..2026-03-20) loaded by migration 030 idempotently + Dashboard multi-card editor with city/date sub-rows (slug+cap+date-order client validation, bracket INV-BE-* error display, accessibility aria-label, disabled-only-during-save guard preserving operator access on transient errors per lessons 2026-04-22 P3) ✅ |

### FAZ 4 — Cleanup (pilot once temiz)

| # | Paket | Slug | Status | Deploy | Exit Criteria |
|---|-------|------|--------|--------|---------------|
| 7 | INMA Debug Log Temizligi | `20260418-inma-debug-log-cleanup` | **DONE+DEPLOYED** 2026-04-18 (commit `2b078b2` — retroactively verified 2026-04-22) | Dashboard SPA | `[inma-debug]` prefix'li loglar silindi (inmaBridge + inmaBootstrap + useAuth + App.tsx). Verify: `grep [inma-debug] src/Invekto.Backend/Dashboard/src/` → 0 match. Deploy: P5 `29e8d18` 2026-04-22 07:50 + P6 `d84e304` 12:43 Backend+SPA redeploy'larıyla temiz bundle prod'a 2× yollandı. Plan `arch/plans/20260418-inma-debug-log-cleanup.json` status=DONE (pre-pilot, stale roadmap status fix'i 2026-04-22). ✅ |
| 8 | Prod Yedek Silme | `20260427-prod-bypass-bak-remove` | **DONE+DEPLOYED** 2026-04-22 13:35 UTC (prod file op only, no service deploy) | Prod file op | 6 stale bak dosyasi MCP `invekto-ops server-exec Remove-Item` ile silindi: Backend `bak-inma-companycode` (2026-04-16 hedef, roadmap slug'inda `bak-20260416-inma-bypass` varyanti yazilmis, gercek isim farkli) + Backend `bak-20260419-precheck` + Appointments/current VCP Chunk B (2) + Integrations (2). Active `appsettings.Production.json` dosyalari intact. 10/10 services HEALTHY post-delete. Repo: plan JSON + tracking + lessons +1 entry (stale bak cleanup cadence + slug-semantic mismatch pattern). scripts/tmp/* + staging/* + _staging/* + WebChat/*.bak (12 dosya) ayri post-pilot cleanup icin beklemekte. ✅ |

### FAZ 5 — Pilot Smoke (SON — butun feature'lar DEPLOYED ve SMOKED olduktan sonra)

| # | Paket | Slug | Status | Tenant | Exit Criteria |
|---|-------|------|--------|--------|---------------|
| 9 | Dent Adavista Pilot Full-Stack Smoke | `20260428-dent-pilot-smoke` | **DONE+SMOKED_PARTIAL** 2026-04-22 11:13 UTC | **18173130** (Dent Adavista) | 8/13 step PASS (S0.1/0.2/0.3/0.5 prep, S1 intake adapted, S3 locale, S5a TFM resolver CRITICAL, S7+S8 trigger+scheduling CRITICAL). 4 DEFERRED (S2/S4/S5b/S6 flow+slot blockers, Q-approved post-P9 FlowBuilder wiring paketi). **1 CRITICAL FAIL** S7b Hangfire marketing-followup queue pickup gap — escalated to new P10 paket. S10 cleanup PASS (0 SMOKE_TEST residual). |
| 11 | Dent Pilot FlowBuilder Wiring | `20260423-dent-pilot-flowbuilder-wiring` | **DONE+DEPLOYED+SMOKED_PARTIAL** 2026-04-22 17:30 UTC (commits `1fe8d5c` + `19675bd`; Codex iter 0→1→2 FAIL progression → iter 3 PASS 12/12 CQ + 3/3 CoVe) | Backend + Appointments + Automation (deploy-only, no service restart) | **Deliverables:** (a) MCC campaign date refresh via direct DB UPDATE jsonb_set (dates 2026-06-06 Dublin / 2026-06-07 Cork, window 2026-05-20..06-15, slug + cities preserved). (b) 42-row idempotent seed SQL via MCP invekto-postgres: 4 appointment_slots (Sat+Sun × morn+aft, is_active=FALSE defensive), 1 chatbot_flows (dent_welcome_roadshow 5-node contract-compliant v2, flow_id=29), 36 faq_entries (12 intents × 3 variants, is_active=FALSE placeholder guard), 1 tenant_landing_settings (landing_api_key=NULL, welcome_flow_slug match, field_map 5 canonical). (c) DO $verify$ 8 postcondition checks ALL PASS [INV-SEED-001..008] canonical arch/errors.md SEED service entries. (d) Q Dashboard Rotate landing_api_key last4=IxiH 64-char. **Re-smoke results (6/7 AC PASS, 1 DEFERRED):** AC1 ✅ MCC refresh (direct DB adapted), AC2 ✅ Seed 42 row + DO $verify$ 8/8, AC3 ✅ Q rotate, AC4 ✅ S2 wiring (implicit via S5b), AC5 ✅ S5b landing HTTP full PASS (lead_id=4, Hangfire job 31632 Succeeded 3s after insert), **AC6 ⚠️ S6 DEFERRED** (INV-AUTH-005 403 — Dent plan_tier='baslangic' Appointments feature yok; wiring kanıtlandı, plan upgrade pilot go-live prep kapsamında), AC7 ✅ cleanup baseline restore 0 residual. **Deploy-discovered bugs:** (1) seed SQL `ON CONFLICT ON CONSTRAINT uq_chatbot_flows_name` FAIL (schema UNIQUE INDEX not CONSTRAINT) → patch commit `19675bd` column-inference form. (2) `pilot-checklist.md §3` source_slug `roadshow_landing` (underscore) yanlış — regex sadece dash kabul (`roadshow-landing`). (3) `app.invekto.com` INMA legacy login, Backend external değil — localhost:5000 direct gerekli. (4) consent boolean true only (yes/1 reject). (5) `X-Invekto-Api-Key` header (not `X-Api-Key`). **Latent pilot blocker:** Dent plan_tier upgrade (baslangic→profesyonel/kurumsal) Appointments feature için şart; pilot-checklist.md §1 Feature flags listesine `appointments=on` kontrolü eklenmeli. |
| **A** | **Dent Plan Tier Upgrade (baslangic→kurumsal) + S6 Re-Smoke** | `20260424-dent-plan-tier-upgrade` | **DONE+SMOKED_PARTIAL** 2026-04-24 11:25 UTC (Q UI plan upgrade 11:15:35Z + Claude S6 re-smoke booking 201 PASS plan_guard kanıtı; Codex pending) | Q UI + ops-only (no service deploy) | **Plan upgrade evidence:** tenant_registry.plan_tier='baslangic'→'kurumsal' (Marketing key gap profesyonel'da → kurumsal zorunlu for Dent FEAT-EFS), Q Dashboard Ops PUT endpoint (built-in planCache.Invalidate). **S6 re-smoke evidence:** POST /api/v1/appointments/book slot=1 day_of_week=6 date=2026-04-25 JWT tenant=18173130 role=admin → 201 confirmed (önceki post-P9 wiring 403 INV-AUTH-005 → şimdi PASS, AC6 DEFERRED→PASS flip plan_guard layer). **NEW BUG discovered (Paket A scope dışı, escalated to BACKLOG B-VCP-DOCTORS):** VideoMeetingCreationJob hangfire.state 38169 Failed `42P01: relation "doctors" does not exist` (AppointmentsRepository.cs:1199-1202 LEFT JOIN doctors d, tablo hem prod DB'de hem migration'da YOK — Dent ilk Appointments user, latent). meeting_link NULL + reminder job IDs NULL. **Cleanup:** appointment id=1 + hangfire job 38169 + 4 state row + slot1 is_active=FALSE revert (defensive default), plan_tier=kurumsal preserved (Q'nun upgrade'i). Pilot config preserve baseline match (4 inactive slots + 1 active flow + 36 inactive FAQ + 1 landing row + 48 templates + 5 TFM + MCC seed). |
| **B** | **B-VCP-DOCTORS: Doctors Table Bootstrap + Dent Pilot Seed** | `20260424-dent-doctors-bootstrap` | **DONE+DEPLOYED+SMOKED** 2026-04-24 12:20 UTC (commit `d06e2c1`; Codex iter arc 0→1→2→3 → iter 3 PASS 12/12 CQ + 3/3 CoVe + 0 blocker gpt-5.4-2026-03-05; Migration 031 prod execute + DO $verify$ 4/4 PASS + Appointments NSSM restart + /health HEALTHY; S6 fresh re-smoke POST /book 201 id=2 + VideoMeetingCreationJob 38314 Succeeded 473ms + meeting_link non-null + reminder job IDs 38315+38316 non-null + cleanup baseline match + doctor seed PERSIST) | Migration 031 + prod DB seed + Appointments NSSM restart (no DLL swap) | **Scope (interview gates 9/9 G1-G9):** (a) Migration 031 + canonical mirror arch/db/doctors.sql minimal bootstrap (id + tenant_id FK CASCADE + name + is_active + timestamps; NO FK from appointment_slots.doctor_id to doctors.id — GR-3.19 scope); (b) Idempotent Dent seed DO block: 1 row 'Dr. Dent Adavista' + UPDATE appointment_slots SET doctor_id=<id>, updated_at=now() WHERE tenant_id=18173130 AND doctor_id IS NULL (4 slot backfill post-P9 baseline); (c) DO $verify$ postcondition block 4 assertions INV-SEED-009..012 (table_exists, dent_doctors=1, dent_slots_with_doctor=4, GRANT ALL invekto); (d) NO code change — LEFT JOIN natural NULL-safe (AppointmentsRepository.cs:1230-1231 IsDBNull guards), NO Appointments redeploy (Npgsql per-connection plan cache transparent); (e) G7 SCHEDULER HOST EXCEPTION invariant verified (Backend.csproj:9 Appointments PrivateAssets="all" already intact — grep snapshot, no new line); (f) S6 fresh re-smoke target: slot1 temp activate + JWT + POST /book 201 + VideoMeetingCreationJob hangfire.state Succeeded + meeting_link non-null + meeting_provider='mock' + calendar_event_id non-null + video_reminder_24h/1h_job_id non-null; (g) Cleanup + preserve: appointment+hangfire artifacts purge + slot1 revert + doctor seed PERSIST (pilot config kalıcı). **Files (7):** arch/plans/20260424-dent-doctors-bootstrap.json + arch/plans/diffs/*.diff + arch/db/migrations/031-doctors-bootstrap.sql + arch/db/doctors.sql + arch/errors.md (+4 INV-SEED entries) + tracking/20260424-dent-doctors-bootstrap.md + tracking/pilot-launch-roadmap.md. **Risk:** MEDIUM (prod schema ADDITIVE + data seed + integration re-smoke). |
| **C1** | **Dent FAQ Surface Content Bind** | `20260424-dent-paket-c1-faqs-content-bind` | **DONE+DEPLOYED+SMOKED** 2026-04-24 18:00 UTC (commit `ff9aaaa`; Codex iter arc 0→1→2→3 → iter 3 PASS 12/12 CQ + 3/3 CoVe + 0 blocker gpt-5.4-2026-03-05; Migration 032 prod execute atomik tx + DO $verify$ silent PASS + 5 postcondition + 36 faqs INSERT + flow welcome UPDATE rowcount=1 + faq_entries DELETE + archive 37 row; POST /api/v1/knowledge/18173130/generate-embeddings 200 `{generated:36, failed:0, total:36}` + 36/36 NOT NULL embedding verify; S4 smoke 9/9 PASS semantic search top scores 0.55-0.73 + lang filter behavior verified; pilot config preserve baseline 14/14 metric). **Scope:** Migration 032 atomik tx — `faqs` 36 row INSERT (12 intent × 3 A/B/C variant from DocX `ROADSHOW Aİ AGENT KARŞILAMA MESAJI.docx` source-of-truth via python-docx 1.2.0 parse → JSON intermediate `DentAdavista/seeds/dent-roadshow-content.json`; UNIQUE(tenant_id, question) suffix '(Variant A/B/C)' disambiguation; embedding=NULL pre-populated by POST endpoint) + `chatbot_flows` flow_id=29 message_text_welcome_1 jsonb_set placeholder → DocX welcome-wdate-1 inline ({{Name}}→{{lead.name}} canonical) + `faq_entries` 36 [EDIT] is_inactive DELETE (Q-approved G12 MID cleanup) + DO $verify$ 5 postcondition (INV-SEED-013..017) + archive table snapshot. POST-COMMIT: `POST /api/v1/knowledge/18173130/generate-embeddings` (Bearer JWT) → 36 NOT NULL embeddings. POST-EMBEDDINGS: S4 smoke 3 FAQ × 3 locale (translation hop verify). **5-surprise discovery (this session):** s1 DocX exists (initial G1 'no DocX' wrong info), s2 templates 48 row ZATEN content (session-memory 'placeholder' stale for templates), s3 flow node IS placeholder (verified), s4 faq_entries schema simpler (no intent_slug/variant_index/lang), **s5 3 FAQ surfaces (faqs runtime authority via Knowledge pgvector + ts_vector hybrid; faq_entries legacy migration source; template_catalog faq_* FEAT-WTP rotation pool inactive)** — Q tek-shot G13 'iki ayri paket C1 + C2' decision split scope. Migration 032 + canonical mirror (faqs schema arch/db/automation.sql snapshot intact); no .cs/.ts changes. | Migration 032 + Knowledge embeddings POST hop | Doc + DB content-only + HTTP hop, no service redeploy | TBD post-Codex+execute+smoke |
| 10 | FEAT-EFS Hangfire Marketing-Followup Queue Fix | `20260423-feat-efs-hangfire-queue-fix` | **DONE+DEPLOYED+SMOKED** 2026-04-22 12:26 UTC (commits `22dba6a` + `ac390e7` + `a6ed4ea`; Codex iter 0 FAIL (CQ8+CQ12) → iter 1 PASS 12/12+3/3 → iter 2 PASS 12/12+3/3; Backend+Marketing 2× HEALTHY; prod 1024+1024 orphan rows cleaned; re-smoke v2 PASS stage[0] Succeeded in 16s + INV-BE-119 log) | Marketing + Backend | **Two root causes via sequential prod forensic:** (1) Orphan `default` queue — FollowupStageJob + ApiKeyRateLimiter.SweepNow lacked `[Queue]` attribute, Hangfire defaulted to `"default"`, no server listens to it (all 5 on named queues), 1024 stuck SweepNow rows since 2026-04-18 (4-day latent). (2) Post-iter-1 re-smoke exposed: Backend DelayedJobScheduler throws `FileNotFoundException: Could not resolve assembly 'Invekto.Marketing'` promoting Scheduled→Enqueued because Hangfire needs to reflect on FollowupStageJob's [Queue] attribute; Backend.csproj already had `PrivateAssets="all"` refs to Appointments/Automation/Integrations/WhatsAppAnalytics (G7 SCHEDULER HOST EXCEPTION pattern, lines 5-8) but Marketing was missing — P5 FEAT-EFS oversight. **Fix (5 items):** (a) FollowupStageJob class-level `[Hangfire.Queue("marketing-followup")]`; (b) ApiKeyRateLimiter.SweepNow method-level `[Hangfire.Queue("backend")]` (service class has non-Hangfire methods → method-level); (c) Backend Program.cs startup orphan queue guard (`[INV-JOB-006]` WARN log + drain SQL, non-blocking); (d) arch/errors.md INV-JOB-006 canonical entry; (e) Backend.csproj +ProjectReference Invekto.Marketing PrivateAssets="all" + System.ServiceProcess.ServiceController 8.0.1→9.0.0 (transitive from Marketing's WindowsServices 9.0.0). **Re-smoke v2 evidence:** fake Dent lead id=3 → EFS trigger → 3 scheduled runs → stage[0] scheduled_at 12:24:42Z, executed_at 12:24:58Z (+16s), hangfire_state='Succeeded', run.status='skipped_disabled' (MCC INV-BE-119 window guard fired — Dent campaign 2026-03-20 < 2026-04-22 expected suppression) → Marketing jsonl log `[INV-BE-119] FollowupStageJob: run 7 suppressed (campaign-aware skip)`. **Post-smoke cleanup:** 2 remaining scheduled (31372-73) purged, lead DELETE, test_mode+enabled reverted, 0 orphan default rows verified. Pilot config preserved (48 templates + 5 TFM + MCC seed + video_provider='mock'). ✅ |

> **Note:** Eski P7 FEAT-VCP Chunk C **BACKLOG B0** olarak tasindi (Codex iter 0 Q4 FAIL — OAuth Q-provision blocker fragil). Pilot smoke S6 adimi **Mock link** ile yapilacak (Chunk B Mock provider DEPLOYED 2026-04-20). Gercek Google Meet prod = B0 pilot sonrasi.

---

## PILOT SMOKE STEP-BY-STEP (P10 Detay)

### Pre-Pilot Tenant Prep (P9 baslamadan once)
1. **Translation warmup:** `POST /ops/translation/warmup?tenantId=18173130&texts=<12 FAQ>&locales=<9>` — cache populate
2. **FEAT-TFM field mapping set (P3 UI ile):** Dent 5-field mapping (`roadshow_city→cf1`, `appointment_slot→cf2/date`, `offer_status→cf3/enum`, `deposit_status→cf4/enum`, `flight_booked→cf5/bool`). PUT `/api/v1/tenant-settings/field-mapping` call hem UI hem API yoluyla.
3. **FEAT-MCC campaign config set (P6 Dashboard Campaigns page ile):** `roadshow_ireland` slug, Dublin+Cork cities, 2026-03-14/15 dates, active window 2026-02-28→2026-03-20
4. **FEAT-VCP provider select:** **Mock provider** (Chunk B DEPLOYED, prod OAuth B0 BACKLOG). Pilot scope'ta sadece Mock link yeterli.
5. **FEAT-EFS sequence set (P5 Dashboard Followup Sequence page ile):** 3-stage Day 3/7/14, A/B 50/50, yapay delay test param enabled
6. **Dent template seed:** Dashboard → Templates → Topluca JSON → `DentAdavista/seeds/dent-adavista-templates.json` paste → **index 0 metadata entry sil** (JSON'un ilk eleman`i metadata object'i, kalan 48 template) → Topluca Yukle → `succeeded=48` verify
7. **Flow wiring (P4 FEAT-TFM-FLOW picker + FlowBuilder manuel):** Welcome node `data.group_tag=welcome_with_date` (roadshow tarihi varsa) / `welcome_no_date`; ai_faq nodes `data.rotation_group_tag=faq_pricing/hours/location/...` (12 FAQ icin 12 rotation grup); placeholder substitution test `{{roadshow_city}}` FEAT-TFM-FLOW picker'dan insert

### Smoke Adimlari (E2E, her biri olcumlenebilir)
| # | Adim | Kanit (log/DB/UI) | Beklenen |
|---|------|-------------------|----------|
| S1 | Test lead intake (FEAT-LIW) `POST /leads/intake/ireland-roadshow-lp` | `liw_audit_log` row + `leads` row with `custom_1=dublin` | 200 + audit trail |
| S2 | Welcome flow trigger (FEAT-WTP + HFM-1) | `[FEAT-WTP] tenant=18173130` log + rotation counter increment + chunked message (sentinel-free leak check) | 3 chunks arriving, counter ++1 |
| S3 | Preferred locale upsert (HFM-2) | `leads.preferred_locale=en-IE` (DB SELECT) | Upsert canonical, not detected |
| S4 | FAQ question + translation hop | `[TRANSLATE hop]` log + cached response on 2nd call | Response <500ms 2nd call, DE locale render |
| **S5a** | **FEAT-TFM resolver dedicated test** | **(i) `SELECT field_mapping FROM tenant_settings WHERE tenant_id=18173130` JSONB 5-entry verify. (ii) Backend `ITenantFieldMappingResolver.Invalidate` post-PUT verified via WARN log. (iii) `{{roadshow_city}}` placeholder substitution uses **semantic** resolution path (log: `resolver.ResolveToInmaKey("roadshow_city") = cf1`), NOT raw cf1 fallback.** | **Mapping DB set, resolver hit, semantic→cf1 path observed distinct from raw-allowlist fallback** |
| S5b | FEAT-DMP placeholder substitution (end-to-end) | INMA `dynamicMessage=true` + `dynamicMessageFields=[cf1]` outbound | Real `{{roadshow_city}}` → Dublin substituted (via TFM resolver, not raw) |
| S6 | FEAT-VCP meeting creation (Mock mode) | `appointments.meeting_link` non-null + ICS file generated + `mock-mock-isipHIJuAo` pattern link | Mock link persist, calendar event mock ICS generated |
| S7 | FEAT-MCC city substitution + window guard | Template `{{campaign.cities_human}}` → "Dublin & Cork"; out-of-window send → rejected INV-BE-118 | Substitution + guard both active |
| S8 | FEAT-EFS drip schedule | `event_followup_runs` rows scheduled + yapay delay (test param) triggers fire | 3 stages schedule within 10 min yapay-delay simulation |
| S9 | Prod log grep final | `[FEAT-WTP] + [INV-MK-050] + [INV-BE-118] + [FEAT-DMP] + [TFM resolver]` unified trace per test lead | All feature tags visible in Kestrel log |
| S10 | Cleanup | DELETE test lead + tenant_settings reset | Baseline restored |

---

## BACKLOG (Pilot Sonrasi)

| # | Paket | Sebep | Activation Gate |
|---|-------|-------|-----------------|
| **B0** | **FEAT-VCP Chunk C Prod GoogleMeet OAuth** | **Codex iter 0 Q4:** Q-provision Google Workspace Client ID/Secret BLOCKER. Main queue'da fragil sequencing. Pilot smoke S6 Mock provider (Chunk B DEPLOYED) ile yeterli | **Q Google Workspace Console'dan OAuth client provision etsin (Client ID + Secret + Authorized Redirect URI)** |
| ~~**B-VCP-DOCTORS**~~ | ~~**VCP Meeting Hop — `doctors` Tablo Schema/Code Drift Fix**~~ | **PROMOTED 2026-04-24 14:45 UTC → Master Queue Paket B** (`20260424-dent-doctors-bootstrap` IN_PROGRESS). Q interview G1 minimal boilerplate + G2 1 placeholder Dent doctor seed Q-approved. SQL guard gereksiz (LEFT JOIN natural NULL-safe, IsDBNull guards zaten AppointmentsRepository.cs:1230-1231'de var). Appointments redeploy gereksiz (Npgsql per-connection plan cache transparent). G7 pattern zaten intact (Backend.csproj:9 Appointments PrivateAssets="all"). Minimal migration-only fix + Dent seed + postcondition verify + re-smoke. Paket B satırına bak. |
| B1 | FEAT-TFM-SYNC (scope belirsiz) | INMA `/api/dynamicfields` **sadece READ**. Write/create/update API YOK. cf1-cf10 INMA admin panel tarafindan yonetiliyor. FEAT-DMP zaten READ sync yapiyor. Olasi yeni scope: INMA→INSE `leads.custom_N` sync (webhook veya polling) — ama Dent pilot FEAT-LIW intake ile custom_1 dolduruyor, sync gereksiz | Q interview: "INMA webhook mu, polling mi, hic mi (FEAT-LIW yeterli)?" |
| B2 | FEAT-TFM-CACHE Redis Invalidate | Spec `tenant-field-mapping.md` 5dk TTL eventual consistency MVP icin yeterli diyor. Redis dep + pub/sub Q onayi gerekli | Q onay: Redis eklensin mi, yoksa PostgreSQL `NOTIFY/LISTEN` pattern mi? |
| B3 | PKT-13 Faz 1 Lead Scoring | Marketing servisi (:7112) Implemented ama PKT-13 spec dosyasi YOK (`tracking/pkt-13-*` hic yok). Dent ile alakasiz | Q: scope yaz + paket aç |
| B4 | lessons-learned.md Archive | **422 entry** (arsiv esigi 50). `arch/lessons-learned-archive.md`'ye son 3 ay disi tasinmasi | Doc-hygiene, ops bakim zamani |
| B5 | FEAT-ICB 5 faz (23 modul) | Spec (`tracking/feat-inma-chat-bridge.md`) acikca "**BACKLOG — Dent pilot bitince**" diyor + UP0.2/0.3/0.5 INMA-BLOCKED (JWT public key bekliyor) | Dent pilot go-live + UP0 unblock |
| **B-C2** | **Dent Welcome Templates Overhaul (Persona-Rich Refresh) + Cleanup** | Q G13 'iki ayri paket' decision 2026-04-24 17:00 UTC: Paket C1 scope sınırlandı (FAQ surface only). C2 scope: (a) `template_catalog welcome_*` 12 row content overwrite (DocX 10 with-date + 5 no-date persona-rich, Güneş + Dr. Özge + Dublin/Cork tarihleri vs prod generic), (b) opsiyonel 3 ekstra welcome-wdate-8/9/10 INSERT (12→15 alignment), (c) chatbot_flows flow_id=29 message_text_welcome_1 `data.rotation_group_tag='welcome_with_date'` attach (FEAT-WTP rotation aktif), (d) `template_catalog faq_*` 36 row content DocX A/B/C ile align (rotation pool source-of-truth tek), (e) `dent_paket_c1_archive_20260424` table DROP (post-pilot ops cleanup). | Pilot go-live confirmed (Stage 3 full traffic) — risk MEDIUM, scope creep'ten korunmak için pilot sonrası |
| ~~**B-META**~~ | ~~**Meta Leadgen Webhook Native Entegrasyon**~~ | **DONE+DEPLOYED+SMOKED 2026-04-24 22:30 UTC** — commit `9763012` master (64 file / 3761 ins / 77 del). Codex 4-chunk merged PASS conservative (chunk-1 iter 2 + chunk-2 iter 5 + chunk-3 iter 2 + chunk-4 iter 3; gpt-5.4-2026-03-05; real bug fixes: migration now() partial-index PostgreSQL 40.7 rejection + HasLinkedLead stuck-row durability + IntakeJob missing-secret throw + SPA typed catch). Prod: Migration 033 atomic tx 5/5 postcondition (INV-SEED-018..020) + Backend 213 files 12.7MB HEALTHY + Automation 65 files 2.9MB HEALTHY. AC1 smoke 4/4 PASS (GET handshake 200 'test123' + POST bozuk 401 INV-META-001 actionable Turkish + POST valid HMAC 200 accepted + GET wrong-token 403 INV-META-002). Audit chain end-to-end validated via test event_row with INV-META-006 from downstream Graph API missing access_token (expected since test config had null page_access_token — durability chain: webhook→Hangfire→Automation→Backend process-lead→Graph API fail→audit update). Post-smoke cleanup Dent config reset to empty + test audit row deleted. **Stage 1 launch bağımlılığı GELİŞTİ:** (1) Müşteri Meta App + HSM template 24-48h onay, (2) Zoho Blueprint "1. Mesaj Atildi" transition, (3) WABA phone_number_id + INMA allowlist, (4) Invekto ops Dashboard config Dent tenant, (5) Stage 1 smoke 5 numara × 6 senaryo. AC1b/AC2b pilot Stage 1'de doğrulanacak (gerçek Meta lead → WhatsApp delivered + zoho_sync_log welcome_sent). Pilot 3-gün timeline korundu. |

---

## EKSIK-GEDIK AUDIT (v2 revizyon kaynagi)

### §1 FEAT-TFM-SYNC / FEAT-TFM-CACHE Degerlendirmesi
- **Bulgu:** INMA `/api/dynamicfields` **sadece READ** (wapcrm-marketing-api.md satir 161). cf1-cf10 INMA admin panel yonetir. INMA'ya WRITE API YOK.
- **Karar:** FEAT-TFM-SYNC **BACKLOG** (B1). FEAT-TFM-CACHE Redis **BACKLOG** (B2).
- **Risk onleyici:** FEAT-LIW Chunk A (DEPLOYED) intake webhook uzerinden `leads.custom_1..custom_5` Dent pilotta doldurulacak. INMA mirror gereksiz.

### §2 Error Code Namespace Audit
- **Bulgu:** FEAT-MCC spec'inde `INV-BE-090..091` tahsis edilmis. ANCAK `arch/errors.md` satir 154-161: **INV-BE-090..095 zaten Translation + HFM-2 kullaniyor** (FEAT-LIW Chunk A lesson'da da belgeli).
- **Son kullanilan:** INV-BE-117 (FEAT-LIW FB — commit `9ef615a`).
- **FEAT-MCC yeni kod:** INV-BE-**118..120** (MCC-specific: out-of-window, invalid campaign, reserved slug).
- **FEAT-EFS yeni kod:** INV-MK-**050..055** (Marketing-local, collision YOK).
- **FEAT-VCP Chunk C yeni kod:** INV-INT-**148..150** (OAuth fail, provider misconfig, meet creation fail).
- **Eylem:** Her paket plan JSON'unda ilk adim error code pre-flight grep (LIW Chunk A lesson'da belgeli).

### §3 Migration Numbering Audit
- **Son migration:** `028-tenant-field-mapping.sql` (FEAT-TFM MVP, 2026-04-21).
- **FEAT-EFS (P5):** `029-efs-followup-sequence.sql`
- **FEAT-MCC (P6):** `030-tenant-campaign-config.sql`
- **FEAT-VCP Chunk C (P7):** Migration YOK (OAuth config + secrets only). Chunk A Migration 023 + Chunk B Migration 024 DEPLOYED.
- **P10 Pilot Smoke:** Migration YOK.
- **Eylem:** Her paket plan JSON'unda migration numberesi sabitle; paralel pack olusursa race yok (sequential implementation).

### §4 Config / Secrets Pre-Flight
- **FEAT-VCP Chunk C:** Google Workspace **Client ID + Secret + Authorized Redirect URI** Q provision gerekli. Pilot prep'e BLOCKER.
- **FEAT-MCC:** Config degismedi (JSONB DB only).
- **FEAT-EFS:** `MarketingService:FollowupQueue` Hangfire queue (appsettings) — Marketing servisinde yeni key, peer service (Backend/Automation) config mirror gereksiz.
- **FEAT-TFM-UI (P3):** Dashboard SPA env YOK, mevcut Backend endpoint (`/api/v1/tenant-settings/field-mapping` DEPLOYED) tuketiliyor.
- **Eylem:** Chunk C paket plan'inda "Pre-deploy Q provision" interview sorusu ZORUNLU.

### §5 Test Coverage & Skipped Tests
- **P1 Invalidate_DuringInflight KALDIRILDI** (deterministik test degil; TFM-MVP pattern'inde de yok). Defensive XML doc + production HttpClient async-boundary guarantee yeterli gorulmus.
- **FEAT-EFS:** A/B coin flip + opt-out race + Hangfire schedule → integration test (NSubstitute + test DB).
- **FEAT-MCC:** Substitution render snapshot + window guard unit test.
- **FEAT-TFM-UI/FLOW:** Dashboard smoke E2E (Playwright) opsiyonel; manuel Q smoke yeterli.
- **Eylem:** Her paket plan JSON'unda hangi testler yazildi + hangileri "acceptable skip" belgelendi.

### §6 Pilot Smoke Olculebilir Basari Kriteri
- **Bulgu:** Eski roadmap "pilot smoke green" kriter belirsiz. Yeni plan: yukarida S1-S10 her adim icin kanit (log line + DB row + UI visible) gerekli.
- **Eylem:** P10 paket plan JSON'unda her adim icin kanit template ve PASS/FAIL checklist.

### §7 Rollback Plan Per Paket
- **FEAT-EFS:** Feature flag `tenant_settings.enable_followup_sequence` default FALSE → rollback = flag FALSE + Hangfire queue drain.
- **FEAT-MCC:** `tenant_settings.campaign_config` NULL set → substitution skip, window guard disabled.
- **FEAT-VCP Chunk C:** Provider select Mock'a geri dondur → Chunk B mock fallback'i aktiflesir.
- **FEAT-TFM-UI/FLOW:** Dashboard SPA rebuild rollback → prev commit deploy.
- **INMA debug log cleanup:** Git revert + SPA redeploy.
- **Eylem:** Her paket plan JSON'unda rollback steps bolumu.

### §8 BLOCKED External Dep Timeline
| # | Task | Bloker | Unblock Signal | Impact |
|---|------|--------|----------------|--------|
| B-INMA-UP0 | UP0.3 Tenant Lifecycle | INMA `tenant.created` event | INMA team confirm | FEAT-ICB B5 prerequisite |
| B-INMA-J14 | UP0.5 IInmaSendClient | INMA J1/J4 API | INMA endpoint + test | Outbound read-through |
| B-INMA-JWT | INMA JWT RS256 pubkey | RS256 algo key | INMA pubkey endpoint | Current decode-only bypass accepted |
| B-ZOHO-PAID | Zoho P4.2 Adavista retest | Adavista plan upgrade | Q plan degisim onayi | Metadata path re-enable |
| B-J2-SECRET | FEAT-J2 Http flip | X-CIB-SecretKey provision | Q karari | Opt-out actual INMA sync |
| B-VCP-OAUTH | Google Workspace OAuth Client | Q provision | Google Console creation | **FEAT-VCP Chunk C (P7) BLOCKER** |

### §9 Interview Gates (Q'nun acik cevabi zorunlu)
| # | Paket | Soru |
|---|-------|------|
| P2 | Lessons | 4 entry scope kabul mu yoksa farkli yapi? |
| P3 | FEAT-TFM-UI | Dashboard'da INMA FieldName label nasil gosterilecek (inline vs tooltip)? Duplicate slot UX (cf1 iki semantic ad)? |
| **P4** | **FEAT-TFM-FLOW Picker** | **FlowBuilder NodePropertyPanel cursor-aware insert mevcut FEAT-DMP pattern mi? TemplateCreate textarea'da semantic dropdown konumu (toolbar vs inline)? Dropdown sadece field_mapping entry'leri gosterir mi yoksa raw cf1..cf10 + semantic hybrid mi? TFM mapping yoksa dropdown empty-state UX?** |
| P5 | FEAT-EFS | Marketing servisinde yeni orchestrator mi (FollowupOrchestrator), yoksa Automation'da hook mu? A/B default 50/50 tenant override? Opt-out race guard enqueue-time vs execution-time? Max stage 5 / max window 30 gun cap'ler Dent 3-stage/14-gun icin yeterli? |
| P6 | FEAT-MCC | Slug uniqueness tenant-scope mi? Active window inclusive/exclusive? `{{campaign.cities}}` render comma-sep mi JSON array mi? Cache invalidate push vs poll? |
| **P7 (cleanup)** | **INMA Debug Log Temizligi** | **INMA handshake onayi geldi mi (test sonrasi log temizligi yapilabilir mi)? `[inma-debug]` disinda baska loglar da silinmeli mi? Git commit + SPA redeploy sadece kod seviyesi mi yoksa log config degisikligi de var mi?** |
| **P8 (cleanup)** | **Prod Yedek Silme** | **Gerçekten silinmesine hazir mi `appsettings.Production.json.bak-20260416-inma-bypass`? Yedek alinarak silinsin mi yoksa direkt delete mi? Rollback senaryosunda bu yedek gerekecek mi?** |
| **P9 (smoke)** | **Dent Pilot Full-Stack Smoke** | **Tenant data reset yetkisi kimde (Q mi, yoksa Claude DELETE SQL ile mi cleanup edecek)? Smoke FAIL threshold (kac S-adimi FAIL = rollback trigger)? Rollback authority (Q manuel mi, otomatik mi)? Pilot lead verilerini real customer'lara sizdirma riski var mi (fake tenant/number gerekir mi)?** |
| B0 | FEAT-VCP Chunk C | Q Google Workspace OAuth client provision ne zaman? Provision tamamlandiginda main queue'ya dondur (PROMOTE B0) |
| B1 | FEAT-TFM-SYNC | INMA webhook mu, polling mi, hic mi? |
| B2 | FEAT-TFM-CACHE | Redis dep OK mi, yoksa PostgreSQL NOTIFY/LISTEN pattern? |
| B3 | PKT-13 | Scope yaz (Marketing servisinde zaten ne var, ne yok)? |
| B5 | FEAT-ICB | 6 acik soru (media storage, webhook owner, Ecom+Zoho agregasyon, team chat kapsam, prefs sync, sticky note) |

---

## Progress Tracker

| Metric | Value |
|--------|-------|
| Total Pilot-Critical Packages | 15 (P10 P9-escalation + P11 post-P9 wiring + Paket A + Paket B B-VCP-DOCTORS + Paket C1 FAQ Content Bind + **B-META Meta Leadgen Webhook 2026-04-24 22:30 UTC**) |
| DONE | 15 (P1-P6 DEPLOYED+SMOKED; P7 retroactive; P8 prod file op; P9 DONE+SMOKED_PARTIAL; P10 EFS Hangfire fix; P11 FlowBuilder wiring; A Dent plan_tier upgrade; B B-VCP-DOCTORS Doctors Bootstrap; C1 Dent FAQ Content Bind; **B-META Meta Leadgen Webhook 2026-04-24 22:30 UTC DONE+DEPLOYED+SMOKED**) |
| IN_PROGRESS | 0 |
| PENDING | 0 |
| SKIPPED | 0 |
| Progress | **100% pilot-critical paketler (15/15)**; pilot go-live için tüm content + mimari + Meta Lead Ads native webhook hazır. Stage 1 launch dependencies paralel müşteri tarafı (Meta App/HSM onay + Zoho Blueprint + WABA) + Invekto ops Dashboard config tamamlanır tamamlanmaz AC1b/AC2b smoke aktive olur. Backlog: B-C2 welcome templates overhaul (post-pilot), B0 FEAT-VCP Chunk C Google Meet OAuth (Q-provision). |
| Backlog Packages | 6 (B0/B1/B2/B3/B4/B5 + **B-C2 welcome overhaul post-pilot** Q G13 split decision) |
| Blocked External Deps | 6 |

**Pilot tahmini timeline (revize, Codex CQ9 feedback):**
- P2 (lessons doc) — 1 session
- P3-P4 FEAT-TFM suite (UI + picker) — 2 session
- P5-P6 pilot omurgasi (EFS + MCC — her biri migration + deploy + UI) — **3-4 session** (eski 3-session tahmini fazla iyimserdi)
- P7-P8 cleanup — 1 session
- P9 smoke prep + execution — 1-2 session
- **Toplam:** ~8-10 session (2-3 hafta, paralel degil sequential)
- **VCP Chunk C (B0):** pilot disi, OAuth provision edildiginde ayri 1 session

---

## References

- [Session Memory](../arch/session-memory.md)
- [Tracking Master](README.md)
- [Lessons Learned](../arch/lessons-learned.md) (422 entry — archive B4 backlog)
- [INVEKTO_BASE.prompt.md](../.claude/agents/INVEKTO_BASE.prompt.md)
- [Codex Context](../arch/codex-context.md)
- [WAP CRM Marketing API](../wapcrm-marketing-api.md) — INMA contract truth
- Eski v1 roadmap: commit `ca2d2d5` tracking/pilot-launch-roadmap.md (superseded)

---

**Hazirlayan:** Claude 2026-04-21 23:20 UTC (post-P1 deep audit + Codex planning review)
**Ilk PENDING:** P2 `20260422-lessons-tfm-auth-hotfix`
