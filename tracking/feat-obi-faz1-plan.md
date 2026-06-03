<!-- Status: PHASE 0 IMPLEMENTED (Codex PASS iter9) | 2026-06-03 -->
<!-- IMPLEMENTED 2026-06-03: Backend Phase 0 CSV bulk send. Migration 051 (bulk_send_jobs + bulk_send_recipients snapshot), Outbound endpoints POST /api/v1/bulk-send/preview + /confirm + GET /{campaignId}/status, INV-OB-039..045. Codex /rev PASS (12/12 CQ + 6/6 CoVe). Build clean. NOT YET DEPLOYED — feature flag Enabled=false (fail-safe). DEPLOY TODO: (1) run migration 051 on prod, (2) appsettings.Production.json BulkSend:Enabled=true + AllowedTenantIds=[DentAdavista tenant_id] + cap kademeli (5→50→100), (3) publish+restart Outbound, (4) operator CSV smoke 5-10 kişi. UI yok (Phase 0 admin endpoint; operatör Swagger/curl). Phase 1 = INMA MSSQL pull + Dashboard UI. -->

<!-- Q KARARLARI (2026-06-03): Phase 0 gated pilot (UI yok) · Kaynak = CSV (MSSQL/INMA pull Phase 1'e ertelendi) · Template VAR onaylı · Gönderim tamamen INMA bridge üzerinden. MSSQL adapter + INMA-onay blokeri KALKTI. Codex güvenlik railları (snapshot/dedup/hard-cap/chunking/idempotency/compliance) AYNEN geçerli. -->
<!-- ⚠️ §2-§3 MSSQL adapter kısımları Phase 1'e ertelendi; bugünkü implementasyon §6.4 Phase 0 + CSV. -->

# FEAT-OBI Faz 1 — Bulk Mesaj (INMA Source) Implementation Plan

> **Tarih:** 2026-06-03
> **Hedef:** Pilot klinikler bugün/yarın gerçek bulk WhatsApp gönderimi yapabilsin (production-ready)
> **Q kararı:** Tam Faz 1 kapsam · Alıcı kaynağı = INMA contacts · Kullanım = pilot klinikler hemen
> **Spec kaynağı:** [feat-obi-bulk-external.md](feat-obi-bulk-external.md)

---

## 1. Mevcut Durum (DOĞRULANDI — kod okundu)

### ✅ Hazır (deployed, port 7107 Outbound)
| Bileşen | Dosya | Durum |
|---------|-------|-------|
| `POST /api/v1/broadcast/send` (max 1000 recipient, opt-out filter, async 202) | `Invekto.Outbound/Program.cs:197` | DEPLOYED |
| `GET /api/v1/broadcast/{id}/status` | `Program.cs:244` | DEPLOYED |
| BroadcastOrchestrator (consent + template validation) | `Services/BroadcastOrchestrator.cs` | DEPLOYED |
| MessageSenderService → INMA bridge (marketing kategori + opt-out double-guard) | `Services/MessageSenderService.cs` | DEPLOYED |
| DB: `outbound_broadcasts`, `outbound_messages`, `outbound_templates` | `arch/db/outbound.sql` | DEPLOYED |

**Send tarafı production-grade.** Mesaj `MessageSenderService` → `MainAppCallbackClient` → INMA `/api/v1/callback/wapcrm` köprüsü üzerinden, `MessageCategory="marketing"` ile gidiyor; INMA server-side opt-out (906/907) uyguluyor.

### ❌ Eksik
- Alıcı listesini INMA'dan **otomatik çekme** (caller manuel array hazırlamak zorunda)
- `bulk_send_jobs` tablosu (DB'de YOK — doğrulandı)
- Dashboard `/outbound/bulk-send` UI (hiç yok)
- Plan-tier volume quota

---

## 2. KRİTİK MİMARİ KARAR — Alıcı çekme yolu

**INMA REST API'sinde toplu contact-pull endpoint'i YOK** (doğrulandı: `wapcrm-marketing-api.md` + `wapcrmapidoc.txt` tüm endpoint'ler tarandı — sadece `chatoperation`, `messagelistforphone` (tek telefon), `conversations`, `dynamicfields`, `optout` var). Mevcut `WapCrmClient` da sadece tek-telefon `messagelistforphone` çağırıyor.

**ÇÖZÜM: Read-only MSSQL (per-tenant WaClient DB).** `customer-mssql` erişimi doğrulandı:
- `WaClient.Management.Companies` köprü tablosu (DOĞRULANDI — schema okundu):
  - `InvektoCompanyCode` → Invekto `tenant_id` eşlemesi
  - `DatabaseName` + `DatabaseServerID` (→ `Servers`) → tenant'ın contact DB adı/sunucusu
  - `ApiKey` → o tenant'ın `X-CIB-SecretKey`'i
  - `MessageLimitPerDaily` / `MessageLimitPerMinute` → INMA'nın kendi rate limiti
  - `ApiStatus` → API aktif mi
- Per-tenant DB (örn. `WaClientDentAdavista`): `Customers` (9196), `CustomerPhones` (8460), `DataLists` (1), `CustomFields` (10) — DOĞRULANDI.

**Akış:** `tenant_id` → Companies'ten DatabaseName/ApiKey çek → `WaClient<X>.CustomerPhones ⋈ Customers` segment filtresiyle telefonları çek → E.164 normalize → mevcut `/broadcast/send`'e POST → INMA bridge gönderir.

**Kişiselleştirme:** Mesaj `{{name}}`, `{{cf1}}` vb. placeholder'lar INMA tarafında `DynamicMessage=true` ile per-recipient çözülüyor (FEAT-DMP zaten forward ediyor). Yani Invekto sadece **telefon listesi + template metni + alan listesi** göndermeli; isim/CF çekmeye gerek yok — segment filtresi için yine de SQL gerekli.

### ⚠️ Q ONAYI GEREKEN NOKTALAR (yeni entegrasyon yüzeyi)
1. **Production Outbound servisi → INMA MSSQL read-only bağlantısı** açılacak. Bugüne kadar Invekto INMA'ya sadece REST + bridge ile bağlanıyordu. Yeni: salt-okunur SQL connection string (Outbound `appsettings.Production.json`). INMA tarafı network/credential onayı?
2. **Segment granülaritesi:** Pilot'ta `DataLists` sadece 1 satır. Segment = (a) tüm contacts, (b) DataList seç, (c) CustomField filtresi (10 tanımlı CF). Faz 1'de hangileri?
3. **Volume/rate:** `Companies.MessageLimitPerDaily` INMA limiti zaten var; Invekto plan-tier cap'i bunun üstüne mi (daha düşük) yoksa atlanacak mı?

---

## 3. Build Planı (sıralı, kritik yol)

### Adım 1 — DB migration (`bulk_send_jobs`)
- `arch/db/migrations/0XX-bulk-send-jobs.sql` (spec §Veri Modeli'ndeki şema)
- snake_case, tenant_id scoped, `query_hash` idempotency unique index `(tenant_id, source, query_hash, scheduled_at)` 24h
- rollout skill ile multi-tenant uyumlu (tek shared Postgres)

### Adım 2 — INMA Contact Pull adapter (Outbound)
- `Invekto.Outbound/Services/InmaContactPullService.cs`
  - `ResolveTenantDbAsync(tenantId)` → Companies sorgusu (DatabaseName, ApiKey, MsgLimit)
  - `PullRecipientsAsync(tenantId, segmentFilter)` → `WaClient<X>` SELECT (CustomerPhones ⋈ Customers), READONLY
  - E.164 normalize (mevcut util varsa kullan), dedup, geçersiz no filtrele
  - Volume cap + `MessageLimitPerDaily` guard
- READONLY MSSQL connection string + tenant→server mapping (Servers tablosu) config
- **İzolasyon:** SQL erişimi sadece bu servis; Shared'a INMA SQL client koyma (mikroservis izolasyonu)

### Adım 3 — Bulk Send Orchestrator (Outbound)
- `Invekto.Outbound/Services/BulkSendOrchestrator.cs`
  - `POST /api/v1/bulk-send` → bulk_send_jobs (status=fetching) → pull → normalize → opt-out → internal `/broadcast/send` → broadcast_id kaydet → status geçişleri (fetching→sending→completed/failed)
  - `POST /api/v1/bulk-send/preview` → ilk 10 recipient + total count (gönderim YOK)
  - `GET /api/v1/bulk-send/{id}/status` → job + broadcast birleşik durum
  - Idempotency: query_hash 24h cache
- Contract: `arch/contracts/bulk-send.json` (yeni)
- Error codes: `arch/errors.md`'ye INV-OBI-xxx ekle

### Adım 4 — Backend proxy + plan permission
- Backend → Outbound proxy route'ları (`/api/ops/bulk-send/*` mevcut proxy pattern)
- Plan-tier gate (FAZ1-1 permission sistemi) — bulk yetkisi + volume cap

### Adım 5 — Dashboard UI `/outbound/bulk-send`
- `pages/outbound/BulkSendPage.tsx` (yeni)
  - Source picker: **INMA** aktif (CSV/Zoho disabled "yakında")
  - Segment builder: DataList seç + CF filtresi (basit)
  - Template seç + `/api/dynamicfields` ile placeholder map
  - Preview paneli (ilk 10 + total)
  - Schedule (şimdi / scheduled_at)
  - Gönder → broadcast_id status polling (progress bar: queued/sent/delivered/failed)
- Route + nav menü entry + plan-gate guard
- **UX:** modal'da "İptal" text butonu YOK, sağ üst X (Q kuralı)

### Adım 6 — Smoke + deploy
- Build (full solution — yeni servis + Shared dokunulursa)
- Migration prod'a (deploy protokolü: önce migration sonra publish)
- DentAdavista pilot DB ile gerçek küçük segment smoke (5-10 recipient test)
- `/rev` Codex review → PASS → commit → deploy

---

## 4. Scope Gerçekçiliği ("bugün" riski)

Tam Faz 1 production-ready tek günde **agresif**. Kritik yol (pilot gönderim çalışsın) = Adım 1+2+3+5 (INMA-only, basit segment). Düşük öncelik bugün dışına itilebilir:
- Zoho COQL adapter (Q sadece INMA seçti — Faz 1'den ÇIKAR)
- CSV upload (Faz 2)
- Recurring/scheduled Hangfire (Faz 2)
- Gelişmiş segment builder (SQL-like) → Faz 1'de basit filtre

**Önerilen "bugün biten" minimum:** INMA pull (tüm contacts + DataList filtresi) → preview → send → status. CF filtresi + plan quota ince ayar yarına sarkabilir.

---

## 5. Açık Riskler
| Risk | Etki | Azaltma |
|------|------|---------|
| Prod Outbound→MSSQL bağlantısı INMA onayı gecikir | Blocker | Q INMA köprüsü; alternatif: bridge'e yeni "pull contacts" endpoint istenir (INMA dev) |
| 24h conversation window / template approval | Mesaj reddi | INMA bridge marketing kategoriyle zaten enforce; onaylı template şart — UI'da template seçimi INMA approved listesinden |
| 8460 telefon = INMA daily limit aşımı | Gönderim 301 hata | `MessageLimitPerDaily` guard + batch + rate limiter (mevcut RateLimiter) |
| MSSQL READONLY ihlali (yazma) | Veri bütünlüğü | Connection read-only; sadece SELECT; service-isolation-checker |
| Phone format tutarsızlığı (CustomerPhones) | Yanlış gönderim | E.164 normalize + validation, preview ile gözle doğrula |

---

## 6. CODEX REVIEW (2026-06-03) — Verdict: MODIFY → Revize Plan

Codex (gpt-5.5, senior backend architect rolü) stress-test etti. **En büyük risk teknik değil: yanlış/duplicate/non-compliant WhatsApp marketing gönderimi GERİ ALINAMAZ.** Direct MSSQL uzun vadede anti-pattern; sadece dar, gated pilot için geçici köprü kabul edilebilir. Tam Faz 1 + UI + gerçek bulk aynı gün = fazla agresif.

### 6.1 Atlanan KRİTİK failure mode'lar (bugün bloklayıcı)
1. **`/broadcast/send` max 1000 AMA pilot DB'de 8-9k telefon var** → chunking tanımsız (her chunk ayrı broadcast mı? parent status? kısmi fail?). Full-tenant istenirse burada patlar.
2. **Duplicate/shared phone** — Customers⋈CustomerPhones join duplicate üretir (aile ortak no, çok telefonlu customer, inactive/deleted, WhatsApp olmayan no). Normalized-phone bazında unique + deterministic name/cf seçimi şart.
3. **Preview→send drift** — preview'daki liste send anında değişmiş olabilir (opt-out, silinen contact, DataList değişimi). **Recipient snapshot tablosu zorunlu**; confirm snapshot üzerinden gider, send öncesi opt-out tekrar uygulanır.
4. **Idempotent olmayan POST zinciri** — bulk-send → /broadcast/send retry'da duplicate broadcast. `/broadcast/send` idempotent değilse orchestrator idempotency'si yetmez.
5. **MSSQL failure states** — bağlantı açılmaz/permission fail/lock/yavaş/yanlış DatabaseName/tenant offline/pool tükenir. `fetching→sending→completed` fazla basit; gerekli: `failed_fetch, partial_snapshot, cancelled, send_failed, completed_with_errors`.
6. **Template param mismatch** — 3 değişken bekleyen template'e 2 göndermek → binlerce fail, retry yine fail, rate-limit boşa. Confirm öncesi template-variable validation.
7. **Observability eksik** — selected/normalized/duplicate-dropped/invalid-dropped/optout-dropped/queued/sent/failed-by-code/906-907/rate-limited canlı görülmeden gönderim YAPILMAZ.

### 6.2 Compliance — opt-out YETMEZ (sert gate'ler)
- Sadece **WhatsApp onaylı marketing template** seçilebilir (template ID/name/language/category=marketing doğrulanır). Freeform marketing YASAK.
- **24h window marketing'i kurtarmaz** — toplu marketing yine approved template ister; `MessageCategory="marketing"` tek başına compliant yapmaz.
- **DynamicMessage riski** — `{{name}}/{{cf1}}` gerçekten approved template parametresi olarak mı gidiyor yoksa server freeform mu üretiyor? DOĞRULANMALI. Klinik = sağlık verisi personalization ("Ayşe, implant kontrolünüz") ülkeye göre hassas veri riski.
- Opt-in source kanıtı + mesajda opt-out footer ("İptal için DUR yazın") + STOP/DUR/İPTAL handling + per-campaign audit trail.

### 6.3 Idempotency revize (query_hash zayıf)
- Client-verilen **`campaign_id` (client_request_id) zorunlu** → unique `(tenant_id, campaign_id)`
- Snapshot immutable; recipient unique `(job_id, normalized_phone)`; message unique `(broadcast_id, normalized_phone)`
- `/confirm` ikinci kez çağrılırsa mevcut job döner, yeni send YARATMAZ
- Rate-limit: tenant per-minute token bucket + daily quota + global concurrency + fail-rate >%10 → campaign pause; parent-job başına quota accounting (chunk başına değil)

### 6.4 REVİZE SCOPE — Phase 0 (bugün, GÜVENLİ) vs Phase 1 (sonra)

**Phase 0 — bugün shippable (UI YOK):**
- Admin-only endpoint: `POST /api/v1/bulk-send/preview` + `POST /api/v1/bulk-send/confirm` (operatör JSON/CSV preview görür, ayrı confirm)
- Feature flag arkasında + **tek pilot tenant allowlist** (DentAdavista)
- MSSQL adapter: serbest SQL YOK, önceden tanımlı 2 segment (all-contacts capped | DataList ID), whitelist regex DatabaseName, view üzerinden SELECT, query timeout 5-10s, PII log YASAK
- **Recipient snapshot tablosu** (immutable) + dedup + E.164 normalize
- **Hard cap kademeli:** ilk 5-10 → 50 → manuel onayla 100/250 (full 9k blast YOK)
- Sadece immediate send, sadece approved template whitelist, campaign_id zorunlu
- Chunking: 1000'lik broadcast parçaları + parent job status aggregation
- Canlı observability counter'ları (6.1.7)

**Phase 1 — production (sonraki):**
- INMA Contact Export/Preview REST endpoint VEYA INMA-side read facade (asıl doğru çözüm — domain kuralları INMA'da kalır, Outbound MSSQL'e bağlanmaz)
- Full Dashboard UI (BulkSendPage), schedule, plan-tier quota, gelişmiş segment builder
- Campaign reporting, consent/opt-in validation, retry/pause/resume, template registry

### 6.5 Q ONAYI/AKSİYONU GEREKEN BLOKERLER (kod öncesi)
1. **INMA yazılı onayı** — prod Outbound→MSSQL read-only erişim (network + read-only credential + view). Alternatif: INMA dev'den "contact preview/export" REST endpoint iste (daha temiz, ama INMA'ya bağımlı).
2. **Onaylı marketing template** — pilot klinik için Meta-approved WhatsApp template VAR MI? Yoksa gönderim compliant olamaz.
3. **DynamicMessage gerçekte approved-template-param mı** — INMA bridge davranışı doğrulanmalı (test mesajı).
4. **Opt-in source** — pilot contact'ların marketing opt-in kanıtı var mı (KVKK)?

> **Net karar:** Direct MSSQL yalnız allowlisted küçük pilot için geçici OK. Asıl bloklayan = compliance + idempotent snapshot olmadan duplicate/non-compliant gönderimin geri alınamazlığı. **Bugün = Phase 0 (gated, capped, UI'siz). UI'li Tam Faz 1 = sonraki.**
