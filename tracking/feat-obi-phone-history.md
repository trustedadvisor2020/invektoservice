<!-- Status: PLANNING (interview DONE, Q onaylı) | 2026-06-17 -->

# FEAT-OBI Faz 2 — Telefon Numarası Ara (Tek Numara Geçmişi)

> **Tarih:** 2026-06-17
> **Slug:** `20260617-feat-obi-phone-history`
> **Hedef:** Operatör Export Manager'da bir telefon numarası girer → o numaraya **bizim** gönderdiğimiz TÜM mesajların (tüm kampanyalar/projeler/legacy broadcast) zaman-sıralı geçmişini görür: şablon · kampanya · durum · zaman damgaları · WhatsApp hata kodu · **gönderilen mesaj metni**. Sonuç CSV/PDF indirilebilir.
> **Önceki faz:** Export Manager v2 (`feat-obi-export-manager-v2.md`) DONE+DEPLOYED — "Telefon Numarası Ara" oradan bilinçle Faz 2'ye ertelendi (§6 satır 129).

---

## 1. Interview Kararları (Q onaylı, 2026-06-17)

| # | Soru | Q Kararı |
|---|------|----------|
| 1 | Veri kaynağı | **Sadece BİZİM outbound geçmişi** (`outbound_messages`). INMA `messagelistforphone` YOK — yeni cross-service yüzeyi açılmaz. |
| 2 | Yüzey/UI | **ExportManagerPage'e yeni bölüm/sekme** — ayrı sayfa/route DEĞİL. |
| 3 | Export | **+ CSV/PDF indir** (sadece ekran görüntüsü değil). |
| 4 | İçerik/PII | **Metadata + mesaj metni** (`message_text` gövdesi dahil). KVKK: tenant-scoped + export audit log şart. |

---

## 2. Mevcut Durum (kod okundu)

- `outbound_messages` tek-numara geçmişi için yeterli — kolonlar: `recipient_phone`, `message_text`, `status`, `created_at`, `sent_at/delivered_at/read_at`, `failed_reason`, `provider_error_message`, `template_id`, `broadcast_id`, `message_kind`, `send_route`, `template_ref` (`arch/db/outbound.sql`).
- Kampanya/şablon adı için: `broadcast_id` → `bulk_send_jobs` (`broadcast_ids[]` içinde) → `campaign_id`+`template_id`; şablon adı `outbound_templates.name` (`COALESCE(om.template_id, j.template_id)`).
- **WaErrorPopover** (2026-06-17, `lib/wa-error-codes.ts` + `components/WaErrorPopover.tsx`) → hata kodu hücresinde AYNEN reuse.
- `PhoneNormalizer.Normalize(string?)` (Outbound) — arama input'u gönderim-anındaki ile **aynı** normalize edilmeli (eşleşme şart).
- Export altyapısı: `ExportService` CSV/XLSX builder (sanitize/BOM/cap), `export_logs` audit (migration 053), client-side jsPDF (ExportManagerPage kampanya raporu).
- **Index eksik:** `outbound_messages` üzerinde `(tenant_id, recipient_phone)` index YOK (mevcut `idx_outbound_messages_tenant_broadcast_phone` ortada `broadcast_id` olduğu için phone-only lookup'a hizmet etmez) → yeni index gerekli.

---

## 3. Mimari

### 3.0 GR0 — Kod ÖNCESİ prod telefon-format sampling (PR-001 blocker)
Kod yazmadan ÖNCE, prod'da (read-only) `recipient_phone` saklama formatını gönderim-yolu bazında örnekle: `SELECT send_route, message_kind, recipient_phone, recipient_phone = <normalize(recipient_phone)> AS matches FROM outbound_messages WHERE tenant_id=100000001 ORDER BY created_at DESC LIMIT 100` benzeri. Bulk/proje/legacy yolları AYNI normalize çıktısını saklıyorsa eşleşme garanti; saklamıyorsa (raw / `+90` / `90` / boşluklu) ya bilinen legacy varyantları açıkça sorgula ya da karşılaştırmayı normalize-on-compare yap. **Bu adım AC1'in "TÜM satırlar" garantisini doğrular.**

### 3.1 Sorgu (Outbound, read-only)
```
SELECT om.id, om.message_text, om.status, om.created_at, om.sent_at, om.delivered_at,
       om.read_at, om.failed_reason, om.provider_error_message, om.message_kind, om.template_ref,
       COALESCE(t.name, '') AS template_name, j.campaign_id
FROM outbound_messages om
LEFT JOIN LATERAL (
    SELECT jj.campaign_id, jj.template_id
    FROM bulk_send_jobs jj
    WHERE jj.tenant_id = @tid AND om.broadcast_id = ANY(jj.broadcast_ids)
    ORDER BY jj.created_at DESC, jj.id DESC   -- PR-004: deterministik (broadcast_id birden çok job'da olabilir)
    LIMIT 1
) j ON TRUE
LEFT JOIN outbound_templates t
    ON t.tenant_id = @tid AND t.id = COALESCE(om.template_id, j.template_id)
WHERE om.tenant_id = @tid AND om.recipient_phone = @phone
ORDER BY om.created_at DESC, om.id DESC
LIMIT @lim   -- her zaman cap (PerPhoneHistoryMaxRows, ör. 1000) — bkz §3.3 cap-vs-all
```
- **Tenant predicate her tabloda** (om, jj, t) — izolasyon. **Tenant SADECE JWT/TenantContext'ten** — client query/body/header'dan ASLA (PR-013).
- `om.recipient_phone = @phone` → input `PhoneNormalizer.Normalize`; GR0 ile eşleşme doğrulanır.
- **Metadata kapsamı (PR-003):** Şablon adı `COALESCE(om.template_id, j.template_id)` → proje (gallery_template) + legacy INSE + bulk hepsini kapsar. `campaign_id` SADECE bulk satırlarda dolu; proje/legacy'de NULL → UI'da "—" (kabul edildi, hata değil).
- **Nullable guard (PR-014, bu haftaki ders):** reader'da `sent_at/delivered_at/read_at/failed_reason/provider_error_message/template_ref/template_name/campaign_id` HEPSİ `IsDBNull` guard'lı; null template/campaign/timestamp içeren fixture satırı testte.
- **WA hata kodu (PR-002/PR-021):** WaErrorPopover client-side `extractWaErrorCode` ile metin içinden `(NNNNN)` çıkarır → satıra Projeler raporundaki AYNI hata-metni beslenir: `COALESCE(failed_reason, NULLIF(provider_error_message,'Success'), CASE failed/ambiguous→'İletilemedi...')` (ProjectsService.CleanNotSentReason deseni reuse). Yeni alan/kolon YOK.

### 3.3 Cap-vs-ALL kararı (PR-009/PR-010)
AC1 "TÜM satırlar" ile sınırsız render çelişir. **Karar:** ekran tablosu `@lim` (1000) ile capli, aşılırsa görünür "İlk 1000 gösteriliyor — tümü için CSV indir" işareti; **CSV/PDF export FULL** (capsiz stream). Ayrı `CountPhoneHistoryAsync` endpoint YOK (PR-010 — ekran `rows.length` + cap işareti yeter; çift sorgu/inconsistency riski elenir).

### 3.4 Migration 067 (additive, kilit-güvenli)
```sql
-- PR-005: ordering'i de besleyen composite (created_at DESC, id DESC)
-- PR-006: shared Postgres'te yazma kilidi olmasın → CONCURRENTLY (tx-dışı; runner desteği GR0'da doğrula)
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_outbound_messages_tenant_phone_created
    ON outbound_messages (tenant_id, recipient_phone, created_at DESC, id DESC);
-- export_logs CHECK genişlet (PR-007 isimli-constraint güvenli, PR-008 destructive down YOK)
ALTER TABLE export_logs DROP CONSTRAINT IF EXISTS chk_export_log_type;
ALTER TABLE export_logs ADD CONSTRAINT chk_export_log_type CHECK (export_type IN (...mevcut..., 'phone_history'));
```
- **PR-006:** `CONCURRENTLY` tx-dışı çalışmalı — migration runner desteklemiyorsa GR0'da tespit et; desteklemiyorsa additive `(tenant_id, recipient_phone, created_at DESC, id DESC)` normal build kısa kilit (kabul, ama önce ölç). **Down-migration `phone_history`'yi CHECK'ten KÖRÜ KÖRÜNE çıkarmaz** (audit satırı varsa patlar) — widened CHECK kalıcı.
- Yeni tablo YOK. `arch/db/outbound.sql` mirror + DO $verify$ postcondition.
- **PR-016:** `om.broadcast_id = ANY(jj.broadcast_ids)` LATERAL maliyeti realistik veride `EXPLAIN (ANALYZE, BUFFERS)` ile ölç; `bulk_send_jobs` tenant başına küçük (kötüyse legacy/ambiguous metadata'yı atla).

### 3.3 Error codes (INV-OB-097+)
| Kod | Anlam | HTTP |
|-----|-------|------|
| INV-OB-097 | PhoneHistoryInvalidNumber (boş/geçersiz/normalize edilemez telefon) | 400/422 |
| (reuse) INV-OB-057 disabled / INV-OB-058 dberror | — | — |

---

## 4. Build Planı (GR breakdown)

### GR1 — Shared DTO + error code + migration
- `ExportDtos.cs` (additive): `PhoneHistoryMessageRow` (id, message_text, status, status_label, created_at, sent_at?, delivered_at?, read_at?, failed_reason?, provider_error_code?, template_name?, campaign_id?, message_kind), `PhoneHistoryResult` (normalized_phone, total_count, rows[]). Yeni `ExportTypes.PhoneHistory`.
- `ErrorCodes.cs` INV-OB-097 + `arch/errors.md`.
- `arch/db/migrations/067-obi-phone-history.sql` + `arch/db/outbound.sql` mirror.

### GR2 — Outbound (port 7107)
- `ExportRepository.cs` (additive): `ReadPhoneHistoryAsync(tenantId, normalizedPhone, limit?)` → `IAsyncEnumerable<PhoneHistoryMessageRow>` (stream, §3.1 sorgu). **Ayrı count metodu YOK** (§3.3).
- `ExportService.cs` (additive): input normalize (`PhoneNormalizer`) → boş/normalize-edilemez ise INV-OB-097; ekran JSON (cap+marker) + CSV builder (mevcut sanitize/BOM/formula-prefix reuse). **Audit (PR-011):** export_logs('phone_history') satırı **stream BAŞLAMADAN** yazılır (sonuç-sayısı biliniyorsa dahil) — client disconnect'te audit kaybolmaz; "attempted export" semantiği.
- `Program.cs` yeni endpoint'ler (ExportOptions gate, no-store + nosniff):
  - **`POST /api/v1/exports/phone-history`** (body: `{phone}`) → PhoneHistoryResult. **POST seçildi (PR-018):** telefon = PII; GET query-string browser history/proxy/APM log'una sızar.
  - **`POST /api/v1/exports/phone-history/download`** (body: `{phone, format: csv}`) → stream + audit.
  - **PDF (PR-012):** client jsPDF ekrandaki veriden üretir AMA indirmeden önce hafif audit ping (`format=pdf` ile aynı download endpoint'i VEYA dedicated audit call) → CSV ile tutarlı KVKK izi (PDF de kişisel-veri export'u).
- İzolasyon: tüm SQL bu serviste.

### GR3 — Backend (proxy)
- `Program.cs`: 2 POST proxy route (phone-history + download stream). **Tenant SADECE JWT'den** (PR-013) — client tenant id geçemez; her SQL predicate o tenant'ı kullanır.
- `OutboundClient.cs`: gerekiyorsa metot ekle.

### GR4 — Dashboard (SPA)
- `ExportManagerPage.tsx`: filtre yüzeyinin üstüne **"Telefon Numarası Ara"** bölümü/sekme — numara input + Ara butonu → sonuç tablosu (zaman-sıralı: tarih · şablon · kampanya · durum rozeti · **hata kodu → WaErrorPopover** · mesaj metni) + benzersiz sayım/total + CSV İndir / PDF İndir.
- `lib/api.ts`: `getPhoneHistory(phone)` + `downloadPhoneHistoryCsv(phone)` blob + tipler. WaErrorPopover/wa-error-codes reuse.
- **UX:** sonuç boşsa "Bu numaraya gönderim bulunamadı" (hata değil). Mesaj metni uzunsa truncate + expand. PDF client-side jsPDF (`tr()` transliterate reuse).

### GR5 — Smoke + deploy
- Build (Shared dokunulduğu için full solution).
- Migration 067 prod (önce migration sonra publish; CONCURRENTLY tx-dışı).
- Medipol (100000001) gerçek numarayla smoke: geçmiş gelmeli + CSV/PDF + WaErrorPopover.
- **Cross-tenant izolasyon testi (PR-022):** aynı telefon iki tenant'ta varsa, tenant A JWT'si ile arama SADECE A'nın satırlarını döner (B'nin mesaj gövdesi sızmaz).
- `/rev` Codex → PASS → commit → Outbound+Backend(SPA) deploy.

---

## 8. Plan Review (codex_consult, 2026-06-17) — 22 bulgu triajı

**Status:** completed. **Kabul (plana işlendi):** PR-001 (GR0 prod sampling), PR-002/PR-021 (WA hata = Projeler reason-metni + WaErrorPopover client extract), PR-003 (template om.template_id direct; campaign bulk-only), PR-004 (deterministik LATERAL ORDER BY), PR-005 (index'e created_at DESC,id DESC), PR-006 (CONCURRENTLY tx-dışı), PR-007/008 (isimli-constraint + destructive-down YOK), PR-009 (cap-vs-all: ekran cap+marker, export full), PR-010 (ayrı count YOK), PR-011 (audit stream-öncesi), PR-012 (PDF de audit), PR-013 (tenant yalnız JWT), PR-014 (IsDBNull guard + null fixture), PR-015 (message_text defensive + UI truncate), PR-016 (EXPLAIN ölç), PR-018 (GET→POST PII), PR-022 (cross-tenant smoke).
**Reddedildi:** PR-017 (order by `created_at`, v2 D1 tutarlılığı — sent_at değil; consistency). 
**Minor/onay:** PR-019 → tek status **422** (mevcut INV-OB validation deseni), PR-020 → mevcut CSV sanitize formula-prefix/newline/quote testleri reuse.
**Q-decision (onayına sunuldu):** §3.3 cap=1000 değeri + PDF-audit yaklaşımı (server ping vs dedicated).

---

## 5. Scope Discipline

**Non-goals:**
- INMA `messagelistforphone` / WhatsApp konuşma geçmişi (inbound) — Q kararı: sadece bizim outbound.
- Inbound/gelen mesajlar (bizde yok zaten).
- Numara bazında AKSİYON (resend/iptal) — bu yüzey READ-ONLY (resend Projeler'de).
- Zamanlanmış/e-posta export.

**Forbidden:** `outbound_messages`/`bulk_send_*` yazma (READ-only); `MessageSenderService`/INMA bridge dokunma; `export_logs` UPDATE/DELETE (append-only); v2 filtre yüzeyi davranışını bozma (additive bölüm).

---

## 6. Risk: MEDIUM
Yeni read query büyük `outbound_messages` tablosunda (yeni index ile) + **PII export** (mesaj gövdesi = klinik/sağlık verisi → export_logs audit zorunlu) + telefon-normalize eşleşme tutarlılığı + 2 cross-service endpoint + SPA additive bölüm. → ≥3 verification question.

## 7. Açık Riskler
| Risk | Etki | Azaltma |
|------|------|---------|
| `recipient_phone` farklı formatlarda saklanmış (E.164 vs raw) | Arama 0 sonuç / eksik geçmiş | Input + saklanan AYNI normalize; gerekirse normalize-on-compare; smoke ile doğrula (verification Q) |
| Çok kayıtlı numara (binlerce mesaj) | Yavaş/UI şişme | Defensive LIMIT + index + stream |
| PII sızıntısı (mesaj gövdesi log/export) | KVKK | tenant-scope her tabloda + PII log YASAK + export_logs audit |
| Index full-table build (büyük tablo) | Migration süresi | `IF NOT EXISTS`; gerekiyorsa `CONCURRENTLY` değerlendir (tek shared Postgres) |
