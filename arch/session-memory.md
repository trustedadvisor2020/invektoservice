# Session Memory — Current State

> **BOUNDED FILE.** OVERWRITE edilir (append değil), ≤300 satır. Session init `limit=320` ile okur; sonundaki `END_CURRENT_STATE` işareti o pencerede görünmüyorsa DUR → `/optimize-memory`. Eski tam log: [session-memory-archive.md](session-memory-archive.md) (SADECE Grep). Kalıcı kararlar: [docs/decisions.md](docs/decisions.md). Standing kurallar: [hot-lessons.md](hot-lessons.md).
> **Pilot Mode:** queue+tracking otoritesi `tracking/pilot-launch-roadmap.md`'de; bu dosya son durum detayı.

_Güncel: 2026-06-17 — **PROJELER BUG-FIX PACK = DONE+DEPLOYED+VERIFIED** (Q-reported, Projeler list page, tenant 100000001 Medipol). Commit'ler `9c964487` (ana paket) + `a66918c5` (rapor hata-sebebi). **5 düzeltme:** (A) borderless ghost ikon-only satır aksiyonları (etiketli butonlar taşıyordu). (B) "Liste" kolonu → **"Alıcı"** = `SUM(data_lists.sendable_count)` (yeni additive `ProjectSummary.recipients_total`, tenant-scoped correlated subquery, list+detail SQL, MapSummary idx 25). (C+D) Sayaçlar 0 + "Çalışıyor"da donma: **kök neden** `ProjectsService.ListAsync` stored rollup okuyordu ama HİÇ recompute etmiyordu (sadece send-status/resend/lifecycle ediyordu) → fire-and-forget run (modal kapanınca) MarkRunning snapshot'ında donuyordu. Fix: yeni `RecomputeTenantRollupsAsync` (per-project `RecomputeRollupAsync`'ın set-based kardeşi, IDENTICAL status/completed_at semantiği + change-guard → steady-state load 0-row UPDATE), `ListAsync`'te SELECT'ten ÖNCE çağrılır (recompute-then-read, `GetSendStatusAsync` deseni). (E) İletildi/Okundu gelmiyor: yeni **`ProjectStatusPullJob`** (default-OFF Timer IHostedService, `CxapiWebhookReconcileJob` deseni: interlocked overlap guard, graceful shutdown, typed catch-only) → manuel "Durumu Yenile" cxapi `/api/message-status` PULL'unu OTOMATİK çalıştırır; tenant set = WapCrm-configured ∩ CxapiSend-allowed, per-tenant scoped candidate query (`GetProjectIdsNeedingStatusPullAsync`, MaxRunAgeDays=14 bound), mevcut `RefreshRunStatusAsync` reuse (ProjectsService singleton). **CANLI KANIT:** ilk tick `checked=144, updated=110` (Medipol proje 7: 143 sorgulandı, 109 ilerledi → cxapi'de receipt VARMIŞ, PULL çekti; webhook PUSH bağlı olmasa da PULL çalışıyor). Proje 7 artık delivered=89/read=37. **Rapor "Hata" düzeltmesi** (`a66918c5`): `provider_error_message='Success'` = send-accept echo, HER kabul edilen mesaja basılı → eski `COALESCE(failed_reason, provider_error_message)` 178 satırda (delivered/read/sent + 24 failed) "Success" gösteriyordu. Fix: pull 'failed'(NotSent) işaretlerken gerçek failed_reason damgalar + report Error = `COALESCE(failed_reason, NULLIF(provider_error_message,'Success'), CASE failed/ambiguous → 'İletilemedi...')`. cxapi batch pull `reasonDetailForNotSent` TAŞIR (ilk varsayım yanlıştı; `9ea2b92b` capture eder → gerçek WhatsApp sebepleri: 131026/131049/130472/131053). **Codex iter0 PASS** her iki commit (12/12 CQ + CoVe). Outbound+Backend deploy 10/10 HEALTHY binary-fresh; `ProjectStatusPull.Enabled=true` Production.json'a targeted-insert (`.bak`'lı). Q'ya not: "26" hata kodu hiçbir mesaj verisinde YOK (iki tenant taranı) → cache'li ekran veya tek-numara gerçek sağlayıcı kodu._

_Önceki: **FEAT-INMA-PIPELINE-V2 C3 HARDENING = DONE+DEPLOYED+SMOKED 2026-06-15** (commit `c7f26673`, Codex iter0 PASS) — flow-trigger transactional outbox (migration 066) + exactly-once job-claim + preflight retry + C4 bounded transient retry. **C4 'Set Customer Status' DONE+DEPLOYED 2026-06-15** (`20e8a1c5`). FEAT-INMA-PIPELINE-V2 5-chunk (C1/C2/C3a/C3b/C4 + C5) + C3 hardening HEPSİ DONE. Detay → session-memory-archive.md (grep)._

## Recently Completed (max 10)

- **projeler-resend-503-hotfix** (2026-06-17) — Rapor "Yeniden gönder" HER denemede 503 (Q canlı console paylaştı). Kök: resend SQL `completed` broadcast'ı `status='sending'` ile reopen ediyordu ama `chk_broadcast_status` yalnız queued|processing|completed|failed kabul eder ('sending'=outbound_MESSAGES sözlüğü) → PG **23514** → ProjectDbError → 503. Tüm 31 broadcast 'completed' → resend %100 kırık (tek+bulk). Fix `'sending'`→`'queued'` (RequeueForResend + RequeueAllForResend). Commit **130d4e85**, Codex iter0 PASS, Outbound deploy HEALTHY, post-restart 0 fail. Gerçek hata `C:\Invekto\Outbound\logs\{date}.jsonl`'de (MCP stdout/stderr default path boş görünüyordu). Lesson kaydedildi.
- **projeler-bugfix-pack** (2026-06-17) — Projeler list: live rollup recompute-on-list (C/D stuck-running+0-counters) + "Alıcı" sendable column (B) + auto status-pull job ProjectStatusPullJob (E, default-OFF, prod-enabled [5050,100000001]) + borderless icon actions (A) + report "Hata" real-reason not 'Success' echo. Commits `9c964487`+`a66918c5`, Codex iter0 PASS both, Outbound+Backend deploy HEALTHY, status-pull first tick checked=144/updated=110 (Medipol receipts pulled live).
- **feat-inma-pipeline-v2-c3-hardening** (2026-06-15) — Flow-trigger transactional outbox (migration 066) + exactly-once job-claim + preflight retry + C4 bounded transient retry; commit `c7f26673`; Codex iter0 PASS. Pre-impl codex_consult iter0'ı kurtardı.
- **feat-inma-pipeline-v2-c4-set-customer-status** (2026-06-15) — Flow ACTION node cxapi write-back; commit `20e8a1c5`; Codex iter2 PASS. Kalan e2e: Q/INMA + Medipol webhook_secret.
- **refactor-audit-consolidation-deploy** (2026-06-15) — 18 branch merge→master + full-platform deploy (10 servis), final all-HEALTHY.
- **outbound-template_id-null-hotfix** (2026-06-15) — Export path firing-crash; sonra consolidation `int?` ile supersede.
- **refactor-audit-consolidation** (2026-06-14) — 18 branch → integration branch, conflict-free, build 0 error, gate GREEN.
- **webchat-section7** (`7d895d47`) — merged+deployed (INV-WC-026).

## Audit kalanı (henüz dokunulmamış — opsiyonel sonraki iş)

- **Automation:** Auto-2 SSRF (MUST-ASK), Auto-11/17 (behavior-change real-bug), Auto-8/22 (TOCTOU).
- **Knowledge:** K-3 (Sector="eticaret" davranış+migration), K-8 (UpdateAsync tx/TOCTOU), K-11/15 (dedup), K-16 (scenario→faq).
- **WAA:** WAA-8 (ON CONFLICT latent), WAA-15 (DI dup). **Integrations:** Int-4 full pagination. **Backend:** kalan low/med. **Marketing-1.**
- Her batch = master'dan yeni `work/` branch → auto+Codex PASS → commit'te DUR → auth/Shared değişikliğinde MUST-ASK. Giriş: iki raporun §4 Quick Wins + §7.

## Platform & Gate

**Platform:** 11 .NET 8 mikroservis, tek shared PostgreSQL 16 + pgvector. Prod = services.invekto.com, `C:\Invekto\{Service}\current\`, NSSM. Portlar: Backend 5000, ChatAnalysis 7101, Appointments 7102, Knowledge 7104, AgentAI 7105, Integrations 7106, **Outbound 7107**, Automation 7108, WhatsAppAnalytics 7109, Marketing 7112, **VoiceAI 7114 (ayrı NSSM, server-deploy enum'da YOK → manuel zip)**. FaceAnalysis 7110 + VisualSearch 7111 = Planned. VoiceRuntime 7115 = ayrı NSSM.

**Fail-closed gate (2026-06-15):** Her servis TEK key gate'ler — ChatAnalysis=`Microservice:InternalApiKey`, WAA=`Benchmark:OpsKey`. Outbound↔Backend `InternalServices:SharedSecret` (iki uçta EŞİT). Config eksikse startup throw = DOWN → deploy öncesi CANLI doğrula.

**P0-3 Gate:** `BulkSend.AllowAllTenants=true` + `Projects=[5050, 15702882, 100000001]` + `CxapiSend=[5050, 100000001]` + `CxapiWebhookReconcile {Enabled:true, AllowAllConfigured:true}` + **`ProjectStatusPull {Enabled:true}`** (2026-06-17, vendor-scope CxapiSend allowlist'i reuse eder → otomatik delivered/read pull). Codex = gpt-5.5, LOW dahil her risk review.

**Pilot tenant gerçeği:** WapCRM-konfigli canlı = 5050 (vendor sandbox) + Medipol (100000001 wappflex, instance 8784 WABA). cxapi send + delivery-ack (PULL üzerinden CANLI) uçtan uca çalışıyor. Medipol = İKİ tenant satırı (mlpcm 15702882 + Medipol 100000001). **Medipol delivered/read webhook PUSH bağlı değil ama PULL (status-pull job) receipt'leri çekiyor (2026-06-17 doğrulandı).**

## Execution Queue (açık/pending — master: tracking/pilot-launch-roadmap.md)

- **Projeler "26" (Q gözlemi 2026-06-17) — MUHTEMELEN ÇÖZÜLDÜ:** "26" = proje 7'nin (Medipol) **failed/ambiguous resendable mesaj sayısı** (error kodu DEĞİL); Q bunları "Yeniden gönder" ile yeniden göndermeye çalışıyordu → 503 (resend hotfix 130d4e85 ile fix). Eğer Q yine "26" görür + resend artık 200 dönerse kapat. Düşük öncelik.
- **Outbound `template_id is null` 500 (2026-06-17 bulundu, fix EDİLMEDİ):** `System.InvalidCastException: Column 'template_id' is null` = NULL kolonu non-nullable okuma (IsDBNull guard'sız `GetFieldValue<T>`), Outbound bir read-path'inde UNHANDLED 500 (resend 503'ten AYRI). stdout'ta tekrarlı. Okuma site'ini bul (report recipients / templates list — `template_id` adlı kolon; `ProjectsRepository`'de DEĞİL — orada wa_template_id/outbound_template_id guard'lı) → IsDBNull guard ekle. Düşük-orta öncelik.
- **FEAT-IYS-INTEGRATION:** DRAFT-RESEARCH, build BLOCKED — İYS A.Ş. Faz 0 statü cevabı bekliyor. `tracking/feat-iys-integration.md`.
- **FEAT-INMA-PIPELINE-V2:** C4 + C3 hardening DONE+DEPLOYED. Kalan: canlı e2e smoke (5050 durum-değişikliği flow + action_set_customer_status node) = Q/INMA aksiyonu; Medipol (100000001) `webhook_secret` set değil (inbound-echo yarısı).
- **UP0.3** Tenant lifecycle handler: PENDING. **UP0.5** IInmaSendClient: PENDING.
- **PKT-13 Faz 1** Lead Scoring: PENDING. **FEAT-OBI Faz 2:** telefon tek-numara geçmiş arama.
- **FEAT-META-CAPI / META-ADS-INSIGHTS:** DRAFT (Q provision). META-MARKETING-API: BACKLOG ($50k+/ay gate).
- **Dent Adavista pilot:** BLOCKED (UP0 + FEAT-* sonrası). **Zoho OAuth smoke:** DEFERRED.

<!-- END_CURRENT_STATE max_lines=300 -->
