# FEAT-CLINIC-METADATA — Multi-Tenant Hardcoded Cleanup

> **Status:** DONE+CODEX_PASS (2026-04-29 16:25 UTC) — deploy bekliyor
> **Created:** 2026-04-28
> **Risk:** MEDIUM (Codex iter 0 CQ3 fix — schema migration + Shared abstraction + endpoint + Automation pipeline scope MEDIUM tam karşılıyor)
> **Slug:** `20260428-feat-clinic-metadata`
> **Plan:** [`arch/plans/20260428-feat-clinic-metadata.json`](../arch/plans/20260428-feat-clinic-metadata.json) (schema 5.1, 6 AC, 5 verification questions, 7 architectural decisions)
> **Codex:** iter 0 FAIL (3 blocker doc-only) → iter 1 PASS 12/12 CQ + 5/5 CoVe + 0 blocker (gpt-5.5, 70489 token)
> **Migration:** 040 (Q kararı 2026-04-28 23:50 + 2026-04-29 14:55 — 039 zaten dolu = FEAT-OBI-MEDIPOL-KANBAN-CARDS, 040 next-free)
> **INV-SEED:** 032..034 (Q kararı 2026-04-29 14:55 — 029..031 buffer slot rezerv)
> **Paralel paket notu:** `20260428-feat-roadmap-audit-fixes` (Migration 038 DONE) bu session'dan ayrı tamamlandı.

## 1. Amaç

Pilot için yapılan tüm özellikler tüm diş/sağlık kliniklerinin ortak ihtiyacı. Sistemin %95'i multi-tenant uyumlu — tek eksik FAQ metinleri + flow welcome text içindeki **3 hardcoded Dent-specific veri**. Bu paket bunları placeholder-driven yapar; 2. müşteri (başka klinik) gelirken sıfır SQL kopyalama olmadan, sadece Dashboard config ile çalışır olur.

## 2. Tespit Edilen 3 Hardcoded (2026-04-29 explore subagent kanıtladı)

| # | Hardcoded | Nerede | Refactor scope |
|---|-----------|--------|----------------|
| 1 | Klinik iletişim bilgileri (`dentadavista.com`, IG/FB URL'leri, `+90 545 343 09 09`, Kuşadası adresi) | Migration 032:140-142 (FAQ `safety_concern` 3 variant + `hotel_transfers` variant A) → `faqs` tablosu Dent tenant_id=18173130 | UPDATE faqs SET answer = REPLACE(answer, 'literal', '{{clinic.X}}') |
| 2 | Persona isimleri ("Dr. Özge Yılmazoğlu", "Güneş from Dent Adavista") | Migration 032:172-173 (chatbot_flows flow_id=29 nodes[1].data.text JSONB) + faqs variant A:116 + dent-roadshow-content.json | jsonb_set chatbot_flows + UPDATE faqs + JSON file edit |
| 3 | Roadshow tarihleri + şehirler ("Dublin (14 March) and Cork (15 March)") | chatbot_flows welcome + faqs variant'lar | {{campaign.cities_human}} + {{campaign.event_date}} (FEAT-MCC zaten render ediyor) |

**Kanıt:** Explore subagent çıktısı 2026-04-29 14:55 UTC + Migration 032 line scan + dent-roadshow-content.json grep.

## 3. Acceptance Criteria

| ID | Kriter | Doğrulama |
|----|--------|-----------|
| AC1 | Migration 040 prod'da uygulandıktan sonra `tenant_settings` tablosunda iki yeni JSONB sütun: `clinic_contact JSONB NOT NULL DEFAULT '{}'::jsonb` + `team_members JSONB NOT NULL DEFAULT '[]'::jsonb`; postcondition INV-SEED-032..034 PASS. | SELECT column_name FROM information_schema.columns + DO $verify$ block |
| AC2 | Dent için seed: `tenant_settings.clinic_contact` doldurulu (5 alan: phone, website, instagram, facebook, address) + `team_members` 2 üye (dentist Özge title='Founder Dentist' + receptionist Güneş title='Patient Coordinator', language=en, pronouns=she/her). Existing tenant'lar için `'{}'` ve `'[]'` default kalır. | SELECT clinic_contact, team_members FROM tenant_settings WHERE tenant_id=18173130 |
| AC3 | **ClinicTemplateApplier** (yeni dosya, CampaignTemplateApplier precedent) iki namespace destekler: `{{clinic.phone}}`, `{{clinic.website}}`, `{{clinic.instagram}}`, `{{clinic.facebook}}`, `{{clinic.address}}`, `{{team.dentist.name}}`, `{{team.receptionist.name}}`, `{{team.dentist.title}}`, `{{team.receptionist.title}}`. AutomationOrchestrator `_campaignApplier.ApplyAsync` sonrasi `_clinicApplier.ApplyAsync` cagrilir. Mevcut DMP UNTOUCHED. | AutomationOrchestrator.cs hook + ClinicTemplateApplier unit test |
| AC4 | FAQ + welcome metinleri içindeki hardcoded string'ler placeholder'a dönüştürüldü: (a) faqs 36 row UPDATE (Migration 032 INSERT'leri), (b) chatbot_flows flow_id=29 nodes[1].data.text JSONB jsonb_set, (c) dent-roadshow-content.json 16 lokasyon refactor. Smoke S1: Dent için render edildiğinde aynı çıktı (regression yok). | Migration 040 SQL ROW_COUNT assertion + manual render compare |
| AC5 | Dashboard `/settings/clinic-metadata` sayfası: 5-input form + team CRUD + form validation (E.164 phone + URL valid) + cross-tenant 403 guard (3-katmanlı: JWT + endpoint inline + SQL WHERE). Backend GET/PUT `/api/v1/tenant-settings/clinic-metadata` JWT-gated. | UI smoke + 3-tier auth test (NoAuth 401 + BadJWT 401 + ValidJWT cross-tenant 403) |
| AC6 | Multi-tenant validation: 2. test tenant default `'{}'` + `'[]'` durumda iken FAQ render placeholders boş string'e dönüşür (render bozulmaz). Build PASS: dotnet build + Dashboard tsc/vite. | Integration smoke + dotnet build solution |

## 4. Implementation Adımları (~6 iş günü)

### Adım 1: Migration 040 (~0.5 gün)
- Dosya: `arch/db/migrations/040-clinic-metadata.sql`
- Atomic BEGIN/COMMIT
- Pre-state snapshot tablosu `dent_paket_clinic_metadata_archive_20260429` (Migration 032 precedent)
- §1 ALTER TABLE tenant_settings ADD COLUMN IF NOT EXISTS clinic_contact JSONB NOT NULL DEFAULT '{}'::jsonb
- §1 ALTER TABLE tenant_settings ADD COLUMN IF NOT EXISTS team_members JSONB NOT NULL DEFAULT '[]'::jsonb
- §2 Dent seed UPDATE WHERE tenant_id=18173130 (5 + 2 üye)
- §3 faqs UPDATE 36 row (literal → placeholder, REPLACE)
- §4 chatbot_flows flow_id=29 nodes[1].data.text jsonb_set
- §5 DO $verify$ block (INV-SEED-032..034): kolon varlık + Dent seed + ROW_COUNT
- Canonical mirror: `arch/db/tenant-settings.sql`

### Adım 2: Shared.Contracts.ClinicMetadata (~0.5 gün)
- DTO'lar: `ClinicContactDto`, `TeamMemberDto`, `ClinicMetadata` (root, JsonPropertyName snake_case)
- Interface: `ITenantClinicMetadataResolver` (GetAsync + Invalidate)
- DB Impl: `DbTenantClinicMetadataResolver` (5dk MemoryCache + CT-safe single-flight + INV-BE-125 fallback)

### Adım 3: ClinicTemplateApplier (~0.5 gün)
- Dosya: `src/Invekto.Automation/Services/ClinicTemplateApplier.cs` (CampaignTemplateApplier precedent)
- 2 regex: `\{\{clinic\.([a-z_]+)\}\}` + `\{\{team\.([a-z_]+)\.([a-z_]+)\}\}`
- ApplyAsync(tenantId, text, ct) → substitute or NoOp
- Token resolution: clinic.X → ClinicContact[X], team.role.field → TeamMembers.FirstOrDefault(role).field

### Adım 4: AutomationOrchestrator hook (~0.25 gün)
- Mevcut `_campaignApplier.ApplyAsync` çağrısından sonra `_clinicApplier.ApplyAsync`
- ctor +1 parametre
- Mevcut campaign substitution UNTOUCHED

### Adım 5: Backend ClinicMetadataEndpoints (~1 gün)
- Dosya: `src/Invekto.Backend/Endpoints/ClinicMetadataEndpoints.cs`
- GET /api/v1/tenant-settings/clinic-metadata → JWT TenantContext + SELECT
- PUT /api/v1/tenant-settings/clinic-metadata → validation + UPSERT WHERE tenant_id=@tid + cache.Invalidate
- 3-katmanlı cross-tenant guard
- Validation: E.164 phone regex + URL valid (https://?... regex) + 6-key clinic_contact + 5-key team_members shape

### Adım 6: Dashboard SPA UI (~2 gün)
- Dosya: `src/Invekto.Backend/Dashboard/src/pages/settings/ClinicMetadataSettingsPage.tsx`
- 5-input form (phone/website/instagram/facebook/address)
- Team CRUD (ekle/düzenle/sil/sıralama drag-handle)
- Dil dropdown (TR/EN/AR/DE/FR/diğer)
- API client: `src/Invekto.Backend/Dashboard/src/api/clinic-metadata.ts`
- Route: `App.tsx` + `SettingsPage.tsx` link

### Adım 7: Seed + JSON refactor (~0.5 gün)
- `DentAdavista/seeds/clinic-metadata-dent.json` source-of-truth (operator reference)
- `DentAdavista/seeds/dent-roadshow-content.json` 16 lokasyon refactor (hardcoded → placeholder)

### Adım 8: Build + /rev (~1 gün)
- `dotnet build C:\CRMs\InvektoServices\InvektoServis.sln` PASS
- Dashboard `tsc --noEmit` + `vite build` PASS
- /rev MCP codex_review (LOW, iter 0 hedefi — CODEX UTANSIN)

## 5. Allowed Files

```
arch/db/migrations/040-clinic-metadata.sql
arch/db/tenant-settings.sql
arch/errors.md
arch/plans/20260428-feat-clinic-metadata.json
src/Invekto.Shared/Contracts/ClinicMetadata/ClinicContactDto.cs
src/Invekto.Shared/Contracts/ClinicMetadata/TeamMemberDto.cs
src/Invekto.Shared/Contracts/ClinicMetadata/ClinicMetadata.cs
src/Invekto.Shared/Contracts/ClinicMetadata/ITenantClinicMetadataResolver.cs
src/Invekto.Shared/Contracts/ClinicMetadata/DbTenantClinicMetadataResolver.cs
src/Invekto.Shared/Constants/ErrorCodes.cs
src/Invekto.Automation/Services/ClinicTemplateApplier.cs
src/Invekto.Automation/Services/AutomationOrchestrator.cs
src/Invekto.Automation/Program.cs
src/Invekto.Backend/Endpoints/ClinicMetadataEndpoints.cs
src/Invekto.Backend/Program.cs
src/Invekto.Backend/Dashboard/src/pages/settings/ClinicMetadataSettingsPage.tsx
src/Invekto.Backend/Dashboard/src/api/clinic-metadata.ts
src/Invekto.Backend/Dashboard/src/lib/api.ts
src/Invekto.Backend/Dashboard/src/App.tsx
src/Invekto.Backend/Dashboard/src/pages/SettingsPage.tsx
DentAdavista/seeds/clinic-metadata-dent.json
DentAdavista/seeds/dent-roadshow-content.json
tracking/feat-clinic-metadata.md
tracking/README.md
```

## 6. Error Codes

- **INV-SEED-032..034**: Migration 040 postcondition (kolonlar + Dent seed + faqs/chatbot_flows ROW_COUNT)
- **INV-BE-122**: Endpoint malformed JSON shape (400)
- **INV-BE-123**: Endpoint phone E.164 format invalid (400)
- **INV-BE-124**: Endpoint URL format invalid (400)
- **INV-BE-125**: ClinicMetadata DB unavailable (503, log + Empty fallback)

## 7. Scope Discipline

**Forbidden areas:**
- INMA tarafı (read-only) — tenant_settings INSE'ye özel
- Mevcut FEAT-DMP altyapısı (DynamicMessageValidator + CampaignTemplateApplier UNTOUCHED, sadece yeni applier)
- Mevcut FEAT-MCC campaign config (zaten çalışıyor)
- Outbound BroadcastOrchestrator/MessageSenderService (template_catalog generic, hardcoded yok)
- Knowledge service AI FAQ retrieval (substitution downstream chat-send hot path'inde)

**Non-goals:**
- Çok dilli template render (welcome locale switch) — ayrı paket
- Team members rol-bazlı yetki (RBAC) — sadece template render için, auth değil
- Klinik logo upload — sadece URL alanı, dosya storage değil
- Roadshow tarih takvim UI — campaign config sayfasında zaten var
- Multi-dentist index syntax `{{team.dentist[0].name}}` — ilk match döner, post-pilot Phase 2

**Intentional exclusions:**
- Migration 040 rollback scripti — postcondition self-validating + snapshot table
- 5+ team member aynı role pattern — array filter ilk match
- Phone format internationalization beyond E.164
- INV-SEED-029..031 buffer (Q kararı — bu paket 032..034)
- DynamicMessageResolver.cs single-file refactor (mevcut DMP iki katmanlı mimari preserved)

## 8. AHA Moments

| Kategori | User Pain | Suggestion | AHA Moment |
|----------|-----------|------------|------------|
| **SALES** | "2. müşteri için sistem hazırlamak 1 hafta SQL/seed kopyalama (36 FAQ + 1 flow welcome JSONB + 16 JSON lokasyon)" | Dashboard'da clinic_contact + team_members + campaign config formu doldurmak. | Sales 1 günde yeni klinik aktive ediyor; demo to live <1 gün. |
| **UX** | Dent koordinatörü WhatsApp'taki Güneş'in iletişim bilgilerini güncellemek için support ticket açıyor | `/settings/clinic-metadata` self-service + 5dk cache invalidate | Koordinatör doğrudan değiştirip "Anında WhatsApp'ta görüyorum" diyor. |
| **RELIABILITY** | Roadshow tarihi değişti ama 36 FAQ + 1 welcome elle güncellendi → 37 lokasyon, kopyalama hatası | Tarih campaign config'de tek yerde, FAQ + welcome otomatik render | "Tek alan değiştirince tüm metinler güncellendi" |
| **SUPPORT** | Multi-tenant pilot 2. müşteri destek bileti: "Bizim FAQ'da Dent Adavista yazıyor" | Hardcoded olmayınca, default `{}` ile boş alan görünür | Bu hatayla hiç karşılaşmıyoruz. |
| **SPEED** | FEAT-DMP DONE 2026-04-20; CampaignTemplateApplier precedent var (FEAT-MCC DONE) | Existing pattern reuse — minimum kod, additive | Codex iter 0 PASS hedefi (CODEX UTANSIN). |

## 9. Smoke Senaryoları

| # | Senaryo | Beklenen |
|---|---------|----------|
| S1 | Dent için `safety_concern` FAQ variant C render (placeholder'lı yeni şablon) | Çıktı eski hardcoded metin ile **birebir aynı** olmalı (regression yok) |
| S2 | Yeni test tenant default `clinic_contact={}` + `team_members=[]` durumda iken FAQ render | Placeholder'lar boş string'e dönüşür, render bozulmaz |
| S3 | Test tenant için Dashboard'dan `clinic_contact` doldur + `team_members` ekle | FAQ render edildiğinde test tenant'ın bilgilerini gösterir, Dent'inkini değil |
| S4 | Cross-tenant guard: tenant A koordinatörü PUT ile tenant B verisini modify denemesi | 403 + INV-AUTH-010 + INV-AT-080 audit |
| S5 | Phone validation: `+invalid-format` PUT | 400 + INV-BE-123 |
| S6 | URL validation: `not-a-url` PUT | 400 + INV-BE-124 |
| S7 | Migration 040 idempotent re-run (ALTER TABLE IF NOT EXISTS + UPDATE re-execution) | İkinci run ROW_COUNT=0 → fail-loud RAISE EXCEPTION (operator awareness) |
| S8 | 5dk MemoryCache invalidate: PUT sonrası GET, render anlık fresh | Backend instance cache invalidate ✓; Automation/Marketing peer cache 5dk eventual consistency (FEAT-TFM/MCC pattern) |

## 10. Dependencies

- **Bağımlılık:** FEAT-DMP (DONE 2026-04-20) — DynamicMessageValidator INMA-side validator (UNTOUCHED)
- **Bağımlılık:** FEAT-MCC (DONE 2026-04-22) — CampaignTemplateApplier precedent + {{campaign.*}} render
- **Bağımlılık:** FEAT-TFM (DONE 2026-04-21) — DbTenantFieldMappingResolver pattern (5dk cache + single-flight + auth hotfix commit 44780d0)
- **Etki:** Mevcut FAQ + welcome render flow'u — backward compatible, regression smoke S1 ile koruma altında

## 11. Pilot ile İlişki

**Pilot blocker DEĞİL.** Dent zaten hardcoded değerlerle çalışır, hata vermez. Ancak **2. müşteri öncesi MUTLAKA tamamlanmalı**.

**Önerilen yerleşim:**
- Stage 1 (yumuşak açılış): Dahil değil — pilot mevcut hardcoded ile başlar
- Stage 2 (sınırlı test): Paralel inşa — Dent operasyonu etkilenmez
- Stage 3 (tam açılış): DONE durumda olmalı — bu noktadan sonra 2. müşteri sırada

## 12. Codex Review Hazırlığı (CODEX UTANSIN — iter 0 hedefi)

Risk LOW (additive migration + content refactor + minimum runtime kod, mevcut DMP UNTOUCHED). 5 architectural decision pre-declared (plan JSON `spec_architectural_decisions`):

| ID | Karar | Gerekçe |
|----|-------|---------|
| AD1 | ClinicTemplateApplier yeni dosya (CampaignTemplateApplier precedent) | Mevcut DMP iki katmanlı mimari preserve, additive |
| AD2 | INV-SEED-032..034 (029..031 buffer) | Q numara aralığı disiplini tampon |
| AD3 | Outbound TemplateEngine hook YOK | Scope discipline: chat-send hot path yeterli |
| AD4 | 3-katmanlı cross-tenant guard | TFM-AUTH-HOTFIX precedent commit 44780d0 |
| AD5 | Migration 040 sequential (039 dolu) | Plan eski metadata düzeltildi |

Verification questions (4):
| ID | Soru | Kategori |
|----|------|----------|
| Q1 | Backward compatibility nasıl korunuyor (mevcut DMP UNTOUCHED, regex isolation) | Lifecycle |
| Q2 | team_members aynı role 2 kayıt → first-match LINQ FirstOrDefault, multi-dentist edge case post-pilot | Data |
| Q3 | Cross-tenant guard 3-katmanlı (TFM-AUTH-HOTFIX precedent) | Auth |
| Q4 | Migration 040 atomic + idempotent re-run + snapshot | Process/Policy |

## 13. Tracking

- Plan JSON: `arch/plans/20260428-feat-clinic-metadata.json` (IN_PROGRESS, status_log 2 entry)
- Toplantı bağlamı: `DentAdavista/plan/dent-toplanti.html §16`
- Golive bağlamı: `DentAdavista/plan/dent-golive.html §18`
- Kod hardcoded audit: 2026-04-29 explore subagent çıktısı (faqs + chatbot_flows + dent-roadshow-content.json)
- Dent tenant_id doğrulama: 2026-04-29 14:55 UTC MCP postgres SELECT FROM tenant_registry → dentadavista, plan_tier=kurumsal, is_active=true, inma_code=dentadavista
