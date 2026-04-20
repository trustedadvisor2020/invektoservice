# InvektoServices - Phase Tracking

> Multi-tenant SaaS mikro servis platformu. .NET 8, PostgreSQL, React 18.
> 12 Paket Stratejisi (v5.2). Her paket: interview + plan + dev + build + Codex review.

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
| FEAT-MCC | Multi-City Campaign | Tenant campaign JSONB (cities, dates, locations) + flow substitution + active window guard | [Spec](../arch/features/multi-city-campaign.md) | DRAFT | 16 Nis | - |
| FEAT-LIW | Lead Intake Webhook | Chunk A: Generic POST /leads/intake + field map + dup merge. Chunk B: TriggerWelcomeFlowJob real dispatch + /api/internal/leads/intake/wa-direct + AutomationOrchestrator hook. Chunk C: Dashboard UI (standalone /settings/lead-intake + 6 endpoint + liw_audit_log + cross-service HTTP hop). FB: TenantExistsAsync pre-check follow-up (INV-BE-117, 3 path Rotate+Revoke+UpdateFieldMap). | [Spec](../arch/features/lead-intake-webhook.md) | A+B+C+FB DONE+DEPLOYED 19 Nis 23:18 UTC | 19 Nis | A iter 4 PASS / B iter 3 → Q FORCE PASS / C iter 3 PASS / FB iter 0 PASS |
| FEAT-VCP | Video Consultation Provider | Chunk A: Shared contracts (IVideoConsultProvider + 3 DTO) + IcsBuilder RFC 5545 + GoogleMeetMockProvider deterministic SHA256 + VideoProviderFactory + Migration 023 tenant_settings. Chunk B: Appointments DI + Hangfire reminders + appointments ALTER (pending). Chunk C: Prod GoogleMeet OAuth + Dashboard UI (pending). | [Spec](../arch/features/video-consultation-provider.md) | A DONE+DEPLOYED 19 Nis 00:17 UTC | 19 Nis | A iter 3 → Q FORCE PASS |
| FEAT-EFS | Event Follow-Up Sequence | N-stage drip sequence + Hangfire scheduled + A/B control + 4 trigger | [Spec](../arch/features/event-followup-sequence.md) | DRAFT | 16 Nis | - |
| FEAT-TFM | Tenant Field Mapping | INMA 10-field semantic overlay (JSONB + resolver + validation + flow/template integration) | [Spec](../arch/features/tenant-field-mapping.md) | DRAFT | 16 Nis | - |
| FEAT-DMP | Dynamic Message Placeholder | INMA chatoperation DynamicMessage entegrasyonu (`{{name}}`, `{{cf1}}`) + `/api/dynamicfields` client + cache + FlowBuilder/TemplateCreate UI picker + FEAT-TFM NullResolver forward-compat hook | [Spec](../arch/features/dynamic-message-placeholder.md) / [Plan](../arch/plans/20260420-feat-dmp-inma-dynamic-message.json) | DONE (Codex iter 5 PASS 12/12 CQ + 6/6 CoVe) | 21 Nis | iter 5, PASS |
| FEAT-J2 | J2 Opt-Out INMA Sync | INMA POST /api/optout+/optin outbox sync + chatoperation MessageCategory (marketing/transactional) + bridge 906/907 response parsing + outbound status='blocked' + enforce_message_category feature flag (default FALSE) | [Plan](../arch/plans/20260417-j2-opt-out-inse-sync.json) | DONE | 20 Nis | iter 2 → Q FORCE PASS (CoVe 7/7 PASS, CQ design-judgment nuance) |
| DENT-PILOT | Dent Adavista Pilot | Generic feature'lari tuketen ilk tenant konfigurasyonu (46 template, 5 field, 3-stage nurture) | [Detay](../DentAdavista/plan/README.md) | BLOCKED (UP0 + FEAT-*) | 16 Nis | - |
| FEAT-DBBK | Daily DB Backup | Hangfire recurring pg_dump -Fc -> C:\Invekto\Backups + 14-day retention + 4 yeni INV-JOB kodu | [Detay](feat-dbbk-daily-backup.md) | DONE | 18 Nis | iter 4, PASS |
| FEAT-ICB | INMA Chat Bridge | 23 backend modul (conv enrichment + media + reactions + flow runtime + templates + customer 360 + reminders + reports) INMA chat-v3 UI icin; SignalR event yayini + OpenAPI contract | [Detay](feat-inma-chat-bridge.md) | BACKLOG (Dent sonrasi) | 20 Nis | - |

**Toplam:** 44 paket (27 done, 0 in-progress, 5 planned, 2 pending, 7 feature DRAFT, 1 pilot blocked, 1 backlog) | 50+ GR + RI-100+ task

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
