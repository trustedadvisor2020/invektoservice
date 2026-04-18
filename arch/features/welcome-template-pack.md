# SPEC: Welcome Template Pack

> **Spec ID:** FEAT-WTP | **Paket:** 20260417-feat-wtp-welcome-faq-rotation | **Risk:** MEDIUM
> **Yazar:** Q | **Son Guncelleme:** 2026-04-17 | **Durum:** IMPLEMENTED (review pending)

## 1. Intent (Ne & Neden)

Her tenant kendi sektoru icin N welcome varyanti + M FAQ intent (her biri K cevap varyanti) yukleyip calistirabilmeli. Amac: ilk mesaj tek-kalip sikici olmasin + FAQ cevaplari robotik hissettirmesin + kanitlanabilir A/B karsilastirma yapilabilsin.

Mevcut durum: Knowledge servisinde template sistemi (5 tip, 3 katman — Platform/Sector/Tenant) var, pgvector cosine similarity suggestion calisiyor. Eksik:
- Welcome-spesifik gruplama (tarihli vs tarihsiz gibi operational group)
- Deterministik A/B rotation (ayni lead her zaman ayni varyanti alsin)
- FAQ intent'te N-cevap round-robin (lead bazinda)

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | Tenant N welcome template yukleyebiliyor (group tag ile, ornegin `welcome_with_date`, `welcome_no_date`) | DB: `template_catalog` row'larinda `group_tag` kolonu populated |
| AC-2 | Welcome gonderimi `hash(contact_key) % N` ile deterministik secim yapiyor | Unit test: ayni phone 10 kez denenince ayni variant_index donuyor |
| AC-3 | FAQ intent cevap varyantlari round-robin rotation (lead bazinda `rotation_index`) | `leads.faq_rotation_state` JSONB guncelleniyor per intent |
| AC-4 | Template dili lead `preferred_locale` ile filtrelenir; 'en' fallback | **MET (HFM-2 2026-04-17):** `ExecutionContext.LeadPreferredLocale` + `KnowledgeSearchClient.FetchVariantPoolAsync(lang)` |
| AC-5 | Knowledge servisinin mevcut suggestion queue'su DOKUNULMADAN calisir (additive) | Diff: sadece rotation + group_tag eklentileri (template_suggestions tablosu hic dokunulmadi) |
| AC-6 | Unknown intent fallback: `human_handoff` trigger + diagnostic log (INV-ATxxx) | **N/A:** mevcut AiIntentHandler fallback korunur, bu pakette degisiklik yok |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| `group_tag` kolonu Knowledge servisinde degil `template_catalog` tablosunda (existing) | Suggestion queue akisini bozmamak icin | EXPECTED: CQ3 scope_files yeni migration + minor repo change |
| Rotation state tenant'in leads tablosunda JSONB (Knowledge'da degil) | Template okuma stateless kalir; rotation tenant-state | EXPECTED: service boundary clean |
| A/B seed = FNV-1a(contact_key) — G3'te (2026-04-14) gelen pattern mirror'u | Yeni algoritma yazma; G3 `ITemplateRotationService` zaten Shared'da | CQ11 schema evidence: Shared/ITemplateRotationService |
| Variant assignment Automation'da (orchestrator), Knowledge sadece okur | Microservice isolation | CQ5/CQ9: ok |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Template CRUD API | `arch/contracts/knowledge-templates.json` (mevcut, additive `group_tag`) |
| DB Schema | `arch/db/template-catalog.sql` (mevcut + ALTER) + `arch/db/pkt6b1-niche-business.sql` (leads.faq_rotation_state ALTER) |
| Error Codes | `arch/errors.md` INV-AT-057 (G3'ten mevcut), yeni **INV-AT-061** (faq_rotation_state upsert fail), **INV-AT-066** (group_tag fetch fail), **INV-AT-067** (rotation state malformed JSONB) |
| Shared Service | `Invekto.Shared/ITemplateRotationService.cs` (mevcut) |

## 5. Scope Boundaries

### In Scope
- `template_catalog.group_tag VARCHAR(50)` kolonu (nullable, indexed)
- Automation orchestrator'da group_tag-bazli template cagrisi
- `leads.faq_rotation_state JSONB` (default `{}`) — per-intent rotation counter
- Dashboard UI: template editor'de group_tag dropdown
- Unit test: deterministic seed + round-robin correctness

### Out of Scope (Explicit)
- Yeni template tipi (mevcut 5 tip yeterli)
- Platform-level welcome template curation (sector ve tenant katmani yeterli)
- Multilingual NMT (mevcut translation servisi kullanilir)

### Degismeyen Alanlar (Pre-existing)
- Knowledge suggestion queue akisi
- pgvector embedding hesaplamasi
- 3-katman (Platform>Sector>Tenant) resolution sirasi
- Template approval/publish workflow (superadmin review)

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Knowledge | Template read + CRUD | Minor: group_tag column (additive) |
| Automation | Orchestrator (group_tag-scoped template fetch + rotation state update) | New method |
| Backend | Dashboard proxy (group_tag editor) | Passthrough |
| Shared | Rotation service (mevcut) | No change |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| `faq_rotation_state` JSONB unbounded growth (cok intent, cok lead) | LOW | Per-intent max entry 1, leads.archived pattern kullan |
| Group tag tenant'lar arasi semantic tutarsizlik (biri `welcome_greeting`, biri `msg_welcome`) | MEDIUM | Sector-level onerilen tag sabit listesi (doc) |
| Rotation seed collision (ayni contact_key farkli tenant) | VERY LOW | Hash input'a tenant_id prepend |

## 8. Pilot Consumer

Dent Adavista (ilk tenant) — 10 welcome + 12 intent × 3 cevap = 46 template. Detay: `DentAdavista/plan/pilot-agent-config.md`.
