# FEAT-CLINIC-METADATA — Multi-Tenant Hardcoded Cleanup

> **Status:** PLANNING (plan JSON committed; dev sonraki session)
> **Created:** 2026-04-28
> **Risk:** LOW (additive migration + content refactor; runtime kod minimum değişir)
> **Slug:** `20260428-feat-clinic-metadata`
> **Plan:** [`arch/plans/20260428-feat-clinic-metadata.json`](../arch/plans/20260428-feat-clinic-metadata.json) (schema 5.1, 6 AC, 4 verification questions)
> **Migration:** 040 (Q kararı 2026-04-28 23:50 — audit-fixes paketi 038, buffer 039 reserved, clinic-metadata 040)
> **Paralel paket notu:** `20260428-feat-roadmap-audit-fixes` (Migration 038, HIGH risk REVIEW pending) bu session'dan ayrıldı (Q kararı C); ayrı session'da devam edecek.

## 1. Amaç

Pilot için yapılan tüm özellikler tüm diş/sağlık kliniklerinin ortak ihtiyacı. Sistemin %95'i multi-tenant uyumlu — tek eksik FAQ metinleri + flow welcome text içindeki **3 hardcoded Dent-specific veri**. Bu paket bunları placeholder-driven yapar; 2. müşteri (başka klinik) gelirken sıfır SQL kopyalama olmadan, sadece Dashboard config ile çalışır olur.

## 2. Tespit Edilen 3 Hardcoded (2026-04-28 audit)

| # | Hardcoded | Nerede |
|---|-----------|--------|
| 1 | Klinik iletişim bilgileri (`dentadavista.com`, IG/FB URL'leri, `+90 545 343 09 09`) | Migration 032:140-142 (FAQ `safety_concern` variant C) + `dent-roadshow-content.json`:174,187 |
| 2 | Persona isimleri ("Dr. Özge Yılmazoğlu", "Güneş from Dent Adavista") | Migration 032:172-173 (flow welcome) + FAQ variant A:116 + roadshow content |
| 3 | Roadshow tarihleri + şehirler ("Dublin (14 March) and Cork (15 March)") | FAQ A/B variants + welcome text |

**Audit detayı:** `DentAdavista/plan/dent-toplanti.html §16` + `dent-golive.html §18`.

## 3. Acceptance Criteria

| ID | Kriter | Doğrulama |
|----|--------|-----------|
| AC1 | Migration 040 prod'da uygulandıktan sonra `tenant_settings` tablosunda iki yeni JSONB sütun bulunur: `clinic_contact JSONB DEFAULT '{}'` + `team_members JSONB DEFAULT '[]'`; postcondition INV-SEED-024..026 PASS. | SELECT column_name FROM information_schema.columns |
| AC2 | Dent için seed: `tenant_settings.clinic_contact` doldurulu (5 alan: phone, website, instagram, facebook, address) + `team_members` 2 üye içerir (dentist Özge + receptionist Güneş, language=en). Existing tenant'lar için `'{}'` ve `'[]'` default kalır. | SELECT clinic_contact, team_members FROM tenant_settings WHERE tenant_id=18173130 |
| AC3 | DMP resolver (`DynamicMessageResolver`) iki yeni namespace destekler: `{{clinic.phone}}`, `{{clinic.website}}`, `{{clinic.instagram}}`, `{{clinic.facebook}}`, `{{team.dentist.name}}`, `{{team.receptionist.name}}`. Render edildiğinde DB'den okur, fallback boş string (hata fırlatmaz). | Unit test: PlaceholderResolverTests |
| AC4 | FAQ + welcome metinleri içindeki hardcoded string'ler placeholder'a dönüştürüldü (SQL UPDATE migration 035 sonu): "Dr. Özge Yılmazoğlu" → `{{team.dentist.name}}`, "+90 545 343 09 09" → `{{clinic.phone}}`, "https://dentadavista.com" → `{{clinic.website}}`, "Dublin (14 March)" → `{{campaign.cities[0].name}} ({{campaign.dates[0] \| format}})`. Smoke: Dent için render edildiğinde aynı çıktıyı verir (regression yok). | E2E smoke: `safety_concern` FAQ variant C render → eski metinle birebir aynı |
| AC5 | Dashboard `/settings/clinic-metadata` sayfası: iletişim bilgileri formu (5 input) + team members CRUD (ekle/düzenle/sil/sıralama/dil seç). Cross-tenant 403 guard. Form validation: phone E.164 format, URL valid. | UI smoke: yeni alan ekleme + listeye yansıma + tenant guard |
| AC6 | Multi-tenant validation: 2. test tenant açılır (mock), Dashboard üzerinden `clinic_contact` + `team_members` doldurulur, mevcut Dent FAQ template'leri o tenant için render edildiğinde **doğru** klinik bilgilerini gösterir (Dent'inkini değil). | Manual smoke: test tenant → render compare |

## 4. Implementation Adımları (~6 iş günü)

### Adım 1: Migration 040 (~0.5 gün)
- Dosya: `arch/db/migrations/040-clinic-metadata.sql`
- ALTER TABLE tenant_settings ADD COLUMN IF NOT EXISTS clinic_contact JSONB DEFAULT '{}'
- ALTER TABLE tenant_settings ADD COLUMN IF NOT EXISTS team_members JSONB DEFAULT '[]'
- Postcondition DO $verify$ block (INV-SEED-024..026): kolonların varlığı + Dent seed satır kontrolü
- Canonical mirror: `arch/db/tenant-settings.sql`

### Adım 2: Dent Seed (Migration 040 sonu, ~0.5 gün)
```sql
UPDATE tenant_settings
SET clinic_contact = '{
  "phone": "+90 545 343 09 09",
  "website": "https://dentadavista.com",
  "instagram": "https://www.instagram.com/dentadavistaclinic",
  "facebook": "https://www.facebook.com/profile.php?id=61554804412823",
  "address": "Kuşadası, Aydın, Türkiye"
}'::jsonb,
team_members = '[
  {"role": "dentist", "name": "Dr. Özge Yılmazoğlu", "pronouns": "she/her", "language": "en", "title": "Founder Dentist"},
  {"role": "receptionist", "name": "Güneş", "pronouns": "she/her", "language": "en", "title": "Patient Coordinator"}
]'::jsonb
WHERE tenant_id = 18173130;
```

### Adım 3: Template Refactor (~1 gün)
- Dosya: `arch/db/migrations/040-clinic-metadata.sql` (aynı migration sonu)
- 36 FAQ + 15 welcome variant + roadshow content seed UPDATE
- Hardcoded string → placeholder dönüşümü
- Smoke öncesi: snapshot al (rollback için)

### Adım 4: DMP Resolver Namespace Genişletme (~1 gün)
- Dosya: `src/Invekto.Shared/Services/DynamicMessageResolver.cs`
- Yeni namespace handler:
  - `clinic.{key}` → `tenant_settings.clinic_contact->>key`
  - `team.{role}.{key}` → `tenant_settings.team_members` array filter on role, return key
- Cache: 5dk MemoryCache (TFM precedent)
- Hata yönetimi: alan yoksa boş string (template render bozulmaz)

### Adım 5: Dashboard UI (~2 gün)
- Dosya: `src/Invekto.Backend/Dashboard/src/pages/settings/ClinicMetadataSettingsPage.tsx`
- Backend endpoint: `GET/PUT /api/v1/tenant-settings/clinic-metadata`
- Form validation: E.164 phone, URL şeması
- Team members CRUD: ekle/düzenle/sil/sıralama/dil dropdown (TR/EN/AR/DE/FR + diğerleri)

### Adım 6: Smoke + Dokümantasyon (~1 gün)
- Spec: `arch/features/clinic-metadata.md`
- Unit test: PlaceholderResolverTests (clinic.* + team.* namespaces)
- E2E smoke: Dent için 36 FAQ render compare (placeholder render = original hardcoded string)
- Multi-tenant smoke: 2. test tenant ile cross-render guard

## 5. Allowed Files

```
arch/db/migrations/040-clinic-metadata.sql
arch/db/tenant-settings.sql
arch/errors.md
arch/features/clinic-metadata.md
arch/plans/20260428-feat-clinic-metadata.json
src/Invekto.Shared/Services/DynamicMessageResolver.cs
src/Invekto.Shared/Contracts/ClinicMetadata/ClinicContactDto.cs
src/Invekto.Shared/Contracts/ClinicMetadata/TeamMemberDto.cs
src/Invekto.Backend/Endpoints/ClinicMetadataEndpoints.cs
src/Invekto.Backend/Dashboard/src/pages/settings/ClinicMetadataSettingsPage.tsx
src/Invekto.Backend/Dashboard/src/api/clinic-metadata.ts
DentAdavista/seeds/clinic-metadata-dent.json
tracking/feat-clinic-metadata.md
```

## 6. Error Codes

- INV-SEED-024..026: Migration 040 postcondition (kolonlar + Dent seed kontrolü)
- INV-BE-{nextfree}..: Endpoint validation (cross-tenant 403, phone format, URL invalid)

## 7. Scope Discipline

**Forbidden areas:**
- INMA tarafı (read-only) — tenant_settings INSE'ye özel
- Mevcut FEAT-DMP altyapısı (modifikasyon değil, sadece namespace ekleme)
- Mevcut FEAT-MCC campaign config (zaten çalışıyor)

**Non-goals:**
- Çok dilli template render (welcome locale switch) — ayrı paket
- Team members rol-bazlı yetki (RBAC) — sadece template render için kullanılır, auth değil
- Klinik logo upload — sadece URL alanı, dosya storage değil
- Roadshow tarih takvim UI — campaign config sayfasında zaten var

**Intentional exclusions:**
- Migration 040 rollback scripti — postcondition INV-SEED-024..026 RAISE EXCEPTION ile self-validating
- 5+ team member aynı role pattern (örn. 3 dentist) — array filter ilk match döner, edge case post-pilot
- Phone format internationalization beyond E.164

## 8. AHA Moments

| Kategori | User Pain | Suggestion | AHA Moment |
|----------|-----------|------------|------------|
| **SALES** | "2. müşteri için sistem hazırlamak 1 hafta SQL/seed kopyalama" | Dashboard'da clinic_contact + team_members + campaign config formu doldurmak. | Sales 1 günde yeni klinik aktive ediyor; demo to live <1 gün. |
| **UX** | Dent koordinatörü WhatsApp'taki Güneş'in iletişim bilgilerini güncellemek için support ticket açıyor | `/settings/clinic-metadata` self-service | Koordinatör doğrudan değiştirip "Anında WhatsApp'ta görüyorum" diyor. |
| **RELIABILITY** | Roadshow tarihi değişti ama 12 FAQ + 15 welcome metni elle güncellendi → 27 dosya, kopyalama hatası | Tarih campaign config'de tek yerde, FAQ'lar otomatik render | "Tek alan değiştirince tüm metinler güncellendi" |
| **SUPPORT** | Multi-tenant pilot 2. müşteri destek bileti: "Bizim FAQ'da Dent Adavista yazıyor" | Hardcoded olmayınca, 2. müşteri default `{}` ile başlar; kendi config'ini doldurana kadar boş alan görünür (placeholder fallback empty). | Bu hatayla hiç karşılaşmıyoruz. |
| **SPEED** | FEAT-DMP altyapısı zaten hazır (DONE 2026-04-20); yeni placeholder eklemek 1 gün | Existing pattern reuse — minimum kod | Refactor risk düşük, paket 6 günde DONE. |

## 9. Smoke Senaryoları

| # | Senaryo | Beklenen |
|---|---------|----------|
| S1 | Dent için `safety_concern` FAQ variant C render (placeholder'lı yeni şablon) | Çıktı eski hardcoded metin ile **birebir aynı** olmalı (regression yok) |
| S2 | Yeni test tenant (örn. tenant_id=99001) `clinic_contact={}` ve `team_members=[]` durumda iken FAQ render | Placeholder'lar boş string'e dönüşür, render bozulmaz |
| S3 | Test tenant için Dashboard'dan `clinic_contact` doldur + `team_members` ekle | FAQ render edildiğinde test tenant'ın bilgilerini gösterir, Dent'inkini değil |
| S4 | Cross-tenant guard: tenant A koordinatörü `/api/v1/tenant-settings/clinic-metadata` ile tenant B verisini okumaya çalışır | 403 + INV-AUTH log |
| S5 | Phone validation: `+invalid-format` → 400 + INV-BE-{code} |
| S6 | Team member sıralama: 3 üye eklendi, sıra değiştirildi, FAQ render `{{team.dentist.name}}` doğru üyeyi gösterir | Array order persistence |
| S7 | Migration 040 idempotent re-run (ALTER TABLE IF NOT EXISTS + UPDATE re-execution) | İki kez çalıştırılabilir, hata yok |
| S8 | Dent için 36 FAQ + 15 welcome variant render → eski hardcoded metinlerle 36 + 15 eşleşme karşılaştırması | %100 match, sadece formatlama farkı yok |

## 10. Dependencies

- **Bağımlılık:** FEAT-DMP (DONE 2026-04-20) — placeholder altyapısı + rendering pipeline
- **Bağımlılık:** FEAT-MCC (DONE 2026-04-22) — campaign config (cities/dates) zaten placeholder ile render
- **Bağımlılık:** FEAT-TFM (DONE 2026-04-21) — pattern referansı (5dk cache, single-flight, cross-tenant guard)
- **Etki:** Mevcut FAQ + welcome render flow'u — backward compatible, regression smoke S1 ile koruma altında

## 11. Pilot ile İlişki

**Pilot blocker DEĞİL.** Dent zaten hardcoded değerlerle çalışır, hata vermez. Ancak **2. müşteri öncesi MUTLAKA tamamlanmalı**.

**Önerilen yerleşim:**
- Stage 1 (yumuşak açılış): Dahil değil — pilot mevcut hardcoded ile başlar
- Stage 2 (sınırlı test): Paralel inşa — Dent operasyonu etkilenmez
- Stage 3 (tam açılış): DONE durumda olmalı — bu noktadan sonra 2. müşteri sırada

## 12. Codex Review Hazırlığı

Risk LOW (additive migration + content refactor + minimum runtime kod). Tek iter PASS hedefi. Verification questions (Codex review için):

| ID | Soru | Kategori |
|----|------|----------|
| Q1 | DynamicMessageResolver namespace genişletmesi mevcut `{{name}}`, `{{cf1}}` lookup path'ini bozar mı? Backward compatibility nasıl korunuyor? | Lifecycle |
| Q2 | `team_members` JSONB array içinde aynı role 2 kayıt varsa (örn. 2 dentist) `{{team.dentist.name}}` hangisini döner? Determinism guarantee var mı? | Data |
| Q3 | Tenant A koordinatörü PUT /api/v1/tenant-settings/clinic-metadata ile tenant B JSONB'ı modify etmeyi deneyebilir mi? Cross-tenant guard hangi katmanda? | Auth |
| Q4 | Migration 040 prod'da çalıştırıldığında mevcut 36 FAQ + 15 welcome variant SQL UPDATE'i başarısız olursa transaction rollback olur mu, yoksa partial state mı kalır? | Process/Policy |

## 13. Tracking

- Spec: `arch/features/clinic-metadata.md` (oluşturulacak)
- Plan JSON: `arch/plans/20260428-feat-clinic-metadata.json` (oluşturulacak)
- Toplantı bağlamı: `DentAdavista/plan/dent-toplanti.html §16`
- Golive bağlamı: `DentAdavista/plan/dent-golive.html §18`
- Kod hardcoded audit: 2026-04-28 Explore subagent çıktısı (transcripts)
