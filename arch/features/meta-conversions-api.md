# SPEC: Meta Conversions API (CAPI) — Server-Side Event Tracking

> **Spec ID:** FEAT-META-CAPI | **Paket:** TBD | **Risk:** MEDIUM (PII handling + per-tenant token)
> **Yazar:** Q + Claude planlama 2026-04-29 | **Son Guncelleme:** 2026-04-29 14:30 UTC (Q kararlari TUM 5/5 final + Pixel/Token provision DONE) | **Durum:** DRAFT (chunk A interview gate icin hazir)

## 0. Q Kararlari (2026-04-29 — 5/5 final)

| # | Karar | Detay |
|---|-------|-------|
| Q1 | **Multi-tenant generic kod, Dent ilk pilot** | Tum sistem icin `tenant_settings.meta_capi_config` per-tenant; Chunk E pilot smoke Dent Adavista ile, sonraki tenant'lar tier-gated rollout |
| Q2 | **Prod pixel + `test_event_code`** (Q karari 2026-04-29 14:30 UTC kabul) | Tek BM/Pixel maintenance; Meta Test Events paneli zaten bu use-case icin; test asamasinda dispatch'lerde `test_event_code` header eklenir, Meta Test Events panelinde gozulur, prod metrics etkilenmez. Final karar. |
| Q3 | **Token expiry warning kanali = Dashboard alert** | Hangfire daily check job → expiry < 7 gun ise tenant Dashboard banner; mevcut `tenant_alerts`/`notifications` altyapisi audit gerek (bulunmazsa minimal table eklenecek) |
| Q4 | **Schedule hook = ikisi de (cift kanal)** | (a) Appointments service icinden direkt randevu olusumunda; (b) Lead pipeline'da `appointment_booked` state'ine gecince. event_id deterministic SHA256(tenant+lead+appointment_id) → Meta 7-gun dedup penceresi her iki yolu da emer |
| Q5 | **consent=false → hard reject** | Marketing dispatcher'da gate: `consent_marketing != true` → CAPI dispatch ATLA + audit log "skipped_consent" + Dashboard counter. KVKK/GDPR uyum guvencesi. FEAT-META-FULL-INTAKE consent_marketing field'i otoritedir. |

## 1. Intent (Ne & Neden)

**Sorun:** Reklam veren Invekto musterileri ($10k/ay toplam spend) **donusum sinyalini Meta'ya geri vermiyor**. Lead Ads'ten gelen lead Invekto'da satisa donusunce, Meta bu sinyali sadece browser-side Pixel ile (eksik EMQ + iOS 14.5+ Limit Track Ad) yakalayabiliyor. Sonuc: reklam optimizasyonu zayif → ROAS dusuk.

**Cozum:** Invekto **server-side** olarak `Lead`, `Schedule`, `Purchase`, `CompleteRegistration` event'lerini Meta Pixel/Dataset'e gonderiyor. EMQ icin hashed PII (email, phone) + FBC/FBP cookie + IP/UA. Browser Pixel ile **deduplication** (ayni event_id).

**Beklenen ROI:** $10k/ay spend uzerinde **%10-15 efficiency artisi** = $1-1.5k/ay tasarruf. Musteri Invekto raporunda "FB attributed sales = X" gorur — somut ROI argumanı.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | `tenant_settings.meta_capi_config JSONB` (pixel_id + dataset_id + access_token_encrypted + test_event_code) | DB INSERT/UPDATE + UNIQUE per-tenant |
| AC-2 | `IMetaCapiClient.SendEventAsync(tenantId, MetaCapiEvent)` server-side POST `/v21.0/{pixel-id}/events` | Mock + integration test |
| AC-3 | EMQ asgari alan seti: `em` (email SHA256), `ph` (phone SHA256), `fbc` (cookie), `fbp` (cookie), `client_ip_address`, `client_user_agent`, `external_id` (lead_id hash) | Hash dogrulama unit test |
| AC-4 | Event tipleri: `Lead` (lead create), `Schedule` (Appointments service hook **+** Lead pipeline `appointment_booked` state hook — cift kanal, deterministic event_id ile Meta 7-gun dedup), `Purchase` (deal close), `CompleteRegistration` (signup) | Per-event mapping integration test + dedup race test |
| AC-4b | **Consent gate (hard reject):** `consent_marketing != true` → dispatch ATLA + audit `skipped_consent` + Dashboard counter | Negative test (consent=false event) + audit row check |
| AC-4c | **Token expiry warning:** Hangfire daily check + Dashboard banner < 7 gun + INV-CAPI-TOKEN-EXPIRY alert | E2E: token TTL simulasyonu + Dashboard render verify |
| AC-5 | Browser Pixel + Server CAPI deduplication: `event_id` GUID payload her iki kanalda ayni gonderilir | Deduplication audit log |
| AC-6 | Dashboard `/settings/meta-capi` — pixel_id + access_token gir, test_event_code ile Meta'da "Test Events" tab'inda dogrulama | Manuel UI smoke + Meta UI verify |
| AC-7 | Hangfire `meta_capi_send` queue: per-event idempotent (`UNIQUE(tenant_id, event_id)`), retry=2, backoff exponential, dead-letter table on permanent fail | Hangfire dashboard verify + dead_letter row |
| AC-8 | Per-tenant rate limit guard (Meta hesap basi BUC) — 429 response durumda exponential backoff + Q'ya alert (INV-META-CAPI-RATE) | Sentry/log alert |
| AC-9 | PII redaction: log icine **asla** raw email/phone yazma; sadece SHA256 prefix ilk 8 char | Audit grep |
| AC-10 | Audit table `meta_capi_event_log`: every dispatch + Meta `events_received` response code + EMQ score (Meta donusu varsa) | DB trace |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Server-side direkt Invekto → Meta (INMA bypass) | Donusum Invekto'da olusur (deal close, appointment book), INMA inbox'ta degil. INMA bridging unnecessary hop. | Microservice isolation OK |
| Per-tenant token (Business Integration System User Token) | Verified Tech Provider olarak Embedded Signup'tan alinabilir; suresiz | — |
| AES-GCM encryption at rest + in-memory decrypt only | INMA chat token saklamada da ayni pattern (FEAT-LIW lessons) | EXPECTED: CQ4 PII security |
| Hangfire queue (sync API call yerine async) | API failure'da retry, response time'i bekleten user yok | EXPECTED: CQ12 reliability |
| `event_id = GUID` Invekto-side generate | Browser Pixel ile dedup, ayni event_id her iki kanalda | — |
| `external_id` = SHA256(tenant_id + lead_id) | Cross-event same-customer matching, raw lead_id leak yok | EXPECTED: CQ4 PII |
| Yeni servis YOK — Marketing servisinde (:7112) | EFS pattern: Marketing zaten ad/marketing logic icin, peer | CQ9: ok |
| Canonical event taxonomy `MetaCapiEventType.cs` enum | InvalidEventName + Meta'nin `Lead`/`Schedule`/`Purchase` standard set'i 1:1 | — |
| Schedule cift hook (Appointments + Lead pipeline) Q kararı | Appointments service direkt user randevu olusumu; Lead pipeline `appointment_booked` ise CRM-level dispatch (manuel agent ekledigi randevu, otomasyon, ya da inbound webhook) | EXPECTED: CQ12 race-safe — deterministic event_id SHA256(tenant+lead+appointment_id) Meta 7-gun dedup |
| consent=false hard reject + skipped_consent audit Q kararı | KVKK/GDPR risk profili dusuk tut — anonymized event yerine sifir gonderim (Meta'nin EMQ optimizasyonu zaten consent'i variden iyi calisir, anonymized eklemek bug surface) | EXPECTED: CQ4 PII security |
| Token expiry Dashboard alert (Q kararı) | Email/log/sentry yerine tenant'in kendisi Dashboard'da goruyor → operasyonel gecikme yok, Q tarafinda manuel takip yuku yok | — |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Tenant Settings API | `arch/contracts/tenant-settings.json` (additive `meta_capi_config`) |
| DB Schema | `arch/db/marketing.sql` (`meta_capi_event_log` + `meta_capi_dead_letter` tables) |
| Shared DTO | `Invekto.Shared/Contracts/MetaCapi/MetaCapiEvent.cs` (yeni) + `MetaCapiUserData.cs` + `MetaCapiCustomData.cs` + `MetaCapiEventType.cs` enum |
| Marketing Internal API | `POST /api/internal/meta-capi/dispatch` (X-Internal-Service-Token) |
| Backend Proxy | `GET/PUT /api/v1/tenant-settings/meta-capi-config` (jwt) |
| Error Codes | INV-META-CAPI-001..010 (sonra atanacak; INV-META-006 Graph API auth fail Meta Leadgen Webhook ile cakismaz, namespace MARK-CAPI-* tercih edilebilir — error code pre-flight) |

## 5. Scope Boundaries

### In Scope
- `tenant_settings.meta_capi_config JSONB` migration (pixel_id, dataset_id, access_token_encrypted, test_event_code, enabled)
- 4 event tipi (Lead, Schedule, Purchase, CompleteRegistration)
- Hangfire queue + retry + dead-letter + idempotency
- Dashboard `/settings/meta-capi` editor (config + test event button)
- Audit log + EMQ score capture
- Browser Pixel deduplication contract (event_id propagation Dashboard SPA tarafinda da emit)

### Out of Scope (Explicit)
- Browser-side Pixel kurulum (musterinin web sitesine gomulen Pixel — onlarin sorumlulugu, Invekto sadece event_id paylasir)
- Custom Audiences API (CAPI degil — Marketing API katmaninda, ayri paket)
- Offline Conversions classic API (deprecated, CAPI ile birlestirildi)
- Multi-pixel per tenant (tek pixel/dataset MVP)
- Multi-step funnel attribution (Lead → Qualified → Purchase chain — backlog)

### Degismeyen Alanlar (Pre-existing)
- Lead intake (FEAT-LIW, FEAT-META-FULL-INTAKE)
- Pipeline/sync (FEAT-PIPELINE)
- INMA chatoperation bridge (CAPI INMA bypass eder, single bridge memory geçerli)
- Existing Marketing servisi feature'lari (FEAT-EFS, FEAT-MCC)

## 6. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| Backend (5000) | Proxy + JWT gate `/api/v1/tenant-settings/meta-capi-config` + token expiry alert source (Dashboard banner) + event emit hooks (Lead create, Deal close) | Yeni endpoint + 2 hook point + alerts query |
| Marketing (7112) | CAPI dispatcher + Hangfire queue + Meta Graph API client + dead-letter + **consent_marketing hard reject gate** + token expiry daily check job | Yeni servis yok, Marketing'e modul ekle |
| Automation (7108) | Lead create event hook + **Lead pipeline `appointment_booked` state hook (Schedule event kanali 1)** → Marketing internal API call | Yeni 2 hook (LeadStatusOrchestrator extension) |
| Appointments (7102) | **Schedule event hook (kanal 2 — randevu olusumunda direkt)** | Yeni hook |
| Dashboard SPA | `/settings/meta-capi` editor + browser Pixel event_id emit hook + **token expiry banner** + skipped_consent counter widget | Yeni page + util + alert UI |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| Token sizinti (production logs/git) | MED | AES-GCM at-rest + redaction lesson kurali (FEAT-LIW pattern) + secret-scan hook |
| Meta rate limit (BUC) | MED | Exponential backoff + per-tenant queue + 429 alert + dead-letter |
| Browser Pixel/CAPI duplicate (dedup fail) | MED | event_id GUID + dedup window 7 gun (Meta default), Dashboard SPA emit standardize |
| EMQ score dusuk (PII eksik) | LOW | em+ph+fbc+fbp+ip+ua minimum set zorunlu; Dashboard'da EMQ score gosterimi (musteri gorur, eksik alani tamamlar) |
| Yanlis event mapping (Lead/Purchase karisir) | LOW | Enum + per-event integration test + audit log |
| Tenant token expire (system user yenilenmeli) | LOW | 60-gun once warning job (Hangfire daily check) |
| GDPR/KVKK (PII Meta'ya gonderim onayi) | MED | `consent_marketing` zaten FEAT-META-FULL-INTAKE'te yakalandi → CAPI sadece consent=true ise gonderir; gate Marketing dispatcher'da |

## 8. Pre-Flight Checks (Codex Pre-Implementation)

- [ ] **Error code namespace:** `arch/errors.md` grep — `INV-META-*` veya `INV-MARK-CAPI-*` ne uygun (INV-META-001..006 zaten Meta Leadgen Webhook icin atandi)
- [ ] **Migration numarasi:** son `034-feat-photo-request-flow.sql`; bu paket icin **035** veya FEAT-PIPELINE sonrasi
- [ ] **Shared contracts existing audit:** `Invekto.Shared/Contracts/Meta*` — Meta Leadgen DTO'lari var, naming collision yok
- [ ] **Marketing servisi mevcut Hangfire queues:** EFS (`marketing-followup`) + yeni `meta-capi-dispatch` ekle, `Marketing.csproj` dependency mirror (Backend.csproj G7 SCHEDULER HOST EXCEPTION pattern: PrivateAssets="all")
- [ ] **Pixel/Dataset Q provision:** Business Manager → Events Manager → Pixel olustur (test pixel + prod pixel ayri); Q manuel adim (B0 FEAT-VCP OAuth pattern)
- [ ] **System User Token:** Verified Tech Provider akisinda generate; Embedded Signup degil, dogrudan BM → System Users → Generate Token (`ads_management`, `business_management`)

## 9. Stage Plan (Chunk Breakdown)

| Chunk | Scope | Risk |
|-------|-------|------|
| A | Shared DTO + Marketing IMetaCapiClient interface + MockMetaCapiClient (provider contract pattern, FEAT-VCP A precedent) | LOW |
| B | Marketing real ProdMetaCapiClient + Hangfire queue + dead-letter + retry | MED |
| C | Migration + Backend tenant-settings endpoint + 3 hook point (Lead/Schedule/Purchase) + audit table | MED |
| D | Dashboard SPA `/settings/meta-capi` editor + test event button + Pixel event_id emit util | LOW |
| E | Pilot smoke (test pixel) + production migration + first tenant rollout (Dent Adavista oneri) | MED |

**Toplam tahmin:** 4-6 session (FEAT-EFS / FEAT-MCC complexity benzeri)
