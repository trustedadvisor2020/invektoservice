# Session Memory — Current State

> **BOUNDED FILE.** OVERWRITE edilir (append değil), ≤300 satır. Session init `limit=320` ile okur; sonundaki `END_CURRENT_STATE` işareti o pencerede görünmüyorsa DUR → `/optimize-memory`. Eski tam log: [session-memory-archive.md](session-memory-archive.md) (SADECE Grep). Kalıcı kararlar: [docs/decisions.md](docs/decisions.md). Standing kurallar: [hot-lessons.md](hot-lessons.md).
> **Pilot Mode:** queue+tracking otoritesi `tracking/pilot-launch-roadmap.md`'de; bu dosya son durum detayı.

_Güncel: 2026-06-15 — **FEAT-INMA-PIPELINE-V2 C4 'Set Customer Status' = DONE+DEPLOYED+SMOKED.** Flow ACTION node `action_set_customer_status` cxapi `/customer-feature-groups/update` ile lead'in INMA durumunu geri yazar (C2/C3 okuma döngüsüne yazma ekler). Commit `20e8a1c5` (master, push'lu). **Mimari (Q interview):** Backend-proxy (Automation handler → service-JWT+X-Internal-Service-Token → Backend internal endpoint → Shared `WapCrmFeatureGroupUpdateClient` → cxapi; C3b secret çözümü + egress reuse) · kimlik customerId-varsa+phone-fallback · single+multi (text disabled, vendor /update text-write yok). **Codex iter0 FAIL(provider-code sınıflama+scope) → iter1 FAIL(taksonomi) → iter2 PASS** (CQ1-12+CoVe 4/4); codex_consult pre-impl critique uygulandı (multi-select full-list-replace footgun → açık UI uyarısı; stable ClientRequestID). Loop-kill: C3a fail-closed actor guard + `invekto-` echo + cxapi value-idempotency (yeni suppress tablosu YOK). Errors INV-AT-089/090 + INV-BE-140..144. **No migration.** Deploy: Backend+Automation `server-deploy` (Shared additive→diğer 9 inert), config-restore'lu, **10/10 HEALTHY**; endpoint no-auth→401; SPA node deployed flow chunk'larda. **CANLI ama inert** (node'u kullanan flow yok). Kalan e2e: Q/INMA + Medipol `webhook_secret`._

_Önceki: **REFACTOR AUDIT CONSOLIDATION (2026-06-15):** 18 audit branch → master (`33583aae`, push'lu); full-platform deploy 10 servis + VoiceAI HEPSİ HEALTHY; `ExportRepository.cs` `int?` modeli 0-sentinel hotfix `3f540ab6`'yı supersede etti; AgentAI SSH-fail manuel recover. Plan: `tracking/audit-consolidation-plan.md`. Audit girişi: `arch/reports/20260614-audit-INDEX.md` (176 bulgu, 8C/34H). Detay → session-memory-archive.md (grep)._

## Recently Completed (max 10)

- **feat-inma-pipeline-v2-c4-set-customer-status** (2026-06-15) — Flow ACTION node cxapi write-back; Backend-proxy; commit `20e8a1c5`; Codex iter2 PASS; Backend+Automation deploy 10/10 HEALTHY, endpoint 401-gated, inert (node-flow yok). Kalan e2e: Q/INMA + Medipol webhook_secret.
- **refactor-audit-consolidation-deploy** (2026-06-15) — 18 branch merge→master + full-platform deploy (10 servis), final all-HEALTHY. AgentAI SSH-fail manuel recover.
- **outbound-template_id-null-hotfix** (2026-06-15) — Export path firing-crash; 0-sentinel deploy, sonra consolidation `int?` ile supersede.
- **refactor-audit-consolidation** (2026-06-14) — 18 branch → integration branch, conflict-free, build 0 error, gate GREEN.
- **webchat-section7** (`7d895d47`) — merged+deployed (INV-WC-026).
- **backend-flowbuilder-login** (`30fd9361`) — merged+deployed, MUST-ASK auth gate.

## Audit kalanı (henüz dokunulmamış — opsiyonel sonraki iş)

- **Automation:** Auto-2 SSRF (MUST-ASK), Auto-11/17 (behavior-change real-bug), Auto-8/22 (TOCTOU).
- **Knowledge:** K-3 (Sector="eticaret" davranış+migration), K-8 (UpdateAsync tx/TOCTOU), K-11/15 (dedup), K-16 (scenario→faq).
- **WAA:** WAA-8 (ON CONFLICT latent), WAA-15 (DI dup). **Integrations:** Int-4 full pagination (canlı ikas PaginationInput sonrası). **Backend:** kalan low/med. **Marketing-1.**
- Her batch = master'dan yeni `work/` branch → auto+Codex PASS → commit'te DUR → auth/Shared değişikliğinde MUST-ASK. Giriş: iki raporun §4 Quick Wins + §7.

## Platform & Gate

**Platform:** 11 .NET 8 mikroservis, tek shared PostgreSQL 16 + pgvector. Prod = services.invekto.com, `C:\Invekto\{Service}\current\`, NSSM. Portlar: Backend 5000, ChatAnalysis 7101, Appointments 7102, Knowledge 7104, AgentAI 7105, Integrations 7106, **Outbound 7107**, Automation 7108, WhatsAppAnalytics 7109, Marketing 7112, **VoiceAI 7114 (ayrı NSSM `InvektoVoiceAI`, server-deploy enum'da YOK → manuel zip-upload-extract)**. FaceAnalysis 7110 + VisualSearch 7111 = Planned. VoiceRuntime 7115 = ayrı NSSM.

**Fail-closed gate (deployed 2026-06-15):** Her servis TEK key gate'ler — ChatAnalysis=`Microservice:InternalApiKey` (466EEEDB), WAA=`Benchmark:OpsKey` (EEB34235). Outbound↔Backend internal çağrı `InternalServices:SharedSecret` (04A96F3D, iki uçta EŞİT). Config eksikse servis startup'ta throw = DOWN → deploy öncesi CANLI doğrula.

**P0-3 Gate (2026-06-13):** `BulkSend.AllowAllTenants=true` (TÜM tenantlar) + `Projects=[5050, 15702882, 100000001]` + `CxapiSend=[5050, 100000001]` (5050 sandbox + Medipol-wappflex). Medipol ack webhook OTOMATİK (`CxapiWebhookReconcileJob` ENABLED/AllowAll). Codex = gpt-5.5, LOW dahil her risk review.

**Pilot tenant gerçeği:** WapCRM-konfigli canlı tenant'lar = 5050 (vendor sandbox) + Medipol (100000001 wappflex, instance 8784 WABA). cxapi send + delivery-ack zinciri uçtan uca canlı. Medipol = İKİ tenant satırı (mlpcm 15702882 + Medipol 100000001).

## Execution Queue (açık/pending — master: tracking/pilot-launch-roadmap.md)

- **FEAT-IYS-INTEGRATION** (yeni, tracking var): DRAFT-RESEARCH, build BLOCKED — İYS A.Ş. Faz 0 statü cevabı (AHS mi/Entegratör mi) bekliyor. `tracking/feat-iys-integration.md`.
- **FEAT-INMA-PIPELINE-V2 C4 ✅ DONE+DEPLOYED 2026-06-15** (commit `20e8a1c5`, Codex iter2 PASS). Kalan: **canlı e2e smoke** (5050'de durum-değişikliği flow + bir flow'a `action_set_customer_status` node ekleyip cxapi write → INMA echo → C3a suppress) = Q/INMA aksiyonu; **Medipol (100000001) `webhook_secret` set değil** (inbound-echo yarısı). **C3 hardening backlog (go-live öncesi):** bounded-retry + idempotency-table + outbox (AutomaticRetry=0 lossy). FEAT-INMA-PIPELINE-V2 5-chunk = C1/C2/C3a/C3b/C4 + C5 HEPSİ DONE.
- **⚠️ İZLE:** Backend stderr tarihsel `Body was inferred but the method does not allow inferred body parameters` crash trace (canlı PID sağlıklı — ayrı incelenmeli).
- **UP0.3** Tenant lifecycle handler: PENDING (INMA tenant.created). **UP0.5** IInmaSendClient: PENDING (INMA J1/J4).
- **PKT-13 Faz 1** Lead Scoring: PENDING (Marketing). **FEAT-OBI Faz 2:** telefon tek-numara geçmiş arama (Faz 1B DONE+DEPLOYED).
- **FEAT-META-CAPI / META-ADS-INSIGHTS:** DRAFT (Q provision: Pixel/token/App Review). META-MARKETING-API: BACKLOG ($50k+/ay gate).
- **Dent Adavista pilot:** BLOCKED (UP0 + FEAT-* sonrası). **Zoho OAuth smoke** (5050 e2e): DEFERRED (INMA creds 401, opsiyonel).

<!-- END_CURRENT_STATE max_lines=300 -->
