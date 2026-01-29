---
description: Automated planning - create implementation plan, follow contracts, build, automatic review, commit
---

# Auto Workflow v1.0 (INVEKTO)

> **Agents:** `agents.md` (v1.0 - Copy-Paste)

## Overview

> **PERSIST AFTER COMPACT:** Bu workflow session sıfırlansa bile aktif kalır.

**Bu workflow HER kod değişikliği için otomatik uygulanır.**
`/auto` yazmaya gerek yok - Q ne istediğini söyler, workflow otomatik başlar.

======================================================================

## Q'NUN YAPACAĞI

```
┌─────────────────────────────────────────────────────────┐
│                Q'NUN ROLÜ                                │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  1. Task söyle                                           │
│  2. Interview sorularını cevapla                        │
│  3. Plan'ı onayla                                        │
│  4. COPY-PASTE köprüsü yap (DevAgent ↔ Codex)          │
│  5. Sonucu gör (DONE veya escalation)                  │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

======================================================================

## SESSION BOOTSTRAP (HER SESSION BAŞINDA)

**Bu workflow her session başında OTOMATİK aktif olur.**

```
Session Başladı
    ↓
[1] KRİTİK DOSYALARI OKU (Read tool ile):
    ├── arch/session-memory.md      → Son durumu anla
    ├── arch/active-work.md         → Devam eden işler
    └── arch/lessons-learned.md     → Tekrarlanan hatalar
    ↓
[2] Q ne istedi? → Interview başlat (STEP 0)
    ↓
[3] Normal auto workflow devam eder
```

======================================================================

## STEP 0: Q INTERVIEW (MANDATORY)

**KOD YAZMADAN ÖNCE BU ADIM ZORUNLU!**

### Interview Akışı

```
1. AskUserQuestion tool ile soru sor (max 4 soru/batch)
2. Q cevaplar
3. Cevaptan yeni gri nokta çıktı mı? → AskUserQuestion ile sor
4. Tüm gri noktalar çözüldü mü? → Hayır ise 3'e dön
5. Özet yap, "Onay?" de
6. Q onaylarsa → STEP 1'e geç
```

### Interview Gate

```
❌ İLERLENMEZ eğer:
- Gri nokta kaldıysa
- Q cevap vermediyse
- Varsayım yapılması gerekiyorsa

✅ Q "skip interview" derse → direkt STEP 1'e geç
```

======================================================================

## STEP 1: PRE-FLIGHT CHECK

### Mandatory Reads (HER ZAMAN)

```
ZORUNLU:
- arch/session-memory.md      → Son durumu anla
- arch/active-work.md         → Devam eden işler
- arch/lessons-learned.md     → Tekrarlanan hatalar
- plans/00_master_implementation_plan.md → İlgili phase
- arch/errors.md              → Error codes (gerekirse)
```

### Risk Classification

| Task Type | Risk |
|-----------|------|
| Typo fix, comment, log msg | **LOW** |
| UI-only (layout, text, no logic) | **LOW** |
| Business logic, queries | **MEDIUM** |
| Multi-file changes | **MEDIUM** |
| DB schema/query change | **HIGH** |
| Auth/security touch | **CRITICAL** |

======================================================================

## PHASE 1: PLAN (DevAgent Mode)

1. Generate slug: `YYYYMMDD-feature-name`
2. Analyze codebase (patterns, conventions)
3. Determine scope
4. Identify risks, affected modules
5. Create JSON plan: `arch/plans/{slug}.json`
6. Verification Questions yazılır
7. Show brief to Q, ask "Onay?"
8. Q onay verince → Work branch oluştur

### JSON Plan Dosyası

Plan dosyası şu alanları içermeli:
- `slug`, `summary`, `risk`
- `allowed_files[]`
- `verification_questions[]`
- `build`, `git_diff`, `verdict`

### Approval Gate (HARD STOP)

```
❌ DEV'E GEÇİLMEZ eğer:
- Q "onay" / "ok" / "evet" / "devam" DEMEDİYSE

✅ SADECE Q açıkça onay verirse Phase 2'ye geç
```

======================================================================

## PHASE 2: DEV (DevAgent Mode)

1. Implement code (max 3 steps per batch)
2. **BUILD immediately** after each file edit:
   - `dotnet build src/Invekto.{Service}/`
3. If build fails → fix immediately
4. Build PASS → Phase 3'e geç

### Build Pass ≠ Done

```
❌ BUILD PASS sonrası DONE DENİLEMEZ!
✅ BUILD PASS → ZORUNLU Phase 3 (Review)
```

======================================================================

## PHASE 3: REVIEW (Copy-Paste)

Build PASS sonrası:

1. DevAgent `/rev` çalıştırır
2. JSON plan dosyası güncellenir
3. Diff dosyası yazılır: `arch/plans/diffs/{slug}.diff`
4. **Q'ya Codex prompt gösterilir**
5. Q Codex penceresine yapıştırır
6. Codex 2 BLOK review üretir
7. Q Codex output'unu DevAgent'a bildirir
8. DevAgent `/rev verdict PASS|FAIL` ile JSON'ı günceller
9. PASS → commit | FAIL → fix (max 3 iter)

### Codex Prompt Format

```
{slug-name} ---
# CODEX REVIEW REQUEST
Plan: arch/plans/{slug}.json
{RISK} :{iteration}
{plan.summary}

## Verification Questions
- [ ] {Q1}
- [ ] {Q2}
...
```

**HARD RULE:** /rev sonrası Codex prompt'u Q'ya gösterilmeden ASLA commit yapılamaz!

======================================================================

## PHASE 4: FIX-RUN (FAIL Sonrası)

### Fix Döngüsü

```
FAIL verdict
    ↓
DevAgent fix yapar
    ↓
Build çalışır
    ↓ (PASS)
DevAgent /rev çalıştırır
    ↓
Q copy-paste → Codex
    ↓
PASS → commit | FAIL → tekrar fix (iter++)
```

### Escalation (3 iter sonrası)

| Kategori | Ne Demek |
|----------|----------|
| **DECISION_CONFLICT** | Tasarım kararı gerekiyor |
| **TOOL_LIMITATION** | Araç/framework limiti |
| **PLAN_ASSUMPTION_WRONG** | Plan varsayımı yanlış |
| **SCOPE_INSUFFICIENT** | Scope yetersiz |
| **ARCHITECTURE_CONFLICT** | Mimari çelişki |

======================================================================

## PHASE 5: DONE (PASS Sonrası)

### After PASS:

1. Generate commit message (conventional commit)
2. Commit to work branch
3. Auto-merge to master
4. Update:
   - `arch/session-memory.md`
   - `arch/active-work.md`
5. JSON plan: `status`: "DONE"
6. Inform Q: "DONE - {slug}"

======================================================================

## Q OVERRIDE

| Q Komutu | Etki |
|----------|------|
| `STOP` | Tüm işlemi durdur |
| `SKIP CODEX` | Bu sefer Codex'i atla |
| `FORCE PASS` | Codex verdict'i override et |
| `ROLLBACK` | Son değişiklikleri geri al |

======================================================================

## SUMMARY

```
Q bir şey ister
    ↓
DevAgent AskUserQuestion ile interview
    ↓
DevAgent plan JSON oluşturur
    ↓
Q: "onay"
    ↓
DevAgent kod yazar
    ↓
Build PASS
    ↓
DevAgent /rev
    ↓
Q'ya: "Codex review: arch/plans/{slug}.json"
    ↓
Q copy-paste → AYRI Codex penceresi
    ↓
Codex 2 BLOK üretir
    ↓
Q verdict bildirir
    ↓
PASS → commit → DONE
FAIL → fix → /rev (max 3 iter)
```
