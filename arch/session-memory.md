# Session Memory — Current State

> **BOUNDED FILE.** OVERWRITE edilir (append değil), ≤300 satır. Session init `limit=320` ile okur; sonundaki `END_CURRENT_STATE` işareti o pencerede görünmüyorsa DUR → `/optimize-memory`. Eski tam log: [session-memory-archive.md](session-memory-archive.md) (SADECE Grep). Kalıcı kararlar: [docs/decisions.md](docs/decisions.md). Standing kurallar: [hot-lessons.md](hot-lessons.md).
> **Pilot Mode:** queue+tracking otoritesi `tracking/pilot-launch-roadmap.md`'de; bu dosya son durum detayı.

_Güncel: 2026-06-15 — **REFACTOR AUDIT CONSOLIDATION = MERGED + FULL-PLATFORM DEPLOYED.** 18 audit branch'i `work/20260614-audit-consolidation` → master (`33583aae`, push'lu). 2 conflict çözüldü: `ExportRepository.cs` → consolidation'ın `int?` modeli (deployed 0-sentinel hotfix `3f540ab6`'yı supersede eder, tüm Outbound consumer'larda uçtan uca threaded); `session-memory.md` → `--ours`. Full-solution build = 0 error. **Deploy (Q onayı: değişen 10 servisin TAMAMI; Marketing dokunulmadı):** D1 Knowledge/Appointments/VoiceAI(manuel, ayrı NSSM) → D2 Backend(+SPA rebuild) → D3 Outbound/ChatAnalysis/WAA (fail-closed gate'ler GEÇTİ = servisler temiz başladı) → D4 Automation/Integrations/AgentAI. **Final health sweep: 10 enum servisi + VoiceAI(7114) HEPSİ HEALTHY.** D3 config gate deploy-öncesi canlı doğrulandı (Backend=Outbound SharedSecret sha8 04A96F3D MATCH; WAA OpsKey EEB34235; ChatAnalysis InternalApiKey 466EEEDB). **AgentAI deploy'unda transient SSH `Channel open failure` (ardışık hızlı deploy → MaxSessions) → servis STOPPED + dir TEMİZ + zip yüklü kaldı → MANUEL recover** (`deploy-AgentAI-*.zip` extract + config `.bak`'tan restore + nssm start → HEALTHY). server-deploy `.bak`/`previous` rollback kopyaları tüm servislerde. **Not:** getint32 (Wave-1) 6 servise yayılı + Wave-2 sweep'lerle aynı binary'lerde iç içe → "Wave-1 deploy" zorunlu olarak neredeyse-tüm-platform oldu; AgentAI tek saf-Wave-2 servisti._

_Önceki: **CONSOLIDATION (2026-06-14):** 18 unmerged audit branch'i tek integration branch'te toplanmıştı (kod conflict-free; sadece `arch/codex-context.md` additive union + session-memory --ours). Plan + tam batch tarihçesi: `tracking/audit-consolidation-plan.md` (Status=MERGED+DEPLOYED). Audit girişi: `arch/reports/20260614-audit-INDEX.md` (176 bulgu, 8C/34H). Daha önce master'a merge+deploy edilmiş tek audit batch'leri: WebChat §7 (`7d895d47`) + Backend auth/login (`30fd9361`). Outbound template_id-null hotfix (`3f540ab6`) ayrı deploy edilmişti, şimdi `int?` modeli supersede etti. Tam batch detay → session-memory-archive.md (grep)._

## Recently Completed (max 10)

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
- **FEAT-INMA-PIPELINE-V2 C4** (refactor-audit sonrası): Set Customer Status write-back — INMA update endpoint + `invekto-{flowRunUuid}` ClientRequestID. **C3 hardening backlog:** bounded-retry + idempotency-table + outbox. **Prereq:** Medipol (100000001) `webhook_secret` set değil. C3b/C4 canlı smoke: 5050'de INMA panel durum değişikliği → flow tetik (AC10).
- **⚠️ İZLE:** Backend stderr tarihsel `Body was inferred but the method does not allow inferred body parameters` crash trace (canlı PID sağlıklı — ayrı incelenmeli).
- **UP0.3** Tenant lifecycle handler: PENDING (INMA tenant.created). **UP0.5** IInmaSendClient: PENDING (INMA J1/J4).
- **PKT-13 Faz 1** Lead Scoring: PENDING (Marketing). **FEAT-OBI Faz 2:** telefon tek-numara geçmiş arama (Faz 1B DONE+DEPLOYED).
- **FEAT-META-CAPI / META-ADS-INSIGHTS:** DRAFT (Q provision: Pixel/token/App Review). META-MARKETING-API: BACKLOG ($50k+/ay gate).
- **Dent Adavista pilot:** BLOCKED (UP0 + FEAT-* sonrası). **Zoho OAuth smoke** (5050 e2e): DEFERRED (INMA creds 401, opsiyonel).

<!-- END_CURRENT_STATE max_lines=300 -->
