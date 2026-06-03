<!-- Status: DONE (GR1-GR4, build PASS, Codex /rev PASS iter4) — NOT deployed | 2026-06-03 -->

# FEAT-OBI Phase 1A Plan B — Export Manager

> **Tarih:** 2026-06-03
> **Slug:** `20260603-feat-obi-export-manager`
> **Hedef:** Operatör, Plan A ile yönettiği kişi listelerini ve bulk-send kampanya sonuçlarını (teslim durumu dahil) CSV / Excel / PDF olarak dışa aktarsın — KVKK denetim izi (`export_logs`) ile.
> **Önceki faz:** Plan A (Contact Lists + CSV/Excel import + list→bulk-send) DONE+DEPLOYED+ENABLED (master `7b07676d`).
> **Spec kaynağı:** [feat-obi-bulk-external.md](feat-obi-bulk-external.md) · [feat-obi-faz1-plan.md](feat-obi-faz1-plan.md)

---

## 1. Interview Kararları (Q onaylı, 2026-06-03)

| # | Soru | Q Kararı |
|---|------|----------|
| 1 | Export kapsamı | **Listeler + gönderim sonuçları** — `list_records` (kişi listeleri) **VE** `bulk_send_jobs`→`outbound_messages` (kampanya sonuçları + teslim durumu = ertelenmiş `contacted_count`). INMA bağımlılığı YOK, hepsi Postgres'te. |
| 2 | Üretim yöntemi | **Server-side + `export_logs`** denetim tablosu. |
| 3 | Formatlar | **CSV (UTF-8 BOM) + Excel (.xlsx) + PDF.** |
| 4 | Tenant gate | **Tüm tenant'lar** (Outbound plan). Codex plan-review sonrası: ContactList reuse YERİNE **yeni `ExportOptions { Enabled, AllowAllTenants }`** (Q onayı) — prod'da all-tenants default-enabled. PII-egress'i import'tan bağımsız kapatılabilir. |
| 5 | PDF kapsamı | **Rapor + capped tablo** (kampanya özet raporu + ≤2000 satır alıcı tablosu). Ham kişi listesi PDF DEĞİL (CSV/XLSX). |
| 6 | PDF kütüphanesi | **HTML→PDF, yeni lisans yok** → mimari çözüm (aşağıda): PDF byte'ları **tarayıcıda** üretilir, server denetim satırını yine yazar. |

### Çözülen mimari gerilim (Q "devam" ile onayladı)
"Server-side üretim" + "HTML→PDF lisanssız" → literal okuma prod Outbound mikroservisine **Chromium/wkhtmltopdf** native runtime'ı bindirirdi (~300MB, her deploy'da install adımı, ekstra memory + crash yüzeyi). PDF yükü küçük ve sınırlı (özet + ≤2k satır) olduğu için server gücü gerekmez.

**KARAR:**
- **CSV + XLSX** → server-side üretilir + stream edilir + `export_logs` satırı yazılır (Q'nun literal seçimi).
- **PDF (rapor + capped tablo)** → server JSON döndürür (`report-data`), **byte'lar tarayıcıda** (jsPDF + jspdf-autotable) üretilir; server `report-data?for=pdf` çağrısında `export_logs` satırını yazar → **denetim izi 3 formatta da aynı.**
- Prod Outbound'a native browser runtime BİNMEZ.

---

## 2. Yeni Bağımlılıklar (Q plan-review onayı bekliyor)

| Bağımlılık | Katman | Lisans | Gerekçe | Risk |
|------------|--------|--------|---------|------|
| **ClosedXML** | Outbound (server) | MIT | Server-side `.xlsx` üretimi. Pure-managed, native dep YOK, EPPlus'ın Polyform-NonCommercial lisans tuzağı YOK. | Düşük |
| **jspdf** + **jspdf-autotable** | Dashboard (frontend) | MIT | Tarayıcıda rapor + tablo PDF. Frontend-only, prod server etkisi YOK. | Düşük |

> SheetJS (xlsx 0.18.5) Plan A'da zaten var ama export server-side XLSX yaptığı için export'ta KULLANILMAZ (parse-only kalır).

---

## 3. Veri Modeli — Migration 053 (`export_logs` + additive index)

KVKK denetim izi: her export isteği = 1 satır (kim / ne zaman / neyi / kaç satır / hangi format / hangi teslim modu).

```
export_logs
  id                    BIGSERIAL PK
  tenant_id             INTEGER NOT NULL FK tenant_registry
  export_type           VARCHAR(24) CHECK ('contact_list','send_recipients','send_summary')
  source_id             BIGINT            -- data_lists.id | bulk_send_jobs.id (her ikisi de BIGINT; nullable)
  source_name_snapshot  VARCHAR(255)      -- liste/kampanya adı export anındaki haliyle (forensic)
  format                VARCHAR(8)  CHECK ('csv','xlsx','pdf')
  delivery_mode         VARCHAR(16) CHECK ('server_stream','client_render')  -- csv/xlsx=server_stream, pdf=client_render
  row_count             INTEGER NOT NULL DEFAULT 0
  status                VARCHAR(16) CHECK ('completed','failed') DEFAULT 'completed'
  requested_by          VARCHAR(120)      -- JWT subject/operatör user id (display name DEĞİL)
  error_code            VARCHAR(16)       -- başarısızsa INV-OB-0xx
  created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW()
  -- index (tenant_id, created_at DESC)
  -- GRANT ALL (codex-context.md L20/L69 canon — append-only app-level, aşağıya bak)
  -- DO $verify$ INV-SEED-053

-- Additive index (mevcut hot tabloya, IF NOT EXISTS — güvenli) — send-results join hızlandırma:
CREATE INDEX IF NOT EXISTS idx_outbound_messages_tenant_broadcast_phone
  ON outbound_messages (tenant_id, broadcast_id, recipient_phone);
```

**Codex plan-review kararları (denetim semantiği):**
- `status='completed'` = **"export verisi üretildi + response'a teslim edildi"** anlamına gelir. Stream yarıda koparsa (client disconnect) ya da PDF'te tarayıcı render'ı başarısız olursa log bunu garanti ETMEZ — `delivery_mode` ile bu açıkça işaretlenir (`server_stream` = byte'lar stream'lendi; `client_render` = yalnız veri servis edildi, PDF tarayıcıda üretilir). Denetim açısından "veri ifşa edildi" kaydı KVKK için yeterli kanıttır; "PDF fiziksel olarak oluştu" iddiası YOK.
- **GRANT ALL korunur** (proje canon'u; B1 session'da downgrade Codex FAIL aldı). Append-only garantisi **uygulama katmanında**: `ExportRepository` yalnız INSERT yapar; export_logs'a UPDATE/DELETE statement'ı YOK. Tek `invekto` rolü mimarisinde DB-level column-grant ayrımı yapılmaz — pre-declared decision (Codex review false-positive önleme).
- `source_id` polymorphic BIGINT yeterli (data_lists.id + bulk_send_jobs.id ikisi de BIGSERIAL). Broadcast-level (UUID) audit YOK.

---

## 4. Error Codes (INV-OB-054..058 — collision-free, son kod INV-OB-053)

| Kod | Anlam | HTTP |
|-----|-------|------|
| INV-OB-054 | ExportValidationError (geçersiz format/tip/aralık) | 400/422 |
| INV-OB-055 | ExportSourceNotFound (liste/kampanya yok ya da tenant'a ait değil) | 404 |
| INV-OB-056 | ExportFeatureDisabled (tenant gate kapalı) | 403 |
| INV-OB-057 | ExportDbError (Npgsql/Postgres export yolunda) | 503 |
| INV-OB-058 | ExportTooLarge (PDF/xlsx satır cap aşıldı) | 422 |

---

## 5. Build Planı (GR breakdown — Plan A deseni)

### GR1 — Data + Contract katmanı
- `arch/db/migrations/053-export-logs.sql` (export_logs + additive `idx_outbound_messages_tenant_broadcast_phone`) + `arch/db/outbound.sql` canonical mirror
- Shared `ExportDtos` (request/response + report-data DTO, additive) + `ErrorCodes.cs` INV-OB-054..058 + `arch/errors.md`
- `ExportOptions` (Outbound-internal config; Enabled + AllowAllTenants) — Q onaylı yeni flag.

### GR2 — Outbound (port 7107) export motoru
- `Services/ExportService.cs`
  - **CSV builder:** UTF-8 BOM + formula-injection escape (Plan A `DataImportPage` hardening'i server-side'a taşı). Tehlikeli prefix seti = `= + - @` + baştaki TAB/CR/LF (ve bunlardan önce gelen boşluk). `/` ESCAPE EDİLMEZ (formula trigger değil — Codex notu). Stream'e satır satır yaz (sabit memory, büyük export'un güvenli yolu).
  - **XLSX builder (ClosedXML):** satır cap zorunlu → over-cap'te workbook **oluşturulmadan** INV-OB-058. `AdjustToContents()` KULLANMA (50k'da pahalı). Telefon/ID hücreleri **text** olarak yaz (Excel scientific-notation/leading-zero/formula yorumunu önle). Formula-injection sanitize XLSX'te de uygulanır. `CancellationToken` ile bound.
  - **`custom_fields` (JSONB):** dinamik kolona AÇMA (kolon patlaması/DoS riski — Codex#6/#12). Tek `custom_fields` JSON-string kolonu olarak export et (deterministik + bounded).
  - **Send-results join (Codex#5 — açık kural):** base = `bulk_send_recipients` (job'un immutable snapshot'ı = kimlere göndermek istedik) **LEFT JOIN** `outbound_messages` — never-sent alıcıları INNER JOIN ile DÜŞÜRME. Join key: `m.tenant_id=@t AND m.broadcast_id = ANY(@broadcast_ids) AND m.recipient_phone = r.normalized_phone`. **Tenant predicate HER tabloda** (r/m/job). Mesaj satırı yoksa status=`not_sent`. Bir alıcı normalde tek child broadcast'tedir; defensive olarak çoklu eşleşmede precedence: `read > delivered > sent > sending > queued > failed > blocked > not_sent` (en iyi terminal durum). Summary = `bulk_send_jobs` sayaçları + `outbound_broadcasts` rollup.
- `Data/ExportRepository.cs`: `export_logs` **INSERT only** (her path; başarı=completed, hata=failed+error_code) + veri SELECT'leri (tenant-scoped, ownership verify → yoksa INV-OB-055). DB path'leri `NpgsqlException` **ve** `PostgresException` typed-catch → INV-OB-057.
- **Endpoints + response header'ları** (hepsinde: `Cache-Control: no-store, no-cache, must-revalidate` + `X-Content-Type-Options: nosniff`):
  - `GET /api/v1/exports/contact-list/{listId}?format=csv|xlsx` → stream + log; `Content-Disposition: attachment; filename="..."` (liste adı CR/LF/path-char strip).
  - `GET /api/v1/exports/send-job/{jobId}/recipients?format=csv|xlsx` → stream + log
  - `GET /api/v1/exports/send-job/{jobId}/report-data?for=pdf` → JSON (summary + ≤2000 alıcı) + log; payload alanları: `generated_at`, `total_recipient_count`, `recipient_table_truncated`, `recipient_table_limit=2000`.
- **Gate:** yeni `ExportOptions { Enabled, AllowAllTenants }` (Q kararı — ContactList reuse değil; prod'da all-tenants default-enabled). Kapalıysa INV-OB-056.
- İzolasyon: export SQL yalnız bu serviste; Shared'a koyma.

### GR3 — Backend (proxy)
- `Program.cs` 3 proxy route (data-list proxy deseni mirror) — **file-streaming aware**: Content-Type + Content-Disposition header pass-through, body buffer ETME (büyük dosya). JWT→tenant gate.

### GR4 — Dashboard (SPA)
- `ExportManagerPage.tsx`: tenant'ın kişi listeleri + son bulk-send kampanyaları; her satırda export butonları (liste: CSV/XLSX; kampanya: CSV/XLSX/PDF).
- `lib/api.ts`: typed client (blob fetch + download trigger; report-data fetch → jsPDF render).
- PDF: jsPDF + jspdf-autotable (rapor sayfası + capped tablo).
- `App.tsx` route `/data-export` + `Layout.tsx` 'Çalışma Alanı' nav (auth-gated, Plan A `/data-import` komşusu).

---

## 6. Scope Discipline

**Non-goals / dışarıda:**
- INMA MSSQL contact pull/export (Faz 1 INMA-onay blokeri — kapsam DIŞI).
- Phone masking (operatör reuse için ham telefon gerekli; denetim = `export_logs`).
- Zamanlanmış/otomatik export, e-posta ile gönderim (yok).
- Server-side PDF (Chromium) — bilinçli reddedildi (§1).
- Yeni feature flag — `ContactListOptions` reuse.

**Forbidden:** `bulk_send_recipients`/`bulk_send_jobs` snapshot mantığına dokunma (immutable); `MessageSenderService`/INMA bridge'e dokunma (export read-only).

---

## 7. Risk: MEDIUM
Yeni tablo + yeni cross-service endpoint'ler + 2 yeni bağımlılık + PII export yüzeyi + proxy streaming. → ≥3 verification question (plan JSON'da).

---

## 8. Codex Plan-Review (2026-06-03, critique mode, gpt-5.5)

**ADOPTED (gerçek bulgular):**
1. **Send-results join underspecified** (en iyi yakalama) → base=`bulk_send_recipients` LEFT JOIN `outbound_messages`, never-sent=`not_sent`, status precedence, her tabloda tenant predicate, additive index. [§5 GR2'ye işlendi]
2. **Audit honesty** → `delivery_mode` kolonu + `completed`="veri servis edildi" tanımı + PDF report-data forensic alanları. [§3]
3. **ClosedXML 50k risk** → XLSX satır cap + no AdjustToContents + text hücreler + custom_fields tek JSON kolon + CancellationToken. [§5 GR2]
4. **Cache/header** → `no-store` + `nosniff` + sanitized `Content-Disposition`. [§5 GR2]
5. **Forensic** → `requested_by`=JWT subject id + `source_name_snapshot`. [§3]

**REJECTED (repo canon ile çelişiyor — pre-declared):**
- **GRANT ALL** → Codex least-privilege istedi; proje canon'u GRANT ALL (B1 downgrade FAIL precedent). Korunur + app-level append-only (INSERT-only repo).
- **Per-operator RBAC** → kodbazında granular operator-permission modeli YOK (tenant-scoped JWT). Q kararı "tüm operatörler export". Deliberate non-goal.

**Q sign-off (Codex#3 flag):** ExportOptions ayrı flag (ContactList reuse değil).

---

## 9. Build Sonucu + Plan Sapmaları (2026-06-03, GR1-GR4 DONE)

**Build:** Full .NET solution = 0 error; SPA (tsc + vite build) = temiz. ClosedXML 0.104.2 (server) + jspdf/jspdf-autotable (frontend) restore edildi.

**Plana eklenenler (gerekçeli, küçük):**
1. **`GET /api/v1/exports/send-jobs` (4. endpoint)** — Plan 3 endpoint öngörmüştü ama Dashboard kampanya seçicisi için "son bulk-send kampanyaları" listesi gerekti ve mevcut bir jobs-list endpoint'i YOKTU. Read-only metadata (id, campaign_id, status, total, tarih — PII yok), `ExportOptions` ile gate'li, tenant-scoped. Outbound + Backend proxy + api.ts `listSendJobs()`. `SendJobSummary` DTO eklendi.
2. **`OutboundClient.ProxyStreamGetAsync`** — Backend file-streaming proxy için `HttpCompletionOption.ResponseHeadersRead` ile yeni metot (body buffer ETMEZ, Q4). `OutboundClient.cs` allowed_files'a eklendi.

**Bilinen sınırlama (v1, dokümante):** jsPDF dahili fontları Türkçe glyph (ş/ğ/ı/İ) render edemez → PDF metni ASCII'ye transliterate edilir (`ş→s` vb). Forward edilebilir rapor okunur kalır; font embedding ileride (bundle maliyeti). CSV/XLSX UTF-8 tam Türkçe.

**Dosya envanteri:**
- GR1: `arch/db/migrations/053-export-logs.sql`, `arch/db/outbound.sql` (mirror), `src/Invekto.Shared/DTOs/Outbound/ExportDtos.cs`, `ErrorCodes.cs` (INV-OB-054..058), `arch/errors.md`, `src/Invekto.Outbound/Services/ExportOptions.cs`, `appsettings.json` (Export section)
- GR2: `src/Invekto.Outbound/Data/ExportRepository.cs`, `Services/ExportService.cs`, `Program.cs` (4 endpoint + ExportStatus helper), `Invekto.Outbound.csproj` (ClosedXML)
- GR3: `src/Invekto.Backend/Services/OutboundClient.cs` (stream metot), `Program.cs` (4 proxy route + OutboundProxyStreamGet helper)
- GR4: `src/Invekto.Backend/Dashboard/src/pages/ExportManagerPage.tsx`, `lib/api.ts` (blob helper + 4 metot + 4 interface), `App.tsx` (route), `components/Layout.tsx` (nav), `package.json` (jspdf×2)
