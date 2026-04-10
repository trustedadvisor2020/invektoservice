# InvektoServices Review Policy v3.2 (Single Source of Truth)

> **Version:** 3.2 | **Updated:** 2026-04-11 | **LOW=Codex REQUIRED (align with CLAUDE.md / BASE / DEV_AGENT)**
>
> Bu dosya InvektoServices review akışının TEK KAYNAĞI'dır. `CLAUDE.md`, `INVEKTO_BASE.prompt.md`, `INVEKTO_DEV_AGENT.prompt.md` ve `INVEKTO_PLAN_AGENT.prompt.md` bu dosyaya referans verir.
> **Çelişen tanımlar YASAKTIR** — değişiklik yapılacaksa ÖNCE bu dosya güncellenir, sonra diğer dosyalar hizalanır.
>
> **v3.2 Farkı (TONIVA v3.2 ile hizalı):** LOW risk Codex review **zorunlu** (fast-track yolu yok). PLAN_OUTDATED kademeli uyarı sistemi eklendi. Verification Questions risk-based minimum zorunluluğu. Microservice isolation checklist kategorisi.

---

## 1. Risk Seviyeleri

| Seviye | Kapsam | Örnek Değişiklikler |
|--------|--------|---------------------|
| **LOW** | UI layout/text only (no logic) | CSS changes, text/label updates, comments |
| **MEDIUM** | Business logic, controllers, repositories | API endpoints, service logic, data handling |
| **HIGH** | Service lifecycle, Shared DTOs, cross-service | Startup, Shared/DTOs, HttpClient contracts |
| **CRITICAL** | Schema, auth/token, tenant isolation | Migrations, auth.cs, tenant scoping, Shared/Middleware |

### Auto-Detection Patterns (Dosya Bazlı İpucu)
```
CRITICAL: auth, Migration, tenant.*scop, Shared/Middleware, Shared/DTOs/Auth*
HIGH:     Services/*.cs, Program.cs, Shared/DTOs/**, Shared/Constants/**
MEDIUM:   Controllers/*.cs, Repositories/*.cs, Handlers/*.cs
LOW:      *.md, wwwroot/ (CSS/text only)
```

**NOT:** Dosya uzantısı tek başına risk belirlemez. İçerik önemli!

### LOW Risk Sınıflandırma Guardrails

Bir değişikliğin LOW risk kalabilmesi için SADECE şu koşullarda:
- Kod değişikliği YOK (sadece comment, typo, log message) veya sadece CSS/layout/text
- Config/appsettings değişikliği YOK
- Schema/migration değişikliği YOK
- Auth/security değişikliği YOK
- Routing/endpoint değişikliği YOK
- Shared DTO / Shared Constants değişikliği YOK

Bu kırmızı çizgilerden BİRİ bile varsa → risk OTOMATİK MEDIUM.

> **Not:** LOW kalsın veya MEDIUM'a yükselsin, **her iki durumda da Codex review zorunlu**. v3.2'den itibaren "fast track / SKIP" yolu YOKTUR. LOW sınıflandırması sadece VQ sayısı ve evidence derinliğini etkiler.

---

## 2. Evidence Gereksinimleri

| Risk | build | db_code_sync | high_checks | invariant_proof_pack | codex_review |
|------|-------|--------------|-------------|---------------------|---------------|
| **LOW** | Build PASS | `N/A -- LOW risk` | `N/A -- LOW risk` | `N/A -- LOW risk` | **ZORUNLU** |
| **MEDIUM** | DOLU | DOLU (≥20 char) | `N/A -- MEDIUM risk` | `N/A -- MEDIUM risk` | **ZORUNLU** |
| **HIGH** | DOLU | DOLU (≥20 char) | DOLU (≥20 char) | `N/A -- HIGH risk` | **ZORUNLU** |
| **CRITICAL** | DOLU | DOLU (≥20 char) | DOLU (≥20 char) | DOLU (≥20 char) | **ZORUNLU** |

### Evidence Kuralları
- **DOLU** = ≥20 karakter, `N/A` ile başlamaz
- Boş bırakma YASAK — ya dolu ya `N/A -- {RISK} risk`
- `db_code_sync`: Postgres schema ↔ C# entity / DTO sync, `arch/db/*.sql` referansı
- `high_checks`: Microservice isolation (Shared üzerinden iletişim), service lifecycle (IDisposable/IHostedService), thread-safety
- `invariant_proof_pack`: `INVARIANT-1: ... (dosya:satır)` formatında

---

## 3. Verdict Kuralları

### PASS Şartları (TÜMÜ gerekli)
- [ ] Architecture coverage (touched scope için)
- [ ] Policy enforced (bu dosya)
- [ ] Checklist PASS (CQ1-12)
- [ ] Evidence = Final Risk requirements
- [ ] No blocking issues
- [ ] Scope discipline (allowed_files içinde)

### WARN Durumları
- Non-blocking issues
- Missing line ranges + adequate evidence
- Minor documentation gaps (scope dışı)

### FAIL Durumları (HERHANGİ BİRİ)
- Tenant/auth/security regression risk
- Architecture/policy violation
- **Microservice isolation violation** (servisler arası doğrudan referans — Shared üzerinden olmalı)
- DB injection / unsafe query
- Missing required arch files (touched scope için)
- Missing evidence (risk level için)
- Schema drift (Medium+ için db_code_sync eksik)
- CRITICAL değişiklik + invariant proof pack eksik
- snake_case violation in DB columns
- Shared DTO breaking change without cross-service update
- **PLAN_OUTDATED** (3. uyarı sonrası) — git diff ↔ allowed_files uyumsuzluğu

---

## 4. Plan Dosyası Formatı

### Slug Formatı
```
Pattern: ^[0-9]{8}-[a-z0-9-]+$
Örnek:   20260411-fix-chat-translation
Dosya:   arch/plans/20260411-fix-chat-translation.json
```

### Schema
```
arch/contracts/plan-schema.json (JSON Schema draft 2020-12)
```

### Plan Dosyası Zorunlu Alanlar
- `schema_version` — "5.1"
- `review_protocol_version` — "5.1"
- `slug` — Task identifier
- `risk` — LOW | MEDIUM | HIGH | CRITICAL
- `status` — PLANNING | IN_PROGRESS | REVIEW | DONE
- `plan.*` — summary, q_intent, interview_notes
- `allowed_files[]` — Scope kontrolü
- `verification_questions[]` — **TÜM risk seviyelerinde zorunlu** (LOW: 1+, MEDIUM/HIGH: 3+, CRITICAL: 5+)
- `aha_moments[]` — Risk-scaled minimum: LOW 0+ (opsiyonel), MEDIUM/HIGH 3+, CRITICAL 5+
- `scope_discipline`, `error_handling` — Top-level objeler (schema zorunlu)
- `verdict.*` — DevAgent tarafından güncellenir, `status` enum: PASS | FAIL | UNKNOWN | null (SKIP yok)

---

## 5. Review Akışı (v3.2 — TÜM risk seviyeleri Codex)

Tek akış var. Risk seviyesi SADECE sorgu yoğunluğunu (VQ sayısı, evidence derinliği, aha_moments) ayarlar — Codex SKIP yolu YOKTUR.

### Primary Path (normal yol)

```
1. DevAgent: Kod yazar
2. Build PASS (dotnet build InvektoServis.sln)
3. DevAgent /rev çalıştırır
4. /rev içinden MCP tool `mcp__codex-review__codex_review` otomatik çağrılır
5. Codex: 2 BLOK review (Code Quality CQ1-12 + CoVe)
6. Verdict plan JSON'a yazılır
7. PASS → commit → DONE | FAIL → fix → /rev tekrar (max 3 iter)
```

### Fallback Path (incident yolu)

MCP tool başarısız olursa (network, rate limit, timeout, MCP server down):

```
Fallback A: /codex {slug} manuel tetik (MCP retry)
Fallback B: Q Codex penceresine manuel copy-paste → verdict'i /rev verdict ile geri yaz
```

> **Fallback primary değildir.** Copy-paste yalnızca MCP tamamen ulaşılamazsa kullanılır. Normal operasyonda DevAgent copy-paste beklemez.

**Codex trigger:** Tercih sırası (1) `/rev` içinden otomatik MCP çağrısı, (2) MCP hata verirse `/codex {slug}` manuel tetik, (3) son çare Q copy-paste.

### Codex 2 BLOK Output
```
=== CODE QUALITY GATE ===
CQ1-CQ12 kontrolü

=== COVE VERIFICATION ===
Q1-Q3+ verification

=== VERDICT ===
OVERALL: PASS | FAIL | UNKNOWN
```

### Fix-Run Kuralları
| Risk | Max Iter | Escalation |
|------|----------|------------|
| LOW | 3 | Q escalate |
| MEDIUM | 3 | Q escalate |
| HIGH+ | 3 | Q onay gerekli |

---

## 6. Scope Discipline

### Path Normalization
```csharp
// Windows uyumu için
path.Replace('\\', '/').ToLowerInvariant()
```

### Scope Guard
- `files_changed[*].path` ∈ `allowed_files[*].path`
- Scope dışı dosya = PLAN_OUTDATED

### PLAN_OUTDATED Kategorisi

**Tanım:** Kod değişti ama plan bu değişikliği kapsamıyor.

**Kontrol:** `/rev` çalıştığında otomatik olarak:
1. `git diff` içindeki dosyalar
2. `plan.allowed_files`
3. Whitelist pattern'leri

karşılaştırılır.

**Whitelist Patterns (İstisna):**
```
*.csproj            → Project manifest
packages.lock.json  → Dependency lock
*.Designer.cs       → Auto-generated designer dosyaları
*.g.cs              → Auto-generated code
Shared/**/*.cs      → Shared utility dosyaları (manuel risk değerlendirmesi ile)
```

**Kademeli Uyarı Sistemi:**
| Sayaç | Sonuç |
|-------|-------|
| 1/3 | WARN — Plan güncelle uyarısı |
| 2/3 | WARN — Son uyarı |
| 3/3 | **FAIL** — /rev bloklanır |

**Iteration Kuralı:**
- PLAN_OUTDATED WARN → iteration HARCANMAZ
- PLAN_OUTDATED FAIL (3. kez) → iteration HARCAR
- 3 kez plan uyumsuz = escalation (kasıtlı baskı)

**Mesaj Formatı:**
```
FAIL: PLAN_OUTDATED
Reason: Diff contains file(s) not declared in plan.allowed_files
Files: {dosya_listesi}
Action: Update plan JSON or reduce diff
```

---

## 7. Checklist Kategorileri

| Kategori | Fail Condition | Evidence Required |
|----------|----------------|-------------------|
| **DB** | Query injection, schema drift | Parameterized query + sync |
| **Auth/Session** | Token bypass | Auth middleware aktif |
| **Tenant** | Cross-tenant leak | tenant_id filtreleme |
| **Microservice Isolation** | Direct cross-service reference | Shared DTO / HttpClient contract üzerinden |
| **Service Lifecycle** | Resource leak, race condition | IDisposable/using pattern, thread-safety |
| **Schema** | Migration outdated | arch/db/*.sql + EF sync |

---

## 8. Verification Questions (TÜM risk seviyelerinde zorunlu)

### Soru Sayısı (schema minItems)

| Risk | Minimum VQ |
|------|-----------|
| LOW | 1+ |
| MEDIUM | 3+ |
| HIGH | 3+ |
| CRITICAL | 5+ |

### Coverage Check

LOW için tek kategori yeterli. MEDIUM ve üstü için aşağıdaki 3 kategorinin tümü temsil edilmelidir:

```
✅ Data (DB, kolon, tip, migration)
✅ Tenant/Auth (isolation, bypass, token scope)
✅ Lifecycle (race, rollback, service restart)
+ Opsiyonel: Microservice isolation / Process / Policy
```

MEDIUM+ planlarda 3 soru da aynı kategoriyi sorgularsa → Coverage FAIL.

### Örnek Sorular
```
"Bu değişiklikte hangi varsayım rollback altında sessizce fail eder?"
"Bu değişiklik tenant isolation'ı dolaylı olarak bypass edebilir mi?"
"Concurrent execution'da ilk ne bozulur?"
"Postgres'te {kolon} gerçekten var mı ve tipi {beklenen} mi?"
"Bu Shared DTO değişikliği hangi servisleri kırabilir?"
```

### Hard Precondition
```
IF verification_questions.length < min_for_risk
→ /rev BLOCKED

LOW risk için en az 1 VQ, MEDIUM/HIGH için 3, CRITICAL için 5 olmadan /rev çalışmaz.
Verification soruları tanımlı değilse /rev çalışmaz (tüm risk seviyeleri).
```

---

## 9. LOW Risk Policy (Tek Gerçeklik)

```
┌─────────────────────────────────────────────────────────────────────┐
│  LOW Risk = Codex ZORUNLU                                           │
│                                                                      │
│  v3.2 itibarıyla LOW risk SKIP yolu kaldırıldı. Tüm risk            │
│  seviyeleri Codex review'den geçmeden commit edilemez.              │
│                                                                      │
│  Bu kural şu dosyalarda BİREBİR AYNI:                               │
│  - arch/review-policy.md (bu dosya — TEK KAYNAK)                    │
│  - CLAUDE.md (§Workflow)                                            │
│  - .claude/agents/INVEKTO_BASE.prompt.md                            │
│  - .claude/agents/INVEKTO_DEV_AGENT.prompt.md                       │
│  - .claude/agents/INVEKTO_PLAN_AGENT.prompt.md                      │
│  - arch/contracts/plan-schema.json (verdict.status SKIP kaldırıldı) │
│                                                                      │
│  İki protokol YOK. Tek gerçeklik VAR.                               │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 10. Evaluator Tuning Döngüsü

Codex review kalitesini artırmak için periyodik tuning yapılır (Anthropic "Building Effective Agents" best practice).

### Döngü Adımları
1. **Log analizi** — Son 10+ plan JSON'dan verdict bölümlerini oku
2. **Divergence tespiti** — False positive (haklı kodu FAIL eden) ve false negative (hatalı kodu PASS eden) bul
3. **Prompt güncelle** — Codex MCP prompt'undaki ET kurallarını güncelle
4. **Rebuild + test** — MCP server rebuild + 3-4 review ile test
5. **Tekrar kontrol** — Yeni kurallar düzgün çalışıyor mu?

### Ne Zaman Tuning Yapılır
- Her 20-30 review sonrası
- Belirgin false positive/negative görüldüğünde
- Model değişikliğinden sonra (ör. gpt-5.2 → gpt-6)

---

## 11. Referans Dosyalar

| Dosya | Rol |
|-------|-----|
| `arch/review-policy.md` | **TEK KAYNAK** (bu dosya) |
| `arch/contracts/plan-schema.json` | JSON Schema (verification_questions, aha_moments) |
| `arch/codex-context.md` | Codex MCP server için project context |
| `.claude/agents/INVEKTO_BASE.prompt.md` | Global agent rules |
| `.claude/agents/INVEKTO_PLAN_AGENT.prompt.md` | Plan agent |
| `.claude/agents/INVEKTO_DEV_AGENT.prompt.md` | Dev agent |
| `.claude/commands/codex.md` | Manuel Codex fallback komutu |
| `~/.claude/commands/auto.md` | Global auto workflow (harness command) |
| `~/.claude/commands/rev.md` | Global `/rev` command |
| `C:\Users\taner\.claude\workflow\shared-lessons.md` | Cross-project kurallar |

---

## 12. Versiyon

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 3.2 | 2026-04-11 | TONIVA v3.2 ile hizalama: Plan dosyası formatı, PLAN_OUTDATED kademeli uyarı, Verification Questions risk-based min, Scope Discipline, Microservice Isolation checklist kategorisi, Evaluator Tuning döngüsü, LOW Risk Policy box |
| 2.0 | 2026-04-10 | LOW=Codex ZORUNLU, MCP-first primary, fallback path ayrımı, evidence tablosuna codex_review kolonu |
| 1.0 | 2026-02 | İlk versiyon |
