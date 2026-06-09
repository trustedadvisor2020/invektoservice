# Session Memory — Current State

> **BOUNDED FILE.** OVERWRITE edilir (append değil), ≤300 satır. Session init `limit=320` ile okur; sonundaki `END_CURRENT_STATE` işareti o pencerede görünmüyorsa DUR → `/optimize-memory`. Eski tam log: [session-memory-archive.md](session-memory-archive.md) (SADECE Grep). Kalıcı kararlar: [docs/decisions.md](docs/decisions.md). Standing kurallar: [hot-lessons.md](hot-lessons.md).
> **Pilot Mode:** queue+tracking otoritesi `tracking/pilot-launch-roadmap.md`'de; bu dosya son durum detayı.

_Güncel: 2026-06-10_

## Current State Snapshot

**Platform:** 11 .NET 8 mikroservis, tek shared PostgreSQL 16 + pgvector. Prod = services.invekto.com, `C:\Invekto\{Service}\current\`, NSSM. Portlar: Backend 5000, ChatAnalysis 7101, Appointments 7102, Knowledge 7104, AgentAI 7105, Integrations 7106, **Outbound 7107**, Automation 7108, WhatsAppAnalytics 7109, Marketing 7112. FaceAnalysis 7110 + VisualSearch 7111 = Planned. VoiceRuntime + VoiceAI = ayrı NSSM (server-deploy enum'da EKSİK — L-2026-05-31).

**Aktif iş: FEAT-PROJELER (PKT-14) — cxapi/WapCRM bulk-send motoru.**
- **2026-06-10 (Backend+SPA, CLIENT-ONLY, 2 deploy, ikisi de Codex PASS):** **(1) wa-templates 502 FIX** (`b28073db`) — canlı cxapi `POST /api/templates` `preview` alanı OBJE (`{header,body,footer,buttons}`), `string?` değil → JsonException → TransportError → 502 (tenant 5050/instance 6570). Fix: `WapCrmTemplatePreviewDto` + name/language/category/paramFormat + gerçek-wire-shape pinli parse testi. **(2) Projeler modal REDESIGN** (`4de49e02`) — iki-kolon (sol ad/açıklama/liste, sağ gönderim) + kanal etiketinden (kod) kaldırıldı + Onaylı Şablon default/leftmost, Düz Metin disabled, Yok kaldırıldı + WhatsApp-stil önizleme baloncuğu (`{{...}}` vurgulu) + her placeholder (header'daki **hname** dahil — önizlemeden regex'le çıkarılır, cxapi requiredInputs eksik bıraksa bile) için liste-kolonu dropdown (Ad/Soyad/E-posta/Alan1-5/Etiket/Not). **Şablon OPSİYONEL** (seçmezse metadata-only kaydedilir). `param_mapping` entry shape += `source:'column'/column` (opak JSON, server `BuildSendConfig` sadece object/array+≤16KB doğrular → backend değişmedi; **PR-4 tüketir, şu an INERT**). SPA bundle artık `index-Bag-9o6R.js`. P0-3 dokunulmadı.
- **2026-06-09 PM: SS-A/SS-B/SS-C PROD'A DEPLOY EDİLDİ.** Migration **059+060** + Outbound + Backend + SPA bundle (`index-L_L4TiDm.js`) canlı. **10/10 service HEALTHY.** Her iki migration INV-SEED verifier PASS; read-back doğrulandı.
- **⛔ PROD-INERT KORUNDU (P0-3 gate):** `Projects` allowlist=[5050] ∩ `BulkSend` allowlist=[18173130] = **∅** → hiçbir tenant hem proje yönetip hem gönderemiyor. `CxapiSend` section prod config'de YOK → cxapi route default-OFF. Hiçbir allowlist'e dokunulmadı. Endpoint smoke: send-status 401 (registered + JWT-gated).
- **Kod (commit'li, hepsi Codex PASS — FORCE PASS değil):** SS-A (`ed641ad8` broadcast inline-text), SS-B (`5f61a077` proje içerik config: gallery_template VEYA free_text), SS-C (`7c82d621` proje run dispatch + Gönder UI). Mimari: bir run = `bulk_send_job(project_id)` → mevcut bulk preview→confirm→status makinesi reuse.
- **Daha önce canlı:** PKT-14 S1-S4 (migration 055+056+057, 2026-06-07) + cxapi PR-3b-1/PR-3b-2 (migration 058 delivery-ack ingress, 2026-06-08). Tüm cxapi send/ack kodu prod'da ama **inert** (external_message_id NULL → ack 404→202-swallow).

**Pilot tenant gerçeği:** WapCRM-konfigli TEK tenant = **5050 (TestEticaret)**, vendor sandbox, instance_id NULL → **gerçek pilot YOK**. Gate-open için gerçek müşteri tenant + instance_id gerekli (Q kararı). Codex = gpt-5.5, LOW dahil her risk review.

**Working tree uyarısı (her session tekrar):** ilgisiz `ui-mocks/*` + `chat-design` WIP master working tree'de — commit'lere dahil EDİLMEDİ; deploy öncesi `git status` + bundle-hash re-verify.

## Execution Queue (açık/pending — master: tracking/pilot-launch-roadmap.md)

- **SS-D:** pause/resume/cancel — migration 061 (`paused` message-status) + run lifecycle + ProjectsPage kontrolleri (Q split kararı, SS-C'den ayrıldı).
- **PR-4:** approved-template (HSM) send — dil=slug net (G13 çözüldü), statik buton; go-live G12/P0-3 gate'ine bağlı.
- **GATE-OPEN kararı (Q'da):** gerçek pilot tenant'ı hem Projects HEM BulkSend allowlist'ine + WapCRM config doğrula (SubscribedWebhookActive + WebhookUrl) → P0-3 kalk. Ayrı/bilinçli karar.
- **KARAR-INMA-PIPELINE-CONTRACT:** PENDING (BLOCKER) — INMA Swagger'da pipeline endpoint yok; FEAT-PIPELINE Faz 1 başlatılamaz, contract draft gerek.
- **FEAT-PIPELINE:** DRAFT (contract'a BLOCKED) — INMA-driven lead pipeline + 3-way sync.
- **UP0.3** Tenant lifecycle handler: PENDING (INMA tenant.created event bekliyor). **UP0.5** IInmaSendClient: PENDING (INMA J1/J4 bekliyor).
- **PKT-13 Faz 1** Lead Scoring: PENDING (Marketing). **FEAT-OBI Faz 2:** telefon tek-numara geçmiş arama (Faz 1B DONE+DEPLOYED).
- **FEAT-META-CAPI / META-ADS-INSIGHTS:** DRAFT (Q provision bekliyor: Pixel/token/App Review). META-MARKETING-API: BACKLOG ($50k+/ay gate).
- **Dent Adavista pilot:** BLOCKED (UP0 + FEAT-* sonrası). **Zoho OAuth smoke** (5050 e2e): DEFERRED (INMA creds 401, opsiyonel).

<!-- END_CURRENT_STATE max_lines=300 -->
