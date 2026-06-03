<!-- Status: DEPLOYED+ENABLED (all tenants) — GR1-GR4, build PASS, Codex /rev PASS iter2, migration 054 applied, Outbound+Backend deployed HEALTHY | 2026-06-03 -->

# FEAT-OBI Phase 1B — Export Manager v2 (Filtre-odaklı yüzey)

> **Tarih:** 2026-06-03
> **Slug:** `20260603-feat-obi-export-manager-v2`
> **Hedef:** Operatör, tüm bulk-send kampanya alıcılarını (gönderim sonuçları) **tek filtre-odaklı yüzeyden** süzsün — Şablon · Kampanya · Data Listesi (üyelik) · Teslim Durumu · Tarih — anlık **benzersiz numara / toplam kayıt** sayımını görsün, filtrelenmiş seti **CSV / Excel** indirsin ya da benzersiz numaralardan **yeni bir data_list (`source='export'`)** oluştursun. Altta **Export Geçmişi** (`export_logs`).
> **Önceki faz:** Plan B (per-row liste+kampanya export) DONE — NOT deployed (master `e5672fc4`). v2 onun yüzeyini **tamamen değiştirir**, audit/CSV/XLSX altyapısını yeniden kullanır.
> **Ekran referansı:** Q'nun gösterdiği dialer Export Manager — **yapı alınır, format değil**.

---

## 1. Interview Kararları (Q onaylı, 2026-06-03)

| # | Soru | Q Kararı |
|---|------|----------|
| 1 | Kayıt evreni | **Kampanya alıcıları (gönderim sonuçları)** — `bulk_send_recipients` + `outbound_messages` teslim durumu. Tüm tenant kampanyaları birleşik (per-job değil). |
| 2 | "Proje" filtresi | **Şablon** (`outbound_templates`). |
| 3 | "Sonuç" filtresi | **Teslim Durumu** — read/delivered/sent/failed/blocked/not_sent (mevcut precedence). |
| 4 | "Data Listesi" filtresi | **Üyelik join'i** — numarası seçili listede olan alıcılar (`bulk_send_recipients.normalized_phone` ⋈ `list_records.normalized_phone`). list_id linki YOK, bu yüzden membership. |
| 5 | Filtre barı | **5 filtre:** Şablon · Kampanya · Data Listesi (üyelik) · Teslim Durumu · Tarih Aralığı (hepsi AND). |
| 6 | Eski tablolar | **Tamamen değiştir** — Plan B'nin per-row liste/kampanya tabloları kaldırılır; tek filtre yüzeyi. |
| 7 | v1 kapsamı | Filtre + sayım kartı + CSV/Excel + **Liste Oluştur** (`source='export'`) + Export Geçmişi. **Telefon Numarası Ara → Faz 2.** |

### Default'lar (Q onayına flag'li — §8)
- **D1 Tarih semantiği:** `bulk_send_jobs.created_at` (kampanyanın gönderildiği tarih).
- **D2 Sayım:** benzersiz = `COUNT(DISTINCT normalized_phone)`; toplam kayıt = `COUNT(*)` (aynı numara iki kampanyada = 2 kayıt).
- **D3 Liste Oluştur:** benzersiz + **geçerli (sendable) numaralardan** yeni liste; isim Q girer; isim çakışırsa INV-OB-059 (başka isim iste). Cap = 50.000 (`MaxRecordsPerList`).
- **D4 Filtre kombinasyonu:** AND.
- **D5 Status filtresi:** computed best-status üzerinde (CTE/subquery), `not_sent` = mesaj satırı yok.

---

## 2. Mimari — Cross-job status join (kritik)

Plan B per-job idi (broadcast_ids tek job'dan). v2 **tüm tenant kampanyalarını** birleştirir:

```
FROM bulk_send_recipients r
JOIN bulk_send_jobs j        ON j.id = r.job_id AND j.tenant_id = @tid
LEFT JOIN LATERAL (
    SELECT om.status, om.sent_at, om.delivered_at, om.read_at, om.failed_reason
    FROM outbound_messages om
    WHERE om.tenant_id = @tid
      AND om.broadcast_id = ANY(j.broadcast_ids)   -- status SADECE kendi kampanyasından (bleed yok)
      AND om.recipient_phone = r.normalized_phone
    ORDER BY <precedence CASE> LIMIT 1
) m ON TRUE
WHERE r.tenant_id = @tid
  [AND j.template_id = @tpl]            -- Şablon
  [AND j.id = @jobId]                   -- Kampanya
  [AND j.created_at >= @from AND j.created_at < @toExcl]  -- Tarih
  [AND EXISTS (SELECT 1 FROM list_records lr
        WHERE lr.tenant_id=@tid AND lr.list_id=@listId
          AND lr.normalized_phone = r.normalized_phone)]  -- Data Listesi (üyelik)
  -- Teslim Durumu: best-status'u CTE'ye sarıp WHERE best=@status
```

- **Tenant predicate her tabloda** (r/j/om/lr) — izolasyon.
- **broadcast_id = ANY(j.broadcast_ids)** → her alıcının status'u kendi kampanyasından gelir, cross-campaign bleed yok.
- `idx_outbound_messages_tenant_broadcast_phone` (migration 053) bu join'i besler. Recipients için `idx_bulk_send_recipients_job` var; **yeni index** gerekebilir: `bulk_send_recipients (tenant_id, normalized_phone)` (membership + distinct count) → migration 054 (additive, IF NOT EXISTS).

---

## 3. Veri Modeli — Migration 054 (additive index, yeni tablo YOK)

```sql
-- distinct-phone count + list-membership EXISTS hızlandırma (büyük tenant için)
CREATE INDEX IF NOT EXISTS idx_bulk_send_recipients_tenant_phone
    ON bulk_send_recipients (tenant_id, normalized_phone);
```
- Yeni tablo YOK. `export_logs` (migration 053) yeni `export_type='filtered_recipients'` ve `'list_from_export'` değerleriyle kullanılır → **CHECK constraint genişletme** gerekir (migration 054'e ekle: `ALTER TABLE export_logs DROP/ADD CONSTRAINT chk_export_log_type`).
- `data_lists.source='export'` zaten destekli (CHECK'te var) — Liste Oluştur ek migration GEREKTİRMEZ.

---

## 4. Error Codes (INV-OB-059..061 — son kod INV-OB-058)

| Kod | Anlam | HTTP |
|-----|-------|------|
| INV-OB-059 | ExportListNameConflict (Liste Oluştur — isim tenant'ta mevcut) | 409 |
| INV-OB-060 | ExportFilterInvalid (geçersiz filtre/tarih aralığı/status değeri) | 400/422 |
| INV-OB-061 | ExportListEmpty (filtre 0 benzersiz numara → liste oluşturulamaz) | 422 |

> Mevcut INV-OB-054..058 (validation/notfound/disabled/dberror/toolarge) yeniden kullanılır.

---

## 5. Build Planı (GR breakdown)

### GR1 — Shared DTO + error codes + migration
- `ExportDtos.cs` (additive): `ExportFilter` (template_id?, job_id?, list_id?, status?, from?, to?), `FilteredCountResult` (unique_count, total_count), `CreateListFromExportRequest` (name, filter), `CreateListFromExportResult` (list_id, name, record_count), `ExportLogEntry` (history item). Yeni `ExportTypes.FilteredRecipients` + `ListFromExport`.
- `ErrorCodes.cs` INV-OB-059..061 + `arch/errors.md`.
- `arch/db/migrations/054-export-v2.sql` + `arch/db/outbound.sql` mirror (index + export_logs CHECK genişletme + verifier).

### GR2 — Outbound (port 7107) filtre motoru
- `ExportRepository.cs` (additive metotlar):
  - `CountFilteredAsync(tenantId, filter)` → (unique, total). Best-status CTE + filtreler.
  - `ReadFilteredRecipientsAsync(tenantId, filter, limit?)` → `IAsyncEnumerable<FilteredRecipientRow>` (stream). Kolonlar: phone, status, status_label, campaign_id, template_name?, sent_at, delivered_at, read_at, job_created_at.
  - `ReadDistinctSendablePhonesAsync(tenantId, filter)` → distinct geçerli numaralar (Liste Oluştur kaynağı).
  - `ListExportHistoryAsync(tenantId, limit)` → `export_logs` son N.
  - Filtre option okuyucuları: `ListTemplatesAsync` (id, name) — jobs + data-lists list zaten var.
- `ExportService.cs`: filtre validasyonu (tarih sırası, status enum, cap) → INV-OB-060; CSV/XLSX builder (mevcut sanitize/BOM/cap mantığını reuse); Liste Oluştur → `ContactListImportService`/`ContactListRepository` reuse ile data_list(source='export')+list_records insert, isim çakışması INV-OB-059, 0 numara INV-OB-061; her path `export_logs` yazar.
- `Program.cs` yeni endpoint'ler (ExportOptions gate, no-store + nosniff header):
  - `GET /api/v1/exports/filter-options` → templates + data-lists + recent campaigns (tek payload).
  - `GET /api/v1/exports/recipients/count?...filtreler` → FilteredCountResult.
  - `GET /api/v1/exports/recipients?format=csv|xlsx&...filtreler` → stream + log.
  - `POST /api/v1/exports/recipients/create-list` (body: name + filter) → CreateListFromExportResult + log.
  - `GET /api/v1/exports/history?limit=` → ExportLogEntry[].
- İzolasyon: tüm export SQL bu serviste; Shared'a SQL koyma.

### GR3 — Backend (proxy)
- `Program.cs`: 5 proxy route (filter-options, count, recipients-stream, create-list POST, history). Stream route `OutboundProxyStreamGet` reuse; POST/GET JSON proxy mevcut desen. JWT→tenant gate.
- `OutboundClient.cs`: yeni metot(lar) gerekiyorsa ekle.

### GR4 — Dashboard (SPA) — yeniden yapı
- `ExportManagerPage.tsx`: **tam yeniden yaz** — header + (Faz 2 placeholder yok) + **Filtreler** (5 dropdown + aktif filtre chip'leri + "Tümünü Temizle") + **Sayım kartı** (benzersiz numara büyük + toplam kayıt alt) + **Export Seçenekleri** (CSV İndir / Excel İndir / Liste Oluştur kartları) + **Export Geçmişi** tablosu.
- Sayım: filtre değişince debounce'lu `count` çağrısı.
- Liste Oluştur: isim modalı (X ile kapat — Q UX kuralı, "İptal" text butonu YOK), başarıda toast + geçmiş yenile.
- **Kampanya Raporu (PDF) bölümü (KORU):** filtre yüzeyinin altında kampanya seçici + PDF butonu — mevcut `getSendReportData` + jsPDF/jspdf-autotable + `tr()` transliterate aynen reuse.
- `lib/api.ts`: yeni tipli client metotları (filter-options, count, downloadFilteredExport blob, createListFromExport, listExportHistory) + interface'ler. **KORU:** `listSendJobs` + `getSendReportData` (PDF bölümü için). Per-row `downloadContactListExport`/`downloadSendRecipientsExport` filtre yüzeyinde KULLANILMAZ ama DataImport vb. başka tüketici varsa silinmez (Grep ile doğrula).
- `App.tsx` route korunur (`/data-export`), `Layout.tsx` nav korunur.

---

## 6. Scope Discipline

**Non-goals / Faz 2:**
- **Telefon Numarası Ara** (tek numaranın tüm geçmişi) — Faz 2.
- Liste kayıtları (kişi listesi) ham export — v2 evreni kampanya alıcıları; liste içeriği Plan A DataImport'ta zaten yönetiliyor.
- PDF (filtreli set için) — v2'de filtreli set PDF YOK. **Plan B kampanya raporu PDF'i KORUNUYOR (Q kararı 2026-06-03):** filtre yüzeyinin altında ayrı "Kampanya Raporu (PDF)" bölümü = kampanya seçici + PDF butonu (mevcut `report-data` endpoint + jsPDF reuse).
- Zamanlanmış export, e-posta, INMA MSSQL pull.

**Forbidden:** `bulk_send_recipients`/`bulk_send_jobs` snapshot mantığına yazma (immutable, sadece READ); `outbound_messages` yazma; `MessageSenderService`/INMA bridge dokunma. `export_logs` UPDATE/DELETE YOK (append-only, app-level).

---

## 7. Risk: MEDIUM-HIGH
Yeni cross-job SQL (performans — distinct count + membership EXISTS + best-status CTE) + yeni **write path** (Liste Oluştur data_list/list_records'a yazar) + 5 yeni cross-service endpoint + tam SPA yeniden yazım + PII export yüzeyi. → ≥3 verification question (plan JSON).

---

## 8. Default'lar — Q ONAYLADI (2026-06-03)
1. **D1 Tarih = kampanya (`bulk_send_jobs.created_at`).** ✓ ONAY
2. **D3 Liste Oluştur = sadece geçerli/sendable benzersiz numaralar.** ✓ ONAY
3. **PDF = KORU** — filtre yüzeyinin altında ayrı "Kampanya Raporu (PDF)" bölümü (kampanya seçici + PDF butonu, Plan B reuse). ✓ ONAY
