# SPEC: Tenant Field Mapping (INMA 10-Field Semantic Overlay)

> **Spec ID:** FEAT-TFM | **Paket:** TBD | **Risk:** MEDIUM
> **Yazar:** Q | **Son Guncelleme:** 2026-04-16 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

INMA platformu her tenant'a 10 generic custom field (muhtemelen `custom_1` ... `custom_10` ya da benzer isimler) saglar. Her tenant kendi business domain'inde bu field'lari farkli semantic ile kullanir:

- Dental klinik: `roadshow_city`, `appointment_slot`, `offer_status`, `deposit_status`, `flight_booked`
- E-ticaret: `cart_id`, `order_id`, `shipping_carrier`, `refund_status`
- Egitim: `course_slug`, `enrollment_stage`, `tuition_paid`, `scholarship_eligible`

Amac: INSE kod tarafi `custom_N` degil **semantic isim** ile calissin (`lead.roadshow_city` vs `lead.custom_1`). Ayrica flow builder, template editor, dashboard UI hepsi semantic isimle gorunur/calisir. Validasyon tiplari (enum, date, bool) tenant-level tanimli.

Custom field tablosu (G4) IPTAL edildi (Karar #7, 2026-04-13) — INMA'nin 10 field'i yeterli. Bu feature ek kolon ekleme DEGIL, mevcut 10 field'a **semantic overlay** kurmaktir.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | `tenant_settings.field_mapping JSONB` — key=semantic_name, value=`{ source: "custom_1", type: "enum\|string\|date\|bool\|int", enum_values?: [...], required?: bool }` | UI editor |
| AC-2 | `ITenantFieldResolver` Shared service — `Resolve(tenantId, semanticName) → FieldDescriptor` | Unit test |
| AC-3 | Lead read: semantic key lookup `lead.GetField("roadshow_city")` → INMA `custom_1` value | Integration test |
| AC-4 | Lead write: semantic set + validation (enum uymayan deger → 400) | Integration test |
| AC-5 | Flow builder UI: condition node field dropdown tenant semantic isimleri listeler | Dashboard screenshot |
| AC-6 | Template substitution: `{{lead.roadshow_city}}` resolve ederek custom_1 degerini render | Render snapshot |
| AC-7 | Tenant mapping degisince cache invalidate (yeni read'lerde fresh) | Cache busting test |
| AC-8 | Reserved semantic names (name, phone, email, created_at gibi) INMA-native alanlar — mapping override edemez | Validation 400 |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| JSONB config tenant_settings'de (`field_mapping`) | Tenant nadir degisir, ayri tablo overkill | — |
| Semantic name alphanumeric_snake_case zorunlu | URL-safe, flow DSL'de guvenli | — |
| Validation tenant-level (type + enum_values), merkezi degil | Her tenant kendi enum'u | — |
| Resolver IMemoryCache 5dk TTL | Hot path (her mesaj template substitution'da cagrilir) | CQ11: cache pattern G3 mirror |
| Lead okumasi = INMA API call mi INSE local copy mi? | **Decision: INSE'de local mirror** (performance + flow context) — webhook/polling ile sync | CQ9: microservice isolation — INMA source of truth, INSE mirror |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Field mapping schema | `arch/contracts/tenant-field-mapping.json` (yeni) |
| DB Schema | `leads.custom_1` ... `leads.custom_10` TEXT — `arch/db/pkt6b1-niche-business.sql` ALTER |
| INMA contract | `Invekto.Shared/Contracts/Inma/InmaCustomFieldsDto.cs` (mevcut/yeni) |
| Shared Service | `Invekto.Shared/Services/ITenantFieldResolver.cs` + impl |
| Error Codes | INV-BE-096 (field_mapping_invalid), INV-BE-097 (reserved_semantic_name), INV-BE-098 (enum_value_not_allowed), INV-BE-099 (field_source_out_of_range) |

## 5. Scope Boundaries

### In Scope
- `tenant_settings.field_mapping JSONB`
- `leads.custom_1` ... `custom_10` kolonlari (INMA mirror)
- `ITenantFieldResolver` + impl + cache
- Dashboard field mapping editor UI
- Template substitution genisletmesi (`{{lead.<semantic>}}`)
- Flow builder condition node UI (semantic picker)
- INMA field sync (webhook + polling fallback)
- Validation engine (type + enum)

### Out of Scope (Explicit)
- 11+ field support (INMA 10 ile sinirli)
- Custom types (only enum/string/date/bool/int — no JSON/array)
- Multi-tenant shared semantic dictionary
- Field-level access control (v2)
- Historical value tracking per field (v2)

### Degismeyen Alanlar (Pre-existing)
- Mevcut `leads` tablosu core kolonlari (id, tenant_id, phone, full_name, email, created_at, pipeline_status)
- INMA READONLY lisans pattern (custom field read/write ayri contract)
- INMA API auth (X-CIB-SecretKey)

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Backend | Config CRUD + resolver host | Yeni endpoints |
| Integrations (or ChatAnalysis) | INMA custom field sync worker | Yeni method |
| Automation | Template substitution + flow condition resolve | Minor |
| Dashboard | Field mapping editor + flow builder UI extension | Yeni page + extension |
| Shared | Resolver interface + DTO | Yeni |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|-----------|
| INMA field rename → INSE mapping kopar | MEDIUM | Mapping snapshot version + INMA schema drift detection (daily check) |
| Enum value INMA'da degisir, INSE validation reject eder | HIGH | Tenant admin notification UI + mapping auto-sync proposal |
| 11. field ihtiyaci ortaya cikar | MEDIUM | Roadmap: INMA ile 15 field'a cikarma konusmasi; veya INSE-local `lead_extensions` tablosu v2 |
| Reserved name carpismasi | LOW | Validation + reserved list whitelist (name, phone, email, created_at, pipeline_status) |

## 8. Pilot Consumer

Dent Adavista — 5 alan mapping (`roadshow_city`, `appointment_slot`, `offer_status`, `deposit_status`, `flight_booked`), 5 alan reserve (ileride). Detay: `DentAdavista/plan/pilot-field-mapping.md`.
