# InvektoServices - Phase Tracking

> Multi-tenant SaaS mikro servis platformu. .NET 8, PostgreSQL, React 18.
> 12 Paket Stratejisi (v5.2). Her paket: interview + plan + dev + build + Codex review.

## 🚀 PILOT LAUNCH MODE AKTIF (2026-04-21)

**Execution queue otoritesi:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md)
9 paket arka arkaya → Dent Adavista pilot full-stack smoke. Session bootstrap zorunlu okuma.
**Pilot Launch Mode 10/10 paket DONE (100%)** — P10 DONE+DEPLOYED+SMOKED 2026-04-22 12:26 UTC (commits `22dba6a` + `ac390e7` + `a6ed4ea`; Codex iter 0 FAIL → iter 1/2 PASS; end-to-end re-smoke verified stage[0] Succeeded in 16s + INV-BE-119 log). **Siradaki milestone:** Dent pilot go-live için **Post-P9 FlowBuilder wiring paketi** (chatbot_flows + appointment_slots + tenant_landing_settings seed — S2/S4/S5b/S6 flow-blocked smoke step'lerin açılması için). **Backlog:** B0 FEAT-VCP Chunk C Google Meet OAuth (Q-provision).

---

## Master Tracking

| # | Paket | Ad | GR | Durum | Tarih | Codex |
|---|-------|----|----|-------|-------|-------|
| 0 | Phase 0 | Pre-Paket (WA-1~6, GR-2.1 A+B, Flow Builder) | 6 WA + 2 GR | DONE | 14-15 Sub | - |
| 1 | PKT-1 | AI Upgrade | GR-2.2, GR-2.3 | DONE | 15 Sub | iter 3, FORCE PASS |
| 2 | PKT-2 | Saglik Core | GR-2.4, GR-2.6 | DONE | 16 Sub | iter 1, FORCE PASS |
| 3 | PKT-3 | Ops Dashboard | GR-2.5, WA-4 | DONE | 16 Sub | iter 1, FORCE PASS |
| 4 | PKT-4 | WA Analytics | WA-6 | DONE | 16 Sub | iter 7 |
| 5A | PKT-5A | Platform Infra | GR-3.4, 3.6, 3.15, 3.26, 3.29 | DONE | 17 Sub | iter 2, FORCE PASS |
| 5B | PKT-5B | Platform UI+Adv | GR-3.14, 3.18, 3.19 | DONE | 17 Sub | iter 4, FORCE PASS |
| 6A | PKT-6A | Niche Foundation | GR-3.1, 3.2, 3.5, 3.9, 3.10, 3.12, 3.23 | DONE | 17 Sub | iter 1 |
| 6B | PKT-6B | Niche Business | GR-3.7, 3.8, 3.11, 3.13, 3.3, 3.16, 3.17 | DONE | 17 Sub | iter 2, FORCE PASS |
| 6C1 | PKT-6C1 | Health Automation | GR-3.20, 3.41, 3.43 | DONE | 17 Sub | iter 7, FORCE PASS |
| 6C2 | PKT-6C2 | Niche Marketing | GR-3.21, 3.22 | DONE | 17 Sub | iter 3 |
| 6C3 | PKT-6C3 | Marketing v2 | GR-3.24, 3.25 | DONE | 18 Sub | iter 2, FORCE PASS |
| 7 | PKT-7 | Visual AI | GR-3C.1~3C.8 | PLANNED | - | - |
| 8 | PKT-8 | Face AI | GR-3D.1~3D.5 | PLANNED | - | - |
| 9 | PKT-9 | Guzellik Salonu | GR-3E.1~3E.8 | PLANNED | - | - |
| 10 | PKT-10 | Egitim | GR-3F.1~3F.8 | PLANNED | - | - |
| 11 | PKT-11 | Voice Message AI | GR-3G.x | **Faz 1 DONE** | 27 Mar | iter 1, FORCE PASS |
| 12 | PKT-12 | Review Rescue AI | GR-3H.x | **Faz 1-4 DONE** | 26-27 Mar | iter 1, FORCE PASS |
| 13 | PKT-13 | Multilingual Medical Tourism | GR-3I.x | PENDING | - | - |
| RI-1 | Faz 1 | RI: Model Secimi & Kalibrasyon | RI-0.x, RI-1.x | DONE | 24 Sub | GATE-1 PASS (tiered 0.8203) |
| RI-2 | Faz 2 | RI: Sektor Pipeline (Top 3) | RI-2.1~2.9 | DONE | 26 Sub | GATE-2 FULL PASS |
| RI-3 | Faz 3 | RI: 7 Insight Engine | RI-3.1~3.7 (28 sub-task) | DONE | 1 Mar | P1-P5 + P6(3.5+3.7): iter 1 FP |
| RI-4 | Faz 4 | RI: Sektor Sablon Mining | RI-4.1~4.6 (21 sub-task) | DONE | 1 Mar | iter 1 FP |
| RI-5 | Faz 5 | RI: Bulk Isleme + Kalan Sektorler | RI-5.5~5.13 | DONE | 1 Mar | iter 0 FP |
| RI-6 | Faz 6 | RI: Dashboard + API + Widget'lar | RI-6.1~6.28 | DONE | 1 Mar | P1: iter 0 FP, P2: iter 1 FP |
| RI-7 | Faz 7 | RI: Tenant Onboarding Deneyimi | RI-7.1~7.7 | DONE | 1 Mar | iter 0 FP |
| RI-8 | Faz 8 | RI: Optimizasyon & Olcekleme | RI-8.1~8.13 | DONE | 1 Mar | pending |

| FAZ1-1 | Faz 1 Pkt 1 | Plan Permission System | plan_definitions, TenantPlanCache, FeatureGuardMiddleware | DONE | 2 Mar | PASS |
| FAZ1-2 | Faz 1 Pkt 2 | SuperAdmin API + Quota | Plan CRUD, Tenant Plan, Cache Invalidation, Quota Enforcement | DONE | 3 Mar | iter 3, FORCE PASS |
| IKAS-1 | ikas E-Com | ikas E-Commerce Integration | IEcommerceProvider, IkasProvider, 6 endpoints, action_ecommerce node, DB migration | DONE | 3 Mar | iter 4, FORCE PASS |
| FM-1a | Flow Monitor Faz 1a | Flow Versioning Backend | [Detay](fm-1a-flow-versioning.md) | DONE | 5 Mar | iter 1, PASS |
| FM-1b | Flow Monitor Faz 1b | Monitor Sayfasi | [Detay](fm-1b-flow-monitor-page.md) | PLANNED | - | - |
| FM-1c | Flow Monitor Faz 1c | Monitor AI Chat | [Detay](fm-1c-monitor-ai-chat.md) | PLANNED | - | - |
| G7 | Hangfire Migration | Scheduler → Hangfire (PG storage, queue-per-service, strangler) | [Detay](g7-hangfire.md) | Faz 1 IN_PROGRESS | 13 Nis | - |
| ZOHO-3C | Zoho Adim 3 P3-C | Super-admin cross-tenant ops dashboard (/api/ops/zoho/* + UI) | 4 endpoint + SPA /ops/zoho | DONE | 17 Nis | iter 3, PASS |
| ZOHO-4 | Zoho Adim 4 | Stage Mapping editor (module transitions + dry-run test + connection gate) | 2 Int + 3 Backend + editable UI | DONE | 17 Nis | iter 1, PASS |
| ZOHO-P4.2 | Zoho P4.2 | OAUTH_SCOPE_MISMATCH investigation + UX fix + metadata path removal | [Detay](zoho-p42-oauth-scope-investigation.md) | INVESTIGATED | 17 Nis | - |
| FEAT-WTP | Welcome Template Pack | Tenant N welcome + M FAQ intent, deterministik A/B rotation, locale-aware | [Spec](../arch/features/welcome-template-pack.md) | DRAFT | 16 Nis | - |
| FEAT-MCC | Multi-City Campaign | Migration 030 (`tenant_settings.campaign_config` JSONB + GIN `jsonb_path_ops` + idempotent Dent seed `roadshow_ireland_2026`) + Shared.Contracts.Campaigns (4 DTO) + ITenantCampaignResolver (5dk MemoryCache + CT-safe single-flight + locale-aware cities_human en-and/tr-ve + lead-aware ResolveDateField) + TenantCampaignConfigValidator (slug regex/reserved/max 8 campaigns/max 20 cities&dates/start≤end/dates.city ref integrity) + Backend GET/PUT `/api/v1/tenant-settings/campaign-config` (cross-tenant 403 + resolver Invalidate push) + Automation SendCallbackAsync CampaignTemplateApplier (`\{\{campaign\.([a-z_]+)\}\}` + window guard between KVKK and DMP) + Marketing FollowupStageJob window guard + AddMemoryCache + Dashboard SPA `/settings/campaigns` multi-card editor. 4 INV-BE-118..121 codes (validation/window-closed/reserved/DB-transient). | [Spec](../arch/features/multi-city-campaign.md) / [Plan](../arch/plans/20260425-feat-mcc-multi-city.json) | **DONE+DEPLOYED+SMOKED** 22 Nis 12:49 UTC (commit `d84e304`, Migration 030 run, Backend+Automation+Marketing deploy 10/10 HEALTHY, Pilot Smoke S7 PASS: validator 4x reject + PUT round-trip + final GET verify) | 22 Nis | Codex 4-chunk PASS: C1 iter 2 / C2 iter 0 / C3 iter 1 / C4 iter 0 (48/48 CQ + 13/13 CoVe total) |
| FEAT-LIW | Lead Intake Webhook | Chunk A: Generic POST /leads/intake + field map + dup merge. Chunk B: TriggerWelcomeFlowJob real dispatch + /api/internal/leads/intake/wa-direct + AutomationOrchestrator hook. Chunk C: Dashboard UI (standalone /settings/lead-intake + 6 endpoint + liw_audit_log + cross-service HTTP hop). FB: TenantExistsAsync pre-check follow-up (INV-BE-117, 3 path Rotate+Revoke+UpdateFieldMap). | [Spec](../arch/features/lead-intake-webhook.md) | A+B+C+FB DONE+DEPLOYED 19 Nis 23:18 UTC | 19 Nis | A iter 4 PASS / B iter 3 → Q FORCE PASS / C iter 3 PASS / FB iter 0 PASS |
| FEAT-VCP | Video Consultation Provider | Chunk A: Shared contracts (IVideoConsultProvider + 3 DTO) + IcsBuilder RFC 5545 + GoogleMeetMockProvider deterministic SHA256 + VideoProviderFactory + Migration 023 tenant_settings. Chunk B: Appointments DI + Hangfire reminders + appointments ALTER (pending). Chunk C: Prod GoogleMeet OAuth + Dashboard UI (pending). | [Spec](../arch/features/video-consultation-provider.md) | A DONE+DEPLOYED 19 Nis 00:17 UTC | 19 Nis | A iter 3 → Q FORCE PASS |
| FEAT-EFS | Event Follow-Up Sequence | Migration 029 (2 tables + FKs + partial unique race guard) + Shared.Contracts.Followup (5 DTOs) + Marketing orchestrator (Hangfire queue=marketing-followup + single-flight CT-safe cache + SHA256 deterministic A/B + 4 endpoints + StageJob execution-time dual-signal opt-out guard) + Automation MarketingFollowupClient + NoReplyCheckJob + Backend 3 SPA-facing proxy endpoints + jwtRequiredPrefixes + Dashboard SPA editor /settings/followup-sequence (TEST MODE banner + threshold hint + cap-enforced validator). 9 INV-MK-050..058 codes (validation/logical-absence/storage/upstream/reserved classes). | [Spec](../arch/features/event-followup-sequence.md) / [Plan](../arch/plans/20260425-feat-efs-drip-sequence.json) / [Detay](feat-efs-drip-sequence.md) | **DONE (code, deploy bekliyor)** 21 Nis 18:50 UTC — 5 iter, arc 0→4 | 21 Nis | iter 4 PASS (CoVe 7/7 + CQ 12/12, 0 blocker) |
| FEAT-PROJELER | cxapi Send Engine + Projeler | INSE bulk WA → doğrudan cxapi /chatoperation taşıma (4-PR send engine) + TONIVA mode4 benzeri "Projeler" yönetim katmanı (PKT-14, 4 slice). **cxapi 4-PR motoru KOD TARAFI TAMAM: PR-1/2/3a/3b/PR-4 ✅ DONE** (prod INERT, default-OFF). **PR-4 ✅ DONE 2026-06-10:** HSM approved-template send — migration 062 (bulk_send_jobs cxapi snapshot kolonları, CQ11 borcu kapandı) + SendTemplateAsync (language YOK, wire pinli) + proje run HSM zinciri (allowlist gate vendor-öncesi [preview+confirm] + canlı katalog hard-fail 081-084 + resolved-param snapshot + skipped audit + confirm ikinci doğrulama + HSM mesaj üretimi + send loop dalı); kapsam sadece proje run'ları; INV-OB-081..085. Projeler PKT-14: S1-S4 + SS-A/B/C/D + aha pack ✅ DONE+DEPLOYED (migration 055-061 canlı). ⛔ LIVE-ENABLEMENT GATE P0-3 aktif — **kalan tek adım GATE-OPEN (Q kararı)**. | [Roadmap](feat-projeler-cxapi-roadmap.md) / [PR-4 Plan](../arch/plans/20260610-cxapi-pr4-approved-template.json) | S1-S4: 7 Haz · SS-A/B/C: 9 Haz · SS-D: 9 Haz · cxapi: 6-10 Haz (**PR-4: 10 Haz**) | S1 iter0 · S2 iter4 · S3 iter1 · S4 iter0 · SS-A iter1 · SS-B iter0 · SS-C iter3 · SS-D iter1+Q FP CQ9 · PR-3a Q FP · PR-3b iter0/iter2 · PR-1 iter3 · PR-2 iter0 · **PR-4 5-chunk: 2/4/5 iter0 PASS, 3 iter2 PASS, 1 Q FP (tek CQ12 pre-existing)** |
| FEAT-TFM | Tenant Field Mapping | MVP: tenant_settings.field_mapping JSONB + DbTenantFieldMappingResolver (5dk cache + single-flight + cancellation poison fix) + Validator (reserved guard `InmaDynamicFieldKeys.Allowlist` ∪ leads core columns + INV-BE-096..099 + INV-BE-110 + INV-AUTH-010) + Backend GET/PUT /api/v1/tenant-settings/field-mapping (cross-tenant 403 + OCE typed catch + `{data:{...}}` envelope; global JWT middleware) + DI swap 3 servis (Null→Db). UI editor (FEAT-TFM-UI) + INMA mirror sync (FEAT-TFM-SYNC) + flow builder picker (FEAT-TFM-FLOW) + cross-instance cache (FEAT-TFM-CACHE) → sonraki paketler. | [Spec](../arch/features/tenant-field-mapping.md) / [Plan MVP](../arch/plans/20260421-feat-tfm-resolver-mvp.json) / [Detay](feat-tfm-mvp.md) | MVP DONE+DEPLOYED+SMOKED 21 Nis 01:15 UTC (commits 9d32538 + e28c202 + hot fix 4fa1550, Migration 028 + 3 servis + 10/10 HEALTHY + 4-layer smoke PASS) | 21 Nis | iter 4 PASS (12/12 CQ + 4/4 CoVe) + hot fix RequireAuthorization removal |
| FEAT-DMP | Dynamic Message Placeholder | INMA chatoperation DynamicMessage entegrasyonu (`{{name}}`, `{{cf1}}`) + `/api/dynamicfields` client + cache + FlowBuilder/TemplateCreate UI picker + FEAT-TFM NullResolver forward-compat hook | [Spec](../arch/features/dynamic-message-placeholder.md) / [Plan](../arch/plans/20260420-feat-dmp-inma-dynamic-message.json) | DONE+DEPLOYED 20 Nis 22:47 UTC (Migration 027 + Backend+Outbound+Automation + 10/10 health + 5/5 smoke) | 21 Nis | iter 5, PASS |
| FEAT-J2 | J2 Opt-Out INMA Sync | INMA POST /api/optout+/optin outbox sync + chatoperation MessageCategory (marketing/transactional) + bridge 906/907 response parsing + outbound status='blocked' + enforce_message_category feature flag (default FALSE) | [Plan](../arch/plans/20260417-j2-opt-out-inse-sync.json) | DONE | 20 Nis | iter 2 → Q FORCE PASS (CoVe 7/7 PASS, CQ design-judgment nuance) |
| DENT-PILOT | Dent Adavista Pilot | Generic feature'lari tuketen ilk tenant konfigurasyonu (46 template, 5 field, 3-stage nurture) | [Detay](../DentAdavista/plan/README.md) | BLOCKED (UP0 + FEAT-*) | 16 Nis | - |
| FEAT-DBBK | Daily DB Backup | Hangfire recurring pg_dump -Fc -> C:\Invekto\Backups + 14-day retention + 4 yeni INV-JOB kodu | [Detay](feat-dbbk-daily-backup.md) | DONE | 18 Nis | iter 4, PASS |
| FEAT-ICB | INMA Chat Bridge | 23 backend modul (conv enrichment + media + reactions + flow runtime + templates + customer 360 + reminders + reports) INMA chat-v3 UI icin; SignalR event yayini + OpenAPI contract | [Detay](feat-inma-chat-bridge.md) | BACKLOG (Dent sonrasi) | 20 Nis | - |
| FEAT-PHOTO | Foto Isteme Akisi (Dent pilot) | Migration 034 (`leads.photo_status/photo_received_at/photo_count` + `photo_inbound_idempotency` UNIQUE(lead_id, sha256) + 3 INV-SEED postcondition pg_class-scoped) + Shared.Contracts.Photos (PhotoStatus enum + FromDbValue out-recognized + PhotoRequestEvent DTO + IsTerminalForPhotoRequest race guard) + Backend PhotoInboundHandler (uc katmanli idempotency: tenant guard + UNIQUE INSERT + atomic UPDATE WHERE photo_status<>'rejected') + 2 endpoint extension (InmaInboundMediaEndpoint INV-AT-081..084 + PhotoEndpoints cross-tenant 403 + INV-AT-085 rejected lock) + Automation PhotoRequestService (INMA chatoperation tek-kopru text-only, parameterized SQL ANY(@allowed_states), PhotoSendOutcome race-terminal sinyali, 4 inline pilot template) + 3 Hangfire job (Dispatch q=photo-request-dispatch retry=2 + race-abort, Reminder/Escalation q=photo-request-reminders retry=0, terminal-skip pattern = FollowupStageJob:35-62 precedent, typed catches) + Dashboard SPA api/photos.ts (PhotoApiError 409 fix) + PhotoTab.tsx + LeadDetailPage shell + DentAdavista/seeds/photo-request-templates.json (4 template). 13 INV-AT-073..085 codes + INV-SEED-021..023. **Deploy blockers:** Program.cs DI/Map wiring (1-line per service) + slot booking dispatch trigger + dental-angles.png static asset. | [Plan](../arch/plans/20260427-feat-photo-request-flow.json) / [Detay](20260427-feat-photo-request-flow.md) | **DONE+DEPLOYED+VERIFIED** 28 Nis 10:30 UTC (~7dk deploy) — commit `1da0da6` master, Migration 034 + 5/5 schema invariant + Backend/Automation HEALTHY + 10/10 service smoke. Q FORCE PASS iter 4. | 27-28 Nis | iter 0→4: 4/8 chunk full CQ PASS, Plan chunk full Q PASS, kalan FAIL'ler TOOL_LIMITATION + ARCHITECTURE_CONFLICT |
| DENT-DOC-DRIFT | ~~dent-golive.html plan-kod kontrat hizalama~~ | **CANCELLED 2026-04-29** — ~~FEAT-PIPELINE supersedes~~ FEAT-INMA-PIPELINE-V2 supersede zinciri (2026-05-12). Pipeline 3-way sync mimarisi iptal, INMA-otorite/Zoho-out yeni model. Doc fix'ler V2 plan refresh'inde değerlendirilir. | superseded | CANCELLED | 29 Nis | - |
| FEAT-META-FULL-INTAKE | Meta Leadgen full field intake (email + custom_1..5 + consent) | MetaLeadgenEndpoints.process-lead handler canonical map sonrası sadece phone+name persist ediyordu (email/custom/consent kayboluyordu). Çözüm: in-process DI `LeadIntakeService.IntakeMetaLeadgenAsync` (WaDirect superset CTE + email INSERT/UPDATE-COALESCE + intake_metadata.resolved.custom_1..5 + consent gate INV-BE-105) + canonical 'consent_marketing'→'consent' drift fix + Dashboard CANONICAL_OPTIONS update. AC3 regression guard: WaDirect path UNTOUCHED. 8 dosya / 718 ins / 15 del. | [Detay](feat-meta-full-intake.md) / [Plan](../arch/plans/20260429-feat-meta-full-intake.json) | DONE+DEPLOYED+SMOKED 6 May 09:43 UTC (commit `a4509a9` code + Backend redeploy 6670b3b chain) | 6 May | iter 0 PASS 12/12 CQ + 4/4 CoVe (CODEX UTANSIN ✅) |
| ~~FEAT-PIPELINE~~ | ~~INMA-driven Lead Pipeline + 3-way Sync~~ | **CANCELLED 2026-05-12** — supersede edildi **FEAT-INMA-PIPELINE-V2** tarafından. Q karari (2026-05-12): Zoho INSE'den TAMAMEN çıkıyor. INMA otorite + tek yön. Eski 3-way loop prevention/source markaj/idempotency hash karmaşası artık gereksiz. | superseded | CANCELLED | 12 May | - |
| FEAT-INMA-PIPELINE-V2 | INMA-Authoritative customer_status (Zoho-out + flow channel + flow action) | Q karari 2026-05-12: Zoho Blueprint INSE'den TAMAMEN çıkıyor. INMA otorite. **5 chunk:** **C1 Zoho-out DONE+DEPLOYED+SMOKED 2026-05-12 17:50 UTC (commit `0c0733b`)** — Migration 048 atomik tx snapshot-then-drop 11 tenant data preserved; 41 dosya delete + 7 servis cleanup; Q FORCE PASS (Codex MCP infra issue); 10/10 service /health. **C5 dashboard cleanup DONE+EXECUTED+VERIFIED 2026-05-12 22:00 UTC** — Migration 049 atomik tx (LIKE + INSERT pattern, NULL-safe COALESCE + IS DISTINCT FROM TRUE verifier + explicit BEGIN/COMMIT + tenant_id IS NULL guard) prod execute 6/6 V2 marker + 6 archive row PASS; DentAdavista/plan 4 doc Zoho 91 mention DELETE + INMA-otorite Customer Status Akışı (V2) section + 8 durum tablosu INMA dropdown; arch/lessons-learned banner (purely Zoho-bound vs evrensel pattern); arch/errors.md INV-SEED-039..045; memory zoho_blueprint_api_quirks deprecated_historical; arch/db/kanban-board.sql executable archive DDL. Codex iter 0→4: Q FORCE PASS (11/12 CQ + 3/4 CoVe substantively PASS, tek kalan CQ11/Q1 pedantic CTAS schema drift). C2 inbound consumer + C3 flow trigger + C4 flow action **UNBLOCKED 2026-06-10 ✅ — CONTRACT FINAL v2.0:** [`arch/contracts/inma-customer-status-webhook.json`](../arch/contracts/inma-customer-status-webhook.json) (INMA gerçek şeması) + INMA cevabı [`arch/reports/20260610-inma-customer-status-webhook-cevap.md`](../arch/reports/20260610-inma-customer-status-webhook-cevap.md). Event `customer.selection_changed` ZATEN CANLI (ayrı sistem-olayları altyapısı, HMAC-SHA256 imzalı, outbox+retry); INMA aynı gün phone/originRequestId-echo/text-modu ekledi; 5050 aboneliği KURULU (MSSQL kanıtlı). C4+dropdown = INMA mevcut customer-feature-groups API'si (PDF §6 + ClientRequestID). ⚠️ C2 zaman-duyarlı: handler `type` dalı yokken eventler 400 (INMA'da kalıcı fail). | [Plan C1](../arch/plans/20260513-feat-inma-pipeline-v2-c1-zoho-out.json) + [Plan C5](../arch/plans/20260513-feat-inma-pipeline-v2-c5-dashboard-cleanup.json) / [Tracking C1](feat-inma-pipeline-v2-c1.md) + [Tracking C5](feat-inma-pipeline-v2-c5.md) / [Memory](../../../Users/taner/.claude/projects/c--CRMs-InvektoServices/memory/project_inma_pipeline_v2_decision.md) | C1+C5 DONE; C2-C4 BLOCKED | 12 May | C1+C5 Q FORCE PASS |
| FEAT-META-CAPI | Meta Conversions API (server-side event tracking) | Reklam veren Invekto musterileri ($10k/ay spend) icin server-side donusum bildirimi. Lead/Schedule/Purchase/CompleteRegistration eventleri Marketing servisi (:7112) Hangfire `meta-capi-dispatch` queue ile Meta'ya gonderilir. EMQ icin hashed PII (email/phone SHA256) + FBC/FBP cookie + IP/UA + external_id. Browser Pixel ile event_id deduplication. AES-GCM token at-rest + INMA bypass (donusum Invekto'da olusur, INMA inbox'ta degil). 5 chunk: Shared+Mock / ProdClient+Hangfire / Migration+Backend hooks / Dashboard `/settings/meta-capi` editor / pilot smoke. ROI: %10-15 reklam efficiency = $1-1.5k/ay tasarruf. **Bagimliliklar:** Q Pixel/Dataset provision (BM Events Manager) + System User Token (`ads_management`+`business_management`) + App Review (Pixel verify hafif yol). | [Spec](../arch/features/meta-conversions-api.md) / [Detay](feat-meta-capi.md) | DRAFT (PENDING Q provision) | 29 Nis | - |
| FEAT-META-ADS-INSIGHTS | Meta Ads performans raporu (read-only Dashboard widget) | Marketing API `ads_read` permission ile reklam performans metrikleri (spend/impressions/clicks/CTR/CPM/CPC + lead_count). Marketing servisinde async report API (run_id + poll) + 1 saat cache + Hangfire daily refresh. Dashboard `/reports/ads-insights` widget + date picker + campaign breakdown + lead matching JOIN (FEAT-META-FULL-INTAKE'in `intake_metadata.campaign_id` field'i ile). 5 chunk: Shared+Mock / ProdClient+Hangfire / Migration+Backend / Dashboard / App Review+rollout. **Bundle onerisi:** FEAT-META-CAPI ile tek App Review submission (`ads_management`+`ads_read` cift permission, tek video). | [Spec](../arch/features/meta-ads-insights.md) / [Detay](feat-meta-ads-insights.md) | DRAFT (CAPI bundle onerisi) | 29 Nis | - |
| FEAT-META-MARKETING-API | Meta Marketing API campaign create/manage (BACKLOG — uzak) | Reklam kampanya olusturma/butce yonetimi/raporlama Invekto Dashboard icinden. **GATE:** `ads_management_standard_access` Meta App Review barı **$10k toplam spend** ile gecmez (Meta "anlamli volume" + tutarli use-case ister). Once FEAT-META-CAPI + FEAT-META-ADS-INSIGHTS production'da olmali (track record gosterimi). **Activation gate:** spend tabani $50k+/ay veya tier'i degistirici musteri talebi. | bagimsiz spec yazimi backlog | BACKLOG | 29 Nis | - |
| FEAT-OBI | Outbound Bulk WhatsApp External Source (INMA + Zoho öncelik) | Outbound `/broadcast/send` zaten DEPLOYED (max 1000 recipient + opt-out filter + status tracking). Eksik: external source ingestion. **Faz 1 (MVP):** INMA `/api/contacts` adapter + Zoho COQL query adapter + Bulk Send Orchestrator (idempotency hash 24h) + Dashboard `/outbound/bulk-send` page (source picker + segment builder + preview + schedule). **Faz 2:** CSV upload + saved segments + recurring (Hangfire). **Bağımlılık:** Outbound DEPLOYED ✅ + INMA contact API kontrol gerekli + Zoho COQL ✅ + opt-out registry ✅. **Aktivasyon gate:** Pilot Stage 3 sonrası, 2. müşteri onboarding pre-req. | [Detay](feat-obi-bulk-external.md) | BACKLOG | 29 Nis | - |
| FEAT-MEDIPOL-WA | ~~Medipol — Doktor Detay WhatsApp Yönlendirme + KVKK + Slot~~ | **CANCELLED 2026-05-06** — Müşteri konfirmasyonu: site Drupal'de + form doğrudan INMA'ya + agent dispatch INMA'da + ortak 1 numara + doktor verisi gerek yok. Yeni akış: `Drupal → INMA → agent`, Invekto kod yok. Faz 2-3 (slot view/book) da iptal. Q netleştirme: INMA/INSE ayrı müşteri ürünü değil; Drupal-INMA-only vakada INSE hop yok. Migration 043 zaten başka pakete (`043-feat-pilot-flags-vcp-phase2-kanban`) gitti. Plan JSON yazılmamıştı. | [Detay](feat-medipol-wa.md) / [Idea](ideas/medipol-whatsapp-redirect.md) | CANCELLED | 6 May | - |
| FEAT-CLINIC-METADATA | Multi-Tenant Hardcoded Cleanup (clinic_contact + team_members) | Pilot için yapılan tüm özellikler tüm diş/sağlık kliniklerinin ortak ihtiyacı. 3 hardcoded Dent-specific veri (klinik iletişim bilgileri, persona isimleri, roadshow tarihleri+şehirler) placeholder-driven yapıldı. Migration 040 tenant_settings tablosuna `clinic_contact JSONB` + `team_members JSONB` ekler + Dent seed (7 contact alan + 2 team üye) + faqs 36 row targeted REPLACE + chatbot_flows flow_id=29 welcome jsonb_set + INV-SEED-032..034 fail-loud postcondition. ClinicTemplateApplier (CampaignTemplateApplier precedent — `{{clinic.X}}` + `{{team.role.field}}` substitution) AutomationOrchestrator pipeline'ına additive (FEAT-DMP UNTOUCHED). Backend GET/PUT `/api/v1/tenant-settings/clinic-metadata` (3-katmanlı cross-tenant guard, INV-BE-122..125 validation) + Dashboard `/settings/clinic-metadata` editor (5-input contact form + team CRUD drag handle + dil dropdown). DentAdavista/seeds/clinic-metadata-dent.json source-of-truth + dent-roadshow-content.json 16 lokasyon refactor. **Pilot blocker DEĞİL** — Dent zaten hardcoded değerlerle çalışır; ancak 2. müşteri öncesi MUTLAKA tamamlanmalı. | [Detay](feat-clinic-metadata.md) / [Plan](../arch/plans/20260428-feat-clinic-metadata.json) | DONE+DEPLOYED 29 Nis 17:25 UTC | 29 Nis | iter 0 FAIL → iter 1 PASS (12/12 CQ + 5/5 CoVe) |
| FEAT-VFB | Voice Flow Builder (WA voice note + WA Calling inbound + Toniva PBX → FlowEngineV2 sub-saniye barge-in) | Yeni mikroservis `Invekto.VoiceRuntime` (:7115) + 5 yeni flow node (`voice_trigger`/`voice_say`/`voice_collect`/`voice_transfer`/`channel_condition`) + `IVoiceCallProvider` 4 impl (Microphone F0 / TonivaPbx F2 / WhatsAppCall F3 / Mock) + OpenAI Realtime API kritik path + paralel GPT-4o-mini intent classifier + Smart-Turn v2 ONNX (F2) + KVKK consent + DTMF "9" opt-out + `call_sessions`/`voice_turns`/`callback_requests` (Migration 050 F2 VoiceRuntime sahip) + Dashboard `/calls` liste+detay+playback + `/settings/voice-runtime` + `/reports/voice-perf` + RI Insight Engine 8. modul + TTS tenant-selectable (Realtime/Azure Neural TR/ElevenLabs clone). **Q customer prospect motivasyonu (2026-05-23):** Spec APPROVED, F0 microphone-based PoC `arch/plans/20260523-feat-vfb-f0-poc.json` (Q live demo + sales-ready). **5 faz:** F0 PoC (5-7 gun microphone) → F1 WA Voice Note (1 hafta) → F2 PBX Live MVP (3-4 hafta) → F3 WA Calling Multi-tenant (2 hafta) → F4 Optim+Analytics (1 hafta). **External onkosul:** OpenAI Realtime tier (Q 1-2 saat) + Toniva PJSIP hook F1 sonu basla + Meta WA Calling allowlist F3-4 hafta once + ElevenLabs/Azure key F2'de. INV-VR-001..010 F0 (errors.md DONE), INV-VR-011..050 reserved. Backlog **B-VFB-SELFHOST** (HF Faster-Whisper+XTTS-v2, gate 15+ premium tenant). | [Spec](../arch/specs/voice-flow-builder.md) / [Detay](feat-vfb-voice-flow-builder.md) / [F0 Plan](../arch/plans/20260523-feat-vfb-f0-poc.json) / [Smoke Senaryolar](../arch/reports/feat-vfb-f0-smoke-scenarios.md) | **F0 DONE+COMMITTED+PROD-DEPLOYED** — kod 24 May commit `b78e8126` master (35 files / +4137 / -2; Codex 3 iter Q FORCE PASS); GA migration 25 May commit `50feb24f` (Beta→GA + Türkçe lock-in + light theme); **prod deploy 25 May 18:03 UTC+3** (NSSM `InvektoVoiceRuntime` :7115 Running, /health=ok, /ready=degraded missing OPENAI_API_KEY — Q manuel set + reverse proxy `voice.invekto.com` pending). AD-20 override edildi: F0 artık sadece Q laptop'unda DEĞİL, prod'a deployed. | 24 May | Q FORCE PASS iter 2 |
| FEAT-WC-INMA-CHANNEL | WebChat as INMA Channel (Phase 1: Invekto Gateway + Phase 2: INMA UI migration) | WebChat artik INMA'nin yeni kanal tipi (WABA/IG simetri). Tenant kendi sitesine widget kodu gomer, mesajlasma INMA operator UI'inde yapilir. Invekto.WebChat → Invekto.WebChat.Gateway rename, sadece browser-INMA koprusu (thin gateway + ephemeral cache + widget config). **Visitor identity:** cookie UUID + external_id `wc-{widget}-{visitor}` snake_case (full UUID 76 char). SameSite=None+Secure HTTPS prod + Lax fallback dev/staging. **Cutover:** 7-day dual-write Outbox Pattern + Hangfire 5dk reconciliation cron (1,2,5,15,60 dk exponential backoff = 83 dk fail-safe window, retry_count <= 5 max 6 attempt). Bridge fix DB CHECK constraint `chk_bridge_gate` enforce. **Q kararlari:** history kaybi kabul + eski Dashboard sil (T+8), cookie clear=yeni visitor recovery yok, 24h idempotency Hangfire saatlik cleanup, AI fallback Invekto'dan kaldir (INMA AI). **Onkosul:** PKT-BRIDGE-906-907-FIX (callback bridge 906/907 parse bug fix) — webchat cutover oncesi tamamlanmali. **INMA'ya teslim:** spec + 2 contract dosyasi (3 endpoint geliştirilecek: inbound/history/widget_sync + callback channel field handling). **10-iter Codex review:** v1 FAIL 13 issue (mimari) → v3 FAIL 0 mimari + 10 format → v10 FAIL 3 issue (1 false positive INMA SQL pre-existing pattern + 1 widget_id UUID/TEXT type drift dev fix + 1 INMA-side open question). Q karari: spec ACCEPTED v10, kod review ayri PASS hedefi. **7 artifact:** spec (453 satir 20 AC + 13 mimari karar) + webchat-inma-channel.json contract + integration-callback.json extend (channel + visitor_key + sender_type + sender_name OPTIONAL conditional schema) + Migration 047 (3 yeni tablo: cutover_state + idempotency + outbox + 2 CHECK constraint idempotent DO blocks + GRANT ALL) + arch/db/webchat.sql DDL sync + arch/errors.md INV-WC-013..025 (13 yeni kod) + tracking dosyasi. **Phase 1 PENDING INMA endpoint** (henuz kod yok). **Phase 2:** INMA UI widget management migration (post-Phase-1, ayni public API). | [Spec](../arch/specs/webchat-inma-channel.md) / [Detay](20260510-feat-webchat-inma-channel.md) / [Plan: PENDING INMA] | SPEC-ACCEPTED v10 (2026-05-10), Phase 1 PENDING | 10 May | iter 1→10 (mimari v3'te netlesti, format/tutarlilik v10'a kadar islendi, residual 3 issue dev'de) |

**Toplam:** 54 paket (28 done, 0 in-progress, 5 planned, 3 pending, 12 feature DRAFT/SPEC, 1 pilot blocked, 0 active customer, 3 backlog, 2 cancelled) | 50+ GR + RI-100+ task

## Mikroservis Port Haritasi

| Servis | Port | Durum | Paket |
|--------|------|-------|-------|
| Backend | 5000 | Active | Stage-0 |
| ChatAnalysis | 7101 | Active | Stage-0 |
| Appointments | 7102 | Implemented | PKT-2 |
| Knowledge | 7104 | Implemented | Phase 0 (GR-2.1) |
| AgentAI | 7105 | Implemented | Phase 0 (GR-1.2) |
| Integrations | 7106 | Implemented | PKT-5A |
| Outbound | 7107 | Implemented | Phase 0 (GR-1.3) |
| Automation | 7108 | Implemented | Phase 0 (GR-1.1) |
| WhatsAppAnalytics | 7109 | Implemented | Phase 0 (WA-5/6) |
| FaceAnalysis | 7110 | Planned | PKT-8 |
| VisualSearch | 7111 | Planned | PKT-7 |
| Marketing | 7112 | Implemented | PKT-6C2 |
| WebChat | 7113 | Implemented | WebChat |
| VoiceAI | 7114 | Implemented | PKT-11 |
| VoiceRuntime | 7115 (HTTP) + 8443 (HTTPS) | **Deployed (F0) PUBLIC** | FEAT-VFB F0 PoC — `https://voice.invekto.com:8443` 2026-05-25 |

## Strateji Gecmisi

| Versiyon | Tarih | Degisiklik |
|----------|-------|------------|
| v5.0 | 2026-02-15 | Tekli GR dongusu -> 10 paket. %60 overhead azalma |
| v5.1 | 2026-02-15 | PKT-6 (19 GR) -> PKT-6A/6B/6C. Codex PASS olasiligi artirmak icin |
| v5.2 | 2026-02-17 | PKT-5 -> PKT-5A/5B. PKT-9/10 eklendi (Phase 3E/F). Toplam: 12 paket |
| v5.3 | 2026-03-04 | PKT-11/12/13 eklendi. Idea lifecycle. active-work kaldirildi |

## Bagimlilik Zinciri

```
Phase 0 (Stage-0 + GR-1.x + WA + Knowledge + Flow Builder)
  |
  +-- PKT-1~4 (Phase 2 tamamlama)
  |
  +-- PKT-5A/5B (Phase 3A platform)
  |    |
  |    +-- PKT-6A (bagimsiz: Intent + Onboarding + Voice AI)
  |    +-- PKT-6B (Integrations bagli: Outbound + Iade + Lead + Yorum)
  |         |
  |         +-- PKT-6C1 (Health Automation - Appointments bagli)
  |         +-- PKT-6C2 (Marketing servisi)
  |         +-- PKT-6C3 (Marketing v2)
  |    |
  |    +-- PKT-7 (Visual AI - yeni servis :7111)
  |    +-- PKT-8 (Face AI - yeni servis :7110)
  |    +-- PKT-9 (Guzellik - config layer, PKT-6 altyapisi)
  |    +-- PKT-10 (Egitim - config layer, PKT-6 altyapisi)
  |    +-- PKT-11 (Voice Message AI - Whisper STT + intent)
  |    +-- PKT-12 (Review Rescue AI - ChatAnalysis + Automation)
  |    +-- PKT-13 (Multilingual Medical Tourism - Health + multilingual)
  |
  +-- REVENUE INTELLIGENCE / SATIS ZEKASI (ANA ODAK — 100+ task)
       |
       +-- RI-1: Model Secimi & Kalibrasyon (DEVAM EDIYOR)
       |    |
       |    +-- RI-2: Sektor Pipeline (Top 3: Saglik, Moda, Gayrimenkul)
       |         |
       |         +-- RI-3: 7 Insight Engine (28 sub-task)
       |         |    (Response Time, Demand, Agent, Revenue, Objection, Rescue, Quality)
       |         |
       |         +-- RI-4: Sektor Sablon Mining (21 sub-task)
       |              (Intent, FAQ, Flow, Objection Handling, Follow-up, Onboarding)
       |              |
       |              +-- RI-5: Bulk Isleme (63M msg) + Kalan 9 Sektor
       |                   |
       |                   +-- RI-6: Dashboard + API + Widget'lar (28 endpoint)
       |                   |
       |                   +-- RI-7: Tenant Onboarding Deneyimi
       |                        (Sektor paketi, checklist, benchmark karsilastirma)
       |                        |
       |                        +-- RI-8: Optimizasyon + FlowBuilder + Marketing + Outbound
```

## Ertelenen

| GR | Aciklama | Neden |
|----|----------|-------|
| GR-3.44 | Guardrail Alert Escalation | GR-3.31 Guardrail framework'e bagli |

## Dosya Yapisi

Her paket icin ayri dosya: `tracking/pkt-XX-slug.md`
Plan JSON'lari: `arch/plans/YYYYMMDD-slug.json` (Codex audit trail)
