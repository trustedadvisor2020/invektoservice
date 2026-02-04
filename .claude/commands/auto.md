---
description: Automated planning - create implementation plan, follow contracts, build, automatic review, commit
---

# Auto Workflow v3.1

## Overview

> **🔄 PERSIST AFTER COMPACT:** Bu workflow session sıfırlansa bile aktif kalır.

**Bu workflow HER kod değişikliği için otomatik uygulanır.**
`/auto` yazmaya gerek yok - Q ne istediğini söyler, workflow otomatik başlar.

### After Compact (Session Sıfırlanırsa)
Compact komutundan sonra auto workflow **DURMAZ**:
- Pending değişiklikler `/rev + Codex review` gerektirir
- Build PASS zorunluluğu devam eder
- Interview gate yeni tasklar için uygulanır

======================================================================

## Q'NUN YAPACAĞI

```
┌─────────────────────────────────────────────────────┐
│                Q'NUN ROLÜ                            │
├─────────────────────────────────────────────────────┤
│                                                      │
│  1. Task söyle                                       │
│  2. Interview sorularını cevapla                    │
│  3. Plan'ı onayla                                    │
│  4. COPY-PASTE köprüsü yap (DevAgent ↔ Codex)      │
│  5. Sonucu gör (DONE veya escalation)              │
│                                                      │
│  Codex trigger: Q'nun manuel copy-paste'i           │
│                                                      │
└─────────────────────────────────────────────────────┘
```

======================================================================

## 🚀 SESSION BOOTSTRAP (HER SESSION BAŞINDA)

**Bu workflow her session başında OTOMATİK aktif olur.**

```
Session Başladı
    ↓
[1] KRİTİK DOSYALARI OKU:
    ├── arch/session-memory.md      → Son durumu anla
    ├── arch/active-work.md         → Devam eden işler
    ├── arch/lessons-learned.md     → Tekrarlanan hatalar
    └── INVEKTO_BASE.prompt.md      → Global kurallar
    ↓
[2] Q ne istedi? → Interview başlat (STEP 0)
    ↓
[3] Normal auto workflow devam eder
```

======================================================================

## STEP 0: Q INTERVIEW (MANDATORY)

**KOD YAZMADAN ÖNCE BU ADIM ZORUNLU!**

Q bir şey istediğinde, **ÖNCE interview yap, SONRA risk belirle.**

### Temel Kural

```
┌─────────────────────────────────────────────────────┐
│           GRİ NOKTA KALMAYANA KADAR SOR              │
├─────────────────────────────────────────────────────┤
│                                                      │
│  Konu ne kadar açık görünürse görünsün,             │
│  interview TÜM gri noktaları çözene kadar devam eder│
│                                                      │
│  "Açık görünüyor" ≠ "Soru sormaya gerek yok"        │
│  Her varsayım = potansiyel yanlış yön               │
│                                                      │
└─────────────────────────────────────────────────────┘
```

### 🔴 ŞEYTANIN AVUKATLIĞI (ZORUNLU - PP-006)

```
┌─────────────────────────────────────────────────────┐
│         Q'YU CHALLENGE ET, UYANIDIR!                 │
├─────────────────────────────────────────────────────┤
│                                                      │
│  Q kısa cevap verse bile pasif kalma!               │
│                                                      │
│  ✅ Yapılması gerekenler:                           │
│  ├── Alternatif yaklaşımlar sun                     │
│  ├── Edge case'leri sor ("Ya X olursa?")           │
│  ├── Potansiyel riskleri belirt                     │
│  ├── Trade-off'ları tartış                          │
│  └── Q'nun varsayımlarını sorgula                   │
│                                                      │
│  ❌ Yapılmaması gerekenler:                         │
│  ├── Q'nun ilk cevabını kabul edip geçme           │
│  ├── "Anlaşıldı" deyip koda dalma                  │
│  └── Soru sormaktan çekinme                         │
│                                                      │
│  🎯 AMAÇ: Q'yu düşündürmek, daha iyi karar vermesini│
│           sağlamak - "evet efendim" yapmak DEĞİL!   │
│                                                      │
└─────────────────────────────────────────────────────┘
```

### Interview Akışı

```
1. AskUserQuestion tool ile soru sor (max 4 soru/batch)
2. Q cevaplar
3. Cevaptan yeni gri nokta çıktı mı? → AskUserQuestion ile sor
4. Tüm gri noktalar çözüldü mü? → Hayır ise 3'e dön
5. Özet yap, "Onay?" de
6. Q onaylarsa → STEP 1'e geç
```

**ZORUNLU:** Interview soruları AskUserQuestion tool ile sorulmalı.

======================================================================

## STEP 1: PRE-FLIGHT CHECK (Risk-Based)

### Mandatory Reads (HER ZAMAN)

**Kod yazmadan ÖNCE bu dosyaları oku:**

```
ZORUNLU:
- arch/session-memory.md
- arch/active-work.md
- arch/lessons-learned.md
- arch/contracts/
- arch/errors.md
- INVEKTO_BASE.prompt.md
```

### Risk Classification

| Task Type | Risk | Pre-flight |
|-----------|------|------------|
| Typo fix, comment, log msg | **LOW** | Skip all |
| UI-only (layout, text, no logic) | **LOW** | Skip all |
| UI display logic (single file) | **LOW** | Build only |
| Business logic, queries, routing | **MEDIUM** | Scope files |
| Multi-file changes | **MEDIUM** | Scope files only |
| DB schema/query change | **HIGH** | Full check |
| Auth/security touch | **CRITICAL** | Full + Q approval |
| New microservice | **HIGH** | Full check + architecture review |

======================================================================

## PHASE 1: PLAN (DevAgent Mode)

1. Generate slug: `YYYYMMDD-feature-name`
2. Analyze codebase (patterns, conventions)
3. Determine scope from `arch/contracts/*`
4. Identify risks, affected modules/services
5. Create JSON plan: `arch/plans/{slug}.json`
6. **Verification Questions yazılır** (TÜM risk seviyeleri için zorunlu)
7. **🎯 AHA MOMENTS YAZILIR** (5 öneri ZORUNLU)
8. Show brief to Q, ask "Onay?"

### AHA Checklist (5 öneri ZORUNLU)

```
┌────────────────────────────────────────────────────────────┐
│ 🎯 AHA MOMENTS (5 öneri - TÜM RİSK SEVİYELERİ):            │
│                                                             │
│ Her öneri şu formatta olmalı:                              │
│ ├── Kategori: UX | SPEED | RELIABILITY | SALES | SUPPORT   │
│ ├── User Pain: Somut kullanıcı problemi                    │
│ ├── Öneri: Ne yapılacak (1 cümle)                          │
│ └── Aha Moment: Kullanıcı ne zaman "vay!" diyecek          │
└────────────────────────────────────────────────────────────┘
```

### Approval Gate (HARD STOP)

```
❌ DEV'E GEÇİLMEZ eğer:
- Q "onay" / "ok" / "evet" / "devam" DEMEDİYSE

✅ SADECE Q açıkça onay verirse Phase 2'ye geç
```

======================================================================

## PHASE 2: DEV (DevAgent Mode)

1. Implement code (max 3 steps per batch)
2. **BUILD immediately** after each file edit
3. If build fails → fix immediately
4. Build PASS → Phase 3'e geç

### Build Pass ≠ Done

```
❌ BUILD PASS sonrası DONE DENİLEMEZ!
✅ BUILD PASS → ZORUNLU Phase 3 (Review)
```

======================================================================

## PHASE 3: REVIEW (Copy-Paste)

### Risk-Based Trigger

```
Build PASS sonrası:
    ↓
┌─────────────────────────────────────┐
│ Risk | Sonraki Adım                 │
├─────────────────────────────────────┤
│ LOW  | /rev → Q copy-paste → Codex  │
│ MEDIUM | /rev → Q copy-paste → Codex│
│ HIGH | /rev → Q copy-paste → Codex  │
│ CRITICAL | /rev + Q onay bekle      │
└─────────────────────────────────────┘
```

**NOT:** Tüm risk seviyelerinde Codex review ZORUNLU.

### Copy-Paste Review

Build PASS sonrası:

1. DevAgent `/rev` çalıştırır
2. JSON plan dosyası güncellenir
3. Diff dosyası yazılır: `arch/plans/diffs/{slug}.diff`

**Codex Prompt (Q'ya gösterilir):**

```
{slug-name} ---
# CODEX REVIEW REQUEST
Plan: arch/plans/{slug}.json
{RISK} :{iteration}
{plan.summary}

## Verification Questions
- [ ] {Q1.category}: {Q1.question}
- [ ] {Q2.category}: {Q2.question}
- [ ] {Q3.category}: {Q3.question}
```

5. Q Codex penceresine yapıştırır
6. Codex 2 BLOK review üretir
7. Q Codex output'unu DevAgent'a bildirir
8. DevAgent `/rev verdict PASS|FAIL` ile JSON'ı günceller
9. PASS → commit | FAIL → fix (max 3 iter)

**HARD RULE:** /rev çalıştırıldıktan sonra Codex prompt'u Q'ya gösterilmeden ASLA commit yapılamaz!

======================================================================

## PHASE 4: FIX-RUN (FAIL Sonrası)

### Iteration Limits

| Risk | Max Iter | Escalation |
|------|----------|------------|
| LOW | 3 | Q'ya bilgi |
| MEDIUM | 3 | Q'ya escalate |
| HIGH+ | 3 | Q onayı gerekli |

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

### Escalation Kategorileri

| Kategori | Ne Demek |
|----------|----------|
| **DECISION_CONFLICT** | Bug değil, tasarım kararı gerekiyor |
| **TOOL_LIMITATION** | Araç/framework limiti |
| **PLAN_ASSUMPTION_WRONG** | Plan varsayımı yanlış çıktı |
| **SCOPE_INSUFFICIENT** | Plan scope'u yetersiz |
| **ARCHITECTURE_CONFLICT** | Mevcut mimari ile çelişki |

======================================================================

## PHASE 5: DONE (PASS Sonrası)

### Done Gate

```
❌ DONE'a GEÇİLEMEZ eğer:
- Plan JSON oluşturulmadıysa
- Codex review yapılmadıysa
- Codex verdict FAIL ise

✅ DONE sadece:
- Build PASS + Codex PASS → commit
```

### After PASS:

1. Generate commit message (conventional commit)
2. Commit to work branch
3. Merge to master
4. Update:
   - `arch/session-memory.md`
   - `arch/active-work.md`
5. JSON plan: `status`: "DONE"
6. Inform Q: "DONE - {slug}"
7. `/learn` onerisi: "Oturumdan ogrenilecek bir sey var mi? /learn"

======================================================================

## Q OVERRIDE

Q her zaman müdahale edebilir:

| Q Komutu | Etki |
|----------|------|
| `STOP` | Tüm işlemi durdur |
| `SKIP CODEX` | Bu sefer Codex'i atla (sadece Q izniyle) |
| `FORCE PASS` | Codex verdict'i override et (sadece Q izniyle) |
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
DevAgent /rev verdict PASS|FAIL
    ↓
PASS → commit → DONE
FAIL → fix → /rev (max 3 iter)
```
