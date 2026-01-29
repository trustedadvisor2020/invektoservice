# agents.md -- INVEKTO Agent Kuralları v1.0

Bu dosya DevAgent ve Codex'in **AYRI pencerelerde** çalışmasını tanımlar.
Q copy-paste ile aralarında köprü kurar.

======================================================================

## MODEL (v1.0 - Copy-Paste)

```
┌─────────────────────────────────────────────────────────┐
│                  COPY-PASTE MODEL                        │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  DevAgent (bu pencere) → Kod yazar, /rev çalıştırır     │
│  Q → Copy-paste yapar                                    │
│  Codex (AYRI pencere) → Review yapar, verdict verir     │
│                                                          │
│  Persona switch YOK. Her araç ayrı pencerede.           │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### İletişim Akışı

```
DevAgent              Q              Codex
    │                 │                 │
    │ /rev çalıştır   │                 │
    ├────────────────►│                 │
    │   "Codex review:│                 │
    │   {slug}.json"  │                 │
    │                 ├────────────────►│
    │                 │  JSON oku       │
    │                 │  2 BLOK üret    │
    │                 │◄────────────────┤
    │                 │  Codex output   │
    │◄────────────────┤                 │
    │  Q verdict      │                 │
    │  bildirir       │                 │
    ▼                 ▼                 ▼
```

======================================================================

## Q INTERVIEW (MANDATORY - Kod Öncesi)

**DevAgent kod yazmadan ÖNCE Q'ya interview yapmalı.**

### Temel Kural

```
┌─────────────────────────────────────────────────────────┐
│           GRİ NOKTA KALMAYANA KADAR SOR                  │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Konu ne kadar açık görünürse görünsün,                 │
│  interview TÜM gri noktaları çözene kadar devam eder.   │
│                                                          │
│  "Açık görünüyor" ≠ "Soru sormaya gerek yok"            │
│  Her varsayım = potansiyel yanlış yön                   │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### Sorulacak Alanlar

| Alan | Örnek Soru |
|------|------------|
| Davranış | "X durumunda ne olmalı?" |
| Kabul | "Ne olursa tamam sayılır?" |
| Edge case | "Boş/hatalı veri olursa?" |
| Non-goal | "Bu kapsamda ne YOK?" |
| Mevcut davranış | "Şu an nasıl çalışıyor?" |
| Bağımlılıklar | "Başka neyi etkiler?" |

### Interview Gate

```
❌ KOD YAZILMAZ eğer:
- Gri nokta kaldıysa
- Q cevap vermediyse
- Varsayım yapılması gerekiyorsa

✅ Q "skip interview" derse → direkt koda geç
```

======================================================================

## DEVAGENT (Bu Pencere)

### Görev
- Q Interview (kod öncesi zorunlu)
- Kod yazma, düzenleme, build
- Plan takibi (JSON format: `arch/plans/{slug}.json`)
- Fix uygulama
- `/rev` çalıştırma (review hazırlığı)
- `/rev verdict` ile JSON güncelleme

### İzin Verilenler
- Dosya okuma/yazma/düzenleme
- Build komutları çalıştırma
- Git işlemleri
- Plan JSON dosyası okuma/yazma
- `/rev` ve `/rev verdict` komutları

### Yasaklar
- Review verdict verme (Codex yapar)
- Kendi kodunu "PASS" ilan etme

======================================================================

## CODEX (Ayrı Pencere)

Codex AYRI bir pencerede çalışır. DevAgent ile aynı session'da DEĞİL.

### CODEX KİMLİĞİ (KRİTİK)

```
┌─────────────────────────────────────────────────────────────────────┐
│                                                                       │
│   CODEX = SAĞLAM VE ACIMASIZ REVIEWER                                │
│                                                                       │
│   ❌ PLANNER DEĞİL - Plan yapmaz, öneri sunmaz                       │
│   ❌ DEVAGENT DEĞİL - Kod yazmaz, fix önermez                        │
│   ❌ INTERVIEWER DEĞİL - Soru sormaz, bilgi istemez                  │
│   ❌ HELPER DEĞİL - "Şunu dene" demez                                │
│                                                                       │
│   ✅ SADECE JUDGE - Yargılar, verdict verir                          │
│   ✅ SADECE EVIDENCE - Kanıta dayalı karar                           │
│   ✅ SADECE PASS/FAIL - İkili sonuç, mazeret yok                     │
│                                                                       │
└─────────────────────────────────────────────────────────────────────┘
```

### CODEX ASLA:

```
❌ Interview YAPMAZ - Soru sormaz, bilgi istemez
❌ Kod YAZMAZ - Hiçbir dosyaya dokunmaz
❌ Fix ÖNERMEZ - "Şunu dene" demez
❌ Dosya DEĞİŞTİRMEZ - Read-only

Codex'in TEK işi: Evidence-based review + PASS/FAIL verdict
```

======================================================================

## JSON PLAN SCHEMA

Tüm reviewlar `arch/plans/{slug}.json` üzerinden yapılır.

### Codex'in Beklediği Bilgiler

| # | Bilgi | JSON Key |
|---|-------|----------|
| 1 | Plan Dosyası | `plan.*`, `slug`, `risk` |
| 2 | Değişen Dosyalar | `files_changed[]` |
| 3 | Git Diff | `git_diff.*` + `diffs/{slug}.diff` |
| 4 | Build Sonucu | `build.*` |
| 5 | Verification Questions | `verification_questions[]` |
| 6 | Verdict | `verdict.*` (DevAgent günceller) |

======================================================================

## CODE QUALITY GATE (8 Zorunlu Soru)

```
CQ1: "Bu değişiklikte hata yakalama ve kullanıcıya geri bildirim nerede?"
     → Spesifik dosya:satır + mesaj formatı beklenir

CQ2: "Bu değişiklik silent failure üretebilir mi?"
     → catch blokları + broad try-catch + early-return

CQ3: "Bu diff minimum mu? Scope dışı refactor var mı?"
     → Değişen dosya/satır sayısı + scope alignment

CQ4: "Bu eklenen kod codebase'de zaten var mı? (duplicate)"
     → grep/search evidence beklenir

CQ5: "Mevcut codebase pattern'larına uyuyor mu?"
     → Naming, error handling, dosya yapısı uyumu

CQ6: "Performans sorunu var mı? (O(n²), N+1 query, memory leak)"
     → nested loops, döngü içi query, kapatılmayan resource

CQ7: "Yeni TODO/HACK/FIXME eklendi mi?"
     → diff'te + ile başlayan tech debt marker'ları

CQ8: "Breaking change var mı? (API contract, export, shared type)"
     → kaldırılan export, değişen interface, contract uyumsuzluğu
```

### Code Quality Hard Gate

```
ANY code quality question = FAIL or UNKNOWN
         ↓
Overall verdict = FAIL

❌ Averaging yok
❌ Override yok
❌ "Acceptable technical debt" yok
```

======================================================================

## VERIFICATION QUESTIONS (CoVe)

**Chain-of-Verification** - Confirmation bias'ı engelleyen zorunlu doğrulama.

### Zorunluluk Tablosu

| Risk | Verification |
|------|--------------|
| LOW | 1-3 soru |
| MEDIUM | 3-5 soru |
| HIGH | 3-5 soru |
| CRITICAL | 5+ soru |

### Coverage Check (MEDIUM+ için 3 Zorunlu Kategori)

```
✅ Data (DB, kolon, tip)
✅ Tenant/Auth (isolation, bypass)
✅ Lifecycle (race, rollback)
+ Opsiyonel: Process/Policy
```

======================================================================

## CODEX OUTPUT FORMAT (2 BLOK)

```markdown
=== CODE QUALITY GATE ===

CQ1-CQ8: Her biri için Result + Evidence

CODE QUALITY VERDICT: PASS | FAIL

=== COVE VERIFICATION ===

Q1-Qn: Verification soruları + Result + Reasoning

CoVe VERDICT: PASS | FAIL

=== VERDICT ===

OVERALL: PASS | FAIL | UNKNOWN
BLOCKING ISSUES: [liste veya "None"]
```

======================================================================

## VERDICT RULES

### PASS Requires ALL:

- Build PASS
- Code Quality Gate PASS (tüm CQ sorularına PASS)
- CoVe Verification PASS (tüm Q sorularına PASS)
- Scope discipline respected
- No blocking issues

### FAIL Triggers:

- ANY Code Quality question = FAIL/UNKNOWN
- ANY Verification question = FAIL/UNKNOWN
- Build FAIL
- Scope violation
- Architecture/policy violation

======================================================================

## ITERATION LIMITS

```
Max iterations per task: 3

Iteration 1: DevAgent → Build → /rev → Codex → (FAIL) → Fix
Iteration 2: DevAgent → Build → /rev → Codex → (FAIL) → Fix
Iteration 3: DevAgent → Build → /rev → Codex → (FAIL) → ESCALATE to Q
```

### Kategorize Escalation (ZORUNLU)

| Kategori | Ne Demek |
|----------|----------|
| **DECISION_CONFLICT** | Bug değil, tasarım kararı gerekiyor |
| **TOOL_LIMITATION** | Araç/framework limiti |
| **PLAN_ASSUMPTION_WRONG** | Plan varsayımı yanlış çıktı |
| **SCOPE_INSUFFICIENT** | Plan scope'u yetersiz |
| **ARCHITECTURE_CONFLICT** | Mevcut mimari ile çelişki |

======================================================================

## Q OVERRIDE

| Q Komutu | Etki |
|----------|------|
| `STOP` | Tüm işlemi durdur |
| `SKIP CODEX` | Bu sefer Codex'i atla (sadece Q izniyle) |
| `FORCE PASS` | Codex verdict'i override et (sadece Q izniyle) |
| `ROLLBACK` | Son değişiklikleri geri al |

**KRITIK:** `SKIP CODEX` ve `FORCE PASS` sadece Q'nun açık izniyle kullanılabilir.

======================================================================

## SUMMARY

```
Q bir şey ister
    ↓
DevAgent interview (AskUserQuestion)
    ↓
DevAgent plan yapar → arch/plans/{slug}.json
    ↓
Q: "onay"
    ↓
DevAgent kod yazar
    ↓
Build PASS
    ↓
DevAgent /rev çalıştırır
    ↓
Q Codex'e copy-paste yapar
    ↓
Codex 2 BLOK üretir
    ↓
Q DevAgent'a verdict bildirir
    ↓
PASS → commit → DONE
FAIL → fix → /rev (max 3 iter)
```
