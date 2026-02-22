# Spec-Driven Development (SDD) - InvektoServis Degerlendirme & Yol Haritasi

> **Tarih:** 2026-02-21 | **Hazirlayan:** Q + Claude Code | **Durum:** DRAFT
> **Hedef:** Mevcut auto workflow'a SDD prensiplerini entegre ederek AI-assisted kod uretim kalitesini artirmak.

---

## 1. SDD Nedir?

Spec-Driven Development, AI destekli yazilim gelistirmede **spec'i source of truth** olarak konumlandiran yaklasim:

```
Geleneksel:  Fikir → Kod → Test → Bug fix dongusu
SDD:         Spec → Plan → AI Code Gen → Validation → Spec'e geri don
```

**Temel Prensip:** Kod "uretilen cikti", spec ise "kaynak belge". Spec degisince kod regenere edilir.

### SDD'nin 5 Sutunu

| # | Sutun | Aciklama |
|---|-------|----------|
| 1 | **Persistent Specs** | Versiyonlanan, review edilen, Q tarafindan yonetilen feature spec'leri |
| 2 | **Constitution (Rules)** | Global kodlama standartlari ve mimari kurallar |
| 3 | **Agent Workflow** | Spec oku → Plan uret → Task'lara bol → Kod uret → Validate |
| 4 | **Drift Detection** | Spec-kod uyumsuzlugunu otomatik tespit |
| 5 | **Traceability** | Her kod satirinin hangi spec'ten uretildigini izleme |

---

## 2. Mevcut Durum Analizi: InvektoServis vs SDD

### Zaten Yapilan (%75)

| SDD Prensibi | InvektoServis Karsiligi | Olgunluk |
|---|---|---|
| Persistent Specs | `arch/contracts/*.json` + `arch/db/*.sql` | **Guclu** - 14 contract, 20+ schema |
| Constitution | `INVEKTO_BASE.prompt.md` + `CLAUDE.md` | **Cok Guclu** - 5.2 versiyonunda |
| Agent Workflow | `auto.md` → Interview → Plan → Dev → `/rev` → Codex | **Cok Guclu** - 12 paket tamamlandi |
| Plan Schema | `arch/contracts/plan-schema.json` v5.0 | **Guclu** - scope discipline, aha moments |
| Living Docs | `tracking/`, `arch/session-memory.md` | **Guclu** - paket bazli izleme |
| Drift Detection | `invariant-check.ps1` hook, `/sync-check` | **Orta** - snake_case, error code kontrol |
| Quality Gate | Codex CQ1-CQ8 + CoVe verification | **Cok Guclu** - iter 0 hedefi |

### Eksik Olan (%25)

| Eksik Parca | Etki | Oncelik |
|---|---|---|
| **Feature-level formal spec** | AI agent context eksikligi → yanlis varsayimlar | **YUKSEK** |
| **Spec → Code traceability** | Kodun hangi spec'ten uretildigini izleyememe | **ORTA** |
| **Acceptance criteria formalization** | Codex review'da "neye gore PASS?" sorusu | **YUKSEK** |
| **Regeneration capability** | Spec degisince kodu sifirdan uretebilme | **DUSUK** |
| **Spec diff → impact analysis** | Spec degisikligi hangi kodu etkiler? | **DUSUK** |

### Lesson-Learned'dan SDD ile Cozulebilecek Problemler

Mevcut `arch/lessons-learned.md`'den SDD eksikligine bagli tekrarlayan sorunlar:

| Problem | Tekrar | SDD Cozumu |
|---|---|---|
| Codex FORCE PASS (11 PKT'den 8'i) | %73 | Spec'teki architectural decisions → Codex context'e gider |
| Cross-service CoVe UNKNOWN | 5+ kez | Spec'te service boundary kararlar explicit olur |
| Plan JSON metadata hatalari | 3+ kez | Spec-to-plan otomatik mapping |
| Contract-DTO field mismatch | 2+ kez | Spec'te field mapping explicit tanimlanir |
| Interview'da ayni konuyu tekrar sorma | 2+ kez | Spec zaten cevaplari icerir |
| Codex pre-existing pattern false positive | 3+ kez | Spec'te "degismeyen alanlar" explicit |

**Tahmini iyilestirme:** FORCE PASS orani %73 → %30 altina dusebilir.

---

## 3. SDD Entegrasyon Stratejisi

### Yaklasim: "Evolutionary SDD" (Big Bang Degil)

```
MEVCUT WORKFLOW (KORUNACAK):
Interview → Plan JSON → Dev → Build → /rev → Codex → Commit

SDD EKLENTILERI (ARAYA GIRECEK):
         ↓
    [1] Feature Spec ← Q yazar/onaylar (YENi)
         ↓
    [2] Spec → Plan mapping (GELISMIS)
         ↓
    Interview → Plan JSON → Dev → Build → /rev → Codex → Commit
                                                    ↑
                                              [3] Spec-aware review (GELISMIS)
                                                    ↓
                                              [4] Spec drift check (YENI)
```

**Kural:** Mevcut auto workflow BOZULMAZ. SDD katmanlar olarak eklenir.

---

## 4. Yol Haritasi

### Faz 1: Feature Spec Template (1 Gun)

**Hedef:** `arch/specs/` klasoru + template + ilk ornek

#### 4.1.1 Spec Template Olustur

`arch/specs/_TEMPLATE.md` — her yeni feature icin kullanilacak:

```markdown
# SPEC: [Feature Adi]

> **Spec ID:** SPEC-XXX | **Paket:** PKT-XX | **Risk:** LOW/MEDIUM/HIGH
> **Yazar:** Q | **Son Guncelleme:** YYYY-MM-DD | **Durum:** DRAFT/APPROVED/IMPLEMENTED

## 1. Intent (Ne & Neden)

[Q'nun kendi sozleriyle: bu feature ne yapiyor ve neden gerekli?]

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | ... | Manual test / Codex CQ / DB query |
| AC-2 | ... | ... |

## 3. Architectural Decisions

[Bilinçli kararlar — Codex'in false positive vermemesi icin]

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Cross-tenant scheduler query | IHostedService design | EXPECTED: CQ5 tenant isolation skip |
| No auth on webhook endpoint | External callback | EXPECTED: CQ5 auth skip |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| API Request/Response | `arch/contracts/xxx.json` |
| DB Schema | `arch/db/xxx.sql` |
| Error Codes | `arch/errors.md` INV-XX-xxx |

## 5. Scope Boundaries

### In Scope
- ...

### Out of Scope (Explicit)
- ...

### Degismeyen Alanlar (Pre-existing)
- [Codex'in pre-existing pattern false positive vermemesi icin]

## 6. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| Backend | Proxy | Yeni endpoint |
| ServisX | Core logic | Yeni servis / Major change |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| ... | LOW/MED/HIGH | ... |
```

#### 4.1.2 PKT-7 (Visual AI) icin Ornek Spec

Mevcut `tracking/pkt-07-visual-ai.md` icerigini `arch/specs/SPEC-007-visual-ai.md` formatina donustur. Bu ilk ornek hem template'i test eder hem de PKT-7 gelistirmesine hazirlik olur.

#### 4.1.3 Deliverables

- [ ] `arch/specs/_TEMPLATE.md` olustur
- [ ] `arch/specs/SPEC-007-visual-ai.md` ornek spec
- [ ] `arch/README.md` guncelle (specs/ referansi)
- [ ] `CLAUDE.md` "Architecture Reference" tablosuna `Yeni feature | arch/specs/` ekle

---

### Faz 2: Plan Schema'ya Spec Baglantisi (1 Gun)

**Hedef:** Plan JSON'un hangi spec'e bağli olduğunu izlemek

#### 4.2.1 plan-schema.json v5.1 Guncelleme

Mevcut `plan-schema.json`'a eklenecek alanlar:

```json
{
  "spec_ref": {
    "type": ["string", "null"],
    "pattern": "^SPEC-[0-9]{3}$",
    "description": "Referans spec ID (e.g. SPEC-007). Null for standalone/legacy tasks."
  },
  "spec_acceptance_criteria": {
    "type": "array",
    "items": {
      "type": "object",
      "properties": {
        "id": { "type": "string", "pattern": "^AC-[0-9]+$" },
        "description": { "type": "string" },
        "status": { "type": "string", "enum": ["PENDING", "MET", "NOT_MET", "N/A"] }
      }
    },
    "description": "Spec acceptance criteria tracking (populated during /rev)"
  },
  "spec_architectural_decisions": {
    "type": "array",
    "items": {
      "type": "object",
      "properties": {
        "decision": { "type": "string" },
        "codex_note": { "type": "string" }
      }
    },
    "description": "Pre-declared architectural decisions for Codex awareness"
  }
}
```

#### 4.2.2 Backward Compatibility

- `spec_ref` nullable — eski plan'lar etkilenmez
- `spec_acceptance_criteria` optional — sadece spec olan plan'larda dolar
- Mevcut required field'lar DEGISMEZ

#### 4.2.3 Deliverables

- [ ] `arch/contracts/plan-schema.json` v5.0 → v5.1 guncelle
- [ ] Ornek plan JSON ile test et (PKT-7 plan)
- [ ] `INVEKTO_BASE.prompt.md` plan section guncelle

---

### Faz 3: Spec-Aware Codex Review (1 Gun)

**Hedef:** Codex review'da spec'teki architectural decisions'i context olarak kullanarak FORCE PASS oranini dusurmek.

#### 4.3.1 `/rev` Workflow Guncelleme

Mevcut `/rev` akisina ekleme:

```
MEVCUT:
  Plan JSON oku → diff olustur → Codex MCP'ye gonder → verdict al

SDD EKLEME:
  Plan JSON oku → spec_ref varsa spec dosyasini oku
    → "Architectural Decisions" section'ini Codex prompt'una ekle
    → diff olustur → Codex MCP'ye gonder → verdict al
    → Acceptance criteria status guncelle
```

**Codex Prompt Eklentisi:**
```
## Pre-Declared Architectural Decisions (Do NOT flag as issues)
- Cross-tenant scheduler query: IHostedService by design (Q approved)
- No auth on webhook: external callback endpoint (Q approved)
- [spec'ten otomatik cekilir]
```

#### 4.3.2 Beklenen Etki

| Metrik | Onceki | Hedef |
|--------|--------|-------|
| FORCE PASS orani | %73 (8/11 PKT) | <%30 |
| Ortalama Codex iter | 3.2 | <2.0 |
| False positive CQ5 | %40+ | <%10 |
| CoVe UNKNOWN (cross-service) | %25 | <%10 |

#### 4.3.3 Deliverables

- [ ] `rev.md` guncelle (spec-aware review adimi)
- [ ] Codex prompt template'ine "Architectural Decisions" section ekle
- [ ] INVEKTO_DEV_AGENT'a spec okuma adimi ekle

---

### Faz 4: Spec Drift Detection (Opsiyonel - Ileri Asamada)

**Hedef:** Kod degisikligi spec ile uyumsuzsa uyari vermek.

#### 4.4.1 `/sync-check` Genisletme

Mevcut `/sync-check` skill'ine ekleme:

```
MEVCUT: arch/db/*.sql ↔ kod senkronizasyonu
EKLEME: arch/specs/*.md ↔ kod senkronizasyonu
  - Spec'teki contract ref'ler hala gecerli mi?
  - Spec'teki service boundaries degisti mi?
  - Acceptance criteria'lar kodda karsiligi var mi?
```

#### 4.4.2 Deliverables

- [ ] `sync-check.md` guncelle
- [ ] `invariant-check.ps1` hook'a spec kontrol ekle (non-blocking)

---

## 5. Uygulama Oncelikleri

```
HEMEN (PKT-7 oncesi):
├── Faz 1: Feature Spec Template ............. [1 gun]
└── Faz 2: Plan Schema Spec Ref .............. [1 gun]

PKT-7 ILE BIRLIKTE (dogal test):
└── Faz 3: Spec-Aware Codex Review ........... [1 gun]

SONRA (ihtiyac oldukca):
└── Faz 4: Spec Drift Detection .............. [opsiyonel]
```

**Toplam ek yuk:** ~3 gun (Faz 1-3). Faz 4 opsiyonel.
**ROI:** PKT-7'den itibaren her paketin Codex review suresi yarisina duser.

---

## 6. Risk Analizi

| Risk | Olasilik | Etki | Mitigation |
|------|----------|------|------------|
| Over-specification (spec yazma yorgunlugu) | ORTA | Workflow yavaslar | Template kisa tut, sadece critical sections zorunlu |
| Spec-kod drift arttikca maintenance yuku | DUSUK | Stale spec | Drift detection (Faz 4) + `/wrap` ile guncelle |
| Mevcut workflow bozulur | COK DUSUK | Ciddi | Ekleme yaklasimi, existing flow DEGISMEZ |
| Codex hala false positive verir | ORTA | FORCE PASS devam | Architectural decisions section ile azaltilir, sifirlanmaz |

---

## 7. Basari Metrikleri

| Metrik | Baseline (PKT-1~6C3) | Hedef (PKT-7+) | Olcum |
|--------|----------------------|-----------------|-------|
| FORCE PASS orani | %73 | <%30 | Plan JSON verdict |
| Ortalama Codex iter | 3.2 | <2.0 | Plan JSON iteration |
| Interview soru sayisi | 5-8 | 2-3 | Spec zaten cevaplari icerir |
| Cross-service UNKNOWN | %25 | <%10 | CoVe results |
| Spec coverage (yeni PKT) | %0 | %100 | arch/specs/ dosya sayisi |

---

## 8. InvektoServis'e Ozel SDD Prensipleri

Genel SDD literaturunden InvektoServis'e uyarlanan prensipler:

1. **Spec = Q'nun niyeti, Plan = AI'in calisma belgesi.** Spec'i Q yazar/onaylar, Plan'i AI uretir.
2. **Spec zorunlu degil, ama FORCE PASS istemiyorsan yaz.** Legacy paketler spec'siz devam edebilir.
3. **Spec minimalist olsun.** 2 sayfa yeterli. 10 sayfalik spec over-engineering.
4. **Architectural decisions = Codex false positive kalkan.** En degerli section bu.
5. **Acceptance criteria = "/rev neye gore PASS?" sorusunun cevabi.** Codex'e explicit olarak verilir.
6. **Spec'i tracking/pkt-XX.md ile KARISTIRMA.** Tracking = ilerleme, Spec = niyet + kriterler.

---

## 9. Referanslar

- [InfoQ: Spec-Driven Development](https://www.infoq.com/articles/spec-driven-development/) — Executable architecture
- [ITNEXT: Up Your AI Dev Game with SDD](https://itnext.io/up-your-ai-development-game-with-spec-driven-development-f5175cf59c7c) — SpecKit workflow
- [Daniel Sogl: SDD - Evolution Beyond Vibe Coding](https://danielsogl.medium.com/spec-driven-development-sdd-the-evolution-beyond-vibe-coding-1e431ae7d47b) — Kiro integration
- [Bito: SDD Explained for AI Coding Teams](https://bito.ai/blog/spec-driven-development-explained-for-ai-coding-teams/) — Practical guide
- [Gojko Adzic: SDD - Revenge of Waterfall or BDD Taken Further?](https://www.linkedin.com/pulse/spec-driven-development-revenge-waterfall-bdd-taken-gojko-adzic-imquf) — Kritik degerlendirme

---

**Sonraki Adim:** Q onayladiginda Faz 1 baslar → `arch/specs/_TEMPLATE.md` + `SPEC-007-visual-ai.md` olusturulur.
