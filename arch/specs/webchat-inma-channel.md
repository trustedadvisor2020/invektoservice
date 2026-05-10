# SPEC: WebChat as INMA Channel

> **Spec ID:** SPEC-WC-INMA | **Paket:** PKT-WC-INMA-CHANNEL | **Risk:** HIGH
> **Yazar:** Q | **Son Guncelleme:** 2026-05-10 | **Durum:** ACCEPTED (v10, Q approved 2026-05-10 after 10 Codex iterations)
> **Codex Review Notes:**
> - 10 iterasyon: v1 (FAIL, 13 issue) → v10 (FAIL, 3 issue: 2 false positive + 1 minor type fix in dev)
> - Mimari kararlar v3'te kapandi, geri kalan iterasyonlar format/tutarlilik
> - Q karari: spec accepted as-is, residual issues dev sirasinda cozulur (kod review ayri PASS hedefiyle gider)
> - CQ5/CQ9 INMA SQL READONLY = false positive (mevcut prod pattern, arch/features/tenant-field-mapping.md:76 referansli)
> - CQ11 widget_id UUID/TEXT type drift = dev fix (contract `string` ya da DB UUID)
>
> **Onkosul Paket:** PKT-BRIDGE-906-907-FIX (callback bridge 906/907 parse bug fix) — webchat live oncesi tamamlanmali
> **Bridge Fix Gate:** DB-level CHECK constraint `chk_bridge_gate` — `feature_flag_enabled=TRUE` mumkun degil eger `bridge_fix_verified=FALSE` ise (migration 047)

## 1. Intent (Ne & Neden)

WebChat'i bagimsiz bir Invekto urunu olmaktan cikarip INMA'nin (WapCRM) yeni bir kanal tipi haline getirmek — WABA ve IG gibi. Operator'lar zaten WABA/IG icin INMA kullaniyor; webchat'in ayri bir UI'da olmasi operator workflow'unu boluyor. Hedef: tum mesajlasma INMA'da, Invekto sadece browser-INMA koprusu + widget yonetimi.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | Visitor browser'dan mesaj attiginda INMA'ya inbound HTTP POST gider, idempotency dogrulanir | Network log + INMA conversation kaydi + duplicate replay testi |
| AC-2 | Operator INMA UI'da yazdiginda mesaj browser'a SignalR ile <2s ulasir | Manuel test, browser DevTools timing |
| AC-3 | Visitor sayfayi yeniledikten sonra son 50 mesaji gorur | INMA history endpoint query + UI test |
| AC-4 | Ayni cookie ile geri donen visitor ayni INMA contact'ina baglanir, yeni conversation acilir | DB query (INMA contact external_id), conversation_id farkli |
| AC-5 | message_id bazli idempotency 24 saat icinde calisir; replay = no-op | Manual replay testi (24h+1 sonra dedupe state expired) |
| AC-6 | Tenant 1+ webchat olusturabilir, her widget bagimsiz config (lang, flow, allowed_origins) | Dashboard'dan 2 widget olustur, ayri ayri test et |
| AC-7 | Cutover sonrasi 7 gun dual-write window'unda her iki path'e de yazilir (Outbox Pattern); 7+1 gun sonra eski path read-only | Outbox pending count cron alert (>100 INV-WC-025), per-message correlation check, `retry_count <= 5` enforcement (max 6 attempt), 7+1 gun sonra trigger error on legacy INSERT |
| AC-8 | Widget API'leri public contract — Phase 2'de INMA cagirinca calisir (auth + tenant_id scope) | API contract test (OpenAPI / curl) |
| AC-9 | 30dk inaktivite sonrasi conversation INMA'da auto-close | Timer test + INMA conversation status |
| AC-10 | Operator dashboard'daki WebChat sayfasi tamamen kaldirildi (history viewer dahil) | Dashboard route check, sayfa yok, link yok |
| AC-11 | Cookie SameSite=None+Secure ile embed iframe'de session persist eder | 3rd-party iframe testi (farkli domain) |
| AC-12 | external_id collision riski yok — full UUID kullaniliyor | Format dogrulama: regex `wc-[0-9a-f-]{36}-[0-9a-f-]{36}` |
| AC-13 | Allowed origins disindan gelen widget istekleri reddedilir | CORS + custom origin check, 403 + INV-WC-018 |
| AC-14 | INMA endpoint'lerinden gelen tum hata response'lari standart envelope ile | Hata simulasyonu + HTTP status + error envelope kontrolu |
| AC-15 | Bridge bug fix paketi (PKT-BRIDGE-906-907) tamamlanmadan webchat cutover yapilmaz | DB-level CHECK constraint `chk_bridge_gate` (migration 047) + boot-time gateway health check |
| AC-16 | webchat_inma_idempotency tablosu 24 saat sonra Hangfire scheduled job ile temizlenir, unbounded growth yok | Hangfire job execution log + table row count steady state |
| AC-17 | webchat_outbox dual-write reconciliation Hangfire cron 5dk'da bir pending entries retry eder, exponential backoff (1,2,5,15,60 dk), `retry_count <= 5` (max 6 attempt: initial + 5 retry), 6.attempt fail = atomik gecis retry_count=6 + status='failed', total 83dk fail-safe window | Hangfire job log + outbox status DB query + next_retry_at kolonu + chk_outbox_no_silent_stuck constraint |
| AC-18 | Local dev/staging'de SameSite=None+Secure cookie HTTPS olmadiginda fallback (NODE_ENV=development -> SameSite=Lax) | Dev environment widget testi |
| AC-19 | INMA endpoint'lerinden non-conforming error response gelirse gateway StandardErrorEnvelope ile wrap eder | Mock INMA non-conforming response testi |
| AC-20 | Widget sync INMA unreachable iken Dashboard CRUD basarili (local DB write), outbox queue'ya pushlanir, Hangfire saatlik retry | INMA mock down testi + Hangfire retry log |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| INMA = single source of truth (mesajlar) | WABA/IG ile simetri, dual-write cehennemini onler | EXPECTED: webchat_messages tablosunun read-only'e dusurulmesi data migration false positive — kasitli |
| Invekto WebChat servisi yeniden adlandiriliyor: **Invekto.WebChat.Gateway** (gateway + widget config) | "Thin gateway" terimi yaniltici, widget config + cookie session taniyor; gateway-with-config daha dogru | EXPECTED: gateway servisinin widget_configs persist etmesi false positive — pre-existing scope |
| Visitor identity = cookie UUID (full v4) + external_id `wc-{widgetUuid}-{visitorUuid}` (full UUID, 76 char) | Collision riski yok; 8-char truncation YANLIS karar idi (Codex bulgusu) | EXPECTED: external_id length 76 char — INMA contact tablosunda VARCHAR(128) gerek |
| Cookie SameSite=None + Secure (HTTPS-only) | Embed iframe 3rd-party domain'lerde Lax cookie gonderilmiyor — embed scenario broken (Codex bulgusu) | EXPECTED: cookie security review — SameSite=None production HTTPS sart |
| Mevcut callback bridge `/api/v1/callback/wapcrm` extend (channel: WEBCHAT) — ANCAK 906/907 bug fix onkosul | Yeni bridge kurmak yerine olgun bridge'i kullan; ancak parse bug'i webchat'i de etkileyebilir | EXPECTED: bridge bug fix ayri paket (PKT-BRIDGE-906-907-FIX), webchat onun tamamlanmasini bekler |
| Webchat-specific callback regression test ZORUNLU | 906/907 fix sonrasi bile webchat field'i bridge'de regression yapabilir | EXPECTED: contract test arch/contracts/webchat-inma-channel.json'a karsi calisir |
| Phase 1 UI Invekto'da, Phase 2 INMA'ya migrate (API-first) | Tenant'lar Phase 1'de degisiklik gormesin; Phase 2 sadece UI tasimasi, API ortak | EXPECTED: dual-UI period false positive — kasitli phasing |
| AI fallback Invekto'dan kaldir; INMA AI veya otomatik "operator yakinda doner" mesaji | INMA kendi AI agent'ini host ediyor; cift AI = otorite catismasi | EXPECTED: ai_active flag + 30s timer dropping = davranis degisikligi (kasitli) |
| Cutover: 7 gun dual-write window | Q karari — rollback safety | EXPECTED: 7 gun boyunca eski + yeni path her iki yerde de write yapilir; reads yeni path'tan |
| BACKFILL YOK — eski conversations INMA'ya tasinmaz; eski dashboard tamamen silinir, history kaybı kabul | Q karari — 11 tenant icin yonetilebilir, complexity > benefit | EXPECTED: webchat_messages read-only mode (T+7 sonra), eventual drop (T+90) |
| Cookie clear = yeni visitor; recovery YOK | Q karari — WABA'da telefon degistirmek gibi | EXPECTED: email enrichment kaldirildi spec'ten |
| Widget INMA'ya explicit-sync (lazy-create degil) | Cutover'da eksik widget = mesaj kaybolur; explicit endpoint ile garanti | EXPECTED: widget create/update Invekto endpoint'inde INMA'ya sync hook |
| message_id idempotency window = 24 saat | Network retry icin yeterli; adversarial replay icin makul; INMA storage maliyeti dusuk | EXPECTED: dedupe key TTL 24h sonra Hangfire `WebChatIdempotencyCleanupJob` ile silinir (saatlik cron) |
| Dual-write reconciliation = Outbox Pattern + Hangfire 5dk cron | 7 gun dual-write window'da partial failure tespiti zorunlu (Q karari) | EXPECTED: webchat_outbox table + per-message correlation + `retry_count <= 5` (max 6 attempt) + Sentry alert >100 pending |
| Widget sync INMA unreachable iken: local DB write basarili, **inma_synced_at=NULL marker** + saatlik retry (outbox kullanmaz, latest-state pattern) | Dashboard CRUD failure cascade'i onler; eventual consistency kabul; widget guncellemeleri en son state'i push eder, ardarda 5 update'te 5 outbox row birikmez | EXPECTED: `webchat_widget_configs WHERE inma_synced_at IS NULL` → Hangfire `WebChatWidgetSyncRetryJob` saatlik. **NOT:** webchat_outbox MESAJ icin (per-message correlation), widget sync icin DEGIL |
| Bridge fix gate = DB CHECK constraint `chk_bridge_gate` | Mekanik enforcement — `feature_flag_enabled=TRUE` mumkun degil eger `bridge_fix_verified=FALSE` ise | EXPECTED: migration 047 constraint, manuel UPDATE attempt PostgreSQL tarafindan reject edilir |
| Local dev/staging cookie SameSite=Lax fallback (HTTP) | Production HTTPS iken None+Secure; local HTTP development workflow'unu engellemez | EXPECTED: ASPNETCORE_ENVIRONMENT check at gateway cookie middleware |
| INMA non-conforming response normalization (Gateway tarafinda) | Contract drift'inde browser'a tutarli StandardErrorEnvelope donsun; INMA bagimsiz disinda | EXPECTED: gateway middleware INMA raw response'u wrap eder, log + Sentry alert |
| History fetch tenant-scoped path: `/api/v1/tenants/{tenant_id}/contacts/{visitor_key}/messages` | Cross-tenant lookup engelleme; JWT claim mismatch → 403 | EXPECTED: INMA enforce path-vs-claim match |
| webchat_inma_idempotency PK = (tenant_id, message_id) compound | Tenant isolation; message_id global namespace yerine per-tenant | EXPECTED: Postgres compound PK, tenant cross-leak imkansiz |

## 4. Pre-existing Patterns (Codex False Positive Onleyici)

| Pattern | Aciklama | Referans |
|---------|----------|----------|
| **INMA SQL Server READONLY** | Invekto, WapClient.Management MSSQL'i SADECE SELECT ile okur (lisans/sirket bilgisi). Bu microservice isolation kuralinin DISINDA tutulmus mevcut prod pattern'idir. | `arch/features/tenant-field-mapping.md:76` ("INMA READONLY lisans pattern") |
| **Mevcut callback bridge** `/api/v1/callback/wapcrm` | INMA-managed kanallardan giden tum mesajlar bu bridge uzerinden geciyor. Webchat de bunu kullanir, ayri bridge kurulmaz. | `arch/contracts/integration-callback.json` |
| **Service-to-service auth** | Invekto ↔ INMA arasi auth zaten tanimli; webchat kendi auth tanitmaz, mevcut pattern'i kullanir. | Mevcut WABA/IG entegrasyonu |

## 5. Contract References

| Contract | Dosya | Durum |
|----------|-------|-------|
| API Request/Response | `arch/contracts/webchat-inma-channel.json` | YENI — bu paket icinde olusturulur |
| DB Schema (source of truth) | `arch/db/webchat.sql` | Guncellendi — yeni tablolar (cutover_state, idempotency, outbox) DDL eklendi; eski `messages/conversations/visitors` deprecated |
| DB Schema Migration | `arch/db/migrations/047-webchat-inma-channel-cutover.sql` | YENI — Phase 1 tablolari + bridge fix gate constraint + GRANT ALL |
| Error Codes | `arch/errors.md` INV-WC-013 ... INV-WC-025 (13 yeni codes) | EKLENDI |
| Existing callback contract | `arch/contracts/integration-callback.json` | GUNCELLENDI — `channel` (optional), `visitor_key`, `sender_type`, `sender_name` OPTIONAL field'lar eklendi + conditional schema (channel=WEBCHAT iken visitor_key+sender_type required). Backward-compatible: existing WABA/IG callbacks bu field'lar olmadan calismaya devam eder. |

## 6. Scope Boundaries

### In Scope (Phase 1)
- Invekto.WebChat → Invekto.WebChat.Gateway rename (or namespace marker — kod refactor minimal)
- Inbound forwarder: browser → SignalR → INMA HTTP POST
- Outbound receiver: INMA callback → SignalR → browser
- Widget management API (CRUD, public contract, OpenAPI doc)
- Widget management UI (Invekto Dashboard — Phase 1)
- Cookie-based visitor identity + session resume (full UUID)
- Cookie SameSite=None + Secure (HTTPS-only)
- 30dk inaktivite auto-close (INMA-driven)
- History fetch endpoint client'i (returning visitor, son 50 mesaj)
- Eski webchat tablolarinin write-path'inin 7 gun sonra kapatilmasi
- Eski WebChatPage Dashboard sayfasinin tamamen silinmesi (T+7)
- INMA explicit widget sync endpoint client'i
- Standart error envelope + HTTP status mapping
- Webchat-specific callback regression test suite
- Allowed origins server-side validation
- Rate limit per IP (Invekto-side, abuse protection)

### Out of Scope (Explicit)
- INMA backend implementasyonu (INMA ekibi)
- Phase 2 UI migration (INMA UI'inin webchat sayfasi)
- File/attachment upload (Phase 2'ye birakildi)
- Typing indicator (Phase 2)
- Multi-device contact merge (anonim oldugu icin yapilamaz)
- WABA-style template system (carrier kisiti yok)
- Mevcut conversation backfill INMA'ya (Q karari: backfill yok)
- Email-based history recovery (Q karari: recovery yok)
- Phased per-tenant rollout (Q karari: 7 gun dual-write tum tenants)
- Bridge 906/907 parse bug fix (ayri paket: PKT-BRIDGE-906-907-FIX, ONKOSUL)

### Degismeyen Alanlar (Pre-existing)
- INMA WapCRM SQL Server (Invekto READONLY mevcut prod pattern, isolation kurali disinda)
- `/api/v1/callback/wapcrm` bridge'in temel auth/routing logic'i (sadece channel: WEBCHAT eklenir)
- Dashboard auth/JWT akisi
- Backend service auth (Invekto ↔ INMA arasindaki mevcut service auth)
- Operator routing (INMA'da, dokunulmaz)
- WABA/IG kanal davranislari

## 7. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| Invekto.WebChat.Gateway (rename) | Browser ↔ INMA gateway + widget config + outbox + Hangfire jobs | Major refactor — ConversationService kaldir; ekle: InboundForwarder, OutboundReceiver, WidgetSyncClient, OutboxWriter, INMAResponseNormalizer; 4 Hangfire job (idempotency cleanup, outbox reconciliation, widget sync retry, outbox archive) |
| Invekto.Backend | Widget management API proxy | Yeni endpoint'ler (widget CRUD), eski WebChat ops endpoint'leri kaldir |
| Invekto.Backend (Dashboard) | Widget management UI | Yeni sayfa: Widget Manager. Eski WebChatPage **tamamen sil** |
| Invekto.Automation | Webhook receiver | Phase 1'de kalir (Invekto-side flow), Phase 2'de INMA flow'a devir |
| Invekto.Shared | DTO updates | WebChatInboundDto, WebChatOutboundCallbackDto, WidgetConfigDto, WidgetSyncDto, StandardErrorEnvelope |
| INMA (External) | Yeni kanal tipi | 3 endpoint: inbound, outbound callback extend, history fetch + widget sync receiver |

## 8. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| INMA endpoint'leri gec gelir → Invekto bekleme | HIGH | Spec'i once teslim et, paralel calis. Stub mode ile dev devam etsin |
| Production cutover sirasinda visitor mesaj kaybi | MED | 7 gun dual-write window: eski + yeni path her iki yerde write. Rollback flag her an OFF'a cekilir |
| Returning visitor history INMA'ya uzak — latency sorunu | MED | History fetch sadece initial load'da. SignalR realtime kanal hizli kalir |
| Cookie clear → visitor "kaybeder" gecmisi | LOW | Documented behavior (Q karari). UI'da bilgilendirme: "Her ziyaret yeni sohbet" |
| Anonim visitor abuse / spam | MED | Rate limit per IP (Invekto-side, INMA'ya gitmesin), CAPTCHA opsiyonel Phase 1.5 |
| Widget allowed_origins yanlis konfigure → embed her yere yapilir | MED | Server-side origin validation (CORS + custom check), config UI'da dogrulama, INV-WC-018 |
| Mevcut callback bridge 906/907 parse bug'i webchat'i bozar | HIGH | **ONKOSUL:** PKT-BRIDGE-906-907-FIX paketi tamamlanmadan webchat cutover yapilmaz. Webchat-specific regression test suite |
| Phase 2'de INMA UI breaking change yapar | LOW | API-first design + versioned API endpoints (v1) |
| Multi-tenant widget config drift (Invekto'da olusur, INMA bilmez) | MED | Widget create/update'te INMA'ya **explicit sync hook** (tenant_id+widget_id mapping) |
| AI fallback kaldirilinca operator yokken visitor mesaji 'cevapsiz' kalirsa | LOW | INMA'nin kendi AI agent'i devrede; degilse "Operator'larimiz size en kisa surede donus yapacak" otomatik mesaji |
| external_id collision (kisaltilmis UUID nedeniyle) | LOW (artik) | Full UUID kullaniliyor (76 char) — collision matematiksel imkansiz |
| iframe SameSite cookie sorunu | LOW (artik) | SameSite=None+Secure HTTPS production zorunlu |
| Rollback safety (24h dual-write yetersiz olabilir) | LOW (artik) | 7 gun dual-write — rollback flag T+0 ile T+7 arasi her an OFF'a cekilebilir |

## 9. Visitor Identity Detail

### Cookie Schema
- Name: `wc_vid`
- Value: UUIDv4 (full, 36 char)
- TTL: 10 yil
- HttpOnly: false (client JS okur — widget bootstrap icin)
- **SameSite: None** (3rd-party iframe scenario zorunlu) — production
- **Secure: true** (HTTPS production zorunlu)
- Path: `/`

**Local Dev / Staging Override:**
- Eger `ASPNETCORE_ENVIRONMENT in (Development, Staging)` AND request scheme = HTTP:
  - SameSite: `Lax`
  - Secure: `false`
- Bu sadece HTTP localhost/staging icin gecerli; production HTTPS iken her zaman None+Secure
- Embed iframe testi yalnizca HTTPS staging/production'da yapilabilir (dokumanted)

### External ID Format
`wc-{widgetUuid}-{visitorUuid}`
- `widgetUuid`: widget UUID full (36 char with dashes)
- `visitorUuid`: visitor UUID full (36 char with dashes)
- Toplam length: 76 char (`wc-` + 36 + `-` + 36)
- Ornek: `wc-550e8400-e29b-41d4-a716-446655440100-660e8400-e29b-41d4-a716-446655440200`
- INMA contact tablosu `external_id` kolonu VARCHAR(128) gerek (mevcut WABA phone ~20 char, IG ~30 char, webchat 76 char)

### INMA Contact Mapping
- `channel_type = WEBCHAT` (yeni enum value)
- `external_id` = yukaridaki format (unique per channel)
- `display_name`: visitor verirse, yoksa "Web Visitor #{visitorUuid[0..8]}"
- `email`: opsiyonel enrichment (visitor verirse, INMA contact'inin email field'ina yazilir)

### Session Lifecycle
- Visitor browser'da `wc_vid` cookie YOK → yeni UUID generate, cookie set
- Visitor browser'da `wc_vid` cookie VAR → mevcut UUID kullanilir, INMA'ya `existing` flag gonderilir
- 30dk inaktivite → INMA conversation status = closed
- Ayni cookie sonradan tekrar mesaj atinca → ayni contact, yeni conversation
- Cookie clear edilirse → yeni visitor, yeni contact (recovery YOK — Q karari)

## 10. INMA Channel Contract (Endpoint Specifications)

### 10.1 Inbound — Visitor → INMA
```http
POST /api/v1/inbound/webchat
Authorization: <existing Invekto-INMA service auth>
Content-Type: application/json
X-Idempotency-Key: <message_id>

{
  "tenant_id": "string",
  "widget_id": "string (UUID)",
  "visitor_key": "wc-{widget_uuid}-{visitor_uuid}",
  "display_name": null | "string",
  "email": null | "string",
  "message_id": "uuid",
  "text": "string (1-4000 char)",
  "attachments": [],
  "locale": "tr-TR" | "en-US" | ...,
  "metadata": {
    "page_url": "string",
    "user_agent": "string",
    "referrer": "string"
  },
  "timestamp": "ISO8601"
}
```

**Naming Convention:** All field names use snake_case to match `arch/contracts/integration-callback.json` (existing Invekto-INMA contract convention).

**INMA davranisi:**
1. message_id ile dedupe (24h TTL) → varsa 200 OK `{deduplicated: true}` no-op
2. visitor_key ile contact upsert (yoksa olustur, varsa last_seen guncelle)
3. Aktif conversation var mi (last_message_at < 30dk)? Yoksa yeni conversation ac
4. Mesaji conversation'a ekle
5. Operator queue dispatch (mevcut routing logic)
6. Flow tetikle (varsa)

**Response (Success):**
```json
{
  "ok": true,
  "conversation_id": "string",
  "deduplicated": false,
  "timestamp": "ISO8601"
}
```

**Response (Failure) — Standard Envelope:**
```json
{
  "ok": false,
  "error": {
    "code": "INV-WC-NNN",
    "message": "User-facing message (Turkish)",
    "details": "Technical detail (English, optional)",
    "request_id": "uuid"
  }
}
```

**HTTP Status Mapping:**
- 200: Success (yeni mesaj veya deduplicated)
- 400: Invalid payload (INV-WC-006)
- 401: Auth failure (INV-WC-023)
- 403: Origin not allowed (INV-WC-018) / Tenant mismatch (INV-WC-014)
- 404: Widget not found (INV-WC-015)
- 429: Rate limit (INV-WC-019)
- 500: INMA internal (INV-WC-016) / Forwarding failed (INV-WC-013)
- 502: INMA unreachable (INV-WC-017)
- 503: Bridge fix gate not verified (INV-WC-024)

**INMA Response Normalization (Gateway Responsibility):**
- Eger INMA non-conforming response donerse (StandardErrorEnvelope formatina uymayan), gateway:
  1. INMA'nin raw response'unu log'lar
  2. Client'a StandardErrorEnvelope ile wrap edilmis yanit doner: `INV-WC-016` veya `INV-WC-017`
  3. Sentry alert + INMA ekibine bildirim (contract drift)
- Bu sayede browser her zaman tutarli format alir

### 10.2 Outbound — INMA → Browser (mevcut callback extend)
Mevcut `/api/v1/callback/wapcrm` payload'una `channel` field eklenir:
```json
{
  "channel": "WEBCHAT",
  "tenant_id": "...",
  "visitor_key": "wc-...",
  "message_id": "uuid",
  "text": "string",
  "sender_type": "operator" | "ai",
  "sender_name": "string",
  "timestamp": "ISO8601"
}
```

WebChat Gateway servisi davranisi:
1. Callback'i al
2. visitor_key'den SignalR group'unu bul (`conv_{visitor_key}`)
3. `Clients.Group(...).SendAsync("ReceiveMessage", payload)`
4. Browser render eder
5. Parse hatasi → INV-WC-020 + alarm

### 10.3 History Fetch — Returning Visitor (Tenant-scoped)
```http
GET /api/v1/tenants/{tenant_id}/contacts/{visitor_key}/messages?limit=50&before={message_id}
Authorization: <service auth — JWT MUST contain tenant_id claim>
```

**Tenant Isolation:** INMA path'teki tenant_id'yi JWT claim'i ile karsilastirir. Mismatch → 403 INV-WC-014. Bu cross-tenant lookup'i engeller.

**Response (Success):**
```json
{
  "ok": true,
  "messages": [
    {
      "message_id": "uuid",
      "text": "string",
      "sender_type": "visitor" | "operator" | "ai",
      "timestamp": "ISO8601"
    }
  ],
  "has_more": false
}
```

### 10.4 Widget Sync — Invekto → INMA
```http
POST /api/v1/sync/webchat-widget
Authorization: <service auth>
{
  "tenant_id": "string",
  "widget_id": "string (UUID)",
  "display_name": "string",
  "primary_locale": "tr-TR",
  "allowed_origins": ["https://acme.com"],
  "is_active": true,
  "operation": "create" | "update" | "deactivate"
}
```

INMA davranisi: widget metadata'sini kendi tablosunda upsert eder. Sonradan inbound geldiginde widget biliniyor olur.

**Failure Behavior (INMA Unreachable) — Latest-State Pattern (NOT Outbox):**
- Dashboard widget create/update tarafi local DB'ye basarili yazilir (transaction commit)
- Sync call fail ederse `webchat_widget_configs.inma_synced_at = NULL` + `inma_sync_error` doldurulur
- Hangfire scheduled job `WebChatWidgetSyncRetryJob` (saatlik): `WHERE inma_synced_at IS NULL` retry eder
- Widget yaratan kullanici "Widget olusturuldu, INMA senkronizasyonu beklemede" mesaji alir (INV-WC-022)
- Inbound mesaj gelirse ve widget INMA'da yoksa → INV-WC-015 (widget not found, retry geciktir)

**Onemli ayrim:** Widget sync **outbox kullanmaz**, latest-state pattern ile çalışır. Ardarda 5 widget update yapilirsa, retry her zaman **son state'i** push eder (5 outbox row biriktirilmez). webchat_outbox tablosu **sadece visitor mesajlarinin** dual-write reconciliation'i icin (per-message correlation gerek).

## 11. Migration & Cutover Strategy

### Cutover Plan (7-day dual-write window — Q karari, Outbox Pattern)
1. **T-14 gun:** INMA endpoint'leri ready, staging'de test
2. **T-7 gun:** PKT-BRIDGE-906-907-FIX paketi production deploy + verify
   - Verification: SQL `UPDATE webchat_cutover_state SET bridge_fix_verified=TRUE, bridge_fix_verified_at=NOW()` (manuel + automated regression test PASS sonrasi)
3. **T-3 gun:** Invekto WebChat Gateway code production'a deploy (feature flag OFF)
   - Boot-time gate: gateway startup'inda DB query — `feature_flag_enabled=TRUE` ise `bridge_fix_verified=TRUE` zorunlu (CHECK constraint enforcement)
4. **T-3 gun:** Mevcut widget'lar INMA'ya explicit sync (one-time backfill of widget metadata, NOT messages)
5. **T-0 (cutover):** `UPDATE webchat_cutover_state SET feature_flag_enabled=TRUE`. **7-day dual-write window basliyor:**
   - Yeni visitor mesajlari Outbox Pattern ile yazilir:
     1. Mesaj `webchat_outbox` tablosuna `inma_status=pending, legacy_status=pending` ile insert
     2. Hangfire reconciliation cron (her 5dk):
        - Pending entries'i fetch (`retry_count <= 5 AND next_retry_at <= NOW()`)
        - Her birini hem INMA inbound POST hem legacy webchat_messages INSERT'e push
        - Her path icin status update (sent/failed)
        - Her iki path de `sent` ise outbox row 24h sonra cleanup
        - Pending count > 100 → INV-WC-025 alert (Sentry)
   - Outbound: INMA callback aktif, eski WebChat dashboard kapali
   - Read path: yeni path (INMA history endpoint)
6. **T+7 gun:** Eski webchat_messages tablosu **read-only mode** (`legacy_readonly_at=NOW()`)
   - Trigger eklenir: legacy table'a INSERT/UPDATE/DELETE error fırlatır
7. **T+8 gun:** Eski WebChatPage Dashboard route'u sil (code change, separate PR)
8. **T+30 gun:** Eski tablolar deprecated stamp, reporting'e tasi (eger gerekirse)
9. **T+90 gun:** Eski tablolar drop (separate migration, eger reporting kullanmiyor)

### Background Jobs (Hangfire — Q karari)
| Job | Frekans | Amac |
|-----|---------|------|
| `WebChatIdempotencyCleanupJob` | Saatlik | `DELETE FROM webchat_inma_idempotency WHERE forwarded_at < NOW() - INTERVAL '24 hours'` |
| `WebChatOutboxReconciliationJob` | 5dk | Outbox pending entries retry, alert if pending>100 |
| `WebChatWidgetSyncRetryJob` | Saatlik | `webchat_widget_configs WHERE inma_synced_at IS NULL` retry |
| `WebChatOutboxArchiveJob` | Gunluk | Outbox row'lari (her iki path sent + 24h gecmis) sil |

### Reconciliation Algorithm Detail (Outbox Pattern, message-only)
- **Scope:** Sadece visitor mesajlari icin dual-write reconciliation. Widget sync FARKLI mekanizma (latest-state, ayri job).
- **Per-message correlation:** Outbox row'unun `message_id` her iki path'te de log'da tracked
- **Retry scheduling:** `next_retry_at` field schema'da var (migration 047). Hangfire 5dk cron predicate:
  ```sql
  SELECT * FROM webchat_outbox
   WHERE (inma_status = 'pending' OR legacy_status = 'pending')
     AND retry_count <= 5
     AND next_retry_at <= NOW()
  ```
- **Exponential backoff (retry_count → backoff):**
  - retry_count=0 (initial enqueue) → next_retry_at = NOW (immediate first attempt)
  - retry_count=1 (1st retry after 1st fail) → next_retry_at = NOW + 1 dk
  - retry_count=2 → next_retry_at = NOW + 2 dk
  - retry_count=3 → next_retry_at = NOW + 5 dk
  - retry_count=4 → next_retry_at = NOW + 15 dk
  - retry_count=5 (final retry) → next_retry_at = NOW + 60 dk
- **Final transition (retry_count=5 attempt fails):**
  - Hangfire job ATOMICALLY: `UPDATE webchat_outbox SET inma_status='failed' (or legacy_status='failed'), retry_count=6, last_error=... WHERE id=...`
  - retry_count=6 = terminal state, **NEVER retried again** (predicate `retry_count <= 5` excludes)
  - INV-WC-025 alert fired (Sentry critical)
  - Manuel mudahale gerek (DBA query, replay or write-off)
- **Important invariant:** Pending rows can only have `retry_count` 0-5. retry_count=6 implies status='failed'. Hangfire job MUST set status='failed' atomically when incrementing retry_count from 5 to 6 — never leave row pending+retry_count=6 (silent stuck state).
- **Total fail-safe window:** 1+2+5+15+60 = 83 dk cumulative backoff before terminal failure
- **Alert thresholds:**
  - Outbox pending count > 100 → INV-WC-025 (Sentry warning)
  - Failed status count > 10 / saat → critical alert
  - INMA unreachable > 5 dk → operational page

### Backfill?
**Karar (Q): BACKFILL YOK.** Eski conversations Invekto'da kalir (T+90 gun sonra drop). INMA'ya tasinmaz. Eski WebChatPage Dashboard SILINIR (history viewer dahil), history kaybi kabul edilir.

### Rollback Plan
- T-0 ile T+7 arasi (dual-write window) → feature flag OFF: yeni mesajlar tekrar eski path'e yazilir, INMA dispatch durur. **SAFE.**
- T+7 sonrasi (eski path read-only) → rollback degil, full migration olarak kabul edilir. Forward-fix only.

## 11.5 Pre-Existing Error Codes (Not in This Diff)

Spec ve contract `INV-WC-006 (Invalid payload)` koduna referans verir. Bu kod **arch/errors.md:637'de zaten mevcuttur** (pre-existing). Bu paket diff'inde sadece YENI kodlar (013-025) ekleniyor.

```yaml
# arch/errors.md:637 (mevcut)
- code: INV-WC-006
  description: Invalid payload
  user_message: Geçersiz istek verisi.
```

Codex review icin not: bu kod diff'te eklenmedigi icin "missing" gibi gozukebilir, ancak repository'de pre-existing.

## 11.6 Operational Sizing (11 Production Tenants)

### Assumption Tablosu

| Metrik | Steady State | Peak (5x) | Notlar |
|--------|--------------|-----------|--------|
| Tenant count | 11 | 11 | Production sabit |
| Avg messages/day/tenant | ~50 | ~250 | Conservative tahmin |
| Total messages/day | ~550 | ~2,750 | All tenants combined |
| Avg messages/hour | ~23 | ~115 | 550/24 ≈ 23, 2750/24 ≈ 115 |

### Tablo Boyutu Tahminleri

| Tablo | Steady Rows | Peak Rows | Disk | TTL |
|-------|-------------|-----------|------|-----|
| webchat_inma_idempotency | ~550 | ~2,750 | <1 MB | 24h, Hangfire saatlik cleanup |
| webchat_outbox (active) | <100 | <500 | <5 MB | 24h after both paths sent |
| webchat_widget_configs | ~30-50 | ~50 | <100 KB | Persistent |

### Hangfire Job Loadlari

| Job | Frekans | Avg Work | Peak Work |
|-----|---------|----------|-----------|
| WebChatIdempotencyCleanupJob | Saatlik | ~23 row DELETE | ~115 row DELETE |
| WebChatOutboxReconciliationJob | 5dk | <20 retry attempts | <100 retry attempts |
| WebChatWidgetSyncRetryJob | Saatlik | <5 retries | <10 retries |
| WebChatOutboxArchiveJob | Gunluk | ~500 archive DELETE | ~2,500 archive DELETE |

### Scaling Headroom

Tasarim 100 tenant × 1000 messages/day = 100k messages/day icin sufficient. Postgres single-node yeterli. Phase 1'de partitioning gerekmez. Phase 3'te (eger growth) range partitioning by day eklenebilir.

## 12. Open Questions (INMA'ya Sorulacak)

| # | Soru | Karar Sahibi |
|---|------|--------------|
| 1 | WEBCHAT enum eklenebilir mi, hangi degerin alti dolu? (mevcut enum listesi) | INMA |
| 2 | Service auth: mevcut JWT (tenant_id claim icermeli — history endpoint icin) onaylaniyor mu? | INMA |
| 3 | Idempotency window: 24 saat (Invekto onerisi) onaylaniyor mu? | INMA |
| 4 | Operator queue: webchat-specific queue mi, yoksa karisik (WABA+IG+WC) queue mi? | INMA |
| 5 | INMA contact tablosu external_id kolonu min VARCHAR(128) mi? (webchat 76 char) | INMA |
| 6 | Widget sync endpoint format Invekto onerisi onaylaniyor mu? | INMA |
