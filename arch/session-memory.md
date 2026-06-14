# Session Memory — Current State

> **BOUNDED FILE.** OVERWRITE edilir (append değil), ≤300 satır. Session init `limit=320` ile okur; sonundaki `END_CURRENT_STATE` işareti o pencerede görünmüyorsa DUR → `/optimize-memory`. Eski tam log: [session-memory-archive.md](session-memory-archive.md) (SADECE Grep). Kalıcı kararlar: [docs/decisions.md](docs/decisions.md). Standing kurallar: [hot-lessons.md](hot-lessons.md).
> **Pilot Mode:** queue+tracking otoritesi `tracking/pilot-launch-roadmap.md`'de; bu dosya son durum detayı.

_Güncel: 2026-06-14 — **AKTİF İŞ: Refactor Audit batch'leri** (aşağıda; giriş `arch/reports/20260614-audit-INDEX.md`). Önceki teslim: FEAT-INMA-PIPELINE-V2 **C3b DONE+DEPLOYED** (2026-06-13 ~22:38, `633c07bd` wrap + `2ad5058b` ttl, Backend-only, 10/10 HEALTHY, SPA `index-E1nc-dJ3.js`). Tam tarihçe (cxapi/PKT-14/C2/C3a/incident'ler) → session-memory-archive.md (grep)._

## AKTİF İŞ — Refactor Audit 2026-06-14

**Giriş:** `arch/reports/20260614-audit-INDEX.md` (master) + memory `project_refactor_audit_2026_06_14`. TÜM .NET kod tabanı read-only tarandı → **176 bulgu (8 Critical / 34 High)**, triage Q'da. Hiçbir şey master'a merge edilmedi, deploy yok.

**Çalışma kuralı (HER batch):** master'dan yeni `work/` branch → auto+Codex PASS → **commit'te DUR** (deploy YOK, master'a merge YOK) → auth/Shared değişikliğinde **MUST-ASK**. Branch'leri master'a merge + deploy kararı Q'da.

**Batch durumu:**
- ✅ **Batch 1** — GetInt32-on-bigint `::int` cast: `work/20260614-getint32-bigint-cast-fix` (`ad7efb63`), remote push'lu, merge YOK
- ✅ **Batch 2** — INV-BE errorcode collision (6 çakışma → 134-139): `work/20260614-errorcode-collision-fix` (`0c3c3a4a`), push'lu, merge YOK
- ✅ **Batch 3** — fail-closed auth (ChatAnalysis+WAA startup-throw): `work/20260614-failclosed-auth-fix` (`82424043`), push'lu, merge YOK. **DEPLOY PREREQ:** prod'da `Benchmark:OpsKey` + `Microservice:InternalApiKey` set et.
- ⏳ **Batch 4 (SIRADAKİ)** — VoiceAI path crash+traversal: `VoiceTranscriptionService.cs:42` TraceIdentifier + filename sanitize
- ⏳ **Batch 5** — Outbound internal-endpoint (önce FALSE-POZİTİF teyit et)
- ⏳ **Batch 8** — tenant-scoping: wa_intents `ReadIntentDistributionAsync` + `ReadFaqClustersAsync` + tenantId threading
- ⏳ **Batch 6/7** — büyük sweep'ler (broad catch + null-forgiving, ~150 medium/low, çok-paket)

## Platform & Gate

**Platform:** 11 .NET 8 mikroservis, tek shared PostgreSQL 16 + pgvector. Prod = services.invekto.com, `C:\Invekto\{Service}\current\`, NSSM. Portlar: Backend 5000, ChatAnalysis 7101, Appointments 7102, Knowledge 7104, AgentAI 7105, Integrations 7106, **Outbound 7107**, Automation 7108, WhatsAppAnalytics 7109, Marketing 7112. FaceAnalysis 7110 + VisualSearch 7111 = Planned. VoiceRuntime + VoiceAI = ayrı NSSM (server-deploy enum'da EKSİK — L-2026-05-31).

**P0-3 Gate (güncel 2026-06-13):** `BulkSend.AllowAllTenants=true` (TÜM tenantlar, bridge route) + `Projects=[5050, 15702882, 100000001]` + `CxapiSend=[5050, 100000001]` (5050 sandbox + Medipol-wappflex). Medipol ack webhook OTOMATİK — FEATURE A `CxapiWebhookReconcileJob` ENABLED/AllowAll prod'da (yabancı URL'lere dokunmaz). Codex = gpt-5.5, LOW dahil her risk seviyesi review.

**Pilot tenant gerçeği:** WapCRM-konfigli canlı tenant'lar = 5050 (vendor sandbox) + Medipol (100000001 wappflex, instance 8784 WABA). cxapi send + delivery-ack zinciri uçtan uca canlı doğrulandı. Medipol = İKİ tenant satırı (mlpcm 15702882 + Medipol 100000001) — firma-bazlı sorguda ikisini de kullan.

## Execution Queue (açık/pending — master: tracking/pilot-launch-roadmap.md)

- **FEAT-INMA-PIPELINE-V2 C4** (refactor-audit sonrası): Set Customer Status write-back — INMA update endpoint + `invekto-{flowRunUuid}` ClientRequestID (`CustomerStatusFlowSuppression.OriginRequestIdPrefix` reuse). **C3 hardening backlog:** bounded-retry + idempotency-table + outbox (AutomaticRetry=0 transient blip'te tek otomasyon kaybı — go-live öncesi). **Prereq:** Medipol (100000001) `webhook_secret` set değil. C3b/C4 canlı smoke: 5050'de INMA panel durum değişikliği → C2→C3a→flow tetik uçtan uca (AC10).
- **⚠️ İZLE:** Backend stderr tarihsel `Body was inferred but the method does not allow inferred body parameters` crash trace (canlı PID sağlıklı, C2 route eklemiyor — ayrı incelenmeli).
- **UP0.3** Tenant lifecycle handler: PENDING (INMA tenant.created event bekliyor). **UP0.5** IInmaSendClient: PENDING (INMA J1/J4 bekliyor).
- **PKT-13 Faz 1** Lead Scoring: PENDING (Marketing). **FEAT-OBI Faz 2:** telefon tek-numara geçmiş arama (Faz 1B DONE+DEPLOYED).
- **FEAT-META-CAPI / META-ADS-INSIGHTS:** DRAFT (Q provision bekliyor: Pixel/token/App Review). META-MARKETING-API: BACKLOG ($50k+/ay gate).
- **Dent Adavista pilot:** BLOCKED (UP0 + FEAT-* sonrası). **Zoho OAuth smoke** (5050 e2e): DEFERRED (INMA creds 401, opsiyonel).

<!-- END_CURRENT_STATE max_lines=300 -->
